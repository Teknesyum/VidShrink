using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// OYA-47: doldur kipinin ölçeği. Saf hesap; pencere açılmaz.
/// </summary>
public sealed class DoldurmaOlcegiTests
{
    [Theory]
    [InlineData(1920, 1080, 800, 600)]
    [InlineData(640, 480, 1600, 500)]
    [InlineData(1000, 1000, 300, 700)]
    public void DoldurKipindeKarePanoyuKaplarSigdirdaPanoyaSigar(double kaynakEn, double kaynakBoy, double panoEn, double panoBoy)
    {
        var zoom = new ZoomGesture();
        zoom.SetSource(kaynakEn, kaynakBoy);
        zoom.SetViewport(panoEn, panoBoy);

        Assert.False(zoom.Fill);
        Assert.True(kaynakEn * zoom.BaseScale <= panoEn + 1e-6 && kaynakBoy * zoom.BaseScale <= panoBoy + 1e-6);

        zoom.Fill = true;
        var en = kaynakEn * zoom.BaseScale;
        var boy = kaynakBoy * zoom.BaseScale;
        Assert.True(en >= panoEn - 1e-6 && boy >= panoBoy - 1e-6, $"kare {en}x{boy}, pano {panoEn}x{panoBoy}");
        Assert.True(Math.Abs(en - panoEn) < 1e-6 || Math.Abs(boy - panoBoy) < 1e-6, $"kare {en}x{boy} panodan gereksiz büyük");
    }
}

/// <summary>
/// OYA-53: dokunma hareketinin ayrımı. Saf mantık; gerçek dokunmatik ekran bu makinede yok,
/// ayrım üretilmiş koordinatlarla ölçülür.
/// </summary>
public sealed class DokunmaHareketiTests
{
    private const double Esik = ClickArbiter.DragThresholdDip;

    [Fact]
    public void EsikAltindaKalanTekParmakDokunustur()
    {
        var h = new DokunmaHareketi(Esik);
        h.Bas(1, 100, 100);
        Assert.Equal(DokunmaKipi.Yok, h.Oyna(1, 101, 101).Kip);
        Assert.True(h.Birak(1));
    }

    [Fact]
    public void YatayKaydirmaAtlamaDikeyKaydirmaSesOlur()
    {
        var yatay = new DokunmaHareketi(Esik);
        yatay.Bas(1, 100, 100);
        var ilk = yatay.Oyna(1, 130, 105);
        Assert.Equal(DokunmaKipi.Atlama, ilk.Kip);
        Assert.Equal(30, ilk.Miktar, 6);
        var ikinci = yatay.Oyna(1, 120, 160);
        Assert.Equal(DokunmaKipi.Atlama, ikinci.Kip);
        Assert.Equal(-10, ikinci.Miktar, 6);
        Assert.False(yatay.Birak(1));

        var dikey = new DokunmaHareketi(Esik);
        dikey.Bas(1, 100, 100);
        var yukari = dikey.Oyna(1, 103, 60);
        Assert.Equal(DokunmaKipi.Ses, yukari.Kip);
        Assert.Equal(40, yukari.Miktar, 6);
        var asagi = dikey.Oyna(1, 200, 80);
        Assert.Equal(DokunmaKipi.Ses, asagi.Kip);
        Assert.Equal(-20, asagi.Miktar, 6);
        Assert.False(dikey.Birak(1));
    }

    [Fact]
    public void IkiParmakKistirmaOraniVerirVeAtlamaYapmaz()
    {
        var h = new DokunmaHareketi(Esik);
        h.Bas(1, 100, 100);
        h.Bas(2, 200, 100);
        var acilan = h.Oyna(2, 300, 100);
        Assert.Equal(DokunmaKipi.Kistirma, acilan.Kip);
        Assert.Equal(2, acilan.Miktar, 6);
        Assert.Equal(200, h.OrtaX, 6);
        var kapanan = h.Oyna(1, 250, 100);
        Assert.Equal(0.5, kapanan.Miktar, 6);

        Assert.False(h.Birak(2));
        Assert.Equal(DokunmaKipi.Yok, h.Oyna(1, 500, 100).Kip);
        Assert.False(h.Birak(1));
    }

