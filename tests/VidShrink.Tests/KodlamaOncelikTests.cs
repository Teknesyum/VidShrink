using System.Diagnostics;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// SIK-P1: kodlama süreç önceliği ve kodlayıcı iş parçacığı sınırı. Ölçüm
/// <c>docs/olcumler/kodlama-oncelik-is-parcacigi.md</c>: <c>-threads</c> yalnız libx264 ve
/// libvpx-vp9'da kodlayıcının iş parçacığını değiştiriyor.
/// </summary>
public sealed class KodlamaOncelikTests
{
    private static MediaInfo Bilgi() => Kaynak(10, Video) with { Width = 640, Height = 360, Fps = 30 };

    private static EncodePlan Plan(string kodek, int? cekirdek = null) => new()
    {
        Codec = kodek,
        Mode = "2pass",
        VideoBitrateK = 800,
        AudioCodec = null,
        AudioBitrateK = 0,
        Width = 640,
        Height = 360,
        Fps = 30,
        Preset = FfmpegArguments.PresetLadder(kodek)[0],
        EncoderThreads = cekirdek
    };

    private static int ThreadsIndex(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
            if (args[i] == "-threads") return i;
        return -1;
    }

    [Fact]
    public void VarsayilanAyarKomutuDegistirmez()
    {
        foreach (var kodek in FfmpegArguments.KnownCodecs)
        {
            var bos = Plan(kodek);
            Assert.Null(bos.EncoderThreads);
            Assert.Equal(EncodePriority.Normal, bos.Priority);
            foreach (var gecis in new[] { 1, 2 })
                Assert.Equal(-1, ThreadsIndex(FfmpegArguments.Build(Bilgi(), bos, "cikti.mp4", gecis, "gecis")));
        }
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libvpx-vp9")]
    public void SinirlayanKodlayicidaThreadsKodlayicidanSonraYazilir(string kodek)
    {
        foreach (var gecis in new[] { 1, 2 })
        {
            var args = FfmpegArguments.Build(Bilgi(), Plan(kodek, 2), "cikti.mp4", gecis, "gecis");
            var at = ThreadsIndex(args);
            Assert.True(at > 0, kodek);
            Assert.Equal("2", args[at + 1]);
            Assert.True(at > args.ToList().IndexOf(kodek));
        }
    }

    [Fact]
    public void SinirlamayanKodlayicidaSecimYazilmaz()
    {
        foreach (var kodek in FfmpegArguments.KnownCodecs.Where(k => !FfmpegArguments.HonoursThreadLimit(k)))
            foreach (var gecis in new[] { 1, 2 })
                Assert.Equal(-1, ThreadsIndex(FfmpegArguments.Build(Bilgi(), Plan(kodek, 2), "cikti.mp4", gecis, "gecis")));
    }

    [Fact]
    public void SinirlayanKumeOlcumleAyni()
    {
        var kume = FfmpegArguments.KnownCodecs.Where(FfmpegArguments.HonoursThreadLimit)
            .OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "libvpx-vp9", "libx264" }, kume);
    }

    [Fact]
    public void SecenekPlanaVeHedefKopyasinaGecer()
    {
        var options = new PlanOptions { TargetMb = 20, EncoderThreads = 3, Priority = EncodePriority.Low };
        var plan = PlanCalculator.Build(Bilgi() with { FileSizeBytes = 100_000_000, TotalBitrateBps = 8_000_000 }, options);
        Assert.Equal(3, plan.EncoderThreads);
        Assert.Equal(EncodePriority.Low, plan.Priority);

        var kopya = PlanCalculator.WithTarget(options, 10);
        Assert.Equal(3, kopya.EncoderThreads);
        Assert.Equal(EncodePriority.Low, kopya.Priority);

        var klon = plan.Clone();
        Assert.Equal(3, klon.EncoderThreads);
        Assert.Equal(EncodePriority.Low, klon.Priority);
    }

    [Theory]
    [InlineData(EncodePriority.Normal, ProcessPriorityClass.Normal)]
    [InlineData(EncodePriority.BelowNormal, ProcessPriorityClass.BelowNormal)]
    [InlineData(EncodePriority.Low, ProcessPriorityClass.Idle)]
    [InlineData(EncodePriority.AboveNormal, ProcessPriorityClass.AboveNormal)]
    public void OncelikSurecSinifinaEslenir(EncodePriority oncelik, ProcessPriorityClass beklenen)
        => Assert.Equal(beklenen, EncodeRunner.PriorityClassOf(oncelik));

    [Fact]
    public void YuksekOncelikAyarlardaSecilirGidipDoner()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "kodlama-oncelik", "yuksek-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var ayar = Path.Combine(klasor, "settings.json");
            new AppSettings { EncodePriority = EncodePriority.AboveNormal }.Save(ayar);
            Assert.Equal(EncodePriority.AboveNormal, AppSettings.Load(ayar).EncodePriority);

            var sonuc = AppHost.Run(() =>
            {
                var pencere = new MainWindow { SettingsPathOverride = ayar };
                try
                {
                    var kutu = pencere.FindControl<ComboBox>("CmbEncodePriority")!;
                    var oge = kutu.ItemCount;
                    pencere.RestoreAppSettingsForTest(new AppSettings { EncodePriority = EncodePriority.AboveNormal });
                    var secili = pencere.SelectedEncodePriority;
                    var toplanan = pencere.CaptureAppSettingsForTest().EncodePriority;
                    return (oge, secili, toplanan);
                }
                finally { pencere.Close(); }
            });

            Assert.Equal(4, sonuc.oge);
            Assert.Equal(EncodePriority.AboveNormal, sonuc.secili);
            Assert.Equal(EncodePriority.AboveNormal, sonuc.toplanan);
        }
        finally { Directory.Delete(klasor, true); }
    }

    [FfmpegFact]
    public async Task KomutSureciAyarlananOncelikteBaslar()
    {
        var okunan = new List<ProcessPriorityClass>();
        foreach (var oncelik in new[] { EncodePriority.Low, EncodePriority.BelowNormal, EncodePriority.Normal })
        {
            await EncodeRunner.RunCommandAsync(KisaKomut(), 0.2, null, "olcum", 0, 1, CancellationToken.None,
                priority: oncelik, started: surec => okunan.Add(surec.PriorityClass));
        }
        Assert.Equal(new[] { ProcessPriorityClass.Idle, ProcessPriorityClass.BelowNormal, ProcessPriorityClass.Normal }, okunan);
    }

    [FfmpegFact]
    public async Task KodlamaSureciPlaninOnceligindeBaslar()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "kodlama-oncelik", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            await EncodeRunner.RunCommandAsync(new[]
            {
                "-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=size=160x120:rate=10:duration=1",
                "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", kaynak
            }, 1, null, "kaynak", 0, 1, CancellationToken.None);

            var info = new MediaInfo
            {
                FilePath = kaynak,
                FileSizeBytes = new FileInfo(kaynak).Length,
                DurationSeconds = 1,
                Width = 160,
                Height = 120,
                Fps = 10,
                VideoCodec = "h264",
                TotalBitrateBps = 200_000
            };
            var plan = new EncodePlan
            {
                Codec = "libx264", Mode = "crf", Crf = 30, VideoBitrateK = 100, AudioCodec = null, AudioBitrateK = 0,
                Width = 160, Height = 120, Fps = 10, Preset = "ultrafast",
                EncoderThreads = 2, Priority = EncodePriority.BelowNormal
            };
            var okunan = new List<ProcessPriorityClass>();
            var runner = new EncodeRunner { Started = surec => okunan.Add(surec.PriorityClass) };

            var sonuc = await runner.RunAsync(info, plan, Path.Combine(klasor, "cikti.mp4"), 1000, null, CancellationToken.None);

            Assert.True(sonuc.Success, sonuc.Error);
            Assert.NotEmpty(okunan);
            Assert.All(okunan, sinif => Assert.Equal(ProcessPriorityClass.BelowNormal, sinif));
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Fact]
    public void AyarGidisDonusVeBozukDegerVarsayilana()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "kodlama-oncelik", "ayar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var dolu = Path.Combine(klasor, "dolu.json");
            var eski = Path.Combine(klasor, "eski.json");
            var bozuk = Path.Combine(klasor, "bozuk.json");
            new AppSettings { EncodePriority = EncodePriority.Low, EncoderThreads = 4 }.Save(dolu);
            File.WriteAllText(eski, "{\"parallelJobs\": 2}");
            File.WriteAllText(bozuk, "{\"encodePriority\": 9, \"encoderThreads\": -3}");

            var okunan = AppSettings.Load(dolu);
            Assert.Equal(EncodePriority.Low, okunan.EncodePriority);
            Assert.Equal(4, okunan.EncoderThreads);
            Assert.Equal(EncodePriority.Normal, AppSettings.Load(eski).EncodePriority);
            Assert.Equal(0, AppSettings.Load(eski).EncoderThreads);
            Assert.Equal(EncodePriority.Normal, AppSettings.Load(bozuk).EncodePriority);
            Assert.Equal(0, AppSettings.Load(bozuk).EncoderThreads);
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Theory]
    [InlineData(null, 0, true)]
    [InlineData("libx264", 0, true)]
    [InlineData("libvpx-vp9", 1, true)]
    [InlineData("libx265", 0, false)]
    [InlineData("libsvtav1", 0, false)]
    [InlineData("h264_nvenc", 0, false)]
    [InlineData(null, 2, false)]
    public void IsParcacigiSecimiSinirlamayandaGizlenir(string? kilit, int yol, bool gorunur)
        => Assert.Equal(gorunur, MainWindow.EncoderThreadsApply(kilit, yol));

    [Fact]
    public void KomutSatiriOncelikVeCekirdekAlir()
    {
        var sonuc = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--oncelik", "dusuk", "--is-parcacigi", "2" });
        Assert.True(sonuc.Ok, sonuc.ErrorKey);
        var options = sonuc.Request!.ToPlanOptions(25);
        Assert.Equal(EncodePriority.Low, options.Priority);
        Assert.Equal(2, options.EncoderThreads);

        var ingilizce = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--priority", "below-normal", "--threads", "1" });
        Assert.Equal(EncodePriority.BelowNormal, ingilizce.Request!.ToPlanOptions(25).Priority);
        Assert.Equal(1, ingilizce.Request!.ToPlanOptions(25).EncoderThreads);

        var varsayilan = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB" }).Request!.ToPlanOptions(25);
        Assert.Equal(EncodePriority.Normal, varsayilan.Priority);
        Assert.Null(varsayilan.EncoderThreads);

        Assert.Equal("error.bad-priority", CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--oncelik", "yuksek" }).ErrorKey);
        Assert.Equal("error.bad-threads", CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--is-parcacigi", "0" }).ErrorKey);
        Assert.Equal("error.bad-threads", CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--threads", "iki" }).ErrorKey);
    }

    private static string[] KisaKomut() => new[]
    {
        "-hide_banner", "-f", "lavfi", "-i", "testsrc2=size=128x128:rate=10:duration=0.2",
        "-threads", "1", "-c:v", "libx264", "-preset", "ultrafast",
        "-f", "null", OperatingSystem.IsWindows() ? "NUL" : "/dev/null"
    };
}
