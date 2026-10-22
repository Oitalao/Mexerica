using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mexerica;

/// Atalhos (.lnk) viram o arquivo para onde apontam; arquivos vazios e temporários esquecidos são tratados aqui.
static class Robust
{
    /// Troca cada atalho do Windows pelo arquivo de destino (se ele ainda existir).
    public static string[] Resolve(string[] files) => files.Select(Resolve).ToArray();

    public static string Resolve(string f)
    {
        if (!f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || !File.Exists(f)) return f;
        try
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return f;
            dynamic sh = Activator.CreateInstance(t)!;
            string target = sh.CreateShortcut(f).TargetPath;
            return !string.IsNullOrEmpty(target) && (File.Exists(target) || Directory.Exists(target)) ? target : f;
        }
        catch { return f; }
    }

    /// Antes de converter: arquivo de 0 bytes não tem o que converter (ex.: "Novo documento" do Explorador).
    public static void CheckNotEmpty(string f)
    {
        if (File.Exists(f) && new FileInfo(f).Length == 0) throw new Exception("O arquivo está vazio (0 bytes)");
    }

    /// Pastas de trabalho que um fechamento brusco deixou para trás: apaga as de mais de um dia.
    public static void CleanOldTemps()
    {
        try
        {
            foreach (var d in Directory.EnumerateDirectories(Path.GetTempPath(), "mexerica-*"))
                if (Directory.GetLastWriteTimeUtc(d) < DateTime.UtcNow.AddDays(-1)) Paths.TryDelete(d);
        }
        catch { }
    }
}

/// Uma operação concluída, para a lista de recentes.
sealed class Recent
{
    public DateTime When { get; set; }
    public string Op { get; set; } = "";
    public string[] Inputs { get; set; } = Array.Empty<string>();
    public string[] Outputs { get; set; } = Array.Empty<string>();
}

/// Histórico das conversões (até 300), em %LOCALAPPDATA%\Mexerica\recentes.json.
static class History
{
    static readonly string file = Path.Combine(Paths.Data, "recentes.json");
    static List<Recent>? items;
    public static event Action? Changed;

    public static List<Recent> Items
    {
        get
        {
            lock (file)
            {
                if (items != null) return items;
                try { items = File.Exists(file) ? JsonSerializer.Deserialize<List<Recent>>(File.ReadAllText(file)) ?? new() : new(); }
                catch (Exception ex) { Log.Write("recentes: " + ex.Message); items = new(); }
                return items;
            }
        }
    }

    public static void Add(string op, string[] inputs, IEnumerable<string> outputs)
    {
        var outs = outputs.Where(o => !string.IsNullOrEmpty(o)).ToArray();
        if (outs.Length == 0) return;
        lock (file)
        {
            Items.Insert(0, new Recent { When = DateTime.Now, Op = op, Inputs = inputs, Outputs = outs });
            if (items!.Count > 300) items.RemoveRange(300, items.Count - 300);
            Save();
        }
        Application.Current?.Dispatcher.BeginInvoke(() => Changed?.Invoke());
    }

    public static void Remove(Recent r) { lock (file) { Items.Remove(r); Save(); } Changed?.Invoke(); }
    public static void Clear() { lock (file) { Items.Clear(); Save(); } Changed?.Invoke(); }

    static void Save()
    {
        try
        {
            Directory.CreateDirectory(Paths.Data);
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(items));
            File.Move(tmp, file, true);
        }
        catch (Exception ex) { Log.Write("recentes: " + ex.Message); }
    }
}

/// Janela "Conversões recentes": busca, miniatura, abrir, abrir com, mostrar na pasta.
sealed class HistoryWindow : Window
{
    static HistoryWindow? open;
    readonly StackPanel list = new();
    readonly TextBox search;

    public static void ShowOne()
    {
        if (open != null) { open.Activate(); return; }
        open = new HistoryWindow();
        open.Closed += (_, _) => open = null;
        open.Show();
    }

