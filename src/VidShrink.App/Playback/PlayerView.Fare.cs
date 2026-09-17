using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    internal const uint WmMoving = 0x0216;

    private readonly ClickArbiter _click = new();

    private SurfacePan? _pan;
    private PointerPressedEventArgs? _pressArgs;
    private DispatcherTimer? _clickTimer;
    private TranslateTransform? _shift;
    private Point _lastDrag;
    private string _dragMode = "";
    private TopLevel? _movingHookTop;
    private Win32Properties.CustomWndProcHookCallback? _movingHook;

    /// <summary>Menunun sol tik ile acilan ayarlar satirinin cagirdigi yol.</summary>
    internal Action? OpenSettings { get; set; }

    internal Func<List<Control>>? AppSettingsItems { get; set; }

    internal ClickArbiter Click => _click;

    internal SurfacePan Pan => _pan ??= new SurfacePan(SnapDip());

    /// <summary>Menu son kez neye yaslandi: fare konumuna mi, ust satirdaki dugmeye mi.</summary>
    internal string MenuAnchor { get; private set; } = "";

    internal string DragMode => _dragMode;

    /// <summary>
    /// Pencere tasima yolu. Windows'ta yerel tasima dongusu (Aero Snap yerinde kalir, miknatis
    /// WM_MOVING'de uygulanir); obur platformlarda kendi dongumuz.
    /// </summary>
    internal bool NativeMoveDrag { get; set; } = OperatingSystem.IsWindows();

    internal bool MovingHookInstalled => _movingHook is not null;

    private void InitFare()
    {
        AddHandler(PointerMovedEvent, OnFarePointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnFarePointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, (_, e) => { if (ReferenceEquals(e.Source, this)) EndWindowDrag("capturelost"); }, Avalonia.Interactivity.RoutingStrategies.Direct | Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        AttachedToVisualTree += (_, _) => InstallMovingHook();
        DetachedFromVisualTree += (_, _) => RemoveMovingHook();
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
        _windowDrag = false;
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
        _clickTimer.Interval = TimeSpan.FromMilliseconds(ClickArbiter.DoubleWindowMs);
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

    internal readonly record struct ScreenArea(PixelRect Bounds, PixelRect WorkingArea, double Scaling);

    internal static PixelPoint CenterSnap(PixelRect area, PixelSize size, PixelPoint position, int threshold)
    {
        var x = area.X + (area.Width - size.Width) / 2;
        var y = area.Y + (area.Height - size.Height) / 2;
        return new PixelPoint(
            Math.Abs(position.X - x) <= threshold ? x : position.X,
            Math.Abs(position.Y - y) <= threshold ? y : position.Y);
    }

    /// <summary>
    /// Pencere dikdortgenini bulundugu ekranin (merkezine en yakin ekran) calisma alani ortasina
    /// ceker; esik o ekranin olcegiyle piksele cevrilir. WM_MOVING ve kendi dongumuz bunu paylasir.
    /// </summary>
    internal static PixelRect SnapRect(PixelRect rect, IReadOnlyList<ScreenArea> screens, double snapDip)
    {
        if (screens.Count == 0) return rect;
        var center = new PixelPoint(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        var screen = screens[0];
        var best = long.MaxValue;
        foreach (var candidate in screens)
        {
            var b = candidate.Bounds;
            var dx = center.X < b.X ? (long)b.X - center.X : center.X >= b.Right ? (long)center.X - b.Right + 1 : 0;
            var dy = center.Y < b.Y ? (long)b.Y - center.Y : center.Y >= b.Bottom ? (long)center.Y - b.Bottom + 1 : 0;
            var distance = dx * dx + dy * dy;
            if (distance >= best) continue;
            best = distance;
            screen = candidate;
        }

        var threshold = (int)Math.Ceiling(snapDip * screen.Scaling);
        return new PixelRect(CenterSnap(screen.WorkingArea, rect.Size, rect.Position, threshold), rect.Size);
    }

    internal static PixelPoint DragPosition(PixelPoint windowStart, PixelPoint pointerStart, PixelPoint pointerNow, Size frameDip, double windowScaling, IReadOnlyList<ScreenArea> screens, double snapDip)
    {
        var position = new PixelPoint(windowStart.X + pointerNow.X - pointerStart.X, windowStart.Y + pointerNow.Y - pointerStart.Y);
        return SnapRect(new PixelRect(position, PixelSize.FromSize(frameDip, windowScaling)), screens, snapDip).Position;
    }

    private static List<ScreenArea> ScreenAreas(TopLevel top)
    {
        var screens = new List<ScreenArea>();
        if (top.Screens is { } all)
            foreach (var screen in all.All)
                screens.Add(new ScreenArea(screen.Bounds, screen.WorkingArea, screen.Scaling));
        return screens;
    }

    private void DragWindow(Window window, PixelPoint pointer)
    {
        var target = DragPosition(_dragWindowStart, _dragPointerStart, pointer, window.FrameSize ?? window.ClientSize, window.RenderScaling, ScreenAreas(window), SnapDip());
        if (target == window.Position) return;
        window.Position = target;
        var free = new PixelPoint(_dragWindowStart.X + pointer.X - _dragPointerStart.X, _dragWindowStart.Y + pointer.Y - _dragPointerStart.Y);
        _trace.Add((target == free ? "move -> " : "snap -> ") + FormattableString.Invariant($"{target.X},{target.Y}"));
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private void InstallMovingHook()
    {
        if (!OperatingSystem.IsWindows() || _movingHook is not null) return;
        if (TopLevel.GetTopLevel(this) is not Window top) return;
        _movingHookTop = top;
        _movingHook = OnMovingMessage;
        Win32Properties.AddWndProcHookCallback(top, _movingHook);
    }

    private void RemoveMovingHook()
    {
        if (_movingHookTop is { } top && _movingHook is { } hook) Win32Properties.RemoveWndProcHookCallback(top, hook);
        _movingHookTop = null;
        _movingHook = null;
    }

    private IntPtr OnMovingMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmMoving || lParam == IntPtr.Zero || _movingHookTop is not { } top) return IntPtr.Zero;
        var native = Marshal.PtrToStructure<NativeRect>(lParam);
        var rect = new PixelRect(native.Left, native.Top, native.Right - native.Left, native.Bottom - native.Top);
        var snapped = SnapRect(rect, ScreenAreas(top), SnapDip());
        if (snapped == rect) return IntPtr.Zero;
        Marshal.StructureToPtr(new NativeRect { Left = snapped.X, Top = snapped.Y, Right = snapped.Right, Bottom = snapped.Bottom }, lParam, false);
        _trace.Add(FormattableString.Invariant($"moving snap -> {snapped.X},{snapped.Y}"));
        handled = true;
        return new IntPtr(1);
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
        FareRelease(Environment.TickCount64);
    }
}
