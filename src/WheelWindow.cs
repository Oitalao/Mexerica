using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Mexerica;

/// A roda. Fica invisível (só uma película para receber o arrasto) até o arrasto entrar com arquivos.
/// Também abre pelo teclado, já com os arquivos selecionados: aí setas escolhem, Enter faz e o mouse também vale.
sealed class WheelWindow : Window
{
    public const double D = 560, R1 = 66;
    double R2 = 205;

    readonly Grid visual;
    readonly Canvas ring;
    readonly TextBlock title, subtitle, hint;
    readonly ScaleTransform zoom = new(1, 1, D / 2, D / 2);
    readonly Func<Mode, string[], List<Op>> provider;
    readonly Action<Op, string[]> runner;
    readonly List<Path> segs = new();

    Mode mode;
    string[]? files;
    List<Op> ops = new();
    int sel = -1;
    bool keySel;      // escolha feita pelas setas: vale até o mouse andar de verdade
    Point lastPick;

    public bool IsArmed { get; private set; }
    /// Aberta pelo teclado (sem arrasto em andamento).
    public bool IsKeyboard { get; private set; }
    public bool HasFiles => files != null;
    /// Roda que já está valendo: recebeu arquivos do arrasto ou foi aberta pelo teclado.
    public bool Active => IsArmed && (files != null || IsKeyboard);
    readonly System.Windows.Threading.DispatcherTimer noFiles = new() { Interval = TimeSpan.FromMilliseconds(1200) };

    static readonly Dictionary<Mode, Color> accent = new()
    {
        [Mode.Convert] = Color.FromRgb(0xFF, 0x8A, 0x1F),
        [Mode.Tools] = Color.FromRgb(0x3A, 0xA8, 0xFF),
        [Mode.Actions] = Color.FromRgb(0x3C, 0xCB, 0x7F),
    };
    static readonly Dictionary<Mode, string> names = new() { [Mode.Convert] = "Converter", [Mode.Tools] = "Ferramentas", [Mode.Actions] = "Ações" };

