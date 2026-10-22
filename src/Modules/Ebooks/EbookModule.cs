// Adaptado de Filee.Engines/Ebooks/EbookConverter.cs, Book.cs e ComicBook.cs. Adaptado do Filee (MIT, © 2026 Myeonggu Kim e colaboradores)
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ImageMagick;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SharpCompress.Archives;

namespace Mexerica;

internal sealed class BookMetadata
{
    public string? Title { get; set; }
    public string? Publisher { get; set; }
    public string? Description { get; set; }
    public string? Language { get; set; }
    public string? CoverImage { get; set; }
    public List<string> Authors { get; } = new();
}
internal sealed record Book(List<string> Chapters, BookMetadata Metadata);
internal static class EbookFiles
{
    public const string DrmMessage = "Este e-book está protegido por DRM e não pode ser convertido";
    public static string NewFolder(string root, string name) => Directory.CreateDirectory(Path.Combine(root, name + Guid.NewGuid().ToString("N"))).FullName;
    public static void CheckDrm(ZipArchive zip)
    {
        if (zip.GetEntry("META-INF/encryption.xml") != null || zip.GetEntry("META-INF/rights.xml") != null)
            throw new Exception(DrmMessage);
    }
}

sealed class EbookModule : Module
{
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    static extern int StrCmpLogicalW(string left, string right);
    static readonly string[] Books = { "epub", "kepub", "mobi", "azw", "azw3", "prc", "fb2", "htmlz", "txtz" };
    static readonly string[] Comics = { "cbz", "cbr", "cb7", "cbt" };
    static readonly string[] BookTargets = { "epub", "kepub", "fb2", "htmlz", "txtz", "txt", "html", "md" };
    public override string Name => "E-books";
    public override IEnumerable<string> Inputs => Books.Concat(Comics).Concat(new[] { "azw4", "pdf" });
    public override IEnumerable<Target> TargetsFor(string inExt)
    {
        if (inExt == "pdf") { yield return new("cbz", "páginas"); yield break; }
        if (inExt == "azw4") { yield return new("pdf", "documento"); yield break; }
        if (Comics.Contains(inExt))
        {
            foreach (var ext in new[] { "pdf", "epub", "cbz" }.Where(e => e != inExt)) yield return new(ext, "quadrinho");
            yield break;
        }
        foreach (var ext in BookTargets.Where(e => e != inExt && (inExt != "epub" || e is "kepub" or "fb2" or "htmlz" or "txtz")))
            yield return new(ext, "e-book");
    }

    public override async Task<string?> Convert(string src, string outExt, P p)
    {
        var ext = Modules.Ext(src);
        if (ext == "pdf") return await PdfToCbz(src);
        return await Task.Run(() => ConvertSync(src, ext, outExt));
    }

    static string ConvertSync(string src, string ext, string to)
    {
        Job.Check();
        if (ext == "azw4") { var dst = Paths.Out(src, "pdf"); File.WriteAllBytes(dst, MobiReader.PrintReplicaPdf(src)); return dst; }
        if (ext == "epub" && to == "kepub") { var dst = Paths.Out(src, "kepub.epub"); Kepub.FromEpub(src, dst, Job.Token); return dst; }
        if (ext == "kepub" && to == "epub") { var dst = Paths.Out(src, "epub"); Kepub.ToEpub(src, dst, Job.Token); return dst; }
        if (Comics.Contains(ext)) return Comic(src, ext, to);
        var temp = Paths.Temp();
        try
        {
            var (title, body) = ReadBook(src, ext, temp);
            Job.Progress(.5);
            var dst = Paths.Out(src, to == "kepub" ? "kepub.epub" : to);
            switch (to)
            {
                case "html": File.WriteAllText(dst, Page(title, body), new UTF8Encoding(false)); break;
                case "txt": File.WriteAllText(dst, TextDocs.HtmlToText(body), new UTF8Encoding(true)); break;
                case "md": File.WriteAllText(dst, TextDocs.HtmlToMd(Page(title, body)), new UTF8Encoding(false)); break;
                case "fb2": WriteFb2(dst, title, body); break;
                case "htmlz": WriteZipped(dst, "index.html", Page(title, body)); break;
                case "txtz": WriteZipped(dst, "book.txt", TextDocs.HtmlToText(body)); break;
                case "epub": WriteEpub(dst, title, body); break;
                case "kepub":
                    var epub = Path.Combine(temp, "book.epub");
                    WriteEpub(epub, title, body);
                    Kepub.FromEpub(epub, dst, Job.Token);
                    break;
                default: throw new Exception("Destino de e-book não suportado");
            }
            Job.Progress(1);
            return dst;
        }
        finally { Paths.TryDelete(temp); }
    }

