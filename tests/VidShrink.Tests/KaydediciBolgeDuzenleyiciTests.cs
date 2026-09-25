using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// Çizimden sonra ekranda kalan bölge düzenleyicisi. Saf hesaplar <see cref="RegionEdit"/>
/// üzerinden, görünüm bağlantısı sahte <see cref="IRegionEditorHost"/> ile ölçülüyor.
/// </summary>
public sealed class KaydediciBolgeDuzenleyiciTests
{
    private static readonly PixelRect Ekran = new(0, 0, 1920, 1080);

    private static readonly PixelRect Bolge = new(100, 100, 640, 360);

    private static readonly double OnAltiDokuz = 16 / 9.0;

    private static PixelRect Surukle(RegionGrip tutamak, int dx, int dy, double? oran = null, PixelRect? bolge = null)
        => RegionEdit.Drag(bolge ?? Bolge, tutamak, new PixelVector(dx, dy), oran, Ekran);

    [Fact]
    public void TasimaBoyuKorurVeEkrandaTutar()
    {
        Assert.Equal(new PixelRect(150, 70, 640, 360), Surukle(RegionGrip.Move, 50, -30));
        Assert.Equal(new PixelRect(1280, 720, 640, 360), Surukle(RegionGrip.Move, 2000, 2000));
        Assert.Equal(new PixelRect(0, 0, 640, 360), Surukle(RegionGrip.Move, -500, -500));
        Assert.Equal(Bolge, Surukle(RegionGrip.None, 50, 50));
    }

    [Fact]
    public void TutamakKarsiKenariSabitTutarVeCiftBoyaIner()
    {
        Assert.Equal(new PixelRect(100, 100, 740, 410), Surukle(RegionGrip.BottomRight, 101, 51));
        Assert.Equal(new PixelRect(50, 100, 690, 360), Surukle(RegionGrip.Left, -50, 999));
        Assert.Equal(new PixelRect(100, 60, 640, 400), Surukle(RegionGrip.Top, 999, -40));
        Assert.Equal(new PixelRect(708, 100, RegionEdit.MinSide, 360), Surukle(RegionGrip.Left, 700, 0));
        Assert.Equal(new PixelRect(100, 100, 1820, 360), Surukle(RegionGrip.Right, 5000, 0));
        Assert.Equal(new PixelRect(0, 0, 740, 460), Surukle(RegionGrip.TopLeft, -500, -500));
    }

    [Fact]
    public void OranKilidiBoyutlandirmadaKorunur()
    {
        var kose = Surukle(RegionGrip.BottomRight, 160, 0, OnAltiDokuz);
        var kenar = Surukle(RegionGrip.Right, 160, 0, OnAltiDokuz);
        var sinirda = Surukle(RegionGrip.BottomRight, 1000, 1000, OnAltiDokuz, new PixelRect(1000, 500, 640, 360));
        var solUst = Surukle(RegionGrip.TopLeft, -160, -90, OnAltiDokuz);

        Assert.Equal(new PixelRect(100, 100, 800, 450), kose);
        Assert.Equal(new PixelRect(100, 55, 800, 450), kenar);
        Assert.Equal(new PixelRect(1000, 500, 920, 518), sinirda);
        Assert.Equal(new PixelRect(0, 44, 740, 416), solUst);
        foreach (var r in new[] { kose, kenar, sinirda, solUst })
        {
            Assert.InRange(r.Width / (double)r.Height, OnAltiDokuz - 0.01, OnAltiDokuz + 0.01);
            Assert.True(r.X >= Ekran.X && r.Y >= Ekran.Y && r.Right <= Ekran.Right && r.Bottom <= Ekran.Bottom, r.ToString());
        }
    }

    [Fact]
    public void SinirdanBuyukBolgeSinirinIcineSigar()
    {
        Assert.Equal(new PixelRect(0, 0, 1920, 1080), RegionEdit.Fit(new PixelRect(-50, -50, 4000, 3000), Ekran));
        Assert.Equal(new PixelRect(1280, 0, 640, 360), RegionEdit.Fit(new PixelRect(1500, -20, 640, 360), Ekran));
    }

    [Fact]
    public void ImlecinAltindakiTutamakBulunur()
    {
        Assert.Equal(RegionGrip.TopLeft, RegionEdit.Hit(new PixelPoint(100, 100), Bolge, 4, 12));
        Assert.Equal(RegionGrip.Top, RegionEdit.Hit(new PixelPoint(420, 100), Bolge, 4, 12));
        Assert.Equal(RegionGrip.Right, RegionEdit.Hit(new PixelPoint(740, 280), Bolge, 4, 12));
        Assert.Equal(RegionGrip.BottomRight, RegionEdit.Hit(new PixelPoint(745, 465), Bolge, 4, 12));
        Assert.Equal(RegionGrip.Move, RegionEdit.Hit(new PixelPoint(300, 103), Bolge, 4, 12));
        Assert.Equal(RegionGrip.Move, RegionEdit.Hit(new PixelPoint(98, 300), Bolge, 4, 12));
        Assert.Equal(RegionGrip.None, RegionEdit.Hit(new PixelPoint(300, 104), Bolge, 4, 12));
        Assert.Equal(RegionGrip.None, RegionEdit.Hit(new PixelPoint(400, 280), Bolge, 4, 12));
        Assert.Equal(RegionGrip.None, RegionEdit.Hit(new PixelPoint(50, 50), Bolge, 4, 12));
    }

