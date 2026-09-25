using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using Xunit.Abstractions;

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
    /// <b>Ne olculuyor:</b> aralik satirinin <b>bilesenleri</b> ve bunlarin yan yana
    /// gelmis hali — ekran yolu degil. Bicim dizgesi burada kopyalanmis durumda,
    /// dolayisiyla bu olcu <c>RefreshEstimateView</c>'deki bir degisikligi yakalamaz.
    /// Ekranin kendi satiri
    /// <see cref="KareYerlesimTests.TahminAraligiSatiriEkrandaDileUyar"/> ile olculuyor;
    /// T192 tur 2'ye kadar bu ayrim rapor tablosunda yaziyordu ama testin adinda
    /// yazmiyordu.
    /// </summary>
    [Theory]
    [InlineData("en", "15.4 - 15.8 MB · 3.7%")]
    [InlineData("tr", "15,4 - 15,8 MB · %3,7")]
    public void AralikSatirininBilesenleriDileUyar(string dil, string beklenen)
    {
        Strings.Use(dil);

        var dusuk = MainWindow.Num(15.4, "0.0");
        var yuksek = MainWindow.Num(15.8, "0.0");
        var satir = $"{dusuk} - {yuksek} MB · " + MainWindow.Percent(0.037);

        Assert.Equal(beklenen, satir);
    }

    /// <summary>
    /// T192 tur 2 K9: <c>DescribeBytes</c> paylasim tavanini ekrana yaziyor
    /// (<c>TxtShareCeiling</c>). Tam ikilik katlar (128 MiB, 25 GiB) ondalik tasimadigi
    /// icin <c>SettingsTabTests</c>'teki pimler bu degisiklikten etkilenmiyor; ondalikli
    /// bir tavan ise dile uyar.
    /// </summary>
    [Theory]
    [InlineData("en", "1.5 GiB")]
    [InlineData("tr", "1,5 GiB")]
    public void PaylasimTavaniDileUyar(string dil, string beklenen)
    {
        Strings.Use(dil);

        Assert.Equal(beklenen, MainWindow.DescribeBytes(1_610_612_736L));
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
        Assert.Equal(ham, LanguageCatalog.Title(ham, turkce ? "tr" : "en"));
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
        Assert.Equal(beklenen, LanguageCatalog.Title(ham, turkce ? "tr" : "en"));
    }
}

/// <summary>
/// T192 K4 ve dorduncu madde. Olcum basiz pencerede, gercek yerlesim motoruyla.
///
/// <para><b>Kaynak bilgi izgarasi.</b> <c>InfoGrid</c> bir <see cref="SutunIzgara"/>:
/// en fazla dort sutun, her sutun kendi en genis hucresi kadar; toplam sigmazsa iki, sonra bir sutuna iner
/// (2026-09-23: sabit dort sutunda deger her dilde ucnoktayla kesiliyordu). Once
/// hucre kirpmiyordu. Turkce etiket Ingilizceden uzun
/// oldugu icin "Video kodeği" kendi hucresinden tasip yanindaki "Ses" etiketinin
/// uzerine biniyordu. Olcu her etiketin genisligini kendi hucresinin genisligiyle
/// karsilastirir; tasma varsa kirmizi doner.</para>
///
/// <para><b>Katlanmis bolum ozeti.</b> Baslik satiri yatay bir <see cref="StackPanel"/>
/// idi; yatay yigin cocuguna sonsuz genislik verir, dolayisiyla ozet kendi istedigi
/// genislikte olculur ve panel kenarinda dumduz kesilirdi ("Kare Hızı D…" degil,
/// harfin ortasindan). Satir <see cref="Grid"/>'e cevrildi; S20 taramasi ucnoktali ozetin
/// dar pencerede 10 px'e indigini gosterdi, ozet artik basligin altinda kendi satirinda
/// sariliyor ve bossa gizleniyor.</para>
///
/// <para><b>Turetme satiri.</b> Satir <c>TxtTarget.Text</c>'i okur ama yalnizca boyut
/// tavani kapaliyken yenileniyordu; kutuda 24 yazarken satir "Hedef 16 MB" diye
/// kaliyordu.</para>
/// </summary>
public sealed class KareYerlesimTests
{
    /// <summary>
    /// Karenin cekildigi olcu. Pencerenin izin verdigi en dar olcu (1040x720,
    /// <c>MainWindow.axaml</c>, <c>MinWidth="1040"</c>) de olculuyor: T194 karari tek satir,
    /// sigmayan metin ucnoktayla kisalir ve tam hali balonda durur.
    /// </summary>
    public static readonly Size Genis = new(1600, 1000);

    public static readonly Size Dar = new(1040, 720);

    public static TheoryData<string, double, double> IkiDilTekOlcu() => new()
    {
        { "tr", 1600, 1000 },
        { "en", 1600, 1000 },
        { "tr", 1040, 720 },
        { "en", 1040, 720 }
    };

    private static T Read<T>(string dil, Func<MainWindow, T> read) => Read(dil, Genis, read);

