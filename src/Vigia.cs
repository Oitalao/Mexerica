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

/// Pasta monitorada: tudo o que chegar nela é convertido para Target (o rótulo da fatia, ex.: "PDF", "JPG"),
/// e/ou comprimido quando passa de MinMb. Action: "converter", "comprimir" ou "ambos" (converte e comprime o resultado).
sealed class WatchFolder
{
    public string Folder { get; set; } = "";
    public string Action { get; set; } = "converter";
    public string Target { get; set; } = "PDF";
    public double MinMb { get; set; } = 4;
    /// Tamanho máximo desejado; 0 = compressão média.
    public double TargetMb { get; set; }
    public bool Subfolders { get; set; }
    public bool MoveOriginals { get; set; }
    public bool Enabled { get; set; } = true;

    public WatchFolder Copy() => (WatchFolder)MemberwiseClone();
    [System.Text.Json.Serialization.JsonIgnore] public bool Exists => Folder.Length > 0 && Directory.Exists(Folder);
}

/// Vigia uma pasta e converte/comprime os arquivos novos, um de cada vez, quando terminam de ser gravados.
sealed class FolderWatcher : IDisposable
{
    readonly WatchFolder w;
    readonly FileSystemWatcher fsw;
    readonly Action<string, string?, string?> report;
    readonly SemaphoreSlim one = new(1);
    readonly CancellationTokenSource stop = new();

    // Compartilhados entre os vigias: ao salvar as configurações os vigias são recriados,
    // e o novo não pode pegar um arquivo que o antigo ainda está fazendo ou acabou de gravar.
    static readonly ConcurrentDictionary<string, byte> pending = new(StringComparer.OrdinalIgnoreCase);
    /// Arquivos que nós mesmos gravamos: até quando ignorá-los e qual versão (tamanho e data; -1 = ainda não existe).
    /// Se outro arquivo ocupar o mesmo nome depois, ele deixa de ser considerado nosso.
    static readonly Dictionary<string, (DateTime until, long len, DateTime time)> ours = new(StringComparer.OrdinalIgnoreCase);

    static readonly string[] ignore = { ".tmp", ".crdownload", ".part", ".partial", ".download", ".opdownload", ".lnk", ".ini", ".db" };
    /// Quanto esperar um arquivo que não para de mudar ou continua aberto por outro programa.
    static readonly TimeSpan giveUp = TimeSpan.FromMinutes(30);

    /// report(arquivo, resultado ou null, erro ou null); com resultado e texto, o texto é um detalhe para o aviso.
    public FolderWatcher(WatchFolder w, Action<string, string?, string?> report)
    {
        this.w = w; this.report = report;
        fsw = new FileSystemWatcher(w.Folder) { IncludeSubdirectories = w.Subfolders, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite, InternalBufferSize = 64 * 1024 };
        fsw.Created += (_, e) => Enqueue(e.FullPath, false);
        fsw.Renamed += (_, e) => Enqueue(e.FullPath, false);
        // Pasta apagada, recriada ou fila de avisos do Windows estourada: o vigia é refeito e confere a pasta inteira.
        fsw.Error += (_, e) =>
        {
            Log.Write($"vigia {w.Folder}: {e.GetException().Message}");
            Broken = true;
            Watchers.Recheck();
        };
        fsw.EnableRaisingEvents = true;
    }

    public bool Broken { get; private set; }

    string OriginalsDir => Path.Combine(w.Folder, "originais");
    bool InOriginals(string path) =>
        path.Equals(OriginalsDir, StringComparison.OrdinalIgnoreCase) || path.StartsWith(OriginalsDir + "\\", StringComparison.OrdinalIgnoreCase);

    /// scan = arquivo que já estava lá (ou chegou dentro de uma pasta): não espera terminar de gravar e não refaz o que já foi feito.
    void Enqueue(string path, bool scan)
    {
        if (stop.IsCancellationRequested || InOriginals(path)) return;
        var name = Path.GetFileName(path);
        if (name.StartsWith("~$") || name.StartsWith(".")) return;
        if (Directory.Exists(path))
        {
            // Pasta inteira chegando: os arquivos de dentro não geram aviso próprio.
            if (w.Subfolders) _ = Task.Run(() => ScanDir(path));
            return;
        }
        if (ignore.Contains(Path.GetExtension(path).ToLowerInvariant())) return;
        if (Ours(path)) return;
        if (!pending.TryAdd(path, 0)) return;
        _ = Task.Run(() => Process(path, scan));
    }

