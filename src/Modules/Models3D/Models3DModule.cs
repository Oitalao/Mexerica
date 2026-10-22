using System.IO;
using System.Threading.Tasks;
using Filee.Engines.Models3D;
using ImageMagick;

namespace Mexerica;

sealed class Models3DModule : Module
{
    static readonly string[] Formats = ["glb", "gltf", "stl", "obj", "3mf", "ply"];
    public override string Name => "Modelos 3D";
    public override IEnumerable<string> Inputs => [.. Formats, "fbx"];
    public override IEnumerable<Target> TargetsFor(string inExt) =>
        [.. Formats.Where(e => e != inExt).Select(e => new Target(e, "modelo", e.ToUpperInvariant())),
         new Target("png", "prévia", "PNG"), new Target("jpg", "prévia", "JPG")];
    public override Task<string?> Convert(string src, string outExt, P p) => Task.Run<string?>(() =>
    {
        try
        {
        Job.Check();
        var model = Modules.Ext(src) switch
        {
            "glb" or "gltf" => GltfFormat.Read(src, null, Job.Token),
            "stl" => StlFormat.Read(src, null, Job.Token),
            "obj" => ObjFormat.Read(src, null, Job.Token),
            "3mf" => ThreeMfFormat.Read(src, null, Job.Token),
            "ply" => PlyFormat.Read(src, null, Job.Token),
            "fbx" => FbxFormat.Read(src, null, Job.Token),
            _ => throw new Exception("Formato de modelo 3D não reconhecido."),
        };
        model.Validate();
        Job.Progress(0.6);
        Job.Check();
        string dst;
        var written = new List<string>();
        try
        {
        if (outExt == "obj")
        {
            dst = Paths.Out(src, "obj");
            while (model.Materials.Count > 0 && File.Exists(Path.ChangeExtension(dst, "mtl"))) dst = Paths.Out(src, "obj");
            var mtl = model.Materials.Count > 0 ? Path.ChangeExtension(dst, "mtl") : null;
            written.Add(dst);
            if (mtl is not null) written.Add(mtl);
            var textures = new Dictionary<Texture3D, string>();
            foreach (var texture in model.Materials.Select(m => m.BaseColorTexture).OfType<Texture3D>().Distinct())
            {
                var name = Path.GetFileNameWithoutExtension(dst) + "_" + Path.GetFileNameWithoutExtension(texture.Name);
                var file = Paths.Out(src, texture.Extension.Length > 0 ? texture.Extension : "png", name);
                written.Add(file);
                File.WriteAllBytes(file, texture.Data);
                textures[texture] = file;
            }
            ObjFormat.Write(model, dst, mtl, textures, Job.Token);
        }
        else
        {
            dst = Paths.Out(src, outExt);
            written.Add(dst);
            if (outExt is "png" or "jpg")
            {
                using var image = new MagickImage(MeshRenderer.RenderPng(model, 1024, Job.Token));
                if (outExt == "jpg") { image.BackgroundColor = MagickColors.White; image.Alpha(AlphaAction.Remove); }
                image.Write(dst, outExt == "png" ? MagickFormat.Png : MagickFormat.Jpeg);
            }
            else
            {
                using var stream = File.Create(dst);
                switch (outExt)
                {
                    case "glb" or "gltf": GltfFormat.Write(model, stream, outExt == "glb", Job.Token); break;
                    case "stl": StlFormat.Write(model, stream, Job.Token); break;
                    case "3mf": ThreeMfFormat.Write(model, stream, Job.Token); break;
                    case "ply": PlyFormat.Write(model, stream, Job.Token); break;
                    default: throw new Exception("Destino de modelo 3D não reconhecido.");
                }
            }
        }
        }
        catch
        {
            foreach (var file in written) Paths.TryDelete(file);
            throw;
        }
        Job.Progress(1);
        return dst;
        }
        catch (InvalidDataException) { throw new Exception("O modelo 3D está inválido ou usa recursos não suportados."); }
    }, Job.Token);
}
