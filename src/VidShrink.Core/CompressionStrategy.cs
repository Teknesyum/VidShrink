namespace VidShrink.Core;

public enum CompressionRegime { Light, Balanced, Aggressive, Extreme }

public enum SpeedMode { Quality, Fast }

public enum AdviceCode
{
    BudgetIsGenerous,
    QualityCeilingReached,
    CodecUpgradeRecommended,
    HardwareCodecCostsQuality,
    ResolutionReduced,
    FrameRateReduced,
    AudioReduced,
    AudioMono,
    AudioDropped,
    TargetEnforcedTwoPass,
    ExtremeRatioWarning,
    ContentIsSimple,
    ContentIsComplex,
    ScaleSavesLittle,
    ScaleSavesMuch,
    TargetBelowCodecFloor,
    FrameRateCutForFloor,
    MotionCutIsCheap,
    MotionCutIsExpensive,
    EncoderFallback,
    HdrTonemapped
}

public readonly record struct PenaltyWeights(double Scale, double Fps, bool LowFpsSurcharge);

public readonly record struct RegimeFloors(double MinScale, int MinHeight, double MinFps);

public sealed record StrategyAdvice(
    CompressionRegime Regime,
    double Ratio,
    string SuggestedCodec,
    CodecPreference SuggestedPreference,
    double PredictedQuality,
    IReadOnlyList<AdviceCode> Notes);

public static class CompressionStrategy
{
    public static CompressionRegime RegimeFor(double sourceMb, double targetMb)
    {
        var ratio = targetMb <= 0 ? 1.0 : sourceMb / targetMb;
        if (ratio < 1.5) return CompressionRegime.Light;
        if (ratio < 6.0) return CompressionRegime.Balanced;
        if (ratio < 30.0) return CompressionRegime.Aggressive;
        return CompressionRegime.Extreme;
    }

    public static double Ratio(double sourceMb, double targetMb)
        => targetMb <= 0 ? 1.0 : sourceMb / Math.Max(targetMb, 0.001);

    public static CodecPreference AutoPreference(CompressionRegime regime) => regime switch
    {
        CompressionRegime.Light => CodecPreference.Compatible,
        CompressionRegime.Balanced => CodecPreference.Compatible,
        _ => CodecPreference.MaxCompression
    };

    public static bool AllowsResolutionDrop(CompressionRegime regime) => regime != CompressionRegime.Light;

    public static bool AllowsFpsDrop(CompressionRegime regime)
        => regime is CompressionRegime.Aggressive or CompressionRegime.Extreme;

    public static double AudioBudgetShare(CompressionRegime regime) => regime switch
    {
        CompressionRegime.Light => 0.30,
        CompressionRegime.Balanced => 0.25,
        CompressionRegime.Aggressive => 0.18,
        _ => 0.12
    };

    public static PenaltyWeights PenaltyWeights(CompressionRegime regime) => regime switch
    {
        CompressionRegime.Aggressive => new PenaltyWeights(0.70, 0.70, true),
        CompressionRegime.Extreme => new PenaltyWeights(0.45, 0.35, false),
        _ => new PenaltyWeights(1.0, 1.0, true)
    };

    public static RegimeFloors FloorsFor(CompressionRegime regime) => regime switch
    {
        CompressionRegime.Aggressive => new RegimeFloors(0.20, 180, 10.0),
        CompressionRegime.Extreme => new RegimeFloors(0.12, 120, 6.0),
        _ => new RegimeFloors(0.25, 240, 12.0)
    };

    public static double TransparencyOffset(Intent intent) => intent switch
    {
        Intent.Archive => -6.0,
        Intent.Sharing => -3.0,
        _ => 0.0
    };
}
