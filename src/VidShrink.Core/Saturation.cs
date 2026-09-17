using System.Globalization;

namespace VidShrink.Core;

public sealed record SizeSample(int VideoBitrateK, double ActualMb, bool OverCeiling);

public static class Saturation
{
    public const double DeadYield = 0.5;
    public const double FloorRequestDrop = 0.80;
    public const double FloorBytesHeld = 0.95;
    public const double FloorLayoutAim = 0.9;
    public const int FloorAudioK = 24;
    public const int ExtraAttemptsAtFloor = 1;

    public static bool AtEncoderFloor(SizeSample earlier, SizeSample later)
        => earlier.OverCeiling && later.OverCeiling
           && later.VideoBitrateK <= earlier.VideoBitrateK * FloorRequestDrop
           && later.ActualMb >= earlier.ActualMb * FloorBytesHeld;

    public static SizeSample? FloorReference(IEnumerable<SizeSample> earlier, SizeSample later)
        => earlier.FirstOrDefault(sample => AtEncoderFloor(sample, later));

    public static bool YieldIsDead(double? rawYield) => rawYield is double y && y < DeadYield;

    public static EncodePlan? StepLayoutDown(EncodePlan plan, SizeSample earlier, SizeSample later, double targetMb)
    {
        var scale = Math.Sqrt(FloorLayoutAim * targetMb / Math.Max(later.ActualMb, 1e-6));
        var why = $"retry: two attempts stayed over the {targetMb:0.###} MB target while the requested video bitrate fell from {earlier.VideoBitrateK}k to {later.VideoBitrateK}k and the file only moved from {earlier.ActualMb:0.###} MB to {later.ActualMb:0.###} MB, so the encoder is at its own floor and a lower bitrate buys nothing";

        if (scale < 1.0 && plan.LayoutStepMinHeight is int minHeight && plan.Height > minHeight)
        {
            var height = Math.Max(minHeight, Even(plan.Height * scale));
            var width = Math.Max(2, Even(plan.Width * (double)height / plan.Height));
            if (height < plan.Height)
            {
                var stepped = plan.Clone();
                stepped.Width = width;
                stepped.Height = height;
                stepped.Mode = "2pass";
                stepped.Crf = null;
                stepped.VideoBitrateK = Math.Min(plan.VideoBitrateK, Math.Max(PlanCalculator.RunnableVideoBitrateK(width, height, plan.Fps), later.VideoBitrateK));
                stepped.Reason = why + $"; the frame steps down from {plan.Width}x{plan.Height} to {width}x{height}, sqrt({FloorLayoutAim.ToString("0.#", CultureInfo.InvariantCulture)} x {targetMb:0.###} / {later.ActualMb:0.###}) = {scale:0.###}";
                stepped.ReasonCodes = new List<ReasonNote> { new(ReasonCode.RetryScaled, Width: width, Height: height, Mb: later.ActualMb, TargetMb: targetMb, Factor: scale) };
                return stepped;
            }
        }

        if (plan.AudioCodec is not null && plan.AudioCodec != "copy" && plan.AudioBitrateK > FloorAudioK)
        {
            var audioK = Math.Max(FloorAudioK, plan.AudioBitrateK / 2);
            var stepped = plan.Clone();
            stepped.AudioBitrateK = audioK;
            stepped.Streams = HalveEncodedAudio(plan.Streams);
            stepped.Reason = why + $"; the frame cannot step further down, so the audio budget halves from {plan.AudioBitrateK}k to {audioK}k";
            stepped.ReasonCodes = new List<ReasonNote> { new(ReasonCode.RetryScaled, Mb: later.ActualMb, TargetMb: targetMb, Factor: (double)audioK / plan.AudioBitrateK) };
            return stepped;
        }

        return null;
    }

    public static int UnderBandBitrateK(int currentK, int? overCeilingK, int budgetK)
    {
        if (overCeilingK is int high && high > currentK)
            return (int)Math.Round(Math.Sqrt((double)currentK * high));
        return Math.Max(currentK, Math.Min(currentK * 2, budgetK));
    }

    public static EncodePlan? StepDeadYield(EncodePlan plan, double actualMb, double targetMb, double durationSeconds, IEnumerable<SizeSample> samples)
    {
        var overK = samples.Where(s => s.OverCeiling && s.VideoBitrateK > plan.VideoBitrateK).Select(s => (int?)s.VideoBitrateK).Min();
        var budgetK = PlanCalculator.VideoBudgetK(targetMb, (int)Math.Ceiling(plan.NonVideoK), durationSeconds);
        var nextK = UnderBandBitrateK(plan.VideoBitrateK, overK, budgetK);
        if (nextK <= plan.VideoBitrateK) return null;
        var yieldText = PlanCalculator.RawEncoderYield(plan, actualMb, durationSeconds) is double y ? y.ToString("0.###", CultureInfo.InvariantCulture) : "unknown";
        var stepped = plan.Clone();
        stepped.Mode = "2pass";
        stepped.Crf = null;
        stepped.VideoBitrateK = nextK;
        stepped.Reason = overK is int high && high > plan.VideoBitrateK
            ? $"retry: {plan.VideoBitrateK}k produced {actualMb:0.###} MB, a yield of {yieldText} under the {DeadYield:0.0} line where scaling by yield stops meaning anything, and {high}k had already landed over the {targetMb:0.###} MB target; the next attempt bisects the two geometrically at {nextK}k"
            : $"retry: {plan.VideoBitrateK}k produced {actualMb:0.###} MB, a yield of {yieldText} under the {DeadYield:0.0} line where scaling by yield stops meaning anything; the next attempt doubles the bitrate once to {nextK}k, capped by the {budgetK}k the target leaves for video";
        stepped.ReasonCodes = new List<ReasonNote> { new(ReasonCode.RetryScaled, Mb: actualMb, TargetMb: targetMb, Factor: (double)nextK / Math.Max(1, plan.VideoBitrateK)) };
        return stepped;
    }

    public static bool FillIsSaturated(double deliveredMb, double targetMb) => targetMb > 0 && deliveredMb < targetMb * DeadYield;

    private static StreamPlan? HalveEncodedAudio(StreamPlan? streams)
    {
        if (streams is null) return null;
        var audio = streams.Audio.Select(track => track.Copies || track.BitrateK <= FloorAudioK ? track : track with { BitrateK = Math.Max(FloorAudioK, track.BitrateK / 2) }).ToList();
        var freedK = streams.Audio.Sum(track => track.BitrateK) - audio.Sum(track => track.BitrateK);
        return streams with { Audio = audio, SideK = Math.Max(0, streams.SideK - freedK) };
    }

    private static int Even(double value) => Math.Max(2, (int)Math.Round(value / 2.0) * 2);
}
