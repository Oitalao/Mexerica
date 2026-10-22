using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Mexerica;

/// Um destino de conversão oferecido por um módulo.
sealed record Target(string Ext, string Sub, string? Label = null);

/// Módulo de formatos: cada família nova (legendas, dados, fontes, e-mail…) é uma classe que herda daqui.
/// Não precisa registrar em lugar nenhum: o Mexerica encontra sozinho todas as classes concretas.
/// Regras:
///  - extensões sempre sem ponto e minúsculas (ex.: "srt", "tar.gz");
///  - Convert grava ao lado do original com Paths.Out(src, ext) e devolve o caminho gerado
///    (ou a pasta, quando o resultado tem vários arquivos), e lança Exception com mensagem em português se falhar;
///  - usa Job.Progress(0..1) para a barra e Job.Check() para respeitar o cancelamento.
abstract class Module
{
    /// Nome curto para registros e mensagens (ex.: "Legendas").
    public abstract string Name { get; }

    /// Extensões de entrada que este módulo entende.
    public abstract IEnumerable<string> Inputs { get; }

    /// Destinos para um arquivo com esta extensão de entrada.
    public abstract IEnumerable<Target> TargetsFor(string inExt);

    /// Destinos olhando o próprio arquivo (ex.: só oferecer "extrair legenda" se o vídeo tiver legenda).
    /// Seja rápido: roda enquanto a roda abre. Por padrão, só a extensão decide.
    public virtual IEnumerable<Target> TargetsFor(string inExt, string path) => TargetsFor(inExt);

    /// Converte um arquivo.
    public abstract Task<string?> Convert(string src, string outExt, P p);

    /// Ferramentas extras (roda "Ferramentas") para a seleção, se houver.
    public virtual IEnumerable<Op> Tools(string[] files) => Array.Empty<Op>();

    /// Quando a conversão embutida do Mexerica também existe, o módulo só a substitui se devolver true aqui.
    public virtual bool Overrides(string inExt, string outExt) => false;

    /// Falso quando falta algo de que o módulo depende (o destino não aparece na roda).
    public virtual bool Available => true;

    /// Tipo usado para escolher ferramentas e menus quando a extensão não é de nenhum tipo embutido.
    public virtual Kind KindHint => Kind.Other;
}

static class Modules
{
    static List<Module>? all;

    /// Só listando (janela de personalizar): decide pela extensão, sem abrir arquivos.
    [ThreadStatic] public static bool Listing;

    /// Todos os módulos, encontrados por reflexão (uma instância de cada).
    public static IReadOnlyList<Module> All => all ??= typeof(Module).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(Module)) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
        .OrderBy(t => t.FullName, StringComparer.Ordinal)
        .Select(t => (Module)Activator.CreateInstance(t)!)
        .ToList();

    /// Extensão normalizada para os módulos: minúscula, sem ponto, com "tar.gz" e afins juntos.
    public static string Ext(string path)
    {
        var name = Path.GetFileName(path.TrimEnd('\\')).ToLowerInvariant();
        if (name.EndsWith(".kepub.epub")) return "kepub";
        foreach (var two in new[] { "tar.gz", "tar.bz2", "tar.xz", "tar.zst", "tar.lz", "tar.7z" })
            if (name.EndsWith("." + two)) return two;
        return Path.GetExtension(name).TrimStart('.');
    }

    public static IEnumerable<string> InputExtensions => All.SelectMany(m => m.Inputs).Select(e => e.ToLowerInvariant()).Distinct();

    public static bool Claims(string ext) => All.Any(m => m.Available && m.Inputs.Contains(ext, StringComparer.OrdinalIgnoreCase));

    public static Kind? KindFor(string ext) =>
        All.FirstOrDefault(m => m.Inputs.Contains(ext, StringComparer.OrdinalIgnoreCase) && m.KindHint != Kind.Other)?.KindHint;

    /// Destinos comuns a todos os arquivos, com o módulo que faz cada um (o primeiro que oferecer ganha).
    public static List<(Module M, Target T)> TargetsFor(string[] files)
    {
        List<(Module, Target)>? common = null;
        // Muitos arquivos: olha no máximo 20 de perto (o resto pela extensão), para a roda abrir rápido.
        foreach (var (f, i) in files.Select((f, i) => (f, i)))
        {
            var e = Ext(f);
            var here = new List<(Module, Target)>();
            foreach (var m in All)
            {
                if (!m.Available || !m.Inputs.Contains(e, StringComparer.OrdinalIgnoreCase)) continue;
                IEnumerable<Target> ts;
                try { ts = i < 20 && !Listing ? m.TargetsFor(e, f) : m.TargetsFor(e); }
                catch (Exception ex) { Log.Write($"{m.Name} destinos: {ex.Message}"); ts = m.TargetsFor(e); }
                foreach (var t in ts)
                    if (!string.Equals(t.Ext, e, StringComparison.OrdinalIgnoreCase) && !here.Any(h => h.Item2.Ext.Equals(t.Ext, StringComparison.OrdinalIgnoreCase)))
                        here.Add((m, t));
            }
            common = common == null ? here : common.Where(c => here.Any(h => h.Item1 == c.Item1 && h.Item2.Ext == c.Item2.Ext)).ToList();
        }
        return common ?? new();
    }
}
