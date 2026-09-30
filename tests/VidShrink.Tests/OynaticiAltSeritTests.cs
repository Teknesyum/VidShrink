using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

public sealed class OynaticiAltSeritTests
{
    private static readonly Avalonia.Input.Pointer Fare = new(7, PointerType.Mouse, true);

    private static string Klasor()
    {
        var path = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-a79dbf67d97f36f3d", "alt-serit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static PlayerView Ac(YolMotoru motor, out Window window, string? klasor)
    {
        var view = new PlayerView { EngineFactory = () => motor };
        if (klasor is not null) view.HistoryPath = () => Path.Combine(klasor, "player-history.json");
        window = new Window { Width = 960, Height = 540, Content = view };
        window.Show();
        var open = view.OpenAsync(Path.Combine(YolKanit.Folder, "yok-sahte.mp4"));
        DenetimSurucu.Pump(view, () => open.IsCompleted, 10);
        open.GetAwaiter().GetResult();
        DenetimSurucu.Wait(view, 0.3);
        return view;
    }

    private static void Kapat(PlayerView view, Window window, string? klasor)
    {
        view.Close();
        window.Close();
        if (klasor is not null && Directory.Exists(klasor)) Directory.Delete(klasor, true);
    }

    private static void Teker(PlayerView view, Interactive hedef, double centik, KeyModifiers mods)
    {
        var args = new PointerWheelEventArgs(
            hedef,
            Fare,
            view,
            new Point(1, 1),
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
            mods,
            new Vector(0, centik))
        {
            RoutedEvent = InputElement.PointerWheelChangedEvent
        };

        hedef.RaiseEvent(args);
        DenetimSurucu.Wait(view, 0.05);
    }

    private static T Bul<T>(PlayerView view, string ad) where T : Control
        => view.FindControl<T>(ad) ?? throw new InvalidOperationException(ad);

    [Fact]
    public void SureEtiketiToplamlaBaslarUzerindeKalaniGosterirTiklamaAnimsanir() => AppHost.Run(() =>
    {
        var klasor = Klasor();
        var dosya = Path.Combine(klasor, PlayerSettings.FileName);
        var view = Ac(new YolMotoru(), out var window, klasor);

        var ilk = view.SeritTimeText;
        view.SeritTimeHover = true;
        var uzerinde = view.SeritTimeText;
        view.SeritTimeHover = false;
        var diskteOnce = File.Exists(dosya);

        view.ToggleSeritTimeMode();
        var degisince = view.SeritTimeText;
        view.SeritTimeHover = true;
        var degisinceUzerinde = view.SeritTimeText;
        view.SeritTimeHover = false;
        var kayitli = PlayerSettings.Load(dosya).ShowRemaining;

        Assert.DoesNotContain("-", ilk);
        Assert.Contains("/ -", uzerinde);
        Assert.False(diskteOnce);
        Assert.Contains("/ -", degisince);
        Assert.DoesNotContain("-", degisinceUzerinde);
        Assert.True(kayitli);

        Kapat(view, window, null);
        var ikinci = Ac(new YolMotoru(), out var window2, klasor);
        var yeniden = ikinci.SeritTimeText;
        Assert.Contains("/ -", yeniden);
        Kapat(ikinci, window2, klasor);
        return 0;
    });

    [Fact]
    public void AyarYoluYokkenSureKipiDiskeYazilmaz() => AppHost.Run(() =>
    {
        var view = Ac(new YolMotoru(), out var window, null);
        view.ToggleSeritTimeMode();
        Assert.True(view.Settings.ShowRemaining);
        Assert.Contains("/ -", view.SeritTimeText);
        Kapat(view, window, null);

        var ikinci = Ac(new YolMotoru(), out var window2, null);
        Assert.False(ikinci.Settings.ShowRemaining);
        Assert.DoesNotContain("-", ikinci.SeritTimeText);
        Kapat(ikinci, window2, null);
        return 0;
    });

    [Fact]
    public void TekerAdimTablosu()
    {
        var mods = new[] { KeyModifiers.Control, KeyModifiers.None, KeyModifiers.Shift, KeyModifiers.Control | KeyModifiers.Shift };
        var konum = new[] { 0.1, 1, 10, 100 };
        var ses = new[] { 0.1, 1, 10, 100 };
        var hiz = new[] { 0.01, 0.1, 1, 10 };
        for (var i = 0; i < mods.Length; i++)
        {
            Assert.Equal(PlayerCommandKind.Seek, Keymap.ForWheel(1, mods[i]).Kind);
            Assert.Equal(konum[i], Keymap.ForWheel(1, mods[i]).Amount, 9);
            Assert.Equal(new PlayerCommand(PlayerCommandKind.Volume, ses[i]).Kind, Keymap.ForVolumeWheel(1, mods[i]).Kind);
            Assert.Equal(ses[i], Keymap.ForVolumeWheel(1, mods[i]).Amount, 9);
            Assert.Equal(-ses[i], Keymap.ForVolumeWheel(-1, mods[i]).Amount, 9);
            Assert.Equal(PlayerCommandKind.Speed, Keymap.ForSpeedWheel(1, mods[i]).Kind);
            Assert.Equal(hiz[i], Keymap.ForSpeedWheel(1, mods[i]).Amount, 9);
        }

        Assert.Equal(PlayerCommandKind.Zoom, Keymap.ForWheel(1, KeyModifiers.Alt).Kind);
    }

    [Fact]
    public void SesKaydiriciVeYongasiUzerindeTekerSesiDegistirir() => AppHost.Run(() =>
    {
        var klasor = Klasor();
        var view = Ac(new YolMotoru(), out var window, klasor);
        var kaydirici = Bul<Slider>(view, "SliderSeritVolume");
        var yonga = Bul<Control>(view, "ChipSeritVolume");
        view.Apply(new PlayerCommand(PlayerCommandKind.Volume, -50));
        var okumalar = new List<double> { view.VolumeLevel };

        Teker(view, kaydirici, 1, KeyModifiers.None);
        okumalar.Add(view.VolumeLevel);
        Teker(view, yonga, 1, KeyModifiers.Shift);
        okumalar.Add(view.VolumeLevel);
        Teker(view, kaydirici, 1, KeyModifiers.Control);
        okumalar.Add(view.VolumeLevel);
        Teker(view, yonga, -1, KeyModifiers.Control | KeyModifiers.Shift);
        okumalar.Add(view.VolumeLevel);

        var konum = view.PositionSeconds;
        GirdiSurucu.Wheel(view, 1, KeyModifiers.None);
        DenetimSurucu.Wait(view, 0.05);
        var disaridaSes = view.VolumeLevel;
        var disaridaKonum = view.PositionSeconds;

        Assert.Equal(new[] { 50, 51, 61, 61.1, 0 }, okumalar.Select(v => Math.Round(v, 6)).ToArray());
        Assert.Equal(0, disaridaSes);
        Assert.Equal(konum + 1, disaridaKonum, 6);
        Assert.Equal("0", view.SeritVolumeText);

        Kapat(view, window, klasor);
        return 0;
    });

    [Fact]
    public void HizKaydiriciVeYongasiUzerindeTekerHiziDegistirir() => AppHost.Run(() =>
    {
        var klasor = Klasor();
        var motor = new YolMotoru();
        var view = Ac(motor, out var window, klasor);
        var kaydirici = Bul<Slider>(view, "SliderSeritSpeed");
        var yonga = Bul<Control>(view, "ChipSeritSpeed");
        var okumalar = new List<double> { view.SpeedFactor };

        Teker(view, kaydirici, 1, KeyModifiers.None);
        okumalar.Add(view.SpeedFactor);
        Teker(view, yonga, 1, KeyModifiers.Control);
        okumalar.Add(view.SpeedFactor);
        Teker(view, kaydirici, 1, KeyModifiers.Shift);
        okumalar.Add(view.SpeedFactor);
        Teker(view, yonga, -1, KeyModifiers.Control | KeyModifiers.Shift);
        okumalar.Add(view.SpeedFactor);
        var ses = view.VolumeLevel;

        Assert.Equal(new[] { 1, 1.1, 1.11, 2.11, Keymap.MinimumSpeed }, okumalar.Select(v => Math.Round(v, 6)).ToArray());
        Assert.Equal(Keymap.MinimumSpeed, motor.Hiz, 6);
        Assert.Equal(100, ses);

        Kapat(view, window, klasor);
        return 0;
    });

    [Fact]
    public void HizAbDugmesiVeVTusuModlarArasindaGecerDegerAnimsanir() => AppHost.Run(() =>
    {
        var klasor = Klasor();
        var dosya = Path.Combine(klasor, PlayerSettings.FileName);
        var motor = new YolMotoru();
        var view = Ac(motor, out var window, klasor);
        var dugme = Bul<ToggleButton>(view, "BtnSeritSpeedAb");
        var iz = new List<string>();
        void Oku() => iz.Add((view.SpeedModeB ? "B" : "A") + (dugme.IsChecked == true ? "+" : "-") + YolKanit.N(view.SpeedFactor) + "/" + YolKanit.N(motor.Hiz));

        Oku();
        GirdiSurucu.Key(view, Key.V);
        DenetimSurucu.Wait(view, 0.05);
        Oku();
        view.Apply(new PlayerCommand(PlayerCommandKind.Speed, 0.2));
        DenetimSurucu.Wait(view, 0.05);
        Oku();
        var b = PlayerSettings.Load(dosya);
        GirdiSurucu.Key(view, Key.V);
        DenetimSurucu.Wait(view, 0.05);
        Oku();
        view.Apply(new PlayerCommand(PlayerCommandKind.Speed, -0.5));
        DenetimSurucu.Wait(view, 0.05);
        Oku();
        var a = PlayerSettings.Load(dosya);
        dugme.RaiseEvent(new RoutedEventArgs(Button.ClickEvent) { Source = dugme });
        DenetimSurucu.Wait(view, 0.05);
        Oku();

        Assert.Equal(new[] { "A-1/1", "B+1.8/1.8", "B+2/2", "A-1/1", "A-0.5/0.5", "B+2/2" }, iz);
        Assert.Equal(2, b.SpeedB, 6);
        Assert.Equal(PlayerSettings.DefaultSpeedA, b.SpeedA, 6);
        Assert.Equal(0.5, a.SpeedA, 6);
        Assert.Equal(2, a.SpeedB, 6);
        Kapat(view, window, null);

        var ikinciMotor = new YolMotoru();
        var ikinci = Ac(ikinciMotor, out var window2, klasor);
        var acilisKipB = ikinci.SpeedModeB;
        var acilisHiz = ikinciMotor.Hiz;
        GirdiSurucu.Key(ikinci, Key.V);
        DenetimSurucu.Wait(ikinci, 0.05);
        var yeniA = ikinci.SpeedFactor;
        GirdiSurucu.Key(ikinci, Key.V);
        DenetimSurucu.Wait(ikinci, 0.05);
        var yeniB = ikinci.SpeedFactor;
        Assert.True(acilisKipB);
        Assert.Equal(2, acilisHiz, 6);
        Assert.Equal(0.5, yeniA, 6);
        Assert.Equal(2, yeniB, 6);
        Kapat(ikinci, window2, klasor);
        return 0;
    });

    [Fact]
    public void VTusuKisayolTablosundaHizAbEylemine()
    {
        var satir = Assert.Single(Keymap.Rows, r => ReferenceEquals(r.Action, Keymap.SpeedAb));
        Assert.Equal(PlayerInputKind.Key, satir.Input.Kind);
        Assert.Equal(Key.V, satir.Input.Key);
        Assert.Equal(KeyModifiers.None, satir.Input.Modifiers);
        Assert.Equal(PlayerCommandKind.SpeedAb, Keymap.ForKey(Key.V, KeyModifiers.None, null).Kind);
        Assert.NotEqual(PlayerCommandKind.SpeedAb, Keymap.ForKey(Key.V, KeyModifiers.Control, null).Kind);
    }

    [Fact]
    public void SeritDugmeleriSeridinDikeyOrtasinda() => AppHost.Run(() =>
    {
        var view = Ac(new YolMotoru(), out var window, null);
        view.SeritZone.PointerAt(view.Bounds.Height - 1, view.Bounds.Height);
        DenetimSurucu.Pump(view, () => view.SeritRevealed, 2);
        DenetimSurucu.Wait(view, 0.5);
        var satir = Bul<Control>(view, "StripRow");
        var orta = satir.Bounds.Height / 2;
        var dugmeler = satir.GetVisualDescendants().OfType<Button>()
            .Where(b => b.TemplatedParent is null && b.IsEffectivelyVisible && b.Bounds.Height > 0)
            .ToList();
        var rapor = new StringBuilder();
        var sapanlar = new List<string>();
        foreach (var d in dugmeler)
        {
            var merkez = d.TranslatePoint(new Point(0, d.Bounds.Height / 2), satir)!.Value.Y;
            var ad = d.Name ?? d.GetType().Name;
            rapor.AppendLine($"{ad} merkez {YolKanit.N(merkez)} satir {YolKanit.N(orta)}");
            if (Math.Abs(merkez - orta) > 1) sapanlar.Add(ad + " " + YolKanit.N(merkez - orta));
        }

        Assert.True(dugmeler.Count >= 5, rapor.ToString());
        Assert.Contains(dugmeler, d => d.Name == "BtnSeritSpeedAb");
        Assert.True(sapanlar.Count == 0, string.Join(", ", sapanlar) + Environment.NewLine + rapor);
        Kapat(view, window, null);
        return 0;
    });

    [Fact]
    public void OnizlemeResmiBesKatVePencereyeSigar() => AppHost.Run(() =>
    {
        var view = Ac(new YolMotoru(), out var window, null);
        var genis = view.ThumbnailSize(10000, 10000);
        var dar = view.ThumbnailSize(300, 10000);
        var alcak = view.ThumbnailSize(10000, 90);
        Kapat(view, window, null);

        Assert.Equal(PlayerView.ThumbnailWidth * 5, genis.Width, 6);
        Assert.Equal(PlayerView.ThumbnailHeight * 5, genis.Height, 6);
        Assert.Equal(300, dar.Width, 6);
        Assert.True(alcak.Height <= 90 + 1e-6, YolKanit.N(alcak.Height));
        return 0;
    });
}
