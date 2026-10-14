using System.Diagnostics;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// SIK-P3: koşan kodlamayı duraklatma. Gerçek ffmpeg tek süreçte, kısa klip, <c>-re</c> ile gerçek
/// zamanlı ve <c>-threads 2</c>: makineye yük binmez. Askıdayken kısmi dosya büyümez ve saat durur,
/// sürdürünce iş biter; askıda iptal süreci öldürür. Kuyruk penceresi sahte koşturucu ve sahte disk
/// okuyucusuyla ölçülür.
/// </summary>
public sealed class KodlamaDuraklatmaTests
{
    private static void Bekle(Func<bool> kosul, string ne)
    {
        var saat = Stopwatch.StartNew();
        while (!kosul())
        {
            Assert.True(saat.Elapsed < TimeSpan.FromSeconds(20), "beklenen olmadı: " + ne);
            Thread.Sleep(20);
        }
    }

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "kodlama-duraklatma", ad + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static string[] GercekZamanliKomut(string cikti) => new[]
    {
        "-hide_banner", "-y", "-re", "-f", "lavfi", "-i", "testsrc2=size=160x120:rate=10:duration=3",
        "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-x264-params", "threads=2",
        "-flush_packets", "1", "-f", "mpegts", cikti
    };

    private static long Boy(string yol) => File.Exists(yol) ? new FileInfo(yol).Length : 0;

    private sealed class Kayit : IProgress<EncodeProgress>
    {
        private readonly List<EncodeProgress> _adimlar = new();
        public void Report(EncodeProgress value) { lock (_adimlar) _adimlar.Add(value); }
        public EncodeProgress? Son { get { lock (_adimlar) return _adimlar.Count > 0 ? _adimlar[^1] : null; } }
    }

    [Fact]
    public void AskidakiSaatDururSurdurunceIsler()
    {
        var duraklat = new EncodePause();
        var saat = Stopwatch.StartNew();
        using (duraklat.Track(saat))
        {
            duraklat.Pause();
            Assert.False(saat.IsRunning);
            var durakta = saat.Elapsed;
            Thread.Sleep(50);
            Assert.Equal(durakta, saat.Elapsed);
            duraklat.Resume();
            Assert.True(saat.IsRunning);
        }
        Assert.False(duraklat.IsPaused);
    }

    [Fact]
    public async Task AskidaYeniSurecBaslamazSurdurunceGecer()
    {
        var duraklat = new EncodePause();
        duraklat.Pause();
        var bekleyen = duraklat.WaitWhilePausedAsync(CancellationToken.None);
        await Task.Delay(30);
        Assert.False(bekleyen.IsCompleted);
        duraklat.Resume();
        await bekleyen.WaitAsync(TimeSpan.FromSeconds(5));

        duraklat.Pause();
        using var iptal = new CancellationTokenSource();
        var iptalli = duraklat.WaitWhilePausedAsync(iptal.Token);
        iptal.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => iptalli.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [FfmpegFact]
    public async Task AskidaKismiDosyaBuyumezSurdurunceIsBiter()
    {
        var klasor = Klasor("askida");
        try
        {
            var cikti = Path.Combine(klasor, "kismi.ts");
            var duraklat = new EncodePause();
            var kayit = new Kayit();
            var duvar = Stopwatch.StartNew();
            var kosum = EncodeRunner.RunCommandAsync(GercekZamanliKomut(cikti), 3, kayit, "olcum", 0, 1, CancellationToken.None,
                pause: duraklat);

            Bekle(() => Boy(cikti) > 0 && kayit.Son is { Fraction: > 0.1 }, "kısmi dosya yazılmaya başlasın");
            duraklat.Pause();
            Thread.Sleep(300);
            var durakBoyu = Boy(cikti);
            var durakSaati = kayit.Son!.Elapsed;
            Thread.Sleep(1500);
            Assert.Equal(durakBoyu, Boy(cikti));
            Assert.False(kosum.IsCompleted);
            duraklat.Resume();

            await kosum.WaitAsync(TimeSpan.FromSeconds(30));
            duvar.Stop();
            Assert.True(Boy(cikti) > durakBoyu);
            var son = kayit.Son!;
            Assert.Equal(1, son.Fraction);
            Assert.True(son.Elapsed <= duvar.Elapsed - TimeSpan.FromSeconds(1.5),
                $"Geçen süre {son.Elapsed}, duvar {duvar.Elapsed}: askıdaki süre sayaca girmiş.");
            Assert.True(son.Elapsed >= durakSaati);
        }
        finally { Directory.Delete(klasor, true); }
    }

    [FfmpegFact]
    public async Task AskidaIptalSureciSonlandirir()
    {
        var klasor = Klasor("iptal");
        try
        {
            var cikti = Path.Combine(klasor, "kismi.ts");
            var duraklat = new EncodePause();
            var kayit = new Kayit();
            var kimlik = 0;
            using var iptal = new CancellationTokenSource();
            var kosum = EncodeRunner.RunCommandAsync(GercekZamanliKomut(cikti), 3, kayit, "olcum", 0, 1, iptal.Token,
                started: surec => kimlik = surec.Id, pause: duraklat);

            Bekle(() => Boy(cikti) > 0 && kayit.Son is not null, "kısmi dosya yazılmaya başlasın");
            duraklat.Pause();
            Thread.Sleep(200);
            iptal.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => kosum.WaitAsync(TimeSpan.FromSeconds(15)));
            Bekle(() => SurecBitti(kimlik), "askıdaki süreç ölsün");
        }
        finally { Directory.Delete(klasor, true); }
    }

