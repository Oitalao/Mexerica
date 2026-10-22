using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ImageMagick;
using Mexerica.Tracing;

namespace Mexerica;

sealed class RasterFormats : Module
{
    static readonly string[] extra = "psd psb jxl jp2 j2k tga dds qoi pcx apng icns cur ppm pgm pbm cr2 cr3 crw nef arw dng orf rw2 raf sr2 srf srw pef 3fr erf k25 kdc mos mrw nrw raw rwl x3f dcm exr hdr xcf jxr wdp hdp emf wmf svgz".Split(' ');
    static readonly string[] common = "jpg png webp pdf avif heic tiff bmp gif ico".Split(' ');
    static readonly string[] newOutputs = "jxl jp2 psd tga dds qoi pcx icns apng svg".Split(' ');
    static readonly string[] baseInputs = "jpg jpeg jfif png webp heic heif tif tiff avif bmp gif ico svg".Split(' ');
    public override string Name => "Imagens e vetor";
    public override IEnumerable<string> Inputs => extra.Concat(baseInputs);
    public override Kind KindHint => Kind.Image;
    public override bool Overrides(string inExt, string outExt) => extra.Contains(inExt);

    public override IEnumerable<Target> TargetsFor(string inExt)
    {
        if (inExt == "svgz") return common.Concat(newOutputs).Where(e => e != "svgz").Select(e => new Target(e, e == "svg" ? "vetor" : "imagem"));
        if (inExt == "svg") return new[] { new Target("svgz", "compactado") }.Concat(newOutputs.Where(e => e != "svg").Select(e => new Target(e, "imagem")));
        if (inExt == "emf" || inExt == "wmf") return new[] { new Target("png", "imagem"), new Target("pdf", "documento"), new Target("svg", "vetorizar") };
        return common.Concat(newOutputs).Where(e => e != inExt).Select(e => new Target(e, e == "svg" ? "vetorizar" : "imagem"));
    }

    // APNG só faz sentido para animação: GIF, WebP ou APNG com mais de um quadro.
    public override IEnumerable<Target> TargetsFor(string inExt, string path)
    {
        bool animated = false;
        if (inExt is "gif" or "webp" or "apng" or "png")
        {
            try
            {
                using var frames = new ImageMagick.MagickImageCollection();
                frames.Ping(path);
                animated = frames.Count > 1;
            }
            catch { }
        }
        foreach (var t in TargetsFor(inExt))
            if (t.Ext != "apng" || animated) yield return t;
        if (animated && inExt == "apng") yield return new Target("gif", "animação");
    }

    public override IEnumerable<Op> Tools(string[] files)
    {
        if (files.Length == 0 || files.Any(f => !Inputs.Contains(Modules.Ext(f)))) return Array.Empty<Op>();
        return new[] { new Op { Label = "Cinza", Sub = "tons de cinza", One = (f, _) => Task.Run(() => (string?)Gray(f)) } };
    }

    public override Task<string?> Convert(string src, string outExt, P p) => Task.Run<string?>(() => Run(src, outExt));

    static string Gray(string src)
    {
        using var image = Read(src);
        image.ColorSpace = ColorSpace.Gray;
        var dst = Paths.Out(src, "png", "cinza");
        Images.Save(image, dst, "png");
        return dst;
    }

    static string Run(string src, string ext)
    {
        Job.Check();
        var input = Modules.Ext(src);
        var dst = Paths.Out(src, ext);
        try
        {
            if (input == "svgz")
            {
                using var zipped = File.OpenRead(src);
                using var gz = new GZipStream(zipped, CompressionMode.Decompress);
                using var output = File.Create(dst);
                gz.CopyTo(output);
            }
            else if (ext == "svgz")
            {
                using var output = File.Create(dst);
                using var gz = new GZipStream(output, CompressionLevel.Optimal);
                using var source = File.OpenRead(src);
                source.CopyTo(gz);
            }
            else if (ext == "apng" || (input == "apng" && ext == "gif"))
            {
                var ff = Ff.Exe ?? throw new Exception("ffmpeg não encontrado");
                var args = ext == "apng"
                    ? new[] { "-hide_banner", "-nostdin", "-y", "-i", src, "-plays", "0", "-f", "apng", dst }
                    : new[] { "-hide_banner", "-nostdin", "-y", "-i", src, dst };
                Proc.Run(ff, args).GetAwaiter().GetResult();
            }
            else
            {
                using var image = Read(src);
                if (ext == "svg") Trace(image, dst);
                else if (ext == "icns") WriteIcns(image, dst);
                else if (common.Contains(ext)) Images.Save(image, dst, ext);
                else
                {
                    image.Format = ext switch
                    {
                        "jxl" => MagickFormat.Jxl,
                        "jp2" => MagickFormat.Jp2,
                        "psd" => MagickFormat.Psd,
                        "tga" => MagickFormat.Tga,
                        "dds" => MagickFormat.Dds,
                        "qoi" => MagickFormat.Qoi,
                        "pcx" => MagickFormat.Pcx,
                        _ => throw new Exception("Formato de saída não suportado"),
                    };
                    image.Write(dst);
                }
            }
            if (!File.Exists(dst) || new FileInfo(dst).Length == 0) throw new Exception("O arquivo de saída ficou vazio");
            Job.Progress(1);
            return dst;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Paths.TryDelete(dst);
            throw new Exception("Não consegui converter a imagem: " + ex.Message, ex);
        }
    }

