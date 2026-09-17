using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;

namespace VidShrink.App.Recorder;

internal interface IInputHooks
{
    bool Start(bool clicks, bool keys, Action<PixelPoint> clicked, Action<uint, bool> key);

    void Stop();
}

internal interface IClickSound
{
    void Play();
}

internal sealed class NoInputHooks : IInputHooks
{
    public bool Start(bool clicks, bool keys, Action<PixelPoint> clicked, Action<uint, bool> key) => false;

    public void Stop()
    {
    }
}

internal sealed class NoClickSound : IClickSound
{
    public void Play()
    {
    }
}

internal static class KeyText
{
    private static readonly Dictionary<uint, string> Named = new()
    {
        [0x08] = "Backspace", [0x09] = "Tab", [0x0D] = "Enter", [0x13] = "Pause", [0x14] = "Caps Lock", [0x1B] = "Esc",
        [0x20] = "Space", [0x21] = "Page Up", [0x22] = "Page Down", [0x23] = "End", [0x24] = "Home",
        [0x25] = "←", [0x26] = "↑", [0x27] = "→", [0x28] = "↓", [0x2C] = "Print Screen", [0x2D] = "Insert", [0x2E] = "Delete",
        [0x6A] = "Num *", [0x6B] = "Num +", [0x6D] = "Num -", [0x6E] = "Num .", [0x6F] = "Num /",
        [0xBA] = ";", [0xBB] = "=", [0xBC] = ",", [0xBD] = "-", [0xBE] = ".", [0xBF] = "/", [0xC0] = "`",
        [0xDB] = "[", [0xDC] = "\\", [0xDD] = "]", [0xDE] = "'"
    };

    internal static bool IsCtrl(uint vk) => vk is 0x11 or 0xA2 or 0xA3;

    internal static bool IsShift(uint vk) => vk is 0x10 or 0xA0 or 0xA1;

    internal static bool IsAlt(uint vk) => vk is 0x12 or 0xA4 or 0xA5;

    internal static bool IsWin(uint vk) => vk is 0x5B or 0x5C;

    internal static bool IsModifier(uint vk) => IsCtrl(vk) || IsShift(vk) || IsAlt(vk) || IsWin(vk);

    internal static string? Name(uint vk) => vk switch
    {
        >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x60 and <= 0x69 => "Num " + (vk - 0x60),
        >= 0x70 and <= 0x87 => "F" + (vk - 0x6F),
        _ => Named.GetValueOrDefault(vk)
    };

    internal static string? Format(uint vk, bool ctrl, bool shift, bool alt, bool win)
    {
        if (IsModifier(vk) || Name(vk) is not { } name) return null;
        var text = new StringBuilder();
        if (ctrl) text.Append("Ctrl + ");
        if (alt) text.Append("Alt + ");
        if (shift) text.Append("Shift + ");
        if (win) text.Append("Win + ");
        return text.Append(name).ToString();
    }
}

internal sealed class KeyTracker
{
    private readonly HashSet<uint> _held = new();

    internal string? Feed(uint vk, bool down)
    {
        if (KeyText.IsModifier(vk))
        {
            if (down) _held.Add(vk);
            else _held.Remove(vk);
            return null;
        }

        if (!down) return null;
        return KeyText.Format(vk, Held(KeyText.IsCtrl), Held(KeyText.IsShift), Held(KeyText.IsAlt), Held(KeyText.IsWin));
    }

    private bool Held(Func<uint, bool> kind)
    {
        foreach (var vk in _held)
            if (kind(vk)) return true;
        return false;
    }
}

internal static class ClickTone
{
    internal const int SampleRate = 22050;
    internal const int Milliseconds = 30;
    internal const double Frequency = 2000;

    internal static byte[] Wave()
    {
        var samples = SampleRate * Milliseconds / 1000;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + samples * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(samples * 2);
        for (var i = 0; i < samples; i++)
        {
            var fade = 1 - i / (double)samples;
            writer.Write((short)(Math.Sin(2 * Math.PI * Frequency * i / SampleRate) * short.MaxValue * 0.5 * fade));
        }

        writer.Flush();
        return stream.ToArray();
    }
}

