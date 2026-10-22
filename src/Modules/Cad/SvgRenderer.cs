// Adaptado do Filee (MIT, © 2026 Myeonggu Kim e colaboradores). Origem: Filee.Engines/Vector/SvgRenderer.cs
// Loads SVG/SVGZ with Svg.Skia (MIT) and draws it with SkiaSharp: onto PDF pages as real vector graphics, or onto
// a bitmap for raster targets.

using System.IO.Compression;


using SkiaSharp;
using Svg;
using Svg.Model;
using Svg.Skia;

using System.IO;
using System.Threading;
namespace Mexerica.Cad;

/// <summary>SVG → PDF and SVG → bitmap.</summary>
internal static class SvgRenderer
{
    /// <summary>CSS pixels per inch: SVG user units are CSS pixels.</summary>
    public const float CssDpi = 96f;

    /// <summary>Small drawings (icons) are rendered at least this large on the long edge.</summary>
    public const int MinimumLongEdge = 1024;

    /// <summary>Upper bounds for rendered bitmaps, so a huge drawing doesn't exhaust memory.</summary>
    private const int MaximumLongEdge = 16384;
    private const long MaximumPixels = 100_000_000;

    private static readonly SvgParameters SafeParameters = new(
        null, null, null,
        new SvgDocumentLoadOptions { ExternalResources = SvgExternalResourcePolicy.SameOrigin });

    static SvgRenderer()
    {
        // An SVG may reference images, other SVGs, fonts and XML entities by URL. SafeParameters (Svg.Skia's SameOrigin
        // policy) keeps them inside the drawing's folder and its subfolders: ../, absolute and UNC paths and web URLs
        // are rejected, also for <use>, nested SVG images and @font-face, which ExternalType.Local alone didn't stop.
        // Converting a file must not read unrelated local files or fetch anything (tracking pixels, slow servers).
        SvgDocument.ResolveExternalImages = ExternalType.Local;
        SvgDocument.ResolveExternalElements = ExternalType.Local;
        SvgDocument.ResolveExternalXmlEntites = ExternalType.None;
    }

    /// <summary>A loaded drawing. Dispose it after drawing.</summary>
    public sealed class Drawing(SKSvg svg, SKPicture picture, SKRect bounds) : IDisposable
    {
        /// <summary>The loaded document (its model is replayed for PDF output).</summary>
        public SKSvg Svg { get; } = svg;

        /// <summary>The drawing as a Skia picture, in CSS pixels.</summary>
        public SKPicture Picture { get; } = picture;

        /// <summary>The SVG viewport (width/height or viewBox) in CSS pixels.</summary>
        public SKRect Bounds { get; } = bounds;

        public void Dispose() => Svg.Dispose();
    }

    /// <summary>Loads an SVG or SVGZ (gzip) file.</summary>
    /// <exception cref="InvalidDataException">The file is not a drawable SVG.</exception>
    public static Drawing Load(string path)
    {
        var bytes = ReadSvgBytes(path);
        var svg = new SKSvg();
        svg.Settings.EnableJavaScript = false;
        svg.Settings.EnableExternalJavaScript = false;
        SKPicture? picture;
        var uri = new Uri(Path.GetFullPath(path));
        try
        {
            picture = Parse(svg, bytes, uri);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && SvgXmlRepair.FixAmpersands(bytes) is { } repaired)
        {
            // "A & B" or &nbsp; in the text (common in generated SVGs): read it again with those escaped.
            try
            {
                picture = Parse(svg, repaired, uri);
            }
            catch (Exception retry) when (retry is not OutOfMemoryException)
            {
                svg.Dispose();
                throw new InvalidDataException("Não foi possível ler o arquivo SVG.", ex);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            svg.Dispose();
            throw new InvalidDataException("Não foi possível ler o arquivo SVG.", ex);
        }
        var bounds = picture?.CullRect ?? SKRect.Empty;
        if (picture is null || bounds.Width <= 0 || bounds.Height <= 0 || float.IsInfinity(bounds.Width) || float.IsInfinity(bounds.Height))
        {
            svg.Dispose();
            throw new InvalidDataException("O SVG não tem conteúdo desenhável ou dimensões válidas.");
        }
        return new Drawing(svg, picture, bounds);
    }

    private static SKPicture? Parse(SKSvg svg, byte[] bytes, Uri baseUri)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return svg.Load(stream, SafeParameters, baseUri);
    }

    /// <summary>The file's SVG text as bytes: SVGZ (or a gzipped .svg) is decompressed.</summary>
    public static byte[] ReadSvgBytes(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (!IsGzip(bytes))
            return bytes;
        using var gzip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
        using var plain = new MemoryStream();
        gzip.CopyTo(plain);
        return plain.ToArray();
    }

    /// <summary>True when the data starts with the gzip magic number.</summary>
    public static bool IsGzip(ReadOnlySpan<byte> data) => data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B;}

