using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.Player;

namespace VidShrink.App.Playback;

internal partial class PlayerView : UserControl
{
    private readonly ZoomGesture _zoom = new();
    private readonly FullscreenSwitch _fullscreen = new();
    private readonly StallWatch _stall = new();
    private readonly SeekCoalescer _seek;
    private readonly List<string> _trace = new();

    private IPlaybackEngine? _engine;
    private WriteableBitmap? _bitmap;
    private DispatcherTimer? _watchdog;
    private DispatcherTimer? _render;
    private long _shown;
    private string? _path;
    private bool _playing;
    private int _leftClicks;

    public PlayerView()
    {
        InitializeComponent();
        _seek = new SeekCoalescer(RunSeekAsync);

        AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        Surface.SizeChanged += OnSurfaceSize;

        RefreshState();
    }

    internal event EventHandler<WindowSnapshot>? FullscreenChanged;

    internal SeekCoalescer Seek => _seek;

    internal ZoomGesture Zoom => _zoom;

    internal FullscreenSwitch Fullscreen => _fullscreen;

    internal StallWatch Stall => _stall;

    internal bool IsPlaying => _playing;

    internal int LeftClicks => _leftClicks;

    internal double PositionSeconds => _seek.Target;

    internal double ZoomScale => _zoom.PanelScale;

    internal IReadOnlyList<string> Trace => _trace.ToArray();

    internal string? LoadedPath => _path;

    internal IPlaybackEngine? Engine => _engine;

    internal Func<IPlaybackEngine> EngineFactory { get; set; } = () => new MpvEngine();

    internal Func<int> PlayerTabIndex { get; set; } = () => 0;

    internal Func<int> CurrentTabIndex { get; set; } = () => 0;

    internal Action<int>? SelectTab { get; set; }

    internal static PlayerModifiers Translate(KeyModifiers modifiers)
    {
        var value = PlayerModifiers.None;
        if ((modifiers & KeyModifiers.Control) != 0) value |= PlayerModifiers.Ctrl;
        if ((modifiers & KeyModifiers.Shift) != 0) value |= PlayerModifiers.Shift;
        if ((modifiers & KeyModifiers.Alt) != 0) value |= PlayerModifiers.Alt;
        return value;
    }

    internal void Apply(PlayerCommand command)
    {
        switch (command.Kind)
        {
            case PlayerCommandKind.Seek:
                _seek.Nudge(command.Amount);
                _trace.Add("seek " + command.Amount.ToString("0.###") + " -> " + _seek.Target.ToString("0.###"));
                break;
            case PlayerCommandKind.Zoom:
                _zoom.Wheel(command.Amount, Surface.Bounds.Width / 2, Surface.Bounds.Height / 2);
                _trace.Add("zoom " + command.Amount.ToString("0.###") + " -> " + _zoom.PanelScale.ToString("0.###"));
                break;
            case PlayerCommandKind.TogglePlay:
                TogglePlay();
                _trace.Add("play -> " + _playing);
                break;
            case PlayerCommandKind.ToggleFullscreen:
                ToggleFullscreen();
                _trace.Add("fullscreen -> " + _fullscreen.IsFullscreen);
                break;
            case PlayerCommandKind.ContextMenu:
                OpenMenu();
                _trace.Add("menu");
                break;
            case PlayerCommandKind.ResetZoom:
                _zoom.Reset();
                _trace.Add("zoomreset -> " + _zoom.PanelScale.ToString("0.###"));
                break;
            case PlayerCommandKind.LeaveFullscreen:
                if (_fullscreen.IsFullscreen) ToggleFullscreen();
                break;
            default:
                _trace.Add("none");
                break;
        }

        RefreshState();
        Echo(_trace.Count > 0 ? _trace[^1] : "");
    }

    [Conditional("DEBUG")]
    internal static void Echo(string line)
    {
        var path = Environment.GetEnvironmentVariable("VIDSHRINK_T176_TRACE");
        if (string.IsNullOrEmpty(path) || line.Length == 0) return;
        try { File.AppendAllText(path, line + Environment.NewLine); }
        catch (IOException) { }
    }

    internal void FeedWheel(double notches, PlayerModifiers modifiers)
        => Apply(PlayerInputMap.Wheel(notches, modifiers));

