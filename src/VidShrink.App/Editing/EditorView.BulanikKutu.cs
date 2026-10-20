using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private Border? _blurBox;
    private Point _blurAnchor;

    internal Border? BlurBox => _blurBox;

    internal static Rect BlurDragRect(Point a, Point b, Rect frame)
    {
        static double Fit(double v, double low, double high) => Math.Clamp(v, low, Math.Max(low, high));
        var x1 = Fit(a.X, frame.Left, frame.Right);
        var x2 = Fit(b.X, frame.Left, frame.Right);
        var y1 = Fit(a.Y, frame.Top, frame.Bottom);
        var y2 = Fit(b.Y, frame.Top, frame.Bottom);
        return new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
    }

    private void InitBlurBox() => AddHandler(PointerMovedEvent, OnMonitorBlurMoved, RoutingStrategies.Tunnel);

    private Rect? MonitorFrameRect()
    {
        var frame = Preview.Frame;
        var size = frame.Bounds.Size;
        if (!frame.IsVisible || size.Width <= 0 || size.Height <= 0) return null;
        return frame.TranslatePoint(new Point(0, 0), Preview.Surface) is { } origin ? new Rect(origin, size) : null;
    }

    private void BeginBlurBox(PointerEventArgs e)
    {
        _blurAnchor = e.GetPosition(Preview.Surface);
        if (_blurBox is null)
        {
            _blurBox = new Border
            {
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                BorderThickness = new Thickness(this.TryFindResource("BorderThinScalar", out var thin) && thin is double t ? t : 0),
            };
            if (this.TryFindResource("NeonBlue", out var brush) && brush is IBrush edge) _blurBox.BorderBrush = edge;
            Preview.Surface.Children.Add(_blurBox);
        }

        MoveBlurBox(_blurAnchor);
        _blurBox.IsVisible = true;
    }

    private void MoveBlurBox(Point at)
    {
        if (_blurBox is null || MonitorFrameRect() is not { } frame) return;
        var box = BlurDragRect(_blurAnchor, at, frame);
        _blurBox.Margin = new Thickness(box.X, box.Y, 0, 0);
        _blurBox.Width = box.Width;
        _blurBox.Height = box.Height;
    }

    private void EndBlurBox()
    {
        if (_blurBox is not null) _blurBox.IsVisible = false;
    }

    private void OnMonitorBlurMoved(object? sender, PointerEventArgs e)
    {
        if (_blurStart is null || _blurBox is null) return;
        MoveBlurBox(e.GetPosition(Preview.Surface));
        e.Handled = true;
    }
}
