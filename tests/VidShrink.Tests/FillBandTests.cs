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
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }
}
