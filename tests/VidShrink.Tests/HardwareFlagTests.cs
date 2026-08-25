using Xunit.Abstractions;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class HardwareFlagTests
{
    private readonly ITestOutputHelper _output;

    public HardwareFlagTests(ITestOutputHelper output) => _output = output;

    private static readonly string[] AllHardwareCodecs =
    {
        "h264_nvenc", "hevc_nvenc", "av1_nvenc",
        "h264_qsv", "hevc_qsv", "av1_qsv",
        "h264_amf", "hevc_amf", "av1_amf"
    };

    private static readonly string[] AllQualityFlags = { "-crf", "-cq", "-global_quality", "-qp_i", "-qp_p", "-qp_b" };

    private sealed class Availability : IEncoderAvailability
    {
        private readonly HashSet<string> _names;
        public Availability(params string[] names) => _names = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        public bool HasEncoder(string name) => _names.Contains(name);
        public bool WorksAsEncoder(string codec) => _names.Contains(codec);
    }

    private static MediaInfo SampleInfo() => new()
    {
        FilePath = "sample.mp4",
        FileSizeBytes = 900L * 1024 * 1024,
        DurationSeconds = 300,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 25_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    private static EncodePlan CrfPlan(string codec) => new()
    {
        Codec = codec,
        Mode = "crf",
        Crf = 24,
        VideoBitrateK = 2000,
        AudioCodec = "aac",
        AudioBitrateK = 128,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        Preset = FfmpegArguments.DefaultPreset(codec),
        PixelFormat = "yuv420p"
    };

    private static string ExpectedFlag(string codec) => CodecModel.Vendor(codec) switch
    {
        EncoderVendor.Nvenc => "-cq",
        EncoderVendor.Qsv => "-global_quality",
        EncoderVendor.Amf => "-qp_i",
        _ => "-crf"
    };

    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    [InlineData("h264_qsv")]
    [InlineData("hevc_qsv")]
    [InlineData("av1_qsv")]
    [InlineData("h264_amf")]
    [InlineData("hevc_amf")]
    [InlineData("av1_amf")]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libsvtav1")]
    public void EachEncoderGetsOnlyItsOwnQualityFlag(string codec)
    {
        var args = FfmpegArguments.Build(SampleInfo(), CrfPlan(codec), "out.mp4", 0, null);
        var expected = ExpectedFlag(codec);

        Assert.Contains(expected, args);
        foreach (var flag in AllQualityFlags)
        {
            if (flag == expected) continue;
            if (CodecModel.Vendor(codec) == EncoderVendor.Amf && flag is "-qp_p" or "-qp_b") continue;
            Assert.DoesNotContain(flag, args);
        }
    }

    [Fact]
    public void TheProbeAndTheEncodeAskForTheSameQualityPoint()
    {
        foreach (var codec in AllHardwareCodecs.Append("libx264").Append("libx265"))
            Assert.Equal(CalibrationProbe.QualityArgs(codec, 24), CodecModel.QualityArgs(codec, 24));
    }

    [Fact]
    public void QualityModeKeepsTheFastPreferenceOnP4()
    {
        var options = new PlanOptions
        {
            TargetMb = 25,
            Codec = CodecPreference.Fast,
            SpeedMode = SpeedMode.Quality
        };

        var plan = PlanCalculator.Build(SampleInfo(), options, new Availability("h264_nvenc", "libx264"));

        Assert.Equal("h264_nvenc", plan.Codec);
        Assert.Equal("p4", plan.Preset);
    }

    [Theory]
    [InlineData(25.0)]
    [InlineData(180.0)]
    public void NoHardwarePlanAimsAboveTheSoftwareBudget(double targetMb)
    {
        var info = SampleInfo();
        var availability = new Availability("libx264", "av1_nvenc", "hevc_nvenc", "h264_nvenc");

        var hardware = PlanCalculator.BuildDetailed(info, new PlanOptions
        {
            TargetMb = targetMb,
            SpeedMode = SpeedMode.Fast,
            AllowResolutionDrop = false,
            AllowFpsDrop = false
        }, null, availability);

        var software = PlanCalculator.BuildDetailed(info, new PlanOptions
        {
            TargetMb = targetMb,
            AllowResolutionDrop = false,
            AllowFpsDrop = false
        }, null, availability);

        Assert.True(CodecModel.IsHardware(hardware.Plan.Codec));
        Assert.True(hardware.Plan.VideoBitrateK + hardware.Plan.AudioBitrateK
                    <= software.Plan.VideoBitrateK + software.Plan.AudioBitrateK);
        Assert.True(hardware.Estimate.ExpectedMb <= PlanCalculator.EffectiveTargetMb(targetMb, info.FileSizeMb) + 1e-9);
    }

    [Fact]
    public void TheAv1HardwareEncodersStayOutOfTheQualityCost()
    {
        Assert.False(CodecModel.CostsQualityInHardware("av1_nvenc"));
        Assert.False(CodecModel.CostsQualityInHardware("av1_qsv"));
        Assert.True(CodecModel.CostsQualityInHardware("hevc_nvenc"));
        Assert.True(CodecModel.CostsQualityInHardware("h264_amf"));
        Assert.False(CodecModel.CostsQualityInHardware("libx265"));
    }

    [Fact]
    public void TheBitrateYieldOpensTheEstimateDownwardWithoutMovingTheRequest()
    {
        var info = SampleInfo();
        var availability = new Availability("libx264", "av1_nvenc");
        var options = new PlanOptions
        {
            TargetMb = 25,
            SpeedMode = SpeedMode.Fast,
            AllowResolutionDrop = false,
            AllowFpsDrop = false
        };

        var result = PlanCalculator.BuildDetailed(info, options, null, availability);

        Assert.Equal(EncodeMode.TwoPass, result.Plan.ModeEnum);
        Assert.Equal(1.0, result.Plan.BitrateBias);
        Assert.True(result.Estimate.LowMb <= result.Estimate.ExpectedMb * CodecModel.HardwareBitrateYield + 1e-9);
        Assert.True(result.Estimate.HighMb <= result.Estimate.ExpectedMb * (1 + PlanCalculator.TwoPassUncertainty) + 1e-9);
    }

    [LiveSourceTheory]
    [InlineData(180.0)]
    public async Task LiveFastRunDoesNotSpendEveryAttempt(double targetMb)
    {
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE")!;
        var outDir = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_OUT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "vidshrink_live");
        Directory.CreateDirectory(outDir);
        var outputPath = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(source)}_fastflag_{targetMb:0.#}mb.mp4");

        var info = await FfprobeClient.ProbeAsync(source);
        var options = new PlanOptions { TargetMb = targetMb, FillPolicy = FillPolicy.FillTarget, SpeedMode = SpeedMode.Fast };

        var profile = await ComplexityProbe.RunAsync(info, options.SpeedMode, CancellationToken.None);
        var draft = PlanCalculator.BuildDetailed(info, options, profile, EncoderCapabilities.Instance).Plan;
        var calibrated = await CalibrationProbe.RunAsync(info, draft, profile, options.SpeedMode, CancellationToken.None);

        var plan = PlanCalculator.BuildDetailed(info, options, calibrated, EncoderCapabilities.Instance).Plan;
        var result = await new EncodeRunner().RunAsync(info, plan, outputPath, targetMb, null, CancellationToken.None, FillPolicy.FillTarget, calibrated);

        var band = FillBand.For(targetMb);
        _output.WriteLine($"target {targetMb:0.##} MB | band {band.LowerMb:0.00}-{band.UpperMb:0.00} | plan {plan.Codec} {plan.Preset} {plan.Width}x{plan.Height}@{plan.Fps:0.##} {plan.Mode} {plan.VideoBitrateK}k");
        foreach (var attempt in result.Trace ?? Array.Empty<EncodeAttempt>())
            _output.WriteLine($"  attempt {attempt.Number}: branch={attempt.Branch} aim={attempt.AimMb:0.00} MB actual={attempt.ActualMb:0.00} MB bitrate={attempt.VideoBitrateK}k mode={attempt.Mode}");
        _output.WriteLine($"  result: success={result.Success} size={result.OutputMb:0.00} MB attempts={result.Attempts}");

        Assert.True(result.Success, result.Error);
        Assert.True(CodecModel.IsHardware(plan.Codec), $"Fast mode did not reach a hardware encoder; it planned {plan.Codec}.");
        Assert.True(result.OutputMb <= targetMb, $"The run delivered {result.OutputMb:0.00} MB, over the {targetMb:0.##} MB target.");
        Assert.True(result.Attempts < 3, $"The run needed {result.Attempts} attempts, spending the whole retry allowance.");
    }
}
