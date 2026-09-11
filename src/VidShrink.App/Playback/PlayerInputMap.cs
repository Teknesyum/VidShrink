using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace VidShrink.App.Playback;

internal enum PlayerButton
{
    Left,
    Middle,
    Right
}

internal enum PlayerCommandKind
{
    None,
    Seek,
    Zoom,
    TogglePlay,
    ToggleFullscreen,
    ContextMenu,
    LeaveFullscreen,
    ResetZoom,
    Volume,
    ToggleMute,
    Speed,
    SpeedReset,
    FrameStep,
    LoopStart,
    LoopEnd,
    LoopClear,
    BookmarkAdd,
    BookmarkNext,
    AudioCycle,
    SubtitleCycle,
    SubtitleDelay,
    AudioDelay
}

internal readonly record struct PlayerCommand(PlayerCommandKind Kind, double Amount)
{
    internal static readonly PlayerCommand None = new(PlayerCommandKind.None, 0);
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

    internal void Follow(double atSeconds)
    {
        lock (_gate)
        {
            if (_pending || _running) return;
            _target = atSeconds < 0 ? 0 : atSeconds > Duration ? Duration : atSeconds;
            Position = _target;
        }
    }

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
