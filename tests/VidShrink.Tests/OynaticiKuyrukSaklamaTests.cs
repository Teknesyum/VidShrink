using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// OYB-08: oynatıcı kuyruğu ve çalan dosya diske yazılır, yeni oynatıcı onu bir kez, duraklatılmış
/// geri yükler; diskte artık olmayan dosyalar atılır, bozuk dosya kuyruksuz açılır.
/// </summary>
public sealed class OynaticiKuyrukSaklamaTests
{
    [Fact]
    public void DepoYazarOkurOlmayaniAtar()
    {
        var kok = Klasor("depo");
        var dosya = Path.Combine(kok, PlayerQueue.FileName);
        string Y(string ad) => Path.Combine(kok, ad);
        var mevcut = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Y("a.mp3"), Y("c.mp3") };

        PlayerQueue.Save(dosya, new PlayerQueue(new[] { Y("a.mp3"), Y("b.mp3"), Y("c.mp3") }, Y("c.mp3"), true, Y("liste.m3u")));
        var okunan = PlayerQueue.Load(dosya, mevcut.Contains);

        PlayerQueue.Save(dosya, new PlayerQueue(new[] { Y("a.mp3"), Y("b.mp3") }, Y("b.mp3"), false, null));
        var calanYok = PlayerQueue.Load(dosya, mevcut.Contains);

        PlayerQueue.Save(dosya, new PlayerQueue(Array.Empty<string>(), Y("b.mp3"), false, null));
        var hepsiYok = PlayerQueue.Load(dosya, mevcut.Contains);

        File.WriteAllText(dosya, "{ \"items\": [ \"yarim");
        var bozuk = PlayerQueue.Load(dosya, mevcut.Contains);

        PlayerQueue.Save(dosya, null);
        var silindi = !File.Exists(dosya);
        Directory.Delete(kok, true);

