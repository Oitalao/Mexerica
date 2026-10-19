using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mexerica;

static class Ff
{
    public static readonly string? Exe = Paths.FindTool("ffmpeg.exe");
    public static readonly string? Probe = Paths.FindTool("ffprobe.exe");
    public static bool Ok => Exe != null && Probe != null;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const string Even = "scale=trunc(iw/2)*2:trunc(ih/2)*2";

    /// Roda o ffmpeg avisando o progresso pela duração esperada (segundos).
    /// Se houver codificador de vídeo na placa (Hw), troca o libx264 por ele; se a placa falhar, refaz no processador.
    public static async Task Run(IEnumerable<string> args, double duration = 0, double from = 0, double span = 1, bool hw = true)
    {
        var list = args.ToList();
        if (hw && Hw.Swap(list) is { } fast)
        {
            try { await RunRaw(fast, duration, from, span); return; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Write($"placa de vídeo ({Hw.Encoder}) falhou, refazendo no processador: {ex.Message}");
                Hw.Failed();
            }
        }
        await RunRaw(list, duration, from, span);
    }

    static Task RunRaw(IEnumerable<string> args, double duration, double from, double span)
    {
        var a = new List<string> { "-hide_banner", "-nostdin", "-y", "-nostats", "-progress", "pipe:1" };
        a.AddRange(args);
        return Proc.Run(Exe ?? throw new Exception("ffmpeg não encontrado"), a, onOut: line =>
        {
            if (duration <= 0 || !line.StartsWith("out_time_us=")) return;
            if (long.TryParse(line[12..], out var us)) Job.Progress(from + span * (us / 1e6 / duration));
        });
    }

    public static async Task<string> RunOut(IEnumerable<string> args) =>
        await Proc.Run(Exe ?? throw new Exception("ffmpeg não encontrado"), new[] { "-hide_banner", "-nostdin", "-y" }.Concat(args));

    // ---------- Leitura do arquivo (uma chamada ao ffprobe por arquivo, guardada) ----------
    public sealed record Stream(int Index, string Type, string Codec, bool Cover, int W, int H, int Rate);
    public sealed record Info(double Duration, List<Stream> Streams)
    {
        public Stream? Video => Streams.FirstOrDefault(s => s.Type == "video" && !s.Cover);
        public List<Stream> Audios => Streams.Where(s => s.Type == "audio").ToList();
    }

    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Info> probes = new(StringComparer.OrdinalIgnoreCase);

    public static Info Read(string src)
    {
        DateTime mt; long len;
        try { var fi = new FileInfo(src); mt = fi.LastWriteTimeUtc; len = fi.Length; } catch { return new Info(0, new()); }
        var key = $"{src}|{mt.Ticks}|{len}";
        if (probes.TryGetValue(key, out var hit)) return hit;
        var info = new Info(0, new());
        try
        {
            var t = Proc.RunOutput(Probe!, new[] { "-v", "error", "-show_entries", "format=duration:stream=index,codec_type,codec_name,width,height,sample_rate:stream_disposition=attached_pic", "-of", "json", src });
            using var doc = System.Text.Json.JsonDocument.Parse(t);
            var root = doc.RootElement;
            double dur = 0;
            if (root.TryGetProperty("format", out var fmt) && fmt.TryGetProperty("duration", out var d))
                double.TryParse(d.GetString(), NumberStyles.Float, Inv, out dur);
            var streams = new List<Stream>();
            if (root.TryGetProperty("streams", out var ss))
                foreach (var s in ss.EnumerateArray())
                {
                    string S(string k) => s.TryGetProperty(k, out var v) ? (v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() ?? "" : v.ToString()) : "";
                    int I(string k) => int.TryParse(S(k), out var v) ? v : 0;
                    bool cover = s.TryGetProperty("disposition", out var disp) && disp.TryGetProperty("attached_pic", out var ap) && ap.GetInt32() == 1;
                    streams.Add(new Stream(I("index"), S("codec_type"), S("codec_name"), cover, I("width"), I("height"), I("sample_rate")));
                }
            info = new Info(dur, streams);
            if (probes.Count > 200) probes.Clear();
            probes[key] = info;
        }
        catch (Exception ex) { Log.Write($"ffprobe {src}: {ex.Message}"); }
        return info;
    }

    public static double Duration(string src) => Read(src).Duration;
    // capas de música contam como vídeo "attached_pic"; não são vídeo de verdade
    public static bool HasAudio(string src) => Read(src).Audios.Count > 0;
    public static bool HasVideo(string src) => Read(src).Video != null;
    public static int SampleRate(string src) => Read(src).Audios.FirstOrDefault()?.Rate ?? 0;
    public static (int w, int h) Size(string src) => Read(src).Video is { } v ? (v.W, v.H) : (0, 0);

    static Config C => Config.Now;

