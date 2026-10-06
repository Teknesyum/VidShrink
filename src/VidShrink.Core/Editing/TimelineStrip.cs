namespace VidShrink.Core.Editing;

public readonly record struct StripTile(long Time, long SourceTime);

/// <summary>
/// Duzenleyici cizelgesinin anahtar kare centikleri ve kucuk resim seridi icin saf hesap:
/// kaynak tick'lerinin cizelge anina inmesi, sik centiklerin seyreltilmesi ve gorunen aralik
/// icin esit aralikli kare anlari. Surec acmaz, arayuz bilmez.
/// </summary>
public static class TimelineStrip
{
    /// <summary>Saniye listesini sirali, tekil kaynak tick'lerine cevirir; sonlu olmayan ve eksi deger atilir.</summary>
    public static long[] SourceTicks(IReadOnlyList<double> seconds)
    {
        ArgumentNullException.ThrowIfNull(seconds);
        var ticks = new SortedSet<long>();
        foreach (var second in seconds)
            if (double.IsFinite(second) && second >= 0) ticks.Add(EditTime.FromSeconds(second));
        return ticks.ToArray();
    }

    /// <summary>
    /// Sirali kaynak tick'lerinden gorunen araliga dusenlerin cizelge anlari, artan sirada. Silinmis
    /// kaynaktaki kare cizelgede yoktur; ayni kaynagi iki kez gosteren cizelgede kare iki kez gorunur.
    /// </summary>
    public static IReadOnlyList<long> Keyframes(EditTimeline timeline, IReadOnlyList<long> source, long viewStart, long viewEnd)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(source);
        var result = new List<long>();
        if (source.Count == 0 || viewEnd < viewStart) return result;

        long start = 0;
        foreach (var clip in timeline.Clips)
        {
            var length = clip.TimelineLength;
            var end = start + length;
            if (start > viewEnd) break;
            if (end > viewStart)
            {
                var near = clip.ToSource(Math.Clamp(viewStart - start, 0, length - 1));
                var far = clip.ToSource(Math.Clamp(viewEnd - start, 0, length - 1));
                var slack = (long)Math.Ceiling(clip.Speed) + 1;
                var low = Math.Max(clip.SourceStart, Math.Min(near, far) - slack);
                var high = Math.Min(clip.SourceEnd - 1, Math.Max(near, far) + slack);
                var first = result.Count;
                for (var i = LowerBound(source, low); i < source.Count && source[i] <= high; i++)
                {
                    var time = start + clip.ToOffset(source[i]);
                    if (time >= viewStart && time <= viewEnd) result.Add(time);
                }

                if (clip.Reversed) result.Reverse(first, result.Count - first);
            }

            start = end;
        }

        return result;
    }

    /// <summary>
    /// Artan anlardan, bir oncekinden en az <paramref name="minSpacing"/> piksel uzakta olanlari
    /// birakir. Ilk an hep kalir; aralik ya da olcek gecersizse liste oldugu gibi doner.
    /// </summary>
    public static IReadOnlyList<long> Thin(IReadOnlyList<long> times, double pixelsPerTick, double minSpacing)
    {
        ArgumentNullException.ThrowIfNull(times);
        if (times.Count < 2 || !(pixelsPerTick > 0) || !(minSpacing > 0)) return times;
        var kept = new List<long>(times.Count) { times[0] };
        for (var i = 1; i < times.Count; i++)
            if ((times[i] - kept[^1]) * pixelsPerTick >= minSpacing) kept.Add(times[i]);
        return kept;
    }

    /// <summary>Bir karenin kapladigi tick: kare genisligi piksel olceginde, yukari yuvarlanir, en az 1.</summary>
    public static long TileStep(double pixelsPerTick, double tileWidth)
    {
        if (!(pixelsPerTick > 0) || !(tileWidth > 0) || !double.IsFinite(tileWidth / pixelsPerTick)) return 0;
        return Math.Max(1, (long)Math.Ceiling(tileWidth / pixelsPerTick));
    }

    /// <summary>
    /// Gorunen araliga degen kareler. Izgara <c>i × step</c> anlarina oturur: kaydirinca ayni anlar
    /// yeniden istenir. Her kare kendi aninin kaynak tick'ini tasir; cizelgenin sonundan sonra kare yok.
    /// </summary>
    public static IReadOnlyList<StripTile> Tiles(EditTimeline timeline, long viewStart, long viewEnd, long step)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        var tiles = new List<StripTile>();
        var duration = timeline.Duration;
        if (step <= 0 || duration <= 0 || viewEnd <= viewStart) return tiles;

        var index = Math.Max(0, viewStart) / step;
        long start = 0;
        var clip = 0;
        var clips = timeline.Clips;
        for (var time = index * step; time < Math.Min(viewEnd, duration); time += step)
        {
            while (clip < clips.Count && time >= start + clips[clip].TimelineLength)
            {
                start += clips[clip].TimelineLength;
                clip++;
            }

            if (clip >= clips.Count) break;
            tiles.Add(new StripTile(time, clips[clip].ToSource(time - start)));
        }

        return tiles;
    }

    private static int LowerBound(IReadOnlyList<long> sorted, long value)
    {
        var low = 0;
        var high = sorted.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (sorted[middle] < value) low = middle + 1;
            else high = middle;
        }

        return low;
    }
}
