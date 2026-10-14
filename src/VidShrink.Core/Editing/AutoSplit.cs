namespace VidShrink.Core.Editing;

public enum AutoSplitMode
{
    Count,
    Duration,
    Size,
}

/// <summary>
/// Otomatik bolmenin sinir listesi: esit sayida parca, sabit sureli parca ya da hedef boyutlu
/// parca. Hedef boyut kaynagin ortalama bit hizindan sureye cevrilir (bayt / saniye); kodlama
/// sonrasi boyutu soylemez. Sinirlar kaynak tick'inde, artan sirada; uc sinir yazilmaz. Liste
/// <see cref="EditTimeline.SplitSource"/>'a verilir.
/// </summary>
public static class AutoSplit
{
    public const int MaxParts = 1000;

    public static IReadOnlyList<long> Boundaries(long duration, AutoSplitMode mode, double value, long sourceBytes = 0)
    {
        if (duration <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Sure arti olmalidir");
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Deger arti ve sonlu olmalidir");

        return mode switch
        {
            AutoSplitMode.Count => ByCount(duration, value),
            AutoSplitMode.Duration => ByStep(duration, EditTime.FromSeconds(value)),
            AutoSplitMode.Size => ByStep(duration, SizeStep(duration, value, sourceBytes)),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Bilinmeyen bolme kipi"),
        };
    }

    private static IReadOnlyList<long> ByCount(long duration, double value)
    {
        if (value != Math.Floor(value) || value > MaxParts)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Parca sayisi 1 ile " + MaxParts + " arasinda tam sayi olmalidir");

        var count = (int)value;
        var boundaries = new long[count - 1];
        for (var i = 1; i < count; i++) boundaries[i - 1] = duration * i / count;
        return boundaries;
    }

    private static long SizeStep(long duration, double megabytes, long sourceBytes)
    {
        if (sourceBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceBytes), sourceBytes, "Kaynak boyutu bilinmiyor");

        var bytesPerSecond = sourceBytes / EditTime.ToSeconds(duration);
        var seconds = megabytes * Megabayt.Bayt / bytesPerSecond;
        if (!double.IsFinite(seconds) || seconds * EditTime.TicksPerSecond > long.MaxValue / 2) return duration;
        return EditTime.FromSeconds(seconds);
    }

    private static IReadOnlyList<long> ByStep(long duration, long step)
    {
        if (step < SceneSplit.MinPieceTicks)
            throw new ArgumentOutOfRangeException(nameof(step), step, "Parca en az " + SceneSplit.MinPieceSeconds + " sn olmalidir");
        if ((duration - 1) / step + 1 > MaxParts)
            throw new ArgumentOutOfRangeException(nameof(step), step, "Parca sayisi " + MaxParts + " ustunde");

        var boundaries = new List<long>();
        for (var at = step; at < duration; at += step) boundaries.Add(at);
        return boundaries;
    }
}
