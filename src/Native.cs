using System;
using System.Runtime.InteropServices;

namespace Mexerica;

static class Native
{
    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr extra; }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; public uint pad1, pad2; }

    public const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x80;
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;

    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT p, uint flags);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr mon, int type, out uint dx, out uint dy);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)] public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }
    [DllImport("user32.dll")] public static extern int GetMessage(out MSG m, IntPtr h, uint min, uint max);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] public static extern IntPtr DispatchMessage(ref MSG m);
    [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint thread, uint msg, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")] public static extern bool SetProcessWorkingSetSize(IntPtr p, nint min, nint max);

    public static void DarkTitle(IntPtr h) { int on = 1; try { DwmSetWindowAttribute(h, 20, ref on, 4); } catch { } }

    public static string ClassOf(IntPtr h) { var sb = new System.Text.StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }
    public static string TextOf(IntPtr h) { var sb = new System.Text.StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }

    /// Aperta e solta uma tecla (marcada como nossa, para o gancho deixar passar).
    public const nint OwnInput = 0x4D455852; // "MEXR"
    public static void Tap(byte vk) { keybd_event(vk, 0, 0, OwnInput); keybd_event(vk, 0, 2, OwnInput); }

    public static double ScaleAt(POINT p)
    {
        var mon = MonitorFromPoint(p, 2);
        return GetDpiForMonitor(mon, 0, out var dx, out _) == 0 ? dx / 96.0 : 1.0;
    }

    public static void MakeToolWindow(IntPtr h, bool noActivate)
    {
        long ex = (long)GetWindowLongPtr(h, GWL_EXSTYLE) | WS_EX_TOOLWINDOW;
        if (noActivate) ex |= WS_EX_NOACTIVATE;
        SetWindowLongPtr(h, GWL_EXSTYLE, new IntPtr(ex));
    }

    // Mexe o mouse 1 px e volta: faz o arrasto do Windows perceber a janela nova debaixo do cursor.
    public static void NudgeMouse()
    {
        var a = new INPUT { type = 0, mi = new MOUSEINPUT { dx = 1, dy = 0, dwFlags = 0x0001 } };
        var b = new INPUT { type = 0, mi = new MOUSEINPUT { dx = -1, dy = 0, dwFlags = 0x0001 } };
        SendInput(2, new[] { a, b }, Marshal.SizeOf<INPUT>());
    }
}