    static (long, DateTime) Stamp(string path)
    {
        try { var f = new FileInfo(path); return f.Exists ? (f.Length, f.LastWriteTimeUtc) : (-1, default); }
        catch { return (-1, default); }
    }

    static bool Ours(string path)
    {
        lock (ours)
        {
            if (!ours.TryGetValue(path, out var m) || m.until <= DateTime.Now) return false;
            if (m.len < 0) return true;
            var (len, time) = Stamp(path);
            return len < 0 || (len == m.len && time == m.time);
        }
    }

    static void Mark(string path, TimeSpan for_)
    {
        var (len, time) = Stamp(path);
        lock (ours) ours[path] = (for_ == TimeSpan.MaxValue ? DateTime.MaxValue : DateTime.Now + for_, len, time);
    }

    string MoveToOriginals(string path)
    {
        Directory.CreateDirectory(OriginalsDir);
        var dest = Free(Path.Combine(OriginalsDir, Path.GetFileName(path)));
        File.Move(path, dest);
        return dest;
    }

    /// O próprio caminho, ou o mesmo nome com 2, 3... no fim, se já existir.
    static string Free(string p)
    {
        var dir = Path.GetDirectoryName(p)!; var b = Path.GetFileNameWithoutExtension(p); var ext = Path.GetExtension(p);
        for (int i = 2; File.Exists(p) || Directory.Exists(p); i++) p = Path.Combine(dir, $"{b} {i}{ext}");
        return p;
    }

    static Op? CompressOp(string file) =>
        Catalog.For(Mode.Tools, new[] { file }).FirstOrDefault(o => o.Label == "Comprimir" && o.One != null);

    bool CanCompress(string file) =>
        w.Action != "converter" && File.Exists(file) && new FileInfo(file).Length > w.MinMb * 1024 * 1024 && CompressOp(file) != null;

