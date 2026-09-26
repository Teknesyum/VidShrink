using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace VidShrink.App.Recorder;

internal interface IRecorderFrameHost
{
    void Show(PixelRect region);

    void Hide();
}

internal partial class RecorderFrame : Window
{
    internal const long ExTransparent = 0x20;
    internal const long ExToolWindow = 0x80;
    internal const long ExLayered = 0x80000;
    internal const long ExNoActivate = 0x08000000;

    private const int GwlExStyle = -20;

    public RecorderFrame()
    {
        InitializeComponent();
        Opened += (_, _) => MakeClickThrough();
    }

    internal static long ClickThroughStyle(long exStyle)
        => exStyle | ExTransparent | ExToolWindow | ExLayered | ExNoActivate;

    internal static int EdgePixels(double thickness, double scaling)
        => Math.Max(1, (int)Math.Ceiling(thickness * scaling));

    internal static PixelRect Outer(PixelRect region, int edge)
        => new(region.X - edge, region.Y - edge, region.Width + 2 * edge, region.Height + 2 * edge);

    internal static PixelRect? Wanted(bool hasSession, PixelRect? region, bool hiddenByUser)
        => hasSession && !hiddenByUser ? region : null;

    internal static PixelRect Placement(PixelRect region, int edge, IEnumerable<PixelRect> screens)
    {
        var outer = Outer(region, edge);
        return screens.Any(s => s.Contains(outer)) ? outer : region;
    }

    internal const uint ExcludeFromCaptureAffinity = 0x11;

    internal bool ExcludeFromCapture { get; init; } = true;

    internal bool CaptureExcluded { get; private set; }

    internal double Thickness
        => this.TryFindResource("RecorderFrameThickness", out var value) && value is double d ? d : 0;

    /// <summary>
    /// Pencere biçimi: yalnız kenar şeridi. İç alan pencereye ait değil, tıklama ve tekerlek
    /// stil kaybolsa da alttaki uygulamaya gidiyor; DWM de iç alanı birleştirmiyor.
    /// </summary>
    internal static IReadOnlyList<PixelRect> Ring(PixelSize size, int edge)
    {
        if (edge * 2 >= size.Width || edge * 2 >= size.Height) return new[] { new PixelRect(size) };
        return new[]
        {
            new PixelRect(0, 0, size.Width, edge),
            new PixelRect(0, size.Height - edge, size.Width, edge),
            new PixelRect(0, edge, edge, size.Height - 2 * edge),
            new PixelRect(size.Width - edge, edge, edge, size.Height - 2 * edge)
        };
    }

    private PixelSize _size;
    private int _edge;

    internal bool Shaped { get; private set; }

    internal void Place(PixelRect region)
    {
        var scaling = (Screens.ScreenFromPoint(new PixelPoint(region.X, region.Y)) ?? Screens.Primary)?.Scaling ?? 1;
        _edge = EdgePixels(Thickness, scaling);
        var placed = Placement(region, _edge, Screens.All.Select(s => s.Bounds));
        _size = placed.Size;
        Position = placed.Position;
        Width = placed.Width / scaling;
        Height = placed.Height / scaling;
        ApplyShape();
    }

    private void ApplyShape()
    {
        if (!OperatingSystem.IsWindows() || _size.Width <= 0 || TryGetPlatformHandle() is not { } handle) return;
        var shape = CreateRectRgn(0, 0, 0, 0);
        if (shape == IntPtr.Zero) return;
        foreach (var part in Ring(_size, _edge))
        {
            var piece = CreateRectRgn(part.X, part.Y, part.Right, part.Bottom);
            if (piece == IntPtr.Zero) continue;
            CombineRgn(shape, shape, piece, RgnOr);
            DeleteObject(piece);
        }

        Shaped = SetWindowRgn(handle.Handle, shape, true) != 0;
        if (!Shaped) DeleteObject(shape);
    }

    internal static PixelRect? WindowBounds(string title)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var hwnd = FindWindowW(null, title);
        if (hwnd == IntPtr.Zero || !GetClientRect(hwnd, out var client)) return null;
        var origin = new NativePoint();
        if (!ClientToScreen(hwnd, ref origin)) return null;
        var width = client.Right - client.Left;
        var height = client.Bottom - client.Top;
        return width > 1 && height > 1 ? new PixelRect(origin.X, origin.Y, width, height) : null;
    }

    private void MakeClickThrough()
    {
        if (!OperatingSystem.IsWindows() || TryGetPlatformHandle() is not { } handle) return;
        var current = GetWindowLongPtr(handle.Handle, GwlExStyle).ToInt64();
        SetWindowLongPtr(handle.Handle, GwlExStyle, new IntPtr(ClickThroughStyle(current)));
        if (ExcludeFromCapture) CaptureExcluded = SetWindowDisplayAffinity(handle.Handle, ExcludeFromCaptureAffinity);
        ApplyShape();
    }

    private const int RgnOr = 2;

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern int CombineRgn(IntPtr destination, IntPtr first, IntPtr second, int mode);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string? className, string title);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
}

internal sealed class RecorderFrameHost : IRecorderFrameHost
{
    private RecorderFrame? _frame;

    public void Show(PixelRect region)
    {
        _frame ??= new RecorderFrame();
        _frame.Place(region);
        if (!_frame.IsVisible) _frame.Show();
    }

    public void Hide()
    {
        _frame?.Close();
        _frame = null;
    }
}
