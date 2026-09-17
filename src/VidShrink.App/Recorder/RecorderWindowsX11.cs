using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace VidShrink.App.Recorder;

internal sealed record DesktopWindow(string Title, string Id, double X, double Y, double Width, double Height);

/// <summary>
/// Linux pencere listesi. Önce <c>xwininfo -root -tree</c>; araç yoksa libX11 üstünden kökün
/// <c>_NET_CLIENT_LIST</c> özelliği. Wayland oturumunda x11grab başka uygulamanın penceresini
/// göremez, liste orada açıkça reddedilir.
/// </summary>
internal static partial class RecorderWindowsX11
{
    internal static bool IsWayland(Func<string, string?> environment)
        => string.Equals(environment("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase)
           || (!string.IsNullOrEmpty(environment("WAYLAND_DISPLAY")) && string.IsNullOrEmpty(environment("DISPLAY")));

    [GeneratedRegex("^\\s+(0x[0-9a-fA-F]+) \"(.*)\": \\(.*\\)\\s+(\\d+)x(\\d+)\\+-?\\d+\\+-?\\d+\\s+\\+(-?\\d+)\\+(-?\\d+)\\s*$")]
    private static partial Regex TreeLine();

    internal static IReadOnlyList<DesktopWindow> ParseTree(string output)
        => output.Split('\n')
            .Select(line => TreeLine().Match(line.TrimEnd('\r')))
            .Where(m => m.Success)
            .Select(m => new DesktopWindow(
                m.Groups[2].Value.Trim(),
                m.Groups[1].Value,
                int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[6].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture)))
            .Where(w => w.Title.Length > 0 && w.Width > 1 && w.Height > 1)
            .ToList();

    internal static IReadOnlyList<DesktopWindow> List()
    {
        try { return FromTool(); }
        catch (Win32Exception) { return ClientList(); }
    }

    internal static IReadOnlyList<DesktopWindow> FromTool()
    {
        var psi = new ProcessStartInfo("xwininfo")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-root");
        psi.ArgumentList.Add("-tree");
        using var process = Process.Start(psi) ?? throw new Win32Exception("xwininfo");
        var stderr = process.StandardError.ReadToEndAsync();
        var text = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        _ = stderr.Result;
        return process.ExitCode == 0 ? ParseTree(text) : throw new InvalidOperationException("xwininfo " + process.ExitCode.ToString(CultureInfo.InvariantCulture));
    }

    internal static IReadOnlyList<DesktopWindow> ClientList()
    {
        var display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero) throw new InvalidOperationException("X display could not be opened.");
        try
        {
            var root = XDefaultRootWindow(display);
            var ids = Longs(display, root, XInternAtom(display, "_NET_CLIENT_LIST", false));
            var found = new List<DesktopWindow>();
            foreach (var id in ids)
            {
                var window = (IntPtr)id;
                if (XGetGeometry(display, window, out _, out _, out _, out var width, out var height, out _, out _) == 0) continue;
                XTranslateCoordinates(display, window, root, 0, 0, out var x, out var y, out _);
                var title = Name(display, window);
                if (title.Length == 0 || width < 2 || height < 2) continue;
                found.Add(new DesktopWindow(title, "0x" + id.ToString("x", CultureInfo.InvariantCulture), x, y, width, height));
            }

            return found;
        }
        finally { XCloseDisplay(display); }
    }

    private static List<long> Longs(IntPtr display, IntPtr window, IntPtr property)
    {
        var values = new List<long>();
        if (XGetWindowProperty(display, window, property, 0, 4096, false, IntPtr.Zero,
                out _, out var format, out var count, out _, out var data) != 0 || data == IntPtr.Zero)
            return values;
        try
        {
            if (format != 32) return values;
            for (var i = 0; i < (long)count; i++) values.Add(Marshal.ReadIntPtr(data, i * IntPtr.Size).ToInt64());
            return values;
        }
        finally { XFree(data); }
    }

    private static string Name(IntPtr display, IntPtr window)
    {
        if (XGetWindowProperty(display, window, XInternAtom(display, "_NET_WM_NAME", false), 0, 1024, false, IntPtr.Zero,
                out _, out var format, out var count, out _, out var data) == 0 && data != IntPtr.Zero)
        {
            try
            {
                if (format == 8 && count > 0)
                {
                    var bytes = new byte[(int)count];
                    Marshal.Copy(data, bytes, 0, bytes.Length);
                    return Encoding.UTF8.GetString(bytes).Trim();
                }
            }
            finally { XFree(data); }
        }

        if (XFetchName(display, window, out var name) == 0 || name == IntPtr.Zero) return string.Empty;
        try { return (Marshal.PtrToStringUTF8(name) ?? string.Empty).Trim(); }
        finally { XFree(name); }
    }

    private const string Lib = "libX11.so.6";

    [DllImport(Lib)]
    private static extern IntPtr XOpenDisplay(IntPtr name);

    [DllImport(Lib)]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport(Lib)]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport(Lib)]
    private static extern IntPtr XInternAtom(IntPtr display, [MarshalAs(UnmanagedType.LPStr)] string name, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);

    [DllImport(Lib)]
    private static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property, nint offset, nint length,
        [MarshalAs(UnmanagedType.Bool)] bool delete, IntPtr requestedType, out IntPtr actualType, out int actualFormat,
        out nuint items, out nuint bytesAfter, out IntPtr data);

    [DllImport(Lib)]
    private static extern int XFree(IntPtr data);

    [DllImport(Lib)]
    private static extern int XFetchName(IntPtr display, IntPtr window, out IntPtr name);

    [DllImport(Lib)]
    private static extern int XGetGeometry(IntPtr display, IntPtr drawable, out IntPtr root, out int x, out int y,
        out uint width, out uint height, out uint border, out uint depth);

    [DllImport(Lib)]
    private static extern int XTranslateCoordinates(IntPtr display, IntPtr source, IntPtr destination, int sourceX, int sourceY,
        out int destinationX, out int destinationY, out IntPtr child);
}
