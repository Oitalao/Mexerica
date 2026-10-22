using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ImageMagick;

namespace Mexerica;

/// Predefinição: uma fatia a mais na roda Converter, com formato, qualidade e tamanho escolhidos pela pessoa.
sealed class Preset
{
    public string Name { get; set; } = "";        // o que aparece na fatia (curto)
    public string Group { get; set; } = "Image";  // Image, Video ou Audio
    public string Format { get; set; } = "jpg";
    public int Quality { get; set; }              // imagem: 30–100; vídeo/áudio: 0 = alta, 1 = média, 2 = leve
    public int MaxSide { get; set; }              // imagem: maior lado em px; vídeo: altura (1080, 720…); 0 = original
    public string Suffix { get; set; } = "";      // acrescentado ao nome do arquivo novo
    public bool StripMetadata { get; set; }       // imagem: tirar EXIF, GPS etc.
}

/// Ordem das fatias, fatias escondidas e predefinições, por tipo de arquivo.
static class WheelCustom
{
    /// Grupo de um arquivo: o tipo embutido (Image, Video…) ou o nome do módulo que o entende.
    public static string Group(string file)
    {
        var k = Catalog.KindOf(file);
        if (k != Kind.Other) return k.ToString();
        var e = Modules.Ext(file);
        var m = Modules.All.FirstOrDefault(m => m.Inputs.Contains(e, StringComparer.OrdinalIgnoreCase));
        return m != null ? "m:" + m.Name : "Other";
    }

    public static string? CommonGroup(string[] files)
    {
        var g = files.Select(Group).Distinct().ToList();
        return g.Count == 1 ? g[0] : null;
    }

    /// Acrescenta predefinições e aplica ordem e esconder.
    public static List<Op> Apply(Mode mode, string[] files, List<Op> ops)
    {
        var c = Config.Now;
        if (files.Length == 0 || CommonGroup(files) is not string g) return ops;
        if (mode == Mode.Convert)
        {
            // As predefinições da pessoa vêm primeiro (ficam no primeiro lado da roda), a não ser que ela mude a ordem.
            var mine = c.Presets.Where(p => p.Group == g).Select(p => { var pr = p; return new Op { Label = pr.Name, Sub = "predefinição", One = (f, _) => Presets.Run(f, pr) }; }).ToList();
            ops.InsertRange(0, mine);
        }
        var key = $"{mode}:{g}";
        if (c.WheelHidden.TryGetValue(key, out var hidden) && hidden.Count > 0)
            ops = ops.Where(o => !hidden.Contains(o.Label)).ToList();
        if (c.WheelOrder.TryGetValue(key, out var order) && order.Count > 0)
            ops = ops.Select((o, i) => (o, i)).OrderBy(x => Rank(order, x.o.Label, x.i, c)).Select(x => x.o).ToList();
        return ops;
    }

    /// Posição na roda: a da ordem salva; fora dela, predefinição nova vai para o começo e o resto para o fim.
    public static int Rank(List<string> order, string label, int index, Config c)
    {
        var j = order.IndexOf(label);
        if (j >= 0) return j;
        return c.Presets.Any(p => p.Name == label) ? -1000 + index : order.Count + index;
    }

    /// Exemplos para a janela de personalizar: um arquivo fictício de cada tipo.
    public static readonly (string name, string sample)[] Samples =
    {
        ("Imagens", "x.jpg"), ("SVG", "x.svg"), ("Vídeo", "x.mp4"), ("Áudio", "x.mp3"), ("PDF", "x.pdf"),
        ("Word e documentos", "x.docx"), ("Planilhas", "x.xlsx"), ("CSV", "x.csv"), ("Apresentações", "x.pptx"),
        ("Texto", "x.txt"), ("Markdown", "x.md"), ("Páginas HTML", "x.html"), ("E-books", "x.epub"),
        ("Legendas", "x.srt"), ("Compactados", "x.zip"), ("Dados (JSON, YAML…)", "x.json"),
        ("Fontes", "x.ttf"), ("Modelos 3D", "x.glb"), ("Mapas", "x.geojson"), ("CAD", "x.dxf"), ("E-mail", "x.eml"),
    };
}

