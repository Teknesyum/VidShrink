using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Sekil = Avalonia.Controls.Shapes.Path;
using VidShrink.App.Recorder;
using Xunit;

namespace VidShrink.Tests;

public sealed class KaydediciYapismaTests
{
    private static readonly PixelRect Ekran = new(0, 0, 1920, 1080);

    private static readonly PixelRect Bolge = new(100, 100, 640, 360);

    private static readonly PixelRect Pencere = new(800, 500, 400, 300);

    private const int Esik = 16;

    private static IReadOnlyList<PixelRect> Hedefler(params PixelRect[] pencereler)
        => RegionSnap.Targets(new[] { Ekran }, pencereler);

    private static (PixelRect Sonuc, SnapResult Yapisma) Surukle(RegionGrip tutamak, int dx, int dy, bool kapali = false,
        PixelRect? bolge = null, IReadOnlyList<PixelRect>? hedefler = null, PixelRect? sinir = null)
    {
        var baslangic = bolge ?? Bolge;
        var yapisma = RegionSnap.Drag(baslangic, tutamak, new PixelVector(dx, dy), hedefler ?? Hedefler(Pencere), Esik, kapali);
        return (RegionEdit.Drag(baslangic, tutamak, yapisma.Delta, null, sinir ?? Ekran), yapisma);
    }

    [Fact]
    public void EsikIcindeKenarPencereyeYapisir()
    {
        var (sonuc, yapisma) = Surukle(RegionGrip.Right, 50, 0, hedefler: Hedefler(new PixelRect(800, 50, 400, 500)));

        Assert.Equal(new PixelVector(60, 0), yapisma.Delta);
        Assert.Equal(new SnapGuide(true, 800), yapisma.X);
        Assert.Null(yapisma.Y);
        Assert.Equal(new PixelRect(100, 100, 700, 360), sonuc);

        var cizgi = Assert.Single(RegionSnap.Lines(sonuc, yapisma.X, yapisma.Y, 4));
        Assert.Equal(new PixelRect(798, 100, 4, 360), cizgi);
    }

    [Fact]
    public void EsikDisindaYapismaz()
    {
        var (sonuc, yapisma) = Surukle(RegionGrip.Right, 20, 0, hedefler: Hedefler(new PixelRect(800, 50, 400, 500)));

        Assert.Equal(new PixelVector(20, 0), yapisma.Delta);
        Assert.Null(yapisma.X);
        Assert.Equal(new PixelRect(100, 100, 660, 360), sonuc);
        Assert.Empty(RegionSnap.Lines(sonuc, yapisma.X, yapisma.Y, 4));

        Assert.Equal(816, RegionSnap.Nearest(800, 0, 100, true, new[] { new PixelRect(816, 0, 10, 100) }, Esik));
        Assert.Null(RegionSnap.Nearest(800, 0, 100, true, new[] { new PixelRect(817, 0, 10, 100) }, Esik));
    }

    [Fact]
    public void AltYapismayiKapatir()
    {
        var hedef = Hedefler(new PixelRect(800, 50, 400, 500));
        var (sonuc, yapisma) = Surukle(RegionGrip.Right, 50, 0, kapali: true, hedefler: hedef);

        Assert.Equal(new PixelVector(50, 0), yapisma.Delta);
        Assert.Null(yapisma.X);
        Assert.Null(yapisma.Y);
        Assert.Equal(new PixelRect(100, 100, 690, 360), sonuc);

        var nokta = RegionSnap.Point(new PixelPoint(795, 300), hedef, Esik, true);
        Assert.Equal(new PixelPoint(795, 300), nokta.Point);
        Assert.Null(nokta.X);
    }

    [Fact]
    public void KoseIkiEksendeYapisir()
    {
        var (sonuc, yapisma) = Surukle(RegionGrip.BottomRight, 55, 35);

        Assert.Equal(new SnapGuide(true, 800), yapisma.X);
        Assert.Equal(new SnapGuide(false, 500), yapisma.Y);
        Assert.Equal(new PixelRect(100, 100, 700, 400), sonuc);
        Assert.Equal(2, RegionSnap.Lines(sonuc, yapisma.X, yapisma.Y, 4).Count);

        var nokta = RegionSnap.Point(new PixelPoint(1195, 806), Hedefler(Pencere), Esik, false);
        Assert.Equal(new PixelPoint(1200, 800), nokta.Point);
    }

    [Fact]
    public void EkranKenarinaYapisir()
    {
        var (sonuc, yapisma) = Surukle(RegionGrip.Left, -90, 0, hedefler: Hedefler());

        Assert.Equal(new SnapGuide(true, 0), yapisma.X);
        Assert.Equal(0, sonuc.X);
        Assert.Equal(Bolge.Right, sonuc.Right);
    }

    [Fact]
    public void TasimaYakinKenariSecer()
    {
        var (sonuc, yapisma) = Surukle(RegionGrip.Move, 52, 0, hedefler: Hedefler(new PixelRect(800, 50, 400, 500)));

        Assert.Equal(new SnapGuide(true, 800), yapisma.X);
        Assert.Equal(new PixelRect(160, 100, 640, 360), sonuc);
    }

    [Fact]
    public void UzaktakiPencereninCizgisiCekmez()
    {
        var uzak = new PixelRect(800, 700, 400, 300);
        var (_, yapisma) = Surukle(RegionGrip.Right, 55, 0, hedefler: Hedefler(uzak));

        Assert.Null(yapisma.X);
        Assert.Equal(new PixelVector(55, 0), yapisma.Delta);
    }

