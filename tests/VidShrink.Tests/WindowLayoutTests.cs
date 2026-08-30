using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.Core;
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _output;

    public WindowLayoutTests(ITestOutputHelper output) => _output = output;

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

    /// <summary>
    /// Yerleşimi <paramref name="size"/> görüş alanında koşturur — <b>ekrandan bağımsız</b>.
    ///
    /// <para>T59: pencerenin kendisine <see cref="Layoutable.Arrange"/> çağırmak boyutu
    /// taşımıyor. <c>Window.ArrangeSetBounds</c> verilen boyutu değil <c>ClientSize</c>'ı
    /// döndürür, yani pencerenin sınırları her zaman <b>platform penceresinden</b> gelir.
    /// Pencere <c>WindowState="Maximized"</c> açıldığı için o platform penceresi makinenin
    /// ekranına göre kuruluyor: bu makinede 1904x990, dolayısıyla hangi boyut verilirse
    /// verilsin sayfanın görüş alanı 895 ölçülüyordu. Ölçüm argümanı geçiyor (pencerenin
    /// istediği boyut doğru çıkıyor), yutulan şey <b>yerleştirme</b>.</para>
    ///
    /// <para>Bu yüzden yerleşim pencereye değil pencerenin kök görsel çocuğuna verilir.
    /// O kök, pencere süsünü de içeren taşıyıcıdır — sayfanın görüş alanı verilen
    /// yükseklikten süsün 95 pikseli düşülmüş hâlidir ve üç çözünürlükte de aynı sayı
    /// çıkar. İlk pencere geçişi yalnız biçimin uygulanması için var; kök ondan sonra
    /// istenen boyutla yeniden ölçülüp yerleştiriliyor.</para>
    ///
    /// <para>Kök geçişinden <b>sonra</b> <see cref="TopLevel.UpdateLayout"/> çağrılmamalı;
    /// pencerenin kendi geçişi yerleşimi yine <c>ClientSize</c>'a döndürür.</para>
    /// </summary>
    private static void LayOutAt(MainWindow window, Size size)
    {
        // Pencerenin kendi Width/Height degerleri ApplyLayoutConstraints tarafindan olcum
        // argumaninin yerine konur; temizlenmezse pencere her boyutta ayni seyi olcer.
        window.Width = double.NaN;
        window.Height = double.NaN;

        window.Measure(size);
        window.Arrange(new Rect(size));
        window.UpdateLayout();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.Measure(size);
        root.Arrange(new Rect(size));

        ClearEntranceTransforms(window);
    }

    /// <summary>
    /// Giriş canlandırması panellere <c>translateY</c> uyguluyor ve <c>PlayPanelEntrance</c>
    /// başsız koşumda hiç çalışmadığı için o dönüşüm asla geri alınmıyordu:
    /// <see cref="Visual.TranslatePoint"/> her bloğu on piksel aşağıda gösteriyordu.
    /// Ölçüm bunu ortamdan devralmıyor, kendisi siliyor.
    /// </summary>
    private static void ClearEntranceTransforms(MainWindow window)
    {
        foreach (var node in window.GetVisualDescendants().OfType<Visual>())
            node.RenderTransform = null;
    }

    /// <summary>
    /// Sekme seçimi değiştikten sonraki geçiş. <see cref="LayOutAt"/> burada yetmiyor:
    /// yeni sekmenin içeriği ilk kez ölçülüyor ve <see cref="TopLevel.UpdateLayout"/> onu
    /// pencerenin kendi <c>ClientSize</c>'ı ile — başsız koşumda sıfır — ölçüp temiz
    /// işaretliyor; ardından gelen kök geçişi artık ona uğramıyor. Sekme ağaçta durur ama
    /// blok sınırları sıfır kalır ve sınıra bakan her süzgeç sekmeyi sessizce eler.
    /// Bu geçiş o yüzden pencereyi hiç sürmez: ağacın tamamı geçersizleştirilip yalnız
    /// kök ölçülür. <see cref="PerformanceCheckUiTests"/> aynı yolu kullanıyor.
    /// </summary>
    private static void RelayoutAt(MainWindow window, Size size)
    {
        foreach (var node in window.GetVisualDescendants().OfType<Layoutable>()) node.InvalidateMeasure();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.InvalidateMeasure();
        root.Measure(size);
        root.Arrange(new Rect(size));
    }

    /// <summary>Bir sekmenin ölçülmüş metin blokları; kırpılanlar ayrı sayılır.</summary>
    private readonly record struct TabScan(
        string Header,
        int Measured,
        int InPerformancePanel,
        IReadOnlyList<string> Clipped);

    /// <summary>
    /// Avalonia seçili olmayan sekmenin içeriğini görsel ağaca almıyor, dolayısıyla tek bir
    /// ölçüm yalnız açık sekmeyi görür. Bu yürüyüş her sekmeyi sırayla seçip
    /// <paramref name="size"/> görüş alanında yeniden ölçer. Kırpma ölçütü
    /// <see cref="Clips"/> içinde.
    /// </summary>
    private static List<TabScan> ScanTabs(MainWindow window, Size size)
    {
        LayOutAt(window, size);

        var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
        var scans = new List<TabScan>();

        for (var index = 0; index < tabs.ItemCount; index++)
        {
            tabs.SelectedIndex = index;
            RelayoutAt(window, size);

            var header = (tabs.ContainerFromIndex(index) as TabItem)?.Header?.ToString() ?? $"{index}";

            var blocks = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible && block.Bounds.Width > 0)
                .ToList();

            scans.Add(new TabScan(
                header,
                blocks.Count,
                blocks.Count(InPerformancePanel),
                blocks
                    .Where(Clips)
                    .Select(block => $"{header}: {(block.Text ?? "(boş)").Split('\n')[0]} :: {Trace(block)}")
                    .ToList()));
        }

        return scans;
    }

    /// <summary>
    /// Bir metin bloğu, ait olduğu kaydırma taşıyıcısının görüş alanının dışına taşıyorsa
    /// kırpılmıştır. Sayfa taşıyıcılarında yatay kaydırma kapalı; görüş alanının dışında
    /// kalan piksel kaydırılarak da getirilemez, kesilir.
    ///
    /// <para>Ölçüt bloğun sağ kenarını taşıyıcının uzayına çevirip
    /// <see cref="ScrollViewer.Viewport"/> genişliğiyle karşılaştırıyor. Bunun yerine
    /// <see cref="Layoutable.DesiredSize"/> ile <see cref="Visual.Bounds"/> genişliğini
    /// karşılaştırmak <b>işe yaramaz</b> ve bir daha denenmemeli: Avalonia bir denetimi
    /// istediğinden dar yerleştirmiyor, <c>Bounds</c> en az <c>DesiredSize</c> kadar
    /// oluyor; sarmayan uzun metnin isteği de ölçüm sırasında verilen genişliğe kırpılıyor.
    /// İki sayı kasten taşırılmış metinde bile eşit çıkar, ölçü hep yeşil kalır. T65'te
    /// ölçüldü, ayrıntı raporda.</para>
    /// </summary>
    private static bool Clips(TextBlock block)
    {
        if (block.FindAncestorOfType<ScrollViewer>() is not { } viewer) return false;
        if (block.TranslatePoint(new Point(block.Bounds.Width, 0), viewer) is not { } right) return false;

        return right.X > viewer.Viewport.Width + 0.5;
    }

    /// <summary>
    /// Kırpılan bloğun nerede taştığını gösterir: sağ kenarı, taşıyıcının görüş alanı ve
    /// bloktan taşıyıcıya kadar olan denetim zinciri genişlikleriyle. Kırmızı bir ölçüm
    /// bu satır olmadan hangi kutunun taştığını söylemiyor.
    /// </summary>
    private static string Trace(TextBlock block)
    {
        var viewer = block.FindAncestorOfType<ScrollViewer>()!;
        var right = block.TranslatePoint(new Point(block.Bounds.Width, 0), viewer)!.Value;
        var chain = block.GetVisualAncestors().OfType<Control>().TakeWhile(node => node != viewer)
            .Select(node => $"{node.GetType().Name}{(string.IsNullOrEmpty(node.Name) ? string.Empty : "#" + node.Name)}[{node.Bounds.Width:0}]");

        return $"sağ kenar {right.X:0.#}, görüş alanı {viewer.Viewport.Width:0.#} | "
             + string.Join(" < ", chain);
    }

    private static bool InPerformancePanel(TextBlock block)
        => block.GetVisualAncestors().OfType<Border>().Any(border => border.Name == "PerformancePanel");

    private static T Read<T>(Size size, bool loaded, Func<MainWindow, T> read) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.UseTurkish();

            if (loaded)
            {
                window.LoadWithoutProbing(SamplePath, Sample());

                // Bırakma alanını gizleme kararını 240 ms sonra bir DispatcherTimer
                // veriyor; pencere gösterilmediği için o zamanlayıcı ateşlenmiyor. Ölçüm
                // geçişin oturmuş halini istiyor — kullanıcının yarım saniye sonra
                // gördüğü yerleşim bu.
                window.SettleFades();
            }

            LayOutAt(window, size);

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

    private static string Describe(Size size, bool loaded, IEnumerable<Overflow> overflowing) =>
        $"{(loaded ? "Dolu" : "Boş")} pencerede {size.Width:0}x{size.Height:0}: "
        + string.Join(", ", overflowing);

    /// <summary>
    /// Pencerenin istediği boyutta <b>dolu</b> sayfa hiç kaymıyor; <b>boş</b> sayfa yalnız
    /// aşağı kayabiliyor ve yana hiç kaymıyor.
    ///
    /// <para>T61'e kadar iki hâl de hiç kaymıyordu. Kalite hedefi denetimi sol ayar
    /// sütununa bir başlık ve bir kaydırıcı satırı ekledi; boş sayfada bunun üstüne
    /// "video yükleyin" satırı da geliyor (T61/K4, kaynak yokken kaliteden MB
    /// türetilemez). Ölçülen sonuç: sol sütun boş sayfada 834'ten 973'e, dolu sayfada
    /// 802'den 904'e çıktı. Dolu sayfa hâlâ orta sütunun (932) boyunda ve sığıyor; boş
    /// sayfanın içeriği 997, tasarım boyutunun görüş alanı 965, yani 32 piksel taşıyor.</para>
    ///
    /// <para><b>Bu ayrım neyi koruyor:</b> yatay ekseni ve dolu hâli. Yana kayma her iki
    /// hâlde de gerileme sayılır, dolu sayfanın kayması da öyle — çalışılan hâl budur ve
    /// tasarım boyutunda tam oturmalıdır. <b>Bozulursa kullanıcı ne görür:</b> dolu hâl
    /// kırılırsa video yüklüyken ayar sütununun sonunu görmek için tekerlek çevirmek
    /// gerekir; yatay eksen kırılırsa sayfa yana kayar ve sağ sütunun bir bölümü ekran
    /// dışında kalır. Boş hâlin aşağı kayması kabul edildi: kaynak yüklenir yüklenmez
    /// yönlendirme satırı kalkar ve sayfa kendiliğinden sığar.</para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThePageScrollsAtMostDownAtTheDesignSize(bool loaded)
    {
        var size = DesignSize();
        var overflowing = LayOut(size, loaded);

        if (loaded)
        {
            Assert.True(overflowing.Count == 0, Describe(size, loaded, overflowing));
            return;
        }

        Assert.True(
            overflowing.Count <= 1 && overflowing.All(entry => entry.Name == "PageShrink"),
            "Boş sayfada tasarım boyutunda yalnız sayfanın kendi kaydırıcısı kayabilir. "
            + Describe(size, loaded, overflowing));

        Assert.All(overflowing, entry => Assert.Equal(0, entry.Horizontal, 1));
    }

    /// <summary>
    /// Açılış boyutu — pencere <c>WindowState="Maximized"</c> açıldığı için ekranın
    /// çalışma alanı. Tasarım boyutunu karşılayan bir alanda dolu sayfa kaymayacak.
    ///
    /// <para>Alan daha küçükse kayma <b>kabul edilir, zorunlu değil</b>: 1920x1080 +
    /// görev çubuğu makinesinin çalışma alanı 1920x1032'dir, yükseklik tasarım boyutunun
    /// (1060) altında kalır ama dolu sayfa o genişlikte zaten sığar — zorunlu kılınsa
    /// sayfa doğru davrandığı için ölçüm kırmızıya düşerdi. Sığdığında geçmeli,
    /// sığmadığında da tek bir dış taşıyıcıdan fazlası kaymamalı.</para>
    ///
    /// <para>T59: bu ölçümün önceki hâli alanı <see cref="Screens"/>'den, yani <b>ölçümü
    /// koşturan makinenin ekranından</b> alıyordu. Aynı commit farklı çözünürlükte farklı
    /// karar veriyordu; verdiği yeşil de kırmızı da kanıt değildi. Alanlar artık burada
    /// yazılı: bugünün ekranı, yaygın 1080p çalışma alanı ve tasarım boyutu. Hiçbiri
    /// çalıştığı makineyi okumuyor.</para>
    ///
    /// <para>Pencerenin tabanı burada değil: <c>MinWidth</c>/<c>MinHeight</c> 1040x720,
    /// tasarım boyutu yalnız tercih edilen boyut. İkisinin arasındaki her alanda pencere
    /// tabanının üstündedir.</para>
    /// </summary>
    [Theory]
    [InlineData(2560, 1400)]
    [InlineData(1920, 1032)]
    [InlineData(1600, 900)]
    public void TheLoadedPageDoesNotScrollAtTheStartingSize(double width, double height)
    {
        var size = new Size(width, height);
        var design = DesignSize();
        var overflowing = LayOut(size, loaded: true);

        if (size.Width >= design.Width && size.Height >= design.Height)
        {
            Assert.True(overflowing.Count == 0, Describe(size, loaded: true, overflowing));
            return;
        }

        Assert.True(
            overflowing.Count <= 1,
            $"Açılış alanı ({size.Width:0}x{size.Height:0}) tasarım boyutunun "
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
    /// taşmayı ölçen <see cref="ThePageScrollsAtMostDownAtTheDesignSize"/> ve
    /// <see cref="ThePageStopsScrollingAtThisHeight"/> ölçümlerine bırakıldı; burada
    /// yalnız içeriğin boyu pinleniyor. <b>Aralıklar değiştirilmedi</b> — dört ölçülen
    /// değer de zaten eski aralıkların içindeydi.</para>
    ///
    /// <para>T52: dolu sayfanın iki aralığı yeniden temellendirildi. Oynatma panelinin
    /// taban boyu <c>PanelMinHeight</c>'ın iki katına çıktı (256 → 512), sayfa içeriği de
    /// onunla birlikte uzadı: tasarım boyunda 830 civarından 956'ya, taban boyunda 975'e.
    /// Boş sayfanın iki aralığı <b>değişmedi</b> — orada sayfayı hâlâ sol ayar sütunu
    /// tutuyor, oynatma paneli onun altında kalıyor.
    /// <para>T83: ölçüm bugüne kadar <b>İngilizce</b> pencereyi ölçüyordu. Uygulama Türkçe
    /// açılıyor ama <c>OnWindowLoaded</c> başsız koşumda hiç ateşlenmediği için dil hiç
    /// değişmiyordu; pinler kullanıcının görmediği bir yerleşimden geliyordu. Düzenek artık
    /// pencereyi Türkçeye alıyor ve dört sayı yeniden temellendi: boş/tasarım 947-1047 →
    /// 939-1039 (ölçülen 989), boş/taban 965-1065 → 960-1060 (1010), dolu/tasarım 906-1006
    /// (956) değişmedi, dolu/taban 925-1025 → 1002-1102 (1052). En büyük kayma dolu/taban:
    /// Türkçe karşılıklar dar pencerede daha çok satıra sarıyor.</para>
    ///
    /// <b>Bu sayı neyi koruyor:</b> sayfanın kendi boyunu. Bir daha şişerse ölçüm kırmızıya
    /// düşer ve yeniden konuşulur. <b>Bozulursa kullanıcı ne görür:</b> sayfa uzar, kısa
    /// pencerelerde dikey kaydırma çubuğu daha erken çıkar.</para>
    /// </summary>
    [Theory]
    [InlineData(false, false, 939, 1039)]
    [InlineData(false, true, 960, 1060)]
    [InlineData(true, false, 906, 1006)]
    [InlineData(true, true, 1002, 1102)]
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
    /// <para>Yükseklik iki yerde yutulabiliyor. Pencerenin kendi
    /// <see cref="Layoutable.Height"/> değeri temizlenmezse <c>ApplyLayoutConstraints</c>
    /// onu ölçüm argümanının yerine koyar. İkincisi T59'da ölçüldü:
    /// <c>Window.ArrangeSetBounds</c> verilen boyutu değil <c>ClientSize</c>'ı döndürdüğü
    /// için pencereye yapılan yerleştirme <b>hiçbir</b> boyutu taşımaz ve yerleşim
    /// platform penceresinin — yani ekranın — boyunda kalır. İkisinden biri devredeyse
    /// aşağıdaki üç görüş alanı aynı sayıyı verir ve ölçüm kırmızıya düşer.</para>
    ///
    /// <para>Üç yükseklik kasten farklı çözünürlük sınıfından: pencerenin kendi tabanı,
    /// tasarım boyu ve bugünün 2560x1440 ekranının çalışma alanı. Sayı hep aynı kuralla
    /// çıkıyor: görüş alanı = verilen yükseklik − pencere süsünün 95 pikseli.</para>
    /// </summary>
    [Fact]
    public void TheMeasurementRigCarriesTheHeightItIsGiven()
    {
        var width = DesignSize().Width;
        var heights = new[] { MinimumSize().Height, DesignSize().Height, 1400.0 };
        var viewports = heights
            .Select(height => Read(new Size(width, height), loaded: false, w => Page(w).Viewport.Height))
            .ToList();

        var trail = string.Join(", ", heights.Zip(viewports, (h, v) => $"{h:0}→{v:0.#}"));

        for (var index = 1; index < viewports.Count; index++)
            Assert.True(
                viewports[index] > viewports[index - 1] + 0.5,
                $"Verilen yükseklik yerleşime ulaşmıyor ({trail}) — ardışık ikisi aynıysa "
                + "argüman ya pencerenin kendi Height değerine ya da ClientSize'a yutuluyor.");

        var chrome = heights.Zip(viewports, (h, v) => h - v).ToList();
        Assert.All(chrome, gap => Assert.Equal(chrome[0], gap, 1));
    }

    /// <summary>
    /// Sayfanın dikey kaymayı bıraktığı yükseklik, tasarım genişliğinde. Sayı pinli.
    ///
    /// <para>T46 bu taramayı "bu koşumda ölçülemiyor" diyerek kaldırmıştı; ölçülemeyen
    /// şey tarama değil, argümanı yutulmuş bir düzenekti (bkz.
    /// <see cref="TheMeasurementRigCarriesTheHeightItIsGiven"/>). Düzenek argümanı
    /// taşıdığında tarama anlamlı: boş sayfa 953, dolu sayfa 921 pikselde sığıyor —
    /// ikisi de kendi içerik boyuna pencere süsünün 95 pikselini eklemekle aynı sayı.</para>
    ///
    /// <para>T52: iki eşik de yükseldi — boş sayfa 1008, dolu sayfa 1052. Sebep oynatma
    /// panelinin taban boyunun iki katına çıkması; kural aynı kaldı, sığma yüksekliği yine
    /// içerik boyu artı pencere süsünün 95 pikseli. Aralıkların genişliği (±45)
    /// değiştirilmedi.</para>
    ///
    /// <para>T83: ölçüm Türkçe pencereye geçti. Boş sayfa 1047-1137 → 1039-1129 (ölçülen
    /// 1084), dolu sayfa 1007-1097 (1052) değişmedi.
    /// <b>Bu sayı neyi koruyor:</b> pencerenin hangi boydan sonra kaymayı
    /// bıraktığını. <b>Bozulursa kullanıcı ne görür:</b> daha uzun pencerelerde bile
    /// kaydırma çubuğu kalır — 1052'nin üstüne çıkan her yeni piksel, dizüstü ekranlarda
    /// sayfanın tamamının bir bakışta görünmemesi demek.</para>
    /// </summary>
    [Theory]
    [InlineData(false, 1039, 1129)]
    [InlineData(true, 1007, 1097)]
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
    /// Sayfanın boyunu ne tutuyor: <b>dolu sayfada orta sütun</b> (önizleme ve plan),
    /// <b>boş sayfada sol sütun</b> (ayarlar).
    ///
    /// <para>Bu ölçüm üç kez yer değiştirdi. T46/K7'de "plan paneli tavanı" iddiası düşüp
    /// yerini sol ayar sütununa bıraktı (sol 802, orta 676, sağ 473). T52'de oynatma
    /// panelinin taban boyu ikiye katlanınca (256 → 512) orta sütun sol sütunu geçti:
    /// dolu sayfada sol 802 / orta 932 / sağ 473, boş sayfada sol 834 / orta 886 / sağ
    /// 437. T61 kalite hedefi denetimini sol sütuna ekleyince tabela ikiye ayrıldı: dolu
    /// sayfada sol 904 / orta 932 / sağ 473 — orta hâlâ önde, ama payı 130'dan 28 piksele
    /// indi; boş sayfada sol 973 / orta 886 / sağ 437 — sol sütun öne geçti, çünkü kaynak
    /// yokken kalite denetiminin altına yönlendirme satırı çıkıyor (T61/K4).</para>
    ///
    /// <para>T74/K1 "Preview" başlığını ve onu taşıyan satırı orta sütundan kaldırdı; orta
    /// sütun 932'den 882'ye indi ve dolu sayfada sol sütun (904) öne geçti. Tabela bu yüzden
    /// iki hâlde de <b>sol sütunu</b> gösteriyor.</para>
    ///
    /// <para>T83 ölçümü Türkçe pencereye taşıdı; tutan sütun değişmedi. Türkçe ölçülen
    /// sütun boyları: boş sayfa sol 973 / orta 844 / sağ 476, dolu sayfa sol 940 /
    /// orta 882 / sağ 512.</para>
    ///
    /// <para>Yani sayfayı kısaltmak isteyen iş hangi hâli kısaltmak istediğine bakmalı.
    /// Korunan ilişki değişmedi, yalnız doğru adıyla yazıldı: sayfa içeriği <b>en uzun sütun
    /// ile sekme şeridinden düşen <c>SectionMargin</c> boşluğunun toplamıdır</b>. Eskiden
    /// burada <c>WorkspaceMargin</c> okunuyordu; iki belirteç aynı sayıyı (24) taşıdığı için
    /// eşitlik tutuyordu, T74/K5 <c>SectionMargin</c>'i 16'ya indirince ayrıştılar.</para>
    ///
    /// <para><b>Bu sayı neyi koruyor:</b> sayfayı hangi sütunun gerdiğini — kısaltma işi
    /// yanlış sütuna harcanmasın diye. <b>Bozulursa kullanıcı ne görür:</b> doğrudan bir
    /// şey görmez; bu bir yön tabelasıdır, yanlış olduğunda sonraki düzen işi boşa gider.
    /// Tutan sütun değişirse ölçüm kırmızıya düşer ve tabela yenilenir.</para>
    /// </summary>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    public void TheTallestColumnIsWhatHoldsThePage(bool loaded, int holder)
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
            columns.ToList().IndexOf(columns.Max()) == holder,
            $"{(loaded ? "Dolu" : "Boş")} sayfayı tutan sütun değişmiş: sol {columns[0]:0.#}, "
            + $"orta {columns[1]:0.#}, sağ {columns[2]:0.#}; beklenen {holder}. numarali sutun.");

        // Sayfa içeriği en uzun sütun ile sekme boşluğunun toplamı.
        var margin = Read(DesignSize(), loaded, window =>
            window.TryFindResource("SectionMargin", out var value) && value is Thickness pad
                ? pad.Top + pad.Bottom
                : double.NaN);

        Assert.Equal(columns.Max() + margin, content, 1);
    }

    /// <summary>
    /// Plan paneli <b>içeriği kadar</b>; tabanı ile tavanı arasında.
    ///
    /// <para>T54/K4 düzeni bunu tersine çevirdi. Eskiden orta sütunun esneyen satırı plan
    /// panelindeydi: panel satırın bıraktığı yere geriliyor, önizleme <c>Auto</c> satırda
    /// yalnız istediğini alıyordu ve ikisinin arasında kimseye gitmeyen boşluk kalıyordu.
    /// Bugün esneyen satır önizlemenin, plan paneli <c>Auto</c> satırda ve boyu kendi
    /// içeriğinden geliyor.</para>
    ///
    /// <para>Ölçülen (tasarım boyutunda): boş sayfada panel 320 — içerik 31 piksel, paneli
    /// tutan şey <c>PlanPanelMinHeight</c>. Dolu sayfada panel 366, içerik 278; taban da
    /// tavan da bağlamıyor, boyu içerik veriyor. İki durumda da içerik panele sığıyor,
    /// <c>PlanScroll</c> kaymıyor.</para>
    ///
    /// <para><b>Bu ölçü neyi koruyor:</b> panelin artan yeri yutmamasını. <b>Bozulursa
    /// kullanıcı ne görür:</b> panel yeniden gerilirse önizleme <c>Auto</c> satıra düşer,
    /// küçük kalır ve plan panelinin altında kullanılmayan boşluk belirir — kullanıcının
    /// şikâyeti buydu.</para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThePlanPanelIsSizedByItsContentBetweenItsFloorAndItsCeiling(bool loaded)
    {
        var layout = PlanPanelLayout(loaded, reasons: false);

        Assert.InRange(layout.PanelHeight, layout.Floor, layout.Ceiling);
        Assert.True(
            layout.ScrollExtent <= layout.ScrollViewport + 0.5,
            $"Plan içeriği ({layout.ScrollExtent:0.#}) görüş alanını ({layout.ScrollViewport:0.#}) aştı; "
            + "katlı hâlde taşma beklenmiyor.");

        if (!loaded)
        {
            // İçerik 31 piksel; paneli tutan tek şey taban.
            Assert.Equal(layout.Floor, layout.PanelHeight, 1);
            return;
        }

        Assert.True(
            layout.PanelHeight > layout.Floor && layout.PanelHeight < layout.Ceiling,
            $"Dolu sayfada panel {layout.PanelHeight:0.#}; taban {layout.Floor:0} ile tavan "
            + $"{layout.Ceiling:0} arasında, içeriğinin belirlediği bir boy bekleniyordu. Panel "
            + "tavana oturuyorsa satır yeniden geriliyor demektir.");
    }

    /// <summary>
    /// Uzun plan tavanda durur ve taşmayı kendi kaydırıcısına verir.
    ///
    /// <para>T52 <c>PlanPanelMaxHeight</c>'ın hiçbir yerleşimde bağlamadığını ölçtü ve
    /// belirtecin ölü kalıp kalmayacağı kararını orta sütun düzenine (T54) bıraktı. Karar:
    /// <b>belirteç kalıyor ve bu ölçüyle nöbete bağlanıyor.</b> Yeni düzende plan paneli
    /// içeriği kadar uzadığı için tavan gerçek bir iş yapıyor — gerekçeler açıldığında
    /// içerik 738 piksel istiyor, panel 512'de duruyor ve fark <c>PlanScroll</c>'a düşüyor.
    /// Tavan olmasaydı panel yedi yüz pikselin üstüne çıkar, esneyen satırdaki önizlemeyi
    /// kendi tabanının altına iterdi.</para>
    ///
    /// <para><b>Bu ölçü neyi koruyor:</b> uzun planın önizlemeyi ezmemesini.
    /// <b>Bozulursa kullanıcı ne görür:</b> gerekçeleri açtığı anda önizleme küçülür ya da
    /// sayfa kaymaya başlar; ikisi de bugün olmuyor.</para>
    /// </summary>
    [Fact]
    public void TheCeilingStopsALongPlanAndHandsTheOverflowToItsOwnScroller()
    {
        var layout = PlanPanelLayout(loaded: true, reasons: true);

        Assert.Equal(layout.Ceiling, layout.PanelHeight, 1);
        Assert.True(
            layout.ScrollExtent > layout.ScrollViewport + 0.5,
            $"Gerekçeler açıkken içerik ({layout.ScrollExtent:0.#}) görüş alanına "
            + $"({layout.ScrollViewport:0.#}) sığdı; tavan bu hâlde de bağlamıyorsa ölçü bir şey "
            + "korumuyor demektir.");
        Assert.True(
            layout.PreviewHeight >= layout.PreviewFloor - 0.5,
            $"Önizleme {layout.PreviewHeight:0.#}; kendi tabanının ({layout.PreviewFloor:0}) altına "
            + "indi, yani uzun plan onu ezdi.");
    }

    private readonly record struct PlanLayout(
        double Floor,
        double Ceiling,
        double PanelHeight,
        double ScrollViewport,
        double ScrollExtent,
        double PreviewHeight,
        double PreviewFloor);

    /// <summary>
    /// Plan paneli ile önizlemenin tasarım boyutundaki ölçüleri. Hiçbir sayı buraya elle
    /// yazılmıyor; taban, tavan ve önizleme tabanı <c>Theme</c> belirteçlerinden okunuyor.
    /// </summary>
    private static PlanLayout PlanPanelLayout(bool loaded, bool reasons) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.UseTurkish();
            if (loaded)
            {
                window.LoadWithoutProbing(SamplePath, Sample());
                window.SettleFades();
            }

            if (reasons) window.ExpandPlanReasons();
            LayOutAt(window, DesignSize());

            var panel = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PlanPanel");
            var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.Name == "PlanScroll");
            var preview = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "Shell");

            window.TryFindResource("PlanPanelMinHeight", out var floor);
            window.TryFindResource("PlanPanelMaxHeight", out var ceiling);
            window.TryFindResource(loaded ? "PlaybackStageMinHeight" : "PlaybackIdleMinHeight", out var previewFloor);

            return new PlanLayout(
                (double)floor!,
                (double)ceiling!,
                panel.Bounds.Height,
                scroll.Viewport.Height,
                scroll.Extent.Height,
                preview.Bounds.Height,
                (double)previewFloor!);
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
    /// Taban boyutta hiçbir sekmede metin kırpılmıyor. Yürüyüş ve kırpma ölçütü
    /// <see cref="ScanTabs"/> içinde.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoTextIsClippedAtTheSmallestSize(bool loaded)
    {
        var size = MinimumSize();
        var scans = Scan(size, loaded);
        var clipped = scans.SelectMany(scan => scan.Clipped).ToList();

        _output.WriteLine(Counts(size, loaded, scans));

        Assert.True(
            clipped.Count == 0,
            $"En küçük boyutta ({size.Width:0}x{size.Height:0}) kırpılan metin var:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, clipped));
    }

    /// <summary>
    /// Kırpılma ölçüsünün gerçekten baktığı yer: her sekmede ölçülmüş blok var mı.
    ///
    /// <para>T65'te ölçüldü: sekme seçimi değiştikten sonra yeni sekmenin içeriği
    /// ölçülmeden kalıyordu, sınırları sıfır çıkıyordu ve
    /// <see cref="NoTextIsClippedAtTheSmallestSize"/> ilk sekme dışında hiçbir şeyi
    /// denetlemiyordu — süzgeç bütün blokları eliyordu. Sıfır blok gören bir sekme,
    /// kırpılma ölçüsünün o sekme için ölü olduğu anlamına gelir; bu ölçü onu yakalar.</para>
    ///
    /// <para>Gelişmiş sekmesindeki başarım paneli ayrıca sayılıyor: sekmenin toplamı
    /// sıfırdan büyükken panelin kendisi ağaca hiç girmemiş olabilir.</para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryTabIsMeasuredAtTheSmallestSize(bool loaded)
    {
        var size = MinimumSize();
        var scans = Scan(size, loaded);
        var report = Counts(size, loaded, scans);

        _output.WriteLine(report);

        Assert.All(scans, scan => Assert.True(scan.Measured > 0, $"{scan.Header} sekmesi hiç ölçülmedi. {report}"));

        var advanced = scans.Single(scan => scan.InPerformancePanel > 0);
        Assert.True(advanced.InPerformancePanel > 0, $"Başarım paneli ölçülmedi. {report}");
    }

    private static List<TabScan> Scan(Size size, bool loaded) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.UseTurkish();
            if (loaded)
            {
                window.LoadWithoutProbing(SamplePath, Sample());
                window.SettleFades();
            }

            return ScanTabs(window, size);
        });

    private static string Counts(Size size, bool loaded, IEnumerable<TabScan> scans) =>
        $"{(loaded ? "Dolu" : "Boş")} pencerede {size.Width:0}x{size.Height:0} ölçülen blok: "
        + string.Join(", ", scans.Select(scan =>
            $"{scan.Header}={scan.Measured}"
            + (scan.InPerformancePanel > 0 ? $" (başarım paneli {scan.InPerformancePanel})" : string.Empty)));

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


    /// <summary>
    /// Bir denetimin yerleşimdeki tepesi. <see cref="Visual.Bounds"/> yerleşim yuvasıdır ve
    /// çizim dönüşümü taşımaz; <see cref="Visual.TranslatePoint"/> burada <b>kullanılamaz</b>
    /// ve bir daha denenmemeli: <c>Panel</c> teması giriş sırasında panellere
    /// <c>translateY(10px)</c> uyguluyor (<c>^.enter</c>), sınıfı kaldıran
    /// <c>PlayPanelEntrance</c> ise başsız koşumda hiç çağrılmıyor. O yolla ölçülen tepe,
    /// giriş canlandırmasının yarısını hizasızlık diye rapor eder.
    /// </summary>
    private static double LayoutTop(Visual node, Visual root)
    {
        var top = 0.0;
        for (var walk = node; walk is not null && walk != root; walk = walk.GetVisualParent()) top += walk.Bounds.Y;
        return top;
    }

    /// <summary>
    /// T74/K1-K2: üç sütun da aynı tepeden başlar ve orta sütunda başlık satırı kalmamıştır.
    ///
    /// <para>Şikâyet buydu: "Source" ve "Target" başlıkları panelin <b>içinde</b>, "Preview"
    /// ise panelin <b>dışında</b> duruyordu, dolayısıyla orta sütunun paneli yan
    /// sütunlardakinden bir başlık boyu aşağıda başlıyordu. Başlık kalkınca orta sütunun
    /// satır sayısı da üçten ikiye indi; inmeseydi yerinde boş bir <c>Auto</c> satır
    /// kalırdı.</para>
    ///
    /// <para><b>Bu ölçü neyi koruyor:</b> üç panelin aynı yatay çizgiden başlamasını.
    /// <b>Bozulursa kullanıcı ne görür:</b> orta sütun yeniden aşağı kayar ve sayfanın
    /// üst kenarı basamaklanır.</para>
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TheThreeColumnsStartAtTheSameTop(bool loaded, bool narrow)
    {
        var size = narrow ? MinimumSize() : DesignSize();
        var (tops, rows) = Read(size, loaded, window =>
        {
            var plan = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "PlanPanel");
            var grid = plan.GetVisualAncestors().OfType<Grid>().First(g => g.ColumnDefinitions.Count == 3);
            var middle = (Grid)grid.Children.OfType<Control>().Single(c => Grid.GetColumn(c) == 1);

            var measured = new[] { "SourcePanel", "Shell", "OutputPanel" }
                .Select(name =>
                {
                    var node = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);
                    return (Name: name, Top: LayoutTop(node, grid));
                })
                .ToList();

            return ((IReadOnlyList<(string Name, double Top)>)measured, middle.RowDefinitions.Count);
        });

        Assert.Equal(2, rows);
        Assert.All(tops, entry => Assert.True(
            Math.Abs(entry.Top) < 0.5,
            $"{entry.Name} sütununun tepesi {entry.Top:0.##}; üç sütun da ızgaranın tepesinden "
            + "başlamalı. " + string.Join(", ", tops.Select(t => $"{t.Name}={t.Top:0.##}"))));
    }

    /// <summary>
    /// T74/K1: önizleme başlığı ekranda yok — ne İngilizcesi ne Türkçesi. İddia biçimleme
    /// dosyasında metin aramıyor, <b>ölçülmüş ağaçta</b> arıyor: kaynakta arayan bir ölçü
    /// başlığı anlatan bir yorum satırına takılır ve yanlış kırmızı verir.
    ///
    /// <para>Sözlükteki karşılık da gitti; kalsaydı ekranda olmayan bir başlığın çevirisi
    /// bakımda duruyor olurdu.</para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThePreviewHeadingIsGone(bool turkish)
    {
        var headings = AppHost.Run(() =>
        {
            var window = new MainWindow();
            if (turkish) window.UseTurkish();
            LayOutAt(window, DesignSize());

            return (IReadOnlyList<string>)window.GetVisualDescendants().OfType<TextBlock>()
                .Select(block => block.Text ?? string.Empty)
                .Where(text => text is "Preview" or "Önizleme")
                .ToList();
        });

        Assert.Empty(headings);
        Assert.DoesNotContain("Preview", Locales.Values("en").Values);
    }

    /// <summary>
    /// T74/K5: sekme şeridi ile panellerin arası <c>SectionMargin</c>'den geliyor ve o boşluk
    /// aralık ölçeğinin bir basamağıdır. 24 (<c>SpaceXl</c>) fazla bulundu, bir basamak
    /// inildi. Çıplak sayı yazılmasın diye ölçü basamağı belirteçten okuyor.
    /// </summary>
    [Fact]
    public void TheSectionInsetStaysOnTheSpacingScale()
    {
        var inset = AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.TryFindResource("SectionMargin", out var margin);
            return (Thickness)margin!;
        });

        Assert.Equal(new Thickness(0, Token("SpaceLg"), 0, 0), inset);
    }

    /// <summary>Bir kutunun yazısı: ekranda görünen metin, ölçüsü ve ona kalan yer.</summary>
    private readonly record struct BoxLabel(string Tab, string Control, string Text, double Needed, double Room)
    {
        internal double Overflow => Needed - Room;

        public override string ToString() =>
            $"{Tab} · {Control} [{Text}]: gereken {Needed:0.#}, kalan yer {Room:0.#}";
    }

    /// <summary>
    /// Bir metin bloğunun <b>gerçek yazı tipiyle</b> istediği genişlik. Yazı tipi, boyut,
    /// kalınlık ve yatıklık bloğun kendisinden okunuyor; ölçü hiçbir değeri varsaymıyor.
    ///
    /// <para><c>Contains</c> ile metin karşılaştırmak bu kusuru göremez: kırpılan metin
    /// ağaçta tam hâliyle durur, kesilen şey çizimdir. <see cref="Visual.Bounds"/> ile
    /// <see cref="Layoutable.DesiredSize"/> karşılaştırması da göremez (bkz.
    /// <see cref="Clips"/>) — ikisi de yerleşimin verdiği genişliğe kırpılır. Kırpılmayan tek
    /// sayı, metnin dizgiden çıkan kendi genişliğidir.</para>
    /// </summary>
    private static double NeededWidth(TextBlock face, string text) =>
        new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(face.FontFamily, face.FontStyle, face.FontWeight),
            face.FontSize,
            Brushes.Black).Width;

    /// <summary>
    /// Bir açılır kutunun yazısına kalan yer. Kutunun kendi genişliği değil: şablondaki
    /// ızgaranın metin sütunundan seçim kutusunun kenar boşluğu düşülmüş hâli. Hiçbir sayı
    /// buraya yazılmıyor — ok sütununun genişliği de dolgu da ağaçtan okunuyor.
    ///
    /// <para>Görünen metin bloğunun <see cref="Visual.Bounds"/> genişliği bu iş için
    /// <b>yetmez</b>: blok o anda yazılı metnin boyunda duruyor, bir seçenek daha uzun
    /// olduğunda ne olacağını söylemiyor.</para>
    /// </summary>
    private static double SelectionRoom(ComboBox box)
    {
        var frame = box.GetVisualDescendants().OfType<Grid>().FirstOrDefault(g => g.ColumnDefinitions.Count == 2);
        var selection = box.GetVisualDescendants().OfType<ContentControl>().FirstOrDefault(c => c.Name == "SelectionBox");
        if (frame is null || selection is null) return double.NaN;

        return frame.ColumnDefinitions[0].ActualWidth - selection.Margin.Left - selection.Margin.Right;
    }

    /// <summary>
    /// Türkçe pencerede her <see cref="ComboBox"/> ve <see cref="Button"/> yazısı kendi
    /// kutusuna sığar — <b>seçili olan da, kutunun her seçeneği de</b>.
    ///
    /// <para>Şikâyet doldurma politikası kutusundaydı: Türkçede "Hedefi Doldur" yazıyor,
    /// kullanıcı "Hedefi Do" görüyordu. Kutu ölçülebilir bir şey: seçim kutusu şablonda sola
    /// yaslı duruyor ve kırpma yapmıyor, yani metnin dizgiden çıkan genişliği ona kalan
    /// yerden büyükse kalanı çizilmez.</para>
    ///
    /// <para>Ölçü seçili seçenekle yetinmiyor: kullanıcı listeyi açıp en uzun seçeneği
    /// seçebilir; o zaman kutunun içeriği değişir, genişliği değişmez. Her seçenek aynı yere
    /// sığmak zorunda.</para>
    ///
    /// <para>Pencere <b>Türkçe</b> ölçülüyor. Uygulama açılışta Türkçe koşuyor
    /// (<c>OnWindowLoaded</c> içinde <c>SetLanguage(true)</c>); başsız ölçümde o olay
    /// ateşlenmediği için bu dosyadaki öteki ölçüler İngilizce pencereyi görüyor — kırpılma
    /// ise tam Türkçe karşılıkların uzunluğundan doğuyor.</para>
    ///
    /// <para><b>Bu ölçü neyi koruyor:</b> Türkçe metnin kutusuna sığmasını.
    /// <b>Bozulursa kullanıcı ne görür:</b> yarısı kesilmiş bir etiket — hangi seçeneğin
    /// açık olduğu okunamaz.</para>
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NoTurkishBoxLabelIsClipped(bool loaded, bool narrow)
    {
        var size = narrow ? MinimumSize() : DesignSize();
        var labels = MeasureTurkishBoxes(size, loaded);
        var clipped = labels.Where(label => label.Overflow > 0.5).ToList();

        _output.WriteLine(
            $"{(loaded ? "Dolu" : "Boş")} Türkçe pencerede {size.Width:0}x{size.Height:0} ölçülen "
            + $"kutu yazısı {labels.Count}; payı en dar üçü: "
            + string.Join(" | ", labels.OrderBy(label => label.Room - label.Needed).Take(3)));

        Assert.Contains(labels, label => label.Control.StartsWith("CmbFillPolicy", StringComparison.Ordinal));

        Assert.True(
            clipped.Count == 0,
            $"Türkçe pencerede ({size.Width:0}x{size.Height:0}) kutusuna sığmayan yazı var:"
            + Environment.NewLine + string.Join(Environment.NewLine, clipped));
    }

    /// <summary>
    /// Türkçe pencerede her sekmedeki her <see cref="ComboBox"/> ve <see cref="Button"/>
    /// yazısının ölçüsü. Sekme yürüyüşü <see cref="ScanTabs"/> ile aynı gerekçeyle sekme
    /// sekme koşuyor: seçili olmayan sekmenin içeriği görsel ağaca hiç girmiyor.
    /// </summary>
    private static IReadOnlyList<BoxLabel> MeasureTurkishBoxes(Size size, bool loaded) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.UseTurkish();
            if (loaded)
            {
                window.LoadWithoutProbing(SamplePath, Sample());
                window.SettleFades();
            }

            LayOutAt(window, size);

            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            var labels = new List<BoxLabel>();

            for (var index = 0; index < tabs.ItemCount; index++)
            {
                tabs.SelectedIndex = index;
                RelayoutAt(window, size);

                var tab = (tabs.ContainerFromIndex(index) as TabItem)?.Header?.ToString() ?? $"{index}";

                foreach (var box in window.GetVisualDescendants().OfType<ComboBox>().Where(b => b.IsEffectivelyVisible))
                {
                    if (box.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault() is not { } shown) continue;

                    var room = SelectionRoom(box);
                    if (double.IsNaN(room)) continue;

                    var name = string.IsNullOrEmpty(box.Name) ? nameof(ComboBox) : box.Name!;
                    labels.Add(new BoxLabel(tab, name, shown.Text ?? string.Empty, NeededWidth(shown, shown.Text ?? string.Empty), room));

                    foreach (var option in box.Items.OfType<ComboBoxItem>()
                                 .Select(item => item.Content?.ToString())
                                 .Where(text => !string.IsNullOrWhiteSpace(text)))
                        labels.Add(new BoxLabel(tab, $"{name} · seçenek", option!, NeededWidth(shown, option!), room));
                }

                foreach (var button in window.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible))
                {
                    if (button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault() is not { } shown) continue;
                    if (string.IsNullOrWhiteSpace(shown.Text)) continue;
                    if (shown.TextWrapping != TextWrapping.NoWrap) continue;

                    var name = string.IsNullOrEmpty(button.Name) ? $"{nameof(Button)}[{shown.Text}]" : button.Name!;
                    labels.Add(new BoxLabel(tab, name, shown.Text!, NeededWidth(shown, shown.Text!), shown.Bounds.Width));
                }
            }

            return (IReadOnlyList<BoxLabel>)labels;
        });

}
