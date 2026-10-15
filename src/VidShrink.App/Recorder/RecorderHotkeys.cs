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

internal enum HotkeyPlatform
{
    None,
    Windows,
    Mac,
    X11
}

internal static class GlobalHotkeyPlatforms
{
    /// <summary>
    /// Masaüstü ömrü yoksa hiçbiri. Linux'ta yalnız X11 oturumu: Wayland genel tuş yakalamaya izin vermez.
    /// </summary>
    internal static HotkeyPlatform Pick(bool windows, bool mac, bool linux, bool desktop, Func<string, string?> environment)
    {
        if (!desktop) return HotkeyPlatform.None;
        if (windows) return HotkeyPlatform.Windows;
        if (mac) return HotkeyPlatform.Mac;
        return linux && !RecorderWindowsX11.IsWayland(environment) && !string.IsNullOrEmpty(environment("DISPLAY"))
            ? HotkeyPlatform.X11
            : HotkeyPlatform.None;
    }

    internal static IGlobalHotkeys Create(HotkeyPlatform platform) => platform switch
    {
        HotkeyPlatform.Windows => new Win32GlobalHotkeys(),
        HotkeyPlatform.Mac => new MacGlobalHotkeys(),
        HotkeyPlatform.X11 => new X11GlobalHotkeys(),
        _ => new NoGlobalHotkeys()
    };
}

internal sealed class MacGlobalHotkeys : IGlobalHotkeys
{
    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";
    internal const uint KeyboardClass = 0x6B657962;
    internal const uint HotKeyPressed = 5;
    internal const uint DirectObject = 0x2D2D2D2D;
    internal const uint HotKeyIdType = 0x686B6964;
    internal const uint Signature = 0x5653484B;
    private const uint CmdKey = 0x0100;
    private const uint ShiftKey = 0x0200;
    private const uint OptionKey = 0x0800;
    private const uint ControlKey = 0x1000;

    private static readonly uint[] Letters =
        [0x00, 0x0B, 0x08, 0x02, 0x0E, 0x03, 0x05, 0x04, 0x22, 0x26, 0x28, 0x25, 0x2E, 0x2D, 0x1F, 0x23, 0x0C, 0x0F, 0x01, 0x11, 0x20, 0x09, 0x0D, 0x07, 0x10, 0x06];
    private static readonly uint[] Digits = [0x1D, 0x12, 0x13, 0x14, 0x15, 0x17, 0x16, 0x1A, 0x1C, 0x19];
    private static readonly uint[] Keypad = [0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5B, 0x5C];
    private static readonly uint[] Functions =
        [0x7A, 0x78, 0x63, 0x76, 0x60, 0x61, 0x62, 0x64, 0x65, 0x6D, 0x67, 0x6F, 0x69, 0x6B, 0x71, 0x6A, 0x40, 0x4F, 0x50, 0x5A];

    private readonly HandlerProc _handler;
    private readonly List<nint> _registered = [];
    private IReadOnlyList<HotkeyBinding> _bindings = [];
    private Action<HotkeyAction>? _pressed;
    private nint _handlerRef;

    public MacGlobalHotkeys() => _handler = OnHotKey;

    public IReadOnlyList<HotkeyBinding> Register(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyAction> pressed)
    {
        Unregister();
        _bindings = bindings;
        _pressed = pressed;

        var target = GetApplicationEventTarget();
        EventTypeSpec[] types = [new EventTypeSpec { EventClass = KeyboardClass, EventKind = HotKeyPressed }];
        if (InstallEventHandler(target, Marshal.GetFunctionPointerForDelegate(_handler), 1, types, 0, out _handlerRef) != 0)
        {
            _handlerRef = 0;
            return bindings;
        }

        var failed = new List<HotkeyBinding>();
        for (var i = 0; i < bindings.Count; i++)
        {
            var id = new EventHotKeyId { Signature = Signature, Id = (uint)(i + 1) };
            if (KeyCode(bindings[i].Key) is { } code
                && RegisterEventHotKey(code, Flags(bindings[i].Modifiers), id, target, 0, out var hotKey) == 0)
                _registered.Add(hotKey);
            else
                failed.Add(bindings[i]);
        }

        return failed;
    }

    public void Unregister()
    {
        foreach (var hotKey in _registered) UnregisterEventHotKey(hotKey);
        _registered.Clear();
        if (_handlerRef != 0) RemoveEventHandler(_handlerRef);
        _handlerRef = 0;
    }

