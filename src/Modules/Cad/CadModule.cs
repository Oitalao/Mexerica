using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Mexerica.Cad;
using SkiaSharp;

namespace Mexerica;

sealed class CadModule : Module
{
    public override string Name => "CAD";
    public override IEnumerable<string> Inputs => ["dwg", "dxf", "svg"];

    public override IEnumerable<Target> TargetsFor(string inExt) => inExt switch
    {
        "svg" => [new("dxf", "desenho", "DXF"), new("dwg", "desenho", "DWG")],
        "dwg" => DrawingTargets("dxf"),
        "dxf" => DrawingTargets("dwg"),
        _ => [],
    };

    static IEnumerable<Target> DrawingTargets(string interchange) =>
    [new(interchange, "desenho", interchange.ToUpperInvariant()),
     new("pdf", "vetorial", "PDF"), new("svg", "vetorial", "SVG"),
     new("png", "imagem", "PNG"), new("jpg", "imagem", "JPG"),
     new("webp", "imagem", "WEBP")];

    public override Task<string?> Convert(string src, string outExt, P p) => Task.Run<string?>(() =>
    {
        Job.Check();
        var input = Modules.Ext(src);
        if (input == "svg") return FromSvg(src, outExt);
        if (input is not ("dwg" or "dxf") || outExt is not ("dwg" or "dxf" or "pdf" or "svg" or "png" or "jpg" or "webp"))
            throw new Exception("Conversão CAD não suportada.");

        var drawing = CadFile.Read(src, input);
        Job.Check();
        Job.Progress(0.4);
        var dst = Paths.Out(src, outExt);
        try
        {
            if (outExt is "dwg" or "dxf") CadFile.Write(drawing, dst, outExt);
            else
            {
                var scene = CadSceneBuilder.Build(drawing, Job.Token);
                if (scene.Items.Count == 0) throw new Exception("O desenho não tem objetos visíveis que possam ser renderizados.");
                Job.Check();
                Job.Progress(0.7);
                if (outExt == "pdf")
                {
                    using var stream = File.Create(dst);
                    CadRenderer.WritePdf(scene, Path.GetFileNameWithoutExtension(src), stream);
                }
                else if (outExt == "svg")
                {
                    using var stream = File.Create(dst);
                    CadRenderer.WriteSvg(scene, stream);
                }
                else
                {
                    using var bitmap = CadRenderer.RenderBitmap(scene, 150);
                    using var image = SKImage.FromBitmap(bitmap);
                    using var data = image.Encode(outExt == "png" ? SKEncodedImageFormat.Png : outExt == "jpg" ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Webp, 90);
                    using var stream = File.Create(dst);
                    data.SaveTo(stream);
                }
            }
            Job.Progress(1);
            return dst;
        }
        catch { Paths.TryDelete(dst); throw; }
    }, Job.Token);

    static string FromSvg(string src, string outExt)
    {
        if (outExt is not ("dxf" or "dwg")) throw new Exception("Conversão SVG para CAD não suportada.");
        var dst = Paths.Out(src, outExt);
        string? intermediate = null;
        try
        {
            using var svg = SvgRenderer.Load(src);
            Job.Progress(0.3);
            if (outExt == "dxf") SvgDxfWriter.Write(svg, dst, Job.Token);
            else
            {
                intermediate = Path.Combine(Paths.Temp(), "desenho.dxf");
                SvgDxfWriter.Write(svg, intermediate, Job.Token);
                Job.Check();
                Job.Progress(0.7);
                CadFile.Write(CadFile.Read(intermediate, "dxf"), dst, "dwg");
            }
            Job.Progress(1);
            return dst;
        }
        catch { Paths.TryDelete(dst); throw; }
        finally { if (intermediate != null) Paths.TryDelete(Path.GetDirectoryName(intermediate)!); }
    }
}
