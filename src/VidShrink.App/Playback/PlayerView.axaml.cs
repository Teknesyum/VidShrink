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
    internal const int HistorySaveTicks = 10;

    private readonly ZoomGesture _zoom = new();
    private readonly FullscreenSwitch _fullscreen = new();
    private readonly StallWatch _stall = new();
    private readonly SeekCoalescer _seek;
    private readonly List<string> _trace = new();

    private IPlaybackEngine? _engine;
    private WriteableBitmap? _bitmap;
    private DispatcherTimer? _watchdog;
    private DispatcherTimer? _render;
    private PlaybackHistory _history = new();
    private long _shown;
    private string? _path;
    private bool _playing;
    private bool _trackPaused;
    private int _leftClicks;
    private int _watchdogTicks;
    private double _volume = 100;
    private bool _muted;
    private double _speed = 1;
    private double _loopStart = double.NaN;
    private double _loopEnd = double.NaN;

    public PlayerView()
    {
        InitializeComponent();
        _seek = new SeekCoalescer(RunSeekAsync);

        AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        Surface.SizeChanged += OnSurfaceSize;
        InitWindow();

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

    internal double VolumeLevel => _volume;

    internal bool IsMuted => _muted;

    internal double SpeedFactor => _speed;

    internal double LoopStart => _loopStart;

    internal double LoopEnd => _loopEnd;

    internal PlaybackHistory History => _history;

    internal IReadOnlyList<string> Trace => _trace.ToArray();

    internal string? LoadedPath => _path;

    internal IPlaybackEngine? Engine => _engine;

    internal Func<IPlaybackEngine> EngineFactory { get; set; } = () => new MpvEngine();

    internal Func<string>? HistoryPath { get; set; }

    internal Func<int> PlayerTabIndex { get; set; } = () => 0;

    internal Func<int> CurrentTabIndex { get; set; } = () => 0;

    internal Action<int>? SelectTab { get; set; }

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
                _trace.Add("leavefullscreen -> " + _fullscreen.IsFullscreen);
                break;
            case PlayerCommandKind.Volume:
                _volume = Math.Clamp(_volume + command.Amount, 0, 100);
                _engine?.SetVolume(_volume);
                _trace.Add("volume " + command.Amount.ToString("0.###") + " -> " + _volume.ToString("0.###"));
                break;
            case PlayerCommandKind.ToggleMute:
                _muted = !_muted;
                _engine?.SetMuted(_muted);
                _trace.Add("mute -> " + _muted);
                break;
            case PlayerCommandKind.Speed:
                _speed = Math.Clamp(Math.Round(_speed + command.Amount, 2), Keymap.MinimumSpeed, Keymap.MaximumSpeed);
                _engine?.SetSpeed(_speed);
                _trace.Add("speed " + command.Amount.ToString("0.###") + " -> " + _speed.ToString("0.###"));
                break;
            case PlayerCommandKind.SpeedReset:
                _speed = 1;
                _engine?.SetSpeed(_speed);
                _trace.Add("speedreset -> " + _speed.ToString("0.###"));
                break;
            case PlayerCommandKind.FrameStep:
                StepFrame(command.Amount < 0);
                _trace.Add("frame " + command.Amount.ToString("0.###"));
                break;
            case PlayerCommandKind.LoopStart:
                MarkLoopStart();
                break;
            case PlayerCommandKind.LoopEnd:
                MarkLoopEnd();
                break;
            case PlayerCommandKind.LoopClear:
                _loopStart = double.NaN;
                _loopEnd = double.NaN;
                _engine?.SetLoop(_loopStart, _loopEnd);
                _trace.Add("loopclear");
                break;
            case PlayerCommandKind.BookmarkAdd:
                AddBookmark();
                break;
            case PlayerCommandKind.BookmarkNext:
                NextBookmark();
                break;
            default:
                if (!ApplyWindow(command)) _trace.Add("none");
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

    internal void FeedWheel(double notches, KeyModifiers modifiers)
        => Apply(Keymap.ForWheel(notches, modifiers));

    internal void FeedPress(PlayerButton button, int clicks = 1)
    {
        if (button == PlayerButton.Left) _leftClicks++;
        Apply(Keymap.ForPress(button, clicks));
    }

    internal bool FeedKey(Key key, KeyModifiers modifiers = KeyModifiers.None, string? symbol = null)
    {
        var command = Keymap.ForKey(key, modifiers, symbol);
        if (command.Kind == PlayerCommandKind.None) return false;
        Apply(command);
        return true;
    }

    internal double CurrentPosition()
    {
        if (_engine is { IsOpen: true } engine && _seek.Idle.IsCompleted)
        {
            var at = engine.PositionSeconds;
            if (double.IsFinite(at)) return at;
        }

        return _seek.Target;
    }

    private void StepFrame(bool backward)
    {
        if (_engine is not { } engine) return;
        _playing = false;
        _trackPaused = true;
        _stall.Reset();
        engine.StepFrame(backward);
    }

    private void MarkLoopStart()
    {
        _loopStart = CurrentPosition();
        if (double.IsFinite(_loopEnd) && _loopEnd <= _loopStart) _loopEnd = double.NaN;
        _engine?.SetLoop(_loopStart, _loopEnd);
        _trace.Add("loop a -> " + _loopStart.ToString("0.###"));
    }

    private void MarkLoopEnd()
    {
        var at = CurrentPosition();
        var start = double.IsFinite(_loopStart) ? _loopStart : 0;
        if (at <= start)
        {
            _trace.Add("loop b -> no");
            return;
        }

        _loopStart = start;
        _loopEnd = at;
        _engine?.SetLoop(_loopStart, _loopEnd);
        _trace.Add("loop b -> " + _loopEnd.ToString("0.###"));
    }

    private void AddBookmark()
    {
        if (_path is not { } path)
        {
            _trace.Add("bookmarkadd -> no");
            return;
        }

        var at = CurrentPosition();
        _history.AddBookmark(path, at);
        _history.Save(HistoryPath?.Invoke());
        _trace.Add("bookmarkadd -> " +at.ToString("0.###"));
    }

    private void NextBookmark()
    {
        if (_path is not { } path || _history.NextBookmark(path, CurrentPosition()) is not { } next)
        {
            _trace.Add("bookmarknext -> no");
            return;
        }

        _trackPaused = false;
        _seek.GoTo(next);
        _trace.Add("bookmarknext -> " +next.ToString("0.###"));
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        var notches = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
        if (notches == 0) return;
        FeedWheel(notches, e.KeyModifiers);
        e.Handled = true;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsSeekBarSource(e.Source)) return;
        var point = e.GetCurrentPoint(this);
        PlayerButton button;
        if (point.Properties.IsRightButtonPressed) button = PlayerButton.Right;
        else if (point.Properties.IsMiddleButtonPressed) button = PlayerButton.Middle;
        else if (point.Properties.IsLeftButtonPressed) button = PlayerButton.Left;
        else return;

        FeedPress(button, e.ClickCount);
        if (button != PlayerButton.Left || e.ClickCount >= 2) e.Handled = true;
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
        SaveHistory(false);
        base.OnDetachedFromVisualTree(e);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsEffectivelyVisible) return;
        if (e.Source is TextBox) return;
        if (FeedKey(e.Key, e.KeyModifiers, e.KeySymbol)) e.Handled = true;
    }

    private void OnMenuButton(object? sender, RoutedEventArgs e)
        => Apply(Keymap.OpenMenu.ToCommand());

    internal MenuFlyout BuildMenu()
    {
        var flyout = new MenuFlyout();
        var group = 0;
        foreach (var action in Keymap.MenuActions)
        {
            if (group != 0 && action.MenuGroup != group) flyout.Items.Add(new Separator());
            group = action.MenuGroup;

            var item = new MenuItem { Header = Strings.Get(action.LabelKey), Tag = action };
            if (Keymap.FirstKeyRow(action) is { } row)
                item.InputGesture = new KeyGesture(row.Input.Key, row.Input.Modifiers);
            item.Click += OnMenuRow;
            flyout.Items.Add(item);
        }

        AppendWindowMenu(flyout);
        return flyout;
    }

    private void OnMenuRow(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: PlayerAction action })
            Apply(action.ToCommand());
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
        _trackPaused = false;
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
            SaveHistory(false);
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
        if (drawn && (_playing || _trackPaused)) _seek.Follow(engine.PositionSeconds);
        if (drawn) RefreshState();
        if (_playing && engine.EndReached)
        {
            TogglePlay();
            SaveHistory(true);
            AfterEnd();
        }
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
        _loopStart = double.NaN;
        _loopEnd = double.NaN;
        if (HistoryPath?.Invoke() is { } file) _history = PlaybackHistory.Load(file);
        if (_volume != 100) engine.SetVolume(_volume);
        if (_muted) engine.SetMuted(true);
        if (_speed != 1) engine.SetSpeed(_speed);

        TxtEmpty.IsVisible = false;
        StartWatchdog();
        StartRender();
        var resume = HistoryPath is null ? 0 : _history.ResumeFor(path, engine.DurationSeconds);
        _seek.GoTo(resume);
        if (resume > 0) _trace.Add("resume -> " + resume.ToString("0.###"));
        AfterOpen(path, engine);
        if (!_playing) TogglePlay();
        RefreshState();
    }

    private void SaveHistory(bool finished)
    {
        if (_path is not { } path || HistoryPath?.Invoke() is not { } file) return;
        _history.Remember(path, CurrentPosition(), finished);
        _history.Save(file);
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
        _watchdogTicks = 0;
        _watchdog = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _watchdog.Tick += (_, _) => OnWatchdog();
        _watchdog.Start();
    }

    private void OnWatchdog()
    {
        PollStall(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
        if (!_playing) return;
        _watchdogTicks++;
        if (_watchdogTicks % HistorySaveTicks == 0) SaveHistory(false);
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

        if (TxtControls is null) return;
        var parts = new List<string> { Strings.Get("main.player.volume", _volume.ToString("0")) };
        if (_muted) parts.Add(Strings.Get("main.player.muted"));
        parts.Add(Strings.Get("main.player.speed", _speed.ToString("0.##")));
        parts.Add(double.IsFinite(_loopStart)
            ? Strings.Get("main.player.loopstate", _loopStart.ToString("0.##"), double.IsFinite(_loopEnd) ? _loopEnd.ToString("0.##") : "…")
            : Strings.Get("main.player.loopoff"));
        parts.Add(Strings.Get("main.player.bookmarkcount", _path is null ? 0 : _history.Bookmarks(_path).Count));
        TxtControls.Text = string.Join(" - ", parts);
        RefreshWindowState();
    }

    internal void Close()
    {
        SaveHistory(false);
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
        _trackPaused = false;
    }
}
