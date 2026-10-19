using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Mexerica;

/// Ganchos globais de mouse e teclado. Sabe se o botão esquerdo está segurando um arrasto,
/// repassa as teclas apertadas durante o arrasto (ou com a roda aberta) e percebe o atalho da roda pelo teclado.
sealed class InputHooks : IDisposable
{
    readonly Native.HookProc mouseProc, keyProc;
    IntPtr mouseHook, keyHook;
    uint threadId;
    readonly HashSet<uint> held = new(), swallowed = new();
    bool down, touch, holdFired;
    Native.POINT downPt;
    uint downTime;

    public bool ButtonDown => down;
    public bool Dragging { get; private set; }

    /// Chamado no keydown (sem repetição). Devolve true para engolir a tecla.
    public Func<uint, bool>? OnKey;
    /// Atalho da roda pelo teclado; devolve true para engolir.
    public Func<bool>? OnHotkey;
    public Func<bool>? WheelOpen;
    public event Action? MouseUp;
    public event Action<Native.POINT>? MouseDown;
    public event Action? Escape;
    /// Toque: segurou parado e começou a arrastar.
    public event Action? TouchHold;

    /// Os ganchos rodam numa linha de execução só deles, com fila de mensagens própria:
    /// se a tela do Mexerica estiver ocupada, o mouse do Windows não trava e o Windows não desliga os ganchos.
    /// Os eventos chegam nessa linha; quem mexe em janela tem que repassar para a linha da tela.
    public InputHooks()
    {
        mouseProc = MouseCb;
        keyProc = KeyCb;
        using var ready = new System.Threading.ManualResetEventSlim();
        Exception? err = null;
        var t = new System.Threading.Thread(() =>
        {
            try
            {
                var mod = Native.GetModuleHandle(null);
                mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, mouseProc, mod, 0);
                keyHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, keyProc, mod, 0);
                if (mouseHook == IntPtr.Zero || keyHook == IntPtr.Zero) throw new Win32Exception();
                threadId = Native.GetCurrentThreadId();
            }
            catch (Exception e) { err = e; ready.Set(); return; }
            ready.Set();
            while (Native.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0) { Native.TranslateMessage(ref msg); Native.DispatchMessage(ref msg); }
            Native.UnhookWindowsHookEx(mouseHook);
            Native.UnhookWindowsHookEx(keyHook);
        }) { IsBackground = true, Name = "Mexerica ganchos", Priority = System.Threading.ThreadPriority.AboveNormal };
        t.Start();
        ready.Wait();
        if (err != null) throw err;
    }

    IntPtr MouseCb(int n, IntPtr w, IntPtr l)
    {
        if (n >= 0)
        {
            var s = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(l);
            switch ((int)w)
            {
                case 0x201:
                    down = true; Dragging = false; downPt = s.pt; downTime = s.time; holdFired = false;
                    touch = ((long)s.extra & 0xFFFFFF00) == 0xFF515700; // toque e caneta vêm marcados assim
                    MouseDown?.Invoke(s.pt);
                    break;
                case 0x200:
                    if (down && !Dragging && (Math.Abs(s.pt.X - downPt.X) > 4 || Math.Abs(s.pt.Y - downPt.Y) > 4))
                    {
                        Dragging = true;
                        if (touch && !holdFired && s.time - downTime >= 450) { holdFired = true; TouchHold?.Invoke(); }
                    }
                    break;
                case 0x202: down = false; Dragging = false; MouseUp?.Invoke(); break;
            }
        }
        return Native.CallNextHookEx(IntPtr.Zero, n, w, l);
    }

    static uint Base(uint vk) => vk switch { 0xA1 => 0xA0, 0xA3 => 0xA2, 0xA5 => 0xA4, 0x10 => 0xA0, 0x11 => 0xA2, 0x12 => 0xA4, _ => vk };

    IntPtr KeyCb(int n, IntPtr w, IntPtr l)
    {
        if (n >= 0)
        {
            var k = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(l);
            if (k.extra == Native.OwnInput) return Native.CallNextHookEx(IntPtr.Zero, n, w, l);
            int msg = (int)w;
            bool isDown = msg == 0x100 || msg == 0x104, isUp = msg == 0x101 || msg == 0x105;
            if (isDown)
            {
                bool repeat = !held.Add(k.vkCode);
                if (repeat && swallowed.Contains(k.vkCode)) return (IntPtr)1;
                bool open = WheelOpen?.Invoke() ?? false;
                if (k.vkCode == 0x1B && open) Escape?.Invoke();
                else if (!repeat && (Dragging || open) && OnKey != null && OnKey(Base(k.vkCode)))
                {
                    swallowed.Add(k.vkCode);
                    return (IntPtr)1;
                }
                else if (!repeat && !Dragging && !open && IsHotkey(k.vkCode) && OnHotkey != null && OnHotkey())
                {
                    swallowed.Add(k.vkCode);
                    return (IntPtr)1;
                }
            }
            else if (isUp)
            {
                held.Remove(k.vkCode);
                if (swallowed.Remove(k.vkCode)) return (IntPtr)1;
            }
        }
        return Native.CallNextHookEx(IntPtr.Zero, n, w, l);
    }

    static bool Pressed(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;

    static bool IsHotkey(uint vk)
    {
        var h = Config.Now.Hotkey;
        if (string.IsNullOrEmpty(h)) return false;
        var parts = h.Split('+');
        uint key = parts[^1] switch { "space" => 0x20, var c when c.Length == 1 => (uint)char.ToUpperInvariant(c[0]), _ => 0 };
        if (vk != key) return false;
        bool Has(string m) => Array.IndexOf(parts, m) >= 0;
        bool ctrl = Has("ctrl"), shift = Has("shift"), alt = Has("alt"), win = Has("win");
        return Pressed(0x11) == ctrl && Pressed(0x10) == shift && Pressed(0x12) == alt && (Pressed(0x5B) || Pressed(0x5C)) == win;
    }

    public void Dispose() => Native.PostThreadMessage(threadId, 0x0012 /*WM_QUIT*/, IntPtr.Zero, IntPtr.Zero);
}
