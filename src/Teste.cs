using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Mexerica;

/// "Como usar": janela no meio da tela, que a pessoa fecha quando quiser (aberta pelo menu do ícone perto do relógio).
sealed class HelpWindow : Window
{
    public static void ShowOne()
    {
        var w = Application.Current.Windows.OfType<HelpWindow>().FirstOrDefault();
        if (w != null) { w.Activate(); return; }
        new HelpWindow().Show();
    }

    internal HelpWindow()
    {
        var c = Config.Now;
        string open = Config.KeyName(c.OpenKey);
        var hk = Config.Hotkeys.FirstOrDefault(h => h.id == c.Hotkey).name;
        Title = Tr.T("Mexerica: como usar");
        Width = 500; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        MaxHeight = Math.Max(400, SystemParameters.WorkArea.Height * 0.9);
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true;
        Ui.Dark(this);
        var st = new StackPanel { Margin = new Thickness(22, 4, 22, 8) };
        void Item(string head, string text)
        {
            st.Children.Add(Ui.H(head));
            st.Children.Add(new TextBlock { Text = Tr.T(text), TextWrapping = TextWrapping.Wrap, Foreground = Ui.Fg, LineHeight = 19 });
        }
        Item("Pelo arrasto", $"Arraste um ou mais arquivos e, sem soltar o botão, aperte {open}. A roda aparece: solte o arquivo em cima da opção.");
        Item("Trocar de roda", $"{open} de novo passa por Converter, Ferramentas e Ações. {Config.KeyName(c.ConvertKey)}, {Config.KeyName(c.ToolsKey)} e {Config.KeyName(c.ActionsKey)} vão direto a cada uma. Tab ou a rodinha do mouse mostram o outro lado quando não cabe tudo.");
        if (!string.IsNullOrEmpty(c.Hotkey))
            Item("Pelo teclado", $"Selecione arquivos no Explorador ou na área de trabalho e aperte {hk}. Escolha com as setas e Enter, ou com o mouse.");
        Item("Pelo botão direito", "Botão direito no arquivo > Mexerica.");
        Item("Cancelar", "Esc fecha a roda sem fazer nada.");
        Item("Para treinar", "No menu do ícone perto do relógio, “Criar pasta de teste” põe na área de trabalho uma pasta com arquivos de exemplo e um roteiro passo a passo.");
        var close = Ui.Button("Fechar", true);
        close.Click += (_, _) => Close();
        var test = Ui.Button("Criar pasta de teste", false);
        test.Click += (_, _) => { Close(); TestFolder.CreateAndOpen(); };
        Content = Ui.Scrolling(st, test, close);
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
    }
}

