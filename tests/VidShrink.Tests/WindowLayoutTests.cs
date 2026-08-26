using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// T14 K1/K5 ve T23 K7: sayfa kaymayacak, metin kırpılmayacak — hem boş hem dosya
/// yüklenmiş pencerede.
///
/// Kanıt bugüne kadar ekran görüntüsüyle isteniyordu ve dört sözleşmede birden alınamadan
/// bekledi. Kaydırma çubuğunun görünüp görünmediği ölçülebilir bir şey: bir
/// <see cref="ScrollViewer"/> yalnız içeriği görüş alanından büyükse kayar. Pencere
/// gösterilmeden ölçülüp yerleştirilirse aynı yerleşim motoru koşar, masaüstünde pencere
/// açılmaz ve ölçüm başsız kalır.
///
/// Hiçbir boyut ölçüme elle yazılmıyor: tasarım boyutu <c>Theme.axaml</c> belirteçlerinden,
/// açılış boyutu ekranın çalışma alanından, taban boyut pencerenin kendi
/// <see cref="Layoutable.MinWidth"/> / <see cref="Layoutable.MinHeight"/> değerlerinden
/// geliyor.
/// </summary>
public sealed class WindowLayoutTests
{
    /// <summary>Bir taşıyıcının taştığı miktar; sıfırın üstündeyse çubuk görünür.</summary>
    private readonly record struct Overflow(string Name, double Vertical, double Horizontal)
    {
        public override string ToString() =>
            $"{Name}: dikey +{Vertical:0.#}, yatay +{Horizontal:0.#}";
    }

    private const string SamplePath = @"C:\Kayitlar\tatil-cekimi-2160p60.mkv";

    /// <summary>
    /// Dolu hâlin kaynağı. Yoklama çağrılmıyor, dolayısıyla ölçüm hiçbir dış araca ve
    /// diskteki hiçbir dosyaya bağlı değil. Değerler yerleşimi en çok zorlayan gerçekçi
    /// hâl: 4K/60, uzun süre, büyük dosya — plan paneli bu girdide en çok satırı üretiyor.
    /// </summary>
    private static MediaInfo Sample() => new()
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

    /// <summary>
    /// Sayfanın kendi kaydırıcısı olmayan, kasten kayan taşıyıcılar. <c>PlanScroll</c>
    /// plan paneli tavanının açtığı supap — plan uzadığında onun kayması beklenen
    /// davranış. <see cref="TextBox"/> içindeki taşıyıcılar da metnin kendi işi.
    /// </summary>
    private static bool IsPageLevel(ScrollViewer viewer)
        => viewer.Name != "PlanScroll" && viewer.FindAncestorOfType<TextBox>() is null;

    private static T Read<T>(Size size, bool loaded, Func<MainWindow, T> read) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();

            if (loaded)
            {
                window.LoadWithoutProbing(SamplePath, Sample());

                // Bırakma alanını gizleme kararını 240 ms sonra bir DispatcherTimer
                // veriyor; pencere gösterilmediği için o zamanlayıcı ateşlenmiyor. Ölçüm
                // geçişin oturmuş halini istiyor — kullanıcının yarım saniye sonra
                // gördüğü yerleşim bu.
                window.SettleFades();
            }

            // Pencerenin kendi Width/Height degerleri ArrangeCore icindeki
            // ApplyLayoutConstraints tarafindan olcum argumanina uygulanir ve argumani
            // yutar; temizlenmezse her boyut ayni yerlesimi olcer.
            window.Width = double.NaN;
            window.Height = double.NaN;

            window.Measure(size);
            window.Arrange(new Rect(size));
            window.UpdateLayout();