        Assert.NotNull(okunan);
        Assert.Equal(new[] { Y("a.mp3"), Y("c.mp3") }, okunan!.Items);
        Assert.Equal(Y("c.mp3"), okunan.Current);
        Assert.True(okunan.Arranged);
        Assert.Equal(Y("liste.m3u"), okunan.Source);
        Assert.Equal(Y("a.mp3"), calanYok!.Current);
        Assert.Null(hepsiYok);
        Assert.Null(bozuk);
        Assert.True(silindi);
    }

    [Fact]
    public void KapanistaYazilanKuyrukAcilistaDuraklatilmisGelir()
    {
        var kok = Klasor("geri");
        foreach (var ad in new[] { "a.mp3", "b.mp3", "c.mp3" }) File.WriteAllBytes(Path.Combine(kok, ad), new byte[16]);
        string Y(string ad) => Path.Combine(kok, ad);
        var gecmis = Path.Combine(kok, "player-history.json");

        var sonuc = AppHost.Run(() =>
        {
            var ilk = new PlayerView { EngineFactory = () => new SesMotoru(MediaTags.Empty), HistoryPath = () => gecmis };
            var pencere = new Window { Width = 640, Height = 360, Content = ilk };
            pencere.Show();
            Assert.True(ilk.EnqueueItems(new[] { Y("a.mp3"), Y("b.mp3"), Y("c.mp3") }));
            Bekle(ilk, ilk.Navigation);
            Assert.True(ilk.MoveDown(Y("a.mp3")));
            ilk.Close();
            pencere.Close();
            var yazildi = File.Exists(Path.Combine(kok, PlayerQueue.FileName));
            File.Delete(Y("c.mp3"));

            var ikinci = new PlayerView { EngineFactory = () => new SesMotoru(MediaTags.Empty), HistoryPath = () => gecmis };
            pencere = new Window { Width = 640, Height = 360, Content = ikinci };
            pencere.Show();
            try
            {
                var geldi = ikinci.RestoreQueue();
                Bekle(ikinci, ikinci.Navigation);
                return (yazildi, geldi, ikinci.LoadedPath, Kuyruk: ikinci.Queue?.ToArray(), Liste: ikinci.PlaylistFiles().ToArray(),
                    ikinci.IsPlaying, Tekrar: ikinci.RestoreQueue());
            }
            finally
            {
                ikinci.Close();
                pencere.Close();
            }
        });
        Directory.Delete(kok, true);

        Assert.True(sonuc.yazildi);
        Assert.True(sonuc.geldi);
        Assert.Equal(Y("a.mp3"), sonuc.LoadedPath);
        Assert.Equal(new[] { Y("b.mp3"), Y("a.mp3") }, sonuc.Kuyruk);
        Assert.Equal(new[] { Y("b.mp3"), Y("a.mp3") }, sonuc.Liste);
        Assert.False(sonuc.IsPlaying);
        Assert.False(sonuc.Tekrar);
    }

    [Fact]
    public void GecmisKapaliykenKuyrukSilinirVeGelmez()
    {
        var kok = Klasor("kapali");
        File.WriteAllBytes(Path.Combine(kok, "a.mp3"), new byte[16]);
        var gecmis = Path.Combine(kok, "player-history.json");
        var kuyruk = Path.Combine(kok, PlayerQueue.FileName);

        var sonuc = AppHost.Run(() =>
        {
            var view = new PlayerView { EngineFactory = () => new SesMotoru(MediaTags.Empty), HistoryPath = () => gecmis };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            try
            {
                var acilis = view.OpenAsync(Path.Combine(kok, "a.mp3"));
                Bekle(view, acilis);
                var acikken = File.Exists(kuyruk);
                view.Settings.KeepHistory = false;
                view.SaveQueue();
                var kapaliyken = File.Exists(kuyruk);
                PlayerQueue.Save(kuyruk, new PlayerQueue(Array.Empty<string>(), Path.Combine(kok, "a.mp3"), false, null));
                var yeni = new PlayerView { HistoryPath = () => gecmis };
                yeni.Settings.KeepHistory = false;
                return (acikken, kapaliyken, Geldi: yeni.RestoreQueue());
            }
            finally
            {
                view.Close();
                pencere.Close();
            }
        });
        Directory.Delete(kok, true);

        Assert.True(sonuc.acikken);
        Assert.False(sonuc.kapaliyken);
        Assert.False(sonuc.Geldi);
    }

    [Fact]
    public void PencereOynaticiSekmesindeKuyruguBirKezGetirir()
    {
        var kok = Klasor("pencere");
        foreach (var ad in new[] { "a.mp3", "b.mp3" }) File.WriteAllBytes(Path.Combine(kok, ad), new byte[16]);
        string Y(string ad) => Path.Combine(kok, ad);
        var kuyruk = Path.Combine(TestAyarYolu.Klasor, PlayerQueue.FileName);
        var eski = PlayerView.QueueRestoreDisabled;

        var sonuc = AppHost.Run(() =>
        {
            PlayerQueue.Save(kuyruk, new PlayerQueue(new[] { Y("a.mp3"), Y("b.mp3") }, Y("b.mp3"), false, null));
            var window = new MainWindow();
            try
            {
                PlayerView.QueueRestoreDisabled = false;
                window.PlayerTab.EngineFactory = () => new SesMotoru(MediaTags.Empty);
                window.Show();
                window.Tabs.SelectedIndex = window.Tabs.Items.IndexOf(window.TabShrink);
                var baska = window.PlayerTab.LoadedPath;
                window.Tabs.SelectedIndex = window.PlayerTabIndex;
                Bekle(window.PlayerTab, window.PlayerTab.Navigation);
                var gelen = (window.PlayerTab.LoadedPath, Kuyruk: window.PlayerTab.Queue?.ToArray(), window.PlayerTab.IsPlaying);
                return (baska, gelen, Tekrar: window.PlayerTab.RestoreQueue());
            }
            finally
            {
                PlayerView.QueueRestoreDisabled = eski;
                window.Close();
                File.Delete(kuyruk);
            }
        });
        Directory.Delete(kok, true);

        Assert.Null(sonuc.baska);
        Assert.Equal(Y("b.mp3"), sonuc.gelen.LoadedPath);
        Assert.Equal(new[] { Y("a.mp3"), Y("b.mp3") }, sonuc.gelen.Kuyruk);
        Assert.False(sonuc.gelen.IsPlaying);
        Assert.False(sonuc.Tekrar);
    }

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "oynatici-kuyruk-saklama", ad, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Bekle(PlayerView view, Task gorev)
    {
        DenetimSurucu.Pump(view, () => gorev.IsCompleted, 10);
        gorev.GetAwaiter().GetResult();
        DenetimSurucu.Wait(view, 0.1);
    }
}
