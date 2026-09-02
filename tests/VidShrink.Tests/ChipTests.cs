using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using VidShrink.App;

namespace VidShrink.Tests;

/// <summary>
/// T46/K1: hedef yongaları etiketsiz. Ölçüm pencere gösterilmeden yapılır; yerleşim
/// motoru aynı motordur, masaüstünde hiçbir şey açılmaz (<see cref="AppHost"/>).
/// </summary>
public sealed class ChipTests
{
    private static readonly Size WindowSize = new(1560, 1060);

    private static T Read<T>(Func<MainWindow, T> read) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.Width = double.NaN;
            window.Height = double.NaN;
            window.Measure(WindowSize);
            window.Arrange(new Rect(WindowSize));
            window.UpdateLayout();
            return read(window);
        });

    private static WrapPanel ChipStrip(MainWindow window) =>
        window.GetVisualDescendants()
            .OfType<WrapPanel>()
            .Single(panel => panel.GetVisualDescendants().OfType<Button>().Any(b => b.Name == "ChipWhatsApp"));

    /// <summary>
    /// Yonga yalnız sayıyı gösterir: içinde tek bir metin var ve o metin sayının kendisi.
    /// Eskiden üçünün içi iki satırlı bir yığındı.
    /// </summary>
    [Theory]
    [InlineData("ChipWhatsApp", "16")]
    [InlineData("Chip8", "8")]
    [InlineData("Chip25", "25")]
    [InlineData("Chip100", "100")]
    [InlineData("Chip128", "128")]
    [InlineData("Chip180", "180")]
    public void YongaTekSayiGosterir(string name, string number)
    {
        var text = Read(window =>
        {
            var chip = ChipStrip(window).Children.OfType<Button>().Single(b => b.Name == name);
            var blocks = chip.GetVisualDescendants().OfType<TextBlock>().ToList();
            return blocks.Count == 1 ? blocks[0].Text : $"{blocks.Count} metin: {string.Join(" / ", blocks.Select(b => b.Text))}";
        });

        Assert.Equal(number, text);
    }

    /// <summary>
    /// Etiketler kalkınca satır yüksekliği düştü. Sayılar ölçümden: önce 46, sonra 27;
    /// şerit üç satırdan (162) iki satıra (70) indi. Ölçüm başsız pencerede yapılıyor,
    /// bu yüzden genişlik ölçüm argümanından gelir ve sabittir.
    /// </summary>
    [Fact]
    public void EtiketsizYongaSeridiKisaldi()
    {
        var (rowHeight, stripHeight) = Read(window =>
        {
            var strip = ChipStrip(window);
            var chips = strip.Children.OfType<Button>().ToList();
            return (chips.Max(c => c.Bounds.Height), strip.Bounds.Height);
        });

        Assert.Equal(27, rowHeight);
        Assert.Equal(70, stripHeight);
    }

    /// <summary>
    /// Açıklama balonda kaldı: eski etiket balonun ilk satırıdır, gerekçe maddeleri
    /// altında durur. Kalite paneli balonun ilk çocuğunu koruyarak eklendiği için ikisi
    /// tek bir iç yığında toplandı.
    /// </summary>
    [Theory]
    [InlineData("ChipWhatsApp", "WhatsApp Recommended")]
    [InlineData("Chip128", "Sharing Maximum")]
    [InlineData("Chip180", "WhatsApp Web Maximum")]
    public void BalonunIlkSatiriEskiEtiket(string name, string label)
    {
        var first = Read(window =>
        {
            var chip = ChipStrip(window).Children.OfType<Button>().Single(b => b.Name == name);
            var tip = (StackPanel)ToolTip.GetTip(chip)!;
            return ((StackPanel)tip.Children[0]).Children.OfType<TextBlock>().First().Text;
        });

        Assert.Equal(label, first);
    }

    private const string SamplePath = @"C:\Kayitlar\tatil-cekimi-2160p60.mkv";

    /// <summary>
    /// Plan panelini en çok zorlayan gerçekçi hâl: 4K/60, uzun süre, büyük dosya.
    /// <c>WindowLayoutTests</c> ile aynı örnek; yoklama çağrılmıyor, ölçüm ffmpeg'e ve
    /// diskteki bir dosyaya bağlı değil.
    /// </summary>
    private static VidShrink.Core.MediaInfo Sample() => new()
    {
        FilePath = SamplePath,
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

    /// <summary>Sıradan bir kayıt: 1080p30, bir dakika. Gerekçe listesi kısa olan hâl.</summary>
    private static VidShrink.Core.MediaInfo Modest() => new()
    {
        FilePath = @"C:\Kayitlar\telefon-kaydi-1080p30.mp4",
        FileSizeBytes = 90L * 1024 * 1024,
        DurationSeconds = 62.0,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 11_600_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    private static T Loaded<T>(VidShrink.Core.MediaInfo info, Func<MainWindow, T> read) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.LoadWithoutProbing(info.FilePath, info);
            window.SettleFades();
            window.Width = double.NaN;
            window.Height = double.NaN;
            window.Measure(WindowSize);
            window.Arrange(new Rect(WindowSize));
            window.UpdateLayout();
            return read(window);
        });

    private static Control Named(MainWindow window, string name) =>
        window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);

    /// <summary>
    /// Boş önizleme paneli tabanının <b>altına inmez</b> ve tasarım boyutunda artan yeri
    /// alır.
    ///
    /// <para>T46/K2 bu ölçüyü "panel bırakma alanı ölçüsünde değil, panel tabanında" diye
    /// kurdu; T52 tabanı 144 → 256 → 512 diye iki katına çıkardı. T54/K4 orta sütunun
    /// esneyen satırını plan panelinden önizlemeye aldı, dolayısıyla panel artık tabanına
    /// <b>oturmuyor</b>, tabanının üstünde artan yer kadar uzuyor: ölçülen 603, taban 512.</para>
    ///
    /// <para><b>Bu ölçü neyi koruyor:</b> iki yönü birden. Aşağı yönü — panel hiçbir
    /// durumda tabanının altına inmez, yani bırakma alanı kalkınca eskisi gibi çökmez.
    /// Yukarı yönü — sütunda artan yer varken panel onu alır. <b>Bozulursa kullanıcı ne
    /// görür:</b> taban kırılırsa boş önizleme yeniden bir şerit kadar kalır; esneme
    /// kırılırsa panel tam tabanında durur ve altında kullanılmayan boşluk belirir.</para>
    ///
    /// <para>Sayı testte sabitlenmiyor: taban <c>PanelMinHeight</c>'tan türetiliyor, kat
    /// değişirse ölçü de kayar.</para>
    /// </summary>
    [Fact]
    public void BosPanelTabaninaOturmazAmaAltinaDaInmez()
    {
        var (height, token, basis) = Read(window =>
        {
            var shell = Named(window, "Shell");
            window.TryFindResource("PlaybackIdleMinHeight", out var value);
            window.TryFindResource("PanelMinHeight", out var panel);
            return (shell.Bounds.Height, (double)value!, (double)panel!);
        });

        Assert.Equal(basis * 2, token);
        Assert.True(
            height > token,
            $"Boş önizleme paneli {height:0.#}; tabanı ({token:0}) aşmadı. Orta sütunun esneyen "
            + "satırı önizlemede değilse panel tam tabanında durur ve altında ölü boşluk kalır.");
    }

    // T52: burada "boş panel yükselirken plan paneli daralmadı" diyen bir ölçü vardı
    // (YuksekBosPanelPlanPaneliniDaraltmadi, PlanPanel = 512 bekliyordu) ve silindi.
    // İddiası bugün yanlış: oynatma panelinin taban boyu 256'dan 512'ye çıkınca aynı
    // sütunda plan paneli 512'den 320'ye indi. Daralma T52'nin bilinçli sonucu, gerileme
    // değil. Plan panelinin asıl korunması gereken yanı — içeriğinin panele sığması, yani
    // gerekçelerin kırpılmadan görünmesi — PlanPaneliKatliykenKaymaz ile ölçülüyor ve o
    // ölçü yeşil. Bu ölçü sabit bir sayıdan başka bir şey korumuyordu.

    /// <summary>
    /// T46/K6: gerekçeler katlıyken plan paneli tipik durumda kaymıyor. İki girdide de
    /// ölçüldü — 4K/60 (T99'dan beri yedi gerekçe, önce dokuzdu) ve 1080p30 (altı gerekçe). Metin kısaltılmadı,
    /// yalnız katlandı; <c>PlanScroll</c> uzun listede taşma supabı olarak duruyor.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlanPaneliKatliykenKaymaz(bool modest)
    {
        var (content, viewport) = Loaded(modest ? Modest() : Sample(), window =>
        {
            var scroll = (ScrollViewer)Named(window, "PlanScroll");
            var body = (StackPanel)Named(window, "PlanBody");
            return (body.DesiredSize.Height, scroll.Viewport.Height);
        });

        Assert.True(content <= viewport, $"icerik={content:0.##} gorunur={viewport:0.##}");
    }

    /// <summary>
    /// Katlıyken gerekçe listesi görünmez, başlığı listenin uzunluğunu söyler. İki iddia
    /// var: başlıktaki sayı listedeki madde sayısıyla aynı olmak zorunda, ve bu örnekte o
    /// sayı yedi.
    ///
    /// <para>T99'a kadar yediydi<b>ler</b> değil dokuzdu. Taban av1'de 0,020×1,25 = 0,025
    /// bppf'ten 0,0095×1,52 = 0,01444'e inince 4K/60 kaynağı kendi kare hızında kalabilir
    /// hâle geldi; kare hızı kesintisini anlatan iki satır birden düştü —
    /// <c>AdviceCode.FrameRateReduced</c> strateji satırı ("frame rate was lowered...") ve
    /// <c>ReasonCode.FrameRateReduced</c> gerekçe satırı ("frame rate reduced to 39.96...").
    /// Kaybolan başka bir şey yok: kalan yedi madde eskisiyle birebir aynı,
    /// yalnız yerleşim 922x518@39,96'dan 1306x734@59,94'e ve tahmini kalite 68,9'dan
    /// 74,4'e çıktı. 1080p30 örneğinde (<see cref="Modest"/>) sayı altı, değişmedi.</para>
    /// </summary>
    [Fact]
    public void KatliGerekceBasligiSayiyiSoyler()
    {
        var (visible, head, count) = Loaded(Sample(), window =>
            (Named(window, "PlanReasons").IsVisible,
                ((TextBlock)Named(window, "TxtPlanReasonsHead")).Text,
                ((Panel)Named(window, "PlanReasons")).Children.Count));

        Assert.False(visible);
        Assert.Equal(7, count);
        Assert.Equal($"Why These Choices · {count}", head);
    }

}