            return read(window);
        });

    private static IReadOnlyList<Overflow> LayOut(Size size, bool loaded) =>
        Read(size, loaded, window => (IReadOnlyList<Overflow>)window.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .Where(viewer => viewer.IsEffectivelyVisible && IsPageLevel(viewer))
            .Select(viewer => new Overflow(
                string.IsNullOrEmpty(viewer.Name) ? viewer.GetType().Name : viewer.Name!,
                viewer.Extent.Height - viewer.Viewport.Height,
                viewer.Extent.Width - viewer.Viewport.Width))
            .Where(entry => entry.Vertical > 0.5 || entry.Horizontal > 0.5)
            .ToList());

    private static double Token(string key) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            Assert.True(window.TryFindResource(key, out var value), $"{key} belirteci yok.");
            return (double)value!;
        });

    /// <summary>Pencerenin istediği boyut; <c>Theme.axaml</c>'de yazılı.</summary>
    private static Size DesignSize() => new(Token("WindowPreferredWidth"), Token("WindowPreferredHeight"));

    private static Size MinimumSize() =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            return new Size(window.MinWidth, window.MinHeight);
        });

    /// <summary>
    /// Pencere <c>WindowState="Maximized"</c> ile açılıyor, yani gerçek açılış boyutu
    /// biçimlemede yazan değil ekranın çalışma alanı. Ölçüm o alanı bu makinenin görev
    /// çubuğuna göre elle yazmıyor, <see cref="Screens"/>'den türetiyor.
    /// </summary>
    private static Size WorkingArea() =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            var screen = window.Screens.Primary ?? window.Screens.All.FirstOrDefault();
            Assert.True(screen is not null, "Ekran bulunamadı; açılış boyutu türetilemiyor.");

            var scaling = screen!.Scaling <= 0 ? 1 : screen.Scaling;
            return new Size(screen.WorkingArea.Width / scaling, screen.WorkingArea.Height / scaling);
        });

    private static string Describe(Size size, bool loaded, IEnumerable<Overflow> overflowing) =>
        $"{(loaded ? "Dolu" : "Boş")} pencerede {size.Width:0}x{size.Height:0}: "
        + string.Join(", ", overflowing);

    /// <summary>
    /// Pencerenin istediği boyutta sayfa kaymıyor — dosya yüklüyken de. Dolu hâli ayakta
    /// tutan şey plan panelinin tavanı; tavan kalkarsa bu ölçüm de kırmızıya düşer.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThePageDoesNotScrollAtTheDesignSize(bool loaded)
    {
        var size = DesignSize();
        var overflowing = LayOut(size, loaded);

        Assert.True(overflowing.Count == 0, Describe(size, loaded, overflowing));
    }

    /// <summary>
    /// Açılış boyutu — ekranın çalışma alanı. Tasarım boyutunu karşılayan bir ekranda
    /// dolu sayfa kaymayacak. Ekran ondan küçükse pencere kendi tabanının altında kalıyor
    /// demektir; orada kaydırma beklenen davranış ve ölçüm tek bir dış taşıyıcının
    /// kaydığını söylüyor. İki dal da ölçtüğü sayıyı hata iletisine yazıyor.
    /// </summary>
    [Fact]
    public void TheLoadedPageDoesNotScrollAtTheStartingSize()
    {
        var size = WorkingArea();
        var design = DesignSize();
        var overflowing = LayOut(size, loaded: true);

        if (size.Width >= design.Width && size.Height >= design.Height)
        {
            Assert.True(overflowing.Count == 0, Describe(size, loaded: true, overflowing));
            return;
        }

        Assert.True(
            overflowing.Count == 1,
            $"Ekranın çalışma alanı ({size.Width:0}x{size.Height:0}) tasarım boyutunun "
            + $"({design.Width:0}x{design.Height:0}) altında; tek bir dış kaydırma bekleniyordu. "
            + Describe(size, loaded: true, overflowing));
    }

    /// <summary>
    /// Sayfanın kaymayı bıraktığı yükseklik, tasarım genişliğinde. Sayı pinli: yerleşim
    /// şiştiğinde ölçüm kırmızıya düşer ve yeniden konuşulur. Dolu sayfa boş sayfadan
    /// daha erken sığıyor, çünkü bırakma alanı dosya yüklenince kalkıyor.
    ///
    /// <para>Aralık dar tutulmadı: panel yükseklikleri kullanılabilir kodlayıcı kümesine
    /// göre makineden makineye oynuyor. Plan paneli tavanının nöbetini bu ölçüm değil,
    /// tasarım boyutu ölçümü ile tavan ölçümü tutuyor.</para>
    /// </summary>
    [Theory]
    [InlineData(false, 950, 1150)]
    [InlineData(true, 900, 1100)]
    public void ThePageStopsScrollingAtThisHeight(bool loaded, double least, double most)
    {
        var width = DesignSize().Width;
        var fitting = double.NaN;

        for (var height = 600.0; height <= 1600.0; height += 4)
        {
            if (LayOut(new Size(width, height), loaded).Count != 0) continue;
            fitting = height;
            break;
        }

        Assert.True(
            !double.IsNaN(fitting),
            $"{(loaded ? "Dolu" : "Boş")} sayfa 1600 piksele kadar hiçbir yükseklikte sığmadı.");
        Assert.InRange(fitting, least, most);
    }

    /// <summary>
    /// Dolu sayfayı kaymaktan alıkoyan şey plan panelinin tavanı. Tavan kaldırılırsa panel
    /// içeriği kadar uzar; ölçüm hem burada hem tasarım boyutu ölçümünde kırmızıya düşer.
    /// Tavanın <b>değeri</b> ölçülmüyor, devrede olduğu ölçülüyor.
    /// </summary>
    [Fact]
    public void ThePlanPanelCeilingIsWhatHoldsTheLoadedPage()
    {
        var (ceiling, panelHeight, contentHeight) = Read(DesignSize(), loaded: true, window =>
        {
            var panel = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PlanPanel");
            var body = window.GetVisualDescendants().OfType<StackPanel>().Single(b => b.Name == "PlanBody");
            window.TryFindResource("PlanPanelMaxHeight", out var value);
            return ((double)value!, panel.Bounds.Height, body.DesiredSize.Height);
        });

        Assert.Equal(ceiling, panelHeight, 1);
        Assert.True(
            contentHeight > ceiling,
            $"Plan içeriği ({contentHeight:0.#}) tavanın ({ceiling:0}) altında kaldı; tavan devrede değil.");
    }

    /// <summary>
    /// Taban boyutta kaydırma <b>beklenen</b> davranış — içerik gerçekten sığmıyor. Ölçüm
    /// bunu kaydediyor ki bir gün sığdığı zaman kimse fark etmeden geçmesin.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheSmallestSizeStillScrolls(bool loaded)
    {
        var size = MinimumSize();
        var overflowing = LayOut(size, loaded);

        Assert.True(
            overflowing.Count == 1,
            $"Taban boyutta ({size.Width:0}x{size.Height:0}) tek bir dış kaydırma bekleniyordu. "
            + Describe(size, loaded, overflowing));
    }

    /// <summary>
    /// Avalonia seçili olmayan sekmenin içeriğini görsel ağaca almıyor, dolayısıyla tek
    /// bir ölçüm yalnız açık sekmeyi görür. Ölçüm bütün sekmeleri dolaşıyor.
    ///
    /// <para><see cref="Layoutable.DesiredSize"/> kenar boşluğunu içeriyor,
    /// <see cref="Visual.Bounds"/> içermiyor; karşılaştırmadan önce boşluk düşülüyor,
    /// yoksa yanı boşluklu her denetim kırpılmış görünür.</para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoTextIsClippedAtTheSmallestSize(bool loaded)
    {
        var size = MinimumSize();

        var clipped = AppHost.Run(() =>
        {
            var window = new MainWindow();
            if (loaded)
            {
                window.LoadWithoutProbing(SamplePath, Sample());
                window.SettleFades();
            }

            window.Width = double.NaN;
            window.Height = double.NaN;
            window.Measure(size);
            window.Arrange(new Rect(size));
            window.UpdateLayout();

            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            var found = new List<string>();

            for (var index = 0; index < tabs.ItemCount; index++)
            {
                tabs.SelectedIndex = index;
                window.Measure(size);
                window.Arrange(new Rect(size));
                window.UpdateLayout();

                var header = (tabs.ContainerFromIndex(index) as TabItem)?.Header?.ToString() ?? $"{index}";

                found.AddRange(window.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(block => block.IsEffectivelyVisible
                                 && block.Bounds.Width > 0
                                 && block.DesiredSize.Width - block.Margin.Left - block.Margin.Right
                                    > block.Bounds.Width + 0.5)
                    .Select(block => $"{header}: {(block.Text ?? "(boş)").Split('\n')[0]}"));
            }

            return found;
        });

        Assert.True(
            clipped.Count == 0,
            $"En küçük boyutta ({size.Width:0}x{size.Height:0}) kırpılan metin var:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, clipped));
    }

    /// <summary>
    /// <c>DropZonePadding</c> aralık ölçeğindeki <c>SpaceMd</c> ile aynı sayıyı taşıyor.
    /// Avalonia biçimlemesinde bir <c>Thickness</c> belirtecini bir <c>x:Double</c>
    /// belirtecinden türetmenin yolu yok — <c>{StaticResource SpaceMd}</c> bir
    /// <c>Thickness</c> özelliğine verildiğinde çalışma zamanında
    /// <c>InvalidCastException</c> ile düşüyor. Bağ o yüzden burada kuruluyor: ikisi
    /// ayrışırsa ölçüm kırmızıya düşer, sessizce geçmez.
    /// </summary>
    [Fact]
    public void TheDropZoneInsetStaysOnTheSpacingScale()
    {
        var inset = AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.TryFindResource("DropZonePadding", out var padding);
            return (Thickness)padding!;
        });

        Assert.Equal(new Thickness(Token("SpaceMd")), inset);
    }
}
