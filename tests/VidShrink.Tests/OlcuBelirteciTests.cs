using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Proje kurali: olcu yalniz <c>Themes/Theme.axaml</c> belirteclerinden gelir. Bu sinif kurali
/// iki taraftan pimler — kaynakta duz sayi kalmadigini tarar, sonra denetimleri gercekten
/// cizip olculerinin belirtecin degeriyle esit ciktigini okur. Yalniz tarama yetmez: bir
/// <c>StaticResource</c> yanlis turde cozulurse derleme gecer, cizim calisma aninda patlar.
/// </summary>
public sealed class OlcuBelirteciTests
{
    private static readonly string[] OlcuNitelikleri =
    [
        "Padding", "Margin", "Width", "Height",
        "MinWidth", "MinHeight", "MaxWidth", "MaxHeight",
        "Spacing", "CornerRadius", "BorderThickness"
    ];

    private static string TemaYolu(string ad) =>
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", ad);

    [Theory]
    [InlineData("Controls.axaml")]
    public void TemaDosyasindaDuzOlcuYok(string dosya)
    {
        var metin = File.ReadAllText(TemaYolu(dosya));
        var desen = new Regex("(" + string.Join("|", OlcuNitelikleri) + ")=\"[0-9]");

        var satirlar = new List<string>();
        var no = 0;
        foreach (var satir in metin.Split('\n'))
        {
            no++;
            if (desen.IsMatch(satir)) satirlar.Add($"{dosya}:{no} {satir.Trim()}");
        }

        Assert.Empty(satirlar);
    }

    [Fact]
    public void TaramaDuzOlcuyuGercektenGoruyor()
    {
        var desen = new Regex("(" + string.Join("|", OlcuNitelikleri) + ")=\"[0-9]");

        Assert.Matches(desen, "<Border Padding=\"10,0\"/>");
        Assert.Matches(desen, "<Thumb Margin=\"1\"/>");
        Assert.DoesNotMatch(desen, "<Border Padding=\"{StaticResource SliderTrackPadding}\"/>");
    }

    [Fact]
    public void KaydiriciDolgusuBelirtectenGelir()
    {
        var (dolgu, belirtec) = AppHost.Run(() =>
        {
            var slider = new Slider { Width = 200 };
            var kok = Ciz(slider, "Root");
            return (((Border)kok).Padding, Belirtec<Thickness>("SliderTrackPadding"));
        });

        Assert.Equal(belirtec, dolgu);
    }

    [Fact]
    public void SeritBasliginKenariBelirtectenGelir()
    {
        var (kenar, belirtec) = AppHost.Run(() =>
        {
            var bar = new ScrollBar
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                Height = 200,
                Minimum = 0,
                Maximum = 100,
                ViewportSize = 0.5
            };

            var gorsel = Ciz(bar, "ThumbVisual");
            return (((Border)gorsel).Margin, Belirtec<Thickness>("ScrollBarThumbMargin"));
        });

        Assert.Equal(belirtec, kenar);
    }

    [Fact]
    public void OnayKutusuKarelerininOlcusuBelirtectenGelir()
    {
        var (anahat, halka, hedef, isaret) = AppHost.Run(() =>
        {
            var kutu = new CheckBox { Content = "olcu", Theme = Belirtec<ControlTheme>("CheckStyle") };
            var disHat = (Border)Ciz(kutu, "CheckOutline");
            var odak = (Border)Bul(kutu, "FocusRing");

            return (disHat.Bounds.Width, odak.Bounds.Width,
                    Belirtec<double>("TargetMinSize"), Belirtec<double>("CheckGlyphSize"));
        });

        Assert.Equal(isaret, anahat);
        Assert.Equal(hedef, halka);
    }

    private static Control Ciz(Control denetim, string ad)
    {
        var pencere = new Window { Width = 400, Height = 400, Content = denetim };
        pencere.Show();
        pencere.Measure(new Size(400, 400));
        pencere.Arrange(new Rect(0, 0, 400, 400));
        pencere.UpdateLayout();
        return Bul(denetim, ad);
    }

    private static Control Bul(Control denetim, string ad) =>
        denetim.GetVisualDescendants().OfType<Control>().First(c => c.Name == ad);

    private static T Belirtec<T>(string anahtar)
    {
        var uygulama = Application.Current ?? throw new InvalidOperationException("Application.Current yok");
        Assert.True(uygulama.TryFindResource(anahtar, out var deger), $"{anahtar} bulunamadi");
        return Assert.IsType<T>(deger);
    }
}
