using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Mexerica;

/// Pasta monitorada: tudo o que chegar nela é convertido para Target (o rótulo da fatia, ex.: "PDF", "JPG").
sealed class WatchFolder
{
    public string Folder { get; set; } = "";
    public string Target { get; set; } = "PDF";
    public bool Subfolders { get; set; }
    public bool MoveOriginals { get; set; }
    public bool Enabled { get; set; } = true;
}

/// Vigia uma pasta e converte os arquivos novos, um de cada vez, quando terminam de ser gravados.
sealed class FolderWatcher : IDisposable
{
    readonly WatchFolder w;
    readonly FileSystemWatcher fsw;
    readonly Action<string, string?, string?> report;
    readonly ConcurrentDictionary<string, byte> pending = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> ours = new(StringComparer.OrdinalIgnoreCase);
    readonly SemaphoreSlim one = new(1);
    readonly CancellationTokenSource stop = new();

    static readonly string[] ignore = { ".tmp", ".crdownload", ".part", ".partial", ".download", ".opdownload", ".lnk", ".ini", ".db" };

    /// report(arquivo, resultado ou null, erro ou null)
    public FolderWatcher(WatchFolder w, Action<string, string?, string?> report)
    {
        this.w = w; this.report = report;
        fsw = new FileSystemWatcher(w.Folder) { IncludeSubdirectories = w.Subfolders, NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite, InternalBufferSize = 64 * 1024 };
        fsw.Created += (_, e) => Enqueue(e.FullPath);
        fsw.Renamed += (_, e) => Enqueue(e.FullPath);
        fsw.EnableRaisingEvents = true;
    }

    string OriginalsDir => Path.Combine(w.Folder, "originais");

    void Enqueue(string path)
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith("~$") || name.StartsWith(".") || ignore.Contains(Path.GetExtension(path).ToLowerInvariant())) return;
        if (path.StartsWith(OriginalsDir + "\\", StringComparison.OrdinalIgnoreCase)) return;
        lock (ours) if (ours.Contains(path)) return;
        if (!pending.TryAdd(path, 0)) return;
        _ = Task.Run(() => Process(path));
    }

    async Task Process(string path)
    {
        try
        {
            // Espera o arquivo terminar de chegar: tamanho parado e dá para abrir sozinho.
            long last = -1;
            for (int stable = 0; stable < 3;)
            {
                await Task.Delay(700, stop.Token);
                if (!File.Exists(path)) return;
                long len;
                try { len = new FileInfo(path).Length; using var _ = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None); }
                catch (IOException) { stable = 0; continue; }
                if (len == last && len > 0) stable++; else { stable = 0; last = len; }
            }
            var op = Catalog.For(Mode.Convert, new[] { path }).FirstOrDefault(o => string.Equals(o.Label, w.Target, StringComparison.OrdinalIgnoreCase));
            if (op?.One == null) return; // esse tipo não vira o formato pedido (ou já está nele)
            await one.WaitAsync(stop.Token);
            try
            {
                var job = new Job(); Job.Current = job;
                string? res;
                try { res = await op.One(path, P.None); }
                finally { Job.Current = null; }
                if (res == null) return;
                lock (ours) ours.Add(res);
                if (w.MoveOriginals)
                {
                    Directory.CreateDirectory(OriginalsDir);
                    var dest = Path.Combine(OriginalsDir, Path.GetFileName(path));
                    for (int i = 2; File.Exists(dest); i++) dest = Path.Combine(OriginalsDir, $"{Path.GetFileNameWithoutExtension(path)} {i}{Path.GetExtension(path)}");
                    File.Move(path, dest);
                }
                History.Add($"{op.Label} (pasta monitorada)", new[] { path }, new[] { res });
                report(path, res, null);
            }
            finally { one.Release(); Paths.ReleaseNames(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log.Write($"vigia {path}: {ex}"); report(path, null, App.Friendly(ex)); }
        finally { pending.TryRemove(path, out _); }
    }

    public void Dispose() { stop.Cancel(); fsw.Dispose(); }
}

/// Liga e desliga os vigias conforme as configurações.
static class Watchers
{
    static readonly List<FolderWatcher> running = new();