    private static T Read<T>(string dil, Size olcu, Func<MainWindow, T> read) =>
        AppHost.Run(() =>
        {
            var onceki = Strings.Language;
            Strings.Use(dil);
            try
            {
                var window = new MainWindow
                {
                    Width = olcu.Width,
                    Height = olcu.Height
                };

                if (window.Content is Layoutable govde)
                {
                    govde.Width = olcu.Width;
                    govde.Height = olcu.Height;
                }

                window.Measure(olcu);
                window.Arrange(new Rect(olcu));
                window.UpdateLayout();

                Visual? node = window.GetVisualDescendants().OfType<SutunIzgara>()
                    .Single(grid => grid.Name == "InfoGrid");
                while (node is not null)
                {
                    if (node is Control control) control.IsVisible = true;
                    node = node.GetVisualParent();
                }

                window.Measure(olcu);
                window.Arrange(new Rect(olcu));
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

    /// <summary>
    /// Etiketin <b>sarilmamis</b> genisligi olculuyor. T192 tur 1'de olcu
    /// <c>label.TextLayout.Width</c>'e bakiyordu, ama etiketlerde
    /// <c>TextWrapping="Wrap"</c> var (<c>MainWindow.axaml:262-291</c>): sarilan metnin
    /// yerlesim genisligi hucreyi <b>hic asamaz</b>, dolayisiyla kosul her zaman yanlis
    /// donuyordu ve olcu mutasyona oluydu. Sarma kapatilip yeniden olculuyor; boylece
    /// etiket uzarsa olcu kirmiziya doner.
    /// <see cref="OlcuUzunEtiketiYakalar"/> bunu ayrica kanitliyor.
    /// </summary>
    [Theory]
    [MemberData(nameof(IkiDilTekOlcu))]
    public void KaynakBilgiEtiketleriKendiHucresindeKalir(string dil, double genislik, double yukseklik)
    {
        var olcu = new Size(genislik, yukseklik);
        var (tasan, satirlar, sutun) = Read(dil, olcu, window =>
        {
            var (t, s) = Tasanlar(window, olcu, null, kirpmaKapali: false, balonKapali: false);
            return (t, s, Named<SutunIzgara>(window, "InfoGrid").Columns);
        });

        var klasor = Path.Combine(TipSources.Root, ".calisma", "t194");
        var ad = $"infogrid-{dil}-{genislik:0}x{yukseklik:0}.txt";
        Directory.CreateDirectory(klasor);
        File.WriteAllLines(Path.Combine(klasor, ad), satirlar);

        Assert.True(tasan.Count == 0, string.Join(Environment.NewLine, tasan));
        Assert.DoesNotContain(satirlar, satir => satir.Contains("kisaldi", StringComparison.Ordinal));
        if (olcu == Dar) Assert.True(sutun < 4, $"dar pencerede {sutun} sutun");

        Kapat(klasor, ad);
    }

    /// <summary>
    /// Olcunun mutasyon sinavi: hucreye sigmayacak kadar uzun bir etiketin kirpmasi ya da
    /// balonu kaldirilinca olcu <b>kirmizi</b> donmeli. Donmezse yukaridaki yesil bir sey
    /// soylemiyor demektir.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void OlcuUzunEtiketiYakalar(bool kirpmaKapali, bool balonKapali)
    {
        var (tasan, _) = Read("tr", Genis, window => Tasanlar(window, Genis, string.Concat(Enumerable.Repeat("Cok Uzun Bir Kaynak Bilgi Etiketi Ornegi Daha Da Uzun ", 6)), kirpmaKapali, balonKapali));

        Assert.NotEmpty(tasan);
    }

    /// <summary>
    /// Her hucrenin metinlerini yeniden yerlestirir. Kural: tek gorsel satir, hucre
    /// genisligini asmaz; dogal genisligi hucreyi asan metin ucnoktayla kisalir ve balonu
    /// metnin tam halini tasir. <paramref name="mutasyon"/> verilirse ilk etiketin metni
    /// onunla degistirilir.
    /// </summary>
    private static (List<string> Tasan, List<string> Satirlar) Tasanlar(MainWindow window, Size olcu, string? mutasyon, bool kirpmaKapali, bool balonKapali)
    {
        var grid = window.GetVisualDescendants().OfType<SutunIzgara>()
            .Single(g => g.Name == "InfoGrid");

        var hucreler = grid.Children
            .OfType<StackPanel>()
            .SelectMany(cell => cell.Children.OfType<TextBlock>().Select(text => (cell, text)))
            .ToList();

        foreach (var (_, text) in hucreler)
            if (string.IsNullOrEmpty(text.Text)) text.Text = "hevc (Main 10) · 3840x2160";

        if (mutasyon is not null)
        {
            var ilk = hucreler[0].text;
            ilk.Text = mutasyon;
            if (kirpmaKapali) ilk.TextTrimming = TextTrimming.None;
            if (balonKapali) ToolTip.SetTip(ilk, null);
        }

        window.Measure(olcu);
        window.Arrange(new Rect(olcu));
        window.UpdateLayout();

        var tasan = new List<string>();
        var satirlar = new List<string>();
        foreach (var (cell, text) in hucreler)
        {
            var dogal = new TextBlock
            {
                Text = text.Text,
                FontFamily = text.FontFamily,
                FontSize = text.FontSize,
                FontWeight = text.FontWeight,
                FontStyle = text.FontStyle,
                LetterSpacing = text.LetterSpacing,
                TextWrapping = TextWrapping.NoWrap
            };
            dogal.Measure(Size.Infinity);
            var dogalGenislik = dogal.DesiredSize.Width;
            var hucre = cell.Bounds.Width;
            var kisaldi = dogalGenislik > hucre + 0.5;
            var satir = text.TextLayout.TextLines.Count;
            var cizilen = text.TextLayout.Width;
            var balon = ToolTip.GetTip(text) as string;

            satirlar.Add($"{text.Text}: dogal {dogalGenislik:0.#} px, cizilen {cizilen:0.#} px, hucre {hucre:0.#} px, satir {satir}, {(kisaldi ? "kisaldi" : "sigdi")}, balon {(balon == text.Text ? "tam" : balon ?? "yok")}");

            if (satir != 1) tasan.Add($"{text.Text}: {satir} satir");
            if (cizilen > hucre + 0.5) tasan.Add($"{text.Text}: cizilen {cizilen:0.#} px, hucre {hucre:0.#} px");
            if (kisaldi && text.TextTrimming == TextTrimming.None) tasan.Add($"{text.Text}: kisalmasi gerekiyor, kirpma yok");
            if (kisaldi && balon != text.Text) tasan.Add($"{text.Text}: kisaldi ama balonda tam metin yok ({balon ?? "yok"})");
        }

        return (tasan, satirlar);
    }

    /// <summary>
    /// Uzun bir ozet metni panelin disina tasmaz. Metin dogrudan yaziliyor cunku olculen
    /// sey ozetin icerigi degil, satirin uzun metni nasil tasidigi.
    ///
    /// <para>O3: satirin icinde kalmak tek basina sarmayi olcmuyor — <c>TextWrapping="Wrap"</c>
    /// yerine kirpma konsa metin yine satirin icinde kalirdi. Bu yuzden sarma niteligi kendi
    /// pimini aldi: uzun metin birden fazla satira boluniyor ve blokta kirpma yok.</para>
    /// </summary>
    [Theory]
    [InlineData("TxtFrameSummary")]
    [InlineData("TxtQualitySummary")]
    [InlineData("TxtAudioSummary")]
    [InlineData("TxtAdvancedSummary")]
    public void KatlanmisBolumOzetiSatirinIcindeKalir(string ad)
    {
        var okunan = Read("tr", window =>
        {
            var ozet = Named<TextBlock>(window, ad);
            ozet.Text = "Çözünürlük Düşürülebilir · Kare Hızı Düşürülebilir · Ses Yeniden Kodlanabilir · Altyazı Korunur · Bölümler Korunur";

            window.Measure(Genis);
            window.Arrange(new Rect(Genis));
            window.UpdateLayout();

            var satir = (Layoutable)ozet.GetVisualParent()!;
            return (sag: ozet.Bounds.Left + ozet.TextLayout.Width, genislik: satir.Bounds.Width,
                metinSatiri: ozet.TextLayout.TextLines.Count, kirpma: ozet.TextTrimming);
        });

        Assert.True(okunan.sag <= okunan.genislik + 0.5,
            $"ozet metni {okunan.sag:0.#} px'te bitiyor, satir {okunan.genislik:0.#} px");
        Assert.True(okunan.metinSatiri > 1, $"{ad} sarmadi: {okunan.metinSatiri} satir");
        Assert.Equal(TextTrimming.None, okunan.kirpma);
    }

    private const string OrnekYol = @"C:\Kayitlar\tatil-cekimi-2160p60.mkv";

    /// <summary>
    /// <c>WindowLayoutTests.Sample</c> ile ayni tasiyici kaynak. Yoklama cagrilmiyor;
    /// olcum diskteki hicbir dosyaya ve hicbir dis araca bagli degil.
    /// </summary>
    private static MediaInfo Ornek() => new()
    {
        FilePath = OrnekYol,
        FileSizeBytes = 420_000_000L,
        DurationSeconds = 187.5,
        Width = 3840,
        Height = 2160,
        Fps = 59.94,
        VideoCodec = "hevc",
        TotalBitrateBps = 18_800_000,
        AudioCodec = "aac",
        AudioBitrateBps = 192_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    /// <summary>
    /// Gercek yukleme yolu: <c>LoadWithoutProbing</c> -> <c>ApplyLoaded</c> -> <c>ShowInfo</c>
    /// ve <c>Recalculate</c>. Ekrana yazan kod bu yoldan geciyor, dolayisiyla bicim dizgesi
    /// testte kopyalanmiyor.
    /// </summary>
    private static T Yuklu<T>(string dil, Func<MainWindow, T> read) => Yuklu(dil, Ornek(), read);

    private static T Yuklu<T>(string dil, MediaInfo bilgi, Func<MainWindow, T> read) =>
        AppHost.Run(() =>
        {
            var onceki = Strings.Language;
            Strings.Use(dil);
            try
            {
                var window = new MainWindow
                {
                    Width = Genis.Width,
                    Height = Genis.Height
                };

                if (window.Content is Layoutable govde)
                {
                    govde.Width = Genis.Width;
                    govde.Height = Genis.Height;
                }

                window.LoadWithoutProbing(OrnekYol, bilgi);
                window.SettleFades();
                window.Measure(Genis);
                window.Arrange(new Rect(Genis));
                window.UpdateLayout();
                return read(window);
            }
            finally
            {
                Strings.Use(onceki);
            }
        });

    /// <summary>
    /// T192 tur 2 K9. <c>TxtFps</c> sabit <see cref="System.Globalization.CultureInfo.InvariantCulture"/>
    /// ile yaziliyordu; Turkce arayuzde kare hizi <c>29.97</c> gorunuyordu. Olcu gercek
    /// yukleme yolundan geciyor.
    /// </summary>
    [Theory]
    [InlineData("en", "59.94")]
    [InlineData("tr", "59,94")]
    public void KaynakBilgiKareHiziDileUyar(string dil, string beklenen)
        => Assert.Equal(beklenen, Yuklu(dil, window => Named<TextBlock>(window, "TxtFps").Text));

    /// <summary>
    /// Kaynak bilgisindeki cozunurluk ailenin carpi isaretini kullanir. Bu satirin hic
    /// pimi yoktu: ayni uygulama <c>1920x1080</c> (kaynak bilgisi, plan gercegi, gelismis
    /// panel), <c>1920×1080</c> (oynatici, kaydedici ozeti) ve <c>1920 × 1080</c> (bolge
    /// secicileri) diye uc turlu yaziyordu. Kultur almaz: tam sayida basamak ayraci
    /// istemiyoruz, iki dilde ayni yazim bekleniyor.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    public void KaynakBilgiCozunurluguCarpiIsaretiyle(string dil)
    {
        var yazi = Yuklu(dil, window => Named<TextBlock>(window, "TxtResolution").Text ?? "");

        Assert.Equal("3840×2160", yazi);
        Assert.DoesNotContain("x", yazi);
    }

    /// <summary>
    /// Plan panelinin ve gelismis panelin cozunurluk satirlari da ailenin yazimini
    /// kullanir. Ikisi de <c>$"{Width}x{Height}"</c> ile kuruluyordu ve **hicbir olcu
    /// okumuyordu**: adim 5'in mutasyonunda ikisi de 0 kirmizi verdi. Deger satiri
    /// anahtarin yaninda duruyor; "x yok" iddiasi tum panele degil o hucreye yazili
    /// (kodek adi <c>libx264</c> panelde x tasiyor).
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    public void PlanVeGelismisPanelCozunurluguCarpiIsaretiyle(string dil)
    {
        var okunan = Yuklu(dil, window =>
        {
            var hucreler = window.FindControl<Panel>("PlanFacts")!.Children.OfType<TextBlock>()
                .Select(t => t.Text ?? "").ToArray();
            return (hucreler, gelismis: Named<TextBlock>(window, "TxtAdvMinResolutionNow").Text ?? "");
        });

        Strings.Use(dil);
        var anahtar = Array.IndexOf(okunan.hucreler, Strings.Get("main.plan.fact.resolution"));

        Assert.True(anahtar >= 0, string.Join(" | ", okunan.hucreler));

        var deger = okunan.hucreler[anahtar + 1];

        Assert.Contains("×", deger);
        Assert.DoesNotContain("x", deger);
        Assert.Contains("×", okunan.gelismis);
        Assert.DoesNotContain("x", okunan.gelismis);
    }

    /// <summary>
    /// Bit hizi ekranda <b>yuvarlanir</b>, kirpilmaz. Bolme tamsayi yapildiginda
    /// 1.499.600 bps ekrana 1499 kbps diye cikiyordu; ayni deger baska bir akista
    /// <c>double</c> ile bolunup 1500 yaziyordu, yani tek kaynak iki sayi gosterebiliyordu.
    /// Govde (<see cref="Bicim.BitHizi.BpsToKbps"/>) tek bolme yapar. Ses izi ayri
    /// pimli cunku iki satir iki ayri cagri yeri.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    public void KaynakBilgiBitHiziYuvarlanir(string dil)
    {
        var bilgi = Ornek() with { TotalBitrateBps = 1_499_600, AudioBitrateBps = 191_500 };

        var okunan = Yuklu(dil, bilgi, window => (
            hiz: Named<TextBlock>(window, "TxtBitrate").Text ?? "",
            ses: Named<TextBlock>(window, "TxtAudio").Text ?? ""));

        Assert.Contains("1500", okunan.hiz);
        Assert.DoesNotContain("1499", okunan.hiz);
        Assert.Contains("192", okunan.ses);
        Assert.DoesNotContain("191", okunan.ses);
    }

    /// <summary>
    /// T192 tur 2, tur 1 borcu 3: tahmin araligi satirinin <b>ekrandaki</b> hali.
    /// <c>RefreshEstimateView</c> gercekten kosuyor; bicim dizgesi testte kopyalanmiyor.
    /// </summary>
    [Theory]
    [InlineData("en", ".", ",")]
    [InlineData("tr", ",", ".")]
    public void TahminAraligiSatiriEkrandaDileUyar(string dil, string ayirici, string yabanci)
    {
        var satir = Yuklu(dil, window => Named<TextBlock>(window, "TxtEstimateRange").Text ?? "");

        Assert.Contains("MB", satir);
        Assert.Contains(ayirici, satir);
        Assert.DoesNotContain(yabanci, satir);
    }

    /// <summary>
    /// S9: dile göre <b>değişen</b> birimler (MB, AI, <c>score-suffix</c>,
    /// <c>score-value</c>, <c>advanced.mode.crf</c>) gerçek yükleme yolundan okunur.
    /// Fransızca "Mo" ve "IA", Rusça "МБ" ve "ИИ" yazar; İngilizce sabit ("MB", "AI")
    /// o dillerde görünmez. <c>kbps-value</c>, <c>fps-value</c>, <c>k-value</c> ve
    /// <c>plan.mode.crf-value</c> şablonları 42 dilde bayt bayt aynı, bu yüzden dil kolu
    /// onları ayirt edemez; burada yalnız metnin biriminde bittiği pimlenir, şablonun dil
    /// dosyasından geldiği <c>HizKareKbitVeCrfBirimleriDilDosyasindanGelir</c>'de enjeksiyonla
    /// ölçülür.
    /// </summary>
    [Theory]
    [InlineData("en", "MB", "AI")]
    [InlineData("fr", "Mo", "IA")]
    [InlineData("ru", "МБ", "ИИ")]
    public void BirimlerDilDosyasindanGelir(string dil, string mb, string ai)
    {
        var okunan = Yuklu(dil, window =>
        {
            var hedef = Named<TextBox>(window, "TxtTarget");
            var hedefIzgara = ((Grid)hedef.Parent!).Children;
            var hedefBirimi = (hedefIzgara[hedefIzgara.IndexOf(hedef) + 1] as TextBlock)?.Text ?? "";
            var kalite = Named<TextBox>(window, "TxtQualityTarget");
            var kaliteIzgara = ((Grid)kalite.Parent!).Children;
            var kaliteBirimi = (kaliteIzgara[kaliteIzgara.IndexOf(kalite) + 1] as TextBlock)?.Text ?? "";
            var kip = (window.FindControl<ComboBox>("CmbQualityMode")!.Items[0] as ComboBoxItem)?.Content?.ToString() ?? "";
            var plan = string.Join(" | ", window.FindControl<Panel>("PlanFacts")!.Children.OfType<TextBlock>().Select(t => t.Text));
            return (hedefBirimi, kaliteBirimi, kip,
                boyut: Named<TextBlock>(window, "TxtSize").Text ?? "",
                hiz: Named<TextBlock>(window, "TxtBitrate").Text ?? "",
                aralik: Named<TextBlock>(window, "TxtEstimateRange").Text ?? "",
                not: Named<TextBlock>(window, "TxtEstimateNote").Text ?? "",
                plan);
        });

        Strings.Use(dil);
        try
        {
            var kanit = Path.Combine(GirdiKanit.Root, ".calisma", "s9-birimler");
            Directory.CreateDirectory(kanit);
            var dosya = Path.Combine(kanit, dil + ".txt");
            File.WriteAllText(dosya, okunan.ToString());

            Assert.Equal(ai, Strings.Get("main.plan.ai"));
            Assert.Equal(mb, okunan.hedefBirimi);
            Assert.EndsWith(" " + mb, okunan.boyut);
            Assert.EndsWith(" " + mb, okunan.aralik.Split(" · ")[0]);
            Assert.Equal(Strings.Get("main.unit.score-suffix"), okunan.kaliteBirimi);
            Assert.EndsWith(Strings.Get("main.unit.score-value", ""), okunan.not);
            Assert.Equal(Strings.Get("main.advanced.mode.crf"), okunan.kip);
            Assert.Equal(Strings.Get("main.unit.kbps-value", "18800"), okunan.hiz);
            Assert.DoesNotContain("kbps", okunan.hiz, StringComparison.Ordinal);
            Assert.Contains(" FPS", okunan.plan);
            if (dil != "en")
            {
                Assert.DoesNotContain(" MB", okunan.boyut + okunan.aralik + okunan.plan);
                Assert.NotEqual("MB", okunan.hedefBirimi);
            }

            File.Delete(dosya);
            if (Directory.GetFileSystemEntries(kanit).Length == 0) Directory.Delete(kanit);
        }
        finally
        {
            Strings.Use("en");
        }
    }

    /// <summary>
    /// Dil dosyalarinin tamamini <c>.calisma</c> altina kopyalar ve Ingilizce katalogda
    /// verilen sablonlari degistirir. Sablonu koddan yazan bir mutasyon bu enjeksiyondan
    /// etkilenmez, dolayisiyla olcum sablonun gercekten dil dosyasindan okundugunu tutar.
    /// </summary>
    /// <summary>
    /// <see cref="Kopya"/>'nin biraktigi <c>.calisma/&lt;ad&gt;</c> klasorunu kapatir. Denetim
    /// borcu: 42 dilin tam kopyasi ve kanit dosyasi agacta kaliyordu. <see cref="Tut"/> acikken
    /// silinmez — testler iddialardan <b>sonra</b> kapatir, boylece kirmizi bir kosumun kaniti
    /// yerinde durur, yesil kosum kendi biraktigini siler.
    /// </summary>
    private sealed class DilKopyasi : IDisposable
    {
        public DilKopyasi(string klasor, string locales) { Klasor = klasor; Locales = locales; }

        public string Klasor { get; }

        public string Locales { get; }

        public bool Tut { get; set; } = true;

        public void Dispose()
        {
            if (Tut) return;
            try { if (Directory.Exists(Klasor)) Directory.Delete(Klasor, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static DilKopyasi Kopya(string ad, Dictionary<string, string> degisim)
    {
        var kaynak = Path.Combine(AppContext.BaseDirectory, "Locales");
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", ad);
        var kopya = Path.Combine(klasor, "Locales");
        foreach (var dosya in Directory.GetFiles(kaynak, "*", SearchOption.AllDirectories))
        {
            var hedef = Path.Combine(kopya, Path.GetRelativePath(kaynak, dosya));
            Directory.CreateDirectory(Path.GetDirectoryName(hedef)!);
            File.Copy(dosya, hedef, overwrite: true);
        }

        var enAna = Path.Combine(kopya, "en", "main.json");
        var metin = File.ReadAllText(enAna);
        foreach (var (arama, yazi) in degisim)
        {
            Assert.Contains(arama, metin);
            metin = metin.Replace(arama, yazi);
        }

        File.WriteAllText(enAna, metin);
        return new DilKopyasi(klasor, kopya);
    }

    /// <summary>
    /// S9 borcu: kalite yongasının balonundaki "tahmini kalite" satırı <c>/100</c>'ü koddan
    /// yazıyordu. Dil dosyasının kopyasında <c>main.unit.score-value</c> başka bir biçime
    /// çevrilir; yüklenen pencerenin balon ızgarasında o biçim okunur, <c>/100</c> okunmaz.
    /// </summary>
    [Fact]
    public void KaliteBalonundakiPuanDilDosyasindanGelir()
    {
        using var kopya = Kopya("s9-puan", new Dictionary<string, string>
        {
            ["\"main.unit.score-value\": \"{0}/100\""] = "\"main.unit.score-value\": \"{0} of 100 pts\""
        });

        List<string> satirlar;
        AppHost.Run(() => Strings.UseRoot(kopya.Locales));
        try
        {
            satirlar = Yuklu("en", window =>
            {
                var cip = Named<Button>(window, "Chip25");
                var balon = (StackPanel)ToolTip.GetTip(cip)!;
                return balon.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
            });
        }
        finally
        {
            AppHost.Run(() => Strings.UseRoot(null));
        }

        Assert.True(satirlar.Any(s => s.EndsWith(" of 100 pts", StringComparison.Ordinal)), string.Join(" | ", satirlar));
        Assert.DoesNotContain(satirlar, s => s.EndsWith("/100", StringComparison.Ordinal));

        kopya.Tut = false;
    }

    /// <summary>
    /// O2: <c>main.unit.kbps-value</c>, <c>main.unit.fps-value</c> ve
    /// <c>main.plan.mode.crf-value</c> sablonlari 42 dilde bayt bayt ayni, bu yuzden dil
    /// kollari onlari ayirt edemiyor; sabiti koda geri yazan iki mutasyon dil kollarindan
    /// sag kaliyordu. Burada dil dosyasinin kopyasinda ucu de baska bir bicime cevrilir;
    /// yuklenen pencerenin hiz satiri, ses satiri ve plan izgarasi o bicimleri okur,
    /// Ingilizce sabitler hicbirinde gorunmez.
    /// </summary>
    [Fact]
    public void HizKareKbitVeCrfBirimleriDilDosyasindanGelir()
    {
        using var kopya = Kopya("s9-birim", new Dictionary<string, string>
        {
            ["\"main.unit.kbps-value\": \"{0} kbit/s\""] = "\"main.unit.kbps-value\": \"{0} kilobit/s\"",
            ["\"main.unit.fps-value\": \"{0} FPS\""] = "\"main.unit.fps-value\": \"{0} kare/s\"",
            ["\"main.plan.mode.crf-value\": \"CRF {0}\""] = "\"main.plan.mode.crf-value\": \"kalite carpani {0}\""
        });

        List<string> satirlar;
        AppHost.Run(() => Strings.UseRoot(kopya.Locales));
        try
        {
            satirlar = Yuklu("en", window =>
            {
                var okunan = new List<string>
                {
                    Named<TextBlock>(window, "TxtBitrate").Text ?? "",
                    Named<TextBlock>(window, "TxtAudio").Text ?? ""
                };
                okunan.AddRange(window.FindControl<Panel>("PlanFacts")!.Children.OfType<TextBlock>().Select(t => t.Text ?? ""));
                window.AdvModeIndex = 1;
                window.RecalculateForTest();
                window.UpdateLayout();
                okunan.AddRange(window.FindControl<Panel>("PlanFacts")!.Children.OfType<TextBlock>().Select(t => t.Text ?? ""));
                return okunan;
            });
        }
        finally
        {
            AppHost.Run(() => Strings.UseRoot(null));
        }

        var govde = string.Join(" | ", satirlar);
        var kanit = Path.Combine(GirdiKanit.Root, ".calisma", "s9-birim");
        Directory.CreateDirectory(kanit);
        File.WriteAllText(Path.Combine(kanit, "okunan.txt"), govde);

        Assert.EndsWith(" kilobit/s", satirlar[0], StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(" kilobit/s", satirlar[1], StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" kare/s", govde, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kalite carpani ", govde, StringComparison.OrdinalIgnoreCase);
        var kucuk = govde.ToLowerInvariant();
        Assert.Equal(5, kucuk.Split(" kilobit/s", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, kucuk.Split(" kare/s", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("kbit/s", govde, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fps", govde, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("crf ", govde, StringComparison.OrdinalIgnoreCase);

        kopya.Tut = false;
    }

    /// <summary>
    /// <para>Denetim borcu: <c>main.unit.score-suffix</c>, <c>main.unit.score-value</c> ve
    /// <c>main.advanced.mode.crf</c> 42 dilde bayt bayt ayni ("/100", "{0}/100", "CRF").
    /// Tek degerli olduklari icin dil kollari onlari ayirt edemiyor; ucunu de koda sabit
    /// yazan bir mutasyon 369 testi yesil birakiyordu.</para>
    /// <para><b>Karar</b>: "bilincli tek degerli" ilan etmek yerine <b>sozlukten okundugu
    /// pimlenir</b>. Gerekce: ucu de ekranda kullaniciya gorunen metin ve ceviriye acik —
    /// "/100" yerine "0-100 puan", "CRF" yerine "kalite carpani" yazmak isteyen bir dil
    /// tamamen mesru. Tek degerli olmalari bugunku ceviri durumu, kalici bir kural degil;
    /// birim sabitini koda tasimak o kapiyi kapatirdi. Olcu, <c>Kopya</c> ile dil
    /// dosyasinin kopyasinda ucunu de baska bicime cevirir: sabiti koddan yazan bir mutasyon
    /// enjeksiyondan etkilenmez ve burada kirmizi verir.</para>
    /// </summary>
    [Fact]
    public void PuanBirimiVeKipAdiDilDosyasindanGelir()
    {
        using var kopya = Kopya("s9-puan-kip", new Dictionary<string, string>
        {
            ["\"main.unit.score-suffix\": \"/100\""] = "\"main.unit.score-suffix\": \" pts (max 100)\"",
            ["\"main.unit.score-value\": \"{0}/100\""] = "\"main.unit.score-value\": \"{0} pts (max 100)\"",
            ["\"main.advanced.mode.crf\": \"CRF\""] = "\"main.advanced.mode.crf\": \"Quality factor\""
        });

        (string birim, string not, string kip) okunan;
        AppHost.Run(() => Strings.UseRoot(kopya.Locales));
        try
        {
            okunan = Yuklu("en", window =>
            {
                var kalite = Named<TextBox>(window, "TxtQualityTarget");
                var izgara = ((Grid)kalite.Parent!).Children;
                var birim = (izgara[izgara.IndexOf(kalite) + 1] as TextBlock)?.Text ?? "";
                var kip = (window.FindControl<ComboBox>("CmbQualityMode")!.Items[0] as ComboBoxItem)?.Content?.ToString() ?? "";
                return (birim, not: Named<TextBlock>(window, "TxtEstimateNote").Text ?? "", kip);
            });
        }
        finally
        {
            AppHost.Run(() => Strings.UseRoot(null));
        }

        var kanit = Path.Combine(GirdiKanit.Root, ".calisma", "s9-puan-kip");
        Directory.CreateDirectory(kanit);
        File.WriteAllText(Path.Combine(kanit, "okunan.txt"), string.Join("\n", okunan.birim, okunan.not, okunan.kip));

        Assert.Equal(" pts (max 100)", okunan.birim.Replace('\u00A0', ' '), StringComparer.OrdinalIgnoreCase);
        Assert.EndsWith(" pts (max 100)", okunan.not.Replace('\u00A0', ' '), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Quality factor", okunan.kip, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("/100", okunan.birim + okunan.not, StringComparison.Ordinal);
        Assert.DoesNotContain("CRF", okunan.kip, StringComparison.OrdinalIgnoreCase);

        kopya.Tut = false;
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

    /// <summary>
    /// <para>Kodek seridindeki etiket, o secim gercekten hangi kodlayiciya gidiyorsa onu
    /// soylemeli. "En kucuk" kolu <c>CodecPreference.MaxCompression</c>'a, o da
    /// <c>PlanCalculator.PreferredCodecFor</c> icinde <c>libsvtav1</c>'e gidiyor; etiket
    /// yillarca <c>H.265</c> yaziyordu. <c>libx265</c> yalnizca AV1 derlemede yoksa devreye
    /// giren <b>yedek</b> (<c>FallbackCodecFor</c>), tercih degil.</para>
    ///
    /// <para>Olcu sabit karsilastirmiyor: etiketi ekrandan, kodlayiciyi gercek esleme
    /// zincirinden (<c>CodecFromIndex</c> -> <c>PreferredCodecFor</c>) okuyup ikisini
    /// karsilastiriyor. <c>RbCodecSmallest</c>'in <c>loc:Text</c> anahtarini degistiren bir
    /// mutasyon etiketi baska bir ailenin adina cevirir ve olcu kirmizi doner.</para>
    ///
    /// <para><b>Dil, pencere kuruldaktan sonra sabitlenir.</b> <c>Read("en", ...)</c>'in
    /// basindaki <c>Strings.Use</c> yetmiyor: <c>MainWindow</c> yapicisi kayitli arayuz
    /// dilini geri yukleyip uzerine yaziyor, dolayisiyla olcu kendi basina kosunca Turkce
    /// etiketi okuyordu ve <c>Locales/en/main.json</c>'a yapilan mutasyon <b>yesil</b>
    /// kaliyordu. Geri cagirmanin govdesindeki <c>Strings.Use("en")</c> bunu kapatir;
    /// <c>loc:Text</c> bir <c>LocalizedText</c> bagi verdigi icin kurulmus icerik de
    /// tazelenir.</para>
    /// </summary>
    [Fact]
    public void KodekEtiketiSecilenKodlayiciyiSoyler()
    {
        var etiket = Read("en", window =>
        {
            Strings.Use("en");
            return new[]
            {
                Named<RadioButton>(window, "RbCodecAuto").Content?.ToString() ?? "",
                Named<RadioButton>(window, "RbCodecCompatible").Content?.ToString() ?? "",
                Named<RadioButton>(window, "RbCodecSmallest").Content?.ToString() ?? ""
            };
        });

        var aile = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["libx264"] = "H.264",
            ["libx265"] = "H.265",
            ["libsvtav1"] = "AV1"
        };

        var kanit = new List<string>();
        foreach (var kol in new[] { 1, 2 })
        {
            var tercih = MainWindow.CodecFromIndex(kol);
            var kodlayici = PlanCalculator.PreferredCodecFor(tercih);
            kanit.Add($"{kol}\t{tercih}\t{kodlayici}\t{etiket[kol]}");
        }

        var dizin = Path.Combine(GirdiKanit.Root, ".calisma", "kodek-etiketi");
        Directory.CreateDirectory(dizin);
        File.WriteAllText(Path.Combine(dizin, "okunan.txt"), string.Join(Environment.NewLine, kanit));

        Assert.Equal("libsvtav1", PlanCalculator.PreferredCodecFor(MainWindow.CodecFromIndex(2)));
        foreach (var kol in new[] { 1, 2 })
        {
            var kodlayici = PlanCalculator.PreferredCodecFor(MainWindow.CodecFromIndex(kol));
            Assert.True(aile.ContainsKey(kodlayici), $"aile tablosunda yok: {kodlayici}");
            Assert.StartsWith(aile[kodlayici], etiket[kol], StringComparison.Ordinal);
        }

        Assert.Equal(3, etiket.Distinct(StringComparer.Ordinal).Count());

        Kapat(dizin, "okunan.txt");
    }

    /// <summary>
    /// Son asertten sonra çağrılır: yeşil koşum kendi bıraktığını siler, kırmızı koşum
    /// kanıtını korur çünkü düşen asert buraya hiç gelmez. Klasör boşalınca o da gider.
    /// </summary>
    private static void Kapat(string klasor, params string[] adlar) => KanitKapanisi.Kapat(klasor, adlar);

    /// <summary>
    /// <para>WhatsApp sohbetteki videoyu kendi dusuk bit hizli kodlayicisiyla yeniden
    /// kodluyor; "belge olarak gonder" yolu bunu atliyor. Ipucu, WhatsApp hedefli cikti
    /// uretildiginde sonuc cumlesinin (<c>TxtResult</c>) arkasina ekleniyor.</para>
    ///
    /// <para>Olcu <c>HizKareKbitVeCrfBirimleriDilDosyasindanGelir</c>'in desenini kullanir:
    /// dil dosyasinin kopyasinda cumle baskasiyla degistirilir ve ekrana <b>o</b> cumlenin
    /// dustugu gorulur. Metin koda sabitlenirse eski cumle gelir ve olcu kirmizi doner.
    /// Negatif kol da tutuluyor: WhatsApp hedefi olmayan bir boyutta ipucu hic cikmaz,
    /// yoksa kosul dalini silen mutasyon sessizce gecerdi.</para>
    ///
    /// <para><c>Strings.Use("en")</c> pencere kuruldu<b>ktan sonra</b> bir kez daha
    /// cagriliyor: <c>MainWindow</c>'un kurucusu kayitli arayuz dilini geri yukluyor ve
    /// <c>Read</c>'in bastaki secimini eziyor. Bu olcu tek basina kosuldugunda Turkce
    /// cumleyi okuyup kirmizi donuyordu; sinif filtresi icinde onceki testler dili
    /// birakmis oldugu icin yesil gorunuyordu.</para>
    /// </summary>
    [Fact]
    public void WhatsAppBelgeIpucuDilDosyasindanGelir()
    {
        using var kopya = Kopya("s-whatsapp-belge", new Dictionary<string, string>
        {
            ["\"main.run.whatsapp-document\": \"Send this on WhatsApp as a document"] =
                "\"main.run.whatsapp-document\": \"Belge kolundan gonder"
        });

        (string Onalti, string Yuz) okunan;
        AppHost.Run(() => Strings.UseRoot(kopya.Locales));
        try
        {
            okunan = Read("en", window =>
            {
                Strings.Use("en");
                return (window.WhatsAppDocumentHintForTest(16),
                    window.WhatsAppDocumentHintForTest(100));
            });
        }
        finally
        {
            AppHost.Run(() => Strings.UseRoot(null));
        }

        var dizin = Path.Combine(GirdiKanit.Root, ".calisma", "s-whatsapp-belge");
        Directory.CreateDirectory(dizin);
        File.WriteAllText(Path.Combine(dizin, "okunan.txt"), $"16 MB: [{okunan.Onalti}]{Environment.NewLine}100 MB: [{okunan.Yuz}]");

        Assert.StartsWith(" Belge kolundan gonder", okunan.Onalti, StringComparison.Ordinal);
        Assert.DoesNotContain("Send this on WhatsApp", okunan.Onalti, StringComparison.Ordinal);
        Assert.Equal("", okunan.Yuz);

        kopya.Tut = false;
    }

    /// <summary>
    /// <para>Kucultme seridi artik <c>AV1</c> diyor ve AV1 eski cihazlarda cozulmez; secimin
    /// bedelini soyleyen bir yer yoktu. Donusum sekmesinin kodek ipucu bu bilgiyi 42 dilde
    /// zaten tasiyordu, <c>main.codec.tip</c> onun H.264 ve AV1 maddelerinden uretildi.</para>
    ///
    /// <para>Olcu ipucunun <b>varligini</b> degil kaynagini tutuyor: dil agacinin kopyasinda
    /// <c>en</c> degeri degistirilip <c>Strings.UseRoot</c> ile enjekte ediliyor, ekrandaki
    /// balon o degeri gostermezse kirmizi doner. Metin koda sabitlenirse olcu duser.</para>
    /// </summary>
    [Fact]
    public void KodekSeridiIpucuDilDosyasindanGelir()
    {
        using var kopya = Kopya("s-kodek-ipucu", new Dictionary<string, string>
        {
            ["\"main.codec.tip\": \"• H.264"] = "\"main.codec.tip\": \"• Balon kolundan H.264"
        });

        string okunan;
        AppHost.Run(() => Strings.UseRoot(kopya.Locales));
        try
        {
            okunan = Read("en", window =>
            {
                Strings.Use("en");
                var satir = Named<Grid>(window, "CodecChoiceRow");
                var balon = ToolTip.GetTip(satir) as StackPanel;
                Assert.NotNull(balon);
                var metin = balon!.Children.OfType<TextBlock>().First();
                return string.Concat(metin.Inlines?.OfType<Run>().Select(kosu => kosu.Text) ?? Array.Empty<string?>());
            });
        }
        finally
        {
            AppHost.Run(() => Strings.UseRoot(null));
        }

        var dizin = Path.Combine(GirdiKanit.Root, ".calisma", "s-kodek-ipucu");
        Directory.CreateDirectory(dizin);
        File.WriteAllText(Path.Combine(dizin, "okunan.txt"), okunan);

        Assert.Contains("Balon kolundan H.264", okunan, StringComparison.Ordinal);
        Assert.Contains("AV1", okunan, StringComparison.Ordinal);

        kopya.Tut = false;
    }
}

/// <summary>
/// T192 tur 2 K8 — baslik kuralinin kapsami.
///
/// <para>Tur 1'de <c>ReadsAsProse</c> genisletildi ve rapor dort satirlik bir tablo
/// verdi. Yan etkinin <b>olcusu</b> yoktu: dil dosyasindaki her metin ayni geciten
/// geciyor, dolayisiyla degisen satir sayisi dortten cok fazla. Bu olcu butun dil
/// dosyalarini gercek <c>LanguageCatalog.Title</c> uzerinden gezer, kol degistiren her
/// anahtari sayar ve doker.</para>
///
/// <para><b>Kol degistiren</b> = <c>fd6fe0c1</c>'in kuralina (yalniz cumle isareti) gore
/// baslik, bugunku kurala gore govde. Eski kural burada tek yerde, alti satirda
/// tekrarlanir — kurulan mekanizma degil, <b>karsilastirma tabani</b> odur.
/// Ciktinin gercekten degistigi ayrica olculdu: ayni dokum bir de
/// <c>origin/main</c>'in <c>LanguageCatalog.cs</c>'siyle alinip <c>diff</c>'lendi,
/// ham cikti <c>.calisma/T192/k8-fark.txt</c>.</para>
///
/// <para>Sayi <b>pimlidir</b>. Dil dosyasina metin eklendiginde ya da bir metin
/// degistiginde bu pim kirilir; kirilinca yapilacak sey susturmak degil, yeni sayiyi
/// <c>docs/olcumler/kare-kusurlari.md</c>'ye yazmaktir.</para>
/// </summary>
public sealed class BaslikKapsamiTests
{
    private readonly ITestOutputHelper _cikti;

    public BaslikKapsamiTests(ITestOutputHelper cikti) => _cikti = cikti;

    /// <summary><c>fd6fe0c1</c>'in kurali: yalniz cumle isareti.</summary>
    private static bool EskiKuralaGoreGovde(string metin)
    {
        for (var index = 0; index < metin.Length; index++)
        {
            if (metin[index] is not ('.' or ';' or '!' or '?')) continue;
            if (index + 1 == metin.Length || char.IsWhiteSpace(metin[index + 1])) return true;
        }

        return false;
    }

    /// <summary>
    /// Sozcugun bas tarafi <c>Names</c>'de bildirilmis bir ad mi? Sinir kurali
    /// <c>LanguageCatalog.KnownSpelling</c> ile ayni: bastaki harf disi imler atlanir,
    /// once bir rakam gelirse sozcuk olcudur ve ada bakilmaz, ad yalniz <b>bastaki</b>
    /// harf/rakam dizisinde aranir. Boylece <c>tools/ffmpeg</c> bir yoldur, ad degil.
    /// </summary>
    private static (string Govde, string? Dogru) AdiCagriliyorsa(string sozcuk)
    {
        var offset = 0;
        while (offset < sozcuk.Length && !char.IsLetter(sozcuk[offset]))
        {
            if (char.IsDigit(sozcuk[offset])) return (sozcuk, null);
            offset++;
        }

        if (offset == sozcuk.Length) return (sozcuk, null);

        var govde = sozcuk[offset..];
        var token = new string(govde.TakeWhile(char.IsLetterOrDigit).ToArray());
        var bare = new string(govde.TakeWhile(char.IsLetter).ToArray());
        if (LanguageCatalog.Names.TryGetValue(token, out var ad)) return (govde, ad);
        if (LanguageCatalog.Names.TryGetValue(bare, out var isim)) return (govde, isim);
        return (govde, null);
    }

    /// <summary>
    /// <para>Sinif <c>BaslikKapsamiTests</c>, dosya <c>BiciminTests.cs</c> (dosya uc sinif tasiyor:
    /// <c>BiciminTests</c>, <c>KareYerlesimTests</c>, <c>BaslikKapsamiTests</c>). Filtre sinif adiyla
    /// eslesir, dosya adiyla eslesmez: <c>--filter "FullyQualifiedName~VidShrink.Tests.BiciminTests"</c>
    /// yalniz 19 test kosturur (25 ms) ve bu sayimlara hic girmez. Bu pinleri kosturan filtre
    /// <c>--filter "FullyQualifiedName~VidShrink.Tests.BaslikKapsamiTests"</c>.</para>
    /// <para><b>18 Eylul 2026 pimleri: toplam 1628, en 193, tr 66.</b> On ayar kutuphanesi
    /// turunun dil basina on dort anahtari kol degistiren kumeyi de buyuttu: 1628 - 1598 = 30,
    /// en 189 + 4, tr 64 + 2. Ayni turda <c>main.preset.add.tip</c> kirk dilde kisaltildi
    /// (ipucu tavani olcusu iki satir tasiyordu); kisaltma kol degistiren kumeyi degistirmedi,
    /// sayilar kisaltmadan once ve sonra ayni cikti.</para>
    /// <para><b>19 Eylul 2026 pimleri: toplam 1649, en 195, tr 67.</b> Paylasim hatalarinin
    /// dili (<c>5ba0e973</c>) dil basina otuz yedi <c>share.*</c> anahtari ekledi; bunlarin
    /// kol degistiren kismi 1649 - 1628 = 21, en 193 + 2, tr 66 + 1. Bit hizi biriminin
    /// birlestirilmesi ayni turda <c>main.unit.k-value</c>'yu dusurdu, o anahtar govde degil
    /// olcu ("{0}k") oldugu icin kol degistiren kumeye hic girmiyordu: sayilar oynamadi.</para>
    /// <para><b>19 Eylul 2026, ikinci yenileme: toplam 1666, en 196, tr 68.</b> Olcek carpani
    /// (<c>b7b485c4</c>) ve cikti adi deseni (<c>1546c9e3</c>) turlari birlikte dil basina on
    /// anahtar ekledi; kol degistiren kismi 1666 - 1649 = 17, en 195 + 1, tr 67 + 1. Iki tur
    /// da pimi yenilemedi, CI iki kosum kirmizi kaldi.</para>
    ///
    /// <para><b>19 Eylul 2026, ucuncu yenileme: toplam 1677, en 198, tr 68.</b> Tani gunlugu
    /// (E7) her dile bes <c>settings.log.*</c> anahtari ekledi; kol degistiren kismi 1677 - 1666 = 11,
    /// en 196 + 2. Turkce tarafta yeni anahtarlarin hicbiri kol degistirmiyor, tr 68'de kaliyor.</para>
    ///
    /// <para><b>19 Eylul 2026, dorduncu yenileme: toplam 1682, en 198, tr 68.</b> Suzgec yuzeyi
    /// (E8) her dile uc <c>main.advanced.filters.*</c> anahtari ekledi; 126 anahtarin yalniz
    /// besi kol degistiriyor (es/pt <c>label</c>, hu/ro <c>bad</c>, ro <c>label</c>). Ingilizce
    /// "Picture filters" ve Turkce "Gorunt&#252; suzgecleri" zaten kurala uygun yazildi, o yuzden
    /// en ve tr sayilari oynamadi.</para>
    ///
    /// <para><b>19 Eylul 2026, besinci yenileme: toplam 1690, en 200, tr 69.</b> Paylasimin
    /// yeniden deneme yuzu uc <c>settings.share.retry*</c>, onizleme rozeti bir
    /// <c>main.preview.temsili</c> anahtari ekledi; 172 anahtarin sekizi kol degistiriyor
    /// (en <c>retry-in</c> ve <c>retry-with</c>, tr <c>retry-with</c>, de/sw <c>retry-in</c>,
    /// lt <c>retry-with</c>, pt <c>retry</c> ve <c>retry-in</c>). <c>main.preview.temsili</c>
    /// hicbir dilde kol degistirmiyor.</para>
    ///
    /// <para><b>19 Eylul 2026, altinci yenileme: toplam 1727, en 205, tr 72.</b> B1a ses kodegi
    /// kolu dort, tasma dugmelerinin ekran okuyucu adi iki anahtar ekledi; 258 anahtarin 37si
    /// kol degistiriyor (<c>dolby-not-in-container</c> 11, <c>dolby-below-channel-floor</c> 9,
    /// <c>retry.trim.name</c> 7, <c>audio-channels.source</c> 6, <c>audio-codec.label</c> 3,
    /// <c>retry.accept.name</c> 1). en besi, tr ucu.</para>
    ///
    /// <para><b>22 Eylul 2026, yedinci yenileme: toplam 1726, en 205, tr 71.</b> Yerlesim
    /// denetcisi (<c>YerlesimDenetimiTests</c>) tr ve en'de 32 basligin (en 22, tr 10) kucuk
    /// harfle baslayan sozcuklerini buyuttu. Yeni anahtar yok; tr'de buyutulen dort anahtardan biri
    /// (<c>player.advanced.*</c> ucu ya da <c>settings-tab.opensubtitles.account</c>) artik duz
    /// yazi okunmuyor ve koldan cikti. en sayisi oynamadi.</para>
    ///
    /// <para><b>22 Eylul 2026, sekizinci yenileme: toplam 1880, en 222, tr 75.</b> Pin main'de
    /// C1-2 kuyruk duzenlemesinden beri kirmiziydi (a26a3b3a CI: 1784). 154 fazlanin 53'u C1-5
    /// suzgec ve C1-6 iz anahtarlari (en 4, tr 2; en cok <c>subtitles.burn</c> 7, iki
    /// <c>filters.rotate.*</c> 5'er), 101'i C1-1..C1-4 kuyruk ve on ayar turunun anahtarlari.
    /// Sonuc durumu denetiminde en'de dokuz <c>retry.*</c>/<c>action.*</c> dugmesi Title Case'e
    /// alindi; islev sozcugu tasidiklari icin kolda kaldilar, sayi oynamadi.</para>
    ///
    /// <para><b>23 Eylul 2026, dokuzuncu yenileme: toplam 1918, en 225, tr 78.</b> B3 (VP9
    /// kucultme) ve B4 (HDR dinamik veri) dil basina uc gerekce anahtari ekledi:
    /// <c>main.reason.hdr-dynamic-dropped</c>, <c>main.reason.stream.webm-audio-opus</c>,
    /// <c>main.reason.stream.webm-stream-dropped</c>. Ucu de noktalama tasimiyor (eski kural
    /// govde sayardi) ama govde sozcugu tasiyor (en <c>the</c>/<c>or</c>/<c>so</c>, tr
    /// <c>ve</c>/<c>ya</c>/<c>da</c>), yani yeni kural ile kol degistiriyor: en ve tr'de ucu de
    /// (1880 + 38 = 1918, en 222 + 3 = 225, tr 75 + 3 = 78). Kalan 38 - 3 - 3 = 32 diger
    /// dillerde, aynı uc anahtarin ceviri sozcuklerine bagli.</para>
    /// <para>2026-09-23: <c>main.reason.vp9-crf-unmeasured</c> (VP9'da CRF yerine iki gecis)
    /// 12 dilde kol degistiriyor, en dahil tr haric: 1918 + 12 = 1930, en 225 + 1 = 226.</para>
    /// <para>2026-09-23, ikinci duzeltme: <c>main.reason.vp9-crf-unmeasured</c> metni CRF
    /// kalite olceginin artik olculdugunu, olculmeyenin CRF-boyut cevirisi oldugunu anlatacak
    /// sekilde yeniden yazildi. Islev sozcugu eslesmesi rastlantisal: cs'de yeni metindeki
    /// "do" (Cekce "-e"), et ve fi'de "on" (Estonca/Fince "-dir") ingilizce govde listesiyle
    /// carpisip kola giriyor (+1'er), de'de eski metindeki "in" (Almanca "-de") kalkiyor ve
    /// govdeden cikiyor (-1). Toplam 1930 + 1 + 1 + 1 - 1 = 1932; en ve tr metni ayni
    /// govde/kol siniri icinde kaldigi icin 226 ve 78 degismedi.</para>
    /// <para>2026-09-23, HDR10+ koprusu: <c>main.reason.hdr10plus-cut</c> 18 dilde,
    /// <c>main.reason.hdr10plus-routed-x265</c> 14 dilde kol degistiriyor, ikisi de en ve tr
    /// dahil: 1932 + 32 = 1964, en 226 + 2 = 228, tr 78 + 2 = 80. Oto-kirpmanin iki anahtari
    /// (<c>main.advanced.filters.autocrop</c>, <c>main.reason.auto-crop</c>) hicbir dilde kola girmiyor.</para>
    /// <para>2026-09-23, tek gecis etiketi: donanim kodlayicisinin tek gecisini soyleyen yedi
    /// anahtardan dordu kola giriyor: <c>main.estimate.mode.enforced-single-pass</c> 8 dilde,
    /// <c>main.reason.budget-below-ceiling-single-pass</c> 10, <c>-fill-band-center-single-pass</c> 10,
    /// <c>-fill-band-narrow-single-pass</c> 15. 1964 + 8 + 10 + 10 + 15 = 2007; en dordunde de
    /// (228 + 4 = 232), tr yalniz band merkezinde (80 + 1 = 81). Yeniden yazilan
    /// <c>main.output.estimated-output.tip</c> ne once ne sonra kolda.</para>
    /// <para>2026-09-23, iki nokta sonrasi cumle (15fa18ac, <c>CarriesClauseAfterColon</c>): iki noktadan sonra
    /// harf tasiyan en az uc sozcuk varsa metin cumle sayilir. 519 anahtar-dil cifti kola girdi, hepsi iki noktadan
    /// sonra cumle ("Dropped: codec not supported", "Duraklatıldı: şimdiki dosya biter, sıradaki bekler"); en cok
    /// <c>main.preset.import.summary</c> 41, <c>main.reason.auto-crop</c> 40. 2007 + 519 = 2526; en 232 + 5 = 237,
    /// tr 81 + 8 = 89. Kurali kaldirmak sayimi 2007/232/81'e geri indiriyor (olculdu).</para>
    /// <para>2026-09-23, Media Foundation: <c>main.reason.no-quality-scale</c> 15 dilde kola giriyor,
    /// en dahil tr haric: iki nokta kuraliyla birlikte 2526 + 15 = 2541, en 237 + 1 = 238, tr 89.</para>
    /// <para>2026-09-24, yalan yok: <c>main.output.remaining</c> en'de "Remaining" iken "Left in this attempt" oldu ve
    /// kola giriyor (tr "Kalan" kaldi): 2541 + 1 = 2542, en 239, tr 89.</para>
    /// <para>2026-09-24, soylenen ile yapilan: <c>main.reason.stream.vp9-fell-back-to-mp4</c> 18 dilde kola
    /// giriyor ("VP9 could not be used, ..."), en dahil tr haric; yeni iki anahtar kolun disinda.
    /// ustune yalan yok CLI satiri: 2542 + 18 = 2560, en 239 + 1 = 240, tr 89.</para>
    /// <para>2026-09-24, kaydedici durustlugu: yedi yeni kaydedici anahtarindan dordu kola giriyor:
    /// <c>recorder.output.done-partial</c> 34 dilde, <c>recorder.output.done-failed</c> 33,
    /// <c>recorder.replay.saved-mkv</c> 7, <c>recorder.discarded-kept</c> 5. 2541 + 79 = 2620; en dordunde de
    /// (238 + 4 = 242), tr yalniz <c>recorder.discarded-kept</c>'te (89 + 1 = 90); ustteki iki satirla birlikte 2560 + 79 = 2639, en 244, tr 90.</para>
    /// <para>2026-09-24, yalan yok kalan uclar: uc yeni anahtardan yalniz <c>main.reason.stream.vp9-fell-back</c>
    /// kola giriyor, 12 dilde (cs en fr hr hu lt nl pl pt ro sk sr), en dahil tr haric;
    /// <c>-image-subtitle-dropped-by-container</c> ve <c>recorder.output.partial-broken-mkv</c> kolun disinda,
    /// yeniden yazilan <c>main.run.over-ceiling</c> ne once ne sonra kolda: 2639 + 12 = 2651, en 245, tr 90.</para>
    /// <para>2026-09-25, bolge duzenleyici: dort yeni <c>recorder.region.*</c> anahtarindan yalniz <c>recorder.region.title</c>
    /// kola giriyor, 4 dilde (es fr pt ro: "Editor de región"), en ve tr haric: 2651 + 4 = 2655, en 245, tr 90.</para>
    /// <para>2026-09-25, libmpv yukleme hatasi: <c>main.player.engine.loadfailed</c> 5 dilde kola giriyor (bn hi ja th ur),
    /// en ve tr haric: 2655 + 5 = 2660, en 245, tr 90.</para>
    /// </summary>
    [Fact]
    public void KolDegistirenAnahtarlarSayilir()
    {
        var toplam = 0;
        var dilBasina = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var dil in Strings.Languages.OrderBy(d => d, StringComparer.Ordinal))
        {
            dilBasina[dil] = 0;
            foreach (var anahtar in Strings.KeysOf(dil).OrderBy(k => k, StringComparer.Ordinal))
            {
                var ham = Strings.GetIn(dil, anahtar);
                if (EskiKuralaGoreGovde(ham)) continue;
                if (!LanguageCatalog.ReadsAsProse(ham)) continue;

                toplam++;
                dilBasina[dil]++;
                _cikti.WriteLine($"KOL\t{dil}\t{anahtar}\t{ham.ReplaceLineEndings(" ")}\t{LanguageCatalog.Title(ham, dil).ReplaceLineEndings(" ")}");
            }
        }

        foreach (var (dil, sayi) in dilBasina) _cikti.WriteLine($"SAYIM\t{dil}\t{sayi}");
        _cikti.WriteLine($"SAYIM\ttoplam\t{toplam}");

        Assert.Equal(2660, toplam);
        Assert.Equal(245, dilBasina["en"]);
        Assert.Equal(90, dilBasina["tr"]);
    }

    /// <summary>
    /// Butun anahtarlarin gercek <c>Title</c> ciktisini doker. Eski/yeni farkinin ham
    /// tabani bu dokum: ayni test <c>origin/main</c>'in <c>LanguageCatalog.cs</c>'siyle
    /// bir kez daha kosuluyor ve iki dokum <c>diff</c>'leniyor.
    /// </summary>
    /// <summary>
    /// T192 tur 3 K11 — ad ve birim yazimi metnin neresinde durursa dursun korunur.
    ///
    /// <para>Tur 2'de <c>Names</c> gecidi yalniz <c>CapitaliseWord</c> icindeydi ve
    /// <c>Sentence</c> onu sadece satir basi sozcugu icin cagiriyordu. Sonuc: baslik
    /// kolundan govde koluna gecen metinlerde (o turda 124, kirk dil eklendikten sonra 784, tagline anahtarlari silinince 778, oynatici 1. dalga anahtarlariyla 844, 3. dalga anahtarlariyla 898, 2. dalga parca anahtarlariyla 950, 4. dalga arac ve gelismis anahtarlariyla 985, 8b kaydedici anahtarlariyla 1078, 8d ses anahtarlariyla 1090, sr'nin yedi ses satiri Kiril'den dosyanin geri kalaniyla ayni Latin yazimina dondurulunce 1091, Kesit A main.language.settings'i dusurunce 1086, Kesit B main.player.title ve main.player.menu'yu dusurunce 1084, Kesit D kaydedicinin otomatik kipine 16 recorder.auto.* anahtari ekleyince 1125, guncelleme rozetinin dort anahtari ve kaydedicinin hedef butcesi eklenince 1135, oynatici kisayollarinin dort menu anahtari 1217'den 1221'e: en bookmarkprev ve tostart, pl bookmarkprev, pt settings-all)
    /// ilk sozcuk disindaki <c>ffmpeg</c>
    /// dil dosyasindaki yazimiyla kaliyordu — <c>en/main.drop.hint</c>,
    /// <c>en|tr/main.reason.encoder-fallback-not-in-build</c>. Bu olcu butun dillerin butun anahtarlarinin (bugun 38313 kalem, sayim BaslikKapsamiTests'te)
    /// <b>tamamini</b> gezer, tek bir kalemi bile atlamaz.</para>
    /// <para>Sayim yansimanin gordugu 43 dil uzerinden: 42 dil klasoru ve gomulu kaynak
    /// adindan gelen bir dil daha, her biri 692 anahtar. 5. dalga ffmpeg oynatma borusunu
    /// cop kutusuna tasiyinca okuyucusu kalmayan dokuz <c>playback.pipe.*</c> /
    /// <c>playback.source.*</c> anahtari kataloglardan cikti: 43 x 486 = 20898'den
    /// 43 x 477 = 20511'e. 1. dalga 32 oynatici anahtari ekledi: 43 x 509 = 21887. 6. dalga Unix kapamasi
    /// <c>main.instance.waiting</c> ekledi: 43 x 510 = 21930. 3. dalga 32 anahtar daha ekledi: 43 x 542 = 23306.
    /// 2. dalga 38 altyazi ve ses parcasi anahtari ekledi: 43 x 580 = 24940.
    /// 4. dalga 39 gelismis ve 12 arac anahtari ekledi: 43 x 631 = 27133.
    /// 7. dalga fare isi sol tik jestini ve menunun ayarlar satirini ekledi: 43 x 633 = 27219. ui-kilavuz sozlesmesi
    /// disabled-affordance icin 4 (main.action.cancel/shrink/convert.disabled-tip, settings.share.delete.disabled-tip)
    /// ve unnamed-interactive/component-without-motion icin 4 (playback.panel.maximize/fullscreen, playback.zoom.out/in)
    /// anahtar ekledi: 43 x 641 = 27563. main.sponsor.label denendi, BrandSpellingTests ile cakisti, geri alindi.
    /// 8. dalga 8b kolu kaydedici sekmesini ekledi: 43 recorder anahtari ve sekme basligi
    /// main.tab.recorder, 43 x 685 = 29455. 8d kolu ses girdisini motora baglayinca arayuze
    /// secim yuzeyi girdi: recorder.audio.* alti anahtar ve recorder.error.audio,
    /// 43 x 692 = 29756. 0.4.4 yeni surum uyarisina Yukle dugmesini ekledi (main.action.install,
    /// 43 x 693 = 29799); ayni turda Ayarlar seride kendi sekmesi olunca baslik cubugundaki
    /// tekerlek dustu ve main.language.settings sahipsiz kaldi, silindi: 43 x 692 = 29756.
    /// Kesit B oynatici basligini kaldirdi (baslik yazisi, parca dugmeleri, uc nokta);
    /// main.player.title ve main.player.menu sahipsiz kaldi, silindi: 43 x 690 = 29670.
    /// Kesit D kaydediciye otomatik kipi ekledi: recorder.auto.* on alti anahtar
    /// (kutu, ipucu, olcum dugmesi, ozet ve sekiz gerekce satiri), 43 x 706 = 30358.
    /// Guncelleme rozeti dort main.update.* anahtari ekledi: 43 x 710 = 30530. Hedef boyut butcesi
    /// dokuz recorder.budget.* / recorder.auto.manual anahtari ekledi, sahipsiz kalan recorder.auto.enable
    /// ve recorder.auto.hint dustu: 43 x 717 = 30831. Iki adimli guncelleme indirme dugmesini, rozet/durum ve dort gunluk satirini ekledi
    /// (main.action.download, main.update.badge/downloading/ready/failed, main.update.log.*): 43 x 732 = 31476'dan 43 x 741 = 31863'e.
    /// Tasma karari dokuz main.retry.* / main.run.* anahtari ekledi (kabul, kesme seridi, iki sonuc satiri): 43 x 742 = 31906'dan 43 x 751 = 32293'e.
    /// Hakkinda'nin platform satirlari iki main.about.platforms.* anahtari ekledi: 43 x 753 = 32379.
    /// Oynatici kisayollari dort main.player.menu.* anahtari ekledi (settings-all, bookmarkprev, stop, tostart): 43 x 757 = 32551.
    /// HandBrake 1c dalgasi main.advanced.keep-tracks.label'i ekledi: 43 x 742 = 31906'dan 43 x 743 = 31949'a;
    /// iz kararlarinin sekiz gerekce notu (main.reason.stream.*) 43 x 751 = 32293'e, kol degistiren toplami 1218'den 1260'a (en 139'dan 143'e, tr 52'den 56'ya). Birlesik: gezilen 33067, toplam 1295 (en 152, tr 58).
    /// Paket 2 kaydedicisi 80 recorder.* anahtari ekledi (Basit/Gelismis, geri sayim, cerceve, tepsi, kisayol, girdi gosterimi, webcam, buyutec ve gelismis panelin on kolu): 43 x 80 = 3440, gezilen 36507; kol degistiren toplami 1406 (en 160, tr 59).
    /// Paket 2b 23 anahtar ekledi (kayit odagi, bosluk kirpma, canli onizleme, kayit tamponu: iki main.*, yirmi bir recorder.*): 43 x 23 = 989, gezilen 37496; kol degistiren toplami 1440 (en 165, tr 59). Karanlik gecis main.reason.dark-content-hevc gerekce notunu ekledi: 43 x 1 = 43, gezilen 37539; kol degistiren toplami 1441 (en 166, tr 59). Kaydedici pencere secicisi iki recorder.error.* anahtari ekledi (window-wayland, window-missing): 43 x 2 = 86, gezilen 37625; kol degistiren toplami degismedi. HandBrake A2 on ayar kutuphanesi 15 main.preset.* anahtari ekledi: 43 x 15 = 645, gezilen 38270; kol degistiren toplami 1471 (en 169, tr 60). Yol D main.update.maintenance-failed bakim hatasi cumlesini ekledi: 43 x 1 = 43, gezilen 38313; kol degistiren toplami 1480 (en 170, tr 61; cs, es, hu, nl, pt, ro, sk birer).</para>
    /// <para>Bu daldaki (<c>t0/yol-b-kabuk</c>) son adim, <c>origin/main</c> (<c>9c4b9907</c>) dala
    /// birlesince olculdu. Dal S9'un on anahtarini ekledi (<c>main.unit.mb</c>, <c>mb-value</c>,
    /// <c>mb-range</c>, <c>kbps-value</c>, <c>fps-value</c>, <c>k-value</c>, <c>score-suffix</c>,
    /// <c>score-value</c>, <c>main.plan.ai</c>, <c>main.plan.mode.crf-value</c>) ve P3
    /// <c>main.player.menu.settings-all</c>'i dusurdu; dalin tepesinde (<c>006c8702</c>)
    /// <c>main.json</c> 500 anahtarli. <c>origin/main</c>'de 492 (HandBrake A2'nin 15
    /// <c>main.preset.*</c> anahtari ve Yol D'nin <c>main.update.maintenance-failed</c>'i dahil).
    /// Birlesik katalog 501; 42 dil dosyasinin hepsi 501'de esit, tek anahtar dusmedi.
    /// <c>origin/main</c>'in pinine gore fark dil basina 501 - 492 = 9 anahtar:
    /// 43 x 9 = 387, gezilen 38313 + 387 = 38700. Kol degistiren toplam: <c>origin/main</c>'in
    /// 1480'i uzerine dalin iki kolu eklenir (<c>de</c> ve <c>nb</c> <c>main.plan.ai</c> = "KI",
    /// dokumde olculdu) ve dusen <c>pt main.player.menu.settings-all</c> bir kol goturur:
    /// 1480 + 2 - 1 = 1481. Diller 43, dil dosyasi klasoru 42: <c>zh-Hans</c> ve <c>zh_Hans</c>
    /// ayni dosyayi iki adla gezer.</para>
    /// <para>Metin duzeltmeleri dali (<c>t0/metin-duzeltmeleri</c>) tek anahtar ekledi:
    /// <c>main.run.whatsapp-document</c>, WhatsApp hedefli cikti bitince sonuc cumlesinin
    /// arkasina eklenen "belge olarak gonder" ipucu. 42 dil dosyasinin hepsine girdi,
    /// dusen anahtar yok: 43 x 501 = 21543 degil, bu testin saydigi <b>butun</b> katalog
    /// kalemleri 38700 + 43 x 1 = 38743. Ayni dal <c>main.codec.smallest</c>'in
    /// <c>H.265</c> yazan govdesini <c>AV1</c>'e cevirdi; anahtar sayisini degistirmedi ve
    /// <c>AV1</c> zaten <c>LanguageCatalog.Names</c>'de bildirildigi icin <c>kayip</c> 0
    /// kaldi. Kol degistiren toplam da degismedi (1481, en 170, tr 61): eklenen cumleler
    /// nokta tasidigi icin eski kurala gore zaten govde sayiliyor.</para>
    /// <para>Ayni dalin denetim borclari <b>ikinci</b> anahtari ekledi:
    /// <c>main.codec.tip</c>, kucultme seridine asilan uyumluluk balonu. Metni uydurulmadi,
    /// 42 dilde <c>main.convert.video-codec.tip</c>'in H.264 ve AV1 maddelerinden kesildi.
    /// Yine 42 dosyanin hepsine girdi, dusen yok: 38743 + 43 x 1 = 38786. Ayni borc
    /// <c>main.advice.codec-upgrade</c>'in iki <c>H.265</c> gecisini <c>AV1</c>'e cevirdi —
    /// anahtar sayisini degistirmez, <c>kayip</c> 0 kalir, kol degistiren toplam 1481 /
    /// en 170 / tr 61 yerinde durur.</para>
    /// <para>P28 (<c>t0/p28-altyazi</c>) iki turda anahtar ekledi. Ilk tur on iki
    /// <c>player.subtitle.download*</c> ve uc <c>settings-tab.opensubtitles.*</c> anahtari
    /// (15); Yol A ikinci turda on <c>settings-tab.opensubtitles.*</c> oturum alani ve uc
    /// oturuma bagli hata kolu (<c>badlogin</c>, <c>expired</c>, <c>toofast.wait</c>) ekledi (13).
    /// Sayi dalin tabanindan (<c>d0aea4d2</c>) turetildi, olcunun fiili ciktisindan degil:
    /// taban dil basina 900 anahtar, dalin tepesi 928, dusen yok: 928 - 900 = 28,
    /// 43 x 28 = 1204, gezilen 38700 + 1204 = 39904.</para>
    /// <para><b>Birlesme sonrasi pim 39990.</b> Dalin pini 39904 kendi tabanina (38700)
    /// gore dogruydu; main ayni surede dil basina iki anahtar ekledi (43 x 2 = 86).
    /// Dil basina 900 + 2 + 28 = 930, 43 x 930 = 39990 (= 38786 + 1204 = 39904 + 86).
    /// Sayi elle hesaplanip yazilmadi: cozum bu degerle konuldu ve olcu yesil dondu,
    /// yani <c>SAYIM gezilen</c> satirinin kendisi 39990 raporladi.</para>
    /// <para>Kol degistiren toplam bu yirmi sekizin <c>Title</c> altinda kol degistiren
    /// kismindan gelir; dokumun KOL satirlari anahtar kumesi basina ve dil basina ayri sayildi.
    /// Ilk turun on besi: en 11, pt 7, es 6, hu 5, nl 5, ro 5, fr 4, nb 4, da 3, et 3, it 3,
    /// de 2, sk 2, sw 2, tr 2, cs 1, fi 1, pl 1 = 67. Ikinci turun on ucu: en 8, es 5,
    /// fr 4, hu 4, pt 4, pl 3, ro 3, cs 2, da 2, it 2, nb 2, nl 2, sk 2, sv 2, sw 2,
    /// de 1, et 1, tr 1 = 50. Toplam 1481 + 67 + 50 = 1598, en 170 + 11 + 8 = 189,
    /// tr 61 + 2 + 1 = 64. Ceviriler kucuk harfle basladigi icin ikinci turda da 43 dilin
    /// 18'i kol degistiriyor; ayni sayi, ayni kume degil: <c>fi</c> ikinci turda kol
    /// degistirmiyor, <c>sv</c> degistiriyor.</para>
    /// <para><b>18 Eylul 2026 pimi 40592.</b> On ayar kutuphanesi turu dil basina on dort
    /// anahtar ekledi: 40592 - 39990 = 602, 602 / 43 = 14, yani dil basina 930 + 14 = 944.
    /// Sayi yine olcunun kendi <c>SAYIM gezilen</c> satirindan alindi; gezilen metin
    /// uzunluguna degil anahtar sayisina bagli oldugu icin ceviri kisaltmalari bu sayiyi
    /// oynatmaz.</para>
    /// <para><b>19 Eylul 2026 pimi 42140.</b> Paylasim hatalarinin dili dil basina otuz yedi
    /// anahtar ekledi, bit hizi biriminin birlestirilmesi <c>main.unit.k-value</c>'yu dusurdu:
    /// dil basina 944 + 37 - 1 = 980, 43 x 980 = 42140. Sayi yine olcunun kendi
    /// <c>SAYIM gezilen</c> satirindan alindi, <c>kayip</c> 0 kaldi.</para>
    /// <para><b>19 Eylul 2026, ikinci yenileme: 42570.</b> Ayni iki tur dil basina on anahtar
    /// ekledi: 980 + 10 = 990, 43 x 990 = 42570. <c>kayip</c> yine 0.</para>
    ///
    /// <para><b>19 Eylul 2026, ucuncu yenileme: 42785.</b> Tani gunlugu dil basina bes anahtar
    /// ekledi: 990 + 5 = 995, 43 x 995 = 42785. <c>kayip</c> yine 0.</para>
    ///
    /// <para><b>19 Eylul 2026, dorduncu yenileme: 42828.</b> Baslik secici (E4) dil basina tek
    /// <c>main.title.label</c> anahtari ekledi: 995 + 1 = 996, 43 x 996 = 42828. <c>kayip</c> yine 0.</para>
    /// <para><b>19 Eylul 2026, besinci yenileme: 42957.</b> Suzgec yuzeyi (E8) dil basina uc
    /// <c>main.advanced.filters.*</c> anahtari ekledi: 996 + 3 = 999, 43 x 999 = 42957. <c>kayip</c> yine 0.</para>
    /// <para><b>19 Eylul 2026, altinci yenileme: 43129.</b> Paylasimin yeniden deneme yuzu
    /// uc <c>settings.share.retry*</c> anahtari, onizleme rozeti bir <c>main.preview.temsili</c>
    /// ekledi: 999 + 4 = 1003, 43 x 1003 = 43129. <c>kayip</c> yine 0.</para>
    /// <para><b>19 Eylul 2026, yedinci yenileme: 43387.</b> B1a ses kodegi kolu dort
    /// (<c>audio-codec.label</c>, <c>audio-channels.source</c>, iki <c>dolby-*</c> gerekcesi), tasma
    /// dugmeleri iki <c>main.retry.*.name</c> anahtari ekledi: 1003 + 6 = 1009, 43 x 1009 = 43387. <c>kayip</c> yine 0.</para>
    /// <para><b>22 Eylul 2026, sekizinci yenileme: 45881.</b> C1-1..C1-6 turu 58 anahtar ekledi:
    /// 43 x 1067 = 45881. <c>kayip</c> 0.</para>
    /// <para><b>23 Eylul 2026, dokuzuncu yenileme: 46010.</b> B4 dil basina bir
    /// <c>main.reason.hdr-dynamic-dropped</c>, B3 dil basina iki <c>main.reason.stream.webm-*</c>
    /// anahtari ekledi: dil basina 1067 + 3 = 1070, 43 x 1070 = 46010. <c>kayip</c> yine 0.</para>
    /// <para>2026-09-23: <c>main.reason.vp9-crf-unmeasured</c> dil basina 1070 + 1 = 1071,
    /// 43 x 1071 = 46053.</para>
    /// <para>2026-09-23: otomatik kirpma (HandBrake #36) dil basina <c>main.advanced.filters.autocrop</c>
    /// ve <c>main.reason.auto-crop</c>: 1071 + 2 = 1073, 43 x 1073 = 46139. <c>kayip</c> yine 0.</para>
    /// <para>2026-09-23: HDR10+ koprusu dil basina bes anahtar
    /// (<c>main.reason.hdr10plus-routed-x265</c>, <c>-svtav1</c>, <c>-cut</c>,
    /// <c>main.stage.hdr10plus-metadata</c>, <c>main.run.hdr10plus-short</c>): 1073 + 5 = 1078,
    /// 43 x 1078 = 46354.</para>
    /// <para>2026-09-23: tek gecis etiketi dil basina yedi anahtar (<c>main.plan.mode.single-pass</c>,
    /// <c>main.estimate.mode.enforced-single-pass</c>, <c>main.estimate.mode.copy</c>, uc
    /// <c>main.reason.*-single-pass</c>, <c>main.advice.single-pass</c>): 1078 + 7 = 1085,
    /// 43 x 1085 = 46655. <c>kayip</c> yine 0.</para>
    /// <para>2026-09-23: Media Foundation dil basina bir anahtar (<c>main.reason.no-quality-scale</c>):
    /// 1085 + 1 = 1086, 43 x 1086 = 46698.</para>
    /// <para>2026-09-24, yalan yok: dil basina <c>settings.share.link-copied</c>: 1086 + 1 = 1087, 43 x 1087 = 46741.</para>
    /// <para>2026-09-24: soylenen ile yapilan dil basina uc anahtar (<c>main.advice.encoder-fallback-hardware</c>,
    /// <c>main.reason.stream.extra-audio-dropped-by-container</c>, <c>main.reason.stream.vp9-fell-back-to-mp4</c>):
    /// ustune yalan yok CLI anahtari: 1087 + 3 = 1090, 43 x 1090 = 46870.</para>
    /// <para>2026-09-24: kaydedici durustlugu dil basina yedi anahtar (<c>recorder.discarded-kept</c>,
    /// <c>recorder.output.done-partial</c>, <c>-done-failed</c>, <c>-partial-unverified</c>, <c>-gif-failed</c>,
    /// <c>-not-moved</c>, <c>recorder.replay.saved-mkv</c>): 1086 + 7 = 1093, 43 x 1093 = 46999; ustteki iki satirla birlikte 1090 + 7 = 1097, 43 x 1097 = 47171.</para>
    /// <para>2026-09-24, yalan yok kalan uclar: dil basina uc anahtar (<c>main.reason.stream.image-subtitle-dropped-by-container</c>,
    /// <c>main.reason.stream.vp9-fell-back</c>, <c>recorder.output.partial-broken-mkv</c>): 1097 + 3 = 1100, 43 x 1100 = 47300.</para>
    /// <para>2026-09-25, bolge duzenleyici: dil basina dort anahtar (<c>recorder.region.title</c>, <c>-start</c>, <c>-settings</c>,
    /// <c>-close</c>): 1100 + 4 = 1104, 43 x 1104 = 47472.</para>
    /// <para>2026-09-25, mini serit: dil basina iki anahtar (<c>recorder.mini.offer</c>, <c>recorder.mini.region</c>):
    /// 1104 + 2 = 1106, 43 x 1106 = 47558.</para>
    /// <para>2026-09-25, libmpv yukleme hatasi: dil basina <c>main.player.engine.loadfailed</c>: 1106 + 1 = 1107, 43 x 1107 = 47601.</para>
    /// </summary>
    [Fact]
    public void AdVeBirimYazimiCumleOrtasindaDaKorunur()
    {
        var kayip = new List<string>();
        var gezilen = 0;

        foreach (var dil in Strings.Languages.OrderBy(d => d, StringComparer.Ordinal))
        {
            foreach (var anahtar in Strings.KeysOf(dil).OrderBy(k => k, StringComparer.Ordinal))
            {
                var cikti = LanguageCatalog.Title(Strings.GetIn(dil, anahtar), dil);
                gezilen++;

                foreach (var sozcuk in cikti.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    var (govde, dogru) = AdiCagriliyorsa(sozcuk);
                    if (dogru is null || govde.StartsWith(dogru, StringComparison.Ordinal)) continue;
                    kayip.Add($"{dil}	{anahtar}	{govde[..dogru.Length]} yerine {dogru}	{cikti}");
                }
            }
        }

        foreach (var satir in kayip) _cikti.WriteLine("KAYIP	" + satir);
        _cikti.WriteLine($"SAYIM	gezilen	{gezilen}");
        _cikti.WriteLine($"SAYIM	kayip	{kayip.Count}");

        Assert.Equal(47601, gezilen);
        Assert.Empty(kayip);
    }

    /// <summary>
    /// <para>Kodek adi bir metinde gecerken kapali listeyle aranmaz; bicimden cikarilir:
    /// <c>H.264</c>/<c>H.265</c> gibi <c>H.</c> + uc rakam, <c>AV1</c>/<c>VP9</c> gibi iki
    /// harf + rakam. Boylece yarin eklenen bir kodek listeyi guncellemeyi unuttugumuz icin
    /// sessizce gozden kacmaz.</para>
    ///
    /// <para>Olcunun kurali: <c>main.advice.codec-upgrade</c> tavsiyesi kullaniciyi kodek
    /// seridinde bir secenege yolluyor, dolayisiyla andigi her kodek adi seridin uc
    /// etiketinden birinde gecmek zorunda. Tavsiye 42 dilde yillarca <c>H.265</c> diyordu;
    /// serit <c>AV1</c>'e cekilince tavsiye var olmayan bir secenegi gosterir oldu. Her dil
    /// ayri gezilir, adsiz kalan dil de kirmizi doner.</para>
    /// </summary>
    [Fact]
    public void TavsiyeninGosterdigiKodekSeritteBulunur()
    {
        var desen = new Regex(@"H\.\d{3}|[A-Z]{2}\d", RegexOptions.CultureInvariant);
        var etiketAnahtarlari = new[] { "main.codec.automatic", "main.codec.compatible", "main.codec.smallest" };

        var eksik = new List<string>();
        var gezilen = 0;

        foreach (var dil in Strings.Languages.OrderBy(d => d, StringComparer.Ordinal))
        {
            var seritte = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var anahtar in etiketAnahtarlari)
                foreach (Match esleme in desen.Matches(Strings.GetIn(dil, anahtar)))
                    seritte.Add(esleme.Value);

            var tavsiyede = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match esleme in desen.Matches(Strings.GetIn(dil, "main.advice.codec-upgrade")))
                tavsiyede.Add(esleme.Value);

            gezilen++;
            _cikti.WriteLine($"SERIT\t{dil}\t{string.Join(",", seritte)}\ttavsiye\t{string.Join(",", tavsiyede)}");

            if (tavsiyede.Count == 0)
            {
                eksik.Add($"{dil}\ttavsiye hicbir kodek adi anmiyor");
                continue;
            }

            foreach (var ad in tavsiyede)
                if (!seritte.Contains(ad))
                    eksik.Add($"{dil}\t{ad}\tseritte yok: {string.Join(",", seritte)}");
        }

        foreach (var satir in eksik) _cikti.WriteLine("EKSIK\t" + satir);
        _cikti.WriteLine($"SAYIM\tdil\t{gezilen}");

        Assert.Equal(Strings.Languages.Count(), gezilen);
        Assert.Empty(eksik);
    }

    [Fact]
    public void TumCiktiDokulur()
    {
        var satir = 0;
        foreach (var dil in Strings.Languages.OrderBy(d => d, StringComparer.Ordinal))
        {
            foreach (var anahtar in Strings.KeysOf(dil).OrderBy(k => k, StringComparer.Ordinal))
            {
                _cikti.WriteLine($"DOKUM\t{dil}\t{anahtar}\t{LanguageCatalog.Title(Strings.GetIn(dil, anahtar), dil).ReplaceLineEndings(" ")}");
                satir++;
            }
        }

        Assert.True(satir > 0);
    }

    /// <summary>
    /// Sayidan sonra gelen birim buyutulmuyor: kaydedicinin tampon listesinde "30 S"
    /// cikiyordu. Kaydedici metni bicimlemeden once buyuttugu icin birimin onunde sayi degil
    /// yer tutucu (<c>{0}</c>) durur; o da sayi sayilir. Formul: onceki sozcuk rakam ve
    /// ayiraclardan ya da <c>{n}</c>den ibaretse ve sozcuk en cok uc kucuk harfse birimdir.
    /// Karsi yon: sayidan ya da yer tutucudan sonra gelen uzun sozcuk basliktaki gibi buyur.
    /// </summary>
    [Theory]
    [InlineData("30 s", "en", "30 s")]
    [InlineData("5 sn", "tr", "5 sn")]
    [InlineData("10 mp", "hu", "10 mp")]
    [InlineData("save last 30 s", "en", "Save Last 30 s")]
    [InlineData("top 10 videos", "en", "Top 10 Videos")]
    [InlineData("{0} s", "en", "{0} s")]
    [InlineData("save last {0} s", "en", "Save Last {0} s")]
    [InlineData("{0} videos", "en", "{0} Videos")]
    public void SayidanSonrakiBirimBuyutulmez(string ham, string dil, string beklenen)
        => Assert.Equal(beklenen, LanguageCatalog.Title(ham, dil));
}


