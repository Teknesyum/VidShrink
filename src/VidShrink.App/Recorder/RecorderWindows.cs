using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace VidShrink.App.Recorder;

internal readonly record struct WindowEntry(string Title, bool Visible, bool Cloaked, bool ToolWindow, int ProcessId, bool Minimized);

internal static class RecorderWindows
{
    private const int GwlExStyle = -20;
    private const long ExToolWindow = 0x80;
    private const int DwmCloaked = 14;

    internal static IReadOnlyList<string> Pick(IEnumerable<WindowEntry> windows, int ownProcessId)
        => windows
            .Where(w => w.Visible && !w.Cloaked && !w.ToolWindow && !w.Minimized && w.ProcessId != ownProcessId)
            .Select(w => w.Title.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    internal static IReadOnlyList<string> Titles()
        => OperatingSystem.IsWindows() ? Pick(Enumerate(), Environment.ProcessId) : Array.Empty<string>();

    private static List<WindowEntry> Enumerate()
    {
        var found = new List<WindowEntry>();
        EnumWindows((hwnd, _) =>
        {
            var length = GetWindowTextLength(hwnd);
            var text = new StringBuilder(length + 1);
            if (length > 0) GetWindowText(hwnd, text, text.Capacity);
            GetWindowThreadProcessId(hwnd, out var pid);
            var cloaked = DwmGetWindowAttribute(hwnd, DwmCloaked, out var value, sizeof(int)) == 0 && value != 0;
            var tool = (GetWindowLongPtr(hwnd, GwlExStyle).ToInt64() & ExToolWindow) != 0;
            found.Add(new WindowEntry(text.ToString(), IsWindowVisible(hwnd), cloaked, tool, (int)pid, IsIconic(hwnd)));
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
}
