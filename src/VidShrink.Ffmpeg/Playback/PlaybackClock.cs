using System.Diagnostics;

namespace VidShrink.Ffmpeg.Playback;

public sealed class PlaybackClock
{
    private readonly Stopwatch _stopwatch = new();
    private readonly object _gate = new();
    private double _baseSeconds;
    private double _rate = 1.0;
    private bool _running;

    public double Rate
    {
        get { lock (_gate) return _rate; }
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            lock (_gate)
            {
                _baseSeconds = PositionLocked();
                _rate = value;
                if (_running) Restart();
            }
        }
    }

    public bool IsRunning { get { lock (_gate) return _running; } }

    public void Start(double atSeconds)
    {
        lock (_gate)
        {
            _baseSeconds = atSeconds;
            _running = true;
            Restart();
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (!_running) return;
            _baseSeconds = PositionLocked();
            _running = false;
            _stopwatch.Stop();
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (_running) return;
            _running = true;
            Restart();
        }
    }

    public void Seek(double toSeconds)
    {
        lock (_gate)
        {
            _baseSeconds = toSeconds;
            if (_running) Restart();
        }
    }

    public double PositionSeconds { get { lock (_gate) return PositionLocked(); } }

    private double PositionLocked()
    {
        if (!_running) return _baseSeconds;
        return _baseSeconds + _stopwatch.Elapsed.TotalSeconds * _rate;
    }

    private void Restart()
    {
        _stopwatch.Restart();
    }
}
