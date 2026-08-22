using System.Diagnostics;
using Xunit.Abstractions;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class LiveSourceFactAttribute : FactAttribute
{
    public LiveSourceFactAttribute()
    {
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE");
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            Skip = "VIDSHRINK_LIVE_SOURCE does not point at an existing file, so the live encode time measurement was not run.";
        else if (!ToolLocator.IsAvailable(out _))
            Skip = "ffmpeg was not found, so the live encode time measurement was not run.";
    }
}

public sealed class CalibrationProbeTests
{
    private readonly ITestOutputHelper _output;

    public CalibrationProbeTests(ITestOutputHelper output) => _output = output;

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

    private static EncodePlan SpeedPlan(string mode = "2pass", double fps = SourceFps, string preset = "slow", int width = 1920, int height = 1080, string codec = Codec) => new()
    {
        Codec = codec,
        Mode = mode,
        Preset = preset,
        Width = width,
        Height = height,
        Fps = fps,
        Crf = mode == "crf" ? 23 : null,
        VideoBitrateK = mode == "crf" ? 0 : 4000
    };

    private static EncodeSpeed Measured(double framesPerSecond = 45.0, string preset = "slow", int width = 1920, int height = 1080, string codec = Codec) => new()
    {
        Codec = codec,
        Preset = preset,
        Width = width,
        Height = height,
        FramesPerSecond = framesPerSecond,
        Frames = 576,
        Seconds = 576 / framesPerSecond
    };

    [Fact]
    public void SinglePassTimeIsTheMeasuredFrameRateAppliedToThePlan()
    {
        var profile = BaseProfile().WithSpeed(Measured(45.0));

        var estimate = profile.EstimateTime(SpeedPlan("crf"), 60.0);

        Assert.NotNull(estimate);
        Assert.Equal(60.0 * SourceFps / 45.0, estimate!.ExpectedSeconds, 6);
        Assert.True(estimate.LowSeconds < estimate.ExpectedSeconds);
        Assert.True(estimate.HighSeconds > estimate.ExpectedSeconds);
    }

    [Fact]
    public void TwoPassTakesLongerThanASinglePass()
    {
        var profile = BaseProfile().WithSpeed(Measured(45.0));

        var single = profile.EstimateTime(SpeedPlan("crf"), 60.0)!;
        var twoPass = profile.EstimateTime(SpeedPlan(), 60.0)!;

        Assert.True(twoPass.ExpectedSeconds > single.ExpectedSeconds);
        Assert.True(twoPass.HighSeconds > single.HighSeconds);
        Assert.True(twoPass.HighSeconds <= single.ExpectedSeconds * 2 * 1.3 + 1e-9);
    }

    [Fact]
    public void LoweringTheFrameRateShortensTheEstimate()
    {
        var profile = BaseProfile().WithSpeed(Measured(45.0));

        var full = profile.EstimateTime(SpeedPlan(fps: SourceFps), 60.0)!;
        var halved = profile.EstimateTime(SpeedPlan(fps: SourceFps / 2), 60.0)!;

        Assert.Equal(full.ExpectedSeconds / 2.0, halved.ExpectedSeconds, 6);
    }

    [Fact]
    public void NoTimeWithoutAMeasurement()
    {
        Assert.Null(BaseProfile().EstimateTime(SpeedPlan(), 60.0));
        Assert.Null(BaseProfile().WithSpeed(null).EstimateTime(SpeedPlan(), 60.0));
        Assert.Null(BaseProfile().WithSpeed(Measured(0.0)).EstimateTime(SpeedPlan(), 60.0));
        Assert.Null(BaseProfile().WithSpeed(Measured()).EstimateTime(SpeedPlan(), 0.0));
    }

    [Theory]
    [InlineData("veryfast", 1920, 1080, Codec)]
    [InlineData("slow", 1280, 720, Codec)]
    [InlineData("slow", 1920, 1080, "hevc_nvenc")]
    public void NoTimeWhenTheSamplesUsedOtherSettings(string preset, int width, int height, string codec)
    {
        var profile = BaseProfile().WithSpeed(Measured());

        Assert.Null(profile.EstimateTime(SpeedPlan(preset: preset, width: width, height: height, codec: codec), 60.0));
    }

    [Fact]
    public void StreamCopyNeedsNoMeasurement()
    {
        var copy = BaseProfile().EstimateTime(SpeedPlan("passthrough"), 60.0);

        Assert.NotNull(copy);
        Assert.True(copy!.StreamCopy);
    }