    [Fact]
    public void AracPaneliUsteYerYoksaAltaOradaDaYoksaIceGecer()
    {
        var panel = new PixelSize(300, 40);

        Assert.Equal(new PixelPoint(100, 50), RegionEdit.Toolbar(Bolge, panel, 10, Ekran));
        Assert.Equal(new PixelPoint(100, 390), RegionEdit.Toolbar(new PixelRect(100, 20, 640, 360), panel, 10, Ekran));
        Assert.Equal(new PixelPoint(0, 10), RegionEdit.Toolbar(Ekran, panel, 10, Ekran));
        Assert.Equal(new PixelPoint(1620, 450), RegionEdit.Toolbar(new PixelRect(1800, 500, 100, 100), panel, 10, Ekran));
        Assert.Equal(new PixelPoint(-1900, 215),
            RegionEdit.Toolbar(new PixelRect(-1900, 5, 200, 200), panel, 10, new PixelRect(-1920, 0, 1920, 1080)));
    }

    [Fact]
    public void PencereBicimiHalkaTutamakVePaneldenOlusurIcAlanBos()
    {
        var panel = new PixelRect(100, 50, 300, 40);
        var koken = new PixelPoint(-10, -20);
        var parcalar = RegionEdit.Shape(Bolge, 4, 12, panel, koken);

        Assert.Equal(4 + 8 + 1, parcalar.Count);
        Assert.Equal(new PixelRect(106, 116, 648, 8), parcalar[0]);
        Assert.Contains(new PixelRect(744, 474, 12, 12), parcalar);
        Assert.Equal(new PixelRect(110, 70, 300, 40), parcalar[^1]);

        bool Kapsar(int x, int y) => parcalar.Any(p => x >= p.X && x < p.Right && y >= p.Y && y < p.Bottom);
        Assert.False(Kapsar(430, 300));
        Assert.False(Kapsar(120, 300));
        Assert.True(Kapsar(110, 300));
        Assert.True(Kapsar(750, 300));
    }

    [Fact]
    public void BolgeninEkraniOrtasindanBulunur()
    {
        var ekranlar = new[] { Ekran, new PixelRect(1920, 0, 2560, 1440) };
        var masaustu = RegionDraw.Desktop(ekranlar);

        Assert.Equal(ekranlar[1], RegionEdit.ScreenOf(new PixelRect(2000, 100, 400, 300), ekranlar, masaustu));
        Assert.Equal(ekranlar[1], RegionEdit.ScreenOf(new PixelRect(1800, 100, 400, 300), ekranlar, masaustu));
        Assert.Equal(ekranlar[0], RegionEdit.ScreenOf(new PixelRect(1500, 1000, 400, 300), ekranlar, masaustu));
        Assert.Equal(masaustu, RegionEdit.ScreenOf(new PixelRect(9000, 9000, 10, 10), ekranlar, masaustu));
    }

    [Fact]
    public void ImlecTutamagaGoreSecilir()
    {
        Assert.Equal(StandardCursorType.SizeAll, RecorderRegionEditor.CursorFor(RegionGrip.Move));
        Assert.Equal(StandardCursorType.SizeWestEast, RecorderRegionEditor.CursorFor(RegionGrip.Left));
        Assert.Equal(StandardCursorType.SizeNorthSouth, RecorderRegionEditor.CursorFor(RegionGrip.Bottom));
        Assert.Equal(StandardCursorType.BottomRightCorner, RecorderRegionEditor.CursorFor(RegionGrip.BottomRight));
        Assert.Equal(StandardCursorType.Arrow, RecorderRegionEditor.CursorFor(RegionGrip.None));
    }

    [Fact]
    public void DuzenleyiciKayitSurerkenGizlenirHedefDegisinceKapanir()
    {
        Assert.Equal(RegionEditorState.Shown, RecorderView.EditorWanted(true, true, true, false));
        Assert.Equal(RegionEditorState.Hidden, RecorderView.EditorWanted(true, true, true, true));
        Assert.Equal(RegionEditorState.Closed, RecorderView.EditorWanted(true, false, true, false));
        Assert.Equal(RegionEditorState.Closed, RecorderView.EditorWanted(true, true, false, false));
        Assert.Equal(RegionEditorState.Closed, RecorderView.EditorWanted(false, true, true, false));

        var serit = File.ReadAllText(Path.Combine(GirdiKanit.Root, "src", "VidShrink.App", "Recorder", "RecorderView.Serit.cs"));
        var yuzey = serit.IndexOf("private void RefreshSerit()", StringComparison.Ordinal);
        Assert.True(yuzey >= 0 && serit.IndexOf("SyncRegionEditor();", yuzey, StringComparison.Ordinal) > yuzey);
    }

