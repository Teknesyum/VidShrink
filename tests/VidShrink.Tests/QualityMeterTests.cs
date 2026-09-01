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
            Assert.Equal(100, score.VmafNegMean);
            Assert.True(score.Xpsnr is { } selfXpsnr && double.IsPositiveInfinity(selfXpsnr), $"expected infinite self XPSNR, got {score.Xpsnr}");
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public async Task Bt709MetadataOnlyRemuxStaysAtTheCeiling()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        if (!EncoderCapabilities.Instance.HasFilter("libvmaf") || !EncoderCapabilities.Instance.HasFilter("zscale")) return;

        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_qm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var source = Path.Combine(dir, "source.mp4");
            var tagged = Path.Combine(dir, "tagged.mp4");
            await EncodeLavfiAsync(source, crf: 18);
            await RemuxWithBt709TagsAsync(source, tagged);

            var score = await QualityMeter.MeasureAsync(source, tagged, CancellationToken.None);

            Assert.True(score.Comparable, score.Message);
            Assert.Equal(100, score.VmafNegMean);
            Assert.True(score.Xpsnr is { } remuxXpsnr && double.IsPositiveInfinity(remuxXpsnr), $"expected infinite remux XPSNR, got {score.Xpsnr}");
            Assert.Contains("bt709 limited", score.ColorNormalization);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public async Task HdrAndTonemappedSdrAreNotComparable()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_qm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var hdr = Path.Combine(dir, "hdr.mkv");
            var sdr = Path.Combine(dir, "sdr.mkv");
            await EncodeHdrAndTonemappedPairAsync(hdr, sdr);

            var score = await QualityMeter.MeasureAsync(hdr, sdr, CancellationToken.None);

            Assert.False(score.Comparable);
            Assert.Null(score.VmafNegMean);
            Assert.Null(score.Xpsnr);
            Assert.Contains("karşılaştırılamaz", score.Message);
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

    [Fact]
    public async Task ReferenceAndSampleMayUseDifferentWindowOffsets()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        if (!EncoderCapabilities.Instance.HasFilter("libvmaf") || !EncoderCapabilities.Instance.HasFilter("zscale")) return;

        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_qm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var reference = Path.Combine(dir, "reference.mp4");
            var sample = Path.Combine(dir, "sample.mkv");
            await RunFfmpegAsync(new[] { "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=10:duration=4", "-c:v", "libx264", "-g", "10", "-pix_fmt", "yuv420p", reference });
            await RunFfmpegAsync(new[] { "-y", "-ss", "1", "-t", "2", "-i", reference, "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", sample });

            var score = await QualityMeter.MeasureWindowAsync(reference, sample, 1, 0, 2);

            Assert.True(score.Comparable, score.Message);
            Assert.True(score.VmafNegMean > 95, $"offset window was not aligned: {score.VmafNegMean}");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
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

    private static Task RemuxWithBt709TagsAsync(string source, string output)
        => RunFfmpegAsync(new[] { "-y", "-i", source, "-map", "0", "-c", "copy", "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709", output });

    private static async Task EncodeHdrAndTonemappedPairAsync(string hdr, string sdr)
    {
        await EncodeLavfiAsync(sdr, crf: 18);
        await RunFfmpegAsync(new[]
        {
            "-y", "-i", sdr, "-map", "0", "-c", "copy",
            "-color_primaries", "bt2020", "-color_trc", "smpte2084", "-colorspace", "bt2020nc", hdr
        });
    }

    private static async Task RunFfmpegAsync(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = psi };
        process.Start();
        var drain = Task.WhenAll(process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync());
        await process.WaitForExitAsync();
        await drain;
        Assert.Equal(0, process.ExitCode);
    }
}
