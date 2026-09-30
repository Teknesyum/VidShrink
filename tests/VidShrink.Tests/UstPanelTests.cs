using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using VidShrink.App;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Oynatıcının üst paneli tek katman (danışma 015 S2): sekme şeridi, başlık düğmeleri ve
/// paravan <c>TopOverlay</c>'in içinde, gizleme sınıfı üçünü birden kapatıyor. Paravan
/// alt şeridin paravanının aynası, yalnız oynatıcı sekmesinde. Başlık düğmelerinin anahattı
/// yok; hover ve basılı durum zeminle ayrılıyor.
/// </summary>
public sealed class UstPanelTests
{
    private readonly ITestOutputHelper _cikti;

    public UstPanelTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static readonly Size Olcu = new(1280, 800);

    private static T Pencerede<T>(Func<MainWindow, T> is_)
        => AppHost.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                UstSeritTikTests.Yerlestir(window, Olcu);
                return is_(window);
            }
            finally
            {
                window.Close();
            }
        });

    private static void Yenile(MainWindow window)
    {
        foreach (var parca in window.GetVisualDescendants().OfType<Avalonia.Animation.Animatable>()) parca.Transitions = null;
        foreach (var parca in window.GetVisualDescendants().OfType<Avalonia.Layout.Layoutable>()) parca.InvalidateMeasure();
        UstSeritTikTests.Yerlestir(window, Olcu);
    }

    private static bool Gorunur(Visual v)
        => v.IsVisible && v.GetVisualAncestors().TakeWhile(a => a is not TopLevel).All(a => a.IsVisible);

    private static T Adli<T>(Visual kok, string ad) where T : Control
        => kok.GetVisualDescendants().OfType<T>().First(c => c.Name == ad);

    [Fact]
    public void UstKatmanTekBaglaAcilipKapanir()
    {
        var sonuc = Pencerede(window =>
        {
            window.Tabs.SelectedIndex = 0;
            Yenile(window);

            var ust = Adli<Panel>(window.Tabs, "TopOverlay");
            var serit = Adli<ItemsPresenter>(window.Tabs, "PART_ItemsPresenter");
            var scrim = Adli<Border>(window.Tabs, "TopScrim");
            var katman = window.TitleBarLayer;

            var icinde = katman.GetVisualAncestors().Contains(ust) && serit.GetVisualAncestors().Contains(ust) && scrim.GetVisualAncestors().Contains(ust);
            var acik = (ust.IsVisible, Gorunur(katman), Gorunur(serit), Gorunur(scrim));

            window.Classes.Add("chrome-hidden");
            Yenile(window);
            var gizli = (ust.IsVisible, Gorunur(katman), Gorunur(serit), Gorunur(scrim));

            window.Classes.Remove("chrome-hidden");
            Yenile(window);
            var geri = (ust.IsVisible, Gorunur(katman), Gorunur(serit), Gorunur(scrim));

            return (icinde, acik, gizli, geri);
        });
        _cikti.WriteLine($"icinde {sonuc.icinde}, acik {sonuc.acik}, gizli {sonuc.gizli}, geri {sonuc.geri}");

        Assert.True(sonuc.icinde, "şerit, düğmeler ve paravan aynı TopOverlay'in içinde olmalı");
        Assert.Equal((true, true, true, true), sonuc.acik);
        Assert.Equal((false, false, false, false), sonuc.gizli);
        Assert.Equal((true, true, true, true), sonuc.geri);
    }

    [Fact]
    public void ParavanAltSeridinAynasiVeYalnizOynaticida()
    {
        var sonuc = Pencerede(window =>
        {
            window.Tabs.SelectedIndex = 0;
            Yenile(window);
            var scrim = Adli<Border>(window.Tabs, "TopScrim");
            var oynaticida = (scrim.IsVisible, scrim.Bounds.Height, scrim.Bounds.Top);
            var u = (LinearGradientBrush)window.FindResource("PlaybackTopScrimVeil")!;
            var a = (LinearGradientBrush)window.FindResource("PlaybackScrimVeil")!;
            var ust = (bas: u.StartPoint.ToString(), son: u.EndPoint.ToString(), duraklar: string.Join(" ", u.GradientStops.Select(d => $"{d.Offset}:{d.Color}")));
            var alt = (bas: a.StartPoint.ToString(), son: a.EndPoint.ToString(), duraklar: string.Join(" ", a.GradientStops.Select(d => $"{d.Offset}:{d.Color}")));
            var boy = (double)window.FindResource("PlaybackTopScrimHeight")!;
            var tetik = (double)window.FindResource("PlaybackTopRevealZone")!;

            window.Tabs.SelectedIndex = window.ShrinkTabIndex;
            Yenile(window);
            var baskaSekmede = Gorunur(scrim);

            return (oynaticida, ust, alt, boy, tetik, baskaSekmede, penceredeTetik: window.TopRevealZone);
        });
        _cikti.WriteLine($"oynaticida {sonuc.oynaticida}, boy {sonuc.boy}, tetik {sonuc.tetik}, baska sekmede {sonuc.baskaSekmede}");
        _cikti.WriteLine($"ust {sonuc.ust}, alt {sonuc.alt}");

        Assert.True(sonuc.oynaticida.IsVisible);
        Assert.NotEqual(sonuc.ust.bas, sonuc.ust.son);
        Assert.Equal(48, sonuc.boy);
        Assert.Equal(48, sonuc.tetik);
        Assert.Equal(sonuc.tetik, sonuc.penceredeTetik);
        Assert.Equal(sonuc.boy, sonuc.oynaticida.Height, 1);
        Assert.Equal(0, sonuc.oynaticida.Top, 1);
        Assert.False(sonuc.baskaSekmede);

        Assert.Equal(sonuc.alt.bas, sonuc.ust.son);
        Assert.Equal(sonuc.alt.son, sonuc.ust.bas);
        Assert.Equal(sonuc.alt.duraklar, sonuc.ust.duraklar);
    }

    private static System.Collections.Generic.IEnumerable<string> Anahat(TemplatedControl d)
        => d.GetVisualDescendants().OfType<Border>()
            .Where(b => Gorunur(b) && b.BorderThickness != default && b.BorderBrush is { } f && !(f is ISolidColorBrush s && s.Color.A == 0) && f.Opacity > 0)
            .Select(b => $"{d.Name ?? d.GetType().Name}/{b.Name} {b.BorderThickness}");

    /// <summary>Hover ve basılı zemin <c>NeonBlueFill</c>, yazı <c>TextBody</c>: <c>NeonBlueHover</c>/<c>NeonBlueActive</c>
    /// üstünde NeonBlue ve PinkText 36 paletin hepsinde 7:1'in altına düşüyordu (main 36334690557); NeonBlueFill üstünde de
    /// PinkText 13 palette, NeonBlue Teknesyum'da (6,61) kalıyor. TextBody dolgu üstünde 36 palette geçiyor.</summary>
    [Fact]
    public void BaslikDugmeleriAnahatsizHoverZeminle()
    {
        var sonuc = Pencerede(window =>
        {
            window.Tabs.SelectedIndex = 0;
            window.BtnUpdateBadge.IsVisible = true;
            Yenile(window);

            var dugmeler = window.TitleBarLayer.GetVisualDescendants().OfType<Button>().ToArray();
            var sekmeler = window.Tabs.Items.OfType<TabItem>().ToArray();
            var kenarli = dugmeler.Cast<TemplatedControl>().Concat(sekmeler)
                .Where(d => d.BorderThickness != default)
                .Select(d => $"{d.Name ?? d.GetType().Name} {d.BorderThickness}")
                .Concat(dugmeler.Cast<TemplatedControl>().Concat(sekmeler).SelectMany(Anahat))
                .ToArray();

            string Zemin(Button d, string sozde)
            {
                var sinif = (IPseudoClasses)d.Classes;
                sinif.Set(sozde, true);
                Yenile(window);
                var kok = Adli<Border>(d, "Root");
                var z = (kok.Background as ISolidColorBrush)?.Color.ToString() ?? "yok";
                z += " " + ((d.Foreground as ISolidColorBrush)?.Color.ToString() ?? "yok");
                var anahat = Anahat(d).ToArray();
                sinif.Set(sozde, false);
                Yenile(window);
                return anahat.Length == 0 ? z : "anahat " + string.Join(", ", anahat);
            }

            var renk = (string ad) => ((ISolidColorBrush)window.FindResource(ad)!).Color.ToString();
            var dil = window.LangSwitch.Children.OfType<Button>().First();
            return (
                sayi: dugmeler.Length + sekmeler.Length,
                kenarli,
                destekHover: Zemin(window.BtnSponsor, ":pointerover"),
                markaHover: Zemin(window.BtnGitHub, ":pointerover"),
                dilHover: Zemin(dil, ":pointerover"),
                markaBasili: Zemin(window.BtnGitHub, ":pressed"),
                destekBasili: Zemin(window.BtnSponsor, ":pressed"),
                dilBasili: Zemin(dil, ":pressed"),
                kapatHover: Zemin(window.BtnClose, ":pointerover"),
                kucultHover: Zemin(window.BtnMinimize, ":pointerover"),
                dolgu: renk("NeonBlueFill") + " " + renk("TextBody"),
                pembe: renk("NeonPinkFill"),
                mavi: renk("NeonBlue"));
        });
        _cikti.WriteLine($"{sonuc.sayi} parca; kenarli: {string.Join(", ", sonuc.kenarli)}");
        _cikti.WriteLine($"destek {sonuc.destekHover}/{sonuc.destekBasili}, marka {sonuc.markaHover}/{sonuc.markaBasili}, dil {sonuc.dilHover}/{sonuc.dilBasili}, kapat {sonuc.kapatHover}, kucult {sonuc.kucultHover}");

        Assert.True(sonuc.sayi >= 10, $"yalnız {sonuc.sayi} parça bulundu");
        Assert.Empty(sonuc.kenarli);
        Assert.Equal(sonuc.dolgu, sonuc.dilHover);
        Assert.Equal(sonuc.dolgu, sonuc.dilBasili);
        Assert.Equal(sonuc.dolgu, sonuc.markaHover);
        Assert.Equal(sonuc.dolgu, sonuc.markaBasili);
        Assert.Equal(sonuc.dolgu, sonuc.destekHover);
        Assert.Equal(sonuc.dolgu, sonuc.destekBasili);
        Assert.StartsWith(sonuc.pembe + " ", sonuc.kapatHover);
        Assert.StartsWith(sonuc.mavi + " ", sonuc.kucultHover);
    }
}
