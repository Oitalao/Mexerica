using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mexerica;

/// Linha de comando: `mexerica converter fotos\*.heic --para jpg`. O instalador põe um mexerica.cmd no PATH,
/// que espera o programa terminar e devolve o código de saída (0 = tudo certo).
static class Cli
{
    public static readonly string[] Verbs = { "converter", "opcoes", "opções", "ferramenta", "vigiar", "ajuda", "versao", "versão", "--help", "-h", "/?" };

    [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int n);
    static TextWriter Out = TextWriter.Null;

    public static int Run(string[] args)
    {
        var h = GetStdHandle(-11);
        if (h != IntPtr.Zero && h != new IntPtr(-1))
            Out = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true };
        else if (AttachConsole(-1))
        {
            var o = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true };
            Out = o;
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Out.WriteLine();
        }
        try
        {
            // A linha de comando enxerga todas as opções: esconder ou reordenar fatias vale só para a roda.
            var plain = Config.Now.Clone(); plain.WheelHidden = new(); plain.WheelOrder = new(); Config.Override(plain);
            var verb = args[0].ToLowerInvariant();
            var rest = args.Skip(1).ToList();
            return verb switch
            {
                "converter" => Convert(rest),
                "opcoes" or "opções" => Options(rest),
                "ferramenta" => Tool(rest),
                "vigiar" => Watch(rest),
                "versao" or "versão" => Say($"Mexerica {Version}"),
                _ => Help(),
            };
        }
        catch (Exception ex) { Out.WriteLine("Erro: " + App.Friendly(ex)); return 2; }
    }

    static string Version => typeof(Cli).Assembly.GetName().Version?.ToString(3) ?? "";

    static int Say(string s) { Out.WriteLine(s); return 0; }

    static int Help() => Say(@"Mexerica pela linha de comando

  mexerica converter <arquivos...> --para <formato> [-o pasta] [--qualidade 1-100]
      Converte cada arquivo (curingas valem: *.heic). Ex.: mexerica converter *.png --para webp --qualidade 80
  mexerica opcoes <arquivos...>
      Lista as conversões e ferramentas disponíveis para esses arquivos.
  mexerica ferramenta ""<nome>"" <arquivos...> [--p chave=valor ...]
      Aplica uma ferramenta da roda. Ex.: mexerica ferramenta Comprimir foto.jpg --p modo=pct --p pct=50
  mexerica vigiar <pasta> --para <formato> [--subpastas] [--mover-originais]
      Converte tudo o que chegar na pasta até você apertar Ctrl+C.
  mexerica versao

Código de saída: 0 = tudo certo, 1 = algum arquivo falhou, 2 = erro nos parâmetros.");

    /// Separa arquivos (expandindo curingas) de opções --nome valor.
    static (List<string> files, Dictionary<string, string> opts, P p) Parse(List<string> a)
    {
        var files = new List<string>();
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var p = new P();
        for (int i = 0; i < a.Count; i++)
        {
            var x = a[i];
            if (x == "--p" && i + 1 < a.Count) { var kv = a[++i].Split('=', 2); p[kv[0]] = kv.Length > 1 ? kv[1] : ""; }
            else if (x is "--subpastas" or "--mover-originais") opts[x.TrimStart('-')] = "1";
            else if (x.StartsWith("-") && i + 1 < a.Count) opts[x.TrimStart('-')] = a[++i];
            else files.AddRange(Expand(x));
        }
        return (files, opts, p);
    }

    static IEnumerable<string> Expand(string pattern)
    {
        if (pattern.IndexOfAny(new[] { '*', '?' }) < 0) { yield return Path.GetFullPath(pattern); yield break; }
        var dir = Path.GetDirectoryName(pattern);
        dir = string.IsNullOrEmpty(dir) ? Directory.GetCurrentDirectory() : Path.GetFullPath(dir);
        foreach (var f in Directory.EnumerateFileSystemEntries(dir, Path.GetFileName(pattern)).OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase))
            yield return f;
    }

    static void ApplyOptions(Dictionary<string, string> opts)
    {
        var c = Config.Now.Clone();
        if (opts.TryGetValue("o", out var o) || opts.TryGetValue("saida", out o) || opts.TryGetValue("saída", out o))
        {
            o = Path.GetFullPath(o);
            Directory.CreateDirectory(o);
            c.OutputDir = o;
        }
        if (opts.TryGetValue("qualidade", out var q) && int.TryParse(q, out var qi)) c.ImageQuality = Math.Clamp(qi, 1, 100);
        Config.Override(c);
    }

    static Op? Find(Mode mode, string[] files, string label) =>
        Catalog.For(mode, files).FirstOrDefault(op => string.Equals(op.Label, label, StringComparison.OrdinalIgnoreCase))
        ?? Catalog.For(mode, files).FirstOrDefault(op => string.Equals(op.Label.Replace(" ", ""), label.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

    static int Convert(List<string> a)
    {
        var (files, opts, p) = Parse(a);
        if (files.Count == 0 || !opts.TryGetValue("para", out var to)) { Help(); return 2; }
        ApplyOptions(opts);
        int fails = 0;
        // Arquivos de tipos diferentes: cada um com a sua própria roda.
        foreach (var f in files)
        {
            var op = Find(Mode.Convert, new[] { f }, to.TrimStart('.'));
            if (op?.One == null)
            {
                bool same = string.Equals(Path.GetExtension(f).TrimStart('.'), to.TrimStart('.'), StringComparison.OrdinalIgnoreCase);
                Out.WriteLine(same ? $"– {Path.GetFileName(f)}: já está em {to.ToUpperInvariant()}" : $"✗ {Path.GetFileName(f)}: não converte para {to.ToUpperInvariant()}");
                if (!same) fails++;
                continue;
            }
            fails += RunOne(op, new[] { f }, p);
        }
        return fails > 0 ? 1 : 0;
    }

    static int Tool(List<string> a)
    {
        if (a.Count < 2) { Help(); return 2; }
        var name = a[0];
        var (files, opts, p) = Parse(a.Skip(1).ToList());
        ApplyOptions(opts);
        var op = Find(Mode.Tools, files.ToArray(), name);
        if (op == null || op.Ui != null) { Out.WriteLine($"A ferramenta “{name}” não vale para esses arquivos. Veja: mexerica opcoes <arquivos>"); return 2; }
        return RunOne(op, files.ToArray(), p) > 0 ? 1 : 0;
    }

    static int Options(List<string> a)
    {
        var (files, _, _) = Parse(a);
        if (files.Count == 0) { Help(); return 2; }
        var fs = files.ToArray();
        Out.WriteLine("Converter para: " + string.Join(", ", Catalog.For(Mode.Convert, fs).Where(o => o.Ui == null).Select(o => o.Label)));
        Out.WriteLine("Ferramentas:    " + string.Join(", ", Catalog.For(Mode.Tools, fs).Where(o => o.Ui == null).Select(o => o.Label)));
        return 0;
    }

    /// Roda uma operação sem tela, mostrando cada resultado. Devolve o número de falhas.
    static int RunOne(Op op, string[] files, P p)
    {
        var job = new Job(); Job.Current = job;
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; job.Cts.Cancel(); };
        var outputs = new List<string>();
        var errors = new List<string>();
        try
        {
            if (op.All != null) { var r = op.All(files, p).GetAwaiter().GetResult(); if (r != null) outputs.Add(r); }
            else App.EachPublic(op, files, p, job, outputs, errors).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) { Out.WriteLine("Cancelado."); return 1; }
        catch (Exception ex) { errors.Add(App.Friendly(ex)); }
        foreach (var o in outputs) Out.WriteLine("✓ " + o);
        foreach (var e in errors) Out.WriteLine("✗ " + e);
        if (outputs.Count > 0) History.Add(op.Label, files, outputs);
        return errors.Count;
    }

    static int Watch(List<string> a)
    {
        var (files, opts, _) = Parse(a);
        if (files.Count != 1 || !Directory.Exists(files[0]) || !opts.TryGetValue("para", out var to)) { Help(); return 2; }
        var w = new WatchFolder { Folder = files[0], Target = to.TrimStart('.').ToUpperInvariant(), Subfolders = opts.ContainsKey("subpastas"), MoveOriginals = opts.ContainsKey("mover-originais") };
        using var watcher = new FolderWatcher(w, (src, res, err) =>
        {
            if (res != null) Out.WriteLine($"✓ {Path.GetFileName(src)} → {res}");
            else Out.WriteLine($"✗ {Path.GetFileName(src)}: {err}");
        });
        Out.WriteLine($"Vigiando {w.Folder} → {w.Target}. Ctrl+C para parar.");
        var stop = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait();
        return 0;
    }
}
