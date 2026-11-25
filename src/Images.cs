using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using ImageMagick;
using ImageMagick.Drawing;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Mexerica;

static class Images
{
    public static MagickImage Load(string src)
    {
        Job.Check();
        if (NormExt(src) is "icns" or "jxr" or "wdp" or "hdp" or "emf" or "wmf" or "dcm" or "svgz" or "xcf") return RasterFormats.Read(src);
        // RAW de câmera: o módulo descarta a moldura virtual e, no X3F, cai para o JPEG embutido.
        if (CameraRaw.Contains(NormExt(src))) return RasterFormats.Read(src);
        return LoadPlain(src);
    }

    static readonly HashSet<string> CameraRaw = new(StringComparer.OrdinalIgnoreCase)
        { "cr2", "cr3", "crw", "nef", "arw", "dng", "orf", "rw2", "raf", "sr2", "srf", "srw", "pef", "3fr", "erf", "k25", "kdc", "mos", "mrw", "nrw", "raw", "rwl", "x3f" };

    /// Leitura direta pelo ImageMagick, sem os leitores próprios dos módulos.
    public static MagickImage LoadPlain(string src)
    {
        Job.Check();
        if (Path.GetExtension(src).Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            var settings = new MagickReadSettings { Density = new Density(192), BackgroundColor = MagickColors.None };
            if (src.Length < 260) return new MagickImage(src, settings);
            using var svg = File.OpenRead(src);
            return new MagickImage(svg, settings);
        }
        MagickImage img;
        if (src.Length < 260) img = new MagickImage(src);
        else { using var input = File.OpenRead(src); img = new MagickImage(input); }
        img.AutoOrient();
        return img;
    }

    static uint Q => (uint)Math.Clamp(Config.Now.ImageQuality, 30, 100);

    public static void Save(MagickImage img, string dst, string ext, uint? quality = null)
    {
        Job.Check();
        if (ext == "heic") { SaveHeic(img, dst, quality ?? Q); return; }
        Prepare(img, ext, quality ?? Q);
        if (dst.Length < 260) img.Write(dst);
        else { using var output = File.Create(dst); img.Write(output); }
    }

