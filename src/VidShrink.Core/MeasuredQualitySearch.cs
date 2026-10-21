namespace VidShrink.Core;

public sealed record QualitySearchRound(int Round, int MaxRounds, double TargetMb, double? MeasuredQuality);

public sealed record QualitySearchResult(
    double TargetMb,
    double? MeasuredQuality,
    double RequestedQuality,
    IReadOnlyList<QualitySearchRound> SearchRounds,
    bool Converged)
{
    public double? Deviation => MeasuredQuality - RequestedQuality;
}

public delegate Task<double?> QualitySampler(double targetMb, CancellationToken ct);

public static class MeasuredQualitySearch
{
    public const int MaxRounds = 4;
    public const double TolerancePoints = 1.0;
    public const double FallbackStep = 1.25;
    private const double SameTargetRatio = 0.001;

    public static bool ShouldRun(bool enabled, bool qualityDrivesTarget, bool meterAvailable, bool planOverridden)
        => enabled && qualityDrivesTarget && meterAvailable && !planOverridden;

    public static async Task<QualitySearchResult> RunAsync(
        double requestedQuality,
        double startMb,
        double floorMb,
        double ceilingMb,
        QualitySampler sample,
        Func<double, double> predictedAt,
        Func<double, double> targetForPredicted,
        IProgress<QualitySearchRound>? progress,
        CancellationToken ct,
        int maxRounds = MaxRounds,
        double tolerance = TolerancePoints)
    {
        if (sample is null) throw new ArgumentNullException(nameof(sample));
        if (predictedAt is null) throw new ArgumentNullException(nameof(predictedAt));
        if (targetForPredicted is null) throw new ArgumentNullException(nameof(targetForPredicted));
        if (maxRounds < 1) throw new ArgumentOutOfRangeException(nameof(maxRounds));
        if (!(floorMb > 0) || !(ceilingMb >= floorMb)) throw new ArgumentOutOfRangeException(nameof(ceilingMb));

        double Clamp(double mb) => Math.Clamp(mb, floorMb, ceilingMb);

        int Side(double quality)
            => quality < requestedQuality - tolerance ? -1
                : quality > requestedQuality + tolerance ? 1
                : 0;

        double Extrapolate(SearchProbe last)
        {
            var needMore = last.Value < requestedQuality;
            var bias = predictedAt(last.TargetMb) - last.Value;
            var modelled = targetForPredicted(requestedQuality + bias);
            var moves = double.IsFinite(modelled)
                        && Math.Abs(modelled / last.TargetMb - 1) > SameTargetRatio
                        && (modelled > last.TargetMb) == needMore;
            return moves ? modelled : needMore ? last.TargetMb * FallbackStep : last.TargetMb / FallbackStep;
        }

        var probes = new List<SearchProbe>();
        var rounds = new List<QualitySearchRound>();
        var next = Clamp(startMb);
        var converged = false;

        for (var round = 1; round <= maxRounds; round++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new QualitySearchRound(round, maxRounds, next, null));
            var measured = await sample(next, ct);
            var done = new QualitySearchRound(round, maxRounds, next, measured);
            rounds.Add(done);
            progress?.Report(done);

            if (measured is not { } quality || !double.IsFinite(quality) || quality <= 0) break;
            probes.Add(new SearchProbe(next, quality));
            if (Side(quality) == 0)
            {
                converged = true;
                break;
            }

            var step = BracketSearch.Next(probes, Side, Extrapolate);
            if (step.Kind == TargetStepKind.Exhausted) break;

            var candidate = Clamp(step.TargetMb);
            if (probes.Any(p => Math.Abs(candidate / p.TargetMb - 1) <= SameTargetRatio)) break;
            next = candidate;
        }

        return Pick(requestedQuality, startMb, tolerance, probes, rounds, converged);
    }

    private static QualitySearchResult Pick(double requested, double startMb, double tolerance,
        IReadOnlyList<SearchProbe> probes, IReadOnlyList<QualitySearchRound> rounds, bool converged)
    {
        if (probes.Count == 0)
            return new QualitySearchResult(startMb, null, requested, rounds, false);

        var chosen = probes
                         .Where(p => Math.Abs(p.Value - requested) <= tolerance)
                         .OrderBy(p => Math.Abs(p.Value - requested))
                         .ThenBy(p => p.TargetMb)
                         .FirstOrDefault()
                     ?? probes.Where(p => p.Value >= requested).OrderBy(p => p.TargetMb).FirstOrDefault()
                     ?? probes.OrderByDescending(p => p.Value).First();

        return new QualitySearchResult(chosen.TargetMb, chosen.Value, requested, rounds, converged);
    }
}