    internal void FeedPress(PlayerButton button)
    {
        if (button == PlayerButton.Left) _leftClicks++;
        Apply(PlayerInputMap.Press(button));
    }

    internal void FeedKey(PlayerKey key) => Apply(PlayerInputMap.Key(key));

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        var notches = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
        if (notches == 0) return;
        FeedWheel(notches, Translate(e.KeyModifiers));
        e.Handled = true;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        PlayerButton button;
        if (point.Properties.IsRightButtonPressed) button = PlayerButton.Right;
        else if (point.Properties.IsMiddleButtonPressed) button = PlayerButton.Middle;
        else if (point.Properties.IsLeftButtonPressed) button = PlayerButton.Left;
        else return;

        FeedPress(button);
        if (button != PlayerButton.Left) e.Handled = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Strings.Changed -= OnLanguageChanged;
        Strings.Changed += OnLanguageChanged;
        if (TopLevel.GetTopLevel(this) is { } top)
            top.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Strings.Changed -= OnLanguageChanged;
        if (TopLevel.GetTopLevel(this) is { } top)
            top.RemoveHandler(KeyDownEvent, OnKey);
        base.OnDetachedFromVisualTree(e);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (!IsEffectivelyVisible) return;

        var key = e.Key switch
        {
            Key.Space => PlayerKey.Space,
            Key.Apps => PlayerKey.Menu,
            Key.Escape => PlayerKey.Escape,
            _ => PlayerKey.None
        };