    internal HistoryWindow()
    {
        Title = "Mexerica: conversões recentes";
        Width = 640; Height = 620; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.Dark(this);
        var root = new DockPanel { Margin = new Thickness(18) };
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var clear = Ui.Button("Limpar lista", false);
        clear.Click += (_, _) =>
        {
            if (Tr.Box(this, "Apagar a lista de recentes? Os arquivos continuam onde estão.", "Mexerica", MessageBoxButton.YesNo) == MessageBoxResult.Yes) History.Clear();
        };
        DockPanel.SetDock(clear, Dock.Right);
        top.Children.Add(clear);
        search = Ui.Text("", 300);
        search.Tag = "buscar";
        search.TextChanged += (_, _) => Fill();
        var lbl = new TextBlock { Text = "Buscar", Foreground = Ui.Dim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        top.Children.Add(lbl); top.Children.Add(search);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
        History.Changed += Fill;
        Closed += (_, _) => History.Changed -= Fill;
        Fill();
        Loaded += (_, _) => search.Focus();
    }

    void Fill()
    {
        list.Children.Clear();
        var q = search.Text.Trim();
        var shown = History.Items.Where(r => q.Length == 0
            || r.Outputs.Concat(r.Inputs).Any(p => Path.GetFileName(p.TrimEnd('\\')).Contains(q, StringComparison.CurrentCultureIgnoreCase))
            || r.Op.Contains(q, StringComparison.CurrentCultureIgnoreCase)).Take(150).ToList();
        if (shown.Count == 0)
        {
            list.Children.Add(new TextBlock { Text = q.Length == 0 ? "Nada convertido ainda." : "Nada encontrado.", Foreground = Ui.Dim, Margin = new Thickness(4, 10, 0, 0) });
            return;
        }
        foreach (var r in shown) list.Children.Add(Row(r));
    }

    UIElement Row(Recent r)
    {
        var first = r.Outputs[0];
        bool exists = File.Exists(first) || Directory.Exists(first);
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8), Background = Ui.Panel };
        var thumb = new Image { Width = 56, Height = 56, Margin = new Thickness(8), Stretch = Stretch.Uniform, Source = Thumb(first) };
        DockPanel.SetDock(thumb, Dock.Left);
        row.Children.Add(thumb);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        Button B(string t, Action a, bool enabled = true) { var b = Ui.Button(t, false); b.MinWidth = 0; b.Padding = new Thickness(10, 4, 10, 4); b.Margin = new Thickness(4, 0, 0, 0); b.IsEnabled = enabled; b.Click += (_, _) => a(); buttons.Children.Add(b); return b; }
        B("Abrir", () => Shell.Open(first), exists);
        B("Abrir com…", () => Shell.OpenWith(first), exists && File.Exists(first));
        B("Pasta", () => Toast.Reveal(first), exists);
        var x = B("✕", () => History.Remove(r));
        x.ToolTip = "Tirar da lista";
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(buttons);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var name = r.Outputs.Length == 1 ? Path.GetFileName(first.TrimEnd('\\')) : $"{Path.GetFileName(first.TrimEnd('\\'))} e mais {r.Outputs.Length - 1}";
        info.Children.Add(new TextBlock { Text = name, Foreground = exists ? Brushes.White : Ui.Dim, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        var from = r.Inputs.Length == 1 ? Path.GetFileName(r.Inputs[0].TrimEnd('\\')) : $"{r.Inputs.Length} arquivos";
        var detail = $"{r.Op} · de {from} · {Quando(r.When)}{(exists ? "" : " · não está mais lá")}";
        info.Children.Add(new TextBlock { Text = detail, ToolTip = detail, Foreground = Ui.Dim, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis });
        row.Children.Add(info);
        row.MouseLeftButtonUp += (_, e) => { if (e.ClickCount == 1 && exists && e.OriginalSource is not Button) { } };
        row.MouseDown += (_, e) => { if (e.ClickCount == 2 && exists) Shell.Open(first); };
        return row;
    }

    static string Quando(DateTime t)
    {
        var d = DateTime.Now - t;
        if (d.TotalMinutes < 1) return "agora";
        if (d.TotalHours < 1) return $"há {(int)d.TotalMinutes} min";
        if (t.Date == DateTime.Today) return $"hoje, {t:HH:mm}";
        if (t.Date == DateTime.Today.AddDays(-1)) return $"ontem, {t:HH:mm}";
        return t.ToString("dd/MM/yyyy HH:mm");
    }

    static readonly string[] pics = { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".ico" };

    static ImageSource? Thumb(string path)
    {
        try
        {
            if (File.Exists(path) && pics.Contains(Path.GetExtension(path).ToLowerInvariant()))
            {
                var b = new BitmapImage();
                b.BeginInit();
                b.CacheOption = BitmapCacheOption.OnLoad;
                b.DecodePixelWidth = 112;
                b.UriSource = new Uri(path);
                b.EndInit();
                b.Freeze();
                return b;
            }
            return Shell.Icon(path);
        }
        catch { return null; }
    }
}
