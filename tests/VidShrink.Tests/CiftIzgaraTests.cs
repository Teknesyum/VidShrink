using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VidShrink.App;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// <see cref="CiftIzgara"/>: değerlerin en genişi etiket sütununun yanına tek satırda sığıyorsa
/// çiftler yan yana, sığmıyorsa hepsi alt alta. Alt alta düzende değer tam genişlik alır ve
/// tek sözcük bölünmez; yan yana düzende değerler aynı x'ten başlar. Genişlikler yazı tipine
/// bağlı kalmasın diye doğal ölçüden türetilir: yan yana gereken genişliğin 20 px altı ve 40 px üstü.
/// </summary>
public sealed class CiftIzgaraTests
{
    private const double Aralik = 12;

    private static (bool Stacked, double DegerX, double DegerGenislik, int Satir, double Genislik) Olc(double pay) => AppHost.Run(() =>
    {
        var izgara = new CiftIzgara { ColumnSpacing = Aralik, RowSpacing = 8 };
        TextBlock Blok(string metin) => new() { Text = metin, FontSize = 14, TextWrapping = TextWrapping.Wrap };
        var etiket = Blok("Kodlayıcı");
        var uzun = Blok("568 kbit/s · Dvojprechodový");
        izgara.Children.Add(Blok("Plan"));
        izgara.Children.Add(Blok("568 kbit/s"));
        izgara.Children.Add(etiket);
        izgara.Children.Add(uzun);

        etiket.Measure(Size.Infinity);
        uzun.Measure(Size.Infinity);
        var genislik = Math.Ceiling(etiket.DesiredSize.Width + Aralik + uzun.DesiredSize.Width) + pay;

        izgara.Measure(new Size(genislik, double.PositiveInfinity));
        izgara.Arrange(new Rect(0, 0, genislik, izgara.DesiredSize.Height));
        return (izgara.Stacked, uzun.Bounds.X, uzun.Bounds.Width, uzun.TextLayout.TextLines.Count, genislik);
    });

    [Fact]
    public void GenisYerdeYanYanaDizilir()
    {
        var (stacked, x, _, satir, genislik) = Olc(40);
        Assert.False(stacked, $"genişlik {genislik}");
        Assert.True(x > 0, $"değer x {x}");
        Assert.Equal(1, satir);
    }

    [Fact]
    public void DarYerdeAltAltaInerVeDegerTekSatirKalir()
    {
        var (stacked, x, degerGenislik, satir, genislik) = Olc(-20);
        Assert.True(stacked, $"genişlik {genislik}");
        Assert.Equal(0, x);
        Assert.Equal(genislik, degerGenislik);
        Assert.Equal(1, satir);
    }
}
