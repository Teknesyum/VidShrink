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

    /// <summary>
    /// Every number that came out of a measurement is pinned here as a plain literal, so moving a
    /// constant moves this test with it instead of sliding through it.
    /// </summary>
    [Fact]
    public void TheMeasuredConstantsArePinnedToTheirMeasurements()
    {
        Assert.Equal(1.5, FfmpegArguments.WidePeakFactor, 4);
        Assert.Equal(1.02, FfmpegArguments.TightPeakFactor, 4);
        Assert.Equal(1.10, FfmpegArguments.HardwarePeakCeiling, 4);
        Assert.Equal(6.0, FfmpegArguments.PeakOpensAtFloorRatio, 4);
        Assert.Equal(11.4, FfmpegArguments.PeakWidestAtFloorRatio, 4);
        Assert.Equal(2.0, FfmpegArguments.NvencPeakFactor, 4);
        Assert.Equal(2.0, FfmpegArguments.NvencBufferFactor, 4);
        Assert.Equal(2.5, CodecModel.NvencH264LayoutPenaltyWeight, 4);
        Assert.Equal(4.5, CodecModel.NvencHevcAv1LayoutPenaltyWeight, 4);
        Assert.Equal(1.0, CodecModel.NvencLayoutWeightRampStart, 4);
        Assert.Equal(1.86, CodecModel.NvencLayoutWeightRampEnd, 4);
        Assert.Equal(0.00429, CodecModel.HardwareMinBitratePerPixelFrame, 6);
        Assert.Equal(0.0756, CodecModel.HardwareMinBitratePerPixelSecond, 6);
        Assert.Equal(1.15, CodecModel.HardwareMinBitrateMargin, 4);
        Assert.Equal(2.0, CodecModel.HardwareMinBitrateHeadroom, 4);
        Assert.Equal(39, CodecModel.HardwareMinBitrateFloorK);
        Assert.Equal(11, PlanCalculator.HardwareDeliveryReserveK);
    }

    [Theory]
    [InlineData("av1_qsv")]
    [InlineData("h264_amf")]
    [InlineData("av1_amf")]
    [InlineData("hevc_qsv")]
    public void ThePeakFactorOpensWithTheHeadroomOverTheEncoderFloor(string codec)
    {
        var floorK = CodecModel.MinBitrateK(codec, 882, 496, 60);

        Assert.Equal(1.02, FfmpegArguments.PeakRateFactor(codec, (int)(floorK * 2.0), 882, 496, 60), 4);
        Assert.Equal(1.02, FfmpegArguments.PeakRateFactor(codec, (int)(floorK * 6.0), 882, 496, 60), 4);
        Assert.Equal(1.06, FfmpegArguments.PeakRateFactor(codec, (int)(floorK * 8.7), 882, 496, 60), 2);
        Assert.Equal(1.10, FfmpegArguments.PeakRateFactor(codec, (int)(floorK * 11.4), 882, 496, 60), 2);
        Assert.Equal(1.10, FfmpegArguments.PeakRateFactor(codec, (int)(floorK * 40.0), 882, 496, 60), 4);
    }

    /// <summary>
    /// The shapes and bitrates the curve was measured at live on av1_nvenc. 1918k at 882x496@60
    /// is the 100 MB plan for that contract's source: at 1.02 it delivered 0.973 of the request,
    /// at 1.10 1.008. NVENC no longer reads the curve: docs/olcumler/nvenc-2.md measured the tight
    /// peak as the largest loss of the old arguments (parlak hevc 2000 kbit, AQ off, 1.094 against
    /// no peak: VMAF-NEG p10 71.81 against 83.86) and 2x/2x level with no peak at -g 240
    /// (89.08/85.49 against 89.09/85.67). So NVENC gets 2.0 at every shape, and QSV and AMF keep the curve unmeasured.
    /// </summary>
    [Fact]
    public void ThePeakIsPinnedAtTheShapesThatWereMeasuredLive()
    {
        Assert.Equal(1.10, FfmpegArguments.PeakRateFactor("av1_qsv", 1918, 882, 496, 60), 3);
        Assert.Equal(1.02, FfmpegArguments.PeakRateFactor("av1_qsv", 890, 882, 496, 60), 3);
        Assert.Equal(1.02, FfmpegArguments.PeakRateFactor("av1_qsv", 1930, 1266, 712, 60), 3);
        Assert.Equal(1.02, FfmpegArguments.PeakRateFactor("av1_qsv", 437, 614, 346, 60), 3);
        Assert.Equal(1.02, FfmpegArguments.PeakRateFactor("av1_qsv", 125, 422, 238, 60), 3);
        foreach (var (bitrateK, width, height) in new[] { (1918, 882, 496), (890, 882, 496), (1930, 1266, 712), (437, 614, 346), (125, 422, 238) })
            Assert.Equal(2.0, FfmpegArguments.PeakRateFactor("av1_nvenc", bitrateK, width, height, 60), 3);
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libvpx-vp9")]
    public void TheProcessorPathKeepsTheWidePeak(string codec)
    {
        foreach (var bitrateK in new[] { 174, 522, 2088, 8000 })
            Assert.Equal(1.5, FfmpegArguments.PeakRateFactor(codec, bitrateK, 1920, 1080, 60), 4);
    }

    [Fact]
    public void TheBufferFollowsThePeakAndKeepsTheOldValueAtTheWidePeak()
    {
        Assert.Equal(2.0, FfmpegArguments.BufferFactor(1.5), 4);
        Assert.Equal(1.4, FfmpegArguments.BufferFactor(1.2), 4);
        Assert.Equal(1.04, FfmpegArguments.BufferFactor(1.02), 4);
        Assert.Equal(1.04, FfmpegArguments.BufferFactor("hevc_qsv", 1.02), 4);
    }

    /// <summary>
    /// NVENC weighs the scale and frame rate penalties by codec: h264 2.5x, hevc and av1 4.5x.
    /// docs/olcumler/nvenc-2.md: with the old weight the plan shrank hevc_nvenc to 0.42-0.60 of
    /// the height at 1000-2000 kbit, where full resolution measured up to 3.9 VMAF-NEG higher, and
    /// h264_nvenc to 0.34-0.60 where 0.67 was best on karanlik and hareketli. The weights were fitted with plan-only runs
    /// on karanlik and hareketli until the picked scale sat on the measured plateau. QSV and AMF
    /// keep the old weight. The weight is full here because 1000 kbit at 1920x818@24 sits 3.1x
    /// above the hardware floor.
    /// </summary>
    [Theory]
    [InlineData("h264_nvenc", "h264_qsv", 2.5)]
    [InlineData("hevc_nvenc", "hevc_qsv", 4.5)]
    [InlineData("av1_nvenc", "av1_qsv", 4.5)]
    public void NvencWeighsTheLayoutPenaltiesByCodec(string nvenc, string other, double weight)
    {
        var profile = ComplexityProfile.FromProbe(0.05, 0.03, 6, 288);
        var nvencHalf = PlanCalculator.ScoreLayout(profile, nvenc, 1000, 960, 408, 12, 24, 818, CompressionRegime.Balanced);
        var otherHalf = PlanCalculator.ScoreLayout(profile, other, 1000, 960, 408, 12, 24, 818, CompressionRegime.Balanced);

        Assert.True(otherHalf.ScalePenalty > 0);
        Assert.Equal(otherHalf.ScalePenalty * weight, nvencHalf.ScalePenalty, 6);
        Assert.Equal(otherHalf.FpsPenalty * weight, nvencHalf.FpsPenalty, 6);
    }

    /// <summary>
    /// The NVENC weight follows the budget at the full source frame measured against the hardware
    /// floor. At 1.0x the av1_nvenc 800k 1080p60 grid rose as the frame shrank (31.8 to 40.0
    /// VMAF-NEG), so the weight is 1; at 1.86x, the 600 kbit cell of docs/olcumler/nvenc-2.md, the
    /// full weight holds. Between them the line is straight, fable's call recorded in
    /// docs/danisma/2026-09-17-nvenc2-fable.md.
    /// </summary>
    [Theory]
    [InlineData("hevc_nvenc", 800, 1920, 1080, 60, 1080, 1.0)]
    [InlineData("av1_nvenc", 780, 1600, 900, 60, 1080, 1.0)]
    [InlineData("h264_nvenc", 1000, 1382, 588, 24, 818, 2.5)]
    [InlineData("av1_nvenc", 700, 1536, 654, 24, 818, 4.5)]
    public void NvencLayoutWeightFollowsTheSourceFloorRatio(string codec, int videoK, int width, int height, double fps, int sourceHeight, double expected)
    {
        var profile = ComplexityProfile.FromProbe(0.05, 0.03, 6, 288);
        var other = codec.Replace("nvenc", "qsv");
        var nvenc = PlanCalculator.ScoreLayout(profile, codec, videoK, width, height, fps, fps, sourceHeight, CompressionRegime.Balanced);
        var qsv = PlanCalculator.ScoreLayout(profile, other, videoK, width, height, fps, fps, sourceHeight, CompressionRegime.Balanced);

        if (qsv.ScalePenalty > 0)
            Assert.Equal(qsv.ScalePenalty * expected, nvenc.ScalePenalty, 6);
        Assert.Equal(1.0, CodecModel.LayoutPenaltyWeight(codec, 1.0), 9);
        Assert.Equal(1.0 + (CodecModel.LayoutPenaltyWeight(codec, 1.86) - 1.0) / 2, CodecModel.LayoutPenaltyWeight(codec, 1.43), 9);
        Assert.Equal(CodecModel.LayoutPenaltyWeight(codec, 1.86), CodecModel.LayoutPenaltyWeight(codec, 9.0), 9);
        Assert.True(CodecModel.LayoutPenaltyWeight(codec, 1.86) > 1.0);
    }

    /// <summary>
    /// NVENC writes maxrate and bufsize both at twice the request. The buffer does not follow the
    /// 1 + 2 * (peak - 1) line, which would give 3x; 2x/2x is the arm measured in
    /// docs/olcumler/nvenc-2.md.
    /// </summary>
    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    public void NvencWritesTwiceTheRequestAsPeakAndBuffer(string codec)
    {
        var plan = new EncodePlan { Codec = codec, Mode = "2pass", VideoBitrateK = 1945, Width = 1882, Height = 802, Fps = 24, Preset = "p4", PixelFormat = "yuv420p" };
        var info = new MediaInfo { FilePath = "in.mkv", FileSizeBytes = 100000000, TotalBitrateBps = 80000000, Width = 1920, Height = 818, Fps = 24, DurationSeconds = 10, VideoCodec = "ffv1" };

        var args = FfmpegArguments.Build(info, plan, "out.mp4", 0, null, null);

        Assert.Equal("3890k", args[args.IndexOf("-maxrate") + 1]);
        Assert.Equal("3890k", args[args.IndexOf("-bufsize") + 1]);
        Assert.Equal(2.0, FfmpegArguments.BufferFactor(codec, FfmpegArguments.NvencPeakFactor), 4);
    }

    /// <summary>
    /// The floor line is what av1_nvenc actually delivers when it is asked for 32k and told to peak
    /// at 33k. Nine layouts, 20 s each, measured on this machine. The model has to sit above every
    /// one of them - a floor that reads low lets the layout search pick a shape the encoder cannot
    /// reach - and not so far above that usable layouts are thrown away.
    /// </summary>
    [Theory]
    [InlineData(1920, 1080, 60, 691)]
    [InlineData(1920, 1080, 30, 424)]
    [InlineData(1280, 720, 60, 341)]
    [InlineData(1280, 720, 30, 211)]
    [InlineData(854, 480, 60, 145)]
    [InlineData(854, 480, 30, 95)]
    [InlineData(640, 360, 60, 72)]
    [InlineData(640, 360, 15, 38)]
    [InlineData(426, 240, 30, 39)]
    public void TheEncoderFloorCoversWhatTheEncoderActuallyDelivered(int width, int height, double fps, int measuredK)
    {
        var modelled = CodecModel.MinBitrateK("av1_nvenc", width, height, fps);

        _output.WriteLine($"{width}x{height}@{fps:0.##}: measured {measuredK}k, model {modelled}k ({modelled / (double)measuredK:0.000}x)");
        Assert.True(modelled >= measuredK, $"The model puts the floor at {modelled}k but the encoder delivered {measuredK}k.");
        // The widest the model sits over what was measured is 1,236x, at 640x360@60; the fit's own
        // worst residual is 1,11x and the 15% margin is carried on top of it. The bound is that
        // measured worst case plus a hair, not a round number chosen for comfort.
        Assert.True(modelled <= measuredK * 1.25, $"The model puts the floor at {modelled}k, far above the {measuredK}k measured.");
    }

    [Fact]
    public void TheFloorIsPinnedAtTheShapesTheContractMeasured()
    {
        Assert.Equal(795, CodecModel.MinBitrateK("av1_nvenc", 1920, 1080, 60));
        Assert.Equal(1590, CodecModel.UsableBitrateK("av1_nvenc", 1920, 1080, 60));
        Assert.Equal(346, CodecModel.MinBitrateK("av1_nvenc", 1266, 712, 60));
        Assert.Equal(692, CodecModel.UsableBitrateK("av1_nvenc", 1266, 712, 60));
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libsvtav1")]
    public void TheProcessorEncodersHaveNoFloor(string codec)
    {
        Assert.Equal(0, CodecModel.MinBitrateK(codec, 1920, 1080, 60));
        Assert.Equal(0, CodecModel.UsableBitrateK(codec, 1920, 1080, 60));
    }

    /// <summary>
    /// The shape the plan settles on has to be one the encoder can actually hit at the bitrate the
    /// plan asks for. This is what failed on the 8 MB target: the search picked 1266x712@60 and
    /// asked for 131k, a fifth of what av1_nvenc delivers at that shape.
    /// </summary>
    [Theory]
    [InlineData(4.0)]
    [InlineData(8.0)]
    [InlineData(16.0)]
    [InlineData(25.0)]
    [InlineData(50.0)]
    [InlineData(100.0)]
    public void EveryHardwarePlanAsksForABitrateTheEncoderCanDeliver(double targetMb)
    {
        var info = SourceInfo();
        foreach (var detail in new[] { -0.2, 0.2, 0.55, 0.9, 1.4 })
        foreach (var motion in new[] { 0.0, 0.25, 0.6, 1.0 })
        {
            var options = new PlanOptions { TargetMb = targetMb, FillPolicy = FillPolicy.FillTarget, SpeedMode = SpeedMode.Fast };
            var profile = ComplexityProfile.FromSourceBitrate(info) with
            {
                Measured = true,
                MotionMeasured = true,
                DetailExponent = detail,
                MotionExponent = motion
            };
            var plan = PlanCalculator.BuildDetailed(info, options, profile, new FixedAvailability("av1_nvenc")).Plan;
            if (plan.ModeEnum != EncodeMode.TwoPass) continue;

            var usable = CodecModel.UsableBitrateK(plan.Codec, plan.Width, plan.Height, plan.Fps);
            if (plan.VideoBitrateK >= usable) continue;

            // Nothing reachable is a legitimate answer - the encoder has an absolute floor that no
            // layout gets under - but only when no candidate layout would have been reachable.
            var reachableExists = SmallestUsableK(info, options, plan.Codec) <= plan.VideoBitrateK;
            Assert.False(reachableExists,
                $"{targetMb:0.#} MB, detail {detail:0.00}, motion {motion:0.00}: the plan asks {plan.VideoBitrateK}k at {plan.Width}x{plan.Height}@{plan.Fps:0.##}, where the encoder does not follow the request below {usable}k, and a reachable layout existed.");
        }
    }

    private static int SmallestUsableK(MediaInfo info, PlanOptions options, string codec)
    {
        var regime = CompressionStrategy.RegimeFor(info.FileSizeMb, options.TargetMb);
        var effective = new PlanOptions
        {
            TargetMb = options.TargetMb,
            AllowResolutionDrop = options.AllowResolutionDrop && CompressionStrategy.AllowsResolutionDrop(regime),
            AllowFpsDrop = options.AllowFpsDrop && CompressionStrategy.AllowsFpsDrop(regime),
            SpeedMode = options.SpeedMode
        };
        var floors = CompressionStrategy.FloorsFor(regime);
        var smallest = int.MaxValue;

        foreach (var fps in PlanCalculator.FpsCandidates(info, effective, regime))
        foreach (var scale in PlanCalculator.ScaleCandidates(effective, regime))
        {
            var width = EvenDown((int)Math.Round(info.Width * scale));
            var height = EvenDown((int)Math.Round(info.Height * scale));
            if (height < floors.MinHeight && height < info.Height) continue;
            if (width < 2 || height < 2) continue;
            smallest = Math.Min(smallest, CodecModel.UsableBitrateK(codec, width, height, fps));
        }

        return smallest;
    }

    private static int EvenDown(int value) => value % 2 == 0 ? value : value - 1;

    /// <summary>
    /// A delivered file costs more than the plan asks for - the container is 9 kbit/s at every
    /// target and the encoder spends a few more past the request - so the hardware request holds
    /// that back. The processor path was not measured and keeps the bitrates it had.
    /// </summary>
    [Fact]
    public void TheHardwareRequestHoldsBackTheDeliveryReserve()
    {
        var info = SourceInfo();
        var options = new PlanOptions { TargetMb = 8, FillPolicy = FillPolicy.FillTarget, SpeedMode = SpeedMode.Fast };

        var hardware = PlanCalculator.BuildDetailed(info, options, null, new FixedAvailability("av1_qsv")).Plan;
        var processor = PlanCalculator.BuildDetailed(info, options, null, new FixedAvailability("libx264")).Plan;

        var hardwareTotalK = hardware.VideoBitrateK + hardware.AudioBitrateK;
        var processorTotalK = processor.VideoBitrateK + processor.AudioBitrateK;

        _output.WriteLine($"hardware {hardware.VideoBitrateK}k + {hardware.AudioBitrateK}k audio, processor {processor.VideoBitrateK}k + {processor.AudioBitrateK}k audio");
        Assert.Equal(11, processorTotalK - hardwareTotalK);
    }

    /// <summary>
    /// The peak curve on a QSV encoder: tight at a small target, open at a large one. NVENC left the
    /// curve in docs/olcumler/nvenc-2.md and writes a flat 2.0.
    /// </summary>
    [Fact]
    public void ASmallHardwareTargetGetsATighterPeakThanALargeOne()
    {
        var info = SourceInfo();
        const int BigK = 12000;
        const int SmallK = 2088;
        var big = FfmpegArguments.Build(info, BitratePlan("av1_qsv", BigK), "out.mp4", 0, null);
        var small = FfmpegArguments.Build(info, BitratePlan("av1_qsv", SmallK), "out.mp4", 0, null);

        var bigShare = FlagValueK(big, "-maxrate") / (double)BigK;
        var smallShare = FlagValueK(small, "-maxrate") / (double)SmallK;

        _output.WriteLine($"{BigK}k -> maxrate {FlagValueK(big, "-maxrate")}k ({bigShare:0.000}x), {SmallK}k -> maxrate {FlagValueK(small, "-maxrate")}k ({smallShare:0.000}x)");
        Assert.True(smallShare < bigShare, "The small target may not carry the same peak share as the large one.");
        Assert.Equal(1.10, bigShare, 2);
        Assert.Equal(1.02, smallShare, 2);
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
        Assert.True(landedShort > 1.0, "An attempt that landed short still has to pull the next one up.");

        // Both directions are bounded, and by the same amount: whatever the clamp is set to, a
        // runaway overspend and a runaway shortfall have to meet back at 1.
        var runawayOverspend = PlanCalculator.HardwareDeliveryBias(4.0);
        var runawayShortfall = PlanCalculator.HardwareDeliveryBias(0.25);
        Assert.True(runawayOverspend > 0.5, "The downward correction has to stay bounded.");
        Assert.Equal(1.0, runawayOverspend * runawayShortfall, 4);
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
        public EncoderProbeState EncoderState(string codec) =>
            WorksAsEncoder(codec) ? EncoderProbeState.Working : EncoderProbeState.NotWorking;
    }

    /// <summary>
    /// The whole point of the contract: with the scaled peak and the two-way bias in place, the small
    /// hardware targets have to land inside the fill band on the first attempt, the way the large ones
    /// already do. Runs the measuring round the way the window runs it and encodes for real.
    /// </summary>
    [LiveSourceTheory]
    [InlineData(180.0)]
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
        var outDir = TestPaths.LiveOut("canli");
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
        var peakFactor = FfmpegArguments.PeakRateFactor(plan.Codec, plan.VideoBitrateK, plan.Width, plan.Height, plan.Fps);
        _output.WriteLine($"  floor {CodecModel.MinBitrateK(plan.Codec, plan.Width, plan.Height, plan.Fps)}k peak factor {peakFactor:0.000} maxrate {(int)(plan.VideoBitrateK * peakFactor)}k");
        _output.WriteLine($"  band {band.LowerMb:0.00}-{band.UpperMb:0.00} MB");
        _output.WriteLine($"  result {result.OutputMb:0.00} MB attempts {result.Attempts} success {result.Success} underBand {result.UnderBand} in {clock.Elapsed.TotalSeconds:0.0}s");
        foreach (var attempt in result.Trace ?? Array.Empty<EncodeAttempt>())
            _output.WriteLine($"    attempt {attempt.Number} {attempt.Branch}: aim {attempt.AimMb:0.00} MB got {attempt.ActualMb:0.00} MB at {attempt.VideoBitrateK}k");

        Assert.True(result.Success, result.Error);
        Assert.True(result.OutputMb <= targetMb, $"The {targetMb:0.#} MB target was exceeded with {result.OutputMb:0.00} MB.");
        Assert.Equal(1, result.Attempts);
    }
}
