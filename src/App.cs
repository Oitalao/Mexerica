using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WF = System.Windows.Forms;

namespace Mexerica;

static class App
{
    public static Toast Toast = null!;
    static WheelWindow wheel = null!;
    static InputHooks hooks = null!;
    static WF.NotifyIcon tray = null!;
    static WF.ToolStripMenuItem pauseItem = null!;
    static WF.ToolStripMenuItem updateItem = null!;
    static string? offeredVersion;

    /// Há versão nova: aparece no topo do menu do ícone.
    public static void OfferUpdate(string version) { offeredVersion = version; updateItem.Text = Tr.T($"Atualizar para a versão {version}"); updateItem.Visible = true; }
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [STAThread]
    static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        if (Environment.GetEnvironmentVariable("MEXERICA_TESTE_UI") is string language && language.Length > 0)
        { RenderUi(language); return; }
        if (args.Length >= 4 && args[0] == "--executar") { Environment.ExitCode = SelfTest(args); return; }
        if (args.Length >= 1 && Cli.Verbs.Contains(args[0].ToLowerInvariant())) { Environment.ExitCode = Cli.Run(args); return; }
        if (args.Length >= 1 && args[0] == "--remover-menu") { ShellMenu.Remove(); return; }
        if (args.Length >= 1 && args[0] == "--sair") { QuitOther(); return; }
        // Do menu do botão direito: --menu-lista <modo> <opção> <arquivo com a lista> (todos os selecionados de uma vez)
        bool fromMenu = args.Length >= 4 && args[0] is "--menu" or "--menu-lista";
        var menuFiles = fromMenu ? MenuFiles(args) : Array.Empty<string>();
        using var mutex = new Mutex(true, @"Local\Mexerica.Instancia", out bool first);
        if (!first)
        {
            if (fromMenu) ShellMenu.Send(args[1], args[2], menuFiles);
            else SignalOther(args);
            return;
        }
        if (fromMenu && Enum.TryParse<Mode>(args[1], out var m0)) foreach (var f in menuFiles) MenuAdd(m0, args[2], f);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { Log.Write("erro: " + e.Exception); e.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("fatal: " + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) => { Log.Write("tarefa: " + e.Exception); e.SetObserved(); };
        bool firstRun = args.Contains("--boas-vindas") || !File.Exists(Path.Combine(Paths.Data, "config.json"));
        app.Startup += (_, _) => Start(fromMenu || args.Contains("--silencioso"), firstRun && !fromMenu);
        app.Run();
    }

