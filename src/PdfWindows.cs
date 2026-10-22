using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace Mexerica;

static class PdfThumbs
{
    public static async Task<BitmapImage> Render(Windows.Data.Pdf.PdfDocument doc, uint i, uint width)
    {
        using var page = doc.GetPage(i);
        var o = new Windows.Data.Pdf.PdfPageRenderOptions { DestinationWidth = width, BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255) };
        using var ras = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(ras, o);
        ras.Seek(0);
        var ms = new MemoryStream();
        await ras.AsStream().CopyToAsync(ms);
        ms.Position = 0;
        var bi = new BitmapImage();
        bi.BeginInit(); bi.StreamSource = ms; bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit(); bi.Freeze();
        return bi;
    }
}

/// Organizar páginas: arrastar para reordenar, girar, apagar, duplicar.
sealed class PdfPagesWindow : Window
{
    sealed class Item { public int Page; public int Rot; public BitmapImage? Thumb; }

    readonly List<Item> items = new();
    readonly List<Item> original = new();
    readonly HashSet<Item> selected = new();
    readonly WrapPanel panel = new() { Margin = new Thickness(8) };
    readonly TextBlock info = new() { Foreground = Ui.Dim, VerticalAlignment = VerticalAlignment.Center };
    Item? anchor;
    Point downAt; Item? pressed;
    P? result;

    public static P? Ask(string src)
    {
        var w = new PdfPagesWindow(src);
        return w.ShowDialog() == true ? w.result : null;
    }

