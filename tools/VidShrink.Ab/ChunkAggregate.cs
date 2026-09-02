namespace VidShrink.Ab;

public sealed record ChunkQuality(
    string Chunk,
    int FrameWeight,
    double? Mean,
    double? Harmonic,
    double? P10,
    double? Min,
    double? Xpsnr,
    double? Ssim);

public sealed record CombinedQuality(
    double? Mean,
    double? Harmonic,
    double? WorstP10,
    double? Min,
    double? Xpsnr,
    double? Ssim,
    int TotalWeight);

public static class ChunkAggregate
{
    public static CombinedQuality Combine(IReadOnlyList<ChunkQuality> parts)
    {
        if (parts is null) throw new ArgumentNullException(nameof(parts));
        if (parts.Count == 0) throw new ArgumentException("Birleştirilecek parça yok.", nameof(parts));
        if (parts.Any(p => p.FrameWeight <= 0))
            throw new ArgumentException("Parça ağırlığı sıfır ya da eksi olamaz.", nameof(parts));

        return new CombinedQuality(
            WeightedMean(parts, p => p.Mean),
            WeightedHarmonic(parts, p => p.Harmonic),
            Worst(parts, p => p.P10),
            Worst(parts, p => p.Min),
            WeightedMean(parts, p => p.Xpsnr),
            WeightedMean(parts, p => p.Ssim),
            parts.Sum(p => p.FrameWeight));
    }

    private static double? WeightedHarmonic(IReadOnlyList<ChunkQuality> parts, Func<ChunkQuality, double?> select)
    {
        double weight = 0, reciprocal = 0;
        foreach (var part in parts)
        {
            if (select(part) is not { } value) return null;
            if (value <= 0) return null;
            weight += part.FrameWeight;
            reciprocal += part.FrameWeight / value;
        }
        return reciprocal <= 0 ? null : weight / reciprocal;
    }

    private static double? WeightedMean(IReadOnlyList<ChunkQuality> parts, Func<ChunkQuality, double?> select)
    {
        double weight = 0, total = 0;
        foreach (var part in parts)
        {
            if (select(part) is not { } value) return null;
            if (double.IsInfinity(value)) return null;
            weight += part.FrameWeight;
            total += part.FrameWeight * value;
        }
        return weight <= 0 ? null : total / weight;
    }

    private static double? Worst(IReadOnlyList<ChunkQuality> parts, Func<ChunkQuality, double?> select)
    {
        double? worst = null;
        foreach (var part in parts)
        {
            if (select(part) is not { } value) return null;
            if (worst is null || value < worst) worst = value;
        }
        return worst;
    }
}
