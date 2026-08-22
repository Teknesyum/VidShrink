using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class HardwareEncoderTests
{
    private static readonly string[] HardwareCodecs =
    {
        "h264_nvenc", "hevc_nvenc", "av1_nvenc",
        "h264_qsv", "hevc_qsv", "av1_qsv",
        "h264_amf", "hevc_amf", "av1_amf"
    };

    private static readonly string[] SoftwareCodecs = { "libx264", "libx265", "libvpx-vp9", "libsvtav1" };

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
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    private static EncodePlan TwoPassPlan(string codec) => new()
    {
        Codec = codec,
        Mode = "2pass",
        VideoBitrateK = 2000,
        AudioCodec = "aac",
        AudioBitrateK = 128,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        Preset = FfmpegArguments.DefaultPreset(codec),
        PixelFormat = "yuv420p"
    };

    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    public void NvencTwoPassDropsFakePassesAndUsesMultipass(string codec)
    {
        var args = FfmpegArguments.Build(SampleInfo(), TwoPassPlan(codec), "out.mp4", 2, "passlog");

        Assert.DoesNotContain("-pass", args);
        Assert.DoesNotContain("-passlogfile", args);
        Assert.Contains("-multipass", args);
        Assert.Equal("fullres", args[args.IndexOf("-multipass") + 1]);
        Assert.Contains("-rc", args);
        Assert.Equal("vbr", args[args.IndexOf("-rc") + 1]);
        Assert.Contains("-b:v", args);
    }

    [Theory]
    [InlineData("h264_amf")]
    [InlineData("hevc_amf")]
    [InlineData("av1_amf")]
    public void AmfTwoPassUsesPeakVariableBitrate(string codec)
    {
        var args = FfmpegArguments.Build(SampleInfo(), TwoPassPlan(codec), "out.mp4", 2, "passlog");

        Assert.DoesNotContain("-pass", args);
        Assert.DoesNotContain("-passlogfile", args);
        Assert.Equal("vbr_peak", args[args.IndexOf("-rc") + 1]);
    }

    [Fact]
    public void QsvTwoPassSkipsPassesAndOnlyH264UsesLookAhead()
    {
        var h264 = FfmpegArguments.Build(SampleInfo(), TwoPassPlan("h264_qsv"), "out.mp4", 2, "passlog");
        var av1 = FfmpegArguments.Build(SampleInfo(), TwoPassPlan("av1_qsv"), "out.mp4", 2, "passlog");

        Assert.DoesNotContain("-pass", h264);
        Assert.DoesNotContain("-passlogfile", h264);
        Assert.Equal("1", h264[h264.IndexOf("-look_ahead") + 1]);
        Assert.DoesNotContain("-look_ahead", av1);
        Assert.DoesNotContain("-rc", av1);
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libvpx-vp9")]
    public void SoftwareTwoPassKeepsBothPasses(string codec)
    {
        var first = FfmpegArguments.Build(SampleInfo(), TwoPassPlan(codec), "out.mp4", 1, "passlog");
        var second = FfmpegArguments.Build(SampleInfo(), TwoPassPlan(codec), "out.mp4", 2, "passlog");

        Assert.Equal("1", first[first.IndexOf("-pass") + 1]);
        Assert.Equal("2", second[second.IndexOf("-pass") + 1]);
        Assert.Contains("-passlogfile", second);
        Assert.DoesNotContain("-multipass", second);
    }

    [Fact]
    public void DefaultPresetIsValidForEveryHardwareCodec()
    {
        foreach (var codec in HardwareCodecs)
            Assert.True(FfmpegArguments.IsValidPreset(codec, FfmpegArguments.DefaultPreset(codec)), codec);
    }

    [Fact]
    public void HardwareAccelerationComesBeforeTheInputFile()
    {
        var args = FfmpegArguments.Build(SampleInfo(), TwoPassPlan("libx264"), "out.mp4", 2, "passlog");

        var hwaccel = args.IndexOf("-hwaccel");
        Assert.True(hwaccel >= 0);
        Assert.Equal("auto", args[hwaccel + 1]);
        Assert.True(hwaccel < args.IndexOf("-i"));
    }

    [Fact]
    public void NeedsTwoPassesSeparatesHardwareFromSoftware()
    {
        foreach (var codec in HardwareCodecs)
            Assert.False(FfmpegArguments.NeedsTwoPasses(codec), codec);
        foreach (var codec in SoftwareCodecs)
            Assert.True(FfmpegArguments.NeedsTwoPasses(codec), codec);
    }

    [Fact]
    public void HardwareFamilyIsRecognised()
    {
        foreach (var codec in HardwareCodecs)
        {
            Assert.True(CodecModel.IsHardware(codec), codec);
            Assert.True(CodecModel.UsesCq(codec), codec);
        }
        foreach (var codec in SoftwareCodecs)
            Assert.False(CodecModel.IsHardware(codec), codec);
    }

    [Fact]
    public void Av1NvencKeepsTheHigherQualityCeiling()
    {
        Assert.Equal(98.0, CodecModel.QualityLimit("av1_nvenc"));
        Assert.Equal(96.0, CodecModel.QualityLimit("hevc_nvenc"));
        Assert.Equal(96.0, CodecModel.QualityLimit("av1_amf"));
        Assert.Equal(99.0, CodecModel.QualityLimit("libx265"));
    }

    [Fact]
    public void NewHardwareCodecsCarryTheirBitrateNeed()
    {
        Assert.Equal(0.60, CodecModel.RelativeBitrateNeed("av1_nvenc"));
        Assert.Equal(0.62, CodecModel.RelativeBitrateNeed("av1_qsv"));
        Assert.Equal(0.66, CodecModel.RelativeBitrateNeed("av1_amf"));
        Assert.Equal(0.95, CodecModel.RelativeBitrateNeed("hevc_amf"));
        Assert.Equal(1.30, CodecModel.RelativeBitrateNeed("h264_amf"));
    }

    [Fact]
    public void Av1EncodersStayInTheAv1Family()
    {
        Assert.Equal(35, CodecModel.ReferenceCrf("av1_nvenc"));
        Assert.Equal(35, CodecModel.ReferenceCrf("av1_qsv"));
        Assert.Equal(35, CodecModel.ReferenceCrf("av1_amf"));
    }

    [Fact]
    public void PlanParserAcceptsTheNewHardwareCodecs()
    {
        foreach (var codec in HardwareCodecs)
        {
            var raw = $"{{\"codec\":\"{codec}\",\"mode\":\"2pass\",\"videoBitrateK\":2000,\"preset\":\"{FfmpegArguments.DefaultPreset(codec)}\",\"width\":1920,\"height\":1080,\"fps\":30}}";
            var result = PlanParser.Parse(raw, SampleInfo(), new PlanOptions { TargetMb = 40 });
            Assert.True(result.Ok, $"{codec}: {string.Join(", ", result.Errors)}");
        }
    }

    [Fact]
    public void Av1EncodersAreRejectedInMovButAcceptedInMp4()
    {
        foreach (var codec in new[] { "av1_nvenc", "av1_qsv", "av1_amf", "libsvtav1" })
        {
            var mov = new ConversionPlan { Container = "mov", VideoCodec = codec, QualityMode = ConversionQualityMode.Crf, Crf = 28 };
            var mp4 = new ConversionPlan { Container = "mp4", VideoCodec = codec, QualityMode = ConversionQualityMode.Crf, Crf = 28 };
            var mkv = new ConversionPlan { Container = "mkv", VideoCodec = codec, QualityMode = ConversionQualityMode.Crf, Crf = 28 };

            Assert.NotEmpty(ConversionArguments.Validate(SampleInfo(), mov));
            Assert.Empty(ConversionArguments.Validate(SampleInfo(), mp4));
            Assert.Empty(ConversionArguments.Validate(SampleInfo(), mkv));
        }
    }
}
