using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mexerica;

static class Shell
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint attr; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string name; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string type; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO fi, uint size, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    public static ImageSource? Icon(string path)
    {
        var fi = new SHFILEINFO();
        if (SHGetFileInfo(path, 0, ref fi, (uint)Marshal.SizeOf(fi), 0x100 /*ICON*/ | 0x0 /*LARGE*/) == IntPtr.Zero || fi.hIcon == IntPtr.Zero) return null;
        try { return Imaging.CreateBitmapSourceFromHIcon(fi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); }
        finally { DestroyIcon(fi.hIcon); }
    }

    /// Abre com o programa padrão do Windows.
    public static void Open(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("abrir: " + ex.Message); }
    }

    /// Mostra o "Abrir com" do Windows.
    public static void OpenWith(string path)
    {
        try { System.Diagnostics.Process.Start("rundll32.exe", $"shell32.dll,OpenAs_RunDLL {path}"); }
        catch (Exception ex) { Log.Write("abrir com: " + ex.Message); }
    }
}
