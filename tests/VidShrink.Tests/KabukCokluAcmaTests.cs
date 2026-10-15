using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Integration;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kabukta çoklu açma ve sıraya ekleme: ikinci örnek bütün yolları ve bayrağı kanaldan
/// aktarır, çalışan örnek art arda gelen yolları tek kuyrukta toplar, "sıraya ekle" çalan
/// listeyi kesmeden sona ekler.
/// </summary>
public sealed class KabukCokluAcmaTests
{
    [Fact]
    public void KanalBayragiBasaKoyarBayraksizIstekEskiDiziyleAyni()
    {
        var eski = new ForwardedRequest(new[] { @"C:\a.mp4", @"C:\b.mp4" }, false);
        Assert.Equal(new[] { @"C:\a.mp4", @"C:\b.mp4" }, eski.Encode());
        Assert.False(eski.Single);

        var sira = new ForwardedRequest(new[] { @"C:\a.mp3", @"C:\b.mp3" }, true);
        Assert.Equal(new[] { ShellIntegration.EnqueueFlag, @"C:\a.mp3", @"C:\b.mp3" }, sira.Encode());

        var cozulen = ForwardedRequest.Decode(sira.Encode());
        Assert.True(cozulen.Enqueue);
        Assert.Equal(new[] { @"C:\a.mp3", @"C:\b.mp3" }, cozulen.Paths);

        var kirli = ForwardedRequest.Decode(new[] { "", " \"C:\\c.mp4\" ", "  " });
        Assert.False(kirli.Enqueue);
        Assert.True(kirli.Single);
        Assert.Equal(@"C:\c.mp4", kirli.First);
    }

    [Fact]
    public void KomutSatiriBayragiYolunArkasindaTanirButunYollariToplar()
    {
        var mevcut = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\x\a.mp3", @"C:\x\b c.mp3", @"C:\x\d.mp4" };
        bool Var(string yol) => mevcut.Contains(yol);

        var sira = ForwardedRequest.FromArguments(new[] { @"C:\x\a.mp3", ShellIntegration.EnqueueFlag }, null, Var);
        Assert.True(sira.Enqueue);
        Assert.Equal(new[] { @"C:\x\a.mp3" }, sira.Paths);

        var coklu = ForwardedRequest.FromArguments(new[] { @"C:\x\d.mp4", @"C:\x\b", "c.mp3", @"C:\x\a.mp3" }, null, Var);
        Assert.False(coklu.Enqueue);
        Assert.Equal(new[] { @"C:\x\d.mp4", @"C:\x\b c.mp3", @"C:\x\a.mp3" }, coklu.Paths);

        var adres = ForwardedRequest.FromArguments(new[] { "https://ornek.test/v.mp4" }, "https://ornek.test/v.mp4", Var);
        Assert.True(adres.Single);
        Assert.Equal("https://ornek.test/v.mp4", adres.First);

        Assert.Empty(ForwardedRequest.FromArguments(Array.Empty<string>(), null, Var).Paths);
    }

    [Fact]
    public void SirayaEkleBoskenIlkiCalarDolukenSonaEklerOynatmayiKesmez()
    {
        var kok = Klasor("sira");
        Yaz(kok, "a.mp3", "b.mp3", "c.mp3", "d.mp3");
        string Y(string ad) => Path.Combine(kok, ad);

        var sonuc = AppHost.Run(() =>
        {
            var view = new PlayerView { EngineFactory = () => new SesMotoru(MediaTags.Empty) };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            try
            {
                var bos = view.EnqueueItems(new[] { Y("b.mp3"), Y("a.mp3") });
                Bekle(view, view.Navigation);
                var ilk = (bos, view.LoadedPath, Kuyruk: view.Queue?.ToArray());

                var dolu = view.EnqueueItems(new[] { Y("d.mp3"), Y("c.mp3") });
                DenetimSurucu.Wait(view, 0.1);
                var ikinci = (dolu, view.LoadedPath, Liste: view.PlaylistFiles().ToArray());
                return (ilk, ikinci, Yok: view.EnqueueItems(new[] { Y("yok.mp3") }));
            }
            finally
            {
                view.Close();
                pencere.Close();
            }
        });
        Directory.Delete(kok, true);

        Assert.True(sonuc.ilk.bos);
        Assert.Equal(Y("a.mp3"), sonuc.ilk.LoadedPath);
        Assert.Equal(new[] { Y("a.mp3"), Y("b.mp3") }, sonuc.ilk.Kuyruk);
        Assert.True(sonuc.ikinci.dolu);
        Assert.Equal(Y("a.mp3"), sonuc.ikinci.LoadedPath);
        Assert.Equal(new[] { Y("a.mp3"), Y("b.mp3"), Y("c.mp3"), Y("d.mp3") }, sonuc.ikinci.Liste);
        Assert.False(sonuc.Yok);
    }

