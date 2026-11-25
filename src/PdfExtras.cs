using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Mexerica;

/// Ferramentas de PDF pensadas para quem protocola documentos: limite de envio, OCR, carimbo, PDF/A e senha.
static class PdfExtras
{
    static PdfExtras()
    {
        // O PDFsharp precisa saber de onde tirar as fontes; no Windows, das fontes do próprio Windows.
        try { GlobalFontSettings.UseWindowsFontsUnderWindows = true; } catch { }
    }

    // ---------- Limite de envio: comprimir até caber e, se não couber, dividir em partes ----------

    public static async Task<string?> FitLimit(string src, P p)
    {
        long limit = (long)(Math.Clamp(p.D("mb", 5), 0.01, 2000) * 1024 * 1024);
        if (new FileInfo(src).Length <= limit) return null; // já cabe
        string work = src, compressed = "";
        if (Gs.Ok)
        {
            try { compressed = await Pdf.Compress(src, new P { ["modo"] = "mb", ["mb"] = p.S("mb", "5"), ["_limite"] = "1" }); work = compressed; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Log.Write("limite, comprimir: " + ex.Message); }
            if (work != src && new FileInfo(work).Length <= limit) return work;
        }
        try
        {
            return await SplitBySize(work, src, limit);
        }
        finally { if (compressed.Length > 0) Paths.TryDelete(compressed); }
    }

    /// Divide em partes que caibam no limite. Soma o tamanho de cada página sozinha (fica um pouco acima do real,
    /// porque fontes e imagens repetidas contam várias vezes), então as partes saem sempre dentro do limite;
    /// confere cada parte e reparte se mesmo assim passar. Páginas grandes são reduzidas individualmente.
    static async Task<string> SplitBySize(string work, string namedAfter, long limit)
    {
        int n = Pdf.Pages(work);
        if (n == 0) throw new Exception("Esse PDF está danificado");
        var tmp = Paths.Temp();
        try
        {
            var size = new long[n + 1];
            var fitted = new HashSet<int>();
            for (int i = 1; i <= n; i++)
            {
                Job.Progress(0.6 * i / n);
                var o = Path.Combine(tmp, $"p{i}.pdf");
                await Qpdf.Run(new[] { "--empty", "--pages", work, $"{i}", "--", o });
                size[i] = new FileInfo(o).Length;
                if (size[i] > limit)
                {
                    await FitPage(o, i, limit, tmp);
                    size[i] = new FileInfo(o).Length;
                    fitted.Add(i);
                }
                else File.Delete(o);
            }
            var groups = new List<(int a, int b)>();
            int start = 1; long sum = 0;
            for (int i = 1; i <= n; i++)
            {
                if (i > start && sum + size[i] > limit) { groups.Add((start, i - 1)); start = i; sum = 0; }
                sum += size[i];
            }
            groups.Add((start, n));
            // Confere de verdade e reparte ao meio o que ainda passar.
            var parts = new List<string>();
            var queue = new Queue<(int a, int b)>(groups);
            var final = new List<(int a, int b, string file)>();
            int k = 0;
            while (queue.Count > 0)
            {
                var (a, b) = queue.Dequeue();
                var o = Path.Combine(tmp, $"g{k++}.pdf");
                await JoinPages(a, b, o);
                if (new FileInfo(o).Length > limit && b > a)
                {
                    File.Delete(o);
                    int m = (a + b) / 2;
                    var rest = queue.ToList(); queue.Clear();
                    queue.Enqueue((a, m)); queue.Enqueue((m + 1, b));
                    foreach (var r in rest) queue.Enqueue(r);
                    continue;
                }
                if (new FileInfo(o).Length > limit) await FitPage(o, a, limit, tmp);
                final.Add((a, b, o));
            }
            final.Sort((x, y) => x.a.CompareTo(y.a));
            // A soma por página exagera (fontes e imagens repetidas): tenta juntar partes vizinhas enquanto couberem.
            for (int i = 0; i + 1 < final.Count;)
            {
                Job.Check();
                var o = Path.Combine(tmp, $"g{k++}.pdf");
                await JoinPages(final[i].a, final[i + 1].b, o);
                if (new FileInfo(o).Length <= limit)
                {
                    File.Delete(final[i].file); File.Delete(final[i + 1].file);
                    final[i] = (final[i].a, final[i + 1].b, o);
                    final.RemoveAt(i + 1);
                }
                else { File.Delete(o); i++; }
            }
            var b0 = Path.GetFileNameWithoutExtension(namedAfter);
            for (int i = 0; i < final.Count; i++)
            {
                var dst = Paths.Out(namedAfter, "pdf", name: $"{b0} - parte {i + 1} de {final.Count}");
                File.Move(final[i].file, dst);
                parts.Add(dst);
            }
            return parts[0];

            Task JoinPages(int a, int b, string output)
            {
                // Páginas comuns saem do PDF original (fontes e imagens compartilhadas uma vez só);
                // só as que precisaram encolher vêm do arquivo próprio.
                var args = new List<string> { "--empty", "--pages" };
                for (int page = a; page <= b; page++)
                {
                    if (fitted.Contains(page)) { args.AddRange(new[] { Path.Combine(tmp, $"p{page}.pdf"), "1" }); continue; }
                    int end = page;
                    while (end + 1 <= b && !fitted.Contains(end + 1)) end++;
                    args.AddRange(new[] { work, end > page ? $"{page}-{end}" : $"{page}" });
                    page = end;
                }
                args.AddRange(new[] { "--", output });
                return Qpdf.Run(args);
            }
        }
        finally { Paths.TryDelete(tmp); }
    }

