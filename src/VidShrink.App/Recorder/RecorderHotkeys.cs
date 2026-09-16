using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Threading;

namespace VidShrink.App.Recorder;

internal enum HotkeyAction
{
    Toggle,
    Stop,
    Frame
}

internal sealed record HotkeyBinding(HotkeyAction Action, Key Key, uint VirtualKey);

internal interface IGlobalHotkeys
{
    IReadOnlyList<HotkeyBinding> Register(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyAction> pressed);

    void Unregister();
}

internal static class RecorderHotkeys
{
    internal static readonly IReadOnlyList<HotkeyBinding> All =
    [
        new(HotkeyAction.Toggle, Key.F7, 0x76),
        new(HotkeyAction.Stop, Key.F8, 0x77),
        new(HotkeyAction.Frame, Key.F9, 0x78)
    ];

    internal static HotkeyAction? ActionOf(Key key, KeyModifiers modifiers)
        => modifiers == KeyModifiers.None && All.FirstOrDefault(b => b.Key == key) is { } binding
            ? binding.Action
            : null;

    internal static string Names(IEnumerable<HotkeyBinding> bindings)
        => string.Join(", ", bindings.Select(b => b.Key.ToString()));
}

internal sealed class NoGlobalHotkeys : IGlobalHotkeys
{
    public IReadOnlyList<HotkeyBinding> Register(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyAction> pressed) => [];

    public void Unregister()
    {
    }
}

internal sealed class Win32GlobalHotkeys : IGlobalHotkeys
{
    private const uint WmHotkey = 0x0312;
    private const uint WmQuit = 0x0012;
    private const uint ModNoRepeat = 0x4000;

    private Thread? _thread;
    private uint _threadId;

    public IReadOnlyList<HotkeyBinding> Register(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyAction> pressed)
    {
        Unregister();

        var ready = new TaskCompletionSource<IReadOnlyList<HotkeyBinding>>();
        _thread = new Thread(() => Loop(bindings, pressed, ready)) { IsBackground = true };
        _thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    }

    public void Unregister()
    {
        if (_thread is null) return;
        PostThreadMessageW(_threadId, WmQuit, 0, 0);
        _thread.Join();
        _thread = null;
    }

    private void Loop(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyAction> pressed, TaskCompletionSource<IReadOnlyList<HotkeyBinding>> ready)
    {
        _threadId = GetCurrentThreadId();
        PeekMessageW(out _, 0, 0, 0, 0);

        var failed = new List<HotkeyBinding>();
        var registered = new List<int>();
        for (var i = 0; i < bindings.Count; i++)
        {
            if (RegisterHotKey(0, i + 1, ModNoRepeat, bindings[i].VirtualKey)) registered.Add(i + 1);
            else failed.Add(bindings[i]);
        }

        ready.SetResult(failed);

        while (GetMessageW(out var message, 0, 0, 0) > 0)
        {
            if (message.Message != WmHotkey) continue;
            var index = (int)message.WParam - 1;
            if (index < 0 || index >= bindings.Count) continue;
            var action = bindings[index].Action;
            Dispatcher.UIThread.Post(() => pressed(action));
        }

        foreach (var id in registered) UnregisterHotKey(0, id);
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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hwnd, int id);

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
