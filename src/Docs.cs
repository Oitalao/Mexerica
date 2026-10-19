using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using ImageMagick;
using ImageMagick.Drawing;
using Markdig;

namespace Mexerica;

static class Office
{
    public static readonly string? Exe = Find();
    public static bool Ok => Exe != null;
    // Dois LibreOffice ao mesmo tempo, cada um com seu perfil (o mesmo perfil não aceita duas cópias abertas).
    static readonly SemaphoreSlim gate = new(2, 2);
    static readonly System.Collections.Concurrent.ConcurrentBag<string> profiles = new(new[] { "lo-perfil", "lo-perfil-2" });

    static string? Find()
    {
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            var p = Path.Combine(root, "LibreOffice", "program", "soffice.com");
            if (File.Exists(p)) return p;
        }
        return Paths.FindTool("soffice.com");
    }

    public static string Spec(string ext) => ext switch
    {
        "pdf" => "pdf",
        "docx" => "docx:MS Word 2007 XML",
        "odt" => "odt",
        "rtf" => "rtf",
        "txt" => "txt:Text (encoded):UTF8",
        "html" => "html:XHTML Writer File:UTF8",
        "epub" => "epub",
        "pptx" => "pptx:Impress MS PowerPoint 2007 XML",
        "odp" => "odp",
        "xlsx" => "xlsx:Calc MS Excel 2007 XML",
        "ods" => "ods",
        "csv" => "csv:Text - txt - csv (StarCalc):44,34,76,1,,0,false,true,true,false",
        "tsv" => "csv:Text - txt - csv (StarCalc):9,34,76,1,,0,false,true,true,false",
        _ => ext,
    };

    /// Converte com o LibreOffice sem tela, um por vez, com perfil próprio
    /// (não briga com um LibreOffice que a pessoa tenha aberto).
    public static async Task<string> Convert(string src, string ext, string dst, string? infilter = null)
    {
        if (Exe == null) throw new Exception("LibreOffice não encontrado");
        await gate.WaitAsync(Job.Token);
        if (!profiles.TryTake(out var mine)) mine = "lo-perfil-" + Guid.NewGuid().ToString("N")[..6];
        var tmp = Paths.Temp();
        try
        {
            var profile = new Uri(Path.Combine(Paths.Data, mine) + "\\").AbsoluteUri;
            var a = new List<string> { "-env:UserInstallation=" + profile, "--headless", "--norestore", "--nolockcheck", "--nologo", "--nodefault" };
            if (infilter != null) a.Add("--infilter=" + infilter);
            a.AddRange(new[] { "--convert-to", Spec(ext), "--outdir", tmp, src });
            await Proc.Run(Exe, a, 300);
            var made = Directory.GetFiles(tmp).FirstOrDefault(f => !f.EndsWith(".tmp")) ?? throw new Exception("O LibreOffice não conseguiu converter esse arquivo");
            if (new FileInfo(made).Length == 0) throw new Exception("O LibreOffice gerou um arquivo vazio");
            File.Move(made, dst);
            // HTML exportado pode levar imagens junto
            foreach (var extra in Directory.GetFiles(tmp))
                File.Move(extra, Path.Combine(Path.GetDirectoryName(dst)!, Path.GetFileName(extra)), false);
            return dst;
        }
        finally { Paths.TryDelete(tmp); profiles.Add(mine); gate.Release(); }
    }

    /// Converte para um arquivo novo ao lado do original (ou na pasta escolhida).
    public static Task<string> Here(string src, string ext, string? infilter = null) =>
        Convert(src, ext, Paths.Out(src, ext), infilter);

    public static string? CsvFilter(string src) =>
        Path.GetExtension(src).Equals(".tsv", StringComparison.OrdinalIgnoreCase) ? "CSV:9,34,76" :
        Path.GetExtension(src).Equals(".csv", StringComparison.OrdinalIgnoreCase) ? "CSV:44,34,76" : null;
}

static class Gs
{
    public static readonly string? Exe = Paths.FindTool("gswin64c.exe") ?? FindInstalled();
    public static bool Ok => Exe != null;

    static string? FindInstalled()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "gs");
        if (!Directory.Exists(root)) return null;
        return Directory.GetFiles(root, "gswin64c.exe", SearchOption.AllDirectories).OrderByDescending(f => f).FirstOrDefault();
    }

    public static Task Run(IEnumerable<string> args) =>
        Proc.Run(Exe ?? throw new Exception("Ghostscript não encontrado"), new[] { "-q", "-dNOPAUSE", "-dBATCH", "-dSAFER" }.Concat(args), 1800);
}

static class Qpdf
{
    public static readonly string? Exe = Paths.FindTool(Path.Combine("qpdf", "qpdf.exe")) ?? Paths.FindTool("qpdf.exe");
    public static bool Ok => Exe != null;

