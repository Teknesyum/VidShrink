using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class EncoderStateConsumptionTests
{
    private sealed class StateAvailability(params (string Codec, EncoderProbeState State)[] answers) : IEncoderAvailability
    {
        private readonly Dictionary<string, EncoderProbeState> _answers = answers.ToDictionary(
            answer => answer.Codec,
            answer => answer.State,
            StringComparer.OrdinalIgnoreCase);

        public bool HasEncoder(string name) => _answers.ContainsKey(name);
        public bool WorksAsEncoder(string codec) =>
            _answers.TryGetValue(codec, out var state) && state == EncoderProbeState.Working;

        public EncoderProbeState EncoderState(string codec) =>
            _answers.TryGetValue(codec, out var state) ? state : EncoderProbeState.NotWorking;
    }

    private static MediaInfo SdrSource() => new()
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

    [Fact]
    public void PickCodecOlculmemisTercihiElemedenGeciriyor()
    {
        var availability = new StateAvailability(("libsvtav1", EncoderProbeState.Unmeasured));

        var result = PlanCalculator.BuildDetailed(
            SdrSource(),
            new PlanOptions { TargetMb = 25, Codec = CodecPreference.MaxCompression },
            null,
            availability);

        Assert.Equal("libsvtav1", result.Plan.Codec);
        Assert.True(result.HardwareNotMeasured);
    }

    [Fact]
    public void PickFastCodecOlculmemisIlkAdayiElemedenGeciriyor()
    {
        var availability = new StateAvailability(("av1_nvenc", EncoderProbeState.Unmeasured));

        var result = PlanCalculator.BuildDetailed(
            SdrSource(),
            new PlanOptions { TargetMb = 25, SpeedMode = SpeedMode.Fast },
            null,
            availability);

        Assert.Equal("av1_nvenc", result.Plan.Codec);
        Assert.True(result.HardwareNotMeasured);
    }

    [Fact]
    public void PerformanceProbeOlculmemisAdayiCalismiyorSaymiyor()
    {
        var availability = new StateAvailability(
            ("h264_nvenc", EncoderProbeState.NotWorking),
            ("h264_qsv", EncoderProbeState.Unmeasured),
            ("h264_amf", EncoderProbeState.Working));

        var selected = PerformanceProbe.SelectHardwareCodec(availability, Stopwatch.StartNew(), 0);

        Assert.Equal("h264_qsv", selected);
    }
}

public sealed class HdrResolverTests
{
    private sealed class UnmeasuredAvailability : IEncoderAvailability
    {
        public bool HasEncoder(string name) => name == "libsvtav1";
        public bool WorksAsEncoder(string codec) => false;
        public EncoderProbeState EncoderState(string codec) => EncoderProbeState.Unmeasured;
    }

    private static MediaInfo HdrSource() => new()
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
        AudioChannels = 2,
        PixelFormat = "yuv420p10le",
        BitDepth = 10,
        ColorPrimaries = "bt2020",
        ColorTransfer = "smpte2084",
        ColorSpace = "bt2020nc",
        IsHdr = true
    };

    [Fact]
    public void SoftwareHdrOlculmemisKodlayiciyiElemedenGeciriyor()
    {
        var result = HdrResolver.Resolve(
            HdrSource(),
            HdrPolicy.Preserve,
            "libsvtav1",
            new UnmeasuredAvailability());

        Assert.False(result.PolicyChanged);
        Assert.True(result.NotMeasured);
        Assert.Equal("yuv420p10le", result.PixelFormat);
    }
}
