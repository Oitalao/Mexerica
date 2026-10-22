using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Mexerica;

sealed class PostScriptFormats : Module
{
    public override string Name => "PostScript";
    public override IEnumerable<string> Inputs => new[] { "eps", "ps", "ai", "pdf" };
    public override bool Available => Gs.Ok;
    public override IEnumerable<Target> TargetsFor(string inExt) => inExt == "pdf"
        ? new[] { new Target("eps", "vetor"), new Target("ps", "vetor") }
        : new[] { new Target("pdf", "documento"), new Target("png", "imagem") };

    public override async Task<string?> Convert(string src, string outExt, P p)
    {
        var dst = Paths.Out(src, outExt);
        var pages = outExt == "eps" || outExt == "png" && Modules.Ext(src) is "ps" or "ai";
        var temp = pages ? Paths.Temp() : null;
        var target = temp == null ? dst : Path.Combine(temp, "pagina-%d." + outExt);
        var device = outExt switch { "pdf" => "pdfwrite", "png" => "pngalpha", "eps" => "eps2write", "ps" => "ps2write", _ => throw new Exception("Destino PostScript não suportado") };
        try
        {
            await Gs.Run(new[] { "-dSAFER", "-dNOPAUSE", "-dBATCH", "-sDEVICE=" + device, "-r150", "-sOutputFile=" + target, src });
            if (temp != null)
            {
                var files = Directory.GetFiles(temp, "pagina-*." + outExt);
                if (files.Length == 0) throw new Exception("O Ghostscript não gerou páginas");
                if (files.Length == 1) File.Move(files[0], dst);
                else
                {
                    var folder = Paths.OutDir(src, "páginas " + outExt.ToUpperInvariant());
                    Directory.CreateDirectory(folder);
                    foreach (var file in files) File.Move(file, Path.Combine(folder, Path.GetFileName(file)));
                    Job.Progress(1);
                    return folder;
                }
            }
            if (!File.Exists(dst) || new FileInfo(dst).Length == 0) throw new Exception("O Ghostscript não gerou arquivo");
            Job.Progress(1);
            return dst;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Paths.TryDelete(dst);
            throw new Exception("Não consegui converter PostScript: " + ex.Message, ex);
        }
        finally { if (temp != null) Paths.TryDelete(temp); }
    }
}
