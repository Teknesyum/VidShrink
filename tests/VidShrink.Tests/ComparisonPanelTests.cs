using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Playback;

namespace VidShrink.Tests;

/// <summary>
/// T44: üç kademe, gecikmeli iniş ve köşe yarıçapı gerçek pencerede ölçülüyor. Pencere
/// gösterilmiyor — <see cref="AppHost"/> Avalonia'yı kendi iş parçacığında kuruyor, ölçüm
/// yerleşimi elle koşturuyor. Ekranda hiçbir şey açılmıyor.
///
/// Zamanlayıcılar burada tik atmaz: bu iş parçacığında ileti döngüsü yok, dolayısıyla
/// <see cref="DispatcherTimer"/> ateşlenmez. Sahte saat (<see cref="FakeClock"/>) yine de
/// <see cref="HoverZone.Clock"/> yerine takılıyor: T79'dan beri panel beklemediği için
/// ölçülen şey sürenin dolması değil, <b>hiç tik kurulmamış olması</b>. Sayacın kendi karar
/// kapıları (<see cref="HoverZone.ShouldHide"/>, <see cref="HoverZone.ShouldShow"/>) de
/// doğrudan okunabiliyor. Hiçbir ölçümde <c>Thread.Sleep</c> yok (T70/K7).
/// </summary>
public sealed class ComparisonPanelTests
{
    private static readonly Size WindowSize = new(1560, 1060);

    /// <summary>
    /// T66/K4: yerleşimi verilen görüş alanında koşturur — konağın penceresinden bağımsız.
    /// Pencerenin kendisine <see cref="Layoutable.Arrange"/> çağırmak boyutu taşımıyor;
    /// <c>Window.ArrangeSetBounds</c> verilen boyutu değil <c>ClientSize</c>'ı döndürür, yani
    /// sınırlar platform penceresinden gelir. Bu yüzden istenen boyut pencerenin kök görsel
    /// çocuğuna veriliyor — <see cref="WindowLayoutTests"/> ile aynı yol (T59). O kök aynı
    /// zamanda panelin terfi ettiği katmanın taşıyıcısıdır, dolayısıyla kök kademelerin
    /// ölçüldüğü alan da buradan gelir.
    /// </summary>
    private static void LayOutAt(MainWindow window, Size size)
    {
        window.Width = double.NaN;
        window.Height = double.NaN;

        window.Measure(size);
        window.Arrange(new Rect(size));
        window.UpdateLayout();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.Measure(size);
        root.Arrange(new Rect(size));
    }

    private static T Read<T>(Func<MainWindow, ComparisonPanel, T> read) => Read(WindowSize, read);

    private static T Read<T>(Size size, Func<MainWindow, ComparisonPanel, T> read) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            LayOutAt(window, size);

