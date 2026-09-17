using System.Globalization;

namespace VidShrink.Core;

public static class CeilingGuard
{
    public const double Aim = 0.90;
    public const double BelowTriedRequest = 0.95;
    public const int OverSamplesNeeded = 2;
    public const double VbvWindowSeconds = 1.0;

    public static bool CapsPeakAtRate(string codec)
        => FfmpegArguments.SupportsRateLimits(codec) && !CodecModel.IsHardware(codec);

    public static double WorstYield(EncodePlan last, IEnumerable<SizeSample> samples, double durationSeconds)
    {
        var worst = 1.0;
        foreach (var sample in samples.Where(s => s.OverCeiling))
        {
            var probe = last.Clone();
            probe.Mode = "2pass";
            probe.Crf = null;
            probe.VideoBitrateK = sample.VideoBitrateK;
            if (PlanCalculator.RawEncoderYield(probe, sample.ActualMb, durationSeconds) is double y && y > worst)
                worst = y;
        }
        return worst;
    }

    public static EncodePlan? Plan(EncodePlan last, IReadOnlyList<SizeSample> samples, double ceilingMb, double durationSeconds)
    {
        var over = samples.Where(s => s.OverCeiling).ToList();
        if (over.Count < OverSamplesNeeded || ceilingMb <= 0 || durationSeconds <= 0) return null;

        var capsPeak = CapsPeakAtRate(last.Codec);
        var worstYield = WorstYield(last, over, durationSeconds);
        var aimMb = Aim * ceilingMb;
        var window = durationSeconds + (capsPeak ? VbvWindowSeconds : 0.0);
        var budgetK = PlanCalculator.VideoBudgetK(aimMb, (int)Math.Ceiling(last.NonVideoK), window);
        var minTriedK = samples.Min(s => s.VideoBitrateK);
        var k = (int)Math.Floor(Math.Min(budgetK / worstYield, BelowTriedRequest * minTriedK));
        var runnableK = PlanCalculator.RunnableVideoBitrateK(last.Width, last.Height, last.Fps);
        if (k < runnableK) return null;

        var guarded = last.Clone();
        guarded.Mode = "2pass";
        guarded.Crf = null;
        guarded.VideoBitrateK = k;
        guarded.BitrateBias = 1.0;
        guarded.PeakEqualsRate = capsPeak;
        var yieldText = worstYield.ToString("0.###", CultureInfo.InvariantCulture);
        guarded.Reason = $"retry: ceiling guard, {over.Count} attempts stayed over the {ceilingMb:0.###} MB target, so the last attempt aims at {Aim:0.00} of it divided by the worst encoder yield {yieldText} seen in this run and asks for {k}k"
            + (capsPeak ? $" with the peak rate held at the average over a {VbvWindowSeconds:0} s buffer" : "");
        guarded.ReasonCodes = new List<ReasonNote> { new(ReasonCode.RetryScaled, Mb: over[^1].ActualMb, TargetMb: ceilingMb, AudioMb: 0, Factor: (double)k / Math.Max(1, last.VideoBitrateK)) };
        return guarded;
    }
}
