using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// 49 ve 159: adlı yer imleri, yer imi penceresi, sık kullanılanlar, geçmişte yer imi işareti ve açılışta "sor" kipi.
/// Pencere ölçüleri sahte motorla (<see cref="BildirimMotoru"/>) başsız koşar; libmpv istemez.
/// </summary>
public sealed class OynaticiYerImiListesiTests : IDisposable
{
    private readonly string _klasor = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-yer-imi", Guid.NewGuid().ToString("N")[..8]);

    public OynaticiYerImiListesiTests() => Directory.CreateDirectory(_klasor);

    public void Dispose() => OynaticiListeTests.Sil(_klasor);

    private string Dosya(string ad)
    {
        var yol = Path.Combine(_klasor, ad);
        File.WriteAllBytes(yol, new byte[1000]);
        return yol;
    }

    private string GecmisYolu => Path.Combine(_klasor, "history.json");

    [Fact]
    public void AdliYerImiEklenirAdlandirilirSilinirVeYenidenAcilistaKalir()
    {
        var dosya = Dosya("film.mp4");
        var gecmis = new PlaybackHistory();
        Assert.True(gecmis.AddBookmark(dosya, 12, "Giriş"));
        Assert.True(gecmis.AddBookmark(dosya, 40));
        Assert.True(gecmis.AddBookmark(dosya, 75, "Son"));
        gecmis.Save(GecmisYolu);
        Assert.Contains("\"name\"", File.ReadAllText(GecmisYolu));

        var geri = PlaybackHistory.Load(GecmisYolu);
        Assert.Equal(new[] { new Bookmark(12, "Giriş"), new Bookmark(40, null), new Bookmark(75, "Son") }, geri.NamedBookmarks(dosya));

        Assert.True(geri.RenameBookmark(dosya, 40, "Orta"));
        Assert.True(geri.RemoveBookmark(dosya, 75));
        Assert.False(geri.RemoveBookmark(dosya, 99));
        geri.Save(GecmisYolu);

        var son = PlaybackHistory.Load(GecmisYolu);
        Assert.Equal(new[] { new Bookmark(12, "Giriş"), new Bookmark(40, "Orta") }, son.NamedBookmarks(dosya));
    }

    [Fact]
    public void AdsizEskiKayitYuklenir()
    {
        var dosya = Path.GetFullPath(Dosya("eski.mp4"));
        var gecmis = new PlaybackHistory();
        gecmis.AddBookmark(dosya, 5);
        gecmis.AddBookmark(dosya, 9);
        gecmis.Save(GecmisYolu);
        Assert.DoesNotContain("\"name\"", File.ReadAllText(GecmisYolu));

        var geri = PlaybackHistory.Load(GecmisYolu);
        Assert.Equal(new[] { 5.0, 9.0 }, geri.Bookmarks(dosya));
        Assert.All(geri.NamedBookmarks(dosya), mark => Assert.Null(mark.Name));
    }

    [Fact]
    public void GecmisSatiriYerImiOlduguniBilir()
    {
        var a = Dosya("isaretli.mp4");
        var b = Dosya("duz.mp4");
        var gecmis = new PlaybackHistory();
        gecmis.Remember(a, 10, false, 600);
        gecmis.Remember(b, 20, false, 600);
        gecmis.AddBookmark(a, 30);
        gecmis.Save(GecmisYolu);

        var satirlar = PlaybackHistory.Load(GecmisYolu).Rows(null);
        Assert.True(satirlar.Single(row => row.Path == Path.GetFullPath(a)).HasBookmarks);
        Assert.False(satirlar.Single(row => row.Path == Path.GetFullPath(b)).HasBookmarks);

        var isaretli = AppHost.Run(() =>
        {
            var motor = new BildirimMotoru();
            var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => GecmisYolu };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            var gecmisPenceresi = view.ShowHistory();
            var yollar = gecmisPenceresi.BookmarkedPaths.ToList();
            gecmisPenceresi.Close();
            view.Close();
            pencere.Close();
            Dispatcher.UIThread.RunJobs();
            Strings.Reset();
            return yollar;
        });

