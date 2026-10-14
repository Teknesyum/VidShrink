using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// OYB-P4 (OYB-11, OYB-13, OYB-14): geçmiş penceresi, devam anahtarı, geçmişi tutmama ve temizleme.
/// Pencere ölçüleri sahte motorla (<see cref="BildirimMotoru"/>) başsız koşar; libmpv istemez.
/// </summary>
public sealed class OynaticiGecmisPenceresiTests : IDisposable
{
    private readonly string _klasor = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-gecmis", Guid.NewGuid().ToString("N")[..8]);

    public OynaticiGecmisPenceresiTests() => Directory.CreateDirectory(_klasor);

    public void Dispose() => OynaticiListeTests.Sil(_klasor);

    private string Dosya(string ad)
    {
        var yol = Path.Combine(_klasor, ad);
        File.WriteAllBytes(yol, new byte[1000]);
        return yol;
    }

    private string GecmisYolu => Path.Combine(_klasor, "history.json");

    [Fact]
    public void SatirlarTarihSirasiylaGelirAramaAdaGoreSuzerAdresGirmez()
    {
        var saat = 1000L;
        var gecmis = new PlaybackHistory { Now = () => saat };
        var a = Dosya("tatil.mp4");
        var b = Dosya("toplanti.mkv");
        var c = Dosya("Tatil-2.mp4");
        gecmis.Remember(a, 10, false, 60);
        saat = 2000;
        gecmis.Remember(b, 20, false, 60);
        saat = 3000;
        gecmis.Remember(c, 30, false, 60);
        saat = 4000;
        gecmis.Remember("http://ornek.test/v.mp4?token=gizli", 5, false, 60);

        var hepsi = gecmis.Rows(null).Select(row => row.Path).ToList();
        Assert.Equal(new[] { Path.GetFullPath(c), Path.GetFullPath(b), Path.GetFullPath(a) }, hepsi);

        var tatil = gecmis.Rows("tatil").Select(row => row.Path).ToList();
        Assert.Equal(new[] { Path.GetFullPath(c), Path.GetFullPath(a) }, tatil);
        Assert.Empty(gecmis.Rows("yok-boyle-bir-ad"));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(3000), gecmis.Rows(null)[0].Seen);
    }

    [Fact]
    public void TemizlemeSatirlariVeKonumuSilerYerImiKalir()
    {
        var gecmis = new PlaybackHistory();
        var a = Dosya("a.mp4");
        var b = Dosya("b.mp4");
        gecmis.Remember(a, 30, false, 60);
        gecmis.Remember(b, 40, false, 60);
        gecmis.AddBookmark(b, 12);

        gecmis.Clear();
        gecmis.Save(GecmisYolu);
        var geri = PlaybackHistory.Load(GecmisYolu);

        Assert.Empty(geri.Rows(null));
        Assert.Equal(0, geri.ResumeFor(a, 60));
        Assert.Equal(0, geri.ResumeFor(b, 60));
        Assert.Equal(new[] { 12.0 }, geri.Bookmarks(b));
        Assert.DoesNotContain("a.mp4", File.ReadAllText(GecmisYolu));
    }

    [Fact]
    public void SonDosyalarTemizlenir()
    {
        var son = new RecentFiles();
        son.Add(Dosya("a.mp4"));
        son.Add(Dosya("b.mp4"));
        son.Clear();
        Assert.Empty(son.Items);
    }

    [Fact]
    public void YeniAnahtarlarVarsayilanAcikVeKayitDonusunuKorur()
    {
        var yol = Path.Combine(_klasor, PlayerSettings.FileName);
        File.WriteAllText(yol, "{\"shuffle\": true}");
        var eski = PlayerSettings.Load(yol);
        Assert.True(eski.KeepHistory);
        Assert.True(eski.Resume);

        eski.KeepHistory = false;
        eski.Resume = false;
        eski.Save(yol);
        var geri = PlayerSettings.Load(yol);
        Assert.False(geri.KeepHistory);
        Assert.False(geri.Resume);
    }

    [Fact]
    public void PencereListeler_ArarVe_SecileniAcar()
    {
        var a = Dosya("tatil.mp4");
        var b = Dosya("toplanti.mp4");
        var onceki = new PlaybackHistory();
        onceki.Remember(a, 10, false, 600);
        onceki.Remember(b, 20, false, 600);
        onceki.Save(GecmisYolu);

        var sonuc = AppHost.Run(() =>
        {
            var motor = new BildirimMotoru();
            var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => GecmisYolu };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            var gecmisPenceresi = view.ShowHistory();
            var ilk = gecmisPenceresi.ShownPaths.ToList();
            gecmisPenceresi.Search.Text = "tatil";
            var suzulmus = gecmisPenceresi.ShownPaths.ToList();
            var acildi = gecmisPenceresi.OpenSelected();
            DenetimSurucu.Pump(view, () => view.Navigation.IsCompleted, 10);
            var yuklu = view.LoadedPath;
            var kapandi = !gecmisPenceresi.IsVisible;
            view.Close();
            pencere.Close();
            Dispatcher.UIThread.RunJobs();
            Strings.Reset();
            return (ilk, suzulmus, acildi, yuklu, kapandi);
        });

        Assert.Equal(2, sonuc.ilk.Count);
        Assert.Contains(Path.GetFullPath(a), sonuc.ilk);
        Assert.Contains(Path.GetFullPath(b), sonuc.ilk);
        Assert.Equal(new[] { Path.GetFullPath(a) }, sonuc.suzulmus);
        Assert.True(sonuc.acildi);
        Assert.Equal(Path.GetFullPath(a), Path.GetFullPath(sonuc.yuklu!));
        Assert.True(sonuc.kapandi);
    }

    [Fact]
    public void MenudenTemizlemeSonDosyalariVeGecmisiBosaltir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var motor = new BildirimMotoru();
            var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => GecmisYolu };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            var dosya = Dosya("izlenen.mp4");
            var acilis = view.OpenAsync(dosya);
            DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
            motor.Konum = 120;
            view.Close();

            var son = view.BuildMenu().Items.OfType<MenuItem>()
                .First(item => Equals(item.Header, Strings.Get("player.list.recent")));
            var satirlar = son.Items.OfType<MenuItem>().Select(item => item.Header as string).ToList();
            var once = (view.Recent.Items.Count, view.HistoryRows(null).Count);
            view.ClearHistory();
            var sonra = (view.Recent.Items.Count, view.HistoryRows(null).Count);
            pencere.Close();
            Dispatcher.UIThread.RunJobs();
            Strings.Reset();
            return (satirlar, once, sonra);
        });

        Assert.Contains(Strings.Get("player.list.history"), sonuc.satirlar);
        Assert.Contains(Strings.Get("player.list.clear-history"), sonuc.satirlar);
        Assert.Equal((1, 1), sonuc.once);
        Assert.Equal((0, 0), sonuc.sonra);
        Assert.DoesNotContain("izlenen.mp4", File.ReadAllText(GecmisYolu));
        Assert.DoesNotContain("izlenen.mp4", File.ReadAllText(Path.Combine(_klasor, RecentFiles.FileName)));
    }

    [Fact]
    public void GecmisKapaliykenDiskeYazilmaz()
    {
        var ayar = new PlayerSettings { KeepHistory = false };
        ayar.Save(Path.Combine(_klasor, PlayerSettings.FileName));

        AppHost.Run(() =>
        {
            var motor = new BildirimMotoru();
            var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => GecmisYolu };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            var acilis = view.OpenAsync(Dosya("gizli.mp4"));
            DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
            motor.Konum = 90;
            view.TogglePlay();
            view.TogglePlay();
            view.Close();
            pencere.Close();
            Dispatcher.UIThread.RunJobs();
            Strings.Reset();
            return true;
        });

        Assert.False(File.Exists(GecmisYolu));
        Assert.False(File.Exists(Path.Combine(_klasor, RecentFiles.FileName)));
    }

    [Theory]
    [InlineData(true, 30.0)]
    [InlineData(false, 0.0)]
    public void DevamAnahtariKonumaUyar(bool devam, double beklenen)
    {
        var dosya = Dosya("devam.mp4");
        var onceki = new PlaybackHistory();
        onceki.Remember(dosya, 30, false, 600);
        onceki.Save(GecmisYolu);
        new PlayerSettings { Resume = devam }.Save(Path.Combine(_klasor, PlayerSettings.FileName));

        var konum = AppHost.Run(() =>
        {
            var motor = new BildirimMotoru();
            var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => GecmisYolu };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            var acilis = view.OpenAsync(dosya);
            DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
            DenetimSurucu.Pump(view, () => false, 0.2);
            var okunan = motor.Konum;
            view.Close();
            pencere.Close();
            Dispatcher.UIThread.RunJobs();
            Strings.Reset();
            return okunan;
        });

        Assert.Equal(beklenen, konum, 3);
    }
}
