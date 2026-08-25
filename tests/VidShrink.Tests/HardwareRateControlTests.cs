using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class HardwareRateControlTests
{
    private readonly ITestOutputHelper _output;

    public HardwareRateControlTests(ITestOutputHelper output) => _output = output;

    private static MediaInfo SourceInfo() => new()
    {
        FilePath = "source.mp4",
        FileSizeBytes = 216L * 1024 * 1024,
        DurationSeconds = 400,
        Width = 1920,
        Height = 1080,
        Fps = 60,
        VideoCodec = "h264",
        TotalBitrateBps = 4_500_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    private static EncodePlan BitratePlan(string codec, int videoBitrateK) => new()
    {
        Codec = codec,
        Mode = "2pass",
        VideoBitrateK = videoBitrateK,
        AudioCodec = "aac",
        AudioBitrateK = 128,
        Width = 1920,
        Height = 1080,
        Fps = 60,
        Preset = FfmpegArguments.DefaultPreset(codec),
        PixelFormat = "yuv420p"
    };

    private static int FlagValueK(IReadOnlyList<string> args, string flag)
    {
        var index = args.ToList().IndexOf(flag);
        Assert.True(index >= 0, $"{flag} is missing from the arguments.");
        var value = args[index + 1];
        Assert.EndsWith("k", value);
        return int.Parse(value[..^1]);
    }

    private static (EncodePlan Plan, ComplexityProfile Profile) SettledPlan(MediaInfo info, PlanOptions options)
    {
        var availability = new FixedAvailability("av1_nvenc");
        var profile = CalibratedProfile(info, PlanCalculator.BuildDetailed(info, options, null, availability).Plan);
        EncodePlan plan;
        for (var round = 0; round < 4; round++)
        {
            plan = PlanCalculator.BuildDetailed(info, options, profile, availability).Plan;
            var scale = (double)plan.Height / info.Height;
            if (profile.AppliesTo(plan.Codec, scale, plan.Fps)) return (plan, profile);
            profile = CalibratedProfile(info, plan);
        }

        plan = PlanCalculator.BuildDetailed(info, options, profile, availability).Plan;
        Assert.True(profile.AppliesTo(plan.Codec, (double)plan.Height / info.Height, plan.Fps),
            "The calibration never settled on a plan shape.");
        return (plan, profile);
    }

    private static ComplexityProfile CalibratedProfile(MediaInfo info, EncodePlan plan)
    {
        var scale = (double)plan.Height / info.Height;
        return ComplexityProfile.FromSourceBitrate(info) with
        {
            Measured = true,
            LevelFactor = 1.0,
            HalvingStep = 6.0,
            WindowBias = 1.0,
            BiasSource = WindowBiasSource.Scan,
            Calibration = new CalibrationSignature
            {
                Codec = plan.Codec,
                Width = plan.Width,
                Height = plan.Height,
                Fps = plan.Fps,
                Scale = scale
            }
        };
    }

    [Theory]
    [InlineData("av1_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_amf")]
    [InlineData("hevc_qsv")]
    public void ThePeakFactorFallsWithTheRequestedBitrate(string codec)
    {
        var knee = FfmpegArguments.PeakScaleBitrateK;
        var far = FfmpegArguments.PeakRateFactor(codec, knee * 4);
        var midway = FfmpegArguments.PeakRateFactor(codec, (int)(knee * 1.5));
        var atKnee = FfmpegArguments.PeakRateFactor(codec, knee);
        var low = FfmpegArguments.PeakRateFactor(codec, knee / 6);

        Assert.Equal(FfmpegArguments.WidePeakFactor, far, 4);
        Assert.Equal(FfmpegArguments.TightPeakFactor, atKnee, 4);
        Assert.Equal(FfmpegArguments.TightPeakFactor, low, 4);
        Assert.True(midway > atKnee && midway < far, "Between the knee and the wide end the peak has to open up gradually.");
        Assert.True(FfmpegArguments.PeakRateFactor(codec, knee * 2) > midway, "The peak has to keep opening with the bitrate.");
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libvpx-vp9")]
    public void TheProcessorPathKeepsTheWidePeak(string codec)
    {
        foreach (var bitrateK in new[] { 174, 522, 2088, 8000 })
            Assert.Equal(FfmpegArguments.WidePeakFactor, FfmpegArguments.PeakRateFactor(codec, bitrateK), 4);
    }

    [Fact]
    public void TheBufferFollowsThePeakAndKeepsTheOldValueAtTheWidePeak()
    {
        Assert.Equal(2.0, FfmpegArguments.BufferFactor(FfmpegArguments.WidePeakFactor), 4);
        Assert.True(FfmpegArguments.BufferFactor(1.2) < 2.0);
        Assert.True(FfmpegArguments.BufferFactor(FfmpegArguments.TightPeakFactor) > 1.0);
    }

    [Fact]
    public void ASmallHardwareTargetGetsATighterPeakThanALargeOne()
    {
        var info = SourceInfo();
        const int BigK = 12000;
        const int SmallK = 2088;
        var big = FfmpegArguments.Build(info, BitratePlan("av1_nvenc", BigK), "out.mp4", 0, null);
        var small = FfmpegArguments.Build(info, BitratePlan("av1_nvenc", SmallK), "out.mp4", 0, null);

        var bigShare = FlagValueK(big, "-maxrate") / (double)BigK;
        var smallShare = FlagValueK(small, "-maxrate") / (double)SmallK;

        _output.WriteLine($"{BigK}k -> maxrate {FlagValueK(big, "-maxrate")}k ({bigShare:0.000}x), {SmallK}k -> maxrate {FlagValueK(small, "-maxrate")}k ({smallShare:0.000}x)");
        Assert.True(smallShare < bigShare, "The small target may not carry the same peak share as the large one.");
        Assert.Equal(FfmpegArguments.WidePeakFactor, bigShare, 2);
        Assert.Equal(FfmpegArguments.TightPeakFactor, smallShare, 2);
        Assert.True(FlagValueK(small, "-bufsize") / (double)SmallK < FlagValueK(big, "-bufsize") / (double)BigK,
            "The buffer has to tighten along with the peak.");
    }

    [Fact]
    public void TheProcessorArgumentsAreUnchanged()
    {
        var info = SourceInfo();
        foreach (var bitrateK in new[] { 174, 522, 2088 })
        {
            var args = FfmpegArguments.Build(info, BitratePlan("libx264", bitrateK), "out.mp4", 2, "log");
            Assert.Equal((int)(bitrateK * 1.5), FlagValueK(args, "-maxrate"));
            Assert.Equal(bitrateK * 2, FlagValueK(args, "-bufsize"));
        }
    }

    [Fact]
    public void TheDeliveryBiasPointsBothWays()
    {
        Assert.Equal(1.0, PlanCalculator.HardwareDeliveryBias(null), 4);
        Assert.Equal(1.0, PlanCalculator.HardwareDeliveryBias(1.0), 4);

        var overspent = PlanCalculator.HardwareDeliveryBias(1.05);
        var overspentMore = PlanCalculator.HardwareDeliveryBias(1.20);
        var landedShort = PlanCalculator.HardwareDeliveryBias(0.95);

        Assert.True(overspent < 1.0, "An attempt that spent past the request has to pull the next one down.");
        Assert.True(overspentMore < overspent, "A bigger overspend has to pull down harder.");
        Assert.True(overspentMore >= CodecModel.HardwareBitrateYield, "The downward correction stays inside the measured hardware yield.");
        Assert.True(landedShort > 1.0, "An attempt that landed short still has to pull the next one up.");
    }

    [Fact]
    public void AnOverspentAttemptLeavesADownwardBiasOnTheNextPlan()
    {
        var info = SourceInfo();
        var plan = BitratePlan("av1_nvenc", 2045);
        var deliveredMb = PlanCalculator.EstimatedMb(plan, info.DurationSeconds)!.Value * 1.06;

        var corrected = PlanCalculator.Correct(plan, deliveredMb, 100.0, info.DurationSeconds);

        _output.WriteLine($"delivered {deliveredMb:0.00} MB -> {corrected.VideoBitrateK}k bias {corrected.BitrateBias:0.###}");
        Assert.True(corrected.BitrateBias < 1.0, "The plan after an overspend has to carry a downward bias.");
        Assert.True(corrected.VideoBitrateK < plan.VideoBitrateK, "The request itself has to come down too.");
        Assert.True(PlanCalculator.EstimatedMb(corrected, info.DurationSeconds) <= 100.0,
            "The corrected plan may not ask for more than the target.");
    }

    [Fact]
    public void AnAttemptThatLandedShortLeavesAnUpwardBiasOnTheNextPlan()
    {
        var info = SourceInfo();
        var plan = BitratePlan("av1_nvenc", 2045);
        var deliveredMb = PlanCalculator.EstimatedMb(plan, info.DurationSeconds)!.Value * 0.94;

        var corrected = PlanCalculator.Correct(plan, deliveredMb, 100.0, info.DurationSeconds, fillUnderBand: true);

        _output.WriteLine($"delivered {deliveredMb:0.00} MB -> {corrected.VideoBitrateK}k bias {corrected.BitrateBias:0.###}");
        Assert.True(corrected.BitrateBias > 1.0, "The plan after a short landing has to carry an upward bias.");
    }

    [Fact]
    public void ACalibratedHardwarePlanKeepsTheNeutralBias()
    {
        var info = SourceInfo();
        var options = new PlanOptions { TargetMb = 100, FillPolicy = FillPolicy.FillTarget, SpeedMode = SpeedMode.Fast };

        var (plan, _) = SettledPlan(info, options);

        _output.WriteLine($"{plan.Codec} {plan.Width}x{plan.Height}@{plan.Fps:0.##} {plan.Mode} {plan.VideoBitrateK}k bias {plan.BitrateBias:0.###}");
        Assert.Equal(1.0, plan.BitrateBias, 3);
    }

    [Fact]
    public void AnUncalibratedHardwarePlanLeavesRoomUnderTheEstimateInsteadOfRaisingTheRequest()
    {
        var info = SourceInfo();
        var options = new PlanOptions { TargetMb = 100, FillPolicy = FillPolicy.FillTarget, SpeedMode = SpeedMode.Fast };

        var plan = PlanCalculator.BuildDetailed(info, options, null, new FixedAvailability("av1_nvenc")).Plan;
        var estimate = PlanCalculator.Estimate(plan, info, null);

        _output.WriteLine($"{plan.VideoBitrateK}k bias {plan.BitrateBias:0.###} estimate {estimate.LowMb:0.00}-{estimate.HighMb:0.00} MB");
        Assert.Equal(1.0, plan.BitrateBias, 3);
        Assert.True(estimate.LowMb < estimate.ExpectedMb * (1 - PlanCalculator.TwoPassUncertainty),
            "A blind hardware plan has to leave room under the estimate for an encoder that lands short.");
    }

    [Fact]
    public void TheRequestNeverAsksForMoreThanTheTargetBudget()
    {
        var info = SourceInfo();
        foreach (var targetMb in new[] { 8.0, 25.0, 50.0, 100.0 })
        {
            var options = new PlanOptions { TargetMb = targetMb, FillPolicy = FillPolicy.FillTarget, SpeedMode = SpeedMode.Fast };
            var (plan, _) = SettledPlan(info, options);
            var estimated = PlanCalculator.EstimatedMb(plan, info.DurationSeconds);

            _output.WriteLine($"{targetMb:0.#} MB -> {plan.VideoBitrateK}k bias {plan.BitrateBias:0.###} estimate {estimated:0.00} MB");
            Assert.True(plan.BitrateBias <= 1.0, "A calibrated hardware plan may not aim above the budget.");
            Assert.True(estimated is null || estimated <= targetMb, $"The {targetMb:0.#} MB plan asks for {estimated:0.00} MB.");
        }
    }

    private sealed class FixedAvailability : IEncoderAvailability
    {
        private readonly string _codec;

        public FixedAvailability(string codec) => _codec = codec;

        public bool HasEncoder(string codec) => string.Equals(codec, _codec, StringComparison.OrdinalIgnoreCase) || !CodecModel.IsHardware(codec);

        public bool WorksAsEncoder(string codec) => string.Equals(codec, _codec, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The whole point of the contract: with the scaled peak and the two-way bias in place, the small
    /// hardware targets have to land inside the fill band on the first attempt, the way the large ones
    /// already do. Runs the measuring round the way the window runs it and encodes for real.
    /// </summary>
    [LiveSourceTheory]
    [InlineData(100.0)]
    [InlineData(50.0)]
    [InlineData(25.0)]
    [InlineData(8.0)]
    public async Task LiveFastTargetsLandInsideTheBandOnTheFirstAttempt(double targetMb)
        => await RunLiveTargetAsync(targetMb, SpeedMode.Fast);

    [LiveSourceTheory]
    [InlineData(180.0)]
    [InlineData(100.0)]
    [InlineData(25.0)]
    [InlineData(8.0)]
    public async Task LiveProcessorTargetsStillLandInsideTheBandOnTheFirstAttempt(double targetMb)
        => await RunLiveTargetAsync(targetMb, SpeedMode.Quality);

    private async Task RunLiveTargetAsync(double targetMb, SpeedMode speed)
    {
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE")!;
        var outDir = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_OUT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "vidshrink_live");
        Directory.CreateDirectory(outDir);
        var outputPath = Path.Combine(outDir, $"rate_{speed}_{targetMb:0.#}mb.mp4");

        var info = await FfprobeClient.ProbeAsync(source);
        var options = new PlanOptions { TargetMb = targetMb, FillPolicy = FillPolicy.FillTarget, SpeedMode = speed };

        var profile = await ComplexityProbe.RunAsync(info, options.SpeedMode, CancellationToken.None);
        var draft = PlanCalculator.BuildDetailed(info, options, profile, EncoderCapabilities.Instance).Plan;

        ComplexityProfile calibrated;
        var rounds = 0;
        while (true)
        {
            rounds++;
            calibrated = await CalibrationProbe.RunAsync(info, draft, profile, options.SpeedMode, CancellationToken.None);
            if (rounds >= 2 || !calibrated.Calibrated) break;

            var settled = PlanCalculator.BuildDetailed(info, options, calibrated, EncoderCapabilities.Instance).Plan;
            var settledScale = info.Height <= 0 ? 1.0 : (double)settled.Height / info.Height;
            if (calibrated.AppliesTo(settled.Codec, settledScale, settled.Fps)) break;
            profile = calibrated.WithoutCalibration();
            draft = settled;
        }

        var plan = PlanCalculator.BuildDetailed(info, options, calibrated, EncoderCapabilities.Instance).Plan;
        var scale = info.Height <= 0 ? 1.0 : (double)plan.Height / info.Height;
        var band = FillBand.For(Math.Min(targetMb, plan.EffectiveTargetMb ?? targetMb));

        var clock = Stopwatch.StartNew();
        var result = await new EncodeRunner().RunAsync(info, plan, outputPath, targetMb, null, CancellationToken.None, FillPolicy.FillTarget, calibrated);
        clock.Stop();

        _output.WriteLine($"{speed} {targetMb:0.#} MB | {plan.Codec} {plan.Preset} {plan.Width}x{plan.Height}@{plan.Fps:0.##} {plan.Mode} {plan.VideoBitrateK}k");
        _output.WriteLine($"  calibrated {calibrated.Calibrated} appliesToPlan {calibrated.AppliesTo(plan.Codec, scale, plan.Fps)} rounds {rounds} bias {plan.BitrateBias:0.###}");
        _output.WriteLine($"  peak factor {FfmpegArguments.PeakRateFactor(plan.Codec, plan.VideoBitrateK):0.000} maxrate {(int)(plan.VideoBitrateK * FfmpegArguments.PeakRateFactor(plan.Codec, plan.VideoBitrateK))}k");
        _output.WriteLine($"  band {band.LowerMb:0.00}-{band.UpperMb:0.00} MB");
        _output.WriteLine($"  result {result.OutputMb:0.00} MB attempts {result.Attempts} success {result.Success} underBand {result.UnderBand} in {clock.Elapsed.TotalSeconds:0.0}s");
        foreach (var attempt in result.Trace ?? Array.Empty<EncodeAttempt>())
            _output.WriteLine($"    attempt {attempt.Number} {attempt.Branch}: aim {attempt.AimMb:0.00} MB got {attempt.ActualMb:0.00} MB at {attempt.VideoBitrateK}k");

        Assert.True(result.Success, result.Error);
        Assert.True(result.OutputMb <= targetMb, $"The {targetMb:0.#} MB target was exceeded with {result.OutputMb:0.00} MB.");
        Assert.Equal(1, result.Attempts);
    }
}