    PdfPagesWindow(string src)
    {
        Title = "Mexerica: organizar páginas";
        Width = 980; Height = 720; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.Dark(this);
        var top = new StackPanel { Margin = new Thickness(18, 14, 18, 6) };
        top.Children.Add(new TextBlock { Text = "Organizar páginas", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Ui.Orange });
        top.Children.Add(new TextBlock { Text = Path.GetFileName(src) + ". Arraste para mudar a ordem; clique para selecionar (Ctrl e Shift para várias).", Foreground = Ui.Dim, TextWrapping = TextWrapping.Wrap });
        var tools = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        Button B(string t, Action a) { var b = Ui.Button(t, false); b.Margin = new Thickness(0, 0, 6, 6); b.Click += (_, _) => { a(); Redraw(); }; tools.Children.Add(b); return b; }
        B("Girar ⟳", () => Rotate(90));
        B("Girar ⟲", () => Rotate(270));
        B("Apagar", () => { foreach (var i in Sel()) items.Remove(i); selected.Clear(); });
        B("Duplicar", () => { foreach (var i in Sel().OrderByDescending(items.IndexOf)) items.Insert(items.IndexOf(i) + 1, new Item { Page = i.Page, Rot = i.Rot, Thumb = i.Thumb }); });
        B("◀ Mover", () => MoveSel(-1));
        B("Mover ▶", () => MoveSel(1));
        B("Inverter ordem", () => items.Reverse());
        B("Selecionar todas", () => { selected.Clear(); foreach (var i in items) selected.Add(i); });
        B("Voltar ao original", () => { items.Clear(); items.AddRange(original.Select(o => new Item { Page = o.Page, Thumb = o.Thumb })); selected.Clear(); });
        top.Children.Add(tools);

        var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = new SolidColorBrush(Color.FromRgb(20, 20, 23)) };
        var bottom = new DockPanel { Margin = new Thickness(18, 10, 18, 14) };
        var ok = Ui.Button("Salvar cópia", true); var cancel = Ui.Button("Cancelar", false);
        ok.Click += (_, _) => Finish(); cancel.Click += (_, _) => DialogResult = false;
        var btns = new StackPanel { Orientation = Orientation.Horizontal }; btns.Children.Add(cancel); btns.Children.Add(ok);
        DockPanel.SetDock(btns, Dock.Right);
        bottom.Children.Add(btns); bottom.Children.Add(info);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(scroll, 1); Grid.SetRow(bottom, 2);
        grid.Children.Add(top); grid.Children.Add(scroll); grid.Children.Add(bottom);
        Content = grid;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete) { foreach (var i in Sel()) items.Remove(i); selected.Clear(); Redraw(); }
            else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control) { foreach (var i in items) selected.Add(i); Redraw(); }
        };
        Loaded += async (_, _) => await Load(src);
    }

    async Task Load(string src)
    {
        try
        {
            var doc = await Pdf.Open(src);
            for (uint i = 0; i < doc.PageCount; i++) original.Add(new Item { Page = (int)i + 1 });
            items.AddRange(original.Select(o => new Item { Page = o.Page }));
            Redraw();
            for (uint i = 0; i < doc.PageCount; i++)
            {
                var t = await PdfThumbs.Render(doc, i, 220);
                original[(int)i].Thumb = t;
                foreach (var it in items.Where(x => x.Page == i + 1)) it.Thumb = t;
                if (i % 6 == 5 || i == doc.PageCount - 1) Redraw();
            }
        }
        catch (Exception ex) { Tr.Box(this, $"Não consegui abrir o PDF: {ex.Message}", "Mexerica"); DialogResult = false; }
    }

    IEnumerable<Item> Sel() => selected.Count > 0 ? items.Where(selected.Contains).ToList() : Enumerable.Empty<Item>();

    void Rotate(int d)
    {
        var targets = selected.Count > 0 ? Sel() : items;
        foreach (var i in targets) i.Rot = (i.Rot + d) % 360;
    }

    void MoveSel(int dir)
    {
        var sel = Sel().ToList();
        if (sel.Count == 0) return;
        var order = dir < 0 ? sel.OrderBy(items.IndexOf) : sel.OrderByDescending(items.IndexOf);
        foreach (var i in order)
        {
            int idx = items.IndexOf(i), to = idx + dir;
            if (to < 0 || to >= items.Count || selected.Contains(items[to])) continue;
            items.RemoveAt(idx); items.Insert(to, i);
        }
    }

    void Redraw()
    {
        panel.Children.Clear();
        for (int n = 0; n < items.Count; n++)
        {
            var it = items[n];
            var img = new Image { Source = it.Thumb, Width = 150, Height = 200, Stretch = Stretch.Uniform, LayoutTransform = new RotateTransform(it.Rot) };
            var label = new TextBlock { Text = $"{n + 1}" + (it.Page != n + 1 ? $"  (era {it.Page})" : "") + (it.Rot != 0 ? $"  {it.Rot}°" : ""), Foreground = Ui.Fg, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
            var st = new StackPanel(); st.Children.Add(new Grid { Width = 200, Height = 200, Children = { img } }); st.Children.Add(label);
            var tile = new Border
            {
                Child = st, Margin = new Thickness(8), Padding = new Thickness(8), CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand, AllowDrop = true,
                Background = selected.Contains(it) ? new SolidColorBrush(Color.FromArgb(90, 255, 138, 31)) : Ui.Panel,
                BorderBrush = selected.Contains(it) ? Ui.Orange : Ui.Line, BorderThickness = new Thickness(2), Tag = it,
            };
            tile.MouseLeftButtonDown += (_, e) => { pressed = it; downAt = e.GetPosition(this); };
            tile.MouseLeftButtonUp += (_, _) => { if (pressed == it) Click(it); pressed = null; };
            tile.MouseMove += (_, e) =>
            {
                if (pressed != it || e.LeftButton != MouseButtonState.Pressed) return;
                if ((e.GetPosition(this) - downAt).Length < 6) return;
                pressed = null;
                if (!selected.Contains(it)) { selected.Clear(); selected.Add(it); }
                DragDrop.DoDragDrop(tile, new DataObject("mexerica.paginas", it), DragDropEffects.Move);
            };
            tile.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent("mexerica.paginas") ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
            tile.Drop += (_, e) =>
            {
                e.Handled = true;
                if (!e.Data.GetDataPresent("mexerica.paginas")) return;
                var moving = Sel().ToList();
                if (moving.Contains(it)) return;
                bool after = e.GetPosition(tile).X > tile.ActualWidth / 2;
                foreach (var m in moving) items.Remove(m);
                int at = items.IndexOf(it) + (after ? 1 : 0);
                items.InsertRange(at, moving);
                Redraw();
            };
            panel.Children.Add(tile);
        }
        info.Text = $"{items.Count} páginas" + (selected.Count > 0 ? $", {selected.Count} selecionadas" : "");
    }

    void Click(Item it)
    {
        var mod = Keyboard.Modifiers;
        if (mod.HasFlag(ModifierKeys.Shift) && anchor != null && items.Contains(anchor))
        {
            int a = items.IndexOf(anchor), b = items.IndexOf(it);
            if (!mod.HasFlag(ModifierKeys.Control)) selected.Clear();
            for (int i = Math.Min(a, b); i <= Math.Max(a, b); i++) selected.Add(items[i]);
        }
        else if (mod.HasFlag(ModifierKeys.Control)) { if (!selected.Remove(it)) selected.Add(it); anchor = it; }
        else { bool only = selected.Count == 1 && selected.Contains(it); selected.Clear(); if (!only) selected.Add(it); anchor = it; }
        Redraw();
    }

    void Finish()
    {
        if (items.Count == 0) { Tr.Box(this, "Não sobrou nenhuma página.", "Mexerica"); return; }
        bool same = items.Count == original.Count && items.Select((x, i) => x.Page == i + 1 && x.Rot == 0).All(b => b);
        if (same) { Tr.Box(this, "Nada foi alterado ainda.", "Mexerica"); return; }
        result = new P { ["ordem"] = string.Join(",", items.Select(i => i.Rot != 0 ? $"{i.Page}r{i.Rot}" : i.Page.ToString())) };
        DialogResult = true;
    }
}

