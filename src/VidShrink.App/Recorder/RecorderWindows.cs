using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

internal readonly record struct WindowEntry(string Title, bool Visible, bool Cloaked, bool ToolWindow, int ProcessId, bool Minimized, PixelRect Frame = default);

internal static class RecorderWindows
{
    private const int GwlExStyle = -20;
    private const long ExToolWindow = 0x80;
    private const int DwmCloaked = 14;
    private const int DwmExtendedFrameBounds = 9;

    internal static IReadOnlyList<string> Pick(IEnumerable<WindowEntry> windows, int ownProcessId)
        => windows
            .Where(w => w.Visible && !w.Cloaked && !w.ToolWindow && !w.Minimized && w.ProcessId != ownProcessId)
            .Select(w => w.Title.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    internal static IReadOnlyList<PixelRect> Frames(IEnumerable<WindowEntry> windows, int ownProcessId)
    {
        var shown = new List<PixelRect>();
        foreach (var w in windows)
        {
            if (!w.Visible || w.Cloaked || w.ToolWindow || w.Minimized || w.ProcessId == ownProcessId) continue;
            if (w.Title.Trim().Length == 0 || w.Frame.Width <= 0 || w.Frame.Height <= 0) continue;
            if (shown.Any(above => above.Contains(w.Frame))) continue;
            shown.Add(w.Frame);
        }

        return shown;
    }

    internal static PixelRect? FromPoints(DesktopWindow window, IReadOnlyList<(PixelRect Bounds, double Scale)> screens)
    {
        var centerX = window.X + window.Width / 2;
        var centerY = window.Y + window.Height / 2;
        foreach (var (b, scale) in screens)
        {
            if (!(scale > 0)) continue;
            double left = b.X / scale, top = b.Y / scale;
            if (centerX < left || centerX >= left + b.Width / scale || centerY < top || centerY >= top + b.Height / scale) continue;
            var x = (int)Math.Round(b.X + (window.X - left) * scale);
            var y = (int)Math.Round(b.Y + (window.Y - top) * scale);
            return new PixelRect(x, y, (int)Math.Round(window.Width * scale), (int)Math.Round(window.Height * scale));
        }

        return null;
    }

    internal static IReadOnlyList<PixelRect> SnapFrames(IReadOnlyList<(PixelRect Bounds, double Scale)> screens)
    {
        try
        {
            if (OperatingSystem.IsWindows()) return Frames(Enumerate(), Environment.ProcessId);
            if (OperatingSystem.IsMacOS())
                return RecorderWindowsMac.List().Select(w => FromPoints(w, screens)).OfType<PixelRect>().ToList();
            return Desktop()
                .Select(w => new PixelRect((int)w.X, (int)w.Y, (int)w.Width, (int)w.Height))
                .ToList();
        }
        catch (Exception)
        {
            return Array.Empty<PixelRect>();
        }
    }

    internal static IReadOnlyList<string> Titles()
        => OperatingSystem.IsWindows()
            ? Pick(Enumerate(), Environment.ProcessId)
            : Desktop().Select(w => w.Title).Distinct(StringComparer.Ordinal).ToList();

    internal static IReadOnlyList<DesktopWindow> Desktop()
        => OperatingSystem.IsMacOS() ? RecorderWindowsMac.List()
            : OperatingSystem.IsLinux() && !RecorderWindowsX11.IsWayland(Environment.GetEnvironmentVariable) ? RecorderWindowsX11.List()
            : Array.Empty<DesktopWindow>();

    internal static DesktopWindow? Find(string title)
        => Desktop().FirstOrDefault(w => string.Equals(w.Title, title, StringComparison.Ordinal));

    /// <summary>
    /// macOS: pencerenin nokta dikdörtgenini en çok örtüştüğü ekranın piksel karesine çevirir.
    /// Ekranlar piksel sınırı ve ölçeğiyle verilir; dönen indeks o ekranın <c>avfoundation</c> sırası.
    /// </summary>
    internal static (int Screen, RecorderRegion Crop)? MacCrop(DesktopWindow window, IReadOnlyList<(ScreenBounds Bounds, double Scale)> screens)
    {
        (int, RecorderRegion)? best = null;
        var bestArea = 0L;
        for (var i = 0; i < screens.Count; i++)
        {
            var (b, scale) = screens[i];
            if (!(scale > 0)) continue;
            var crop = RecorderArguments.WindowCrop(window.X, window.Y, window.Width, window.Height,
                b.X / scale, b.Y / scale, b.Width / scale, b.Height / scale, scale);
            if (crop is null || (long)crop.Width * crop.Height <= bestArea) continue;
            bestArea = (long)crop.Width * crop.Height;
            best = (i, crop);
        }

        return best;
    }

    private static List<WindowEntry> Enumerate()
    {
        var found = new List<WindowEntry>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            var length = GetWindowTextLength(hwnd);
            var text = new StringBuilder(length + 1);
            if (length > 0) GetWindowText(hwnd, text, text.Capacity);
            GetWindowThreadProcessId(hwnd, out var pid);
            var cloaked = DwmGetWindowAttribute(hwnd, DwmCloaked, out var value, sizeof(int)) == 0 && value != 0;
            var tool = (GetWindowLongPtr(hwnd, GwlExStyle).ToInt64() & ExToolWindow) != 0;
            var frame = DwmGetFrame(hwnd, DwmExtendedFrameBounds, out FrameRect r, Marshal.SizeOf<FrameRect>()) == 0
                ? new PixelRect(r.Left, r.Top, Math.Max(0, r.Right - r.Left), Math.Max(0, r.Bottom - r.Top))
                : default;
            found.Add(new WindowEntry(text.ToString(), IsWindowVisible(hwnd), cloaked, tool, (int)pid, IsIconic(hwnd), frame));
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

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetFrame(IntPtr hwnd, int attribute, out FrameRect value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct FrameRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
