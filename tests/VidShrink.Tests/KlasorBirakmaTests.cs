using System.Globalization;
using System.Text.Json;
using VidShrink.App;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// C1-3: ana pencereye klasör ya da birden çok video bırakılabiliyor. Toplama yalnız videoları,
/// klasörün yalnız üst düzeyinden, tekrarsız alıyor; kuyruk penceresi her dosyayı pencerenin o anki
/// seçenekleriyle kuruyor, çıktı uzantısı plandan geliyor. Kabuk menüsü kolu eskisi gibi yalnız hedefi taşıyor.
/// </summary>
public sealed class KlasorBirakmaTests : IDisposable
{
    private readonly string _klasor = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        ".calisma", "test-ciktilari", "c1-3-klasor", Guid.NewGuid().ToString("N")[..8]));

    public KlasorBirakmaTests() => Directory.CreateDirectory(_klasor);

    public void Dispose()
    {
        try { Directory.Delete(_klasor, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Dosya(string ad)
    {
        var yol = Path.Combine(_klasor, ad);
        Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
        File.WriteAllText(yol, "x");
        return yol;
    }

    private static MediaInfo Kaynak(int sesIzi = 1) => new()
    {
        FilePath = "kaynak.mkv",
        FileSizeBytes = 400_000_000L,
        DurationSeconds = 600,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        Streams = new[] { new SourceStream(0, StreamKind.Video, "h264") }
            .Concat(Enumerable.Range(1, sesIzi).Select(i => new SourceStream(i, StreamKind.Audio, "aac", Channels: 2)))
            .ToArray(),
    };

    /// <summary>Video olmayan, gizli ve alt klasördeki dosya alınmıyor; aynı dosya iki yoldan gelince bir kez.</summary>
    [Fact]
    public void ToplamaYalnizUstDuzeydekiVideolariAlir()
    {
        var b = Dosya(Path.Combine("klasor", "b.mkv"));
        var a = Dosya(Path.Combine("klasor", "a.MP4"));
        Dosya(Path.Combine("klasor", "notlar.txt"));
        Dosya(Path.Combine("klasor", ".gizli.mp4"));
        Dosya(Path.Combine("klasor", "alt", "derin.mp4"));
        var tek = Dosya("tek.mov");
        var metin = Dosya("oku.txt");

        var bulunan = DroppedMedia.Collect(new[] { tek, Path.Combine(_klasor, "klasor"), b, metin, Path.Combine(_klasor, "yok.mp4") });

        Assert.Equal(new[] { a, b, tek }, bulunan);
    }

    /// <summary>Boş ya da videosuz bırakma boş liste verir; pencere bunu "video yok" diye gösterir.</summary>
    [Fact]
    public void VideosuzBirakmaBosDoner()
    {
        Dosya(Path.Combine("bos", "oku.txt"));
        Assert.Empty(DroppedMedia.Collect(new[] { Path.Combine(_klasor, "bos") }));
        Assert.Empty(DroppedMedia.Collect(Array.Empty<string>()));
    }

    /// <summary>Bırakılan kuyruk şablonun bütün seçeneklerini taşır; kabuk kolu yalnız hedefi (olumsuz kontrol).</summary>
    [Fact]
    public void KuyrukSablonunSecenekleriniTasir()
    {
        var sablon = new PlanOptions { TargetMb = 12.5, Codec = CodecPreference.MaxCompression, LockedCodec = "libx265", KeepAllTracks = true, AllowFpsDrop = false };
        var (kuyruk, kabuk) = AppHost.Run(() =>
        {
            var batch = new ShrinkJobWindow(new[] { "a.mp4", "b.mp4" }, sablon, false, null);
            var shell = new ShrinkJobWindow(new ShellShrinkStartup(null, ShrinkArgumentProblem.NoTarget, null), null);
            try
            {
                return (batch.OptionsFor(new ShrinkRequest(0, "a.mp4"), Kaynak()), shell.OptionsFor(new ShrinkRequest(25, "a.mp4"), Kaynak()));
            }
            finally { batch.Close(); shell.Close(); }
        });

        Assert.Equal(12.5, kuyruk.TargetMb);
        Assert.Equal(JsonSerializer.Serialize(sablon), JsonSerializer.Serialize(kuyruk.Options));
        Assert.Equal(25, kabuk.TargetMb);
        Assert.Equal(JsonSerializer.Serialize(new PlanOptions { TargetMb = 25 }), JsonSerializer.Serialize(kabuk.Options));
    }

    /// <summary>Boyut tavanı olmayan yongada hedef her dosyanın kendi kalite tavanından.</summary>
    [Fact]
    public void TavansizYongadaHedefDosyaBasina()
    {
        var kisa = Kaynak();
        var uzun = Kaynak() with { DurationSeconds = 2400, FileSizeBytes = 1600_000_000L };
        var (h1, h2) = AppHost.Run(() =>
        {
            var batch = new ShrinkJobWindow(new[] { "a.mp4", "b.mp4" }, new PlanOptions { TargetMb = 12.5 }, true, null);
            try { return (batch.OptionsFor(new ShrinkRequest(0, "a.mp4"), kisa).TargetMb, batch.OptionsFor(new ShrinkRequest(0, "b.mp4"), uzun).TargetMb); }
            finally { batch.Close(); }
        });

        Assert.Equal(PlanCalculator.QualityCeilingTargetMb(kisa), h1);
        Assert.Equal(PlanCalculator.QualityCeilingTargetMb(uzun), h2);
        Assert.NotEqual(h1, h2);
    }

    /// <summary>İzleri koruyan plan MKV'ye iner ve kuyruk dosyayı .mkv yazar; ön ayar kabı seçiliyse o kazanır.</summary>
    [Fact]
    public void UzantiPlandanGelir()
    {
        var info = Kaynak(sesIzi: 2);
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, KeepAllTracks = true });
        var duz = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25 });
        var (izli, duzUzanti, onAyar) = AppHost.Run(() =>
        {
            var batch = new ShrinkJobWindow(new[] { "a.mp4", "b.mp4" }, new PlanOptions { TargetMb = 25 }, false, null);
            var webm = new ShrinkJobWindow(new[] { "a.mp4", "b.mp4" }, new PlanOptions { TargetMb = 25 }, false, "webm");
            try { return (batch.ExtensionFor(plan), batch.ExtensionFor(duz), webm.ExtensionFor(plan)); }
            finally { batch.Close(); webm.Close(); }
        });

        Assert.Equal("mkv", izli);
        Assert.Equal("mp4", duzUzanti);
        Assert.Equal("webm", onAyar);
        var yol = ShrinkJobWindow.UniqueOutputPath(Path.Combine(_klasor, "kaynak.mkv"), new AppSettings(), plan, 25, izli);
        Assert.Equal(".mkv", Path.GetExtension(yol));
    }

    /// <summary>Ana pencere kuyruğu kendi seçenekleriyle açar: hedef kutusu, kodek kilidi ve iz kutusu kuyruğa geçer.</summary>
    [Fact]
    public void AnaPencereKuyruguKendiSecenekleriyleAcar()
    {
        var ayar = Path.Combine(_klasor, "ayar", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ayar)!);
        var (pencere, kuyruk) = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = ayar };
            ShrinkJobWindow? batch = null;
            try
            {
                window.TxtTarget.Text = 12.5.ToString("0.##", CultureInfo.InvariantCulture);
                window.ChkAdvKeepTracks.IsChecked = true;
                var beklenen = window.PlanOptionsForTest();
                batch = window.OpenBatch(new[] { "a.mp4", "b.mp4" });
                return (beklenen, batch.OptionsFor(new ShrinkRequest(0, "a.mp4"), Kaynak()).Options);
            }
            finally { batch?.Close(); window.Close(); }
        });

        Assert.Equal(12.5, kuyruk.TargetMb);
        Assert.True(kuyruk.KeepAllTracks);
        Assert.Equal(JsonSerializer.Serialize(pencere), JsonSerializer.Serialize(kuyruk));
    }

    /// <summary>Dört yeni anahtar 42 dilde, sayı yer tutucusuyla; eski iki ret anahtarı hiçbir dilde kalmadı.</summary>
    [Fact]
    public void AnahtarlarButunDillerde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in new[] { "main.drop.batch", "main.drop.batch-hint", "main.drop.none", "main.shrink-job.batch" })
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
            Assert.Contains("{0}", values["main.drop.batch"], StringComparison.Ordinal);
            Assert.Contains("{0}", values["main.shrink-job.batch"], StringComparison.Ordinal);
            Assert.False(values.ContainsKey("main.drop.single"), language);
            Assert.False(values.ContainsKey("main.drop.no-folder"), language);
        }
    }
}
