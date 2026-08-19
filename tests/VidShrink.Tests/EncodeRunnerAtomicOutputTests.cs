using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class EncodeRunnerAtomicOutputTests
{
    [Fact]
    public async Task FailedEncodeLeavesNoFileAtFinalOutputNameOrAsPartial()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var info = new MediaInfo
        {
            FilePath = Path.Combine(Path.GetTempPath(), "vidshrink_missing_" + Guid.NewGuid().ToString("N") + ".mp4"),
            FileSizeBytes = 1024,
            DurationSeconds = 5,
            Width = 320,
            Height = 240,
            Fps = 24,
            VideoCodec = "h264",
            TotalBitrateBps = 500_000,
            AudioCodec = "aac",
            AudioBitrateBps = 128_000,
            AudioChannels = 2
        };
        var plan = new EncodePlan
        {
            Codec = "libx264",
            Mode = "crf",
            Crf = 30,
            VideoBitrateK = 500,
            AudioCodec = "aac",
            AudioBitrateK = 128,
            Width = 320,
            Height = 240,
            Fps = 24,
            Preset = "ultrafast"
        };
        var outputPath = Path.Combine(Path.GetTempPath(), "vidshrink_test_out_" + Guid.NewGuid().ToString("N") + ".mp4");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new EncodeRunner().RunAsync(info, plan, outputPath, 5, null, CancellationToken.None));

        Assert.False(File.Exists(outputPath));
        Assert.False(File.Exists(outputPath + ".partial"));
    }
}