        Assert.Equal(new[] { Path.GetFullPath(a) }, isaretli);
    }

    [Fact]
    public void SikKullanilanlarDosyaKlasorVeAdresiTutarAdresSirriDiskeInmez()
    {
        var dosya = Dosya("sevilen.mp4");
        var yol = Path.Combine(_klasor, Favorites.FileName);
        var liste = new Favorites();
        Assert.True(liste.Add(dosya));
        Assert.True(liste.Add(_klasor));
        Assert.True(liste.Add("http://ad:parola@ornek.test/v.mp4?token=gizli"));
        Assert.False(liste.Add(dosya));
        liste.Save(yol);

        var metin = File.ReadAllText(yol);
        Assert.DoesNotContain("token", metin);
        Assert.DoesNotContain("gizli", metin);
        Assert.DoesNotContain("parola", metin);

        var geri = Favorites.Load(yol);
        Assert.Equal(3, geri.Items.Count);
        Assert.Equal(FavoriteKind.File, Favorites.KindOf(geri.Items[0]));
        Assert.Equal(FavoriteKind.Folder, Favorites.KindOf(geri.Items[1]));
        Assert.Equal(FavoriteKind.Address, Favorites.KindOf(geri.Items[2]));
        Assert.True(geri.Contains("http://ornek.test/v.mp4?token=baska"));

        Assert.True(geri.Remove(dosya));
        geri.Save(yol);
        Assert.Equal(2, Favorites.Load(yol).Items.Count);
    }

    [Fact]
    public void MenudenSikKullanilanEklenirVeAcilir()
    {
        var dosya = Dosya("menu.mp4");
        var sonuc = AppHost.Run(() =>
        {
            var motor = new BildirimMotoru();
            var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => GecmisYolu };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            var acilis = view.OpenAsync(dosya);
            DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
            var eklendi = view.ToggleFavorite(dosya);
            view.Close();
            DenetimSurucu.Pump(view, () => view.LoadedPath is null, 2);

            var menu = view.FavoritesMenu();
            var satir = menu.Items.OfType<MenuItem>().First(item => item.Tag is string);
            var acildi = view.OpenFavorite((string)satir.Tag!);
            DenetimSurucu.Pump(view, () => view.Navigation.IsCompleted, 10);
            var yuklu = view.LoadedPath;
            var kayip = view.OpenFavorite(Path.Combine(_klasor, "yok.mp4"));
            view.Close();
            pencere.Close();
            Dispatcher.UIThread.RunJobs();
            Strings.Reset();
            return (eklendi, baslik: menu.Header as string, acildi, yuklu, kayip);
        });

        Assert.True(sonuc.eklendi);
        Assert.Equal(Strings.Get("player.favorites.title"), sonuc.baslik);
        Assert.True(sonuc.acildi);
        Assert.Equal(Path.GetFullPath(dosya), Path.GetFullPath(sonuc.yuklu!));
        Assert.False(sonuc.kayip);
        Assert.Single(Favorites.Load(Path.Combine(_klasor, Favorites.FileName)).Items);
    }

    [Fact]
    public void YerImiPencereListelerAdlandirirSilerVeGider()
    {
        var dosya = Dosya("pencere.mp4");
        var onceki = new PlaybackHistory();
        onceki.AddBookmark(dosya, 15, "Bir");
        onceki.AddBookmark(dosya, 45);
        onceki.AddBookmark(dosya, 90);
        onceki.Save(GecmisYolu);

        var sonuc = AppHost.Run(() =>
        {
            var motor = new BildirimMotoru();
            var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => GecmisYolu };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            var bos = view.ShowBookmarks();
            var acilis = view.OpenAsync(dosya);
            DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);

            var liste = view.ShowBookmarks()!;
            var ilk = liste.Shown.ToList();
            liste.Select(1);
            liste.NameField.Text = "İki";
            var adlandi = liste.RenameSelected();
            liste.Select(2);
            var silindi = liste.DeleteSelected();
            var kalan = liste.Shown.ToList();
            liste.Select(1);
            var gitti = liste.GoSelected();
            DenetimSurucu.Pump(view, () => Math.Abs(motor.Konum - 45) < 0.01, 5);
            var konum = motor.Konum;
            var kapandi = !liste.IsVisible;
            view.Close();
            pencere.Close();
            Dispatcher.UIThread.RunJobs();
            Strings.Reset();
            return (bos, ilk, adlandi, silindi, kalan, gitti, konum, kapandi);
        });

        Assert.Null(sonuc.bos);
        Assert.Equal(new[] { new Bookmark(15, "Bir"), new Bookmark(45, null), new Bookmark(90, null) }, sonuc.ilk);
        Assert.True(sonuc.adlandi);
        Assert.True(sonuc.silindi);
        Assert.Equal(new[] { new Bookmark(15, "Bir"), new Bookmark(45, "İki") }, sonuc.kalan);
        Assert.True(sonuc.gitti);
        Assert.Equal(45, sonuc.konum, 3);
        Assert.True(sonuc.kapandi);
        Assert.Equal(new[] { new Bookmark(15, "Bir"), new Bookmark(45, "İki") }, PlaybackHistory.Load(GecmisYolu).NamedBookmarks(dosya));
    }

    [Theory]
    [InlineData(true, 30.0)]
    [InlineData(false, 0.0)]
    public void SorKipindeKararKutusuCikarVeSecimeUyar(bool devam, double beklenen)
    {
        var dosya = Dosya("sor.mp4");
        var onceki = new PlaybackHistory();
        onceki.Remember(dosya, 30, false, 600);
        onceki.Save(GecmisYolu);
        new PlayerSettings { Resume = ResumeMode.Ask }.Save(Path.Combine(_klasor, PlayerSettings.FileName));

        var sonuc = AppHost.Run(() =>
        {
            var motor = new BildirimMotoru();
            var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => GecmisYolu };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            var acilis = view.OpenAsync(dosya);
            DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
            DenetimSurucu.Pump(view, () => false, 0.2);
            var gorunur = view.ResumePromptVisible;
            var bekleyen = motor.Konum;
            view.ResumeChoice(devam);
            DenetimSurucu.Pump(view, () => Math.Abs(motor.Konum - beklenen) < 0.01, 5);
            var secilen = motor.Konum;
            var kapandi = !view.ResumePromptVisible;
            view.Close();
            pencere.Close();
            Dispatcher.UIThread.RunJobs();
            Strings.Reset();
            return (gorunur, bekleyen, secilen, kapandi);
        });

        Assert.True(sonuc.gorunur);
        Assert.Equal(0, sonuc.bekleyen, 3);
        Assert.Equal(beklenen, sonuc.secilen, 3);
        Assert.True(sonuc.kapandi);
    }

    [Fact]
    public void DevamKipiKayitDonusunuKorurEskiAnahtarOkunur()
    {
        var yol = Path.Combine(_klasor, PlayerSettings.FileName);
        new PlayerSettings { Resume = ResumeMode.Ask }.Save(yol);
        Assert.Equal(ResumeMode.Ask, PlayerSettings.Load(yol).Resume);

        File.WriteAllText(yol, "{\"resume\": false}");
        Assert.Equal(ResumeMode.Off, PlayerSettings.Load(yol).Resume);
    }

    [Fact]
    public void CtrlBYerImiListesiniAcar()
    {
        Assert.Equal(PlayerCommandKind.BookmarkList, Keymap.ForKey(Key.B, KeyModifiers.Control, null).Kind);
        Assert.Contains(Keymap.BookmarkList, Keymap.LoopMenu);
    }
}
