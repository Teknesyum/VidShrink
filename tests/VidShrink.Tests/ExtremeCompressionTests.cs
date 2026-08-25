using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class ExtremeCompressionTests
{
    private readonly ITestOutputHelper _output;

    public ExtremeCompressionTests(ITestOutputHelper output) => _output = output;

    private const double SourceFps = 48.0;
    private const double SourceSeconds = 52.6;

    private static MediaInfo GameCapture() => new()
    {
        FilePath = "gothic.mp4",
        FileSizeBytes = 830L * 1024 * 1024,
        DurationSeconds = SourceSeconds,
        Width = 1920,
        Height = 1080,
        Fps = SourceFps,
        VideoCodec = "h264",
        TotalBitrateBps = 132_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 160_000,
        AudioChannels = 2
    };

    private static ComplexityProfile Measured(double motionExponent) => new()
    {
        ReferenceBppf = 0.1264,
        Measured = true,
        MotionExponent = motionExponent,
        MotionMeasured = true,
        DetailExponent = 0.55,
        SampledSeconds = 6,
        SampledFrames = 288
    };

    private static double Bppf(EncodePlan plan)
        => plan.VideoBitrateK * 1000.0 / ((double)plan.Width * plan.Height * plan.Fps);

    [Fact]
    public void OneMegabyteTargetDropsTheFrameRateAndKeepsTheFramesAboveTheFloor()
    {
        var info = GameCapture();
        var result = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 1.0 }, null);
        var plan = result.Plan;

        _output.WriteLine($"1 MB -> {plan.Codec} {plan.Width}x{plan.Height}@{plan.Fps:0.##} {plan.VideoBitrateK}k bppf {Bppf(plan):0.0000}");

        Assert.Equal(CompressionRegime.Extreme, result.Advice.Regime);
        Assert.True(plan.Fps < 15.0, $"The frame rate stayed at {plan.Fps:0.##} at a 1 MB target.");
        Assert.True(Bppf(plan) >= result.Profile.FloorBppf(plan.Codec, plan.Fps, info.Fps),
            $"The plan lands at {Bppf(plan):0.0000} bits per pixel per frame, under the {result.Profile.FloorBppf(plan.Codec, plan.Fps, info.Fps):0.0000} floor.");
    }

    [Fact]
    public void GenerousTargetOnTheSameSourceKeepsResolutionAndFrameRate()
    {
        var info = GameCapture();
        var result = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 180.0 }, Measured(0.25));

        _output.WriteLine($"180 MB -> {result.Plan.Width}x{result.Plan.Height}@{result.Plan.Fps:0.##} {result.Advice.Regime}");

        Assert.Equal(CompressionRegime.Balanced, result.Advice.Regime);
        Assert.Equal(1920, result.Plan.Width);
        Assert.Equal(1080, result.Plan.Height);
        Assert.Equal(SourceFps, result.Plan.Fps, 2);
    }

    [Fact]
    public void HighMotionCutsResolutionWhileLowMotionCutsFrames()
    {
        var info = GameCapture();
        var options = new PlanOptions { TargetMb = 3.0 };

        var lowMotion = PlanCalculator.BuildDetailed(info, options, Measured(0.10));
        var highMotion = PlanCalculator.BuildDetailed(info, options, Measured(0.85));

        _output.WriteLine($"low motion  -> {lowMotion.Plan.Width}x{lowMotion.Plan.Height}@{lowMotion.Plan.Fps:0.##}");
        _output.WriteLine($"high motion -> {highMotion.Plan.Width}x{highMotion.Plan.Height}@{highMotion.Plan.Fps:0.##}");

        Assert.True(highMotion.Plan.Fps > lowMotion.Plan.Fps,
            $"High motion kept {highMotion.Plan.Fps:0.##} fps, low motion {lowMotion.Plan.Fps:0.##} fps.");
        Assert.True(highMotion.Plan.Height < lowMotion.Plan.Height,
            $"High motion kept {highMotion.Plan.Height}p, low motion {lowMotion.Plan.Height}p.");
        Assert.Contains(AdviceCode.MotionCutIsExpensive, highMotion.Advice.Notes);
        Assert.Contains(AdviceCode.MotionCutIsCheap, lowMotion.Advice.Notes);
    }

    [Fact]
    public void ATargetNoLayoutCanCarryIsReportedInsteadOfPretended()
    {
        var info = GameCapture();
        var options = new PlanOptions { TargetMb = 2.0, AllowResolutionDrop = false, AllowFpsDrop = false };
        var result = PlanCalculator.BuildDetailed(info, options, Measured(0.25));

        Assert.Contains(AdviceCode.TargetBelowCodecFloor, result.Advice.Notes);
        Assert.Contains("too small", result.Plan.Reason);
    }

    [Fact]
    public void MotionExponentComesFromTheHalfFrameRateSample()
    {
        var withoutMotion = ComplexityProfile.FromProbe(0.1264, 0.09, 6, 288);
        var withMotion = ComplexityProfile.FromProbe(0.1264, 0.09, 6, 288, 0, WindowBiasSource.Scan, 0.1264 * 1.76);

        Assert.False(withoutMotion.MotionMeasured);
        Assert.Equal(0.25, withoutMotion.MotionExponent, 3);
        Assert.True(withMotion.MotionMeasured);
        Assert.Equal(0.8155, withMotion.MotionExponent, 3);
        Assert.Equal(1.76, withMotion.TemporalFactor(24, 48), 4);
    }

    [Fact]
    public void MeasuredMotionReplacesTheModelConstantAndTheConstantIsTheFallback()
    {
        var fallback = ComplexityProfile.FromProbe(0.148, 0.09, 6, 288);
        var legacy = Math.Pow(0.5, CodecModel.FpsBitrateExponent - 1.0);

        Assert.Equal(legacy, fallback.TemporalFactor(24, 48), 6);
        Assert.NotEqual(legacy, Measured(0.85).TemporalFactor(24, 48), 3);
    }

    [Fact]
    public void CodecFloorsAreStatedPerCodecAndRaisedForHardware()
    {
        Assert.Equal(0.035, CodecModel.FloorBppf("libx264"), 6);
        Assert.Equal(0.025, CodecModel.FloorBppf("libx265"), 6);
        Assert.Equal(0.020, CodecModel.FloorBppf("libsvtav1"), 6);
        Assert.Equal(0.035 * 1.25, CodecModel.FloorBppf("h264_nvenc"), 6);
        Assert.Equal(0.025 * 1.25, CodecModel.FloorBppf("hevc_nvenc"), 6);
        Assert.Equal(0.020 * 1.25, CodecModel.FloorBppf("av1_nvenc"), 6);
    }

    [Fact]
    public void TheFloorFollowsTheContentAndTheFrameRate()
    {
        var simple = Measured(0.25) with { ReferenceBppf = 0.037 };
        var complex = Measured(0.25) with { ReferenceBppf = 0.592 };
        var unmeasured = ComplexityProfile.FromSourceBitrate(GameCapture());

        Assert.True(simple.FloorBppf("libx264", 48, 48) < 0.035, "Simple content should get by under the plain codec floor.");
        Assert.True(complex.FloorBppf("libx264", 48, 48) > 0.035, "Complex content should need more than the plain codec floor.");
        Assert.Equal(0.035, unmeasured.FloorBppf("libx264", 48, 48), 6);
        Assert.True(simple.FloorBppf("libx264", 12, 48) > simple.FloorBppf("libx264", 48, 48),
            "Frames that stand further apart carry more new detail, so the floor has to rise.");
    }

    [Fact]
    public void PenaltyWeightsFallWithTheRegime()
    {
        var light = CompressionStrategy.PenaltyWeights(CompressionRegime.Light);
        var balanced = CompressionStrategy.PenaltyWeights(CompressionRegime.Balanced);
        var aggressive = CompressionStrategy.PenaltyWeights(CompressionRegime.Aggressive);
        var extreme = CompressionStrategy.PenaltyWeights(CompressionRegime.Extreme);

        Assert.Equal(new PenaltyWeights(1.0, 1.0, true), light);
        Assert.Equal(new PenaltyWeights(1.0, 1.0, true), balanced);
        Assert.Equal(new PenaltyWeights(0.70, 0.70, true), aggressive);
        Assert.Equal(new PenaltyWeights(0.45, 0.35, false), extreme);
    }

    [Fact]
    public void RegimeFloorsOpenUpAsTheRatioGrows()
    {
        Assert.Equal(new RegimeFloors(0.25, 240, 12.0), CompressionStrategy.FloorsFor(CompressionRegime.Light));
        Assert.Equal(new RegimeFloors(0.25, 240, 12.0), CompressionStrategy.FloorsFor(CompressionRegime.Balanced));
        Assert.Equal(new RegimeFloors(0.20, 180, 10.0), CompressionStrategy.FloorsFor(CompressionRegime.Aggressive));
        Assert.Equal(new RegimeFloors(0.12, 120, 6.0), CompressionStrategy.FloorsFor(CompressionRegime.Extreme));
    }

    [Fact]
    public void FrameRateCandidatesCoverTheDividersAndTheFixedSteps()
    {
        var info = GameCapture();
        var options = new PlanOptions { AllowFpsDrop = true };
        var extreme = PlanCalculator.FpsCandidates(info, options, CompressionRegime.Extreme).ToList();

        foreach (var expected in new[] { 48.0, 32.0, 24.0, 19.2, 16.0, 12.0, 9.6, 8.0, 30.0, 25.0, 20.0, 15.0, 10.0, 6.0 })
            Assert.Contains(extreme, candidate => Math.Abs(candidate - expected) < 0.001);

        Assert.Equal(extreme.Count, extreme.Distinct().Count());
        Assert.All(extreme, candidate => Assert.True(candidate >= 6.0));
        Assert.All(PlanCalculator.FpsCandidates(info, options, CompressionRegime.Aggressive), candidate => Assert.True(candidate >= 10.0));
        Assert.All(PlanCalculator.FpsCandidates(info, options, CompressionRegime.Balanced), candidate => Assert.True(candidate >= 12.0));
        Assert.Single(PlanCalculator.FpsCandidates(info, new PlanOptions { AllowFpsDrop = false }, CompressionRegime.Extreme));
    }

    [Fact]
    public void ScaleCandidatesReachFurtherDownAsTheRegimeHardens()
    {
        var options = new PlanOptions { AllowResolutionDrop = true };

        foreach (var (regime, floor) in new[]
                 {
                     (CompressionRegime.Balanced, 0.25),
                     (CompressionRegime.Aggressive, 0.20),
                     (CompressionRegime.Extreme, 0.12)
                 })
        {
            var smallest = PlanCalculator.ScaleCandidates(options, regime).Min();
            Assert.True(smallest >= floor - 1e-9, $"{regime} reached {smallest:0.###}, under its {floor:0.##} floor.");
            Assert.True(smallest < floor + 0.02, $"{regime} stopped at {smallest:0.###}, more than one step above its {floor:0.##} floor.");
        }

        Assert.Single(PlanCalculator.ScaleCandidates(new PlanOptions { AllowResolutionDrop = false }, CompressionRegime.Extreme));
    }

    [LiveSourceFact]
    public async Task LiveProbeMeasuresMotionWithinItsTimeBudget()
    {
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE")!;
        var info = await FfprobeClient.ProbeAsync(source);

        var clock = Stopwatch.StartNew();
        var profile = await ComplexityProbe.RunAsync(info, SpeedMode.Quality, CancellationToken.None);
        clock.Stop();

        _output.WriteLine($"source {info.Width}x{info.Height}@{info.Fps:0.##} {info.FileSizeMb:0.0} MB {info.DurationSeconds:0.0} s");
        _output.WriteLine($"probe {clock.Elapsed.TotalSeconds:0.00} s | reference bppf {profile.ReferenceBppf:0.0000} | detail {profile.DetailExponent:0.000} | motion {profile.MotionExponent:0.000} (measured {profile.MotionMeasured}) | bias {profile.WindowBias:0.000} {profile.BiasSource}");
        _output.WriteLine($"floor adaptation {profile.FloorAdaptation:0.000} | libx264 floor at source fps {profile.FloorBppf("libx264", info.Fps, info.Fps):0.0000}");
        _output.WriteLine($"halving the frame rate saves {(1 - Math.Pow(2, profile.MotionExponent - 1)) * 100:0.#}% of the bits");

        Assert.True(profile.Measured);
        Assert.True(profile.MotionMeasured, "The half frame rate sample did not come back.");
    }

    [LiveSourceTheory]
    [InlineData(1.0)]
    [InlineData(3.0)]
    [InlineData(8.0)]
    [InlineData(25.0)]
    [InlineData(180.0)]
    public async Task LiveExtremeTargetsProduceAPlayablePicture(double targetMb)
    {
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE")!;
        var outDir = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_OUT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "vidshrink_extreme");
        Directory.CreateDirectory(outDir);
        var outputPath = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(source)}_{targetMb:0.#}mb.mp4");

        var info = await FfprobeClient.ProbeAsync(source);
        var options = new PlanOptions { TargetMb = targetMb, FillPolicy = FillPolicy.FillTarget };

        var profile = await ComplexityProbe.RunAsync(info, options.SpeedMode, CancellationToken.None);
        var draft = PlanCalculator.BuildDetailed(info, options, profile, EncoderCapabilities.Instance).Plan;
        profile = await CalibrationProbe.RunAsync(info, draft, profile, options.SpeedMode, CancellationToken.None);

        var built = PlanCalculator.BuildDetailed(info, options, profile, EncoderCapabilities.Instance);
        var plan = built.Plan;
        var result = await new EncodeRunner().RunAsync(info, plan, outputPath, targetMb, null, CancellationToken.None, FillPolicy.FillTarget, profile);

        _output.WriteLine($"| {targetMb:0.#} MB | {plan.Width}x{plan.Height} | {plan.Fps:0.##} fps | {plan.Codec} | {plan.Mode} | {result.OutputMb:0.00} MB | bppf {Bppf(plan):0.0000} | floor {profile.FloorBppf(plan.Codec, plan.Fps, info.Fps):0.0000} | {built.Advice.Regime} |");
        foreach (var note in built.Advice.Notes) _output.WriteLine($"    note {note}");

        Assert.True(result.Success, result.Error);
        Assert.True(result.OutputMb <= targetMb, $"The hard ceiling was crossed: {result.OutputMb:0.00} MB against a {targetMb:0.##} MB target.");
    }

    [LiveSourceFact]
    public async Task LiveQualityCurveShowsWhereTheCodecStopsCarryingThePicture()
    {
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE")!;
        var outDir = Path.Combine(Path.GetTempPath(), "vidshrink_floor_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);

        const int width = 640;
        const int height = 360;
        const double fps = 24.0;
        var referencePath = Path.Combine(outDir, "reference.mp4");

        try
        {
            await RunFfmpegAsync("-hide_banner", "-nostdin", "-y", "-ss", "20", "-t", "8", "-i", source,
                "-an", "-sn", "-dn", "-vf", $"fps={fps.ToString(CultureInfo.InvariantCulture)},scale={width}:{height}",
                "-c:v", "libx264", "-crf", "14", "-preset", "medium", referencePath);

            foreach (var bppf in new[] { 0.010, 0.015, 0.020, 0.025, 0.030, 0.035, 0.045, 0.060, 0.090 })
            {
                var bitrateK = (int)Math.Round(bppf * width * height * fps / 1000.0);
                var testPath = Path.Combine(outDir, $"floor_{bppf:0.000}.mp4");
                await RunFfmpegAsync("-hide_banner", "-nostdin", "-y", "-i", referencePath,
                    "-an", "-c:v", "libx264", "-b:v", $"{bitrateK}k", "-maxrate", $"{bitrateK}k",
                    "-bufsize", $"{bitrateK * 2}k", "-preset", "medium", testPath);

                var score = await QualityMeter.MeasureAsync(referencePath, testPath, CancellationToken.None);
                _output.WriteLine($"bppf {bppf:0.000} ({bitrateK}k) -> vmaf {score.VmafNegMean?.ToString("0.0") ?? "-"} p10 {score.VmafNegP10?.ToString("0.0") ?? "-"} ssim {score.Ssim?.ToString("0.0000") ?? "-"}");
            }
        }
        finally
        {
            try { Directory.Delete(outDir, recursive: true); } catch { }
        }
    }

    private static async Task RunFfmpegAsync(params string[] args)
    {
        var startInfo = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException(await stderr);
    }
}
