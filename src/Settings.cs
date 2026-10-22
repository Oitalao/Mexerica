using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mexerica;

/// Preferências gravadas em %LOCALAPPDATA%\Mexerica\config.json.
sealed class Config
{
    public uint OpenKey { get; set; } = 0x20;          // Espaço
    public uint ConvertKey { get; set; } = 0xA0;       // Shift
    public uint ToolsKey { get; set; } = 0xA2;         // Ctrl
    public uint ActionsKey { get; set; } = 0xA4;       // Alt
    public string Hotkey { get; set; } = "ctrl+shift+space"; // abre a roda para os arquivos selecionados
    public bool HoldToConvert { get; set; } = true;    // toque: segurar parado antes de arrastar
    public int ImageQuality { get; set; } = 90;
    public string VideoQuality { get; set; } = "alta"; // alta, media, leve
    public string AudioQuality { get; set; } = "alta"; // alta, media, leve
    public int PdfDpi { get; set; } = 300;
    public string OutputDir { get; set; } = "";        // vazio = ao lado do original
    public bool RevealWhenDone { get; set; } = false;
    public bool Paused { get; set; }
    public bool UseGpu { get; set; } = true;           // vídeo pela placa (NVIDIA, Intel, AMD) quando houver
    public bool ShellMenu { get; set; } = true;        // submenu "Mexerica" no botão direito do Explorador
    public string WheelTheme { get; set; } = "escuro";  // escuro, claro, sistema
    public string Language { get; set; } = "pt-BR";   // padrão para todos: português do Brasil
    public int WheelOpacity { get; set; } = 93;         // % de opacidade das fatias
    public List<WatchFolder> WatchFolders { get; set; } = new();
    public bool AutoUpdate { get; set; } = true;
    public Dictionary<string, List<string>> WheelOrder { get; set; } = new();
    public Dictionary<string, List<string>> WheelHidden { get; set; } = new();
    public List<Preset> Presets { get; set; } = new();
    public string SkippedVersion { get; set; } = "";
    public string LastSeenVersion { get; set; } = "";
    public string UpdateNotes { get; set; } = "";
    public string UpdateNotesVersion { get; set; } = "";

    public int Crf => VideoQuality switch { "media" => 23, "leve" => 27, _ => 20 };
    public string Mp3Q => AudioQuality switch { "media" => "4", "leve" => "6", _ => "2" };
    public int AacK => AudioQuality switch { "media" => 160, "leve" => 112, _ => 192 };

    static readonly string file = Path.Combine(Paths.Data, "config.json");
    public static Config Now { get; private set; } = Load();

    static Config Load()
    {
        try { if (File.Exists(file)) return JsonSerializer.Deserialize<Config>(File.ReadAllText(file)) ?? new(); }
        catch (Exception ex) { Log.Write("config: " + ex.Message); }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Paths.Data);
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, file, true);
        }
        catch (Exception ex) { Log.Write("config: " + ex.Message); }
        Now = this;
    }

    public Config Clone() => JsonSerializer.Deserialize<Config>(JsonSerializer.Serialize(this))!;

    /// Só nesta execução, sem gravar (linha de comando: -o, --qualidade).
    public static void Override(Config c) => Now = c;

    public static readonly (uint vk, string name)[] OpenKeys =
    {
        (0x20, "Espaço"), (0x09, "Tab"), (0xC0, "Aspas (')"), (0x14, "Caps Lock"), (0x51, "Q"), (0x45, "E"),
    };
    public static readonly (uint vk, string name)[] Modifiers = { (0xA0, "Shift"), (0xA2, "Ctrl"), (0xA4, "Alt") };
    public static readonly (string id, string name)[] Hotkeys =
    {
        ("ctrl+shift+space", "Ctrl + Shift + Espaço"), ("ctrl+alt+space", "Ctrl + Alt + Espaço"),
        ("ctrl+shift+m", "Ctrl + Shift + M"), ("win+shift+m", "Win + Shift + M"), ("", "Desligado"),
    };

    public static string KeyName(uint vk) =>
        Tr.T(OpenKeys.Concat(Modifiers).FirstOrDefault(k => k.vk == vk).name ?? $"tecla {vk}");
}

/// Janela de configurações (aberta pelo ícone da bandeja).
sealed class SettingsWindow : Window
{
    readonly Config c = Config.Now.Clone();

