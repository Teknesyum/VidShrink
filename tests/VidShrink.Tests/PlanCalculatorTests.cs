using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class PlanCalculatorTests
{
    private sealed class FakeAvailability : IEncoderAvailability
    {
        private readonly HashSet<string> _encoders;
        public FakeAvailability(params string[] encoders) => _encoders = new HashSet<string>(encoders, StringComparer.OrdinalIgnoreCase);
        public bool HasEncoder(string name) => _encoders.Contains(name);
        public bool WorksAsEncoder(string codec) => _encoders.Contains(codec);
    }

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

    [Fact]
    public void MissingLibsvtav1FallsBackToLibx265AndExplainsWhy()
    {
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.MaxCompression };
        var availability = new FakeAvailability("libx264", "libx265", "h264_nvenc");

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, availability);

        Assert.Equal("libx265", result.Plan.Codec);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback && n.RequestedCodec == "libsvtav1" && n.FallbackCodec == "libx265");
        Assert.Contains("libx265", result.Plan.Reason);
    }

    [Fact]
    public void Libsvtav1AvailablePicksItForMaxCompression()
    {
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.MaxCompression };
        var availability = new FakeAvailability("libx264", "libx265", "libsvtav1");

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, availability);

        Assert.Equal("libsvtav1", result.Plan.Codec);
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback);
    }

    [Fact]
    public void CorrectionReservesFixedAudioBeforeScalingVideo()
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
    public void BuildDetailedProducesReasonCodesAlongsideReasonText()
    {
        var info = new MediaInfo
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

        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing };
        var result = PlanCalculator.BuildDetailed(info, options, null);

        Assert.NotEmpty(result.Plan.ReasonCodes);
        Assert.False(string.IsNullOrWhiteSpace(result.Plan.Reason));
    }
}
