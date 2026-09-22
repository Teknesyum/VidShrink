using VidShrink.App;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// C1-6: B1'de yalnız CLI'dan açılan ses ve altyazı kolları arayüzde. Ses yüksekliği ve kazanç
/// plana geçer; dış altyazı ve yakma seçimi o videoya aittir — yeni kaynakta sıfırlanır,
/// kuyruk penceresine taşınmaz.
/// </summary>
public sealed class IzPaneliTests : IDisposable
{
    private static readonly string Klasor = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".calisma", "iz-paneli");

    private static MediaInfo Kaynak(string yol = "C:\\ornek\\film.mkv") => new()
    {
        FilePath = yol,
        FileSizeBytes = 50L * 1024 * 1024,
        DurationSeconds = 60,
        Width = 1280,
        Height = 720,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 6_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p",
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "h264"),
            new SourceStream(1, StreamKind.Audio, "aac", "tur", Channels: 2),
            new SourceStream(2, StreamKind.Subtitle, "hdmv_pgs_subtitle", "eng"),
            new SourceStream(3, StreamKind.Subtitle, "subrip", "tur", Title: "Türkçe"),
            new SourceStream(4, StreamKind.Subtitle, "ass", "eng")
        }
    };

    private static T Pencerede<T>(Func<MainWindow, T> is_)
        => AppHost.Run(() =>
        {
            var window = new MainWindow();
            try { return is_(window); }
            finally { window.Close(); }
        });

    private readonly List<string> _klasorler = new();

    public void Dispose()
    {
        foreach (var klasor in _klasorler)
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
    }

    private string[] Dosyalar(params string[] adlar)
    {
        var klasor = Path.GetFullPath(Path.Combine(Klasor, Guid.NewGuid().ToString("N")[..8]));
        Directory.CreateDirectory(klasor);
        _klasorler.Add(klasor);
        return adlar.Select(ad =>
        {
            var yol = Path.Combine(klasor, ad);
            File.WriteAllText(yol, "1\n00:00:01,000 --> 00:00:02,000\nmerhaba\n");
            return yol;
        }).ToArray();
    }

    /// <summary>Ses yüksekliği ve kazanç plana geçer; 0 dB kazanç yazılmaz (olumsuz kontrol).</summary>
    [Fact]
    public void SesKollariPlanaGecer()
    {
        var (bos, dolu, sifir) = Pencerede(window =>
        {
            var ilk = window.PlanOptionsForTest();
            window.ChkAudioLoudnorm.IsChecked = true;
            window.CmbAudioGain.SelectedIndex = Array.IndexOf(MainWindow.GainCandidates, -6);
            var ikinci = window.PlanOptionsForTest();
            window.CmbAudioGain.SelectedIndex = Array.IndexOf(MainWindow.GainCandidates, 0);
            return (ilk, ikinci, window.PlanOptionsForTest());
        });

        Assert.False(bos.AudioLoudnorm);
        Assert.Null(bos.AudioGainDb);
        Assert.True(dolu.AudioLoudnorm);
        Assert.Equal(-6, dolu.AudioGainDb);
        Assert.Null(sifir.AudioGainDb);
    }

    /// <summary>Kazanç adayları motorun sınırı içinde ve sıfırı içerir.</summary>
    [Fact]
    public void KazancAdaylariSinirda()
    {
        Assert.Contains(0, MainWindow.GainCandidates);
        Assert.All(MainWindow.GainCandidates, db => Assert.InRange(db, StreamMapping.MinGainDb, StreamMapping.MaxGainDb));
    }

    /// <summary>Dış altyazı bir kez eklenir, desteklenmeyen uzantı ve olmayan dosya atlanır, çıkarma listeden düşer.</summary>
    [Fact]
    public void DisAltyaziEklenirVeCikarilir()
    {
        var yollar = Dosyalar("film.tr.srt", "film.en.vtt", "notlar.txt");
        var (eklenen, ikinci, plandaki, dil, kalan, gorunur) = Pencerede(window =>
        {
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", Kaynak());
            var n = window.AddSubtitleFiles(yollar.Append(yollar[0].ToUpperInvariant()).Append(yollar[0] + ".yok.srt"));
            var m = window.AddSubtitleFiles(new[] { yollar[1] });
            var plan = window.PlanOptionsForTest().ExternalSubtitles.Select(s => s.Path).ToList();
            var tr = window.ExternalSubtitles[0].Language;
            window.RemoveSubtitle(0);
            return (n, m, plan, tr, window.PlanOptionsForTest().ExternalSubtitles.Select(s => s.Path).ToList(), window.SubtitleList.IsVisible);
        });

        Assert.Equal(2, eklenen);
        Assert.Equal(0, ikinci);
        Assert.Equal(new[] { yollar[0], yollar[1] }, plandaki);
        Assert.Equal("tur", dil);
        Assert.Equal(new[] { yollar[1] }, kalan);
        Assert.True(gorunur);
    }

    /// <summary>Yakma listesi yalnız metin altyazıları sunar; seçim altyazılar içindeki sıraya iner. PGS listede yok.</summary>
    [Fact]
    public void YakmaYalnizMetinAltyazisi()
    {
        var (adet, acik, secim, suzgec, secimsiz) = Pencerede(window =>
        {
            var basta = window.PlanOptionsForTest().Filters.BurnSubtitle;
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", Kaynak());
            var n = window.CmbBurnSubtitle.ItemCount;
            var a = window.CmbBurnSubtitle.IsEnabled;
            window.CmbBurnSubtitle.SelectedIndex = 1;
            return (n, a, window.BurnChoice, window.PlanOptionsForTest().Filters.BurnSubtitle, basta);
        });

        Assert.Equal(3, adet);
        Assert.True(acik);
        Assert.Equal(1, secim);
        Assert.Equal(1, suzgec);
        Assert.Null(secimsiz);
    }

    /// <summary>Altyazısız kaynakta yakma kutusu kapalı ve tek "kapalı" satırı var.</summary>
    [Fact]
    public void AltyazisizKaynaktaYakmaKapali()
    {
        var (adet, acik) = Pencerede(window =>
        {
            window.LoadWithoutProbing("C:\\ornek\\duz.mp4", Kaynak("C:\\ornek\\duz.mp4") with { AudioCodec = null, Streams = new[] { new SourceStream(0, StreamKind.Video, "h264") } });
            return (window.CmbBurnSubtitle.ItemCount, window.CmbBurnSubtitle.IsEnabled);
        });

        Assert.Equal(1, adet);
        Assert.False(acik);
    }

    /// <summary>Yeni kaynak önceki videonun dış altyazısını ve yakma seçimini taşımaz; ses kolları kalır.</summary>
    [Fact]
    public void YeniKaynakIzleriSifirlar()
    {
        var yollar = Dosyalar("film.srt");
        var (once, sonra) = Pencerede(window =>
        {
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", Kaynak());
            window.AddSubtitleFiles(yollar);
            window.CmbBurnSubtitle.SelectedIndex = 2;
            window.ChkAudioLoudnorm.IsChecked = true;
            var ilk = window.PlanOptionsForTest();
            window.LoadWithoutProbing("C:\\ornek\\ikinci.mkv", Kaynak("C:\\ornek\\ikinci.mkv"));
            return (ilk, window.PlanOptionsForTest());
        });

        Assert.Single(once.ExternalSubtitles);
        Assert.Equal(2, once.Filters.BurnSubtitle);
        Assert.Empty(sonra.ExternalSubtitles);
        Assert.Null(sonra.Filters.BurnSubtitle);
        Assert.True(sonra.AudioLoudnorm);
    }

    /// <summary>Kuyruk penceresi dış altyazıyı ve yakmayı almaz; ses kolları geçer (olumsuz kontrol).</summary>
    [Fact]
    public void KuyrukIzleriTasimaz()
    {
        var yollar = Dosyalar("film.srt");
        var (pencere, kuyruk) = Pencerede(window =>
        {
            ShrinkJobWindow? batch = null;
            try
            {
                window.LoadWithoutProbing("C:\\ornek\\film.mkv", Kaynak());
                window.AddSubtitleFiles(yollar);
                window.CmbBurnSubtitle.SelectedIndex = 1;
                window.ChkAudioLoudnorm.IsChecked = true;
                var tek = window.PlanOptionsForTest();
                batch = window.OpenBatch(new[] { "a.mp4", "b.mp4" });
                return (tek, batch.OptionsFor(new ShrinkRequest(0, "a.mp4"), Kaynak("a.mp4")).Options);
            }
            finally { batch?.Close(); }
        });

        Assert.Single(pencere.ExternalSubtitles);
        Assert.NotNull(pencere.Filters.BurnSubtitle);
        Assert.Empty(kuyruk.ExternalSubtitles);
        Assert.Null(kuyruk.Filters.BurnSubtitle);
        Assert.True(kuyruk.AudioLoudnorm);
    }

    /// <summary>Altyazı dosyası tanıma bırakma kolunun kapısıdır; video altyazı sayılmaz.</summary>
    [Theory]
    [InlineData("a.srt", true)]
    [InlineData("a.ASS", true)]
    [InlineData("a.vtt", true)]
    [InlineData("a.mkv", false)]
    [InlineData("a.txt", false)]
    public void AltyaziDosyasiTanima(string yol, bool beklenen) => Assert.Equal(beklenen, MainWindow.IsSubtitleFile(yol));

    /// <summary>On anahtar 42 dilde dolu.</summary>
    [Fact]
    public void AnahtarlarButunDillerde()
    {
        var anahtarlar = new[]
        {
            "main.audio.level.label", "main.audio.loudnorm", "main.audio.gain", "main.audio.gain.none",
            "main.subtitles.label", "main.subtitles.add", "main.subtitles.burn", "main.subtitles.burn.off",
            "main.subtitles.remove", "main.subtitles.file-type"
        };
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in anahtarlar)
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
        }
    }
}
