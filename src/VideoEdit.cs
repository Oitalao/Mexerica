using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mexerica;

/// Um pedaço da linha do tempo do editor: trecho [In, Out] de um vídeo (ou foto parada por Out segundos).
sealed class Clip
{
    public string Src { get; set; } = "";
    public bool Photo { get; set; }
    public double In { get; set; }
    public double Out { get; set; }
    public double Speed { get; set; } = 1;
    public double Volume { get; set; } = 1;
    /// Duração do arquivo de origem (foto: sem limite).
    public double SrcDur { get; set; }

    /// Quanto tempo o pedaço ocupa no vídeo final.
    public double Len => Math.Max(0, (Out - In) / Speed);
    public Clip Copy() => (Clip)MemberwiseClone();
}

/// O projeto inteiro: pedaços em ordem, proporção e como encaixar a imagem.
sealed class EditProject
{
    public List<Clip> Clips { get; set; } = new();
    /// original | 9:16 | 1:1 | 16:9
    public string Ratio { get; set; } = "original";
    /// preencher (corta as sobras) | caber (barras pretas)
    public string Fit { get; set; } = "preencher";

    public double Total => Clips.Sum(c => c.Len);
    public EditProject Copy() => new() { Clips = Clips.Select(c => c.Copy()).ToList(), Ratio = Ratio, Fit = Fit };

    static readonly JsonSerializerOptions json = new() { WriteIndented = false };
    public string ToJson() => JsonSerializer.Serialize(this, json);
    public static EditProject FromJson(string s) => JsonSerializer.Deserialize<EditProject>(s, json) ?? new();
}

