// Adaptado do Filee (MIT, © 2026 Myeonggu Kim e colaboradores). Origem: Filee.Engines/Fonts/FontConverter.cs
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Threading.Tasks;
using Filee.Engines.Fonts;

namespace Mexerica;

sealed class FontModule : Module
{
    public override string Name => "Fontes";
    public override IEnumerable<string> Inputs => FontFile.Formats;
    public override IEnumerable<Target> TargetsFor(string inExt) => FontFile.Formats
        .Where(f => f != inExt).Select(f => new Target(f, "fonte", f.ToUpperInvariant()));

    public override Task<string?> Convert(string src, string outExt, P p) => Task.Run<string?>(() =>
    {
        Job.Check();
        var font = FontFile.Load(File.ReadAllBytes(src), Job.Token);
        Job.Progress(.2);
        var bytes = FontFile.Save(font, outExt, Job.Token);
        Job.Check();
        FontFile.Load(bytes, Job.Token); // Confere o contêiner antes de entregar.
        var dst = Paths.Out(src, outExt);
        try { File.WriteAllBytes(dst, bytes); Job.Progress(1); return dst; }
        catch { Paths.TryDelete(dst); throw; }
    }, Job.Token);

    public override IEnumerable<Op> Tools(string[] files)
    {
        if (files.Length == 0 || files.Any(f => !FontFile.Formats.Contains(Modules.Ext(f)))) return [];
        return [new Op { Label = "Amostra", Sub = "fonte em PNG", One = (src, _) => Task.Run<string?>(() => Sample(src), Job.Token) }];
    }

    static string Sample(string src)
    {
        Job.Check();
        var font = FontFile.Load(File.ReadAllBytes(src), Job.Token);
        var temp = Paths.Temp();
        try
        {
            var ttf = Path.Combine(temp, "amostra.ttf");
            File.WriteAllBytes(ttf, FontFile.Save(font, "ttf", Job.Token));
            using var fonts = new PrivateFontCollection();
            fonts.AddFontFile(ttf);
            if (fonts.Families.Length == 0) throw new Exception("Não consegui abrir esta fonte para gerar a amostra.");
            using var image = new Bitmap(1400, 420);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(Color.White);
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var title = new Font(fonts.Families[0], 48, FontStyle.Regular, GraphicsUnit.Pixel);
            using var body = new Font(fonts.Families[0], 38, FontStyle.Regular, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Color.FromArgb(35, 35, 35));
            graphics.DrawString(fonts.Families[0].Name, title, brush, 40, 35);
            graphics.DrawString("ABCDEFGHIJKLMNOPQRSTUVWXYZ", body, brush, 40, 140);
            graphics.DrawString("abcdefghijklmnopqrstuvwxyz", body, brush, 40, 210);
            graphics.DrawString("ÁÀÂÃÉÊÍÓÔÕÚÜÇ áàâãéêíóôõúüç 0123456789", body, brush, 40, 280);
            var dst = Paths.Out(src, "png", "amostra");
            try { image.Save(dst, ImageFormat.Png); Job.Progress(1); return dst; }
            catch { Paths.TryDelete(dst); throw; }
        }
        finally { Paths.TryDelete(temp); }
    }
}
