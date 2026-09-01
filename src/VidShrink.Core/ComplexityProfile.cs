namespace VidShrink.Core;

public sealed record CalibrationSignature
{
    private const double ScaleTolerance = 0.005;
    private const double FpsTolerance = 0.01;

    public required string Codec { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required double Fps { get; init; }
    public required double Scale { get; init; }

    public bool Matches(string codec, double scale, double fps)
        => string.Equals(Codec, codec, StringComparison.OrdinalIgnoreCase)
           && Math.Abs(Scale - scale) <= ScaleTolerance
           && Math.Abs(Fps - fps) <= FpsTolerance;
}

public sealed record EncodeSpeed
{
    public required string Codec { get; init; }
    public required string Preset { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required double FramesPerSecond { get; init; }
    public required long Frames { get; init; }
    public required double Seconds { get; init; }

    public bool Matches(EncodePlan plan)
        => string.Equals(Codec, plan.Codec, StringComparison.OrdinalIgnoreCase)
           && string.Equals(Preset, plan.Preset, StringComparison.OrdinalIgnoreCase)
           && Width == plan.Width
           && Height == plan.Height;
}

public sealed record TimeEstimate(double ExpectedSeconds, double LowSeconds, double HighSeconds, bool StreamCopy)
{
    public static TimeEstimate Copy { get; } = new(0, 0, 0, true);
}

public enum WindowBiasSource
{
    None,
    Packets,
    Scan
}

public sealed record QualitySample(double Bppf, double VmafNeg);

public sealed record QualityAnchor
{
    public required double Bppf { get; init; }
    public required double VmafNeg { get; init; }
    public required int Points { get; init; }
    public double SpreadHalvings { get; init; }
    public double? PerHalving { get; init; }
}

public readonly record struct QualityLevel(double AtReference, double PerHalving, bool Measured, bool SlopeMeasured);

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
    public const double DefaultMotionExponent = 0.871;
    private const double MotionExponentMin = 0.0;
    private const double MotionExponentMax = 1.4;
    public const double FloorComplexityAnchor = 0.1264;
    private const double FloorAdaptExponent = 0.5;
    private const double FloorAdaptMin = 0.6;
    private const double FloorAdaptMax = 1.6;
    private const double HalvingStepMin = 3.0;
    private const double HalvingStepMax = 12.0;
    private const double LevelFactorMin = 0.1;
    private const double LevelFactorMax = 10.0;
    public const double WindowBiasMin = 0.5;
    public const double WindowBiasMax = 2.0;

    public const double SampleWindowSeconds = 2.0;
    public const double SampleMotionFpsRatio = 0.5;
    public const double SampleContainerFixedBytes = 764.3;
    public const double SampleContainerBytesPerFrame = 6.545;
    public const double VmafNegMin = 1.0;
    public const double VmafNegMax = 100.0;
    private const double QualitySlopeMinSpreadHalvings = 0.25;
    private const double QualitySlopeMin = 1.5;
    private const double QualitySlopeMax = 15.0;

    private const double SpeedBand = 0.30;
    private const double FirstPassMinShare = 0.0;
    private const double FirstPassMaxShare = 1.0;

    private const double CalibratedBand = 0.05;
    private const double PacketBiasBand = 0.08;
    private const double MeasuredBand = 0.14;
    private const double EstimatedBand = 0.32;

    public required double ReferenceBppf { get; init; }
    public required bool Measured { get; init; }
    public double DetailExponent { get; init; } = DefaultDetailExponent;
    public double MotionExponent { get; init; } = DefaultMotionExponent;
    public bool MotionMeasured { get; init; }
    public double SampledSeconds { get; init; }
    public long SampledFrames { get; init; }
    public double LevelFactor { get; init; } = 1.0;
    public double HalvingStep { get; init; }
    public CalibrationSignature? Calibration { get; init; }
    public EncodeSpeed? Speed { get; init; }
    public double WindowBias { get; init; } = 1.0;
    public WindowBiasSource BiasSource { get; init; } = WindowBiasSource.None;
    public QualityAnchor? QualityAnchor { get; init; }
    public bool SampleContainerBiasRemoved { get; init; }

    public bool Calibrated => Calibration is not null && LevelFactor > 0 && HalvingStep > 0;

