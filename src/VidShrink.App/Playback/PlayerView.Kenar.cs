using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    internal const int DwmBorderColorAttribute = 34;
    internal const uint DwmColorNone = 0xFFFFFFFE;
    internal const uint DwmColorDefault = 0xFFFFFFFF;

    internal uint? BorderColorRequest { get; private set; }

    internal int BorderColorWrites { get; private set; }

    internal double VideoAspect
        => _zoom.SourceWidth > 0 && _zoom.SourceHeight > 0 ? _zoom.SourceWidth / _zoom.SourceHeight : 0;

    private void CompactBorder(Window window, bool compact)
    {
        var color = compact ? DwmColorNone : DwmColorDefault;
        BorderColorRequest = color;
        _trace.Add("border -> " + (compact ? "none" : "default"));
        if (!OperatingSystem.IsWindows()) return;
        if (window.TryGetPlatformHandle() is not { } handle || handle.Handle == IntPtr.Zero) return;
        if (WriteBorderColor(handle.Handle, color) == 0) BorderColorWrites++;
    }

    internal static int WriteBorderColor(IntPtr hwnd, uint color)
    {
        try
        {
            return DwmSetWindowAttribute(hwnd, DwmBorderColorAttribute, ref color, sizeof(uint));
        }
        catch (DllNotFoundException)
        {
            return -1;
        }
        catch (EntryPointNotFoundException)
        {
            return -1;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int size);
}