    static readonly Brush segFill = Frozen(new SolidColorBrush(Color.FromArgb(236, 30, 30, 35)));
    static readonly Brush segStroke = Frozen(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)));
    static readonly Brush dim = Frozen(new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)));
    static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }

    public WheelWindow(Func<Mode, string[], List<Op>> provider, Action<Op, string[]> runner)
    {
        this.provider = provider; this.runner = runner;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        Width = D; Height = D; AllowDrop = true; Focusable = false; Title = "Mexerica";

        var root = new Grid();
        // Película quase transparente: com alfa 0 o Windows deixaria o arrasto passar direto.
        root.Children.Add(new Ellipse { Width = D, Height = D, Fill = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) });

        visual = new Grid { Opacity = 0, IsHitTestVisible = false, RenderTransform = zoom };
        ring = new Canvas { Width = D, Height = D };
        visual.Children.Add(ring);
        visual.Children.Add(new Ellipse
        {
            Width = 2 * R1 - 8, Height = 2 * R1 - 8, Fill = segFill, Stroke = segStroke, StrokeThickness = 1,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        });
        title = new TextBlock { FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
        subtitle = new TextBlock
        {
            FontSize = 10.5, Foreground = dim, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
            MaxWidth = 2 * R1 - 26, MaxHeight = 30, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 0),
        };
        var center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        center.Children.Add(title); center.Children.Add(subtitle);
        visual.Children.Add(center);
        hint = new TextBlock { FontSize = 10.5, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
        visual.Children.Add(new Border
        {
            Child = hint, Background = segFill, CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 3, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 2),
        });
        root.Children.Add(visual);
        Content = root;

        // Se nenhum arquivo chegou (o arrasto não era de arquivo), recolhe a película.
        noFiles.Tick += (_, _) => { noFiles.Stop(); if (IsArmed && !IsKeyboard && files == null) Disarm(); };
        DragEnter += OnEnter;
        DragOver += OnOver;
        // Sair de cima da roda não fecha (como no original): fecha ao soltar em outro lugar ou com Esc.
        DragLeave += (_, _) => { if (!keySel && sel >= 0) { sel = -1; Paint(); } };
        Drop += OnDrop;
        MouseMove += (_, e) => { if (IsKeyboard && files != null) PickAt(e.GetPosition(this)); };
        MouseLeftButtonUp += (_, e) => { if (IsKeyboard && files != null) { PickAt(e.GetPosition(this)); RunSelected(); } };
        SourceInitialized += (_, _) => Native.MakeToolWindow(new WindowInteropHelper(this).Handle, true);
    }

    string Hint()
    {
        var c = Config.Now;
        string K(uint vk) => Config.KeyName(vk);
        var open = K(c.OpenKey);
        return IsKeyboard
            ? $"Setas escolhem · Enter faz · {open} troca · Esc cancela"
            : $"{open} troca · {K(c.ConvertKey)} Converter · {K(c.ToolsKey)} Ferramentas · {K(c.ActionsKey)} Ações · Esc cancela";
    }

    /// Arma durante um arrasto, centrada no cursor; aparece quando o arrasto entrar com arquivos.
    public void Arm(Mode m)
    {
        mode = m; files = null; sel = -1; IsArmed = true; IsKeyboard = false; keySel = false; lastPick = new Point(D / 2, D / 2);
        hint.Text = Hint();
        visual.BeginAnimation(OpacityProperty, null);
        visual.Opacity = 0;
        Show();
        Native.GetCursorPos(out var p);
        Place(p);
        Dispatcher.BeginInvoke(Native.NudgeMouse, System.Windows.Threading.DispatcherPriority.Background);
        noFiles.Stop(); noFiles.Start();
    }

    /// Abre pelo teclado com os arquivos já conhecidos.
    public void ArmKeyboard(Mode m, string[] fs, Native.POINT at)
    {
        mode = m; files = fs; sel = -1; IsArmed = true; IsKeyboard = true; keySel = false; lastPick = new Point(-100, -100);
        hint.Text = Hint();
        Show();
        Place(at);
        Rebuild();
        if (ops.Count > 0) { sel = 0; Paint(); }
        Appear();
    }

    void Place(Native.POINT p)
    {
        var size = (int)Math.Round(D * Native.ScaleAt(p));
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, Native.HWND_TOPMOST, p.X - size / 2, p.Y - size / 2, size, size,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }

    void Appear()
    {
        visual.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(110)));
        var grow = new DoubleAnimation(0.86, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        zoom.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        zoom.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    public bool Contains(Native.POINT p)
    {
        Native.GetWindowRect(new WindowInteropHelper(this).Handle, out var r);
        return p.X >= r.Left && p.X < r.Right && p.Y >= r.Top && p.Y < r.Bottom;
    }

    public void Disarm()
    {
        if (!IsArmed) return;
        IsArmed = false; IsKeyboard = false; files = null; sel = -1;
        Hide();
    }

    public void SetMode(Mode m)
    {
        if (!IsArmed) return;
        mode = m;
        if (files != null) { Rebuild(); if (IsKeyboard && ops.Count > 0) { sel = 0; Paint(); } }
    }

    public void Cycle() => SetMode((Mode)(((int)mode + 1) % 3));

    /// Setas: anda pela roda no sentido do relógio (direita/baixo) ou ao contrário.
    public void Step(int d)
    {
        if (ops.Count == 0) return;
        sel = sel < 0 ? (d > 0 ? 0 : ops.Count - 1) : ((sel + d) % ops.Count + ops.Count) % ops.Count;
        keySel = true;
        Paint();
    }

    /// Enter ou clique: faz o que está escolhido. Devolve false se nada estava escolhido.
    public bool RunSelected()
    {
        var op = sel >= 0 && sel < ops.Count ? ops[sel] : null;
        var fs = files;
        if (op == null || fs == null) return false;
        Disarm();
        Dispatcher.BeginInvoke(() => runner(op, fs));
        return true;
    }

    void OnEnter(object s, DragEventArgs e)
    {
        e.Handled = true;
        if (IsKeyboard) { e.Effects = DragDropEffects.None; return; }
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetData(DataFormats.FileDrop) is not string[] fs || fs.Length == 0)
        {
            e.Effects = DragDropEffects.None;
            Dispatcher.BeginInvoke(Disarm);
            return;
        }
        bool first = files == null;
        files = fs;
        Rebuild();
        Pick(e);
        if (first) Appear();
    }

    void OnOver(object s, DragEventArgs e)
    {
        e.Handled = true;
        if (files == null || IsKeyboard) { e.Effects = DragDropEffects.None; return; }
        Pick(e);
    }

    void Pick(DragEventArgs e)
    {
        var idx = PickAt(e.GetPosition(this));
        // Copy sempre: nunca deixar o Explorer achar que é para apagar o original.
        e.Effects = idx >= 0 ? DragDropEffects.Copy : DragDropEffects.None;
    }

    int PickAt(Point p)
    {
        // O Windows repete DragOver mesmo com o mouse parado: só troca a escolha das setas se o mouse andar.
        if (keySel && (p - lastPick).Length < 6) return sel;
        keySel = false;
        lastPick = p;
        double dx = p.X - D / 2, dy = p.Y - D / 2;
        int n = ops.Count, idx = -1;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (n > 0 && dist > R1 && dist < D / 2)
        {
            double span = 360.0 / n;
            double ang = Math.Atan2(dy, dx) * 180 / Math.PI;
            idx = (int)(((ang + 90 + span / 2) % 360 + 360) % 360 / span) % n;
        }
        if (IsKeyboard && idx < 0) return sel; // pelo teclado, sair do anel não apaga a escolha
        if (idx != sel) { sel = idx; Paint(); }
        return idx;
    }

    void OnDrop(object s, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = DragDropEffects.Copy;
        var op = sel >= 0 && sel < ops.Count ? ops[sel] : null;
        var fs = files;
        Disarm();
        if (op != null && fs != null) Dispatcher.BeginInvoke(() => runner(op, fs));
    }

    void Rebuild()
    {
        ops = files == null ? new() : provider(mode, files);
        sel = -1; keySel = false;
        var c = accent[mode];
        title.Text = names[mode];
        title.Foreground = new SolidColorBrush(c);
        subtitle.Text = ops.Count == 0 ? "nada para estes arquivos"
            : files!.Length == 1 ? System.IO.Path.GetFileName(files[0].TrimEnd('\\')) : $"{files.Length} itens";
        ring.Children.Clear();
        segs.Clear();
        int n = ops.Count;
        if (n == 0) return;
        R2 = n <= 8 ? 205 : 245;
        double span = 360.0 / n, gap = n == 1 ? 0.6 : 1.1;
        double r = n <= 2 ? (R1 + R2) / 2 : (R1 + R2) / 2 + (n > 8 ? 12 : 4);
        double labelW = n <= 3 ? 120 : Math.Min(112, r * span * Math.PI / 180 - 8);
        for (int i = 0; i < n; i++)
        {
            double mid = -90 + i * span;
            var path = new Path { Data = Segment(D / 2, D / 2, R1, R2, mid - span / 2 + gap, mid + span / 2 - gap), Fill = segFill, Stroke = segStroke, StrokeThickness = 1 };
            ring.Children.Add(path);
            segs.Add(path);
            var op = ops[i];
            var label = new StackPanel { Width = labelW, IsHitTestVisible = false };
            double fs = op.Label.Length <= 5 ? 19 : op.Label.Length <= 8 ? 14.5 : 13;
            if (n > 8) fs = Math.Min(fs, op.Label.Length <= 5 ? 16 : 13);
            label.Children.Add(new TextBlock
            {
                Text = op.Label, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = fs,
                TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
            });
            if (op.Sub.Length > 0)
                label.Children.Add(new TextBlock { Text = op.Sub, Foreground = dim, FontSize = n > 8 ? 9.5 : 10.5, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            double a = mid * Math.PI / 180;
            label.Measure(new Size(labelW, 60));
            Canvas.SetLeft(label, D / 2 + r * Math.Cos(a) - labelW / 2);
            Canvas.SetTop(label, D / 2 + r * Math.Sin(a) - label.DesiredSize.Height / 2);
            ring.Children.Add(label);
        }
        Paint();
    }

    void Paint()
    {
        var c = accent[mode];
        for (int i = 0; i < segs.Count; i++)
        {
            segs[i].Fill = i == sel ? new SolidColorBrush(Color.FromArgb(245, c.R, c.G, c.B)) : segFill;
            segs[i].Stroke = i == sel ? Brushes.White : segStroke;
        }
    }

    static Geometry Segment(double cx, double cy, double r1, double r2, double a0, double a1)
    {
        Point P(double r, double a) => new(cx + r * Math.Cos(a * Math.PI / 180), cy + r * Math.Sin(a * Math.PI / 180));
        bool large = a1 - a0 > 180;
        var f = new PathFigure { StartPoint = P(r2, a0), IsClosed = true };
        f.Segments.Add(new ArcSegment(P(r2, a1), new Size(r2, r2), 0, large, SweepDirection.Clockwise, true));
        f.Segments.Add(new LineSegment(P(r1, a1), true));
        f.Segments.Add(new ArcSegment(P(r1, a0), new Size(r1, r1), 0, large, SweepDirection.Counterclockwise, true));
        var g = new PathGeometry(new[] { f });
        g.Freeze();
        return g;
    }
}
