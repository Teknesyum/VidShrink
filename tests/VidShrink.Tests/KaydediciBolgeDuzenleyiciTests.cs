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
using VidShrink.Ffmpeg;
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

        var kayitta = RegionEdit.Shape(Bolge, 4, 12, panel, koken, editable: false);
        Assert.Equal(new[] { new PixelRect(110, 70, 300, 40) }, kayitta);
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
    public void DuzenleyiciTampondaGizlenirHedefDegisinceKapanir()
    {
        Assert.Equal(RegionEditorState.Shown, RecorderView.EditorWanted(true, true, true, false));
        Assert.Equal(RegionEditorState.Hidden, RecorderView.EditorWanted(true, true, true, true));
        Assert.Equal(RegionEditorState.Closed, RecorderView.EditorWanted(true, false, true, false));
        Assert.Equal(RegionEditorState.Closed, RecorderView.EditorWanted(true, true, false, false));
        Assert.Equal(RegionEditorState.Closed, RecorderView.EditorWanted(false, true, true, false));
        Assert.Equal(RegionEditorState.Hidden, RecorderView.EditorWanted(true, true, true, false, hidden: true));
        Assert.Equal(RegionEditorState.Closed, RecorderView.EditorWanted(true, false, true, false, hidden: true));

        var serit = File.ReadAllText(Path.Combine(GirdiKanit.Root, "src", "VidShrink.App", "Recorder", "RecorderView.Serit.cs"));
        var yuzey = serit.IndexOf("private void RefreshSerit()", StringComparison.Ordinal);
        Assert.True(yuzey >= 0 && serit.IndexOf("SyncRegionEditor();", yuzey, StringComparison.Ordinal) > yuzey);
    }

    [Fact]
    public void KayitEvresiOturumdanVeGeriSayimdanOkunur()
    {
        Assert.Equal(RegionEditorPhase.Idle, RecorderView.EditorPhase(false, RecorderState.Stopped, false));
        Assert.Equal(RegionEditorPhase.Counting, RecorderView.EditorPhase(false, RecorderState.Stopped, true));
        Assert.Equal(RegionEditorPhase.Running, RecorderView.EditorPhase(true, RecorderState.Running, false));
        Assert.Equal(RegionEditorPhase.Paused, RecorderView.EditorPhase(true, RecorderState.Paused, false));

        Assert.True(RecorderRegionEditor.Editable(RegionEditorPhase.Idle));
        Assert.All(new[] { RegionEditorPhase.Counting, RegionEditorPhase.Running, RegionEditorPhase.Paused },
            e => Assert.False(RecorderRegionEditor.Editable(e)));
    }

    [Fact]
    public void DuzenleyicininDugmeleriKaydedicininKendiYolunuCagirir()
    {
        var kaynak = File.ReadAllText(Path.Combine(GirdiKanit.Root, "src", "VidShrink.App", "Recorder", "RecorderView.Duzenleyici.cs"));

        Assert.Contains("OnEditorStart(object? sender, EventArgs e) => await StartAsync();", kaynak);
        Assert.Contains("OnEditorPause(object? sender, EventArgs e) => await PauseAsync();", kaynak);
        Assert.Contains("OnEditorResume(object? sender, EventArgs e) => await ResumeAsync();", kaynak);
        Assert.Contains("OnEditorStop(object? sender, EventArgs e) => await StopAsync();", kaynak);
        Assert.DoesNotContain("session.StopAsync", kaynak);
        Assert.DoesNotContain("_session.PauseAsync", kaynak);
    }

    [Fact]
    public void GeriSayimdaPanelDurdurGosterirVeDurdurSayimiKeser()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var sahte = new SahteDuzenleyici();
            var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true, RegionEditorEnabled = true, RegionEditor = sahte };
            Elle(view);
            view.DrawRegion = _ => Task.FromResult<PixelRect?>(new PixelRect(100, 50, 640, 360));
            view.DrawRegionAsync().GetAwaiter().GetResult();
            var bosta = sahte.Evre;
            Bul<ComboBox>(view, "CmbCountdown").SelectedIndex = 3;

            var adim = 0;
            RegionEditorPhase? sayarken = null;
            view.CountdownDelay = (_, ct) =>
            {
                if (++adim == 2)
                {
                    sayarken = sahte.Evre;
                    sahte.Durdur();
                }

                return ct.IsCancellationRequested ? Task.FromCanceled(ct) : Task.CompletedTask;
            };
            var tamamlandi = view.CountdownAsync().GetAwaiter().GetResult();

            sahte.Duraklat();
            sahte.Surdur();

            return (bosta, sayarken, tamamlandi, adim, sonra: sahte.Evre, gizle: sahte.Cagrilar.Count(c => c == "gizle"),
                oturum: view.HasSession || view.CountingDown, acik: sahte.IsOpen);
        }));

        Assert.Equal(RegionEditorPhase.Idle, olcu.bosta);
        Assert.Equal(RegionEditorPhase.Counting, olcu.sayarken);
        Assert.False(olcu.tamamlandi);
        Assert.Equal(2, olcu.adim);
        Assert.Equal(RegionEditorPhase.Idle, olcu.sonra);
        Assert.Equal(0, olcu.gizle);
        Assert.False(olcu.oturum);
        Assert.True(olcu.acik);
    }

    private sealed class SahteDuzenleyici : IRegionEditorHost
    {
        public List<string> Cagrilar { get; } = new();

        public PixelRect? Son { get; private set; }

        public double? Oran { get; private set; }

        public RegionEditorPhase? Evre { get; private set; }

        public bool IsOpen { get; private set; }

        public event EventHandler<PixelRect>? Changed;

        public event EventHandler<PixelRect>? Committed;

        public event EventHandler? StartRequested { add { } remove { } }

        public event EventHandler? SettingsRequested;

        public event EventHandler? PauseRequested;

        public event EventHandler? ResumeRequested;

        public event EventHandler? StopRequested;

        public event EventHandler? SnapshotRequested;

        public event EventHandler? HideRequested;

        public event EventHandler? Dismissed;

        public void KareAl() => SnapshotRequested?.Invoke(this, EventArgs.Empty);

        public void Gizle() => HideRequested?.Invoke(this, EventArgs.Empty);

        public void Show(PixelRect region, double? ratio, RegionEditorPhase phase)
        {
            IsOpen = true;
            Son = region;
            Oran = ratio;
            Evre = phase;
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

        public void Durdur() => StopRequested?.Invoke(this, EventArgs.Empty);

        public void Duraklat() => PauseRequested?.Invoke(this, EventArgs.Empty);

        public void Surdur() => ResumeRequested?.Invoke(this, EventArgs.Empty);

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

    private static readonly string Kanit = Path.Combine(GirdiKanit.Root, ".calisma", "bolge-duzenleyici");

    private static string[] Gorunenler(RecorderRegionEditor d)
        => new (string ad, Button b)[]
            {
                ("baslat", d.BtnStart), ("ayarlar", d.BtnSettings), ("duraklat", d.BtnPause), ("surdur", d.BtnResume),
                ("durdur", d.BtnStop), ("kare", d.BtnSnapshot), ("gizle", d.BtnHide), ("kapat", d.BtnClose)
            }
            .Where(x => x.b.IsVisible).Select(x => x.ad).ToArray();

    private static double PanelGenisligi(RecorderRegionEditor d)
    {
        d.Toolbar.InvalidateMeasure();
        d.Toolbar.Measure(Size.Infinity);
        return d.Toolbar.DesiredSize.Width;
    }

    [Fact]
    public void PanelHerEvredeKendiDugmeleriniGosterirKenarBostaKesikKayittaGizli()
    {
        var olcu = AppHost.Run(() =>
        {
            var d = new RecorderRegionEditor(new PixelRect(0, 0, 640, 360), null);
            double Deger(string anahtar) => d.TryFindResource(anahtar, out var v) && v is double x ? x : double.NaN;
            var evreler = new[] { RegionEditorPhase.Idle, RegionEditorPhase.Counting, RegionEditorPhase.Running, RegionEditorPhase.Paused };
            var satirlar = new List<(RegionEditorPhase evre, string[] dugmeler, double genislik, string? ipucu, double[]? kesik, bool kenar)>();
            foreach (var evre in evreler.Concat(new[] { RegionEditorPhase.Idle }))
            {
                d.SetPhase(evre);
                satirlar.Add((evre, Gorunenler(d), PanelGenisligi(d), ToolTip.GetTip(d.BtnClose) as string,
                    d.Edge.StrokeDashArray?.ToArray(), d.Edge.IsVisible));
            }

            d.TryFindResource("NeonEmber", d.ActualThemeVariant, out var ember);
            var cerceve = new RecorderFrame();
            var sonuc = (satirlar, boy: Deger("RecorderRegionButtonSize"), aralik: Deger("RecorderRegionButtonGap"),
                kesik: Deger("RecorderRegionDash"), kalinlik: Deger("RecorderFrameThickness"),
                ember, kenar: d.Edge.Stroke, cerceveKenar: cerceve.FrameEdge.BorderBrush,
                kapat: VidShrink.App.LanguageCatalog.Display(VidShrink.App.Localization.Strings.Get("recorder.region.close")),
                durdurKapat: VidShrink.App.LanguageCatalog.Display(VidShrink.App.Localization.Strings.Get("recorder.region.stop-close")));
            cerceve.Close();
            d.CloseQuietly();
            return sonuc;
        });

        var s = olcu.satirlar;
        Assert.Equal(new[] { "baslat", "ayarlar", "kare", "kapat" }, s[0].dugmeler);
        Assert.Equal(new[] { "durdur", "kapat" }, s[1].dugmeler);
        Assert.Equal(new[] { "duraklat", "durdur", "kare", "gizle", "kapat" }, s[2].dugmeler);
        Assert.Equal(new[] { "surdur", "durdur", "kare", "gizle", "kapat" }, s[3].dugmeler);
        Assert.Equal(s[0].dugmeler, s[4].dugmeler);

        Assert.Equal(40, olcu.boy);
        var adim = olcu.boy + olcu.aralik;
        Assert.Equal(2 * adim, s[0].genislik - s[1].genislik, 3);
        Assert.Equal(adim, s[2].genislik - s[0].genislik, 3);
        Assert.Equal(s[2].genislik, s[3].genislik, 3);

        Assert.Equal(olcu.kapat, s[0].ipucu);
        Assert.All(new[] { s[1], s[2], s[3] }, r => Assert.Equal(olcu.durdurKapat, r.ipucu));
        Assert.NotEqual(olcu.kapat, olcu.durdurKapat);

        Assert.Equal(new[] { olcu.kesik / olcu.kalinlik, olcu.kesik / olcu.kalinlik }, s[0].kesik);
        Assert.Equal(new[] { 4.0, 4.0 }, s[0].kesik);
        Assert.True(s[0].kenar);
        Assert.All(new[] { s[1], s[2], s[3] }, r => { Assert.Null(r.kesik); Assert.False(r.kenar); });
        Assert.Equal(s[0].kesik, s[4].kesik);

        Assert.NotNull(olcu.ember);
        Assert.Same(olcu.ember, olcu.kenar);
        Assert.Same(olcu.ember, olcu.cerceveKenar);
    }

    [Fact]
    public void KenarKesigiBelirtectenGelirKayittaDuzdur()
    {
        Assert.Equal(new[] { 4.0, 4.0 }, RecorderRegionEditor.EdgeDash(true, 8, 2)!.ToArray());
        Assert.Equal(new[] { 2.0, 2.0 }, RecorderRegionEditor.EdgeDash(true, 8, 4)!.ToArray());
        Assert.Null(RecorderRegionEditor.EdgeDash(false, 8, 2));
        Assert.Null(RecorderRegionEditor.EdgeDash(true, 0, 2));
        Assert.Null(RecorderRegionEditor.EdgeDash(true, 8, 0));
    }

    [Fact]
    public void CarpiBostaYalnizKapatirKayittaDurdurupKapatir()
    {
        var olcu = AppHost.Run(() =>
        {
            (int durdur, bool kapandi) Dene(RegionEditorPhase evre)
            {
                var d = new RecorderRegionEditor(new PixelRect(100, 100, 320, 240), null);
                var durdur = 0;
                var kapandi = false;
                d.StopRequested += (_, _) => durdur++;
                d.Closed += (_, _) => kapandi = true;
                d.SetPhase(evre);
                d.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                d.CloseFromToolbar();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                if (!kapandi) d.CloseQuietly();
                return (durdur, kapandi);
            }

            return (bosta: Dene(RegionEditorPhase.Idle), kayitta: Dene(RegionEditorPhase.Running),
                duraklatildi: Dene(RegionEditorPhase.Paused), sayarken: Dene(RegionEditorPhase.Counting));
        });

        Assert.Equal((0, true), olcu.bosta);
        Assert.Equal((1, true), olcu.kayitta);
        Assert.Equal((1, true), olcu.duraklatildi);
        Assert.Equal((1, true), olcu.sayarken);
    }

    [Fact]
    public void KayitYokkenKameraDugmesiBolgeninKaresiniPngOlarakAlir()
    {
        var klasor = Path.Combine(Kanit, "bosta-kare");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);

        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var sahte = new SahteDuzenleyici();
            var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true, RegionEditorEnabled = true, RegionEditor = sahte };
            Elle(view);
            Bul<ComboBox>(view, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Region;
            Yaz(view, "TxtOutputFolder", klasor);
            view.DrawRegion = _ => Task.FromResult<PixelRect?>(new PixelRect(100, 50, 320, 240));
            view.DrawRegionAsync().GetAwaiter().GetResult();

            RecorderRequest? istek = null;
            string? yol = null;
            view.TakeFrame = (r, p) =>
            {
                istek = r;
                yol = p;
                if (!p.StartsWith(klasor, StringComparison.OrdinalIgnoreCase)) return Task.FromResult(false);
                Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                File.WriteAllBytes(p, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
                return Task.FromResult(true);
            };
            sahte.KareAl();
            var saat = System.Diagnostics.Stopwatch.StartNew();
            while (yol is null && saat.ElapsedMilliseconds < 5000) Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            return (yol, istek, var: yol is not null && File.Exists(yol), not: view.NoticeText, hata: view.ErrorText,
                oturum: view.HasSession);
        }));

        try
        {
            Assert.NotNull(olcu.yol);
            Assert.StartsWith(klasor, olcu.yol!, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("kare_", Path.GetFileName(olcu.yol));
            Assert.EndsWith(".png", olcu.yol);
            Assert.True(olcu.var);
            Assert.Contains(olcu.yol!, olcu.not);
            Assert.Equal(string.Empty, olcu.hata);
            Assert.False(olcu.oturum);
            Assert.Equal(RecorderTargetKind.Region, olcu.istek!.Target);
            Assert.Equal(new RecorderRegion(100, 50, 320, 240), olcu.istek.Region);
            Assert.Equal(5, RecorderHotkeys.All.Count);
            Assert.Equal(5, RecorderHotkeys.All.Select(b => b.VirtualKey).Distinct().Count());
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    private sealed class SahteCerceve : IRecorderFrameHost
    {
        public List<string> Cagrilar { get; } = new();

        public void Show(PixelRect region) => Cagrilar.Add("goster");

        public void Hide() => Cagrilar.Add("gizle");
    }

    private static void Pompala(Task gorev, int sinirMs)
    {
        var saat = System.Diagnostics.Stopwatch.StartNew();
        while (!gorev.IsCompleted && saat.ElapsedMilliseconds < sinirMs)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            System.Threading.Thread.Sleep(20);
        }

        Assert.True(gorev.IsCompleted, "gorev zamaninda bitmedi");
        gorev.GetAwaiter().GetResult();
    }

    private static void Bekle(Func<bool> sart, int sinirMs)
    {
        var saat = System.Diagnostics.Stopwatch.StartNew();
        while (!sart() && saat.ElapsedMilliseconds < sinirMs)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            System.Threading.Thread.Sleep(20);
        }
    }

    [KayitFact]
    public void KayitSirasindaPanelDuraklatirKareAlirCerceveyiGizlerVeDurdurur()
    {
        var klasor = Path.Combine(Kanit, "canli");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);

        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var sahte = new SahteDuzenleyici();
            var cerceve = new SahteCerceve();
            var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true, RegionEditorEnabled = true, RegionEditor = sahte, FrameHost = cerceve };
            Elle(view);
            Bul<ComboBox>(view, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Region;
            Bul<ComboBox>(view, "CmbCountdown").SelectedIndex = 0;
            Sec(view, "CmbCodec", "libx264");
            Sec(view, "CmbPreset", "ultrafast");
            Yaz(view, "TxtOutputFolder", klasor);
            view.DrawRegion = _ => Task.FromResult<PixelRect?>(new PixelRect(0, 0, 320, 240));
            view.DrawRegionAsync().GetAwaiter().GetResult();
            view.TakeFrame = (r, p) => p.StartsWith(klasor, StringComparison.OrdinalIgnoreCase)
                ? RecorderSession.CaptureAsync(r, p)
                : Task.FromResult(false);

            var onceKare = view.SnapshotNowAsync();
            Pompala(onceKare, 30000);
            var bostaKare = (ok: onceKare.Result, pngler: Directory.Exists(klasor) ? Directory.GetFiles(klasor, "kare_*.png") : Array.Empty<string>());

            var baslat = view.StartAsync();
            Pompala(baslat, 45000);
            var basladi = (view.HasSession, sahte.Evre, view.ErrorText);
            Pompala(Task.Delay(1200), 3000);

            sahte.Duraklat();
            Bekle(() => view.State == RecorderState.Paused, 10000);
            var duraklatildi = (view.State, sahte.Evre);
            sahte.Surdur();
            Bekle(() => view.State == RecorderState.Running, 10000);
            var surdu = (view.State, sahte.Evre);

            var gizleOnce = sahte.Cagrilar.Count(c => c == "gizle");
            sahte.Gizle();
            var gizlendi = (view.FrameHiddenByUser, view.FrameShown, panel: sahte.Cagrilar.Count(c => c == "gizle") - gizleOnce,
                cerceve: cerceve.Cagrilar.LastOrDefault(), view.HasSession, view.State);
            view.OnTrayClicked();
            var tepsi = (view.FrameHiddenByUser, view.FrameShown, son: sahte.Cagrilar.LastOrDefault());
            sahte.Gizle();
            var tus = view.RunHotkeyAsync(HotkeyAction.Frame);
            Pompala(tus, 5000);
            var kisayol = (view.FrameHiddenByUser, view.FrameShown);

            sahte.Durdur();
            Bekle(() => !view.HasSession, 30000);
            var durdu = (view.HasSession, view.FrameHiddenByUser, sahte.Evre, sonuc: view.ResultPathText, view.ErrorText);
            if (view.HasSession)
            {
                var iptal = view.RunHotkeyAsync(HotkeyAction.Discard);
                Pompala(iptal, 15000);
            }

            return (bostaKare, basladi, duraklatildi, surdu, gizlendi, tepsi, kisayol, durdu,
                videolar: Directory.Exists(klasor) ? Directory.GetFiles(klasor, "*.mp4") : Array.Empty<string>());
        }));

        try
        {
            Assert.True(olcu.bostaKare.ok);
            var png = Assert.Single(olcu.bostaKare.pngler);
            var imza = File.ReadAllBytes(png).Take(8).ToArray();
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, imza);

            Assert.True(olcu.basladi.HasSession, olcu.basladi.ErrorText);
            Assert.Equal(RegionEditorPhase.Running, olcu.basladi.Evre);
            Assert.Equal((RecorderState.Paused, (RegionEditorPhase?)RegionEditorPhase.Paused), olcu.duraklatildi);
            Assert.Equal((RecorderState.Running, (RegionEditorPhase?)RegionEditorPhase.Running), olcu.surdu);

            Assert.True(olcu.gizlendi.FrameHiddenByUser);
            Assert.False(olcu.gizlendi.FrameShown);
            Assert.Equal(1, olcu.gizlendi.panel);
            Assert.Equal("gizle", olcu.gizlendi.cerceve);
            Assert.True(olcu.gizlendi.HasSession);
            Assert.Equal(RecorderState.Running, olcu.gizlendi.State);

            Assert.Equal((false, true, "goster"), olcu.tepsi);
            Assert.Equal((false, true), olcu.kisayol);

            Assert.False(olcu.durdu.HasSession, olcu.durdu.ErrorText);
            Assert.False(olcu.durdu.FrameHiddenByUser);
            Assert.Equal(RegionEditorPhase.Idle, olcu.durdu.Evre);
            var video = Assert.Single(olcu.videolar);
            Assert.True(new FileInfo(video).Length > 0);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }
}