    [Fact]
    public void TanimadigiParmakYokSayilir()
    {
        var h = new DokunmaHareketi(Esik);
        Assert.Equal(DokunmaKipi.Yok, h.Oyna(7, 10, 10).Kip);
        Assert.False(h.Birak(7));
    }
}

/// <summary>
/// OYA-47 ve OYA-53 oynatıcıda: D tuşu sığdır ile doldur arasında geçer, üretilmiş dokunma
/// olayları (<see cref="PointerType.Touch"/>) atlama, ses ve yakınlaştırmayı sürer, fare
/// davranışı değişmez. Motor sahte (<see cref="KareMotoru"/>); gerçek dokunmatik ekran ölçülmedi.
/// </summary>
public sealed class OynaticiDoldurmaTests
{
    private static PlayerView Ac(out Window pencere, KareMotoru? motor = null)
    {
        var gecmis = Path.Combine(KarePanoKanit.Folder, "gecmis-doldurma", "gecmis.json");
        Directory.CreateDirectory(Path.GetDirectoryName(gecmis)!);
        var view = new PlayerView { HistoryPath = () => gecmis };
        if (motor is not null) view.EngineFactory = () => motor;
        pencere = new Window { Width = 640, Height = 480, Content = view };
        pencere.Show();
        DenetimSurucu.Wait(view, 0.1);
        return view;
    }

