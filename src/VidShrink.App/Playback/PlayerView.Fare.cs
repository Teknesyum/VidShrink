using System;
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
    private DispatcherTimer? _clickTimer;
    private TranslateTransform? _shift;
    private Point _lastDrag;
    private string _dragMode = "";

    /// <summary>Menunun sol tik ile acilan ayarlar satirinin cagirdigi yol.</summary>
    internal Action? OpenSettings { get; set; }

    internal ClickArbiter Click => _click;

    internal SurfacePan Pan => _pan ??= new SurfacePan(SnapDip());

    /// <summary>Menu son kez neye yaslandi: fare konumuna mi, ust satirdaki dugmeye mi.</summary>
    internal string MenuAnchor { get; private set; } = "";

    internal string DragMode => _dragMode;

    private void InitFare()
    {
        AddHandler(PointerMovedEvent, OnFarePointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnFarePointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Sol basis. Cift tik burada hemen tam ekrana gider; tek tik yalniz kurulur, isini
    /// birakista ve cift tik penceresi dolunca yapar. Doner deger: olay yutuldu mu.
    /// </summary>
    internal bool FarePress(PointerPressedEventArgs e, double x, double y)
    {
        _pressArgs = e;
        return FarePress(e.ClickCount, x, y);
    }

    internal bool FarePress(int clicks, double x, double y)
    {
        _leftClicks++;
        _dragMode = "";
        if (_click.Press(clicks, x, y) == PressOutcome.Double)
        {
            StopClickTimer();
            Apply(Keymap.ForPress(PlayerButton.Left, clicks));
            return true;
        }

        _lastDrag = new Point(x, y);
        return false;
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

    /// <summary>Birakis. Surukleme olmadan biten basis bekleyen tek tika donusur.</summary>
    internal ReleaseOutcome FareRelease(double nowMs)
    {
        var outcome = _click.Release(nowMs);
        _dragMode = "";
        if (outcome == ReleaseOutcome.Click) StartClickTimer();
        else StopClickTimer();
        return outcome;
    }

    /// <summary>Cift tik penceresi doldu mu; dolduysa bekleyen tek tik duraklat/baslat olur.</summary>
    internal bool FareDue(double nowMs)
    {
        if (!_click.Due(nowMs)) return false;
        StopClickTimer();
        Apply(Keymap.ForPress(PlayerButton.Left, 1));
        return true;
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
        => !_fullscreen.IsFullscreen
           && (TopLevel.GetTopLevel(this) is not Window window || window.WindowState == WindowState.Normal);

    private void StartClickTimer()
    {
        _clickTimer ??= NewClickTimer();
        _clickTimer.Stop();
        _clickTimer.Start();
    }

    private void StopClickTimer() => _clickTimer?.Stop();

    private DispatcherTimer NewClickTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ClickArbiter.DoubleWindowMs) };
        timer.Tick += (_, _) => FareDue(Environment.TickCount64 + ClickArbiter.DoubleWindowMs);
        return timer;
    }

    private void OnFarePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_seekDragging || IsSeekBarSource(e.Source)) return;
        var point = e.GetPosition(this);
        if (FareMove(point.X, point.Y) != "window") return;
        if (TopLevel.GetTopLevel(this) is not Window window || _pressArgs is not { } press) return;

        _click.Cancel();
        _dragMode = "";
        window.BeginMoveDrag(press);
    }

    private void OnFarePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        if (IsSeekBarSource(e.Source)) return;
        FareRelease(Environment.TickCount64);
    }
}
