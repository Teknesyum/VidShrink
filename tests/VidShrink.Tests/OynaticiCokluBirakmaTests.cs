using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

public sealed class OynaticiCokluBirakmaTests
{
    private const BindingFlags Her = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [Fact]
    public void ToplayiciDosyalariVeKlasorleriDogalSiradaAltKlasorlerleAlir()
    {
        var kok = Klasor("toplayici");
        try
        {
            var klasor = Path.Combine(kok, "albüm");
            Yaz(klasor, "parca10.mp3", "parca2.mp3", "parca1.flac", "notlar.txt", "liste.m3u", "kapak.jpg");
            Yaz(Path.Combine(klasor, "disk 10"), "son.mkv");
            Yaz(Path.Combine(klasor, "disk 2"), "orta.mp4");
            Yaz(Path.Combine(klasor, "disk 2", "ek"), "derin.ogg");
            Yaz(Path.Combine(klasor, "bos"));
            var gizli = Path.Combine(klasor, ".gizli.mp3");
            File.WriteAllBytes(gizli, new byte[16]);
            File.SetAttributes(gizli, FileAttributes.Hidden);
            Yaz(kok, "tek10.mp4", "tek9.mp4");
            string Y(params string[] parca) => Path.Combine(parca.Prepend(kok).ToArray());

            var sonuc = FolderNavigator.Collect(new[]
            {
                klasor,
                Y("tek10.mp4"),
                Y("yok.mp4"),
                Y("tek9.mp4"),
                Y("tek9.mp4"),
                Y("albüm", "liste.m3u")
            });

            Assert.Equal(new[]
            {
                Y("tek9.mp4"),
                Y("tek10.mp4"),
                Y("albüm", "parca1.flac"),
                Y("albüm", "parca2.mp3"),
                Y("albüm", "parca10.mp3"),
                Y("albüm", "disk 2", "orta.mp4"),
                Y("albüm", "disk 2", "ek", "derin.ogg"),
                Y("albüm", "disk 10", "son.mkv")
            }, sonuc);
            Assert.Empty(FolderNavigator.Collect(new[] { Path.Combine(klasor, "bos"), Y("yok.mp4"), Y("albüm", "notlar.txt") }));
        }
        finally
        {
            Directory.Delete(kok, true);
        }
    }

    [Fact]
    public void BirakilanKlasorVeDosyalarKuyrukOlurIlkiCalarSonrakiKuyruguIzler()
    {
        var kok = Klasor("birak");
        var klasor = Path.Combine(kok, "seri");
        Yaz(klasor, "b10.mp3", "b2.mp3", "b1.mp3", "oku.txt");
        Yaz(Path.Combine(klasor, "alt"), "c.mp3");
        Yaz(kok, "tek.mp3", "yalniz.mp3");
        string Y(params string[] parca) => Path.Combine(parca.Prepend(kok).ToArray());

        var sonuc = AppHost.Run(() =>
        {
            var view = new PlayerView { EngineFactory = () => new SesMotoru(MediaTags.Empty) };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            try
            {
                var klasorOgesi = pencere.StorageProvider.TryGetFolderFromPathAsync(new Uri(klasor));
                DenetimSurucu.Pump(view, () => klasorOgesi.IsCompleted, 5);
                var ogeler = new[] { klasorOgesi.Result!, ParcaKanit.Dosya(Y("tek.mp3")) };

                var uzerinde = Olay(view, DragDrop.DragOverEvent, ogeler).DragEffects;
                Olay(view, DragDrop.DropEvent, ogeler);
                Bekle(view, view.Navigation);
                var ilk = (view.LoadedPath, Kuyruk: view.Queue?.ToArray(), view.QueueSource, Liste: view.PlaylistFiles().ToArray());

                view.Apply(new PlayerCommand(PlayerCommandKind.FileStep, 1));
                Bekle(view, view.Navigation);
                var ikinci = view.LoadedPath;

                Olay(view, DragDrop.DropEvent, new[] { ParcaKanit.Dosya(Y("yalniz.mp3")) });
                Bekle(view, view.Navigation);
                var tek = (view.LoadedPath, view.Queue, Iz: view.Trace.ToArray());
                return (uzerinde, ilk, ikinci, tek);
            }
            finally
            {
                view.Close();
                pencere.Close();
            }
        });
        Directory.Delete(kok, true);

        Assert.Equal(DragDropEffects.Copy, sonuc.uzerinde);
        var beklenen = new[] { Y("tek.mp3"), Y("seri", "b1.mp3"), Y("seri", "b2.mp3"), Y("seri", "b10.mp3"), Y("seri", "alt", "c.mp3") };
        Assert.Equal(beklenen, sonuc.ilk.Kuyruk);
        Assert.Equal(beklenen, sonuc.ilk.Liste);
        Assert.Equal(Y("tek.mp3"), sonuc.ilk.LoadedPath);
        Assert.Null(sonuc.ilk.QueueSource);
        Assert.Equal(Y("seri", "b1.mp3"), sonuc.ikinci);
        Assert.Equal(Y("yalniz.mp3"), sonuc.tek.LoadedPath);
        Assert.Null(sonuc.tek.Queue);
        Assert.Contains("drop -> yalniz.mp3", sonuc.tek.Iz);
    }