    [LiveSourceFact]
    public async Task LiveTimeSurvivesTheTargetsThatKeepThePlanShape()
    {
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE")!;
        var info = await FfprobeClient.ProbeAsync(source);
        var opening = new PlanOptions { TargetMb = 16, FillPolicy = FillPolicy.FillTarget };

        var profile = await ComplexityProbe.RunAsync(info, CancellationToken.None);
        var draft = PlanCalculator.BuildDetailed(info, opening, profile, EncoderCapabilities.Instance).Plan;
        profile = await CalibrationProbe.RunAsync(info, draft, profile, CancellationToken.None);

        Assert.NotNull(profile.Speed);
        _output.WriteLine($"calibrated on {draft.Codec} {draft.Preset} {draft.Width}x{draft.Height} at {profile.Speed!.FramesPerSecond:0.0} fps");

        foreach (var targetMb in new[] { 8.0, 16.0, 25.0, 50.0, 100.0, 180.0 })
        {
            var plan = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = targetMb, FillPolicy = FillPolicy.FillTarget }, profile, EncoderCapabilities.Instance).Plan;
            var estimate = profile.EstimateTime(plan, info.DurationSeconds);
            var reading = estimate is null ? "-" : $"{estimate.LowSeconds:0}-{estimate.HighSeconds:0}s";
            _output.WriteLine($"  {targetMb:0.#} MB -> {plan.Codec} {plan.Preset} {plan.Width}x{plan.Height}@{plan.Fps:0.##} {reading}");
        }
    }

    [LiveSourceTheory]
    [InlineData(180.0)]
    [InlineData(8.0)]
    public async Task LiveEncodeTimeMatchesTheMeasuredEstimate(double targetMb)
    {
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE")!;
        var outDir = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_OUT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "vidshrink_live");
        Directory.CreateDirectory(outDir);
        var outputPath = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(source)}_time_{targetMb:0.#}mb.mp4");

        var info = await FfprobeClient.ProbeAsync(source);
        var options = new PlanOptions { TargetMb = targetMb, FillPolicy = FillPolicy.FillTarget };

        var round = Stopwatch.StartNew();
        var profile = await ComplexityProbe.RunAsync(info, CancellationToken.None);
        var complexitySeconds = round.Elapsed.TotalSeconds;
        var draft = PlanCalculator.BuildDetailed(info, options, profile, EncoderCapabilities.Instance).Plan;
        profile = await CalibrationProbe.RunAsync(info, draft, profile, CancellationToken.None);
        round.Stop();

        var plan = PlanCalculator.BuildDetailed(info, options, profile, EncoderCapabilities.Instance).Plan;
        var estimate = profile.EstimateTime(plan, info.DurationSeconds);

        var run = Stopwatch.StartNew();
        var result = await new EncodeRunner().RunAsync(info, plan, outputPath, targetMb, null, CancellationToken.None, FillPolicy.FillTarget, profile);
        run.Stop();

        var actual = run.Elapsed.TotalSeconds;
        var speed = profile.Speed;
        _output.WriteLine($"target {targetMb:0.#} MB | plan {plan.Codec} {plan.Preset} {plan.Width}x{plan.Height}@{plan.Fps:0.##} {plan.Mode}");
        _output.WriteLine($"  round: complexity {complexitySeconds:0.00}s calibration {round.Elapsed.TotalSeconds - complexitySeconds:0.00}s total {round.Elapsed.TotalSeconds:0.00}s");
        _output.WriteLine($"  speed: {speed?.FramesPerSecond ?? 0:0.0} fps measured from {speed?.Frames ?? 0} frames in {speed?.Seconds ?? 0:0.00}s");
        _output.WriteLine($"  actual: {actual:0.00}s attempts {result.Attempts} size {result.OutputMb:0.00} MB success {result.Success}");

        var planFrames = info.DurationSeconds * plan.Fps;
        _output.WriteLine($"  actual single-pass fps equivalent: {planFrames / actual:0.0} (one pass) {planFrames * 2 / actual:0.0} (two passes)");

        if (estimate is null)
        {
            _output.WriteLine("  estimate: none");
            return;
        }

        var deviation = (estimate.ExpectedSeconds - actual) / actual * 100.0;
        _output.WriteLine($"  estimate: {estimate.LowSeconds:0.0}-{estimate.HighSeconds:0.0}s expected {estimate.ExpectedSeconds:0.0}s deviation {deviation:+0.0;-0.0}%");
        Assert.True(actual >= estimate.LowSeconds && actual <= estimate.HighSeconds,
            $"The run took {actual:0.0}s, outside the {estimate.LowSeconds:0.0}-{estimate.HighSeconds:0.0}s range shown before it started.");

    }
}