    public SettingsWindow()
    {
        c.Language = Tr.Languages.FirstOrDefault(l => l.Code.Equals(c.Language, StringComparison.OrdinalIgnoreCase)).Code ?? "pt-BR";
        Title = Tr.T("Mexerica: configurações");
        Width = 540; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.CanMinimize;
        MaxHeight = Math.Max(420, SystemParameters.WorkArea.Height * 0.9); // telas baixas: rola em vez de sair da tela
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.Dark(this);

        var st = new StackPanel { Margin = new Thickness(22, 4, 22, 12) };
        st.Children.Add(Ui.H("Idioma"));
        st.Children.Add(Ui.Row("Idioma da interface", Ui.Combo(Tr.Languages.Select(l => (l.Code, l.Name)), c.Language, v => c.Language = v)));
        st.Children.Add(Ui.H("Teclas"));
        st.Children.Add(Ui.Row("Abrir a roda durante o arrasto", Ui.Combo(Config.OpenKeys.Select(k => (k.vk.ToString(), k.name)), c.OpenKey.ToString(), v => c.OpenKey = uint.Parse(v))));
        st.Children.Add(Ui.Row("Ir para Converter", Ui.Combo(Config.Modifiers.Select(k => (k.vk.ToString(), k.name)), c.ConvertKey.ToString(), v => c.ConvertKey = uint.Parse(v))));
        st.Children.Add(Ui.Row("Ir para Ferramentas", Ui.Combo(Config.Modifiers.Select(k => (k.vk.ToString(), k.name)), c.ToolsKey.ToString(), v => c.ToolsKey = uint.Parse(v))));
        st.Children.Add(Ui.Row("Ir para Ações", Ui.Combo(Config.Modifiers.Select(k => (k.vk.ToString(), k.name)), c.ActionsKey.ToString(), v => c.ActionsKey = uint.Parse(v))));
        st.Children.Add(Ui.Row("Roda pelo teclado (arquivos selecionados)", Ui.Combo(Config.Hotkeys.Select(h => (h.id, h.name)), c.Hotkey, v => c.Hotkey = v)));
        st.Children.Add(Ui.Check("Tela de toque: segurar parado um instante antes de arrastar abre Converter", c.HoldToConvert, v => c.HoldToConvert = v));

        st.Children.Add(Ui.H("Qualidade"));
        st.Children.Add(Ui.Row("Imagens (JPG, WebP, AVIF, HEIC)", Ui.Combo(new[] { ("100", "Máxima"), ("95", "Muito alta"), ("90", "Alta"), ("80", "Média"), ("70", "Leve") }, c.ImageQuality.ToString(), v => c.ImageQuality = int.Parse(v))));
        st.Children.Add(Ui.Row("Vídeo", Ui.Combo(new[] { ("alta", "Alta"), ("media", "Média"), ("leve", "Arquivo menor") }, c.VideoQuality, v => c.VideoQuality = v)));
        st.Children.Add(Ui.Row("Áudio", Ui.Combo(new[] { ("alta", "Alta"), ("media", "Média"), ("leve", "Arquivo menor") }, c.AudioQuality, v => c.AudioQuality = v)));
        st.Children.Add(Ui.Row("PDF para imagem", Ui.Combo(new[] { ("300", "300 DPI"), ("200", "200 DPI"), ("150", "150 DPI"), ("600", "600 DPI") }, c.PdfDpi.ToString(), v => c.PdfDpi = int.Parse(v))));

        st.Children.Add(Ui.H("Resultado"));
        var outLabel = new TextBlock { Foreground = Ui.Dim, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 250 };
        void ShowOut() => outLabel.Text = c.OutputDir.Length == 0 ? Tr.T("Ao lado do original") : c.OutputDir;
        ShowOut();
        var pick = Ui.Button("Escolher…", false);
        pick.Click += (_, _) =>
        {
            var d = new Microsoft.Win32.OpenFolderDialog { Title = Tr.T("Pasta para os arquivos novos") };
            if (d.ShowDialog(this) == true) { c.OutputDir = d.FolderName; ShowOut(); }
        };
        var reset = Ui.Button("Ao lado", false);
        reset.Click += (_, _) => { c.OutputDir = ""; ShowOut(); };
        var outRow = new StackPanel { Orientation = Orientation.Horizontal };
        outRow.Children.Add(outLabel); outRow.Children.Add(pick); outRow.Children.Add(reset);
        st.Children.Add(Ui.Row("Onde salvar", outRow));
        st.Children.Add(Ui.Check("Ao terminar, mostrar o arquivo novo na pasta", c.RevealWhenDone, v => c.RevealWhenDone = v));
        var watchLabel = new TextBlock { Foreground = Ui.Dim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        void ShowWatch() => watchLabel.Text = c.WatchFolders.Count == 0 ? Tr.T("Nenhuma") : Tr.T($"{c.WatchFolders.Count(w => w.Enabled)} ligada(s)");
        ShowWatch();
        var watchBtn = Ui.Button("Escolher…", false);
        watchBtn.Click += (_, _) => { var r = WatchWindow.Ask(this, c.WatchFolders); if (r != null) { c.WatchFolders = r; ShowWatch(); } };
        var watchRow = new StackPanel { Orientation = Orientation.Horizontal };
        watchRow.Children.Add(watchLabel); watchRow.Children.Add(watchBtn);
        st.Children.Add(Ui.Row("Pastas monitoradas", watchRow));

        st.Children.Add(Ui.H("Aparência da roda"));
        var custom = Ui.Button("Personalizar…", false);
        custom.Click += (_, _) => CustomizeWindow.Ask(this, c);
        st.Children.Add(Ui.Row("Fatias, ordem e predefinições", custom));
        st.Children.Add(Ui.Row("Tema", Ui.Combo(new[] { ("escuro", "Escuro"), ("claro", "Claro"), ("sistema", "Igual ao Windows") }, c.WheelTheme, v => c.WheelTheme = v)));
        st.Children.Add(Ui.Row("Opacidade", Ui.Combo(new[] { ("100", "Sólida"), ("93", "Quase sólida"), ("80", "Um pouco transparente"), ("65", "Transparente") }, c.WheelOpacity.ToString(), v => c.WheelOpacity = int.Parse(v))));

        st.Children.Add(Ui.H("Desempenho e Windows"));
        st.Children.Add(Ui.Check("Usar a placa de vídeo para converter vídeos (bem mais rápido)", c.UseGpu, v => c.UseGpu = v));
        st.Children.Add(Ui.Check("Mostrar o Mexerica no menu do botão direito dos arquivos", c.ShellMenu, v => c.ShellMenu = v));

        st.Children.Add(Ui.H("Atualizações"));
        st.Children.Add(Ui.Check("Procurar versões novas sozinho (o aviso aparece perto do relógio)", c.AutoUpdate, v => c.AutoUpdate = v));
        var now = Ui.Button("Procurar agora", false);
        now.Click += (_, _) => _ = Updater.Check(manual: true);
        var verRow = new StackPanel { Orientation = Orientation.Horizontal };
        verRow.Children.Add(new TextBlock { Text = Tr.T($"Versão {Updater.CurrentText}"), Foreground = Ui.Dim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        verRow.Children.Add(now);
        st.Children.Add(Ui.Row("Esta cópia", verRow));

        var ok = Ui.Button("Salvar", true);
        var cancel = Ui.Button("Cancelar", false);
        ok.Click += (_, _) =>
        {
            if (new[] { c.ConvertKey, c.ToolsKey, c.ActionsKey }.Distinct().Count() < 3)
            { Tr.Box(this, "Converter, Ferramentas e Ações precisam de teclas diferentes.", "Mexerica", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            c.Save(); DialogResult = true;
            ShellMenu.Apply();
            Watchers.Apply();
        };
        cancel.Click += (_, _) => DialogResult = false;
        Content = Ui.Scrolling(st, cancel, ok);
    }
}

/// Peças de interface no tema escuro da roda.
static class Ui
{
    public static readonly Brush Bg = Frozen(new SolidColorBrush(Color.FromRgb(30, 30, 35)));
    public static readonly Brush Panel = Frozen(new SolidColorBrush(Color.FromRgb(44, 44, 50)));
    public static readonly Brush Line = Frozen(new SolidColorBrush(Color.FromRgb(70, 70, 78)));
    public static readonly Brush Fg = Brushes.White;
    public static readonly Brush Dim = Frozen(new SolidColorBrush(Color.FromArgb(175, 255, 255, 255)));
    public static readonly Brush Orange = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x1F)));
    static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }

    public static void Dark(Window w)
    {
        w.Background = Bg; w.Foreground = Fg; Tr.Window(w); w.FontSize = 13;
        w.SourceInitialized += (_, _) => Native.DarkTitle(new System.Windows.Interop.WindowInteropHelper(w).Handle);
        w.Resources.Add(typeof(ComboBox), ComboStyle());
    }

    static Style ComboStyle()
    {
        var s = new Style(typeof(ComboBox));
        s.Setters.Add(new Setter(Control.MinWidthProperty, 170.0));
        s.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
        s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
        return s;
    }

    /// Conteúdo com rolagem e os botões sempre visíveis embaixo.
    public static FrameworkElement Scrolling(FrameworkElement body, params Button[] buttons)
    {
        var dock = new DockPanel { LastChildFill = true };
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(22, 10, 22, 14) };
        foreach (var b in buttons) bar.Children.Add(b);
        var foot = new Border { BorderBrush = Line, BorderThickness = new Thickness(0, 1, 0, 0), Child = bar };
        DockPanel.SetDock(foot, Dock.Bottom);
        dock.Children.Add(foot);
        dock.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 10, 0, 0) });
        return dock;
    }

    public static TextBlock H(string t) => new() { Text = Tr.T(t), FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Orange, Margin = new Thickness(0, 14, 0, 6) };

    public static FrameworkElement Row(string label, UIElement control)
    {
        var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var t = new TextBlock { Text = Tr.T(label), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 12, 0) };
        g.Children.Add(t);
        Grid.SetColumn(control, 1);
        g.Children.Add(control);
        return g;
    }

    /// Rótulo em cima, controle embaixo (painéis estreitos).
    public static FrameworkElement Stack(string label, FrameworkElement control)
    {
        var st = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        st.Children.Add(new TextBlock { Text = Tr.T(label), Margin = new Thickness(0, 0, 0, 4) });
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        st.Children.Add(control);
        return st;
    }

    /// Botão de alternar sem o azul padrão: fundo e borda vêm das propriedades.
    public static ControlTemplate FlatToggle()
    {
        var t = new ControlTemplate(typeof(System.Windows.Controls.Primitives.ToggleButton));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        border.AppendChild(cp);
        t.VisualTree = border;
        return t;
    }

    /// Botão plano com leve destaque ao passar o mouse e ao apertar.
    public static ControlTemplate FlatButton()
    {
        var t = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border), "b");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(cp);
        t.VisualTree = border;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.85, "b"));
        var press = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        press.Setters.Add(new Setter(UIElement.OpacityProperty, 0.7, "b"));
        var off = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        off.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4, "b"));
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, Orange, "b"));
        t.Triggers.Add(hover); t.Triggers.Add(press); t.Triggers.Add(off); t.Triggers.Add(focus);
        return t;
    }

    public static ComboBox Combo(IEnumerable<(string value, string label)> items, string current, Action<string> set)
    {
        var cb = new ComboBox();
        foreach (var (v, l) in items) cb.Items.Add(new ComboBoxItem { Content = Tr.T(l), Tag = v });
        cb.SelectedItem = cb.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == current) ?? cb.Items[0];
        cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is ComboBoxItem i) set((string)i.Tag); };
        return cb;
    }

    public static CheckBox Check(string label, bool value, Action<bool> set)
    {
        var cb = new CheckBox
        {
            Content = new TextBlock { Text = Tr.T(label), TextWrapping = TextWrapping.Wrap, Foreground = Fg },
            IsChecked = value, Margin = new Thickness(0, 6, 0, 4), Foreground = Fg,
        };
        cb.Checked += (_, _) => set(true); cb.Unchecked += (_, _) => set(false);
        return cb;
    }

    public static Button Button(string text, bool primary)
    {
        var b = new Button
        {
            Content = Tr.T(text), Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(8, 0, 0, 0), MinWidth = 84,
            Foreground = primary ? Brushes.Black : Fg, Background = primary ? Orange : Panel, BorderBrush = Line,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        b.Template = FlatButton();
        if (primary) b.IsDefault = true; else if (text == "Cancelar") b.IsCancel = true;
        return b;
    }

    public static TextBox Text(string value, double width = 170) =>
        new() { Text = value, Width = width, Padding = new Thickness(6, 4, 6, 4), Background = Panel, Foreground = Fg, BorderBrush = Line, CaretBrush = Fg };
}
