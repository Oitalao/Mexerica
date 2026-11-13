using System;
using System.IO;
using System.Linq;

namespace Mexerica;

/// Janelinhas de ajuste das ferramentas simples.
static class Dialogs
{
    static readonly (string, string[]) Nivel = ("modo", new[] { "nivel" });

    /// Pede a senha de um PDF protegido. Devolve null se o usuário cancelar.
    public static string? PdfPassword(string[] fs, bool retry = false)
    {
        var f = new Form("Senha do PDF", fs, retry ? "Senha incorreta. Tente de novo." : "Esse PDF tem senha. Digite a senha para abrir e converter.");
        f.Text("senha", "Senha", "", validate: v => string.IsNullOrEmpty(v) ? "Digite a senha." : null, width: 220);
        var p = f.Run();
        return p?.S("senha");
    }

    public static P? Compress(string[] fs, string what)
    {
        long size = fs.Sum(f => new FileInfo(f).Length);
        var f = new Form("Comprimir", fs, $"Tamanho atual: {Human(size)}" + (fs.Length > 1 ? " no total" : ""));
        f.Choice("modo", "Como", new[] { ("nivel", "Por nível"), ("pct", "Para uma porcentagem do tamanho"), ("mb", "Para um tamanho exato") }, "nivel");
        f.Choice("nivel", "Nível", new[] { ("leve", "Leve (quase sem perda)"), ("media", "Equilibrado"), ("forte", "Forte (bem menor)") }, "media", Nivel);
        f.Number("pct", "Tamanho final", 50, 1, 99, "% do original", ("modo", new[] { "pct" }));
        double defMb = Math.Max(0.1, Math.Round(size / (double)fs.Length / 1024 / 1024 / 2, 1));
        f.Number("mb", fs.Length > 1 ? "Tamanho máximo de cada um" : "Tamanho máximo", defMb, 0.01, 100000, "MB", ("modo", new[] { "mb" }), decimals: true);
        if (what == "video") f.Note("Por tamanho exato o vídeo é feito em duas passadas: demora mais, mas acerta o tamanho.");
        if (what == "pdf") f.Note("Reduz a resolução das imagens do PDF; o texto continua texto.");
        return f.Run();
    }

    public static P? ResizeImage(string[] fs)
    {
        var f = new Form("Tamanho", fs);
        f.Choice("modo", "Como", new[] { ("pct", "Porcentagem"), ("largura", "Largura"), ("altura", "Altura"), ("lado", "Lado maior") }, "pct");
        f.Number("valor", "Valor", 50, 1, 20000, "% ou pixels");
        return f.Run();
    }

    public static P? Rotate(string[] fs, string what)
    {
        var f = new Form("Girar", fs);
        f.Choice("giro", "Como", new[] { ("90", "90° à direita"), ("270", "90° à esquerda"), ("180", "180°"), ("h", "Espelhar na horizontal"), ("v", "Espelhar na vertical") }, "90");
        return f.Run();
    }

    public static P? Background(string[] fs)
    {
        var f = new Form("Fundo", fs, "Põe uma cor atrás da imagem (útil para PNG transparente) e, se quiser, uma margem ou um formato.");
        f.Color("cor", "Cor", "#FFFFFF");
        f.Number("margem", "Margem", 0, 0, 100, "% do lado maior");
        f.Choice("formato", "Formato", new[] { ("original", "Do jeito que está"), ("quadrado", "Quadrado"), ("4:5", "4:5 (retrato)"), ("16:9", "16:9 (paisagem)"), ("9:16", "9:16 (story)") }, "original");
        return f.Run();
    }

    /// Nome do arquivo que resulta de juntar vários. Sem perguntar (opção desligada), usa o nome do primeiro arquivo.
    public static P? MergeName(string[] fs)
    {
        if (!Config.Now.AskMergeName) return new P();
        var f = new Form("Juntar", fs);
        var first = Path.GetFileNameWithoutExtension(fs[0].TrimEnd('\\'));
        if (fs.Length > 1) f.Order("ordem", fs, "Ordem no arquivo novo");
        f.Text("nome", "Nome do arquivo novo", first, width: 230);
        f.Check("nao_perguntar", "Não perguntar de novo (usar o nome do primeiro arquivo)", false);
        f.Note("Para voltar a perguntar: Configurações → Pastas.");
        var p = f.Run();
        if (p == null) return null;
        // Mudou a ordem e não mexeu no nome: o nome acompanha o novo primeiro arquivo.
        var ordered = Form.Ordered(fs, p);
        if (p.S("nome") == first && ordered[0] != fs[0]) p["nome"] = Path.GetFileNameWithoutExtension(ordered[0].TrimEnd('\\'));
        if (p.S("nao_perguntar") == "1")
        {
            var c = Config.Now.Clone();
            c.AskMergeName = false;
            c.Save();
        }
        return p;
    }

