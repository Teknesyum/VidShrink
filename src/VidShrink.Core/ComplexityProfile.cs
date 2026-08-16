namespace VidShrink.Core;

public sealed record ComplexityProfile
{
    public const double ProbeCrf = 23.0;
    public const string ProbePreset = "medium";
    public const double ProbeScale = 0.5;

    private const double SourceSlackFactor = 0.82;
    private const double DefaultDetailExponent = 0.55;
    private const double LowScaleDamping = 0.3;
    private const double DetailExponentMin = -0.2;
    private const double DetailExponentMax = 1.4;

    public required double ReferenceBppf { get; init; }
    public required bool Measured { get; init; }
    public double DetailExponent { get; init; } = DefaultDetailExponent;
    public double SampledSeconds { get; init; }
    public long SampledFrames { get; init; }

    public static ComplexityProfile FromSourceBitrate(MediaInfo info)
    {
        var videoBps = Math.Max(1.0, info.TotalBitrateBps - info.AudioBitrateBps);
        var sourceBppf = videoBps / Math.Max(1.0, (double)info.Width * info.Height * info.Fps);
        var x264Equivalent = sourceBppf / CodecModel.SourceBitrateNeed(info.VideoCodec) * SourceSlackFactor;
        return new ComplexityProfile
        {
            ReferenceBppf = Math.Clamp(x264Equivalent, 0.004, 1.5),
            Measured = false,
            DetailExponent = DefaultDetailExponent
        };
    }

    public static ComplexityProfile FromProbe(double fullScaleBppf, double halfScaleBppf, double sampledSeconds, long sampledFrames)
    {
        var exponent = DefaultDetailExponent;
        if (fullScaleBppf > 0 && halfScaleBppf > 0)
            exponent = Math.Clamp(Math.Log(halfScaleBppf / fullScaleBppf) / Math.Log(ProbeScale), DetailExponentMin, DetailExponentMax);

        return new ComplexityProfile
        {
            ReferenceBppf = Math.Clamp(fullScaleBppf, 0.002, 2.0),
            Measured = true,
            DetailExponent = exponent,
            SampledSeconds = sampledSeconds,
            SampledFrames = sampledFrames
        };
    }

    public double ScaleFactor(double scale)
    {
        var s = Math.Clamp(scale, 0.05, 1.0);
        if (s >= ProbeScale)
            return Math.Pow(s, DetailExponent);

        var atProbe = Math.Pow(ProbeScale, DetailExponent);
        var damped = DetailExponent * LowScaleDamping;
        return atProbe * Math.Pow(s / ProbeScale, damped);
    }

    public double RequiredBppf(string codec, double scale, double fps, double sourceFps)
    {
        var detail = ScaleFactor(scale);
        var temporal = Math.Pow(Math.Max(fps / Math.Max(sourceFps, 0.1), 0.05), CodecModel.FpsBitrateExponent - 1.0);
        return ReferenceBppf * CodecModel.RelativeBitrateNeed(codec) * detail * temporal;
    }

    public double BppfAtCrf(string codec, double crf, double scale, double fps, double sourceFps)
    {
        var reference = RequiredBppf(codec, scale, fps, sourceFps);
        var offset = (CodecModel.ReferenceCrf(codec) - crf) / CodecModel.CrfHalvingStep(codec);
        return reference * Math.Pow(2, offset);
    }

    public double CrfForBppf(string codec, double bppf, double scale, double fps, double sourceFps)
    {
        var reference = RequiredBppf(codec, scale, fps, sourceFps);
        var ratio = Math.Max(bppf, 1e-6) / Math.Max(reference, 1e-6);
        return CodecModel.ReferenceCrf(codec) - CodecModel.CrfHalvingStep(codec) * Math.Log2(ratio);
    }
}

public sealed record SizeEstimate(double ExpectedMb, double LowMb, double HighMb, bool Measured, bool Enforced)
{
    public double SpreadRatio => ExpectedMb <= 0 ? 0 : (HighMb - LowMb) / ExpectedMb;
}
