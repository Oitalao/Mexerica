using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Filee.Engines.Office;
using Filee.Engines.Office.Sheets;
using Filee.Engines.Hwp.Hwpx;
using Filee.Engines.Hwp.Hwpx.Docx;
using Filee.Engines.Hwp.Hwpx.Pptx;
using Filee.Engines.Infrastructure;
using Filee.Engines.Pdf;
using Filee.Engines.Hwp;

namespace Mexerica;

sealed class OfficeSemLibreModule : Module
{
    static readonly string[] Sheets = ["xlsx", "xlsm", "xls", "ods", "csv", "tsv"];
    static readonly string[] SheetOutputs = ["xlsx", "ods", "csv", "tsv"];
    static readonly string[] RenderInputs = ["docx", "docm", "dotx", "xlsx", "xlsm", "xls", "ods", "csv", "tsv", "pptx", "pptm", "ppsx", "html", "md", "txt"];
    public override string Name => "Office sem LibreOffice";
    public override IEnumerable<string> Inputs => RenderInputs.Concat(OoxmlConverter.Variants.Keys).Append("pdf").Distinct();
    public override bool Available => File.Exists(Path.Combine(AppContext.BaseDirectory, "tools", "rhwp", "rhwp.exe"));
    public override bool Overrides(string inExt, string outExt) => !Office.Ok;

    public override IEnumerable<Target> TargetsFor(string inExt)
    {
        if (RenderInputs.Contains(inExt)) yield return new Target("pdf", "documento");
        if (inExt != "docx" && inExt is not "docm" and not "dotx" && (RenderInputs.Contains(inExt) || inExt == "pdf"))
            yield return new Target("docx", "documento editável");
        if (inExt is "pptx" or "pptm" or "ppsx")
        {
            yield return new Target("png", "uma imagem por slide");
            yield return new Target("jpg", "uma imagem por slide");
        }
        if (Sheets.Contains(inExt))
            foreach (var output in SheetOutputs.Where(o => o != inExt && !(inExt == "xlsm" && o == "xlsx")))
                yield return new Target(output, "planilha");
        if (OoxmlConverter.Variants.TryGetValue(inExt, out var variant))
            foreach (var output in OoxmlConverter.Variants.Where(v => v.Key != inExt && v.Value.Family == variant.Family))
                yield return new Target(output.Key, "documento");
    }

