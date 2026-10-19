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
        Title = "Mexerica: " + title;
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
            foreach (var c in checks) { var err = c(); if (err != null) { MessageBox.Show(this, err, "Mexerica", MessageBoxButton.OK, MessageBoxImage.Warning); return; } }
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

    public Form Note(string text)
    {
        body.Children.Add(new TextBlock { Text = text, Foreground = Ui.Dim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 2), FontSize = 12 });
        return this;
    }

    public P? Run() => ShowDialog() == true ? values : null;
}
