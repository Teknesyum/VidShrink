using Xunit.Abstractions;
using Xunit.Sdk;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class LiveSourceTheoryAttribute : TheoryAttribute
{
    public LiveSourceTheoryAttribute()
    {
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE");
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            Skip = "VIDSHRINK_LIVE_SOURCE does not point at an existing file, so the live fill band measurement was not run.";
        else if (!ToolLocator.IsAvailable(out _))
            Skip = "ffmpeg was not found, so the live fill band measurement was not run.";
    }
}

public sealed class FillBandTests
{
    private readonly ITestOutputHelper _output;

    public FillBandTests(ITestOutputHelper output) => _output = output;

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
        Assert.Equal(EncodeMode.TwoPass, fillResult.Plan.ModeEnum);
        Assert.Contains(fillResult.Plan.ReasonCodes, n => n.Code == ReasonCode.FillTwoPassBandTooNarrowForCrf);
    }

    [Fact]
    public void FillTargetFallsBackToTwoPassWhenTheCrfFloorCannotReachTheBand()
    {
        var info = SampleInfo() with { FileSizeBytes = 12_000L * 1024 * 1024 };
        var options = new PlanOptions
        {
            TargetMb = 5000,
            Intent = Intent.Sharing,
            Codec = CodecPreference.Compatible,
            FillPolicy = FillPolicy.FillTarget
        };

        var result = PlanCalculator.BuildDetailed(info, options, null);

        Assert.Equal(EncodeMode.TwoPass, result.Plan.ModeEnum);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code is ReasonCode.FillTwoPassBandCenter or ReasonCode.FillTwoPassBandTooNarrowForCrf);
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
        Assert.True(corrected.VideoBitrateK < 1200, $"Expected the correction to reserve audio before scaling video, staying below the audio-blind whole-file proportional result of 1200k, got {corrected.VideoBitrateK}k.");
        Assert.True(PlanCalculator.EstimatedMb(corrected, 120) <= 20 * 0.94 + 0.05);
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

    private const double MeasuredLibx264Yield = 0.9815;

    [Theory]
    [InlineData(180.0)]
    [InlineData(25.0)]
    [InlineData(8.0)]
    public void RetryAimTargetsTheBandCenterWhenTheYieldIsMeasured(double targetMb)
    {
        var band = FillBand.For(targetMb);
        var bandCenterMb = (band.LowerMb + band.UpperMb) / 2.0;

        var aim = PlanCalculator.RetryAimMb(targetMb, MeasuredLibx264Yield);

        Assert.Equal(bandCenterMb, aim, 6);
        Assert.True(aim >= band.LowerMb && aim <= band.UpperMb,
            $"A measured yield must let the retry aim at the {band.LowerMb:0.0}-{band.UpperMb:0.0} MB band, got {aim:0.0} MB.");
    }

    [Theory]
    [InlineData(180.0)]
    [InlineData(25.0)]
    [InlineData(8.0)]
    public void UncalibratedRetryAimStaysAboveTheHardFloorAndUnderTheCeiling(double targetMb)
    {
        var aim = PlanCalculator.RetryAimMb(targetMb, null);

        var band = FillBand.For(targetMb);
        var impliedError = targetMb / aim - 1;

        Assert.True(aim > band.HardFloorMb,
            $"The unmeasured retry aim must stay above the {band.HardFloorMb:0.0} MB hard floor, got {aim:0.0} MB.");
        Assert.True(aim <= targetMb + 1e-9,
            $"The unmeasured retry aim must stay at or under the {targetMb:0.##} MB ceiling, got {aim:0.0} MB.");
        Assert.True(impliedError >= PlanCalculator.TwoPassUncertainty - 1e-9,
            $"Without a measured yield the aim must carry the whole two-pass spread; implied {impliedError * 100:0.#}% vs {PlanCalculator.TwoPassUncertainty * 100:0.#}%.");
    }

    [Fact]
    public void MeasuredEncoderEfficiencyIsNullForACrfAttempt()
    {
        var plan = BigPlan();
        plan.Mode = "crf";
        plan.Crf = 22;

        Assert.Null(PlanCalculator.MeasuredEncoderEfficiency(plan, actualMb: 170, durationSeconds: 120));
    }

    [Fact]
    public void MeasuredEncoderEfficiencyIsTheDeliveredShareOfTheRequest()
    {
        var plan = BigPlan();
        var requested = PlanCalculator.EstimatedMb(plan, 120)!.Value;
        var audioMb = requested - PlanCalculator.EstimatedMb(new EncodePlan
        {
            Mode = "2pass",
            VideoBitrateK = plan.VideoBitrateK,
            AudioBitrateK = 0,
            Width = plan.Width,
            Height = plan.Height,
            Fps = plan.Fps,
            Codec = plan.Codec,
            Preset = plan.Preset
        }, 120)!.Value;
        var delivered = audioMb + (requested - audioMb) * MeasuredLibx264Yield;

        var efficiency = PlanCalculator.MeasuredEncoderEfficiency(plan, delivered, 120);

        Assert.NotNull(efficiency);
        Assert.Equal(MeasuredLibx264Yield, efficiency!.Value, 4);
    }

    [Theory]
    [InlineData(180.0)]
    [InlineData(25.0)]
    [InlineData(8.0)]
    public void UnderBandRetryDividesTheRequestByTheMeasuredYieldAndLandsInTheBand(double targetMb)
    {
        var plan = BigPlan();
        var band = FillBand.For(targetMb);
        var requested = PlanCalculator.EstimatedMb(plan, 120)!.Value;
        var delivered = requested * MeasuredLibx264Yield;

        var corrected = PlanCalculator.Correct(plan, delivered, targetMb, 120, fillUnderBand: true);
        var nextRequest = PlanCalculator.EstimatedMb(corrected, 120)!.Value;
        var nextDelivered = nextRequest * MeasuredLibx264Yield;

        Assert.True(nextDelivered >= band.LowerMb && nextDelivered <= band.UpperMb,
            $"With the yield measured at {MeasuredLibx264Yield:0.####} the retry must deliver inside the {band.LowerMb:0.0}-{band.UpperMb:0.0} MB band, got {nextDelivered:0.0} MB from a {nextRequest:0.0} MB request.");
    }

    [Fact]
    public void UnderBandRetryMayRequestAboveTheTargetWhenTheYieldIsMeasured()
    {
        var plan = BigPlan();
        var requested = PlanCalculator.EstimatedMb(plan, 120)!.Value;
        var delivered = requested * MeasuredLibx264Yield;

        var corrected = PlanCalculator.Correct(plan, delivered, 180, 120, fillUnderBand: true);
        var nextRequest = PlanCalculator.EstimatedMb(corrected, 120)!.Value;

        Assert.True(nextRequest > FillBand.For(180).LowerMb,
            $"The nominal request must not be clamped back onto the band edge, got {nextRequest:0.0} MB.");
        Assert.Contains("encoder yield", corrected.Reason);
    }

    [Fact]
    public void UncalibratedOverTargetCorrectionNoLongerAimsBelowTheHardFloor()
    {
        var plan = BigPlan();
        plan.Mode = "crf";
        plan.Crf = 20;

        var corrected = PlanCalculator.Correct(plan, actualMb: 190, targetMb: 180, durationSeconds: 120);
        var estimated = PlanCalculator.EstimatedMb(corrected, 120)!.Value;
        var band = FillBand.For(180);

        Assert.True(estimated > band.HardFloorMb,
            $"An unmeasured over-ceiling correction must not aim under the {band.HardFloorMb:0.0} MB hard floor, got {estimated:0.0} MB.");
        Assert.True(estimated <= 180, $"It must still stay under the ceiling, got {estimated:0.0} MB.");
    }

    [Fact]
    public void OverCeilingCorrectionWithAMeasuredYieldStaysUnderTheCeiling()
    {
        var plan = BigPlan();
        var requested = PlanCalculator.EstimatedMb(plan, 120)!.Value;
        var overshootYield = 1.04;
        var delivered = requested * overshootYield;

        var corrected = PlanCalculator.Correct(plan, delivered, 180, 120);
        var nextDelivered = PlanCalculator.EstimatedMb(corrected, 120)!.Value * overshootYield;
        var band = FillBand.For(180);

        Assert.True(nextDelivered <= 180 + 1e-9,
            $"An over-ceiling correction that knows the yield must land under the ceiling, got {nextDelivered:0.0} MB.");
        Assert.True(nextDelivered >= band.LowerMb,
            $"It must also land inside the {band.LowerMb:0.0}-{band.UpperMb:0.0} MB band, got {nextDelivered:0.0} MB.");
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
            var underBandRetries = (result.Trace ?? Array.Empty<EncodeAttempt>()).Where(a => a.Branch == "under band").ToList();
            Assert.True(underBandRetries.Count <= 2,
                $"The under-band branch must not spend more than two attempts, got {underBandRetries.Count}.");
            if (underBandRetries.Count == 2)
                Assert.NotNull(underBandRetries[1].MeasuredEfficiency);
            Assert.DoesNotContain(result.Trace ?? Array.Empty<EncodeAttempt>(), a => a.Branch == "over ceiling");
            Assert.True(result.OutputMb <= 5, $"The hard ceiling must hold, got {result.OutputMb:0.00} MB.");
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

    [LiveSourceTheory]
    [InlineData(180.0)]
    [InlineData(100.0)]
    [InlineData(25.0)]
    [InlineData(8.0)]
    public async Task LiveFillTargetRunStaysInsideTheBand(double targetMb)
    {
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE")!;

        var outDir = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_OUT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "vidshrink_live");
        Directory.CreateDirectory(outDir);
        var outputPath = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(source)}_fill_{targetMb:0.#}mb.mp4");

        var info = await FfprobeClient.ProbeAsync(source);
        var options = new PlanOptions { TargetMb = targetMb, FillPolicy = FillPolicy.FillTarget };

        var profile = await ComplexityProbe.RunAsync(info, options.SpeedMode, CancellationToken.None);
        var draft = PlanCalculator.BuildDetailed(info, options, profile, EncoderCapabilities.Instance).Plan;
        profile = await CalibrationProbe.RunAsync(info, draft, profile, options.SpeedMode, CancellationToken.None);

        var plan = PlanCalculator.BuildDetailed(info, options, profile, EncoderCapabilities.Instance).Plan;
        var result = await new EncodeRunner().RunAsync(info, plan, outputPath, targetMb, null, CancellationToken.None, FillPolicy.FillTarget, profile);

        var band = FillBand.For(targetMb);
        _output.WriteLine($"target {targetMb:0.##} MB | band {band.LowerMb:0.00}-{band.UpperMb:0.00} | hard floor {band.HardFloorMb:0.00} | calibrated {profile.Calibrated}");
        foreach (var attempt in result.Trace ?? Array.Empty<EncodeAttempt>())
            _output.WriteLine($"  attempt {attempt.Number}: branch={attempt.Branch} aim={attempt.AimMb:0.00} MB actual={attempt.ActualMb:0.00} MB bitrate={attempt.VideoBitrateK}k mode={attempt.Mode}");
        _output.WriteLine($"  result: success={result.Success} size={result.OutputMb:0.00} MB attempts={result.Attempts} underBand={result.UnderBand} ceilingExceeded={result.CeilingExceeded}");

        _output.WriteLine($"  inside the band: {result.OutputMb >= band.LowerMb}");

        Assert.True(result.Success, result.Error);
        Assert.True(result.OutputMb <= targetMb, $"The hard ceiling was crossed: {result.OutputMb:0.00} MB against a {targetMb:0.##} MB target.");
        Assert.True(result.OutputMb >= band.HardFloorMb, $"The result fell below the {band.HardFloorMb:0.00} MB hard floor at {result.OutputMb:0.00} MB.");
        Assert.True(result.OutputMb >= band.LowerMb,
            $"The result missed the {band.LowerMb:0.00}-{band.UpperMb:0.00} MB fill band at {result.OutputMb:0.00} MB.");
    }
}
