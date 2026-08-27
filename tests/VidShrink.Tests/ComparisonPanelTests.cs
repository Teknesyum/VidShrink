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
/// <see cref="DispatcherTimer"/> ateşlenmez. Bu yüzden iki saniyelik bekleme duvar
/// saatiyle değil, sayacın kendi karar kapısı (<see cref="HoverZone.ShouldHide"/>) ve
/// zaman aşımının çağırdığı yol (<see cref="ComparisonPanel.Descend"/>) üstünden ölçülüyor.
/// </summary>
public sealed class ComparisonPanelTests
{
    private static readonly Size WindowSize = new(1560, 1060);

    private static T Read<T>(Func<MainWindow, ComparisonPanel, T> read) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.Width = double.NaN;
            window.Height = double.NaN;
            window.Measure(WindowSize);
            window.Arrange(new Rect(WindowSize));
            window.UpdateLayout();

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

    [Fact]
    public void Orta_kademe_bandindan_buyuk_pencereden_kucuktur()
    {
        var (band, mid, window) = Read((host, panel) =>
        {
            var bandWidth = panel.Bounds.Width;
            WheelTo(host, panel, ShelterStage.Mid);
            return (bandWidth, panel.StageTarget, OverlayBounds(panel));
        });

        Assert.True(mid.Width > band, $"orta {mid.Width:0.#} <= band {band:0.#}");
        Assert.True(mid.Width < window.Width, $"orta {mid.Width:0.#} >= pencere {window.Width:0.#}");
        Assert.True(mid.Height < window.Height, $"orta {mid.Height:0.#} >= pencere {window.Height:0.#}");
        Assert.True(mid.X > 0 && mid.Y > 0);
    }

    [Fact]
    public void Tam_kademe_pencerenin_tamamini_kaplar()
    {
        var (full, window) = Read((host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Full);
            return (panel.StageTarget, OverlayBounds(panel));
        });

        Assert.Equal(0, full.X, 6);
        Assert.Equal(0, full.Y, 6);
        Assert.Equal(window.Width, full.Width, 6);
        Assert.Equal(window.Height, full.Height, 6);
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

    [Fact]
    public void Cik_gir_cik_dizisinde_panel_inip_kalkmaz()
    {
        var (stale, current, staleDecides, currentDecides, stage) = Read((host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Mid);
            var target = panel.StageTarget;
            var outside = new Point(target.X / 2, target.Y / 2);

            panel.TrackPointer(outside);
            var first = panel.Descent.Generation;

            panel.TrackPointer(target.Center);
            panel.TrackPointer(outside);
            var second = panel.Descent.Generation;

            return (first, second, panel.Descent.ShouldHide(first), panel.Descent.ShouldHide(second), panel.Shelter);
        });

        Assert.True(current > stale, $"kuşak ilerlemedi: {stale} -> {current}");
        Assert.False(staleDecides);
        Assert.True(currentDecides);
        Assert.Equal(ShelterStage.Mid, stage);
    }

    [Fact]
    public void Fare_geri_girince_bekleyen_inis_kararini_uygulamaz()
    {
        var (stale, holds) = Read((host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Mid);
            var target = panel.StageTarget;

            panel.TrackPointer(new Point(target.X / 2, target.Y / 2));
            var pending = panel.Descent.Generation;

            panel.TrackPointer(target.Center);
            return (panel.Descent.ShouldHide(pending), panel.Shelter == ShelterStage.Mid);
        });

        Assert.False(stale);
        Assert.True(holds);
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

    [Fact]
    public void Fare_pencereden_cikinca_inis_sayaci_kurulur()
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

        Assert.True(armed, "çıkış olayı sayacı kurmadı");
        Assert.True(decides, "bekleyen tik inişe karar vermiyor");
        Assert.Equal(ShelterStage.Full, stage);
    }

    [Fact]
    public void Pencere_disina_cikis_hedef_sinira_gore_kararlidir()
    {
        var (afterExit, afterReturn) = Read((host, panel) =>
        {
            WheelTo(host, panel, ShelterStage.Mid);

            panel.TrackPointer(panel.StageTarget.Center);
            panel.PointerLeftWindow();
            var pending = panel.Descent.Generation;
            var leaves = panel.Descent.ShouldHide(pending);

            // Fare geri hedefin içine girdi: eski tik kuşağını kaybetti, panel inmez.
            panel.TrackPointer(panel.StageTarget.Center);
            return (leaves, panel.Descent.ShouldHide(pending));
        });

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

    [Fact]
    public void Inis_suresi_temadan_gelir()
    {
        var delay = AppHost.Run(() =>
        {
            var window = new MainWindow();
            return window.TryFindResource("PlaybackDescendDelay", out var value) && value is TimeSpan span
                ? span
                : TimeSpan.Zero;
        });

        Assert.Equal(TimeSpan.FromSeconds(2), delay);
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
    /// T52/K2: fare panele girince <b>panel</b> tema belirtecinin soyledigi kata cikar.
    /// Iki kat panel bandina sigmadigi icin panel ayni anda kok katmana terfi eder.
    /// Fare cikinca olcek hemen dusmez: inis kararini gecikmeli inis sayaci verir, sayac
    /// isini bitirince (Descend) panel taban boya doner.
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
        window.Measure(WindowSize);
        window.Arrange(new Rect(WindowSize));
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
        Assert.Equal("Yakla\u015f\u0131k \u00d6nizleme", LanguageCatalog.Localize("Approximate preview", true));
        Assert.Equal(
            "\u00d6nizleme \u00d6rne\u011fi Kodlanamad\u0131",
            LanguageCatalog.Localize("The preview sample could not be encoded", true));
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
