using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// T61: hedef MB ile kalite skoru birbirini sürüyor. Ölçüm pencere gösterilmeden yapılır
/// (<see cref="AppHost"/>); ekran kapısı kapalı olduğu için doğrulama buradan gelir.
///
/// Aritmetiğin kendisi <c>QualityTargetTests</c>'te ölçüldü ve mühürlendi; burada ölçülen
/// şey arayüzün o aritmetiği nasıl sürdüğü: döngü kurulmuyor mu, kullanıcının yazdığı sayı
/// yerinde duruyor mu, sınır durumu ekranda yazılı mı, kaynak yokken denetim kapalı mı.
/// </summary>
public sealed class QualityTargetUiTests
{
    private readonly ITestOutputHelper _output;

    public QualityTargetUiTests(ITestOutputHelper output) => _output = output;

    private static readonly string MeasurementDirectory =
        Path.Combine(TipSources.Root, ".calisma", "t61");

    /// <summary>
    /// 4K/60, uzun süre, büyük dosya. <c>ChipTests</c> ve <c>WindowLayoutTests</c> ile aynı
    /// örnek; taban ile tavan arasındaki kalite aralığını en geniş bırakan hâl.
    /// </summary>
    private static MediaInfo Sample() => new()
    {
        FilePath = @"C:\Kayitlar\tatil-cekimi-2160p60.mkv",
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

    private static T Empty<T>(Func<MainWindow, T> read) =>
        AppHost.Run(() => read(new MainWindow()));

    private static T Loaded<T>(Func<MainWindow, T> read) => Loaded(Sample(), read);

    private static T Loaded<T>(MediaInfo info, Func<MainWindow, T> read) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.LoadWithoutProbing(info.FilePath, info);
            return read(window);
        });

    /// <summary>
    /// T107: <see cref="Sample"/> artık taban sınırını gösteremiyor. Yerleşim skorunun
    /// <c>rate</c> yarısı yazılım kodlayıcısında kaynak ızgarasına taşınınca en küçük plan
    /// çok daha kötü puan alıyor. Arayüzden ölçülen: o 4K/60 örnekte kaliteye 1 yazınca
    /// denetimin altındaki satır <b>boş</b> geliyor, yani <c>BelowFloor</c> hiç doğmuyor.
    /// Varsayılan <c>PlanOptions</c> ile aynı kaynağın taban planı <b>-3,989</b> puan
    /// (<c>.calisma/T107/taban-kalitesi.tsv</c>), yani denetime yazılabilen en küçük sayı
    /// olan 1 bile tabanın üstünde.
    ///
    /// <para>Buradaki örnek, taban planının kalitesi ölçülerek seçildi. Arayüzün kendi
    /// seçenekleriyle bu kaynakta taban hedefi <b>20,7 MB</b> ve orada ulaşılan kalite
    /// <b>40,5/100</b>; kaynak tavanı <b>2090 MB</b> ve orada ulaşılan kalite
    /// <b>94,1/100</b> — ikisi de bu ölçünün kendi çıktısıdır, elle yazılmadı. 1 tabanın
    /// altında, 100 tavanın üstünde, 80 ikisinin arasında. (Varsayılan
    /// <c>PlanOptions</c> ile aynı kaynağın taban planı 13,358 puan; arayüz kendi
    /// seçenekleriyle daha büyük bir taban planı kuruyor, iki sayı farklı yollardan
    /// gelir ve karıştırılmamalıdır.) Aynı kaynak
    /// <c>QualityTargetTests.QualityBelowTheSmallestPlanIsReportedNotClipped</c>
    /// düzeneğinin de kaynağıdır.</para>
    /// </summary>
    private static MediaInfo FloorBoundSample() => new()
    {
        FilePath = @"C:\Kayitlar\ekran-kaydi-1440p30.mkv",
        FileSizeBytes = 2200L * 1024 * 1024,
        DurationSeconds = 3600,
        Width = 2560,
        Height = 1440,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 5_100_000,
        AudioCodec = "aac",
        AudioBitrateBps = 96_000,
        AudioChannels = 2
    };

    private static TextBox QualityBox(MainWindow window) => window.FindControl<TextBox>("TxtQualityTarget")!;
    private static Slider QualitySlider(MainWindow window) => window.FindControl<Slider>("SliderQualityTarget")!;
    private static TextBox TargetBox(MainWindow window) => window.FindControl<TextBox>("TxtTarget")!;
    private static Slider TargetSlider(MainWindow window) => window.FindControl<Slider>("SliderTarget")!;
    private static TextBlock Notice(MainWindow window) => window.FindControl<TextBlock>("TxtQualityTargetNotice")!;

    private static string Written(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// K1: kaliteyi 60'a ayarlamak tek bir güncelleme turu açar. Hedef bir kez türetilir,
    /// o türetme kaliteyi geri türetmez, ve ikinci bir okuma aynı sayıları verir — yani
    /// değerler oturmuştur, titremez.
    /// </summary>
    [Fact]
    public void KaliteYazmakTekTurAcar()
    {
        var (targets, qualities, first, second) = Loaded(window =>
        {
            var before = (window.QualityDerivedTargets, window.TargetDerivedQualities);
            QualityBox(window).Text = "60";

            var settled = (QualityBox(window).Text, TargetBox(window).Text,
                QualitySlider(window).Value, TargetSlider(window).Value);
            var again = (QualityBox(window).Text, TargetBox(window).Text,
                QualitySlider(window).Value, TargetSlider(window).Value);

            return (window.QualityDerivedTargets - before.QualityDerivedTargets,
                window.TargetDerivedQualities - before.TargetDerivedQualities,
                settled, again);
        });

        _output.WriteLine($"kaliteden hedef tureme: {targets}, hedeften kalite tureme: {qualities}");
        _output.WriteLine($"oturan degerler: kalite={first.Item1} hedef={first.Item2} MB");

        Assert.Equal(1, targets);
        Assert.Equal(0, qualities);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// K2: kullanıcının yazdığı sayı geri yazılmaz. 1'den 100'e her tam kalite değeri için
    /// kalite yazılır, hedef türetilir ve kutunun hâlâ yazılan sayıyı gösterdiği doğrulanır.
    /// Ham ölçüm <c>.calisma/t61/kalite-gidis-donus.txt</c> dosyasına yazılır.
    /// </summary>
    [Fact]
    public void YazilanKaliteYerindeDurur()
    {
        var lines = new List<string>();

        var bozuk = Loaded(window =>
        {
            var offenders = new List<string>();
            for (var quality = 1; quality <= 100; quality++)
            {
                var written = quality.ToString(CultureInfo.InvariantCulture);
                QualityBox(window).Text = written;

                var read = QualityBox(window).Text ?? "";
                var target = TargetBox(window).Text ?? "";
                var slider = QualitySlider(window).Value;

                lines.Add(FormattableString.Invariant(
                    $"kalite {quality,3} -> hedef {target,8} MB, kutuda {read,4}, kaydirici {slider:0.##}"));

                if (read != written) offenders.Add($"yazilan {written}, okunan {read}");
            }
            return offenders;
        });

        Directory.CreateDirectory(MeasurementDirectory);
        File.WriteAllText(
            Path.Combine(MeasurementDirectory, "kalite-gidis-donus.txt"),
            string.Join(Environment.NewLine, lines) + Environment.NewLine,
            Encoding.UTF8);

        foreach (var line in lines) _output.WriteLine(line);

        Assert.True(bozuk.Count == 0,
            "Kullanicinin yazdigi kalite geri yazildi:" + Environment.NewLine + string.Join(Environment.NewLine, bozuk));
    }

    /// <summary>
    /// K3: iki sınır da ekranda yazılı. 1 tabanın altında, 100 kaynağın tavanının üstünde;
    /// ikisinde de denetimin altındaki satır görünür olur ve iki cümle birbirinden ayrıdır.
    /// </summary>
    [Fact]
    public void SinirDurumuEkrandaYazili()
    {
        var (floor, ceiling, matched) = Loaded(FloorBoundSample(), window =>
        {
            QualityBox(window).Text = "1";
            var low = (Notice(window).IsVisible, Notice(window).Text ?? "");

            QualityBox(window).Text = "100";
            var high = (Notice(window).IsVisible, Notice(window).Text ?? "");

            QualityBox(window).Text = "80";
            var mid = (Notice(window).IsVisible, Notice(window).Text ?? "");

            return (low, high, mid);
        });

        _output.WriteLine($"taban: {floor.Item2}");
        _output.WriteLine($"tavan: {ceiling.Item2}");

        Assert.True(floor.Item1,
            "Taban sinirinda satir gorunmuyor. Bu ornekte arayuzun taban hedefi 20,7 MB ve orada ulasilan kalite 40,5/100 olculdu; 1 istegi onun altinda kaldigi icin BelowFloor dogmali. Satir yoksa ya taban rejimi kayboldu ya da ornek artik onu gostermiyor - T107'de Sample() tam olarak boyle sessizce ise yaramaz olmustu.");
        Assert.True(ceiling.Item1, "Tavan sinirinda satir gorunmuyor.");
        Assert.NotEqual(floor.Item2, ceiling.Item2);
        Assert.False(matched.Item1, $"Sinir yokken satir duruyor: {matched.Item2}");
    }

    /// <summary>
    /// K3 devamı: cümleler sayıyı sessizce değiştirmiyor, ne olduğunu söylüyor — ulaşılan
    /// hedef ve ulaşılan kalite ikisi de metnin içinde geçer.
    /// </summary>
    [Fact]
    public void SinirCumlesiSayilariSoyler()
    {
        var (text, target) = Loaded(window =>
        {
            QualityBox(window).Text = "100";
            return (Notice(window).Text ?? "", TargetBox(window).Text ?? "");
        });

        Assert.Contains(target, text);
        Assert.Contains("/100", text);
    }

    /// <summary>K4: kaynak yokken denetim kapalı ve neden kapalı olduğu yazılı.</summary>
    [Fact]
    public void VideoYokkenKaliteGirdisiKapali()
    {
        var (sliderOn, boxOn, noticeShown, notice) = Empty(window =>
            (QualitySlider(window).IsEnabled, QualityBox(window).IsEnabled,
             Notice(window).IsVisible, Notice(window).Text ?? ""));

        _output.WriteLine($"kapali ipucu: {notice}");

        Assert.False(sliderOn);
        Assert.False(boxOn);
        Assert.True(noticeShown);
        Assert.NotEqual("", notice);
    }

    /// <summary>K4: video yüklenince denetim açılır ve yönlendirme satırı kalkar.</summary>
    [Fact]
    public void VideoYuklenincaKaliteGirdisiAcilir()
    {
        var (sliderOn, boxOn, noticeShown, quality) = Loaded(window =>
            (QualitySlider(window).IsEnabled, QualityBox(window).IsEnabled,
             Notice(window).IsVisible, QualityBox(window).Text ?? ""));

        Assert.True(sliderOn);
        Assert.True(boxOn);
        Assert.False(noticeShown);
        Assert.NotEqual("", quality);
    }

    /// <summary>
    /// K3'ün yerleşim bedeli. Sınır satırı görününce sol ayar sütunu 904'ten 941 piksele
    /// çıkıyor ve sayfa içeriği tasarım boyutunun görüş alanına <b>tam</b> oturuyor —
    /// ölçülen 965'e 965. Pay sıfır olduğu için bu ölçüm nöbetçi: sol sütuna bir piksel
    /// daha eklenirse dolu sayfa da kaymaya başlar ve burası kırmızıya düşer.
    ///
    /// <para>Düzenek <c>WindowLayoutTests.LayOutAt</c> ile aynı: yerleştirme pencereye
    /// değil kök görsel çocuğuna verilir, yoksa <c>Window.ArrangeSetBounds</c> boyutu
    /// yutar ve yerleşim ekranın boyunda kalır.</para>
    /// </summary>
    [Fact]
    public void SinirSatiriDoluSayfayiTasirmaz()
    {
        var (viewport, content) = AppHost.Run(() =>
        {
            var window = new MainWindow();
            var info = Sample();
            window.LoadWithoutProbing(info.FilePath, info);
            window.SettleFades();
            QualityBox(window).Text = "100";

            window.Width = double.NaN;
            window.Height = double.NaN;
            window.TryFindResource("WindowPreferredWidth", out var width);
            window.TryFindResource("WindowPreferredHeight", out var height);
            var size = new Size((double)width!, (double)height!);

            window.Measure(size);
            window.Arrange(new Rect(size));
            window.UpdateLayout();
            var root = (Layoutable)window.GetVisualChildren().Single();
            root.Measure(size);
            root.Arrange(new Rect(size));

            var page = window.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.Name == "PageShrink");
            return (page.Viewport.Height, page.Extent.Height);
        });

        _output.WriteLine($"gorus alani {viewport:0.#}, icerik {content:0.#}, pay {viewport - content:0.#}");

        Assert.True(content <= viewport + 0.5,
            $"Sinir satiri dolu sayfayi tasirdi: gorus alani {viewport:0.#}, icerik {content:0.#}.");
    }

    /// <summary>
    /// Ters yön: hedef MB elle yazılınca kalite türetilir, ve o türetme hedefi geri
    /// yazmaz — kullanıcının yazdığı MB olduğu gibi durur.
    /// </summary>
    [Fact]
    public void YazilanHedefYerindeDururVeKaliteyiSurer()
    {
        var (target, quality, before, derivedTargets, derivedQualities) = Loaded(window =>
        {
            var first = QualityBox(window).Text ?? "";
            var targets = window.QualityDerivedTargets;
            var qualities = window.TargetDerivedQualities;
            TargetBox(window).Text = Written(24);
            return (TargetBox(window).Text ?? "", QualityBox(window).Text ?? "", first,
                window.QualityDerivedTargets - targets, window.TargetDerivedQualities - qualities);
        });

        _output.WriteLine($"hedef {target} MB -> kalite {before} yerine {quality}");

        Assert.Equal(Written(24), target);
        Assert.Equal(0, derivedTargets);
        Assert.Equal(1, derivedQualities);
        Assert.NotEqual(before, quality);
    }
}
