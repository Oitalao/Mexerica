using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace Mexerica;

static class Actions
{
    public static List<Op> List(string[] files) => new()
    {
        new Op { Label = "Compartilhar", Sub = "por perto", Ui = Share },
        new Op { Label = "Flutuar", Sub = "deixar à mão", Ui = fs => new FloatWindow(fs).Show() },
        new Op { Label = "Copiar", Sub = "para pasta…", Ui = fs => CopyOrMove(fs, false) },
        new Op { Label = "Mover", Sub = "para pasta…", Ui = fs => CopyOrMove(fs, true) },
        new Op { Label = "Caminho", Sub = "copiar", Ui = fs => { Clipboard.SetText(string.Join("\r\n", fs)); App.Toast.Show("Caminho copiado", fs.Length == 1 ? fs[0] : $"{fs.Length} caminhos"); } },
        new Op { Label = "Terminal", Sub = "abrir aqui", Ui = Terminal },
        new Op { Label = "Renomear", Sub = "vários de uma vez", Ui = Rename },
    };

    /// Nomes novos, na ordem dada: {n} = número, {nome} = nome atual. Nome que já existe (e não é de outro da lista) ganha (1), (2)…
    public static List<(string from, string to)> RenamePlan(string[] files, P p)
    {
        int n0 = (int)p.D("inicio", 1), dig = Math.Clamp((int)p.D("digitos", 2), 1, 6);
        var model = p.S("modelo", "{nome}");
        var mine = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plan = new List<(string, string)>();
        for (int i = 0; i < files.Length; i++)
        {
            var f = files[i].TrimEnd('\\');
            bool dir = Directory.Exists(f);
            var ext = dir ? "" : Path.GetExtension(f);
            var cur = dir ? Path.GetFileName(f) : Path.GetFileNameWithoutExtension(f);
            var name = model.Replace("{n}", (n0 + i).ToString(new string('0', dig))).Replace("{nome}", cur);
            name = Paths.CleanName(name + ".x") ?? cur; // tira caracteres proibidos (o ".x" protege um ponto no fim do nome)
            var folder = Path.GetDirectoryName(f)!;
            string to = Path.Combine(folder, name + ext);
            for (int k = 1; taken.Contains(to) || ((File.Exists(to) || Directory.Exists(to)) && !mine.Contains(to)); k++)
                to = Path.Combine(folder, $"{name} ({k}){ext}");
            taken.Add(to);
            plan.Add((f, to));
        }
        return plan;
    }

    static void Rename(string[] fs)
    {
        var p = Dialogs.Rename(fs);
        if (p == null) return;
        var plan = RenamePlan(Form.Ordered(fs, p), p).Where(x => !string.Equals(x.from, x.to, StringComparison.Ordinal)).ToList();
        if (plan.Count == 0) { App.Toast.Show("Nada a fazer", "Os nomes já estavam assim."); return; }
        var done = Apply(plan);
        App.Toast.Done("Renomeados", done.Count == 1 ? Path.GetFileName(done[0].to) : $"{done.Count} arquivos", done[0].to,
            undo: () => { Apply(done.Select(x => (x.to, x.from)).ToList()); App.Toast.Show("Desfeito", "Os nomes voltaram ao que eram."); });
    }

    /// Renomeia em duas etapas (primeiro para nomes provisórios), para que trocar nomes entre si não esbarre.
    static List<(string from, string to)> Apply(List<(string from, string to)> plan)
    {
        var tmp = new List<(string tmp, string from, string to)>();
        try
        {
            foreach (var (from, to) in plan)
            {
                var t = Path.Combine(Path.GetDirectoryName(from)!, $".mexerica-{Guid.NewGuid():N}");
                if (Directory.Exists(from)) Directory.Move(from, t); else File.Move(from, t);
                tmp.Add((t, from, to));
            }
            foreach (var (t, _, to) in tmp)
                if (Directory.Exists(t)) Directory.Move(t, to); else File.Move(t, to);
            return plan;
        }
        catch
        {
            // Algo deu errado (arquivo aberto em outro programa): devolve o que já foi mexido ao nome original.
            foreach (var (t, from, to) in tmp)
                try
                {
                    if (File.Exists(t)) File.Move(t, from); else if (Directory.Exists(t)) Directory.Move(t, from);
                    else if (File.Exists(to)) File.Move(to, from); else if (Directory.Exists(to)) Directory.Move(to, from);
                }
                catch { }
            throw new Exception("Não deu para renomear: algum arquivo está aberto em outro programa");
        }
    }

    static void Terminal(string[] fs)
    {
        var p = fs[0].TrimEnd('\\');
        var dir = Directory.Exists(p) ? p : Path.GetDirectoryName(p)!;
        var wt = Paths.FindTool("wt.exe") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
        if (File.Exists(wt)) { var psi = new ProcessStartInfo(wt) { UseShellExecute = false }; psi.ArgumentList.Add("-d"); psi.ArgumentList.Add(dir); Process.Start(psi); }
        else Process.Start(new ProcessStartInfo("powershell.exe") { UseShellExecute = true, WorkingDirectory = dir, Arguments = "-NoExit" });
    }