    [Fact]
    public void OynatilabilirDosyasiOlmayanKlasorBirakilincaHicbirSeyAcilmaz()
    {
        var kok = Klasor("bos");
        Yaz(kok, "oku.txt");
        var sonuc = AppHost.Run(() =>
        {
            var view = new PlayerView { EngineFactory = () => new SesMotoru(MediaTags.Empty) };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            try
            {
                var oge = pencere.StorageProvider.TryGetFolderFromPathAsync(new Uri(kok));
                DenetimSurucu.Pump(view, () => oge.IsCompleted, 5);
                Olay(view, DragDrop.DropEvent, new IStorageItem[] { oge.Result! });
                DenetimSurucu.Wait(view, 0.2);
                return (view.LoadedPath, view.Queue, acti: view.OpenFolder(kok));
            }
            finally
            {
                view.Close();
                pencere.Close();
            }
        });
        Directory.Delete(kok, true);

        Assert.Null(sonuc.LoadedPath);
        Assert.Null(sonuc.Queue);
        Assert.False(sonuc.acti);
    }

    [Fact]
    public void MenudekiKlasorAcSatiriSecilenKlasoruKuyrugaAlir()
    {
        var kok = Klasor("menu");
        Yaz(kok, "x2.mp3", "x1.mp3");
        Yaz(Path.Combine(kok, "alt"), "y.mp3");
        string Y(params string[] parca) => Path.Combine(parca.Prepend(kok).ToArray());

        var sonuc = AppHost.Run(() =>
        {
            var view = new PlayerView { EngineFactory = () => new SesMotoru(MediaTags.Empty) };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            try
            {
                var gercek = pencere.StorageProvider;
                var sahte = DispatchProxy.Create<IStorageProvider, SahteKlasorSecici>();
                var secici = (SahteKlasorSecici)(object)sahte;
                secici.Gercek = gercek;
                var bul = gercek.TryGetFolderFromPathAsync(new Uri(kok));
                DenetimSurucu.Pump(view, () => bul.IsCompleted, 5);
                secici.Donus = new[] { bul.Result! };
                typeof(TopLevel).GetField("_storageProvider", Her)!.SetValue(pencere, sahte);

                var basliklar = view.BuildMenu().Items.OfType<MenuItem>().Select(item => item.Header as string).ToList();
                var satir = view.BuildMenu().Items.OfType<MenuItem>().Single(item => Equals(item.Header, Strings.Get("player.menu.open-folder")));
                satir.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                DenetimSurucu.Pump(view, () => view.LoadedPath is not null, 10);
                Bekle(view, view.Navigation);
                return (basliklar, secici.Cagri, view.LoadedPath, Kuyruk: view.Queue?.ToArray());
            }
            finally
            {
                view.Close();
                pencere.Close();
            }
        });
        Directory.Delete(kok, true);

        var yeri = sonuc.basliklar.IndexOf(Strings.Get("player.menu.open-folder"));
        Assert.Equal(sonuc.basliklar.IndexOf(Strings.Get("player.list.recent")) - 1, yeri);
        Assert.Equal(1, sonuc.Cagri);
        Assert.Equal(Y("x1.mp3"), sonuc.LoadedPath);
        Assert.Equal(new[] { Y("x1.mp3"), Y("x2.mp3"), Y("alt", "y.mp3") }, sonuc.Kuyruk);
    }

    [Fact]
    public void KlasorAcAnahtariButunDillerde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var degerler = Locales.Domain(dil, "playback");
            Assert.True(degerler.TryGetValue("player.menu.open-folder", out var deger) && !string.IsNullOrWhiteSpace(deger), dil);
            Assert.NotEqual(degerler["player.menu.reveal"], deger);
        }

        Assert.Equal("Klasör aç", Locales.Domain("tr", "playback")["player.menu.open-folder"]);
        Assert.Equal("Open folder", Locales.Domain("en", "playback")["player.menu.open-folder"]);
    }

    private static DragEventArgs Olay(PlayerView view, RoutedEvent<DragEventArgs> olay, IEnumerable<IStorageItem> ogeler)
    {
        var aktarim = new DataTransfer();
        foreach (var oge in ogeler) aktarim.Add(DataTransferItem.CreateFile(oge));
        var args = new DragEventArgs(olay, aktarim, view, new Point(1, 1), KeyModifiers.None);
        view.RaiseEvent(args);
        return args;
    }

    private static void Yaz(string klasor, params string[] adlar)
    {
        Directory.CreateDirectory(klasor);
        foreach (var ad in adlar) File.WriteAllBytes(Path.Combine(klasor, ad), new byte[16]);
    }

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(TipSources.Root, ".calisma", "worktree-agent-a9ce7984706fa7581", "coklu-birakma", ad, Guid.NewGuid().ToString("N"));
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
