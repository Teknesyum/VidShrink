using System.Globalization;
using Avalonia.Controls;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Oynatıcı panelinin sayıları arayüzün dilini değil makinenin kültürünü okuyordu:
/// konum, yakınlaştırma, ses, hız ve A-B döngüsü araya biçim konmadan yazılıyordu.
/// Aynı pencerenin küçültme sekmesi <c>12,5</c> derken oynatıcı şeridi <c>12.5</c>
/// diyordu — ikisi de aynı kullanıcının aynı ekranında.
///
/// <para>Ölçü motor açmıyor: <see cref="PlayerView"/> dosyasız kuruluyor,
/// <c>RefreshState</c> yalnız panelin metnini yeniliyor.</para>
/// </summary>
public sealed class OynaticiSayiYazimiTests
{
    private static (string Durum, string Denetim) Panel(string dil) => AppHost.Run(() =>
    {
        var onceki = Strings.Language;
        Strings.Use(dil);
        try
        {
            var view = new PlayerView();
            var window = new Window { Content = view };
            try
            {
                view.Apply(new PlayerCommand(PlayerCommandKind.Speed, 0.25));
                view.Apply(new PlayerCommand(PlayerCommandKind.Volume, -35));
                view.RefreshState();

                return (view.FindControl<TextBlock>("TxtState")?.Text ?? string.Empty,
                    view.FindControl<TextBlock>("TxtControls")?.Text ?? string.Empty);
            }
            finally
            {
                view.Close();
                window.Close();
            }
        }
        finally { Strings.Use(onceki); }
    });

    [Fact]
    public void DurumSatiriArayuzunDilinceYaziliyor()
    {
        var (_, turkce) = Panel("tr");
        var (_, ingilizce) = Panel("en");

        Assert.Contains("1,25", turkce);
        Assert.DoesNotContain("1.25", turkce);
        Assert.Contains("1.25", ingilizce);
        Assert.DoesNotContain("1,25", ingilizce);
        Assert.Contains("65", turkce);
    }

    /// <summary>
    /// Yeni yüzeylerin kendi pimleri: ondalık sayısı ve kültür dikişi. Ondalığı
    /// değiştirmek de kültürü sabitlemek de bu asertleri kırmalı.
    /// </summary>
    [Fact]
    public void YeniYuzeylerOndaliginiVeKulturunuTutuyor()
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        var en = CultureInfo.GetCultureInfo("en-US");

        Assert.Equal("12,345", Saat.Konum(12.3454, tr));
        Assert.Equal("12.345", Saat.Konum(12.3454, en));
        Assert.Equal("12.345", Saat.Tani.Konum(12.3454));

        Assert.Equal("0,05", Saat.Adim(0.05, tr));
        Assert.Equal("0.05", Saat.Adim(0.05, en));

        Assert.Equal("1,25", Bicim.Kat(1.25, tr));
        Assert.Equal("1.25", Bicim.Kat(1.25, en));

        Assert.Equal("65", Bicim.Yuzde.HazirTam(65, tr));
        Assert.Equal("150", Bicim.Yuzde.Tam(1.5, tr));
    }
}
