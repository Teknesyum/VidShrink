using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Core;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// DON-P2'nin arayüz yüzü: Dönüştür'ün altyazı paneli. İz kutusu komuttaki <c>-map</c>'i açıp kapatır,
/// eklenen dış dosya ek <c>-i</c> olur, yeni kaynak seçimi sıfırlar, altyazı taşımayan kapta kutular
/// kapanır, MP4'te düşen resim altyazı Dönüştür'ün kendi cümlesiyle söylenir, metin izi sahte çıkarıcıyla
/// kaynağın yanına kaydedilir. Geçici dosyalar <c>.calisma/test-ciktilari/donustur-altyazi/</c>, test siler.
/// </summary>
public sealed class DonusturAltyaziArayuzTests
{
    private static readonly string[] Anahtarlar =
    {
        "main.convert.subtitles",
        "main.convert.subtitles.none",
        "main.convert.subtitles.not-carried",
        "main.convert.subtitles.extract",
        "main.convert.subtitles.extracted",
        "main.convert.subtitles.extract-failed",
        "main.convert.subtitles.image-dropped"
    };

    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);
    private static readonly SourceStream Turkce = new(2, StreamKind.Subtitle, "subrip", "tur");
    private static readonly SourceStream Ingilizce = new(3, StreamKind.Subtitle, "subrip", "eng");
    private static readonly SourceStream Pgs = new(4, StreamKind.Subtitle, "hdmv_pgs_subtitle", "deu");

    private static MediaInfo Iki() => Kaynak(62, Video, Ses, Turkce, Ingilizce);

    private static List<string> Parcalar(MainWindow window) => (window.TxtConvertCommand.Text ?? "").Split(' ').ToList();

    private static bool Eslenir(MainWindow window, int index)
    {
        var parcalar = Parcalar(window);
        return parcalar.Where((p, i) => p == "-map" && i + 1 < parcalar.Count && parcalar[i + 1] == "0:" + index).Any();
    }

    [Fact]
    public void KutuKaldirilincaIzKomuttanDuser()
    {
        var (once, sonra, geri) = Pencerede(window =>
        {
            window.LoadWithoutProbing(Iki().FilePath, Iki());
            window.CmbContainer.SelectedItem = Kap(window, "mkv");
            var ilk = (Eslenir(window, 2), Eslenir(window, 3), window.ConvertSubtitleChoice());
            window.ConvertTrackChecks[0].IsChecked = false;
            var ikinci = (Eslenir(window, 2), Eslenir(window, 3), window.ConvertSubtitleChoice());
            window.ConvertTrackChecks[0].IsChecked = true;
            return (ilk, ikinci, (Eslenir(window, 2), Eslenir(window, 3), window.ConvertSubtitleChoice()));
        });

        Assert.True(once.Item1);
        Assert.True(once.Item2);
        Assert.Null(once.Item3);
        Assert.False(sonra.Item1);
        Assert.True(sonra.Item2);
        Assert.Equal(new[] { 1 }, sonra.Item3);
        Assert.True(geri.Item1 && geri.Item2);
        Assert.Null(geri.Item3);
    }

    [Fact]
    public void DisAltyaziEkGirdiOlurVeYeniKaynaktaSifirlanir()
    {
        var klasor = Klasor();
        var srt = Path.Combine(klasor, "dis.srt");
        File.WriteAllText(srt, "1\n00:00:00,000 --> 00:00:01,000\nmerhaba\n");
        try
        {
            var (eklenen, tekrar, girdiSayisi, yolVar, sonraSayi, sonraGirdi, sonraKutu) = Pencerede(window =>
            {
                window.LoadWithoutProbing(Iki().FilePath, Iki());
                window.CmbContainer.SelectedItem = Kap(window, "mkv");
                window.ConvertTrackChecks[1].IsChecked = false;
                var ilk = window.AddConvertSubtitleFiles(new[] { srt, Path.Combine(klasor, "yok.srt"), Path.Combine(klasor, "dis.txt") });
                var ikinci = window.AddConvertSubtitleFiles(new[] { srt });
                var girdi = Parcalar(window).Count(p => p == "-i");
                var yol = (window.TxtConvertCommand.Text ?? "").Contains(srt, StringComparison.Ordinal);
                window.LoadWithoutProbing(Iki().FilePath, Iki());
                return (ilk, ikinci, girdi, yol, window.ConvertExternalSubtitles.Count, Parcalar(window).Count(p => p == "-i"),
                    window.ConvertTrackChecks.All(c => c.IsChecked == true));
            });

            Assert.Equal(1, eklenen);
            Assert.Equal(0, tekrar);
            Assert.Equal(2, girdiSayisi);
            Assert.True(yolVar);
            Assert.Equal(0, sonraSayi);
            Assert.Equal(1, sonraGirdi);
            Assert.True(sonraKutu);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Theory]
    [InlineData("gif")]
    [InlineData("mp3")]
    public void AltyaziTasimayanKaptaKutularKapanir(string kap)
    {
        var tr = Locales.Domain("tr", "main");
        var (kutular, dugme, ipucu, mkvKutular, mkvIpucu) = Pencerede(window =>
        {
            window.UseTurkish();
            window.LoadWithoutProbing(Iki().FilePath, Iki());
            window.CmbContainer.SelectedItem = Kap(window, kap);
            var kapali = (window.ConvertTrackChecks.Any(c => c.IsEnabled), window.BtnConvertAddSubtitle.IsEnabled,
                window.TxtConvertSubtitleHint.IsVisible ? window.TxtConvertSubtitleHint.Text : null);
            window.CmbContainer.SelectedItem = Kap(window, "mkv");
            return (kapali.Item1, kapali.Item2, kapali.Item3, window.ConvertTrackChecks.All(c => c.IsEnabled), window.TxtConvertSubtitleHint.IsVisible);
        });

        Assert.False(kutular);
        Assert.False(dugme);
        Assert.Equal(tr["main.convert.subtitles.not-carried"], ipucu);
        Assert.True(mkvKutular);
        Assert.False(mkvIpucu);
    }

    [Fact]
    public void AltyazisizKaynaktaIpucuSoyler()
    {
        var tr = Locales.Domain("tr", "main");
        var info = Kaynak(62, Video, Ses);
        var (ipucu, kutuSayisi) = Pencerede(window =>
        {
            window.UseTurkish();
            window.LoadWithoutProbing(info.FilePath, info);
            window.CmbContainer.SelectedItem = Kap(window, "mkv");
            return (window.TxtConvertSubtitleHint.Text, window.ConvertTrackChecks.Count);
        });

        Assert.Equal(tr["main.convert.subtitles.none"], ipucu);
        Assert.Equal(0, kutuSayisi);
    }

    [Fact]
    public void Mp4teResimAltyaziDusunceDonusturunCumlesiCikar()
    {
        var tr = Locales.Domain("tr", "main");
        var info = Kaynak(62, Video, Ses, Turkce, Pgs);
        var (mp4, mkv, pgsKapali) = Pencerede(window =>
        {
            window.UseTurkish();
            window.LoadWithoutProbing(info.FilePath, info);
            window.CmbContainer.SelectedItem = Kap(window, "mp4");
            var ilk = window.ConvertStatusForTest ?? "";
            window.CmbContainer.SelectedItem = Kap(window, "mkv");
            var ikinci = window.ConvertStatusForTest ?? "";
            window.CmbContainer.SelectedItem = Kap(window, "mp4");
            window.ConvertTrackChecks[1].IsChecked = false;
            return (ilk, ikinci, window.ConvertStatusForTest ?? "");
        });

        Assert.Contains(tr["main.convert.subtitles.image-dropped"][1..], mp4);
        Assert.DoesNotContain(tr["main.reason.stream.image-subtitle-dropped"][1..], mp4);
        Assert.DoesNotContain(tr["main.convert.subtitles.image-dropped"][1..], mkv);
        Assert.DoesNotContain(tr["main.convert.subtitles.image-dropped"][1..], pgsKapali);
    }

    [Fact]
    public void MetinIziKaynaginYaninaKaydedilir()
    {
        var tr = Locales.Domain("tr", "main");
        var info = Kaynak(62, Video, Ses, Turkce, Pgs);
        var cagrilar = new List<(int Sira, string Yol)>();
        var (basari, hata, gorunur, kaydetSayisi) = Pencerede(window =>
        {
            window.UseTurkish();
            window.LoadWithoutProbing(info.FilePath, info);
            window.SubtitleExtractor = (_, sira, yol, _) => { cagrilar.Add((sira, yol)); return Task.CompletedTask; };
            window.ExtractConvertSubtitleAsync(0).GetAwaiter().GetResult();
            var ilk = window.TxtConvertSubtitleResult.Text;
            window.SubtitleExtractor = (_, _, _, _) => Task.FromException(new InvalidOperationException("disk dolu"));
            window.ExtractConvertSubtitleAsync(0).GetAwaiter().GetResult();
            var dugmeler = window.ConvertSubtitleTracks.Children.OfType<Grid>().Select(g => g.Children.Count(c => c is Button and not CheckBox)).ToList();
            return (ilk, window.TxtConvertSubtitleResult.Text, window.TxtConvertSubtitleResult.IsVisible, dugmeler);
        });

        var hedef = Path.Combine(@"C:\kaynak", "girdi.tur.srt");
        Assert.Equal(new[] { (0, hedef) }, cagrilar);
        Assert.Equal(string.Format(tr["main.convert.subtitles.extracted"], "girdi.tur.srt"), basari);
        Assert.Equal(string.Format(LanguageCatalog.Title(tr["main.convert.subtitles.extract-failed"], "tr"), "disk dolu"), hata);
        Assert.True(gorunur);
        Assert.Equal(new[] { 1, 0 }, kaydetSayisi);
    }

    [Fact]
    public void HedefAdiDoluysaSayiAlir()
    {
        var klasor = Klasor();
        try
        {
            var info = Kaynak(62, Video, Turkce, new SourceStream(3, StreamKind.Subtitle, "ass")) with { FilePath = Path.Combine(klasor, "gezi.mkv") };
            Assert.Equal(Path.Combine(klasor, "gezi.tur.srt"), MainWindow.ConvertSubtitleTarget(info, 0));
            File.WriteAllText(Path.Combine(klasor, "gezi.tur.srt"), "");
            Assert.Equal(Path.Combine(klasor, "gezi.tur-2.srt"), MainWindow.ConvertSubtitleTarget(info, 0));
            Assert.Equal(Path.Combine(klasor, "gezi.2.ass"), MainWindow.ConvertSubtitleTarget(info, 1));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void YediAnahtarKirkIkiDildeCevrili()
    {
        var english = Locales.Domain("en", "main");
        Assert.Equal(42, Locales.Languages.Count);
        var eksik = new List<string>();
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Domain(language, "main");
            foreach (var key in Anahtarlar)
            {
                if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) eksik.Add($"{language} {key}: yok");
                else if (language != "en" && value == english[key]) eksik.Add($"{language} {key}: cevrilmemis");
                else if (key.EndsWith("extracted", StringComparison.Ordinal) || key.EndsWith("extract-failed", StringComparison.Ordinal))
                {
                    if (!value.Contains("{0}", StringComparison.Ordinal)) eksik.Add($"{language} {key}: {{0}} yok");
                }
            }
        }

        Assert.True(eksik.Count == 0, string.Join(Environment.NewLine, eksik));
    }

    private static ComboBoxItem Kap(MainWindow window, string etiket)
        => window.CmbContainer.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == etiket);

    private static string Klasor()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "donustur-altyazi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static T Pencerede<T>(Func<MainWindow, T> govde)
    {
        var klasor = Klasor();
        try
        {
            return AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = Path.Combine(klasor, "settings.json") };
                try { return govde(window); }
                finally { window.Close(); }
            });
        }
        finally
        {
            try { Directory.Delete(klasor, true); }
            catch (IOException) { }
        }
    }
}