    [Fact]
    public void CokluMonitorNegatifKoordinattaYapisir()
    {
        var sol = new PixelRect(-1920, 0, 1920, 1080);
        var sag = new PixelRect(0, 0, 2560, 1440);
        var masaustu = RegionDraw.Desktop(new[] { sol, sag });
        var hedef = RegionSnap.Targets(new[] { sol, sag }, new[] { new PixelRect(-1500, -8, 900, 600) });
        var bolge = new PixelRect(-1000, 200, 640, 360);

        var (sagKenar, y1) = Surukle(RegionGrip.Right, 350, 0, bolge: bolge, hedefler: hedef, sinir: masaustu);
        Assert.Equal(new SnapGuide(true, 0), y1.X);
        Assert.Equal(0, sagKenar.Right);

        var (solKenar, y2) = Surukle(RegionGrip.Left, -910, 0, bolge: bolge, hedefler: hedef, sinir: masaustu);
        Assert.Equal(new SnapGuide(true, -1920), y2.X);
        Assert.Equal(-1920, solKenar.X);

        var (ust, y3) = Surukle(RegionGrip.TopLeft, -495, -195, bolge: bolge, hedefler: hedef, sinir: masaustu);
        Assert.Equal(new SnapGuide(true, -1500), y3.X);
        Assert.Equal(new SnapGuide(false, 0), y3.Y);
        Assert.Equal(new PixelPoint(-1500, 0), ust.Position);

        var nokta = RegionSnap.Point(new PixelPoint(-1506, 590), hedef, Esik, false);
        Assert.Equal(new PixelPoint(-1500, 592), nokta.Point);
    }

    [Fact]
    public void PencereCerceveleriKendiniGizliyiVeOrtulenleriEler()
    {
        var ust = new PixelRect(0, 0, 1000, 800);
        var pencereler = new[]
        {
            new WindowEntry("Tarayıcı", true, false, false, 10, false, ust),
            new WindowEntry("Arkada kalan", true, false, false, 10, false, new PixelRect(100, 100, 300, 300)),
            new WindowEntry("Yarısı açık", true, false, false, 10, false, new PixelRect(900, 100, 400, 300)),
            new WindowEntry("VidShrink", true, false, false, 99, false, new PixelRect(1200, 0, 300, 300)),
            new WindowEntry("Simge", true, false, false, 10, true, new PixelRect(1300, 0, 300, 300)),
            new WindowEntry("Araç", true, false, true, 10, false, new PixelRect(1400, 0, 300, 300)),
            new WindowEntry("Başka masaüstü", true, true, false, 10, false, new PixelRect(1500, 0, 300, 300)),
            new WindowEntry("Boyutsuz", true, false, false, 10, false)
        };

        Assert.Equal(new[] { ust, new PixelRect(900, 100, 400, 300) }, RecorderWindows.Frames(pencereler, 99));
    }

    [Fact]
    public void MacNoktaDikdortgeniEkranOlcegiyleCevrilir()
    {
        var ekranlar = new List<(PixelRect, double)>
        {
            (new PixelRect(0, 0, 2880, 1800), 2),
            (new PixelRect(-1920, 0, 1920, 1080), 1)
        };

        Assert.Equal(new PixelRect(200, 100, 800, 600),
            RecorderWindows.FromPoints(new DesktopWindow("a", "1", 100, 50, 400, 300), ekranlar));
        Assert.Equal(new PixelRect(-1800, 10, 400, 300),
            RecorderWindows.FromPoints(new DesktopWindow("b", "2", -1800, 10, 400, 300), ekranlar));
        Assert.Null(RecorderWindows.FromPoints(new DesktopWindow("c", "3", 5000, 5000, 10, 10), ekranlar));
    }

    [Fact]
    public void DuzenleyiciDugmeleriKareVeBelirtectenGelir()
    {
        var olcu = AppHost.Run(() =>
        {
            var duzenleyici = new RecorderRegionEditor(new PixelRect(0, 0, 640, 360), null);
            double Deger(string anahtar) => duzenleyici.TryFindResource(anahtar, out var v) && v is double d ? d : double.NaN;
            CornerRadius Kose(string anahtar) => duzenleyici.TryFindResource(anahtar, out var v) && v is CornerRadius c ? c : default;
            var dugmeler = new[] { duzenleyici.BtnStart, duzenleyici.BtnSettings, duzenleyici.BtnPause, duzenleyici.BtnResume, duzenleyici.BtnStop, duzenleyici.BtnSnapshot, duzenleyici.BtnHide, duzenleyici.BtnClose };
            var sonuc = (
                boy: Deger("FieldMinHeight"),
                simge: Deger("IconSizeMd"),
                kose: Kose("RadiusSquare"),
                dugmeler: dugmeler.Select(b => (b.Width, b.Height, b.CornerRadius,
                    simge: ((Sekil)b.Content!).Width, simgeY: ((Sekil)b.Content!).Height)).ToArray());
            duzenleyici.CloseQuietly();
            return sonuc;
        });

        Assert.Equal(40, olcu.boy);
        Assert.Equal(8, olcu.dugmeler.Length);
        Assert.All(olcu.dugmeler, d =>
        {
            Assert.Equal(olcu.boy, d.Width);
            Assert.Equal(olcu.boy, d.Height);
            Assert.Equal(olcu.kose, d.CornerRadius);
            Assert.Equal(olcu.simge, d.simge);
            Assert.Equal(olcu.simge, d.simgeY);
        });
    }
}
