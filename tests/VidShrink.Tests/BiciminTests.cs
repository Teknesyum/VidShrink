using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;

namespace VidShrink.Tests;

/// <summary>
/// T192 K2 ve K3.
///
/// <para><b>K2 — sayi bicimi kulturden gelir.</b> Iki yanlis vardi. <c>MainWindow.Num</c>
/// sabit <see cref="System.Globalization.CultureInfo.InvariantCulture"/> kullaniyordu, yani
/// Turkce arayuzde de nokta yaziyordu; buna karsilik ekranin bir bolumu <c>Num</c>'dan hic
/// gecmiyor, C# ara deger dizgesiyle (<c>$"{x:0.0}"</c>) yaziliyordu ve o dizge
/// <b>makinenin</b> kulturunu kullandigi icin Ingilizce arayuzde <c>15,6 MB</c> ciktisi
/// veriyordu. Olcu sabiti sabitle karsilastirmiyor: gercek bicimlendiriciyi iki dilde
/// cagirip ciktilarin birbirinden <b>farkli</b> oldugunu da tutuyor, boylece kulturu yeniden
/// sabitlemek bu olcuyu kirar.</para>
///
/// <para><b>K3 — baslik kurali cumleye uygulanmaz.</b> <c>fd6fe0c1</c>'in kurdugu
/// <c>ReadsAsProse</c> yalniz cumle isaretine bakiyordu; nokta tasimayan govde cumleleri
/// ("Load a file to see the two sides") baslik kolundan geciyordu. Ayni mekanizma
/// genisletildi: islev sozcugu tasiyan metin de govde sayilir.</para>
/// </summary>
public sealed class BiciminTests : IDisposable
{
    private readonly string _onceki = Strings.Language;

    public void Dispose() => Strings.Use(_onceki);

    [Theory]
    [InlineData("en", "15.6")]
    [InlineData("tr", "15,6")]
    public void OndalikAyiriciDildenGelir(string dil, string beklenen)
    {
        Strings.Use(dil);

        Assert.Equal(beklenen, MainWindow.Num(15.6, "0.0"));
    }

    [Theory]
    [InlineData("en", "3.7%")]
    [InlineData("tr", "%3,7")]
    public void YuzdeIsaretininYeriDildenGelir(string dil, string beklenen)
    {
        Strings.Use(dil);

        Assert.Equal(beklenen, MainWindow.Percent(0.037));
    }

    /// <summary>
    /// Sabit bir kulture geri donulurse iki dilin ciktisi esitlenir. Bu olcu esitligi
    /// reddediyor, dolayisiyla kulturu sabitleyen bir mutasyon kirmizi doner.
    /// </summary>
    [Fact]
    public void IkiDilinBicimiAyniDegildir()
    {
        Strings.Use("en");
        var ingilizceSayi = MainWindow.Num(15.6, "0.0");
        var ingilizceYuzde = MainWindow.Percent(0.037);

        Strings.Use("tr");
        var turkceSayi = MainWindow.Num(15.6, "0.0");
        var turkceYuzde = MainWindow.Percent(0.037);

        Assert.NotEqual(ingilizceSayi, turkceSayi);
        Assert.NotEqual(ingilizceYuzde, turkceYuzde);
    }

    /// <summary>
    /// Ekranda gorunen aralik satiri: <c>15.4 - 15.8 MB</c> ve yuzde. Tek tek sayi degil,
    /// satirin kendisi olculuyor.
    /// </summary>
    [Theory]
    [InlineData("en", "15.4 - 15.8 MB · 3.7%")]
    [InlineData("tr", "15,4 - 15,8 MB · %3,7")]
    public void TahminAraligiSatiriDileUyar(string dil, string beklenen)
    {
        Strings.Use(dil);

        var dusuk = MainWindow.Num(15.4, "0.0");
        var yuksek = MainWindow.Num(15.8, "0.0");
        var satir = $"{dusuk} - {yuksek} MB · " + MainWindow.Percent(0.037);

        Assert.Equal(beklenen, satir);
    }

    [Theory]
    [InlineData("playback.panel.hint", false)]
    [InlineData("main.plan.title", false)]
    [InlineData("main.section.quality", false)]
    [InlineData("main.section.frame", false)]
    [InlineData("playback.panel.hint", true)]
    [InlineData("main.section.quality", true)]
    public void GovdeCumlesiDilDosyasindakiGibiKalir(string anahtar, bool turkce)
    {
        var ham = Strings.GetIn(turkce ? "tr" : "en", anahtar);

        Assert.True(LanguageCatalog.ReadsAsProse(ham), $"'{ham}' govde sayilmadi");
        Assert.Equal(ham, LanguageCatalog.Title(ham, turkce));
    }

    /// <summary>
    /// Karsi yon: gercek basliklar kelime kelime buyutulmeye devam eder. Bu olmasa
    /// <c>ReadsAsProse</c>'u her zaman <c>true</c> dondurmek yesil kalirdi.
    /// </summary>
    [Theory]
    [InlineData("main.info.video-codec", false, "Video Codec")]
    [InlineData("main.info.fps", false, "FPS")]
    [InlineData("main.output.current-size", false, "Current Output Size")]
    [InlineData("main.info.video-codec", true, "Video Kodeği")]
    public void BaslikKelimeKelimeBuyutulmeyeDevamEder(string anahtar, bool turkce, string beklenen)
    {
        var ham = Strings.GetIn(turkce ? "tr" : "en", anahtar);

        Assert.False(LanguageCatalog.ReadsAsProse(ham), $"'{ham}' yanlislikla govde sayildi");
        Assert.Equal(beklenen, LanguageCatalog.Title(ham, turkce));
    }
}

