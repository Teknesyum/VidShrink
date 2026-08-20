using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class CalibrationProbeTests
{
    private const string Codec = "libx264";
    private const double SourceFps = 48.0;

    private static ComplexityProfile BaseProfile() => new()
    {
        ReferenceBppf = 0.08,
        Measured = true,
        DetailExponent = 0.55,
        SampledSeconds = 6,
        SampledFrames = 288
    };

    private static CalibrationSignature Signature(double scale = 1.0, double fps = SourceFps, string codec = Codec) => new()
    {
        Codec = codec,
        Width = 1920,
        Height = 1080,
        Fps = fps,
        Scale = scale
    };

    [Theory]
    [InlineData(4.0)]
    [InlineData(6.5)]
    [InlineData(9.0)]
    public void SolvesHalvingStepFromTwoMeasuredPoints(double step)
    {
        const double lowCrf = 25.0;
        const double highCrf = 29.0;
        const double lowBppf = 0.12;
        var highBppf = lowBppf / Math.Pow(2, (highCrf - lowCrf) / step);

        var profile = BaseProfile().Calibrate(Signature(), lowCrf, lowBppf, highCrf, highBppf, SourceFps);

        Assert.True(profile.Calibrated);
        Assert.Equal(step, profile.HalvingStep, 6);
        Assert.Equal(lowBppf, profile.BppfAtCrf(Codec, lowCrf, 1.0, SourceFps, SourceFps), 9);
        Assert.Equal(highBppf, profile.BppfAtCrf(Codec, highCrf, 1.0, SourceFps, SourceFps), 9);
        Assert.Equal(lowCrf, profile.CrfForBppf(Codec, lowBppf, 1.0, SourceFps, SourceFps), 6);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(20.0)]
    public void ClampsHalvingStepIntoSupportedRange(double step)
    {
        const double lowCrf = 23.0;
        const double highCrf = 27.0;
        const double lowBppf = 0.1;
        var highBppf = lowBppf / Math.Pow(2, (highCrf - lowCrf) / step);

        var profile = BaseProfile().Calibrate(Signature(), lowCrf, lowBppf, highCrf, highBppf, SourceFps);

        Assert.True(profile.HalvingStep >= 3.0 && profile.HalvingStep <= 12.0);
    }

    [Fact]
    public void CalibrationIsNotAppliedWhenSignatureDiffers()
    {
        var uncalibrated = BaseProfile();
        var calibrated = uncalibrated.Calibrate(Signature(), 25, 0.12, 29, 0.06, SourceFps);

        Assert.NotEqual(
            uncalibrated.BppfAtCrf(Codec, 25, 1.0, SourceFps, SourceFps),
            calibrated.BppfAtCrf(Codec, 25, 1.0, SourceFps, SourceFps), 9);

        Assert.Equal(
            uncalibrated.BppfAtCrf("libx265", 25, 1.0, SourceFps, SourceFps),
            calibrated.BppfAtCrf("libx265", 25, 1.0, SourceFps, SourceFps), 9);
        Assert.Equal(
            uncalibrated.BppfAtCrf(Codec, 25, 0.62, SourceFps, SourceFps),
            calibrated.BppfAtCrf(Codec, 25, 0.62, SourceFps, SourceFps), 9);
        Assert.Equal(
            uncalibrated.BppfAtCrf(Codec, 25, 1.0, 24.0, SourceFps),
            calibrated.BppfAtCrf(Codec, 25, 1.0, 24.0, SourceFps), 9);
        Assert.Equal(
            uncalibrated.CrfForBppf(Codec, 0.05, 1.0, 24.0, SourceFps),
            calibrated.CrfForBppf(Codec, 0.05, 1.0, 24.0, SourceFps), 9);

        Assert.False(calibrated.AppliesTo(Codec, 0.62, SourceFps));
        Assert.True(calibrated.AppliesTo(Codec, 1.0, SourceFps));
    }

    [Theory]
    [InlineData(0.0, 0.06)]
    [InlineData(0.12, 0.0)]
    [InlineData(double.NaN, 0.06)]
    [InlineData(0.12, 0.12)]
    [InlineData(0.06, 0.12)]
    public void FallsBackToUncalibratedProfileOnBrokenMeasurement(double lowBppf, double highBppf)
    {
        var uncalibrated = BaseProfile();
        var result = uncalibrated.Calibrate(Signature(), 25, lowBppf, 29, highBppf, SourceFps);

        Assert.False(result.Calibrated);
        Assert.Equal(1.0, result.LevelFactor);
        Assert.Equal(
            uncalibrated.BppfAtCrf(Codec, 25, 1.0, SourceFps, SourceFps),
            result.BppfAtCrf(Codec, 25, 1.0, SourceFps, SourceFps), 9);
    }

    [Fact]
    public void EstimateBandNarrowsOnlyWhenCalibrationApplies()
    {
        var measured = BaseProfile();
        var estimated = measured with { Measured = false };
        var scanned = (measured with { WindowBias = 1.19, BiasSource = WindowBiasSource.Scan })
            .Calibrate(Signature(), 25, 0.12, 29, 0.06, SourceFps);

        Assert.Equal(0.32, estimated.EstimateBand, 9);
        Assert.Equal(0.14, measured.EstimateBand, 9);
        Assert.Equal(0.05, scanned.EstimateBand, 9);
        Assert.Equal(0.05, scanned.EstimateBandFor(Codec, 1.0, SourceFps), 9);
        Assert.Equal(0.14, scanned.EstimateBandFor(Codec, 0.62, SourceFps), 9);
    }

    [Fact]
    public void WithoutCalibrationRestoresModelCoefficients()
    {
        var uncalibrated = BaseProfile();
        var reverted = uncalibrated.Calibrate(Signature(), 25, 0.12, 29, 0.06, SourceFps).WithoutCalibration();

        Assert.False(reverted.Calibrated);
        Assert.Null(reverted.Calibration);
        Assert.Equal(
            uncalibrated.BppfAtCrf(Codec, 25, 1.0, SourceFps, SourceFps),
            reverted.BppfAtCrf(Codec, 25, 1.0, SourceFps, SourceFps), 9);
    }
}
