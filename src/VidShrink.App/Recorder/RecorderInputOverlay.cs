using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace VidShrink.App.Recorder;

internal interface IInputOverlay
{
    void Ring(PixelPoint point);

    void Keys(string text, PixelRect region);

    void Hide();
}

internal static class OverlayPlace
{
    internal static PixelRect Ring(PixelPoint click, int size)
        => new(click.X - size / 2, click.Y - size / 2, size, size);

    internal static PixelPoint Caption(PixelRect region, PixelSize caption, int gap)
        => new(region.X + (region.Width - caption.Width) / 2, Math.Max(region.Y, region.Bottom - caption.Height - gap));

    internal static long ClickThrough(nint hwnd)
    {
        if (!OperatingSystem.IsWindows()) return 0;
        var style = RecorderFrame.ClickThroughStyle(GetWindowLongPtrW(hwnd, -20).ToInt64());
        SetWindowLongPtrW(hwnd, -20, new IntPtr(style));
        return style;
    }

    internal static TimeSpan Hold(Control owner, string key, TimeSpan fallback)
        => owner.TryFindResource(key, out var value) && value is TimeSpan span ? span : fallback;

    internal static double Size(Control owner, string key)
        => owner.TryFindResource(key, out var value) && value is double d ? d : 0;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtrW(nint hwnd, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtrW(nint hwnd, int index, IntPtr value);
}

internal partial class RecorderClickRing : Window
{
    public RecorderClickRing()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (TryGetPlatformHandle() is { } handle) OverlayPlace.ClickThrough(handle.Handle);
        };
    }

    internal void Place(PixelPoint click)
    {
        var scaling = (Screens.ScreenFromPoint(click) ?? Screens.Primary)?.Scaling ?? 1;
        var size = (int)Math.Ceiling(OverlayPlace.Size(this, "RecorderClickRingSize") * scaling);
        Position = OverlayPlace.Ring(click, size).Position;
    }
}

internal partial class RecorderKeyCaption : Window
{
    public RecorderKeyCaption()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (TryGetPlatformHandle() is { } handle) OverlayPlace.ClickThrough(handle.Handle);
        };
    }

    internal string Text
    {
        get => TxtKeys.Text ?? string.Empty;
        set => TxtKeys.Text = value;
    }

    internal void Place(PixelRect region)
    {
        var scaling = (Screens.ScreenFromPoint(region.Center) ?? Screens.Primary)?.Scaling ?? 1;
        Measure(Size.Infinity);
        var size = PixelSize.FromSize(DesiredSize, scaling);
        Position = OverlayPlace.Caption(region, size, (int)Math.Ceiling(OverlayPlace.Size(this, "RecorderKeyCaptionGap") * scaling));
    }
}

internal sealed class RecorderInputOverlay : IInputOverlay
{
    private RecorderClickRing? _ring;
    private RecorderKeyCaption? _caption;
    private int _ringTurn;
    private int _captionTurn;

    internal RecorderClickRing? RingWindow => _ring;

    internal RecorderKeyCaption? CaptionWindow => _caption;

    public void Ring(PixelPoint point)
    {
        _ring ??= new RecorderClickRing();
        _ring.Place(point);
        if (!_ring.IsVisible) _ring.Show();
        var turn = ++_ringTurn;
        _ = HideLater(OverlayPlace.Hold(_ring, "RecorderClickRingHold", TimeSpan.FromMilliseconds(360)), () =>
        {
            if (turn == _ringTurn) _ring?.Hide();
        });
    }

    public void Keys(string text, PixelRect region)
    {
        _caption ??= new RecorderKeyCaption();
        _caption.Text = text;
        _caption.Place(region);
        if (!_caption.IsVisible) _caption.Show();
        var turn = ++_captionTurn;
        _ = HideLater(OverlayPlace.Hold(_caption, "RecorderKeyCaptionHold", TimeSpan.FromSeconds(1.5)), () =>
        {
            if (turn == _captionTurn) _caption?.Hide();
        });
    }

    public void Hide()
    {
        _ringTurn++;
        _captionTurn++;
        _ring?.Close();
        _caption?.Close();
        _ring = null;
        _caption = null;
    }

    private static async System.Threading.Tasks.Task HideLater(TimeSpan hold, Action done)
    {
        await System.Threading.Tasks.Task.Delay(hold).ConfigureAwait(false);
        Dispatcher.UIThread.Post(done);
    }
}
