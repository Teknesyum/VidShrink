using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VidShrink.App.Recorder;

internal interface IDesktopIcons
{
    bool? Visible();

    void Toggle();
}

internal sealed class DesktopIconGuard
{
    internal const string StateFileName = "masaustu-simgeleri.gizlendi";

    private readonly IDesktopIcons _icons;
    private readonly string? _statePath;
    private bool _hiddenInMemory;

    internal DesktopIconGuard(IDesktopIcons icons, string? statePath)
    {
        _icons = icons;
        _statePath = statePath;
    }

    internal static string? DefaultStatePath(string? settingsPath)
    {
        var folder = string.IsNullOrEmpty(settingsPath) ? RecorderSettings.Folder : Path.GetDirectoryName(settingsPath);
        return string.IsNullOrEmpty(folder) ? null : Path.Combine(folder, StateFileName);
    }

    internal static DesktopIconGuard ForSettings(string? settingsPath)
        => new(OperatingSystem.IsWindows() ? new WindowsDesktopIcons() : new NoDesktopIcons(), DefaultStatePath(settingsPath));

    internal bool HiddenByUs => _statePath is null ? _hiddenInMemory : File.Exists(_statePath);

    internal bool Hide()
    {
        if (HiddenByUs) return true;
        if (_icons.Visible() != true) return false;
        if (!Remember(true)) return false;
        if (SetVisible(false)) return true;
        Remember(false);
        return false;
    }

    internal bool Restore()
    {
        if (!HiddenByUs) return false;
        var now = _icons.Visible();
        if (now is null) return false;
        if (now == false && !SetVisible(true)) return false;
        Remember(false);
        return true;
    }

    private bool SetVisible(bool visible)
    {
        try
        {
            if (_icons.Visible() == visible) return true;
            _icons.Toggle();
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ExternalException or IOException)
        {
            return false;
        }
    }

    private bool Remember(bool hidden)
    {
        if (_statePath is null)
        {
            _hiddenInMemory = hidden;
            return true;
        }

        try
        {
            if (hidden)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
                File.WriteAllText(_statePath, DateTimeOffset.UtcNow.ToString("O"));
            }
            else if (File.Exists(_statePath))
            {
                File.Delete(_statePath);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

internal sealed class NoDesktopIcons : IDesktopIcons
{
    public bool? Visible() => null;

    public void Toggle() { }
}

internal sealed class WindowsDesktopIcons : IDesktopIcons
{
    private const uint WmCommand = 0x0111;
    private const nint ToggleDesktopIcons = 0x7402;
    private const uint SmtoAbortIfHung = 0x0002;

    public bool? Visible()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var view = ShellView();
        if (view == IntPtr.Zero) return null;
        var list = FindWindowEx(view, IntPtr.Zero, "SysListView32", null);
        return list == IntPtr.Zero ? null : IsWindowVisible(list);
    }

    public void Toggle()
    {
        if (!OperatingSystem.IsWindows()) return;
        var view = ShellView();
        if (view == IntPtr.Zero) return;
        SendMessageTimeout(view, WmCommand, ToggleDesktopIcons, IntPtr.Zero, SmtoAbortIfHung, 2000, out _);
    }

    private static IntPtr ShellView()
    {
        var progman = FindWindow("Progman", null);
        var view = progman == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        var worker = IntPtr.Zero;
        while (view == IntPtr.Zero)
        {
            worker = FindWindowEx(IntPtr.Zero, worker, "WorkerW", null);
            if (worker == IntPtr.Zero) break;
            view = FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
        }
        return view;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, nint wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
}
