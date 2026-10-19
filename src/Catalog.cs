using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Mexerica;

/// Decide o que aparece em cada roda para os arquivos arrastados.
static class Catalog
{
    static readonly Dictionary<string, Kind> kinds = new(StringComparer.OrdinalIgnoreCase);

    static Catalog()
    {
        void Add(Kind k, params string[] exts) { foreach (var e in exts) kinds["." + e] = k; }
        Add(Kind.Image, "jpg", "jpeg", "jfif", "png", "webp", "heic", "heif", "tif", "tiff", "avif", "bmp", "gif", "ico");
        Add(Kind.Svg, "svg");
        Add(Kind.Audio, "mp3", "m4a", "wav", "flac", "ogg", "oga", "opus", "aif", "aiff", "wma", "aac");
        Add(Kind.Video, "mp4", "mov", "mkv", "webm", "avi", "wmv", "m4v", "flv", "mpg", "mpeg", "3gp", "ts", "mts", "m2ts");
        Add(Kind.Pdf, "pdf");
        Add(Kind.Doc, "docx", "doc", "docm", "odt", "rtf", "wpd", "pages");
        Add(Kind.Markdown, "md", "markdown");
        Add(Kind.Text, "txt");
        Add(Kind.Html, "html", "htm", "xhtml");
        Add(Kind.Epub, "epub");
        Add(Kind.Slides, "pptx", "ppt", "pps", "ppsx", "odp", "key");
        Add(Kind.Sheet, "xlsx", "xls", "xlsm", "ods", "numbers");
        Add(Kind.Csv, "csv", "tsv");
        Add(Kind.Subtitle, "srt", "vtt");
        Add(Kind.Archive, "zip", "tar", "gz", "tgz", "rar", "7z", "bz2", "xz", "tbz2", "txz");
    }

    /// Extensões conhecidas (com ponto), para o menu do botão direito.
    public static IEnumerable<string> Extensions => kinds.Keys;

    public static Kind KindOf(string p)
    {
        if (Directory.Exists(p)) return Kind.Folder;
        return kinds.TryGetValue(Path.GetExtension(p), out var k) ? k : Kind.Other;
    }