    /// Limite de tamanho do sistema de envio (tribunal, e-mail, formulário).
    public static P? SizeLimit(string[] fs)
    {
        var f = new Form("Limite de envio", fs);
        f.Choice("mb", "Cada arquivo com no máximo", new[] { ("1", "1 MB"), ("2", "2 MB"), ("3", "3 MB"), ("5", "5 MB"), ("10", "10 MB"), ("15", "15 MB"), ("20", "20 MB"), ("25", "25 MB (e-mail)"), ("30", "30 MB"), ("outro", "Outro") }, "5");
        f.Number("outro", "Tamanho", 5, 0.2, 2000, "MB", ("mb", new[] { "outro" }), decimals: true);
        f.Note("Primeiro comprime. Se mesmo assim não couber, divide em partes (nome - parte 1 de 3).");
        var p = f.Run();
        if (p != null && p.S("mb") == "outro") p["mb"] = p.S("outro");
        return p;
    }

    /// Número de página e carimbo ("Doc. 01") no canto das páginas.
    public static P? Stamp(string[] fs)
    {
        var f = new Form("Carimbar", fs);
        if (fs.Length > 1) f.Order("ordem", fs, "Ordem dos documentos");
        f.Text("carimbo", "Carimbo ({d} = número)", "Doc. {d}", width: 200);
        f.Number("inicio", "Começar no número", 1, 0, 99999);
        f.Choice("digitos", "Algarismos", new[] { ("1", "1, 2, 3"), ("2", "01, 02, 03"), ("3", "001, 002, 003") }, "2");
        f.Check("numerar", "Numerar as páginas", true);
        f.Choice("formato", "Número da página", new[] { ("{n}", "1"), ("{n}/{total}", "1/10"), ("Página {n} de {total}", "Página 1 de 10"), ("Fls. {n}", "Fls. 1") }, "Página {n} de {total}");
        f.Choice("posicao", "Posição", new[] { ("sd", "Em cima, à direita"), ("se", "Em cima, à esquerda"), ("sc", "Em cima, no meio"), ("id", "Embaixo, à direita"), ("ie", "Embaixo, à esquerda"), ("ic", "Embaixo, no meio") }, "sd");
        f.Number("tamanho", "Tamanho da letra", 10, 6, 48, "pt");
        f.Note("Deixe o carimbo vazio para só numerar as páginas. Com vários PDFs, cada um ganha o número seguinte.");
        return f.Run();
    }

