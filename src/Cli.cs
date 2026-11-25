using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mexerica;

/// Linha de comando: `mexerica converter fotos\*.heic --para jpg`. O instalador põe um mexerica.cmd no PATH,
/// que espera o programa terminar e devolve o código de saída (0 = tudo certo).
static class Cli
{
    public static readonly string[] Verbs = { "converter", "opcoes", "opções", "ferramenta", "acao", "ação", "vigiar", "skill", "doctor", "ajuda", "versao", "versão", "--help", "-h", "/?" };

    [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int n);
    static TextWriter Out = TextWriter.Null;

    public static int Run(string[] args)
    {
        Catalog.Legacy = true;
        var h = GetStdHandle(-11);
        if (h != IntPtr.Zero && h != new IntPtr(-1))
            Out = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true };
        else if (AttachConsole(-1))
        {
            var o = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true };
            Out = o;
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Out.WriteLine();
        }
        try
        {
            // A linha de comando enxerga todas as opções: esconder ou reordenar fatias vale só para a roda.
            var plain = Config.Now.Clone(); plain.WheelHidden = new(); plain.WheelOrder = new(); Config.Override(plain);
            var verb = args[0].ToLowerInvariant();
            var rest = args.Skip(1).ToList();
            return verb switch
            {
                "converter" => Convert(rest),
                "opcoes" or "opções" => Options(rest),
                "ferramenta" => Tool(rest),
                "acao" or "ação" => Act(rest),
                "ajuda" when rest.Count >= 2 => Describe(rest),
                "vigiar" => Watch(rest),
                "doctor" => Doctor(),
                "versao" or "versão" => Say($"Mexerica {Version}"),
                "skill" => rest.Contains("--instalar") ? Run(Skill.Install) : Say(rest.Contains("--referencia") ? Skill.Reference() : Skill.Text()),
                _ => Help(),
            };
        }
        catch (Exception ex) { Out.WriteLine("Erro: " + App.Friendly(ex)); return 2; }
    }

    public static string Version => typeof(Cli).Assembly.GetName().Version?.ToString(3) ?? "";

    static int Say(string s) { Out.WriteLine(s); return 0; }
    static int Run(Action a) { a(); return 0; }

    static int Help() => Say(@"Mexerica pela linha de comando (faz tudo o que a roda faz)

  mexerica opcoes <arquivos...>
      Lista as conversões, ferramentas e ações que valem para esses arquivos.
  mexerica ajuda ""<nome>"" <arquivos...>
      Mostra os ajustes (--p chave=valor) de uma ferramenta ou ação, com os valores padrão.
  mexerica converter <arquivos...> --para <formato> [-o pasta] [--qualidade 1-100]
      Converte cada arquivo (curingas valem: *.heic). Ex.: mexerica converter *.png --para webp --qualidade 80
  mexerica ferramenta ""<nome>"" <arquivos...> [--p chave=valor ...] [-o pasta]
      Aplica uma ferramenta da roda com os mesmos padrões da janela de ajuste.
      Ex.: mexerica ferramenta Dividir contrato.pdf --p modo=intervalos --p ""intervalos=1-3, 4-6"" --p saida=zip
  mexerica acao ""<nome>"" <arquivos...> [--p chave=valor ...]
      Renomear, Copiar, Mover, Caminho, Terminal, Compartilhar, Flutuar.
      Ex.: mexerica acao Renomear *.pdf --p ""modelo=Doc. {n} - {nome}""   |   mexerica acao Mover *.jpg --p destino=D:\Fotos
  mexerica vigiar <pasta> [--acao converter|comprimir|ambos] [--para <formato>] [--mb 4] [--alvo-mb 0] [--subpastas] [--mover-originais]
      Converte e/ou comprime tudo o que chegar na pasta até você apertar Ctrl+C.
  mexerica doctor
      Confere se as ferramentas externas, o menu do botão direito e a pasta de dados estão bem.
  mexerica versao

Código de saída: 0 = tudo certo, 1 = algum arquivo falhou, 2 = erro nos parâmetros.");

    static int Doctor()
    {
        Out.Write(DoctorText(out int problemas));
        return problemas > 0 ? 1 : 0;
    }

    /// Conferência das ferramentas externas; a bandeja mostra o mesmo texto ("Verificar integridade do programa…").
    public static string DoctorText(out int problemas)
    {
        var o = new StringWriter();
        o.WriteLine($"Mexerica {Version}");
        o.WriteLine();
        int n = 0;
        void Check(string nome, bool ok, string paraQue, bool critico = true)
        {
            o.WriteLine((ok ? "✓ " : critico ? "✗ " : "– ") + Tr.T(nome) + (ok ? "" : Tr.T($" — falta; serve para: {Tr.T(paraQue)}")));
            if (!ok && critico) n++;
        }

        Check("FFmpeg / FFprobe", Ff.Ok, "converter ou editar vídeo e áudio");
        Check("Ghostscript", Gs.Ok, "comprimir ou tarjar PDF");
        Check("qpdf", Qpdf.Ok, "organizar, dividir ou juntar PDF");
        Check("ExifTool", Meta.Ok, "ver ou editar metadados");
        Check("Remover fundo (isnet-general-use)", Bg.Ok, "remover fundo de foto", critico: false);
        Check("LibreOffice", Office.Ok, "converter documentos com mais fidelidade (sem ele o Mexerica converte por conta própria)", critico: false);

        var enc = Hw.Encoder;
        o.WriteLine((enc != null ? "✓ " : "– ") + Tr.T("Vídeo pela placa de vídeo") + (enc != null ? $" ({enc})" : Tr.T(" — vai pelo processador (mais lento, mas funciona)")));

        bool menuAtivo = false;
        try { using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Mexerica"); menuAtivo = k?.GetValue("MenuAssinatura") != null; } catch { }
        if (!Config.Now.ShellMenu) o.WriteLine("– " + Tr.T("Menu do botão direito — desligado nas configurações"));
        else Check("Menu do botão direito", menuAtivo, "usar o Mexerica pelo botão direito do Explorador; abra o programa de novo para registrar");

        bool dadosOk = false;
        try { Directory.CreateDirectory(Paths.Data); var t = Path.Combine(Paths.Data, ".teste-doctor"); File.WriteAllText(t, ""); File.Delete(t); dadosOk = true; } catch { }
        Check(Tr.T($"Pasta de dados gravável ({Paths.Data})"), dadosOk, "salvar configurações, histórico e o log");

        o.WriteLine();
        o.WriteLine(n == 0 ? Tr.T("Tudo certo.") : Tr.T($"{n} problema(s) acima."));
        problemas = n;
        return o.ToString();
    }

    /// Separa arquivos (expandindo curingas) de opções --nome valor.
    static (List<string> files, Dictionary<string, string> opts, P p) Parse(List<string> a)
    {
        var files = new List<string>();
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var p = new P();
        bool literal = false;
        for (int i = 0; i < a.Count; i++)
        {
            var x = a[i];
            if (!literal && x == "--") literal = true;
            else if (!literal && x == "--p" && i + 1 < a.Count) { var kv = a[++i].Split('=', 2); p[kv[0]] = kv.Length > 1 ? kv[1] : ""; }
            else if (!literal && x is "--subpastas" or "--mover-originais") opts[x.TrimStart('-')] = "1";
            else if (!literal && x.StartsWith("-") && i + 1 < a.Count) opts[x.TrimStart('-')] = a[++i];
            else files.AddRange(Expand(x).Select(Robust.Resolve));
        }
        return (files, opts, p);
    }

    static IEnumerable<string> Expand(string pattern)
    {
        if (pattern.IndexOfAny(new[] { '*', '?' }) < 0) { yield return Path.GetFullPath(pattern); yield break; }
        var dir = Path.GetDirectoryName(pattern);
        dir = string.IsNullOrEmpty(dir) ? Directory.GetCurrentDirectory() : Path.GetFullPath(dir);
        foreach (var f in Directory.EnumerateFileSystemEntries(dir, Path.GetFileName(pattern)).OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase))
            yield return f;
    }

    static void ApplyOptions(Dictionary<string, string> opts)
    {
        var c = Config.Now.Clone();
        if (opts.TryGetValue("o", out var o) || opts.TryGetValue("saida", out o) || opts.TryGetValue("saída", out o))
        {
            o = Path.GetFullPath(o);
            Directory.CreateDirectory(o);
            c.OutputDir = o;
        }
        if (opts.TryGetValue("qualidade", out var q) && int.TryParse(q, out var qi)) c.ImageQuality = Math.Clamp(qi, 1, 100);
        Config.Override(c);
    }

    static Op? Find(Mode mode, string[] files, string label) =>
        Catalog.For(mode, files).FirstOrDefault(op => string.Equals(op.Label, label, StringComparison.OrdinalIgnoreCase))
        ?? Catalog.For(mode, files).FirstOrDefault(op => string.Equals(op.Label.Replace(" ", ""), label.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

    static int Convert(List<string> a)
    {
        var (files, opts, p) = Parse(a);
        if (files.Count == 0 || !opts.TryGetValue("para", out var to)) { Help(); return 2; }
        ApplyOptions(opts);
        int fails = 0;
        // Arquivos de tipos diferentes: cada um com a sua própria roda.
        foreach (var f in files)
        {
            if (!File.Exists(f) && !Directory.Exists(f))
            {
                Out.WriteLine($"✗ {Path.GetFileName(f)}: O arquivo não existe mais (foi movido ou apagado?)");
                fails++;
                continue;
            }
            var op = Find(Mode.Convert, new[] { f }, to.TrimStart('.'));
            if (op?.One == null)
            {
                bool same = string.Equals(Path.GetExtension(f).TrimStart('.'), to.TrimStart('.'), StringComparison.OrdinalIgnoreCase);
                Out.WriteLine(same ? $"– {Path.GetFileName(f)}: já está em {to.ToUpperInvariant()}" : $"✗ {Path.GetFileName(f)}: não converte para {to.ToUpperInvariant()}");
                if (!same) fails++;
                continue;
            }
            if (op.Ui != null) { Gui(Mode.Convert, op.Label, new[] { f }); continue; }
            var pf = Prepare(op, new[] { f }, p, out var err);
            if (pf == null) { Out.WriteLine($"✗ {Path.GetFileName(f)}: {err}"); fails++; continue; }
            fails += RunOne(op, new[] { f }, pf);
        }
        return fails > 0 ? 1 : 0;
    }

    static int Tool(List<string> a)
    {
        if (a.Count < 2) { Help(); return 2; }
        var name = a[0];
        var (files, opts, p) = Parse(a.Skip(1).ToList());
        ApplyOptions(opts);
        var fs = files.ToArray();
        var op = Find(Mode.Tools, fs, name);
        if (op == null) { Out.WriteLine($"A ferramenta “{name}” não vale para esses arquivos. Veja: mexerica opcoes <arquivos>"); return 2; }
        if (op.Ui != null) return Gui(Mode.Tools, op.Label, fs);
        // Metadados sem nada para mudar: mostra, como a janela faz ao abrir.
        if (op.Label == "Metadados" && p.Count == 0)
        {
            foreach (var f in fs)
            {
                if (fs.Length > 1) Out.WriteLine($"== {Path.GetFileName(f)}");
                foreach (var (g, tag, v) in Meta.Read(f)) Out.WriteLine($"{g}:{tag} = {v}");
            }
            return 0;
        }
        var pf = Prepare(op, fs, p, out var err);
        if (pf == null) { Out.WriteLine("✗ " + err); return 2; }
        return RunOne(op, fs, pf) > 0 ? 1 : 0;
    }

    /// Ações da roda. As que só existem na tela (Compartilhar, Flutuar) abrem no Mexerica da bandeja.
    static int Act(List<string> a)
    {
        if (a.Count < 2) { Help(); return 2; }
        var name = a[0];
        var (files, opts, p) = Parse(a.Skip(1).ToList());
        var fs = files.ToArray();
        var op = Find(Mode.Actions, fs, name);
        if (op == null) { Out.WriteLine($"A ação “{name}” não existe. Veja: mexerica opcoes <arquivos>"); return 2; }
        switch (op.Label)
        {
            case "Renomear":
            {
                var pf = Defaults(Dialogs.Rename, fs, p, out var err);
                if (pf == null) { Out.WriteLine("✗ " + err); return 2; }
                var plan = Actions.RenamePlan(Form.Ordered(fs, pf), pf).Where(x => !string.Equals(x.from, x.to, StringComparison.Ordinal)).ToList();
                if (plan.Count == 0) return Say("Nada a fazer: os nomes já estavam assim.");
                foreach (var (from, to) in Actions.Apply(plan)) Out.WriteLine($"✓ {Path.GetFileName(from)} → {to}");
                return 0;
            }
            case "Copiar" or "Mover":
            {
                var dest = p.S("destino", opts.GetValueOrDefault("o") ?? opts.GetValueOrDefault("destino") ?? "");
                if (dest.Length == 0) { Out.WriteLine("Diga para onde: --p destino=<pasta>"); return 2; }
                dest = Path.GetFullPath(dest);
                Directory.CreateDirectory(dest);
                foreach (var to in Actions.CopyMove(fs, dest, op.Label == "Mover")) Out.WriteLine("✓ " + to);
                return 0;
            }
            case "Caminho":
                System.Windows.Clipboard.SetText(string.Join("\r\n", fs));
                foreach (var f in fs) Out.WriteLine(f);
                return 0;
            case "Terminal":
                Actions.Terminal(fs);
                return 0;
            default:
                return Gui(Mode.Actions, op.Label, fs);
        }
    }

    /// Entrega ao Mexerica da bandeja (abre se estiver fechado) o que precisa de janela: a mesma via do menu do botão direito.
    static int Gui(Mode mode, string label, string[] fs)
    {
        var list = Path.Combine(Path.GetTempPath(), $"mexerica-{Guid.NewGuid():N}.txt");
        File.WriteAllLines(list, fs, new System.Text.UTF8Encoding(false));
        var psi = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        foreach (var x in new[] { "--menu-lista", mode.ToString(), label, list }) psi.ArgumentList.Add(x);
        Process.Start(psi);
        return Say($"Aberto no Mexerica: {label}");
    }

    /// Valores da janela de ajuste (sem abri-la) com os dados por --p por cima, conferidos como o botão Fazer confere.
    static P? Defaults(Func<string[], P?> ask, string[] fs, P given, out string? err)
    {
        err = null;
        var forms = new List<Form>();
        Form.Headless = forms;
        try { ask(fs); }
        finally { Form.Headless = null; }
        var merged = new P();
        if (forms.Count == 0) { foreach (var (k, v) in given) merged[k] = v; return merged; } // janela própria: só o que veio por --p
        err = forms[0].Validate(given);
        if (err != null) { err = Tr.T(err); return null; }
        foreach (var (k, v) in forms[0].Values) merged[k] = v;
        return merged;
    }

    static P? Prepare(Op op, string[] fs, P given, out string? err)
    {
        err = null;
        return op.Ask == null ? given : Defaults(op.Ask, fs, given, out err);
    }

    /// mexerica ajuda <nome> <arquivos>: os ajustes que a janela mostraria, como --p.
    static int Describe(List<string> a)
    {
        var name = a[0];
        var (files, _, _) = Parse(a.Skip(1).ToList());
        var fs = files.ToArray();
        foreach (var mode in new[] { Mode.Tools, Mode.Convert, Mode.Actions })
        {
            var op = Find(mode, fs, name);
            if (op == null) continue;
            Out.WriteLine($"{op.Label} ({op.Sub})");
            Out.WriteLine(Settings(mode, op, fs));
            return 0;
        }
        Out.WriteLine($"“{name}” não vale para esses arquivos. Veja: mexerica opcoes <arquivos>");
        return 2;
    }

    /// Os ajustes de uma opção como linhas "--p chave=…" (a ajuda e a instrução para IAs usam o mesmo texto).
    public static string Settings(Mode mode, Op op, string[] fs)
    {
        Func<string[], P?>? ask = op.Ask ?? (op.Label == "Renomear" && mode == Mode.Actions ? Dialogs.Rename : null);
        var forms = new List<Form>();
        bool failed = false;
        if (ask != null) { Form.Headless = forms; try { ask(fs); } catch { failed = true; } finally { Form.Headless = null; } }
        if (forms.Count > 0)
            return string.Join("\n", forms[0].Fields.Select(f =>
                $"  --p {f.key}=…   {f.label} ({f.kind}{(f.vals.Length > 0 ? ": " + f.vals : "")}){(f.def.Length > 0 ? $" [padrão: {f.def}]" : "")}{(f.when != null ? $" — só com {f.when}" : "")}"));
        if (Manual(op.Label, fs) is { } m) return m;
        if (failed) return "  Os ajustes dependem do arquivo: mexerica ajuda \"" + op.Label + "\" <arquivo>";
        return op.Ui != null && mode != Mode.Actions ? "  Abre uma janela no Mexerica." : "  Sem ajustes.";
    }

    /// Ajustes das ferramentas que têm janela própria (editor, linha do tempo, páginas), em vez de formulário.
    public static string? Manual(string label, string[] fs)
    {
        var k = fs.Length > 0 ? Catalog.KindOf(fs[0]) : Kind.Other;
        bool media = k is Kind.Audio or Kind.Video;
        return label switch
        {
            "Editar" when k == Kind.Video => "  sem --p: junta todos os arquivos inteiros, na ordem dada\n  --p \"trechos=0:00-0:10;0:30-0:45\"   (pedaços do primeiro arquivo que ficam; o resto sai)\n  --p projeto=<arquivo .json>   (linha do tempo salva: {\"Clips\":[{\"Src\":\"…\",\"In\":0,\"Out\":5,\"Speed\":1,\"Volume\":1,\"Photo\":false}]})\n  --p proporcao=original|9:16|1:1|16:9   --p ajuste=preencher|caber\n  --p velocidade=0.25 a 4   --p volume=0 a 200   (todos os pedaços)",
            "Editar" => "  --p recorte=x,y,largura,altura   (pixels da imagem)\n  --p tarjas=\"x,y,l,a,modo;...\"   (modo: preto | desfoque | pixel)\n  --p brilho= contraste= saturacao=   (-100 a 100)   --p nitidez=   (0 a 100)\n  --p giro=0|90|180|270",
            "Recortar" => "  --p recorte=x,y,largura,altura   (pixels do vídeo)",
            "Cortar" => "  --p inicio=segundos --p fim=segundos\n  --p modo=preciso (padrão, recodifica) | rapido (copia, corta no quadro-chave)",
            "Dividir" when media => "  --p modo=partes --p partes=2   (partes iguais, padrão)\n  --p modo=cada --p cada=60   (a cada tantos segundos)\n  --p modo=pontos --p \"pontos=1:30;3:00\"   (tempos de corte, segundos ou mm:ss)",
            "Quadro" => "  --p tempo=segundos   --p formato=jpg|png",
            "Bipe" => "  --p \"trechos=0:10-0:12;1:05-1:06\"   (segundos ou mm:ss)\n  --p freq=1000   (Hz)",
            "Organizar" => "  --p ordem=3,1,2r90,5r180   (páginas na nova ordem; rN gira; as que ficarem fora saem)",
            "Tarjar" => "  --p \"tarjas=pagina:x,y,l,a;...\"   (x,y,l,a em fração da página, de 0 a 1, a partir do canto de cima)",
            "Metadados" => "  sem --p: mostra os metadados\n  --p limpar=1   (tira tudo)   --p semgps=1   (tira só a localização)\n  --p m.Campo=valor   (altera um campo; veja os nomes na lista)",
            "Copiar" or "Mover" => "  --p destino=<pasta>",
            "Caminho" => "  Mostra os caminhos e copia para a área de transferência.",
            "Terminal" => "  Abre o terminal na pasta do arquivo.",
            "Compartilhar" or "Flutuar" => "  Abre no Mexerica (precisa de tela).",
            _ => null,
        };
    }

    static int Options(List<string> a)
    {
        var (files, _, _) = Parse(a);
        if (files.Count == 0) { Help(); return 2; }
        var fs = files.ToArray();
        Out.WriteLine("Converter para: " + string.Join(", ", Catalog.For(Mode.Convert, fs).Select(o => o.Label)));
        Out.WriteLine("Ferramentas:    " + string.Join(", ", Catalog.For(Mode.Tools, fs).Select(o => o.Label)));
        Out.WriteLine("Ações:          " + string.Join(", ", Catalog.For(Mode.Actions, fs).Select(o => o.Label)));
        Out.WriteLine("Ajustes de cada uma: mexerica ajuda \"<nome>\" <arquivos>");
        return 0;
    }

    /// Roda uma operação sem tela, mostrando cada resultado. Devolve o número de falhas.
    static int RunOne(Op op, string[] files, P p)
    {
        // Como App.Run: ordem escolhida e lista dos arquivos (o Carimbar numera por ela).
        if (p.ContainsKey("ordem")) files = Form.Ordered(files, p);
        p["arquivos"] = string.Join("\n", files);
        var started = DateTime.Now.AddSeconds(-2);
        var job = new Job(); Job.Current = job;
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; job.Cts.Cancel(); };
        var outputs = new List<string>();
        var errors = new List<string>();
        try
        {
            if (op.All != null) { var r = op.All(files, p).GetAwaiter().GetResult(); if (r != null) outputs.Add(r); }
            else App.EachPublic(op, files, p, job, outputs, errors).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) { Out.WriteLine("Cancelado."); return 1; }
        catch (Exception ex) { errors.Add(App.Friendly(ex)); }
        if (op.Show != null) { foreach (var o in outputs) Out.WriteLine(o); outputs.Clear(); }
        foreach (var o in outputs) Out.WriteLine("✓ " + o);
        // Partes ao lado do original (Dividir, Limite): mostra todas, não só a primeira.
        foreach (var o in outputs.ToList())
        {
            var n = Path.GetFileNameWithoutExtension(o);
            int at = n.LastIndexOf(" - parte 1 de ", StringComparison.Ordinal);
            if (at <= 0 || !File.Exists(o)) continue;
            foreach (var sib in Directory.GetFiles(Path.GetDirectoryName(o)!, n[..at] + " - parte * de " + new string(n[(at + 14)..].TakeWhile(char.IsDigit).ToArray()) + "*" + Path.GetExtension(o))
                .Where(s => !string.Equals(s, o, StringComparison.OrdinalIgnoreCase) && File.GetLastWriteTime(s) >= started).OrderBy(s => int.TryParse(System.Text.RegularExpressions.Regex.Match(Path.GetFileName(s), @" - parte (\d+) de ").Groups[1].Value, out var k) ? k : 0))
                Out.WriteLine("✓ " + sib);
        }
        foreach (var warning in job.Warnings) Out.WriteLine("– " + warning);
        foreach (var e in errors) Out.WriteLine("✗ " + e);
        if (outputs.Count > 0) History.Add(op.Label, files, outputs);
        return errors.Count;
    }

    static int Watch(List<string> a)
    {
        var (files, opts, _) = Parse(a);
        var acao = (opts.GetValueOrDefault("acao") ?? opts.GetValueOrDefault("ação") ?? "converter").ToLowerInvariant();
        opts.TryGetValue("para", out var to);
        if (files.Count != 1 || !Directory.Exists(files[0]) || acao is not ("converter" or "comprimir" or "ambos") || (acao != "comprimir" && to == null)) { Help(); return 2; }
        static double Num(string? s, double def) => s == null ? def :
            double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) && d >= 0
                ? d : throw new ArgumentException("Tamanho inválido: use um número maior ou igual a zero.");
        var w = new WatchFolder
        {
            Folder = files[0], Action = acao, Target = (to ?? "PDF").TrimStart('.').ToUpperInvariant(),
            MinMb = Num(opts.GetValueOrDefault("mb"), 4), TargetMb = Num(opts.GetValueOrDefault("alvo-mb"), 0),
            Subfolders = opts.ContainsKey("subpastas"), MoveOriginals = opts.ContainsKey("mover-originais"),
        };
        using var watcher = new FolderWatcher(w, (src, res, err) =>
        {
            if (res != null) Out.WriteLine($"✓ {Path.GetFileName(src)} → {res}");
            else Out.WriteLine($"✗ {Path.GetFileName(src)}: {err}");
        });
        Out.WriteLine($"Vigiando {w.Folder} → {(acao == "comprimir" ? "comprimir" : acao == "ambos" ? w.Target + " + comprimir" : w.Target)}. Ctrl+C para parar.");
        var stop = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait();
        return 0;
    }
}