    /// Já convertido antes (o arquivo com a extensão de destino está ao lado ou na pasta de saída)?
    bool AlreadyConverted(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path) + "." + w.Target.ToLowerInvariant();
        if (File.Exists(Path.Combine(Path.GetDirectoryName(path)!, name))) return true;
        var o = Config.Now.OutputDir;
        return o.Length > 0 && File.Exists(Path.Combine(o, name));
    }

    /// Comprime file. Arquivo gerado por nós é trocado no lugar; o que chegou vai para "originais".
    /// O resultado fica com o nome do arquivo (e a extensão que a compressão der). Devolve null se não deu para reduzir.
    async Task<(string path, string note)?> Compress(string file, bool ours)
    {
        var op = CompressOp(file)!;
        var p = new P();
        if (w.TargetMb > 0) { p["modo"] = "mb"; p["mb"] = w.TargetMb.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        else p["nivel"] = "media";
        string? res;
        try { res = await op.One!(file, p); }
        catch (Exception ex) when (ex.Message.StartsWith("Esse PDF já está bem comprimido"))
        {
            Log.Write($"vigia {file}: já estava comprimido");
            return null;
        }
        if (res == null) return null;
        long before = new FileInfo(file).Length, after = new FileInfo(res).Length;
        // Ganho pequeno não compensa a perda de qualidade (e evita comprimir de novo o que já foi comprimido).
        if (after > before * 0.95) { Paths.TryDelete(res); Log.Write($"vigia {file}: a compressão quase não reduziu"); return null; }
        var final = Path.ChangeExtension(file, Path.GetExtension(res));
        Mark(res, TimeSpan.FromMinutes(1));
        Mark(file, TimeSpan.FromMinutes(1));
        string kept;
        if (ours) { File.Delete(file); kept = file; }
        else kept = MoveToOriginals(file);
        // A extensão mudou (ex.: PNG virou JPG) e já existe outro arquivo com esse nome: não apaga, usa o nome com 2 no fim.
        if (!final.Equals(file, StringComparison.OrdinalIgnoreCase)) final = Free(final);
        var span = ours ? TimeSpan.MaxValue : TimeSpan.FromMinutes(1);
        Mark(final, span);
        File.Move(res, final);
        Mark(final, span); // agora com a versão gravada
        History.Add("Comprimir (pasta monitorada)", new[] { kept }, new[] { final });
        return (final, $"{before / 1048576.0:0.0} MB → {after / 1048576.0:0.0} MB");
    }

    /// Processa o que já está na pasta (pasta nova, configuração mudada ou avisos perdidos).
    public void ScanExisting() => _ = Task.Run(() => ScanDir(w.Folder));

    void ScanDir(string dir)
    {
        try
        {
            var opts = new EnumerationOptions { RecurseSubdirectories = w.Subfolders, IgnoreInaccessible = true };
            foreach (var f in Directory.EnumerateFiles(dir, "*", opts)) Enqueue(f, true);
        }
        catch (Exception ex) { Log.Write($"vigia {dir}: {ex.Message}"); }
    }

    static bool Locked(string path)
    {
        try { using var _ = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None); return false; }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }

    async Task Process(string path, bool scan)
    {
        try
        {
            if (!File.Exists(path)) return;
            // Da varredura, só espera se o arquivo ainda estiver sendo gravado.
            if (!scan || Locked(path))
            {
                // Espera o arquivo terminar de chegar: tamanho parado e dá para abrir sozinho.
                var start = DateTime.Now;
                long last = -1;
                for (int stable = 0; stable < 3;)
                {
                    await Task.Delay(700, stop.Token);
                    if (!File.Exists(path)) return;
                    if (DateTime.Now - start > giveUp) { Log.Write($"vigia {path}: continua aberto ou mudando; desisti"); return; }
                    long len;
                    try { len = new FileInfo(path).Length; } catch (IOException) { stable = 0; continue; }
                    if (Locked(path)) { stable = 0; continue; }
                    if (len == last) stable++; else { stable = 0; last = len; }
                }
            }
            if (Ours(path) || new FileInfo(path).Length == 0) return; // arquivo vazio: nada a fazer
            bool doConvert = w.Action != "comprimir", doCompress = w.Action != "converter";
            Op? conv = null;
            if (doConvert && !(scan && AlreadyConverted(path)))
            {
                conv = Catalog.For(Mode.Convert, new[] { path }).FirstOrDefault(o => string.Equals(o.Label, w.Target, StringComparison.OrdinalIgnoreCase));
                if (conv?.One == null) conv = null;
            }
            // Já comprimido antes: o original (com o mesmo nome, talvez outra extensão) está em "originais".
            if (scan && Directory.Exists(OriginalsDir) &&
                Directory.EnumerateFiles(OriginalsDir, Path.GetFileNameWithoutExtension(path) + ".*").Any()) doCompress = false;
            if (conv == null && !(doCompress && CanCompress(path))) return;
            await one.WaitAsync(stop.Token);
            Paths.Enter();
            try
            {
                var job = new Job(); Job.Current = job;
                try
                {
                    string cur = path, note = "";
                    if (conv != null)
                    {
                        var res = await conv.One!(path, P.None);
                        if (res == null) return;
                        Mark(res, TimeSpan.MaxValue);
                        History.Add($"{conv.Label} (pasta monitorada)", new[] { path }, new[] { res });
                        if (w.MoveOriginals) MoveToOriginals(path);
                        cur = res;
                    }
                    if (doCompress && CanCompress(cur))
                    {
                        var done = await Compress(cur, cur != path);
                        if (done != null) { note = done.Value.note; cur = done.Value.path; }
                    }
                    if (cur == path) return; // nada mudou
                    report(path, cur, note == "" ? null : note);
                }
                finally { Job.Current = null; }
            }
            finally { one.Release(); Paths.Leave(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log.Write($"vigia {path}: {ex}"); report(path, null, App.Friendly(ex)); }
        finally { pending.TryRemove(path, out _); }
    }

    public void Dispose() { stop.Cancel(); fsw.EnableRaisingEvents = false; fsw.Dispose(); }
}

/// Liga e desliga os vigias conforme as configurações.
static class Watchers
{
    /// Vigias rodando, pela chave (pasta e opções): o que não mudou continua, com a fila intacta.
    static readonly Dictionary<string, FolderWatcher> running = new();
    static Timer? timer;
    static string lastState = "";

    static string Key(WatchFolder w) => string.Join("|", w.Folder.TrimEnd('\\').ToLowerInvariant(), w.Action, w.Target, w.MinMb, w.TargetMb, w.Subfolders, w.MoveOriginals);

    /// Quais pastas ligadas existem agora; se mudar (pasta apagada, pendrive tirado ou recolocado), religa os vigias.
    static string State() => string.Join("|", Config.Now.WatchFolders.Where(w => w.Enabled).Select(w => w.Folder + "=" + w.Exists));

    /// Pasta apagada (a unidade continua lá, só a pasta sumiu) sai da lista. Unidade ausente (pendrive, rede) fica, esperando voltar.
    static bool Prune()
    {
        var gone = Config.Now.WatchFolders.Where(w =>
        {
            if (w.Exists) return false;
            try { var root = Path.GetPathRoot(w.Folder); return !string.IsNullOrEmpty(root) && Directory.Exists(root); }
            catch { return false; }
        }).ToList();
        if (gone.Count == 0) return false;
        foreach (var g in gone) { Config.Now.WatchFolders.Remove(g); Log.Write($"vigia {g.Folder}: pasta apagada, saiu da lista"); }
        Config.Now.Save();
        return true;
    }

    /// Confere de novo (chamado pelo relógio e quando um vigia perde a pasta).
    public static void Recheck() => Application.Current?.Dispatcher.BeginInvoke(() =>
    {
        bool broken; lock (running) broken = running.Values.Any(r => r.Broken);
        if (Prune() || broken || State() != lastState) Apply();
    });

    static void Report(string src, string? res, string? err) => Application.Current?.Dispatcher.BeginInvoke(() =>
    {
        if (res != null && err != null) App.Toast.Show("Pasta monitorada", $"{Path.GetFileName(src)}: {err}", res);
        else if (res != null) App.Toast.Show("Pasta monitorada", $"{Path.GetFileName(src)} → {Path.GetFileName(res.TrimEnd('\\'))}", res);
        else App.Toast.Show("Pasta monitorada: não deu certo", $"{Path.GetFileName(src)}: {err}", error: true);
    });

    /// Liga os vigias. Pasta nova (ou com configuração mudada) processa uma vez o que já está nela;
    /// as já varridas ficam em Config.WatchScanned, para não refazer a cada abertura.
    public static void Apply()
    {
        lock (running)
        {
            Prune();
            var want = new Dictionary<string, WatchFolder>();
            foreach (var w in Config.Now.WatchFolders.Where(w => w.Enabled && w.Exists)) want.TryAdd(Key(w), w);
            var rescan = new HashSet<string>();
            foreach (var (k, fw) in running.ToList())
            {
                if (want.ContainsKey(k) && !fw.Broken) continue;
                if (fw.Broken) rescan.Add(k);
                fw.Dispose();
                running.Remove(k);
            }
            foreach (var (k, w) in want)
            {
                if (running.ContainsKey(k)) continue;
                try
                {
                    var fw = new FolderWatcher(w.Copy(), Report);
                    running[k] = fw;
                    if (rescan.Contains(k) || !Config.Now.WatchScanned.Contains(k)) fw.ScanExisting();
                }
                catch (Exception ex) { Log.Write($"vigia {w.Folder}: {ex.Message}"); }
            }
            var keys = running.Keys.ToList();
            if (!keys.ToHashSet().SetEquals(Config.Now.WatchScanned)) { Config.Now.WatchScanned = keys; Config.Now.Save(); }
            lastState = State();
            timer ??= new Timer(_ => Recheck(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
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
        items = current.Select(x => x.Copy()).ToList();
        Title = "Mexerica: pastas monitoradas";
        Width = 600; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaxHeight = Math.Max(420, SystemParameters.WorkArea.Height * 0.9); // muitas pastas ou tela baixa: rola em vez de sair da tela
        Ui.Dark(this);
        var st = new StackPanel { Margin = new Thickness(22, 8, 22, 8) };
        st.Children.Add(new TextBlock
        {
            Text = "Tudo o que chegar numa pasta monitorada é convertido sozinho para o formato escolhido, ao lado do original, ou comprimido quando passar do tamanho escolhido, ou as duas coisas. O que já está na pasta também é processado ao salvar. Ex.: Downloads → PDF.",
            Foreground = Ui.Dim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12),
        });
        st.Children.Add(list);
        var add = Ui.Button("Adicionar pasta…", false);
        add.HorizontalAlignment = HorizontalAlignment.Left;
        add.Click += (_, _) =>
        {
            var d = new Microsoft.Win32.OpenFolderDialog { Title = Tr.T("Pasta para monitorar") };
            if (d.ShowDialog(this) != true) return;
            if (items.Any(x => string.Equals(x.Folder.TrimEnd('\\'), d.FolderName.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            { Tr.Box(this, "Essa pasta já está na lista.", "Mexerica", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            items.Add(new WatchFolder { Folder = d.FolderName });
            Fill();
        };
        st.Children.Add(add);
        var ok = Ui.Button("Salvar", true);
        var cancel = Ui.Button("Cancelar", false);
        ok.Click += (_, _) => DialogResult = true;
        cancel.Click += (_, _) => DialogResult = false;
        Content = Ui.Scrolling(st, cancel, ok);
        Fill();
    }

    static readonly string[] formats = { "PDF", "JPG", "PNG", "WEBP", "AVIF", "HEIC", "MP4", "MP3", "M4A", "DOCX", "TXT", "XLSX", "CSV", "EPUB", "ZIP" };

    static TextBox Mb(double value, Action<double> set)
    {
        var t = Ui.Text(value.ToString("0.##"), 80);
        t.TextChanged += (_, _) =>
        {
            if (double.TryParse(t.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) && d >= 0) set(d);
        };
        return t;
    }

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
            var name = Path.GetFileName(w.Folder.TrimEnd('\\'));
            var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center, ToolTip = w.Folder };
            title.Children.Add(new TextBlock { Text = name.Length > 0 ? name : w.Folder, Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            title.Children.Add(new TextBlock { Text = w.Folder, Foreground = Ui.Dim, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
            head.Children.Add(title);
            box.Children.Add(head);
            if (!w.Exists)
                box.Children.Add(new TextBlock { Text = Tr.T("Pasta não encontrada (unidade desconectada?). Volta a funcionar sozinha quando ela aparecer."), Foreground = Ui.Orange, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            box.Children.Add(Ui.Row("O que fazer", Ui.Combo(new[] { ("converter", "Converter"), ("comprimir", "Comprimir"), ("ambos", "Converter e comprimir") }, w.Action, v => { w.Action = v; Fill(); })));
            if (w.Action != "comprimir")
            {
                var known = formats.FirstOrDefault(f => f.Equals(w.Target, StringComparison.OrdinalIgnoreCase));
                if (known != null) w.Target = known;
                var fmts = known != null ? formats : formats.Append(w.Target).ToArray();
                box.Children.Add(Ui.Row("Converter para", Ui.Combo(fmts.Select(f => (f, f)), w.Target, v => w.Target = v)));
            }
            if (w.Action != "converter")
            {
                box.Children.Add(Ui.Row("Comprimir só os maiores que (MB)", Mb(w.MinMb, v => w.MinMb = v)));
                box.Children.Add(Ui.Row("Deixar com até (MB; 0 = compressão média)", Mb(w.TargetMb, v => w.TargetMb = v)));
                box.Children.Add(new TextBlock { Text = Tr.T("Vale para PDF, imagem, vídeo e áudio. O comprimido fica com o nome do arquivo; o que chegou vai para a subpasta \"originais\"."), Foreground = Ui.Dim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            }
            box.Children.Add(Ui.Check("Ligada", w.Enabled, v => w.Enabled = v));
            box.Children.Add(Ui.Check("Incluir subpastas", w.Subfolders, v => w.Subfolders = v));
            if (w.Action == "converter")
                box.Children.Add(Ui.Check("Guardar os originais numa subpasta \"originais\"", w.MoveOriginals, v => w.MoveOriginals = v));
            list.Children.Add(box);
        }
    }
}
