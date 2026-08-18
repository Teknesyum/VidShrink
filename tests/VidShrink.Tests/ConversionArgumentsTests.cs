using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class ConversionArgumentsTests
{
    private static readonly MediaInfo Source = new()
    {
        FilePath = @"C:\media\source.mp4",
        FileSizeBytes = 20 * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1_400_000,
        AudioCodec = "aac",
        AudioChannels = 2
    };

    [Fact]
    public void GifSecondPassWithoutScalingUsesSourceAndPaletteInputs()
    {
        var plan = new ConversionPlan { Container = "gif", VideoCodec = "libx264", AudioCodec = null };

        var args = ConversionArguments.Build(Source, plan, "out.gif", "palette.png");

        var graph = args[args.IndexOf("-lavfi") + 1];
        Assert.Equal("[0:v][1:v]paletteuse=dither=sierra2_4a", graph);
    }

    [Fact]
    public void GifIgnoresSelectedAudioEncoderDuringValidation()
    {
        var plan = new ConversionPlan { Container = "gif", VideoCodec = "libx264", AudioCodec = "aac" };

        var errors = ConversionArguments.Validate(Source, plan);

        Assert.Empty(errors);
    }

    [Fact]
    public void GifSecondPassWithScalingLabelsFilteredVideo()
    {
        var plan = new ConversionPlan { Container = "gif", VideoCodec = "libx264", AudioCodec = null, Height = 720 };

        var args = ConversionArguments.Build(Source, plan, "out.gif", "palette.png");

        var graph = args[args.IndexOf("-lavfi") + 1];
        Assert.Equal("scale=-2:720:flags=lanczos[x];[x][1:v]paletteuse=dither=sierra2_4a", graph);
    }

    [Fact]
    public void ValidationRejectsOddDimensionsBeforeYuv420Encode()
    {
        var plan = new ConversionPlan { Width = 1279, Height = 719 };

        var errors = ConversionArguments.Validate(Source, plan);

        Assert.Contains(errors, error => error.Contains("must be even", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("webm", "libx264", "aac")]
    [InlineData("mp4", "libx264", "libopus")]
    public void ValidationRejectsContainerCodecMismatches(string container, string videoCodec, string audioCodec)
    {
        var plan = new ConversionPlan { Container = container, VideoCodec = videoCodec, AudioCodec = audioCodec };

        var errors = ConversionArguments.Validate(Source, plan);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidationRejectsTrimStartBeyondSource()
    {
        var plan = new ConversionPlan { Start = TimeSpan.FromSeconds(120) };

        var errors = ConversionArguments.Validate(Source, plan);

        Assert.Contains(errors, error => error.Contains("before the end", StringComparison.Ordinal));
    }
}

internal static class ArgumentListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++)
            if (values[index] == value) return index;
        return -1;
    }
}
