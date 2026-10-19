using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Mexerica;

/// Arquivos selecionados na janela do Explorador (ou na área de trabalho) que está em primeiro plano.
static class Explorer
{
    [ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    interface IShellWindows
    {
        int Count { get; }
        [return: MarshalAs(UnmanagedType.IDispatch)] object Item(object index);
        [return: MarshalAs(UnmanagedType.IUnknown)] object _NewEnum();
        int Register([MarshalAs(UnmanagedType.IDispatch)] object pid, int hwnd, int swClass);
        int RegisterPending(int tid, ref object loc, ref object locRoot, int swClass);
        void Revoke(int cookie);
        void OnNavigate(int cookie, ref object loc);
        void OnActivated(int cookie, bool active);
        [return: MarshalAs(UnmanagedType.IDispatch)] object FindWindowSW(ref object loc, ref object locRoot, int swClass, out int hwnd, int options);
    }

    static readonly Guid ShellWindowsClsid = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");

    public static bool IsExplorer(IntPtr h)
    {
        var c = Native.ClassOf(Native.GetAncestor(h, 2));
        return c is "CabinetWClass" or "ExploreWClass" or "Progman" or "WorkerW";
    }

    public static string[] Selection()
    {
        var fg = Native.GetAncestor(Native.GetForegroundWindow(), 2);
        var cls = Native.ClassOf(fg);
        try
        {
            var sw = (IShellWindows)Activator.CreateInstance(Type.GetTypeFromCLSID(ShellWindowsClsid)!)!;
            if (cls is "Progman" or "WorkerW")
            {
                object loc = 0, root = Type.Missing;
                dynamic desk = sw.FindWindowSW(ref loc, ref root, 8 /*SWC_DESKTOP*/, out _, 1 /*SWFO_NEEDDISPATCH*/);
                return Paths(desk.Document.SelectedItems());
            }
            var title = Native.TextOf(fg);
            string[]? fallback = null;
            for (int i = 0; i < sw.Count; i++)
            {
                dynamic? w = sw.Item(i);
                if (w == null) continue;
                try
                {
                    if ((long)w.HWND != (long)fg) continue;
                    var sel = Paths(w.Document.SelectedItems());
                    // Com abas no Windows 11, várias entradas têm a mesma janela: a da aba ativa tem o nome no título.
                    string name = w.LocationName;
                    if (title.StartsWith(name, StringComparison.OrdinalIgnoreCase) && sel.Length > 0) return sel;
                    if (sel.Length > 0) fallback ??= sel;
                }
                catch { }
            }
            return fallback ?? Array.Empty<string>();
        }
        catch (Exception ex) { Log.Write("seleção do Explorador: " + ex.Message); return Array.Empty<string>(); }
    }

    static string[] Paths(dynamic items)
    {
        var list = new List<string>();
        foreach (dynamic it in items)
        {
            try { string p = it.Path; if (File.Exists(p) || Directory.Exists(p)) list.Add(p); } catch { }
        }
        return list.ToArray();
    }
}