    private sealed class SahteDuzenleyici : IRegionEditorHost
    {
        public List<string> Cagrilar { get; } = new();

        public PixelRect? Son { get; private set; }

        public double? Oran { get; private set; }

        public bool IsOpen { get; private set; }

        public event EventHandler<PixelRect>? Changed;

        public event EventHandler<PixelRect>? Committed;

        public event EventHandler? StartRequested { add { } remove { } }

        public event EventHandler? SettingsRequested;

        public event EventHandler? Dismissed;

        public void Show(PixelRect region, double? ratio)
        {
            IsOpen = true;
            Son = region;
            Oran = ratio;
            Cagrilar.Add("goster");
        }

        public void Hide() => Cagrilar.Add("gizle");

        public void Close()
        {
            if (IsOpen) Cagrilar.Add("kapat");
            IsOpen = false;
        }

        public void Degistir(PixelRect r) => Changed?.Invoke(this, r);

        public void Birak(PixelRect r) => Committed?.Invoke(this, r);

        public void Ayarlar() => SettingsRequested?.Invoke(this, EventArgs.Empty);

        public void KullaniciKapatti()
        {
            IsOpen = false;
            Dismissed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string? Dosyada(string dosya, string anahtar)
        => File.Exists(dosya) ? JsonNode.Parse(File.ReadAllText(dosya))?[anahtar]?.ToJsonString() : null;

    private static string Kutular(RecorderView view)
        => string.Join(",", new[] { "TxtRegionX", "TxtRegionY", "TxtRegionWidth", "TxtRegionHeight" }.Select(a => Bul<TextBox>(view, a).Text));

    [Fact]
    public void CizilenBolgeDuzenleyicideKalirVeHerDegisimKutularaVeAyaraGecer()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var sahte = new SahteDuzenleyici();
            var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true, RegionEditorEnabled = true, RegionEditor = sahte };
            Elle(view);
            Bul<ComboBox>(view, "CmbAspect").SelectedIndex = Array.IndexOf(RegionDraw.Aspects, "16:9");
            view.DrawRegion = _ => Task.FromResult<PixelRect?>(new PixelRect(100, 50, 641, 361));

            var cizildi = view.DrawRegionAsync().GetAwaiter().GetResult();
            var acildi = (view.EditingRegion, sahte.Son, sahte.Oran);

            sahte.Degistir(new PixelRect(200, 150, 801, 451));
            var surerken = (kutu: Kutular(view), dosya: Dosyada(ayarYolu, "regionX"));

            sahte.Birak(new PixelRect(210, 160, 800, 450));
            var birakinca = (kutu: Kutular(view), x: Dosyada(ayarYolu, "regionX"), w: Dosyada(ayarYolu, "regionWidth"));

            Yaz(view, "TxtRegionWidth", "1000");
            var elle = sahte.Son;

            sahte.Ayarlar();
            Bul<ComboBox>(view, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Screen;
            var hedefDegisti = (view.EditingRegion, sahte.IsOpen, kapat: sahte.Cagrilar.Count(c => c == "kapat"));

            Bul<ComboBox>(view, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Region;
            var geriGelmedi = sahte.IsOpen;
            view.DrawRegionAsync().GetAwaiter().GetResult();
            var yeniden = view.EditingRegion;
            sahte.KullaniciKapatti();
            var kullaniciKapatti = view.EditingRegion;

            var eskiSahte = new SahteDuzenleyici();
            var eski = new RecorderView(ayarYolu) { SkipAutoMeasure = true, RegionEditorEnabled = false, RegionEditor = eskiSahte };
            eski.DrawRegion = _ => Task.FromResult<PixelRect?>(new PixelRect(10, 10, 200, 100));
            var eskiCizildi = eski.DrawRegionAsync().GetAwaiter().GetResult();

            return (cizildi, acildi, surerken, birakinca, elle, hedefDegisti, geriGelmedi, yeniden, kullaniciKapatti,
                eskiCizildi, eskiAcik: eski.EditingRegion, eskiCagri: eskiSahte.Cagrilar.Count);
        }));

        Assert.True(olcu.cizildi);
        Assert.Equal((true, (PixelRect?)new PixelRect(100, 50, 640, 360)), (olcu.acildi.EditingRegion, olcu.acildi.Son));
        Assert.Equal(OnAltiDokuz, olcu.acildi.Oran!.Value, 3);
        Assert.Equal(("200,150,800,450", "100"), olcu.surerken);
        Assert.Equal(("210,160,800,450", "210", "800"), olcu.birakinca);
        Assert.Equal(new PixelRect(210, 160, 1000, 450), olcu.elle);
        Assert.Equal((false, false, 1), olcu.hedefDegisti);
        Assert.False(olcu.geriGelmedi);
        Assert.True(olcu.yeniden);
        Assert.False(olcu.kullaniciKapatti);
        Assert.True(olcu.eskiCizildi);
        Assert.False(olcu.eskiAcik);
        Assert.Equal(0, olcu.eskiCagri);
    }
}