/// Pasta de teste na área de trabalho: arquivos de exemplo feitos na hora (pelo ffmpeg que vem junto) e um roteiro.
static class TestFolder
{
    public static void CreateAndOpen()
    {
        App.Toast.Show("Criando a pasta de teste…", "Leva alguns segundos.");
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var dir = Create();
                var script = Path.Combine(dir, Tr.T("ROTEIRO") + ".txt");
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    App.Toast.Hide();
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
                    Process.Start(new ProcessStartInfo("notepad.exe", "\"" + script + "\"") { UseShellExecute = true });
                });
            }
            catch (Exception ex)
            {
                Log.Write("pasta de teste: " + ex);
                Application.Current.Dispatcher.BeginInvoke(() => App.Toast.Show("Não deu certo", App.Friendly(ex), error: true));
            }
        });
    }

    public static string Create(string? parent = null)
    {
        var desk = parent ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var name = Tr.T("Teste do Mexerica");
        var dir = Path.Combine(desk, name);
        for (int i = 2; Directory.Exists(dir); i++) dir = Path.Combine(desk, $"{name} {i}");
        Directory.CreateDirectory(dir);
        var watch = Path.Combine(dir, Tr.T("Pasta monitorada"));
        Directory.CreateDirectory(watch);

        string F(string n) => Path.Combine(dir, Tr.T(n));
        var bars = "testsrc2=size=1280x720:rate=30";
        Ffmpeg("-f", "lavfi", "-i", "testsrc2=size=800x600", "-frames:v", "1", "-q:v", "3", F("foto") + ".jpg");
        Ffmpeg("-f", "lavfi", "-i", "testsrc2=size=1600x1200", "-frames:v", "1", F("copie para a pasta monitorada") + ".png");
        Ffmpeg("-f", "lavfi", "-i", "sine=frequency=440:duration=3", "-c:a", "libmp3lame", "-q:a", "4", F("musica") + ".mp3");
        Ffmpeg("-f", "lavfi", "-i", bars, "-t", "4", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-an", F("video sem som") + ".mp4");
        Ffmpeg("-f", "lavfi", "-i", bars, "-f", "lavfi", "-i", "sine=frequency=660:duration=4", "-t", "4",
               "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", F("video com som") + ".mp4");
        Xlsx(F("planilha") + ".xlsx");
        File.WriteAllText(F("ROTEIRO") + ".txt", Script(), new UTF8Encoding(true));
        return dir;
    }

    static void Ffmpeg(params string[] args)
    {
        var exe = Ff.Exe ?? throw new Exception("ffmpeg não encontrado");
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var a in new[] { "-hide_banner", "-nostdin", "-y", "-loglevel", "error" }.Concat(args)) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new Exception("ffmpeg: " + err.Trim());
    }

    /// Planilha pequena, escrita à mão (o Excel e o LibreOffice abrem).
    static void Xlsx(string path)
    {
        string[][] rows =
        {
            new[] { Tr.T("Produto"), Tr.T("Quantidade"), Tr.T("Preço") },
            new[] { Tr.T("Mexerica"), "12", "1.5" },
            new[] { Tr.T("Laranja"), "8", "1.2" },
            new[] { Tr.T("Limão"), "20", "0.8" },
        };
        static string Esc(string s) => System.Security.SecurityElement.Escape(s) ?? "";
        var sheet = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        for (int r = 0; r < rows.Length; r++)
        {
            sheet.Append($"<row r=\"{r + 1}\">");
            for (int c = 0; c < rows[r].Length; c++)
            {
                var refc = $"{(char)('A' + c)}{r + 1}";
                var v = rows[r][c];
                if (r > 0 && c > 0) sheet.Append($"<c r=\"{refc}\"><v>{v}</v></c>");
                else sheet.Append($"<c r=\"{refc}\" t=\"inlineStr\"><is><t>{Esc(v)}</t></is></c>");
            }
            sheet.Append("</row>");
        }
        sheet.Append("</sheetData></worksheet>");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string n, string x) { using var w = new StreamWriter(zip.CreateEntry(n).Open(), new UTF8Encoding(false)); w.Write(x); }
        Add("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
        Add("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        var sheetName = Esc(Tr.T("Frutas"));
        Add("xl/workbook.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"{sheetName}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Add("xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
        Add("xl/worksheets/sheet1.xml", sheet.ToString());
    }

    static string Script()
    {
        var c = Config.Now;
        string open = Config.KeyName(c.OpenKey);
        var hk = Config.Hotkeys.FirstOrDefault(h => h.id == c.Hotkey).name;
        string foto = Tr.T("foto"), musica = Tr.T("musica"), semSom = Tr.T("video sem som"), comSom = Tr.T("video com som"),
               planilha = Tr.T("planilha"), monit = Tr.T("Pasta monitorada"), png = Tr.T("copie para a pasta monitorada");
        // Cada passo: o que fazer, o que deve acontecer e, se houver, comandos (iguais em qualquer idioma).
        var sections = new (string head, (string todo, string expected, string[]? cmds)[] steps)[]
        {
            (Tr.T("A RODA"), new (string, string, string[]?)[]
            {
                (Tr.T($"Arraste {foto}.jpg e, sem soltar, aperte {open}. Depois aperte Tab e gire a rodinha do mouse."), Tr.T("Embaixo do nome aparece \"lado 1/2\" e a roda troca de lado."), null),
                (Tr.T("Ainda com a roda aberta, solte a foto em PSD (ou JXL)."), Tr.T($"Aparece {foto}.psd nesta pasta."), null),
                (Tr.T($"Arraste \"{semSom}.mp4\" e aperte {open}."), Tr.T($"Não aparecem MP3 nem M4A. Arraste \"{comSom}.mp4\": aí eles aparecem."), null),
                (Tr.T($"Arraste {musica}.mp3, aperte {open} e solte em FLAC."), Tr.T("Aparece o arquivo de áudio novo."), null),
                (string.IsNullOrEmpty(c.Hotkey) ? "" : Tr.T($"Clique uma vez em {foto}.jpg (só selecionar) e aperte {hk}."), Tr.T("A roda abre sem precisar arrastar."), null),
            }),
            (Tr.T("DO NAVEGADOR"), new (string, string, string[]?)[]
            {
                (Tr.T($"Abra um site com foto (ex.: Wikipédia), arraste uma imagem, aperte {open} e solte em PNG."), Tr.T("A imagem vai para a pasta Downloads, já convertida."), null),
            }),
            (Tr.T("CONFIGURAÇÕES (botão direito no ícone do Mexerica perto do relógio > Configurações…)"), new (string, string, string[]?)[]
            {
                (Tr.T("Aparência da roda > Personalizar…: desmarque WEBP, suba o PSD com a seta e clique em \"Nova predefinição…\" (deixe como \"Site\" e Salvar). Clique em Pronto e depois em Salvar."), Tr.T("Na roda da foto, WEBP sumiu, PSD subiu e a fatia \"Site\" aparece logo no começo."), null),
                (Tr.T("Tema: Claro. Opacidade: Transparente. Salvar e abrir a roda."), Tr.T("Roda clara, deixando ver o que está atrás. Depois volte para o que preferir."), null),
                (Tr.T($"Pastas monitoradas > Escolher… > Adicionar pasta… > escolha \"{monit}\" (dentro desta pasta), formato JPG, Salvar e Salvar. Copie \"{png}.png\" para dentro dela."), Tr.T("Em poucos segundos aparece o JPG ao lado do PNG."), null),
                (Tr.T("Idioma (primeira opção das Configurações): escolha outro idioma, Salvar, abra a roda e as Configurações. Depois volte para o seu."), Tr.T("Tudo no idioma escolhido."), null),
            }),
            (Tr.T("ÍCONE PERTO DO RELÓGIO"), new (string, string, string[]?)[]
            {
                (Tr.T("Clique no ícone > Conversões recentes…"), Tr.T("Aparecem as conversões acima, com miniatura. Teste a busca, Abrir e Pasta."), null),
                (Tr.T("Clique no ícone > Como usar."), Tr.T("Abre uma janela no meio da tela, que fecha no botão Fechar ou com Esc."), null),
            }),
            (Tr.T("BOTÃO DIREITO NO EXPLORADOR"), new (string, string, string[]?)[]
            {
                (Tr.T($"Botão direito em {planilha}.xlsx > Mexerica > Para CSV (ou Para PDF)."), Tr.T($"Aparece {planilha}.csv (ou {planilha}.pdf)."), null),
            }),
            (Tr.T("LINHA DE COMANDO (precisa da opção do comando mexerica marcada na instalação)"), new (string, string, string[]?)[]
            {
                (Tr.T("Abra um terminal novo nesta pasta (clique na barra de endereço, digite cmd e Enter) e rode:"),
                 Tr.T($"O primeiro lista as opções; o segundo cria {foto}.webp, mesmo que o WEBP esteja escondido na roda."),
                 new[] { $"mexerica opcoes {foto}.jpg", $"mexerica converter {foto}.jpg --para webp" }),
            }),
        };
        var sb = new StringBuilder();
        sb.AppendLine(Tr.T($"TESTE DO MEXERICA {Updater.CurrentText} (uns 15 minutos)"));
        sb.AppendLine(Tr.T("Os arquivos de teste estão nesta pasta. Se algo não fizer o que está escrito, anote o número do passo e o que apareceu."));
        string expectedWord = Tr.T("Esperado:");
        int n = 0;
        foreach (var (head, steps) in sections)
        {
            sb.AppendLine().AppendLine(head);
            foreach (var (todo, expected, cmds) in steps.Where(x => x.todo.Length > 0))
            {
                sb.AppendLine($"{++n}. {todo}");
                foreach (var cmd in cmds ?? Array.Empty<string>()) sb.AppendLine("    " + cmd);
                sb.AppendLine($"   {expectedWord} {expected}");
            }
        }
        sb.AppendLine().AppendLine(Tr.T("Depois do teste, desfaça a personalização e a pasta monitorada se não quiser manter (Personalizar > \"Voltar ao padrão deste tipo\"; Pastas monitoradas > Remover). Esta pasta pode ser apagada."));
        return sb.ToString();
    }
}
