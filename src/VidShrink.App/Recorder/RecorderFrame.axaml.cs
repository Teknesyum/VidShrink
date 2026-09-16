using System;
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

    internal double Thickness
        => this.TryFindResource("RecorderFrameThickness", out var value) && value is double d ? d : 0;

    internal void Place(PixelRect region)
    {
        var scaling = (Screens.ScreenFromPoint(new PixelPoint(region.X, region.Y)) ?? Screens.Primary)?.Scaling ?? 1;
        var outer = Outer(region, EdgePixels(Thickness, scaling));
        Position = outer.Position;
        Width = outer.Width / scaling;
        Height = outer.Height / scaling;
    }

    private void MakeClickThrough()
    {
        if (!OperatingSystem.IsWindows() || TryGetPlatformHandle() is not { } handle) return;
        var current = GetWindowLongPtr(handle.Handle, GwlExStyle).ToInt64();
        SetWindowLongPtr(handle.Handle, GwlExStyle, new IntPtr(ClickThroughStyle(current)));
    }

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
