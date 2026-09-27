using System.Diagnostics;
using VidShrink.Core.Editing;
using VidShrink.Player;

namespace VidShrink.App.Playback;

public sealed class EdlPreviewDriver : IDisposable
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(50);

    private readonly IPlaybackEngine _engine;
    private readonly EdlPreview _preview;
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _loop;
    private volatile bool _playing;
    private EdlPart? _reverse;
    private long _cursor;

    public EdlPreviewDriver(IPlaybackEngine engine, EdlPreview preview, TimeSpan? interval = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(preview);
        _engine = engine;
        _preview = preview;
        _interval = interval ?? DefaultInterval;
        if (_interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Aralik pozitif olmalidir");
    }

    public EdlPreview Preview => _preview;

    public bool Playing => _playing;

    public EdlPart? ReversingPart => _reverse;

    public long EdlPosition => _reverse is not null ? _cursor : Clamp(EditTime.FromSeconds(Finite(_engine.PositionSeconds)));

    public long TimelinePosition => _preview.ToTimeline(EdlPosition);

    public Task OpenAsync(CancellationToken ct = default) => _engine.OpenAsync(_preview.Uri, ct);

    public void Play()
    {
        _playing = true;
        if (_reverse is null) _engine.Play();
        if (_loop is null)
        {
            _loop = new CancellationTokenSource();
            _ = RunAsync(_loop.Token);
        }
    }

    public void Pause()
    {
        _playing = false;
        _engine.Pause();
    }

    public Task SeekAsync(long timelineTime, CancellationToken ct = default) =>
        SeekAsync(timelineTime, SeekPrecision.Exact, ct);

    public async Task SeekAsync(long timelineTime, SeekPrecision precision, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _reverse = null;
            var edl = _preview.ToEdl(timelineTime);
            var part = _preview.PartAtEdl(edl);
            ApplySpeed(part);
            await _engine.SeekAsync(EditTime.ToSeconds(edl), precision, ct).ConfigureAwait(false);
            if (part.Clip.Reversed) EnterReverse(part, edl);
            else if (_playing) _engine.Play();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task TickAsync(double elapsedSeconds, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_playing) return;

            if (_reverse is { } reversing)
            {
                await StepBackAsync(reversing, elapsedSeconds, ct).ConfigureAwait(false);
                return;
            }

            var part = _preview.PartAtEdl(EdlPosition);
            if (part.Clip.Reversed)
            {
                EnterReverse(part, part.EdlEnd - 1);
                await _engine.SeekAsync(EditTime.ToSeconds(_cursor), SeekPrecision.Exact, ct).ConfigureAwait(false);
                return;
            }

            ApplySpeed(part);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _loop?.Cancel();
        _loop?.Dispose();
        _loop = null;
    }

    private async Task StepBackAsync(EdlPart part, double elapsedSeconds, CancellationToken ct)
    {
        var step = EditTime.FromSeconds(Math.Max(0, elapsedSeconds) * (double)part.Clip.Speed);
        _cursor -= step;
        if (_cursor > part.EdlStart)
        {
            await _engine.SeekAsync(EditTime.ToSeconds(_cursor), SeekPrecision.Exact, ct).ConfigureAwait(false);
            return;
        }

        _reverse = null;
        if (part.Index + 1 >= _preview.Parts.Count)
        {
            _cursor = part.EdlStart;
            await _engine.SeekAsync(EditTime.ToSeconds(part.EdlStart), SeekPrecision.Exact, ct).ConfigureAwait(false);
            _playing = false;
            return;
        }

        var next = _preview.Parts[part.Index + 1];
        ApplySpeed(next);
        await _engine.SeekAsync(EditTime.ToSeconds(next.EdlStart), SeekPrecision.Exact, ct).ConfigureAwait(false);
        if (next.Clip.Reversed) EnterReverse(next, next.EdlEnd - 1);
        else _engine.Play();
    }

    private void EnterReverse(EdlPart part, long cursor)
    {
        _engine.Pause();
        _reverse = part;
        _cursor = Math.Clamp(cursor, part.EdlStart, part.EdlEnd - 1);
    }

    private void ApplySpeed(EdlPart part)
    {
        var speed = (double)part.Clip.Speed;
        if (Math.Abs(_engine.Speed - speed) > 1e-9) _engine.SetSpeed(speed);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(_interval);
            var clock = Stopwatch.StartNew();
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                var elapsed = clock.Elapsed.TotalSeconds;
                clock.Restart();
                await TickAsync(elapsed, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private long Clamp(long edlTime) => Math.Clamp(edlTime, 0, _preview.EdlDuration);

    private static double Finite(double seconds) => double.IsFinite(seconds) ? seconds : 0;
}
