using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Mexerica;

/// Submenu "Mexerica" no botão direito do Explorador, por tipo de arquivo: converter direto, ferramentas e a roda.
/// Fica só no registro do usuário (HKCU), sem precisar de administrador nem de extensão COM.
/// Cada item chama "Mexerica.exe --menu <modo> <opção> <arquivo>"; com vários arquivos o Windows abre um
/// processo por arquivo, e todos entregam o pedido à cópia que já está na bandeja, que junta tudo num trabalho só.
static class ShellMenu
{
    const string Classes = @"Software\Classes";
    const string Prefix = "Mexerica.Menu.";
    const string Signature = "MenuAssinatura";
    public const string Wheel = "__roda";

    // Texto do item no menu (a roda usa rótulos curtos; no menu cabe a frase inteira).
    static readonly Dictionary<string, string> text = new()
    {
        ["Comprimir"] = "Comprimir", ["Tamanho"] = "Mudar o tamanho", ["Girar"] = "Girar ou espelhar", ["Editar"] = "Editar (recortar, ajustar, tarjar)",
        ["Fundo"] = "Fundo e margem", ["Metadados"] = "Ver e editar metadados", ["Ler QR"] = "Ler código QR", ["PDF"] = "Fazer PDF",
        ["Colagem"] = "Colagem", ["Cortar"] = "Cortar um trecho", ["Recortar"] = "Recortar (enquadrar)", ["Velocidade"] = "Mudar a velocidade",
        ["Dividir"] = "Dividir em partes", ["Quadro"] = "Salvar um quadro como foto", ["Áudio"] = "Extrair o áudio", ["Mudo"] = "Tirar o som",
        ["Juntar"] = "Juntar num arquivo só", ["Normalizar"] = "Normalizar o volume", ["Bipe"] = "Bipe por cima (censurar)", ["Canais"] = "Canais (mono, estéreo)",
        ["Organizar"] = "Organizar as páginas", ["Tarjar"] = "Tarjar (esconder trechos)", ["Extrair"] = "Extrair aqui", ["ZIP"] = "Compactar em ZIP",
        ["TAR.GZ"] = "Compactar em TAR.GZ", ["LibreOffice"] = "Instalar o LibreOffice (para converter documentos)",
    };

    static string Exe => Environment.ProcessPath!;
    const string Clsid = "{7A1C3E52-4B9D-4F0E-9C2A-6D5E8F1B3A47}";
    static string ListFile => Path.Combine(Paths.Data, "menu.txt");
    static string DllSource => Path.Combine(AppContext.BaseDirectory, "MexericaMenu.dll");

    /// Põe ou tira o menu conforme a configuração. Só regrava quando algo mudou (programa, versão, ferramentas).
    public static void Apply()
    {
        Task.Run(() =>
        {
            try
            {
                using var app = Registry.CurrentUser.CreateSubKey(@"Software\Mexerica");
                if (!Config.Now.ShellMenu) { if (app.GetValue(Signature) != null) Remove(); return; }
                if (!File.Exists(DllSource)) { Log.Write("menu do botão direito: falta MexericaMenu.dll ao lado do programa"); return; }
                var dll = InstallDll();
                var sig = $"{Exe}|{dll}|{typeof(ShellMenu).Assembly.GetName().Version}|{Office.Ok}|{Ff.Ok}|{Gs.Ok}|{Qpdf.Ok}|{Meta.Ok}|{Tr.Code}|7";
                if (app.GetValue(Signature) as string == sig && File.Exists(ListFile)) return;
                RemoveStatic();
                WriteList();
                Register(dll);
                app.SetValue(Signature, sig);
                Refresh();
            }
            catch (Exception ex) { Log.Write("menu do botão direito: " + ex); }
        });
    }