    public override Task<string?> Convert(string src, string outExt, P p) => Task.Run<string?>(() =>
    {
        Job.Check();
        var input = Modules.Ext(src);
        if (outExt == "pdf" && RenderInputs.Contains(input)) return RenderPdf(src, input);
        if (outExt == "docx" && input != "docx" && (RenderInputs.Contains(input) || input == "pdf") && input is not "docm" and not "dotx")
            return WriteDocx(src, input);
        if (outExt is "png" or "jpg" && input is "pptx" or "pptm" or "ppsx")
            return RenderSlides(src, input, outExt);
        if (Sheets.Contains(input) && SheetOutputs.Contains(outExt) && input != outExt && !(input == "xlsm" && outExt == "xlsx"))
        {
            var book = input switch
            {
                "xls" => XlsReader.Read(src),
                "ods" => OdsReader.Read(src),
                "csv" => CsvFormat.Read(src),
                "tsv" => CsvFormat.ReadTsv(src),
                _ => XlsxReader.Read(src),
            };
            Job.Progress(0.5);
            Job.Check();
            if (outExt is "xlsx" or "ods")
            {
                var temp = Paths.Temp();
                try
                {
                    var staged = Path.Combine(temp, "planilha." + outExt);
                    if (outExt == "xlsx") XlsxWriter.Write(book, staged);
                    else OdsWriter.Write(book, staged);
                    Job.Check();
                    var dst = Paths.Out(src, outExt);
                    File.Move(staged, dst);
                    Job.Progress(1);
                    return dst;
                }
                finally { Paths.TryDelete(temp); }
            }
            var sheets = book.Sheets.Where(s => s.UsedRange() is not null).ToList();
            if (sheets.Count == 0 && book.Sheets.Count > 0) sheets.Add(book.Sheets[0]);
            if (sheets.Count == 0) throw new Exception("A planilha não contém abas legíveis");
            var dstOne = sheets.Count == 1 ? Paths.Out(src, outExt) : null;
            var dir = dstOne == null ? Paths.OutDir(src, "abas") : null;
            var stagedDir = dir == null ? Paths.Temp() : dir + ".mexerica-" + Guid.NewGuid().ToString("N")[..8];
            Directory.CreateDirectory(stagedDir);
            try
            {
                foreach (var (sheet, index) in sheets.Select((s, i) => (s, i)))
                {
                    Job.Check();
                    var name = string.Concat(sheet.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
                    var staged = Path.Combine(stagedDir, $"{index + 1:00} {name}.{outExt}");
                    CsvFormat.Write(sheet, staged, outExt == "tsv" ? CsvFormat.Tab : ',');
                }
                Job.Check();
                if (dstOne != null) File.Move(Directory.GetFiles(stagedDir).Single(), dstOne);
                else Directory.Move(stagedDir, dir!);
                Job.Progress(1);
                return dstOne ?? dir;
            }
            finally { Paths.TryDelete(stagedDir); }
        }
        if (OoxmlConverter.Variants.TryGetValue(input, out var from) &&
            OoxmlConverter.Variants.TryGetValue(outExt, out var to) && from.Family == to.Family)
        {
            var temp = Paths.Temp();
            try
            {
                var staged = Path.Combine(temp, "documento." + outExt);
                OoxmlConverter.Rewrite(src, staged, outExt);
                Job.Check();
                var dst = Paths.Out(src, outExt);
                File.Move(staged, dst);
                Job.Progress(1);
                return dst;
            }
            finally { Paths.TryDelete(temp); }
        }
        throw new Exception("Essa conversão ainda não está disponível sem LibreOffice");
    });

    static string RenderPdf(string src, string input)
    {
        if (ComplexScripts.FindInFile(src, input) is { } script)
            throw new Exception($"O texto em {script} exige LibreOffice para renderizar corretamente");
        var temp = Paths.Temp();
        try
        {
            var document = ReadDocument(src, input, Path.Combine(temp, "imagens"));
            Job.Progress(0.4);
            Job.Check();
            var pdf = Path.Combine(temp, "documento.pdf");
            RenderSingle(document, temp, "documento", pdf);
            if (!File.Exists(pdf) || new FileInfo(pdf).Length == 0)
                throw new Exception("O renderizador não gerou um PDF válido");
            var dst = Paths.Out(src, "pdf");
            File.Move(pdf, dst);
            Job.Progress(1);
            return dst;
        }
        finally { Paths.TryDelete(temp); }
    }

    static void RenderSingle(HDocument document, string temp, string name, string pdf)
    {
        var hwpx = Path.Combine(temp, name + ".hwpx");
        HwpxWriter.Write(document, hwpx);
        Job.Check();
        var prepared = RhwpInput.Prepare(hwpx, temp) ?? hwpx;
        var exe = Path.Combine(AppContext.BaseDirectory, "tools", "rhwp", "rhwp.exe");
        // O backend direto mantém acentos pesquisáveis e copiáveis no PDF.
        Proc.Run(exe, ["export-pdf", prepared, "-o", pdf, "--backend", "direct"], 3600).GetAwaiter().GetResult();
    }

    static string WriteDocx(string src, string input)
    {
        var temp = Paths.Temp();
        try
        {
            var document = ReadDocument(src, input, Path.Combine(temp, "imagens"));
            Job.Progress(0.6);
            Job.Check();
            var staged = Path.Combine(temp, "documento.docx");
            DocxWriter.Write(document, staged);
            Job.Check();
            var dst = Paths.Out(src, "docx");
            File.Move(staged, dst);
            Job.Progress(1);
            return dst;
        }
        finally { Paths.TryDelete(temp); }
    }

    static string RenderSlides(string src, string input, string ext)
    {
        var pdf = RenderPdf(src, input);
        try { return Pdf.ToImages(pdf, ext, src).GetAwaiter().GetResult(); }
        finally { File.Delete(pdf); }
    }

    static HDocument ReadDocument(string src, string input, string media) => input switch
    {
        "docx" or "docm" or "dotx" => DocxReader.Read(src, media),
        "pptx" or "pptm" or "ppsx" => PptxReader.Read(src, media),
        "xlsx" or "xlsm" => SheetDocument.Build(XlsxReader.Read(src)),
        "xls" => SheetDocument.Build(XlsReader.Read(src)),
        "ods" => SheetDocument.Build(OdsReader.Read(src)),
        "csv" => SheetDocument.Build(CsvFormat.Read(src)),
        "tsv" => SheetDocument.Build(CsvFormat.ReadTsv(src)),
        "html" => HtmlReader.Read(src, media),
        "md" => MarkdownReader.Read(TextEncoding.Decode(File.ReadAllBytes(src)), Path.GetDirectoryName(src)!),
        "txt" => PlainDocument(src),
        "pdf" => PdfDocumentReader.Read(src, media),
        _ => throw new Exception("Formato não suportado"),
    };

    static HDocument PlainDocument(string src)
    {
        var document = new HDocument();
        var section = new HSection();
        document.Sections.Add(section);
        foreach (var line in TextEncoding.Decode(File.ReadAllBytes(src)).Replace("\r\n", "\n").Split('\n'))
        {
            var paragraph = new HParagraph();
            if (line.Length > 0) paragraph.Inlines.Add(new HText(line, default));
            section.Blocks.Add(paragraph);
        }
        return document;
    }
}

internal static class TextEncoding
{
    internal static string Decode(byte[] bytes)
    {
        if (bytes is [0xFF, 0xFE, ..] or [0xFE, 0xFF, ..])
            return new StreamReader(new MemoryStream(bytes), detectEncodingFromByteOrderMarks: true).ReadToEnd();
        try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(949).GetString(bytes);
        }
    }
}