    private int OnHotKey(nint callRef, nint eventRef, nint userData)
    {
        if (GetEventParameter(eventRef, DirectObject, HotKeyIdType, 0, (nuint)Marshal.SizeOf<EventHotKeyId>(), 0, out var id) != 0
            || id.Signature != Signature)
            return -9874;
        var index = (int)id.Id - 1;
        if (index < 0 || index >= _bindings.Count || _pressed is not { } pressed) return -9874;
        var action = _bindings[index].Action;
        Dispatcher.UIThread.Post(() => pressed(action));
        return 0;
    }

    /// <summary>Carbon sanal tuş kodu (kVK). F21–F24 ve Pause'un Mac klavyesinde karşılığı yok.</summary>
    internal static uint? KeyCode(Key key) => key switch
    {
        >= Key.A and <= Key.Z => Letters[key - Key.A],
        >= Key.D0 and <= Key.D9 => Digits[key - Key.D0],
        >= Key.NumPad0 and <= Key.NumPad9 => Keypad[key - Key.NumPad0],
        >= Key.F1 and <= Key.F20 => Functions[key - Key.F1],
        Key.Home => 0x73,
        Key.End => 0x77,
        Key.PageUp => 0x74,
        Key.PageDown => 0x79,
        Key.Insert => 0x72,
        _ => null
    };

