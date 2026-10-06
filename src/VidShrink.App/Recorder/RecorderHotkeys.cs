using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Threading;
using VidShrink.App.Playback;

namespace VidShrink.App.Recorder;

internal enum HotkeyAction
{
    Toggle,
    Stop,
    Frame,
    Discard,
    ReplaySave,
    Chapter
}

internal sealed record HotkeyBinding(HotkeyAction Action, Key Key, uint VirtualKey, KeyModifiers Modifiers = KeyModifiers.None);

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
        new(HotkeyAction.Frame, Key.F9, 0x78),
        new(HotkeyAction.Discard, Key.F10, 0x79),
        new(HotkeyAction.ReplaySave, Key.F11, 0x7A),
        new(HotkeyAction.Chapter, Key.F6, 0x75)
    ];

    /// <summary>Bölüm işareti eklenmeden önceki ayarın taşıdığı eylem sayısı.</summary>
    internal const int LegacyCount = 5;

    internal static HotkeyAction? ActionOf(Key key, KeyModifiers modifiers) => ActionOf(All, key, modifiers);

    internal static HotkeyAction? ActionOf(IReadOnlyList<HotkeyBinding> bindings, Key key, KeyModifiers modifiers)
        => bindings.FirstOrDefault(b => b.Key == key && b.Modifiers == Clean(modifiers))?.Action;

    internal static HotkeyBinding Of(IReadOnlyList<HotkeyBinding> bindings, HotkeyAction action)
        => bindings.First(b => b.Action == action);

    internal static KeyModifiers Clean(KeyModifiers modifiers)
        => modifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt);

    internal static bool Same(HotkeyBinding a, HotkeyBinding b) => a.Key == b.Key && a.Modifiers == b.Modifiers;

    /// <summary>
    /// Genel kısayola çevrilebilen tuşların sanal tuş kodu. Küme kapalı: işlev tuşları, harfler,
    /// rakamlar, sayı takımı ve birkaç gezinme tuşu. Dışında kalan tuş atanamaz.
    /// </summary>
    internal static uint? VirtualKeyOf(Key key) => key switch
    {
        >= Key.F1 and <= Key.F24 => 0x70 + (uint)(key - Key.F1),
        >= Key.A and <= Key.Z => 0x41 + (uint)(key - Key.A),
        >= Key.D0 and <= Key.D9 => 0x30 + (uint)(key - Key.D0),
        >= Key.NumPad0 and <= Key.NumPad9 => 0x60 + (uint)(key - Key.NumPad0),
        Key.PageUp => 0x21,
        Key.PageDown => 0x22,
        Key.End => 0x23,
        Key.Home => 0x24,
        Key.Insert => 0x2D,
        Key.Pause => 0x13,
        _ => null
    };

    /// <summary>
    /// Atanabilir bağ. İşlev tuşu tek başına olur; öbür tuşlar Ctrl ya da Alt ister, çünkü
    /// genel kısayol o tuşu her uygulamada yutar ve düz harf yazı yazmayı keser.
    /// </summary>
    internal static HotkeyBinding? Bind(HotkeyAction action, Key key, KeyModifiers modifiers)
    {
        var clean = Clean(modifiers);
        if (VirtualKeyOf(key) is not { } code) return null;
        var bare = key is >= Key.F1 and <= Key.F24;
        return bare || (clean & (KeyModifiers.Control | KeyModifiers.Alt)) != 0 ? new HotkeyBinding(action, key, code, clean) : null;
    }

    internal static string Gesture(HotkeyBinding binding) => Keymap.Gesture(PlayerInput.OnKey(binding.Key, binding.Modifiers));

    internal static string Names(IEnumerable<HotkeyBinding> bindings)
        => string.Join(", ", bindings.Select(Gesture));

    internal static string Write(IReadOnlyList<HotkeyBinding> bindings)
        => string.Join(",", All.Select(d => Of(bindings, d.Action))
            .Select(b => ((int)b.Modifiers).ToString(CultureInfo.InvariantCulture) + ":" + b.Key));

    /// <summary>
    /// Ayardaki yazımı bağlara çevirir. Eksik, tanınmayan ya da iki eyleme aynı tuşu veren yazım
    /// <c>null</c> döner; çağıran varsayılana düşer. Eski sürümün <see cref="LegacyCount"/> eylemli
    /// yazımı geçerlidir: atamalar korunur, sonradan eklenen eylem varsayılan tuşunu alır; o tuş
    /// eski atamalardan birindeyse yazım yine <c>null</c> döner.
    /// </summary>
    internal static IReadOnlyList<HotkeyBinding>? Read(string? text)
    {
        var parts = (text ?? string.Empty).Split(',');
        if (parts.Length != All.Count && parts.Length != LegacyCount) return null;

        var bindings = new List<HotkeyBinding>();
        for (var i = 0; i < parts.Length; i++)
        {
            var pair = parts[i].Split(':', 2);
            if (pair.Length != 2
                || !int.TryParse(pair[0], NumberStyles.None, CultureInfo.InvariantCulture, out var modifiers)
                || !Enum.TryParse<Key>(pair[1], false, out var key) || !Enum.IsDefined(key)
                || Bind(All[i].Action, key, (KeyModifiers)modifiers) is not { } binding
                || (int)binding.Modifiers != modifiers
                || bindings.Any(b => Same(b, binding)))
                return null;
            bindings.Add(binding);
        }

        foreach (var added in All.Skip(bindings.Count))
        {
            if (bindings.Any(b => Same(b, added))) return null;
            bindings.Add(added);
        }

        return bindings;
    }
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
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;

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
            if (RegisterHotKey(0, i + 1, ModNoRepeat | Flags(bindings[i].Modifiers), bindings[i].VirtualKey)) registered.Add(i + 1);
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

    internal static uint Flags(KeyModifiers modifiers)
        => ((modifiers & KeyModifiers.Alt) != 0 ? ModAlt : 0)
           | ((modifiers & KeyModifiers.Control) != 0 ? ModControl : 0)
           | ((modifiers & KeyModifiers.Shift) != 0 ? ModShift : 0);

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
