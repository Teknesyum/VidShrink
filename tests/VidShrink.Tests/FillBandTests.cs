using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class FillBandTests
{
    private static MediaInfo SampleInfo() => new()
    {
        FilePath = "sample.mp4",
        FileSizeBytes = 500L * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    [Theory]
    [InlineData(180, 174.96, 169.92)]
    [InlineData(50, 48.6, 47.2)]
    [InlineData(25, 23.75, 22.5)]
    [InlineData(10, 9.5, 9.0)]
    [InlineData(8, 7.36, 6.8)]
    [InlineData(1, 0.92, 0.85)]
    public void ForScalesBandByTargetClass(double targetMb, double expectedLower, double expectedFloor)
    {
        var band = FillBand.For(targetMb);

        Assert.Equal(expectedLower, band.LowerMb, 2);
        Assert.Equal(expectedFloor, band.HardFloorMb, 2);
        Assert.Equal(targetMb, band.UpperMb);
    }

    [Fact]
    public void FillTargetNeverEstimatesAboveTheUpperBound()
    {
        var info = SampleInfo();
        var options = new PlanOptions { TargetMb = 60, Intent = Intent.Sharing, FillPolicy = FillPolicy.FillTarget };

        var result = PlanCalculator.BuildDetailed(info, options, null);
        var estimate = PlanCalculator.Estimate(result.Plan, info, result.Profile);

        Assert.True(estimate.ExpectedMb <= options.TargetMb + 0.5,
            $"Expected fill estimate to stay at or under the target, got {estimate.ExpectedMb:0.0} MB for a {options.TargetMb} MB target.");
    }

    [Fact]
    public void FillTargetDoesNotProduceASmallerPlanThanQualityCeiling()
    {
        var info = SampleInfo();
        var fillOptions = new PlanOptions { TargetMb = 60, Intent = Intent.Sharing, FillPolicy = FillPolicy.FillTarget };
        var ceilingOptions = new PlanOptions { TargetMb = 60, Intent = Intent.Sharing, FillPolicy = FillPolicy.QualityCeiling };

        var fillResult = PlanCalculator.BuildDetailed(info, fillOptions, null);
        var ceilingResult = PlanCalculator.BuildDetailed(info, ceilingOptions, null);

        var fillEstimate = PlanCalculator.Estimate(fillResult.Plan, info, fillResult.Profile).ExpectedMb;
        var ceilingEstimate = PlanCalculator.Estimate(ceilingResult.Plan, info, ceilingResult.Profile).ExpectedMb;

        Assert.True(fillEstimate >= ceilingEstimate - 0.01,
            $"Fill target ({fillEstimate:0.0} MB) should never land below quality ceiling ({ceilingEstimate:0.0} MB) for the same source and target.");
    }

    [Fact]
    public void FillTargetReachesTheBandWhenTheCeilingWouldLeaveItUnfilled()
    {
        var info = SampleInfo();
        var ceilingOptions = new PlanOptions { TargetMb = 120, Intent = Intent.Sharing, FillPolicy = FillPolicy.QualityCeiling };
        var ceilingResult = PlanCalculator.BuildDetailed(info, ceilingOptions, null);
        var ceilingEstimate = PlanCalculator.Estimate(ceilingResult.Plan, info, ceilingResult.Profile).ExpectedMb;
        var band = FillBand.For(120);

        Assert.True(ceilingEstimate < band.LowerMb,
            $"This fixture is expected to hit the transparency ceiling below the band ({ceilingEstimate:0.0} MB < {band.LowerMb:0.0} MB); adjust the fixture if the model changes.");

        var fillOptions = new PlanOptions { TargetMb = 120, Intent = Intent.Sharing, FillPolicy = FillPolicy.FillTarget };
        var fillResult = PlanCalculator.BuildDetailed(info, fillOptions, null);
        var fillEstimate = PlanCalculator.Estimate(fillResult.Plan, info, fillResult.Profile).ExpectedMb;

        Assert.True(fillEstimate >= band.LowerMb - 1.5,
            $"Expected the fill policy to reach the {band.LowerMb:0.0}-{band.UpperMb:0.0} MB band, got {fillEstimate:0.0} MB.");
        Assert.Contains("fill target policy", fillResult.Plan.Reason);
        Assert.Contains(fillResult.Plan.ReasonCodes, n => n.Code == ReasonCode.FillCrfLowered);
    }

    [Fact]
    public void FillTargetFallsBackToTwoPassWhenTheCrfFloorCannotReachTheBand()
    {
        var info = SampleInfo();
        var options = new PlanOptions
        {
            TargetMb = 5000,
            Intent = Intent.Sharing,
            Codec = CodecPreference.Compatible,
            FillPolicy = FillPolicy.FillTarget
        };

        var result = PlanCalculator.BuildDetailed(info, options, null);

        Assert.Equal(EncodeMode.TwoPass, result.Plan.ModeEnum);
        Assert.Contains("CRF floor", result.Plan.Reason);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.FillTwoPassBandCenter);
    }

    [Fact]
    public void CorrectFillsUnderBandTowardTheBandCenter()
    {
        var plan = new EncodePlan
        {
            Mode = "2pass",
            VideoBitrateK = 400,
            AudioBitrateK = 128,
            AudioCodec = "aac",
            Width = 1920,
            Height = 1080,
            Fps = 30,
            Codec = "libx264",
            Preset = "slow"
        };

        var corrected = PlanCalculator.Correct(plan, actualMb: 10, targetMb: 25, durationSeconds: 120, fillUnderBand: true);

        var band = FillBand.For(25);
        var bandCenterMb = (band.LowerMb + band.UpperMb) / 2.0;

        Assert.Equal(EncodeMode.TwoPass, corrected.ModeEnum);
        Assert.True(corrected.VideoBitrateK > plan.VideoBitrateK,
            "Expected bitrate to increase when the previous attempt landed under the hard floor.");
        Assert.True(PlanCalculator.EstimatedMb(corrected, 120) <= bandCenterMb + 1.0);
    }

    [Fact]
    public void CorrectOverTargetBehaviorIsUnchanged()
    {
        var plan = new EncodePlan
        {
            Mode = "crf",
            Crf = 23,
            VideoBitrateK = 1800,
            AudioBitrateK = 128,
            AudioCodec = "aac",
            Width = 1920,
            Height = 1080,
            Fps = 30,
            Codec = "libx264",
            Preset = "slow"
        };

        var corrected = PlanCalculator.Correct(plan, actualMb: 30, targetMb: 20, durationSeconds: 120);

        Assert.Equal(EncodeMode.TwoPass, corrected.ModeEnum);
        Assert.Null(corrected.Crf);
        Assert.True(corrected.VideoBitrateK < 1128, $"Expected audio-aware correction below the old whole-file proportional result, got {corrected.VideoBitrateK}k.");
        Assert.True(PlanCalculator.EstimatedMb(corrected, 120) <= 20 * 0.94 + 0.05);
    }

    private static ComplexityProfile CalibratedProfile(string codec, double scale, double fps, double sourceFps)
    {
        var signature = new CalibrationSignature
        {
            Codec = codec,
            Width = 1920,
            Height = (int)Math.Round(1080 * scale),
            Fps = fps,
            Scale = scale
        };
        return ComplexityProfile
            .FromProbe(0.06, 0.03, sampledSeconds: 6, sampledFrames: 180, windowBias: 1.0)
            .Calibrate(signature, lowCrf: 18, lowBppf: 0.10, highCrf: 28, highBppf: 0.03, sourceFps: sourceFps);
    }

    private static EncodePlan BigPlan() => new()
    {
        Mode = "2pass",
        VideoBitrateK = 30000,
        AudioBitrateK = 128,
        AudioCodec = "aac",
        Width = 1920,
        Height = 1080,
        Fps = 48,
        Codec = "libx264",
        Preset = "slow"
    };

    [Theory]
    [InlineData(180.0)]
    [InlineData(25.0)]
    [InlineData(8.0)]
    public void CalibratedRetryAimKeepsItsWholeSpreadUnderTheCeiling(double targetMb)
    {
        var plan = BigPlan();
        var profile = CalibratedProfile("libx264", 1.0, 48, 48);
        Assert.True(profile.Calibrated);

        var aim = PlanCalculator.RetryAimMb(targetMb, profile, plan, sourceHeight: 1080, out var calibrated);
        Assert.True(calibrated, "The retry aim must use the calibrated profile when it applies to this codec, scale and fps.");

        var band = FillBand.For(targetMb);
        var spread = PlanCalculator.CalibratedRetrySpread;

        Assert.True(aim * (1 + spread) <= targetMb + 1e-9,
            $"The upper end of the retry spread must stay at or under the {targetMb:0.##} MB ceiling, got {aim * (1 + spread):0.00} MB.");
        Assert.True(aim > band.HardFloorMb,
            $"The retry aim must stay above the {band.HardFloorMb:0.0} MB hard floor, got {aim:0.0} MB.");
        Assert.True(aim >= band.LowerMb,
            $"The retry aim itself must sit inside the {band.LowerMb:0.0}-{band.UpperMb:0.0} MB band, got {aim:0.0} MB.");
    }

    [Fact]
    public void UncalibratedRetryAimFallsBackToTheFixedMarginAndIsConsistentWithIt()
    {
        var plan = BigPlan();

        var aim = PlanCalculator.RetryAimMb(180, null, plan, sourceHeight: 1080, out var calibrated);

        Assert.False(calibrated);
        Assert.Equal(180 * 0.94, aim, 3);

        var impliedError = 180 / aim - 1;
        Assert.True(impliedError >= PlanCalculator.CalibratedRetrySpread,
            $"The uncalibrated fallback margin must be at least as conservative as the calibrated retry spread; implied {impliedError * 100:0.#}% vs {PlanCalculator.CalibratedRetrySpread * 100:0.#}%.");
    }

    [Fact]
    public void CalibratedOverTargetCorrectionNoLongerAimsBelowTheHardFloor()
    {
        var plan = BigPlan();
        var profile = CalibratedProfile("libx264", 1.0, 48, 48);

        var corrected = PlanCalculator.Correct(plan, actualMb: 190, targetMb: 180, durationSeconds: 120, profile: profile, sourceHeight: 1080);
        var estimated = PlanCalculator.EstimatedMb(corrected, 120)!.Value;
        var band = FillBand.For(180);

        Assert.True(estimated > band.HardFloorMb,
            $"An over-ceiling correction must not aim under the {band.HardFloorMb:0.0} MB hard floor, got {estimated:0.0} MB.");
        Assert.True(estimated >= band.LowerMb - 0.5,
            $"An over-ceiling correction must aim at the {band.LowerMb:0.0}-{band.UpperMb:0.0} MB band, got {estimated:0.0} MB.");
        Assert.True(estimated * (1 + PlanCalculator.CalibratedRetrySpread) <= 180 + 1e-9,
            $"The corrected plan plus its own spread must stay under the ceiling, got {estimated * (1 + PlanCalculator.CalibratedRetrySpread):0.0} MB.");
    }

    [Fact]
    public void CorrectFillsUnderBandLeavesPayBelowTheHardCeiling()
    {
        var plan = BigPlan();
        var profile = CalibratedProfile("libx264", 1.0, 48, 48);

        var corrected = PlanCalculator.Correct(plan, actualMb: 170, targetMb: 180, durationSeconds: 120, fillUnderBand: true, profile: profile, sourceHeight: 1080);
        var estimated = PlanCalculator.EstimatedMb(corrected, 120)!.Value;
        var band = FillBand.For(180);

        Assert.True(estimated * (1 + PlanCalculator.CalibratedRetrySpread) <= 180 + 1e-9,
            $"Retry plus its own spread must never reach the hard ceiling, got {estimated * (1 + PlanCalculator.CalibratedRetrySpread):0.0} MB.");
        Assert.True(estimated >= band.LowerMb,
            $"Expected the capped retry to aim inside the {band.LowerMb:0.0}-{band.UpperMb:0.0} MB band, got {estimated:0.0} MB.");
    }

    [Fact]
    public async Task EncodeRunnerRetriesWhenUnderTheHardFloorInFillTargetMode()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_fillband_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var source = Path.Combine(dir, "source.mp4");
        var outputPath = Path.Combine(dir, "result.mp4");
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ToolLocator.Ffmpeg,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in new[]
            {
                "-y", "-f", "lavfi", "-i", "testsrc=size=320x240:rate=10:duration=2",
                "-c:v", "libx264", "-crf", "40", "-pix_fmt", "yuv420p", source
            }) psi.ArgumentList.Add(arg);
            using var make = new System.Diagnostics.Process { StartInfo = psi };
            make.Start();
            var drain = Task.WhenAll(make.StandardOutput.ReadToEndAsync(), make.StandardError.ReadToEndAsync());
            await make.WaitForExitAsync();
            await drain;
            Assert.True(File.Exists(source));

            var info = new MediaInfo
            {
                FilePath = source,
                FileSizeBytes = new FileInfo(source).Length,
                DurationSeconds = 2,
                Width = 320,
                Height = 240,
                Fps = 10,
                VideoCodec = "h264",
                TotalBitrateBps = 400_000
            };
            var plan = new EncodePlan
            {
                Codec = "libx264",
                Mode = "crf",
                Crf = 40,
                VideoBitrateK = 20,
                AudioCodec = "aac",
                AudioBitrateK = 64,
                Width = 320,
                Height = 240,
                Fps = 10,
                Preset = "ultrafast"
            };

            var result = await new EncodeRunner().RunAsync(info, plan, outputPath, targetMb: 5, progress: null, ct: CancellationToken.None, fillPolicy: FillPolicy.FillTarget);

            Assert.True(File.Exists(outputPath));
            Assert.True(result.Attempts > 1,
                $"Expected the under-floor result to trigger a retry, got {result.Attempts} attempt(s) at {result.OutputMb:0.0} MB.");
            Assert.True(result.Attempts <= 2,
                $"Expected the under-band correction to be used at most once, leaving the rest of MaxAttempts for the ceiling side; got {result.Attempts} attempt(s).");
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public async Task EncodeRunnerWritesNoFileWhenStillOverTheCeilingAfterMaxAttempts()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_fillband_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var source = Path.Combine(dir, "source.mp4");
        var outputPath = Path.Combine(dir, "result.mp4");
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ToolLocator.Ffmpeg,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in new[]
            {
                "-y", "-f", "lavfi", "-i", "testsrc=size=320x240:rate=10:duration=2",
                "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", source
            }) psi.ArgumentList.Add(arg);
            using var make = new System.Diagnostics.Process { StartInfo = psi };
            make.Start();
            var drain = Task.WhenAll(make.StandardOutput.ReadToEndAsync(), make.StandardError.ReadToEndAsync());
            await make.WaitForExitAsync();
            await drain;
            Assert.True(File.Exists(source));

            var info = new MediaInfo
            {
                FilePath = source,
                FileSizeBytes = new FileInfo(source).Length,
                DurationSeconds = 2,
                Width = 320,
                Height = 240,
                Fps = 10,
                VideoCodec = "h264",
                TotalBitrateBps = 400_000
            };
            var plan = new EncodePlan
            {
                Codec = "libx264",
                Mode = "crf",
                Crf = 18,
                VideoBitrateK = 2000,
                AudioCodec = null,
                AudioBitrateK = 0,
                Width = 320,
                Height = 240,
                Fps = 10,
                Preset = "ultrafast"
            };

            var result = await new EncodeRunner().RunAsync(info, plan, outputPath, targetMb: 0.001, progress: null, ct: CancellationToken.None, fillPolicy: FillPolicy.QualityCeiling);

            Assert.False(result.Success);
            Assert.True(result.CeilingExceeded);
            Assert.False(File.Exists(outputPath), "A file larger than the target must never be handed back.");
            Assert.Equal(3, result.Attempts);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }
}
