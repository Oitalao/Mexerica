using System;
using System.Collections.Generic;
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
using ImageMagick;
using Path = System.Windows.Shapes.Path;
using Orientation = System.Windows.Controls.Orientation;
using FillRule = System.Windows.Media.FillRule;

namespace Mexerica;

/// Editor de foto: recortar, ajustar, anotar, tarjar e girar. Devolve os parâmetros; quem aplica é Images.Edit.
sealed class ImageEditor : Window
{
    enum Tool { Crop, Adjust, Pen, Arrow, Box, Text, Redact }

    readonly int W, H;
    readonly bool cropOnly;
    readonly Grid stage;                    // do tamanho da imagem, em pixels da imagem
    readonly Image photo = new() { Stretch = Stretch.Fill };
    readonly Canvas ink = new(), tarjas = new(), cropLayer = new() { IsHitTestVisible = false };
    readonly Viewbox view;
    readonly MagickImage? preview;          // cópia pequena para a prévia dos ajustes
    readonly double previewScale;
    readonly Stack<(Canvas layer, UIElement el)> undo = new();
    readonly StackPanel options = new() { Margin = new Thickness(0, 12, 0, 0) };
    readonly Dictionary<Tool, ToggleButton> toolButtons = new();
    readonly DispatcherTimer adjustTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    readonly RotateTransform rotation = new(0);

    Tool tool;
    Rect crop = Rect.Empty;
    double aspect = 0; // 0 = livre
    double brightness, contrast, saturation, sharpness;
    Color color = System.Windows.Media.Colors.Red;
    double thickness = 4;
    string redactMode = "preto";
    Point start; bool dragging; int corner = -1; Rect cropAtStart;
    UIElement? drawing;
    Polyline? pen;
    P? result;

    public static P? Ask(string src, bool cropOnly)
    {
        ImageEditor w;
        try { w = new ImageEditor(src, cropOnly); }
        catch (Exception ex) { App.Toast.Show("Não consegui abrir a imagem", ex.Message, error: true); return null; }
        return w.ShowDialog() == true ? w.result : null;
    }

    /// Recorte de vídeo: tira um quadro e recorta em cima dele.
    public static P? AskVideoCrop(string video)
    {
        var tmp = Paths.Temp();
        try
        {
            var png = System.IO.Path.Combine(tmp, "quadro.png");
            var dur = Ff.Duration(video);
            var t = Math.Min(1, dur / 2).ToString("0.###", CultureInfo.InvariantCulture);
            Mouse.OverrideCursor = Cursors.Wait;
            try { Task.Run(() => Ff.RunOut(new[] { "-ss", t, "-i", video, "-frames:v", "1", png })).GetAwaiter().GetResult(); }
            finally { Mouse.OverrideCursor = null; }
            if (!File.Exists(png)) throw new Exception("Não consegui tirar um quadro do vídeo");
            var w = new ImageEditor(png, true) { Title = "Mexerica: recortar vídeo" };
            if (w.ShowDialog() != true || w.result == null) return null;
            if (!w.result.ContainsKey("recorte")) { App.Toast.Show("Nada recortado", "Desenhe a área que deve ficar."); return null; }
            return w.result;
        }
        catch (Exception ex) { App.Toast.Show("Não deu certo", ex.Message, error: true); return null; }
        finally { Paths.TryDelete(tmp); }
    }

