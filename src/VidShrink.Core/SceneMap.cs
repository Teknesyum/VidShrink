namespace VidShrink.Core;

public readonly record struct SceneScore(double Time, double Score);

public readonly record struct ProbeFrame(double Time, long Size);

public sealed record Scene
{
    public required int Index { get; init; }
    public required double Start { get; init; }
    public required double End { get; init; }
    public required long Bits { get; init; }
    public required double Complexity { get; init; }

    public double Duration => End - Start;

    public double BitsPerSecond => Duration > 0 ? Bits / Duration : 0.0;
}

public readonly record struct ThresholdRule(
    double Floor,
    double Ceiling,
    double Offset,
    double Slope,
    double NeighbourhoodSeconds,
    double Percentile)
{
    public static ThresholdRule Measured => new(0.05, 0.15, 0.08, 2.09, 40.0, 0.90);

    public double At(double agitation) => Math.Clamp(Offset + Slope * agitation, Floor, Ceiling);
}

public sealed record SceneMap
{
    public const double FixedThreshold = 0.105;
    public const double DefaultThreshold = FixedThreshold;
    public const double DefaultMinSceneSeconds = 1.0;

    public required double Threshold { get; init; }
    public required double Duration { get; init; }
    public required IReadOnlyList<Scene> Scenes { get; init; }
    public ThresholdRule? Rule { get; init; }

