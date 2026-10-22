// Adaptado de Filee.Engines/Media/MotionPhoto.cs, LivePhotoSource.cs e AppleLivePhoto.cs.
// Adaptado do Filee (MIT, © 2026 Myeonggu Kim e colaboradores).
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using ImageMagick;

namespace Mexerica;

sealed class LiveMotionPhoto : Module
{
    const long MaxVideo = 512L * 1024 * 1024;
    public override string Name => "Live e Motion Photo";
    public override IEnumerable<string> Inputs => ["livp", "heic", "heif", "jpg", "jpeg"];
    public override Kind KindHint => Kind.Image;
    public override IEnumerable<Target> TargetsFor(string inExt) { yield return new("mp4", "vídeo", "Vídeo da foto"); }

    // Só oferece para fotos que de fato trazem vídeo (Motion Photo, par HEIC+MOV ou LIVP).
    public override IEnumerable<Target> TargetsFor(string inExt, string path)
    {
        bool has;
        try { has = inExt == "livp" || FindEmbedded(path) != null || PairedMovie(path) != null; }
        catch { has = false; }
        return has ? TargetsFor(inExt) : Array.Empty<Target>();
    }

    public override async Task<string?> Convert(string src, string outExt, P p)
    {
        if (outExt != "mp4") throw new Exception("Destino desconhecido");
        var dst = Paths.Out(src, "mp4", "vídeo");
        try
        {
            if (Modules.Ext(src) == "livp")
            {
                using var zip = ZipFile.OpenRead(src);
                if (zip.Entries.Count > 16) throw new Exception("O LIVP contém arquivos demais");
                var photos = zip.Entries.Where(e => e.Name.Length > 0 && !e.Name.StartsWith("._") && !e.FullName.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase) && (e.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".heic", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".heif", StringComparison.OrdinalIgnoreCase))).ToList();
                var movies = zip.Entries.Where(e => e.Name.Length > 0 && !e.Name.StartsWith("._") && !e.FullName.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase) && (e.Name.EndsWith(".mov", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))).ToList();
                if (photos.Count != 1 || photos[0].Length is <= 0 or > 64L * 1024 * 1024 || movies.Count != 1 || movies[0].Length is <= 0 or > MaxVideo)
                    throw new Exception("O LIVP precisa conter uma foto e um vídeo válidos");
                var tmp = Paths.Temp();
                try
                {
                    var movie = Path.Combine(tmp, "live" + Path.GetExtension(movies[0].Name));
                    await using (var input = movies[0].Open()) await using (var output = File.Create(movie)) await input.CopyToAsync(output);
                    await Remux(movie, dst);
                }
                finally { Paths.TryDelete(tmp); }
            }
            else if (FindEmbedded(src) is { } video)
            {
                var tmp = Paths.Temp();
                try
                {
                    var movie = Path.Combine(tmp, "motion.mp4");
                    await using (var input = File.OpenRead(src))
                    await using (var output = File.Create(movie))
                    {
                        input.Position = video.offset;
                        await CopyRange(input, output, video.length);
                    }
                    await Remux(movie, dst);
                }
                finally { Paths.TryDelete(tmp); }
            }
            else if (PairedMovie(src) is { } companion) await Remux(companion, dst);
            else throw new Exception("Essa foto não contém vídeo Live/Motion Photo");
            return dst;
        }
        catch { Paths.TryDelete(dst); throw; }
    }

    static async Task CopyRange(Stream input, Stream output, long length)
    {
        var buffer = new byte[81920];
        while (length > 0)
        {
            Job.Check();
            int n = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(length, buffer.Length)));
            if (n == 0) throw new Exception("O vídeo embutido está incompleto");
            await output.WriteAsync(buffer.AsMemory(0, n));
            length -= n;
        }
    }

    static async Task Remux(string movie, string dst)
    {
        if (!Ff.HasVideo(movie)) throw new Exception("O vídeo da foto está inválido");
        await Ff.Run(["-i", movie, "-map", "0:v:0", "-map", "0:a?", "-c", "copy", "-movflags", "+faststart", dst], Ff.Duration(movie));
    }

    static (long offset, long length)? FindEmbedded(string src)
    {
        using var file = File.OpenRead(src);
        if (file.ReadByte() != 0xff || file.ReadByte() != 0xd8) return null;
        while (file.Position < Math.Min(file.Length, 8 * 1024 * 1024))
        {
            if (file.ReadByte() != 0xff) return null;
            int marker;
            do { marker = file.ReadByte(); } while (marker == 0xff);
            if (marker is 0xda or 0xd9 or -1) return null;
            Span<byte> size = stackalloc byte[2];
            file.ReadExactly(size);
            int len = BinaryPrimitives.ReadUInt16BigEndian(size) - 2;
            if (len < 0 || len > file.Length - file.Position) throw new Exception("Metadados JPEG inválidos");
            if (marker != 0xe1 || len > 65533) { file.Position += len; continue; }
            var data = new byte[len]; file.ReadExactly(data);
            if (!data.AsSpan().StartsWith("http://ns.adobe.com/xap/1.0/\0"u8)) continue;
            var length = VideoLength(data);
            if (length == null || length == 0) continue;
            var start = file.Length - length.Value;
            if (length < 16 || length > MaxVideo || start < file.Position || !ValidMovie(file, start, length.Value))
                throw new Exception("O vídeo declarado na Motion Photo está inválido");
            return (start, length.Value);
        }
        return null;
    }

    static long? VideoLength(byte[] data)
    {
        const string camera = "http://ns.google.com/photos/1.0/camera/";
        const string container = "http://ns.google.com/photos/1.0/container/";
        const string item = "http://ns.google.com/photos/1.0/container/item/";
        int start = "http://ns.adobe.com/xap/1.0/\0"u8.Length;
        int end = Math.Max(start, Array.LastIndexOf(data, (byte)'>') + 1);
        using var reader = XmlReader.Create(new MemoryStream(data, start, end - start), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65535 });
        var xml = XDocument.Load(reader);
        string? Prop(string name) => xml.Descendants().Attributes(XName.Get(name, camera)).FirstOrDefault()?.Value ?? xml.Descendants(XName.Get(name, camera)).FirstOrDefault()?.Value;
        if (Prop("MotionPhoto") is { } motion)
        {
            if (motion.Trim() != "1") return 0;
            var last = xml.Descendants(XName.Get("Item", container)).LastOrDefault();
            var semantic = last?.Attribute(XName.Get("Semantic", item))?.Value;
            var mime = last?.Attribute(XName.Get("Mime", item))?.Value;
            var length = last?.Attribute(XName.Get("Length", item))?.Value;
            if (semantic != "MotionPhoto" || mime is not ("video/mp4" or "video/quicktime") || !long.TryParse(length, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                throw new Exception("Diretório Motion Photo inválido");
            return n;
        }
        if (Prop("MicroVideo")?.Trim() == "1")
            return long.TryParse(Prop("MicroVideoOffset"), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : throw new Exception("Offset MicroVideo inválido");
        return null;
    }

    static bool ValidMovie(Stream file, long start, long length)
    {
        long at = start, end = start + length;
        bool header = false;
        Span<byte> box = stackalloc byte[16];
        while (end - at >= 8)
        {
            file.Position = at; file.ReadExactly(box[..8]);
            long size = BinaryPrimitives.ReadUInt32BigEndian(box);
            string type = Encoding.Latin1.GetString(box[4..8]);
            if (size == 1 && end - at >= 16) { file.ReadExactly(box[8..]); var n = BinaryPrimitives.ReadUInt64BigEndian(box[8..]); size = n > long.MaxValue ? -1 : (long)n; }
            else if (size == 0) size = end - at;
            if (size < 8 || size > end - at || (at == start && type != "ftyp")) return false;
            header |= type == "moov";
            at += size;
        }
        return header && at == end;
    }

    static string? PairedMovie(string src)
    {
        if (Modules.Ext(src) is not ("heic" or "heif" or "jpg" or "jpeg")) return null;
        var id = ImageIdentifier(src);
        if (id == null) return null;
        var movies = Directory.EnumerateFiles(Path.GetDirectoryName(Path.GetFullPath(src))!)
            .Where(p => Path.GetExtension(p).Equals(".mov", StringComparison.OrdinalIgnoreCase) && Path.GetFileNameWithoutExtension(p).Equals(Path.GetFileNameWithoutExtension(src), StringComparison.OrdinalIgnoreCase)).ToList();
        return movies.Count == 1 && string.Equals(id, MovieIdentifier(movies[0]), StringComparison.OrdinalIgnoreCase) ? movies[0] : null;
    }

    static string? ImageIdentifier(string src)
    {
        try
        {
            using var image = new MagickImage(); image.Ping(src);
            var data = image.GetExifProfile()?.GetValue(ExifTag.MakerNote)?.Value;
            if (data == null || data.Length < 30 || !data.AsSpan().StartsWith("Apple iOS\0"u8)) return null;
            bool little = data.AsSpan(12, 2).SequenceEqual("II"u8);
            if (!little && !data.AsSpan(12, 2).SequenceEqual("MM"u8)) return null;
            ushort U16(int at) => little ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at)) : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at));
            uint U32(int at) => little ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at)) : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at));
            int count = U16(14);
            if (16L + count * 12L > data.Length) return null;
            for (int i = 0; i < count; i++)
            {
                int at = 16 + i * 12;
                if (U16(at) != 17 || U16(at + 2) != 2) continue;
                uint len = U32(at + 4), offset = U32(at + 8);
                if (len < 36 || len > 128 || offset > data.Length || len > data.Length - offset) return null;
                var value = Encoding.ASCII.GetString(data, (int)offset, (int)len).TrimEnd('\0');
                return Guid.TryParse(value, out var guid) ? guid.ToString("D") : null;
            }
        }
        catch (MagickException) { }
        return null;
    }

    static string? MovieIdentifier(string src)
    {
        try
        {
            var json = Proc.RunOutput(Ff.Probe!, ["-v", "error", "-show_entries", "format_tags=com.apple.quicktime.content.identifier", "-of", "json", src]);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("format", out var format) && format.TryGetProperty("tags", out var tags))
                foreach (var tag in tags.EnumerateObject())
                    if (tag.Name.Equals("com.apple.quicktime.content.identifier", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(tag.Value.GetString(), out var id)) return id.ToString("D");
        }
        catch { }
        return null;
    }
}