    public static string[] AudioOpts(string ext) => ext switch
    {
        "mp3" => new[] { "-c:a", "libmp3lame", "-q:a", C.Mp3Q },
        "m4a" => new[] { "-c:a", "aac", "-b:a", $"{C.AacK}k" },
        "wav" => new[] { "-c:a", "pcm_s16le" },
        "flac" => new[] { "-c:a", "flac" },
        "ogg" => new[] { "-c:a", "libvorbis", "-q:a", C.AudioQuality switch { "media" => "4", "leve" => "2", _ => "6" } },
        "opus" => new[] { "-c:a", "libopus", "-b:a", C.AudioQuality switch { "media" => "96k", "leve" => "64k", _ => "128k" } },
        "aiff" => new[] { "-c:a", "pcm_s16be" },
        "wma" => new[] { "-c:a", "wmav2", "-b:a", $"{C.AacK}k" },
        _ => throw new Exception($"Formato {ext} não suportado"),
    };

    /// Codificação de vídeo por contêiner; vf = filtros extras (vão antes do ajuste de par).
    public static string[] VideoOpts(string ext, string? vf = null, string? af = null, bool audio = true)
    {
        string F(string baseVf) => vf == null ? baseVf : $"{vf},{baseVf}";
        var a = new List<string>();
        switch (ext)
        {
            case "webm":
                if (vf != null) a.AddRange(new[] { "-vf", vf });
                a.AddRange(new[] { "-c:v", "libvpx-vp9", "-crf", (C.Crf + 12).ToString(), "-b:v", "0", "-row-mt", "1", "-tile-columns", "2", "-deadline", "good", "-cpu-used", "5" });
                if (audio) a.AddRange(new[] { "-c:a", "libopus", "-b:a", "128k" });
                break;
            case "avi":
                if (vf != null) a.AddRange(new[] { "-vf", vf });
                a.AddRange(new[] { "-c:v", "mpeg4", "-q:v", C.VideoQuality == "alta" ? "3" : C.VideoQuality == "media" ? "5" : "8" });
                if (audio) a.AddRange(new[] { "-c:a", "libmp3lame", "-q:a", "3" });
                break;
            case "wmv":
                if (vf != null) a.AddRange(new[] { "-vf", vf });
                a.AddRange(new[] { "-c:v", "wmv2", "-q:v", C.VideoQuality == "alta" ? "3" : C.VideoQuality == "media" ? "5" : "8" });
                if (audio) a.AddRange(new[] { "-c:a", "wmav2", "-b:a", "160k" });
                break;
            case "gif":
                a.AddRange(new[] { "-an", "-vf", (vf == null ? "" : vf + ",") + "fps=12,scale='min(640,iw)':-2:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4", "-loop", "0" });
                return a.ToArray();
            default: // mp4, mov, mkv, m4v (sem placa de vídeo, "fast": quase a mesma qualidade, bem mais rápido)
                a.AddRange(new[] { "-vf", F(Even), "-c:v", "libx264", "-preset", "fast", "-crf", C.Crf.ToString(), "-pix_fmt", "yuv420p" });
                if (audio) a.AddRange(new[] { "-c:a", "aac", "-b:a", "160k" });
                if (ext is "mp4" or "m4v" or "mov") a.AddRange(new[] { "-movflags", "+faststart" });
                break;
        }
        if (af != null && audio) a.AddRange(new[] { "-af", af });
        if (!audio) a.Add("-an");
        return a.ToArray();
    }

    static string Ext(string src) => Path.GetExtension(src).TrimStart('.').ToLowerInvariant();
    static readonly string[] videoOut = { "mp4", "mov", "mkv", "webm", "avi", "wmv", "m4v" };
    static readonly string[] audioOut = { "mp3", "m4a", "wav", "flac", "ogg", "opus", "aiff", "wma" };
    /// Contêiner de saída das ferramentas: o mesmo do original quando dá.
    static string VOut(string src) => videoOut.Contains(Ext(src)) ? Ext(src) : "mp4";
    static string AOut(string src) => Ext(src) switch { "aif" => "aiff", "oga" => "ogg", var e when audioOut.Contains(e) => e, _ => "m4a" };

    // Codec de áudio que cada formato de saída usa: se o original já está nele, basta copiar.
    static readonly Dictionary<string, string> sameAudio = new() { ["m4a"] = "aac", ["mp3"] = "mp3", ["opus"] = "opus", ["ogg"] = "vorbis", ["flac"] = "flac", ["wma"] = "wmav2" };

