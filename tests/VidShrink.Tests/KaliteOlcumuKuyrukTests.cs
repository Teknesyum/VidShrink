using System.Diagnostics;
using System.Globalization;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public sealed class KaliteOlcumuKuyrukTests : IDisposable
{
    private readonly string _kok = Path.Combine(Path.GetTempPath(), "vidshrink_kok_" + Guid.NewGuid().ToString("N"));
    private readonly string _ayar;

    public KaliteOlcumuKuyrukTests()
    {
        _ayar = Path.Combine(_kok, "ayar", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_ayar)!);
    }

    public void Dispose()
    {
        try { Directory.Delete(_kok, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class SahteEylem : IQueueEndActions
    {
        public void Reveal(string path) { }
        public void Sleep() { }
        public void PowerOff() { }
        public void Quit() { }
    }

    [Fact]
    public void ProgressLinesBecomeAFractionOfTheSourceDuration()
    {
        Assert.Equal(0.25, OutputQualityCheck.ProgressFraction("out_time_us=2500000", 10));
        Assert.Equal(0.5, OutputQualityCheck.ProgressFraction("out_time_ms=5000000", 10));
        Assert.Equal(1.0, OutputQualityCheck.ProgressFraction("out_time_us=12000000", 10));
        Assert.Null(OutputQualityCheck.ProgressFraction("out_time_us=N/A", 10));
        Assert.Null(OutputQualityCheck.ProgressFraction("frame=12", 10));
        Assert.Null(OutputQualityCheck.ProgressFraction("out_time_us=2500000", 0));
        Assert.Null(OutputQualityCheck.ProgressFraction(null, 10));
    }

    [Fact]
    public void LogLineCarriesScoreBandAndOnlyTheFileName()
    {
        var yol = Path.Combine("C:", "Kullanici", "Gizli", "cikti.mp4");
        var satir = OutputQualityCheck.LogLine(yol, new OutputQuality(OutputQualityMetric.Vmaf, 88.456, 3));
        Assert.Equal("kalite: cikti.mp4 VMAF 88.46 bant=good alt-ornekleme=3", satir);
        Assert.DoesNotContain("Gizli", satir);
        Assert.Equal("kalite: cikti.mp4 SSIM 0.9512 bant=good alt-ornekleme=1",
            OutputQualityCheck.LogLine("cikti.mp4", new OutputQuality(OutputQualityMetric.Ssim, 0.95123, 1)));
        Assert.Equal("kalite: cikti.mp4 olculemedi", OutputQualityCheck.LogLine("cikti.mp4", null));
    }

    [Fact]
    public void JobWindowShowsPercentWhileMeasuringAndWritesTheScoreToTheLog()
    {
        var sonuc = AppHost.Run(() =>
        {
            var eskiAyar = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
            var kultur = CultureInfo.CurrentUICulture;
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", _ayar);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            ShrinkJobWindow? window = null;
            try
            {
                window = new ShrinkJobWindow(Array.Empty<string>(), new PlanOptions { TargetMb = 25 }, false, null) { Actions = new SahteEylem() };
                var gunluk = new List<string>();
                window.KaliteGunlugu = gunluk.Add;
                string? olcerken = null;
                string? asama = null;
                window.KaliteOlcer = (_, _, ilerleme, _) =>
                {
                    ilerleme!.Report(0.42);
                    olcerken = window.KaliteYuzdesi;
                    asama = window.StageText;
                    return Task.FromResult<OutputQuality?>(new OutputQuality(OutputQualityMetric.Vmaf, 95.24, 1));
                };
                var istek = new ShrinkRequest(25, Path.Combine(_kok, "kaynak.mp4"));
                var gorev = window.KaliteEkiAsync(istek, Path.Combine(_kok, "cikti.mp4"), CancellationToken.None);
                Assert.True(gorev.IsCompleted);
                var metin = gorev.GetAwaiter().GetResult();
                var beklenen = string.Format(CultureInfo.GetCultureInfo("en"), Strings.GetIn("en", "main.run.quality"),
                    "VMAF", "95.2", LanguageCatalog.Title(Strings.GetIn("en", "main.run.quality.transparent"), "en"));

                window.KaliteOlcer = (_, _, _, _) => throw new InvalidOperationException("ffmpeg");
                var hata = window.KaliteEkiAsync(istek, Path.Combine(_kok, "cikti.mp4"), CancellationToken.None).GetAwaiter().GetResult();
                return (olcerken, asama, sonra: window.KaliteYuzdesi, metin, beklenen, gunluk, hata,
                    olculemedi: LanguageCatalog.Title(Strings.GetIn("en", "main.run.quality.failed"), "en"),
                    olcuAsamasi: LanguageCatalog.Title(Strings.GetIn("en", "main.output.quality-check"), "en"));
            }
            finally
            {
                window?.Close();
                CultureInfo.CurrentUICulture = kultur;
                Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", eskiAyar);
            }
        });

        Assert.Equal(0.42.ToString("P0", CultureInfo.GetCultureInfo("en")), sonuc.olcerken);
        Assert.Equal(sonuc.olcuAsamasi, sonuc.asama);
        Assert.Equal("", sonuc.sonra);
        Assert.Contains("95.2", sonuc.metin);
        Assert.Equal(LanguageCatalog.Title(sonuc.beklenen, "en"), sonuc.metin);
        Assert.Equal(sonuc.olculemedi, sonuc.hata);
        Assert.Equal(new[] { "kalite: cikti.mp4 VMAF 95.24 bant=transparent alt-ornekleme=1", "kalite: cikti.mp4 olculemedi" }, sonuc.gunluk);
    }

    [Fact]
    public void MainWindowShowsPercentWhileMeasuringAndHidesItAfter()
    {
        var sonuc = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            try
            {
                var gunluk = new List<string>();
                pencere.KaliteGunlugu = gunluk.Add;
                string? olcerken = null;
                pencere.KaliteOlcer = (_, _, ilerleme, _) =>
                {
                    ilerleme!.Report(0.5);
                    olcerken = pencere.KaliteYuzdesi;
                    return Task.FromResult<OutputQuality?>(new OutputQuality(OutputQualityMetric.Psnr, 33.3, 1));
                };
                var gorev = pencere.KaliteSatiriAsync("kaynak.mp4", Path.Combine("klasor", "cikti.mp4"), CancellationToken.None);
                Assert.True(gorev.IsCompleted);
                return (olcerken, sonra: pencere.KaliteYuzdesi, gunluk, beklenen: KaliteSatiri.Yuzde(0.5, Strings.Culture));
            }
            finally { pencere.Close(); }
        });

        Assert.Equal(sonuc.beklenen, sonuc.olcerken);
        Assert.Contains("50", sonuc.olcerken);
        Assert.Equal("", sonuc.sonra);
        Assert.Equal(new[] { "kalite: cikti.mp4 PSNR 33.30 bant=visible alt-ornekleme=1" }, sonuc.gunluk);
    }

    [FfmpegFact]
    public async Task QueuedJobAppendsTheScoreWhenTheSettingIsOn()
    {
        var kaynak = Path.Combine(_kok, "kaynak.mp4");
        var uret = await FfmpegRunner.RunAsync(new[]
        {
            "-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=10:duration=2",
            "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "4", "-pix_fmt", "yuv420p", kaynak
        });
        Assert.True(uret.Ok, uret.StandardError);
        new AppSettings { AdvMeasureQuality = true, EncoderThreads = 2 }.Save(_ayar);
        var hedefMb = new FileInfo(kaynak).Length / 1024.0 / 1024.0 / 3;

        var sonuc = AppHost.Run(() =>
        {
            var eskiAyar = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
            var kultur = CultureInfo.CurrentUICulture;
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", _ayar);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            ShrinkJobWindow? window = null;
            try
            {
                window = new ShrinkJobWindow(new[] { kaynak }, new PlanOptions { TargetMb = hedefMb }, false, null)
                {
                    Actions = new SahteEylem(),
                    PencereEtkin = () => true
                };
                var gunluk = new List<string>();
                var olculen = new List<(string Kaynak, string Cikti)>();
                window.KaliteGunlugu = gunluk.Add;
                window.KaliteOlcer = (k, c, _, _) =>
                {
                    olculen.Add((k, c));
                    return Task.FromResult<OutputQuality?>(new OutputQuality(OutputQualityMetric.Vmaf, 91.5, 1));
                };
                string? teslim = null;
                var bitti = false;
                window.JobFinished += (_, cikti) => { teslim = cikti; bitti = true; };
                var olcuAcik = window.KaliteyiOlc;
                window.Begin();
                var saat = Stopwatch.StartNew();
                while (!bitti && saat.Elapsed < TimeSpan.FromSeconds(120))
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(20);
                }
                return (bitti, olcuAcik, teslim, olculen, gunluk, mesaj: window.MessageText);
            }
            finally
            {
                window?.Close();
                CultureInfo.CurrentUICulture = kultur;
                Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", eskiAyar);
            }
        });

        Assert.True(sonuc.bitti, "iş bitmedi");
        Assert.True(sonuc.olcuAcik);
        Assert.NotNull(sonuc.teslim);
        Assert.Equal(new[] { (kaynak, sonuc.teslim!) }, sonuc.olculen);
        Assert.Contains("91.5", sonuc.mesaj);
        Assert.StartsWith(sonuc.teslim!, sonuc.mesaj);
        Assert.Equal(new[] { $"kalite: {Path.GetFileName(sonuc.teslim)} VMAF 91.50 bant=good alt-ornekleme=1" }, sonuc.gunluk);
    }

    [FfmpegFact]
    public async Task InterfaceLineReadsTheSameVmafAsTheCliResultLine()
    {
        var source = Path.Combine(_kok, "source.mp4");
        var output = Path.Combine(_kok, "output.mp4");
        Assert.True((await FfmpegRunner.RunAsync(new[] { "-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=10:duration=2", "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", "-threads", "2", source })).Ok);
        Assert.True((await FfmpegRunner.RunAsync(new[] { "-hide_banner", "-y", "-nostdin", "-i", source, "-c:v", "libx264", "-crf", "36", "-pix_fmt", "yuv420p", "-threads", "2", output })).Ok);

        var cli = await CliApp.MeasureVmafAsync(source, output, null, CancellationToken.None);
        var ui = await OutputQualityCheck.MeasureAsync(source, output, OutputQualityMetric.Vmaf, CancellationToken.None);

        Assert.NotNull(ui);
        Assert.NotNull(cli.VmafNegMean);
        Assert.InRange(ui!.Score - cli.VmafNegMean!.Value, -0.01, 0.01);
        Assert.True(ui.Score < 99, $"re-encoded output must not score as identical ({ui.Score})");

        var text = CliText.ForLanguage("en");
        var cliLine = CliApp.VmafLine(cli, text);
        var uiNumber = ui.Score.ToString("0.0", text.Culture);
        Assert.Equal(text.Format("result.vmaf", uiNumber), cliLine);
        Assert.Contains(uiNumber, KaliteSatiri.Metin(ui, (key, args) => Strings.GetIn("en", key, args), text.Culture));
    }
}
