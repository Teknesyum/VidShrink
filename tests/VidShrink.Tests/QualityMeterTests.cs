using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class QualityMeterTests
{
    [FfmpegFact]
    public async Task IdenticalClipReportsTheModelCeilingInsteadOfAForcedHundred()
    {
        var dir = NewDir();
        try
        {
            var clip = Path.Combine(dir, "clip.mp4");
            await EncodeLavfiAsync(clip, crf: 23);

            var score = await QualityMeter.MeasureAsync(clip, clip, CancellationToken.None);

            Assert.NotNull(score.VmafNegMean);
            Assert.InRange(score.VmafNegMean!.Value, 99.0, 99.999);
            Assert.True(score.Xpsnr is { } selfXpsnr && double.IsPositiveInfinity(selfXpsnr), $"expected infinite self XPSNR, got {score.Xpsnr}");
        }
        finally { Cleanup(dir); }
    }

    [FfmpegFact]
    public async Task TwoNearLosslessRivalsKeepTheirOrderAboveTheCeilingBand()
    {
        var dir = NewDir();
        try
        {
            var reference = Path.Combine(dir, "reference.mp4");
            var rival = Path.Combine(dir, "rival.mp4");
            await EncodeLavfiAsync(reference, crf: 23);
            await RunFfmpegAsync(new[] { "-y", "-i", reference, "-c:v", "libx264", "-preset", "veryslow", "-crf", "10", "-pix_fmt", "yuv420p", rival });

            var identical = await QualityMeter.MeasureAsync(reference, reference, CancellationToken.None);
            var reencoded = await QualityMeter.MeasureAsync(reference, rival, CancellationToken.None);

            Assert.NotNull(identical.VmafNegMean);
            Assert.NotNull(reencoded.VmafNegMean);
            Assert.True(identical.VmafNegMean > 99.5 && reencoded.VmafNegMean > 99.5,
                $"both rivals should sit in the near-lossless band ({identical.VmafNegMean} / {reencoded.VmafNegMean})");
            Assert.True(reencoded.VmafNegMean < identical.VmafNegMean,
                $"the re-encoded rival must stay below the identical copy ({reencoded.VmafNegMean} vs {identical.VmafNegMean})");
        }
        finally { Cleanup(dir); }
    }

    [FfmpegFact]
    public async Task Bt709MetadataOnlyRemuxMatchesTheIdenticalCopyScore()
    {
        var dir = NewDir();
        try
        {
            var source = Path.Combine(dir, "source.mp4");
            var tagged = Path.Combine(dir, "tagged.mp4");
            await EncodeLavfiAsync(source, crf: 18);
            await RunFfmpegAsync(new[] { "-y", "-i", source, "-map", "0", "-c", "copy", "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709", tagged });

            var identical = await QualityMeter.MeasureAsync(source, source, CancellationToken.None);
            var score = await QualityMeter.MeasureAsync(source, tagged, CancellationToken.None);

            Assert.True(score.Comparable, score.Message);
            Assert.NotNull(identical.VmafNegMean);
            Assert.NotNull(score.VmafNegMean);
            Assert.Equal(identical.VmafNegMean!.Value, score.VmafNegMean!.Value, 3);
            Assert.True(score.Xpsnr is { } remuxXpsnr && double.IsPositiveInfinity(remuxXpsnr), $"expected infinite remux XPSNR, got {score.Xpsnr}");
            Assert.Contains("bt709 limited", score.ColorNormalization);
        }
        finally { Cleanup(dir); }
    }

    [FfmpegFact]
    public async Task HdrAndTonemappedSdrAreNotComparable()
    {
        var dir = NewDir();
        try
        {
            var hdr = Path.Combine(dir, "hdr.mkv");
            var sdr = Path.Combine(dir, "sdr.mkv");
            await EncodeLavfiAsync(sdr, crf: 18);
            await RunFfmpegAsync(new[]
            {
                "-y", "-i", sdr, "-map", "0", "-c", "copy",
                "-color_primaries", "bt2020", "-color_trc", "smpte2084", "-colorspace", "bt2020nc", hdr
            });

            var score = await QualityMeter.MeasureAsync(hdr, sdr, CancellationToken.None);

            Assert.False(score.Comparable);
            Assert.Null(score.VmafNegMean);
            Assert.Null(score.Xpsnr);
            Assert.Contains("karşılaştırılamaz", score.Message);
        }
        finally { Cleanup(dir); }
    }

    [TonemapFact]
    public async Task TonemappedReferenceSeparatesTwoSdrQualities()
    {
        var dir = NewDir();
        try
        {
            var hdr = Path.Combine(dir, "hdr.mkv");
            var high = Path.Combine(dir, "high.mp4");
            var low = Path.Combine(dir, "low.mp4");
            await EncodeHdrAsync(hdr);
            await EncodeTonemappedSdrAsync(hdr, high, new[] { "-crf", "16" });
            await EncodeTonemappedSdrAsync(hdr, low, new[] { "-b:v", "24k", "-maxrate", "32k", "-bufsize", "64k" });

            var plain = await QualityMeter.MeasureAsync(hdr, high, CancellationToken.None);
            var highScore = await QualityMeter.MeasureTonemappedReferenceAsync(hdr, high, CancellationToken.None);
            var lowScore = await QualityMeter.MeasureTonemappedReferenceAsync(hdr, low, CancellationToken.None);

            Assert.False(plain.Comparable);

            Assert.True(highScore.Comparable, highScore.Message);
            Assert.True(lowScore.Comparable, lowScore.Message);
            Assert.True(highScore.TonemappedReference && lowScore.TonemappedReference);
            Assert.NotNull(highScore.VmafNegMean);
            Assert.NotNull(lowScore.VmafNegMean);
            Assert.NotNull(highScore.Xpsnr);
            Assert.NotNull(lowScore.Xpsnr);

            Assert.True(highScore.VmafNegMean > 70,
                $"tonemapped reference lost alignment with a high quality SDR output ({highScore.VmafNegMean})");
            Assert.True(lowScore.VmafNegMean < highScore.VmafNegMean - 20,
                $"tonemapped path is blind to bitrate ({lowScore.VmafNegMean} vs {highScore.VmafNegMean})");
            Assert.True(lowScore.Xpsnr < highScore.Xpsnr - 3,
                $"tonemapped XPSNR is blind to bitrate ({lowScore.Xpsnr} vs {highScore.Xpsnr})");
        }
        finally { Cleanup(dir); }
    }

    [FfmpegFact]
    public async Task HeavilyDegradedCopyScoresClearlyLowerThanTheOriginal()
    {
        var dir = NewDir();
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
        finally { Cleanup(dir); }
    }

    [FfmpegFact]
    public async Task ReferenceAndSampleMayUseDifferentWindowOffsets()
    {
        var dir = NewDir();
        try
        {
            var reference = Path.Combine(dir, "reference.mp4");
            var sample = Path.Combine(dir, "sample.mkv");
            await RunFfmpegAsync(new[] { "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=10:duration=4", "-c:v", "libx264", "-g", "10", "-pix_fmt", "yuv420p", reference });
            await RunFfmpegAsync(new[] { "-y", "-ss", "1", "-t", "2", "-i", reference, "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", sample });

            var score = await QualityMeter.MeasureWindowAsync(reference, sample, 1, 0, 2);

            Assert.True(score.Comparable, score.Message);
            Assert.True(score.VmafNegMean > 95, $"offset window was not aligned: {score.VmafNegMean}");
            Assert.NotNull(score.WorstSceneStartSeconds);
            Assert.InRange(score.WorstSceneStartSeconds!.Value, 1.0, 3.0);
        }
        finally { Cleanup(dir); }
    }

    [FfmpegFact]
    public async Task WorstSceneFindsTheDamagedSectionTheMeanHides()
    {
        var dir = NewDir();
        try
        {
            var reference = Path.Combine(dir, "reference.mp4");
            var damaged = Path.Combine(dir, "damaged.mp4");
            await RunFfmpegAsync(new[] { "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=30:duration=8", "-c:v", "libx264", "-crf", "16", "-pix_fmt", "yuv420p", reference });
            await RunFfmpegAsync(new[]
            {
                "-y", "-i", reference, "-vf", "boxblur=12:3:enable='gte(t,6)'",
                "-c:v", "libx264", "-crf", "14", "-pix_fmt", "yuv420p", damaged
            });

            var score = await QualityMeter.MeasureAsync(reference, damaged, CancellationToken.None);

            Assert.True(score.Comparable, score.Message);
            Assert.NotNull(score.SceneWindowSeconds);
            Assert.NotNull(score.VmafNegMean);
            Assert.NotNull(score.VmafNegWorstScene);
            Assert.NotNull(score.WorstSceneStartSeconds);
            Assert.True(score.VmafNegWorstScene < score.VmafNegMean - 10,
                $"the scene floor did not drop below the clip mean ({score.VmafNegWorstScene} vs {score.VmafNegMean})");
            Assert.InRange(score.WorstSceneStartSeconds!.Value, 5.0, 7.0);
            Assert.Equal(0.0, score.WorstSceneStartSeconds!.Value % score.SceneWindowSeconds!.Value, 6);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void WorstSceneAveragesOverTwoSecondBuckets()
    {
        var scores = Enumerable.Repeat(100.0, 600).ToArray();
        for (var i = 120; i < 180; i++) scores[i] = 0.0;

        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0);

        Assert.Equal(50.0, worst, 6);
        Assert.Equal(2.0, at, 6);
    }

    [Fact]
    public void WorstSceneReportsTheWindowStartOnTheReferenceTimeline()
    {
        var scores = Enumerable.Repeat(100.0, 600).ToArray();
        for (var i = 360; i < 480; i++) scores[i] = 40.0;

        var (worst, at) = QualityMeter.WorstScene(scores, 60, 12.5);

        Assert.Equal(40.0, worst, 6);
        Assert.Equal(18.5, at, 6);
    }

    [Fact]
    public void WorstSceneFallsBackToTheWholeClipWhenItIsShorterThanOneWindow()
    {
        var scores = new[] { 80.0, 60.0, 40.0 };

        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0);

        Assert.Equal(60.0, worst, 6);
        Assert.Equal(0.0, at, 6);
    }

    [Fact]
    public void WorstSceneUsesSceneBoundariesWhenTheMapIsPresent()
    {
        var scores = Enumerable.Repeat(100.0, 600).ToArray();
        for (var i = 150; i < 330; i++) scores[i] = 40.0;

        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0, MapWithCuts(10.0, 2.5, 5.5));

        Assert.Equal(40.0, worst, 6);
        Assert.Equal(2.5, at, 6);
    }

    [Fact]
    public void FixedWindowsDiluteTheSceneTheMapWouldIsolate()
    {
        var scores = Enumerable.Repeat(100.0, 600).ToArray();
        for (var i = 150; i < 330; i++) scores[i] = 40.0;

        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0, null);

        Assert.Equal(55.0, worst, 6);
        Assert.Equal(2.0, at, 6);
    }

    [Fact]
    public void MapWithASingleSceneFallsBackToTheFixedWindow()
    {
        var scores = Enumerable.Repeat(100.0, 600).ToArray();
        for (var i = 120; i < 240; i++) scores[i] = 0.0;

        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0, MapWithCuts(10.0));

        Assert.Equal(0.0, worst, 6);
        Assert.Equal(2.0, at, 6);
    }

    [Fact]
    public void SceneBoundariesAreReadOnTheReferenceTimelineNotFromZero()
    {
        var scores = Enumerable.Repeat(100.0, 600).ToArray();
        for (var i = 150; i < 330; i++) scores[i] = 40.0;

        var (worst, at) = QualityMeter.WorstScene(scores, 60, 12.5, MapWithCuts(30.0, 15.0, 18.0));

        Assert.Equal(40.0, worst, 6);
        Assert.Equal(15.0, at, 6);
    }

    [Fact]
    public void CollapseInTheTrailingHalfSecondIsNotDropped()
    {
        var scores = Enumerable.Repeat(100.0, 990).ToArray();
        for (var i = 960; i < 990; i++) scores[i] = 0.0;

        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0);

        Assert.Equal(0.0, worst, 6);
        Assert.Equal(16.0, at, 6);
    }

    [Fact]
    public void TrailingUnitShorterThanHalfASecondIsDropped()
    {
        var scores = Enumerable.Repeat(100.0, 975).ToArray();
        for (var i = 960; i < 975; i++) scores[i] = 0.0;

        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0);

        Assert.Equal(100.0, worst, 6);
        Assert.Equal(0.0, at, 6);
    }

    [Fact]
    public void SceneShorterThanHalfASecondIsNotTheWorstScene()
    {
        var scores = Enumerable.Repeat(100.0, 600).ToArray();
        for (var i = 0; i < 12; i++) scores[i] = 0.0;
        for (var i = 150; i < 330; i++) scores[i] = 40.0;

        var (worst, at) = QualityMeter.WorstScene(scores, 60, 12.5, MapWithCuts(30.0, 12.7, 15.0, 18.0));

        Assert.Equal(40.0, worst, 6);
        Assert.Equal(15.0, at, 6);
    }

    [Fact]
    public void WorstSceneRejectsAnEmptyScoreList()
        => Assert.Throws<ArgumentException>(() => QualityMeter.WorstScene(Array.Empty<double>(), 60, 0));

    private static SceneMap MapWithCuts(double duration, params double[] cuts)
        => SceneMap.Build(
            duration,
            cuts.Select(c => new SceneScore(c, 1.0)).ToArray(),
            SceneMap.DefaultThreshold,
            Array.Empty<ProbeFrame>());

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_qm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { }
    }

    private static Task EncodeLavfiAsync(string outputPath, int crf)
        => RunFfmpegAsync(new[]
        {
            "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=10:duration=2",
            "-c:v", "libx264", "-crf", crf.ToString(), "-pix_fmt", "yuv420p", outputPath
        });

    private static Task EncodeHdrAsync(string outputPath)
        => RunFfmpegAsync(new[]
        {
            "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=10:duration=3",
            "-vf", "zscale=min=bt709:tin=bt709:pin=bt709:rin=limited:m=bt2020nc:t=smpte2084:p=bt2020:r=limited,format=yuv420p10le",
            "-c:v", "libx264", "-crf", "12", "-pix_fmt", "yuv420p10le",
            "-color_primaries", "bt2020", "-color_trc", "smpte2084", "-colorspace", "bt2020nc",
            outputPath
        });

    private static Task EncodeTonemappedSdrAsync(string hdrPath, string outputPath, string[] rateControl)
    {
        var args = new List<string> { "-y", "-i", hdrPath, "-vf", HdrResolver.TonemapFilter, "-c:v", "libx264" };
        args.AddRange(rateControl);
        args.AddRange(new[] { "-pix_fmt", "yuv420p", outputPath });
        return RunFfmpegAsync(args);
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
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        Assert.True(process.ExitCode == 0, await stderr);
    }
}