    public static async Task<string> ToAudio(string src, string ext, bool fromVideo)
    {
        var dst = Paths.Out(src, ext);
        var info = Read(src);
        var first = info.Audios.FirstOrDefault();
        if (first == null) throw new Exception("Esse arquivo não tem a faixa necessária (sem áudio?)");
        // Áudio de vídeo (ou de outro envelope) que já está no codec certo: copia sem perder nada, em segundos.
        if (sameAudio.TryGetValue(ext, out var codec) && first.Codec == codec)
        {
            try
            {
                await Run(new[] { "-i", src, "-map", $"0:{first.Index}", "-vn", "-map_metadata", "0", "-c:a", "copy", dst }, info.Duration);
                return dst;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { Paths.TryDelete(dst); Log.Write($"cópia de áudio falhou, recodificando: {ex.Message}"); }
        }
        var a = new List<string> { "-i", src, "-vn", "-map_metadata", "0" };
        a.AddRange(AudioOpts(ext)); a.Add(dst);
        await Guard(dst, Run(a, info.Duration));
        return dst;
    }

    // O que cada envelope aceita sem recodificar.
    static readonly Dictionary<string, (string[] v, string[] a)> fits = new()
    {
        ["mp4"] = (new[] { "h264", "hevc", "av1", "mpeg4" }, new[] { "aac", "mp3", "ac3", "eac3", "alac", "opus" }),
        ["m4v"] = (new[] { "h264", "hevc", "mpeg4" }, new[] { "aac", "mp3", "ac3", "eac3", "alac" }),
        ["mov"] = (new[] { "h264", "hevc", "mpeg4", "prores", "mjpeg" }, new[] { "aac", "mp3", "ac3", "eac3", "alac", "pcm_s16le", "pcm_s24le" }),
        ["mkv"] = (new[] { "h264", "hevc", "av1", "vp8", "vp9", "mpeg4", "mpeg2video", "mpeg1video", "mjpeg", "prores", "theora", "vc1" },
                   new[] { "aac", "mp3", "ac3", "eac3", "opus", "vorbis", "flac", "alac", "dts", "truehd", "pcm_s16le", "pcm_s24le", "mp2" }),
        ["webm"] = (new[] { "vp8", "vp9", "av1" }, new[] { "opus", "vorbis" }),
    };

    /// Muda o envelope sem recodificar a imagem (MKV→MP4, MOV→MP4…): segundos em vez de minutos, sem perder qualidade.
    /// Só o áudio é refeito se o envelope novo não aceitar o original. Devolve false se não dá para fazer assim.
    static async Task<bool> Remux(string src, string ext, string dst, Info info)
    {
        if (!fits.TryGetValue(ext, out var ok) || info.Video is not { } v || !ok.v.Contains(v.Codec)) return false;
        bool audioCopy = info.Audios.All(s => ok.a.Contains(s.Codec));
        var a = new List<string> { "-i", src, "-map", $"0:{v.Index}" };
        foreach (var s in info.Audios) a.AddRange(new[] { "-map", $"0:{s.Index}" });
        a.AddRange(new[] { "-map_metadata", "0", "-c:v", "copy", "-sn", "-dn" });
        if (info.Audios.Count > 0)
        {
            if (audioCopy) a.AddRange(new[] { "-c:a", "copy" });
            else if (ext == "webm") a.AddRange(new[] { "-c:a", "libopus", "-b:a", "128k" });
            else a.AddRange(new[] { "-c:a", "aac", "-b:a", "192k" });
        }
        if (v.Codec == "hevc" && ext is "mp4" or "mov" or "m4v") a.AddRange(new[] { "-tag:v", "hvc1" }); // abre no Windows e no iPhone
        if (ext is "mp4" or "mov" or "m4v") a.AddRange(new[] { "-movflags", "+faststart" });
        a.Add(dst);
        try { await Run(a, info.Duration); return true; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Paths.TryDelete(dst);
            Log.Write($"troca de envelope falhou ({Path.GetFileName(src)} → {ext}), recodificando: {ex.Message}");
            return false;
        }
    }

    public static async Task<string> ToVideo(string src, string ext)
    {
        var dst = Paths.Out(src, ext);
        var info = Read(src);
        if (await Remux(src, ext, dst, info)) return dst;
        await Guard(dst, Run(new[] { "-i", src }.Concat(VideoOpts(ext, audio: info.Audios.Count > 0 && ext != "gif")).Append(dst), info.Duration));
        return dst;
    }

    public static async Task<string> Simple(string src, string ext, string suffix, params string[] opts)
    {
        var dst = Paths.Out(src, ext, suffix);
        await Guard(dst, Run(new[] { "-i", src }.Concat(opts).Append(dst), Duration(src)));
        return dst;
    }

    static long TargetBytes(string src, P p) => p.S("modo") switch
    {
        "pct" => (long)(new FileInfo(src).Length * Math.Clamp(p.D("pct", 50), 1, 99) / 100),
        "mb" => (long)(p.D("mb", 10) * 1024 * 1024),
        _ => 0,
    };

    // ---------- Vídeo ----------
    public static async Task<string> CompressVideo(string src, P p)
    {
        var dur = Duration(src);
        long target = TargetBytes(src, p);
        bool audio = HasAudio(src);
        var dst = Paths.Out(src, "mp4", "comprimido");
        if (target <= 0)
        {
            var (crf, max, ab) = p.S("nivel", "media") switch { "leve" => (24, 2560, "160k"), "forte" => (32, 1280, "96k"), _ => (28, 1920, "128k") };
            var a = new List<string> { "-i", src, "-vf", $"scale='min({max},iw)':-2,{Even}", "-c:v", "libx264", "-preset", "medium", "-crf", crf.ToString(), "-pix_fmt", "yuv420p" };
            a.AddRange(audio ? new[] { "-c:a", "aac", "-b:a", ab } : new[] { "-an" });
            a.AddRange(new[] { "-movflags", "+faststart", dst });
            await Guard(dst, Run(a, dur, hw: false)); // comprimir é para ficar pequeno: o x264 faz arquivo menor que a placa
            return dst;
        }
        if (dur <= 0) throw new Exception("Não consegui ler a duração do vídeo");
        // Duas passadas com taxa calculada para caber no tamanho pedido (com 3% de folga).
        int ak = audio ? (target * 8 / dur / 1000 < 400 ? 64 : 128) : 0;
        double vk = target * 0.97 * 8 / dur / 1000 - ak;
        if (vk < 50) throw new Exception("Esse tamanho é pequeno demais para a duração do vídeo");
        var (w, h) = Size(src);
        int maxSide = vk < 400 ? 640 : vk < 900 ? 960 : vk < 2000 ? 1280 : vk < 5000 ? 1920 : 3840;
        var scale = $"scale='if(gt(iw,ih),min({maxSide},iw),-2)':'if(gt(iw,ih),-2,min({maxSide},ih))',{Even}";
        var tmp = Paths.Temp();
        try
        {
            var log = Path.Combine(tmp, "passe");
            var common = new List<string> { "-vf", scale, "-c:v", "libx264", "-preset", "medium", "-b:v", $"{(int)vk}k", "-maxrate", $"{(int)(vk * 1.4)}k", "-bufsize", $"{(int)(vk * 2)}k", "-pix_fmt", "yuv420p", "-passlogfile", log };
            await Run(new[] { "-i", src }.Concat(common).Concat(new[] { "-pass", "1", "-an", "-f", "null", "NUL" }), dur, 0, 0.5);
            var a = new List<string> { "-i", src };
            a.AddRange(common); a.AddRange(new[] { "-pass", "2" });
            a.AddRange(audio ? new[] { "-c:a", "aac", "-b:a", $"{ak}k" } : new[] { "-an" });
            a.AddRange(new[] { "-movflags", "+faststart", dst });
            await Guard(dst, Run(a, dur, 0.5, 0.5));
            return dst;
        }
        finally { Paths.TryDelete(tmp); }
    }

    public static Task<string> ResizeVideo(string src, P p)
    {
        var v = p.S("altura", "50%");
        var vf = v == "50%" ? "scale=trunc(iw/4)*2:trunc(ih/4)*2" : $"scale=-2:'min({v},ih)'";
        var ext = VOut(src);
        return Simple(src, ext, v == "50%" ? "50%" : v + "p", VideoOpts(ext, vf, audio: HasAudio(src)));
    }

    public static Task<string> RotateVideo(string src, P p)
    {
        var how = p.S("giro", "90");
        var vf = how switch { "180" => "transpose=1,transpose=1", "270" => "transpose=2", "h" => "hflip", "v" => "vflip", _ => "transpose=1" };
        var ext = VOut(src);
        return Simple(src, ext, how is "h" or "v" ? "espelhado" : "girado", VideoOpts(ext, vf, audio: HasAudio(src)));
    }

    public static Task<string> Mute(string src)
    {
        var ext = VOut(src);
        return Simple(src, ext, "sem áudio", "-map", "0:v", "-c", "copy", "-an");
    }

    public static Task<string> StripMedia(string src)
    {
        var ext = Ext(src);
        return Simple(src, ext, "sem metadados", "-map", "0", "-map_metadata", "-1", "-map_chapters", "-1", "-c", "copy");
    }

    public static async Task<string> Frame(string src, P p)
    {
        var t = p.D("tempo", -1);
        var dur = Duration(src);
        if (t < 0) t = Math.Min(1, dur / 2);
        var ext = p.S("formato", "jpg");
        var dst = Paths.Out(src, ext, "quadro " + t.ToString("0.#", CultureInfo.GetCultureInfo("pt-BR")) + "s");
        var a = new List<string> { "-ss", t.ToString("0.###", Inv), "-i", src, "-frames:v", "1" };
        if (ext == "jpg") a.AddRange(new[] { "-q:v", "2" });
        a.Add(dst);
        await Guard(dst, Run(a));
        if (!File.Exists(dst)) throw new Exception("Esse ponto passa do fim do vídeo");
        return dst;
    }

    public static async Task<string> Crop(string src, P p)
    {
        var r = Images.Nums(p.S("recorte"));
        if (r.Length != 4) throw new Exception("Recorte inválido");
        int x = (int)r[0] / 2 * 2, y = (int)r[1] / 2 * 2, w = (int)r[2] / 2 * 2, h = (int)r[3] / 2 * 2;
        var ext = VOut(src);
        return await Simple(src, ext, "recortado", VideoOpts(ext, $"crop={w}:{h}:{x}:{y}", audio: HasAudio(src)));
    }

    public static async Task<string> Speed(string src, P p)
    {
        double f = Math.Clamp(p.D("fator", 2), 0.1, 16);
        bool video = HasVideo(src), audio = HasAudio(src);
        string atempo = Atempo(f);
        var label = f.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR")) + "x";
        if (!video)
        {
            var ext = AOut(src);
            return await Simple(src, ext, label, new[] { "-vn", "-af", atempo }.Concat(AudioOpts(ext)).ToArray());
        }
        var vext = VOut(src);
        var vf = $"setpts=PTS/{f.ToString(Inv)}";
        return await Simple(src, vext, label, VideoOpts(vext, vf, audio ? atempo : null, audio));
    }

    static string Atempo(double f)
    {
        var parts = new List<string>();
        while (f > 2) { parts.Add("atempo=2"); f /= 2; }
        while (f < 0.5) { parts.Add("atempo=0.5"); f /= 0.5; }
        parts.Add("atempo=" + f.ToString("0.#####", Inv));
        return string.Join(",", parts);
    }

    /// Corta um trecho: inicio/fim em segundos. preciso=1 recodifica; senão copia (rápido, corta no quadro-chave).
    public static async Task<string> Trim(string src, P p)
    {
        var dur = Duration(src);
        double s = Math.Max(0, p.D("inicio")), e = p.D("fim", dur);
        if (e <= 0 || e > dur && dur > 0) e = dur;
        if (e - s < 0.05) throw new Exception("O trecho escolhido está vazio");
        bool video = HasVideo(src);
        var ext = video ? VOut(src) : AOut(src);
        var dst = Paths.Out(src, ext, "trecho");
        var a = new List<string> { "-ss", s.ToString("0.###", Inv), "-to", e.ToString("0.###", Inv), "-i", src, "-map_metadata", "0" };
        if (p.S("modo", "preciso") == "rapido") a.AddRange(new[] { "-c", "copy", "-avoid_negative_ts", "make_zero" });
        else a.AddRange(video ? VideoOpts(ext, audio: HasAudio(src)) : new[] { "-vn" }.Concat(AudioOpts(ext)));
        a.Add(dst);
        await Guard(dst, Run(a, e - s));
        return dst;
    }

    /// Divide em partes: modo pontos (tempos "t1;t2"), partes (n iguais) ou cada (segundos).
    public static async Task<string> Split(string src, P p)
    {
        var dur = Duration(src);
        if (dur <= 0) throw new Exception("Não consegui ler a duração");
        var cuts = new List<double>();
        switch (p.S("modo", "partes"))
        {
            case "pontos":
                cuts = ParseTimes(p.S("pontos")).Where(t => t > 0.05 && t < dur - 0.05).OrderBy(t => t).ToList();
                break;
            case "cada":
                var step = p.D("cada", 60);
                if (step < 1) throw new Exception("Intervalo pequeno demais");
                for (double t = step; t < dur - 0.05; t += step) cuts.Add(t);
                break;
            default:
                int n = Math.Clamp(p.I("partes", 2), 2, 500);
                for (int i = 1; i < n; i++) cuts.Add(dur * i / n);
                break;
        }
        if (cuts.Count == 0) throw new Exception("Nenhum ponto de corte dentro do arquivo");
        var bounds = new List<double> { 0 }; bounds.AddRange(cuts); bounds.Add(dur);
        bool video = HasVideo(src);
        var ext = video ? VOut(src) : AOut(src);
        var dir = Paths.OutDir(src, "partes");
        Directory.CreateDirectory(dir);
        var b = Path.GetFileNameWithoutExtension(src);
        try
        {
            for (int i = 0; i < bounds.Count - 1; i++)
            {
                Job.Part(i, bounds.Count - 1);
                var dst = Path.Combine(dir, $"{b} {i + 1:00}.{ext}");
                var a = new List<string> { "-ss", bounds[i].ToString("0.###", Inv), "-to", bounds[i + 1].ToString("0.###", Inv), "-i", src, "-map_metadata", "0" };
                a.AddRange(video ? VideoOpts(ext, audio: HasAudio(src)) : new[] { "-vn" }.Concat(AudioOpts(ext)));
                a.Add(dst);
                await Run(a, bounds[i + 1] - bounds[i]);
            }
        }
        catch { Paths.TryDelete(dir); throw; }
        return dir;
    }

    public static async Task<string> Concat(string[] files, bool video)
    {
        var tmp = Paths.Temp();
        try
        {
            var ext = video ? "mp4" : (files.Select(AOut).Distinct().Count() == 1 ? AOut(files[0]) : "mp3");
            var dst = Paths.Out(files[0], ext, "juntos");
            double total = files.Sum(Duration);
            var a = new List<string>();
            if (video)
            {
                // Iguala tamanho e taxa de quadros: vídeos de origens diferentes não juntam por concat simples.
                var (w, h) = Size(files[0]);
                if (w <= 0) (w, h) = (1280, 720);
                w = w / 2 * 2; h = h / 2 * 2;
                var fc = new StringBuilder();
                for (int i = 0; i < files.Length; i++)
                {
                    a.AddRange(new[] { "-i", files[i] });
                    fc.Append($"[{i}:v]scale={w}:{h}:force_original_aspect_ratio=decrease,pad={w}:{h}:(ow-iw)/2:(oh-ih)/2,setsar=1,fps=30,format=yuv420p[v{i}];");
                    if (HasAudio(files[i])) fc.Append($"[{i}:a]aresample=48000,aformat=channel_layouts=stereo[a{i}];");
                    else fc.Append($"anullsrc=r=48000:cl=stereo,atrim=0:{Duration(files[i]).ToString("0.###", Inv)}[a{i}];");
                }
                for (int i = 0; i < files.Length; i++) fc.Append($"[v{i}][a{i}]");
                fc.Append($"concat=n={files.Length}:v=1:a=1[v][a]");
                a.AddRange(new[] { "-filter_complex", fc.ToString(), "-map", "[v]", "-map", "[a]", "-c:v", "libx264", "-preset", "fast", "-crf", C.Crf.ToString(), "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart", dst });
            }
            else
            {
                var fc = new StringBuilder();
                for (int i = 0; i < files.Length; i++) { a.AddRange(new[] { "-i", files[i] }); fc.Append($"[{i}:a]aresample=48000,aformat=channel_layouts=stereo[a{i}];"); }
                for (int i = 0; i < files.Length; i++) fc.Append($"[a{i}]");
                fc.Append($"concat=n={files.Length}:v=0:a=1[a]");
                a.AddRange(new[] { "-filter_complex", fc.ToString(), "-map", "[a]" });
                a.AddRange(AudioOpts(ext)); a.Add(dst);
            }
            await Guard(dst, Run(a, total));
            return dst;
        }
        finally { Paths.TryDelete(tmp); }
    }

    // ---------- Áudio ----------
    public static async Task<string> CompressAudio(string src, P p)
    {
        long target = TargetBytes(src, p);
        int kbps;
        if (target > 0)
        {
            var dur = Duration(src);
            if (dur <= 0) throw new Exception("Não consegui ler a duração");
            kbps = (int)(target * 0.97 * 8 / dur / 1000);
            if (kbps < 16) throw new Exception("Esse tamanho é pequeno demais para a duração do áudio");
            kbps = Math.Min(kbps, 320);
        }
        else kbps = p.S("nivel", "media") switch { "leve" => 192, "forte" => 64, _ => 128 };
        var a = new List<string> { "-vn", "-map_metadata", "0", "-c:a", "libmp3lame", "-b:a", $"{kbps}k" };
        if (kbps <= 64) a.AddRange(new[] { "-ac", "1" });
        return await Simple(src, "mp3", "comprimido", a.ToArray());
    }

    /// Normaliza o volume (EBU R128, duas passadas).
    public static async Task<string> Normalize(string src, P p)
    {
        double lufs = p.D("alvo", -14);
        var dur = Duration(src);
        var first = await RunOut(new[] { "-i", src, "-vn", "-af", $"loudnorm=I={lufs.ToString(Inv)}:TP=-1.5:LRA=11:print_format=json", "-f", "null", "NUL" });
        Job.Progress(0.4);
        var m = Regex.Match(first, @"\{[^{}]*""input_i""[^{}]*\}", RegexOptions.Singleline);
        string Val(string k) => Regex.Match(m.Value, $"\"{k}\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
        string af = m.Success && Val("input_i") is var ii && ii != "-inf"
            ? $"loudnorm=I={lufs.ToString(Inv)}:TP=-1.5:LRA=11:measured_I={ii}:measured_TP={Val("input_tp")}:measured_LRA={Val("input_lra")}:measured_thresh={Val("input_thresh")}:offset={Val("target_offset")}:linear=true"
            : $"loudnorm=I={lufs.ToString(Inv)}:TP=-1.5:LRA=11";
        // o loudnorm trabalha em 192 kHz; volta para a taxa original
        var rate = SampleRate(src);
        af += $",aresample={(rate > 0 ? rate : 48000)}";
        if (HasVideo(src))
        {
            var ext = VOut(src);
            var dst = Paths.Out(src, ext, "normalizado");
            await Guard(dst, Run(new[] { "-i", src, "-map", "0:v:0", "-map", "0:a:0", "-c:v", "copy", "-af", af, "-c:a", "aac", "-b:a", "192k", dst }, dur, 0.4, 0.6));
            return dst;
        }
        var aext = AOut(src);
        var adst = Paths.Out(src, aext, "normalizado");
        var a = new List<string> { "-i", src, "-vn", "-map_metadata", "0", "-af", af };
        if (aext is "flac" or "wav" or "aiff") a.AddRange(new[] { "-sample_fmt", "s16" });
        a.AddRange(AudioOpts(aext)); a.Add(adst);
        await Guard(adst, Run(a, dur, 0.4, 0.6));
        return adst;
    }

    /// Bipe por cima de trechos: trechos="inicio-fim;inicio-fim" (segundos ou mm:ss).
    public static async Task<string> Bleep(string src, P p)
    {
        var ranges = ParseRanges(p.S("trechos"));
        if (ranges.Count == 0) throw new Exception("Nenhum trecho para o bipe");
        var cond = string.Join("+", ranges.Select(r => $"between(t,{r.a.ToString("0.###", Inv)},{r.b.ToString("0.###", Inv)})"));
        var freq = p.I("freq", 1000);
        var dur = Duration(src);
        var fc = freq <= 0
            ? $"[0:a]volume=0:enable='{cond}'[a]"
            : $"[0:a]volume=0:enable='{cond}'[m];sine=f={freq}:sample_rate=48000:d={dur.ToString("0.###", Inv)},volume=2,volume=0:enable='not({cond})'[s];[m][s]amix=inputs=2:duration=first:normalize=0[a]";
        if (HasVideo(src))
        {
            var ext = VOut(src);
            var dst = Paths.Out(src, ext, "com bipe");
            await Guard(dst, Run(new[] { "-i", src, "-filter_complex", fc, "-map", "0:v:0", "-map", "[a]", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", dst }, dur));
            return dst;
        }
        var aext = AOut(src);
        var adst = Paths.Out(src, aext, "com bipe");
        var a = new List<string> { "-i", src, "-filter_complex", fc, "-map", "[a]", "-map_metadata", "0" };
        a.AddRange(AudioOpts(aext)); a.Add(adst);
        await Guard(adst, Run(a, dur));
        return adst;
    }

    public static async Task<string> Channels(string src, P p)
    {
        var how = p.S("canais", "mono");
        var (af, ac, label) = how switch
        {
            "estereo" => ("aformat=channel_layouts=stereo", "2", "estéreo"),
            "esquerdo" => ("pan=mono|c0=FL", "1", "só esquerdo"),
            "direito" => ("pan=mono|c0=FR", "1", "só direito"),
            "inverter" => ("pan=stereo|c0=FR|c1=FL", "2", "lados trocados"),
            "dobrar" => ("pan=stereo|c0=FL|c1=FL", "2", "esquerdo nos dois"),
            _ => ("pan=mono|c0=0.5*FL+0.5*FR", "1", "mono"),
        };
        if (HasVideo(src))
        {
            var ext = VOut(src);
            return await Simple(src, ext, label, "-map", "0:v:0", "-map", "0:a:0", "-c:v", "copy", "-af", af, "-ac", ac, "-c:a", "aac", "-b:a", "160k");
        }
        var aext = AOut(src);
        return await Simple(src, aext, label, new[] { "-vn", "-map_metadata", "0", "-af", af, "-ac", ac }.Concat(AudioOpts(aext)).ToArray());
    }

    public static async Task<string> SetMeta(string src, P p)
    {
        var ext = Ext(src);
        var dst = Paths.Out(src, ext, p.B("limpar") ? "sem metadados" : "metadados");
        var a = new List<string> { "-i", src, "-map", "0", "-c", "copy" };
        if (p.B("limpar")) a.AddRange(new[] { "-map_metadata", "-1", "-map_chapters", "-1" });
        foreach (var (k, v) in p.Where(kv => kv.Key.StartsWith("m.")))
            a.AddRange(new[] { "-metadata", $"{FfTag(k[2..])}={v}" });
        a.Add(dst);
        await Guard(dst, Run(a, Duration(src)));
        return dst;
    }

    static string FfTag(string k) => k switch
    {
        "Title" => "title", "Artist" => "artist", "Album" => "album", "Comment" => "comment", "Copyright" => "copyright",
        "Date" => "date", "Genre" => "genre", "Description" => "description", "Track" => "track", "Keywords" => "keywords",
        _ => k.ToLowerInvariant(),
    };

    // ---------- Tempo ----------
    public static string Clock(double s)
    {
        if (s < 0) s = 0;
        var t = TimeSpan.FromSeconds(s);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}" + (t.Milliseconds > 0 ? $",{t.Milliseconds / 100}" : "");
    }

    /// Aceita "90", "1:30", "01:02:03,5".
    public static double ParseTime(string s)
    {
        s = s.Trim().Replace(',', '.');
        if (s.Length == 0) return double.NaN;
        double total = 0;
        foreach (var part in s.Split(':'))
        {
            if (!double.TryParse(part, NumberStyles.Float, Inv, out var v)) return double.NaN;
            total = total * 60 + v;
        }
        return total;
    }

    public static List<double> ParseTimes(string s) =>
        s.Split(new[] { ';', ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(ParseTime).Where(d => !double.IsNaN(d)).ToList();

    public static List<(double a, double b)> ParseRanges(string s)
    {
        var list = new List<(double, double)>();
        foreach (var part in s.Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var f = part.Split(new[] { '-', '–', 'a' }, 2);
            if (f.Length != 2) continue;
            double a = ParseTime(f[0]), b = ParseTime(f[1]);
            if (!double.IsNaN(a) && !double.IsNaN(b) && b > a) list.Add((a, b));
        }
        return list;
    }

    static async Task Guard(string dst, Task t)
    {
        try { await t; }
        catch { Paths.TryDelete(dst); throw; }
    }
}

/// Codificador de vídeo na placa (NVIDIA, Intel Quick Sync ou AMD). Testado uma vez por sessão, em segundo plano.
static class Hw
{
    static Task<string?>? detect;
    static volatile bool failed;

    /// Começa o teste da placa sem travar nada (chamado ao abrir o Mexerica).
    public static void Warm() => detect ??= Task.Run(Detect);

    public static string? Encoder
    {
        get
        {
            if (failed || !Config.Now.UseGpu) return null;
            Warm();
            return detect!.Wait(TimeSpan.FromSeconds(20)) ? detect.Result : null;
        }
    }

    /// A placa falhou num arquivo de verdade: o resto da sessão vai pelo processador.
    public static void Failed() => failed = true;

    static string? Detect()
    {
        if (Ff.Exe == null) return null;
        foreach (var enc in new[] { "h264_nvenc", "h264_qsv", "h264_amf" })
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(Ff.Exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
                foreach (var a in new[] { "-hide_banner", "-v", "error", "-f", "lavfi", "-i", "color=black:s=640x360:r=30:d=0.3", "-c:v", enc, "-f", "null", "NUL" })
                    psi.ArgumentList.Add(a);
                using var p = System.Diagnostics.Process.Start(psi)!;
                var err = p.StandardError.ReadToEndAsync();
                p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(15000)) { try { p.Kill(true); } catch { } continue; }
                if (p.ExitCode == 0) { Log.Write($"vídeo pela placa: {enc}"); return enc; }
            }
            catch { }
        }
        return null;
    }

    /// Troca o libx264 de uma linha do ffmpeg pelo codificador da placa, com qualidade equivalente.
    /// Devolve null quando não há placa ou a linha não serve (duas passadas, taxa fixa).
    public static List<string>? Swap(List<string> args)
    {
        int i = args.FindIndex(a => a == "-c:v");
        if (i < 0 || i + 1 >= args.Count || args[i + 1] != "libx264") return null;
        if (args.Contains("-pass") || args.Contains("-b:v")) return null;
        var enc = Encoder;
        if (enc == null) return null;
        int crf = 20;
        var a = new List<string>(args);
        int Take(string key)
        {
            int k = a.IndexOf(key);
            if (k >= 0 && k + 1 < a.Count) { var v = a[k + 1]; a.RemoveRange(k, 2); return int.TryParse(v, out var n) ? n : -1; }
            return -1;
        }
        var c = Take("-crf"); if (c > 0) crf = c;
        Take("-preset");
        int pf = a.IndexOf("-pix_fmt");
        if (pf >= 0 && pf + 1 < a.Count && a[pf + 1] == "yuv420p") a.RemoveRange(pf, 2);
        i = a.IndexOf("-c:v");
        a.RemoveRange(i, 2);
        a.InsertRange(i, enc switch
        {
            "h264_nvenc" => new[] { "-c:v", enc, "-preset", "p5", "-tune", "hq", "-rc", "vbr", "-cq", (crf + 1).ToString(), "-b:v", "0", "-pix_fmt", "yuv420p" },
            "h264_qsv" => new[] { "-c:v", enc, "-preset", "medium", "-global_quality", crf.ToString(), "-pix_fmt", "nv12" },
            _ => new[] { "-c:v", enc, "-quality", "quality", "-rc", "cqp", "-qp_i", crf.ToString(), "-qp_p", (crf + 2).ToString(), "-qp_b", (crf + 4).ToString(), "-pix_fmt", "yuv420p" },
        });
        return a;
    }
}
