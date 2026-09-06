using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace VidShrink.App.Playback;

[Flags]
internal enum PlayerModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4
}

internal enum PlayerButton
{
    Left,
    Middle,
    Right
}

internal enum PlayerKey
{
    None,
    Space,
    Menu,
    Escape
}

internal enum PlayerCommandKind
{
    None,
    Seek,
    Zoom,
    TogglePlay,
    ToggleFullscreen,
    ContextMenu,
    LeaveFullscreen
}

internal readonly record struct PlayerCommand(PlayerCommandKind Kind, double Amount)
{
    internal static readonly PlayerCommand None = new(PlayerCommandKind.None, 0);
}

internal static class PlayerInputMap
{
    internal const double WheelStepSeconds = 1;
    internal const double CtrlStepSeconds = 10;
    internal const double ShiftStepSeconds = 60;
    internal const double CtrlShiftStepSeconds = 300;

    internal static double StepFor(PlayerModifiers modifiers)
    {
        var ctrl = (modifiers & PlayerModifiers.Ctrl) != 0;
        var shift = (modifiers & PlayerModifiers.Shift) != 0;
        if (ctrl && shift) return CtrlShiftStepSeconds;
        if (shift) return ShiftStepSeconds;
        if (ctrl) return CtrlStepSeconds;
        return WheelStepSeconds;
    }

    internal static PlayerCommand Wheel(double notches, PlayerModifiers modifiers)
    {
        if ((modifiers & PlayerModifiers.Alt) != 0)
            return new PlayerCommand(PlayerCommandKind.Zoom, notches);

        return new PlayerCommand(PlayerCommandKind.Seek, notches * StepFor(modifiers));
    }

    internal static PlayerCommand Press(PlayerButton button) => button switch
    {
        PlayerButton.Right => new PlayerCommand(PlayerCommandKind.TogglePlay, 0),
        PlayerButton.Middle => new PlayerCommand(PlayerCommandKind.ToggleFullscreen, 0),
        _ => PlayerCommand.None
    };

    internal static PlayerCommand Key(PlayerKey key) => key switch
    {
        PlayerKey.Space => new PlayerCommand(PlayerCommandKind.TogglePlay, 0),
        PlayerKey.Menu => new PlayerCommand(PlayerCommandKind.ContextMenu, 0),
        PlayerKey.Escape => new PlayerCommand(PlayerCommandKind.LeaveFullscreen, 0),
        _ => PlayerCommand.None
    };

    internal static PlayerCommand MenuButton() => new(PlayerCommandKind.ContextMenu, 0);
}

internal sealed class SeekCoalescer
{
    private readonly Func<double, Task> _seek;
    private readonly object _gate = new();
    private readonly List<double> _latencies = new();
    private readonly List<double> _targets = new();
    private readonly List<string> _failures = new();

    private double _target;
    private bool _pending;
    private bool _running;
    private Task _pump = Task.CompletedTask;

    internal SeekCoalescer(Func<double, Task> seek) => _seek = seek;

    internal double Duration { get; set; } = double.PositiveInfinity;

    internal double Target { get { lock (_gate) return _target; } }

    internal double Position { get; private set; }

    internal int SeekCalls { get; private set; }

    internal IReadOnlyList<double> LatenciesMs { get { lock (_gate) return _latencies.ToArray(); } }

    internal IReadOnlyList<double> IssuedTargets { get { lock (_gate) return _targets.ToArray(); } }

    internal IReadOnlyList<string> Failures { get { lock (_gate) return _failures.ToArray(); } }

    internal Task Idle { get { lock (_gate) return _pump; } }

    internal void Nudge(double deltaSeconds) => GoTo(Target + deltaSeconds);

    internal void GoTo(double atSeconds)
    {
        lock (_gate)
        {
            var clamped = atSeconds < 0 ? 0 : atSeconds > Duration ? Duration : atSeconds;
            _target = clamped;
            _pending = true;
            if (_running) return;
            _running = true;
            _pump = PumpAsync();
        }
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            double at;
            lock (_gate)
            {
                if (!_pending)
                {
                    _running = false;
                    return;
                }

                _pending = false;
                at = _target;
                SeekCalls++;
                _targets.Add(at);
            }

            var watch = Stopwatch.StartNew();
            try
            {
                await _seek(at).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lock (_gate) _failures.Add(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                watch.Stop();
                lock (_gate) _latencies.Add(watch.Elapsed.TotalMilliseconds);
                Position = at;
            }
        }
    }
}

internal readonly record struct WindowSnapshot(
    int State,
    double X,
    double Y,
    double Width,
    double Height,
    int TabIndex);

internal sealed class FullscreenSwitch
{
    private WindowSnapshot? _previous;

    internal bool IsFullscreen => _previous is not null;

    internal WindowSnapshot? Previous => _previous;

    internal WindowSnapshot Toggle(WindowSnapshot current, int fullscreenState, int playerTabIndex)
    {
        if (_previous is { } restore)
        {
            _previous = null;
            return restore;
        }

        _previous = current;
        return current with { State = fullscreenState, TabIndex = playerTabIndex };
    }

    internal WindowSnapshot? Leave()
    {
        if (_previous is not { } restore) return null;
        _previous = null;
        return restore;
    }
}

internal sealed class StallWatch
{
    private readonly double _limitSeconds;
    private long _frames = -1;
    private double _since;

    internal StallWatch(double limitSeconds = 2.0) => _limitSeconds = limitSeconds;

    internal bool Stalled { get; private set; }

    internal void Reset()
    {
        _frames = -1;
        Stalled = false;
    }

    internal bool Observe(bool playing, long framesDecoded, double nowSeconds)
    {
        if (!playing)
        {
            _frames = framesDecoded;
            _since = nowSeconds;
            Stalled = false;
            return false;
        }

        if (framesDecoded != _frames)
        {
            _frames = framesDecoded;
            _since = nowSeconds;
            Stalled = false;
            return false;
        }

        Stalled = nowSeconds - _since >= _limitSeconds;
        return Stalled;
    }
}
