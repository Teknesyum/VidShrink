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
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(outputPath)!, "vidshrink_partial_*"));
    }

    [Fact]
    public void PartialPathKeepsTheOutputExtensionSoFfmpegCanPickAMuxer()
    {
        var partial = EncodeRunner.PartialPathFor(Path.Combine(Path.GetTempPath(), "clip.mp4"));

        Assert.Equal(".mp4", Path.GetExtension(partial));
        Assert.Equal(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(partial));
        Assert.StartsWith("vidshrink_partial_", Path.GetFileName(partial));
    }

    [Fact]
    public async Task SuccessfulEncodeProducesTheFinalFileAndLeavesNoPartial()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_atomic_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var source = Path.Combine(dir, "source.mp4");
        var outputPath = Path.Combine(dir, "result.mp4");
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ToolLocator.Ffmpeg,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in new[]
            {
                "-y", "-f", "lavfi", "-i", "testsrc=size=320x240:rate=10:duration=2",
                "-c:v", "libx264", "-crf", "28", "-pix_fmt", "yuv420p", source
            }) psi.ArgumentList.Add(arg);
            using var make = new System.Diagnostics.Process { StartInfo = psi };
            make.Start();
            var drain = Task.WhenAll(make.StandardOutput.ReadToEndAsync(), make.StandardError.ReadToEndAsync());
            await make.WaitForExitAsync();
            await drain;
            Assert.True(File.Exists(source));

            var info = new MediaInfo
            {
                FilePath = source,
                FileSizeBytes = new FileInfo(source).Length,
                DurationSeconds = 2,
                Width = 320,
                Height = 240,
                Fps = 10,
                VideoCodec = "h264",
                TotalBitrateBps = 400_000
            };
            var plan = new EncodePlan
            {
                Codec = "libx264",
                Mode = "crf",
                Crf = 30,
                VideoBitrateK = 200,
                AudioCodec = "aac",
                AudioBitrateK = 64,
                Width = 320,
                Height = 240,
                Fps = 10,
                Preset = "ultrafast"
            };

            var result = await new EncodeRunner().RunAsync(info, plan, outputPath, 5, null, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(File.Exists(outputPath));
            Assert.Empty(Directory.GetFiles(dir, "vidshrink_partial_*"));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }
}
