using System;

namespace VidShrink.Core;

/// <summary>
/// Duraklatılabilen geri sayım. Saat dışarıdan verilir; duraklatınca kalan süre saklanır,
/// sürdürünce oradan devam eder. Bildirimi kendiliğinden kapatan sayaç bununla kurulur:
/// fare üstündeyken ya da odak içerideyken sayım durur.
/// </summary>
public sealed class PausableCountdown
{
    private readonly Func<TimeSpan> _now;
    private TimeSpan _remaining;
    private TimeSpan? _resumedAt;

    public PausableCountdown(TimeSpan duration, Func<TimeSpan> now)
    {
        _remaining = duration;
        _now = now;
    }

    public bool Paused => _resumedAt is null;

    public TimeSpan Remaining
    {
        get
        {
            if (_resumedAt is not { } start) return _remaining;
            var left = _remaining - (_now() - start);
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    public bool Elapsed => Remaining <= TimeSpan.Zero;

    public void Resume() => _resumedAt ??= _now();

    public void Pause()
    {
        if (_resumedAt is null) return;
        _remaining = Remaining;
        _resumedAt = null;
    }
}