    public static async Task Run(IEnumerable<string> args)
    {
        try { await Proc.Run(Exe ?? throw new Exception("qpdf não encontrado"), new[] { "--warning-exit-0" }.Concat(args), 900); }
        catch (Exception ex) when (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
        { throw new Exception("Esse PDF tem senha"); }
    }
}

static class Pdf
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static async Task<string> ToImages(string pdf, string ext, string namedAfter)
    {
        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(pdf);
        var doc = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);
        if (doc.PageCount == 1)
        {
            var dst = Paths.Out(namedAfter, ext);
            await Render(doc, 0, dst, ext, Config.Now.PdfDpi);
            return dst;
        }
        var dir = Paths.OutDir(namedAfter, "páginas");
        Directory.CreateDirectory(dir);
        var b = Path.GetFileNameWithoutExtension(namedAfter);
        try
        {
            for (uint i = 0; i < doc.PageCount; i++)
            {
                Job.Progress(i / (double)doc.PageCount);
                await Render(doc, i, Path.Combine(dir, $"{b} {i + 1:000}.{ext}"), ext, Config.Now.PdfDpi);
            }
        }
        catch { Paths.TryDelete(dir); throw; }
        return dir;
    }

    public static async Task Render(Windows.Data.Pdf.PdfDocument doc, uint i, string dst, string ext, int dpi, uint? width = null)
    {
        Job.Check();
        using var page = doc.GetPage(i);
        var w = width ?? (uint)Math.Min(12000, Math.Round(page.Size.Width * dpi / 96));
        var o = new Windows.Data.Pdf.PdfPageRenderOptions
        {
            DestinationWidth = w,
            BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255),
            BitmapEncoderId = ext == "jpg" ? Windows.Graphics.Imaging.BitmapEncoder.JpegEncoderId : Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
        };
        using var ras = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(ras, o);
        ras.Seek(0);
        using var s = ras.AsStream();
        using var fs = File.Create(dst);
        await s.CopyToAsync(fs);
    }

    public static async Task<Windows.Data.Pdf.PdfDocument> Open(string pdf) =>
        await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(await Windows.Storage.StorageFile.GetFileFromPathAsync(pdf));

    /// Parágrafos do PDF na ordem de leitura (blocos de texto pela posição na página).
    static List<string> Paragraphs(string src)
    {
        using var d = UglyToad.PdfPig.PdfDocument.Open(src);
        var paras = new List<string>();
        foreach (var p in d.GetPages())
        {
            Job.Check();
            var words = p.GetWords(UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor.NearestNeighbourWordExtractor.Instance).ToList();
            if (words.Count == 0) continue;
            var blocks = UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter.DocstrumBoundingBoxes.Instance.GetBlocks(words);
            var ordered = UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector.UnsupervisedReadingOrderDetector.Instance.Get(blocks);
            foreach (var block in ordered)
            {
                var sb = new StringBuilder();
                foreach (var line in block.TextLines)
                {
                    var l = line.Text.Trim();
                    if (l.Length == 0) continue;
                    if (sb.Length > 0)
                    {
                        if (sb[^1] == '-' && char.IsLower(l[0])) sb.Length--;
                        else sb.Append(' ');
                    }
                    sb.Append(l);
                }
                if (sb.Length > 0) paras.Add(sb.ToString());
            }
        }
        // Parágrafo partido na virada da página: junta se o anterior não terminou a frase.
        for (int i = paras.Count - 1; i > 0; i--)
            if (!".!?:;\"”)".Contains(paras[i - 1][^1]) && paras[i].Length > 0 && char.IsLower(paras[i][0])) { paras[i - 1] += " " + paras[i]; paras.RemoveAt(i); }
        if (paras.Count == 0) throw new Exception("Esse PDF não tem texto (é imagem escaneada)");
        return paras;
    }

    public static string ToText(string src)
    {
        var dst = Paths.Out(src, "txt");
        using var d = UglyToad.PdfPig.PdfDocument.Open(src);
        var sb = new StringBuilder();
        foreach (var p in d.GetPages())
        {
            Job.Check();
            sb.AppendLine(UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(p));
            sb.AppendLine();
        }
        if (sb.ToString().Trim().Length == 0) throw new Exception("Esse PDF não tem texto (é imagem escaneada)");
        File.WriteAllText(dst, sb.ToString(), new UTF8Encoding(true));
        return dst;
    }

    public static string ToMarkdown(string src)
    {
        var dst = Paths.Out(src, "md");
        File.WriteAllText(dst, string.Join("\n\n", Paragraphs(src).Select(EscapeMd)) + "\n", new UTF8Encoding(false));
        return dst;
    }

    static string EscapeMd(string s) => Regex.Replace(s, @"^([#>*+\-]|\d+\.)\s", m => "\\" + m.Value);

    public static string HtmlOf(string src) =>
        TextDocs.Page(Path.GetFileNameWithoutExtension(src), string.Join("\n", Paragraphs(src).Select(p => $"<p>{WebUtility.HtmlEncode(p)}</p>")));

    public static async Task<string> ToEpub(string src)
    {
        var tmp = Paths.Temp();
        try
        {
            var h = Path.Combine(tmp, Path.GetFileNameWithoutExtension(src) + ".html");
            File.WriteAllText(h, HtmlOf(src), new UTF8Encoding(false));
            return await Office.Convert(h, "epub", Paths.Out(src, "epub"), "HTML (StarWriter)");
        }
        finally { Paths.TryDelete(tmp); }
    }

    /// Tabelas do PDF para planilha: agrupa palavras em linhas e colunas pela posição.
    public static async Task<string> ToSheet(string src, string ext)
    {
        using var d = UglyToad.PdfPig.PdfDocument.Open(src);
        var rows = new List<List<string>>();
        foreach (var page in d.GetPages())
        {
            Job.Check();
            var words = page.GetWords().Where(w => w.Text.Trim().Length > 0).ToList();
            if (words.Count == 0) continue;
            double lineTol = words.Average(w => w.BoundingBox.Height) * 0.6;
            var lines = new List<List<UglyToad.PdfPig.Content.Word>>();
            foreach (var w in words.OrderByDescending(w => w.BoundingBox.Bottom))
            {
                var line = lines.FirstOrDefault(l => Math.Abs(l[0].BoundingBox.Bottom - w.BoundingBox.Bottom) < lineTol);
                if (line == null) lines.Add(new List<UglyToad.PdfPig.Content.Word> { w }); else line.Add(w);
            }
            foreach (var line in lines)
            {
                var ordered = line.OrderBy(w => w.BoundingBox.Left).ToList();
                double space = ordered.Average(w => w.BoundingBox.Width / Math.Max(1, w.Text.Length)) * 1.8;
                var cells = new List<string>(); var cur = new StringBuilder(); double lastRight = double.NaN;
                foreach (var w in ordered)
                {
                    if (!double.IsNaN(lastRight) && w.BoundingBox.Left - lastRight > space) { cells.Add(cur.ToString()); cur.Clear(); }
                    if (cur.Length > 0) cur.Append(' ');
                    cur.Append(w.Text);
                    lastRight = w.BoundingBox.Right;
                }
                cells.Add(cur.ToString());
                rows.Add(cells);
            }
            rows.Add(new List<string>());
        }
        if (rows.Count == 0) throw new Exception("Esse PDF não tem texto (é imagem escaneada)");
        var tmp = Paths.Temp();
        try
        {
            var csv = Path.Combine(tmp, Path.GetFileNameWithoutExtension(src) + ".csv");
            File.WriteAllLines(csv, rows.Select(r => string.Join(",", r.Select(c => "\"" + c.Replace("\"", "\"\"") + "\""))), new UTF8Encoding(false));
            if (ext == "csv") { var dst = Paths.Out(src, "csv"); File.Copy(csv, dst); return dst; }
            return await Office.Convert(csv, ext, Paths.Out(src, ext), "CSV:44,34,76");
        }
        finally { Paths.TryDelete(tmp); }
    }

    public static int Pages(string src)
    {
        try { using var d = UglyToad.PdfPig.PdfDocument.Open(src); return d.NumberOfPages; } catch { return 0; }
    }

    public static async Task<string> Merge(string[] files)
    {
        var dst = Paths.Out(files[0], "pdf", "juntos");
        var a = new List<string> { "--empty", "--pages" };
        a.AddRange(files); a.AddRange(new[] { "--", dst });
        try { await Qpdf.Run(a); } catch { Paths.TryDelete(dst); throw; }
        return dst;
    }

    /// modo: paginas (uma por arquivo), cada (a cada N páginas), intervalos ("1-3,4,5-9").
    public static async Task<string> Split(string src, P p)
    {
        int n = Pages(src);
        if (n == 0) throw new Exception("Esse PDF está danificado");
        var groups = new List<(int a, int b)>();
        switch (p.S("modo", "paginas"))
        {
            case "cada":
                int k = Math.Max(1, p.I("cada", 2));
                for (int i = 1; i <= n; i += k) groups.Add((i, Math.Min(n, i + k - 1)));
                break;
            case "intervalos":
                groups = ParseRanges(p.S("intervalos"), n);
                if (groups.Count == 0) throw new Exception("Nenhum intervalo válido (exemplo: 1-3, 4, 5-9)");
                break;
            default:
                for (int i = 1; i <= n; i++) groups.Add((i, i));
                break;
        }
        var dir = Paths.OutDir(src, "partes");
        Directory.CreateDirectory(dir);
        var b = Path.GetFileNameWithoutExtension(src);
        try
        {
            for (int i = 0; i < groups.Count; i++)
            {
                Job.Part(i, groups.Count);
                var (x, y) = groups[i];
                var name = x == y ? $"{b} p{x:000}.pdf" : $"{b} p{x:000}-{y:000}.pdf";
                await Qpdf.Run(new[] { src, "--pages", ".", $"{x}-{y}", "--", Path.Combine(dir, name) });
            }
        }
        catch { Paths.TryDelete(dir); throw; }
        return dir;
    }

    public static List<(int a, int b)> ParseRanges(string s, int max)
    {
        var list = new List<(int, int)>();
        foreach (var part in s.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var f = part.Split('-');
            if (f.Length == 1 && int.TryParse(f[0], out var one) && one >= 1 && one <= max) list.Add((one, one));
            else if (f.Length == 2 && int.TryParse(f[0], out var a) && int.TryParse(f[1], out var b) && a >= 1 && b >= a && a <= max)
                list.Add((a, Math.Min(b, max)));
        }
        return list;
    }

    /// ordem="3,1,2r90,5r180": páginas na nova ordem, com giro opcional; páginas fora da lista saem.
    public static async Task<string> Organize(string src, P p)
    {
        var items = p.S("ordem").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t =>
        {
            var m = Regex.Match(t.Trim(), @"^(\d+)(?:r(\d+))?$");
            if (!m.Success) throw new Exception("Ordem de páginas inválida");
            return (page: int.Parse(m.Groups[1].Value), rot: m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0);
        }).ToList();
        if (items.Count == 0) throw new Exception("Nenhuma página ficou");
        int n = Pages(src);
        if (n == 0) throw new Exception("Esse PDF está danificado");
        if (items.Any(i => i.page < 1 || i.page > n)) throw new Exception("Página fora do PDF");
        var dst = Paths.Out(src, "pdf", "organizado");
        var a = new List<string> { "--empty", "--pages", src, string.Join(",", items.Select(i => i.page)), "--" };
        for (int i = 0; i < items.Count; i++)
            if (items[i].rot % 360 != 0) a.Add($"--rotate=+{items[i].rot % 360}:{i + 1}");
        a.Add(dst);
        try { await Qpdf.Run(a); } catch { Paths.TryDelete(dst); throw; }
        return dst;
    }

    /// Comprime: nivel leve/media/forte ou tamanho alvo (modo pct/mb), baixando a resolução das imagens.
    public static async Task<string> Compress(string src, P p)
    {
        if (!Gs.Ok) throw new Exception("Ghostscript não encontrado");
        long target = p.S("modo") switch
        {
            "pct" => (long)(new FileInfo(src).Length * Math.Clamp(p.D("pct", 50), 1, 99) / 100),
            "mb" => (long)(p.D("mb", 2) * 1024 * 1024),
            _ => 0,
        };
        var dst = Paths.Out(src, "pdf", "comprimido");
        var tmp = Paths.Temp();
        try
        {
            async Task<string> Try(int dpi, double qf, int idx)
            {
                var o = Path.Combine(tmp, $"t{idx}.pdf");
                var dict = $"<< /QFactor {qf.ToString("0.00", Inv)} /Blend 1 /HSamples [2 1 1 2] /VSamples [2 1 1 2] >>";
                await Gs.Run(new[]
                {
                    "-sDEVICE=pdfwrite", "-dCompatibilityLevel=1.6", "-dPDFSETTINGS=/ebook", "-dDetectDuplicateImages=true",
                    "-dDownsampleColorImages=true", "-dDownsampleGrayImages=true", "-dDownsampleMonoImages=true",
                    $"-dColorImageResolution={dpi}", $"-dGrayImageResolution={dpi}", $"-dMonoImageResolution={Math.Max(dpi * 2, 300)}",
                    "-dColorImageDownsampleThreshold=1.0", "-dGrayImageDownsampleThreshold=1.0",
                    "-dAutoFilterColorImages=false", "-dAutoFilterGrayImages=false", "-dColorImageFilter=/DCTEncode", "-dGrayImageFilter=/DCTEncode",
                    "-o", o, "-c", $"<< /ColorImageDict {dict} /GrayImageDict {dict} >> setdistillerparams", "-f", src,
                });
                return o;
            }
            string result;
            if (target <= 0)
            {
                var (dpi, qf) = p.S("nivel", "media") switch { "leve" => (200, 0.15), "forte" => (96, 0.76), _ => (150, 0.4) };
                result = await Try(dpi, qf, 0);
            }
            else
            {
                var steps = new[] { (200, 0.15), (150, 0.3), (120, 0.5), (96, 0.76), (72, 1.0), (50, 1.3) };
                result = "";
                for (int i = 0; i < steps.Length; i++)
                {
                    Job.Progress(i / (double)steps.Length);
                    result = await Try(steps[i].Item1, steps[i].Item2, i);
                    if (new FileInfo(result).Length <= target) break;
                }
            }
            // Se não ficou menor, avisa em vez de entregar um arquivo maior.
            if (new FileInfo(result).Length >= new FileInfo(src).Length)
                throw new Exception("Esse PDF já está bem comprimido; não deu para reduzir mais");
            File.Move(result, dst);
            return dst;
        }
        finally { Paths.TryDelete(tmp); }
    }

    /// Tarja de verdade: as páginas marcadas viram imagem com retângulos pretos (o texto embaixo some).
    /// tarjas="pagina:x,y,w,h;..." com x,y,w,h em fração da página (0 a 1, origem no canto de cima).
    public static async Task<string> Redact(string src, P p)
    {
        var byPage = new Dictionary<int, List<double[]>>();
        foreach (var part in p.S("tarjas").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = part.Split(':');
            if (f.Length != 2 || !int.TryParse(f[0], out var pg)) continue;
            var r = Images.Nums(f[1]);
            if (r.Length != 4 || r[2] <= 0 || r[3] <= 0) continue;
            if (!byPage.TryGetValue(pg, out var l)) byPage[pg] = l = new();
            l.Add(r);
        }
        if (byPage.Count == 0) throw new Exception("Nenhuma tarja marcada");
        int n = Pages(src);
        if (n == 0) throw new Exception("Esse PDF está danificado");
        var tmp = Paths.Temp();
        var dst = Paths.Out(src, "pdf", "tarjado");
        try
        {
            int dpi = 200, k = 0;
            foreach (var (pg, rects) in byPage)
            {
                Job.Progress(k++ / (double)(byPage.Count + 1));
                var png = Path.Combine(tmp, $"p{pg}.png");
                await Gs.Run(new[] { "-sDEVICE=png16m", $"-r{dpi}", $"-dFirstPage={pg}", $"-dLastPage={pg}", "-dTextAlphaBits=4", "-dGraphicsAlphaBits=4", "-o", png, "-f", src });
                using var img = new MagickImage(png);
                var dr = new Drawables().FillColor(MagickColors.Black);
                foreach (var r in rects)
                    dr.Rectangle(r[0] * img.Width, r[1] * img.Height, (r[0] + r[2]) * img.Width, (r[1] + r[3]) * img.Height);
                dr.Draw(img);
                img.Alpha(AlphaAction.Off);
                img.Density = new Density(dpi, dpi, DensityUnit.PixelsPerInch);
                img.Format = MagickFormat.Pdf;
                img.Settings.Compression = CompressionMethod.JPEG;
                img.Quality = 90;
                img.Write(Path.Combine(tmp, $"p{pg}.pdf"));
            }
            // Monta: páginas originais, trocando as tarjadas.
            var a = new List<string> { "--empty", "--pages" };
            int start = 1;
            foreach (var pg in byPage.Keys.OrderBy(x => x))
            {
                if (pg < 1 || pg > n) continue;
                if (pg > start) { a.Add(src); a.Add($"{start}-{pg - 1}"); }
                a.Add(Path.Combine(tmp, $"p{pg}.pdf")); a.Add("1");
                start = pg + 1;
            }
            if (start <= n) { a.Add(src); a.Add($"{start}-{n}"); }
            a.Add("--"); a.Add(dst);
            await Qpdf.Run(a);
            return dst;
        }
        catch { Paths.TryDelete(dst); throw; }
        finally { Paths.TryDelete(tmp); }
    }

    public static async Task<string> PptxFromPdf(string src) => await Office.Convert(src, "pptx", Paths.Out(src, "pptx"), "impress_pdf_import");
}

