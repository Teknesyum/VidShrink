using System.Diagnostics;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class QualityMeterTests
{
    [Fact]
    public async Task SameClipComparedToItselfScoresHighVmaf()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        if (!EncoderCapabilities.Instance.HasFilter("libvmaf")) return;

        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_qm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var clip = Path.Combine(dir, "clip.mp4");
            await EncodeLavfiAsync(clip, crf: 23);

            var score = await QualityMeter.MeasureAsync(clip, clip, CancellationToken.None);

            Assert.NotNull(score.VmafNegMean);
            Assert.True(score.VmafNegMean >= 95, $"expected VMAF NEG >= 95, got {score.VmafNegMean}");
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public async Task HeavilyDegradedCopyScoresClearlyLowerThanTheOriginal()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        if (!EncoderCapabilities.Instance.HasFilter("libvmaf")) return;

        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_qm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var reference = Path.Combine(dir, "reference.mp4");
            var degraded = Path.Combine(dir, "degraded.mp4");
            await EncodeLavfiAsync(reference, crf: 18);
            await EncodeLavfiAsync(degraded, crf: 51);

            var selfScore = await QualityMeter.MeasureAsync(reference, reference, CancellationToken.None);
            var degradedScore = await QualityMeter.MeasureAsync(reference, degraded, CancellationToken.None);

            Assert.NotNull(selfScore.VmafNegMean);
            Assert.NotNull(degradedScore.VmafNegMean);
            Assert.True(degradedScore.VmafNegMean < selfScore.VmafNegMean - 20,
                $"expected the degraded copy to score well below the original ({degradedScore.VmafNegMean} vs {selfScore.VmafNegMean})");
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    private static async Task EncodeLavfiAsync(string outputPath, int crf)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=10:duration=2",
            "-c:v", "libx264", "-crf", crf.ToString(), "-pix_fmt", "yuv420p", outputPath
        }) psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        process.Start();
        var drain = Task.WhenAll(process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync());
        await process.WaitForExitAsync();
        await drain;

        Assert.True(File.Exists(outputPath));
    }
}
