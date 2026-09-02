using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class HdrArgumentsTests
{
    private sealed class FakeAvailability : IEncoderAvailability, IHdr10EncoderAvailability
    {
        private readonly HashSet<string> _encoders;
        public FakeAvailability(params string[] encoders) => _encoders = new HashSet<string>(encoders, StringComparer.OrdinalIgnoreCase);
        public bool HasEncoder(string name) => _encoders.Contains(name);
        public bool WorksAsEncoder(string codec) => _encoders.Contains(codec);
        public string? Hdr10PixelFormat(string codec)
            => _encoders.Contains(codec) && CodecModel.IsHardware(codec) ? "p010le" : null;
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
    public void FastAv1NvencPreservesHdrWhenTenBitProbeSucceeds()
    {
        var options = new PlanOptions { TargetMb = 40, SpeedMode = SpeedMode.Fast, HdrPolicy = HdrPolicy.Preserve };
        var availability = new FakeAvailability("av1_nvenc", "libx265");

        var result = PlanCalculator.BuildDetailed(Hdr10Info(), options, null, availability);
        var args = FfmpegArguments.Build(Hdr10Info(), result.Plan, "out.mp4", 0, null);

        Assert.Equal("av1_nvenc", result.Plan.Codec);
        Assert.Equal("p010le", result.Plan.PixelFormat);
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.HdrTonemapped);
        Assert.DoesNotContain(args, a => a.Contains("tonemap="));
        Assert.Contains("smpte2084", args);
        Assert.Contains("bt2020", args);
    }

    [Fact]
    public void RemovingAv1NvencTenBitCapabilityMakesFastPathTonemap()
    {
        var options = new PlanOptions { TargetMb = 40, SpeedMode = SpeedMode.Fast, HdrPolicy = HdrPolicy.Preserve };
        var availability = new FakeAvailability("av1_nvenc");
        IHdr10EncoderAvailability mutation = new NoHdrAvailability();

        var result = PlanCalculator.BuildDetailed(Hdr10Info(), options, null, new MutatedAvailability(availability, mutation));

        Assert.Equal("av1_nvenc", result.Plan.Codec);
        Assert.Equal("yuv420p", result.Plan.PixelFormat);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.HdrTonemapped);
    }

    private sealed class NoHdrAvailability : IHdr10EncoderAvailability
    {
        public string? Hdr10PixelFormat(string codec) => null;
    }

    private sealed class MutatedAvailability(IEncoderAvailability encoders, IHdr10EncoderAvailability hdr) : IEncoderAvailability, IHdr10EncoderAvailability
    {
        public bool HasEncoder(string name) => encoders.HasEncoder(name);
        public bool WorksAsEncoder(string codec) => encoders.WorksAsEncoder(codec);
        public string? Hdr10PixelFormat(string codec) => hdr.Hdr10PixelFormat(codec);
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

    [Fact]
    public void ProbeAnswerDecidesFastPathPixelFormatNotTheCodecName()
    {
        var options = new PlanOptions { TargetMb = 40, SpeedMode = SpeedMode.Fast, HdrPolicy = HdrPolicy.Preserve };
        var encoders = new FakeAvailability("av1_nvenc");

        var p010 = PlanCalculator.BuildDetailed(Hdr10Info(), options, null,
            new MutatedAvailability(encoders, new FixedHdrAvailability("p010le")));
        var planar = PlanCalculator.BuildDetailed(Hdr10Info(), options, null,
            new MutatedAvailability(encoders, new FixedHdrAvailability("yuv420p10le")));

        Assert.Equal("av1_nvenc", p010.Plan.Codec);
        Assert.Equal("av1_nvenc", planar.Plan.Codec);
        Assert.Equal("p010le", p010.Plan.PixelFormat);
        Assert.Equal("yuv420p10le", planar.Plan.PixelFormat);
        Assert.Contains("p010le", FfmpegArguments.Build(Hdr10Info(), p010.Plan, "out.mp4", 0, null));
        Assert.Contains("yuv420p10le", FfmpegArguments.Build(Hdr10Info(), planar.Plan, "out.mp4", 0, null));
    }

    [Fact]
    public void SoftwareHdr10SetMembershipDecidesPreservationWhenProbeSaysNothing()
    {
        IHdr10EncoderAvailability silentProbe = new NoHdrAvailability();

        var inSet = HdrResolver.Resolve(Hdr10Info(), HdrPolicy.Preserve, "libx265",
            new MutatedAvailability(new FakeAvailability("libx265"), silentProbe));
        var outOfSet = HdrResolver.Resolve(Hdr10Info(), HdrPolicy.Preserve, "libx264",
            new MutatedAvailability(new FakeAvailability("libx264"), silentProbe));

        Assert.False(inSet.PolicyChanged);
        Assert.Equal("yuv420p10le", inSet.PixelFormat);
        Assert.Null(inSet.VideoFilter);

        Assert.True(outOfSet.PolicyChanged);
        Assert.Equal("yuv420p", outOfSet.PixelFormat);
        Assert.Contains("tonemap=hable", outOfSet.VideoFilter!);
    }

    [Fact]
    public void RequestedTonemapIsNotReportedAsAPolicyChange()
    {
        var availability = new FakeAvailability("av1_nvenc");

        var forced = HdrResolver.Resolve(Hdr10Info(), HdrPolicy.Preserve, "libx264", new FakeAvailability("libx264"));
        var asked = HdrResolver.Resolve(Hdr10Info(), HdrPolicy.TonemapToSdr, "av1_nvenc", availability);

        Assert.Contains("tonemap=hable", forced.VideoFilter!);
        Assert.Contains("tonemap=hable", asked.VideoFilter!);
        Assert.True(forced.PolicyChanged);
        Assert.False(asked.PolicyChanged);
    }

    [Fact]
    public void DroppedHdrReachesTheUserAsAPlanLineInBothLanguages()
    {
        var options = new PlanOptions { TargetMb = 40, SpeedMode = SpeedMode.Fast, HdrPolicy = HdrPolicy.Preserve };
        var availability = new MutatedAvailability(new FakeAvailability("av1_nvenc"), new NoHdrAvailability());

        var dropped = PlanCalculator.BuildDetailed(Hdr10Info(), options, null, availability);
        var kept = PlanCalculator.BuildDetailed(Hdr10Info(), options, null, new FakeAvailability("av1_nvenc"));

        Assert.Contains(AdviceCode.HdrTonemapped, dropped.Advice.Notes);
        Assert.DoesNotContain(AdviceCode.HdrTonemapped, kept.Advice.Notes);

        var turkish = MainWindow.AdviceLine(AdviceCode.HdrTonemapped, language: "tr", fastGpu: false);
        var english = MainWindow.AdviceLine(AdviceCode.HdrTonemapped, language: "en", fastGpu: false);

        Assert.False(string.IsNullOrWhiteSpace(turkish));
        Assert.False(string.IsNullOrWhiteSpace(english));
        Assert.NotEqual(turkish, english);
    }

    [Theory]
    [InlineData(0, "", true)]
    [InlineData(0, "x265 [warning]: Source height < 720p; disabling lookahead-slices\n", true)]
    [InlineData(0, "Incompatible pixel format 'p010le' for codec 'libx265', auto-selecting format 'yuv420p10le'\n", false)]
    [InlineData(0, "auto-selecting format 'yuv420p10le'\n", false)]
    [InlineData(1, "", false)]
    public void SilentPixelFormatConversionIsNotAcceptance(int exitCode, string diagnostic, bool expected)
        => Assert.Equal(expected, EncoderCapabilities.PixelFormatAccepted(exitCode, diagnostic));

    [FfmpegFact]
    public void RealEncoderRefusesP010leForLibx265AndTheGateReadsThatRefusal()
    {
        Assert.True(EncoderCapabilities.Instance.HasEncoder("libx265"), "libx265 bu ffmpeg derlemesinde yok; olcu kosturulamaz.");

        var refused = EncodeOneFrame("libx265", "p010le");
        var accepted = EncodeOneFrame("libx265", "yuv420p10le");

        Assert.Contains("Incompatible pixel format", refused, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Incompatible pixel format", accepted, StringComparison.OrdinalIgnoreCase);

        Assert.False(EncoderCapabilities.PixelFormatAccepted(0, refused));
        Assert.True(EncoderCapabilities.PixelFormatAccepted(0, accepted));
    }

    private static string EncodeOneFrame(string codec, string pixelFormat)
    {
        var args = string.Join(' ',
            "-hide_banner", "-loglevel", "warning",
            "-f", "lavfi", "-i", "testsrc2=size=256x256:rate=30:duration=0.1",
            "-vf", $"format={pixelFormat}", "-c:v", codec, "-pix_fmt", pixelFormat,
            "-color_primaries", "bt2020", "-color_trc", "smpte2084", "-colorspace", "bt2020nc",
            "-frames:v", "1", "-f", "null", OperatingSystem.IsWindows() ? "NUL" : "/dev/null");

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("ffmpeg", args)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return stderr;
    }

    private sealed class FixedHdrAvailability(string pixelFormat) : IHdr10EncoderAvailability
    {
        public string? Hdr10PixelFormat(string codec) => pixelFormat;
    }
}