    public static void Apply()
    {
        lock (running)
        {
            foreach (var r in running) r.Dispose();
            running.Clear();
            foreach (var w in Config.Now.WatchFolders.Where(w => w.Enabled && Directory.Exists(w.Folder)))
            {
                try
                {
                    running.Add(new FolderWatcher(w, (src, res, err) => Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        if (res != null) App.Toast.Show("Pasta monitorada", $"{Path.GetFileName(src)} → {Path.GetFileName(res.TrimEnd('\\'))}", res);
                        else App.Toast.Show("Pasta monitorada: não deu certo", $"{Path.GetFileName(src)}: {err}", error: true);
                    })));
                }
                catch (Exception ex) { Log.Write($"vigia {w.Folder}: {ex.Message}"); }
            }
        }
    }
}

/// Janela para escolher as pastas monitoradas.
sealed class WatchWindow : Window
{
    readonly List<WatchFolder> items;
    readonly StackPanel list = new();

    public static List<WatchFolder>? Ask(Window owner, List<WatchFolder> current)
    {
        var w = new WatchWindow(current) { Owner = owner };
        return w.ShowDialog() == true ? w.items : null;
    }

    internal WatchWindow(List<WatchFolder> current)
    {
        items = current.Select(x => new WatchFolder { Folder = x.Folder, Target = x.Target, Subfolders = x.Subfolders, MoveOriginals = x.MoveOriginals, Enabled = x.Enabled }).ToList();
        Title = "Mexerica: pastas monitoradas";
        Width = 600; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Ui.Dark(this);
        var st = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
        st.Children.Add(new TextBlock
        {
            Text = "Tudo o que chegar numa pasta monitorada é convertido sozinho para o formato escolhido, ao lado do original. Ex.: Downloads → PDF.",
            Foreground = Ui.Dim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12),
        });
        st.Children.Add(list);
        var add = Ui.Button("Adicionar pasta…", false);
        add.HorizontalAlignment = HorizontalAlignment.Left;
        add.Click += (_, _) =>
        {
            var d = new Microsoft.Win32.OpenFolderDialog { Title = Tr.T("Pasta para monitorar") };
            if (d.ShowDialog(this) != true) return;
            items.Add(new WatchFolder { Folder = d.FolderName });
            Fill();
        };
        st.Children.Add(add);
        var ok = Ui.Button("Salvar", true);
        var cancel = Ui.Button("Cancelar", false);
        ok.Click += (_, _) => DialogResult = true;
        cancel.Click += (_, _) => DialogResult = false;
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        bar.Children.Add(cancel); bar.Children.Add(ok);
        st.Children.Add(bar);
        Content = st;
        Fill();
    }

    static readonly string[] formats = { "PDF", "JPG", "PNG", "WEBP", "AVIF", "HEIC", "MP4", "MP3", "M4A", "DOCX", "TXT", "XLSX", "CSV", "EPUB", "ZIP" };

    void Fill()
    {
        list.Children.Clear();
        if (items.Count == 0) list.Children.Add(new TextBlock { Text = "Nenhuma pasta ainda.", Foreground = Ui.Dim, Margin = new Thickness(0, 0, 0, 10) });
        foreach (var w in items)
        {
            var box = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var head = new DockPanel();
            var rm = Ui.Button("Remover", false);
            rm.Click += (_, _) => { items.Remove(w); Fill(); };
            DockPanel.SetDock(rm, Dock.Right);
            head.Children.Add(rm);
            head.Children.Add(new TextBlock { Text = w.Folder, Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            box.Children.Add(head);
            var fmts = formats.Contains(w.Target) ? formats : formats.Append(w.Target).ToArray();
            box.Children.Add(Ui.Row("Converter para", Ui.Combo(fmts.Select(f => (f, f)), w.Target, v => w.Target = v)));
            box.Children.Add(Ui.Check("Ligada", w.Enabled, v => w.Enabled = v));
            box.Children.Add(Ui.Check("Incluir subpastas", w.Subfolders, v => w.Subfolders = v));
            box.Children.Add(Ui.Check("Guardar os originais numa subpasta \"originais\"", w.MoveOriginals, v => w.MoveOriginals = v));
            list.Children.Add(box);
        }
    }
}
