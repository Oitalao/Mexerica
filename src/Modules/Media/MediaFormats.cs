using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Mexerica;

sealed class AudioFormats : Module
{
    static readonly string[] NewInputs = ["ape", "wv", "tta", "dsf", "dff", "dts", "ac3", "eac3", "amr", "mka", "caf", "au", "mpc", "w64"];
    static readonly string[] OldInputs = ["mp3", "m4a", "wav", "flac", "ogg", "oga", "opus", "aif", "aiff", "wma", "aac"];
    static readonly string[] OldOutputs = ["mp3", "m4a", "wav", "flac", "ogg", "opus", "aiff", "wma"];
    static readonly (string key, string ext, string label, string codec)[] NewOutputs =
    [
        ("alac", "m4a", "ALAC", "alac"), ("wv", "wv", "WV", "wavpack"),
        ("tta", "tta", "TTA", "tta"), ("ac3", "ac3", "AC3", "ac3"),
        ("eac3", "eac3", "E-AC3", "eac3"), ("w64", "w64", "W64", "pcm_s24le"),
        ("aac", "aac", "AAC", "aac")
    ];

    public override string Name => "Áudio adicional";
    public override IEnumerable<string> Inputs => NewInputs.Concat(OldInputs);
    public override Kind KindHint => Kind.Audio;
    public override bool Available => Ff.Ok;
    public override IEnumerable<Target> TargetsFor(string inExt)
    {
        if (NewInputs.Contains(inExt))
            foreach (var ext in OldOutputs) yield return new(ext, "áudio");
        foreach (var (key, _, label, _) in NewOutputs)
            yield return new(key, key is "alac" or "wv" or "tta" or "w64" ? "sem perda" : "áudio", label);
    }

    public override async Task<string?> Convert(string src, string outExt, P p)
    {
        if (OldOutputs.Contains(outExt)) return await Ff.ToAudio(src, outExt, false);
        var format = NewOutputs.FirstOrDefault(x => x.key == outExt);
        if (format.key == null) throw new Exception("Destino de áudio desconhecido");
        if (!Ff.HasAudio(src)) throw new Exception("Esse arquivo não contém áudio");
        var dst = Paths.Out(src, format.ext, format.key == "alac" ? "ALAC" : null);
        try
        {
            var args = new List<string> { "-i", src, "-map", "0:a:0", "-vn", "-map_metadata", "0", "-c:a", format.codec };
            // WavPack e TTA recusam o primeiro bloco curto que alguns MP3/AAC entregam: blocos de tamanho fixo resolvem.
            if (format.key is "wv" or "tta") args.AddRange(["-af", "asetnsamples=n=4096:p=0"]);
            if (format.key is "ac3" or "eac3") args.AddRange(["-b:a", "640k"]);
            if (format.key == "aac") args.AddRange(["-b:a", $"{Config.Now.AacK}k"]);
            args.AddRange(Ff.AudioFit(src, format.key));
            args.Add(dst);
            await Ff.Run(args, Ff.Duration(src));
            return dst;
        }
        catch { Paths.TryDelete(dst); throw; }
    }
}

sealed class VideoFormats : Module
{
    static readonly string[] NewInputs = ["vob", "ogv", "dat", "mxf", "asf", "rm", "rmvb", "divx", "f4v"];
    static readonly string[] OldInputs = ["mp4", "mov", "mkv", "webm", "avi", "wmv", "m4v", "flv", "mpg", "mpeg", "3gp", "ts", "mts", "m2ts"];
    static readonly string[] OldOutputs = ["mp4", "mov", "mkv", "webm", "avi", "wmv", "gif", "mp3", "m4a"];
    public override string Name => "Vídeo adicional";
    public override IEnumerable<string> Inputs => NewInputs.Concat(OldInputs);
    public override Kind KindHint => Kind.Video;
    public override bool Available => Ff.Ok;

    public override IEnumerable<Target> TargetsFor(string inExt)
    {
        if (NewInputs.Contains(inExt))
            foreach (var ext in OldOutputs) yield return new(ext, ext is "mp3" or "m4a" ? "áudio" : "vídeo");
        yield return new("mp4-hevc", "vídeo", "MP4 HEVC");
        yield return new("mp4-av1", "vídeo", "MP4 AV1");
        yield return new("apng", "animação", "APNG");
        yield return new("webp", "animação", "WEBP animado");
        yield return new("720p", "vídeo", "720p");
        yield return new("srt", "legenda", "SRT");
        yield return new("ass", "legenda", "ASS");
    }

