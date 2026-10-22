using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Mexerica;

sealed class ArchiveModule : Module
{
    static readonly string[] Added = ["iso", "cab", "dmg", "wim", "arj", "lzh", "lha", "cpio", "rpm", "deb", "zst", "lz", "lzma", "z", "tar.zst", "tar.lz"];
    static readonly string[] Existing = ["zip", "tar", "gz", "tgz", "rar", "7z", "bz2", "xz", "tbz2", "txz", "tar.gz", "tar.xz", "tar.bz2"];
    static readonly string[] Destinations = ["zip", "7z", "tar", "tar.gz", "tar.xz", "tar.bz2"];
    static string Exe => Path.Combine(AppContext.BaseDirectory, "tools", "7zip", "7z.exe");
    static string Lzip => Path.Combine(AppContext.BaseDirectory, "tools", "lzip", "lzip.exe");
    public override string Name => "Compactados";
    public override bool Available => File.Exists(Exe) && File.Exists(Path.Combine(Path.GetDirectoryName(Exe)!, "7z.dll")) && File.Exists(Lzip);
    public override IEnumerable<string> Inputs => Added.Concat(Existing);
    // Os formatos novos usam o próprio botão Extrair; os antigos preservam o botão existente.
    public override Kind KindHint => Kind.Other;

    public override IEnumerable<Target> TargetsFor(string inExt) => (Added.Contains(inExt) ? Destinations : new[] { "7z", "tar.xz", "tar.bz2" })
        .Where(d => d != inExt && !(inExt is "txz" && d == "tar.xz") && !(inExt is "tbz2" && d == "tar.bz2"))
        .Select(d => new Target(d, "compactado", d.ToUpperInvariant()));

    public override Task<string?> Convert(string src, string outExt, P p) => ConvertArchive(src, outExt);

    public override IEnumerable<Op> Tools(string[] files)
    {
        if (files.Length == 0 || files.Any(f => !Inputs.Contains(Modules.Ext(f)))) return [];
        var ops = new List<Op>();
        if (files.All(f => Added.Contains(Modules.Ext(f))))
            ops.Add(new Op { Label = "Extrair", Sub = "em pasta", One = (src, _) => Extract(src) });
        ops.Add(new Op { Label = "Testar", Sub = "integridade", One = (src, _) => Test(src),
            Show = result => System.Windows.MessageBox.Show(result, "Teste do compactado", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information) });
        return ops;
    }

    static async Task<string?> Test(string src)
    {
        try
        {
            if (Modules.Ext(src) is "lz" or "tar.lz") await Proc.Run(Lzip, ["-t", src]);
            else await Proc.Run(Exe, ["t", "-y", "--", src]);
        }
        catch (OperationCanceledException) { throw; }
        catch { throw new Exception("O compactado está corrompido ou não pôde ser lido."); }
        Job.Progress(1);
        return "Íntegro: " + Path.GetFileName(src);
    }

    static async Task<string?> Extract(string src)
    {
        var dst = Paths.OutDir(src);
        try
        {
            Directory.CreateDirectory(dst);
            await Unpack(src, dst);
            Job.Check();
            Job.Progress(1);
            return dst;
        }
        catch { Paths.TryDelete(dst); throw; }
    }

    static async Task<string?> ConvertArchive(string src, string ext)
    {
        var stage = Paths.Temp();
        string? dst = null;
        try
        {
            await Unpack(src, stage);
            Job.Progress(.4);
            Job.Check();
            dst = Paths.Out(src, ext);
            var type = ext == "7z" ? "7z" : ext == "zip" ? "zip" : "tar";
            if (type != "tar")
                await Proc.Run(Exe, ["a", "-t" + type, "-y", dst, ".\\*"], cwd: stage);
            else
            {
                var tar = ext == "tar" ? dst : Path.Combine(stage, "conteudo.tar");
                await Proc.Run(Exe, ["a", "-ttar", "-y", tar, ".\\*"], cwd: stage);
                if (ext != "tar")
                {
                    var codec = ext[4..] switch { "gz" => "gzip", "xz" => "xz", "bz2" => "bzip2", _ => throw new Exception("Destino compactado desconhecido.") };
                    await Proc.Run(Exe, ["a", "-t" + codec, "-y", dst, tar]);
                }
            }
            Job.Progress(1);
            return dst;
        }
        catch { if (dst != null) Paths.TryDelete(dst); throw; }
        finally { Paths.TryDelete(stage); }
    }

    static async Task Unpack(string src, string stage)
    {
        try
        {
            var ext = Modules.Ext(src);
            if (ext is "lz" or "tar.lz")
            {
                var output = Path.Combine(stage, Path.GetFileNameWithoutExtension(src));
                await Proc.Run(Lzip, ["-d", "-o", output, src]);
            }
            else await Proc.Run(Exe, ["x", "-y", "-o" + stage, "--", src]);
            // Compressores de um arquivo só (.tar.Z, .tar.lzma, .gz…) entregam um .tar: abre também essa camada.
            if (ext is "tar.zst" or "tar.lz" or "tar.xz" or "tar.bz2" or "tar.gz" or "tgz" or "txz" or "tbz2" or "deb" or "rpm" or "z" or "lzma" or "zst" or "lz" or "gz" or "xz" or "bz2")
            {
                for (var layer = 0; layer < 3; layer++)
                {
                    var entries = Directory.GetFileSystemEntries(stage);
                    if (entries.Length != 1 || !File.Exists(entries[0]) || Path.GetExtension(entries[0]).ToLowerInvariant() is not (".tar" or ".cpio")) break;
                    await Proc.Run(Exe, ["x", "-y", "-o" + stage, "--", entries[0]]);
                    File.Delete(entries[0]);
                }
            }
            if (!Directory.EnumerateFileSystemEntries(stage).Any()) throw new Exception("O compactado não contém arquivos.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex.Message == "O compactado não contém arquivos.") { throw; }
        catch { throw new Exception("Não consegui abrir este compactado: ele está danificado ou protegido."); }
    }
}