    /// O Explorador prende a DLL enquanto está aberto; por isso cada versão roda de uma cópia com nome próprio
    /// (o Mexerica pode ser atualizado sem reiniciar o Explorador). Cópias antigas saem quando estiverem soltas.
    static string InstallDll()
    {
        var bytes = File.ReadAllBytes(DllSource);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..10].ToLowerInvariant();
        var dir = Path.Combine(Paths.Data, "menu");
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, $"MexericaMenu-{hash}.dll");
        if (!File.Exists(target)) { File.WriteAllBytes(target + ".tmp", bytes); File.Move(target + ".tmp", target, true); }
        foreach (var old in Directory.GetFiles(dir, "MexericaMenu-*.dll").Where(f => !f.Equals(target, StringComparison.OrdinalIgnoreCase)))
            try { File.Delete(old); } catch { }
        return target;
    }

    static void Register(string dll)
    {
        using (var k = Registry.CurrentUser.CreateSubKey($@"{Classes}\CLSID\{Clsid}"))
        {
            k.SetValue("", "Mexerica");
            using var s = k.CreateSubKey("InprocServer32");
            s.SetValue("", dll);
            s.SetValue("ThreadingModel", "Apartment");
        }
        foreach (var where in new[] { "*", "Directory" })
        {
            using var h = Registry.CurrentUser.CreateSubKey($@"{Classes}\{where}\shellex\ContextMenuHandlers\Mexerica");
            h.SetValue("", Clsid);
        }
        using var ap = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved");
        ap.SetValue(Clsid, "Mexerica");
    }

    /// Tira tudo que o Mexerica pôs no registro (usado também pelo desinstalador: Mexerica.exe --remover-menu).
    public static void Remove()
    {
        RemoveStatic();
        using (var classes = Registry.CurrentUser.OpenSubKey(Classes, true))
            if (classes != null)
            {
                foreach (var where in new[] { "*", "Directory" })
                    try { classes.DeleteSubKeyTree($@"{where}\shellex\ContextMenuHandlers\Mexerica", false); } catch { }
                try { classes.DeleteSubKeyTree($@"CLSID\{Clsid}", false); } catch { }
            }
        try { using var ap = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved", true); ap?.DeleteValue(Clsid, false); } catch { }
        try { using var app = Registry.CurrentUser.OpenSubKey(@"Software\Mexerica", true); app?.DeleteValue(Signature, false); } catch { }
        Paths.TryDelete(ListFile);
        try { foreach (var f in Directory.GetFiles(Path.Combine(Paths.Data, "menu"))) try { File.Delete(f); } catch { } } catch { }
        Refresh();
    }

    /// Menus da versão anterior, feitos só pelo registro (limitados a 16 itens): saem na atualização.
    static void RemoveStatic()
    {
        using var classes = Registry.CurrentUser.OpenSubKey(Classes, true);
        if (classes == null) return;
        foreach (var name in classes.GetSubKeyNames().Where(n => n.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)))
            try { classes.DeleteSubKeyTree(name, false); } catch { }
        foreach (var ext in Catalog.Extensions)
            foreach (var v in new[] { "Mexerica", "MexericaFerramentas" })
                try { classes.DeleteSubKeyTree($@"SystemFileAssociations\{ext}\shell\{v}", false); } catch { }
        try { classes.DeleteSubKeyTree(@"Directory\shell\Mexerica", false); } catch { }
    }

    /// Lista que a extensão lê: por extensão, grupo (C converter, F ferramentas, R roda), modo, rótulo e texto.
    static void WriteList()
    {
        var sb = new StringBuilder();
        sb.Append("exe\t").Append(Exe).Append('\n');
        void Line(string key, string group, Mode mode, string label, string caption) =>
            sb.Append(key).Append('\t').Append(group).Append('\t').Append(mode).Append('\t').Append(label).Append('\t').Append(Tr.T(caption).Replace('\t', ' ')).Append('\n');
        var fake = Path.Combine(Path.GetTempPath(), "mexerica-menu-inexistente");
        foreach (var ext in Catalog.Extensions)
        {
            var e = ext.ToLowerInvariant();
            string one = fake + e, two = fake + "-2" + e;
            // Tipos que o Mexerica trata juntos (SVG com imagens, CSV com planilhas) contam como um só.
            var kind = Catalog.KindOf(one) switch { Kind.Svg => Kind.Image, Kind.Csv => Kind.Sheet, var k => k };
            sb.Append("kind\t").Append(e).Append('\t').Append(kind).Append('\n');
            foreach (var op in Catalog.For(Mode.Convert, new[] { one }))
                Line(e, "C", Mode.Convert, op.Label, op.Label == "LibreOffice" && op.Ui != null ? text["LibreOffice"] : Tr.T($"Para {op.Label}") + (op.Sub.Length > 0 ? $"  ({Tr.T(op.Sub)})" : ""));
            // Ferramentas de um arquivo e de vários juntas: a que não servir para a seleção avisa na hora.
            foreach (var op in Tools(new[] { one }, new[] { one, two })) Line(e, "F", Mode.Tools, op.Label, Text(op));
            Line(e, "R", Mode.Convert, Wheel, "Abrir a roda…");
        }
        var dir = Path.GetTempPath().TrimEnd('\\');
        foreach (var op in Tools(new[] { dir }, new[] { dir, dir })) Line("<pasta>", "F", Mode.Tools, op.Label, Text(op));
        Line("<pasta>", "R", Mode.Convert, Wheel, "Abrir a roda…");
        // Arquivo de tipo desconhecido, e seleção com tipos diferentes: compactar e a roda (Ações vale para tudo).
        string x1 = fake + ".xyz", x2 = fake + ".jpg", x3 = fake + ".mp4";
        foreach (var (key, one, many) in new[] { ("<outro>", new[] { x1 }, new[] { x1, x1 }), ("<misto>", new[] { x2, x3 }, new[] { x2, x3 }) })
        {
            foreach (var op in Tools(one, many)) Line(key, "F", Mode.Tools, op.Label, Text(op));
            Line(key, "R", Mode.Convert, Wheel, "Abrir a roda…");
        }
        Directory.CreateDirectory(Paths.Data);
        File.WriteAllText(ListFile + ".tmp", sb.ToString(), new UTF8Encoding(false));
        File.Move(ListFile + ".tmp", ListFile, true);
    }

    static List<Op> Tools(string[] one, string[] many) =>
        Catalog.For(Mode.Tools, one).Concat(Catalog.For(Mode.Tools, many))
            .GroupBy(o => o.Label).Select(g => g.First())
            .OrderBy(o => o.Label is "ZIP" or "TAR.GZ" ? 1 : 0).ToList(); // compactar por último

    static string Text(Op op) => text.TryGetValue(op.Label, out var t) ? t : op.Sub.Length > 0 ? $"{Tr.T(op.Label)} ({Tr.T(op.Sub)})" : Tr.T(op.Label);

    [DllImport("shell32.dll")] static extern void SHChangeNotify(int eventId, uint flags, IntPtr a, IntPtr b);
    static void Refresh() { try { SHChangeNotify(0x08000000 /*ASSOCCHANGED*/, 0, IntPtr.Zero, IntPtr.Zero); } catch { } }

    // ---------- Entrega dos pedidos à cópia que está na bandeja ----------

    static string PipeName => $"Mexerica.Menu.{Environment.UserName}.{System.Diagnostics.Process.GetCurrentProcess().SessionId}";

    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);

    /// Processo aberto pelo menu: passa o pedido adiante e sai. Devolve false se ninguém atendeu.
    public static bool Send(string mode, string label, IEnumerable<string> files)
    {
        // O Explorador deu a este processo o direito de trazer janela para frente; repassa para a bandeja
        // (senão os ajustes de Comprimir, Cortar etc. abririam atrás do Explorador).
        AllowSetForegroundWindow(-1);
        var msg = Encoding.UTF8.GetBytes($"{mode}\t{label}\t{string.Join("\t", files)}");
        var until = Environment.TickCount64 + 15000;
        while (Environment.TickCount64 < until)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                pipe.Connect(1000);
                pipe.Write(msg);
                pipe.Flush();
                return true;
            }
            catch { Thread.Sleep(100); }
        }
        return false;
    }

    /// Na cópia da bandeja: escuta os pedidos (várias conexões ao mesmo tempo).
    public static void Listen(Action<Mode, string, string> onRequest)
    {
        for (int w = 0; w < 4; w++)
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                        await server.WaitForConnectionAsync();
                        using var ms = new MemoryStream();
                        await server.CopyToAsync(ms);
                        var f = Encoding.UTF8.GetString(ms.ToArray()).Split('\t');
                        if (f.Length >= 3 && Enum.TryParse<Mode>(f[0], out var mode))
                            foreach (var file in f.Skip(2).Where(x => x.Length > 0)) onRequest(mode, f[1], file);
                    }
                    catch (Exception ex) { Log.Write("menu: " + ex.Message); await Task.Delay(200); }
                }
            });
    }
}