    static MagickImage Read(string src)
    {
        var ext = Modules.Ext(src);
        if (ext == "jxr" || ext == "wdp" || ext == "hdp")
        {
            using var stream = File.OpenRead(src);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) throw new Exception("JPEG XR sem imagem");
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(decoder.Frames[0]));
            using var bytes = new MemoryStream();
            png.Save(bytes);
            return new MagickImage(bytes.ToArray());
        }
        if (ext == "icns")
        {
            var candidates = IcnsFile.Candidates(IcnsFile.Parse(File.ReadAllBytes(src)));
            foreach (var c in candidates)
            {
                try
                {
                    if (c.Encoded != null) return new MagickImage(c.Encoded);
                    var bitmap = c.Decode!();
                    var result = new MagickImage();
                    result.ReadPixels(bitmap.Rgba, new PixelReadSettings((uint)bitmap.Width, (uint)bitmap.Height, StorageType.Char, PixelMapping.RGBA));
                    return result;
                }
                catch (MagickException) { }
            }
            throw new Exception("ICNS sem imagem legível");
        }
        if (ext == "dcm")
        {
            using var dicom = Dicom.TryRead(src);
            if (dicom != null && dicom.Count > 0) return (MagickImage)dicom[0].Clone();
        }
        if (ext == "svgz")
        {
            using var file = File.OpenRead(src);
            using var gz = new GZipStream(file, CompressionMode.Decompress);
            using var bytes = new MemoryStream();
            gz.CopyTo(bytes);
            return new MagickImage(bytes.ToArray(), new MagickReadSettings { Format = MagickFormat.Svg, Density = new Density(192) });
        }
        if (ext == "emf" || ext == "wmf")
        {
            using var vector = System.Drawing.Image.FromFile(src);
            using var bitmap = new System.Drawing.Bitmap(vector.Width, vector.Height);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) graphics.DrawImage(vector, 0, 0, bitmap.Width, bitmap.Height);
            using var bytes = new MemoryStream();
            bitmap.Save(bytes, System.Drawing.Imaging.ImageFormat.Png);
            return new MagickImage(bytes.ToArray());
        }
        return Images.Load(src);
    }

    static void WriteIcns(MagickImage image, string dst)
    {
        var entries = new List<IcnsEntry>();
        foreach (var (type, size) in IcnsFile.WrittenTypes)
        {
            Job.Check();
            using var icon = (MagickImage)image.Clone();
            icon.Resize(new MagickGeometry((uint)size, (uint)size));
            using var square = new MagickImage(MagickColors.None, (uint)size, (uint)size);
            square.Composite(icon, Gravity.Center, CompositeOperator.Over);
            entries.Add(new IcnsEntry(type, square.ToByteArray(MagickFormat.Png32)));
        }
        File.WriteAllBytes(dst, IcnsFile.Write(entries));
    }

    static void Trace(MagickImage image, string dst)
    {
        int width = (int)image.Width, height = (int)image.Height;
        if (width == 0 || height == 0) throw new Exception("Imagem vazia");
        double scale = Math.Min(1, Math.Min(4096.0 / Math.Max(width, height), Math.Sqrt(6_000_000.0 / ((double)width * height))));
        if (scale < 1) image.Resize(new Percentage(scale * 100));
        image.ColorSpace = ColorSpace.sRGB;
        using var pixels = image.GetPixels();
        var rgba = pixels.ToByteArray(PixelMapping.RGBA) ?? throw new Exception("Não consegui ler os pixels");
        var traced = ImageTracer.Trace(rgba, (int)image.Width, (int)image.Height, new TraceSettings(), null, Job.Token);
        using var output = File.Create(dst);
        ImageTracer.WriteSvg(traced, output, width, height);
    }
}