/// Executa uma predefinição.
static class Presets
{
    public static Task<string?> Run(string src, Preset p) => p.Group switch
    {
        "Image" or "Svg" => Task.Run(() => (string?)Image(src, p)),
        "Video" => Video(src, p),
        "Audio" => Audio(src, p),
        _ => throw new Exception("Predefinição sem tipo"),
    };

    static string Out(string src, Preset p) => Paths.Out(src, p.Format, p.Suffix.Trim().Length > 0 ? p.Suffix.Trim() : null);

    static string Image(string src, Preset p)
    {
        using var img = new MagickImage(src);
        img.AutoOrient();
        if (p.MaxSide > 0 && Math.Max(img.Width, img.Height) > p.MaxSide) img.Resize(new MagickGeometry((uint)p.MaxSide, (uint)p.MaxSide));
        if (p.StripMetadata) img.Strip();
        var dst = Out(src, p);
        Images.Save(img, dst, p.Format, p.Quality > 0 ? (uint)Math.Clamp(p.Quality, 30, 100) : null);
        return dst;
    }

    static async Task<string?> Video(string src, Preset p)
    {
        int crf = p.Quality switch { 1 => 23, 2 => 27, _ => 20 };
        var dst = Out(src, p);
        var a = new List<string> { "-i", src, "-map", "0:v:0", "-map", "0:a?", "-sn" };
        var vf = p.MaxSide > 0 ? $"scale=-2:'min({p.MaxSide},ih)'" : "scale=trunc(iw/2)*2:trunc(ih/2)*2";
        if (p.Format == "webm") a.AddRange(new[] { "-vf", vf, "-c:v", "libvpx-vp9", "-crf", (crf + 10).ToString(), "-b:v", "0", "-c:a", "libopus", "-b:a", "128k" });
        else a.AddRange(new[] { "-vf", vf + ",format=yuv420p", "-c:v", "libx264", "-preset", "medium", "-crf", crf.ToString(), "-c:a", "aac", "-b:a", p.Quality == 2 ? "112k" : "160k", "-movflags", "+faststart" });
        a.Add(dst);
        try { await Ff.Run(a, Ff.Duration(src)); return dst; }
        catch { Paths.TryDelete(dst); throw; }
    }

    static async Task<string?> Audio(string src, Preset p)
    {
        var dst = Out(src, p);
        var a = new List<string> { "-i", src, "-vn", "-map", "0:a:0" };
        string[] q = p.Format switch
        {
            "mp3" => new[] { "-c:a", "libmp3lame", "-q:a", p.Quality switch { 1 => "4", 2 => "6", _ => "2" } },
            "m4a" => new[] { "-c:a", "aac", "-b:a", p.Quality switch { 1 => "160k", 2 => "112k", _ => "192k" } },
            "ogg" => new[] { "-c:a", "libvorbis", "-q:a", p.Quality switch { 1 => "4", 2 => "2", _ => "6" } },
            "opus" => new[] { "-c:a", "libopus", "-b:a", p.Quality switch { 1 => "96k", 2 => "64k", _ => "128k" } },
            "flac" => new[] { "-c:a", "flac" },
            _ => new[] { "-c:a", "pcm_s16le" },
        };
        a.AddRange(q);
        a.Add(dst);
        try { await Ff.Run(a, Ff.Duration(src), hw: false); return dst; }
        catch { Paths.TryDelete(dst); throw; }
    }
}

/// Janela "Personalizar a roda": ordem e visibilidade das fatias por tipo de arquivo, e predefinições.
sealed class CustomizeWindow : Window
{
    readonly Config c;
    readonly ComboBox kindBox, modeBox;
    readonly StackPanel list = new(), presetList = new();