    static string Norm(string p)
    {
        var e = Path.GetExtension(p).TrimStart('.').ToLowerInvariant();
        return e switch
        {
            "jpeg" or "jfif" => "jpg", "tif" => "tiff", "heif" => "heic", "aif" => "aiff", "htm" or "xhtml" => "html",
            "markdown" => "md", "oga" => "ogg", "gz" when p.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) => "tgz",
            _ => e,
        };
    }

    // Formatos de destino por tipo de arquivo de origem.
    static readonly Dictionary<Kind, string[]> targets = new()
    {
        [Kind.Image] = new[] { "jpg", "png", "webp", "heic", "avif", "tiff", "bmp", "gif", "ico", "pdf" },
        [Kind.Svg] = new[] { "png", "jpg", "webp", "avif", "tiff", "ico", "pdf" },
        [Kind.Audio] = new[] { "mp3", "m4a", "wav", "flac", "ogg", "opus", "aiff", "wma" },
        [Kind.Video] = new[] { "mp4", "mov", "mkv", "webm", "avi", "wmv", "gif", "mp3", "m4a" },
        [Kind.Pdf] = new[] { "docx", "pptx", "xlsx", "jpg", "png", "epub", "txt", "md" },
        [Kind.Doc] = new[] { "pdf", "docx", "odt", "md", "txt", "epub", "html", "rtf" },
        [Kind.Markdown] = new[] { "pdf", "docx", "html", "txt", "epub", "odt" },
        [Kind.Text] = new[] { "pdf", "docx", "md", "epub", "html", "odt" },
        [Kind.Html] = new[] { "pdf", "docx", "md", "txt", "epub", "odt" },
        [Kind.Epub] = new[] { "pdf", "docx", "md", "txt", "html", "odt" },
        [Kind.Slides] = new[] { "pdf", "png", "jpg", "pptx", "odp" },
        [Kind.Sheet] = new[] { "xlsx", "ods", "csv", "tsv", "pdf" },
        [Kind.Csv] = new[] { "xlsx", "ods", "csv", "tsv", "pdf" },
        [Kind.Subtitle] = new[] { "srt", "vtt", "txt" },
        [Kind.Archive] = new[] { "zip", "tar", "tgz" },
    };

    static readonly Dictionary<string, string> subs = new()
    {
        ["jpg"] = "imagem", ["png"] = "imagem", ["webp"] = "imagem", ["avif"] = "imagem", ["heic"] = "imagem", ["tiff"] = "imagem", ["bmp"] = "imagem",
        ["ico"] = "ícone", ["gif"] = "animação", ["pdf"] = "documento", ["mp3"] = "áudio", ["m4a"] = "áudio", ["wav"] = "áudio",
        ["flac"] = "sem perda", ["ogg"] = "áudio", ["opus"] = "áudio", ["aiff"] = "áudio", ["wma"] = "áudio", ["mp4"] = "vídeo",
        ["mov"] = "vídeo", ["mkv"] = "vídeo", ["webm"] = "vídeo", ["avi"] = "vídeo", ["wmv"] = "vídeo", ["txt"] = "texto", ["md"] = "Markdown",
        ["docx"] = "Word", ["odt"] = "Writer", ["rtf"] = "texto rico", ["html"] = "página", ["epub"] = "e-book",
        ["pptx"] = "PowerPoint", ["odp"] = "Impress", ["xlsx"] = "Excel", ["ods"] = "Calc", ["csv"] = "vírgulas", ["tsv"] = "tabulação",
        ["srt"] = "legenda", ["vtt"] = "legenda", ["zip"] = "compactado", ["tar"] = "pacote", ["tgz"] = "tar.gz",
    };

    static bool NeedsOffice(Kind k, string ext) => (k, ext) switch
    {
        (Kind.Doc or Kind.Slides or Kind.Sheet or Kind.Csv, _) => true,
        (Kind.Markdown or Kind.Text or Kind.Html or Kind.Epub, "pdf" or "docx" or "odt" or "epub") => true,
        (Kind.Pdf, "docx" or "pptx" or "xlsx" or "epub") => true,
        _ => false,
    };

    public static List<Op> For(Mode mode, string[] files) => mode switch
    {
        Mode.Convert => Conversions(files),
        Mode.Tools => Tools(files),
        _ => Actions.List(files),
    };

    static Kind? Common(string[] files)
    {
        var ks = files.Select(KindOf).Distinct().ToList();
        if (ks.Count == 1) return ks[0];
        if (ks.All(k => k is Kind.Image or Kind.Svg)) return Kind.Image;
        if (ks.All(k => k is Kind.Sheet or Kind.Csv)) return Kind.Sheet;
        return null;
    }

    static List<Op> Conversions(string[] files)
    {
        var list = new List<Op>();
        if (Common(files) is not Kind k || !targets.TryGetValue(k, out var exts)) return list;
        if (!Office.Ok && k is Kind.Doc or Kind.Slides or Kind.Sheet or Kind.Csv)
        {
            // Sem LibreOffice não dá para converter documentos do Office: oferece instalar na hora em vez de uma roda vazia.
            list.Add(new Op { Label = "LibreOffice", Sub = "instalar para converter", Ui = _ => LibreOfficeSetup.Offer() });
            return list;
        }
        var srcExts = files.Select(Norm).Distinct().ToList();
        foreach (var ext in exts)
        {
            if (srcExts.Count == 1 && srcExts[0] == ext) continue;
            if (k is Kind.Audio or Kind.Video && !Ff.Ok) continue;
            if (NeedsOffice(k, ext) && !Office.Ok) continue;
            var e = ext;
            list.Add(new Op { Label = e == "tgz" ? "TAR.GZ" : e.ToUpperInvariant(), Sub = subs.GetValueOrDefault(e, ""), One = (f, _) => ConvertOne(f, e) });
        }
        return list;
    }

    static async Task<string?> ConvertOne(string src, string ext)
    {
        var k = KindOf(src);
        if (Norm(src) == ext) return null; // já está nesse formato
        switch (k)
        {
            case Kind.Image or Kind.Svg: return await Task.Run(() => Images.Convert(src, ext));
            case Kind.Audio: return await Ff.ToAudio(src, ext, false);
            case Kind.Video: return ext is "mp3" or "m4a" ? await Ff.ToAudio(src, ext, true) : await Ff.ToVideo(src, ext);
            case Kind.Pdf:
                return ext switch
                {
                    "png" or "jpg" => await Pdf.ToImages(src, ext, src),
                    "txt" => await Task.Run(() => Pdf.ToText(src)),
                    "md" => await Task.Run(() => Pdf.ToMarkdown(src)),
                    "epub" => await Pdf.ToEpub(src),
                    "xlsx" => await Pdf.ToSheet(src, "xlsx"),
                    "pptx" => await Pdf.PptxFromPdf(src),
                    _ => await Office.Here(src, ext, "writer_pdf_import"),
                };
            case Kind.Markdown: return await TextDocs.FromMarkdown(src, ext);
            case Kind.Text: return await TextDocs.FromText(src, ext);
            case Kind.Html: return await TextDocs.FromHtmlFile(src, ext);
            case Kind.Epub: return await TextDocs.FromEpub(src, ext);
            case Kind.Doc: return await TextDocs.FromOffice(src, ext);
            case Kind.Slides:
                if (ext is "png" or "jpg")
                {
                    var tmp = Paths.Temp();
                    try
                    {
                        var pdf = await Office.Convert(src, "pdf", Path.Combine(tmp, "slides.pdf"), null);
                        return await Pdf.ToImages(pdf, ext, src);
                    }
                    finally { Paths.TryDelete(tmp); }
                }
                return await Office.Here(src, ext);
            case Kind.Sheet or Kind.Csv: return await Office.Here(src, ext, Office.CsvFilter(src));
            case Kind.Subtitle: return await Task.Run(() => Subtitles.Convert(src, ext));
            case Kind.Archive: return await Archives.Convert(src, ext);
        }
        throw new Exception("Tipo de arquivo não suportado");
    }

    static Op T(string label, string sub, Func<string, P, Task<string?>> one, Func<string[], P?>? ask = null) => new() { Label = label, Sub = sub, One = one, Ask = ask };
    static Op TA(string label, string sub, Func<string[], P, Task<string?>> all, Func<string[], P?>? ask = null) => new() { Label = label, Sub = sub, All = all, Ask = ask };
    static Func<string, P, Task<string?>> Sync(Func<string, P, string> f) => (s, p) => Task.Run(() => (string?)f(s, p));

    static List<Op> Tools(string[] files)
    {
        var list = new List<Op>();
        var k = Common(files);
        bool many = files.Length > 1, one = !many;

        if (k == Kind.Image)
        {
            list.Add(T("Comprimir", "menor", Sync(Images.Compress), fs => Dialogs.Compress(fs, "imagem")));
            list.Add(T("Tamanho", "redimensionar", Sync(Images.Resize), Dialogs.ResizeImage));
            list.Add(T("Girar", "ou espelhar", Sync(Images.Rotate), fs => Dialogs.Rotate(fs, "imagem")));
            if (one) list.Add(T("Editar", "recortar, ajustar…", Sync(Images.Edit), fs => ImageEditor.Ask(fs[0], false)));
            list.Add(T("Fundo", "cor e margem", Sync(Images.Background), Dialogs.Background));
            if (Meta.Ok) list.Add(T("Metadados", "ver e editar", (f, p) => Meta.Apply(f, p).AsNullable(), fs => MetaWindow.Ask(fs)));
            list.Add(new Op { Label = "Ler QR", Sub = "códigos", All = (fs, _) => Task.Run(() => QrWindow.Read(fs)), Show = QrWindow.Show });
            list.Add(TA("PDF", many ? "juntar em um" : "imagem em PDF", (fs, _) => Task.Run(() => (string?)Images.ToPdf(fs))));
            if (many) list.Add(TA("Colagem", "uma imagem só", (fs, p) => Task.Run(() => (string?)Images.Collage(fs, p)), Dialogs.Collage));
        }
        if (k == Kind.Video && Ff.Ok)
        {
            list.Add(T("Comprimir", "menor", (f, p) => Ff.CompressVideo(f, p).AsNullable(), fs => Dialogs.Compress(fs, "video")));
            if (one) list.Add(T("Cortar", "trecho", (f, p) => Ff.Trim(f, p).AsNullable(), fs => MediaWindow.Ask(fs[0], MediaMode.Trim)));
            if (one) list.Add(T("Recortar", "enquadrar", (f, p) => Ff.Crop(f, p).AsNullable(), fs => ImageEditor.AskVideoCrop(fs[0])));
            list.Add(T("Velocidade", "acelerar", (f, p) => Ff.Speed(f, p).AsNullable(), Dialogs.Speed));
            if (one) list.Add(T("Dividir", "em partes", (f, p) => Ff.Split(f, p).AsNullable(), fs => MediaWindow.Ask(fs[0], MediaMode.Split)));
            if (one) list.Add(T("Quadro", "foto do vídeo", (f, p) => Ff.Frame(f, p).AsNullable(), fs => MediaWindow.Ask(fs[0], MediaMode.Frame)));
            list.Add(T("Áudio", "extrair", (f, p) => Ff.ToAudio(f, p.S("formato", "mp3"), true).AsNullable(), Dialogs.ExtractAudio));
            list.Add(T("Girar", "ou espelhar", (f, p) => Ff.RotateVideo(f, p).AsNullable(), fs => Dialogs.Rotate(fs, "video")));
            list.Add(T("Tamanho", "resolução", (f, p) => Ff.ResizeVideo(f, p).AsNullable(), Dialogs.ResizeVideo));
            list.Add(T("Mudo", "tirar o som", (f, _) => Ff.Mute(f).AsNullable()));
            if (Meta.Ok) list.Add(T("Metadados", "ver e editar", (f, p) => Meta.Apply(f, p).AsNullable(), fs => MetaWindow.Ask(fs)));
            if (many) list.Add(TA("Juntar", "um vídeo só", (fs, _) => Ff.Concat(fs, true).AsNullable()));
        }
        if (k == Kind.Audio && Ff.Ok)
        {
            list.Add(T("Comprimir", "menor", (f, p) => Ff.CompressAudio(f, p).AsNullable(), fs => Dialogs.Compress(fs, "audio")));
            if (one) list.Add(T("Cortar", "trecho", (f, p) => Ff.Trim(f, p).AsNullable(), fs => MediaWindow.Ask(fs[0], MediaMode.Trim)));
            list.Add(T("Normalizar", "volume", (f, p) => Ff.Normalize(f, p).AsNullable(), Dialogs.Normalize));
            if (one) list.Add(T("Bipe", "censurar", (f, p) => Ff.Bleep(f, p).AsNullable(), fs => MediaWindow.Ask(fs[0], MediaMode.Bleep)));
            list.Add(T("Canais", "mono, estéreo", (f, p) => Ff.Channels(f, p).AsNullable(), Dialogs.Channels));
            list.Add(T("Velocidade", "acelerar", (f, p) => Ff.Speed(f, p).AsNullable(), Dialogs.Speed));
            if (one) list.Add(T("Dividir", "em partes", (f, p) => Ff.Split(f, p).AsNullable(), fs => MediaWindow.Ask(fs[0], MediaMode.Split)));
            if (Meta.Ok) list.Add(T("Metadados", "ver e editar", (f, p) => Meta.Apply(f, p).AsNullable(), fs => MetaWindow.Ask(fs)));
            if (many) list.Add(TA("Juntar", "um áudio só", (fs, _) => Ff.Concat(fs, false).AsNullable()));
        }
        if (k == Kind.Pdf)
        {
            if (Gs.Ok) list.Add(T("Comprimir", "menor", (f, p) => Pdf.Compress(f, p).AsNullable(), fs => Dialogs.Compress(fs, "pdf")));
            if (Qpdf.Ok)
            {
                if (one) list.Add(T("Organizar", "páginas", (f, p) => Pdf.Organize(f, p).AsNullable(), fs => PdfPagesWindow.Ask(fs[0])));
                if (!many || files.All(f => Pdf.Pages(f) > 1))
                    list.Add(T("Dividir", "em partes", (f, p) => Pdf.Split(f, p).AsNullable(), Dialogs.SplitPdf));
                if (one && Gs.Ok) list.Add(T("Tarjar", "esconder trechos", (f, p) => Pdf.Redact(f, p).AsNullable(), fs => PdfRedactWindow.Ask(fs[0])));
                if (many) list.Add(TA("Juntar", "um PDF só", (fs, _) => Pdf.Merge(fs).AsNullable()));
            }
            if (Meta.Ok) list.Add(T("Metadados", "ver e editar", (f, p) => Meta.Apply(f, p).AsNullable(), fs => MetaWindow.Ask(fs)));
        }
        if (k == Kind.Archive)
            list.Add(T("Extrair", "aqui", (f, _) => Archives.Extract(f).AsNullable()));

        list.Add(TA("ZIP", "compactar", (fs, _) => Task.Run(() => (string?)Archives.Zip(fs))));
        if (k is Kind.Folder or Kind.Other or null) list.Add(TA("TAR.GZ", "compactar", (fs, _) => Archives.TarGz(fs).AsNullable()));
        return list;
    }

    static async Task<string?> AsNullable(this Task<string> t) => await t;
}
