using System.Globalization;
using System.Text.RegularExpressions;

namespace VidShrink.Tests;

/// <summary>
/// Bindirmenin tutma sureleri uc yerde yaziliydi ve hicbiri obürünü baglamiyordu:
/// canli sayi <c>Themes/Recorder.axaml</c>'da, <see cref="RecorderInputOverlay"/>'de
/// kaynak bulunamazsa kullanilan yedek sayi, bir de canli olcunun alt siniri.
/// Denetim yedegi 360'tan 120'ye cevirdi, hicbir olcu kirilmadi; ayrica tutmanin
/// <b>artmasi</b> (0,36 -> 5 sn) da hicbir seyi kirmiyordu, cunku canli olcunun
/// duvar saati ust siniri bilincli kaldirilmisti.
///
/// <para>Burada kaynak tek: XAML. Yedek deger ondan ayrisirsa ve XAML'daki sayi
/// degisirse olcu kirmizi olur. Sureç acmaz, pencere acmaz — iki dosyayi okur.</para>
/// </summary>
public sealed class KaydediciTutmaSuresiTests
{
    private static string Tema() => File.ReadAllText(
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", "Recorder.axaml"));

    private static string Bindirme() => File.ReadAllText(
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Recorder", "RecorderInputOverlay.cs"));

    private static TimeSpan Kaynaktan(string anahtar)
    {
        var eslesme = Regex.Match(
            Tema(),
            $"<sys:TimeSpan x:Key=\"{Regex.Escape(anahtar)}\">([^<]+)</sys:TimeSpan>");

        Assert.True(eslesme.Success, $"Recorder.axaml icinde {anahtar} bulunamadi.");
        return TimeSpan.Parse(eslesme.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Canli sayilar pimli: tutmanin uzamasi da kisalmasi da olcuyu kirar.
    /// Halka goz kirpma kadar, yazi okunacak kadar durur.
    /// </summary>
    [Theory]
    [InlineData("RecorderClickRingHold", 360)]
    [InlineData("RecorderKeyCaptionHold", 1500)]
    public void TutmaSuresiKaynaktaPimli(string anahtar, int milisaniye)
        => Assert.Equal(milisaniye, Kaynaktan(anahtar).TotalMilliseconds, 3);

    /// <summary>
    /// Yedek deger kaynaktakiyle ayni. Kaynak bulunamadiginda sessizce baska bir
    /// sure kullanilmasi, kusurun yalniz kaynak kaybinda ortaya cikmasi demekti.
    /// </summary>
    [Fact]
    public void YedekDegerKaynaktakiyleAyni()
    {
        var kaynak = Bindirme();
        var cagrilar = Regex.Matches(
            kaynak,
            @"OverlayPlace\.Hold\([^,]+,\s*""(?<anahtar>[^""]+)"",\s*TimeSpan\.(?<uretici>FromMilliseconds|FromSeconds)\((?<deger>[0-9.]+)\)\)");

        Assert.Equal(2, cagrilar.Count);

        foreach (Match cagri in cagrilar)
        {
            var sayi = double.Parse(cagri.Groups["deger"].Value, CultureInfo.InvariantCulture);
            var yedek = cagri.Groups["uretici"].Value == "FromSeconds"
                ? TimeSpan.FromSeconds(sayi)
                : TimeSpan.FromMilliseconds(sayi);

            Assert.Equal(
                Kaynaktan(cagri.Groups["anahtar"].Value).TotalMilliseconds,
                yedek.TotalMilliseconds,
                3);
        }
    }

    /// <summary>
    /// Olumsuz kontrol: her <c>Hold</c> cagrisinin bir yedegi var, yani desen
    /// yukarida sessizce sifir cagri eslestirip yesil donmuyor.
    /// </summary>
    [Fact]
    public void HerHoldCagrisiDesenceGoruluyor()
        => Assert.Equal(
            Regex.Matches(Bindirme(), @"OverlayPlace\.Hold\(").Count,
            Regex.Matches(Bindirme(), @"OverlayPlace\.Hold\([^,]+,\s*""[^""]+"",\s*TimeSpan\.").Count);
}
