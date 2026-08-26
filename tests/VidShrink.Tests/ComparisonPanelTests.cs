using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Raw;
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
}