        if (key == PlayerKey.None) return;
        FeedKey(key);
        e.Handled = true;
    }

    private void OnMenuButton(object? sender, RoutedEventArgs e)
        => Apply(PlayerInputMap.MenuButton());

    internal MenuFlyout BuildMenu()
    {
        var flyout = new MenuFlyout();
        foreach (var (row, key) in PlayerInputMap.MenuRows)
        {
            var item = new MenuItem { Header = Strings.Get(key), Tag = row };
            item.Click += OnMenuRow;
            flyout.Items.Add(item);
        }

        return flyout;
    }

    private void OnMenuRow(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: PlayerMenuRow row })
            Apply(PlayerInputMap.MenuRow(row));
    }

    private void OpenMenu()
    {
        var flyout = BuildMenu();
        try { flyout.ShowAt(BtnPlayerMenu); }
        catch (InvalidOperationException) { }
    }

    internal void ToggleFullscreen()
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        var current = window is null
            ? new WindowSnapshot(0, 0, 0, 0, 0, CurrentTabIndex())
            : new WindowSnapshot(
                (int)window.WindowState,
                window.Position.X,
                window.Position.Y,
                window.Width,
                window.Height,
                CurrentTabIndex());

        var next = _fullscreen.Toggle(current, (int)WindowState.FullScreen, PlayerTabIndex());

        if (window is not null)
        {
            window.WindowState = (WindowState)next.State;
            if (next.State != (int)WindowState.FullScreen)
            {
                window.Position = new PixelPoint((int)next.X, (int)next.Y);
                window.Width = next.Width;
                window.Height = next.Height;
            }
        }

        SelectTab?.Invoke(next.TabIndex);
        FullscreenChanged?.Invoke(this, next);
    }

    internal void TogglePlay()
    {
        _playing = !_playing;
        if (_engine is not { } engine) return;

        _stall.Reset();
        if (_playing)
        {
            engine.Play();
        }
        else
        {
            _seek.Follow(engine.PositionSeconds);
            engine.Pause();
        }
    }

    private void StartRender()
    {
        _shown = 0;
        if (_render is null)
        {
            _render = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _render.Tick += (_, _) => RenderLatest();
        }
        _render.Start();
    }

    internal bool RenderLatest()
    {
        if (_engine is not { } engine) return false;
        var drawn = engine.TryCopyLatest(ref _shown, DrawFrame);
        if (drawn && _playing) _seek.Follow(engine.PositionSeconds);
        if (drawn) RefreshState();
        if (_playing && engine.EndReached) TogglePlay();
        return drawn;
    }

    internal async Task OpenAsync(string path, CancellationToken ct = default)
    {
        Close();
        IPlaybackEngine? engine = null;
        try
        {
            engine = EngineFactory();
            engine.Faulted += OnFaulted;
            await engine.OpenAsync(path, ct).ConfigureAwait(true);
        }
        catch
        {
            if (engine is not null)
            {
                engine.Faulted -= OnFaulted;
                engine.Dispose();
            }

            throw;
        }

        _engine = engine;
        _path = path;
        _seek.Duration = engine.DurationSeconds > 0 ? engine.DurationSeconds : double.PositiveInfinity;

        TxtEmpty.IsVisible = false;
        StartWatchdog();
        StartRender();
        _seek.GoTo(0);
        if (!_playing) TogglePlay();
        RefreshState();
    }

    private void OnFaulted(object? sender, PlaybackFault fault)
        => Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(sender, _engine)) return;
            var text = Strings.Get(fault.MessageKey);
            if (!string.IsNullOrEmpty(fault.MessageArg)) text += ": " + fault.MessageArg;
            TxtStall.IsVisible = true;
            TxtStall.Text = LanguageCatalog.Display(text);
        });

    private void StartWatchdog()
    {
        _watchdog?.Stop();
        _watchdog = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _watchdog.Tick += (_, _) => PollStall(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
        _watchdog.Start();
    }

    internal void PollStall(double nowSeconds)
    {
        var frames = _engine?.FramesRendered ?? 0;
        var stalled = _stall.Observe(_playing && _engine is { EndReached: false }, frames, nowSeconds);
        TxtStall.IsVisible = stalled;
        if (stalled) TxtStall.Text = Strings.Get("main.player.stalled");
    }

    private async Task RunSeekAsync(double atSeconds)
    {
        var engine = _engine;
        if (engine is null) return;

        var result = await engine.SeekAsync(atSeconds, SeekPrecision.Exact).ConfigureAwait(false);
        if (result.Outcome is not (SeekOutcome.Failed or SeekOutcome.TimedOut)) return;

        void ShowFailure()
        {
            if (!ReferenceEquals(engine, _engine)) return;
            TxtStall.IsVisible = true;
            TxtStall.Text = Strings.Get("main.player.seekfailed");
        }

        if (Dispatcher.UIThread.CheckAccess()) ShowFailure();
        else Dispatcher.UIThread.Post(ShowFailure);
    }

    private void DrawFrame(IntPtr pixels, int width, int height, int stride)
    {
        var fresh = _bitmap is null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height;
        if (fresh)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
        }

        using (var buffer = _bitmap!.Lock())
            FramePixels.CopyRows(pixels, stride, buffer.Address, buffer.RowBytes, Math.Min(height, buffer.Size.Height));

        if (fresh || !ReferenceEquals(Frame.Source, _bitmap))
        {
            _zoom.SetSource(width, height);
            Frame.Source = _bitmap;
            Resize();
        }
        Frame.InvalidateVisual();
        TxtEmpty.IsVisible = false;
        RefreshState();
    }

    /// <summary>
    /// Goruntuyu panonun bugunku olcusune sigdirir. Olcu iki yerden degisir: yeni kare
    /// gelince ve pano yeniden boyutlanınca. Ikisi de buraya girer, boylece pencere
    /// buyudugunde duraklatilmis goruntu de buyur.
    /// <c>ZoomGesture.Scale</c> band kademesinde <c>PanelScale</c>'i zaten iceriyor;
    /// burada ikinci kez carpilmaz.
    /// </summary>
    private void Resize()
    {
        if (Frame.Source is null) return;
        _zoom.SetViewport(Surface.Bounds.Width, Surface.Bounds.Height);
        Frame.Width = _zoom.ContentWidth;
        Frame.Height = _zoom.ContentHeight;
        Frame.InvalidateVisual();
    }

    private void OnSurfaceSize(object? sender, SizeChangedEventArgs e) => Resize();

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess()) RefreshState();
        else Dispatcher.UIThread.Post(RefreshState);
    }

    internal void RefreshState()
    {
        if (TxtState is null) return;
        TxtState.Text = Strings.Get(
            "main.player.state",
            _seek.Target.ToString("0.###"),
            _playing ? Strings.Get("main.player.playing") : Strings.Get("main.player.paused"),
            (_zoom.PanelScale * 100).ToString("0"));
    }

    internal void Close()
    {
        _watchdog?.Stop();
        _watchdog = null;
        _render?.Stop();
        _stall.Reset();
        if (_engine is { } engine)
        {
            engine.Faulted -= OnFaulted;
            engine.Dispose();
        }

        _engine = null;
        _shown = 0;
        _path = null;
        _playing = false;
    }
}
