using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Mexerica;

/// Editor de vídeo com linha do tempo: vários vídeos e fotos, dividir no ponto, apagar, reordenar arrastando,
/// aparar pelas bordas, velocidade e volume por pedaço, proporção da saída, desfazer e refazer.
/// A agulha fica parada no meio; quem anda é a linha do tempo.
sealed class VideoEditor : Window
{
    // ---------- Estado ----------
    EditProject proj;
    readonly Stack<EditProject> undo = new(), redo = new();
    int sel = -1;
    double T;                 // posição da agulha no vídeo final (segundos)
    double pps = 40;          // pixels por segundo da linha do tempo
    bool playing, changed;
    P? result;

    // ---------- Tela ----------
    readonly MediaElement me = new() { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual, ScrubbingEnabled = true, Stretch = Stretch.UniformToFill };
    readonly Image photo = new() { Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed };
    readonly Border frame = new() { Background = Brushes.Black, ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Grid previewArea = new() { Background = new SolidColorBrush(Color.FromRgb(18, 18, 22)) };
    readonly TextBlock waiting = new() { Foreground = Ui.Dim, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
    readonly ScrollViewer sv = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false, Focusable = false };
    readonly Canvas canvas = new() { Height = TimelineH, Background = Brushes.Transparent };
    readonly TextBlock clock = new() { Foreground = Ui.Fg, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Segoe UI Variable, Segoe UI") };
    readonly TextBlock hint = new() { Foreground = Ui.Dim, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly Button playBtn;
    readonly Button undoBtn, redoBtn;
    readonly StackPanel tools = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    readonly Border extra = new() { Visibility = Visibility.Collapsed, Padding = new Thickness(0, 6, 0, 2) };
    readonly Dictionary<string, ToggleButton> ratioBtns = new();
    readonly ToggleButton fitBtn;
    readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromMilliseconds(30) };
    readonly DispatcherTimer seekLater = new() { Interval = TimeSpan.FromMilliseconds(60) };
    readonly DispatcherTimer hold = new() { Interval = TimeSpan.FromMilliseconds(380) };
    readonly Stopwatch photoClock = new();

    const string SpeedPanel = "vel", VolumePanel = "vol";
    const double TimelineH = 118, ClipY = 30, ClipH = 70, HandleW = 14;
    static readonly Brush ClipBg = new SolidColorBrush(Color.FromRgb(58, 58, 66));
    static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    // Prévia
    string? loaded;
    bool mediaReady;
    double pendingPos;
    bool pendingPlay;
    int cur = -1;
    double photoFrom;
    bool scrollFromCode;
    readonly ConcurrentDictionary<string, string> proxies = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> proxyFailed = new(StringComparer.OrdinalIgnoreCase);
    readonly string tempDir = Paths.Temp();

    // Miniaturas por arquivo (tempo de cada uma = índice × passo)
    sealed class Thumbs { public double Step; public List<BitmapSource> Frames = new(); public double Aspect = 16 / 9.0; }
    readonly ConcurrentDictionary<string, Thumbs> thumbs = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, BitmapSource?> photos = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> thumbsLoading = new(StringComparer.OrdinalIgnoreCase);

    public static P? Ask(string[] files)
    {
        if (Form.Headless != null) return null; // linha de comando: os valores vêm por --p
        var w = new VideoEditor(files);
        return w.ShowDialog() == true ? w.result : null;
    }

    VideoEditor(string[] files)
    {
        proj = new EditProject();
        foreach (var f in files) proj.Clips.Add(VideoEdit.NewClip(f));
        Title = "Mexerica: " + Tr.T("editar vídeo");
        Width = 1180; Height = 820; MinWidth = 860; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.CanResize;
        Ui.Dark(this);
        var wa = SystemParameters.WorkArea;
        if (Height > wa.Height - 20) Height = wa.Height - 20;
        if (Width > wa.Width - 20) Width = wa.Width - 20;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // barra de cima
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // prévia
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // tempo e tocar
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // linha do tempo
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // ferramentas

        // ---- Barra de cima ----
        var top = new DockPanel { Margin = new Thickness(14, 10, 14, 10), LastChildFill = false };
        undoBtn = IconButton("", "Desfazer (Ctrl+Z)", Undo);
        redoBtn = IconButton("", "Refazer (Ctrl+Y)", Redo);
        DockPanel.SetDock(undoBtn, Dock.Left); DockPanel.SetDock(redoBtn, Dock.Left);
        top.Children.Add(undoBtn); top.Children.Add(redoBtn);

        var export = Ui.Button("Exportar", true);
        export.Click += (_, _) => Export();
        var cancel = Ui.Button("Cancelar", false);
        cancel.Click += (_, _) => Close();
        DockPanel.SetDock(export, Dock.Right); DockPanel.SetDock(cancel, Dock.Right);
        top.Children.Add(export); top.Children.Add(cancel);

        var ratios = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 0) };
        foreach (var (v, label) in new[] { ("original", "Original"), ("9:16", "9:16"), ("1:1", "1:1"), ("16:9", "16:9") })
        {
            var b = Toggle(label, v switch { "9:16" => "Em pé (Reels, Status, TikTok)", "1:1" => "Quadrado", "16:9" => "Deitado (YouTube)", _ => "Proporção do primeiro vídeo" });
            b.Click += (_, _) => { Change(() => proj.Ratio = v); };
            ratioBtns[v] = b; ratios.Children.Add(b);
        }
        fitBtn = Toggle("Preencher", "Preencher a tela (corta as sobras) ou caber inteiro (com barras pretas)");
        fitBtn.Click += (_, _) => Change(() => proj.Fit = proj.Fit == "caber" ? "preencher" : "caber");
        ratios.Children.Add(new Border { Width = 10 });
        ratios.Children.Add(fitBtn);
        DockPanel.SetDock(ratios, Dock.Right);
        top.Children.Add(ratios);
        Grid.SetRow(top, 0); root.Children.Add(top);

        // ---- Prévia ----
        var inner = new Grid();
        inner.Children.Add(me);
        inner.Children.Add(photo);
        frame.Child = inner;
        previewArea.Children.Add(frame);
        previewArea.Children.Add(waiting);
        previewArea.SizeChanged += (_, _) => FitFrame();
        previewArea.MouseLeftButtonDown += (_, _) => TogglePlay();
        Grid.SetRow(previewArea, 1); root.Children.Add(previewArea);

        // ---- Tempo e tocar ----
        var mid = new Grid { Margin = new Thickness(14, 8, 14, 4) };
        mid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mid.Children.Add(clock);
        playBtn = IconButton("", "Tocar ou pausar (Espaço)", TogglePlay);
        playBtn.Width = 46; playBtn.Height = 40; playBtn.FontSize = 18;
        Grid.SetColumn(playBtn, 1); mid.Children.Add(playBtn);
        var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        right.Children.Add(IconButton("", "Menos zoom (-)", () => Zoom(1 / 1.5)));
        right.Children.Add(IconButton("", "Mais zoom (+)", () => Zoom(1.5)));
        var add = Ui.Button("+ Adicionar", false);
        add.ToolTip = Tr.T("Adicionar vídeos ou fotos à linha do tempo");
        add.Click += (_, _) => AddMedia();
        right.Children.Add(add);
        Grid.SetColumn(right, 2); mid.Children.Add(right);
        Grid.SetRow(mid, 2); root.Children.Add(mid);

        // ---- Linha do tempo ----
        var tl = new Grid { Height = TimelineH, Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)) };
        sv.Content = canvas;
        tl.Children.Add(sv);
        tl.Children.Add(new Rectangle { Width = 2, Fill = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false, Margin = new Thickness(0, 22, 0, 6) });
        tl.Children.Add(new Polygon { Points = new PointCollection { new(0, 0), new(12, 0), new(6, 8) }, Fill = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 16, 0, 0), IsHitTestVisible = false });
        sv.ScrollChanged += (_, _) => OnScroll();
        sv.SizeChanged += (_, _) => { RenderTimeline(); ScrollTo(T); };
        canvas.MouseLeftButtonDown += Down;
        canvas.MouseMove += Move;
        canvas.MouseLeftButtonUp += Up;
        canvas.LostMouseCapture += (_, _) => { if (drag != DragMode.None) Up(null, null); };
        sv.PreviewMouseWheel += Wheel;
        Grid.SetRow(tl, 3); root.Children.Add(tl);

        // ---- Ferramentas ----
        var bottom = new StackPanel { Margin = new Thickness(14, 8, 14, 12) };
        bottom.Children.Add(tools);
        bottom.Children.Add(extra);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.Margin = new Thickness(0, 6, 0, 0);
        bottom.Children.Add(hint);
        Grid.SetRow(bottom, 4); root.Children.Add(bottom);
        Content = root;

        // ---- Eventos ----
        tick.Tick += (_, _) => Tick();
        seekLater.Tick += (_, _) => { seekLater.Stop(); ShowAt(T, false); };
        hold.Tick += (_, _) => { hold.Stop(); if (drag == DragMode.Press) StartReorder(); };
        me.MediaOpened += (_, _) => OnOpened();
        me.MediaFailed += async (_, e) => await OnFailed(e.ErrorException?.Message);
        me.MediaEnded += (_, _) => { if (playing && cur >= 0 && cur < proj.Clips.Count && !proj.Clips[cur].Photo) Next(); };
        PreviewKeyDown += Key;
        Closing += (_, e) =>
        {
            if (result == null && changed && proj.Clips.Count > 0 &&
                Tr.Box(this, "Sair sem exportar? As mudanças serão perdidas.", "Mexerica", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                e.Cancel = true;
        };
        Closed += (_, _) => { closed = true; tick.Stop(); seekLater.Stop(); me.Stop(); me.Close(); Paths.TryDelete(tempDir); };
        Loaded += (_, _) =>
        {
            fpsCache = VideoEdit.Fps(proj);
            FitZoom();
            Refresh();
            ShowAt(0, false);
            tick.Start();
            foreach (var c in proj.Clips) LoadThumbs(c.Src);
        };
    }

    // ---------- Teste sem tela ----------

    internal static async Task SelfTest(string[] files, string folder, List<string> log)
    {
        var w = new VideoEditor(files) { Left = -30000, Top = -30000, ShowActivated = false, ShowInTaskbar = false };
        w.Show();
        await Task.Delay(6000); // miniaturas
        void Shot(string name)
        {
            w.UpdateLayout();
            var root = (FrameworkElement)w.Content;
            var bmp = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen()) { dc.DrawRectangle(w.Background, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight)); dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight)); }
            bmp.Render(dv);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp));
            using var s = File.Create(System.IO.Path.Combine(folder, $"editor-{name}.png")); png.Save(s);
        }
        string State() => $"pedaços={w.proj.Clips.Count} total={w.proj.Total:0.00} sel={w.sel} T={w.T:0.00} " +
            string.Join(" ", w.proj.Clips.Select(c => $"[{System.IO.Path.GetFileName(c.Src)} {c.In:0.00}-{c.Out:0.00} x{c.Speed} v{c.Volume}]"));
        log.Add("início: " + State());
        Shot("1-inicio");
        w.T = 1.5; w.ScrollTo(w.T); w.Split(); log.Add("dividir em 1,5: " + State());
        w.T = 4.0; w.ScrollTo(w.T); w.Split(); log.Add("dividir em 4,0: " + State());
        Shot("2-dividido");
        w.sel = 1; w.Refresh(); w.Delete(); log.Add("apagar o 2º: " + State());
        w.sel = 0; w.Refresh(); w.ShowExtra("vel"); Shot("3-velocidade");
        w.SetSpeed(2); log.Add("velocidade 2x no 1º: " + State());
        w.ShowExtra("vol"); w.ShowExtra("vol"); w.SetVolume(0, true); log.Add("mudo no 1º: " + State());
        Shot("4-volume");
        w.MoveSel(1); log.Add("1º para depois: " + State());
        w.Undo(); log.Add("desfazer: " + State());
        w.Undo(); w.Undo(); log.Add("desfazer x2: " + State());
        w.Redo(); log.Add("refazer: " + State());
        w.proj.Ratio = "9:16"; w.Refresh(); Shot("5-vertical");
        w.dragIdx = 0; w.sel = 0; w.Refresh(); w.StartTrim(DragMode.TrimRight); w.Trim(-w.pps * 0.5); w.Up(null, null); log.Add("aparar 0,5 s do fim do 1º: " + State());
        w.Zoom(3); Shot("6-zoom");
        w.Play(); await Task.Delay(2500); log.Add("tocando 2,5 s: " + State()); w.Pause();
        w.Export(); log.Add("exportar: " + (w.result?.S("projeto") ?? "(nada)"));
        if (w.result != null) { var path = await VideoEdit.Render(files, w.result); log.Add("vídeo: " + path); }
        else w.Close();
    }

    // ---------- Peças de interface ----------

    Button IconButton(string glyph, string tip, Action a)
    {
        var b = Ui.Button("", false);
        var t = new TextBlock { Text = glyph, FontFamily = Icons, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Tr.Raw(t);
        b.Content = t; b.MinWidth = 38; b.Padding = new Thickness(10, 6, 10, 6); b.Margin = new Thickness(0, 0, 6, 0);
        b.ToolTip = Tr.T(tip); b.Focusable = false;
        b.Click += (_, _) => a();
        return b;
    }

    ToggleButton Toggle(string text, string tip)
    {
        var b = new ToggleButton
        {
            Content = Tr.T(text), Template = Ui.FlatToggle(), Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 4, 0),
            Foreground = Ui.Fg, Background = Ui.Panel, BorderBrush = Ui.Line, ToolTip = Tr.T(tip), Focusable = false, Cursor = Cursors.Hand,
        };
        return b;
    }

    Button Tool(string glyph, string label, string tip, Action a, bool enabled = true)
    {
        var st = new StackPanel();
        var g = new TextBlock { Text = glyph, FontFamily = Icons, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center };
        Tr.Raw(g);
        st.Children.Add(g);
        st.Children.Add(new TextBlock { Text = Tr.T(label), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
        var b = new Button
        {
            Content = st, Template = Ui.FlatButton(), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, Foreground = Ui.Fg,
            Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(2, 0, 2, 0), MinWidth = 78, ToolTip = Tr.T(tip), Focusable = false,
            IsEnabled = enabled, Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => a();
        return b;
    }

    // ---------- Projeto: mudanças, desfazer ----------

    void Snapshot()
    {
        undo.Push(proj.Copy());
        if (undo.Count > 200) { var keep = undo.Take(150).Reverse().ToList(); undo.Clear(); foreach (var k in keep) undo.Push(k); }
        redo.Clear();
        changed = true;
        UpdateUndo();
    }

    void UpdateUndo() { undoBtn.IsEnabled = undo.Count > 0; redoBtn.IsEnabled = redo.Count > 0; }

    void Change(Action a) { Pause(); Snapshot(); a(); Refresh(); ShowAt(T, false); }

    void Undo()
    {
        if (undo.Count == 0) return;
        Pause();
        redo.Push(proj.Copy()); proj = undo.Pop();
        if (sel >= proj.Clips.Count) sel = -1;
        T = Math.Min(T, proj.Total);
        Refresh(); ScrollTo(T); ShowAt(T, false);
    }

    void Redo()
    {
        if (redo.Count == 0) return;
        Pause();
        undo.Push(proj.Copy()); proj = redo.Pop();
        if (sel >= proj.Clips.Count) sel = -1;
        T = Math.Min(T, proj.Total);
        Refresh(); ScrollTo(T); ShowAt(T, false);
    }

    double Start(int i) { double s = 0; for (int k = 0; k < i && k < proj.Clips.Count; k++) s += proj.Clips[k].Len; return s; }

    /// Pedaço que está na posição t do vídeo final, e quanto já andou dentro dele.
    (int idx, double local) Locate(double t)
    {
        double s = 0;
        for (int i = 0; i < proj.Clips.Count; i++)
        {
            var l = proj.Clips[i].Len;
            if (t < s + l || i == proj.Clips.Count - 1) return (i, Math.Clamp(t - s, 0, l));
            s += l;
        }
        return (-1, 0);
    }

    void Split()
    {
        if (proj.Clips.Count == 0) return;
        Pause();
        var (i, local) = Locate(T);
        var c = proj.Clips[i];
        if (local < VideoEdit.MinLen || c.Len - local < VideoEdit.MinLen) { Hint("Leve a agulha para o meio de um pedaço para dividir."); return; }
        Snapshot();
        var b = c.Copy();
        if (c.Photo) { c.Out = local * c.Speed; b.Out = b.Out - c.Out; }
        else { var at = c.In + local * c.Speed; c.Out = at; b.In = at; }
        proj.Clips.Insert(i + 1, b);
        sel = i + 1;
        Refresh(); ShowAt(T, false);
    }

    void Delete()
    {
        if (sel < 0 || sel >= proj.Clips.Count) return;
        Pause();
        Snapshot();
        proj.Clips.RemoveAt(sel);
        sel = proj.Clips.Count == 0 ? -1 : Math.Min(sel, proj.Clips.Count - 1);
        sel = -1;
        T = Math.Min(T, proj.Total);
        Refresh(); ScrollTo(T); ShowAt(T, false);
    }

    void Duplicate()
    {
        if (sel < 0) return;
        Pause();
        Snapshot();
        proj.Clips.Insert(sel + 1, proj.Clips[sel].Copy());
        sel++;
        Refresh(); ShowAt(T, false);
    }

    void MoveSel(int d)
    {
        if (sel < 0) return;
        int to = sel + d;
        if (to < 0 || to >= proj.Clips.Count) return;
        Pause();
        Snapshot();
        var c = proj.Clips[sel]; proj.Clips.RemoveAt(sel); proj.Clips.Insert(to, c);
        sel = to;
        T = Start(sel);
        Refresh(); ScrollTo(T); ShowAt(T, false);
    }

    void SetSpeed(double s)
    {
        if (sel < 0) return;
        Pause();
        var c = proj.Clips[sel];
        if (!c.Photo && (c.Out - c.In) / s < VideoEdit.MinLen) { Hint("Pedaço curto demais para essa velocidade."); return; }
        Snapshot();
        if (c.Photo) { var len = c.Len; c.Speed = 1; c.Out = len; } // foto: velocidade não muda nada
        else c.Speed = s;
        T = Start(sel);
        Refresh(); ScrollTo(T); ShowAt(T, false);
    }

    void SetVolume(double v, bool snapshot)
    {
        if (sel < 0) return;
        if (snapshot) Snapshot();
        proj.Clips[sel].Volume = v;
        if (cur == sel) { me.Volume = Math.Min(1, v); me.IsMuted = v <= 0; }
        RenderTimeline();
    }

    void AddMedia()
    {
        Pause();
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true, Title = Tr.T("Adicionar vídeos ou fotos"),
            Filter = Tr.T("Vídeos e fotos") + "|*.mp4;*.mov;*.mkv;*.webm;*.avi;*.wmv;*.m4v;*.flv;*.mpg;*.mpeg;*.3gp;*.ts;*.mts;*.m2ts;*.jpg;*.jpeg;*.png;*.webp;*.heic;*.heif;*.bmp;*.gif;*.tif;*.tiff;*.avif",
        };
        if (proj.Clips.Count > 0 && System.IO.Path.GetDirectoryName(proj.Clips[0].Src) is { } d) dlg.InitialDirectory = d;
        if (dlg.ShowDialog(this) != true) return;
        var add = dlg.FileNames.Where(f => Catalog.KindOf(f) is Kind.Video or Kind.Image).Select(VideoEdit.NewClip).Where(c => c.Out - c.In >= VideoEdit.MinLen).ToList();
        if (add.Count == 0) { Hint("Nenhum desses arquivos é vídeo ou foto."); return; }
        Snapshot();
        // Entra depois do pedaço selecionado; sem seleção, depois do pedaço que está na agulha.
        int at = sel >= 0 ? sel + 1 : proj.Clips.Count == 0 ? 0 : Locate(T).idx + 1;
        proj.Clips.InsertRange(at, add);
        foreach (var c in add) LoadThumbs(c.Src);
        sel = at;
        T = Start(at);
        if (proj.Clips.Count == add.Count) FitZoom();
        fpsCache = VideoEdit.Fps(proj);
        Refresh(); ScrollTo(T); ShowAt(T, false);
    }

    void Export()
    {
        Pause();
        if (proj.Clips.Count == 0 || proj.Total < VideoEdit.MinLen) { Tr.Box(this, "A linha do tempo está vazia. Adicione um vídeo ou uma foto.", "Mexerica", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        result = new P { ["projeto"] = proj.ToJson() };
        try { DialogResult = true; } catch (InvalidOperationException) { Close(); } // fora de ShowDialog (teste)
    }

    // ---------- Tela: atualizar ----------

    void Refresh()
    {
        RenderTimeline();
        RenderTools();
        foreach (var (v, b) in ratioBtns) Mark(b, proj.Ratio == v);
        fitBtn.Content = Tr.T(proj.Fit == "caber" ? "Caber" : "Preencher");
        Mark(fitBtn, false);
        UpdateUndo();
        me.Stretch = photo.Stretch = proj.Fit == "caber" ? Stretch.Uniform : Stretch.UniformToFill;
        outSize = null;
        FitFrame();
        UpdateClock();
    }

    static void Mark(ToggleButton b, bool on)
    {
        b.IsChecked = on;
        b.Background = on ? Ui.Orange : Ui.Panel;
        b.Foreground = on ? Brushes.Black : Ui.Fg;
        b.BorderBrush = on ? Ui.Orange : Ui.Line;
    }

    (int w, int h)? outSize;

    void FitFrame()
    {
        if (previewArea.ActualWidth <= 0) return;
        outSize ??= proj.Clips.Count > 0 ? VideoEdit.OutputSize(proj) : (16, 9);
        var (w, h) = outSize.Value;
        double aw = previewArea.ActualWidth - 24, ah = previewArea.ActualHeight - 24;
        if (aw <= 0 || ah <= 0 || w <= 0 || h <= 0) return;
        double k = Math.Min(aw / w, ah / h);
        frame.Width = w * k; frame.Height = h * k;
    }

    void UpdateClock() => clock.Text = $"{Time(T)}  /  {Time(proj.Total)}";

    static string Time(double s)
    {
        if (s < 0) s = 0;
        var t = TimeSpan.FromSeconds(s);
        var dec = (int)(t.Milliseconds / 100);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00},{dec}";
    }

    void Hint(string text) { hint.Text = Tr.T(text); hint.Foreground = Ui.Orange; }

    void RenderTools()
    {
        tools.Children.Clear();
        bool has = sel >= 0 && sel < proj.Clips.Count;
        // Painel de velocidade/volume continua aberto enquanto o pedaço selecionado for vídeo.
        if (extra.Visibility == Visibility.Visible && has && !proj.Clips[sel].Photo && extra.Tag is string open) ShowExtra(open, true);
        else extra.Visibility = Visibility.Collapsed;
        tools.Children.Add(Tool("", "Dividir", "Dividir na agulha (S)", Split, proj.Clips.Count > 0));
        tools.Children.Add(Tool("", "Apagar", "Apagar o pedaço selecionado (Delete)", Delete, has));
        tools.Children.Add(Tool("", "Duplicar", "Duplicar o pedaço selecionado (Ctrl+D)", Duplicate, has));
        tools.Children.Add(Tool("", "Velocidade", "Velocidade do pedaço selecionado", () => ShowExtra(SpeedPanel), has && !proj.Clips[sel].Photo));
        tools.Children.Add(Tool("", "Volume", "Volume do pedaço selecionado", () => ShowExtra(VolumePanel), has && !proj.Clips[sel].Photo));
        tools.Children.Add(Tool("", "Para trás", "Levar o pedaço para antes (Ctrl+←)", () => MoveSel(-1), has && sel > 0));
        tools.Children.Add(Tool("", "Para frente", "Levar o pedaço para depois (Ctrl+→)", () => MoveSel(1), has && sel < proj.Clips.Count - 1));
        hint.Foreground = Ui.Dim;
        hint.Text = Tr.T(has
            ? "Arraste as bordas laranja para aparar. Segure e arraste o pedaço para mudar de lugar."
            : "Toque num pedaço para editar. Arraste a linha do tempo para andar. Espaço toca, S divide, Delete apaga.");
    }

    void ShowExtra(string which, bool keep = false)
    {
        if (sel < 0) return;
        if (!keep && extra.Visibility == Visibility.Visible && (string?)extra.Tag == which) { extra.Visibility = Visibility.Collapsed; return; }
        var c = proj.Clips[sel];
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        if (which == "vel")
        {
            foreach (var s in new[] { 0.25, 0.5, 0.75, 1, 1.5, 2, 3, 4 })
            {
                var b = Toggle(s.ToString("0.##", CultureInfo.CurrentCulture) + "x", "");
                b.ToolTip = null;
                Mark(b, Math.Abs(c.Speed - s) < 0.001);
                var v = s;
                b.Click += (_, _) => SetSpeed(v);
                row.Children.Add(b);
            }
        }
        else
        {
            var mute = Toggle(c.Volume <= 0 ? "Com som" : "Sem som", "");
            mute.ToolTip = null;
            var label = new TextBlock { Width = 52, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right, Margin = new Thickness(8, 0, 0, 0) };
            var slider = new Slider { Minimum = 0, Maximum = 200, Width = 260, Value = Math.Round(c.Volume * 100), VerticalAlignment = VerticalAlignment.Center, IsSnapToTickEnabled = true, TickFrequency = 5, Margin = new Thickness(10, 0, 0, 0) };
            label.Text = $"{slider.Value:0}%";
            bool first = true;
            slider.PreviewMouseDown += (_, _) => first = true;
            slider.ValueChanged += (_, e) =>
            {
                label.Text = $"{e.NewValue:0}%";
                SetVolume(e.NewValue / 100, first); first = false;
                // O tocador do Windows não passa de 100%: o ganho acima disso só se ouve no vídeo exportado.
                if (e.NewValue > 100) Hint("Acima de 100% o aumento vale no vídeo exportado; a prévia toca no máximo a 100%.");
                mute.Content = Tr.T(e.NewValue <= 0 ? "Com som" : "Sem som");
            };
            mute.Click += (_, _) => { first = true; slider.Value = proj.Clips[sel].Volume <= 0 ? 100 : 0; Mark(mute, false); };
            Mark(mute, false);
            row.Children.Add(mute); row.Children.Add(slider); row.Children.Add(label);
        }
        extra.Child = row; extra.Tag = which; extra.Visibility = Visibility.Visible;
    }

    // ---------- Linha do tempo: desenho ----------

    readonly List<(Rect r, int idx)> hits = new();
    Rect leftHandle, rightHandle;
    double Pad => Math.Max(0, sv.ViewportWidth > 0 ? sv.ViewportWidth / 2 : sv.ActualWidth / 2);

    void FitZoom()
    {
        var total = Math.Max(1, proj.Total);
        var vw = sv.ActualWidth > 0 ? sv.ActualWidth : Width - 40;
        pps = Math.Clamp(vw * 0.8 / total, 4, 300);
    }

    void Zoom(double k)
    {
        pps = Math.Clamp(pps * k, 2, 400);
        // A linha do tempo não pode ficar enorme demais (miniaturas demais travam a tela).
        if (proj.Total * pps > 200000) pps = 200000 / Math.Max(1, proj.Total);
        RenderTimeline(); ScrollTo(T);
    }

    void RenderTimeline()
    {
        canvas.Children.Clear();
        hits.Clear();
        leftHandle = rightHandle = Rect.Empty;
        double pad = Pad;
        double total = proj.Total;
        canvas.Width = pad * 2 + total * pps;

        // Régua
        double step = new[] { 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600 }.FirstOrDefault(s => s * pps >= 70, 1200);
        for (double t = 0; t <= total + 0.001; t += step)
        {
            var x = pad + t * pps;
            canvas.Children.Add(new Rectangle { Width = 1, Height = 5, Fill = Ui.Line, IsHitTestVisible = false }.At(x, 20));
            var tb = new TextBlock { Text = Ruler(t), FontSize = 10, Foreground = Ui.Dim, IsHitTestVisible = false };
            Tr.Raw(tb);
            canvas.Children.Add(tb.At(x + 3, 4));
        }

        double s = 0;
        for (int i = 0; i < proj.Clips.Count; i++)
        {
            var c = proj.Clips[i];
            double x = pad + s * pps, w = Math.Max(4, c.Len * pps);
            var r = new Rect(x, ClipY, w, ClipH);
            hits.Add((r, i));
            bool isSel = i == sel;
            bool dragging = drag == DragMode.Reorder && i == dragIdx;
            var box = new Border
            {
                Width = Math.Max(2, w - 2), Height = ClipH, CornerRadius = new CornerRadius(6), ClipToBounds = true,
                Background = ClipBg, BorderBrush = isSel ? Ui.Orange : Brushes.Transparent, BorderThickness = new Thickness(isSel ? 3 : 0),
                Opacity = dragging ? 0.35 : 1, IsHitTestVisible = false,
            };
            var g = new Grid();
            g.Children.Add(Strip(c, w - 2));
            var badges = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(5, 0, 5, 3) };
            if (w > 46) badges.Children.Add(Badge(c.Len.ToString("0.0", CultureInfo.CurrentCulture) + " s"));
            if (!c.Photo && Math.Abs(c.Speed - 1) > 0.001 && w > 90) badges.Children.Add(Badge(c.Speed.ToString("0.##", CultureInfo.CurrentCulture) + "x"));
            if (!c.Photo && c.Volume <= 0 && w > 120) badges.Children.Add(Badge("", true));
            g.Children.Add(badges);
            box.Child = g;
            canvas.Children.Add(box.At(x + 1, ClipY));
            if (isSel && !dragging)
            {
                leftHandle = new Rect(x - HandleW + 1, ClipY, HandleW, ClipH);
                rightHandle = new Rect(x + w - 1, ClipY, HandleW, ClipH);
                canvas.Children.Add(HandleShape(true).At(leftHandle.X, ClipY));
                canvas.Children.Add(HandleShape(false).At(rightHandle.X, ClipY));
            }
            s += c.Len;
        }

        if (drag == DragMode.Reorder && dragIdx >= 0 && dragIdx < proj.Clips.Count)
        {
            // Pedaço "na mão" segue o mouse; a barra laranja mostra onde vai cair.
            var c = proj.Clips[dragIdx];
            double w = Math.Clamp(c.Len * pps, 60, 160);
            var ghost = new Border { Width = w, Height = ClipH + 8, CornerRadius = new CornerRadius(8), ClipToBounds = true, Background = ClipBg, BorderBrush = Ui.Orange, BorderThickness = new Thickness(2), IsHitTestVisible = false, Opacity = 0.95 };
            ghost.Child = Strip(c, w);
            canvas.Children.Add(ghost.At(dragX - w / 2, ClipY - 4));
            double ix = pad + Start(dropAt) * pps;
            if (dropAt > dragIdx) ix = pad + Start(dropAt + 1) * pps;
            canvas.Children.Add(new Rectangle { Width = 4, Height = ClipH + 12, Fill = Ui.Orange, RadiusX = 2, RadiusY = 2, IsHitTestVisible = false }.At(ix - 2, ClipY - 6));
        }

        if (proj.Clips.Count == 0)
        {
            var tb = new TextBlock { Text = Tr.T("Linha do tempo vazia. Use + Adicionar."), Foreground = Ui.Dim, IsHitTestVisible = false };
            canvas.Children.Add(tb.At(pad + 10, ClipY + 26));
        }
    }

    static string Ruler(double t)
    {
        var ts = TimeSpan.FromSeconds(t);
        return t % 1 > 0.001 ? $"{(int)ts.TotalMinutes}:{ts.Seconds:00},5" : $"{(int)ts.TotalMinutes}:{ts.Seconds:00}";
    }

    static FrameworkElement Badge(string text, bool icon = false)
    {
        var tb = new TextBlock { Text = text, FontSize = icon ? 11 : 10.5, Foreground = Brushes.White };
        if (icon) tb.FontFamily = Icons;
        Tr.Raw(tb);
        return new Border { Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)), CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(0, 0, 4, 0), Child = tb };
    }

    static FrameworkElement HandleShape(bool left)
    {
        var b = new Border
        {
            Width = HandleW, Height = ClipH, Background = Ui.Orange, IsHitTestVisible = false,
            CornerRadius = left ? new CornerRadius(6, 0, 0, 6) : new CornerRadius(0, 6, 6, 0),
        };
        b.Child = new Rectangle { Width = 2, Height = 22, Fill = Brushes.Black, RadiusX = 1, RadiusY = 1, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.6 };
        return b;
    }

    /// Miniaturas do pedaço, lado a lado, cada uma mostrando o quadro daquele ponto.
    FrameworkElement Strip(Clip c, double width)
    {
        var cv = new Canvas { Width = Math.Max(1, width), Height = ClipH, ClipToBounds = true };
        if (c.Photo)
        {
            var bmp = PhotoBitmap(c.Src, 240);
            if (bmp == null) return cv;
            double tw = Math.Max(20, ClipH * bmp.PixelWidth / Math.Max(1.0, bmp.PixelHeight));
            for (double x = 0; x < width; x += tw)
                cv.Children.Add(new Image { Source = bmp, Width = tw, Height = ClipH, Stretch = Stretch.UniformToFill }.At(x, 0));
            return cv;
        }
        if (!thumbs.TryGetValue(c.Src, out var th) || th.Frames.Count == 0)
        {
            var tb = new TextBlock { Text = System.IO.Path.GetFileName(c.Src), Foreground = Ui.Dim, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Width = Math.Max(1, width - 10) };
            Tr.Raw(tb);
            cv.Children.Add(tb.At(6, 6));
            return cv;
        }
        double w = Math.Max(24, ClipH * th.Aspect);
        for (double x = 0; x < width; x += w)
        {
            double t = c.In + (x + w / 2) / pps * c.Speed;
            int k = Math.Clamp((int)Math.Round(t / th.Step), 0, th.Frames.Count - 1);
            cv.Children.Add(new Image { Source = th.Frames[k], Width = w, Height = ClipH, Stretch = Stretch.UniformToFill }.At(x, 0));
        }
        return cv;
    }

    void LoadThumbs(string src)
    {
        if (VideoEdit.IsPhoto(src) || thumbs.ContainsKey(src)) return;
        lock (thumbsLoading) if (!thumbsLoading.Add(src)) return;
        var dur = Ff.Duration(src);
        Task.Run(async () =>
        {
            var dir = System.IO.Path.Combine(Temp(), "th" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            try
            {
                double step = Math.Max(0.5, dur / 120);
                // Só quadros-chave: bem mais rápido, e para miniatura basta (vídeo com poucos quadros-chave cai no jeito completo).
                try { await Ff.RunOut(new[] { "-skip_frame", "nokey", "-i", src, "-an", "-sn", "-vf", $"fps=1/{step.ToString("0.###", CultureInfo.InvariantCulture)},scale=-2:96", "-fps_mode", "vfr", "-q:v", "6", System.IO.Path.Combine(dir, "%04d.jpg") }); }
                catch { }
                var files = Directory.GetFiles(dir, "*.jpg").OrderBy(f => f, StringComparer.Ordinal).ToList();
                if (files.Count < Math.Min(8, dur / step) / 2)
                {
                    foreach (var f in files) File.Delete(f);
                    await Ff.RunOut(new[] { "-i", src, "-an", "-sn", "-vf", $"fps=1/{step.ToString("0.###", CultureInfo.InvariantCulture)},scale=-2:96", "-q:v", "6", System.IO.Path.Combine(dir, "%04d.jpg") });
                }
                files =Directory.GetFiles(dir, "*.jpg").OrderBy(f => f, StringComparer.Ordinal).ToList();
                var th = new Thumbs { Step = files.Count > 1 && dur > 0 ? dur / files.Count : step };
                foreach (var f in files)
                {
                    var bi = new BitmapImage();
                    bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.UriSource = new Uri(f); bi.EndInit(); bi.Freeze();
                    th.Frames.Add(bi);
                }
                if (th.Frames.Count > 0) th.Aspect = th.Frames[0].PixelWidth / (double)Math.Max(1, th.Frames[0].PixelHeight);
                thumbs[src] = th;
                if (!closed) await Dispatcher.InvokeAsync(() => { if (!closed) RenderTimeline(); });
            }
            catch (Exception ex) { Log.Write($"miniaturas {src}: {ex.Message}"); }
            finally { Paths.TryDelete(dir); if (closed) Paths.TryDelete(tempDir); }
        });
    }

    BitmapSource? PhotoBitmap(string src, int max)
    {
        var key = $"{src}|{max}";
        if (photos.TryGetValue(key, out var hit)) return hit;
        BitmapSource? bmp = null;
        try
        {
            // ImageMagick lê HEIC, AVIF e o resto; já gira pela orientação da câmera.
            using var img = new ImageMagick.MagickImage(src);
            img.AutoOrient();
            img.Resize(new ImageMagick.MagickGeometry((uint)max, (uint)max) { Greater = true });
            img.Format = ImageMagick.MagickFormat.Png;
            var bi = new BitmapImage();
            bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = new MemoryStream(img.ToByteArray()); bi.EndInit(); bi.Freeze();
            bmp = bi;
        }
        catch (Exception ex) { Log.Write($"foto {src}: {ex.Message}"); }
        photos[key] = bmp;
        return bmp;
    }

    // Criada na linha da tela, antes das miniaturas (que rodam em paralelo), para não nascerem duas pastas.
    string Temp() => tempDir;
    bool closed;

    // ---------- Linha do tempo: mouse ----------

    enum DragMode { None, Press, Scrub, TrimLeft, TrimRight, Reorder }
    DragMode drag;
    int dragIdx = -1, dropAt = -1;
    double downX, downOffset, dragX;
    bool moved;
    Clip? trimOrig;

    void Down(object sender, MouseButtonEventArgs e)
    {
        Pause();
        var p = e.GetPosition(canvas);
        downX = e.GetPosition(sv).X; downOffset = sv.HorizontalOffset; dragX = p.X; moved = false;
        if (sel >= 0 && Grow(leftHandle).Contains(p)) { StartTrim(DragMode.TrimLeft); }
        else if (sel >= 0 && Grow(rightHandle).Contains(p)) { StartTrim(DragMode.TrimRight); }
        else
        {
            dragIdx = -1;
            foreach (var h in hits) if (h.r.Contains(p)) { dragIdx = h.idx; break; }
            drag = dragIdx >= 0 ? DragMode.Press : DragMode.Scrub;
            if (drag == DragMode.Press) hold.Start();
        }
        canvas.CaptureMouse();
        e.Handled = true;
    }

    static Rect Grow(Rect r) => r.IsEmpty ? r : new Rect(r.X - 6, r.Y - 6, r.Width + 12, r.Height + 12);

    EditProject? trimBefore;

    void StartTrim(DragMode m)
    {
        // A foto do "antes" só entra no Desfazer quando a borda mexer de verdade (tocar sem puxar não apaga o Refazer).
        trimBefore = proj.Copy();
        drag = m; dragIdx = sel; trimOrig = proj.Clips[sel].Copy();
    }

    void Move(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(canvas);
        double dx = e.GetPosition(sv).X - downX;
        if (drag == DragMode.None)
        {
            Cursor = (sel >= 0 && (Grow(leftHandle).Contains(p) || Grow(rightHandle).Contains(p))) ? Cursors.SizeWE : null;
            return;
        }
        if (Math.Abs(dx) > 5) moved = true;
        switch (drag)
        {
            case DragMode.Press:
                if (moved) { hold.Stop(); drag = DragMode.Scrub; }
                break;
            case DragMode.Scrub:
                sv.ScrollToHorizontalOffset(Math.Max(0, downOffset - dx));
                break;
            case DragMode.TrimLeft:
            case DragMode.TrimRight:
                Trim(dx);
                break;
            case DragMode.Reorder:
                dragX = p.X;
                dropAt = DropIndex(p.X);
                RenderTimeline();
                AutoScroll(e.GetPosition(sv).X);
                break;
        }
    }

    void Trim(double dx)
    {
        if (trimOrig == null || dragIdx < 0 || dragIdx >= proj.Clips.Count) return;
        var c = proj.Clips[dragIdx];
        var o = trimOrig;
        double d = dx / pps * o.Speed; // segundos do arquivo
        double minSrc = VideoEdit.MinLen * c.Speed;
        double newIn = c.In, newOut = c.Out;
        if (c.Photo)
        {
            // Foto não tem começo e fim de arquivo: as duas bordas mudam a duração.
            double len = drag == DragMode.TrimRight ? o.Out + d : o.Out - d;
            newIn = 0; newOut = Math.Clamp(len, 0.5, 600);
        }
        else if (drag == DragMode.TrimLeft)
        {
            double max = o.Out - minSrc;
            newIn = max <= 0 ? o.In : Math.Clamp(o.In + d, 0, max);
        }
        else
        {
            double min = o.In + minSrc, max = c.SrcDur > 0 ? c.SrcDur : Math.Max(min, o.Out + d);
            newOut = max <= min ? o.Out : Math.Clamp(o.Out + d, min, max);
        }
        if (Math.Abs(newIn - c.In) < 1e-9 && Math.Abs(newOut - c.Out) < 1e-9) return;
        if (trimBefore != null) { undo.Push(trimBefore); redo.Clear(); changed = true; trimBefore = null; UpdateUndo(); }
        c.In = newIn; c.Out = newOut;
        RenderTimeline();
        UpdateClock();
        // Mostra o quadro da borda que está sendo puxada.
        if (!c.Photo) { ShowVideo(); Load(c, drag == DragMode.TrimLeft ? c.In : Math.Max(c.In, c.Out - 0.04), false); }
    }

    int DropIndex(double x)
    {
        double t = (x - Pad) / pps;
        double s = 0;
        for (int i = 0; i < proj.Clips.Count; i++)
        {
            var l = proj.Clips[i].Len;
            if (t < s + l / 2) return i <= dragIdx ? i : i - 1;
            s += l;
        }
        return proj.Clips.Count - 1;
    }

    void AutoScroll(double xInView)
    {
        double edge = 60;
        if (xInView < edge) sv.ScrollToHorizontalOffset(Math.Max(0, sv.HorizontalOffset - (edge - xInView) / 3));
        else if (xInView > sv.ViewportWidth - edge) sv.ScrollToHorizontalOffset(sv.HorizontalOffset + (xInView - (sv.ViewportWidth - edge)) / 3);
    }

    void StartReorder()
    {
        if (dragIdx < 0 || proj.Clips.Count < 2) return;
        drag = DragMode.Reorder;
        sel = dragIdx;
        dropAt = dragIdx;
        RenderTimeline();
        RenderTools();
    }

    void Up(object? sender, MouseButtonEventArgs? e)
    {
        hold.Stop();
        var mode = drag;
        drag = DragMode.None;
        if (canvas.IsMouseCaptured) canvas.ReleaseMouseCapture();
        switch (mode)
        {
            case DragMode.Press:
                // toque num pedaço: seleciona (ou tira a seleção se já estava)
                sel = sel == dragIdx ? -1 : dragIdx;
                Refresh();
                break;
            case DragMode.Scrub:
                if (!moved && sel >= 0) { sel = -1; Refresh(); }
                break;
            case DragMode.TrimLeft:
            case DragMode.TrimRight:
                if (trimOrig != null && trimBefore == null && dragIdx >= 0 && dragIdx < proj.Clips.Count)
                {
                    // mexeu: a agulha vai para a borda que foi puxada
                    var c = proj.Clips[dragIdx];
                    T = mode == DragMode.TrimLeft ? Start(dragIdx) : Math.Max(Start(dragIdx), Start(dragIdx) + c.Len - 0.04);
                }
                trimOrig = null; trimBefore = null;
                Refresh(); ScrollTo(T); ShowAt(T, false);
                break;
            case DragMode.Reorder:
                if (dropAt >= 0 && dropAt != dragIdx && dragIdx >= 0 && dragIdx < proj.Clips.Count)
                {
                    Snapshot();
                    var c = proj.Clips[dragIdx];
                    proj.Clips.RemoveAt(dragIdx);
                    proj.Clips.Insert(Math.Clamp(dropAt, 0, proj.Clips.Count), c);
                    sel = Math.Clamp(dropAt, 0, proj.Clips.Count - 1);
                    T = Start(sel);
                }
                dragIdx = dropAt = -1;
                Refresh(); ScrollTo(T); ShowAt(T, false);
                break;
        }
        dragIdx = -1;
    }

    void Wheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Zoom(e.Delta > 0 ? 1.25 : 0.8); return; }
        Pause();
        sv.ScrollToHorizontalOffset(Math.Max(0, sv.HorizontalOffset - e.Delta * 0.6));
    }

    void ScrollTo(double t)
    {
        scrollFromCode = true;
        sv.ScrollToHorizontalOffset(Math.Max(0, t * pps));
        sv.UpdateLayout();
        scrollFromCode = false;
        UpdateClock();
    }

    void OnScroll()
    {
        if (scrollFromCode || playing) return;
        T = Math.Clamp(sv.HorizontalOffset / pps, 0, proj.Total);
        UpdateClock();
        seekLater.Stop(); seekLater.Start();
    }

    // ---------- Teclado ----------

    void Key(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox) return;
        // No meio de um arrasto (aparar, mudar de lugar) nada muda o projeto: o gesto termina primeiro.
        if (drag != DragMode.None) { e.Handled = true; return; }
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        switch (e.Key)
        {
            case System.Windows.Input.Key.Space: TogglePlay(); break;
            case System.Windows.Input.Key.S when !ctrl: Split(); break;
            case System.Windows.Input.Key.Delete or System.Windows.Input.Key.Back: Delete(); break;
            case System.Windows.Input.Key.Z when ctrl && shift: Redo(); break;
            case System.Windows.Input.Key.Z when ctrl: Undo(); break;
            case System.Windows.Input.Key.Y when ctrl: Redo(); break;
            case System.Windows.Input.Key.D when ctrl: Duplicate(); break;
            case System.Windows.Input.Key.Left when ctrl: MoveSel(-1); break;
            case System.Windows.Input.Key.Right when ctrl: MoveSel(1); break;
            case System.Windows.Input.Key.Left: Nudge(shift ? -1 : -1 / Math.Max(24, fpsCache)); break;
            case System.Windows.Input.Key.Right: Nudge(shift ? 1 : 1 / Math.Max(24, fpsCache)); break;
            case System.Windows.Input.Key.Home: Pause(); T = 0; ScrollTo(T); ShowAt(T, false); break;
            case System.Windows.Input.Key.End: Pause(); T = proj.Total; ScrollTo(T); ShowAt(T, false); break;
            case System.Windows.Input.Key.OemPlus or System.Windows.Input.Key.Add: Zoom(1.5); break;
            case System.Windows.Input.Key.OemMinus or System.Windows.Input.Key.Subtract: Zoom(1 / 1.5); break;
            case System.Windows.Input.Key.Escape when sel >= 0: sel = -1; Refresh(); break;
            default: return;
        }
        e.Handled = true;
    }

    double fpsCache = 30;

    void Nudge(double d)
    {
        Pause();
        T = Math.Clamp(T + d, 0, proj.Total);
        ScrollTo(T); ShowAt(T, false);
    }

    // ---------- Prévia e reprodução ----------

    void TogglePlay() { if (playing) Pause(); else Play(); }

    void Play()
    {
        if (proj.Clips.Count == 0) return;
        if (T >= proj.Total - 0.05) T = 0;
        seekLater.Stop(); // uma busca atrasada da rolagem não pode pausar o que acabou de começar
        playing = true;
        SetPlayIcon();
        ShowAt(T, true);
    }

    void Pause()
    {
        if (!playing) return;
        playing = false;
        me.Pause();
        photoClock.Stop();
        SetPlayIcon();
        ScrollTo(T);
    }

    void SetPlayIcon() { if (playBtn.Content is TextBlock t) t.Text = playing ? "" : ""; }

    /// Mostra (e, se for o caso, toca) o ponto t do vídeo final.
    void ShowAt(double t, bool play)
    {
        if (proj.Clips.Count == 0) { me.Pause(); me.Visibility = Visibility.Collapsed; photo.Visibility = Visibility.Collapsed; cur = -1; return; }
        var (i, local) = Locate(t);
        cur = i;
        var c = proj.Clips[i];
        if (c.Photo)
        {
            me.Pause();
            pendingPlay = false; // vídeo que ainda estava abrindo não pode começar a tocar por trás da foto
            photo.Source = PhotoBitmap(c.Src, 1600);
            photo.Visibility = Visibility.Visible; me.Visibility = Visibility.Collapsed;
            waiting.Text = "";
            photoFrom = local;
            if (play) photoClock.Restart(); else photoClock.Reset();
            return;
        }
        ShowVideo();
        me.SpeedRatio = c.Speed;
        me.Volume = Math.Min(1, c.Volume); me.IsMuted = c.Volume <= 0;
        Load(c, c.In + local * c.Speed, play);
    }

    void ShowVideo() { me.Visibility = Visibility.Visible; photo.Visibility = Visibility.Collapsed; }

    void Load(Clip c, double pos, bool play)
    {
        var path = proxies.TryGetValue(c.Src, out var px) ? px : c.Src;
        if (!string.Equals(loaded, path, StringComparison.OrdinalIgnoreCase))
        {
            loaded = path; mediaReady = false; pendingPos = pos; pendingPlay = play;
            me.Source = new Uri(path);
            return;
        }
        if (!mediaReady) { pendingPos = pos; pendingPlay = play; return; }
        me.Position = TimeSpan.FromSeconds(pos);
        if (play) me.Play(); else me.Pause();
    }

    void OnOpened()
    {
        mediaReady = true;
        waiting.Text = "";
        me.Play(); me.Pause();
        me.Position = TimeSpan.FromSeconds(pendingPos);
        bool videoNow = cur >= 0 && cur < proj.Clips.Count && !proj.Clips[cur].Photo;
        if (videoNow) { var c = proj.Clips[cur]; me.SpeedRatio = c.Speed; me.Volume = Math.Min(1, c.Volume); me.IsMuted = c.Volume <= 0; }
        if (pendingPlay && playing && videoNow) me.Play();
    }

    /// O Windows não toca alguns formatos: gera uma prévia leve com o ffmpeg (o vídeo final usa o original).
    async Task OnFailed(string? why)
    {
        var src = cur >= 0 && cur < proj.Clips.Count ? proj.Clips[cur].Src : null;
        if (src == null || proxies.ContainsKey(src) || !proxyFailed.Add(src)) { waiting.Text = Tr.T("Sem prévia para esse arquivo; o vídeo final sai normalmente."); return; }
        Log.Write($"prévia {src}: {why}");
        waiting.Text = Tr.T("Preparando a prévia…");
        var file = System.IO.Path.Combine(Temp(), "p" + Guid.NewGuid().ToString("N")[..8] + ".mp4");
        try
        {
            await Task.Run(() => Ff.RunOut(new[] { "-i", src, "-vf", "scale=-2:'min(540,ih)',format=yuv420p", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "28", "-g", "15", "-c:a", "aac", "-b:a", "96k", file }));
            if (closed) { Paths.TryDelete(file); Paths.TryDelete(tempDir); return; } // a janela fechou enquanto preparava
            proxies[src] = file;
            loaded = null;
            ShowAt(T, playing);
        }
        catch (Exception ex) { waiting.Text = Tr.T("Sem prévia para esse arquivo; o vídeo final sai normalmente."); Log.Write("prévia: " + ex.Message); }
    }

    void Tick()
    {
        if (!playing || cur < 0 || cur >= proj.Clips.Count) return;
        var c = proj.Clips[cur];
        double local;
        if (c.Photo) local = photoFrom + photoClock.Elapsed.TotalSeconds;
        else
        {
            if (!mediaReady) return;
            local = (me.Position.TotalSeconds - c.In) / c.Speed;
        }
        if (local >= c.Len - 0.02) { Next(); return; }
        T = Start(cur) + Math.Max(0, local);
        scrollFromCode = true;
        sv.ScrollToHorizontalOffset(T * pps);
        scrollFromCode = false;
        UpdateClock();
    }

    void Next()
    {
        if (cur + 1 < proj.Clips.Count)
        {
            T = Start(cur + 1);
            ShowAt(T, true);
        }
        else
        {
            T = proj.Total;
            Pause();
        }
    }
}

/// Teste sem tela do editor (MEXERICA_TESTE_EDITOR=arquivo1|arquivo2…): abre a janela fora da área visível,
/// faz as operações pelo código e grava capturas e um relatório em "capturas" ao lado do programa.
static class VideoEditorTest
{
    public static void Run(string[] files)
    {
        var folder = System.IO.Path.Combine(AppContext.BaseDirectory, "capturas");
        Directory.CreateDirectory(folder);
        var log = new List<string>();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            try { await VideoEditor.SelfTest(files, folder, log); }
            catch (Exception ex) { log.Add("ERRO: " + ex); }
            File.WriteAllLines(System.IO.Path.Combine(folder, "editor-teste.txt"), log);
            app.Shutdown();
        };
        app.Run();
    }
}

static class CanvasPlace
{
    public static T At<T>(this T e, double x, double y) where T : UIElement { Canvas.SetLeft(e, x); Canvas.SetTop(e, y); return e; }
}
