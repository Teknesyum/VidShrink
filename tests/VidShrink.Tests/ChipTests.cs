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

    [Fact]
    public void PlanOlcumModestYazdir()
    {
        var info = Modest();
        var report = AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.LoadWithoutProbing(info.FilePath, info);
            window.SettleFades();
            window.Width = double.NaN;
            window.Height = double.NaN;
            window.Measure(WindowSize);
            window.Arrange(new Rect(WindowSize));
            window.UpdateLayout();

            var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "PlanScroll");
            var reasons = window.GetVisualDescendants().OfType<StackPanel>().Single(s => s.Name == "PlanReasons");
            var facts = window.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "PlanFacts");
            var plan = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PlanPanel");
            return $"MODEST PlanScroll icerik={scroll.Extent.Height:0.##} gorunur={scroll.Viewport.Height:0.##} tasma={scroll.Extent.Height - scroll.Viewport.Height:0.##} | PlanPanel g={plan.Bounds.Width:0.##} y={plan.Bounds.Height:0.##} | PlanFacts satir={facts.RowDefinitions.Count} y={facts.Bounds.Height:0.##} g={facts.Bounds.Width:0.##} | PlanReasons adet={reasons.Children.Count} y={reasons.Bounds.Height:0.##}";
        });

        Assert.True(false, report);
    }

    [Fact]
    public void PlanOlcumYazdir()
    {
        var report = AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.LoadWithoutProbing(SamplePath, Sample());
            window.SettleFades();
            window.Width = double.NaN;
            window.Height = double.NaN;
            window.Measure(WindowSize);
            window.Arrange(new Rect(WindowSize));
            window.UpdateLayout();

            var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "PlanScroll");
            var plan = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PlanPanel");
            var shell = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Shell");
            var reasons = window.GetVisualDescendants().OfType<StackPanel>().Single(s => s.Name == "PlanReasons");
            var facts = window.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "PlanFacts");
            return $"PlanScroll icerik={scroll.Extent.Height:0.##} gorunur={scroll.Viewport.Height:0.##} tasma={scroll.Extent.Height - scroll.Viewport.Height:0.##} | PlanPanel={plan.Bounds.Height:0.##} | Shell={shell.Bounds.Height:0.##} | PlanFacts satir={facts.RowDefinitions.Count} y={facts.Bounds.Height:0.##} | PlanReasons adet={reasons.Children.Count} y={reasons.Bounds.Height:0.##}";
        });

        Assert.True(false, report);
    }

    [Fact]
    public void BosPanelOlcumYazdir()
    {
        var report = Read(window =>
        {
            var shell = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Shell");
            var plan = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PlanPanel");
            return $"BosShell={shell.Bounds.Height:0.##} MinHeight={shell.MinHeight:0.##} | BosPlanPanel={plan.Bounds.Height:0.##}";
        });

        Assert.True(false, report);
    }
}
