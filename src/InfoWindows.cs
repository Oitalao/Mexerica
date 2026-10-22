using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mexerica;

/// Metadados: mostra tudo o que o arquivo guarda e deixa editar os campos principais ou apagar tudo.
sealed class MetaWindow : Window
{
    P? result;

    public static P? Ask(string[] fs)
    {
        List<(string group, string tag, string value)> data;
        try { data = Meta.Read(fs[0]); }
        catch (Exception ex) { App.Toast.Show("Não consegui ler os metadados", ex.Message, error: true); return null; }
        var w = new MetaWindow(fs, data);
        return w.ShowDialog() == true ? w.result : null;
    }

    MetaWindow(string[] fs, List<(string group, string tag, string value)> data)
    {
        var kind = Catalog.KindOf(fs[0]);
        Title = "Mexerica: metadados";
        Width = 860; Height = 720; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.Dark(this);

        var top = new StackPanel { Margin = new Thickness(18, 14, 18, 8) };
        top.Children.Add(new TextBlock { Text = "Metadados", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Ui.Orange });
        top.Children.Add(new TextBlock { Text = fs.Length == 1 ? Path.GetFileName(fs[0]) : $"{Path.GetFileName(fs[0])} e mais {fs.Length - 1} (as mudanças valem para todos)", Foreground = Ui.Dim, TextTrimming = TextTrimming.CharacterEllipsis });
        var gps = data.Where(d => d.tag.StartsWith("GPS", StringComparison.OrdinalIgnoreCase) && d.tag is "GPSLatitude" or "GPSLongitude" or "GPSPosition").ToList();
        if (gps.Count > 0)
            top.Children.Add(new TextBlock { Text = "Atenção: este arquivo guarda o local onde foi feito.", Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xB4, 0x4A)), Margin = new Thickness(0, 6, 0, 0) });

        // Campos editáveis
        string Cur(params string[] tags) => tags.Select(t => data.FirstOrDefault(d => d.tag.Equals(t, StringComparison.OrdinalIgnoreCase)).value).FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "";
        var fields = new List<(string key, string label, string value)>();
        void F(string key, string label, params string[] tags) => fields.Add((key, label, Cur(tags)));
        F("Title", "Título", "Title", "ObjectName", "XPTitle");
        F("Artist", kind == Kind.Audio ? "Artista" : "Autor", "Artist", "Author", "Creator", "By-line");
        if (kind == Kind.Audio) { F("Album", "Álbum", "Album"); F("Genre", "Gênero", "Genre"); }
        F("Description", kind == Kind.Pdf ? "Assunto" : "Descrição", "Description", "ImageDescription", "Subject", "Caption-Abstract", "Comment");
        if (kind != Kind.Audio) F("Copyright", "Direitos autorais", "Copyright", "Rights", "CopyrightNotice");
        if (kind is Kind.Image or Kind.Pdf) F("Keywords", "Palavras-chave", "Keywords", "Subject");
        F("Date", kind == Kind.Audio ? "Ano ou data" : "Data", "DateTimeOriginal", "CreateDate", "Date", "Year");

        var edit = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        edit.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        edit.ColumnDefinitions.Add(new ColumnDefinition());
        var boxes = new Dictionary<string, (TextBox box, string orig)>();
        for (int i = 0; i < fields.Count; i++)
        {
            edit.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = fields[i].label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 8, 3) };
            var tb = Ui.Text(fields[i].value, double.NaN); tb.Margin = new Thickness(0, 3, 0, 3);
            Grid.SetRow(l, i); Grid.SetRow(tb, i); Grid.SetColumn(tb, 1);
            edit.Children.Add(l); edit.Children.Add(tb);
            boxes[fields[i].key] = (tb, fields[i].value);
        }
        top.Children.Add(edit);
        var noGps = Ui.Check("Remover a localização (GPS)", gps.Count > 0, _ => { });
        if (kind is Kind.Image or Kind.Video) top.Children.Add(noGps);

        // Tudo o que há no arquivo
        var filter = Ui.Text("", 260); filter.ToolTip = "Filtrar";
        var list = new ListView { Background = Ui.Panel, Foreground = Ui.Fg, BorderBrush = Ui.Line };
        var gv = new GridView();
        gv.Columns.Add(new GridViewColumn { Header = "Grupo", Width = 120, DisplayMemberBinding = new System.Windows.Data.Binding("Item1") });
        gv.Columns.Add(new GridViewColumn { Header = "Campo", Width = 220, DisplayMemberBinding = new System.Windows.Data.Binding("Item2") });
        gv.Columns.Add(new GridViewColumn { Header = "Valor", Width = 420, DisplayMemberBinding = new System.Windows.Data.Binding("Item3") });
        list.View = gv;
        void Fill() => list.ItemsSource = data.Where(d => filter.Text.Length == 0 || (d.group + " " + d.tag + " " + d.value).Contains(filter.Text, StringComparison.OrdinalIgnoreCase)).Select(d => Tuple.Create(d.group, d.tag, d.value)).ToList();
        filter.TextChanged += (_, _) => Fill();
        Fill();
        var mid = new DockPanel { Margin = new Thickness(18, 4, 18, 0) };
        var fr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        fr.Children.Add(new TextBlock { Text = $"Tudo o que o arquivo guarda ({data.Count} campos)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        fr.Children.Add(filter);
        DockPanel.SetDock(fr, Dock.Top);
        mid.Children.Add(fr); mid.Children.Add(list);

        var strip = Ui.Button("Apagar tudo", false); strip.Margin = new Thickness(0);
        strip.ToolTip = "Cria uma cópia sem nenhum metadado (local, câmera, autor, datas)";
        var save = Ui.Button("Salvar cópia", true); var close = Ui.Button("Cancelar", false);
        strip.Click += (_, _) => { result = new P { ["limpar"] = "1" }; DialogResult = true; };
        save.Click += (_, _) =>
        {
            var p = new P();
            foreach (var (k, (box, orig)) in boxes) if (box.Text != orig) p["m." + k] = box.Text;
            if (noGps.IsChecked == true && (kind is Kind.Image or Kind.Video)) p["semgps"] = "1";
            if (p.Count == 0) { Tr.Box(this, "Nada foi alterado.", "Mexerica"); return; }
            result = p; DialogResult = true;
        };
        close.Click += (_, _) => DialogResult = false;
        var bar = new DockPanel { Margin = new Thickness(18, 12, 18, 14) };
        var right = new StackPanel { Orientation = Orientation.Horizontal }; right.Children.Add(close); right.Children.Add(save);
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(right);
        var leftBtns = new StackPanel { Orientation = Orientation.Horizontal }; leftBtns.Children.Add(strip);
        bar.Children.Add(leftBtns);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(mid, 1); Grid.SetRow(bar, 2);
        grid.Children.Add(top); grid.Children.Add(mid); grid.Children.Add(bar);
        Content = grid;
    }
}

