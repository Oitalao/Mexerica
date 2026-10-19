using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mexerica;

static class Paths
{
    public static readonly string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mexerica");

    /// Caminho de saída ao lado do original, sem sobrescrever nada.
    public static string Out(string src, string ext, string? suffix = null)
    {
        var dir = Dir(src);
        var b = Path.GetFileNameWithoutExtension(src.TrimEnd('\\'));
        if (b.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)) b = b[..^4];
        if (suffix != null) b += $" ({suffix})";
        lock (reserved)
        {
            var p = Path.Combine(dir, $"{b}.{ext}");
            for (int i = 2; Taken(p); i++) p = Path.Combine(dir, $"{b} {i}.{ext}");
            reserved.Add(p);
            return p;
        }
    }

    // Nomes já entregues a trabalhos em andamento: com vários arquivos ao mesmo tempo,
    // dois deles não podem escolher o mesmo nome antes de o primeiro gravar.
    static readonly HashSet<string> reserved = new(StringComparer.OrdinalIgnoreCase);
    static bool Taken(string p) => File.Exists(p) || Directory.Exists(p) || reserved.Contains(p);

    /// Esquece os nomes reservados (quando nenhum trabalho está rodando).
    public static void ReleaseNames() { lock (reserved) reserved.Clear(); }

    public static string OutDir(string src, string? suffix = null)
    {
        var dir = Dir(src);
        var b = Path.GetFileNameWithoutExtension(src.TrimEnd('\\'));
        if (b.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)) b = b[..^4];
        if (suffix != null) b += $" ({suffix})";
        lock (reserved)
        {
            var p = Path.Combine(dir, b);
            for (int i = 2; Taken(p); i++) p = Path.Combine(dir, $"{b} {i}");
            reserved.Add(p);
            return p;
        }
    }

    /// Pasta de saída: a escolhida nas configurações ou a do original.
    static string Dir(string src)
    {
        var o = Config.Now.OutputDir;
        if (o.Length > 0 && Directory.Exists(o) && Writable(o)) return o;
        var d = Path.GetDirectoryName(src.TrimEnd('\\'))!;
        if (Writable(d)) return d;
        // Pasta só de leitura (CD, compartilhamento, Arquivos de Programas): vai para Downloads.
        var dl = Downloads();
        Directory.CreateDirectory(dl);
        return dl;
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int SHGetKnownFolderPath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out string path);

    /// Pasta Downloads de verdade (pode ter sido mudada de lugar pela pessoa).
    public static string Downloads()
    {
        try { if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out var p) == 0 && Directory.Exists(p)) return p; } catch { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    static readonly Dictionary<string, (bool ok, DateTime when)> writable = new(StringComparer.OrdinalIgnoreCase);

    static bool Writable(string dir)
    {
        lock (writable)
            if (writable.TryGetValue(dir, out var w) && DateTime.Now - w.when < TimeSpan.FromMinutes(2)) return w.ok;
        bool ok;
        try
        {
            var probe = Path.Combine(dir, ".mexerica-" + Guid.NewGuid().ToString("N")[..8]);
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            ok = true;
        }
        catch { ok = false; }
        lock (writable) writable[dir] = (ok, DateTime.Now);
        return ok;
    }

    public static string Temp()
    {
        var d = Path.Combine(Path.GetTempPath(), "mexerica-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(d);
        return d;
    }

    public static void TryDelete(string p)
    {
        try { if (Directory.Exists(p)) Directory.Delete(p, true); else if (File.Exists(p)) File.Delete(p); } catch { }
    }

    public static string? FindTool(string exe)
    {
        var local = Path.Combine(AppContext.BaseDirectory, "tools", exe);
        if (File.Exists(local)) return local;
        exe = Path.GetFileName(exe);
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try { var p = Path.Combine(d.Trim(), exe); if (File.Exists(p)) return p; } catch { }
        }
        return null;
    }
}

static class Log
{
    static readonly object gate = new();
    public static void Write(string msg)
    {
        try
        {
            lock (gate)
            {
                Directory.CreateDirectory(Paths.Data);
                var f = Path.Combine(Paths.Data, "log.txt");
                if (File.Exists(f) && new FileInfo(f).Length > 1_000_000) File.Delete(f);
                File.AppendAllText(f, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}\r\n");
            }
        }
        catch { }
    }
}

static class Proc
{
    /// Roda um programa sem janela e devolve a saída de erro; falha com as últimas linhas do erro.
    /// Respeita o cancelamento do trabalho atual (mata o processo) e repassa cada linha da saída padrão.
    public static async Task<string> Run(string exe, IEnumerable<string> args, int timeoutSec = 7200, string? cwd = null, Action<string>? onOut = null)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardError = true, RedirectStandardOutput = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        if (cwd != null) psi.WorkingDirectory = cwd;
        foreach (var a in args) psi.ArgumentList.Add(a);
        var token = Job.Token;
        token.ThrowIfCancellationRequested();
        using var p = Process.Start(psi) ?? throw new Exception($"Não consegui abrir {Path.GetFileName(exe)}");
        var err = p.StandardError.ReadToEndAsync();
        var outp = Task.Run(async () =>
        {
            var sb = new System.Text.StringBuilder();
            string? line;
            while ((line = await p.StandardOutput.ReadLineAsync()) != null)
            {
                if (sb.Length < 4_000_000) sb.AppendLine(line);
                try { onOut?.Invoke(line); } catch { }
            }
            return sb.ToString();
        });
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch { }
            if (token.IsCancellationRequested) throw new OperationCanceledException(token);
            throw new Exception("Demorou demais e foi interrompido");
        }
        var e = await err; await outp;
        if (p.ExitCode != 0)
        {
            var lines = e.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).TakeLast(3);
            Log.Write($"{Path.GetFileName(exe)} saiu com {p.ExitCode}: {e}");
            var msg = string.Join(" ", lines);
            throw new Exception(msg.Length > 0 ? msg : $"{Path.GetFileName(exe)} falhou (código {p.ExitCode})");
        }
        return e;
    }

    /// Versão curta e síncrona que devolve a saída padrão (consultas rápidas: ffprobe, exiftool).
    public static string RunOutput(string exe, IEnumerable<string> args, int timeoutSec = 60)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new Exception($"Não consegui abrir {Path.GetFileName(exe)}");
        var o = p.StandardOutput.ReadToEndAsync();
        var e = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeoutSec * 1000)) { try { p.Kill(true); } catch { } throw new Exception("Demorou demais"); }
        return o.Result;
    }
}