    public bool WindowBiasKnown => double.IsFinite(WindowBias) && WindowBias > 0;

    private double WindowDomainFactor => WindowBiasKnown ? WindowBias : 1.0;

    private double BiasBand => BiasSource switch
    {
        WindowBiasSource.Scan => CalibratedBand,
        WindowBiasSource.Packets => PacketBiasBand,
        _ => MeasuredBand
    };

    public double EstimateBand => Calibrated && WindowBiasKnown ? BiasBand : Measured ? MeasuredBand : EstimatedBand;

    public double EstimateBandFor(string codec, double scale, double fps)
        => AppliesTo(codec, scale, fps) && WindowBiasKnown ? BiasBand : Measured ? MeasuredBand : EstimatedBand;

    public bool QualityMeasured => QualityAnchor is not null;

    public ComplexityProfile WithoutSampleContainerBias(int width, int height)
    {
        if (SampleContainerBiasRemoved || !Measured || width <= 0 || height <= 0) return this;

        var windows = (int)Math.Round(SampledSeconds / SampleWindowSeconds, MidpointRounding.AwayFromZero);
        if (windows <= 0 || SampledFrames <= 0) return this;

        var framesPerWindow = SampledFrames / (double)windows;
        if (framesPerWindow < 1) return this;

        var pixels = (double)width * height;
        var bytesPerFrame = ProbeBppf * pixels / 8.0;
        var cleanFull = bytesPerFrame - (SampleContainerFixedBytes / framesPerWindow + SampleContainerBytesPerFrame);
        if (cleanFull <= 0) return this;

        var motion = MotionExponent;
        if (MotionMeasured)
        {
            var motionFrames = framesPerWindow * SampleMotionFpsRatio;
            var cleanMotion = bytesPerFrame * Math.Pow(2, MotionExponent)
                              - (SampleContainerFixedBytes / motionFrames + SampleContainerBytesPerFrame);
            if (cleanMotion > 0)
                motion = Math.Clamp(Math.Log2(cleanMotion / cleanFull), MotionExponentMin, MotionExponentMax);
        }

        var cleanBppf = cleanFull * 8.0 / pixels;
        var reference = WindowBiasKnown && WindowBias > 0 ? cleanBppf / WindowBias : cleanBppf;

        return this with
        {
            ReferenceBppf = Math.Clamp(reference, 0.002, 2.0),
            MotionExponent = motion,
            SampleContainerBiasRemoved = true
        };
    }

    public double ProbeBppf => ReferenceBppf * WindowDomainFactor;

    public QualityLevel Level
    {
        get
        {
            if (QualityAnchor is not { } anchor)
                return new QualityLevel(CodecModel.PriorQualityAtReference, CodecModel.PriorQualityPerHalving, false, false);

            var slopeMeasured = anchor.PerHalving is > 0;
            var perHalving = anchor.PerHalving ?? CodecModel.PriorQualityPerHalving;
            var offset = Math.Log2(Math.Max(anchor.Bppf, 1e-9) / Math.Max(ReferenceBppf, 1e-9));
            var atReference = Math.Clamp(anchor.VmafNeg - perHalving * offset, VmafNegMin, VmafNegMax);
            return new QualityLevel(atReference, perHalving, true, slopeMeasured);
        }
    }

    public ComplexityProfile WithProbeQuality(IReadOnlyList<double> vmafNegPerWindow)
        => WithMeasuredQuality(vmafNegPerWindow.Select(v => new QualitySample(ProbeBppf, v)).ToArray());

    public ComplexityProfile WithMeasuredQuality(IReadOnlyList<QualitySample> samples)
    {
        var usable = samples
            .Where(s => double.IsFinite(s.Bppf) && s.Bppf > 0 && double.IsFinite(s.VmafNeg) && s.VmafNeg is > 0 and <= VmafNegMax)
            .ToArray();
        if (usable.Length == 0) return this with { QualityAnchor = null };

        var logs = usable.Select(s => Math.Log2(s.Bppf)).ToArray();
        var meanLog = logs.Average();
        var meanVmaf = usable.Average(s => s.VmafNeg);
        var spread = logs.Max() - logs.Min();

        double? slope = null;
        if (spread >= QualitySlopeMinSpreadHalvings)
        {
            var sxx = logs.Sum(l => (l - meanLog) * (l - meanLog));
            var sxy = logs.Zip(usable, (l, s) => (l - meanLog) * (s.VmafNeg - meanVmaf)).Sum();
            if (sxx > 0)
            {
                var fitted = sxy / sxx;
                if (double.IsFinite(fitted) && fitted >= QualitySlopeMin && fitted <= QualitySlopeMax) slope = fitted;
            }
        }

        return this with
        {
            QualityAnchor = new QualityAnchor
            {
                Bppf = Math.Pow(2, meanLog),
                VmafNeg = meanVmaf,
                Points = usable.Length,
                SpreadHalvings = spread,
                PerHalving = slope
            }
        };
    }