    static (string Title, string Body) ReadBook(string src, string ext, string temp)
    {
        var title = Path.GetFileNameWithoutExtension(src);
        if (ext is "epub" or "kepub")
        {
            using var zip = ZipFile.OpenRead(src); EbookFiles.CheckDrm(zip);
            var html = TextDocs.EpubHtml(src);
            return (title, Body(html));
        }
        if (ext is "mobi" or "azw" or "azw3" or "prc")
        {
            var book = MobiReader.ReadBook(src, temp, Job.Token);
            return (book.Metadata.Title ?? title, string.Join("\n", book.Chapters.Select(c => EmbedImages(Body(File.ReadAllText(c)), Path.GetDirectoryName(c)!))));
        }
        if (ext == "fb2")
        {
            var xml = XDocument.Load(src);
            title = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "book-title")?.Value ?? title;
            var images = xml.Descendants().Where(e => e.Name.LocalName == "binary" && e.Attribute("id") != null)
                .ToDictionary(e => e.Attribute("id")!.Value, e => "data:" + (e.Attribute("content-type")?.Value ?? "image/jpeg") + ";base64," + Regex.Replace(e.Value, "\\s", ""));
            var bodies = xml.Descendants().Where(e => e.Name.LocalName == "body").SelectMany(e => e.Elements()).Select(e => Fb2Html(e, images));
            return (title, string.Join("\n", bodies));
        }
        using var zipText = ZipFile.OpenRead(src);
        var entry = zipText.Entries.FirstOrDefault(e => ext == "htmlz" ? e.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) : e.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) ?? throw new Exception("O e-book não contém texto");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8, true);
        var content = reader.ReadToEnd();
        return (title, ext == "htmlz" ? EmbedImages(Body(content), "", zipText, entry.FullName) : "<pre>" + WebUtility.HtmlEncode(content) + "</pre>");
    }

    static string Fb2Html(XElement e, Dictionary<string, string> images)
    {
        var tag = e.Name.LocalName switch { "section" => "section", "title" => "h2", "subtitle" => "h3", "p" => "p", "emphasis" => "em", "strong" => "strong", "empty-line" => "br", "image" => "img", _ => "div" };
        if (tag == "img")
        {
            var id = e.Attributes().FirstOrDefault(a => a.Name.LocalName == "href")?.Value.TrimStart('#');
            return id != null && images.TryGetValue(id, out var data) ? "<img src=\"" + data + "\">" : "";
        }
        return "<" + tag + ">" + string.Concat(e.Nodes().Select(n => n is XElement child ? Fb2Html(child, images) : WebUtility.HtmlEncode(n.ToString()))) + "</" + tag + ">";
    }

    static string Body(string html)
    {
        var match = Regex.Match(html, "<body[^>]*>(.*?)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : html;
    }
    static string Page(string title, string body) => "<!doctype html><html><head><meta charset=\"utf-8\"><title>" + WebUtility.HtmlEncode(title) + "</title></head><body>" + body + "</body></html>";

    static string EmbedImages(string body, string root, ZipArchive? zip = null, string? entry = null) => Regex.Replace(body,
        "(<img[^>]*\\bsrc=[\"'])([^\"']+)([\"'])", m =>
        {
            var relative = WebUtility.UrlDecode(m.Groups[2].Value);
            if (relative.StartsWith("data:")) return m.Value;
            if (relative.Contains(":") || relative.StartsWith('/')) return m.Value;
            byte[] bytes;
            if (zip != null)
            {
                var baseDir = Path.GetDirectoryName(entry)?.Replace('\\', '/') ?? "";
                var path = (baseDir.Length == 0 ? "" : baseDir + "/") + relative;
                var item = zip.GetEntry(path);
                if (item == null) return m.Value;
                using var stream = item.Open(); using var ms = new MemoryStream(); stream.CopyTo(ms); bytes = ms.ToArray();
            }
            else
            {
                var full = Path.GetFullPath(Path.Combine(root, relative));
                if (!full.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return m.Value;
                bytes = File.ReadAllBytes(full);
            }
            var mime = Path.GetExtension(relative).ToLowerInvariant() switch { ".png" => "image/png", ".gif" => "image/gif", ".webp" => "image/webp", _ => "image/jpeg" };
            return m.Groups[1].Value + "data:" + mime + ";base64," + System.Convert.ToBase64String(bytes) + m.Groups[3].Value;
        }, RegexOptions.IgnoreCase);

    static void WriteZipped(string dst, string name, string content)
    {
        using var zip = ZipFile.Open(dst, ZipArchiveMode.Create);
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    static void WriteFb2(string dst, string title, string body)
    {
        var text = TextDocs.HtmlToText(body).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        XNamespace ns = "http://www.gribuser.ru/xml/fictionbook/2.0";
        XNamespace link = "http://www.w3.org/1999/xlink";
        var pictures = Regex.Matches(body, "data:(image/(?:png|jpeg|gif));base64,([A-Za-z0-9+/=]+)").Cast<Match>().ToList();
        var fb = new XDocument(new XElement(ns + "FictionBook",
            new XElement(ns + "description", new XElement(ns + "title-info", new XElement(ns + "book-title", title))),
            new XElement(ns + "body", new XElement(ns + "section", new XElement(ns + "title", new XElement(ns + "p", title)),
                text.Select(p => new XElement(ns + "p", p)),
                pictures.Select((_, i) => new XElement(ns + "p", new XElement(ns + "image", new XAttribute(link + "href", "#picture" + i)))))),
            pictures.Select((m, i) => new XElement(ns + "binary", new XAttribute("id", "picture" + i), new XAttribute("content-type", m.Groups[1].Value), m.Groups[2].Value))));
        fb.Save(dst);
    }

    static void WriteEpub(string dst, string title, string body)
    {
        var images = new List<(string Name, byte[] Data, string Mime)>();
        body = Regex.Replace(body, "data:(image/(?:png|jpeg|gif|webp));base64,([A-Za-z0-9+/=]+)", m =>
        {
            var mime = m.Groups[1].Value;
            var name = "images/" + (images.Count + 1) + "." + (mime == "image/jpeg" ? "jpg" : mime[6..]);
            images.Add((name, System.Convert.FromBase64String(m.Groups[2].Value), mime));
            return name;
        });
        using var zip = ZipFile.Open(dst, ZipArchiveMode.Create);
        void Add(string name, string content, CompressionLevel level = CompressionLevel.Optimal)
        { using var writer = new StreamWriter(zip.CreateEntry(name, level).Open(), new UTF8Encoding(false)); writer.Write(content); }
        Add("mimetype", "application/epub+zip", CompressionLevel.NoCompression);
        Add("META-INF/container.xml", "<?xml version=\"1.0\"?><container xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\" version=\"1.0\"><rootfiles><rootfile full-path=\"content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles></container>");
        var manifest = string.Join("", images.Select((im, i) => "<item id=\"img" + i + "\" href=\"" + im.Name + "\" media-type=\"" + im.Mime + "\"/>"));
        Add("content.opf", "<?xml version=\"1.0\"?><package xmlns=\"http://www.idpf.org/2007/opf\" version=\"3.0\" unique-identifier=\"id\"><metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:identifier id=\"id\">urn:uuid:" + Guid.NewGuid() + "</dc:identifier><dc:title>" + WebUtility.HtmlEncode(title) + "</dc:title><dc:language>pt-BR</dc:language></metadata><manifest><item id=\"chapter\" href=\"chapter.xhtml\" media-type=\"application/xhtml+xml\"/>" + manifest + "</manifest><spine><itemref idref=\"chapter\"/></spine></package>");
        var parsed = new HtmlParser().ParseDocument(Page(title, body));
        XNamespace ns = "http://www.w3.org/1999/xhtml";
        XNode? Node(INode n) => n is IElement child ? Element(child) : n is IText text ? new XText(text.Data) : null;
        XElement Element(IElement e) => new(ns + (e.LocalName.Contains(':') ? "span" : e.LocalName),
            e.Attributes.Where(a => !a.Name.Contains(':')).Select(a => new XAttribute(a.Name, a.Value)),
            e.ChildNodes.Select(Node));
        var chapter = new XDocument(new XElement(ns + "html",
            new XElement(ns + "head", new XElement(ns + "title", title)),
            new XElement(ns + "body", parsed.Body!.ChildNodes.Select(Node))));
        Add("chapter.xhtml", chapter.ToString());
        foreach (var (name, data, _) in images) { using var stream = zip.CreateEntry(name).Open(); stream.Write(data); }
    }

    static string Comic(string src, string ext, string to)
    {
        var temp = Paths.Temp();
        try
        {
            var pages = ExtractPages(src, temp);
            if (pages.Count == 0) throw new Exception("O quadrinho não contém imagens");
            var dst = Paths.Out(src, to);
            if (to == "cbz") WriteCbz(dst, pages);
            else if (to == "pdf")
            {
                try { ComicPdf(dst, pages); }
                catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
                {
                    Paths.TryDelete(dst);
                    File.Move(Images.ToPdf(pages.ToArray()), dst);
                }
            }
            else if (to == "epub")
            {
                var html = string.Join("", pages.Select(p =>
                {
                    using var image = new MagickImage(p);
                    var png = Path.ChangeExtension(p, ".epub.png");
                    image.Write(png, MagickFormat.Png);
                    return "<div style=\"page-break-after:always\"><img style=\"max-width:100%\" src=\"data:image/png;base64," + System.Convert.ToBase64String(File.ReadAllBytes(png)) + "\"/></div>";
                }));
                WriteEpub(dst, Path.GetFileNameWithoutExtension(src), html);
            }
            Job.Progress(1); return dst;
        }
        finally { Paths.TryDelete(temp); }
    }

    static List<string> ExtractPages(string src, string temp)
    {
        using var archive = ArchiveFactory.OpenArchive(src);
        if (archive.Entries.Any(e => e.IsEncrypted)) throw new Exception("O quadrinho está protegido por senha");
        var entries = archive.Entries.Where(e => !e.IsDirectory && Regex.IsMatch(e.Key ?? "", "\\.(jpe?g|png|gif|webp|bmp|tiff?)$", RegexOptions.IgnoreCase))
            .OrderBy(e => e.Key ?? "", Comparer<string>.Create(StrCmpLogicalW)).ToList();
        var pages = new List<string>();
        // shortcut: 7z sólido pode reprocessar blocos por página; usar leitura sequencial se ficar lento em quadrinhos grandes.
        foreach (var entry in entries)
        {
            Job.Check();
            var path = Path.Combine(temp, (pages.Count + 1).ToString("D5") + Path.GetExtension(entry.Key));
            using (var input = entry.OpenEntryStream()) using (var output = File.Create(path)) input.CopyTo(output);
            pages.Add(path);
        }
        return pages;
    }

    static void ComicPdf(string dst, List<string> pages)
    {
        using var document = new PdfDocument();
        foreach (var file in pages)
        {
            Job.Check();
            using var image = new MagickImage(file);
            var safe = Path.ChangeExtension(file, ".png");
            image.Write(safe, MagickFormat.Png);
            using var x = XImage.FromFile(safe);
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(Math.Min(14400, image.Width * .75));
            page.Height = XUnit.FromPoint(Math.Min(14400, image.Height * .75));
            using var graphics = XGraphics.FromPdfPage(page);
            graphics.DrawImage(x, 0, 0, page.Width.Point, page.Height.Point);
        }
        document.Save(dst);
    }

    static void WriteCbz(string dst, List<string> pages)
    {
        using var zip = ZipFile.Open(dst, ZipArchiveMode.Create);
        for (var i = 0; i < pages.Count; i++) { Job.Check(); zip.CreateEntryFromFile(pages[i], (i + 1).ToString("D5") + Path.GetExtension(pages[i])); }
    }

    static async Task<string> PdfToCbz(string src)
    {
        var temp = Paths.Temp();
        try
        {
            var document = await Pdf.Open(src);
            if (document.PageCount == 0) throw new Exception("O PDF não contém páginas");
            var pages = new List<string>();
            for (uint i = 0; i < document.PageCount; i++)
            {
                Job.Check();
                var file = Path.Combine(temp, (i + 1).ToString("D5") + ".png");
                await Pdf.Render(document, i, file, "png", Config.Now.PdfDpi);
                pages.Add(file);
                Job.Progress((i + 1.0) / document.PageCount);
            }
            var dst = Paths.Out(src, "cbz");
            await Task.Run(() => WriteCbz(dst, pages));
            return dst;
        }
        finally { Paths.TryDelete(temp); }
    }
}

