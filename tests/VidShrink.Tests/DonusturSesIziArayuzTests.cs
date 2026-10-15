using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Core;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// DON-P4'ün arayüz yüzü: Dönüştür'ün ses izi paneli. Açılışta yalnız ffmpeg'in kendiliğinden seçeceği izin
/// kutusu işaretlidir ve plan seçimsiz kalır (komut bugünküyle aynı); kutu eklemek izi <c>-map</c> ile çıktıya
/// alır, yeni kaynak seçimi sıfırlar, ses taşımayan kapta kutular kapanır. Geçici ayar
/// <c>.calisma/test-ciktilari/donustur-ses-izi/</c>, test siler.
/// </summary>
public sealed class DonusturSesIziArayuzTests
{
    private static readonly string[] Anahtarlar =
    {
        "main.convert.audio-tracks",
        "main.convert.audio-tracks.none",
        "main.convert.audio-tracks.not-carried",
        "main.validation.no-audio-selected",
        "main.validation.single-audio-container"
    };

    private static readonly SourceStream Mono = new(1, StreamKind.Audio, "aac", "eng", Channels: 1, BitrateBps: 64_000);
    private static readonly SourceStream Stereo = new(2, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);

    private static MediaInfo Iki() => Kaynak(62, Video, Mono, Stereo);

    private static List<string> Eslemeler(MainWindow window)
    {
        var parcalar = (window.TxtConvertCommand.Text ?? "").Split(' ').ToList();
        return parcalar.Select((p, i) => (p, i)).Where(x => x.p == "-map" && x.i + 1 < parcalar.Count).Select(x => parcalar[x.i + 1]).ToList();
    }

    [Fact]
    public void VarsayilanYalnizFfmpeginIziniIsaretlerVeKomutuDegistirmez()
    {
        var (isaretler, secim, eslemeler, komut) = Pencerede(window =>
        {
            window.LoadWithoutProbing(Iki().FilePath, Iki());
            window.CmbContainer.SelectedItem = Kap(window, "mp4");
            return (window.ConvertAudioChecks.Select(c => c.IsChecked == true).ToArray(), window.ConvertAudioChoice(), Eslemeler(window), window.TxtConvertCommand.Text);
        });

        Assert.Equal(new[] { false, true }, isaretler);
        Assert.Null(secim);
        Assert.Empty(eslemeler);
        Assert.False(string.IsNullOrEmpty(komut));
    }

    [Fact]
    public void IkinciKutuIkiIziEsler()
    {
        var (iki, tek, bos, geri) = Pencerede(window =>
        {
            window.LoadWithoutProbing(Iki().FilePath, Iki());
            window.CmbContainer.SelectedItem = Kap(window, "mp4");
            window.ConvertAudioChecks[0].IsChecked = true;
            var ikiz = (window.ConvertAudioChoice(), Eslemeler(window));
            window.ConvertAudioChecks[1].IsChecked = false;
            var tekz = (window.ConvertAudioChoice(), Eslemeler(window));
            window.ConvertAudioChecks[0].IsChecked = false;
            var bosz = (window.ConvertAudioChoice(), Eslemeler(window), window.TxtConvertCommand.Text ?? "");
            window.ConvertAudioChecks[1].IsChecked = true;
            return (ikiz, tekz, bosz, (window.ConvertAudioChoice(), Eslemeler(window)));
        });

        Assert.Equal(new[] { 0, 1 }, iki.Item1);
        Assert.Equal(new[] { "0:0", "0:1", "0:2" }, iki.Item2);
        Assert.Equal(new[] { 0 }, tek.Item1);
        Assert.Equal(new[] { "0:0", "0:1" }, tek.Item2);
        Assert.Equal(Array.Empty<int>(), bos.Item1);
        Assert.Equal(new[] { "0:0" }, bos.Item2);
        Assert.Contains(" -an ", bos.Item3);
        Assert.Null(geri.Item1);
        Assert.Empty(geri.Item2);
    }

    [Fact]
    public void YeniKaynakSecimiSifirlar()
    {
        var (sonra, secim) = Pencerede(window =>
        {
            window.LoadWithoutProbing(Iki().FilePath, Iki());
            window.ConvertAudioChecks[0].IsChecked = true;
            var tek = Kaynak(30, Video, Stereo);
            window.LoadWithoutProbing(tek.FilePath, tek);
            return (window.ConvertAudioChecks.Select(c => c.IsChecked == true).ToArray(), window.ConvertAudioChoice());
        });

        Assert.Equal(new[] { true }, sonra);
        Assert.Null(secim);
    }

    [Fact]
    public void SesTasimayanKaptaKutularKapanir()
    {
        var tr = Locales.Domain("tr", "main");
        var (gifAcik, gifIpucu, mkvAcik, mkvIpucuVar, sessizIpucu) = Pencerede(window =>
        {
            window.UseTurkish();
            window.LoadWithoutProbing(Iki().FilePath, Iki());
            window.CmbContainer.SelectedItem = Kap(window, "gif");
            var gif = (window.ConvertAudioChecks.Any(c => c.IsEnabled), window.TxtConvertAudioHint.IsVisible ? window.TxtConvertAudioHint.Text : null);
            window.CmbContainer.SelectedItem = Kap(window, "mkv");
            var mkv = (window.ConvertAudioChecks.All(c => c.IsEnabled), window.TxtConvertAudioHint.IsVisible);
            var sessiz = Kaynak(30, Video);
            window.LoadWithoutProbing(sessiz.FilePath, sessiz);
            return (gif.Item1, gif.Item2, mkv.Item1, mkv.Item2, window.TxtConvertAudioHint.IsVisible ? window.TxtConvertAudioHint.Text : null);
        });

        Assert.False(gifAcik);
        Assert.Equal(tr["main.convert.audio-tracks.not-carried"], gifIpucu);
        Assert.True(mkvAcik);
        Assert.False(mkvIpucuVar);
        Assert.Equal(tr["main.convert.audio-tracks.none"], sessizIpucu);
    }

    [Fact]
    public void TekSesliKapUyarisiCevrilir()
    {
        var tr = Locales.Domain("tr", "main");
        var dogrulama = Pencerede(window =>
        {
            window.UseTurkish();
            window.LoadWithoutProbing(Iki().FilePath, Iki());
            window.CmbContainer.SelectedItem = Kap(window, "mp3");
            window.ConvertAudioChecks[0].IsChecked = true;
            return window.TxtConvertValidation.Text ?? "";
        });

        Assert.Contains(string.Format(tr["main.validation.single-audio-container"], "MP3"), dogrulama);
    }

    [Fact]
    public void BesAnahtarKirkIkiDildeCevrili()
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
                else if (key.EndsWith("single-audio-container", StringComparison.Ordinal) && !value.Contains("{0}", StringComparison.Ordinal))
                    eksik.Add($"{language} {key}: {{0}} yok");
            }
        }
        Assert.Empty(eksik);
    }

    private static ComboBoxItem Kap(MainWindow window, string etiket)
        => window.CmbContainer.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == etiket);

    private static T Pencerede<T>(Func<MainWindow, T> govde)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "donustur-ses-izi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
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