    /// Senha para abrir o PDF.
    public static P? PdfProtect(string[] fs)
    {
        var f = new Form("Pôr senha", fs);
        f.Text("senha", "Senha", "", validate: v => v.Length < 4 ? "Use uma senha com pelo menos 4 caracteres." : null, width: 200);
        f.Text("senha2", "Repita a senha", "", width: 200);
        f.Note("Guarde a senha: sem ela, ninguém abre o arquivo (nem o Mexerica).");
        while (true)
        {
            var p = f.Run();
            if (p == null) return null;
            if (p.S("senha") == p.S("senha2")) return p;
            Tr.Box("As duas senhas não são iguais.", "Mexerica", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            f = new Form("Pôr senha", fs);
            f.Text("senha", "Senha", "", validate: v => v.Length < 4 ? "Use uma senha com pelo menos 4 caracteres." : null, width: 200);
            f.Text("senha2", "Repita a senha", "", width: 200);
        }
    }

    /// Renomear vários arquivos de uma vez, com número e na ordem escolhida.
    public static P? Rename(string[] fs)
    {
        var f = new Form("Renomear", fs);
        if (fs.Length > 1) f.Order("ordem", fs, "Ordem");
        f.Text("modelo", "Novo nome", "Doc. {n} - {nome}", width: 230);
        f.Number("inicio", "Começar no número", 1, 0, 99999);
        f.Choice("digitos", "Algarismos", new[] { ("1", "1, 2, 3"), ("2", "01, 02, 03"), ("3", "001, 002, 003") }, "2");
        f.Note("{n} = número, {nome} = nome atual. A extensão continua a mesma.");
        f.Preview(p => Tr.T("Exemplo") + ": " + string.Join("   ", Actions.RenamePlan(Form.Ordered(fs, p), p).Take(3).Select(x => Path.GetFileName(x.to))));
        return f.Run();
    }

    public static P? Collage(string[] fs)
    {
        var f = new Form("Colagem", fs);
        f.Number("colunas", "Colunas (0 = automático)", 0, 0, 50);
        f.Number("largura", "Largura final", 2400, 300, 12000, "px");
        f.Number("espaco", "Espaço entre as fotos", 12, 0, 400, "px");
        f.Choice("encaixe", "Encaixe", new[] { ("preencher", "Preencher o quadro (corta as bordas)"), ("inteira", "Foto inteira") }, "preencher");
        f.Color("cor", "Cor do fundo", "#FFFFFF");
        f.Choice("formato", "Salvar como", new[] { ("jpg", "JPG"), ("png", "PNG") }, "jpg");
        return f.Run();
    }

    public static P? Speed(string[] fs)
    {
        var f = new Form("Velocidade", fs);
        f.Choice("fator", "Velocidade", new[] { ("0.25", "0,25x (bem devagar)"), ("0.5", "0,5x"), ("0.75", "0,75x"), ("1.25", "1,25x"), ("1.5", "1,5x"), ("2", "2x"), ("3", "3x"), ("4", "4x"), ("8", "8x"), ("outro", "Outra") }, "2");
        f.Number("outro", "Fator", 1.5, 0.1, 16, "x", ("fator", new[] { "outro" }), decimals: true);
        var p = f.Run();
        if (p != null && p.S("fator") == "outro") p["fator"] = p.S("outro");
        return p;
    }

    public static P? ExtractAudio(string[] fs)
    {
        var f = new Form("Extrair áudio", fs);
        f.Choice("formato", "Formato", new[] { ("mp3", "MP3"), ("m4a", "M4A (AAC)"), ("wav", "WAV (sem perda)"), ("flac", "FLAC (sem perda)"), ("opus", "Opus") }, "mp3");
        return f.Run();
    }

    public static P? ResizeVideo(string[] fs)
    {
        var f = new Form("Tamanho do vídeo", fs);
        f.Choice("altura", "Resolução", new[] { ("50%", "Metade"), ("2160", "4K (2160p)"), ("1440", "1440p"), ("1080", "Full HD (1080p)"), ("720", "HD (720p)"), ("480", "480p"), ("360", "360p") }, "720");
        f.Note("Nunca aumenta: se o vídeo já é menor, fica como está.");
        return f.Run();
    }

    public static P? Normalize(string[] fs)
    {
        var f = new Form("Normalizar volume", fs);
        f.Choice("alvo", "Volume", new[] { ("-14", "Streaming e celular (-14 LUFS)"), ("-16", "Podcast (-16 LUFS)"), ("-19", "Voz baixa (-19 LUFS)"), ("-23", "TV e rádio (-23 LUFS)") }, "-14");
        return f.Run();
    }

    public static P? Channels(string[] fs)
    {
        var f = new Form("Canais", fs);
        f.Choice("canais", "Deixar", new[] { ("mono", "Mono (mistura os dois lados)"), ("estereo", "Estéreo"), ("esquerdo", "Só o lado esquerdo"), ("direito", "Só o lado direito"), ("inverter", "Trocar esquerdo e direito"), ("dobrar", "Esquerdo nos dois lados") }, "mono");
        return f.Run();
    }

    public static P? SplitPdf(string[] fs)
    {
        int pages = fs.Length == 1 ? Pdf.Pages(fs[0]) : 0;
        var f = new Form("Dividir PDF", fs, pages > 0 ? $"{pages} páginas" : null);
        f.Choice("modo", "Como", new[] { ("paginas", "Uma página por arquivo"), ("cada", "A cada tantas páginas"), ("intervalos", "Intervalos escolhidos") }, "paginas");
        f.Number("cada", "Páginas por arquivo", 2, 1, 100000, "", ("modo", new[] { "cada" }));
        f.Text("intervalos", "Intervalos", "1-3, 4-6", ("modo", new[] { "intervalos" }),
            v => Pdf.ParseRanges(v, pages > 0 ? pages : int.MaxValue).Count == 0 ? "Escreva os intervalos assim: 1-3, 4, 5-9" : null);
        return f.Run();
    }

    public static string Human(long b) =>
        b >= 1L << 30 ? $"{b / (double)(1L << 30):0.##} GB" : b >= 1 << 20 ? $"{b / (double)(1 << 20):0.#} MB" : $"{Math.Max(1, b / 1024)} KB";
}