    public static IReadOnlyList<double> CutTimes(
        IEnumerable<SceneScore> candidates,
        double threshold,
        double duration,
        double minSceneSeconds = DefaultMinSceneSeconds)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var cuts = new List<double>();
        var last = 0.0;
        foreach (var candidate in candidates.Where(c => c.Score >= threshold).OrderBy(c => c.Time))
        {
            if (candidate.Time - last < minSceneSeconds) continue;
            if (duration - candidate.Time < minSceneSeconds) continue;
            cuts.Add(candidate.Time);
            last = candidate.Time;
        }
        return cuts;
    }

    public static double Agitation(
        IReadOnlyList<SceneScore> ordered,
        double time,
        double duration,
        double frameRate,
        ThresholdRule rule)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        if (!double.IsFinite(duration) || duration <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration));
        if (!double.IsFinite(frameRate) || frameRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameRate));

        var width = rule.NeighbourhoodSeconds;
        var low = UpperBound(ordered, time - width);
        var high = UpperBound(ordered, time + width);
        var span = Math.Min(time + width, duration) - Math.Max(time - width, 0.0);
        if (span <= 0) return 0.0;

        var count = Math.Max(1, (int)Math.Round(span * frameRate));
        var index = (int)(rule.Percentile * count);
        var present = high - low;
        var zeros = Math.Max(0, count - present);
        if (index < zeros || present == 0) return 0.0;

        var values = new double[present];
        for (var i = 0; i < present; i++) values[i] = ordered[low + i].Score;
        Array.Sort(values);
        return values[Math.Min(index - zeros, present - 1)];
    }

    public static IReadOnlyList<double> DerivedCutTimes(
        IEnumerable<SceneScore> candidates,
        double duration,
        double frameRate,
        ThresholdRule rule,
        double minSceneSeconds = DefaultMinSceneSeconds)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var ordered = candidates.OrderBy(c => c.Time).ToArray();
        var cuts = new List<double>();
        var last = 0.0;
        foreach (var candidate in ordered)
        {
            var threshold = rule.At(Agitation(ordered, candidate.Time, duration, frameRate, rule));
            if (candidate.Score < threshold) continue;
            if (candidate.Time - last < minSceneSeconds) continue;
            if (duration - candidate.Time < minSceneSeconds) continue;
            cuts.Add(candidate.Time);
            last = candidate.Time;
        }
        return cuts;
    }

    public static SceneMap BuildDerived(
        double duration,
        IReadOnlyList<SceneScore> candidates,
        IReadOnlyList<ProbeFrame> frames,
        ThresholdRule rule,
        double minSceneSeconds = DefaultMinSceneSeconds)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(frames);
        if (!double.IsFinite(duration) || duration <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration));
        if (frames.Count == 0)
            throw new ArgumentException("Turetilen esik kare hizina dayanir; sonda karesi yok.", nameof(frames));

        var frameRate = frames.Count / duration;
        var cuts = DerivedCutTimes(candidates, duration, frameRate, rule, minSceneSeconds);
        return Assemble(duration, cuts, frames, double.NaN, rule);
    }

    private static int UpperBound(IReadOnlyList<SceneScore> ordered, double time)
    {
        var low = 0;
        var high = ordered.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (ordered[mid].Time <= time) low = mid + 1;
            else high = mid;
        }
        return low;
    }

    public static SceneMap Build(
        double duration,
        IReadOnlyList<SceneScore> candidates,
        double threshold,
        IReadOnlyList<ProbeFrame> frames,
        double minSceneSeconds = DefaultMinSceneSeconds)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(frames);
        if (!double.IsFinite(duration) || duration <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration));

        return Assemble(duration, CutTimes(candidates, threshold, duration, minSceneSeconds), frames, threshold, null);
    }

    private static SceneMap Assemble(
        double duration,
        IReadOnlyList<double> cuts,
        IReadOnlyList<ProbeFrame> frames,
        double threshold,
        ThresholdRule? rule)
    {
        var bounds = new List<double> { 0.0 };
        bounds.AddRange(cuts);
        bounds.Add(duration);

        var bits = new long[bounds.Count - 1];
        foreach (var frame in frames)
        {
            if (frame.Time < 0 || frame.Time >= duration) continue;
            bits[SegmentIndex(bounds, frame.Time)] += frame.Size * 8;
        }

        var meanBps = bits.Sum() / duration;
        var scenes = new List<Scene>(bits.Length);
        for (var i = 0; i < bits.Length; i++)
        {
            var start = bounds[i];
            var end = bounds[i + 1];
            var bps = bits[i] / (end - start);
            scenes.Add(new Scene
            {
                Index = i,
                Start = start,
                End = end,
                Bits = bits[i],
                Complexity = meanBps > 0 ? bps / meanBps : 0.0
            });
        }

        return new SceneMap { Threshold = threshold, Duration = duration, Scenes = scenes, Rule = rule };
    }

    private static int SegmentIndex(List<double> bounds, double time)
    {
        var low = 0;
        var high = bounds.Count - 2;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (bounds[mid] <= time) low = mid;
            else high = mid - 1;
        }
        return low;
    }

    public static double Spearman(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Count != b.Count) throw new ArgumentException("Dizi boylari esit degil.", nameof(b));
        if (a.Count < 2) return 0.0;

        var ra = Ranks(a);
        var rb = Ranks(b);
        var meanA = ra.Average();
        var meanB = rb.Average();

        double cov = 0, varA = 0, varB = 0;
        for (var i = 0; i < ra.Length; i++)
        {
            var da = ra[i] - meanA;
            var db = rb[i] - meanB;
            cov += da * db;
            varA += da * da;
            varB += db * db;
        }

        var denom = Math.Sqrt(varA * varB);
        return denom > 0 ? cov / denom : 0.0;
    }

    private static double[] Ranks(IReadOnlyList<double> values)
    {
        var order = Enumerable.Range(0, values.Count).OrderBy(i => values[i]).ToArray();
        var ranks = new double[values.Count];
        var i0 = 0;
        while (i0 < order.Length)
        {
            var i1 = i0;
            while (i1 + 1 < order.Length && values[order[i1 + 1]] == values[order[i0]]) i1++;
            var rank = (i0 + i1) / 2.0 + 1.0;
            for (var i = i0; i <= i1; i++) ranks[order[i]] = rank;
            i0 = i1 + 1;
        }
        return ranks;
    }
}
