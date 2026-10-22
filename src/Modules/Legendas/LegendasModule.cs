// Adaptado do Filee (MIT, © 2026 Myeonggu Kim e colaboradores). Origem: Filee.Engines/Subtitles/SubtitleConverter.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Filee.Engines.Subtitles;

namespace Mexerica;

sealed class LegendasModule : Module
{
    static readonly string[] Sources = ["srt", "ass", "ssa", "vtt", "smi", "sami", "lrc", "ttml", "dfxp", "sub", "scc"];
    static readonly string[] Destinations = ["srt", "ass", "vtt", "smi", "lrc", "ttml", "sub", "txt"];
    public override string Name => "Legendas";
    public override IEnumerable<string> Inputs => Sources;
    public override Kind KindHint => Kind.Subtitle;
    public override bool Overrides(string inExt, string outExt) => inExt is not ("srt" or "vtt") && outExt is "srt" or "vtt" or "txt";
    public override IEnumerable<Target> TargetsFor(string inExt) => Destinations
        .Where(e => e != inExt && !(inExt is "srt" or "vtt" && e is "srt" or "vtt" or "txt"))
        .Select(e => new Target(e, e == "txt" ? "texto" : "legenda", e.ToUpperInvariant()));

    public override Task<string?> Convert(string src, string outExt, P p) => Task.Run<string?>(() =>
    {
        try
        {
        Job.Check();
        var document = Read(src);
        Job.Progress(0.5);
        Job.Check();
        var dst = Paths.Out(src, outExt);
        if (outExt == "txt")
            File.WriteAllText(dst, string.Join("\r\n", document.Cues.Select(c => SubtitleMarkup.PlainText(c.Text))) + "\r\n", new UTF8Encoding(false));
        else File.WriteAllBytes(dst, SubtitleWriter.Write(document, outExt));
        Job.Progress(1);
        return dst;
        }
        catch (InvalidDataException ex) when (ex.Message.Contains("VobSub"))
        { throw new Exception("Este SUB contém imagens de DVD; só legendas MicroDVD em texto podem ser convertidas."); }
        catch (InvalidDataException)
        { throw new Exception("Arquivo de legenda inválido ou sem trechos reconhecíveis."); }
    }, Job.Token);

    public override IEnumerable<Op> Tools(string[] files)
    {
        if (files.Length == 0 || files.Any(f => !Sources.Contains(Modules.Ext(f)))) return [];
        return [new Op
        {
            Label = "Ajustar tempo", Sub = "legenda",
            Ask = fs => new Form("Ajustar tempo", fs, "Use valor negativo para adiantar a legenda.")
                .Number("segundos", "Deslocamento", 0, -86400, 86400, "segundos", decimals: true).Run(),
            One = (src, p) => Task.Run<string?>(() => Shift(src, p), Job.Token),
        }];
    }

    static SubtitleDocument Read(string src)
    {
        var format = Modules.Ext(src) switch { "sami" => "smi", "dfxp" => "ttml", var e => e };
        return SubtitleReader.Read(format, File.ReadAllBytes(src));
    }

    static string Shift(string src, P p)
    {
        if (!double.TryParse(p.S("segundos").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            || !double.IsFinite(seconds) || Math.Abs(seconds) > 86400)
            throw new Exception("Informe um deslocamento válido entre -86400 e 86400 segundos.");
        var document = Read(src);
        var offset = TimeSpan.FromSeconds(seconds);
        for (var i = 0; i < document.Cues.Count; i++)
        {
            var cue = document.Cues[i];
            document.Cues[i] = cue with { Start = MaxZero(cue.Start + offset), End = MaxZero(cue.End + offset) };
        }
        document.Cues.RemoveAll(c => c.End <= c.Start);
        if (document.Cues.Count == 0) throw new Exception("O ajuste deixou todas as legendas antes do início.");
        Job.Check();
        var format = Modules.Ext(src) switch { "sami" => "smi", "dfxp" => "ttml", "ssa" => "ass", "scc" => "srt", var e => e };
        var dst = Paths.Out(src, format, "tempo ajustado");
        File.WriteAllBytes(dst, SubtitleWriter.Write(document, format));
        Job.Progress(1);
        return dst;
    }

    static TimeSpan MaxZero(TimeSpan time) => time < TimeSpan.Zero ? TimeSpan.Zero : time;
}