/// Tarjar PDF: desenhar retângulos por página.
sealed class PdfRedactWindow : Window
{
    readonly Dictionary<int, List<Rect>> marks = new(); // em fração da página
    readonly Image page = new() { Stretch = Stretch.Fill };
    readonly Canvas layer = new() { Background = Brushes.Transparent, Cursor = Cursors.Cross };
    readonly Grid stage = new();
    readonly TextBlock pageLabel = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
    Windows.Data.Pdf.PdfDocument? doc;
    int current = 1, count;
    Point start; Rectangle? drawing;
    P? result;

    public static P? Ask(string src)
    {
        var w = new PdfRedactWindow(src);
        return w.ShowDialog() == true ? w.result : null;
    }

    PdfRedactWindow(string src)
    {
        Title = "Mexerica: tarjar PDF";
        Width = 900; Height = 860; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.Dark(this);
        var top = new StackPanel { Margin = new Thickness(18, 14, 18, 6) };
        top.Children.Add(new TextBlock { Text = "Tarjar", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Ui.Orange });
        top.Children.Add(new TextBlock { Text = "Arraste sobre o que deve sumir. As páginas tarjadas viram imagem: o texto escondido some de verdade, não dá para copiar nem buscar.", Foreground = Ui.Dim, TextWrapping = TextWrapping.Wrap });
        var nav = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var prev = Ui.Button("◀", false); prev.Margin = new Thickness(0); prev.MinWidth = 44;
        var next = Ui.Button("▶", false); next.Margin = new Thickness(0); next.MinWidth = 44;
        var undo = Ui.Button("Desfazer", false); var clear = Ui.Button("Limpar página", false);
        prev.Click += async (_, _) => await Show(current - 1);
        next.Click += async (_, _) => await Show(current + 1);
        undo.Click += (_, _) => { if (marks.TryGetValue(current, out var l) && l.Count > 0) { l.RemoveAt(l.Count - 1); DrawMarks(); } };
        clear.Click += (_, _) => { marks.Remove(current); DrawMarks(); };
        nav.Children.Add(prev); nav.Children.Add(pageLabel); nav.Children.Add(next); nav.Children.Add(undo); nav.Children.Add(clear);
        top.Children.Add(nav);

        stage.Children.Add(page); stage.Children.Add(layer);
        var view = new Viewbox { Child = stage, Margin = new Thickness(12) };
        var bg = new Border { Background = new SolidColorBrush(Color.FromRgb(20, 20, 23)), Child = view };
        var ok = Ui.Button("Salvar cópia tarjada", true); var cancel = Ui.Button("Cancelar", false);
        ok.Click += (_, _) => Finish(); cancel.Click += (_, _) => DialogResult = false;
        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(18, 10, 18, 14) };
        btns.Children.Add(cancel); btns.Children.Add(ok);
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(bg, 1); Grid.SetRow(btns, 2);
        grid.Children.Add(top); grid.Children.Add(bg); grid.Children.Add(btns);
        Content = grid;