    static async Task FitPage(string page, int number, long limit, string tmp)
    {
        var png = Path.Combine(tmp, "limit.png");
        var candidate = Path.Combine(tmp, "limit.pdf");
        foreach (int dpi in new[] { 200, 150, 120 })
        {
            Job.Check();
            await Gs.Run(new[] { "-sDEVICE=png16m", $"-r{dpi}", "-o", png, "-f", page });
            foreach (uint quality in new uint[] { 85, 70, 55, 40 })
            {
                using var image = new ImageMagick.MagickImage(png);
                image.Density = new ImageMagick.Density(dpi);
                image.Settings.Compression = ImageMagick.CompressionMethod.JPEG;
                image.Quality = quality;
                image.Write(candidate, ImageMagick.MagickFormat.Pdf);
                if (new FileInfo(candidate).Length > limit) continue;
                File.Move(candidate, page, true);
                // A página virou imagem: o OCR devolve o texto pesquisável, se ainda couber no limite.
                try
                {
                    if (await Ocr(page, P.None) is { } searchable)
                    {
                        if (new FileInfo(searchable).Length <= limit) File.Move(searchable, page, true);
                        else File.Delete(searchable);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Log.Write($"limite, OCR da página {number}: {ex.Message}"); }
                return;
            }
        }
        throw new Exception($"A página {number} não cabe no limite sem perder legibilidade (mínimo de 120 DPI). Aumente o limite em MB.");
    }

    // ---------- OCR: texto pesquisável em PDF escaneado, pelo reconhecimento do próprio Windows ----------

    public static async Task<string?> Ocr(string src, P p)
    {
        var engine = OcrEngine() ?? throw new Exception("O reconhecimento de texto do Windows não está instalado para o português (Configurações → Hora e idioma → Idioma)");
        int n = Pdf.Pages(src);
        if (n == 0) throw new Exception("Esse PDF está danificado");
        var regions = new List<XRect>[n];
        var existing = new List<XRect>[n];
        using (var d = UglyToad.PdfPig.PdfDocument.Open(src))
            for (int i = 0; i < n; i++)
            {
                var page = d.GetPage(i + 1);
                XRect Box(UglyToad.PdfPig.Core.PdfRectangle b)
                {
                    // PdfPig mantém os cantos orientados: em /Rotate=90, Left pode ser maior que Right.
                    var corners = new[] { b.BottomLeft, b.BottomRight, b.TopLeft, b.TopRight };
                    double left = Math.Clamp(corners.Min(c => c.X) / page.Width, 0, 1);
                    double right = Math.Clamp(corners.Max(c => c.X) / page.Width, 0, 1);
                    double top = Math.Clamp(1 - corners.Max(c => c.Y) / page.Height, 0, 1);
                    double bottom = Math.Clamp(1 - corners.Min(c => c.Y) / page.Height, 0, 1);
                    return new XRect(left, top, right - left, bottom - top);
                }
                existing[i] = page.GetWords().Select(w => Box(w.BoundingBox)).ToList();
                regions[i] = new();
                var letters = page.Letters.Where(l => !string.IsNullOrWhiteSpace(l.Value)).ToList();
                foreach (var image in page.GetImages())
                {
                    var box = Box(image.BoundingBox);
                    double area = box.Width * box.Height;
                    if (image.IsImageMask || area < 0.05) continue;
                    int count = letters.Count(l => box.Contains(Box(l.BoundingBox).Location));
                    // Cabeçalhos digitais não tornam o corpo escaneado pesquisável.
                    if (count < Math.Max(80, 300 * area)) regions[i].Add(box);
                }
                if (regions[i].Count == 0 && letters.Count < 20) regions[i].Add(new XRect(0, 0, 1, 1));
            }
        if (regions.All(r => r.Count == 0)) throw new Exception("Esse PDF já tem texto pesquisável");

        var winDoc = await Pdf.Open(src);
        var dst = Paths.Out(src, "pdf", "pesquisável");
        try
        {
            using var doc = await OpenForEdit(src);
            var brush = XBrushes.Black;
            for (int i = 0; i < n; i++)
            {
                Job.Check();
                Job.Progress(i / (double)n);
                if (regions[i].Count == 0) continue;
                using var winPage = winDoc.GetPage((uint)i);
                // 300 DPI, com o lado maior dentro do que o OCR do Windows aceita (e do que vale a pena: ~A4 a 400 DPI).
                double sw = winPage.Size.Width, sh = winPage.Size.Height;
                double cap = Math.Min(Windows.Media.Ocr.OcrEngine.MaxImageDimension, 4700);
                double wpx = sw * 300 / 96;
                if (Math.Max(wpx, wpx * sh / sw) > cap) wpx = cap * sw / Math.Max(sw, sh);
                uint w = (uint)Math.Max(200, Math.Round(wpx));
                using var ras = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                await winPage.RenderToStreamAsync(ras, new Windows.Data.Pdf.PdfPageRenderOptions
                {
                    DestinationWidth = w,
                    BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255),
                    BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                });
                ras.Seek(0);
                var dec = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ras);
                // Folha digitalizada de lado ou de cabeça para baixo: o OCR do Windows só lê texto em pé
                // (aceita só uma leve inclinação). Lê em pé; se não parecer texto de verdade, tenta girar
                // 90, 180 e 270 graus e fica com a leitura que mais parece texto.
                Windows.Media.Ocr.OcrResult? result = null;
                int turn = 0, best = -1;
                uint bw = dec.PixelWidth;
                uint bh = dec.PixelHeight;
                foreach (int t in new[] { 0, 90, 180, 270 })
                {
                    var tf = new Windows.Graphics.Imaging.BitmapTransform { Rotation = Spin(t) };
                    using var bmp = await dec.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                        Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied, tf,
                        Windows.Graphics.Imaging.ExifOrientationMode.IgnoreExifOrientation,
                        Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage);
                    var res = await engine.RecognizeAsync(bmp);
                    var (score, good) = TextScore(res);
                    if (score > best) { best = score; result = res; turn = t; }
                    if (t == 0 && good) break;
                }
                if (result == null) continue;

                var page = doc.Pages[i];
                int rot = ((page.Rotate % 360) + 360) % 360;
                double mw = page.MediaBox.Width, mh = page.MediaBox.Height;
                double dw = rot % 180 != 0 ? mh : mw, dh = rot % 180 != 0 ? mw : mh; // página como aparece na tela
                double k = dw / bw;
                using var g = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                // Volta das coordenadas da imagem girada para as da tela, e da tela para as da página sem /Rotate.
                Unturn(g, rot, mw, mh);
                Unturn(g, turn, dw, dh);
                foreach (var line in result.Lines)
                    foreach (var word in line.Words)
                    {
                        var r = word.BoundingRect;
                        // Testa no espaço da página exibida, antes dos giros usados para reconhecer.
                        var corners = new[] { Original(r.X, r.Y), Original(r.X + r.Width, r.Y), Original(r.X, r.Y + r.Height), Original(r.X + r.Width, r.Y + r.Height) };
                        var box = new XRect(corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X) - corners.Min(c => c.X), corners.Max(c => c.Y) - corners.Min(c => c.Y));
                        var center = new XPoint(box.X + box.Width / 2, box.Y + box.Height / 2);
                        if (!regions[i].Any(region => region.Contains(center)) || existing[i].Any(e => e.IntersectsWith(box))) continue;
                        double h = r.Height * k, x = r.X * k, y = r.Y * k, ww = r.Width * k;
                        if (h < 1 || ww < 1 || string.IsNullOrWhiteSpace(word.Text)) continue;
                        var font = new XFont("Arial", h * 0.85);
                        var real = g.MeasureString(word.Text, font).Width;
                        if (real <= 0) continue;
                        var st = g.Save();
                        g.TranslateTransform(x, y);
                        g.ScaleTransform(ww / real, 1);
                        g.DrawString(word.Text, font, brush, 0, 0, XStringFormats.TopLeft);
                        g.Restore(st);
                    }
                g.Dispose();
                // Tr=3 torna a camada invisível também nos leitores que ignoram alfa em texto.
                var content = page.Contents.Elements.GetDictionary(page.Contents.Elements.Count - 1)!;
                var operators = PdfSharp.Pdf.Content.ContentReader.ReadContent(content.Stream.UnfilteredValue);
                for (int j = operators.Count - 1; j >= 0; j--)
                    if (operators[j] is PdfSharp.Pdf.Content.Objects.COperator op && op.Name == "BT")
                    {
                        var invisible = PdfSharp.Pdf.Content.ContentReader.ReadContent(System.Text.Encoding.ASCII.GetBytes("3 Tr\n"))[0];
                        operators.Insert(j + 1, invisible);
                    }
                content.Elements.Remove("/Filter");
                content.Stream.Value = operators.ToContent();
                XPoint Original(double x, double y) => turn switch
                {
                    90 => new XPoint(y / bw, 1 - x / bh),
                    180 => new XPoint(1 - x / bw, 1 - y / bh),
                    270 => new XPoint(1 - y / bw, x / bh),
                    _ => new XPoint(x / bw, y / bh),
                };
            }
            doc.Save(dst);
            return dst;
        }
        catch { Paths.TryDelete(dst); throw; }
    }

    static Windows.Graphics.Imaging.BitmapRotation Spin(int t) => t switch
    {
        90 => Windows.Graphics.Imaging.BitmapRotation.Clockwise90Degrees,
        180 => Windows.Graphics.Imaging.BitmapRotation.Clockwise180Degrees,
        270 => Windows.Graphics.Imaging.BitmapRotation.Clockwise270Degrees,
        _ => Windows.Graphics.Imaging.BitmapRotation.None,
    };

    /// Desfaz um giro horário de t graus: o que se desenhar depois, no espaço girado, cai no lugar certo
    /// do espaço original (w × h, antes do giro).
    static void Unturn(XGraphics g, int t, double w, double h)
    {
        switch (t)
        {
            case 90: g.TranslateTransform(0, h); g.RotateTransform(-90); break;
            case 180: g.TranslateTransform(w, h); g.RotateTransform(180); break;
            case 270: g.TranslateTransform(w, 0); g.RotateTransform(90); break;
        }
    }

    // Palavras curtas e muito comuns nos idiomas latinos e no inglês. Texto lido de cabeça para baixo
    // vira letra solta ("ep!oaquooau"), quase nunca uma dessas.
    static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "da", "do", "das", "dos", "em", "no", "na", "nos", "nas", "os", "as", "um", "uma", "que", "para",
        "com", "por", "não", "se", "ao", "aos", "ou", "pelo", "pela", "mais", "seu", "sua",
        "the", "and", "of", "to", "in", "is", "for", "on", "with", "that", "by",
        "el", "la", "los", "las", "en", "del", "con", "por", "y", "les", "des", "et", "le", "du", "il", "di", "che", "per",
        "und", "der", "die", "das", "mit", "von", "zu",
    };

    /// Nota de "parece texto de verdade": letras de palavras que são só letras, com peso grande para
    /// palavras comuns. good = já dá para confiar sem tentar outros giros.
    static (int score, bool good) TextScore(Windows.Media.Ocr.OcrResult r)
    {
        int letters = 0, common = 0, words = 0;
        foreach (var line in r.Lines)
            foreach (var w in line.Words)
            {
                words++;
                var s = w.Text.Trim('.', ',', ';', ':', '(', ')', '"', '\'');
                if (s.Length >= 2 && s.All(char.IsLetter)) letters += s.Length;
                if (Common.Contains(s)) common++;
            }
        int score = letters + 15 * common;
        bool good = words == 0 ? false : common >= 2 && common * 12 >= words;
        return (score, good);
    }

    static Windows.Media.Ocr.OcrEngine? OcrEngine()
    {
        try
        {
            var pt = new Windows.Globalization.Language("pt-BR");
            if (Windows.Media.Ocr.OcrEngine.IsLanguageSupported(pt) && Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(pt) is { } engine) return engine;
            return Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
        }
        catch { return null; }
    }

    public static bool OcrOk => OcrEngine() != null;

    public static P? AskOcr(string[] files)
    {
        if (OcrOk || Form.Headless != null) return P.None;
        if (Tr.Box("Instale o reconhecimento de texto em português em Configurações → Hora e idioma → Idioma → Português → Opções de idioma. Abrir as configurações agora?", "OCR",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Information) == System.Windows.MessageBoxResult.Yes)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:regionlanguage") { UseShellExecute = true });
        return null;
    }

    // ---------- Numerar páginas e carimbar ("Doc. 01") ----------

    /// p: numerar (1/0), formato ("Página {n} de {total}"), carimbo ("Doc. {d}"), inicio, digitos, posicao, tamanho, arquivos (ordem).
    static async Task<PdfDocument> OpenForEdit(string src)
    {
        try { return PdfReader.Open(src, PdfDocumentOpenMode.Modify); }
        catch (PdfReaderException) when (Qpdf.Ok && !Qpdf.NeedsPassword(src))
        {
            // PDFsharp exige a senha de dono até quando abrir e modificar é permitido sem senha.
            var tmp = Paths.Temp();
            try
            {
                var plain = Path.Combine(tmp, "aberto.pdf");
                await Qpdf.Decrypt(src, "", plain);
                using var input = new MemoryStream(await File.ReadAllBytesAsync(plain));
                return PdfReader.Open(input, PdfDocumentOpenMode.Modify);
            }
            finally { Paths.TryDelete(tmp); }
        }
    }

    public static Task<string?> Stamp(string src, P p) => Task.Run<string?>(async () =>
    {
        bool number = p.S("numerar", "1") == "1";
        string stamp = p.S("carimbo").Trim();
        if (!number && stamp.Length == 0) throw new Exception("Escolha numerar as páginas ou escrever um carimbo");
        var order = p.S("arquivos").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int idx = Math.Max(0, Array.FindIndex(order, o => string.Equals(o, src, StringComparison.OrdinalIgnoreCase)));
        int docNum = (int)p.D("inicio", 1) + idx;
        string docText = stamp.Replace("{d}", docNum.ToString(new string('0', Math.Clamp((int)p.D("digitos", 2), 1, 6))));
        double size = Math.Clamp(p.D("tamanho", 10), 6, 48);
        string pos = p.S("posicao", "sd");

        var dst = Paths.Out(src, "pdf", "carimbado");
        try
        {
            using var doc = await OpenForEdit(src);
            int total = doc.PageCount;
            var font = new XFont("Arial", size);
            var bold = new XFont("Arial", size, XFontStyleEx.Bold);
            for (int i = 0; i < total; i++)
            {
                Job.Check();
                Job.Progress(i / (double)total);
                var page = doc.Pages[i];
                using var g = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                double W = g.PageSize.Width, H = g.PageSize.Height, m = 18;
                var lines = new List<(string t, XFont f)>();
                if (docText.Length > 0)
                {
                    string line = "";
                    var elements = System.Globalization.StringInfo.GetTextElementEnumerator(docText);
                    while (elements.MoveNext())
                    {
                        var element = elements.GetTextElement();
                        if (line.Length > 0 && g.MeasureString(line + element, bold).Width > Math.Max(1, W - 2 * m - 10))
                        {
                            int space = line.LastIndexOf(' ');
                            lines.Add((space > 0 ? line[..space] : line, bold));
                            line = space > 0 ? line[(space + 1)..] : "";
                        }
                        line += element;
                    }
                    if (line.Length > 0) lines.Add((line, bold));
                }
                if (number) lines.Add((Tr.T(p.S("formato", "{n}").Replace("{n}", (i + 1).ToString()).Replace("{total}", total.ToString())), font));
                double lh = size * 1.3, bw = lines.Max(l => g.MeasureString(l.t, l.f).Width) + 10, bh = lh * lines.Count + 6;
                double scale = Math.Min(1, Math.Min(Math.Max(1, W - 2 * m) / bw, Math.Max(1, H - 2 * m) / bh));
                double x = pos[1] switch { 'e' => m, 'c' => (W - bw * scale) / 2, _ => W - m - bw * scale };
                double y = pos[0] == 's' ? m : H - m - bh * scale;
                g.TranslateTransform(x, y);
                g.ScaleTransform(scale, scale);
                g.DrawRectangle(new XPen(XColor.FromArgb(160, 0, 0, 0), 0.6), new XSolidBrush(XColor.FromArgb(235, 255, 255, 255)), 0, 0, bw, bh);
                for (int j = 0; j < lines.Count; j++)
                    g.DrawString(lines[j].t, lines[j].f, XBrushes.Black, new XRect(0, 3 + j * lh, bw, lh), XStringFormats.Center);
            }
            doc.Save(dst);
            return dst;
        }
        catch { Paths.TryDelete(dst); throw; }
    });

    // ---------- PDF/A (arquivamento), pelo Ghostscript ----------

    public static async Task<string?> PdfA(string src, P p)
    {
        if (!Gs.Ok) throw new Exception("Ghostscript não encontrado");
        var dst = Paths.Out(src, "pdf", "PDF-A");
        var tmp = Paths.Temp();
        try
        {
            // Definição do PDF/A (a mesma do PDFA_def.ps do Ghostscript), com o perfil de cor sRGB que vem dentro dele.
            var def = Path.Combine(tmp, "pdfa.ps");
            File.WriteAllText(def, string.Join("\n",
                "%!",
                "/ICCProfile (%rom%iccprofiles/srgb.icc) def",
                "[/_objdef {icc_PDFA} /type /stream /OBJ pdfmark",
                "[{icc_PDFA} << /N 3 >> /PUT pdfmark",
                "[{icc_PDFA} ICCProfile (r) file /PUT pdfmark",
                "[/_objdef {OutputIntent_PDFA} /type /dict /OBJ pdfmark",
                "[{OutputIntent_PDFA} << /Type /OutputIntent /S /GTS_PDFA1 /DestOutputProfile {icc_PDFA} /OutputConditionIdentifier (sRGB) >> /PUT pdfmark",
                "[{Catalog} << /OutputIntents [ {OutputIntent_PDFA} ] >> /PUT pdfmark", ""));
            var o = Path.Combine(tmp, "a.pdf");
            await Gs.Run(new[]
            {
                "-dPDFA=2", "-dPDFACompatibilityPolicy=1", "-sColorConversionStrategy=RGB", "-sDEVICE=pdfwrite",
                $"--permit-file-read={tmp}\\", "-o", o, def, src,
            });
            File.Move(o, dst);
            return dst;
        }
        catch { Paths.TryDelete(dst); throw; }
        finally { Paths.TryDelete(tmp); }
    }

    // ---------- Senha ----------

    public static async Task<string?> Protect(string src, P p)
    {
        var pw = p.S("senha");
        if (pw.Length == 0) throw new Exception("Digite a senha.");
        var dst = Paths.Out(src, "pdf", "com senha");
        var tmp = Paths.Temp();
        try
        {
            // A senha vai por arquivo de argumentos, para não aparecer na lista de processos do Windows.
            var args = Path.Combine(tmp, "a.txt");
            File.WriteAllLines(args, new[] { "--encrypt", $"--user-password={pw}", $"--owner-password={pw}", "--bits=256", "--", src, dst });
            await Qpdf.Run(new[] { "@" + args });
            return dst;
        }
        catch { Paths.TryDelete(dst); throw; }
        finally { Paths.TryDelete(tmp); }
    }
}
