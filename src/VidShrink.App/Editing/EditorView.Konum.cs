using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private Point? _moveStart;
    private ClipEffects? _moveEffects;
    private Vector _moveDelta;

    internal bool ClipMoving => _moveStart is not null;

    private void InitClipMove()
    {
        BtnClipMove.IsCheckedChanged += (_, _) =>
        {
            if (BtnClipMove.IsChecked == true) BtnClipBlurRegion.IsChecked = false;
            else CancelClipMove();
        };
        BtnClipBlurRegion.IsCheckedChanged += (_, _) =>
        {
            if (BtnClipBlurRegion.IsChecked == true) BtnClipMove.IsChecked = false;
        };
        AddHandler(PointerPressedEvent, OnMonitorMovePressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnMonitorMoveMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnMonitorMoveReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, (_, _) => CancelClipMove(), RoutingStrategies.Direct | RoutingStrategies.Bubble, true);
    }

    internal bool BeginClipMove(double fx, double fy)
    {
        if (!_showingClip || Exporting || _model is not { } model) return false;
        var targets = ClipTargets();
        if (targets.Count == 0) return false;
        _moveStart = new Point(fx, fy);
        _moveEffects = model.Clips[targets[0]].Effects;
        _moveDelta = default;
        return true;
    }

    internal bool DragClipTo(double fx, double fy)
    {
        if (_moveStart is not { } start || _moveEffects is not { } effects) return false;
        _moveDelta = new Vector(fx - start.X, fy - start.Y);
        ShowTransform(Moved(effects, _moveDelta));
        return true;
    }

    internal bool EndClipMove()
    {
        if (_moveStart is null) return false;
        return ApplyClipMove(TakeClipMove());
    }

    private Vector TakeClipMove()
    {
        var delta = _moveDelta;
        _moveStart = null;
        _moveEffects = null;
        _moveDelta = default;
        return delta;
    }

    private bool ApplyClipMove(Vector delta)
    {
        if (delta == default)
        {
            ShowClipPanel();
            return false;
        }

        return SetClipEffects(e => Moved(e, delta));
    }

    private void CancelClipMove()
    {
        if (_moveStart is null) return;
        TakeClipMove();
        ShowClipPanel();
    }

    private static ClipEffects Moved(ClipEffects effects, Vector delta)
        => (effects with { OffsetX = effects.OffsetX + delta.X, OffsetY = effects.OffsetY + delta.Y }).Normalized();

    private void OnMonitorMovePressed(object? sender, PointerPressedEventArgs e)
    {
        if (BtnClipMove.IsChecked != true) return;
        if (e.Source is not Visual source || !(ReferenceEquals(source, Preview.Surface) || Preview.Surface.IsVisualAncestorOf(source))) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || !MonitorFraction(e, out var fx, out var fy)) return;
        if (!BeginClipMove(fx, fy)) return;
        e.Pointer.Capture(Preview.Surface);
        e.Handled = true;
    }

    private void OnMonitorMoveMoved(object? sender, PointerEventArgs e)
    {
        if (_moveStart is null || !MonitorFraction(e, out var fx, out var fy)) return;
        DragClipTo(fx, fy);
        e.Handled = true;
    }

    private void OnMonitorMoveReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_moveStart is null) return;
        if (MonitorFraction(e, out var fx, out var fy)) DragClipTo(fx, fy);
        var delta = TakeClipMove();
        e.Pointer.Capture(null);
        ApplyClipMove(delta);
        e.Handled = true;
    }
}