        layer.MouseLeftButtonDown += (_, e) =>
        {
            start = e.GetPosition(layer);
            drawing = new Rectangle { Fill = Brushes.Black, Opacity = 0.85 };
            Canvas.SetLeft(drawing, start.X); Canvas.SetTop(drawing, start.Y);
            layer.Children.Add(drawing); layer.CaptureMouse();
        };
        layer.MouseMove += (_, e) =>
        {
            if (drawing == null) return;
            var p = e.GetPosition(layer);
            p = new Point(Math.Clamp(p.X, 0, layer.Width), Math.Clamp(p.Y, 0, layer.Height));
            var r = new Rect(start, p);
            Canvas.SetLeft(drawing, r.X); Canvas.SetTop(drawing, r.Y); drawing.Width = r.Width; drawing.Height = r.Height;
        };
        layer.MouseLeftButtonUp += (_, _) =>
        {
            if (drawing == null) return;
            layer.ReleaseMouseCapture();
            if (drawing.Width > 2 && drawing.Height > 2)
            {
                if (!marks.TryGetValue(current, out var l)) marks[current] = l = new();
                l.Add(new Rect(Canvas.GetLeft(drawing) / layer.Width, Canvas.GetTop(drawing) / layer.Height, drawing.Width / layer.Width, drawing.Height / layer.Height));
            }
            drawing = null; DrawMarks();
        };
        KeyDown += async (_, e) =>
        {
            if (e.Key is Key.PageDown or Key.Right) await Show(current + 1);
            else if (e.Key is Key.PageUp or Key.Left) await Show(current - 1);
            else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) undo.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        };
        Loaded += async (_, _) =>
        {
            try { doc = await Pdf.Open(src); count = (int)doc.PageCount; await Show(1); }
            catch (Exception ex) { Tr.Box(this, $"Não consegui abrir o PDF: {ex.Message}", "Mexerica"); DialogResult = false; }
        };
    }

    async Task Show(int n)
    {
        if (doc == null || n < 1 || n > count) return;
        current = n;
        var bmp = await PdfThumbs.Render(doc, (uint)(n - 1), 1100);
        page.Source = bmp;
        stage.Width = layer.Width = bmp.PixelWidth; stage.Height = layer.Height = bmp.PixelHeight;
        pageLabel.Text = $"Página {n} de {count}  ·  {marks.Values.Sum(l => l.Count)} tarjas";
        DrawMarks();
    }

    void DrawMarks()
    {
        layer.Children.Clear();
        if (marks.TryGetValue(current, out var l))
            foreach (var r in l)
            {
                var rect = new Rectangle { Fill = Brushes.Black, Width = r.Width * layer.Width, Height = r.Height * layer.Height };
                Canvas.SetLeft(rect, r.X * layer.Width); Canvas.SetTop(rect, r.Y * layer.Height);
                layer.Children.Add(rect);
            }
        pageLabel.Text = $"Página {current} de {count}  ·  {marks.Values.Sum(x => x.Count)} tarjas";
    }

    void Finish()
    {
        var all = marks.Where(kv => kv.Value.Count > 0).SelectMany(kv => kv.Value.Select(r => $"{kv.Key}:{F(r.X)},{F(r.Y)},{F(r.Width)},{F(r.Height)}")).ToList();
        if (all.Count == 0) { Tr.Box(this, "Nenhuma tarja marcada.", "Mexerica"); return; }
        result = new P { ["tarjas"] = string.Join(";", all) };
        DialogResult = true;
    }

    static string F(double d) => d.ToString("0.#####", CultureInfo.InvariantCulture);
}