/// Resultado da leitura de QR e códigos de barras.
sealed class QrWindow : Window
{
    /// Lê todos os arquivos; devolve uma linha por código: "arquivo\ttexto".
    public static string? Read(string[] fs)
    {
        var lines = new List<string>();
        for (int i = 0; i < fs.Length; i++)
        {
            Job.Part(i, fs.Length);
            foreach (var t in Images.ReadCodes(fs[i])) lines.Add(Path.GetFileName(fs[i]) + "\t" + t.Replace("\r", " ").Replace("\n", " ") + "\t" + t);
        }
        return string.Join("\n", lines);
    }

    public static void Show(string result)
    {
        var items = result.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('\t', 3)).Where(f => f.Length == 3).ToList();
        if (items.Count == 0) { App.Toast.Show("Nenhum código encontrado", "Não achei QR nem código de barras na imagem."); return; }
        new QrWindow(items).Show();
    }

    QrWindow(List<string[]> items)
    {
        Title = "Mexerica: códigos lidos";
        Width = 560; SizeToContent = SizeToContent.Height; MaxHeight = 700; ResizeMode = ResizeMode.NoResize; Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.Dark(this);
        var st = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
        st.Children.Add(new TextBlock { Text = items.Count == 1 ? "Código lido" : $"{items.Count} códigos lidos", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Ui.Orange, Margin = new Thickness(0, 0, 0, 8) });
        var many = items.Select(i => i[0]).Distinct().Count() > 1;
        foreach (var it in items)
        {
            var text = it[2];
            var card = new Border { Background = Ui.Panel, CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8) };
            var cs = new StackPanel();
            if (many) cs.Children.Add(new TextBlock { Text = it[0], Foreground = Ui.Dim, FontSize = 11 });
            cs.Children.Add(new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Background = Brushes.Transparent, Foreground = Ui.Fg, BorderThickness = new Thickness(0), MaxHeight = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var bs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var copy = Ui.Button("Copiar", false); copy.Margin = new Thickness(0, 0, 6, 0);
            copy.Click += (_, _) => { Clipboard.SetText(text); copy.Content = "Copiado"; };
            bs.Children.Add(copy);
            if (Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme is "http" or "https" or "mailto" or "tel"))
            {
                var open = Ui.Button("Abrir", true); open.Margin = new Thickness(0);
                open.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); } catch { } };
                bs.Children.Add(open);
            }
            cs.Children.Add(bs);
            card.Child = cs;
            st.Children.Add(card);
        }
        var close = Ui.Button("Fechar", false); close.HorizontalAlignment = HorizontalAlignment.Right; close.IsCancel = true;
        close.Click += (_, _) => Close();
        st.Children.Add(close);
        Content = new ScrollViewer { Content = st, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