            var panel = window.GetVisualDescendants().OfType<ComparisonPanel>().Single();
            return read(window, panel);
        });

    /// <summary>Kademeyi tekerlekle sürer; her dokunuştan sonra bekleyen işler koşturulur.</summary>
    private static void WheelTo(MainWindow window, ComparisonPanel panel, ShelterStage stage)
    {
        for (var i = 0; i < 100 && panel.Shelter != stage; i++)
        {
            panel.Zoom(1, new Point(0, 0));
            Settle(window);
        }

        Assert.Equal(stage, panel.Shelter);
    }

    /// <summary>
    /// Terfi sonrası boy <see cref="DispatcherPriority.Render"/> önceliğinde gönderiliyor
    /// (geçiş bandından başlasın diye). Ölçüm o işi elle koşturur.
    /// </summary>
    private static void Settle(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static Rect OverlayBounds(ComparisonPanel panel)
    {
        var overlay = panel.GetVisualAncestors().OfType<VisualLayerManager>().First();
        return new Rect(overlay.Bounds.Size);
    }

    // ---- K1: üç kademe --------------------------------------------------------------

    /// <summary>
    /// Ölçülen pencere boyutları. Tasarım boyutu, pencerenin kendi tabanı ve tabanın
    /// altında kalan bir başsız konak boyutu — sonuncusu CI'ın koştuğu haldir (T66).
    /// </summary>
    public static TheoryData<double, double> WindowSizes() => new()
    {
        { 1560, 1060 },
        { 1040, 720 },
        { 480, 326 }
    };

    [Theory]
    [MemberData(nameof(WindowSizes))]
    public void Orta_kademe_bandindan_buyuk_pencereden_kucuktur(double width, double height)
    {
        var (band, mid, window) = Read(new Size(width, height), (host, panel) =>
        {
            var bandWidth = panel.Bounds.Width;
            WheelTo(host, panel, ShelterStage.Mid);
            return (bandWidth, panel.StageTarget, OverlayBounds(panel));
        });

        Assert.True(mid.Width > band, $"orta {mid.Width:0.#} <= band {band:0.#}");
        Assert.True(mid.Width < window.Width, $"orta {mid.Width:0.#} >= pencere {window.Width:0.#}");
        Assert.True(mid.Height < window.Height, $"orta {mid.Height:0.#} >= pencere {window.Height:0.#}");
        Assert.True(mid.X > 0 && mid.Y > 0, $"orta kademe kenara yapıştı: {mid.X:0.#},{mid.Y:0.#}");
    }

    [Theory]
    [MemberData(nameof(WindowSizes))]
    public void Tam_kademe_pencerenin_tamamini_kaplar(double width, double height)
    {
        var (full, window) = Read(new Size(width, height), (host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Full);
            return (panel.StageTarget, OverlayBounds(panel));
        });

        Assert.Equal(0, full.X, 6);
        Assert.Equal(0, full.Y, 6);
        Assert.Equal(window.Width, full.Width, 6);
        Assert.Equal(window.Height, full.Height, 6);
    }

    /// <summary>
    /// T66/K3: kullanıcı tekerleği çevirdiğinde üç kademe görür, pencere ne kadar küçük
    /// olursa olsun. Orta kademe tam kademeye eşitlenirse üç kademe ikiye düşer — bu bir
    /// ürün kusurudur, ölçü kusuru değil. Ölçülen şey her iki eksende de ölçülebilir bir
    /// pay: orta kademe hem bandından büyük hem tam kademeden küçüktür.
    /// </summary>
    [Theory]
    [MemberData(nameof(WindowSizes))]
    public void Uc_kademe_her_pencere_boyutunda_uc_ayri_boydur(double width, double height)
    {
        var (band, mid, full) = Read(new Size(width, height), (host, panel) =>
        {
            var bandSize = panel.Bounds.Size;
            WheelTo(host, panel, ShelterStage.Mid);
            var midTarget = panel.StageTarget;
            WheelTo(host, panel, ShelterStage.Full);
            return (bandSize, midTarget, panel.StageTarget);
        });

        Assert.True(mid.Width > band.Width, $"orta {mid.Width:0.#} <= band {band.Width:0.#}");
        Assert.True(mid.Width < full.Width, $"orta {mid.Width:0.#} >= tam {full.Width:0.#}");
        Assert.True(mid.Height < full.Height, $"orta {mid.Height:0.#} >= tam {full.Height:0.#}");
    }

    [Fact]
    public void Orta_kademe_panel_kok_katmandadir()
    {
        var promoted = Read((host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Mid);
            return panel.IsPromoted;
        });

        Assert.True(promoted);
    }

    [Fact]
    public void Yer_tutucu_kademe_degisince_bandini_korur()
    {
        var (atMid, atFull) = Read((host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Mid);
            var mid = panel.Bounds.Height;
            WheelTo(host, panel, ShelterStage.Full);
            return (mid, panel.Bounds.Height);
        });

        Assert.Equal(atMid, atFull, 6);
    }

    // ---- K2: gecikmeli iniş ---------------------------------------------------------

    [Fact]
    public void Fare_testi_hedef_kademenin_sinirina_gore_yapilir()
    {
        var (insideTarget, outsideTarget, window) = Read((host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Mid);
            var target = panel.StageTarget;
            var bounds = OverlayBounds(panel);

            // Hedefin dışında ama pencerenin içinde bir nokta: tam pencere sınırına göre
            // ölçülseydi bu nokta panelin üstünde sayılırdı.
            return (panel.TargetCovers(target.Center),
                    panel.TargetCovers(new Point(target.X / 2, bounds.Height / 2)),
                    bounds);
        });

        Assert.True(insideTarget);
        Assert.False(outsideTarget);
        Assert.True(window.Width > 0);
    }

    /// <summary>
    /// T70/K7: bekleme süresini duvar saati olmadan süren sahte saat. Gerçek zamanlayıcı bu
    /// iş parçacığında zaten tik atmaz (ileti döngüsü yok); ölçüm süreyi kendi ilerletir,
    /// yani hiçbir ölçüm <c>Thread.Sleep</c> beklemiyor.
    /// </summary>
    private sealed class FakeClock : IHoverClock
    {
        private Action? _fire;

        /// <summary>Sayaca sorulan süre. Belirtecin koda ulaştığını bu gösterir.</summary>
        public TimeSpan Delay { get; private set; }

        public TimeSpan Remaining { get; private set; }

        /// <summary>Bekleyen bir tik var mı. Yoksa karar beklemeden verilmiştir.</summary>
        public bool Pending => _fire is not null;

        public void Start(TimeSpan delay, Action fire)
        {
            Delay = delay;
            Remaining = delay;
            _fire = fire;
        }

        public void Stop() => _fire = null;

        public void Advance(TimeSpan step)
        {
            if (_fire is null) return;
            Remaining -= step;
            if (Remaining > TimeSpan.Zero) return;
            var fire = _fire;
            _fire = null;
            fire();
        }
    }

    /// <summary>Paneli tabana indirir ve büyüme sayacına sahte saat takar.</summary>
    private static FakeClock AtBand(MainWindow window, ComparisonPanel panel)
    {
        panel.Descend();
        Settle(window);

        var clock = new FakeClock();
        panel.Descent.Clock = clock;
        return clock;
    }

    private static double PanelScale(ComparisonPanel panel) => Math.Round(panel.Gesture.PanelScale, 9);

    /// <summary>
    /// T70/K1: fare çekildiği an panel küçülür. Sahte saat hiç ilerletilmiyor ve panel yine
    /// de bandına dönüyor; üstelik küçültme için bir tik bile kurulmuyor.
    /// </summary>
    [Fact]
    public void Fare_cekilince_panel_beklemeden_kuculur()
    {
        var (stage, scale, pending) = Read((host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Mid);

            var clock = new FakeClock();
            panel.Descent.Clock = clock;

            panel.TrackPointer(panel.StageTarget.Center);
            panel.PointerLeftWindow();

            return (panel.Shelter, PanelScale(panel), clock.Pending);
        });

        Assert.Equal(ShelterStage.Band, stage);
        Assert.Equal(1.0, scale);
        Assert.False(pending, "küçülme için bekleme sayacı kuruldu");
    }

    /// <summary>
    /// T79/K1: bekleme kalktı. Fare panele girdiği turda panel büyür — sahte saat hiç
    /// ilerletilmiyor, üstelik büyüme için bir tik bile kurulmuyor. T73'ün iki saniyelik
    /// ölçüsünün yerini bu alıyor: eski ölçü sürenin dolmasını şart koşuyordu.
    /// </summary>
    [Fact]
    public void Fare_girer_girmez_panel_buyur()
    {
        var (floor, wanted, grown, stage, pending) = Read((host, panel) =>
        {
            var clock = AtBand(host, panel);
            host.TryFindResource("PlaybackHoverZoom", out var token);
            var before = PanelScale(panel);

            panel.HoverPanel(true);
            Settle(host);

            return (before, (double)token!, PanelScale(panel), panel.Shelter, clock.Pending);
        });

        Assert.Equal(1.0, floor);
        Assert.True(grown >= wanted, $"ölçek {grown:0.###}, eşik {wanted:0.###}");
        Assert.Equal(ShelterStage.Mid, stage);
        Assert.False(pending, "büyüme için bekleme sayacı kuruldu");
    }

    /// <summary>
    /// T70/K5: denetim şeridi değişmedi. Gösterme beklemez — sayaç kurulmadan şerit açılır;
    /// gizleme kendi belirtecinin söylediği 360 ms'yi bekler.
    /// </summary>
    [Fact]
    public void Serit_gostermede_beklemez_gizlemede_bekler()
    {
        var (shown, armedOnShow, heldOpen, hideDelay, hidden) = Read((host, panel) =>
        {
            var zone = panel.Controls.Zone;
            zone.Reset(false);

            var clock = new FakeClock();
            zone.Clock = clock;

            zone.PointerWithin(true);
            var open = zone.IsVisible;
            var pendingOnShow = clock.Pending;

            zone.PointerWithin(false);
            var stillOpen = zone.IsVisible;
            var asked = clock.Delay;

            clock.Advance(asked);
            return (open, pendingOnShow, stillOpen, asked, zone.IsVisible);
        });

        Assert.True(shown, "şerit gösterme için bekledi");
        Assert.False(armedOnShow, "gösterme sayaç kurdu");
        Assert.True(heldOpen, "şerit beklemeden gizlendi");
        Assert.Equal(TimeSpan.FromMilliseconds(360), hideDelay);
        Assert.False(hidden, "şerit gecikme dolunca gizlenmedi");
    }

    // ---- T73/K5: büyümüş panel pencereye sığar --------------------------------------

    /// <summary>Fareyi panele sokar; T79'dan beri büyüme aynı turda uygulanır.</summary>
    private static void HoverUntilRise(MainWindow window, ComparisonPanel panel)
    {
        AtBand(window, panel);
        panel.HoverPanel(true);
        Settle(window);
        Assert.Equal(ShelterStage.Mid, panel.Shelter);
    }

    public static TheoryData<double, double> RiseWindows() => new()
    {
        { 1560, 1060 },
        { 2560, 1440 }
    };

    /// <summary>
    /// K5: büyümüş panelin boyu pencereden hesaplanıyor. Beklenen sayı ölçümde uydurulmuyor,
    /// kök katmanın kendi boyu ile <c>PlaybackMidShare</c> belirtecinden çıkıyor — sabit
    /// çarpan kalsaydı uzun pencerede panel bu boyun altında kalırdı.
    /// </summary>
    [Theory]
    [MemberData(nameof(RiseWindows))]
    public void Fareyle_buyuyen_panel_pencereye_sigan_en_buyuk_boyu_alir(double width, double height)
    {
        var (grown, area, share) = Read(new Size(width, height), (host, panel) =>
        {
            HoverUntilRise(host, panel);
            host.TryFindResource("PlaybackMidShare", out var value);
            return (panel.StageTarget, OverlayBounds(panel), (double)value!);
        });

        Assert.Equal(area.Height * share, grown.Height, 3);
        Assert.True(grown.Height < area.Height, $"panel {grown.Height:0.#} >= pencere {area.Height:0.#}");
        Assert.True(grown.Y > 0, $"panel üst kenara yapıştı: {grown.Y:0.#}");
    }

    /// <summary>Yerleşimi verilen boyda yeniden koşturur; ölçüm arada bir ölçü değiştirdiyse.</summary>
    private static void ReLayOut(MainWindow window, Size size)
    {
        window.InvalidateMeasure();
        foreach (var part in window.GetVisualDescendants().OfType<Layoutable>()) part.InvalidateMeasure();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.Measure(size);
        root.Arrange(new Rect(size));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// K5'in asıl ölçüsü: taban boy küçükken de panel pencereyi dolduruyor.
    ///
    /// Bu ayrımı ölçmek için band bilerek kısaltılıyor. Sebebi şu: bu düzende panel bandı
    /// pencereyle birlikte uzuyor, dolayısıyla eski sabit çarpan (2x) her erişilebilir
    /// pencere boyunda tavanı zaten aşıyor ve fark görünmüyor. Band kısaldığı anda —
    /// önizleme sütununa başka bir şey girdiğinde olacağı gibi — sabit çarpan pencerenin
    /// altında kalır: 300'ün iki katı 600, pencerenin payı ise 954. Ölçek pencereden
    /// hesaplandığında panel o 954'ü alıyor.
    ///
    /// Tavan ölçüldükten sonra kaldırılıyor: kısaltma bandı kurmak içindi, büyümüş paneli
    /// kısıtlamak için değil. Ölçüm hem hedefi hem kabuğun gerçekten yerleştiği boyu okuyor.
    /// </summary>
    /// <summary>
    /// Ölçümün kısalttığı band. Pencerenin payının (1060 x 0,9 = 954) yarısından küçük
    /// olmak zorunda: büyüğü sabit çarpanla da tavanı aşar ve iki hesap ayırt edilemez.
    /// </summary>
    private const double ShortBand = 300;

    [Fact]
    public void Kisa_bandli_panel_de_pencereyi_dolduruyor()
    {
        var (band, target, settled, area, share, scale, fixedFactor) = Read((host, panel) =>
        {
            // Kabuğun gerçekten yerleştiği boy okunacak: geçiş açıkken boy bir animasyon
            // değeridir ve bu koşuda saat tik atmaz, kabuk terfi anındaki boyunda kalır.
            // Hareket azaltma yolu boyu doğrudan uygular, ölçülen sayı da gerçek olur.
            panel.MotionReduced = true;
            panel.Descend();
            panel.Shell.MinHeight = ShortBand;
            panel.Shell.MaxHeight = ShortBand;
            ReLayOut(host, WindowSize);
            var shortBand = panel.Shell.Bounds.Height;

            HoverUntilRise(host, panel);
            var grown = panel.StageTarget;
            var reach = panel.Gesture.PanelScale;

            panel.Shell.ClearValue(Layoutable.MaxHeightProperty);
            panel.Shell.ClearValue(Layoutable.MinHeightProperty);
            ReLayOut(host, WindowSize);

            host.TryFindResource("PlaybackMidShare", out var midShare);
            host.TryFindResource("PlaybackHoverZoom", out var hoverZoom);
            return (shortBand, grown, panel.Shell.Bounds.Height, OverlayBounds(panel),
                (double)midShare!, reach, (double)hoverZoom!);
        });

        Assert.Equal(ShortBand, band, 3);
        Assert.Equal(area.Height * share, target.Height, 3);
        Assert.Equal(area.Height * share, settled, 3);
        Assert.True(scale > fixedFactor,
            $"ölçek {scale:0.###} sabit çarpanda ({fixedFactor:0.###}) kaldı, band {band:0.#}");
    }

    /// <summary>
    /// K5 ikinci yarısı: iki farklı pencere boyu iki farklı boy veriyor. Sabit çarpan tek
    /// sayı üretirdi; ölçülen şey tam olarak bu farktır.
    /// </summary>
    [Fact]
    public void Iki_pencere_boyu_iki_ayri_buyume_boyu_verir()
    {
        double Grown(double width, double height) =>
            Read(new Size(width, height), (host, panel) =>
            {
                HoverUntilRise(host, panel);
                return panel.StageTarget.Height;
            });

        var shortWindow = Grown(1560, 1060);
        var tallWindow = Grown(1560, 1600);

        Assert.True(tallWindow > shortWindow + 1,
            $"uzun pencere {tallWindow:0.#}, kısa pencere {shortWindow:0.#}");
    }

    /// <summary>
    /// K6 ikinci kapı: fareyle büyümek paneli tam kademeye çıkarmıyor. T66'da orta kademe
    /// pencereye eşitlenerek üç kademeyi ikiye düşürmüştü; aynı sonuç bu kez ölçeği
    /// tavana dayamakla doğardı. Uzun pencerede bile kademe orta kalıyor ve tekerlekle
    /// çıkılan tam kademe bundan ölçülebilir biçimde büyük.
    /// </summary>
    [Theory]
    [MemberData(nameof(RiseWindows))]
    public void Fareyle_buyume_tam_kademeye_esitlenmez(double width, double height)
    {
        var (stage, grown, full) = Read(new Size(width, height), (host, panel) =>
        {
            HoverUntilRise(host, panel);
            var mid = (panel.Shelter, panel.StageTarget);

            WheelTo(host, panel, ShelterStage.Full);
            return (mid.Shelter, mid.StageTarget, panel.StageTarget);
        });

        Assert.Equal(ShelterStage.Mid, stage);
        Assert.True(grown.Height < full.Height, $"fareyle büyüyen {grown.Height:0.#} >= tam {full.Height:0.#}");
        Assert.True(grown.Width < full.Width, $"fareyle büyüyen {grown.Width:0.#} >= tam {full.Width:0.#}");
    }


    [Fact]
    public void Zaman_asimi_parametreyi_de_tabana_indirir()
    {
        var (t, stage, afterOneNotch) = Read((host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Full);

            // İniş sayacının zaman aşımı bu yolu çağırır.
            panel.Descend();
            Settle(host);

            var floor = panel.Gesture.T;
            var landed = panel.Shelter;

            panel.Zoom(1, new Point(0, 0));
            Settle(host);
            return (floor, landed, panel.Shelter);
        });

        Assert.Equal(0, t, 9);
        Assert.Equal(ShelterStage.Band, stage);
        Assert.Equal(ShelterStage.Band, afterOneNotch);
    }

    /// <summary>
    /// T70: pencerenin kendi çıkış olayı paneli indirir. Eskiden bu olay yalnız sayacı
    /// kurardı; küçülme artık beklemesiz olduğu için panel aynı turda bandına döner.
    /// </summary>
    [Fact]
    public void Fare_pencereden_cikinca_panel_iner()
    {
        var (armed, decides, stage) = Read((host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Full);

            // Fare önce panelin üstünde: sayaç tutuluyor, iniş kararı yok.
            panel.TrackPointer(panel.StageTarget.Center);
            var before = panel.Descent.Generation;
            Assert.False(panel.Descent.ShouldHide(before));

            // Pencerenin kendi çıkış olayı. Kaynak pencerenin kendisi, yani gerçek terk.
            host.RaiseEvent(new PointerEventArgs(
                InputElement.PointerExitedEvent,
                host,
                new Pointer(0, PointerType.Mouse, true),
                host,
                new Point(-1, -1),
                0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
                KeyModifiers.None));

            var after = panel.Descent.Generation;
            return (after > before, panel.Descent.ShouldHide(after), panel.Shelter);
        });

        Assert.True(armed, "çıkış olayı sayacı ilerletmedi");
        Assert.True(decides, "sayaç iniş kararını taşımıyor");
        Assert.Equal(ShelterStage.Band, stage);
    }

    /// <summary>
    /// T70: fare pencereden çıkınca panel iner; geri girince büyüme sayacı kurulur ve eski
    /// kuşak düşer. İki yön de aynı sayaçta yaşıyor, kuşak koruması ikisini karıştırmıyor.
    /// </summary>
    [Fact]
    public void Pencere_disina_cikis_paneli_indirir_geri_giris_sayaci_kurar()
    {
        var (stage, afterExit, afterReturn) = Read((host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Mid);

            panel.TrackPointer(panel.StageTarget.Center);
            panel.PointerLeftWindow();
            var landed = panel.Shelter;
            var pending = panel.Descent.Generation;
            var leaves = panel.Descent.ShouldHide(pending);

            // Fare geri girdi: eski kuşak düştü, bekleyen karar artık uygulanmaz.
            panel.HoverPanel(true);
            return (landed, leaves, panel.Descent.ShouldHide(pending));
        });

        Assert.Equal(ShelterStage.Band, stage);
        Assert.True(afterExit);
        Assert.False(afterReturn);
    }

    [Fact]
    public void Terfi_kalkinca_pencere_dinleyicileri_sokulur()
    {
        var (before, after) = Read((host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Full);
            panel.Descend();
            Settle(host);

            // Panel bandına indi. Dinleyici sökülmediyse bu çıkış olayı hâlâ sayacı
            // oynatırdı; kuşak sabit kalıyorsa pencereden gerçekten ayrılmış demektir.
            var stale = panel.Descent.Generation;
            host.RaiseEvent(new PointerEventArgs(
                InputElement.PointerExitedEvent,
                host,
                new Pointer(0, PointerType.Mouse, true),
                host,
                new Point(-1, -1),
                0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
                KeyModifiers.None));

            return (stale, panel.Descent.Generation);
        });

        Assert.Equal(before, after);
    }

    [Fact]
    public void Inis_baslamisken_tekerlek_geri_yukari_cevrilebilir()
    {
        var (stage, promoted) = Read((host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Mid);
            panel.Descend();
            Settle(host);

            WheelTo(host, panel, ShelterStage.Mid);
            return (panel.Shelter, panel.IsPromoted);
        });

        Assert.Equal(ShelterStage.Mid, stage);
        Assert.True(promoted);
    }

    private static TimeSpan? Span(MainWindow window, string key)
        => window.TryFindResource(key, out var value) && value is TimeSpan span ? span : null;

    /// <summary>
    /// T79/K1, K2: panelin iki bekleme belirteci de temadan kalktı — sıfırlanmadı, silindi.
    /// Geri sayımın üç ölçüsü de yok. Şeridin kendi iki süresi duruyor: onlar başka bir
    /// kararın ölçüsü ve bu sözleşme onlara dokunmuyor.
    /// </summary>
    [Fact]
    public void Kalkan_belirtecler_temada_yok()
    {
        var (gone, stripShow, stripHide) = AppHost.Run(() =>
        {
            var window = new MainWindow();
            var dead = new[]
            {
                "PlaybackPanelRiseDelay", "PlaybackPanelFallDelay", "PlaybackDescendDelay",
                "PlaybackCountdownSize", "PlaybackCountdownRadius", "PlaybackCountdownStroke",
                "PlaybackCountdown"
            };
            return (dead.Where(key => window.TryFindResource(key, out _)).ToArray(),
                    Span(window, "PlaybackStripShowDelay"),
                    Span(window, "PlaybackStripHideDelay"));
        });

        Assert.Empty(gone);
        Assert.Equal(TimeSpan.Zero, stripShow);
        Assert.Equal(TimeSpan.FromMilliseconds(360), stripHide);
    }

    // ---- K3: köşe yarıçapı ----------------------------------------------------------

    [Fact]
    public void Pano_kabugun_kose_yaricapini_tasir()
    {
        var (radius, expected, size) = Read((host, panel) =>
        {
            var clip = Assert.IsType<RectangleGeometry>(panel.Stage.Clip);
            var token = panel.TryFindResource("RadiusPanelScalar", out var value) && value is double number ? number : -1;
            return (clip.RadiusX, token, clip.Rect.Size);
        });

        Assert.Equal(expected, radius, 6);
        Assert.True(size.Width > 0 && size.Height > 0, $"pano ölçüsü {size}");
    }

    [Fact]
    public void Kose_pikseli_pano_zemininin_disinda_kalir()
    {
        var (corner, centre) = Read((host, panel) =>
        {
            var clip = Assert.IsType<RectangleGeometry>(panel.Stage.Clip);
            return (clip.FillContains(new Point(1, 1)), clip.FillContains(clip.Rect.Center));
        });

        Assert.False(corner);
        Assert.True(centre);
    }

    private static Button ZoomButton(MainWindow window, string name) =>
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name);

    private static PointerEventArgs PointerCrossing(Control source, RoutedEvent<PointerEventArgs> which) =>
        new(which, source, new Pointer(0, PointerType.Mouse, true), source, default,
            0, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
            KeyModifiers.None);

    /// <summary>
    /// T46/K3: dugmeler tek jest parametresini centik centik surer. Ayri bir yakinlastirma
    /// durumu yok - tekerlekle dugme ayni sayiya yaziyor.
    /// </summary>
    [Fact]
    public void TiklamaDugmeleriTekParametreyiSurer()
    {
        var (afterIn, afterTwo, afterOut, afterWheelThenOut) = Read((window, panel) =>
        {
            var zoomIn = ZoomButton(window, "BtnZoomIn");
            var zoomOut = ZoomButton(window, "BtnZoomOut");

            panel.Descend();
            zoomIn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var one = panel.Gesture.T;
            zoomIn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var two = panel.Gesture.T;
            zoomOut.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var back = panel.Gesture.T;
            panel.Zoom(1, new Point(0, 0));
            zoomOut.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return (one, two, back, panel.Gesture.T);
        });

        Assert.Equal(ZoomGesture.NotchStep, afterIn, 9);
        Assert.Equal(2 * ZoomGesture.NotchStep, afterTwo, 9);
        Assert.Equal(ZoomGesture.NotchStep, afterOut, 9);
        Assert.Equal(ZoomGesture.NotchStep, afterWheelThenOut, 9);
    }

    /// <summary>T46/K3: uclarda dugmeler devre disi - tavanda arti, tabanda eksi.</summary>
    [Fact]
    public void UclardaDugmelerDevreDisi()
    {
        var (floorIn, floorOut, ceilingIn, ceilingOut) = Read((window, panel) =>
        {
            var zoomIn = ZoomButton(window, "BtnZoomIn");
            var zoomOut = ZoomButton(window, "BtnZoomOut");

            panel.Descend();
            var atFloorIn = zoomIn.IsEnabled;
            var atFloorOut = zoomOut.IsEnabled;
            WheelTo(window, panel, ShelterStage.Full);
            return (atFloorIn, atFloorOut, zoomIn.IsEnabled, zoomOut.IsEnabled);
        });

        Assert.True(floorIn);
        Assert.False(floorOut);
        Assert.False(ceilingIn);
        Assert.True(ceilingOut);
    }

    /// <summary>
    /// T46/K3: fare dugmelerin ustundeyken inis sayaci durur. Dugmeler kabugun icinde
    /// oldugu icin, tutma olmadan kullanici yakinlastirmaya calisirken panel inerdi.
    /// </summary>
    [Fact]
    public void DugmeUstundeInisSayaciDurur()
    {
        var (heldWhileOver, releasedAfter) = Read((window, panel) =>
        {
            WheelTo(window, panel, ShelterStage.Full);
            var zoomIn = ZoomButton(window, "BtnZoomIn");

            zoomIn.RaiseEvent(PointerCrossing(zoomIn, InputElement.PointerEnteredEvent));
            panel.PointerLeftWindow();
            var pending = panel.Descent.Generation;
            var held = !panel.Descent.ShouldHide(pending);

            zoomIn.RaiseEvent(PointerCrossing(zoomIn, InputElement.PointerExitedEvent));
            var next = panel.Descent.Generation;
            return (held, panel.Descent.ShouldHide(next));
        });

        Assert.True(heldWhileOver);
        Assert.True(releasedAfter);
    }

    /// <summary>
    /// T52/K2: buyume yolu tema belirtecinin soyledigi kata cikar. Iki kat panel bandina
    /// sigmadigi icin panel ayni anda kok katmana terfi eder. T70: bu yolu artik fare
    /// girisi degil, bekleme suresi dolan sayac cagiriyor; olcum yolu dogrudan suruyor.
    /// Terfi ettikten sonra HoverZoom(false) olcegi dusurmez — inis Descend'in isidir.
    /// </summary>
    [Fact]
    public void GirisOlceklemesiPaneliIkiKatinaCikarir()
    {
        var (wanted, scale, stage, held, afterDescent) = Read((window, panel) =>
        {
            panel.Descend();
            window.TryFindResource("PlaybackHoverZoom", out var token);

            panel.HoverZoom(true);
            Settle(window);
            var entered = Math.Round(panel.Gesture.PanelScale, 9);
            var shelter = panel.Shelter;

            // Fare cikti ama inis sayaci daha karar vermedi: olcek yerinde durur.
            panel.HoverZoom(false);
            var stillUp = Math.Round(panel.Gesture.PanelScale, 9);

            panel.Descend();
            return ((double)token!, entered, shelter, stillUp, Math.Round(panel.Gesture.PanelScale, 9));
        });

        Assert.Equal(wanted, scale);
        Assert.Equal(ShelterStage.Mid, stage);
        Assert.Equal(wanted, held);
        Assert.Equal(1.0, afterDescent);
    }

    /// <summary>
    /// T46/K5, birinci tuzak: kullanicinin kendi secimi ezilmiyor. Tekerlekle bir deger
    /// secildikten sonra fare girip cikmak o degeri ne 2x'e ziplatir ne de sifirlar.
    /// </summary>
    [Fact]
    public void GirisYakinlastirmasiKullaniciSeciminiEzmez()
    {
        var (chosen, afterEnter, afterExit) = Read((window, panel) =>
        {
            panel.Descend();
            panel.Zoom(2, new Point(0, 0));
            var picked = panel.Gesture.T;
            panel.HoverZoom(true);
            var entered = panel.Gesture.T;
            panel.HoverZoom(false);
            return (picked, entered, panel.Gesture.T);
        });

        Assert.Equal(2 * ZoomGesture.NotchStep, chosen, 9);
        Assert.Equal(chosen, afterEnter, 9);
        Assert.Equal(chosen, afterExit, 9);
    }

    // ---- T79/K3: yakinlastirma dugmeleri paneli gercekten buyutur --------------------

    /// <summary>
    /// K3, asıl şikâyet. Düğmeye basmak için fare panelin üstünde olmak zorunda, yani panel
    /// zaten fareyle büyümüş ve orta kademenin payına dayanmış durumda. Eskiden artı tuşu
    /// bu noktada panelin boyunu değil yalnız yüzde okumasını oynatıyordu.
    ///
    /// Ölçü kademenin sınırını da pinliyor: yükseklik payda (<c>PlaybackMidShare</c>) asılı
    /// duruyor — panel o eksende zaten tavanda — ama panel yine de büyüyor. Eksi aynı yoldan
    /// geri getiriyor.
    /// </summary>
    [Fact]
    public void Yakinlastirma_dugmesi_buyumus_paneli_de_buyutur()
    {
        var (risen, afterIn, afterOut, area, share) = Read((window, panel) =>
        {
            HoverUntilRise(window, panel);
            var grown = panel.StageTarget;

            var zoomIn = ZoomButton(window, "BtnZoomIn");
            zoomIn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle(window);
            var bigger = panel.StageTarget;

            var zoomOut = ZoomButton(window, "BtnZoomOut");
            zoomOut.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle(window);

            window.TryFindResource("PlaybackMidShare", out var value);
            return (grown, bigger, panel.StageTarget, OverlayBounds(panel), (double)value!);
        });

        Assert.True(afterIn.Width > risen.Width + 1,
            $"artı yutuldu: {risen.Width:0.#} -> {afterIn.Width:0.#}");
        Assert.Equal(area.Height * share, risen.Height, 3);
        Assert.Equal(risen.Height, afterIn.Height, 3);

        Assert.True(afterOut.Width < afterIn.Width - 1,
            $"eksi yutuldu: {afterIn.Width:0.#} -> {afterOut.Width:0.#}");
        Assert.Equal(risen.Width, afterOut.Width, 3);
    }

    /// <summary>
    /// K3'ün sınır yarısı: hiçbir dokunuş sessizce yutulmuyor. Panel fareyle büyümüş
    /// hâlden başlayıp artı tuşuna tam kademeye varana kadar basılıyor ve <b>her</b> dokunuşun
    /// boyu değiştirmesi şart koşuluyor.
    ///
    /// Düzeltmeden önce bu dizinin ortası dümdüzdü: iki eksen de paya dayandıktan sonra beş
    /// dokunuş üst üste hiçbir şey yapmıyor, panel ancak onuncu dokunuşta tam kademeye
    /// çıkıyordu. Dizinin son halkası pencerenin kendisi: kademe sınırı tuşu yutmuyor, geçiriyor.
    /// </summary>
    [Fact]
    public void Hicbir_yakinlastirma_dokunusu_yutulmaz()
    {
        var (steps, stage, area) = Read((window, panel) =>
        {
            HoverUntilRise(window, panel);

            var zoomIn = ZoomButton(window, "BtnZoomIn");
            var seen = new List<Rect> { panel.StageTarget };

            for (var i = 0; i < 12 && panel.Shelter != ShelterStage.Full; i++)
            {
                zoomIn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Settle(window);
                seen.Add(panel.StageTarget);
            }

            return (seen.ToArray(), panel.Shelter, OverlayBounds(panel));
        });

        for (var i = 1; i < steps.Length; i++)
            Assert.True(steps[i].Width > steps[i - 1].Width + 1 || steps[i].Height > steps[i - 1].Height + 1,
                $"{i}. dokunuş yutuldu: {steps[i - 1].Width:0.#}x{steps[i - 1].Height:0.#} -> " +
                $"{steps[i].Width:0.#}x{steps[i].Height:0.#}");

        Assert.Equal(ShelterStage.Full, stage);
        Assert.Equal(area.Width, steps[^1].Width, 3);
        Assert.Equal(area.Height, steps[^1].Height, 3);
    }

    /// <summary>
    /// K3'ün üçüncü yarısı: düzeltme "her artı tam kademeye fırlatır" demiyor. Tam kademeden
    /// bir eksi panel bandına değil orta kademeye iner ve panel pencereyi kaplamayı bırakır —
    /// eskiden ilk eksi histerezis bandına düşüp hiçbir şey yapmıyordu.
    /// </summary>
    [Fact]
    public void Tam_kademeden_bir_eksi_paneli_kucultur()
    {
        var (full, after, stage) = Read((window, panel) =>
        {
            WheelTo(window, panel, ShelterStage.Full);
            var covered = panel.StageTarget;

            var zoomOut = ZoomButton(window, "BtnZoomOut");
            zoomOut.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle(window);

            return (covered, panel.StageTarget, panel.Shelter);
        });

        Assert.Equal(ShelterStage.Mid, stage);
        Assert.True(after.Height < full.Height - 1,
            $"eksi yutuldu: {full.Height:0.#} -> {after.Height:0.#}");
    }

    // ---- T79/K4: bos onizlemenin arkasi saydam ---------------------------------------

    /// <summary>
    /// K4: kaynak yüklü değilken panonun zemini arkayı gösterir. Saydamlık kodda yazılmış
    /// bir sayı değil: perdenin opaklığı <c>PlaybackIdleVeilOpacity</c> belirtecinin kendisi
    /// ve o belirteç de ankanın çizildiği opaklıkla aynı kaynaktan geliyor. Kare geldiğinde
    /// zemin yine örtücüdür; iki fırça aynı renk, tek fark opaklık.
    ///
    /// Örtü iki kat olduğu için ikisi de ölçülüyor: pano perdesi ile panelin kendi yüzeyi.
    /// Yalnız pano saydamlaşsaydı kabuğun %90 örtücü zemini ankanın önünde kalırdı.
    ///
    /// Fırçaların kendisi dışarı taşınmıyor: Avalonia nesnelerinin özellikleri yalnız kendi iş
    /// parçacığında okunabiliyor, bu yüzden karşılaştırma konakta yapılıp sayı dönüyor.
    /// </summary>
    [Fact]
    public void Bos_onizlemenin_zemini_arkayi_gosterir()
    {
        var (idle, filled, veil, phoenix, sameColour, bareShell, filledShell) = Read((window, panel) =>
        {
            var empty = (ISolidColorBrush)panel.Stage.Background!;
            var emptyOpacity = empty.Opacity;
            var shellInk = ((ISolidColorBrush)panel.Shell.Background!).Color.A;

            FakeFrame(panel);
            var loaded = (ISolidColorBrush)panel.Stage.Background!;

            window.TryFindResource("PlaybackIdleVeilOpacity", out var token);
            window.TryFindResource("PhoenixOpacity", out var anka);
            return (emptyOpacity, loaded.Opacity, (double)token!, (double)anka!, empty.Color == loaded.Color,
                shellInk, ((ISolidColorBrush)panel.Shell.Background!).Color.A);
        });

        Assert.Equal(veil, idle, 6);
        Assert.True(veil < 1, $"boş önizlemenin perdesi örtücü: {veil:0.###}");
        Assert.Equal(phoenix, veil, 6);
        Assert.Equal(1.0, filled, 6);
        Assert.True(sameColour, "iki zemin aynı renk değil");

        Assert.Equal(0, bareShell);
        Assert.Equal(byte.MaxValue, filledShell);
    }

    // ---- T79/K5: paravan sekilleniyor ------------------------------------------------

    /// <summary>
    /// K5: paravan artık düz bir örtü değil. Seçilen biçim kenar sönümü: örtü altından
    /// yukarı doğru son çeyreğinde aynı rengin sıfır örtücülüklü haline geçiyor. Ölçü
    /// biçimin sayısını pinliyor: sönen çeyreğin başladığı durak <c>1 - PlaybackScrimEdge</c>,
    /// ve o oran panelin tek band payı oranıyla (<c>PlaybackHoverZoneShare</c>) aynı saydır —
    /// yeni bir oran uydurulmadı. Biçimin yüzeye gerçekten geçtiği de ölçülüyor: şeridin
    /// örtüsü bu fırçanın kendisidir.
    /// </summary>
    [Fact]
    public void Paravanin_kenari_soner()
    {
        var (onStrip, start, end, offsets, colours, edge, zone, solid) = Read((window, panel) =>
        {
            var brush = (LinearGradientBrush)window.FindResource("PlaybackScrimVeil")!;
            window.TryFindResource("PlaybackScrimEdge", out var share);
            window.TryFindResource("PlaybackHoverZoneShare", out var band);
            window.TryFindResource("PlaybackScrimColor", out var opaque);

            return (ReferenceEquals(brush, panel.Controls.Bar.Background),
                brush.StartPoint,
                brush.EndPoint,
                brush.GradientStops.Select(stop => stop.Offset).ToArray(),
                brush.GradientStops.Select(stop => stop.Color).ToArray(),
                (double)share!,
                (double)band!,
                (Color)opaque!);
        });

        Assert.True(onStrip, "şeridin örtüsü biçimli paravan değil");

        // Aşağıdan yukarı: örtücü taban şeridin metninin altında, sönen uç üst kenarda.
        Assert.Equal(new RelativePoint(0.5, 1, RelativeUnit.Relative), start);
        Assert.Equal(new RelativePoint(0.5, 0, RelativeUnit.Relative), end);

        Assert.Equal(new[] { 0, 1 - edge, 1 }, offsets);
        Assert.Equal(edge, zone, 6);

        Assert.Equal(solid, colours[0]);
        Assert.Equal(solid, colours[1]);

        // Sönen uç aynı renk: değişen tek şey örtücülük, kenarda ikinci bir renk yok.
        Assert.Equal(0, colours[2].A);
        Assert.Equal((solid.R, solid.G, solid.B), (colours[2].R, colours[2].G, colours[2].B));
    }

    // ---- T49: yaklasik onizleme rozeti ----------------------------------------------

    /// <summary>
    /// Ikinci bir yerlesim turu. Pencere gosterilmedigi icin <c>UpdateLayout</c> tek
    /// basina yeni tur acmiyor: ilk turdan sonra degisen gorunurluk ve metin ancak olcum
    /// elle yeniden kosturuldugunda desired/bounds degerlerine yansiyor.
    /// </summary>
    private static void Relayout(MainWindow window)
    {
        window.InvalidateMeasure();
        foreach (var part in window.GetVisualDescendants().OfType<Layoutable>()) part.InvalidateMeasure();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.Measure(WindowSize);
        root.Arrange(new Rect(WindowSize));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Panelin bos durumu kare varligina bakiyor; kare yolu gercek cizim turunda aciliyor
    /// ve olcumde cizim turu yok. Bu yuzden kapi dogrudan aciliyor: yuzeyin kendisi
    /// olcumun sahibi degil, burada yalniz "elimde kare var" hali kuruluyor.
    /// </summary>
    private static void FakeFrame(ComparisonPanel panel)
    {
        panel.Frames.Configure(new PixelSize(2560, 720));
        typeof(ComparisonSurface)
            .GetField("_hasFrame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(panel.Frames, true);
        panel.RefreshEmptyState();
    }

    private const string BadgeText = "Approximate preview \u00b7 CRF 21";

    [Fact]
    public void Rozet_yalniz_yaklasik_parcada_gorunur()
    {
        var (clip, full, curtain) = Read((window, panel) =>
        {
            FakeFrame(panel);

            panel.SetRightBadge(BadgeText);
            Relayout(window);
            var onClip = panel.ApproxBadge.IsVisible;

            panel.SetRightBadge(null);
            var onFull = panel.ApproxBadge.IsVisible;

            panel.SetRightBadge(BadgeText);
            panel.SetRightNotice("This part will be processed");
            var onCurtain = panel.ApproxBadge.IsVisible;

            return (onClip, onFull, onCurtain);
        });

        Assert.True(clip, "parca gosterilirken rozet yok");
        Assert.False(full, "tam ciktida rozet duruyor");
        Assert.False(curtain, "perde inmisken rozet duruyor");
    }

    [Fact]
    public void Rozet_panelin_sag_yarisindadir()
    {
        var (badgeLeft, half, stageWidth) = Read((window, panel) =>
        {
            FakeFrame(panel);
            panel.SetRightBadge(BadgeText);
            Relayout(window);

            var origin = panel.ApproxBadge.TranslatePoint(new Point(0, 0), panel.Stage) ?? default;
            return (origin.X, panel.Stage.Bounds.Width / 2, panel.ApproxBadge.Bounds.Width);
        });

        Assert.True(stageWidth > 0, $"rozet olculmedi: {stageWidth:0.#}");
        Assert.True(badgeLeft > half, $"rozetin sol kenari {badgeLeft:0.#}, panonun ortasi {half:0.#}");
    }

    /// <summary>
    /// T49/K2: metin panelde uretilmiyor. Barindiran taraf ne verirse ekranda o duruyor;
    /// panel sayiyi yeniden bicimlendirmiyor, ceviriyi ikinci kez uygulamiyor.
    /// </summary>
    [Fact]
    public void Rozet_metni_verildigi_gibi_gorunur()
    {
        var (english, turkish) = Read((window, panel) =>
        {
            FakeFrame(panel);
            panel.SetRightBadge(BadgeText);
            var en = panel.ApproxBadgeText.Text;

            panel.SetLanguage(true);
            panel.SetRightBadge("Yakla\u015f\u0131k \u00d6nizleme \u00b7 CRF 21");
            return (en, panel.ApproxBadgeText.Text);
        });

        Assert.Equal(BadgeText, english);
        Assert.Equal("Yakla\u015f\u0131k \u00d6nizleme \u00b7 CRF 21", turkish);
    }

    [Fact]
    public void Rozetin_iki_anahtari_sozlukte()
    {
        var turkish = Locales.Values("tr");

        Assert.Equal("Yakla\u015f\u0131k \u00f6nizleme", turkish["playback.approximate-preview"]);
        Assert.Equal("\u00d6nizleme \u00f6rne\u011fi kodlanamad\u0131", turkish["playback.sample-failed"]);
    }

    /// <summary>
    /// T49/K3 tuzagi: rozet fare olaylarini yutarsa panel kullanici rozetin ustunden
    /// gecerken inmez. Uc olcum: rozet ve metni isabet testine kapali, rozetten kalkan
    /// bir fare olayi panonun kendi isleyicisine ulasiyor, ve rozet gosterilirken inis
    /// sayaci hala karar verebiliyor.
    ///
    /// Gercek isabet testi burada olculemez: <c>InputHitTest</c> penceresiz kosuda
    /// panonun ortasinda bile <c>null</c> donuyor, yani cizim yuzeyi olmadan calismiyor.
    /// Olculen sey isabet testini surukleyen ozelligin kendisi ve olay yolu.
    /// </summary>
    [Fact]
    public void Rozet_fare_olaylarina_saydamdir()
    {
        var (badgeOpen, textOpen, reached, descends) = Read((window, panel) =>
        {
            FakeFrame(panel);
            panel.SetRightBadge(BadgeText);
            Relayout(window);

            var seen = 0;
            void Spy(object? sender, PointerEventArgs e) => seen++;
            panel.Stage.PointerMoved += Spy;
            panel.ApproxBadge.RaiseEvent(PointerCrossing(panel.ApproxBadge, InputElement.PointerMovedEvent));
            panel.Stage.PointerMoved -= Spy;

            WheelTo(window, panel, ShelterStage.Full);
            Settle(window);
            panel.PointerLeftWindow();
            var generation = panel.Descent.Generation;

            return (panel.ApproxBadge.IsHitTestVisible, panel.ApproxBadgeText.IsHitTestVisible,
                seen, panel.Descent.ShouldHide(generation));
        });

        Assert.False(badgeOpen, "rozet isabet testine acik");
        Assert.False(textOpen, "rozet metni isabet testine acik");
        Assert.Equal(1, reached);
        Assert.True(descends, "rozet gosterilirken inis sayaci karar veremedi");
    }

    /// <summary>
    /// T49/K4: secilen davranis kirpma. Rozet sarmiyor, paneli genisletmiyor; sigmayan
    /// metin uc noktayla kesiliyor. Tavan sag yarinin kendi genisliginden geliyor.
    /// </summary>
    [Fact]
    public void Uzun_rozet_metni_paneli_genisletmez()
    {
        var (shortPanel, longPanel, shortDesired, longDesired, badgeWidth, cap, half) =
            Read((window, panel) =>
            {
                FakeFrame(panel);

                panel.SetRightBadge(BadgeText);
                panel.InvalidateMeasure();
                Relayout(window);
                var narrowPanel = panel.Bounds.Width;
                var narrowDesired = panel.DesiredSize.Width;

                panel.SetRightBadge(new string('W', 400));
                panel.InvalidateMeasure();
                Relayout(window);

                return (narrowPanel, panel.Bounds.Width, narrowDesired, panel.DesiredSize.Width,
                    panel.ApproxBadge.Bounds.Width, panel.ApproxBadge.MaxWidth, panel.Stage.Bounds.Width / 2);
            });

        Assert.Equal(shortPanel, longPanel, 3);
        Assert.Equal(shortDesired, longDesired, 3);
        Assert.True(cap > 0 && cap < half, $"tavan {cap:0.#}, sag yari {half:0.#}");
        Assert.True(badgeWidth <= cap + 0.5, $"rozet {badgeWidth:0.#} > tavan {cap:0.#}");
    }
}
