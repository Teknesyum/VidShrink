using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    private readonly ClickArbiter _click = new();

    private SurfacePan? _pan;
    private PointerPressedEventArgs? _pressArgs;
    private TranslateTransform? _shift;
    private Point _lastDrag;
    private string _dragMode = "";

    /// <summary>Menunun sol tik ile acilan ayarlar satirinin cagirdigi yol.</summary>
    internal Action? OpenSettings { get; set; }

    internal Func<List<Control>>? AppSettingsItems { get; set; }

    internal ClickArbiter Click => _click;

    internal SurfacePan Pan => _pan ??= new SurfacePan(SnapDip());

    /// <summary>Menu son kez neye yaslandi: fare konumuna mi, ust satirdaki dugmeye mi.</summary>
    internal string MenuAnchor { get; private set; } = "";

    internal string DragMode => _dragMode;

    internal bool NativeMoveDrag { get; set; } = OperatingSystem.IsWindows();

    private void InitFare()
    {
        AddHandler(PointerMovedEvent, OnFarePointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnFarePointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, (_, e) => { if (ReferenceEquals(e.Source, this)) EndWindowDrag("capturelost"); }, Avalonia.Interactivity.RoutingStrategies.Direct | Avalonia.Interactivity.RoutingStrategies.Bubble, true);
    }

    /// <summary>
    /// Sol basis yalniz kurulur; isini birakista yapar. Cift tik ayri bir hareket degil.
    /// </summary>
    internal void FarePress(PointerPressedEventArgs e, double x, double y)
    {
        _pressArgs = e;
        FarePress(x, y);
    }

    internal void FarePress(double x, double y)
    {
        _windowDrag = false;
        _leftClicks++;
        _dragMode = "";
        _click.Press(x, y);
        _lastDrag = new Point(x, y);
    }

    /// <summary>
    /// Fare oynadi. Esik gecilmeden once bos doner; gecilince kip secilir: normal pencerede
    /// <c>window</c> (pencere tasinir), maksimize ya da tam ekranda <c>pan</c> (goruntu kayar).
    /// </summary>
    internal string FareMove(double x, double y)
    {
        if (!_click.Move(x, y)) return "";

        if (_dragMode.Length == 0)
        {
            _dragMode = WindowIsNormal() ? "window" : "pan";
            _lastDrag = new Point(x, y);
            _trace.Add("drag -> " + _dragMode);
            return _dragMode;
        }

        if (_dragMode == "pan")
        {
            PanBy(x - _lastDrag.X, y - _lastDrag.Y);
            _lastDrag = new Point(x, y);
        }

        return _dragMode;
    }

    /// <summary>Birakis. Surukleme olmadan biten basis o anda duraklat/baslat olur.</summary>
    internal ReleaseOutcome FareRelease()
    {
        var outcome = _click.Release();
        _dragMode = "";
        if (outcome == ReleaseOutcome.Click) Apply(Keymap.ForPress(PlayerButton.Left));
        return outcome;
    }

    internal void ResetPan()
    {
        if (_pan is null) return;
        _pan.Reset();
        ShowPan();
    }

    private void PanBy(double deltaX, double deltaY)
    {
        var pan = Pan;
        pan.SetBounds(Frame.Bounds.Width, Frame.Bounds.Height, Surface.Bounds.Width, Surface.Bounds.Height);
        pan.Drag(deltaX, deltaY);
        ShowPan();
        _trace.Add(FormattableString.Invariant($"pan -> {pan.X:0.###},{pan.Y:0.###}"));
    }

    private void ShowPan()
    {
        if (_pan is null) return;
        _shift ??= new TranslateTransform();
        _shift.X = _pan.X;
        _shift.Y = _pan.Y;
        Frame.RenderTransform = _shift;
    }

    private double SnapDip()
        => this.TryFindResource("PlaybackBadgeMargin", out var value) && value is Thickness edge ? edge.Left : 0;

    private bool WindowIsNormal()
        => !Kip.TamEkran
           && (TopLevel.GetTopLevel(this) is not Window window || window.WindowState == WindowState.Normal);

    private void OnFarePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_windowDrag)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Pointer.Captured is null)
            {
                EndWindowDrag("nobutton");
                return;
            }

            if (TopLevel.GetTopLevel(this) is Window dragged) DragWindow(dragged, this.PointToScreen(e.GetPosition(this)));
            e.Handled = true;
            return;
        }

        if (_seekDragging || IsSeekBarSource(e.Source)) return;
        var point = e.GetPosition(this);
        var grab = _lastDrag;
        if (FareMove(point.X, point.Y) != "window") return;
        if (TopLevel.GetTopLevel(this) is not Window window) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _click.Cancel();
            _dragMode = "";
            return;
        }

        _click.Cancel();
        if (NativeMoveDrag)
        {
            _dragMode = "";
            if (_pressArgs is not { } press)
            {
                _trace.Add("movedrag -> nopress");
                return;
            }

            _trace.Add("movedrag -> native");
            window.BeginMoveDrag(press);
            return;
        }

        e.Pointer.Capture(this);
        _dragMode = "window";
        _windowDrag = true;
        _dragWindowStart = window.Position;
        _dragPointerStart = this.PointToScreen(grab);
        DragWindow(window, this.PointToScreen(point));
        e.Handled = true;
    }

    private void EndWindowDrag(string reason)
    {
        if (!_windowDrag && _dragMode != "window") return;
        _windowDrag = false;
        _dragMode = "";
        _click.Cancel();
        _trace.Add("dragend -> " + reason);
    }

    private bool _windowDrag;
    private PixelPoint _dragWindowStart;
    private PixelPoint _dragPointerStart;

    internal bool WindowDragging => _windowDrag;

    internal static PixelPoint DragPosition(PixelPoint windowStart, PixelPoint pointerStart, PixelPoint pointerNow)
        => new(windowStart.X + pointerNow.X - pointerStart.X, windowStart.Y + pointerNow.Y - pointerStart.Y);

    private void DragWindow(Window window, PixelPoint pointer)
    {
        var target = DragPosition(_dragWindowStart, _dragPointerStart, pointer);
        if (target == window.Position) return;
        window.Position = target;
        _trace.Add("move -> " + FormattableString.Invariant($"{target.X},{target.Y}"));
    }

    private void OnFarePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        if (_windowDrag)
        {
            _windowDrag = false;
            _dragMode = "";
            e.Handled = true;
        }

        if (IsSeekBarSource(e.Source)) return;
        FareRelease();
    }
}
