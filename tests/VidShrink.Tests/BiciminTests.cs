using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
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
    /// <summary>
    /// Karenin cekildigi olcu. Pencerenin izin verdigi en dar olcu (1040x720,
    /// <c>MainWindow.axaml</c>, <c>MinWidth="1040"</c>) burada <b>olculmuyor</b>: o olcude
    /// <c>InfoGrid</c>'in dort sutunu hucreyi 67 px'e dusuruyor ve etiketler sigmiyor.
    /// Dar pencerenin kabul edilen davranisi henuz karara baglanmadi, ayri sozlesmede.
    /// </summary>
    public static readonly Size Genis = new(1600, 1000);

    public static TheoryData<string, double, double> IkiDilTekOlcu() => new()
    {
        { "tr", 1600, 1000 },
        { "en", 1600, 1000 }
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

                Visual? node = window.GetVisualDescendants().OfType<UniformGrid>()
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
        var tasan = Read(dil, new Size(genislik, yukseklik), window => Tasanlar(window, new Size(genislik, yukseklik), null));

        Assert.True(tasan.Count == 0, string.Join(Environment.NewLine, tasan));
    }

    /// <summary>
    /// Olcunun mutasyon sinavi: hucreye sigmayacak kadar uzun bir etiket verildiginde
    /// olcu <b>kirmizi</b> donmeli. Donmezse yukaridaki yesil bir sey soylemiyor demektir.
    /// </summary>
    [Fact]
    public void OlcuUzunEtiketiYakalar()
    {
        var tasan = Read("tr", Genis, window => Tasanlar(window, Genis, "Cok Uzun Bir Kaynak Bilgi Etiketi Ornegi"));

        Assert.NotEmpty(tasan);
    }

    /// <summary>
    /// Her hucrenin ilk metnini alir, sarmayi kapatir, yeniden yerlestirir ve metin
    /// genisligini hucre genisligiyle karsilastirir. <paramref name="mutasyon"/> verilirse
    /// ilk etiketin metni onunla degistirilir.
    /// </summary>
    private static List<string> Tasanlar(MainWindow window, Size olcu, string? mutasyon)
    {
        var grid = window.GetVisualDescendants().OfType<UniformGrid>()
            .Single(g => g.Name == "InfoGrid");

        var hucreler = grid.Children
            .OfType<StackPanel>()
            .Select(cell => (cell, label: cell.Children.OfType<TextBlock>().First()))
            .ToList();

        foreach (var (_, label) in hucreler) label.TextWrapping = TextWrapping.NoWrap;
        if (mutasyon is not null) hucreler[0].label.Text = mutasyon;

        window.Measure(olcu);
        window.Arrange(new Rect(olcu));
        window.UpdateLayout();

        return hucreler
            .Where(pair => pair.label.TextLayout.Width > pair.cell.Bounds.Width + 0.5)
            .Select(pair => $"{pair.label.Text}: metin {pair.label.TextLayout.Width:0.#} px, hucre {pair.cell.Bounds.Width:0.#} px")
            .ToList();
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

            window.Measure(Genis);
            window.Arrange(new Rect(Genis));
            window.UpdateLayout();

            var satir = (Layoutable)ozet.GetVisualParent()!;
            return (ozet.Bounds.Left + ozet.TextLayout.Width, satir.Bounds.Width);
        });

        Assert.True(ozetSagi <= satirGenisligi + 0.5,
            $"ozet metni {ozetSagi:0.#} px'te bitiyor, satir {satirGenisligi:0.#} px");
    }

    private const string OrnekYol = @"C:\Kayitlar\tatil-cekimi-2160p60.mkv";

    /// <summary>
    /// <c>WindowLayoutTests.Sample</c> ile ayni tasiyici kaynak. Yoklama cagrilmiyor;
    /// olcum diskteki hicbir dosyaya ve hicbir dis araca bagli degil.
    /// </summary>
    private static MediaInfo Ornek() => new()
    {
        FilePath = OrnekYol,
        FileSizeBytes = 420L * 1024 * 1024,
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
    private static T Yuklu<T>(string dil, Func<MainWindow, T> read) =>
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

                window.LoadWithoutProbing(OrnekYol, Ornek());
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

        Assert.Equal(1078, toplam);
        Assert.Equal(117, dilBasina["en"]);
        Assert.Equal(49, dilBasina["tr"]);
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
    /// kolundan govde koluna gecen metinlerde (o turda 124, kirk dil eklendikten sonra 784, tagline anahtarlari silinince 778, oynatici 1. dalga anahtarlariyla 844, 3. dalga anahtarlariyla 898, 2. dalga parca anahtarlariyla 950, 4. dalga arac ve gelismis anahtarlariyla 985, 8b kaydedici anahtarlariyla 1078)
    /// ilk sozcuk disindaki <c>ffmpeg</c>
    /// dil dosyasindaki yazimiyla kaliyordu — <c>en/main.drop.hint</c>,
    /// <c>en|tr/main.reason.encoder-fallback-not-in-build</c>. Bu olcu butun dillerin butun anahtarlarinin (bugun 29455 kalem)
    /// <b>tamamini</b> gezer, tek bir kalemi bile atlamaz.</para>
    /// <para>Sayim yansimanin gordugu 43 dil uzerinden: 42 dil klasoru ve gomulu kaynak
    /// adindan gelen bir dil daha, her biri 685 anahtar. 5. dalga ffmpeg oynatma borusunu
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
    /// main.tab.recorder, 43 x 685 = 29455.</para>
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

        Assert.Equal(29455, gezilen);
        Assert.Empty(kayip);
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
}


