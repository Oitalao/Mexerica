using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using ImageMagick;
using ImageMagick.Drawing;

namespace Mexerica;

static class Images
{
    public static MagickImage Load(string src)
    {
        Job.Check();
        if (Path.GetExtension(src).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            return new MagickImage(src, new MagickReadSettings { Density = new Density(192), BackgroundColor = MagickColors.None });
        var img = new MagickImage(src);
        img.AutoOrient();
        return img;
    }

    static uint Q => (uint)Math.Clamp(Config.Now.ImageQuality, 30, 100);

    public static void Save(MagickImage img, string dst, string ext, uint? quality = null)
    {
        Job.Check();
        var q = quality ?? Q;
        switch (ext)
        {
            case "jpg":
                Flatten(img, MagickColors.White);
                img.Format = MagickFormat.Jpeg; img.Quality = q; break;
            case "png": img.Format = MagickFormat.Png; break;
            case "webp": img.Format = MagickFormat.WebP; img.Quality = q; break;
            case "avif":
                img.Format = MagickFormat.Avif; img.Quality = Math.Min(q, 85);
                img.Settings.SetDefine("heic:speed", "7"); // o padrão (5) é lento demais para foto grande
                break;
            case "heic": SaveHeic(img, dst, q); return;
            case "tiff": img.Format = MagickFormat.Tiff; img.Settings.Compression = CompressionMethod.LZW; break;
            case "bmp": Flatten(img, MagickColors.White); img.Format = MagickFormat.Bmp; break;
            case "gif": img.Format = MagickFormat.Gif; break;
            case "ico":
                if (img.Width > 256 || img.Height > 256) img.Resize(new MagickGeometry(256, 256));
                img.Format = MagickFormat.Ico; break;
            case "pdf": Flatten(img, MagickColors.White); img.Format = MagickFormat.Pdf; break;
            default: throw new Exception($"Formato {ext} não suportado");
        }
        img.Write(dst);
    }

    static void Flatten(MagickImage img, IMagickColor<byte> bg)
    {
        if (!img.HasAlpha) return;
        img.BackgroundColor = bg; img.Alpha(AlphaAction.Remove); img.Alpha(AlphaAction.Off);
    }

    /// HEIC pelo codificador do próprio Windows (extensões HEIF e HEVC).
    static void SaveHeic(MagickImage img, string dst, uint q)
    {
        using var copy = (MagickImage)img.Clone();
        copy.Format = MagickFormat.Png;
        var png = copy.ToByteArray();
        try
        {
            using var ms = new MemoryStream(png);
            var dec = Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ms.AsRandomAccessStream()).AsTask().GetAwaiter().GetResult();
            using var sb = dec.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied).AsTask().GetAwaiter().GetResult();
            using var outMs = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var props = new Windows.Graphics.Imaging.BitmapPropertySet
            {
                ["ImageQuality"] = new Windows.Graphics.Imaging.BitmapTypedValue(q / 100f, Windows.Foundation.PropertyType.Single),
            };
            var enc = Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.HeifEncoderId, outMs, props).AsTask().GetAwaiter().GetResult();
            enc.SetSoftwareBitmap(sb);
            enc.FlushAsync().AsTask().GetAwaiter().GetResult();
            outMs.Seek(0);
            using var s = outMs.AsStreamForRead();
            using var f = File.Create(dst);
            s.CopyTo(f);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Paths.TryDelete(dst);
            Log.Write("heic: " + ex);
            throw new Exception("O Windows não conseguiu gravar HEIC (faltam as extensões HEIF e HEVC da Microsoft Store?)");
        }
    }

    public static string Convert(string src, string ext)
    {
        var dst = Paths.Out(src, ext);
        using var img = Load(src);
        Save(img, dst, ext);
        return dst;
    }

    public static string NormExt(string src)
    {
        var e = Path.GetExtension(src).TrimStart('.').ToLowerInvariant();
        return e switch { "jpeg" or "jfif" => "jpg", "tif" => "tiff", "heif" => "heic", _ => e };
    }

    static readonly string[] writable = { "jpg", "png", "webp", "avif", "heic", "tiff", "bmp", "gif" };
    static string SameOrJpg(string src) { var e = NormExt(src); return writable.Contains(e) ? e : "jpg"; }
    static string SameOrPng(string src) { var e = NormExt(src); return writable.Contains(e) && e != "bmp" ? e : "png"; }

    static long TargetBytes(string src, P p) => p.S("modo") switch
    {
        "pct" => (long)(new FileInfo(src).Length * Math.Clamp(p.D("pct", 50), 1, 99) / 100),
        "mb" => (long)(p.D("mb", 1) * 1024 * 1024),
        _ => 0,
    };

    // ---------- Comprimir ----------
    public static string Compress(string src, P p)
    {
        using var orig = Load(src);
        var ext = SameOrJpg(src);
        if (ext is "tiff" or "bmp" or "gif") ext = orig.HasAlpha ? "png" : "jpg";
        var dst = Paths.Out(src, ext, "comprimido");
        long target = TargetBytes(src, p);
        orig.Strip();

        if (target <= 0)
        {
            var (q, max) = p.S("nivel", "media") switch { "leve" => (85u, 4000u), "forte" => (58u, 1920u), _ => (72u, 2560u) };
            using var img = (MagickImage)orig.Clone();
            img.Resize(new MagickGeometry(max, max) { Greater = true });
            if (ext == "png") Quant(img, p.S("nivel") == "forte" ? 128u : 256u);
            Save(img, dst, ext, q);
            return dst;
        }

        // Tamanho alvo: busca a qualidade; se não couber, diminui as dimensões.
        double scale = 1;
        for (int round = 0; round < 14; round++)
        {
            Job.Progress(round / 14.0);
            using var img = (MagickImage)orig.Clone();
            if (scale < 1) img.Resize(new Percentage(scale * 100));
            if (ext == "png")
            {
                Quant(img, 256);
                var b = Bytes(img, ext, 100);
                if (b.Length <= target || round == 13) { File.WriteAllBytes(dst, b); return dst; }
            }
            else
            {
                uint lo = 25, hi = 95; byte[]? best = null;
                while (lo <= hi)
                {
                    uint mid = (lo + hi) / 2;
                    var b = Bytes(img, ext, mid);
                    if (b.Length <= target) { best = b; lo = mid + 1; } else hi = mid - 1;
                }
                if (best != null) { File.WriteAllBytes(dst, best); return dst; }
                if (round == 13) { File.WriteAllBytes(dst, Bytes(img, ext, 25)); return dst; }
            }
            scale *= 0.8;
        }
        return dst;
    }

    static void Quant(MagickImage img, uint colors)
    {
        img.Quantize(new QuantizeSettings { Colors = colors, DitherMethod = DitherMethod.FloydSteinberg });
        img.Settings.SetDefine(MagickFormat.Png, "compression-level", "9");
    }

    static byte[] Bytes(MagickImage img, string ext, uint q)
    {
        Job.Check();
        using var c = (MagickImage)img.Clone();
        if (ext == "heic")
        {
            var tmp = Path.Combine(Paths.Temp(), "x.heic");
            try { SaveHeic(c, tmp, q); return File.ReadAllBytes(tmp); } finally { Paths.TryDelete(Path.GetDirectoryName(tmp)!); }
        }
        if (ext == "jpg") Flatten(c, MagickColors.White);
        c.Format = ext switch { "jpg" => MagickFormat.Jpeg, "png" => MagickFormat.Png, "webp" => MagickFormat.WebP, "avif" => MagickFormat.Avif, _ => MagickFormat.Jpeg };
        c.Quality = q;
        if (ext == "avif") c.Settings.SetDefine("heic:speed", "7");
        return c.ToByteArray();
    }

    // ---------- Tamanho ----------
    public static string Resize(string src, P p)
    {
        using var img = Load(src);
        var v = p.D("valor", 50);
        string suffix;
        switch (p.S("modo", "pct"))
        {
            case "largura": img.Resize((uint)v, 0); suffix = $"{v:0}px"; break;
            case "altura": img.Resize(0, (uint)v); suffix = $"{v:0}px"; break;
            case "lado": img.Resize(new MagickGeometry((uint)v, (uint)v)); suffix = $"{v:0}px"; break;
            default: img.Resize(new Percentage(v)); suffix = $"{v:0}%"; break;
        }
        var ext = SameOrJpg(src);
        var dst = Paths.Out(src, ext, suffix);
        Save(img, dst, ext);
        return dst;
    }

    // ---------- Girar ----------
    public static string Rotate(string src, P p)
    {
        using var img = Load(src);
        var how = p.S("giro", "90");
        switch (how)
        {
            case "h": img.Flop(); break;
            case "v": img.Flip(); break;
            default: img.Rotate(double.Parse(how, CultureInfo.InvariantCulture)); break;
        }
        var ext = SameOrJpg(src);
        var dst = Paths.Out(src, ext, how is "h" or "v" ? "espelhada" : "girada");
        Save(img, dst, ext, Math.Max(Q, 92));
        return dst;
    }

    public static string Strip(string src)
    {
        using var img = new MagickImage(src);
        img.AutoOrient();
        img.Strip();
        var ext = SameOrJpg(src);
        var dst = Paths.Out(src, ext, "sem metadados");
        Save(img, dst, ext, img.Quality > 0 ? Math.Min(img.Quality, 100) : 92);
        return dst;
    }

    // ---------- Editar (recorte, ajustes, anotações, tarjas) ----------
    /// p: tarjas="x,y,w,h,modo;..." (pixels da imagem, modo preto|desfoque|pixel), brilho/contraste/saturacao (-100..100),
    /// nitidez (0..100), camada=PNG transparente do tamanho da imagem, recorte="x,y,w,h", giro (0/90/180/270).
    public static string Edit(string src, P p)
    {
        using var img = Load(src);
        foreach (var r in Rects(p.S("tarjas")))
        {
            var (x, y, w, h, mode) = r;
            var geo = new MagickGeometry(x, y, (uint)w, (uint)h);
            switch (mode)
            {
                case "desfoque":
                {
                    using var part = (MagickImage)img.Clone(); part.Crop(geo); part.ResetPage();
                    part.Blur(0, Math.Max(8, Math.Min(w, h) / 6.0));
                    img.Composite(part, x, y, CompositeOperator.Over);
                    break;
                }
                case "pixel":
                {
                    using var part = (MagickImage)img.Clone(); part.Crop(geo); part.ResetPage();
                    var cell = Math.Max(8, Math.Min(w, h) / 10);
                    part.Scale(new MagickGeometry((uint)Math.Max(1, w / cell), (uint)Math.Max(1, h / cell)) { IgnoreAspectRatio = true });
                    part.Scale(new MagickGeometry((uint)w, (uint)h) { IgnoreAspectRatio = true });
                    img.Composite(part, x, y, CompositeOperator.Over);
                    break;
                }
                default:
                    new Drawables().FillColor(MagickColors.Black).StrokeColor(MagickColors.Black).Rectangle(x, y, x + w - 1, y + h - 1).Draw(img);
                    break;
            }
        }
        Adjust(img, p.D("brilho"), p.D("contraste"), p.D("saturacao"), p.D("nitidez"));
        var layer = p.S("camada");
        if (layer.Length > 0 && File.Exists(layer))
        {
            using var ov = new MagickImage(layer);
            if (ov.Width != img.Width || ov.Height != img.Height) ov.Resize(new MagickGeometry(img.Width, img.Height) { IgnoreAspectRatio = true });
            img.Composite(ov, 0, 0, CompositeOperator.Over);
        }
        var crop = Nums(p.S("recorte"));
        if (crop.Length == 4 && crop[2] > 0 && crop[3] > 0)
        {
            img.Crop(new MagickGeometry((int)crop[0], (int)crop[1], (uint)crop[2], (uint)crop[3]));
            img.ResetPage();
        }
        var rot = p.I("giro");
        if (rot % 360 != 0) img.Rotate(rot);
        var ext = SameOrPng(src);
        var dst = Paths.Out(src, ext, "editada");
        Save(img, dst, ext, Math.Max(Q, 92));
        return dst;
    }

    /// Ajustes de cor; a prévia do editor usa a mesma conta (nitidez já escalada para o tamanho da prévia).
    public static void Adjust(MagickImage img, double br, double ct, double sat, double sharp)
    {
        if (br != 0 || sat != 0) img.Modulate(new Percentage(100 + br * 0.6), new Percentage(100 + sat));
        if (ct != 0) img.BrightnessContrast(new Percentage(0), new Percentage(ct * 0.6));
        if (sharp > 0.01) img.UnsharpMask(0, 0.5 + sharp / 40, 0.5 + sharp / 100, 0.02);
    }

    public static IEnumerable<(int x, int y, int w, int h, string mode)> Rects(string s)
    {
        foreach (var part in s.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = part.Split(',');
            if (f.Length < 4) continue;
            int Num(int i) => (int)Math.Round(double.Parse(f[i], CultureInfo.InvariantCulture));
            var (w, h) = (Num(2), Num(3));
            if (w <= 0 || h <= 0) continue;
            yield return (Num(0), Num(1), w, h, f.Length > 4 ? f[4] : "preto");
        }
    }

    public static double[] Nums(string s) =>
        s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();

    // ---------- Fundo ----------
    public static string Background(string src, P p)
    {
        using var img = Load(src);
        var color = new MagickColor(p.S("cor", "#FFFFFF"));
        int margin = (int)Math.Round(Math.Max(img.Width, img.Height) * p.D("margem", 0) / 100);
        uint w = img.Width + (uint)(2 * margin), h = img.Height + (uint)(2 * margin);
        switch (p.S("formato", "original"))
        {
            case "quadrado": w = h = Math.Max(w, h); break;
            case "16:9": if (w * 9 > h * 16) h = w * 9 / 16; else w = h * 16 / 9; break;
            case "4:5": if (w * 5 > h * 4) h = w * 5 / 4; else w = h * 4 / 5; break;
            case "9:16": if (w * 16 > h * 9) h = w * 16 / 9; else w = h * 9 / 16; break;
        }
        using var canvas = new MagickImage(color, w, h);
        canvas.Composite(img, Gravity.Center, CompositeOperator.Over);
        var ext = NormExt(src) is "png" or "webp" or "avif" or "heic" or "tiff" ? NormExt(src) : "jpg";
        if (ext == "jpg" || color.A == 255) canvas.Alpha(AlphaAction.Off);
        var dst = Paths.Out(src, ext, "com fundo");
        Save(canvas, dst, ext, Math.Max(Q, 92));
        return dst;
    }

    // ---------- PDF e colagem ----------
    public static string ToPdf(string[] files)
    {
        using var all = new MagickImageCollection();
        foreach (var f in files)
        {
            var img = Load(f);
            Flatten(img, MagickColors.White);
            img.Density = new Density(150);
            all.Add(img);
        }
        var dst = Paths.Out(files[0], "pdf", files.Length > 1 ? "juntas" : null);
        all.Write(dst, MagickFormat.Pdf);
        return dst;
    }

    public static string Collage(string[] files, P p)
    {
        int n = files.Length;
        int cols = p.I("colunas", 0);
        if (cols <= 0) cols = (int)Math.Ceiling(Math.Sqrt(n));
        int rows = (int)Math.Ceiling(n / (double)cols);
        int total = Math.Clamp(p.I("largura", 2400), 300, 12000);
        int gap = Math.Clamp(p.I("espaco", 12), 0, 400);
        int cell = (total - gap * (cols + 1)) / cols;
        if (cell < 20) throw new Exception("Espaço demais para essa largura");
        var bg = new MagickColor(p.S("cor", "#FFFFFF"));
        bool fill = p.S("encaixe", "preencher") == "preencher";
        using var canvas = new MagickImage(bg, (uint)total, (uint)(rows * cell + gap * (rows + 1)));
        for (int i = 0; i < n; i++)
        {
            Job.Progress(i / (double)n);
            using var img = Load(files[i]);
            if (fill)
            {
                img.Resize(new MagickGeometry((uint)cell, (uint)cell) { FillArea = true });
                img.Crop((uint)cell, (uint)cell, Gravity.Center); img.ResetPage();
            }
            else img.Resize(new MagickGeometry((uint)cell, (uint)cell));
            int r = i / cols, c = i % cols;
            int x = gap + c * (cell + gap) + (cell - (int)img.Width) / 2;
            int y = gap + r * (cell + gap) + (cell - (int)img.Height) / 2;
            canvas.Composite(img, x, y, CompositeOperator.Over);
        }
        var ext = bg.A < 255 ? "png" : p.S("formato", "jpg");
        if (ext == "jpg") canvas.Alpha(AlphaAction.Off);
        var dst = Paths.Out(files[0], ext, "colagem");
        Save(canvas, dst, ext, Math.Max(Q, 92));
        return dst;
    }

    // ---------- QR ----------
    public static List<string> ReadCodes(string src)
    {
        using var img = Load(src);
        Flatten(img, MagickColors.White);
        if (img.Width > 3000 || img.Height > 3000) img.Resize(new MagickGeometry(3000, 3000));
        img.ColorSpace = ColorSpace.sRGB;
        img.Depth = 8;
        using var pixels = img.GetPixels();
        var px = pixels.ToByteArray(PixelMapping.RGB) ?? throw new Exception("Não consegui ler os pixels");
        var reader = new ZXing.BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions { TryHarder = true, TryInverted = true },
        };
        var lum = new ZXing.RGBLuminanceSource(px, (int)img.Width, (int)img.Height, ZXing.RGBLuminanceSource.BitmapFormat.RGB24);
        var res = reader.DecodeMultiple(lum);
        return res?.Select(r => r.Text).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList() ?? new List<string>();
    }
}