    // SRT/ASS só aparecem quando o vídeo tem legenda de texto embutida (as de imagem, como as de DVD e Blu-ray, não viram texto).
    public override IEnumerable<Target> TargetsFor(string inExt, string path)
    {
        bool textSubs;
        try { textSubs = Subtitles(path).Any(s => s.Codec is not ("hdmv_pgs_subtitle" or "dvd_subtitle" or "dvb_subtitle")); }
        catch { textSubs = false; }
        bool audio;
        try { audio = Ff.HasAudio(path); } catch { audio = true; }
        foreach (var t in TargetsFor(inExt))
            if ((textSubs || t.Ext is not ("srt" or "ass")) && (audio || t.Ext is not ("mp3" or "m4a"))) yield return t;
    }

    public override async Task<string?> Convert(string src, string outExt, P p)
    {
        if (OldOutputs.Contains(outExt))
            return outExt is "mp3" or "m4a" ? await Ff.ToAudio(src, outExt, true) : await Ff.ToVideo(src, outExt);
        if (outExt is "srt" or "ass") return await ExtractOneSubtitle(src, outExt);
        if (!Ff.HasVideo(src)) throw new Exception("Esse arquivo não contém vídeo");
        string ext = outExt is "mp4-hevc" or "mp4-av1" or "720p" ? "mp4" : outExt;
        var dst = Paths.Out(src, ext, outExt switch { "mp4-hevc" => "HEVC", "mp4-av1" => "AV1", "720p" => "720p", _ => null });
        var args = new List<string> { "-i", src, "-map", "0:v:0" };
        bool audio = Ff.HasAudio(src);
        if (audio && ext == "mp4") args.AddRange(["-map", "0:a:0"]);
        switch (outExt)
        {
            case "mp4-hevc":
                // O Hw atual só troca libx264; HEVC usa libx265 para preservar 10 bits na saída.
                args.AddRange(["-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2", "-c:v", "libx265", "-preset", "fast", "-crf", Config.Now.Crf.ToString(), "-pix_fmt", IsTenBit(src) ? "yuv420p10le" : "yuv420p", "-tag:v", "hvc1"]);
                break;
            case "mp4-av1":
                args.AddRange(["-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2", "-c:v", "libsvtav1", "-preset", "8", "-crf", Config.Now.Crf.ToString(), "-pix_fmt", IsTenBit(src) ? "yuv420p10le" : "yuv420p"]);
                break;
            case "720p":
                args.AddRange(["-vf", "scale=w='min(iw,1280)':h='min(ih,720)':force_original_aspect_ratio=decrease:force_divisible_by=2", "-c:v", "libx264", "-preset", "fast", "-crf", Config.Now.Crf.ToString(), "-pix_fmt", "yuv420p"]);
                break;
            case "apng": args.AddRange(["-an", "-plays", "0", "-f", "apng"]); break;
            case "webp": args.AddRange(["-an", "-c:v", "libwebp_anim", "-loop", "0", "-f", "webp"]); break;
            default: throw new Exception("Destino de vídeo desconhecido");
        }
        if (ext == "mp4") args.AddRange(audio ? ["-c:a", "aac", "-b:a", "160k"] : ["-an"]);
        args.Add(dst);
        try
        {
            if (outExt is "mp4-hevc" or "mp4-av1" && HardwareArgs(args, outExt) is { } hardware)
            {
                try { await Ff.Run(hardware, Ff.Duration(src), hw: false); return dst; }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Paths.TryDelete(dst);
                    Log.Write($"Codificação {outExt} pela placa falhou; tentando pelo processador: {ex.Message}");
                }
            }
            await Ff.Run(args, Ff.Duration(src));
            return dst;
        }
        catch { Paths.TryDelete(dst); throw; }
    }

    static List<string>? HardwareArgs(List<string> args, string output)
    {
        var encoder = Hw.Encoder switch
        {
            "h264_nvenc" => output == "mp4-hevc" ? "hevc_nvenc" : "av1_nvenc",
            "h264_qsv" => output == "mp4-hevc" ? "hevc_qsv" : "av1_qsv",
            "h264_amf" => output == "mp4-hevc" ? "hevc_amf" : "av1_amf",
            _ => null
        };
        if (encoder == null) return null;
        var fast = new List<string>(args);
        int codec = fast.IndexOf("-c:v");
        fast[codec + 1] = encoder;
        int preset = fast.IndexOf("-preset");
        fast.RemoveRange(preset, 2);
        int crf = fast.IndexOf("-crf");
        var quality = fast[crf + 1];
        fast.RemoveRange(crf, 2);
        fast.InsertRange(codec + 2, encoder.EndsWith("_nvenc") ? ["-preset", "p5", "-cq", quality, "-b:v", "0"] :
            encoder.EndsWith("_qsv") ? ["-global_quality", quality] : ["-quality", "quality", "-qp_i", quality, "-qp_p", quality]);
        return fast;
    }

    static bool IsTenBit(string src)
    {
        var result = Proc.RunOutput(Ff.Probe!, ["-v", "error", "-select_streams", "v:0", "-show_entries", "stream=pix_fmt", "-of", "default=noprint_wrappers=1:nokey=1", src]).Trim();
        return result.Contains("10le", StringComparison.OrdinalIgnoreCase) || result.Contains("p010", StringComparison.OrdinalIgnoreCase);
    }

    static List<Ff.Stream> Subtitles(string src) => Ff.Read(src).Streams.Where(s => s.Type == "subtitle").ToList();

    static async Task<string> ExtractOneSubtitle(string src, string ext)
    {
        var subs = Subtitles(src);
        if (subs.Count == 0) throw new Exception("Esse vídeo não contém legendas embutidas");
        if (subs[0].Codec is "hdmv_pgs_subtitle" or "dvd_subtitle" or "dvb_subtitle") throw new Exception("Essa legenda é imagem e não pode virar texto");
        var dst = Paths.Out(src, ext);
        try { await Ff.Run(["-i", src, "-map", $"0:{subs[0].Index}", "-c:s", ext == "srt" ? "srt" : "ass", dst], Ff.Duration(src)); return dst; }
        catch { Paths.TryDelete(dst); throw; }
    }

    public override IEnumerable<Op> Tools(string[] files)
    {
        if (files.Length == 0 || files.Any(f => !Inputs.Contains(Modules.Ext(f)))) yield break;
        yield return new Op { Label = "Quadros", Sub = "todos em PNG ou JPG", One = (f, p) => Frames(f, p) };
        bool subs = files.All(f => { try { return Subtitles(f).Count > 0; } catch { return false; } });
        if (subs) yield return new Op { Label = "Legendas", Sub = "todas as faixas", One = (f, p) => ExtractAllSubtitles(f) };
    }

    static async Task<string?> Frames(string src, P p)
    {
        if (!Ff.HasVideo(src)) throw new Exception("Esse arquivo não contém vídeo");
        var ext = p.S("formato", "png").ToLowerInvariant();
        if (ext is not ("png" or "jpg")) throw new Exception("Use png ou jpg como formato dos quadros");
        var dir = Paths.OutDir(src, "quadros");
        Directory.CreateDirectory(dir);
        try
        {
            var args = new List<string> { "-i", src, "-map", "0:v:0", "-fps_mode", "passthrough" };
            if (ext == "jpg") args.AddRange(["-q:v", "2"]);
            args.Add(Path.Combine(dir, $"quadro-%06d.{ext}"));
            await Ff.Run(args, Ff.Duration(src));
            if (!Directory.EnumerateFiles(dir).Any()) throw new Exception("Nenhum quadro foi extraído");
            return dir;
        }
        catch { Paths.TryDelete(dir); throw; }
    }

    static async Task<string?> ExtractAllSubtitles(string src)
    {
        var subs = Subtitles(src);
        if (subs.Count == 0) throw new Exception("Esse vídeo não contém legendas embutidas");
        var dir = Paths.OutDir(src, "legendas");
        Directory.CreateDirectory(dir);
        try
        {
            for (int i = 0; i < subs.Count; i++)
            {
                Job.Check();
                var s = subs[i];
                var ext = s.Codec switch { "ass" or "ssa" => "ass", "subrip" or "mov_text" or "webvtt" or "text" => "srt", _ => null };
                ext ??= s.Codec == "hdmv_pgs_subtitle" ? "sup" : "mks";
                var args = new List<string> { "-i", src, "-map", $"0:{s.Index}", "-c:s", ext switch { "ass" => "ass", "srt" => "srt", _ => "copy" } };
                if (ext == "sup") args.AddRange(["-f", "sup"]);
                if (ext == "mks") args.AddRange(["-f", "matroska"]);
                args.Add(Path.Combine(dir, $"legenda-{i + 1:00}.{ext}"));
                await Ff.Run(args, Ff.Duration(src), i / (double)subs.Count, 1.0 / subs.Count);
            }
            return dir;
        }
        catch { Paths.TryDelete(dir); throw; }
    }
}