    static void RenderUi(string language)
    {
        Config.Now.Language = language;
        if (!Tr.T("Mexerica: configurações").Contains("Mexerica", StringComparison.Ordinal) ||
            Tr.T("PDF") != "PDF" || (Tr.Code != "pt-BR" && Tr.T("3 itens") == "3 itens"))
            throw new InvalidOperationException("Catálogo de idioma ou marcadores inválidos.");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var probe = new System.Windows.Controls.TextBlock { Text = "Salvar" };
        probe.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, probe));
        probe.Text = "Cancelar";
        if (probe.Text != Tr.T("Cancelar")) throw new InvalidOperationException("Tradução dinâmica inválida.");
        var folder = Path.Combine(AppContext.BaseDirectory, "capturas");
        Directory.CreateDirectory(folder);
        var settings = new SettingsWindow();
        Render(settings, Path.Combine(folder, $"configuracoes-{language}.png"));
        var sample = Path.Combine(folder, "amostra.jpg");
        var wheelPreview = new WheelWindow(Catalog.For, (_, _) => { });
        wheelPreview.Preview(sample);
        Render(wheelPreview, Path.Combine(folder, $"roda-{language}.png"));
        Render(new CustomizeWindow(Config.Now), Path.Combine(folder, $"personalizar-{language}.png"));
        Render(new PresetWindow(null), Path.Combine(folder, $"predefinicao-{language}.png"));
        Render(new WatchWindow(new() { new WatchFolder { Folder = folder, Target = "jpg", Subfolders = true } }), Path.Combine(folder, $"pastas-{language}.png"));
        Render(new HistoryWindow(), Path.Combine(folder, $"recentes-{language}.png"));
        Render(new WhatsNewWindow("1.1.0", "- Novidade de exemplo\n- Outra novidade"), Path.Combine(folder, $"novidades-{language}.png"));
        Render(new HelpWindow(), Path.Combine(folder, $"como-usar-{language}.png"));
        var test = Path.Combine(folder, "pasta-teste-" + language);
        if (Directory.Exists(test)) Directory.Delete(test, true);
        Directory.CreateDirectory(test);
        TestFolder.Create(test);
        app.Shutdown();
    }

    static void Render(Window window, string file)
    {
        var visual = (FrameworkElement)window.Content;
        var width = window.Width;
        if (double.IsNaN(width)) width = 560;
        visual.Measure(new System.Windows.Size(width, 2000));
        var height = double.IsNaN(window.Height) ? visual.DesiredSize.Height : window.Height;
        visual.Arrange(new Rect(0, 0, width, height));
        visual.UpdateLayout();
        Tr.TranslateTree(visual);
        visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
        if (window is WheelWindow) bitmap.Render(visual);
        else
        {
            var canvas = new DrawingVisual();
            using (var drawing = canvas.RenderOpen())
            {
                drawing.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
                drawing.DrawRectangle(new VisualBrush(visual), null, new Rect(0, 0, width, height));
            }
            bitmap.Render(canvas);
        }
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file); png.Save(stream);
    }

    /// Abrir o Mexerica de novo (pelo menu Iniciar) mostra as configurações da instância que já está rodando.
    static void SignalOther(string[] args)
    {
        if (args.Contains("--silencioso")) return;
        try { using var ev = EventWaitHandle.OpenExisting(@"Local\Mexerica.Mostrar"); ev.Set(); } catch { }
    }

    /// Pedido do instalador (antes de atualizar ou remover): fecha a cópia que está na bandeja e espera ela sair.
    static void QuitOther()
    {
        try { using var ev = EventWaitHandle.OpenExisting(@"Local\Mexerica.Sair"); ev.Set(); } catch { return; }
        var me = System.Diagnostics.Process.GetCurrentProcess();
        foreach (var p in System.Diagnostics.Process.GetProcessesByName("Mexerica").Where(p => p.Id != me.Id))
        {
            try { if (!p.WaitForExit(10000)) p.Kill(true); } catch { }
        }
    }

    static void Quit()
    {
        tray.Visible = false;
        hooks.Dispose();
        Application.Current.Shutdown();
    }

    static void Start(bool quiet, bool welcome = false)
    {
        Toast = new Toast();
        wheel = new WheelWindow(Catalog.For, Run);
        new System.Windows.Interop.WindowInteropHelper(wheel).EnsureHandle();

        var ui = Application.Current.Dispatcher;
        hooks = new InputHooks { WheelOpen = () => wheel.Active, OnKey = OnKey, OnHotkey = OnHotkey, OnScroll = OnScroll };
        hooks.Escape += () => ui.BeginInvoke(wheel.Disarm);
        hooks.MouseUp += () => ui.BeginInvoke(() =>
        {
            if (!wheel.IsArmed || wheel.IsKeyboard) return;
            // O soltar do botão chega antes do Drop; espera um pouco para não cortar a operação.
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            t.Tick += (_, _) => { t.Stop(); if (!hooks.ButtonDown && !wheel.IsKeyboard) wheel.Disarm(); };
            t.Start();
        });
        hooks.MouseDown += p =>
        {
            // Pelo teclado, clicar fora da roda fecha.
            if (wheel.IsArmed && wheel.IsKeyboard)
                Application.Current.Dispatcher.BeginInvoke(() => { if (!wheel.Contains(p)) wheel.Disarm(); });
        };
        hooks.TouchHold += () => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (Config.Now.HoldToConvert && !Config.Now.Paused && !wheel.IsArmed && hooks.ButtonDown) wheel.Arm(Mode.Convert);
        });

        var show = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Mexerica.Mostrar");
        ThreadPool.RegisterWaitForSingleObject(show, (_, _) => Application.Current.Dispatcher.BeginInvoke(OpenSettings), null, -1, false);
        var quit = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Mexerica.Sair");
        ThreadPool.RegisterWaitForSingleObject(quit, (_, _) => Application.Current.Dispatcher.BeginInvoke(Quit), null, -1, true);

        BuildTray();
        ShellMenu.Listen((m, label, file) => ui.BeginInvoke(() => MenuAdd(m, label, file)));
        ShellMenu.Apply();
        Hw.Warm();
        System.Threading.Tasks.Task.Run(Robust.CleanOldTemps);
        Watchers.Apply();
        Updater.Start();
        lock (menuQueue) menuReady = true;
        ui.BeginInvoke(MenuFlushLater);
        Application.Current.Dispatcher.BeginInvoke(Trim, DispatcherPriority.ApplicationIdle);
        if (welcome)
        {
            Config.Now.Save(); // daqui em diante não é mais a primeira vez
            new WelcomeWindow().Show();
        }
        else if (!quiet)
            Toast.Show("Mexerica ligado", $"Arraste um arquivo e, sem soltar, aperte {Config.KeyName(Config.Now.OpenKey)}.");
    }

    static Mode? ModeFor(uint vk)
    {
        var c = Config.Now;
        if (vk == c.ConvertKey) return Mode.Convert;
        if (vk == c.ToolsKey) return Mode.Tools;
        if (vk == c.ActionsKey) return Mode.Actions;
        return null;
    }

    // Roda dentro do gancho do teclado: só decide e agenda, nada pesado aqui.
    static bool OnKey(uint vk)
    {
        if (Config.Now.Paused) return false;
        var d = Application.Current.Dispatcher;
        // "Aberta" só depois que a roda recebeu arquivos (ou veio pelo teclado). Antes disso nenhuma tecla é engolida:
        // num arrasto que não é de arquivo (selecionar texto, Espaço+arrastar do Photoshop) tudo segue normal.
        bool open = wheel.Active;
        if (vk == Config.Now.OpenKey)
        {
            d.BeginInvoke(() => { if (wheel.Active) wheel.Cycle(); else if (!wheel.IsArmed && hooks.ButtonDown) wheel.Arm(Mode.Convert); });
            return open; // no arrasto de arquivo o Windows já ignora a tecla; fora dele ela tem que chegar ao programa
        }
        if (!open) return false;
        if (ModeFor(vk) is Mode m) { d.BeginInvoke(() => wheel.SetMode(m)); return true; }
        switch (vk)
        {
            case 0x25 or 0x26: d.BeginInvoke(() => wheel.Step(-1)); return true; // ← ↑
            case 0x27 or 0x28: d.BeginInvoke(() => wheel.Step(1)); return true;  // → ↓
            case 0x09: d.BeginInvoke(() => { if (wheel.Faces > 1) wheel.Flip(1); else wheel.Cycle(); }); return true; // Tab vira a roda (ou troca, se ela tem um lado só)
            case 0x0D:
                d.BeginInvoke(() =>
                {
                    bool drag = !wheel.IsKeyboard;
                    if (!wheel.HasFiles) return;
                    if (wheel.RunSelected() && drag) Native.Tap(0x1B); // termina o arrasto sem soltar em lugar nenhum
                });
                return true;
        }
        return wheel.IsKeyboard; // pela roda do teclado, as demais teclas não vazam para o Explorador
    }

    // Roda do mouse com a roda aberta: vira para o outro lado quando há mais de um.
    static bool OnScroll(int delta)
    {
        if (Config.Now.Paused || wheel.Faces < 2) return false;
        Application.Current.Dispatcher.BeginInvoke(() => wheel.Flip(delta > 0 ? -1 : 1));
        return true;
    }

    // Atalho da roda pelo teclado: só vale com o Explorador (ou a área de trabalho) em primeiro plano.
    static bool OnHotkey()
    {
        if (Config.Now.Paused || !Explorer.IsExplorer(Native.GetForegroundWindow())) return false;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var fs = Explorer.Selection();
            if (fs.Length == 0) { Toast.Show("Nada selecionado", "Selecione arquivos no Explorador e use o atalho de novo."); return; }
            Native.GetCursorPos(out var p);
            // Se o cursor está longe da janela, abre no meio dela.
            var fg = Native.GetAncestor(Native.GetForegroundWindow(), 2);
            if (Native.GetWindowRect(fg, out var r) && !(p.X >= r.Left && p.X < r.Right && p.Y >= r.Top && p.Y < r.Bottom))
                p = new Native.POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 };
            wheel.ArmKeyboard(Mode.Convert, fs, p);
        });
        return true;
    }

    // ---------- Pedidos do menu do botão direito ----------
    // O Windows abre um processo por arquivo selecionado; cada um entrega seu arquivo aqui.
    // Pedidos iguais que chegam juntos viram um trabalho só (assim 30 fotos = 30 conversões em paralelo, um aviso).
    static readonly Dictionary<(Mode, string), List<string>> menuQueue = new();
    static DispatcherTimer? menuTimer;
    static bool menuReady;

    static string[] MenuFiles(string[] args)
    {
        if (args[0] == "--menu") return new[] { args[3] };
        try
        {
            var list = File.ReadAllLines(args[3], Encoding.UTF8).Where(l => l.Length > 0).ToArray();
            File.Delete(args[3]);
            return list;
        }
        catch (Exception ex) { Log.Write("lista do menu: " + ex.Message); return Array.Empty<string>(); }
    }

    static void MenuAdd(Mode mode, string label, string file)
    {
        lock (menuQueue)
        {
            if (!menuQueue.TryGetValue((mode, label), out var l)) menuQueue[(mode, label)] = l = new List<string>();
            if (!l.Contains(file, StringComparer.OrdinalIgnoreCase)) l.Add(file);
            if (!menuReady) return; // ainda abrindo: Start chama MenuFlushLater
        }
        MenuFlushLater();
    }

    static void MenuFlushLater()
    {
        menuTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Normal, (_, _) => MenuFlush(), Application.Current.Dispatcher);
        menuTimer.Stop(); menuTimer.Start();
    }

    static void MenuFlush()
    {
        menuTimer?.Stop();
        List<((Mode mode, string label) key, string[] files)> batch;
        lock (menuQueue) { batch = menuQueue.Select(kv => (kv.Key, kv.Value.ToArray())).ToList(); menuQueue.Clear(); }
        foreach (var ((mode, label), files) in batch)
        {
            if (Config.Now.Paused) { Toast.Show("Mexerica pausado", "Tire a pausa pelo ícone perto do relógio."); return; }
            var fs = files.Where(f => File.Exists(f) || Directory.Exists(f)).ToArray();
            if (fs.Length == 0) continue;
            if (label == ShellMenu.Wheel)
            {
                Native.GetCursorPos(out var pt);
                wheel.ArmKeyboard(mode, fs, pt);
                continue;
            }
            var op = Catalog.For(mode, fs).FirstOrDefault(o => o.Label == label);
            if (op == null)
            {
                Toast.Show($"“{label}” não vale para essa seleção",
                    fs.Length == 1 ? "Essa opção precisa de dois ou mais arquivos. Selecione todos e tente de novo." :
                    "Essa opção funciona com um arquivo por vez, ou os arquivos são de tipos diferentes.", error: true);
                continue;
            }
            Run(op, fs);
        }
    }

    static int running;

    static async void Run(Op op, string[] files)
    {
        files = Robust.Resolve(files);
        if (op.Ui != null)
        {
            try { op.Ui(files); }
            catch (Exception ex) { Log.Write(ex.ToString()); Toast.Show("Não deu certo", ex.Message, error: true); }
            return;
        }
        P p = P.None;
        if (op.Ask != null)
        {
            try { p = op.Ask(files)!; }
            catch (Exception ex) { Log.Write(ex.ToString()); Toast.Show("Não deu certo", ex.Message, error: true); return; }
            if (p == null) { Trim(); return; }
        }
        // PDFs com senha: pede a senha e descriptografa para um temp antes de converter.
        string? pwTmp = null;
        try { (files, pwTmp) = await UnlockPdfs(files); }
        catch (OperationCanceledException) { Trim(); return; }
        catch (Exception ex) { Log.Write(ex.ToString()); Toast.Show("Não deu certo", Friendly(ex), error: true); return; }
        string what = files.Length == 1 ? Path.GetFileName(files[0].TrimEnd('\\')) : $"{files.Length} arquivos";
        var job = new Job();
        Job.Current = job;
        Interlocked.Increment(ref running);
        Toast.Track(job, $"{op.Label}…", what);
        var outputs = new List<string>();
        var errors = new List<string>();
        bool canceled = false;
        try
        {
            if (op.All != null)
            {
                try { var r = await Task.Run(() => op.All(files, p)); if (r != null) outputs.Add(r); }
                catch (OperationCanceledException) { canceled = true; }
                catch (Exception ex) { errors.Add(Friendly(ex)); Log.Write($"{op.Label}: {ex}"); }
            }
            else if (op.One != null)
            {
                var ui = Application.Current.Dispatcher;
                int n = files.Length;
                if (n > 1) Toast.Status(job, $"{op.Label}… 0 de {n}", Path.GetFileName(files[0]));
                canceled = await Each(op, files, p, job, outputs, errors,
                    (d, f) => { if (n > 1) _ = ui.BeginInvoke(() => Toast.Status(job, $"{op.Label}… {d} de {n}", Path.GetFileName(f))); });
            }
        }
        finally
        {
            if (Interlocked.Decrement(ref running) == 0) Paths.ReleaseNames();
            Job.Current = null;
            if (p.TryGetValue("_tmp", out var tmp)) Paths.TryDelete(tmp);
            if (pwTmp != null) Paths.TryDelete(pwTmp);
            _ = Application.Current.Dispatcher.BeginInvoke(Trim, DispatcherPriority.ApplicationIdle);
        }

        if (op.Show == null) History.Add(op.Label, files, outputs);
        if (canceled) { Toast.Show("Cancelado", outputs.Count > 0 ? $"{outputs.Count} já estavam prontos" : what, outputs.FirstOrDefault()); return; }
        if (op.Show != null && errors.Count == 0) { Toast.Hide(); op.Show(string.Join("\n", outputs)); return; }
        if (errors.Count == 0)
        {
            if (outputs.Count == 0) { Toast.Show("Nada a fazer", "Os arquivos já estavam assim."); return; }
            Toast.Show("Pronto", outputs.Count == 1 ? Path.GetFileName(outputs[0].TrimEnd('\\')) : $"{outputs.Count} arquivos criados", outputs[0]);
            if (Config.Now.RevealWhenDone) Toast.Reveal(outputs[0]);
        }
        else if (outputs.Count > 0)
            Toast.Show($"{outputs.Count} prontos, {errors.Count} com erro", errors[0], outputs[0], error: true);
        else
            Toast.Show("Não deu certo", errors[0], error: true);
    }

    /// Se houver PDF com senha entre os arquivos, pede a senha (uma vez) e devolve a lista com cópias
    /// sem senha num temp; os outros arquivos seguem como estão. Cancelar o prompt cancela a operação.
    static async Task<(string[] files, string? tmp)> UnlockPdfs(string[] files)
    {
        if (!Qpdf.Ok) return (files, null);
        var enc = await Task.Run(() => files
            .Where(f => Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase) && Qpdf.NeedsPassword(f))
            .ToArray());
        if (enc.Length == 0) return (files, null);
        string? tmp = null;
        bool retry = false;
        while (true)
        {
            var pw = Dialogs.PdfPassword(enc, retry);
            if (pw == null) { if (tmp != null) Paths.TryDelete(tmp); throw new OperationCanceledException(); }
            tmp ??= Paths.Temp();
            try
            {
                var map = new Dictionary<string, string>();
                foreach (var f in enc)
                {
                    var dst = Path.Combine(tmp, Path.GetFileName(f));
                    await Qpdf.Decrypt(f, pw, dst);
                    map[f] = dst;
                }
                return (files.Select(f => map.TryGetValue(f, out var d) ? d : f).ToArray(), tmp);
            }
            catch (WrongPasswordException) { retry = true; }
        }
    }

    /// Roda op.One em cada arquivo, vários ao mesmo tempo (como o File Converter), cada um na sua vaga da barra.
    /// Devolve true se foi cancelado. Os resultados saem na ordem dos arquivos.
    public static Task<bool> EachPublic(Op op, string[] files, P p, Job job, List<string> outputs, List<string> errors) => Each(op, files, p, job, outputs, errors);

    static async Task<bool> Each(Op op, string[] files, P p, Job job, List<string> outputs, List<string> errors, Action<int, string>? onDone = null)
    {
        int n = files.Length, done = 0;
        bool canceled = false;
        var results = new string?[n];
        var gate = Gate(Parallelism(files));
        job.Slots(n);
        var tasks = files.Select((f, i) => Task.Run(async () =>
        {
            Job.Slot(i);
            try
            {
                await gate.WaitAsync(job.Cts.Token);
                try { Robust.CheckNotEmpty(f); results[i] = await op.One!(f, p); }
                finally { gate.Release(); }
            }
            catch (OperationCanceledException) { canceled = true; }
            catch (Exception ex)
            {
                lock (errors) errors.Add($"{(n > 1 ? Path.GetFileName(f) + ": " : "")}{Friendly(ex)}");
                Log.Write($"{op.Label} {f}: {ex}");
            }
            finally
            {
                Job.Progress(1);
                onDone?.Invoke(Interlocked.Increment(ref done), f);
            }
        })).ToArray();
        await Task.WhenAll(tasks);
        foreach (var r in results) if (r != null) outputs.Add(r);
        return canceled;
    }

    // Vagas compartilhadas por todos os trabalhos: dois lotes ao mesmo tempo (dois cliques no menu)
    // dividem as mesmas vagas em vez de somar e afogar o processador.
    static readonly Dictionary<int, SemaphoreSlim> gates = new();
    static SemaphoreSlim Gate(int par)
    {
        lock (gates)
        {
            if (!gates.TryGetValue(par, out var g)) gates[par] = g = new SemaphoreSlim(par);
            return g;
        }
    }

    /// Quantos arquivos processar ao mesmo tempo. Vídeo já usa o processador inteiro (ou a placa de vídeo),
    /// então vai um por vez; imagem e áudio usam um núcleo cada e ganham muito em paralelo.
    static int Parallelism(string[] files)
    {
        int cores = Environment.ProcessorCount;
        var kinds = files.Select(Catalog.KindOf).Distinct().ToList();
        if (kinds.Contains(Kind.Video)) return 1;
        if (kinds.Any(k => k is Kind.Doc or Kind.Slides or Kind.Sheet or Kind.Csv or Kind.Pdf or Kind.Epub or Kind.Html or Kind.Markdown or Kind.Text)) return 2;
        if (kinds.All(k => k is Kind.Image or Kind.Svg or Kind.Audio or Kind.Subtitle)) return Math.Clamp(cores / 2, 2, 8);
        return 2;
    }

    /// Traduz erros técnicos das ferramentas para algo que dá para entender.
    public static string Friendly(Exception ex)
    {
        var m = ex.Message;
        if (ex is FileNotFoundException || ex is DirectoryNotFoundException || m.Contains("No such file", StringComparison.OrdinalIgnoreCase))
            return "O arquivo não existe mais (foi movido ou apagado?)";
        if (ex is UnauthorizedAccessException) return "Sem permissão para gravar nessa pasta";
        if (ex is IOException io && (io.HResult & 0xFFFF) is 112 or 39) return "O disco está cheio";
        if (ex is IOException io2 && (io2.HResult & 0xFFFF) is 32 or 33) return "O arquivo está aberto em outro programa";
        if (ex is ImageMagick.MagickException) return "Não consegui ler essa imagem (arquivo danificado ou formato desconhecido)";
        if (m.Contains("Invalid data found", StringComparison.OrdinalIgnoreCase) || m.Contains("moov atom not found", StringComparison.OrdinalIgnoreCase)
            || m.Contains("Error opening input", StringComparison.OrdinalIgnoreCase))
            return "Não consegui ler esse arquivo de mídia (danificado ou incompleto?)";
        if (m.Contains("does not contain any stream", StringComparison.OrdinalIgnoreCase) || m.Contains("matches no streams", StringComparison.OrdinalIgnoreCase))
            return "Esse arquivo não tem a faixa necessária (sem áudio ou sem vídeo?)";
        if (ex is UglyToad.PdfPig.Core.PdfDocumentFormatException || m.Contains("not a PDF", StringComparison.OrdinalIgnoreCase))
            return "Esse PDF está danificado";
        if (m.Contains("password", StringComparison.OrdinalIgnoreCase) || m.Contains("encrypted", StringComparison.OrdinalIgnoreCase)
            || m.Contains("senha", StringComparison.OrdinalIgnoreCase) || m.Contains("criptografad", StringComparison.OrdinalIgnoreCase))
            return "Esse arquivo tem senha";
        // Conversores adaptados do Filee e bibliotecas de fora falam inglês técnico. O usuário recebe português; o log guarda o original.
        if (ex.TargetSite?.DeclaringType?.Namespace?.StartsWith("Filee", StringComparison.Ordinal) == true || LooksEnglish(m))
            return "Não consegui converter esse arquivo: ele está danificado ou usa um recurso que o Mexerica ainda não entende";
        return m;
    }

    static readonly System.Text.RegularExpressions.Regex english = new(
        @"\b(the|is|are|was|not|can't|cannot|invalid|unknown|failed|damaged|unsupported|missing|error|unexpected|file|stream|value|index)\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    static bool LooksEnglish(string m) =>
        m.Length > 0 && !m.Any(c => "ãõçáéíóúâêôàÃÕÇÁÉÍÓÚÂÊÔ".Contains(c)) && english.Matches(m).Count >= 2;

    // Modo de teste sem tela: --executar <Convert|Tools> <rótulo|*> <relatório.txt> [--p chave=valor ...] arquivos...
    static int SelfTest(string[] a)
    {
        var mode = Enum.Parse<Mode>(a[1]);
        var p = new P();
        var files = new List<string>();
        for (int i = 4; i < a.Length; i++)
        {
            if (a[i] == "--p" && i + 1 < a.Length) { var kv = a[++i].Split('=', 2); p[kv[0]] = kv.Length > 1 ? kv[1] : ""; }
            else files.Add(a[i]);
        }
        var fs = files.ToArray();
        var lines = new List<string>();
        var ops = Catalog.For(mode, fs);
        lines.Add("opcoes: " + string.Join(", ", ops.Select(o => o.Label)));
        int fails = 0;
        foreach (var op in ops.Where(o => a[2] == "*" || o.Label == a[2]))
        {
            if (op.Ui != null) { lines.Add($"PULA {op.Label} (só na tela)"); continue; }
            if (op.Ask != null && p.Count == 0 && a[2] == "*") { lines.Add($"PULA {op.Label} (precisa de ajustes)"); continue; }
            var t0 = DateTime.Now;
            var job = new Job(); Job.Current = job;
            try
            {
                var res = new List<string>();
                var errs = new List<string>();
                if (op.All != null) { var r = op.All(fs, p).GetAwaiter().GetResult(); if (r != null) res.Add(r); }
                else if (op.One != null) Each(op, fs, p, job, res, errs).GetAwaiter().GetResult();
                if (errs.Count > 0) throw new Exception(string.Join(" | ", errs));
                lines.Add($"OK   {op.Label} ({(DateTime.Now - t0).TotalSeconds:0.0}s): {string.Join(" | ", res)}");
            }
            catch (Exception ex) { fails++; var f = Friendly(ex); var raw = f == ex.Message ? "" : " | " + ex.Message; lines.Add($"ERRO {op.Label}: {f}{raw}"); }
            finally { Job.Current = null; Paths.ReleaseNames(); }
        }
        File.WriteAllLines(a[3], lines, new UTF8Encoding(false));
        return fails;
    }

    static void OpenSettings()
    {
        var existing = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        if (existing != null) { existing.Activate(); return; }
        var language = Tr.Code;
        if (new SettingsWindow().ShowDialog() == true)
        {
            if (language != Tr.Code)
            {
                tray.Dispose(); BuildTray();
                Tr.Window(wheel); Toast.LanguageChanged();
            }
            pauseItem.Checked = Config.Now.Paused;
        }
    }

    static void BuildTray()
    {
        var menu = new WF.ContextMenuStrip { RightToLeft = Tr.Rtl ? WF.RightToLeft.Yes : WF.RightToLeft.No };
        pauseItem = new WF.ToolStripMenuItem(Tr.T("Pausar")) { CheckOnClick = true, Checked = Config.Now.Paused };
        pauseItem.CheckedChanged += (_, _) =>
        {
            var c = Config.Now.Clone(); c.Paused = pauseItem.Checked; c.Save();
            tray.Text = c.Paused ? Tr.T("Mexerica (pausado)") : "Mexerica";
            if (c.Paused) wheel.Disarm();
        };
        var auto = new WF.ToolStripMenuItem(Tr.T("Iniciar com o Windows")) { Checked = AutoStart, CheckOnClick = true };
        auto.CheckedChanged += (_, _) => AutoStart = auto.Checked;
        var settings = new WF.ToolStripMenuItem(Tr.T("Configurações…"));
        settings.Click += (_, _) => OpenSettings();
        var help = new WF.ToolStripMenuItem(Tr.T("Como usar"));
        help.Click += (_, _) => HelpWindow.ShowOne();
        var testFolder = new WF.ToolStripMenuItem(Tr.T("Criar pasta de teste"));
        testFolder.Click += (_, _) => TestFolder.CreateAndOpen();
        updateItem = new WF.ToolStripMenuItem(Tr.T("Atualizar o Mexerica")) { Visible = false, Font = new System.Drawing.Font(WF.Control.DefaultFont, System.Drawing.FontStyle.Bold) };
        updateItem.Click += (_, _) => _ = Updater.InstallOffered();
        if (offeredVersion != null) OfferUpdate(offeredVersion);
        var recent = new WF.ToolStripMenuItem(Tr.T("Conversões recentes…"));
        recent.Click += (_, _) => HistoryWindow.ShowOne();
        var log = new WF.ToolStripMenuItem(Tr.T("Abrir registro de erros"));
        log.Click += (_, _) => { var f = Path.Combine(Paths.Data, "log.txt"); if (File.Exists(f)) System.Diagnostics.Process.Start("notepad.exe", f); else Toast.Show("Nenhum erro registrado", "O registro está vazio."); };
        var quit = new WF.ToolStripMenuItem(Tr.T("Sair"));
        quit.Click += (_, _) => Quit();
        menu.Items.AddRange(new WF.ToolStripItem[] { updateItem, help, testFolder, recent, settings, pauseItem, auto, new WF.ToolStripSeparator(), log, quit });
        tray = new WF.NotifyIcon { Icon = MakeIcon(), Text = Config.Now.Paused ? Tr.T("Mexerica (pausado)") : "Mexerica", Visible = true, ContextMenuStrip = menu };
        // Clique simples abre o mesmo menu do botão direito (sem mensagem no canto da tela).
        // Espera o tempo do clique duplo: clique duplo abre as Configurações e não deve piscar o menu antes.
        var single = new WF.Timer { Interval = WF.SystemInformation.DoubleClickTime };
        single.Tick += (_, _) =>
        {
            single.Stop();
            typeof(WF.NotifyIcon).GetMethod("ShowContextMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(tray, null);
        };
        tray.MouseClick += (_, e) => { if (e.Button == WF.MouseButtons.Left) { single.Stop(); single.Start(); } };
        tray.MouseDoubleClick += (_, e) => { if (e.Button == WF.MouseButtons.Left) { single.Stop(); OpenSettings(); } };
        if (!AutoStartChosen) AutoStart = true;
    }

    static bool AutoStartChosen
    {
        get { using var k = Registry.CurrentUser.OpenSubKey(@"Software\Mexerica"); return k?.GetValue("AutoInicioEscolhido") != null; }
    }

    static bool AutoStart
    {
        get { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue("Mexerica") != null; }
        set
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (value) k.SetValue("Mexerica", $"\"{Environment.ProcessPath}\" --silencioso");
                else k.DeleteValue("Mexerica", false);
            }
            using var s = Registry.CurrentUser.CreateSubKey(@"Software\Mexerica");
            s.SetValue("AutoInicioEscolhido", 1);
        }
    }

    static Icon MakeIcon()
    {
        try { return new Icon(Path.Combine(AppContext.BaseDirectory, "mexerica.ico"), WF.SystemInformation.SmallIconSize); }
        catch { return Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application; }
    }

    /// Devolve ao Windows a memória das janelas e imagens que já fecharam (o Mexerica fica o dia todo na bandeja).
    public static void Trim()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        try { Native.SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1); } catch { }
    }
}
