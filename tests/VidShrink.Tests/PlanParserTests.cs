using System.Text.Json;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class PlanParserTests
{
    private static readonly MediaInfo Source = new()
    {
        FilePath = @"C:\media\source.mp4",
        FileSizeBytes = 40 * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 2_700_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    [Fact]
    public void ParserRemovesOutputCreatingExtraArgumentsAsWholePairs()
    {
        var json = ValidPlan(new[] { "-f", "image2", "stolen.png", "-tune", "film" });

        var result = PlanParser.Parse(json, Source, new PlanOptions());

        Assert.True(result.Ok);
        Assert.Equal(new[] { "-tune", "film" }, result.Plan!.ExtraArgs);
        Assert.Contains(result.Warnings, warning => warning.Contains("-f", StringComparison.Ordinal));
    }

    [Fact]
    public void ParserHonorsDisabledResolutionAndFrameRateReduction()
    {
        var json = ValidPlan(Array.Empty<string>(), width: 1280, height: 720, fps: 24);
        var options = new PlanOptions { AllowResolutionDrop = false, AllowFpsDrop = false };

        var result = PlanParser.Parse(json, Source, options);

        Assert.True(result.Ok);
        Assert.Equal(1920, result.Plan!.Width);
        Assert.Equal(1080, result.Plan.Height);
        Assert.Equal(30, result.Plan.Fps);
    }

    [Fact]
    public void ParserRejectsAspectRatioDistortion()
    {
        var json = ValidPlan(Array.Empty<string>(), width: 1280, height: 800);

        var result = PlanParser.Parse(json, Source, new PlanOptions());

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("aspect ratio", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("hevc_videotoolbox")]
    [InlineData("h264_videotoolbox")]
    public void ParserStillRejectsVideoToolboxEncoders(string codec)
    {
        var json = ValidPlan(Array.Empty<string>(), codec: codec);

        var result = PlanParser.Parse(json, Source, new PlanOptions());

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error =>
            error.Contains("Unsupported codec", StringComparison.Ordinal)
            && error.Contains(codec, StringComparison.Ordinal));
    }

    private sealed class VtAvailability(EncoderProbeState state) : IEncoderAvailability
    {
        public bool HasEncoder(string name) => name == "hevc_videotoolbox";
        public bool WorksAsEncoder(string codec) => HasEncoder(codec) && state == EncoderProbeState.Working;
        public EncoderProbeState EncoderState(string codec) => HasEncoder(codec) ? state : EncoderProbeState.NotWorking;
    }

    [Fact]
    public void ParserAcceptsHevcVideoToolboxOnlyOnMacWhenProbeWorks()
    {
        var json = ValidPlan(Array.Empty<string>(), codec: "hevc_videotoolbox", preset: "");

        var kabul = PlanParser.Parse(json, Source, new PlanOptions(), new VtAvailability(EncoderProbeState.Working), macOS: true);
        Assert.True(kabul.Ok, string.Join(" | ", kabul.Errors));
        Assert.False(PlanParser.Parse(json, Source, new PlanOptions(), new VtAvailability(EncoderProbeState.Working), macOS: false).Ok);
        Assert.False(PlanParser.Parse(json, Source, new PlanOptions(), new VtAvailability(EncoderProbeState.NotWorking), macOS: true).Ok);
        Assert.False(PlanParser.Parse(json, Source, new PlanOptions(), null, macOS: true).Ok);
        Assert.False(PlanParser.Parse(ValidPlan(Array.Empty<string>(), codec: "h264_videotoolbox", preset: ""), Source, new PlanOptions(), new VtAvailability(EncoderProbeState.Working), macOS: true).Ok);
    }

    [Fact]
    public void ParserRejectsCrfForHevcVideoToolbox()
    {
        var json = ValidPlan(Array.Empty<string>(), codec: "hevc_videotoolbox", preset: "").Replace("\"2pass\"", "\"crf\"").Replace("\"videoBitrateK\"", "\"crf\":24,\"videoBitrateK\"");

        var result = PlanParser.Parse(json, Source, new PlanOptions(), new VtAvailability(EncoderProbeState.Working), macOS: true);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("does not take CRF", StringComparison.Ordinal));
    }

    private static string ValidPlan(string[] extraArgs, int width = 1920, int height = 1080, double fps = 30, string codec = "libx264", string preset = "slow")
        => JsonSerializer.Serialize(new
        {
            codec,
            mode = "2pass",
            videoBitrateK = 1200,
            crf = (int?)null,
            audioCodec = "aac",
            audioBitrateK = 128,
            width,
            height,
            fps,
            preset,
            extraArgs,
            reason = "test"
        });
}
