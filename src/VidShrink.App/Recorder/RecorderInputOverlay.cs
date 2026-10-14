using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace VidShrink.App.Recorder;

internal enum PointerColor
{
    Ember,
    Blue,
    Pink,
    Purple,
    Green,
    Flame
}

internal enum PointerRingSize
{
    Small,
    Medium,
    Large
}

internal sealed record PointerStyle(bool Highlight, bool Spotlight, PointerRingSize Size, PointerColor HighlightColor, PointerColor LeftColor, PointerColor RightColor)
{
    internal static PointerStyle Default { get; } = new(false, false, PointerRingSize.Medium, PointerColor.Flame, PointerColor.Ember, PointerColor.Blue);

    internal string SizeKey => Size switch
    {
        PointerRingSize.Small => "RecorderPointerRingSmall",
        PointerRingSize.Large => "RecorderPointerRingLarge",
        _ => "RecorderClickRingSize"
    };

    internal PointerColor ClickColor(PointerButton button) => button == PointerButton.Right ? RightColor : LeftColor;

    internal static string BrushKey(PointerColor color) => color switch
    {
        PointerColor.Blue => "NeonBlue",
        PointerColor.Pink => "NeonPink",
        PointerColor.Purple => "NeonPurple",
        PointerColor.Green => "NeonSuccess",
        PointerColor.Flame => "EmberFlame",
        _ => "NeonEmber"
    };
}

internal interface IInputOverlay
{
    void Style(PointerStyle style);

    void Ring(PixelPoint point, PointerButton button);

    void Pointer(PixelPoint point, PixelRect area);

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

    internal static Geometry Spotlight(Size size, Point center, double radius)
        => new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(size)), new EllipseGeometry(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2)));

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

    internal IBrush? Stroke => Ring.Stroke;

    internal void Paint(PointerColor color)
    {
        if (this.TryFindResource(PointerStyle.BrushKey(color), out var brush) && brush is IBrush paint) Ring.Stroke = paint;
    }

    internal void Place(PixelPoint click, string sizeKey)
    {
        var scaling = (Screens.ScreenFromPoint(click) ?? Screens.Primary)?.Scaling ?? 1;
        var dip = OverlayPlace.Size(this, sizeKey);
        Width = dip;
        Height = dip;
        var size = (int)Math.Ceiling(dip * scaling);
        Position = OverlayPlace.Ring(click, size).Position;
    }
}

internal partial class RecorderSpotlight : Window
{
    public RecorderSpotlight()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (TryGetPlatformHandle() is { } handle) OverlayPlace.ClickThrough(handle.Handle);
        };
    }

    internal Point Hole { get; private set; }

    internal bool Dims(Point point) => Dim.Data?.FillContains(point) ?? false;

    internal void Place(PixelRect area, PixelPoint point)
    {
        var scaling = (Screens.ScreenFromPoint(area.Center) ?? Screens.Primary)?.Scaling ?? 1;
        var size = new Size(area.Width / scaling, area.Height / scaling);
        Position = area.Position;
        Width = size.Width;
        Height = size.Height;
        Hole = new Point((point.X - area.X) / scaling, (point.Y - area.Y) / scaling);
        Dim.Data = OverlayPlace.Spotlight(size, Hole, OverlayPlace.Size(this, "RecorderSpotlightSize") / 2);
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
    private RecorderClickRing? _pointer;
    private RecorderSpotlight? _spotlight;
    private RecorderKeyCaption? _caption;
    private PointerStyle _style = PointerStyle.Default;
    private int _ringTurn;
    private int _captionTurn;

    internal RecorderClickRing? RingWindow => _ring;

    internal RecorderClickRing? PointerWindow => _pointer;

    internal RecorderSpotlight? SpotlightWindow => _spotlight;

    internal RecorderKeyCaption? CaptionWindow => _caption;

    public void Style(PointerStyle style) => _style = style;

    public void Ring(PixelPoint point, PointerButton button)
    {
        _ring ??= new RecorderClickRing();
        _ring.Paint(_style.ClickColor(button));
        _ring.Place(point, "RecorderClickRingSize");
        if (!_ring.IsVisible) _ring.Show();
        var turn = ++_ringTurn;
        _ = HideLater(OverlayPlace.Hold(_ring, "RecorderClickRingHold", TimeSpan.FromMilliseconds(360)), () =>
        {
            if (turn == _ringTurn) _ring?.Hide();
        });
    }

    public void Pointer(PixelPoint point, PixelRect area)
    {
        var inside = area.Contains(point);
        if (_style.Spotlight)
        {
            _spotlight ??= new RecorderSpotlight();
            if (inside)
            {
                _spotlight.Place(area, point);
                if (!_spotlight.IsVisible) _spotlight.Show();
            }
            else if (_spotlight.IsVisible) _spotlight.Hide();
        }

        if (_style.Highlight)
        {
            _pointer ??= new RecorderClickRing();
            if (inside)
            {
                _pointer.Paint(_style.HighlightColor);
                _pointer.Place(point, _style.SizeKey);
                if (!_pointer.IsVisible) _pointer.Show();
            }
            else if (_pointer.IsVisible) _pointer.Hide();
        }
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
        _pointer?.Close();
        _spotlight?.Close();
        _caption?.Close();
        _ring = null;
        _pointer = null;
        _spotlight = null;
        _caption = null;
    }

    private static async System.Threading.Tasks.Task HideLater(TimeSpan hold, Action done)
    {
        await System.Threading.Tasks.Task.Delay(hold).ConfigureAwait(false);
        Dispatcher.UIThread.Post(done);
    }
}
