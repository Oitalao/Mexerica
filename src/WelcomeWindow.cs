using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Mexerica;

/// Primeira vez: explica, sem jargão, as três formas de usar e onde o Mexerica fica.
sealed class WelcomeWindow : Window
{
    public WelcomeWindow()
    {
        Title = "Bem-vindo ao Mexerica";
        Width = 540; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.Dark(this);
        Topmost = true;
        Loaded += (_, _) => { Activate(); Topmost = false; };

        var st = new StackPanel { Margin = new Thickness(26, 20, 26, 20) };
        st.Children.Add(new TextBlock { Text = "O Mexerica está pronto", FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = Ui.Fg });
        st.Children.Add(Para("Ele converte e ajeita arquivos (fotos, vídeos, músicas, PDFs, documentos) sem abrir programa nenhum. " +
                             "O resultado aparece ao lado do arquivo original; o original nunca é alterado."));

        var c = Config.Now;
        st.Children.Add(Ui.H("Três jeitos de usar"));
        st.Children.Add(Step("1", "Botão direito",
            "Clique com o botão direito em um ou mais arquivos e escolha Mexerica." +
            (ModernMenu ? " No Windows 11, ele fica em \"Mostrar mais opções\" (ou aperte Shift junto com o botão direito)." : "")));
        st.Children.Add(Step("2", "Arrastar",
            $"Comece a arrastar um arquivo e, sem soltar o mouse, aperte {Config.KeyName(c.OpenKey)}. Aparece uma roda: solte o arquivo em cima do que você quer."));
        var hk = Array.Find(Config.Hotkeys, h => h.id == c.Hotkey).name;
        if (!string.IsNullOrEmpty(c.Hotkey))
            st.Children.Add(Step("3", "Teclado", $"Selecione arquivos no Explorador ou na área de trabalho e aperte {hk}."));

        st.Children.Add(Ui.H("Onde ele fica"));
        st.Children.Add(Para("O Mexerica fica quietinho perto do relógio (ícone da mexerica; se não aparecer, clique na setinha ^). " +
                             "Por ali dá para pausar, mudar teclas e qualidade, ou sair. Ele já abre sozinho quando o computador liga."));

        var ok = Ui.Button("Entendi", true);
        ok.Click += (_, _) => Close();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        bar.Children.Add(ok);
        st.Children.Add(bar);
        Content = st;
    }

    static TextBlock Para(string t) => new()
    {
        Text = t, Foreground = Ui.Dim, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), LineHeight = 19,
    };

    static FrameworkElement Step(string n, string title, string text)
    {
        var g = new Grid { Margin = new Thickness(0, 6, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        var badge = new Border
        {
            Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = Ui.Orange, VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock { Text = n, Foreground = Brushes.Black, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = title, Foreground = Ui.Fg, FontSize = 14, FontWeight = FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Text = text, Foreground = Ui.Dim, FontSize = 13, TextWrapping = TextWrapping.Wrap, LineHeight = 19, Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(body, 1);
        g.Children.Add(badge); g.Children.Add(body);
        return g;
    }

    /// Windows 11 com o menu novo (o Mexerica aparece só em "Mostrar mais opções").
    static bool ModernMenu
    {
        get
        {
            if (Environment.OSVersion.Version.Build < 22000) return false;
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32");
            return k == null;
        }
    }
}