/// <summary>
/// T192 K4 ve dorduncu madde. Olcum basiz pencerede, gercek yerlesim motoruyla.
///
/// <para><b>Kaynak bilgi izgarasi.</b> <c>InfoGrid</c> bir <see cref="UniformGrid"/>:
/// dort sutun esit genislikte ve hucre kirpmiyor. Turkce etiket Ingilizceden uzun
/// oldugu icin "Video kodeği" kendi hucresinden tasip yanindaki "Ses" etiketinin
/// uzerine biniyordu. Olcu her etiketin genisligini kendi hucresinin genisligiyle
/// karsilastirir; tasma varsa kirmizi doner.</para>
///
/// <para><b>Katlanmis bolum ozeti.</b> Baslik satiri yatay bir <see cref="StackPanel"/>
/// idi; yatay yigin cocuguna sonsuz genislik verir, dolayisiyla ozet kendi istedigi
/// genislikte olculur ve panel kenarinda dumduz kesilirdi ("Kare Hızı D…" degil,
/// harfin ortasindan). Satir <see cref="Grid"/>'e cevrildi ve ozet yildiz sutunda
/// ucnoktayla kisaliyor.</para>
///
/// <para><b>Turetme satiri.</b> Satir <c>TxtTarget.Text</c>'i okur ama yalnizca boyut
/// tavani kapaliyken yenileniyordu; kutuda 24 yazarken satir "Hedef 16 MB" diye
/// kaliyordu.</para>
/// </summary>
public sealed class KareYerlesimTests
{
    private static readonly Size WindowSize = new(1600, 1000);

    private static T Read<T>(string dil, Func<MainWindow, T> read) =>
        AppHost.Run(() =>
        {
            var onceki = Strings.Language;
            Strings.Use(dil);
            try
            {
                var window = new MainWindow
                {
                    Width = double.NaN,
                    Height = double.NaN
                };

                window.Measure(WindowSize);
                window.Arrange(new Rect(WindowSize));
                window.UpdateLayout();

                Visual? node = window.GetVisualDescendants().OfType<UniformGrid>()
                    .Single(grid => grid.Name == "InfoGrid");
                while (node is not null)
                {
                    if (node is Control control) control.IsVisible = true;
                    node = node.GetVisualParent();
                }

                window.Measure(WindowSize);
                window.Arrange(new Rect(WindowSize));
                window.UpdateLayout();
                return read(window);
            }
            finally
            {
                Strings.Use(onceki);
            }
        });

    private static T Named<T>(MainWindow window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    [Theory]
    [InlineData("tr")]
    [InlineData("en")]
    public void KaynakBilgiEtiketleriKendiHucresindeKalir(string dil)
    {
        var tasan = Read(dil, window =>
        {
            var grid = window.GetVisualDescendants().OfType<UniformGrid>()
                .Single(g => g.Name == "InfoGrid");

            return grid.Children
                .OfType<StackPanel>()
                .Select(cell => (cell, label: cell.Children.OfType<TextBlock>().First()))
                .Where(pair => true)
                .Select(pair => $"{pair.label.Text}: metin {pair.label.TextLayout.Width:0.#} px, hucre {pair.cell.Bounds.Width:0.#} px")
                .ToList();
        });

        Assert.True(tasan.Count == 0, string.Join(Environment.NewLine, tasan));
    }

    /// <summary>
    /// Uzun bir ozet metni panelin disina tasmaz. Metin dogrudan yaziliyor cunku olculen
    /// sey ozetin icerigi degil, satirin uzun metni nasil tasidigi.
    /// </summary>
    [Theory]
    [InlineData("TxtFrameSummary")]
    [InlineData("TxtQualitySummary")]
    [InlineData("TxtAudioSummary")]
    [InlineData("TxtAdvancedSummary")]
    public void KatlanmisBolumOzetiSatirinIcindeKalir(string ad)
    {
        var (ozetSagi, satirGenisligi) = Read("tr", window =>
        {
            var ozet = Named<TextBlock>(window, ad);
            ozet.Text = "Çözünürlük Düşürülebilir · Kare Hızı Düşürülebilir · Ses Yeniden Kodlanabilir";

            window.Measure(WindowSize);
            window.Arrange(new Rect(WindowSize));
            window.UpdateLayout();

            var satir = (Layoutable)ozet.GetVisualParent()!;
            return (ozet.Bounds.Left + ozet.TextLayout.Width, satir.Bounds.Width);
        });

        Assert.True(ozetSagi <= satirGenisligi + 0.5,
            $"ozet metni {ozetSagi:0.#} px'te bitiyor, satir {satirGenisligi:0.#} px");
    }

    [Fact]
    public void TuretmeSatiriHedefKutusunuIzler()
    {
        var satir = Read("en", window =>
        {
            Named<TextBox>(window, "TxtTarget").Text = "24";
            window.UpdateLayout();
            return Named<TextBlock>(window, "TxtChipDerivation").Text ?? "";
        });

        Assert.Contains("24", satir);
        Assert.DoesNotContain("16", satir);
    }
}
