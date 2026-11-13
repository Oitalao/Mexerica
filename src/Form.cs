using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Mexerica;

/// Janelinha de ajuste montada por código: escolhas, números e textos, com campos que aparecem conforme outra escolha.
sealed class Form : Window
{
    readonly StackPanel body = new();
    readonly P values = new();
    readonly List<(FrameworkElement row, string key, string[] vals)> conds = new();
    readonly List<Func<string?>> checks = new();

    public Form(string title, string[] files, string? note = null)
    {
        Title = "Mexerica: " + Tr.T(title);
        Width = 440; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual; Topmost = true; ShowInTaskbar = true;
        Ui.Dark(this);
        var st = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
        st.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Ui.Orange });
        st.Children.Add(new TextBlock
        {
            Text = files.Length == 1 ? Path.GetFileName(files[0].TrimEnd('\\')) : $"{files.Length} arquivos",
            Foreground = Ui.Dim, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 8),
        });
        if (note != null) st.Children.Add(new TextBlock { Text = note, Foreground = Ui.Dim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        st.Children.Add(body);
        var ok = Ui.Button("Fazer", true);
        var cancel = Ui.Button("Cancelar", false);
        ok.Click += (_, _) =>
        {
            foreach (var c in checks) { var err = c(); if (err != null) { Tr.Box(this, err, "Mexerica", MessageBoxButton.OK, MessageBoxImage.Warning); return; } }
            DialogResult = true;
        };
        cancel.Click += (_, _) => DialogResult = false;
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        bar.Children.Add(cancel); bar.Children.Add(ok);
        st.Children.Add(bar);
        Content = st;
        Loaded += (_, _) => PlaceNearCursor(this);
    }

    public static void PlaceNearCursor(Window w)
    {
        Native.GetCursorPos(out var p);
        var k = Native.ScaleAt(p);
        var wa = SystemParameters.WorkArea;
        double x = p.X / k - w.ActualWidth / 2, y = p.Y / k - w.ActualHeight / 3;
        w.Left = Math.Max(wa.Left + 8, Math.Min(x, wa.Right - w.ActualWidth - 8));
        w.Top = Math.Max(wa.Top + 8, Math.Min(y, wa.Bottom - w.ActualHeight - 8));
        w.Activate();
    }

    void Add(FrameworkElement row, (string key, string[] vals)? showIf)
    {
        body.Children.Add(row);
        if (showIf is { } s) conds.Add((row, s.key, s.vals));
        Refresh();
    }

    void Refresh()
    {
        foreach (var (row, key, vals) in conds)
            row.Visibility = vals.Contains(values.S(key)) ? Visibility.Visible : Visibility.Collapsed;
    }

    bool Visible(string key) => !conds.Any(c => c.row.Tag as string == key && c.row.Visibility != Visibility.Visible);

    public Form Choice(string key, string label, (string value, string label)[] options, string def, (string key, string[] vals)? showIf = null)
    {
        values[key] = def;
        var cb = Ui.Combo(options, def, v => { values[key] = v; Refresh(); });
        var row = Ui.Row(label, cb); row.Tag = key;
        Add(row, showIf);
        return this;
    }

    public Form Number(string key, string label, double def, double min, double max, string suffix = "", (string key, string[] vals)? showIf = null, bool decimals = false)
    {
        values[key] = def.ToString(CultureInfo.InvariantCulture);
        var tb = Ui.Text(def.ToString(decimals ? "0.##" : "0", CultureInfo.GetCultureInfo("pt-BR")), 90);
        tb.TextChanged += (_, _) => values[key] = tb.Text.Trim().Replace(',', '.');
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(tb);
        if (suffix.Length > 0) sp.Children.Add(new TextBlock { Text = suffix, Foreground = Ui.Dim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
        var row = Ui.Row(label, sp); row.Tag = key;
        Add(row, showIf);
        checks.Add(() =>
        {
            if (row.Visibility != Visibility.Visible) return null;
            if (!double.TryParse(values.S(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || d < min || d > max)
                return $"{label}: use um número entre {min.ToString(CultureInfo.GetCultureInfo("pt-BR"))} e {max.ToString(CultureInfo.GetCultureInfo("pt-BR"))}.";
            return null;
        });
        return this;
    }

    public Form Text(string key, string label, string def, (string key, string[] vals)? showIf = null, Func<string, string?>? validate = null, double width = 200)
    {
        values[key] = def;
        var tb = Ui.Text(def, width);
        tb.TextChanged += (_, _) => values[key] = tb.Text;
        var row = Ui.Row(label, tb); row.Tag = key;
        Add(row, showIf);
        if (validate != null) checks.Add(() => row.Visibility == Visibility.Visible ? validate(values.S(key)) : null);
        return this;
    }

    public Form Color(string key, string label, string def, (string key, string[] vals)? showIf = null)
    {
        values[key] = def;
        var sw = new Border { Width = 26, Height = 22, CornerRadius = new CornerRadius(4), BorderBrush = Ui.Line, BorderThickness = new Thickness(1), Margin = new Thickness(6, 0, 0, 0) };
        void Paint() { try { sw.Background = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(values.S(key))); } catch { } }
        var tb = Ui.Text(def, 90);
        tb.TextChanged += (_, _) => { values[key] = tb.Text.Trim(); Paint(); };
        var pick = Ui.Button("…", false); pick.MinWidth = 30; pick.Padding = new Thickness(6, 2, 6, 2);
        pick.Click += (_, _) =>
        {
            using var d = new System.Windows.Forms.ColorDialog { FullOpen = true };
            try { var c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(values.S(key)); d.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B); } catch { }
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) tb.Text = $"#{d.Color.R:X2}{d.Color.G:X2}{d.Color.B:X2}";
        };
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(tb); sp.Children.Add(sw); sp.Children.Add(pick);
        Paint();
        var row = Ui.Row(label, sp); row.Tag = key;
        Add(row, showIf);
        return this;
    }

    public Form Check(string key, string label, bool def)
    {
        values[key] = def ? "1" : "0";
        body.Children.Add(Ui.Check(label, def, v => values[key] = v ? "1" : "0"));
        return this;
    }

    /// Lista dos arquivos para pôr em ordem (setas ou arrastar). Grava em key os índices originais na nova ordem ("2,0,1").
    public Form Order(string key, string[] files, string label = "Ordem")
    {
        var order = Enumerable.Range(0, files.Length).ToList();
        void Save() => values[key] = string.Join(",", order);
        Save();
        var list = new ListBox
        {
            Height = Math.Min(220, 26 * files.Length + 8), Background = Ui.Panel, Foreground = Ui.Fg, BorderBrush = Ui.Line,
            AllowDrop = true, Margin = new Thickness(0, 4, 0, 0),
        };
        void Fill(int sel)
        {
            list.Items.Clear();
            for (int i = 0; i < order.Count; i++)
            {
                var item = new ListBoxItem { Content = $"{i + 1}. {Path.GetFileName(files[order[i]].TrimEnd('\\'))}", Foreground = Ui.Fg, Tag = i };
                Tr.Raw(item);
                list.Items.Add(item);
            }
            list.SelectedIndex = sel;
        }
        void Move(int from, int to)
        {
            if (from < 0 || to < 0 || from >= order.Count || to >= order.Count || from == to) return;
            var v = order[from]; order.RemoveAt(from); order.Insert(to, v);
            Save(); Fill(to); Changed?.Invoke();
        }
        Fill(0);
        Point down = default; int dragFrom = -1;
        list.PreviewMouseLeftButtonDown += (_, e) => { down = e.GetPosition(list); dragFrom = list.SelectedIndex; };
        list.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || list.SelectedItem is not ListBoxItem it) return;
            var p = e.GetPosition(list);
            if (Math.Abs(p.Y - down.Y) < 6) return;
            dragFrom = list.SelectedIndex;
            DragDrop.DoDragDrop(list, it, DragDropEffects.Move);
        };
        list.Drop += (_, e) =>
        {
            var target = (e.OriginalSource as DependencyObject) is { } d ? ItemsControl.ContainerFromElement(list, d) as ListBoxItem : null;
            Move(dragFrom, target != null ? list.Items.IndexOf(target) : order.Count - 1);
        };
        var up = Ui.Button("↑", false); up.MinWidth = 34; up.Click += (_, _) => Move(list.SelectedIndex, list.SelectedIndex - 1);
        var dn = Ui.Button("↓", false); dn.MinWidth = 34; dn.Click += (_, _) => Move(list.SelectedIndex, list.SelectedIndex + 1);
        var btns = new StackPanel { Margin = new Thickness(6, 4, 0, 0) };
        btns.Children.Add(up); btns.Children.Add(dn);
        var grid = new DockPanel();
        DockPanel.SetDock(btns, Dock.Right);
        grid.Children.Add(btns); grid.Children.Add(list);
        body.Children.Add(new TextBlock { Text = Tr.T(label), Foreground = Ui.Fg, Margin = new Thickness(0, 6, 0, 0) });
        body.Children.Add(grid);
        body.Children.Add(new TextBlock { Text = Tr.T("Arraste ou use as setas para mudar a ordem."), Foreground = Ui.Dim, FontSize = 12, Margin = new Thickness(0, 2, 0, 4) });
        return this;
    }

    /// Avisado quando a ordem muda (para atualizar uma prévia).
    public event Action? Changed;

    /// Texto de prévia que se atualiza sozinho enquanto a pessoa mexe nos campos.
    public Form Preview(Func<P, string> make)
    {
        var tb = new TextBlock { Foreground = Ui.Dim, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        Tr.Raw(tb);
        body.Children.Add(tb);
        void Upd() { try { tb.Text = make(values); } catch { tb.Text = ""; } }
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        t.Tick += (_, _) => Upd();
        Loaded += (_, _) => { Upd(); t.Start(); };
        Closed += (_, _) => t.Stop();
        return this;
    }

    /// Os arquivos na ordem escolhida em Order (ou como vieram, se não houve lista).
    public static string[] Ordered(string[] files, P p, string key = "ordem")
    {
        var idx = p.S(key).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out var i) ? i : -1).ToArray();
        if (idx.Length != files.Length || idx.Any(i => i < 0 || i >= files.Length) || idx.Distinct().Count() != idx.Length) return files;
        return idx.Select(i => files[i]).ToArray();
    }

    public Form Note(string text)
    {
        body.Children.Add(new TextBlock { Text = Tr.T(text), Foreground = Ui.Dim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 2), FontSize = 12 });
        return this;
    }

    /// Só no modo de capturas de tela (MEXERICA_TESTE_UI): desenha a janela em arquivo em vez de abrir.
    public static Action<Window>? Capture;

    public P? Run()
    {
        if (Capture != null) { Capture(this); return null; }
        return ShowDialog() == true ? values : null;
    }
}
