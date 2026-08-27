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
    /// T46/K2: önizleme yokken panel bırakma alanı ölçüsünde değil, panel tabanında.
    /// T52/K3: o taban panel tabanının iki katına çıktı (144 → 256 → 512). Sayı testte
    /// sabitlenmiyor, <c>PanelMinHeight</c>'tan türetiliyor; kat değişirse test de kayar.
    /// </summary>
    [Fact]
    public void BosPanelPanelTabaninaOturur()
    {
        var (height, token, basis) = Read(window =>
        {
            var shell = Named(window, "Shell");
            window.TryFindResource("PlaybackIdleMinHeight", out var value);
            window.TryFindResource("PanelMinHeight", out var panel);
            return (shell.Bounds.Height, (double)value!, (double)panel!);
        });

        Assert.Equal(basis * 2, token);
        Assert.Equal(token, height);
    }

    /// <summary>
    /// T46/K2: boş panel yükselirken plan paneli daralmadı — tavanı
    /// <c>PlanPanelMaxHeight</c> (512) ve tasarım boyutunda hâlâ oraya varıyor.
    /// </summary>
    [Fact]
    public void YuksekBosPanelPlanPaneliniDaraltmadi()
    {
        var height = Read(window => Named(window, "PlanPanel").Bounds.Height);
        Assert.Equal(512, height);
    }

    /// <summary>
    /// T46/K6: gerekçeler katlıyken plan paneli tipik durumda kaymıyor. İki girdide de
    /// ölçüldü — 4K/60 (dokuz gerekçe) ve 1080p30 (altı gerekçe). Metin kısaltılmadı,
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

    /// <summary>Katlıyken gerekçe listesi görünmez, başlığı sayıyı söyler.</summary>
    [Fact]
    public void KatliGerekceBasligiSayiyiSoyler()
    {
        var (visible, head) = Loaded(Sample(), window =>
            (Named(window, "PlanReasons").IsVisible, ((TextBlock)Named(window, "TxtPlanReasonsHead")).Text));

        Assert.False(visible);
        Assert.Equal("Why These Choices · 9", head);
    }
}