    public static bool Ask(Window owner, Config c) => new CustomizeWindow(c) { Owner = owner }.ShowDialog() == true;

    internal CustomizeWindow(Config cfg)
    {
        c = cfg;
        Title = "Mexerica: personalizar a roda";
        Width = 520; Height = Math.Min(560, SystemParameters.WorkArea.Height * 0.85); MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Ui.Dark(this);
        var st = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
        st.Children.Add(Ui.H("Fatias da roda"));
        st.Children.Add(new TextBlock { Text = "Escolha o tipo de arquivo e a roda. Desmarque o que você não usa e mude a ordem com as setas: as primeiras ficam no primeiro lado.", Foreground = Ui.Dim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        kindBox = Ui.Combo(WheelCustom.Samples.Select(s => (s.sample, s.name)), "x.jpg", _ => Fill());
        modeBox = Ui.Combo(new[] { ("Convert", "Converter"), ("Tools", "Ferramentas") }, "Convert", _ => Fill());
        st.Children.Add(Ui.Row("Tipo de arquivo", kindBox));
        st.Children.Add(Ui.Row("Roda", modeBox));
        st.Children.Add(list);
        var reset = Ui.Button("Voltar ao padrão deste tipo", false);
        reset.HorizontalAlignment = HorizontalAlignment.Left;
        reset.Click += (_, _) => { c.WheelOrder.Remove(Key); c.WheelHidden.Remove(Key); Fill(); };
        st.Children.Add(reset);

        st.Children.Add(Ui.H("Predefinições"));
        st.Children.Add(new TextBlock { Text = "Uma predefinição vira uma fatia a mais em Converter. Ex.: “Site” = JPG, qualidade 80, no máximo 1600 px, sem dados de GPS.", Foreground = Ui.Dim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        st.Children.Add(presetList);
        var add = Ui.Button("Nova predefinição…", false);
        add.HorizontalAlignment = HorizontalAlignment.Left;
        add.Click += (_, _) => { var p = PresetWindow.Ask(this, null); if (p != null) { c.Presets.Add(p); FillPresets(); Fill(); } };
        st.Children.Add(add);

        var ok = Ui.Button("Pronto", true);
        ok.Click += (_, _) => DialogResult = true;
        var cancel = Ui.Button("Cancelar", false);
        cancel.Click += (_, _) => DialogResult = false;
        st.Margin = new Thickness(22, 4, 22, 12);
        Content = Ui.Scrolling(st, cancel, ok);
        Fill(); FillPresets();
    }

    string Sample => (string)((ComboBoxItem)kindBox.SelectedItem).Tag;
    Mode SelMode => Enum.Parse<Mode>((string)((ComboBoxItem)modeBox.SelectedItem).Tag);
    string Key => $"{SelMode}:{WheelCustom.Group(Sample)}";

    void Fill()
    {
        if (list == null || kindBox?.SelectedItem == null || modeBox?.SelectedItem == null) return;
        list.Children.Clear();
        List<string> labels;
        var saved = Config.Now;
        try
        {
            // Lista sem ordem nem esconder aplicados, para mostrar tudo o que existe para o tipo.
            var plain = c.Clone(); plain.WheelOrder = new(); plain.WheelHidden = new();
            Config.Override(plain);
            Modules.Listing = true;
            labels = Catalog.For(SelMode, new[] { Path.Combine(Path.GetTempPath(), Sample) }).Select(o => o.Label).Distinct().ToList();
        }
        finally { Config.Override(saved); Modules.Listing = false; }
        c.WheelOrder.TryGetValue(Key, out var order);
        if (order != null) labels = labels.Select((l, i) => (l, i)).OrderBy(x => WheelCustom.Rank(order, x.l, x.i, c)).Select(x => x.l).ToList();
        c.WheelHidden.TryGetValue(Key, out var hidden);
        if (labels.Count == 0) { list.Children.Add(new TextBlock { Text = "Nada para este tipo.", Foreground = Ui.Dim }); return; }
        for (int i = 0; i < labels.Count; i++)
        {
            var l = labels[i]; int idx = i;
            var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
            var up = Ui.Button("↑", false); up.IsEnabled = i > 0; up.Padding = new Thickness(8, 2, 8, 2);
            var down = Ui.Button("↓", false); down.IsEnabled = i < labels.Count - 1; down.Padding = new Thickness(8, 2, 8, 2);
            up.Click += (_, _) => Move(labels, idx, -1);
            down.Click += (_, _) => Move(labels, idx, 1);
            DockPanel.SetDock(down, Dock.Right); DockPanel.SetDock(up, Dock.Right);
            row.Children.Add(down); row.Children.Add(up);
            row.Children.Add(Ui.Check(l, hidden == null || !hidden.Contains(l), v =>
            {
                if (!c.WheelHidden.TryGetValue(Key, out var h)) c.WheelHidden[Key] = h = new();
                if (v) h.Remove(l); else if (!h.Contains(l)) h.Add(l);
            }));
            list.Children.Add(row);
        }
    }

    void Move(List<string> labels, int i, int d)
    {
        var l = labels[i]; labels.RemoveAt(i); labels.Insert(i + d, l);
        c.WheelOrder[Key] = labels.ToList();
        Fill();
    }

    void FillPresets()
    {
        presetList.Children.Clear();
        if (c.Presets.Count == 0) presetList.Children.Add(new TextBlock { Text = "Nenhuma ainda.", Foreground = Ui.Dim, Margin = new Thickness(0, 0, 0, 6) });
        foreach (var p in c.Presets.ToList())
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var rm = Ui.Button("Remover", false); rm.Click += (_, _) => { c.Presets.Remove(p); FillPresets(); Fill(); };
            var ed = Ui.Button("Editar…", false); ed.Click += (_, _) => { var n = PresetWindow.Ask(this, p); if (n != null) { c.Presets[c.Presets.IndexOf(p)] = n; FillPresets(); Fill(); } };
            DockPanel.SetDock(rm, Dock.Right); DockPanel.SetDock(ed, Dock.Right);
            row.Children.Add(rm); row.Children.Add(ed);
            row.Children.Add(new TextBlock { Text = $"{p.Name}  ·  {PresetWindow.Describe(p)}", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            presetList.Children.Add(row);
        }
    }
}

/// Criar ou editar uma predefinição.
sealed class PresetWindow : Window
{
    readonly Preset p;

    public static Preset? Ask(Window owner, Preset? edit)
    {
        var w = new PresetWindow(edit) { Owner = owner };
        return w.ShowDialog() == true ? w.p : null;
    }

    static readonly Dictionary<string, string[]> formats = new()
    {
        ["Image"] = new[] { "jpg", "png", "webp", "avif", "heic", "tiff", "gif" },
        ["Video"] = new[] { "mp4", "webm", "mkv", "mov" },
        ["Audio"] = new[] { "mp3", "m4a", "ogg", "opus", "flac", "wav" },
    };

    public static string Describe(Preset p)
    {
        var tipo = p.Group switch { "Video" => "vídeo", "Audio" => "áudio", _ => "imagem" };
        var q = p.Group == "Image" ? (p.Quality > 0 ? $"qualidade {p.Quality}" : "qualidade padrão") : new[] { "alta", "média", "leve" }[Math.Clamp(p.Quality, 0, 2)];
        var s = p.MaxSide > 0 ? (p.Group == "Video" ? $", até {p.MaxSide}p" : $", até {p.MaxSide} px") : "";
        return $"{tipo} → {p.Format.ToUpperInvariant()}, {q}{s}{(p.StripMetadata ? ", sem metadados" : "")}";
    }

    internal PresetWindow(Preset? edit)
    {
        p = edit == null ? new Preset { Name = "Site", Group = "Image", Format = "jpg", Quality = 80, MaxSide = 1600, StripMetadata = true }
                         : new Preset { Name = edit.Name, Group = edit.Group, Format = edit.Format, Quality = edit.Quality, MaxSide = edit.MaxSide, Suffix = edit.Suffix, StripMetadata = edit.StripMetadata };
        Title = edit == null ? "Nova predefinição" : "Editar predefinição";
        Width = 470; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Ui.Dark(this);
        var st = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
        var name = Ui.Text(p.Name, 170);
        name.MaxLength = 14;
        st.Children.Add(Ui.Row("Nome na roda (curto)", name));
        var fmtHost = new ContentControl();
        var qHost = new ContentControl();
        var sizeHost = new ContentControl();
        void Rebuild()
        {
            var fs = formats[p.Group];
            if (!fs.Contains(p.Format)) p.Format = fs[0];
            fmtHost.Content = Ui.Combo(fs.Select(f => (f, f.ToUpperInvariant())), p.Format, v => p.Format = v);
            qHost.Content = p.Group == "Image"
                ? Ui.Combo(new[] { ("0", "Padrão do Mexerica"), ("95", "Muito alta"), ("90", "Alta"), ("80", "Média"), ("70", "Leve"), ("55", "Bem leve") }, p.Quality.ToString(), v => p.Quality = int.Parse(v))
                : Ui.Combo(new[] { ("0", "Alta"), ("1", "Média"), ("2", "Arquivo menor") }, Math.Clamp(p.Quality, 0, 2).ToString(), v => p.Quality = int.Parse(v));
            sizeHost.Content = p.Group == "Video"
                ? Ui.Combo(new[] { ("0", "Original"), ("2160", "4K (2160p)"), ("1080", "1080p"), ("720", "720p"), ("480", "480p") }, p.MaxSide.ToString(), v => p.MaxSide = int.Parse(v))
                : p.Group == "Image"
                    ? Ui.Combo(new[] { ("0", "Original"), ("3840", "3840 px"), ("2560", "2560 px"), ("1920", "1920 px"), ("1600", "1600 px"), ("1280", "1280 px"), ("1080", "1080 px"), ("800", "800 px"), ("512", "512 px") }, p.MaxSide.ToString(), v => p.MaxSide = int.Parse(v))
                    : new TextBlock { Text = "—", Foreground = Ui.Dim };
        }
        st.Children.Add(Ui.Row("Tipo de arquivo", Ui.Combo(new[] { ("Image", "Imagens"), ("Video", "Vídeo"), ("Audio", "Áudio") }, p.Group, v => { p.Group = v; if (v != "Image") p.Quality = 0; Rebuild(); })));
        st.Children.Add(Ui.Row("Formato", fmtHost));
        st.Children.Add(Ui.Row("Qualidade", qHost));
        st.Children.Add(Ui.Row("Tamanho máximo", sizeHost));
        var suffix = Ui.Text(p.Suffix, 170);
        st.Children.Add(Ui.Row("Acrescentar ao nome", suffix));
        st.Children.Add(Ui.Check("Tirar metadados (local, câmera, data) das imagens", p.StripMetadata, v => p.StripMetadata = v));
        Rebuild();
        var ok = Ui.Button("Salvar", true);
        ok.Click += (_, _) =>
        {
            p.Name = name.Text.Trim(); p.Suffix = suffix.Text.Trim();
            if (p.Name.Length == 0) { Tr.Box(this, "Dê um nome curto à predefinição.", "Mexerica"); return; }
            DialogResult = true;
        };
        var cancel = Ui.Button("Cancelar", false);
        cancel.Click += (_, _) => DialogResult = false;
        var bar = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        bar.Children.Add(cancel); bar.Children.Add(ok);
        st.Children.Add(bar);
        Content = st;
    }
}