    static void Prepare(MagickImage img, string ext, uint q)
    {
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
            case "tiff": img.Format = MagickFormat.Tiff; img.Settings.Compression = CompressionMethod.LZW; break;
            case "bmp": Flatten(img, MagickColors.White); img.Format = MagickFormat.Bmp; break;
            case "gif": img.Format = MagickFormat.Gif; break;
            case "ico":
                if (img.Width > 256 || img.Height > 256) img.Resize(new MagickGeometry(256, 256));
                img.Format = MagickFormat.Ico; break;
            case "pdf": Flatten(img, MagickColors.White); img.Format = MagickFormat.Pdf; break;
            default: throw new Exception($"Formato {ext} não suportado");
        }
    }

    public static bool MultiFrame(string ext) => ext is "gif" or "webp" or "tiff" or "apng" or "pdf";

    public static bool AnimatedPng(string src)
    {
        if (NormExt(src) is not ("png" or "apng")) return false;
        using var file = File.OpenRead(src);
        var header = new byte[8];
        if (file.Read(header) != 8 || !header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return false;
        while (file.Read(header) == 8)
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header);
            var type = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            if (type == "acTL") return true;
            if (type is "IDAT" or "IEND" || length > file.Length - file.Position - 4) return false;
            file.Seek(length + 4L, SeekOrigin.Current);
        }
        return false;
    }

    static void ReadAnimatedPng(string src, MagickImageCollection frames)
    {
        uint iterations = 0;
        using (var file = File.OpenRead(src))
        {
            file.Position = 8;
            var header = new byte[8];
            while (file.Read(header) == 8)
            {
                var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header);
                if (length > file.Length - file.Position - 4) break;
                var type = Encoding.ASCII.GetString(header, 4, 4);
                if (type == "acTL" && length == 8 && file.Read(header) == 8)
                {
                    iterations = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4));
                    break;
                }
                if (type is "IDAT" or "IEND") break;
                file.Seek(length + 4L, SeekOrigin.Current);
            }
        }
        var tmp = Paths.Temp();
        try
        {
            var delays = Proc.RunOutput(Ff.Probe!, new[] { "-v", "error", "-show_frames", "-show_entries", "frame=duration_time", "-of", "csv=p=0", src })
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0.1).ToArray();
            var ffmpeg = Ff.Exe ?? throw new Exception("ffmpeg não encontrado");
            Proc.Run(ffmpeg, new[] { "-hide_banner", "-nostdin", "-y", "-i", src, "-fps_mode", "passthrough", Path.Combine(tmp, "%08d.png") }).GetAwaiter().GetResult();
            var files = Directory.GetFiles(tmp, "*.png").OrderBy(f => f, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < files.Length; i++)
            {
                Job.Check();
                var frame = new MagickImage(files[i]) { AnimationIterations = iterations, AnimationTicksPerSecond = 1000, AnimationDelay = (uint)Math.Max(1, Math.Round((i < delays.Length ? delays[i] : 0.1) * 1000)) };
                frames.Add(frame);
            }
        }
        finally { Paths.TryDelete(tmp); }
    }

    public static MagickImageCollection LoadFrames(string src, string ext)
    {
        if (MultiFrame(ext) && NormExt(src) == "dcm" && Dicom.TryRead(src) is { } dicom) return dicom;
        var frames = new MagickImageCollection();
        try
        {
            if (MultiFrame(ext) && AnimatedPng(src)) ReadAnimatedPng(src, frames);
            else if (MultiFrame(ext) && NormExt(src) is "gif" or "webp" or "tiff" or "apng" or "png" or "dcm")
            {
                if (src.Length < 260) frames.Read(src);
                else { using var input = File.OpenRead(src); frames.Read(input); }
                if (NormExt(src) is "gif" or "webp" or "apng" or "png") frames.Coalesce();
                foreach (var frame in frames) frame.AutoOrient();
            }
            else frames.Add(Load(src));
            return frames;
        }
        catch { frames.Dispose(); throw; }
    }

    public static void SaveFrames(MagickImageCollection frames, string dst, string ext, uint? quality = null)
    {
        Job.Check();
        if (ext == "apng")
        {
            var tmp = Paths.Temp();
            try
            {
                var list = new List<string>();
                for (int i = 0; i < frames.Count; i++)
                {
                    var file = Path.Combine(tmp, $"{i}.png");
                    frames[i].Write(file, MagickFormat.Png);
                    list.Add($"file {i}.png");
                    list.Add("option framerate 1000");
                    var delay = frames[i].AnimationDelay / (double)Math.Max(1, frames[i].AnimationTicksPerSecond);
                    list.Add("duration " + (delay > 0 ? delay : 0.1).ToString("0.#########", CultureInfo.InvariantCulture));
                }
                var concat = Path.Combine(tmp, "frames.txt");
                File.WriteAllLines(concat, list, new System.Text.UTF8Encoding(false));
                var ffmpeg = Ff.Exe ?? throw new Exception("ffmpeg não encontrado");
                Proc.Run(ffmpeg, new[] { "-hide_banner", "-nostdin", "-y", "-f", "concat", "-safe", "0", "-i", concat, "-fps_mode", "vfr", "-pix_fmt", "rgba", "-plays", frames[0].AnimationIterations.ToString(CultureInfo.InvariantCulture), "-final_delay", $"{Math.Max(1, frames[frames.Count - 1].AnimationDelay)}/{Math.Max(1, frames[frames.Count - 1].AnimationTicksPerSecond)}", "-f", "apng", dst }).GetAwaiter().GetResult();
            }
            catch { Paths.TryDelete(dst); throw; }
            finally { Paths.TryDelete(tmp); }
            return;
        }
        if (frames.Count == 1 || !MultiFrame(ext)) { Save((MagickImage)frames[0], dst, ext, quality); return; }
        foreach (MagickImage frame in frames) Prepare(frame, ext, quality ?? Q);
        try
        {
            if (dst.Length < 260) frames.Write(dst);
            else { using var output = File.Create(dst); frames.Write(output); }
        }
        catch { Paths.TryDelete(dst); throw; }
    }

    static void Flatten(MagickImage img, IMagickColor<byte> bg)
    {
        if (!img.HasAlpha) return;
        img.BackgroundColor = bg; img.Alpha(AlphaAction.Remove); img.Alpha(AlphaAction.Off);
    }

    /// HEIC sem depender de nada instalado no Windows: o ffmpeg embutido codifica em HEVC (x265)
    /// e o Heif.Wrap monta o arquivo .heic em volta.
    static void SaveHeic(MagickImage img, string dst, uint q)
    {
        var tmp = Paths.Temp();
        try
        {
            using var copy = (MagickImage)img.Clone();
            Flatten(copy, MagickColors.White);
            var width = copy.Width;
            var height = copy.Height;
            // HEVC exige pelo menos 16 pixels e 4:2:0 exige dimensões pares; o HEIF recorta só o preenchimento.
            copy.Extent(Math.Max(16u, (width + 1) & ~1u), Math.Max(16u, (height + 1) & ~1u), Gravity.Northwest);
            var png = Path.Combine(tmp, "in.png");
            copy.Format = MagickFormat.Png;
            copy.Settings.SetDefine(MagickFormat.Png, "compression-level", "1");
            copy.Write(png);
            var mp4 = Path.Combine(tmp, "out.mp4");
            int crf = (int)Math.Round(14 + (100 - Math.Clamp(q, 1, 100)) * 0.5); // 90 → 19, 80 → 24, 70 → 29
            Proc.Run(Ff.Exe ?? throw new Exception("ffmpeg não encontrado"), new[]
            {
                "-hide_banner", "-nostdin", "-y", "-i", png, "-frames:v", "1", "-an",
                "-vf", "scale=out_color_matrix=bt601:out_range=tv,format=yuv420p",
                "-c:v", "libx265", "-preset", "fast", "-crf", crf.ToString(), "-tag:v", "hvc1",
                "-x265-params", "log-level=error:keyint=1:info=0", mp4,
            }).GetAwaiter().GetResult();
            Heif.Wrap(mp4, dst, (int)copy.Width, (int)copy.Height, (int)width, (int)height);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Paths.TryDelete(dst);
            Log.Write("heic: " + ex);
            throw new Exception("Não consegui gravar HEIC: " + ex.Message);
        }
        finally { Paths.TryDelete(tmp); }
    }

    public static string Convert(string src, string ext)
    {
        var dst = Paths.Out(src, ext);
        using var frames = LoadFrames(src, ext);
        SaveFrames(frames, dst, ext);
        return dst;
    }

    public static string NormExt(string src)
    {
        var e = Path.GetExtension(src).TrimStart('.').ToLowerInvariant();
        return e switch { "jpeg" or "jfif" => "jpg", "tif" => "tiff", "heif" => "heic", _ => e };
    }

    public static string FrameExt(string src) => AnimatedPng(src) ? "apng" : NormExt(src) == "dcm" ? "tiff" : NormExt(src);

    static readonly string[] writable = { "jpg", "png", "webp", "avif", "heic", "tiff", "bmp", "gif", "apng" };
    static string SameOrJpg(string src) { var e = FrameExt(src); return writable.Contains(e) ? e : "jpg"; }
    static string SameOrPng(string src) { var e = FrameExt(src); return writable.Contains(e) && e != "bmp" ? e : "png"; }

    static long TargetBytes(string src, P p) => p.S("modo") switch
    {
        "pct" => (long)(new FileInfo(src).Length * Math.Clamp(p.D("pct", 50), 1, 99) / 100),
        "mb" => (long)(p.D("mb", 1) * 1024 * 1024),
        _ => 0,
    };

    // ---------- Comprimir ----------
    public static string Compress(string src, P p)
    {
        var sourceExt = FrameExt(src);
        using var frames = LoadFrames(src, sourceExt);
        if (frames.Count > 1) return CompressFrames(src, p, frames, sourceExt);
        using var orig = (MagickImage)frames[0].Clone();
        var ext = sourceExt == "apng" ? "png" : SameOrJpg(src);
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
                if (b.Length <= target) { File.WriteAllBytes(dst, b); return dst; }
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
                if (round == 13) throw new Exception("Esse tamanho é pequeno demais para a imagem");
            }
            scale *= 0.8;
        }
        throw new Exception("Esse tamanho é pequeno demais para a imagem");
    }

    static string CompressFrames(string src, P p, MagickImageCollection originals, string ext)
    {
        var dst = Paths.Out(src, ext, "comprimido");
        long target = TargetBytes(src, p);
        var (quality, max) = p.S("nivel", "media") switch { "leve" => (85u, 4000u), "forte" => (58u, 1920u), _ => (72u, 2560u) };
        var tmp = Paths.Temp();
        try
        {
            for (int round = 0; round < (target > 0 ? 30 : 1); round++)
            {
                Job.Progress(round / 30.0);
                using var frames = new MagickImageCollection();
                foreach (var original in originals)
                {
                    var image = (MagickImage)original.Clone();
                    frames.Add(image);
                    image.Strip();
                    if (target > 0) image.Resize(new Percentage(Math.Pow(0.8, round) * 100));
                    else image.Resize(new MagickGeometry(max, max) { Greater = true });
                    if (ext is "gif" or "tiff") Quant(image, p.S("nivel") == "forte" ? 128u : 256u);
                    image.ResetPage();
                }
                var candidate = Path.Combine(tmp, "comprimido." + ext);
                SaveFrames(frames, candidate, ext, target > 0 ? 50u : quality);
                if (target <= 0 || new FileInfo(candidate).Length <= target)
                {
                    File.Move(candidate, dst);
                    return dst;
                }
                if (frames.All(f => f.Width == 1 && f.Height == 1)) break;
            }
            throw new Exception("Esse tamanho é pequeno demais para a imagem");
        }
        finally { Paths.TryDelete(tmp); }
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
        var ext = NormExt(src) == "apng" ? "apng" : SameOrJpg(src);
        using var frames = LoadFrames(src, ext);
        var v = p.D("valor", 50);
        foreach (var img in frames)
        {
            switch (p.S("modo", "pct"))
            {
                case "largura": img.Resize((uint)v, 0); break;
                case "altura": img.Resize(0, (uint)v); break;
                case "lado": img.Resize(new MagickGeometry((uint)v, (uint)v)); break;
                default: img.Resize(new Percentage(v)); break;
            }
            img.ResetPage();
        }
        var suffix = p.S("modo", "pct") == "pct" ? $"{v:0}%" : $"{v:0}px";
        var dst = Paths.Out(src, ext, suffix);
        SaveFrames(frames, dst, ext);
        return dst;
    }

    // ---------- Girar ----------
    public static string Rotate(string src, P p)
    {
        var ext = NormExt(src) == "apng" ? "apng" : SameOrJpg(src);
        using var frames = LoadFrames(src, ext);
        var how = p.S("giro", "90");
        foreach (var img in frames)
        {
            switch (how)
            {
                case "h": img.Flop(); break;
                case "v": img.Flip(); break;
                default: img.Rotate(double.Parse(how, CultureInfo.InvariantCulture)); break;
            }
            img.ResetPage();
        }
        var dst = Paths.Out(src, ext, how is "h" or "v" ? "espelhada" : "girada");
        SaveFrames(frames, dst, ext, Math.Max(Q, 92));
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
        var ext = NormExt(src) == "apng" ? "apng" : SameOrPng(src);
        using var frames = LoadFrames(src, ext);
        foreach (MagickImage img in frames)
        {
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
        }
        var dst = Paths.Out(src, ext, "editada");
        SaveFrames(frames, dst, ext, Math.Max(Q, 92));
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
        var inputExt = FrameExt(src);
        var ext = inputExt is "png" or "webp" or "avif" or "heic" or "tiff" or "gif" or "apng" ? inputExt : "jpg";
        using var frames = LoadFrames(src, ext);
        var color = new MagickColor(p.S("cor", "#FFFFFF"));
        foreach (MagickImage img in frames)
        {
            int margin = (int)Math.Round(Math.Max(img.Width, img.Height) * p.D("margem", 0) / 100);
            uint w = img.Width + (uint)(2 * margin), h = img.Height + (uint)(2 * margin);
            switch (p.S("formato", "original"))
            {
                case "quadrado": w = h = Math.Max(w, h); break;
                case "16:9": if (w * 9 > h * 16) h = w * 9 / 16; else w = h * 16 / 9; break;
                case "4:5": if (w * 5 > h * 4) h = w * 5 / 4; else w = h * 4 / 5; break;
                case "9:16": if (w * 16 > h * 9) h = w * 16 / 9; else w = h * 9 / 16; break;
            }
            // Coalesced frames retain their timing while the canvas grows around them.
            img.BackgroundColor = MagickColors.None;
            img.Extent(w, h, Gravity.Center);
            using var canvas = new MagickImage(color, w, h);
            img.Composite(canvas, CompositeOperator.DstOver);
            if (ext == "jpg" || color.A == 255) img.Alpha(AlphaAction.Off);
            img.ResetPage();
        }
        var dst = Paths.Out(src, ext, "com fundo");
        SaveFrames(frames, dst, ext, Math.Max(Q, 92));
        return dst;
    }

    // ---------- Remover fundo (IA) ----------
    public static string RemoveBackground(string src, P _)
    {
        using var frames = LoadFrames(src, FrameExt(src));
        foreach (MagickImage original in frames)
        {
            int ow = (int)original.Width, oh = (int)original.Height;

            using var work = original.Clone();
            work.Alpha(AlphaAction.Off);
            work.ColorSpace = ColorSpace.sRGB;
            work.Depth = 8;
            work.Resize(new MagickGeometry((uint)Bg.Size, (uint)Bg.Size) { IgnoreAspectRatio = true });
            using var workPixels = work.GetPixels();
            var rgb = workPixels.ToByteArray(PixelMapping.RGB) ?? throw new Exception("Não consegui ler os pixels");

            Job.Check();
            double max = 1e-6;
            foreach (var b in rgb) if (b > max) max = b;

            // Mesmo pré-processamento do rembg: RGB 0..1 pelo pico da própria foto (não 255 fixo), depois -0.5.
            var input = new DenseTensor<float>(new[] { 1, 3, Bg.Size, Bg.Size });
            for (int y = 0; y < Bg.Size; y++)
                for (int x = 0; x < Bg.Size; x++)
                {
                    int o = (y * Bg.Size + x) * 3;
                    for (int c = 0; c < 3; c++)
                        input[0, c, y, x] = (float)(rgb[o + c] / max - 0.5);
                }

            var mask = Bg.Mask(input);
            Job.Check();
            var maskBytes = new byte[mask.Length * 3];
            for (int i = 0; i < mask.Length; i++)
            {
                byte v = (byte)Math.Round(mask[i] * 255);
                maskBytes[i * 3] = maskBytes[i * 3 + 1] = maskBytes[i * 3 + 2] = v;
            }

            using var maskImg = new MagickImage(MagickColors.Black, (uint)Bg.Size, (uint)Bg.Size);
            using (var mp = maskImg.GetPixels()) mp.SetBytePixels(maskBytes);
            maskImg.Resize(new MagickGeometry((uint)ow, (uint)oh) { IgnoreAspectRatio = true });
            using var maskPixels = maskImg.GetPixels();
            var maskOut = maskPixels.ToByteArray(PixelMapping.RGB)!;

            // A máscara só reduz o alfa existente; as cores e os pixels já transparentes ficam intactos.
            original.Alpha(AlphaAction.On);
            using var origPixels = original.GetPixels();
            var rgba = origPixels.ToByteArray(PixelMapping.RGBA)!;
            for (int i = 0; i < ow * oh; i++) rgba[i * 4 + 3] = (byte)((rgba[i * 4 + 3] * maskOut[i * 3] + 127) / 255);
            origPixels.SetBytePixels(rgba);
        }

        var ext = NormExt(src) is "webp" or "tiff" ? NormExt(src) : frames.Count > 1 ? "apng" : NormExt(src) == "avif" ? "avif" : "png";
        var dst = Paths.Out(src, ext, "sem fundo");
        SaveFrames(frames, dst, ext, 92);
        return dst;
    }

    // ---------- PDF e colagem ----------
    public static string ToPdf(string[] files, string? name = null)
    {
        using var all = new MagickImageCollection();
        foreach (var f in files)
        {
            using var frames = LoadFrames(f, "pdf");
            foreach (var frame in frames)
            {
                var img = (MagickImage)frame.Clone();
                Flatten(img, MagickColors.White);
                img.Density = new Density(150);
                all.Add(img);
            }
        }
        var dst = Paths.Out(files[0], "pdf", name: name);
        if (dst.Length < 260) all.Write(dst, MagickFormat.Pdf);
        else { using var output = File.Create(dst); all.Write(output, MagickFormat.Pdf); }
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
