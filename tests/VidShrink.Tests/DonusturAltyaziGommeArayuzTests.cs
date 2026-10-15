using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Core;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// DON-P3'ün arayüz yüzü: Dönüştür'ün altyazı panelindeki yakma seçimi. Liste küçültmenin kuralıyla
/// kurulur (libass'sız ffmpeg'de metin izi düşer, resim izi kalır), biçem satırı yalnız metin izde
/// görünür ve plana yalnız orada geçer, yeni kaynak seçimi sıfırlar. Geçici dosyalar
/// <c>.calisma/test-ciktilari/donustur-gomme/</c>, test siler.
/// </summary>
public sealed class DonusturAltyaziGommeArayuzTests
{
    private static readonly string[] Anahtarlar =
    {
        "main.subtitles.style.color",
        "main.subtitles.style.color.white",
        "main.subtitles.style.color.yellow",
        "main.subtitles.style.color.cyan",
        "main.subtitles.style.color.green"
    };

    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);
    private static readonly SourceStream Srt = new(2, StreamKind.Subtitle, "subrip", "tur");
    private static readonly SourceStream Pgs = new(3, StreamKind.Subtitle, "hdmv_pgs_subtitle", "deu");

    private static MediaInfo Ornek() => Kaynak(62, Video, Ses, Srt, Pgs);

    [Fact]
    public void LibasssizFfmpegdeMetinIziGizlenirResimIziKalir()
    {
        var (tam, secili, eksik, dusen, resim, geri) = Pencerede(window =>
        {
            window.LoadWithoutProbing(Ornek().FilePath, Ornek());
            window.CmbContainer.SelectedItem = Kap(window, "mkv");
            var t = window.CmbConvertBurn.ItemCount;
            window.CmbConvertBurn.SelectedIndex = 1;
            var s = window.ConvertBurnChoice;
            window.ApplyHardwareVerdict(AltyaziYakmaKodlamaYolTests.Yoklama(libass: false), false, HardwareVerdict.NotProbed);
            var e = window.CmbConvertBurn.ItemCount;
            var d = (window.ConvertBurnChoice, window.CurrentConversionPlan().BurnSubtitle);
            window.CmbConvertBurn.SelectedIndex = 1;
            var r = window.ConvertBurnChoice;
            window.ApplyHardwareVerdict(AltyaziYakmaKodlamaYolTests.Yoklama(libass: true), false, HardwareVerdict.NotProbed);
            return (t, s, e, d, r, (window.CmbConvertBurn.ItemCount, window.ConvertBurnChoice));
        });

        Assert.Equal(3, tam);
        Assert.Equal(0, secili);
        Assert.Equal(2, eksik);
        Assert.Equal(((int?)null, (int?)null), dusen);
        Assert.Equal(1, resim);
        Assert.Equal((3, (int?)1), geri);
    }

    [Fact]
    public void BicemSatiriYalnizMetinIzindeGorunur()
    {
        var (kapali, metin, resimde, geriKapali) = Pencerede(window =>
        {
            window.LoadWithoutProbing(Ornek().FilePath, Ornek());
            var k = window.ConvertBurnStylePanel.IsVisible;
            window.CmbConvertBurn.SelectedIndex = 1;
            var m = window.ConvertBurnStylePanel.IsVisible;
            window.CmbConvertBurn.SelectedIndex = 2;
            var r = window.ConvertBurnStylePanel.IsVisible;
            window.CmbConvertBurn.SelectedIndex = 0;
            return (k, m, r, window.ConvertBurnStylePanel.IsVisible);
        });

        Assert.False(kapali);
        Assert.True(metin);
        Assert.False(resimde);
        Assert.False(geriKapali);
    }

    [Fact]
    public void PlanYakmaVeBicemiTasirResimIzdeBicemYazmaz()
    {
        var (metinPlan, metinKomut, resimPlan, resimKomut) = Pencerede(window =>
        {
            window.LoadWithoutProbing(Ornek().FilePath, Ornek());
            window.CmbContainer.SelectedItem = Kap(window, "mkv");
            window.CmbConvertBurn.SelectedIndex = 1;
            window.CmbConvertBurnFont.SelectedIndex = 1;
            window.CmbConvertBurnSize.SelectedIndex = 4;
            window.CmbConvertBurnColor.SelectedIndex = 2;
            window.ChkConvertBurnBox.IsChecked = true;
            var mp = window.CurrentConversionPlan();
            var mk = window.TxtConvertCommand.Text ?? "";
            window.CmbConvertBurn.SelectedIndex = 2;
            return (mp, mk, window.CurrentConversionPlan(), window.TxtConvertCommand.Text ?? "");
        });

        Assert.Equal(0, metinPlan.BurnSubtitle);
        Assert.Equal(MainWindow.BurnFontChoices[0], metinPlan.BurnFont);
        Assert.Equal(MainWindow.BurnSizeCandidates[3], metinPlan.BurnFontSize);
        Assert.Equal("FFFF00", metinPlan.BurnColor);
        Assert.True(metinPlan.BurnBox);
        Assert.Contains("subtitles=", metinKomut);
        Assert.Contains("PrimaryColour=&H0000FFFF", metinKomut);
        Assert.Contains("BorderStyle=3", metinKomut);
        Assert.DoesNotContain("-map 0:2", metinKomut);

        Assert.Equal(1, resimPlan.BurnSubtitle);
        Assert.Null(resimPlan.BurnFont);
        Assert.Null(resimPlan.BurnFontSize);
        Assert.Null(resimPlan.BurnColor);
        Assert.False(resimPlan.BurnBox);
        Assert.Contains("-filter_complex", resimKomut);
        Assert.Contains("-map [v]", resimKomut);
        Assert.DoesNotContain("force_style", resimKomut);
    }

    [Fact]
    public void YakmasizPlanBicemAlaniTasimaz()
    {
        var plan = Pencerede(window =>
        {
            window.LoadWithoutProbing(Ornek().FilePath, Ornek());
            window.CmbConvertBurnFont.SelectedIndex = 1;
            window.CmbConvertBurnColor.SelectedIndex = 1;
            return window.CurrentConversionPlan();
        });

        Assert.Null(plan.BurnSubtitle);
        Assert.Null(plan.BurnFont);
        Assert.Null(plan.BurnColor);
    }

    [Fact]
    public void YeniKaynakYakmaSeciminiVeBicemiSifirlar()
    {
        var (once, sonra, yazi, renk, kutu) = Pencerede(window =>
        {
            window.LoadWithoutProbing(Ornek().FilePath, Ornek());
            window.CmbConvertBurn.SelectedIndex = 1;
            window.CmbConvertBurnFont.SelectedIndex = 2;
            window.CmbConvertBurnColor.SelectedIndex = 3;
            window.ChkConvertBurnBox.IsChecked = true;
            var o = window.ConvertBurnChoice;
            var ikinci = Ornek() with { FilePath = @"C:\Kayitlar\ikinci.mkv" };
            window.LoadWithoutProbing(ikinci.FilePath, ikinci);
            return (o, window.ConvertBurnChoice, window.CmbConvertBurnFont.SelectedIndex, window.CmbConvertBurnColor.SelectedIndex, window.ChkConvertBurnBox.IsChecked);
        });

        Assert.Equal(0, once);
        Assert.Null(sonra);
        Assert.Equal(0, yazi);
        Assert.Equal(0, renk);
        Assert.False(kutu);
    }

    [Fact]
    public void AltyazisizKaynaktaYakmaKapali()
    {
        var (sayi, etkin) = Pencerede(window =>
        {
            var info = Kaynak(62, Video, Ses);
            window.LoadWithoutProbing(info.FilePath, info);
            return (window.CmbConvertBurn.ItemCount, window.CmbConvertBurn.IsEnabled);
        });

        Assert.Equal(1, sayi);
        Assert.False(etkin);
    }

    [Fact]
    public void RenkAnahtarlariKirkIkiDildeCevrili()
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
            }
        }

        Assert.True(eksik.Count == 0, string.Join(Environment.NewLine, eksik));
    }

    private static ComboBoxItem Kap(MainWindow window, string etiket)
        => window.CmbContainer.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == etiket);

    private static T Pencerede<T>(Func<MainWindow, T> govde)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "donustur-gomme", Guid.NewGuid().ToString("N"));
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
