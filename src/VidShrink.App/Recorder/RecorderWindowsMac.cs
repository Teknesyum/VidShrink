using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace VidShrink.App.Recorder;

/// <summary>
/// macOS pencere listesi <c>CGWindowListCopyWindowInfo</c>'dan. Sınırlar nokta cinsinden, ana ekranın
/// sol üstünden; <c>avfoundation</c> tek pencere vermediği için bu dikdörtgen ekran kırpmasına çevrilir.
/// </summary>
internal static class RecorderWindowsMac
{
    private const uint OnScreenOnly = 1;
    private const uint ExcludeDesktopElements = 16;
    private const uint Utf8 = 0x08000100;
    private const int SInt64Type = 4;

    internal static IReadOnlyList<DesktopWindow> List()
    {
        var found = new List<DesktopWindow>();
        var array = CGWindowListCopyWindowInfo(OnScreenOnly | ExcludeDesktopElements, 0);
        if (array == IntPtr.Zero) return found;

        var keyLayer = Key("kCGWindowLayer");
        var keyName = Key("kCGWindowName");
        var keyOwner = Key("kCGWindowOwnerName");
        var keyBounds = Key("kCGWindowBounds");
        var keyNumber = Key("kCGWindowNumber");
        try
        {
            var count = CFArrayGetCount(array);
            for (nint i = 0; i < count; i++)
            {
                var entry = CFArrayGetValueAtIndex(array, i);
                if (Number(entry, keyLayer) != 0) continue;
                if (!CGRectMakeWithDictionaryRepresentation(CFDictionaryGetValue(entry, keyBounds), out var rect)) continue;
                var name = Text(entry, keyName);
                var owner = Text(entry, keyOwner);
                var title = name.Length == 0 ? owner : owner.Length == 0 ? name : owner + " — " + name;
                if (title.Length == 0 || rect.Width < 2 || rect.Height < 2) continue;
                found.Add(new DesktopWindow(title, (Number(entry, keyNumber) ?? 0).ToString(CultureInfo.InvariantCulture),
                    rect.X, rect.Y, rect.Width, rect.Height));
            }
        }
        finally
        {
            foreach (var key in new[] { keyLayer, keyName, keyOwner, keyBounds, keyNumber }) CFRelease(key);
            CFRelease(array);
        }

        return found;
    }

    private static IntPtr Key(string text) => CFStringCreateWithCString(IntPtr.Zero, text, Utf8);

    private static long? Number(IntPtr dictionary, IntPtr key)
    {
        var value = CFDictionaryGetValue(dictionary, key);
        return value != IntPtr.Zero && CFNumberGetValue(value, SInt64Type, out var number) ? number : null;
    }

    private static string Text(IntPtr dictionary, IntPtr key)
    {
        var value = CFDictionaryGetValue(dictionary, key);
        if (value == IntPtr.Zero) return string.Empty;
        var buffer = new byte[1024];
        return CFStringGetCString(value, buffer, buffer.Length, Utf8)
            ? System.Text.Encoding.UTF8.GetString(buffer, 0, Math.Max(0, Array.IndexOf(buffer, (byte)0))).Trim()
            : string.Empty;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }

    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool CGRectMakeWithDictionaryRepresentation(IntPtr dictionary, out CGRect rect);

    [DllImport(CoreFoundation)]
    private static extern nint CFArrayGetCount(IntPtr array);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, uint encoding);

    [DllImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool CFStringGetCString(IntPtr text, byte[] buffer, nint size, uint encoding);

    [DllImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool CFNumberGetValue(IntPtr number, int type, out long value);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr value);
}
