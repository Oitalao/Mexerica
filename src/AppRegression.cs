using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Mexerica;

/// Regressões do esqueleto: nunca abre janelas nem usa as preferências instaladas.
static class AppRegression
{
    public static int Run(string report)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MEXERICA_TESTE_DADOS")))
            throw new InvalidOperationException("Defina MEXERICA_TESTE_DADOS para isolar os testes.");
        var lines = new List<string>();
        void Test(string name, Action test)
        {
            try { test(); lines.Add("OK " + name); }
            catch (Exception ex) { lines.Add("ERRO " + name + ": " + (ex.InnerException ?? ex).Message); }
            File.WriteAllLines(report, lines);
        }
        var saved = new Config { UseGpu = false };
        Config.Override(saved);
        Directory.CreateDirectory(Paths.Data);
        Test("configuração com valores nulos", () =>
        {
            File.WriteAllText(Path.Combine(Paths.Data, "config.json"), "{\"OutputDir\":null,\"Language\":null,\"Presets\":[null],\"WatchFolders\":null,\"WheelOrder\":{\"Convert:Image\":null},\"WheelHidden\":null}");
            var loaded = (Config)Call(typeof(Config), null, "Load")!;
            Config.Override(loaded);
            _ = new SettingsWindow();
            _ = Catalog.For(Mode.Convert, new[] { "x.jpg" });
        });
        Config.Override(saved);
        Test("histórico danificado não impede janela", () =>
        {
            File.WriteAllText(Path.Combine(Paths.Data, "recentes.json"), "[null,{\"Inputs\":null,\"Outputs\":[]},{\"Op\":null,\"Outputs\":[null]}]");
            typeof(History).GetField("items", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, null);
            _ = new HistoryWindow();
        });
        Test("número NaN recusado", () =>
        {
            var f = new Form("Teste", Array.Empty<string>()).Number("n", "Tamanho", 10, 1, 100);
            Require(f.Validate(new P { ["n"] = "NaN" }) != null, "NaN passou pela validação");
        });
        Test("escolha inexistente recusada", () =>
        {
            var f = new Form("Teste", Array.Empty<string>()).Choice("modo", "Modo", new[] { ("a", "A"), ("b", "B") }, "a");
            Require(f.Validate(new P { ["modo"] = "inexistente" }) != null, "escolha inválida passou");
        });
        Test("cor inválida recusada", () =>
        {
            var f = new Form("Teste", Array.Empty<string>()).Color("cor", "Fundo", "#FFFFFF");
            Require(f.Validate(new P { ["cor"] = "batata" }) != null, "cor inválida passou");
        });
        Test("catálogo de lote PDF com corrompido", () =>
        {
            var a = Path.Combine(Paths.Data, "invalido.pdf"); File.WriteAllText(a, "PDF estragado");
            Require(Catalog.For(Mode.Tools, new[] { a, a }).Count > 0, "catálogo vazio");
        });
        Test("parâmetro numérico não finito usa padrão", () =>
        {
            Require(new P { ["n"] = "NaN" }.D("n", 10) == 10, "NaN chegou ao motor");
            Require(new P { ["n"] = "1e999" }.I("n", 10) == 10, "infinito chegou ao motor");
        });
        Test("nomes de dispositivo não são saídas válidas", () =>
        {
            foreach (var name in new[] { "CON", "AUX.txt", "NUL", "COM1", "LPT9", "com1.extra.pdf" })
            {
                var clean = Paths.CleanName(name)!;
                Require(!new[] { "CON", "AUX", "NUL", "COM1", "LPT9" }.Contains(clean.Split('.')[0], StringComparer.OrdinalIgnoreCase), clean);
            }
        });
        Test("atualizador não aceita instalador sem hash", () =>
        {
            using var json = JsonDocument.Parse("{\"tag_name\":\"v9.0.0\",\"assets\":[{\"name\":\"Mexerica-instalador.exe\",\"browser_download_url\":\"https://example.com/test.exe\",\"size\":1}]}");
            Require(Call(typeof(Updater), null, "Parse", json.RootElement) == null, "release sem SHA-256 foi aceita");
        });
        Test("versões iguais, menores e maiores são comparadas corretamente", () =>
        {
            var current = Updater.Current;
            foreach (var (tag, expected) in new[] { (current.ToString(3), 0), ($"{current.Major}.{current.Minor}.{current.Build + 1}", 1), ($"{current.Major + 1}.0", 1), ($"{Math.Max(0, current.Major - 1)}.0.0", -1) })
            {
                using var json = JsonDocument.Parse("{\"tag_name\":\"v" + tag + "\",\"assets\":[{\"name\":\"Mexerica-instalador.exe\",\"browser_download_url\":\"https://example.com/test.exe\",\"size\":1,\"digest\":\"sha256:" + new string('a', 64) + "\"}]}");
                var release = Call(typeof(Updater), null, "Parse", json.RootElement)!;
                var version = (Version)release.GetType().GetProperty("Version")!.GetValue(release)!;
                Require(Math.Sign(version.CompareTo(current)) == expected && version.ToString(3).Length > 0, $"versão {tag} comparada incorretamente");
            }
        });
        Test("desfazer não inclui vizinho de outro trabalho", () =>
        {
            var first = Path.Combine(Paths.Data, "pedido - parte 1 de 2.pdf");
            var other = Path.Combine(Paths.Data, "pedido - parte 2 de 20.pdf");
            File.WriteAllText(first, "nosso"); File.WriteAllText(other, "outra operação");
            File.SetCreationTime(first, DateTime.Now); File.SetCreationTime(other, DateTime.Now);
            var undo = (Action)Call(typeof(App), null, "UndoFor", new List<string> { first }, new[] { "original.pdf" }, DateTime.Now.AddSeconds(-2))!;
            var made = (List<string>)undo.Target!.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Single(f => f.FieldType == typeof(List<string>)).GetValue(undo.Target)!;
            Require(!made.Contains(other), "vizinho de 20 partes entrou no Desfazer de 2 partes");
        });
        Test("parâmetro inteiro excessivo usa padrão", () =>
            Require(new P { ["n"] = "1e100" }.I("n", 10) == 10, "estouro virou inteiro negativo"));
        Test("predefinição com sufixo inválido produz arquivo", () =>
        {
            var src = Path.Combine(Paths.Data, "foto.PNG");
            using (var img = new ImageMagick.MagickImage(ImageMagick.MagickColors.Red, 32, 24)) img.Write(src);
            var dst = Presets.Run(src, new Preset { Name = "Teste", Group = "Image", Format = "png", Suffix = "web: celular/mini" }).GetAwaiter().GetResult()!;
            using var result = new ImageMagick.MagickImage(dst);
            Require(result.Width == 32 && result.Height == 24, "dimensões erradas");
        });
        Test("lote mantém válidos antes e depois do inválido", () =>
        {
            var a = Path.Combine(Paths.Data, "bom.PNG");
            var bad = Path.Combine(Paths.Data, "ruim.PNG");
            var empty = Path.Combine(Paths.Data, "vazio.PNG"); File.WriteAllBytes(empty, Array.Empty<byte>());
            using (var img = new ImageMagick.MagickImage(ImageMagick.MagickColors.Blue, 20, 30)) img.Write(a);
            File.WriteAllText(bad, "corrompido");
            var files = Enumerable.Repeat(a, 32).Concat(new[] { bad, empty }).Concat(Enumerable.Repeat(a, 32)).ToArray();
            var op = Catalog.For(Mode.Convert, files).Single(o => o.Label == "JPG");
            var outputs = new List<string>(); var errors = new List<string>(); var j = new Job(); Job.Current = j;
            try
            {
                App.EachPublic(op, files, new P(), j, outputs, errors).GetAwaiter().GetResult();
                Require(outputs.Count == 64 && errors.Count == 2 && outputs.Distinct().Count() == 64, "contagem ou colisão");
                foreach (var o in outputs) { using var image = new ImageMagick.MagickImage(o); Require(image.Width == 20 && image.Height == 30, "imagem errada"); }
            }
            finally { Job.Current = null; Paths.ReleaseNames(); }
        });
        Test("PDFs com senha e nomes iguais permanecem distintos", () =>
        {
            var originals = new List<string>();
            for (int i = 0; i < 2; i++)
            {
                var dir = Path.Combine(Paths.Data, "senha" + i); Directory.CreateDirectory(dir);
                var plain = Path.Combine(dir, "entrada.pdf");
                using (var pdf = new PdfSharp.Pdf.PdfDocument())
                {
                    var page = pdf.AddPage(); page.Width = PdfSharp.Drawing.XUnit.FromPoint(300 + i * 100); pdf.Save(plain);
                }
                originals.Add(PdfExtras.Protect(plain, new P { ["senha"] = "teste-123" }).GetAwaiter().GetResult()!);
            }
            Form.TestAnswer = _ => new P { ["senha"] = "teste-123" };
            string? tmp = null;
            try
            {
                var unlocked = OnUi(() => (Task<(string[] files, string? tmp)>)Call(typeof(App), null, "UnlockPdfs", (object)originals.ToArray())!); tmp = unlocked.tmp;
                Require(unlocked.files.Distinct().Count() == 2, "os dois PDFs foram substituídos pelo último");
                using var a = UglyToad.PdfPig.PdfDocument.Open(unlocked.files[0]);
                using var b = UglyToad.PdfPig.PdfDocument.Open(unlocked.files[1]);
                Require(a.GetPage(1).Width != b.GetPage(1).Width, "conteúdo do primeiro PDF foi perdido");
            }
            finally { Form.TestAnswer = null; if (tmp != null) Paths.TryDelete(tmp); }
        });
        Test("resultado de PDF desbloqueado fica fora do temporário", () =>
        {
            var original = Directory.GetFiles(Path.Combine(Paths.Data, "senha0"), "*com senha*.pdf").First();
            Form.TestAnswer = _ => new P { ["senha"] = "teste-123" };
            string? tmp = null;
            try
            {
                var unlocked = OnUi(() => (Task<(string[] files, string? tmp)>)Call(typeof(App), null, "UnlockPdfs", (object)new[] { original })!); tmp = unlocked.tmp;
                var output = Pdf.Organize(unlocked.files[0], new P { ["ordem"] = "1r90" }).GetAwaiter().GetResult();
                Require(Path.GetDirectoryName(output) == Path.GetDirectoryName(original), "resultado seria apagado junto com " + tmp);
                Paths.TryDelete(tmp!); tmp = null;
                using var result = UglyToad.PdfPig.PdfDocument.Open(output);
                Require(result.NumberOfPages == 1, "PDF final foi perdido ao limpar os temporários");
            }
            finally { Form.TestAnswer = null; if (tmp != null) Paths.TryDelete(tmp); }
        });
        Test("estrutura de entrada Win32 tem tamanho do SDK x64", () =>
            Require(System.Runtime.InteropServices.Marshal.SizeOf<Native.INPUT>() == 40,
                "INPUT tem " + System.Runtime.InteropServices.Marshal.SizeOf<Native.INPUT>() + " bytes; SDK exige 40"));
        Test("QR com várias linhas é exibido completo", () =>
        {
            const string text = "BEGIN:VCARD\nVERSION:3.0\nFN:Contato de teste\nEND:VCARD";
            var qr = new ZXing.BarcodeWriterPixelData { Format = ZXing.BarcodeFormat.QR_CODE, Options = new ZXing.Common.EncodingOptions { Width = 256, Height = 256, Margin = 4 } }.Write(text);
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
            png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(qr.Width, qr.Height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, qr.Pixels, qr.Width * 4)));
            var file = Path.Combine(Paths.Data, "contato.png"); using (var stream = File.Create(file)) png.Save(stream);
            string? shown = null;
            Form.Capture = w => shown = Logical(w).OfType<TextBox>().First().Text;
            try { QrWindow.Show(QrWindow.Read(new[] { file })!); }
            finally { Form.Capture = null; }
            Require(shown == text, "conteúdo exibido: " + shown);
        });
        Test("modo sem tela avisa operação inexistente", () =>
        {
            var file = Path.Combine(Paths.Data, "bom.PNG");
            var result = (int)Call(typeof(App), null, "SelfTest", (object)new[] { "--executar", "Tools", "Inexistente", Path.Combine(Paths.Data, "op-inexistente.txt"), file })!;
            Require(result != 0, "retornou sucesso sem executar nada");
        });
        Test("salvamentos simultâneos não perdem preferências", () =>
        {
            var log = Path.Combine(Paths.Data, "log.txt");
            var before = File.Exists(log) ? File.ReadAllText(log).Length : 0;
            Parallel.For(0, 80, i => new Config { ImageQuality = i + 1 }.Save());
            var added = File.Exists(log) ? File.ReadAllText(log)[before..] : "";
            Require(!added.Contains("config:"), "salvamentos falharam: " + added.Split('\n').FirstOrDefault());
            _ = JsonDocument.Parse(File.ReadAllText(Path.Combine(Paths.Data, "config.json")));
        });
        Config.Override(saved);
        Test("histórico pode ser lido enquanto vigia grava", () =>
        {
            History.Clear(); History.Add("Teste", new[] { "origem" }, new[] { "primeiro" });
            using var enumerator = History.Items.GetEnumerator();
            Require(enumerator.MoveNext(), "histórico vazio");
            Task.Run(() => History.Add("Teste", new[] { "origem" }, new[] { "segundo" })).GetAwaiter().GetResult();
            _ = enumerator.MoveNext();
        });
        Test("predefinições de áudio aceitam 96 kHz", () =>
        {
            var src = Path.Combine(Paths.Data, "alta-definicao.wav");
            Proc.Run(Paths.FindTool("ffmpeg.exe")!, new[] { "-v", "error", "-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=0.25:sample_rate=96000", "-c:a", "pcm_s24le", src }).GetAwaiter().GetResult();
            foreach (var format in new[] { "mp3", "m4a", "ogg", "opus", "flac", "wav" })
            {
                var dst = Presets.Run(src, new Preset { Group = "Audio", Format = format }).GetAwaiter().GetResult()!;
                var info = Ff.Read(dst);
                Require(info.Audios.Count > 0 && info.Duration > 0.15, format + " perdeu áudio");
            }
        });
        Test("predefinições de vídeo aceitam dimensões ímpares", () =>
        {
            var src = Path.Combine(Paths.Data, "video-impar.mkv");
            Proc.Run(Paths.FindTool("ffmpeg.exe")!, new[] { "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc=size=321x241:rate=10:duration=0.3", "-f", "lavfi", "-i", "sine=frequency=440:duration=0.3:sample_rate=96000", "-c:v", "ffv1", "-c:a", "pcm_s24le", "-shortest", src }).GetAwaiter().GetResult();
            foreach (var format in new[] { "mp4", "mov", "mkv", "webm" })
            {
                var dst = Presets.Run(src, new Preset { Group = "Video", Format = format, MaxSide = 720 }).GetAwaiter().GetResult()!;
                var info = Ff.Read(dst);
                Require(info.Video != null && info.Audios.Count > 0 && info.Duration > 0.15, format + " perdeu vídeo/áudio");
            }
        });
        Test("download antigo não substitui uma roda nova", () =>
        {
            var wheel = new WheelWindow(Catalog.For, (_, _) => { });
            var a = new TaskCompletionSource<string>(); var b = new TaskCompletionSource<string>();
            wheel.Download = url => url == "a" ? a.Task : b.Task;
            var armed = typeof(WheelWindow).GetField("<IsArmed>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var files = typeof(WheelWindow).GetField("files", BindingFlags.Instance | BindingFlags.NonPublic)!;
            OnUi(async () =>
            {
                armed.SetValue(wheel, true); Call(typeof(WheelWindow), wheel, "StartDownload", "a");
                wheel.Disarm(); armed.SetValue(wheel, true); files.SetValue(wheel, null);
                Call(typeof(WheelWindow), wheel, "StartDownload", "b");
                a.SetResult(Path.Combine(Paths.Data, "bom.PNG")); await Task.Delay(30);
                Require(files.GetValue(wheel) == null, "download antigo colocou seu arquivo na roda nova");
                b.SetResult(Path.Combine(Paths.Data, "foto.PNG")); await Task.Delay(30);
                Require(((string[])files.GetValue(wheel)!)[0].EndsWith("foto.PNG"), "download novo foi descartado");
                return true;
            });
        });
        Test("atualização com falha não deixa pasta temporária", () =>
        {
            var before = Directory.GetDirectories(Path.GetTempPath(), "mexerica-*").ToHashSet();
            using var json = JsonDocument.Parse("{\"tag_name\":\"v9.0.0\",\"assets\":[{\"name\":\"Mexerica-instalador.exe\",\"browser_download_url\":\"http://127.0.0.1:1/sem-instalador.exe\",\"size\":1,\"digest\":\"sha256:" + new string('a', 64) + "\"}]}");
            var release = Call(typeof(Updater), null, "Parse", json.RootElement)!;
            Form.Capture = _ => { }; App.Toast = new Toast();
            try
            {
                OnUi(async () => { await (Task)Call(typeof(Updater), null, "Install", release)!; return true; });
                Require(Directory.GetDirectories(Path.GetTempPath(), "mexerica-*").All(before.Contains), "pasta do instalador ficou abandonada");
            }
            finally { Form.Capture = null; }
        });
        Test("download interrompido não deixa arquivo incompleto", () =>
        {
            var dir = Paths.Downloads(); Directory.CreateDirectory(dir);
            var before = Directory.GetFiles(dir).ToHashSet();
            var server = Serve("parcial.bin", "abc", 100);
            try { WebDrop.Download(server.url).GetAwaiter().GetResult(); throw new Exception("download incompleto foi aceito"); }
            catch (System.Net.Http.HttpRequestException) { }
            finally { server.done.GetAwaiter().GetResult(); }
            Require(Directory.GetFiles(dir).All(before.Contains), "arquivo parcial ficou em Downloads");
        });
        Test("download evita nome já usado por pasta", () =>
        {
            Directory.CreateDirectory(Path.Combine(Paths.Downloads(), "arquivo.bin"));
            var server = Serve("arquivo.bin", "arquivo completo", 16);
            var dst = WebDrop.Download(server.url).GetAwaiter().GetResult(); server.done.GetAwaiter().GetResult();
            Require(File.ReadAllText(dst) == "arquivo completo", "download incorreto");
        });
        Test("cancelar personalização preserva preferências", () =>
        {
            var config = new Config(); var window = new CustomizeWindow(config);
            var check = Logical(window).OfType<CheckBox>().First(); check.IsChecked = false;
            Require(config.WheelHidden.Count == 0, "preferências mudaram antes de Salvar");
        });
        Test("trocar tipo da predefinição usa tamanho mostrado", () =>
        {
            var window = new PresetWindow(null);
            var group = Logical(window).OfType<ComboBox>().Single(c => c.Items.OfType<ComboBoxItem>().Any(i => (string)i.Tag == "Video"));
            group.SelectedItem = group.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag == "Video");
            var p = (Preset)typeof(PresetWindow).GetField("p", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            var size = Logical(window).OfType<ComboBox>().Single(c => c.Items.OfType<ComboBoxItem>().Any(i => (string)i.Tag == "720"));
            Require(p.MaxSide.ToString() == (string)((ComboBoxItem)size.SelectedItem).Tag, "tamanho oculto " + p.MaxSide + " diverge da tela");
        });
        Test("boas-vindas traduz parágrafos concatenados", () =>
        {
            var previous = Config.Now.Language; Config.Now.Language = "en";
            try
            {
                var window = new WelcomeWindow();
                foreach (var tb in Logical(window).OfType<TextBlock>()) tb.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, tb));
                Require(!Logical(window).OfType<TextBlock>().Any(t => t.Text.StartsWith("Ele converte") || t.Text.StartsWith("O Mexerica fica quietinho") || t.Text.Contains("Espaço")), "parágrafos ou atalho permanecem em português");
            }
            finally { Config.Now.Language = previous; }
        });
        Test("assinatura copiada de executável alterado é recusada", () =>
        {
            var source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe");
            if (!File.Exists(source)) throw new Exception("amostra assinada do LibreOffice não está instalada");
            LibreOfficeSetup.VerifyPackage(source);
            var fake = Path.Combine(Paths.Data, "libreoffice-alterado.exe"); File.Copy(source, fake, true);
            var bytes = File.ReadAllBytes(fake);
            int pe = BitConverter.ToInt32(bytes, 0x3c);
            int section = pe + 24 + BitConverter.ToUInt16(bytes, pe + 20);
            int data = BitConverter.ToInt32(bytes, section + 20);
            bytes[data + 128] ^= 1; File.WriteAllBytes(fake, bytes);
            bool rejected = false;
            try { LibreOfficeSetup.VerifyPackage(fake); } catch { rejected = true; }
            Require(rejected, "assinante correto bastou mesmo com conteúdo adulterado");
        });
        Test("tarjar mostra a página pedida após navegação rápida", () =>
        {
            var src = Path.Combine(Paths.Data, "navegacao.pdf");
            using (var pdf = new PdfSharp.Pdf.PdfDocument())
            {
                var one = pdf.AddPage(); one.Width = PdfSharp.Drawing.XUnit.FromPoint(400); one.Height = PdfSharp.Drawing.XUnit.FromPoint(200);
                var two = pdf.AddPage(); two.Width = PdfSharp.Drawing.XUnit.FromPoint(200); two.Height = PdfSharp.Drawing.XUnit.FromPoint(400);
                using var g = PdfSharp.Drawing.XGraphics.FromPdfPage(two);
                for (int i = 0; i < 3000; i++) g.DrawEllipse(PdfSharp.Drawing.XPens.Red, i % 190, i % 390, 10, 10);
                pdf.Save(src);
            }
            OnUi(async () =>
            {
                var doc = await Pdf.Open(src);
                var window = Activator.CreateInstance(typeof(PdfRedactWindow), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { src }, null)!;
                typeof(PdfRedactWindow).GetField("doc", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, doc);
                typeof(PdfRedactWindow).GetField("count", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, 2);
                var a = (Task)Call(typeof(PdfRedactWindow), window, "Show", 2)!;
                var b = (Task)Call(typeof(PdfRedactWindow), window, "Show", 1)!;
                await Task.WhenAll(a, b);
                var image = (Image)typeof(PdfRedactWindow).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                var bmp = (System.Windows.Media.Imaging.BitmapSource)image.Source;
                Require(bmp.PixelWidth > bmp.PixelHeight, "página 2 apareceu enquanto tarjas eram atribuídas à página 1");
                return true;
            });
        });
        Test("tarjar bloqueia marcação enquanto carrega a página", () =>
        {
            var src = Path.Combine(Paths.Data, "navegacao.pdf");
            OnUi(async () =>
            {
                var window = Activator.CreateInstance(typeof(PdfRedactWindow), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { src }, null)!;
                typeof(PdfRedactWindow).GetField("doc", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, await Pdf.Open(src));
                typeof(PdfRedactWindow).GetField("count", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, 2);
                await (Task)Call(typeof(PdfRedactWindow), window, "Show", 1)!;
                var loading = (Task)Call(typeof(PdfRedactWindow), window, "Show", 2)!;
                var layer = (System.Windows.Controls.Canvas)typeof(PdfRedactWindow).GetField("layer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                bool accepted = layer.IsHitTestVisible;
                await loading;
                Require(!accepted && layer.IsHitTestVisible, "página antiga aceitava marcações durante a troca");
                return true;
            });
        });
        Test("editor exporta anotações na posição correta em português e árabe", () =>
        {
            var previous = Config.Now.Language;
            try
            {
                foreach (var language in new[] { "pt-BR", "ar" })
                {
                    Config.Now.Language = language;
                    var window = (Window)Activator.CreateInstance(typeof(ImageEditor), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { Path.Combine(Paths.Data, "foto.PNG"), false }, null)!;
                    var ink = (Canvas)typeof(ImageEditor).GetField("ink", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                    var mark = new System.Windows.Shapes.Rectangle { Width = 8, Height = 8, Fill = System.Windows.Media.Brushes.Red };
                    Canvas.SetLeft(mark, 4); Canvas.SetTop(mark, 5); ink.Children.Add(mark);
                    var visual = (FrameworkElement)window.Content; visual.Measure(new Size(1180, 800)); visual.Arrange(new Rect(0, 0, 1180, 800)); visual.UpdateLayout();
                    var dst = Path.Combine(Paths.Data, "camada-" + language + ".png"); Call(typeof(ImageEditor), window, "SaveLayer", dst);
                    using var image = new ImageMagick.MagickImage(dst);
                    var pixel = image.GetPixels().GetPixel(6, 7).ToColor()!;
                    Require(image.Width == 32 && image.Height == 24 && pixel.R > 200 && pixel.G < 30 && pixel.A > 200, "posição ou transparência errada: " + language);
                }
            }
            finally { Config.Now.Language = previous; }
        });
        Test("nomes longos e arquivo somente leitura preservam original", () =>
        {
            var dir = Paths.Data;
            while (dir.Length < 270) dir = Path.Combine(dir, "pasta com espaço e acento ção");
            Directory.CreateDirectory(dir);
            var src = Path.Combine(dir, "coração 🍊 FOTO.PNG");
            using (var image = new ImageMagick.MagickImage(ImageMagick.MagickColors.Green, 27, 19)) using (var stream = File.Create(src)) image.Write(stream, ImageMagick.MagickFormat.Png);
            var bytes = File.ReadAllBytes(src); File.SetAttributes(src, FileAttributes.ReadOnly);
            try
            {
                var dst = Images.Convert(src, "jpg"); using var image = Images.Load(dst);
                Require(image.Width == 27 && image.Height == 19 && File.ReadAllBytes(src).SequenceEqual(bytes), "dimensão ou original alterado");
            }
            finally { File.SetAttributes(src, FileAttributes.Normal); }
        });
        Test("janela de tarjas cabe na área útil da tela", () =>
        {
            var window = (Window)Activator.CreateInstance(typeof(PdfRedactWindow), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { "teste.pdf" }, null)!;
            Require(window.Height <= SystemParameters.WorkArea.Height, $"janela: {window.Height}; tela útil: {SystemParameters.WorkArea.Height}");
        });
        Test("lote de PDFs com senhas diferentes conclui sem repetir os já abertos", () =>
        {
            var plain = Path.Combine(Paths.Data, "senhas-diferentes.pdf");
            using (var document = new PdfSharp.Pdf.PdfDocument()) { document.AddPage(); document.Save(plain); }
            var a = PdfExtras.Protect(plain, new P { ["senha"] = "senha-um" }).GetAwaiter().GetResult()!;
            var b = PdfExtras.Protect(plain, new P { ["senha"] = "senha-dois" }).GetAwaiter().GetResult()!;
            int prompts = 0;
            Form.TestAnswer = _ => ++prompts switch { 1 => new P { ["senha"] = "senha-um" }, 2 => new P { ["senha"] = "senha-dois" }, _ => null };
            string? tmp = null;
            try
            {
                var unlocked = OnUi(() => (Task<(string[] files, string? tmp)>)Call(typeof(App), null, "UnlockPdfs", (object)new[] { a, b })!); tmp = unlocked.tmp;
                Require(prompts == 2 && unlocked.files.Distinct().Count() == 2 && unlocked.files.All(f => !Qpdf.NeedsPassword(f)), "senhas aceitas foram pedidas novamente");
            }
            finally { Form.TestAnswer = null; if (tmp != null) Paths.TryDelete(tmp); }
        });
        Test("legendas dos ajustes do editor seguem os 13 idiomas", () =>
        {
            var previous = Config.Now.Language;
            try
            {
                foreach (var language in Tr.Languages.Where(l => l.Code != "auto"))
                {
                    Config.Now.Language = language.Code;
                    var editor = (Window)Activator.CreateInstance(typeof(ImageEditor), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { Path.Combine(Paths.Data, "foto.PNG"), false }, null)!;
                    Call(typeof(ImageEditor), editor, "SetTool", Enum.Parse(typeof(ImageEditor).GetNestedType("Tool", BindingFlags.NonPublic)!, "Adjust"));
                    Require(Logical(editor).OfType<TextBlock>().Any(t => t.Text.StartsWith(Tr.T("Brilho") + ":")), "legenda fora do idioma " + language.Code);
                }
            }
            finally { Config.Now.Language = previous; }
        });
        Config.Override(saved);
        File.WriteAllLines(report, lines);
        return lines.Count(l => l.StartsWith("ERRO"));
    }

    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static (string url, Task done) Serve(string filename, string content, int length)
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0); listener.Start();
        var url = "http://127.0.0.1:" + ((System.Net.IPEndPoint)listener.LocalEndpoint).Port + "/teste";
        var done = Task.Run(async () =>
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, leaveOpen: true);
                while (await reader.ReadLineAsync() is { Length: > 0 }) { }
                var response = System.Text.Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {length}\r\nContent-Disposition: attachment; filename=\"{filename}\"\r\nConnection: close\r\n\r\n{content}");
                await stream.WriteAsync(response);
            }
            finally { listener.Stop(); }
        });
        return (url, done);
    }
    static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var item in Logical(child)) yield return item;
    }
    internal static T OnUi<T>(Func<Task<T>> make)
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var previous = System.Threading.SynchronizationContext.Current;
        System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
        try
        {
            var task = make();
            if (!task.IsCompleted)
            {
                var frame = new System.Windows.Threading.DispatcherFrame();
                _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
            return task.GetAwaiter().GetResult();
        }
        finally { System.Threading.SynchronizationContext.SetSynchronizationContext(previous); }
    }
    internal static object? Call(Type type, object? instance, string name, params object?[] args) =>
        type.GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args);
}