static class TextDocs
{
    public static string Page(string title, string body) =>
        $"<!doctype html>\n<html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(title)}</title>\n<style>body{{font-family:Segoe UI,Arial,sans-serif;max-width:46em;margin:2em auto;line-height:1.55;padding:0 1em}}pre,code{{font-family:Consolas,monospace;background:#f3f3f3}}pre{{padding:.8em;overflow:auto}}table{{border-collapse:collapse}}td,th{{border:1px solid #ccc;padding:.3em .6em}}img{{max-width:100%}}</style>\n</head><body>\n{body}</body></html>\n";

    static string HtmlFromMd(string src)
    {
        var md = File.ReadAllText(src);
        var pipe = new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        return Page(Path.GetFileNameWithoutExtension(src), Markdig.Markdown.ToHtml(md, pipe));
    }

    static string HtmlFromTxt(string src) =>
        Page(Path.GetFileNameWithoutExtension(src), string.Join("\n", Regex.Split(File.ReadAllText(src).Replace("\r", ""), @"\n\s*\n")
            .Where(p => p.Trim().Length > 0).Select(p => "<p>" + WebUtility.HtmlEncode(p.Trim()).Replace("\n", "<br>") + "</p>")));

    public static string HtmlToMd(string html)
    {
        var cfg = new ReverseMarkdown.Config { GithubFlavored = true };
        cfg.Tags.Unknown = ReverseMarkdown.Config.UnknownTagsOption.Bypass;
        cfg.Formatting.RemoveComments = true;
        cfg.Links.SmartHref = true;
        var conv = new ReverseMarkdown.Converter(cfg);
        var body = Regex.Match(html, @"<body[^>]*>(.*)</body>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var md = conv.Convert(body.Success ? body.Groups[1].Value : html);
        return Regex.Replace(md, @"\n{3,}", "\n\n").Trim() + "\n";
    }

    public static string HtmlToText(string html)
    {
        var body = Regex.Match(html, @"<body[^>]*>(.*)</body>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var s = body.Success ? body.Groups[1].Value : html;
        s = Regex.Replace(s, @"<(script|style)[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</(p|div|h\d|li|tr|blockquote)>", "\n\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "");
        s = WebUtility.HtmlDecode(s);
        s = Regex.Replace(s, @"[ \t]+\n", "\n");
        return Regex.Replace(s, @"\n{3,}", "\n\n").Trim() + "\r\n";
    }

    static void Write(string dst, string text, bool bom) => File.WriteAllText(dst, text, new UTF8Encoding(bom));

    /// Converte a partir de um HTML intermediário (de Markdown, texto, EPUB ou HTML).
    static async Task<string> FromHtml(string src, string html, string ext)
    {
        switch (ext)
        {
            case "html": { var d = Paths.Out(src, "html"); Write(d, html, false); return d; }
            case "md": { var d = Paths.Out(src, "md"); Write(d, HtmlToMd(html), false); return d; }
            case "txt": { var d = Paths.Out(src, "txt"); Write(d, HtmlToText(html), true); return d; }
        }
        var tmp = Paths.Temp();
        try
        {
            var h = Path.Combine(tmp, Path.GetFileNameWithoutExtension(src) + ".html");
            Write(h, html, false);
            return await Office.Convert(h, ext, Paths.Out(src, ext), "HTML (StarWriter)");
        }
        finally { Paths.TryDelete(tmp); }
    }

    public static Task<string> FromMarkdown(string src, string ext) => FromHtml(src, HtmlFromMd(src), ext);
    public static Task<string> FromText(string src, string ext) => FromHtml(src, HtmlFromTxt(src), ext);
    public static Task<string> FromHtmlFile(string src, string ext) => ext switch
    {
        "md" or "txt" => FromHtml(src, File.ReadAllText(src), ext),
        _ => Office.Here(src, ext, "HTML (StarWriter)"),
    };

    /// Documento do Office para Markdown/HTML: LibreOffice gera HTML, e daí para Markdown.
    public static async Task<string> FromOffice(string src, string ext)
    {
        if (ext is not ("md" or "html")) return await Office.Here(src, ext);
        var tmp = Paths.Temp();
        try
        {
            var html = await Office.Convert(src, "html", Path.Combine(tmp, "doc.html"), null);
            if (ext == "html")
            {
                var d = Paths.Out(src, "html");
                File.Copy(html, d);
                foreach (var img in Directory.GetFiles(tmp).Where(f => f != html)) File.Copy(img, Path.Combine(Path.GetDirectoryName(d)!, Path.GetFileName(img)), false);
                return d;
            }
            var dst = Paths.Out(src, "md");
            Write(dst, HtmlToMd(File.ReadAllText(html)), false);
            return dst;
        }
        finally { Paths.TryDelete(tmp); }
    }

    // ---------- EPUB ----------
    /// Lê o EPUB (zip com XHTML) e junta os capítulos na ordem do livro em um HTML só, com imagens embutidas.
    public static string EpubHtml(string src)
    {
        using var z = ZipFile.OpenRead(src);
        string Read(string name) { var e = z.GetEntry(name) ?? z.Entries.FirstOrDefault(x => x.FullName.Equals(name, StringComparison.OrdinalIgnoreCase)); if (e == null) return ""; using var r = new StreamReader(e.Open()); return r.ReadToEnd(); }
        var container = XDocument.Parse(Read("META-INF/container.xml"));
        var opfPath = container.Descendants().First(e => e.Name.LocalName == "rootfile").Attribute("full-path")!.Value;
        var opf = XDocument.Parse(Read(opfPath));
        var baseDir = opfPath.Contains('/') ? opfPath[..(opfPath.LastIndexOf('/') + 1)] : "";
        var manifest = opf.Descendants().Where(e => e.Name.LocalName == "item").ToDictionary(e => e.Attribute("id")!.Value, e => (href: e.Attribute("href")!.Value, type: e.Attribute("media-type")?.Value ?? ""));
        var title = opf.Descendants().FirstOrDefault(e => e.Name.LocalName == "title")?.Value ?? Path.GetFileNameWithoutExtension(src);
        var body = new StringBuilder();
        foreach (var itemref in opf.Descendants().Where(e => e.Name.LocalName == "itemref"))
        {
            if (!manifest.TryGetValue(itemref.Attribute("idref")!.Value, out var item)) continue;
            var path = Combine(baseDir, item.href);
            var html = Read(path);
            var m = Regex.Match(html, @"<body[^>]*>(.*)</body>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var chunk = m.Success ? m.Groups[1].Value : html;
            var dir = path.Contains('/') ? path[..(path.LastIndexOf('/') + 1)] : "";
            chunk = Regex.Replace(chunk, @"(<img[^>]+(?:src|xlink:href)\s*=\s*"")([^""]+)("")", mm =>
            {
                var ip = Combine(dir, WebUtility.UrlDecode(mm.Groups[2].Value));
                var e = z.GetEntry(ip);
                if (e == null) return mm.Value;
                using var ms = new MemoryStream(); using (var s = e.Open()) s.CopyTo(ms);
                var mime = Path.GetExtension(ip).ToLowerInvariant() switch { ".png" => "image/png", ".gif" => "image/gif", ".svg" => "image/svg+xml", ".webp" => "image/webp", _ => "image/jpeg" };
                return mm.Groups[1].Value + $"data:{mime};base64,{System.Convert.ToBase64String(ms.ToArray())}" + mm.Groups[3].Value;
            }, RegexOptions.IgnoreCase);
            chunk = Regex.Replace(chunk, @"<svg\b.*?</svg>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            body.Append(chunk).Append('\n');
        }
        return Page(title, body.ToString());
    }

    static string Combine(string dir, string rel)
    {
        rel = rel.Split('#')[0];
        var parts = new List<string>((dir + rel).Split('/'));
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] == "..") { parts.RemoveAt(i); if (i > 0) parts.RemoveAt(--i); i--; }
            else if (parts[i] == ".") { parts.RemoveAt(i); i--; }
        }
        return string.Join("/", parts);
    }

    public static Task<string> FromEpub(string src, string ext) => FromHtml(src, EpubHtml(src), ext);
}

static class Subtitles
{
    static readonly Regex Timing = new(@"^\s*(\d{1,2}:)?\d{2}:\d{2}[.,]\d{3}\s*-->\s*(\d{1,2}:)?\d{2}:\d{2}[.,]\d{3}", RegexOptions.Compiled);

    record Cue(string Time, List<string> Lines);

    static List<Cue> Parse(string src)
    {
        var cues = new List<Cue>();
        Cue? cur = null;
        foreach (var raw in File.ReadAllLines(src))
        {
            var line = raw.TrimEnd();
            if (Timing.IsMatch(line)) { cur = new Cue(line.Trim(), new()); cues.Add(cur); }
            else if (line.Length == 0) cur = null;
            else if (cur != null) cur.Lines.Add(line);
        }
        if (cues.Count == 0) throw new Exception("Não achei nenhuma legenda nesse arquivo");
        return cues;
    }

    static string Fix(string t, char sep)
    {
        var parts = t.Split("-->");
        string One(string s)
        {
            s = s.Trim().Split(' ')[0].Replace(',', '.').Replace('.', sep);
            if (s.Count(c => c == ':') == 1) s = "00:" + s;
            if (s.IndexOf(':') == 1) s = "0" + s;
            return s;
        }
        return $"{One(parts[0])} --> {One(parts[1])}";
    }

    public static string Convert(string src, string ext)
    {
        var cues = Parse(src);
        var dst = Paths.Out(src, ext);
        var sb = new StringBuilder();
        if (ext == "vtt")
        {
            sb.Append("WEBVTT\n\n");
            foreach (var c in cues) sb.Append(Fix(c.Time, '.')).Append('\n').Append(string.Join("\n", c.Lines)).Append("\n\n");
        }
        else if (ext == "srt")
        {
            int n = 1;
            foreach (var c in cues) sb.Append(n++).Append("\r\n").Append(Fix(c.Time, ',')).Append("\r\n").Append(string.Join("\r\n", c.Lines)).Append("\r\n\r\n");
        }
        else
        {
            foreach (var c in cues) sb.Append(Regex.Replace(string.Join(" ", c.Lines), "<[^>]+>", "")).Append("\r\n");
        }
        File.WriteAllText(dst, sb.ToString(), new UTF8Encoding(ext == "txt"));
        return dst;
    }
}

static class Archives
{
    static readonly string Tar = Path.Combine(Environment.SystemDirectory, "tar.exe");

    static bool IsPlainGz(string src) =>
        Path.GetExtension(src).Equals(".gz", StringComparison.OrdinalIgnoreCase) &&
        !src.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase);

    static async Task ExtractTo(string src, string dir)
    {
        Directory.CreateDirectory(dir);
        if (IsPlainGz(src))
        {
            using var i = new GZipStream(File.OpenRead(src), CompressionMode.Decompress);
            using var o = File.Create(Path.Combine(dir, Path.GetFileNameWithoutExtension(src)));
            await i.CopyToAsync(o, Job.Token);
        }
        else if (Path.GetExtension(src).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            await Task.Run(() => ZipFile.ExtractToDirectory(src, dir, Encoding.GetEncoding(850), true)); // nomes de zip antigos vêm em CP850
        else
        {
            try { await Proc.Run(Tar, new[] { "-xf", src, "-C", dir }); }
            catch (Exception ex) when (ex.Message.Contains("encrypt", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("passphrase", StringComparison.OrdinalIgnoreCase))
            { throw new Exception("Esse arquivo compactado tem senha"); }
        }
    }

    public static async Task<string> Extract(string src)
    {
        var dir = Paths.OutDir(src);
        try { await ExtractTo(src, dir); }
        catch { Paths.TryDelete(dir); throw; }
        // Se dentro só tem uma pasta, sobe ela um nível.
        var items = Directory.GetFileSystemEntries(dir);
        if (items.Length == 1 && Directory.Exists(items[0]))
        {
            var inner = items[0];
            var target = Path.Combine(Path.GetDirectoryName(dir)!, Path.GetFileName(inner));
            if (!Directory.Exists(target) && !File.Exists(target))
            {
                var hold = dir + ".tmp-" + Guid.NewGuid().ToString("N")[..6];
                Directory.Move(dir, hold);
                Directory.Move(Path.Combine(hold, Path.GetFileName(inner)), target);
                Directory.Delete(hold, true);
                return target;
            }
        }
        return dir;
    }

    public static async Task<string> Convert(string src, string ext)
    {
        var tmp = Paths.Temp();
        try
        {
            var inner = Path.Combine(tmp, "x");
            await ExtractTo(src, inner);
            var dst = Paths.Out(src, ext == "tgz" ? "tar.gz" : ext);
            if (ext == "zip") await Task.Run(() => ZipFile.CreateFromDirectory(inner, dst, CompressionLevel.Optimal, false, new UTF8Encoding(false)));
            else await Proc.Run(Tar, new[] { ext == "tgz" ? "-czf" : "-cf", dst, "-C", inner, "." });
            return dst;
        }
        finally { Paths.TryDelete(tmp); }
    }

    public static string Zip(string[] items)
    {
        string dst = items.Length == 1 ? Paths.Out(items[0], "zip") : Paths.Out(Path.Combine(Path.GetDirectoryName(items[0].TrimEnd('\\'))!, "Arquivos.x"), "zip");
        try
        {
            using var z = ZipFile.Open(dst, ZipArchiveMode.Create, new UTF8Encoding(false));
            var all = new List<(string file, string entry)>();
            foreach (var it in items)
            {
                var p = it.TrimEnd('\\');
                if (Directory.Exists(p))
                {
                    var parent = Path.GetDirectoryName(p)!;
                    foreach (var f in Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories)) all.Add((f, Path.GetRelativePath(parent, f).Replace('\\', '/')));
                }
                else all.Add((p, Path.GetFileName(p)));
            }
            for (int i = 0; i < all.Count; i++)
            {
                Job.Check(); Job.Progress(i / (double)all.Count);
                z.CreateEntryFromFile(all[i].file, all[i].entry, CompressionLevel.Optimal);
            }
        }
        catch { Paths.TryDelete(dst); throw; }
        return dst;
    }

    public static async Task<string> TarGz(string[] items)
    {
        string dst = items.Length == 1 ? Paths.Out(items[0], "tar.gz") : Paths.Out(Path.Combine(Path.GetDirectoryName(items[0].TrimEnd('\\'))!, "Arquivos.x"), "tar.gz");
        var parent = Path.GetDirectoryName(items[0].TrimEnd('\\'))!;
        var a = new List<string> { "-czf", dst, "-C", parent };
        a.AddRange(items.Select(i => Path.GetFileName(i.TrimEnd('\\'))));
        try { await Proc.Run(Tar, a); } catch { Paths.TryDelete(dst); throw; }
        return dst;
    }
}

/// Metadados pelo ExifTool (imagens, PDF, MP4/MOV) e pelo ffmpeg (demais áudios e vídeos).
static class Meta
{
    public static readonly string? Exe = Paths.FindTool("exiftool.exe");
    public static bool Ok => Exe != null;

    static readonly string[] exifWritable = { "jpg", "jpeg", "png", "webp", "heic", "heif", "tif", "tiff", "avif", "gif", "pdf", "mp4", "mov", "m4a", "m4v", "3gp" };
    public static bool ExifCanWrite(string src) => exifWritable.Contains(Path.GetExtension(src).TrimStart('.').ToLowerInvariant());

    public static List<(string group, string tag, string value)> Read(string src)
    {
        var list = new List<(string, string, string)>();
        if (Exe == null) return list;
        var json = Run(new[] { "-json", "-G1", "-a", "-s", "-api", "LargeFileSupport=1", src });
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement[0].EnumerateObject())
        {
            var name = prop.Name;
            if (name == "SourceFile") continue;
            var colon = name.IndexOf(':');
            var group = colon > 0 ? name[..colon] : "";
            var tag = colon > 0 ? name[(colon + 1)..] : name;
            if (group is "ExifTool" or "System") continue;
            var v = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() ?? "" : prop.Value.ToString();
            if (v.StartsWith("(Binary data")) v = "(dados binários)";
            list.Add((group, tag, v));
        }
        return list;
    }

    /// p: limpar=1 tira tudo; m.Campo=valor altera; semgps=1 tira só a localização.
    public static async Task<string> Apply(string src, P p)
    {
        var kind = Catalog.KindOf(src);
        if ((kind is Kind.Audio or Kind.Video) && !ExifCanWrite(src))
        {
            if (p.B("semgps") && !p.B("limpar")) p["m.location"] = "";
            return await Ff.SetMeta(src, p);
        }
        if (Exe == null) throw new Exception("ExifTool não encontrado");
        var ext = Path.GetExtension(src).TrimStart('.').ToLowerInvariant();
        var dst = Paths.Out(src, ext, p.B("limpar") ? "sem metadados" : "metadados");
        var a = new List<string> { "-api", "LargeFileSupport=1", "-o", dst };
        if (p.B("limpar")) { a.Add("-all="); if (kind == Kind.Image) { a.Add("-tagsfromfile"); a.Add("@"); a.Add("-Orientation"); a.Add("-ICC_Profile"); } }
        else if (p.B("semgps")) a.Add("-gps:all=");
        foreach (var (k, v) in p.Where(kv => kv.Key.StartsWith("m.")))
            foreach (var tag in Tags(k[2..], kind)) a.Add($"-{tag}={v}");
        a.Add(src);
        try { await Task.Run(() => Run(a)); }
        catch { Paths.TryDelete(dst); throw; }
        if (!File.Exists(dst)) throw new Exception("O ExifTool não gravou o arquivo");
        if (kind == Kind.Pdf && Qpdf.Ok)
        {
            // O ExifTool só acrescenta no PDF (dá para desfazer); o qpdf regrava sem os dados antigos.
            var tmp = dst + ".qpdf";
            await Qpdf.Run(new[] { dst, tmp });
            File.Move(tmp, dst, true);
        }
        return dst;
    }

    /// O ExifTool recebe tudo por um arquivo de argumentos em UTF-8: a linha de comando do Windows
    /// estragaria acentos nos valores e nos nomes de arquivo.
    static string Run(List<string> args) => Run(args.ToArray());
    static string Run(string[] args)
    {
        var dir = Paths.Temp();
        try
        {
            var argfile = Path.Combine(dir, "args.txt");
            File.WriteAllLines(argfile, new[] { "-charset", "filename=utf8", "-charset", "utf8" }.Concat(args.Select(x => x.Replace("\r", " ").Replace("\n", " "))), new UTF8Encoding(false));
            var psi = new System.Diagnostics.ProcessStartInfo(Exe!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            psi.ArgumentList.Add("-@"); psi.ArgumentList.Add(argfile);
            using var p = System.Diagnostics.Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(120_000)) { try { p.Kill(true); } catch { } throw new Exception("O ExifTool demorou demais"); }
            var err = e.Result.Trim();
            if (p.ExitCode != 0) { Log.Write("exiftool: " + err); throw new Exception(err.Length > 0 ? err.Split('\n')[0] : "O ExifTool falhou"); }
            return o.Result;
        }
        finally { Paths.TryDelete(dir); }
    }

    static IEnumerable<string> Tags(string field, Kind k) => (field, k) switch
    {
        ("Title", Kind.Image) => new[] { "XMP-dc:Title", "IPTC:ObjectName", "EXIF:XPTitle" },
        ("Title", _) => new[] { "Title" },
        ("Artist", Kind.Image) => new[] { "EXIF:Artist", "XMP-dc:Creator", "IPTC:By-line" },
        ("Artist", Kind.Pdf) => new[] { "PDF:Author", "XMP-dc:Creator" },
        ("Artist", _) => new[] { "Artist" },
        ("Description", Kind.Image) => new[] { "EXIF:ImageDescription", "XMP-dc:Description", "IPTC:Caption-Abstract" },
        ("Description", Kind.Pdf) => new[] { "PDF:Subject", "XMP-dc:Description" },
        ("Description", _) => new[] { "Description", "Comment" },
        ("Copyright", Kind.Image) => new[] { "EXIF:Copyright", "XMP-dc:Rights", "IPTC:CopyrightNotice" },
        ("Copyright", _) => new[] { "Copyright" },
        ("Keywords", Kind.Image) => new[] { "XMP-dc:Subject", "IPTC:Keywords" },
        ("Keywords", Kind.Pdf) => new[] { "PDF:Keywords" },
        ("Keywords", _) => new[] { "Keywords" },
        ("Date", Kind.Image) => new[] { "EXIF:DateTimeOriginal", "EXIF:CreateDate" },
        ("Date", Kind.Pdf) => new[] { "PDF:CreateDate" },
        ("Date", _) => new[] { "CreateDate" },
        ("Album", _) => new[] { "Album" },
        _ => new[] { field },
    };
}