    [Fact]
    public void DTusuKareyiPanoyuKaplayacakBicimdeBuyuturVeGeriSigdirir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var view = Ac(out var pencere, new KareMotoru());
            try
            {
                var acilis = view.OpenAsync(KarePanoKanit.Sahte("doldurma.mp4"));
                DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
                acilis.GetAwaiter().GetResult();
                DenetimSurucu.Pump(view, () => view.Frame.Source is not null, 5);
                DenetimSurucu.Wait(view, 0.2);

                var surface = view.FindControl<Panel>("Surface")!;
                var pano = surface.Bounds.Size;
                var sigdir = new Size(view.Frame.Width, view.Frame.Height);
                GirdiSurucu.Key(view, Key.D);
                DenetimSurucu.Wait(view, 0.1);
                var doldur = new Size(view.Frame.Width, view.Frame.Height);
                var doluKip = view.FillMode;
                var iz = view.Trace[^1];
                GirdiSurucu.Key(view, Key.D);
                DenetimSurucu.Wait(view, 0.1);
                var geri = new Size(view.Frame.Width, view.Frame.Height);
                var bos = view.FillMode;
                view.Close();
                pencere.Close();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                return (pano, sigdir, doldur, doluKip, iz, geri, bos);
            }
            finally
            {
                KarePanoKanit.Kapat("doldurma.mp4", "gecmis-doldurma");
            }
        });

        var (pano, sigdir, doldur, doluKip, iz, geri, bos) = sonuc;
        var not = $"pano {pano}, sigdir {sigdir}, doldur {doldur}, geri {geri}";
        Assert.True(pano.Width > 0 && pano.Height > 0, not);
        Assert.True(sigdir.Width <= pano.Width + 0.5 && sigdir.Height <= pano.Height + 0.5, not);
        Assert.True(sigdir.Width < pano.Width - 0.5 || sigdir.Height < pano.Height - 0.5, "sigdirda bant yok: " + not);
        Assert.True(doluKip, not);
        Assert.Equal("fill -> True", iz);
        Assert.True(doldur.Width >= pano.Width - 0.5 && doldur.Height >= pano.Height - 0.5, "doldur panoyu kaplamadi: " + not);
        Assert.False(bos);
        Assert.Equal(sigdir, geri);
    }

    private static readonly Pointer Fare = new(90, PointerType.Mouse, true);

    private static Pointer Parmak(int id) => new(id, PointerType.Touch, false);

    private static PointerPointProperties Basili => new(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);

    private static void Bas(PlayerView view, Pointer p, double x, double y)
        => view.RaiseEvent(new PointerPressedEventArgs(view, p, view, new Point(x, y), 0, Basili, KeyModifiers.None, 1)
        {
            RoutedEvent = InputElement.PointerPressedEvent
        });

    private static void Oyna(PlayerView view, Pointer p, double x, double y)
        => view.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, view, p, view, new Point(x, y), 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));

    private static void Birak(PlayerView view, Pointer p, double x, double y)
        => view.RaiseEvent(new PointerReleasedEventArgs(view, p, view, new Point(x, y), 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left)
        {
            RoutedEvent = InputElement.PointerReleasedEvent
        });

    [Fact]
    public void UretilmisDokunmaOlaylariAtlamaSesVeYakinlastirmaSurer()
    {
        var olcum = AppHost.Run(() =>
        {
            var view = Ac(out var pencere);
            view.Seek.Duration = 100000;
            view.Seek.GoTo(1000);
            view.Apply(new PlayerCommand(PlayerCommandKind.Volume, -50));
            var surface = view.FindControl<Panel>("Surface")!;
            var en = surface.Bounds.Width;
            var boy = surface.Bounds.Height;

            var konum = view.PositionSeconds;
            Bas(view, Parmak(11), 100, 200);
            Oyna(view, Parmak(11), 100 + en / 2, 205);
            Birak(view, Parmak(11), 100 + en / 2, 205);
            var atlama = view.PositionSeconds - konum;

            var ses = view.VolumeLevel;
            Bas(view, Parmak(12), 300, 300);
            Oyna(view, Parmak(12), 302, 300 - boy / 10);
            Birak(view, Parmak(12), 302, 300 - boy / 10);
            var sesFarki = view.VolumeLevel - ses;
            var konumSesSonrasi = view.PositionSeconds;

            var olcek = view.ZoomScale;
            Bas(view, Parmak(13), 200, 200);
            Bas(view, Parmak(14), 260, 200);
            Oyna(view, Parmak(14), 320, 200);
            var kistirma = view.ZoomScale;
            Birak(view, Parmak(14), 320, 200);
            Birak(view, Parmak(13), 200, 200);

            var oynatma = view.IsPlaying;
            Bas(view, Parmak(15), 200, 200);
            Birak(view, Parmak(15), 200, 200);
            var dokunus = view.IsPlaying;

            view.NativeMoveDrag = false;
            var fareKonum = view.PositionSeconds;
            var fareSes = view.VolumeLevel;
            Bas(view, Fare, 100, 100);
            Oyna(view, Fare, 300, 100);
            Birak(view, Fare, 300, 100);
            var fareIz = string.Join(" | ", view.Trace.TakeLast(4));
            var fareAtlama = view.PositionSeconds - fareKonum;
            var fareSesFarki = view.VolumeLevel - fareSes;

            view.Close();
            pencere.Close();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            KarePanoKanit.Kapat("gecmis-doldurma");
            return (en, boy, atlama, sesFarki, konumSes: konumSesSonrasi - konum - atlama, olcek, kistirma, oynatma, dokunus, fareIz, fareAtlama, fareSesFarki,
                beklenenAtlama: Keymap.Steps.Of(SeekStep.Medium) / 2);
        });

        var not = olcum.ToString();
        Assert.True(olcum.en > 0 && olcum.boy > 0, not);
        Assert.Equal(olcum.beklenenAtlama, olcum.atlama, 3);
        Assert.True(olcum.sesFarki > 0, not);
        Assert.Equal(0, olcum.konumSes, 6);
        Assert.True(olcum.kistirma > olcum.olcek + 0.5, not);
        Assert.NotEqual(olcum.oynatma, olcum.dokunus);
        Assert.Equal(0, olcum.fareAtlama, 6);
        Assert.Equal(0, olcum.fareSesFarki, 6);
        Assert.Contains("drag -> ", olcum.fareIz, StringComparison.Ordinal);
    }
}