    internal static uint Flags(KeyModifiers modifiers)
        => ((modifiers & KeyModifiers.Control) != 0 ? ControlKey : 0)
           | ((modifiers & KeyModifiers.Alt) != 0 ? OptionKey : 0)
           | ((modifiers & KeyModifiers.Shift) != 0 ? ShiftKey : 0);

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventHotKeyId
    {
        public uint Signature;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventTypeSpec
    {
        public uint EventClass;
        public uint EventKind;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int HandlerProc(nint callRef, nint eventRef, nint userData);

    [DllImport(Carbon)]
    private static extern nint GetApplicationEventTarget();

    [DllImport(Carbon)]
    private static extern int InstallEventHandler(nint target, nint handler, nuint count, EventTypeSpec[] types, nint userData, out nint handlerRef);

    [DllImport(Carbon)]
    private static extern int RemoveEventHandler(nint handlerRef);

    [DllImport(Carbon)]
    private static extern int RegisterEventHotKey(uint keyCode, uint modifiers, EventHotKeyId id, nint target, uint options, out nint hotKey);

    [DllImport(Carbon)]
    private static extern int UnregisterEventHotKey(nint hotKey);

    [DllImport(Carbon)]
    private static extern int GetEventParameter(nint eventRef, uint name, uint desiredType, nint actualType, nuint bufferSize, nint actualSize, out EventHotKeyId data);
}

internal sealed class X11GlobalHotkeys : IGlobalHotkeys
{
    private const string Lib = "libX11.so.6";
    private const int KeyPress = 2;
    private const int KeyRelease = 3;
    private const int GrabModeAsync = 1;
    private const uint ShiftMask = 1;
    private const uint ControlMask = 4;
    private const uint Mod1Mask = 8;
    internal static readonly uint[] LockVariants = [0, 2, 16, 18];

    private static readonly ErrorProc OnError = (display, _) =>
    {
        if (display == _checking) _grabFailed = true;
        return 0;
    };

    private static nint _checking;
    private static bool _grabFailed;

    private Thread? _thread;
    private ManualResetEventSlim? _stop;

    public IReadOnlyList<HotkeyBinding> Register(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyAction> pressed)
    {
        Unregister();

        var ready = new TaskCompletionSource<IReadOnlyList<HotkeyBinding>>();
        var stop = new ManualResetEventSlim();
        _stop = stop;
        _thread = new Thread(() => Loop(bindings, pressed, stop, ready)) { IsBackground = true };
        _thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    }

    public void Unregister()
    {
        if (_thread is null) return;
        _stop!.Set();
        _thread.Join();
        _stop.Dispose();
        _stop = null;
        _thread = null;
    }

    private static void Loop(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyAction> pressed, ManualResetEventSlim stop, TaskCompletionSource<IReadOnlyList<HotkeyBinding>> ready)
    {
        var display = XOpenDisplay(0);
        if (display == 0)
        {
            ready.SetResult(bindings);
            return;
        }

        var root = XDefaultRootWindow(display);
        XkbSetDetectableAutoRepeat(display, true, 0);
        var grabbed = new List<(int Code, uint Mask, HotkeyAction Action)>();
        var failed = new List<HotkeyBinding>();
        var previous = XSetErrorHandler(Marshal.GetFunctionPointerForDelegate(OnError));
        foreach (var binding in bindings)
        {
            int code = Keysym(binding.Key) is { } sym ? XKeysymToKeycode(display, sym) : 0;
            var mask = Mask(binding.Modifiers);
            var ok = code != 0;
            if (ok)
            {
                _checking = display;
                _grabFailed = false;
                foreach (var variant in LockVariants) XGrabKey(display, code, mask | variant, root, true, GrabModeAsync, GrabModeAsync);
                XSync(display, false);
                ok = !_grabFailed;
                if (!ok)
                {
                    foreach (var variant in LockVariants) XUngrabKey(display, code, mask | variant, root);
                    XSync(display, false);
                }
            }

            if (ok) grabbed.Add((code, mask, binding.Action));
            else failed.Add(binding);
        }

        _checking = 0;
        XSetErrorHandler(previous);
        ready.SetResult(failed);

        var held = new HashSet<uint>();
        var report = new nint[24];
        while (!stop.IsSet)
        {
            while (XPending(display) > 0)
            {
                XNextEvent(display, report);
                var (type, state, keycode) = ParseKey(report);
                if (!Pressed(held, type, keycode)) continue;
                var clean = Clean(state);
                foreach (var grab in grabbed)
                {
                    if (grab.Code != keycode || grab.Mask != clean) continue;
                    var action = grab.Action;
                    Dispatcher.UIThread.Post(() => pressed(action));
                    break;
                }
            }

            stop.Wait(50);
        }

        foreach (var grab in grabbed)
            foreach (var variant in LockVariants) XUngrabKey(display, grab.Code, grab.Mask | variant, root);
        XCloseDisplay(display);
    }

    /// <summary>Basılı tutulan tuş bir kez sayılır; bırakılınca yeniden basılabilir.</summary>
    internal static bool Pressed(HashSet<uint> held, int type, uint keycode)
    {
        if (type == KeyRelease) held.Remove(keycode);
        return type == KeyPress && held.Add(keycode);
    }

    /// <summary>XKeyEvent'in 64 bit yerleşimi: tür 0'da, durum 80'de, tuş kodu 84'te.</summary>
    internal static (int Type, uint State, uint Keycode) ParseKey(nint[] report)
        => ((int)(report[0] & 0xFFFFFFFF), (uint)((long)report[10] & 0xFFFFFFFF), (uint)((long)report[10] >> 32));

    internal static uint Clean(uint state) => state & (ShiftMask | ControlMask | Mod1Mask);

    internal static uint Mask(KeyModifiers modifiers)
        => ((modifiers & KeyModifiers.Shift) != 0 ? ShiftMask : 0)
           | ((modifiers & KeyModifiers.Control) != 0 ? ControlMask : 0)
           | ((modifiers & KeyModifiers.Alt) != 0 ? Mod1Mask : 0);

    internal static nuint? Keysym(Key key) => key switch
    {
        >= Key.A and <= Key.Z => 0x61 + (nuint)(key - Key.A),
        >= Key.D0 and <= Key.D9 => 0x30 + (nuint)(key - Key.D0),
        >= Key.NumPad0 and <= Key.NumPad9 => 0xFFB0 + (nuint)(key - Key.NumPad0),
        >= Key.F1 and <= Key.F24 => 0xFFBE + (nuint)(key - Key.F1),
        Key.Home => 0xFF50,
        Key.PageUp => 0xFF55,
        Key.PageDown => 0xFF56,
        Key.End => 0xFF57,
        Key.Insert => 0xFF63,
        Key.Pause => 0xFF13,
        _ => null
    };

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ErrorProc(nint display, nint errorEvent);

    [DllImport(Lib)]
    private static extern nint XOpenDisplay(nint name);

    [DllImport(Lib)]
    private static extern int XCloseDisplay(nint display);

    [DllImport(Lib)]
    private static extern nint XDefaultRootWindow(nint display);

    [DllImport(Lib)]
    private static extern byte XKeysymToKeycode(nint display, nuint keysym);

    [DllImport(Lib)]
    private static extern int XGrabKey(nint display, int keycode, uint modifiers, nint window, bool ownerEvents, int pointerMode, int keyboardMode);

    [DllImport(Lib)]
    private static extern int XUngrabKey(nint display, int keycode, uint modifiers, nint window);

    [DllImport(Lib)]
    private static extern int XkbSetDetectableAutoRepeat(nint display, bool detectable, nint supported);

    [DllImport(Lib)]
    private static extern int XSync(nint display, bool discard);

    [DllImport(Lib)]
    private static extern int XPending(nint display);

    [DllImport(Lib)]
    private static extern int XNextEvent(nint display, [Out] nint[] report);

    [DllImport(Lib)]
    private static extern nint XSetErrorHandler(nint handler);
}