    ImageEditor(string src, bool cropOnly)
    {
        this.cropOnly = cropOnly;
        Title = "Mexerica: editar " + System.IO.Path.GetFileName(src);
        Width = 1180; Height = 800; MinWidth = 760; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.Dark(this);

        using (var full = Images.Load(src))
        {
            W = (int)full.Width; H = (int)full.Height;
            preview = (MagickImage)full.Clone();
            preview.Resize(new MagickGeometry(1600, 1600) { Greater = true });
            previewScale = preview.Width / (double)W;
        }
        photo.Source = ToBitmap(preview);

        stage = new Grid { Width = W, Height = H, Background = Brushes.Transparent, LayoutTransform = rotation, ClipToBounds = true };
        foreach (var c in new[] { ink, tarjas, cropLayer }) { c.Width = W; c.Height = H; }
        stage.Children.Add(photo); stage.Children.Add(tarjas); stage.Children.Add(ink); stage.Children.Add(cropLayer);
        view = new Viewbox { Child = stage, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Margin = new Thickness(16) };
        stage.MouseLeftButtonDown += Down; stage.MouseMove += Move; stage.MouseLeftButtonUp += Up;
        stage.Cursor = Cursors.Cross;

        // Barra lateral
        var side = new StackPanel { Width = 230, Margin = new Thickness(16, 16, 8, 16) };
        side.Children.Add(new TextBlock { Text = cropOnly ? "Recortar" : "Editar foto", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Ui.Orange });
        side.Children.Add(new TextBlock { Text = $"{W} × {H} px", Foreground = Ui.Dim, Margin = new Thickness(0, 2, 0, 10) });
        var tools = new WrapPanel();
        void AddTool(Tool t, string label)
        {
            var b = new ToggleButton { Content = label, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 5, 10, 5), Background = Ui.Panel, Foreground = Ui.Fg, BorderBrush = Ui.Line, Cursor = Cursors.Hand, Template = Ui.FlatToggle() };
            b.Click += (_, _) => SetTool(t);
            toolButtons[t] = b; tools.Children.Add(b);
        }
        AddTool(Tool.Crop, "Recortar");
        if (!cropOnly)
        {
            AddTool(Tool.Adjust, "Ajustar");
            AddTool(Tool.Pen, "Caneta"); AddTool(Tool.Arrow, "Seta"); AddTool(Tool.Box, "Retângulo"); AddTool(Tool.Text, "Texto");
            AddTool(Tool.Redact, "Tarjar");
        }
        side.Children.Add(tools);
        side.Children.Add(options);

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 0, 16, 16) };
        var undoB = Ui.Button("Desfazer", false); undoB.Click += (_, _) => Undo();
        var rotB = Ui.Button("Girar ⟳", false); rotB.Click += (_, _) => rotation.Angle = (rotation.Angle + 90) % 360;
        var cancel = Ui.Button("Cancelar", false); cancel.Click += (_, _) => DialogResult = false;
        var ok = Ui.Button(cropOnly ? "Salvar recorte" : "Salvar cópia", true); ok.Click += (_, _) => Finish();
        if (!cropOnly) { bottom.Children.Add(undoB); bottom.Children.Add(rotB); }
        bottom.Children.Add(cancel); bottom.Children.Add(ok);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var canvasBg = new Border { Background = new SolidColorBrush(Color.FromRgb(20, 20, 23)), Child = view };
        Grid.SetColumn(canvasBg, 1); Grid.SetRowSpan(side, 2);
        Grid.SetRow(bottom, 1); Grid.SetColumn(bottom, 1);
        grid.Children.Add(side); grid.Children.Add(canvasBg); grid.Children.Add(bottom);
        Content = grid;

        adjustTimer.Tick += (_, _) => { adjustTimer.Stop(); RefreshPreview(); };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) { Undo(); e.Handled = true; }
            if (e.Key == Key.Escape && tool == Tool.Crop && !crop.IsEmpty) { crop = Rect.Empty; DrawCrop(); e.Handled = true; }
        };
        SetTool(Tool.Crop);
    }

    static BitmapSource ToBitmap(MagickImage img)
    {
        using var c = (MagickImage)img.Clone();
        c.Format = MagickFormat.Bmp3; // rápido e sem perda para a tela
        if (c.HasAlpha) c.Format = MagickFormat.Png;
        var bytes = c.ToByteArray();
        var bi = new BitmapImage();
        bi.BeginInit(); bi.StreamSource = new MemoryStream(bytes); bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit(); bi.Freeze();
        return bi;
    }

    double Scale()
    {
        var a = stage.TranslatePoint(new Point(0, 0), this);
        var b = stage.TranslatePoint(new Point(100, 0), this);
        return Math.Max(0.0001, (b - a).Length / 100);
    }

    double Px(double screen) => screen / Scale();

    void SetTool(Tool t)
    {
        tool = t;
        foreach (var (k, b) in toolButtons) { b.IsChecked = k == t; b.Background = k == t ? Ui.Orange : Ui.Panel; b.Foreground = k == t ? Brushes.Black : Ui.Fg; }
        options.Children.Clear();
        stage.Cursor = t is Tool.Adjust ? Cursors.Arrow : t == Tool.Text ? Cursors.IBeam : Cursors.Cross;
        switch (t)
        {
            case Tool.Crop:
                options.Children.Add(Hint("Arraste sobre a foto para marcar o que fica. Arraste dentro para mover, pelos cantos para ajustar. Esc limpa."));
                options.Children.Add(Ui.Stack("Proporção", Ui.Combo(new[] { ("0", "Livre"), ("o", "Original"), ("1", "1:1"), ("1.3333", "4:3"), ("1.5", "3:2"), ("1.7778", "16:9"), ("0.8", "4:5"), ("0.5625", "9:16") },
                    aspect == 0 ? "0" : aspect.ToString("0.####", CultureInfo.InvariantCulture), v =>
                    {
                        aspect = v == "o" ? W / (double)H : double.Parse(v, CultureInfo.InvariantCulture);
                        if (aspect > 0 && !crop.IsEmpty) { crop = FitAspect(crop); DrawCrop(); }
                    })));
                break;
            case Tool.Adjust:
                options.Children.Add(Slider("Brilho", -100, 100, brightness, v => brightness = v));
                options.Children.Add(Slider("Contraste", -100, 100, contrast, v => contrast = v));
                options.Children.Add(Slider("Saturação", -100, 100, saturation, v => saturation = v));
                options.Children.Add(Slider("Nitidez", 0, 100, sharpness, v => sharpness = v));
                var reset = Ui.Button("Zerar ajustes", false); reset.Margin = new Thickness(0, 8, 0, 0); reset.HorizontalAlignment = HorizontalAlignment.Left;
                reset.Click += (_, _) => { brightness = contrast = saturation = sharpness = 0; RefreshPreview(); SetTool(Tool.Adjust); };
                options.Children.Add(reset);
                break;
            case Tool.Pen or Tool.Arrow or Tool.Box or Tool.Text:
                options.Children.Add(Hint(t switch { Tool.Pen => "Desenhe à mão livre.", Tool.Arrow => "Arraste do começo para a ponta da seta.", Tool.Box => "Arraste para desenhar o retângulo.", _ => "Clique onde o texto deve ficar." }));
                options.Children.Add(ColorPicker());
                options.Children.Add(Slider(t == Tool.Text ? "Tamanho do texto" : "Espessura", 1, 20, thickness, v => thickness = v, false));
                break;
            case Tool.Redact:
                options.Children.Add(Hint("Arraste sobre o que deve sumir."));
                options.Children.Add(Ui.Stack("Tipo", Ui.Combo(new[] { ("preto", "Tarja preta"), ("desfoque", "Desfocar"), ("pixel", "Pixelar") }, redactMode, v => redactMode = v)));
                break;
        }
    }

    static TextBlock Hint(string t) => new() { Text = t, Foreground = Ui.Dim, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 8) };

    FrameworkElement Slider(string label, double min, double max, double value, Action<double> set, bool preview = true)
    {
        var st = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        var t = new TextBlock { Text = $"{label}: {value:0}" };
        var s = new Slider { Minimum = min, Maximum = max, Value = value, IsSnapToTickEnabled = true, TickFrequency = 1 };
        s.ValueChanged += (_, e) => { set(e.NewValue); t.Text = $"{label}: {e.NewValue:0}"; if (preview) { adjustTimer.Stop(); adjustTimer.Start(); } };
        st.Children.Add(t); st.Children.Add(s);
        return st;
    }

    FrameworkElement ColorPicker()
    {
        var wp = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        foreach (var c in new[] { System.Windows.Media.Colors.Red, Color.FromRgb(255, 138, 31), System.Windows.Media.Colors.Yellow, Color.FromRgb(60, 203, 127), Color.FromRgb(58, 168, 255), System.Windows.Media.Colors.White, System.Windows.Media.Colors.Black })
        {
            var b = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(c), Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand, BorderThickness = new Thickness(c == color ? 3 : 1), BorderBrush = c == color ? Ui.Orange : Ui.Line };
            var cc = c;
            b.MouseLeftButtonUp += (_, _) => { color = cc; SetTool(tool); };
            wp.Children.Add(b);
        }
        return wp;
    }

    void RefreshPreview()
    {
        if (preview == null) return;
        using var img = (MagickImage)preview.Clone();
        Images.Adjust(img, brightness, contrast, saturation, sharpness * previewScale);
        photo.Source = ToBitmap(img);
    }

    // ---------- Mouse ----------
    Point Clamp(Point p) => new(Math.Clamp(p.X, 0, W), Math.Clamp(p.Y, 0, H));

    void Down(object s, MouseButtonEventArgs e)
    {
        var p = Clamp(e.GetPosition(stage));
        start = p; dragging = true;
        stage.CaptureMouse();
        double ln = Math.Max(W, H);
        double th = thickness * ln / 800;
        var brush = new SolidColorBrush(color);
        switch (tool)
        {
            case Tool.Crop:
                corner = CropHit(p);
                cropAtStart = crop;
                if (corner < 0) { crop = new Rect(p, new Size(0, 0)); }
                break;
            case Tool.Pen:
                pen = new Polyline { Stroke = brush, StrokeThickness = th, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
                pen.Points.Add(p);
                Add(ink, pen); drawing = pen;
                break;
            case Tool.Arrow:
                drawing = new Path { Fill = brush, Stroke = brush, StrokeThickness = th, StrokeLineJoin = PenLineJoin.Round };
                Add(ink, drawing);
                break;
            case Tool.Box:
                drawing = new Rectangle { Stroke = brush, StrokeThickness = th };
                Canvas.SetLeft(drawing, p.X); Canvas.SetTop(drawing, p.Y);
                Add(ink, drawing);
                break;
            case Tool.Redact:
                drawing = new Rectangle
                {
                    Fill = redactMode == "preto" ? Brushes.Black : new SolidColorBrush(Color.FromArgb(150, 120, 120, 130)),
                    Stroke = redactMode == "preto" ? Brushes.Black : Brushes.White, StrokeThickness = Px(1.5),
                    StrokeDashArray = redactMode == "preto" ? null : new DoubleCollection { 4, 3 }, Tag = redactMode,
                };
                Canvas.SetLeft(drawing, p.X); Canvas.SetTop(drawing, p.Y);
                Add(tarjas, drawing);
                break;
            case Tool.Text:
                dragging = false; stage.ReleaseMouseCapture();
                var size = thickness * ln / 160;
                // abre a caixa depois que o clique terminar, para não prender o mouse
                Dispatcher.BeginInvoke(() => AddText(p, brush, size), DispatcherPriority.Input);
                break;
        }
    }

    /// Onde o clique caiu no recorte: 0-3 canto (ajustar), 4 dentro (mover), -1 fora (recorte novo).
    /// A zona de canto nunca passa de 1/3 do lado menor, então o centro é sempre área de mover (como no Canva).
    int CropHit(Point p)
    {
        if (crop.IsEmpty) return -1;
        var tol = Math.Min(Px(14), Math.Min(crop.Width, crop.Height) / 3);
        var pts = new[] { crop.TopLeft, crop.TopRight, crop.BottomRight, crop.BottomLeft };
        for (int i = 0; i < 4; i++) if ((pts[i] - p).Length < tol) return i;
        return crop.Contains(p) ? 4 : -1;
    }

    void Move(object s, MouseEventArgs e)
    {
        var p = Clamp(e.GetPosition(stage));
        if (!dragging)
        {
            // Sem arrastar: o cursor mostra o que dá para fazer ali (mover dentro, ajustar nos cantos).
            if (tool == Tool.Crop)
                stage.Cursor = CropHit(p) switch
                {
                    4 => Cursors.SizeAll,
                    0 or 2 => Cursors.SizeNWSE,
                    1 or 3 => Cursors.SizeNESW,
                    _ => Cursors.Cross,
                };
            return;
        }
        switch (tool)
        {
            case Tool.Crop:
                if (corner == 4)
                {
                    var d = p - start;
                    var r = cropAtStart; r.Offset(d);
                    r.X = Math.Clamp(r.X, 0, W - r.Width); r.Y = Math.Clamp(r.Y, 0, H - r.Height);
                    crop = r;
                }
                else
                {
                    Point anchor = corner switch { 0 => cropAtStart.BottomRight, 1 => cropAtStart.BottomLeft, 2 => cropAtStart.TopLeft, 3 => cropAtStart.TopRight, _ => start };
                    crop = Span(anchor, p);
                }
                DrawCrop();
                break;
            case Tool.Pen: pen?.Points.Add(p); break;
            case Tool.Arrow when drawing is Path path:
                path.Data = Arrow(start, p, Math.Max(W, H) * thickness / 800);
                break;
            case Tool.Box or Tool.Redact when drawing is Rectangle rect:
                var rr = new Rect(start, p);
                Canvas.SetLeft(rect, rr.X); Canvas.SetTop(rect, rr.Y); rect.Width = rr.Width; rect.Height = rr.Height;
                break;
        }
    }

    void Up(object s, MouseButtonEventArgs e)
    {
        if (!dragging) return;
        dragging = false; stage.ReleaseMouseCapture();
        if (tool == Tool.Crop && (crop.Width < 4 || crop.Height < 4)) { crop = corner >= 0 ? cropAtStart : Rect.Empty; DrawCrop(); }
        if (drawing is Rectangle r && (r.Width < 3 || r.Height < 3)) { Remove(drawing); }
        if (drawing is Path path && path.Data == null) Remove(drawing);
        drawing = null; pen = null;
    }

    Rect Span(Point a, Point b)
    {
        double w = Math.Abs(b.X - a.X), h = Math.Abs(b.Y - a.Y);
        if (aspect > 0) { if (w / Math.Max(h, 0.01) > aspect) w = h * aspect; else h = w / aspect; }
        double x = b.X < a.X ? a.X - w : a.X, y = b.Y < a.Y ? a.Y - h : a.Y;
        var r = new Rect(x, y, w, h);
        r.Intersect(new Rect(0, 0, W, H));
        return r.IsEmpty ? new Rect(a, new Size(0, 0)) : r;
    }

    Rect FitAspect(Rect r)
    {
        var c = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
        double w = r.Width, h = r.Height;
        if (w / h > aspect) w = h * aspect; else h = w / aspect;
        var n = new Rect(c.X - w / 2, c.Y - h / 2, w, h);
        n.Intersect(new Rect(0, 0, W, H));
        return n;
    }

    void DrawCrop()
    {
        cropLayer.Children.Clear();
        if (crop.IsEmpty || crop.Width < 1) return;
        var outside = new GeometryGroup { FillRule = FillRule.EvenOdd };
        outside.Children.Add(new RectangleGeometry(new Rect(0, 0, W, H)));
        outside.Children.Add(new RectangleGeometry(crop));
        cropLayer.Children.Add(new Path { Data = outside, Fill = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)) });
        var border = new Rectangle { Width = crop.Width, Height = crop.Height, Stroke = Brushes.White, StrokeThickness = Px(1.5) };
        Canvas.SetLeft(border, crop.X); Canvas.SetTop(border, crop.Y);
        cropLayer.Children.Add(border);
        double hs = Px(10);
        foreach (var pt in new[] { crop.TopLeft, crop.TopRight, crop.BottomRight, crop.BottomLeft })
        {
            var h = new Rectangle { Width = hs, Height = hs, Fill = Brushes.White };
            Canvas.SetLeft(h, pt.X - hs / 2); Canvas.SetTop(h, pt.Y - hs / 2);
            cropLayer.Children.Add(h);
        }
        var label = new TextBlock { Text = $"{crop.Width:0} × {crop.Height:0}", Foreground = Brushes.White, FontSize = Px(13), Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), Padding = new Thickness(Px(4), 0, Px(4), 0) };
        Canvas.SetLeft(label, crop.X + Px(4)); Canvas.SetTop(label, crop.Y + Px(4));
        cropLayer.Children.Add(label);
    }

    static Geometry Arrow(Point a, Point b, double th)
    {
        var v = b - a;
        if (v.Length < 2) return Geometry.Empty;
        v.Normalize();
        var n = new Vector(-v.Y, v.X);
        double head = Math.Max(th * 4, 12);
        var baseP = b - v * head;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(a, false, false); c.LineTo(baseP, true, true);
            c.BeginFigure(b, true, true); c.LineTo(baseP + n * head * 0.55, true, true); c.LineTo(baseP - n * head * 0.55, true, true);
        }
        g.Freeze();
        return g;
    }

    void AddText(Point p, Brush brush, double size)
    {
        var f = new Form("Texto", new[] { "anotação" });
        f.Text("t", "Texto", "", validate: v => v.Trim().Length == 0 ? "Escreva o texto." : null, width: 240);
        var r = f.Run();
        if (r == null) return;
        var tb = new TextBlock { Text = r.S("t"), Foreground = brush, FontSize = Math.Max(8, size), FontWeight = FontWeights.SemiBold, FontFamily = new FontFamily("Segoe UI") };
        tb.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = color.R + color.G + color.B > 380 ? System.Windows.Media.Colors.Black : System.Windows.Media.Colors.White, BlurRadius = size / 6, ShadowDepth = 0, Opacity = 0.9 };
        Canvas.SetLeft(tb, p.X); Canvas.SetTop(tb, p.Y);
        Add(ink, tb);
    }

    void Add(Canvas layer, UIElement el) { layer.Children.Add(el); undo.Push((layer, el)); }
    void Remove(UIElement el) { ink.Children.Remove(el); tarjas.Children.Remove(el); }

    void Undo()
    {
        if (undo.Count == 0) return;
        var (layer, el) = undo.Pop();
        layer.Children.Remove(el);
    }

    // ---------- Final ----------
    void Finish()
    {
        var p = new P();
        bool changed = false;
        if (!crop.IsEmpty && crop.Width >= 2 && crop.Height >= 2 && (crop.Width < W - 0.5 || crop.Height < H - 0.5))
        {
            p["recorte"] = string.Join(",", new[] { crop.X, crop.Y, crop.Width, crop.Height }.Select(v => Math.Round(v).ToString(CultureInfo.InvariantCulture)));
            changed = true;
        }
        if (cropOnly) { result = p; DialogResult = true; return; }
        var tj = new List<string>();
        foreach (var r in tarjas.Children.OfType<Rectangle>())
            tj.Add(string.Join(",", new[] { Canvas.GetLeft(r), Canvas.GetTop(r), r.Width, r.Height }.Select(v => Math.Round(v).ToString(CultureInfo.InvariantCulture))) + "," + r.Tag);
        if (tj.Count > 0) { p["tarjas"] = string.Join(";", tj); changed = true; }
        void Num(string k, double v) { if (Math.Abs(v) > 0.01) { p[k] = v.ToString(CultureInfo.InvariantCulture); changed = true; } }
        Num("brilho", brightness); Num("contraste", contrast); Num("saturacao", saturation); Num("nitidez", sharpness);
        if (rotation.Angle != 0) { p["giro"] = ((int)rotation.Angle).ToString(); changed = true; }
        if (ink.Children.Count > 0)
        {
            var dir = Paths.Temp();
            var file = System.IO.Path.Combine(dir, "camada.png");
            SaveLayer(file);
            p["camada"] = file; p["_tmp"] = dir;
            changed = true;
        }
        if (!changed) { MessageBox.Show(this, "Nada foi alterado ainda.", "Mexerica", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        result = p; DialogResult = true;
    }

    void SaveLayer(string file)
    {
        double s = Math.Min(1, Math.Sqrt(40_000_000.0 / ((double)W * H)));
        int w = Math.Max(1, (int)(W * s)), h = Math.Max(1, (int)(H * s));
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            dc.DrawRectangle(new VisualBrush(ink) { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, W, H) }, null, new Rect(0, 0, w, h));
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(file);
        enc.Save(fs);
    }
}