/// Monta o vídeo final do editor com uma chamada só ao ffmpeg.
static class VideoEdit
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public const double PhotoSeconds = 3;
    public const double MinLen = 0.1;

    public static bool IsPhoto(string f) => Catalog.KindOf(f) is Kind.Image or Kind.Svg;

    public static Clip NewClip(string f)
    {
        if (IsPhoto(f)) return new Clip { Src = f, Photo = true, In = 0, Out = PhotoSeconds, SrcDur = 0 };
        var d = Ff.Duration(f);
        return new Clip { Src = f, In = 0, Out = d, SrcDur = d };
    }

    /// Projeto a partir dos parâmetros (janela ou linha de comando).
    /// --p projeto=<arquivo .json ou o próprio JSON>; --p "trechos=0:00-0:10;0:30-0:45" (do primeiro arquivo);
    /// sem nada: todos os arquivos inteiros, na ordem.
    public static EditProject FromParams(string[] files, P p)
    {
        EditProject proj;
        var pj = p.S("projeto");
        if (pj.Length > 0)
            proj = EditProject.FromJson(File.Exists(pj) ? File.ReadAllText(pj) : pj);
        else if (p.S("trechos").Length > 0)
        {
            var src = files[0];
            var full = NewClip(src);
            proj = new EditProject();
            foreach (var (a, b) in Ff.ParseRanges(p.S("trechos").Replace(";", "\n")))
            {
                var c = full.Copy();
                c.In = Math.Max(0, a);
                c.Out = full.Photo ? b - a : (full.SrcDur > 0 ? Math.Min(b, full.SrcDur) : b);
                if (full.Photo) c.In = 0;
                if (c.Out - c.In >= MinLen) proj.Clips.Add(c);
            }
        }
        else
        {
            proj = new EditProject();
            foreach (var f in files) proj.Clips.Add(NewClip(f));
        }
        if (p.S("proporcao").Length > 0) proj.Ratio = p.S("proporcao");
        if (p.S("ajuste").Length > 0) proj.Fit = p.S("ajuste");
        if (p.S("velocidade").Length > 0) foreach (var c in proj.Clips) c.Speed = Math.Clamp(p.D("velocidade", 1), 0.25, 4);
        if (p.S("volume").Length > 0) foreach (var c in proj.Clips) c.Volume = Math.Clamp(p.D("volume", 100) / 100, 0, 2);
        return proj;
    }

    public static Task<string?> Render(string[] files, P p) => RenderProject(FromParams(files, p), p.S("nome"));

    /// Tamanho final: proporção escolhida com base no primeiro vídeo (sem passar de 1080 no lado menor).
    public static (int w, int h) OutputSize(EditProject proj)
    {
        int sw = 1920, sh = 1080;
        var firstVideo = proj.Clips.FirstOrDefault(c => !c.Photo);
        var first = firstVideo ?? proj.Clips.FirstOrDefault();
        if (first != null)
        {
            var (w, h) = first.Photo ? PhotoSize(first.Src) : Ff.Size(first.Src);
            if (!first.Photo) w = (int)Math.Round(w * Sar(first.Src)); // tamanho de exibição, não de armazenamento
            var rot = first.Photo ? 0 : Rotation(first.Src);
            if (rot is 90 or 270) (w, h) = (h, w);
            if (w > 0 && h > 0) { sw = w; sh = h; }
        }
        int shortSide = Math.Min(1080, Math.Min(sw, sh));
        (int w, int h) r = proj.Ratio switch
        {
            "9:16" => (shortSide, shortSide * 16 / 9),
            "1:1" => (shortSide, shortSide),
            "16:9" => (shortSide * 16 / 9, shortSide),
            _ => (sw, sh),
        };
        if (proj.Ratio == "original" && Math.Max(r.w, r.h) > 3840)
        {
            double k = 3840.0 / Math.Max(r.w, r.h);
            r = ((int)(r.w * k), (int)(r.h * k));
        }
        return (Math.Max(2, r.w & ~1), Math.Max(2, r.h & ~1));
    }

    /// Tamanho da foto já em pé (a câmera grava deitado e marca a orientação no EXIF).
    static (int, int) PhotoSize(string f)
    {
        try
        {
            using var img = new ImageMagick.MagickImage();
            img.Ping(f);
            bool turned = img.Orientation.ToString() is "RightTop" or "LeftBottom" or "LeftTop" or "RightBottom";
            return turned ? ((int)img.Height, (int)img.Width) : ((int)img.Width, (int)img.Height);
        }
        catch { return (0, 0); }
    }

    /// Proporção do pixel (vídeo anamórfico de DVD/câmera: 720×576 exibido em 16:9). 1 se for pixel quadrado.
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, double> sars = new(StringComparer.OrdinalIgnoreCase);
    public static double Sar(string f) => sars.GetOrAdd(f, src =>
    {
        try
        {
            var t = Proc.RunOutput(Ff.Probe!, new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=sample_aspect_ratio", "-of", "default=nw=1:nk=1", src }).Trim();
            var p = t.Split(':');
            if (p.Length == 2 && double.TryParse(p[0], NumberStyles.Float, Inv, out var a) && double.TryParse(p[1], NumberStyles.Float, Inv, out var b) && a > 0 && b > 0)
                return Math.Clamp(a / b, 0.25, 4);
        }
        catch { }
        return 1;
    });

    /// Giro gravado pelo celular (os vídeos em pé costumam vir deitados com rotação de 90°).
    static int Rotation(string f)
    {
        try
        {
            var t = Proc.RunOutput(Ff.Probe!, new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream_side_data=rotation:stream_tags=rotate", "-of", "default=nw=1:nk=1", f });
            foreach (var line in t.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (int.TryParse(line, out var r)) return ((r % 360) + 360) % 360;
        }
        catch { }
        return 0;
    }

    /// Quadros por segundo do primeiro vídeo (24 a 60; 30 se não der para ler).
    public static double Fps(EditProject proj)
    {
        var v = proj.Clips.FirstOrDefault(c => !c.Photo);
        if (v == null) return 30;
        try
        {
            var t = Proc.RunOutput(Ff.Probe!, new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=avg_frame_rate,r_frame_rate", "-of", "default=nw=1:nk=1", v.Src }).Trim();
            foreach (var line in t.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split('/');
                if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, Inv, out var a) && double.TryParse(parts[1], NumberStyles.Float, Inv, out var b) && b > 0 && a > 0)
                    return Math.Clamp(Math.Round(a / b, 3), 24, 60);
            }
        }
        catch { }
        return 30;
    }

    static string N(double d) => d.ToString("0.###", Inv);

    /// atempo aceita de 0,5 a 2 por filtro: encadeia para chegar a 0,25–4.
    public static string Atempo(double f)
    {
        var parts = new List<string>();
        while (f > 2.0) { parts.Add("atempo=2.0"); f /= 2.0; }
        while (f < 0.5) { parts.Add("atempo=0.5"); f /= 0.5; }
        if (Math.Abs(f - 1) > 0.0001) parts.Add($"atempo={N(f)}");
        return string.Join(",", parts);
    }

    public static async Task<string?> RenderProject(EditProject proj, string? name = null)
    {
        if (!Ff.Ok) throw new Exception("ffmpeg não encontrado");
        foreach (var c in proj.Clips) { c.Speed = Math.Clamp(c.Speed, 0.25, 4); c.Volume = Math.Clamp(c.Volume, 0, 2); }
        // Arquivo que sumiu (apagado ou movido depois de entrar na linha do tempo): avisa em vez de exportar faltando pedaço.
        if (proj.Clips.FirstOrDefault(c => !File.Exists(c.Src)) is { } gone)
            throw new Exception($"Arquivo não encontrado: {Path.GetFileName(gone.Src)}");
        // Só some o que dura menos que um quadro no vídeo final.
        var clips = proj.Clips.Where(c => c.Len >= 0.04).ToList();
        if (clips.Count == 0) throw new Exception("A linha do tempo está vazia");
        var tmp = Paths.Temp();
        try { return await RenderClips(proj, clips, name, tmp); }
        finally { Paths.TryDelete(tmp); }
    }

    /// Foto vira PNG em pé e sem transparência (fundo preto, igual à prévia): o ffmpeg não lê HEIC/AVIF de câmera
    /// nem aplica a orientação do EXIF.
    static string PhotoInput(string src, string tmp, int i)
    {
        var png = Path.Combine(tmp, $"foto{i}.png");
        using var img = new ImageMagick.MagickImage(src);
        img.AutoOrient();
        if (img.HasAlpha) { img.BackgroundColor = ImageMagick.MagickColors.Black; img.Alpha(ImageMagick.AlphaAction.Remove); }
        if (Math.Max(img.Width, img.Height) > 4096) img.Resize(new ImageMagick.MagickGeometry(4096, 4096));
        img.Write(png, ImageMagick.MagickFormat.Png);
        return png;
    }

    static async Task<string?> RenderClips(EditProject proj, List<Clip> clips, string? name, string tmp)
    {

        var (W, H) = OutputSize(proj);
        var fps = Fps(proj);
        bool fill = proj.Fit != "caber";
        string fit = fill
            ? $"scale={W}:{H}:force_original_aspect_ratio=increase:flags=lanczos,crop={W}:{H}"
            : $"scale={W}:{H}:force_original_aspect_ratio=decrease:flags=lanczos,pad={W}:{H}:(ow-iw)/2:(oh-ih)/2:color=black";

        var a = new List<string>();
        var fc = new StringBuilder();
        int input = 0;
        var reused = new HashSet<int>();
        foreach (var group in clips.Select((clip, index) => (clip, index)).Where(c => !c.clip.Photo)
            .GroupBy(c => c.clip.Src, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            a.AddRange(new[] { "-i", group.Key });
            fc.Append($"[{input}:v]split={group.Count()}");
            foreach (var c in group) { fc.Append($"[rv{c.index}]"); reused.Add(c.index); }
            fc.Append(';');
            var audible = group.Where(c => c.clip.Volume > 0 && Ff.HasAudio(c.clip.Src)).ToList();
            if (audible.Count > 0)
            {
                fc.Append($"[{input}:a:0]asplit={audible.Count}");
                foreach (var c in audible) fc.Append($"[ra{c.index}]");
                fc.Append(';');
            }
            input++;
        }
        for (int i = 0; i < clips.Count; i++)
        {
            var c = clips[i];
            double len = c.Out - c.In;
            if (c.Photo)
            {
                a.AddRange(new[] { "-loop", "1", "-framerate", N(fps), "-t", N(c.Len), "-i", PhotoInput(c.Src, tmp, i) });
                fc.Append($"[{input++}:v]{fit},setsar=1,fps={N(fps)},format=yuv420p,trim=duration={N(c.Len)},setpts=PTS-STARTPTS[v{i}];");
                fc.Append($"anullsrc=r=48000:cl=stereo,atrim=duration={N(c.Len)}[a{i}];");
                continue;
            }
            // -ss antes do -i: rápido, e preciso porque recodifica.
            var videoInput = $"[{input}:v]";
            var audioInput = $"[{input}:a:0]";
            if (reused.Contains(i))
            {
                videoInput = $"[rv{i}]trim=start={N(c.In)}:duration={N(len)},";
                audioInput = $"[ra{i}]atrim=start={N(c.In)}:duration={N(len)},asetpts=PTS-{N(c.In)}/TB,";
            }
            else
            {
                a.AddRange(new[] { "-ss", N(c.In), "-t", N(len), "-i", c.Src });
                input++;
            }
            var sp = Math.Abs(c.Speed - 1) > 0.0001 ? $"setpts=(PTS-STARTPTS)/{N(c.Speed)}" : "setpts=PTS-STARTPTS";
            // Pixel não quadrado (anamórfico) vira quadrado antes de encaixar, sem espremer a imagem.
            var square = Math.Abs(Sar(c.Src) - 1) > 0.01 ? "scale='trunc(iw*sar/2)*2':ih,setsar=1," : "";
            fc.Append($"{videoInput}{sp},{square}{fit},setsar=1,fps={N(fps)},format=yuv420p,tpad=stop_mode=clone:stop=-1,trim=duration={N(c.Len)}[v{i}];");
            if (Ff.HasAudio(c.Src) && c.Volume > 0)
            {
                // first_pts=0: o áudio fica na mesma régua do vídeo (o ponto do corte). Se o som do arquivo começa
                // depois da imagem, entra silêncio no começo em vez de adiantar o som.
                var chain = new List<string> { "aresample=async=1:first_pts=0" };
                var at = Atempo(c.Speed);
                if (at.Length > 0) chain.Add(at);
                if (Math.Abs(c.Volume - 1) > 0.001) chain.Add($"volume={N(c.Volume)}");
                chain.Add("aresample=48000");
                chain.Add("aformat=sample_fmts=fltp:channel_layouts=stereo");
                // garante a mesma duração da imagem (áudio mais curto que o vídeo deixaria a junção fora de sincronia)
                chain.Add($"apad,atrim=duration={N(c.Len)}");
                // apad sozinho não gera amostras quando o corte está inteiro antes/depois da faixa de áudio.
                fc.Append($"{audioInput}{string.Join(",", chain)}[som{i}];");
                fc.Append($"anullsrc=r=48000:cl=stereo,atrim=duration={N(c.Len)}[silencio{i}];[silencio{i}][som{i}]amix=inputs=2:duration=first:normalize=0[a{i}];");
            }
            else fc.Append($"anullsrc=r=48000:cl=stereo,atrim=duration={N(c.Len)}[a{i}];");
        }
        for (int i = 0; i < clips.Count; i++) fc.Append($"[v{i}][a{i}]");
        fc.Append($"concat=n={clips.Count}:v=1:a=1[v][a]");

        var first = clips[0].Src;
        var dst = Paths.Out(first, "mp4", string.IsNullOrWhiteSpace(name) ? "editado" : null, string.IsNullOrWhiteSpace(name) ? null : name);
        var crf = Config.Now.Crf.ToString();
        var graph = Path.Combine(tmp, "filtros.txt");
        File.WriteAllText(graph, fc.ToString(), new UTF8Encoding(false));
        a.AddRange(new[] { "-/filter_complex", graph, "-map", "[v]", "-map", "[a]",
            "-c:v", "libx264", "-preset", "fast", "-crf", crf, "-pix_fmt", "yuv420p", "-r", N(fps),
            "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", dst });
        try { await Ff.Run(a, clips.Sum(c => c.Len)); }
        catch { Paths.TryDelete(dst); throw; }
        return dst;
    }
}