    public static ComplexityProfile FromSourceBitrate(MediaInfo info)
    {
        var videoBps = Math.Max(1.0, info.TotalBitrateBps - info.AudioBitrateBps);
        var sourceBppf = videoBps / Math.Max(1.0, (double)info.Width * info.Height * info.Fps);
        var x264Equivalent = sourceBppf / CodecModel.SourceBitrateNeed(info.VideoCodec) * SourceSlackFactor;
        return new ComplexityProfile
        {
            ReferenceBppf = Math.Clamp(x264Equivalent, 0.004, 1.5),
            Measured = false,
            DetailExponent = DefaultDetailExponent,
            MotionExponent = DefaultMotionExponent,
            WindowBias = 0.0,
            BiasSource = WindowBiasSource.None
        };
    }

    public static ComplexityProfile FromProbe(double fullScaleBppf, double halfScaleBppf, double sampledSeconds, long sampledFrames, double windowBias = 0, WindowBiasSource biasSource = WindowBiasSource.Scan, double halfFpsBppf = 0)
    {
        var exponent = DefaultDetailExponent;
        if (fullScaleBppf > 0 && halfScaleBppf > 0)
            exponent = Math.Clamp(Math.Log(halfScaleBppf / fullScaleBppf) / Math.Log(ProbeScale), DetailExponentMin, DetailExponentMax);

        var motionMeasured = fullScaleBppf > 0 && halfFpsBppf > 0;
        var motion = motionMeasured
            ? Math.Clamp(Math.Log2(halfFpsBppf / fullScaleBppf), MotionExponentMin, MotionExponentMax)
            : DefaultMotionExponent;

        var bias = IsTrustedBias(windowBias) ? windowBias : 0.0;
        var corrected = bias > 0 ? fullScaleBppf / bias : fullScaleBppf;

        return new ComplexityProfile
        {
            ReferenceBppf = Math.Clamp(corrected, 0.002, 2.0),
            Measured = true,
            DetailExponent = exponent,
            MotionExponent = motion,
            MotionMeasured = motionMeasured,
            SampledSeconds = sampledSeconds,
            SampledFrames = sampledFrames,
            WindowBias = bias,
            BiasSource = bias > 0 ? biasSource : WindowBiasSource.None
        };
    }

    public static bool IsTrustedBias(double bias)
        => double.IsFinite(bias) && bias >= WindowBiasMin && bias <= WindowBiasMax;

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
        return ReferenceBppf * CodecModel.RelativeBitrateNeed(codec) * detail * TemporalFactor(fps, sourceFps);
    }

    public double TemporalFactor(double fps, double sourceFps)
        => Math.Pow(Math.Max(fps / Math.Max(sourceFps, 0.1), 0.05), -MotionExponent);

    public double FloorBppf(string codec, double fps, double sourceFps)
        => CodecModel.FloorBppf(codec) * FloorAdaptation * TemporalFactor(fps, sourceFps);

    public double FloorAdaptation
        => Measured && ReferenceBppf > 0
            ? Math.Clamp(Math.Pow(ReferenceBppf / FloorComplexityAnchor, FloorAdaptExponent), FloorAdaptMin, FloorAdaptMax)
            : 1.0;

    public double BppfAtCrf(string codec, double crf, double scale, double fps, double sourceFps)
    {
        var applies = AppliesTo(codec, scale, fps);
        var reference = RequiredBppf(codec, scale, fps, sourceFps) * (applies ? LevelFactor : 1.0);
        var step = applies ? HalvingStep : CodecModel.CrfHalvingStep(codec);
        var offset = (CodecModel.ReferenceCrf(codec) - crf) / step;
        return reference * Math.Pow(2, offset);
    }

