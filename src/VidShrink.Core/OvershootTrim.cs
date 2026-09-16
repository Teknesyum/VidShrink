namespace VidShrink.Core;

public enum TrimSide
{
    End,
    Start,
    Both
}

public readonly record struct MediaPacket(bool IsVideo, double Pts, long Size, bool Keyframe);

public sealed record TrimPlan(TrimSide Side, double StartSeconds, double EndSeconds, double DurationSeconds, long KeptBytes)
{
    public double KeptSeconds => Math.Max(0, EndSeconds - StartSeconds);
    public double RemovedSeconds => Math.Max(0, DurationSeconds - KeptSeconds);
    public double RemovedFromStart => Math.Max(0, StartSeconds);
    public double RemovedFromEnd => Math.Max(0, DurationSeconds - EndSeconds);
}

public static class OvershootTrim
{
    public const double ThresholdPercent = 3.0;

    public static bool Offered(double actualMb, double targetMb)
    {
        if (targetMb <= 0 || actualMb <= targetMb) return false;
        return Math.Round((actualMb - targetMb) / targetMb * 100.0, 6) <= ThresholdPercent;
    }

    public static TrimPlan? Plan(IReadOnlyList<MediaPacket> packets, long fileBytes, long targetBytes, double durationSeconds, TrimSide side, long extraBytes = 0)
    {
        if (packets.Count == 0 || targetBytes <= 0 || durationSeconds <= 0) return null;

        var ordered = packets.Where(p => double.IsFinite(p.Pts) && p.Size >= 0).OrderBy(p => p.Pts).ToArray();
        if (ordered.Length == 0) return null;

        var origin = ordered[0].Pts;
        var payload = ordered.Sum(p => p.Size);
        var overhead = Math.Max(0, fileBytes - payload);
        var budget = targetBytes - overhead - Math.Max(0, extraBytes);
        if (budget <= 0) return null;

        var prefix = new long[ordered.Length + 1];
        for (var i = 0; i < ordered.Length; i++) prefix[i + 1] = prefix[i] + ordered[i].Size;

        var hasVideo = ordered.Any(p => p.IsVideo);
        var starts = Enumerable.Range(0, ordered.Length)
            .Where(i => hasVideo ? ordered[i].IsVideo && ordered[i].Keyframe : true)
            .ToArray();
        if (starts.Length == 0) return null;

        double Rel(int index) => index >= ordered.Length ? durationSeconds : Math.Min(durationSeconds, ordered[index].Pts - origin);

        int LastEnd(int from)
        {
            var end = ordered.Length;
            while (end > from && prefix[end] - prefix[from] > budget) end--;
            return end;
        }

        TrimPlan Make(int from, int end) => new(side, Rel(from), Rel(end), durationSeconds, prefix[end] - prefix[from] + overhead);

        switch (side)
        {
            case TrimSide.End:
            {
                var end = LastEnd(0);
                return end <= 0 ? null : Make(0, end);
            }
            case TrimSide.Start:
            {
                foreach (var from in starts)
                    if (prefix[ordered.Length] - prefix[from] <= budget)
                        return from >= ordered.Length ? null : Make(from, ordered.Length);
                return null;
            }
            default:
            {
                TrimPlan? best = null;
                var bestGap = double.MaxValue;
                foreach (var from in starts)
                {
                    var end = LastEnd(from);
                    if (end <= from) break;
                    var plan = Make(from, end);
                    var gap = Math.Abs(plan.RemovedFromStart - plan.RemovedFromEnd);
                    if (gap < bestGap)
                    {
                        best = plan;
                        bestGap = gap;
                    }
                    if (plan.RemovedFromStart >= plan.RemovedFromEnd) break;
                }
                return best;
            }
        }
    }
}