internal sealed class Win32ClickSound : IClickSound
{
    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndMemory = 0x0004;

    private static readonly byte[] Tone = ClickTone.Wave();
    private static readonly GCHandle Pinned = GCHandle.Alloc(Tone, GCHandleType.Pinned);

    public void Play() => PlaySoundW(Pinned.AddrOfPinnedObject(), 0, SndAsync | SndNoDefault | SndMemory);

    [DllImport("winmm.dll")]
    private static extern bool PlaySoundW(nint sound, nint module, uint flags);
}

internal sealed class Win32InputHooks : IInputHooks
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const uint WmQuit = 0x0012;
    private const nint WmKeyDown = 0x0100;
    private const nint WmKeyUp = 0x0101;
    private const nint WmSysKeyDown = 0x0104;
    private const nint WmSysKeyUp = 0x0105;
    private const nint WmLButtonDown = 0x0201;
    private const nint WmRButtonDown = 0x0204;
    private const nint WmMButtonDown = 0x0207;

    private delegate nint HookProc(int code, nint wParam, nint lParam);

    private Thread? _thread;
    private uint _threadId;
    private HookProc? _mouseProc;
    private HookProc? _keyProc;

    public bool Start(bool clicks, bool keys, Action<PixelPoint> clicked, Action<uint, bool> key)
    {
        Stop();
        if (!clicks && !keys) return false;

        var ready = new TaskCompletionSource<bool>();
        _thread = new Thread(() => Loop(clicks, keys, clicked, key, ready)) { IsBackground = true };
        _thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    }

    public void Stop()
    {
        if (_thread is null) return;
        PostThreadMessageW(_threadId, WmQuit, 0, 0);
        _thread.Join();
        _thread = null;
    }

    private void Loop(bool clicks, bool keys, Action<PixelPoint> clicked, Action<uint, bool> key, TaskCompletionSource<bool> ready)
    {
        _threadId = GetCurrentThreadId();
        PeekMessageW(out _, 0, 0, 0, 0);
        var module = GetModuleHandleW(null);

        _mouseProc = (code, wParam, lParam) =>
        {
            if (code >= 0 && wParam is WmLButtonDown or WmRButtonDown or WmMButtonDown)
            {
                var info = Marshal.PtrToStructure<MouseInfo>(lParam);
                var point = new PixelPoint(info.X, info.Y);
                Dispatcher.UIThread.Post(() => clicked(point));
            }

            return CallNextHookEx(0, code, wParam, lParam);
        };

        _keyProc = (code, wParam, lParam) =>
        {
            if (code >= 0 && wParam is WmKeyDown or WmKeyUp or WmSysKeyDown or WmSysKeyUp)
            {
                var vk = (uint)Marshal.ReadInt32(lParam);
                var down = wParam is WmKeyDown or WmSysKeyDown;
                Dispatcher.UIThread.Post(() => key(vk, down));
            }

            return CallNextHookEx(0, code, wParam, lParam);
        };

        var mouse = clicks ? SetWindowsHookExW(WhMouseLl, _mouseProc, module, 0) : 0;
        var keyboard = keys ? SetWindowsHookExW(WhKeyboardLl, _keyProc, module, 0) : 0;
        ready.SetResult((!clicks || mouse != 0) && (!keys || keyboard != 0));

        while (GetMessageW(out _, 0, 0, 0) > 0)
        {
        }

        if (mouse != 0) UnhookWindowsHookEx(mouse);
        if (keyboard != 0) UnhookWindowsHookEx(keyboard);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInfo
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public nint Hwnd;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookExW(int hook, HookProc proc, nint module, uint threadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? name);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out Msg message, nint hwnd, uint min, uint max);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessageW(out Msg message, nint hwnd, uint min, uint max, uint remove);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessageW(uint threadId, uint message, nuint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
