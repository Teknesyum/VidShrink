using System.Diagnostics;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public sealed class OutputQualityCheckTests
{
    [Fact]
    public void PicksVmafThenSsimThenPsnrAndNothingWithoutZscale()
    {
        Assert.Null(OutputQualityCheck.Pick(name => name != "zscale"));
        Assert.Equal(OutputQualityMetric.Vmaf, OutputQualityCheck.Pick(_ => true));
        Assert.Equal(OutputQualityMetric.Ssim, OutputQualityCheck.Pick(name => name is "zscale" or "ssim" or "psnr"));
        Assert.Equal(OutputQualityMetric.Psnr, OutputQualityCheck.Pick(name => name is "zscale" or "psnr"));
        Assert.Null(OutputQualityCheck.Pick(name => name is "zscale"));
    }

    [Fact]
    public void ShortVideoIsMeasuredWholeAndLongVideoIsSampledToTheFrameBudget()
    {
        Assert.Equal(1, OutputQualityCheck.Subsample(20, 30));
        Assert.Equal(2, OutputQualityCheck.Subsample(21, 30));
        Assert.Equal(30, OutputQualityCheck.Subsample(600, 30));
        Assert.Equal(2, OutputQualityCheck.Subsample(25, 0));
        Assert.Equal(1, OutputQualityCheck.Subsample(double.NaN, 30));
        Assert.True(600 * 30 / OutputQualityCheck.Subsample(600, 30) <= OutputQualityCheck.FrameBudget);
    }

    [Fact]
    public void VmafArgumentsUseTwoThreadsAndLibvmafSubsampling()
    {
        var args = OutputQualityCheck.Arguments("out.mp4", "src.mp4", "zscale=a", "zscale=b", OutputQualityMetric.Vmaf, 7);

        Assert.Equal(new[] { "-threads", "2", "-i", "out.mp4" }, args.Skip(2).Take(4));
        Assert.Equal(new[] { "-threads", "2", "-i", "src.mp4" }, args.Skip(6).Take(4));
        Assert.Equal("2", args[args.ToList().IndexOf("-filter_threads") + 1]);
        var graph = args[args.ToList().IndexOf("-lavfi") + 1];
        Assert.Contains("libvmaf=model=version=vmaf_v0.6.1neg:n_threads=2:n_subsample=7", graph);
        Assert.DoesNotContain("framestep", graph);
        Assert.StartsWith("[0:v]zscale=a,", graph);
    }

    [Fact]
    public void SsimAndPsnrSampleBothSidesWithTheSameFramestep()
    {
        var ssim = OutputQualityCheck.Arguments("out.mp4", "src.mp4", "zscale=a", "zscale=b", OutputQualityMetric.Ssim, 3);
        var graph = ssim[ssim.ToList().IndexOf("-lavfi") + 1];
        Assert.Contains("[0:v]zscale=a,framestep=3,", graph);
        Assert.Contains("[1:v]zscale=b,framestep=3,", graph);
        Assert.EndsWith("ssim", graph);

        var whole = OutputQualityCheck.Arguments("out.mp4", "src.mp4", "zscale=a", "zscale=b", OutputQualityMetric.Psnr, 1);
        var wholeGraph = whole[whole.ToList().IndexOf("-lavfi") + 1];
        Assert.DoesNotContain("framestep", wholeGraph);
        Assert.EndsWith("psnr", wholeGraph);
    }

    [Fact]
    public void ParsesTheSummaryLineOfEachMetric()
    {
        Assert.Equal(94.123456, OutputQualityCheck.ParseScore(OutputQualityMetric.Vmaf, "[Parsed_libvmaf_4 @ 0x1] VMAF score: 94.123456\n"));
        Assert.Equal(0.987654, OutputQualityCheck.ParseScore(OutputQualityMetric.Ssim, "[Parsed_ssim_4 @ 0x1] SSIM Y:0.990000 (20.0) U:0.98 (19.0) V:0.98 (19.0) All:0.987654 (19.1)\n"));
        Assert.Equal(41.5, OutputQualityCheck.ParseScore(OutputQualityMetric.Psnr, "[Parsed_psnr_4 @ 0x1] PSNR y:42.1 u:40.0 v:40.0 average:41.5 min:38.0 max:45.0\n"));
        Assert.Equal(double.PositiveInfinity, OutputQualityCheck.ParseScore(OutputQualityMetric.Psnr, "PSNR y:inf u:inf v:inf average:inf min:inf max:inf"));
        Assert.Null(OutputQualityCheck.ParseScore(OutputQualityMetric.Vmaf, "frame=  30 fps=0.0"));
        Assert.Null(OutputQualityCheck.ParseScore(OutputQualityMetric.Ssim, "VMAF score: 90"));
    }

    [Theory]
    [InlineData(OutputQualityMetric.Vmaf, 93.0, OutputQualityBand.Transparent)]
    [InlineData(OutputQualityMetric.Vmaf, 92.99, OutputQualityBand.Good)]
    [InlineData(OutputQualityMetric.Vmaf, 85.0, OutputQualityBand.Good)]
    [InlineData(OutputQualityMetric.Vmaf, 84.99, OutputQualityBand.Visible)]
    [InlineData(OutputQualityMetric.Vmaf, 70.0, OutputQualityBand.Visible)]
    [InlineData(OutputQualityMetric.Vmaf, 69.99, OutputQualityBand.Poor)]
    [InlineData(OutputQualityMetric.Ssim, 0.98, OutputQualityBand.Transparent)]
    [InlineData(OutputQualityMetric.Ssim, 0.9799, OutputQualityBand.Good)]
    [InlineData(OutputQualityMetric.Ssim, 0.9499, OutputQualityBand.Visible)]
    [InlineData(OutputQualityMetric.Ssim, 0.8999, OutputQualityBand.Poor)]
    [InlineData(OutputQualityMetric.Psnr, 42.0, OutputQualityBand.Transparent)]
    [InlineData(OutputQualityMetric.Psnr, 41.9, OutputQualityBand.Good)]
    [InlineData(OutputQualityMetric.Psnr, 36.9, OutputQualityBand.Visible)]
    [InlineData(OutputQualityMetric.Psnr, 31.9, OutputQualityBand.Poor)]
    public void BandThresholds(OutputQualityMetric metric, double score, OutputQualityBand band)
        => Assert.Equal(band, new OutputQuality(metric, score, 1).Band);

    [FfmpegFact]
    public async Task InterfaceScoreMatchesTheCommandLineScoreOnTheSameFile()
    {
        var dir = NewDir();
        try
        {
            var source = Path.Combine(dir, "source.mp4");
            var output = Path.Combine(dir, "output.mp4");
            await RunFfmpegAsync("-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=10:duration=2", "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", "-threads", "2", source);
            await RunFfmpegAsync("-y", "-i", source, "-c:v", "libx264", "-crf", "38", "-pix_fmt", "yuv420p", "-threads", "2", output);

            var cli = await QualityMeter.MeasureAsync(source, output, CancellationToken.None);
            var ui = await OutputQualityCheck.MeasureAsync(source, output, CancellationToken.None);

            Assert.NotNull(ui);
            Assert.Equal(1, ui!.Subsample);
            if (ui.Metric == OutputQualityMetric.Vmaf)
                Assert.InRange(ui.Score - cli.VmafNegMean!.Value, -0.01, 0.01);
            else
                Assert.InRange(ui.Score - cli.Ssim!.Value, -0.0001, 0.0001);

            var ssim = await OutputQualityCheck.MeasureAsync(source, output, OutputQualityMetric.Ssim, CancellationToken.None);
            Assert.NotNull(ssim);
            Assert.InRange(ssim!.Score - cli.Ssim!.Value, -0.0001, 0.0001);
            Assert.True(ssim.Score < 0.999, $"re-encoded output must not score as identical ({ssim.Score})");
        }
        finally { Cleanup(dir); }
    }

    [FfmpegFact]
    public async Task CancelledMeasurementThrowsInsteadOfReturningAScore()
    {
        var dir = NewDir();
        try
        {
            var source = Path.Combine(dir, "source.mp4");
            await RunFfmpegAsync("-y", "-f", "lavfi", "-i", "testsrc2=size=160x120:rate=10:duration=1", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-threads", "2", source);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => OutputQualityCheck.MeasureAsync(source, source, OutputQualityMetric.Ssim, cts.Token));
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public async Task NoMetricMeansNoMeasurement()
        => Assert.Null(await OutputQualityCheck.MeasureAsync("yok.mp4", "yok.mp4", null, CancellationToken.None));

    [Fact]
    public void SettingIsOffByDefaultAndSurvivesARoundTrip()
    {
        var dir = NewDir();
        try
        {
            Assert.False(new AppSettings().AdvMeasureQuality);
            var path = Path.Combine(dir, "settings.json");
            new AppSettings { AdvMeasureQuality = true }.Save(path);
            Assert.True(AppSettings.Load(path).AdvMeasureQuality);
            new AppSettings().Save(path);
            Assert.False(AppSettings.Load(path).AdvMeasureQuality);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void ResultPanelShowsOneNumberAndItsMeaning()
    {
        var sonuc = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            try
            {
                var gunluk = new List<string>();
                pencere.KaliteGunlugu = gunluk.Add;
                pencere.KaliteOlcer = (_, _, _, _) => Task.FromResult<OutputQuality?>(new OutputQuality(OutputQualityMetric.Vmaf, 95.24, 1));
                var iyi = Bekle(pencere.KaliteSatiriAsync("a", "b", CancellationToken.None));
                pencere.KaliteOlcer = (_, _, _, _) => Task.FromResult<OutputQuality?>(new OutputQuality(OutputQualityMetric.Ssim, 0.91234, 4));
                var ssim = Bekle(pencere.KaliteSatiriAsync("a", "b", CancellationToken.None));
                pencere.KaliteOlcer = (_, _, _, _) => Task.FromResult<OutputQuality?>(null);
                var yok = Bekle(pencere.KaliteSatiriAsync("a", "b", CancellationToken.None));
                pencere.KaliteOlcer = (_, _, _, _) => throw new InvalidOperationException("ffmpeg");
                var hata = Bekle(pencere.KaliteSatiriAsync("a", "b", CancellationToken.None));
                using var iptal = new CancellationTokenSource();
                iptal.Cancel();
                pencere.KaliteOlcer = (_, _, _, ct) => Task.FromCanceled<OutputQuality?>(ct);
                var iptalde = Bekle(pencere.KaliteSatiriAsync("a", "b", iptal.Token));
                var beklenenIyi = Beklenen("VMAF", MainWindow.Num(95.24, "0.0"), "main.run.quality.transparent");
                var beklenenSsim = Beklenen("SSIM", MainWindow.Num(0.91234, "0.000"), "main.run.quality.visible");
                var olculemedi = LanguageCatalog.Display(Strings.Get("main.run.quality.failed"));
                return (iyi, ssim, yok, hata, iptalde, beklenenIyi, beklenenSsim, olculemedi, gunluk);
            }
            finally { pencere.Close(); }
        });

        Assert.Equal(sonuc.beklenenIyi, sonuc.iyi);
        Assert.Contains("95.2", sonuc.iyi);
        Assert.Equal(sonuc.beklenenSsim, sonuc.ssim);
        Assert.Contains("0.912", sonuc.ssim);
        Assert.Equal(sonuc.olculemedi, sonuc.yok);
        Assert.Equal(sonuc.olculemedi, sonuc.hata);
        Assert.NotEqual(sonuc.olculemedi, sonuc.iyi);
        Assert.Equal("", sonuc.iptalde);
        Assert.Equal(4, sonuc.gunluk.Count);
        Assert.Equal("kalite: b VMAF 95.24 bant=transparent alt-ornekleme=1", sonuc.gunluk[0]);
        Assert.Equal("kalite: b olculemedi", sonuc.gunluk[3]);
    }

    private static string Beklenen(string olcut, string sayi, string bant)
        => LanguageCatalog.Display(Strings.Get("main.run.quality", olcut, sayi, LanguageCatalog.Display(Strings.Get(bant))));

    private static string Bekle(Task<string> gorev)
    {
        Assert.True(gorev.IsCompleted);
        return gorev.GetAwaiter().GetResult();
    }

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_oqc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { }
    }

    private static async Task RunFfmpegAsync(params string[] args)
    {
        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException(await stderr);
    }
}