    public double CrfForBppf(string codec, double bppf, double scale, double fps, double sourceFps)
    {
        var applies = AppliesTo(codec, scale, fps);
        var reference = RequiredBppf(codec, scale, fps, sourceFps) * (applies ? LevelFactor : 1.0);
        var step = applies ? HalvingStep : CodecModel.CrfHalvingStep(codec);
        var ratio = Math.Max(bppf, 1e-6) / Math.Max(reference, 1e-6);
        return CodecModel.ReferenceCrf(codec) - step * Math.Log2(ratio);
    }

    public bool AppliesTo(string codec, double scale, double fps)
        => Calibrated && Calibration!.Matches(codec, scale, fps);

    public double CrfStepSizeEffect(string codec, double scale, double fps)
    {
        var step = AppliesTo(codec, scale, fps) ? HalvingStep : CodecModel.CrfHalvingStep(codec);
        return Math.Pow(2, 1.0 / Math.Max(step, HalvingStepMin)) - 1.0;
    }

    public ComplexityProfile Calibrate(CalibrationSignature signature, double lowCrf, double lowBppf, double highCrf, double highBppf, double sourceFps)
    {
        if (!IsUsable(lowBppf) || !IsUsable(highBppf)) return WithoutCalibration();
        if (!double.IsFinite(lowCrf) || !double.IsFinite(highCrf)) return WithoutCalibration();

        var gap = highCrf - lowCrf;
        if (gap <= 0.5 || lowBppf <= highBppf) return WithoutCalibration();

        var decades = Math.Log2(lowBppf / highBppf);
        if (!double.IsFinite(decades) || decades <= 1e-6) return WithoutCalibration();

        var step = Math.Clamp(gap / decades, HalvingStepMin, HalvingStepMax);
        var modelled = RequiredBppf(signature.Codec, signature.Scale, signature.Fps, sourceFps)
                       * WindowDomainFactor
                       * Math.Pow(2, (CodecModel.ReferenceCrf(signature.Codec) - lowCrf) / step);
        if (!IsUsable(modelled)) return WithoutCalibration();

        var level = lowBppf / modelled;
        if (!double.IsFinite(level) || level <= 0) return WithoutCalibration();

        return this with
        {
            LevelFactor = Math.Clamp(level, LevelFactorMin, LevelFactorMax),
            HalvingStep = step,
            Calibration = signature
        };
    }

    public ComplexityProfile WithSpeed(EncodeSpeed? speed)
        => speed is null ? this : this with { Speed = speed };

    public TimeEstimate? EstimateTime(EncodePlan plan, double durationSeconds)
    {
        if (plan.ModeEnum == EncodeMode.PassThrough) return TimeEstimate.Copy;
        if (Speed is not { } speed || !speed.Matches(plan)) return null;
        if (speed.FramesPerSecond <= 0 || durationSeconds <= 0 || plan.Fps <= 0) return null;

        var onePass = durationSeconds * plan.Fps / speed.FramesPerSecond;
        if (!double.IsFinite(onePass) || onePass <= 0) return null;

        var twoPass = plan.ModeEnum == EncodeMode.TwoPass;
        var minShare = twoPass ? FirstPassMinShare : 0.0;
        var maxShare = twoPass ? FirstPassMaxShare : 0.0;
        var expected = onePass * (1.0 + (minShare + maxShare) / 2.0);
        var low = onePass * (1.0 + minShare) * (1.0 - SpeedBand);
        var high = onePass * (1.0 + maxShare) * (1.0 + SpeedBand);
        return new TimeEstimate(expected, low, high, false);
    }

    public ComplexityProfile WithoutCalibration()
        => Calibration is null ? this : this with { LevelFactor = 1.0, HalvingStep = 0, Calibration = null };

    private static bool IsUsable(double value) => double.IsFinite(value) && value > 0;
}

public sealed record SizeEstimate(double ExpectedMb, double LowMb, double HighMb, bool Measured, bool Enforced)
{
    public double SpreadRatio => ExpectedMb <= 0 ? 0 : (HighMb - LowMb) / ExpectedMb;
}