    private static bool SurecBitti(int kimlik)
    {
        try
        {
            using var surec = Process.GetProcessById(kimlik);
            return surec.HasExited;
        }
        catch (ArgumentException) { return true; }
        catch (InvalidOperationException) { return true; }
    }

    [FfmpegFact]
    public async Task AskidaKodlamaIptalindeKismiDosyaKalmaz()
    {
        var klasor = Klasor("motor");
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            await EncodeRunner.RunCommandAsync(new[]
            {
                "-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=size=160x120:rate=10:duration=2",
                "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", kaynak
            }, 2, null, "kaynak", 0, 1, CancellationToken.None);

            var info = new MediaInfo
            {
                FilePath = kaynak, FileSizeBytes = new FileInfo(kaynak).Length, DurationSeconds = 2,
                Width = 160, Height = 120, Fps = 10, VideoCodec = "h264", TotalBitrateBps = 200_000
            };
            var plan = new EncodePlan
            {
                Codec = "libx264", Mode = "crf", Crf = 30, VideoBitrateK = 100, AudioCodec = null, AudioBitrateK = 0,
                Width = 160, Height = 120, Fps = 10, Preset = "ultrafast", EncoderThreads = 2
            };
            var duraklat = new EncodePause();
            duraklat.Pause();
            var surecler = new List<int>();
            using var iptal = new CancellationTokenSource();
            var runner = new EncodeRunner { Pause = duraklat, Started = surec => { lock (surecler) surecler.Add(surec.Id); } };

            var kosum = runner.RunAsync(info, plan, Path.Combine(klasor, "cikti.mp4"), 1000, null, iptal.Token);
            await Task.Delay(500);
            Assert.False(kosum.IsCompleted);
            iptal.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => kosum.WaitAsync(TimeSpan.FromSeconds(15)));
            lock (surecler) Assert.All(surecler, kimlik => Bekle(() => SurecBitti(kimlik), "süreç ölsün"));
            Assert.Empty(Directory.GetFiles(klasor, "vidshrink_partial_*"));
            Assert.False(File.Exists(Path.Combine(klasor, "cikti.mp4")));
        }
        finally { Directory.Delete(klasor, true); }
    }

    private sealed class SahteKosum
    {
        private readonly Dictionary<string, TaskCompletionSource> _bitis = new();

        public Task Kostur(ShrinkRequest istek, CancellationToken ct)
        {
            var bitis = new TaskCompletionSource();
            _bitis[istek.Path] = bitis;
            ct.Register(() => bitis.TrySetCanceled(ct));
            return bitis.Task;
        }

        public void Bitir(string yol)
        {
            _bitis[yol].TrySetResult();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static ShrinkJobWindow Pencere(SahteKosum kosum, Func<string, long?> bosAlan, params string[] yollar)
        => new(yollar, new PlanOptions { TargetMb = 25 }, false, null)
        {
            Kosturucu = kosum.Kostur,
            FreeSpace = bosAlan
        };

    [Fact]
    public void KuyrukDuraklatmasiKosanIsiDeAskiyaAlir()
    {
        var kosum = new SahteKosum();
        var sonuc = AppHost.Run(() =>
        {
            var window = Pencere(kosum, _ => long.MaxValue, "a.mp4", "b.mp4");
            try
            {
                window.Begin();
                window.SetPaused(true);
                var durakta = (window.EncodePause.IsPaused, window.PausedText);
                window.SetPaused(false);
                return (durakta, window.EncodePause.IsPaused, window.Language);
            }
            finally { window.Close(); }
        });

        Assert.True(sonuc.durakta.IsPaused);
        Assert.Equal(LanguageCatalog.Title(Strings.GetIn(sonuc.Language, "main.shrink-job.paused"), sonuc.Language), sonuc.durakta.PausedText);
        Assert.False(sonuc.Item2);
    }

    [Fact]
    public void DiskEsiginAltinaInincePencereKendiligindenDuraklar()
    {
        var kosum = new SahteKosum();
        long bos = 10L * 1000 * 1000 * 1000;
        var okunan = new List<string>();
        var sonuc = AppHost.Run(() =>
        {
            var window = Pencere(kosum, klasor => { okunan.Add(klasor); return bos; }, "a.mp4", "b.mp4");
            try
            {
                window.Begin();
                window.CheckDiskSpace();
                var bolluk = (window.Paused, window.EncodePause.IsPaused);
                bos = DiskSpaceGuard.PauseBelowBytes - 1;
                window.CheckDiskSpace();
                var darlik = (window.Paused, window.EncodePause.IsPaused, window.PausedText);
                kosum.Bitir("a.mp4");
                var sonra = (window.RunningJobs.Count, window.Pending.Count);
                return (bolluk, darlik, sonra, window.Language);
            }
            finally { window.Close(); }
        });

        Assert.NotEmpty(okunan);
        Assert.Equal((false, false), sonuc.bolluk);
        Assert.True(sonuc.darlik.Paused);
        Assert.True(sonuc.darlik.Item2);
        Assert.Equal(LanguageCatalog.Title(Strings.GetIn(sonuc.Language, "main.shrink-job.disk-paused", 199), sonuc.Language),
            sonuc.darlik.PausedText);
        Assert.Equal((0, 1), sonuc.sonra);
    }

    [Fact]
    public void DiskOkunamazsaDuraklatmaz()
    {
        var kosum = new SahteKosum();
        var sonuc = AppHost.Run(() =>
        {
            var window = Pencere(kosum, _ => null, "a.mp4");
            try
            {
                window.Begin();
                window.CheckDiskSpace();
                return window.Paused;
            }
            finally { window.Close(); }
        });
        Assert.False(sonuc);
    }

    [Fact]
    public void IsBaslamadanAlanDenetlenir()
    {
        var kosum = new SahteKosum();
        var sonuc = AppHost.Run(() =>
        {
            var window = Pencere(kosum, _ => DiskSpaceGuard.RequiredBytes(25) - 1, "a.mp4");
            try
            {
                var yetmez = window.SpaceBeforeStart(Path.Combine("x", "cikti.mp4"), 25);
                var durdu = (window.Paused, window.PausedText);
                window.SetPaused(false);
                window.FreeSpace = _ => DiskSpaceGuard.RequiredBytes(25);
                var yeter = window.SpaceBeforeStart(Path.Combine("x", "cikti.mp4"), 25);
                return (yetmez, durdu, yeter, window.Paused, window.Language);
            }
            finally { window.Close(); }
        });

        Assert.NotNull(sonuc.yetmez);
        Assert.Equal(LanguageCatalog.Title(Strings.GetIn(sonuc.Language, "main.run.no-space", "275"), sonuc.Language), sonuc.yetmez);
        Assert.True(sonuc.durdu.Paused);
        Assert.Null(sonuc.yeter);
        Assert.False(sonuc.Item4);
    }

    [Fact]
    public void AnaSekmeDuraklatDugmesiKodlamayiAskiyaAlir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            try
            {
                var bosta = pencere.RunPauseEnabled;
                pencere.ShowEncodeProgressForTest(new EncodeProgress(0.4, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6), 1, "pass 1/1 (attempt 1)"));
                var kosarken = pencere.RunPauseEnabled;
                pencere.ToggleRunPause();
                var askida = pencere.RunPause.IsPaused;
                pencere.ToggleRunPause();
                var surdu = pencere.RunPause.IsPaused;
                pencere.ToggleRunPause();
                pencere.EndEncodeForTest();
                return (bosta, kosarken, askida, surdu, pencere.RunPause.IsPaused, pencere.RunPauseEnabled);
            }
            finally { pencere.Close(); }
        });

        Assert.False(sonuc.bosta);
        Assert.True(sonuc.kosarken);
        Assert.True(sonuc.askida);
        Assert.False(sonuc.surdu);
        Assert.False(sonuc.Item5);
        Assert.False(sonuc.Item6);
    }

    [Fact]
    public void AnahtarlarButunDillerde()
    {
        var anahtarlar = new[] { "main.action.pause-run", "main.action.resume-run", "main.shrink-job.disk-paused", "main.shrink-job.paused" };
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in anahtarlar)
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
            Assert.Contains("{0}", values["main.shrink-job.disk-paused"], StringComparison.Ordinal);
        }
    }
}
