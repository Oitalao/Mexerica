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
            try { compressed = await Pdf.Compress(src, new P { ["modo"] = "mb", ["mb"] = p.S("mb", "5") }); work = compressed; }
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
    /// confere cada parte e reparte se mesmo assim passar. Página que sozinha passa do limite vira uma parte só.
    static async Task<string> SplitBySize(string work, string namedAfter, long limit)
    {
        int n = Pdf.Pages(work);
        if (n == 0) throw new Exception("Esse PDF está danificado");
        var tmp = Paths.Temp();
        try
        {
            var size = new long[n + 1];
            for (int i = 1; i <= n; i++)
            {
                Job.Progress(0.6 * i / n);
                var o = Path.Combine(tmp, $"p{i}.pdf");
                await Qpdf.Run(new[] { "--empty", "--pages", work, $"{i}", "--", o });
                size[i] = new FileInfo(o).Length;
                File.Delete(o);
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
                await Qpdf.Run(new[] { "--empty", "--pages", work, $"{a}-{b}", "--", o });
                if (new FileInfo(o).Length > limit && b > a)
                {
                    File.Delete(o);
                    int m = (a + b) / 2;
                    var rest = queue.ToList(); queue.Clear();
                    queue.Enqueue((a, m)); queue.Enqueue((m + 1, b));
                    foreach (var r in rest) queue.Enqueue(r);
                    continue;
                }
                final.Add((a, b, o));
            }
            final.Sort((x, y) => x.a.CompareTo(y.a));
            // A soma por página exagera (fontes e imagens repetidas): tenta juntar partes vizinhas enquanto couberem.
            for (int i = 0; i + 1 < final.Count;)
            {
                Job.Check();
                var o = Path.Combine(tmp, $"g{k++}.pdf");
                await Qpdf.Run(new[] { "--empty", "--pages", work, $"{final[i].a}-{final[i + 1].b}", "--", o });
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
        }
        finally { Paths.TryDelete(tmp); }
    }

    // ---------- OCR: texto pesquisável em PDF escaneado, pelo reconhecimento do próprio Windows ----------

    public static async Task<string?> Ocr(string src, P p)
    {
        var engine = OcrEngine() ?? throw new Exception("O reconhecimento de texto do Windows não está instalado para o português (Configurações → Hora e idioma → Idioma)");
        int n = Pdf.Pages(src);
        if (n == 0) throw new Exception("Esse PDF está danificado");
        var hasText = new bool[n];
        using (var d = UglyToad.PdfPig.PdfDocument.Open(src))
            for (int i = 0; i < n; i++)
                try { hasText[i] = d.GetPage(i + 1).Letters.Count(l => !string.IsNullOrWhiteSpace(l.Value)) >= 20; } catch { }
        if (hasText.All(h => h)) throw new Exception("Esse PDF já tem texto pesquisável");

        var winDoc = await Pdf.Open(src);
        var dst = Paths.Out(src, "pdf", "pesquisável");
        try
        {
            using var doc = PdfReader.Open(src, PdfDocumentOpenMode.Modify);
            var brush = new XSolidBrush(XColor.FromArgb(0, 0, 0, 0)); // invisível, mas selecionável e pesquisável
            for (int i = 0; i < n; i++)
            {
                Job.Check();
                Job.Progress(i / (double)n);
                if (hasText[i]) continue;
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
            if (Windows.Media.Ocr.OcrEngine.IsLanguageSupported(pt)) return Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(pt);
            return Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
        }
        catch { return null; }
    }

    static readonly Lazy<bool> ocrOk = new(() => OcrEngine() != null);
    public static bool OcrOk => ocrOk.Value;

    // ---------- Numerar páginas e carimbar ("Doc. 01") ----------

    /// p: numerar (1/0), formato ("Página {n} de {total}"), carimbo ("Doc. {d}"), inicio, digitos, posicao, tamanho, arquivos (ordem).
    public static Task<string?> Stamp(string src, P p) => Task.Run<string?>(() =>
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
            using var doc = PdfReader.Open(src, PdfDocumentOpenMode.Modify);
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
                if (docText.Length > 0) lines.Add((docText, bold));
                if (number) lines.Add((Tr.T(p.S("formato", "{n}").Replace("{n}", (i + 1).ToString()).Replace("{total}", total.ToString())), font));
                double lh = size * 1.3, bw = lines.Max(l => g.MeasureString(l.t, l.f).Width) + 10, bh = lh * lines.Count + 6;
                double x = pos[1] switch { 'e' => m, 'c' => (W - bw) / 2, _ => W - m - bw };
                double y = pos[0] == 's' ? m : H - m - bh;
                g.DrawRectangle(new XPen(XColor.FromArgb(160, 0, 0, 0), 0.6), new XSolidBrush(XColor.FromArgb(235, 255, 255, 255)), x, y, bw, bh);
                for (int j = 0; j < lines.Count; j++)
                    g.DrawString(lines[j].t, lines[j].f, XBrushes.Black, new XRect(x, y + 3 + j * lh, bw, lh), XStringFormats.Center);
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
