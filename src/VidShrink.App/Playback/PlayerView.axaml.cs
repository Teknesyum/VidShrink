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
using VidShrink.Ffmpeg.Playback;

namespace VidShrink.App.Playback;

internal partial class PlayerView : UserControl
{
    private readonly ZoomGesture _zoom = new();
    private readonly FullscreenSwitch _fullscreen = new();
    private readonly StallWatch _stall = new();
    private readonly SeekCoalescer _seek;
    private readonly List<string> _trace = new();

    private DecoderPipe? _pipe;
    private DecoderPipe.ContinuousPlayback? _play;
    private AudioSink? _audio;
    private WriteableBitmap? _bitmap;
    private DispatcherTimer? _watchdog;
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

        Strings.Changed += (_, _) => RefreshState();
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

    internal MenuFlyout? OpenedMenu { get; private set; }

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

    private void OnKey(object? sender, KeyEventArgs e)
    {
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
        flyout.Items.Add(new MenuItem { Header = Strings.Get("main.player.menu.playpause") });
        flyout.Items.Add(new MenuItem { Header = Strings.Get("main.player.menu.fullscreen") });
        flyout.Items.Add(new MenuItem { Header = Strings.Get("main.player.menu.reset") });
        return flyout;
    }

    private void OpenMenu()
    {
        var flyout = BuildMenu();
        OpenedMenu = flyout;
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
        if (_pipe is null) return;

        if (_playing)
        {
            _play?.Dispose();
            _play = _pipe.StartContinuousPlayback(_seek.Target);
            _audio?.Play();
            _stall.Reset();
        }
        else
        {
            _play?.Dispose();
            _play = null;
            _audio?.Pause();
            _stall.Reset();
        }
    }

    internal async Task OpenAsync(string path, CancellationToken ct = default)
    {
        Close();
        var pipe = new DecoderPipe();
        pipe.Faulted += OnFaulted;
        await pipe.OpenAsync(path, null, ct).ConfigureAwait(true);
        _pipe = pipe;
        _path = path;
        _seek.Duration = pipe.DurationSeconds;
        if (pipe.HasAudio)
        {
            _audio = new AudioSink(true);
            pipe.AttachAudioSink(_audio);
        }

        TxtEmpty.IsVisible = false;
        StartWatchdog();
        _seek.GoTo(0);
        RefreshState();
    }

    private void OnFaulted(object? sender, PipeFault fault)
        => Dispatcher.UIThread.Post(() =>
        {
            TxtStall.IsVisible = true;
            TxtStall.Text = Strings.Language == "tr" ? fault.ReasonTr : fault.ReasonEn;
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
        var frames = _play?.FramesDecoded ?? 0;
        var stalled = _stall.Observe(_playing, frames, nowSeconds);
        TxtStall.IsVisible = stalled;
        if (stalled) TxtStall.Text = Strings.Get("main.player.stalled");
    }

    private async Task RunSeekAsync(double atSeconds)
    {
        var pipe = _pipe;
        if (pipe is null) return;

        var frame = await pipe.SeekAsync(atSeconds).ConfigureAwait(true);
        if (frame is null)
        {
            TxtStall.IsVisible = true;
            TxtStall.Text = Strings.Get("main.player.seekfailed");
            return;
        }

        pipe.SeekAudio(atSeconds);
        Draw(frame);
    }

    private void Draw(DecoderPipeFrame frame)
    {
        if (_bitmap is null || _bitmap.PixelSize.Width != frame.Width || _bitmap.PixelSize.Height != frame.Height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(frame.Width, frame.Height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
        }

        using (var buffer = _bitmap.Lock())
        {
            var count = Math.Min(frame.Bgra.Length, buffer.RowBytes * buffer.Size.Height);
            System.Runtime.InteropServices.Marshal.Copy(frame.Bgra, 0, buffer.Address, count);
        }

        _zoom.SetSource(frame.Width, frame.Height);
        _zoom.SetViewport(Surface.Bounds.Width, Surface.Bounds.Height);
        Frame.Source = _bitmap;
        Frame.Width = frame.Width * _zoom.Scale * _zoom.PanelScale;
        Frame.Height = frame.Height * _zoom.Scale * _zoom.PanelScale;
        Frame.InvalidateVisual();
        TxtEmpty.IsVisible = false;
        RefreshState();
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
        _play?.Dispose();
        _play = null;
        _audio?.Dispose();
        _audio = null;
        _pipe?.Dispose();
        _pipe = null;
        _path = null;
        _playing = false;
    }
}
