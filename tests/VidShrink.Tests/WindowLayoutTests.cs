using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
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
    /// Pencerenin istediği boyutta sayfa kaymıyor — dosya yüklüyken de. Dolu hâlin boyunu
    /// tutan şey sol ayar sütunu (T46/K7); o sütun uzarsa bu ölçüm kırmızıya düşer.
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
    /// dolu sayfa kaymayacak.
    ///
    /// <para>Ekran daha küçükse kayma <b>kabul edilir, zorunlu değil</b>. Zorunlu
    /// kılınırsa ölçüm ters döner: 1920x1080 + görev çubuğu makinesinin çalışma alanı
    /// 1920x1032'dir ve yükseklik tasarım boyutunun (1060) altında kalır, ama dolu sayfa
    /// o genişlikte zaten sığar — yani sayfa doğru davrandığı için ölçüm kırmızıya
    /// düşerdi. Sığdığında geçmeli, sığmadığında da tek bir dış taşıyıcıdan fazlası
    /// kaymamalı.</para>
    ///
    /// <para>Pencerenin tabanı burada değil: <c>MinWidth</c>/<c>MinHeight</c> 1040x720,
    /// tasarım boyutu yalnız tercih edilen boyut. İkisinin arasındaki her ekranda pencere
    /// tabanının üstündedir.</para>
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
            overflowing.Count <= 1,
            $"Ekranın çalışma alanı ({size.Width:0}x{size.Height:0}) tasarım boyutunun "
            + $"({design.Width:0}x{design.Height:0}) altında; en çok tek bir dış kaydırma "
            + "kabul ediliyor. " + Describe(size, loaded: true, overflowing));
    }

    /// <summary>
    /// Biçimlemedeki açılış boyutu ile belirteç aynı sayıyı iki yerde tutuyor; ölçüm
    /// yalnız belirteci okuduğu için ayrışma sessiz kalırdı.
    /// </summary>
    [Fact]
    public void TheWindowAsksForTheSizeItsTokensName()
    {
        var markup = File.ReadAllText(TipSources.WindowXamlPath);
        var declared = Regex.Match(markup, @"Width=""(?<w>\d+)""\s+Height=""(?<h>\d+)""");
        Assert.True(declared.Success, "Biçimlemede açılış boyutu bulunamadı.");

        var design = DesignSize();

        Assert.Equal(design.Width, double.Parse(declared.Groups["w"].Value, CultureInfo.InvariantCulture));
        Assert.Equal(design.Height, double.Parse(declared.Groups["h"].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>Sayfanın kendi kaydırıcısı.</summary>
    private static ScrollViewer Page(MainWindow window) =>
        window.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.Name == "PageShrink");

    /// <summary>
    /// Sayfa içeriğinin kendi boyu, pinli. Yerleşim şiştiğinde ölçüm kırmızıya düşer ve
    /// yeniden konuşulur. Aralık dar tutulmadı, panel yükseklikleri kullanılabilir
    /// kodlayıcı kümesine göre makineden makineye oynuyor.
    ///
    /// <para>T51: bu ölçümün önceki hâli içeriği <b>görüş alanıyla</b> karşılaştırıyordu
    /// ve sığdığı durumda bile kırmızı veriyordu. Sebebi kurgu hatası:
    /// <see cref="ScrollViewer"/> içeriği sığdığında görüş alanı içeriğin boyuna eşitlenir,
    /// dolayısıyla <c>içerik &lt; görüş alanı</c> hiçbir zaman doğru olamaz. Sığma iddiası
    /// taşmayı ölçen <see cref="ThePageDoesNotScrollAtTheDesignSize"/> ve
    /// <see cref="ThePageStopsScrollingAtThisHeight"/> ölçümlerine bırakıldı; burada
    /// yalnız içeriğin boyu pinleniyor. <b>Aralıklar değiştirilmedi</b> — dört ölçülen
    /// değer de zaten eski aralıkların içindeydi.</para>
    /// </summary>
    [Theory]
    [InlineData(false, false, 810, 910)]
    [InlineData(false, true, 810, 910)]
    [InlineData(true, false, 780, 880)]
    [InlineData(true, true, 815, 915)]
    public void ThePageContentStaysAtItsPinnedHeight(bool loaded, bool narrow, double least, double most)
    {
        var size = narrow ? MinimumSize() : DesignSize();
        var content = Read(size, loaded, window => ((Control)Page(window).Content!).DesiredSize.Height);

        Assert.InRange(content, least, most);
    }

    /// <summary>
    /// Ölçüm düzeneği <see cref="Layoutable.Measure"/>'a verilen yüksekliği gerçekten
    /// yerleşime taşıyor mu — T51/K1'in nöbetçisi.
    ///
    /// <para>Pencerenin kendi <see cref="Layoutable.Height"/> değeri temizlenmezse
    /// <c>ApplyLayoutConstraints</c> onu ölçüm argümanının yerine koyar ve <b>her</b>
    /// yükseklik aynı yerleşimi ölçer. O hâlde ölçüm sessizce sabit bir sayı üretir;
    /// T46 tam bunu yaşadı ve "yükseklik yerleşime ulaşmıyor" sonucuna vardı. Argüman
    /// yutulursa aşağıdaki iki görüş alanı eşitlenir ve ölçüm kırmızıya düşer.</para>
    /// </summary>
    [Fact]
    public void TheMeasurementRigCarriesTheHeightItIsGiven()
    {
        var width = DesignSize().Width;
        var atFloor = Read(new Size(width, MinimumSize().Height), loaded: false, w => Page(w).Viewport.Height);
        var atDesign = Read(new Size(width, DesignSize().Height), loaded: false, w => Page(w).Viewport.Height);

        Assert.True(
            atDesign > atFloor + 0.5,
            $"Verilen yükseklik yerleşime ulaşmıyor: taban {atFloor:0.#}, tasarım {atDesign:0.#} "
            + "— ikisi aynıysa argüman pencerenin kendi Height değerine yutuluyor.");
    }

    /// <summary>
    /// Sayfanın dikey kaymayı bıraktığı yükseklik, tasarım genişliğinde. Sayı pinli.
    ///
    /// <para>T46 bu taramayı "bu koşumda ölçülemiyor" diyerek kaldırmıştı; ölçülemeyen
    /// şey tarama değil, argümanı yutulmuş bir düzenekti (bkz.
    /// <see cref="TheMeasurementRigCarriesTheHeightItIsGiven"/>). Düzenek argümanı
    /// taşıdığında tarama anlamlı: boş sayfa 953, dolu sayfa 921 pikselde sığıyor —
    /// ikisi de kendi içerik boyuna pencere süsünün 95 pikselini eklemekle aynı sayı.</para>
    /// </summary>
    [Theory]
    [InlineData(false, 910, 1000)]
    [InlineData(true, 880, 970)]
    public void ThePageStopsScrollingAtThisHeight(bool loaded, double least, double most)
    {
        var width = DesignSize().Width;
        var fitting = double.NaN;

        for (var height = 700.0; height <= 1600.0; height += 4)
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
    /// Dolu sayfanın boyunu ne tutuyor: <b>sol ayar sütunu</b>.
    ///
    /// <para>Bu ölçüm T46/K7'de yeniden kuruldu. Önceki hâli "sayfayı tutan şey plan
    /// paneli tavanıdır" diyordu ve K6 gerekçeleri katlayınca yanlış hale geldi — plan
    /// içeriği 681'den 278'e indi, tavana (512) artık dayanmıyor. Ölçülen yeni durum:
    /// sol sütun 802, orta sütun 676, sağ sütun 473. Sayfa içeriği (826) sol sütunun
    /// çalışma alanı kenar boşluğuyla toplamıdır.</para>
    ///
    /// <para>Yani sayfayı kısaltmak isteyen iş orta sütuna değil sol sütuna bakmalı;
    /// orta sütun 126 piksel geride.</para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheSettingsColumnIsWhatHoldsThePage(bool loaded)
    {
        var (columns, content) = Read(DesignSize(), loaded, window =>
        {
            var plan = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "PlanPanel");
            var grid = plan.GetVisualAncestors().OfType<Grid>().First(g => g.ColumnDefinitions.Count == 3);
            var heights = grid.Children.OfType<Control>()
                .OrderBy(Grid.GetColumn)
                .Select(c => c.DesiredSize.Height)
                .ToList();

            var page = window.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.Name == "PageShrink");
            return ((IReadOnlyList<double>)heights, ((Control)page.Content!).DesiredSize.Height);
        });

        Assert.Equal(3, columns.Count);
        Assert.True(
            columns[0] > columns[1] && columns[0] > columns[2],
            $"Sayfayı tutan sütun değişmiş: sol {columns[0]:0.#}, orta {columns[1]:0.#}, sağ {columns[2]:0.#}.");

        // Sayfa içeriği en uzun sütun ile çalışma alanı kenar boşluğunun toplamı.
        var margin = Read(DesignSize(), loaded, window =>
            window.TryFindResource("WorkspaceMargin", out var value) && value is Thickness pad
                ? pad.Top + pad.Bottom
                : double.NaN);

        Assert.Equal(columns[0] + margin, content, 1);
    }

    /// <summary>
    /// Plan paneli tavanı devrede — <b>satırın izin verdiği yerde</b>.
    ///
    /// <para>T51/K3: ölçümün önceki hâli tavanın dolu sayfada da bağlayıcı olmasını
    /// bekliyordu ve 512 yerine 492 ölçtü. Yirmi pikselin nereye gittiği ölçüldü ve
    /// <b>rozetle ilgisi yok</b>: önizleme bloğu iki durumda da 294 piksel. Fark sol ayar
    /// sütununda — bırakma alanı dosya yüklenince kalktığı için sol sütun 834'ten 802'ye
    /// iniyor. Üç sütunlu ızgara bütün sütunları en uzununa göre geriyor, dolayısıyla
    /// orta sütun da 32 piksel kısalıyor ve plan satırına panele bırakacak 518 değil 492
    /// piksel kalıyor. Tavan bağlayıcı olmaktan çıkıyor, paneli satırın kendisi tutuyor.</para>
    ///
    /// <para>Ölçüm bunu olduğu gibi söylüyor: panelin boyu her durumda <b>satırın bıraktığı
    /// yer ile tavanın küçüğü</b>. Tavanın kaldırıldığı bir kurulumda boş sayfadaki panel
    /// 518'e uzar ve ölçüm kırmızıya düşer — yani tavanın nöbeti duruyor.</para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThePlanPanelTakesTheSmallerOfTheCeilingAndItsRow(bool loaded)
    {
        var (ceiling, panelHeight, roomInRow, content) = PlanPanelLayout(loaded);

        Assert.Equal(Math.Min(roomInRow, ceiling), panelHeight, 1);
        Assert.True(
            content < panelHeight,
            $"Plan içeriği ({content:0.#}) panelin boyunu ({panelHeight:0.#}) aştı; taşma PlanScroll'a düşüyor.");
    }

    /// <summary>
    /// Tavan hiç bağlanmıyorsa gereksizdir. Boş sayfada satır panele tavandan fazla yer
    /// bırakıyor ve panel tam tavanda duruyor — tavanın devrede olduğunu söyleyen ölçüm bu.
    /// </summary>
    [Fact]
    public void ThePlanPanelCeilingIsEngagedWhereTheRowIsTallEnough()
    {
        var (ceiling, panelHeight, roomInRow, _) = PlanPanelLayout(loaded: false);

        Assert.True(
            roomInRow > ceiling,
            $"Satır panele tavandan ({ceiling:0}) az yer bırakıyor ({roomInRow:0.#}); tavan hiçbir "
            + "durumda devrede değil, paneli tutan şey satırın kendisi.");
        Assert.Equal(ceiling, panelHeight, 1);
    }

    private static (double Ceiling, double PanelHeight, double RoomInRow, double Content) PlanPanelLayout(bool loaded) =>
        Read(DesignSize(), loaded, window =>
        {
            var panel = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PlanPanel");
            var body = window.GetVisualDescendants().OfType<StackPanel>().Single(b => b.Name == "PlanBody");
            var column = panel.GetVisualAncestors().OfType<Grid>().First(g => g.RowDefinitions.Count == 2);
            window.TryFindResource("PlanPanelMaxHeight", out var value);

            // Satırın panele bıraktığı yer: sütunun boyundan panelin sütun içindeki
            // başlangıcı düşülür. Üstünde önizleme paneli ve başlığı duruyor.
            return ((double)value!, panel.Bounds.Height, column.Bounds.Height - panel.Bounds.Y, body.DesiredSize.Height);
        });

    /// <summary>
    /// Taban boyutta sayfa <b>aşağı</b> kayıyor ve bu doğru davranış; yana hiç kaymıyor.
    ///
    /// <para>T51/K2 kararı: <b>iddia yanlıştı, ürün değil.</b> Pencerenin tabanı 1040x720,
    /// yani sayfaya kalan görüş alanı 625 piksel. Sayfayı tutan sol ayar sütunu tek başına
    /// 802 piksel istiyor (bkz. <see cref="TheSettingsColumnIsWhatHoldsThePage"/>); içerik
    /// 953 pikselden kısa bir pencereye sığamaz. Sığmayan içeriği kaydırmak
    /// <see cref="ScrollViewer"/>'ın işi. T46 iddiayı "taban boyutta artık kaymıyor" diye
    /// tersine çevirirken yalnız <b>genişlik</b> eksenini taramıştı — yükseklik o
    /// düzenekte yerleşime ulaşmıyordu, dolayısıyla dikey eksen hiç ölçülmemişti.</para>
    ///
    /// <para>Ölçülen ve korunan iddia: taban boyutta tek bir dış kaydırma vardır, o da
    /// sayfanın kendi kaydırıcısıdır, ve <b>yatay taşma sıfırdır</b>. Yana kayma bir
    /// gerileme olurdu; aşağı kayma tasarımın kendisi.</para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheSmallestSizeScrollsDownAndNeverSideways(bool loaded)
    {
        var size = MinimumSize();
        var overflowing = LayOut(size, loaded);

        Assert.True(
            overflowing.Count == 1 && overflowing[0].Name == "PageShrink",
            $"Taban boyutta ({size.Width:0}x{size.Height:0}) yalnız sayfanın kendi kaydırıcısı "
            + "kaymalıydı. " + Describe(size, loaded, overflowing));

        Assert.Equal(0, overflowing[0].Horizontal, 1);
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