    static async void CopyOrMove(string[] fs, bool move)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = Tr.T(move ? "Mover para…" : "Copiar para…") };
        if (dlg.ShowDialog() != true) return;
        var dest = dlg.FolderName;
        App.Toast.Show(move ? "Movendo…" : "Copiando…", fs.Length == 1 ? Path.GetFileName(fs[0].TrimEnd('\\')) : $"{fs.Length} itens", sticky: true);
        try
        {
            string? last = null;
            await Task.Run(() =>
            {
                foreach (var f in fs)
                {
                    var src = f.TrimEnd('\\');
                    var target = Path.Combine(dest, Path.GetFileName(src));
                    if (string.Equals(Path.GetFullPath(target), Path.GetFullPath(src), StringComparison.OrdinalIgnoreCase)) continue;
                    target = Unique(target);
                    if (Directory.Exists(src))
                    {
                        if (move && SameVolume(src, dest)) Directory.Move(src, target);
                        else { CopyDir(src, target); if (move) Directory.Delete(src, true); }
                    }
                    else if (move) File.Move(src, target);
                    else File.Copy(src, target);
                    last = target;
                }
            });
            App.Toast.Show(move ? "Movido" : "Copiado", dest, last);
        }
        catch (Exception ex) { App.Toast.Show("Não deu certo", ex.Message, error: true); Log.Write(ex.ToString()); }
    }

    static string Unique(string p)
    {
        if (!File.Exists(p) && !Directory.Exists(p)) return p;
        var dir = Path.GetDirectoryName(p)!; var b = Path.GetFileNameWithoutExtension(p); var e = Path.GetExtension(p);
        if (Directory.Exists(p)) { b = Path.GetFileName(p); e = ""; }
        for (int i = 2; ; i++) { var q = Path.Combine(dir, $"{b} {i}{e}"); if (!File.Exists(q) && !Directory.Exists(q)) return q; }
    }

    static bool SameVolume(string a, string b) =>
        string.Equals(Path.GetPathRoot(Path.GetFullPath(a)), Path.GetPathRoot(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);

    static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
    }

    // ---- Compartilhar (painel do Windows: Compartilhamento por Proximidade, e-mail, apps) ----

    [ComImport, Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow(IntPtr appWindow, [In] ref Guid riid);
        void ShowShareUIForWindow(IntPtr appWindow);
    }

    static ShareHost? host;

    static void Share(string[] fs)
    {
        host?.Close();
        host = new ShareHost(fs);
        host.Show();
    }

    /// Janela invisível dona do painel de compartilhar (o Windows exige uma janela).
    sealed class ShareHost : Window
    {
        readonly string[] files;

        public ShareHost(string[] files)
        {
            this.files = files;
            Width = 1; Height = 1; WindowStyle = WindowStyle.None; ShowInTaskbar = false; AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent; Topmost = true;
            Native.GetCursorPos(out var p); Left = p.X / Native.ScaleAt(p); Top = p.Y / Native.ScaleAt(p);
            Loaded += (_, _) => Open();
        }

        void Open()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var factory = WinRT.ActivationFactory.Get("Windows.ApplicationModel.DataTransfer.DataTransferManager");
                var interop = (IDataTransferManagerInterop)Marshal.GetObjectForIUnknown(factory.ThisPtr);
                var iid = new Guid("A5CAEE9B-8708-49D1-8D36-67D25A8DA00C"); // IDataTransferManager
                var ptr = interop.GetForWindow(hwnd, ref iid);
                var dtm = WinRT.MarshalInterface<Windows.ApplicationModel.DataTransfer.DataTransferManager>.FromAbi(ptr);
                Marshal.Release(ptr);
                dtm.DataRequested += async (s, e) =>
                {
                    var def = e.Request.GetDeferral();
                    try
                    {
                        e.Request.Data.Properties.Title = files.Length == 1 ? Path.GetFileName(files[0]) : $"{files.Length} arquivos";
                        var items = new List<Windows.Storage.IStorageItem>();
                        foreach (var f in files)
                        {
                            if (Directory.Exists(f)) items.Add(await Windows.Storage.StorageFolder.GetFolderFromPathAsync(f));
                            else items.Add(await Windows.Storage.StorageFile.GetFileFromPathAsync(f));
                        }
                        e.Request.Data.SetStorageItems(items);
                    }
                    catch (Exception ex) { Log.Write("share: " + ex); }
                    finally { def.Complete(); }
                };
                dtm.TargetApplicationChosen += (_, _) => Dispatcher.BeginInvoke(Close);
                interop.ShowShareUIForWindow(hwnd);
                // Fecha a janela de apoio depois de um tempo, escolhendo ou não.
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(3) };
                t.Tick += (_, _) => { t.Stop(); Close(); };
                t.Start();
            }
            catch (Exception ex)
            {
                Log.Write("share: " + ex);
                App.Toast.Show("Não deu para compartilhar", ex.Message, error: true);
                Close();
            }
        }
    }
}
