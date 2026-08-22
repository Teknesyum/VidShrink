using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class HdrArgumentsTests
{
    private sealed class FakeAvailability : IEncoderAvailability
    {
        private readonly HashSet<string> _encoders;
        public FakeAvailability(params string[] encoders) => _encoders = new HashSet<string>(encoders, StringComparer.OrdinalIgnoreCase);
        public bool HasEncoder(string name) => _encoders.Contains(name);
        public bool WorksAsEncoder(string codec) => _encoders.Contains(codec);
    }

    private static MediaInfo BaseInfo() => new()
    {
        FilePath = "sample.mp4",
        FileSizeBytes = 500L * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "hevc",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    private static MediaInfo Hdr10Info() => BaseInfo() with
    {
        PixelFormat = "yuv420p10le",
        BitDepth = 10,
        ColorPrimaries = "bt2020",
        ColorTransfer = "smpte2084",
        ColorSpace = "bt2020nc",
        IsHdr = true
    };

    private static MediaInfo HlgInfo() => BaseInfo() with
    {
        PixelFormat = "yuv420p10le",
        BitDepth = 10,
        ColorPrimaries = "bt2020",
        ColorTransfer = "arib-std-b67",
        ColorSpace = "bt2020nc",
        IsHdr = true
    };

    private static MediaInfo SdrInfo() => BaseInfo() with
    {
        PixelFormat = "yuv420p",
        BitDepth = 8,
        ColorPrimaries = "bt709",
        ColorTransfer = "bt709",
        ColorSpace = "bt709",
        IsHdr = false,
        VideoCodec = "h264"
    };

    [Fact]
    public void Hdr10SourcePreservedWhenLibx265Available()
    {
        var options = new PlanOptions { TargetMb = 40, Intent = Intent.Sharing, Codec = CodecPreference.MaxCompression, HdrPolicy = HdrPolicy.Preserve };
        var availability = new FakeAvailability("libx264", "libx265");

        var result = PlanCalculator.BuildDetailed(Hdr10Info(), options, null, availability);
        var args = FfmpegArguments.Build(Hdr10Info(), result.Plan, "out.mp4", 0, null);

        Assert.Equal("libx265", result.Plan.Codec);
        Assert.Equal("yuv420p10le", result.Plan.PixelFormat);
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.HdrTonemapped);
        Assert.Contains("-pix_fmt", args);
        Assert.Contains("yuv420p10le", args);
        Assert.Contains("-color_primaries", args);
        Assert.Contains("bt2020", args);
        Assert.Contains("-color_trc", args);
        Assert.Contains("smpte2084", args);
        Assert.Contains("-colorspace", args);
        Assert.Contains("-x265-params", args);
        Assert.Contains(args, a => a.Contains("hdr10-opt=1"));
    }

    [Fact]
    public void HlgSourcePreservesTransferCharacteristic()
    {
        var options = new PlanOptions { TargetMb = 40, Intent = Intent.Sharing, Codec = CodecPreference.MaxCompression, HdrPolicy = HdrPolicy.Preserve };
        var availability = new FakeAvailability("libx264", "libx265");

        var result = PlanCalculator.BuildDetailed(HlgInfo(), options, null, availability);
        var args = FfmpegArguments.Build(HlgInfo(), result.Plan, "out.mp4", 0, null);

        Assert.Equal("yuv420p10le", result.Plan.PixelFormat);
        Assert.Contains("-color_trc", args);
        Assert.Contains("arib-std-b67", args);
    }

    [Fact]
    public void HdrSourceFallsBackToTonemapWhenEncoderLacks10Bit()
    {
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Compatible, HdrPolicy = HdrPolicy.Preserve };
        var availability = new FakeAvailability("libx264");

        var result = PlanCalculator.BuildDetailed(Hdr10Info(), options, null, availability);
        var args = FfmpegArguments.Build(Hdr10Info(), result.Plan, "out.mp4", 0, null);

        Assert.Equal("libx264", result.Plan.Codec);
        Assert.Equal("yuv420p", result.Plan.PixelFormat);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.HdrTonemapped);
        Assert.Contains("-vf", args);
        Assert.Contains(args, a => a.Contains("tonemap=hable"));
        Assert.Contains("-color_primaries", args);
        Assert.Contains("bt709", args);
    }

    [Fact]
    public void SdrSourceUnaffectedByHdrHandling()
    {
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Compatible, HdrPolicy = HdrPolicy.Preserve };

        var result = PlanCalculator.BuildDetailed(SdrInfo(), options, null, null);
        var args = FfmpegArguments.Build(SdrInfo(), result.Plan, "out.mp4", 0, null);

        Assert.Equal("yuv420p", result.Plan.PixelFormat);
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.HdrTonemapped);
        Assert.Contains("-pix_fmt", args);
        Assert.Contains("yuv420p", args);
        Assert.DoesNotContain("-color_primaries", args);
        Assert.DoesNotContain("-x265-params", args);
    }

    [Fact]
    public void ConversionArgumentsPreserveHdrForLibx265()
    {
        var plan = new ConversionPlan { Container = "mp4", VideoCodec = "libx265", AudioCodec = "aac", HdrPolicy = HdrPolicy.Preserve };

        var args = ConversionArguments.Build(Hdr10Info(), plan, "out.mp4");

        Assert.Contains("-pix_fmt", args);
        Assert.Contains("yuv420p10le", args);
        Assert.Contains("-x265-params", args);
    }

    [Fact]
    public void ConversionArgumentsTonemapWhenPolicyRequestsSdr()
    {
        var plan = new ConversionPlan { Container = "mp4", VideoCodec = "libx265", AudioCodec = "aac", HdrPolicy = HdrPolicy.TonemapToSdr };

        var args = ConversionArguments.Build(Hdr10Info(), plan, "out.mp4");

        Assert.Contains("-pix_fmt", args);
        Assert.Contains("yuv420p", args);
        Assert.DoesNotContain("yuv420p10le", args);
        Assert.Contains(args, a => a.Contains("tonemap=hable"));
    }
}
