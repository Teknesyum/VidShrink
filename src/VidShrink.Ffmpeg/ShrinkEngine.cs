using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public enum ShrinkMeasureStage { Probed, Calibrated }

public static class ShrinkEngine
{
    public const int CalibrationRounds = 2;

    public static async Task<ComplexityProfile> ProbeWithMeasuredQualityAsync(
        MediaInfo info, SpeedMode speed, IQualityMeasurement? meter, CancellationToken ct)
    {
        var probed = await ComplexityProbe.RunDetailedAsync(info, speed, measureQuality: true, meter, ct);
        var anchors = probed.QualityMeasurements
            .Where(q => q is { Comparable: true, VmafNegMean: not null })
            .Select(q => q.VmafNegMean!.Value)
            .ToArray();
        return anchors.Length > 0 ? probed.Profile.WithProbeQuality(anchors) : probed.Profile;
    }

    public static SceneMap? CalibrationScenes(SceneMapAttempt? attempt) => attempt?.Map;

    public static SceneMap? QualityScenes(SceneMapAttempt? attempt) => attempt?.Map;

    public static IQualityMeasurement ProbeMeter(SceneMap? scenes)
        => scenes is null ? QualityMeasurement.Instance : new QualityMeasurement(scenes);

    public static async Task<ComplexityProfile?> CalibrateAsync(
        MediaInfo info,
        SceneMapAttempt? sceneMap,
        SpeedMode speed,
        Func<PlanOptions> options,
        IEncoderAvailability? availability,
        Func<ShrinkMeasureStage, ComplexityProfile, bool>? onProfile,
        CancellationToken ct)
    {
        var profile = await ProbeWithMeasuredQualityAsync(info, speed, ProbeMeter(QualityScenes(sceneMap)), ct);
        if (onProfile?.Invoke(ShrinkMeasureStage.Probed, profile) == false) return null;

        var current = profile;
        var draft = PlanCalculator.BuildDetailed(info, options(), profile, availability).Plan;

        for (var round = 0; round < CalibrationRounds; round++)
        {
            var calibrated = await CalibrationProbe.RunAsync(info, draft, profile, speed, ct, CalibrationScenes(sceneMap));
            current = calibrated;
            if (onProfile?.Invoke(ShrinkMeasureStage.Calibrated, calibrated) == false) return null;

            if (!calibrated.Calibrated) break;

            var settled = PlanCalculator.BuildDetailed(info, options(), calibrated, availability).Plan;
            if (calibrated.AppliesTo(settled.Codec, PlanScale(info, settled), settled.Fps)) break;

            draft = settled;
            profile = calibrated.WithoutCalibration();
        }

        return current;
    }

    public static PlanResult Decide(MediaInfo info, PlanOptions options, ComplexityProfile? profile, IEncoderAvailability? availability)
        => PlanCalculator.BuildDetailed(info, options, profile, availability);

    public static ComplexityProfile SettledProfile(MediaInfo info, PlanOptions options, ComplexityProfile? profile, IEncoderAvailability? availability)
        => profile ?? Decide(info, options, null, availability).Profile;

    public static QualityTargetResult TargetForQuality(MediaInfo info, PlanOptions options, double quality,
        ComplexityProfile? profile, IEncoderAvailability? availability)
        => PlanCalculator.TargetMbForQuality(info, options, quality, profile, availability);

    public static double RoundedTargetMb(QualityTargetResult result) => Math.Round(result.TargetMb, 1);

    public static IReadOnlyList<string> DisplayedArguments(MediaInfo info, EncodePlan plan,
        string outputPath, IEncoderAvailability? availability, SceneMap? scenes = null)
        => FfmpegArguments.Build(info, plan, outputPath,
            plan.ModeEnum == EncodeMode.TwoPass ? 2 : 0, null, availability, scenes);

    public static string UniqueOutputPath(string inputPath, string suffix = "shrunk", string extension = "mp4")
    {
        var dir = Path.GetDirectoryName(inputPath)!;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        const int firstIndex = 2;
        if (suffix == "shrunk" && name.EndsWith("_shrunk", StringComparison.OrdinalIgnoreCase))
            name = name[..^"_shrunk".Length];
        var candidate = Path.Combine(dir, $"{name}_{suffix}.{extension}");
        for (var index = firstIndex; PathEquals(candidate, inputPath) || File.Exists(candidate); index++)
            candidate = Path.Combine(dir, $"{name}_{suffix}_{index}.{extension}");
        return candidate;
    }

    public static Task<EncodeResult> EncodeAsync(
        MediaInfo info,
        EncodePlan plan,
        string outputPath,
        double targetMb,
        IProgress<EncodeProgress>? progress,
        CancellationToken ct,
        FillPolicy fillPolicy,
        ComplexityProfile? profile,
        RetryDecisionAsync? askBeforeRetry,
        SceneMap? scenes)
        => new EncodeRunner().RunAsync(info, plan, outputPath, targetMb, progress, ct, fillPolicy, profile, askBeforeRetry, scenes);

    private static double PlanScale(MediaInfo info, EncodePlan plan)
        => info.Height <= 0 ? 1.0 : (double)plan.Height / info.Height;

    private static bool PathEquals(string left, string right) => PathEquality.Same(left, right);
}