    [Fact]
    public void TopluAcilisCalanDosyaListedeyseKesmezDegilseIlktenBaslar()
    {
        var kok = Klasor("toplu");
        Yaz(kok, "a.mp3", "b.mp3", "c.mp3", "x.mp3");
        string Y(string ad) => Path.Combine(kok, ad);

        var sonuc = AppHost.Run(() =>
        {
            var view = new PlayerView { EngineFactory = () => new SesMotoru(MediaTags.Empty) };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            try
            {
                var acilis = view.OpenAsync(Y("b.mp3"));
                Bekle(view, acilis);
                var gezinme = view.Navigation;
                var kesmeden = view.OpenShellBatch(new[] { Y("b.mp3"), Y("c.mp3"), Y("a.mp3") });
                var ilk = (kesmeden, AyniGezinme: ReferenceEquals(gezinme, view.Navigation), view.LoadedPath, Kuyruk: view.Queue?.ToArray());

                var yeniden = view.OpenShellBatch(new[] { Y("x.mp3"), Y("c.mp3") });
                Bekle(view, view.Navigation);
                return (ilk, ikinci: (yeniden, view.LoadedPath, Kuyruk: view.Queue?.ToArray()), Iz: view.Trace.ToArray());
            }
            finally
            {
                view.Close();
                pencere.Close();
            }
        });
        Directory.Delete(kok, true);

        Assert.True(sonuc.ilk.kesmeden);
        Assert.True(sonuc.ilk.AyniGezinme);
        Assert.Equal(Y("b.mp3"), sonuc.ilk.LoadedPath);
        Assert.Equal(new[] { Y("a.mp3"), Y("b.mp3"), Y("c.mp3") }, sonuc.ilk.Kuyruk);
        Assert.True(sonuc.ikinci.yeniden);
        Assert.Equal(Y("c.mp3"), sonuc.ikinci.LoadedPath);
        Assert.Equal(new[] { Y("c.mp3"), Y("x.mp3") }, sonuc.ikinci.Kuyruk);
        Assert.Contains("shell -> 3", sonuc.Iz);
        Assert.Contains("shell -> 2", sonuc.Iz);
    }

    [Fact]
    public void PencereArtArdaGelenYollariTekKuyruktaToplarBayrakliIstekSonaEkler()
    {
        var kok = Klasor("pencere");
        Yaz(kok, "a.mp3", "b.mp3", "c.mp3", "d.mp3");
        string Y(string ad) => Path.Combine(kok, ad);
        var eskiPencere = MainWindow.ShellBatchWindow;

        var sonuc = AppHost.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                MainWindow.ShellBatchWindow = TimeSpan.FromMinutes(5);
                window.PlayerTab.EngineFactory = () => new SesMotoru(MediaTags.Empty);
                window.Show();

                Assert.True(window.AcceptForwarded(ForwardedRequest.Decode(new[] { Y("a.mp3") })));
                Assert.True(window.AcceptForwarded(ForwardedRequest.Decode(new[] { Y("b.mp3") })));
                Bekle(window, window.ForwardedLoad);
                var toplu = (window.PlayerTab.LoadedPath, Kuyruk: window.PlayerTab.Queue?.ToArray());

                MainWindow.ShellBatchWindow = TimeSpan.Zero;
                Assert.True(window.AcceptForwarded(ForwardedRequest.Decode(new[] { ShellIntegration.EnqueueFlag, Y("c.mp3") })));
                Bekle(window, window.ForwardedLoad);
                var sira = (window.PlayerTab.LoadedPath, Liste: window.PlayerTab.PlaylistFiles().ToArray());

                Assert.True(window.AcceptForwarded(ForwardedRequest.Decode(new[] { Y("d.mp3") })));
                Bekle(window, window.ForwardedLoad);
                return (toplu, sira, Tek: window.PlayerTab.LoadedPath);
            }
            finally
            {
                MainWindow.ShellBatchWindow = eskiPencere;
                window.Close();
            }
        });
        Directory.Delete(kok, true);

        Assert.Equal(Y("a.mp3"), sonuc.toplu.LoadedPath);
        Assert.Equal(new[] { Y("a.mp3"), Y("b.mp3") }, sonuc.toplu.Kuyruk);
        Assert.Equal(Y("a.mp3"), sonuc.sira.LoadedPath);
        Assert.Equal(new[] { Y("a.mp3"), Y("b.mp3"), Y("c.mp3") }, sonuc.sira.Liste);
        Assert.Equal(Y("d.mp3"), sonuc.Tek);
    }

    private static void Yaz(string klasor, params string[] adlar)
    {
        foreach (var ad in adlar) File.WriteAllBytes(Path.Combine(klasor, ad), new byte[16]);
    }

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "kabuk-coklu-acma", ad, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Bekle(PlayerView view, Task gorev)
    {
        DenetimSurucu.Pump(view, () => gorev.IsCompleted, 10);
        gorev.GetAwaiter().GetResult();
        DenetimSurucu.Wait(view, 0.1);
    }

    private static void Bekle(MainWindow window, Task? gorev)
    {
        Assert.NotNull(gorev);
        DenetimSurucu.Pump(window.PlayerTab, () => gorev!.IsCompleted, 30);
        gorev!.GetAwaiter().GetResult();
        DenetimSurucu.Wait(window.PlayerTab, 0.1);
    }
}
