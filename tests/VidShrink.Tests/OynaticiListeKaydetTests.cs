using System.Text;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Player;
using Xunit;
using L = VidShrink.Tests.OynaticiListeTests;

namespace VidShrink.Tests;

public sealed class OynaticiListeKaydetTests
{
    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "oynatici-liste-kaydet", ad, Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Sil(string klasor)
    {
        try
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
        catch (IOException)
        {
        }
    }

    private static string[] Satirlar(string dosya) => File.ReadAllText(dosya, Encoding.UTF8).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string[] Artik(string klasor) => Directory.GetFiles(klasor, "*.tmp", SearchOption.AllDirectories);

    private static PlaylistEntry[] Girdi(params string[] yollar) => yollar.Select(yol => new PlaylistEntry(yol)).ToArray();

    [Fact]
    public void YazilanListeAyniSiraylaGeriOkunur()
    {
        var kok = Klasor("gidis-donus");
        try
        {
            var muzik = Path.Combine(kok, "muzik");
            var disari = Path.Combine(kok, "baska", "x #2 %41 ş.flac");
            var yollar = new[]
            {
                Path.Combine(muzik, "şarkı ğüı.mp3"),
                "http://radyo.example/akis?a=1&b=2",
                Path.Combine(muzik, "alt", "derin", "b.ogg"),
                disari,
                Path.Combine(muzik, "#1 numara.mp3"),
                Path.Combine(muzik, " bosluk basta.mp3"),
                Path.Combine(muzik, "yuzde%20li.mp3"),
                "https://h.example/z.mp3",
                Path.Combine(muzik, "a.mp3"),
            };
            Directory.CreateDirectory(muzik);
            var liste = Path.Combine(muzik, "liste.m3u8");

            PlaylistFile.Write(liste, Girdi(yollar));

            Assert.Equal(yollar, PlaylistFile.Read(liste));
            var bayt = File.ReadAllBytes(liste);
            Assert.Equal((byte)'#', bayt[0]);
            Assert.Equal(Encoding.UTF8.GetString(bayt), new UTF8Encoding(false, true).GetString(bayt));
            var satirlar = Satirlar(liste);
            Assert.Equal("#EXTM3U", satirlar[0]);
            var girdiler = satirlar.Where(satir => !satir.StartsWith("#EXT", StringComparison.Ordinal)).ToArray();
            Assert.Equal(yollar.Length, girdiler.Length);
            Assert.Equal("şarkı ğüı.mp3", girdiler[0]);
            Assert.Equal(yollar[1], girdiler[1]);
            Assert.Equal("alt/derin/b.ogg", girdiler[2]);
            Assert.Equal(disari, girdiler[3]);
            Assert.Equal("./#1 numara.mp3", girdiler[4]);
            Assert.Equal("./ bosluk basta.mp3", girdiler[5]);
            Assert.Empty(Artik(kok));
        }
        finally
        {
            Sil(kok);
        }
    }

    [Fact]
    public void KlasorTasininceGoreliGirdilerYeniYerdenCozulur()
    {
        var kok = Klasor("tasima");
        try
        {
            var eski = Path.Combine(kok, "eski");
            var yeni = Path.Combine(kok, "yeni");
            var disari = Path.Combine(kok, "disari.mp4");
            Directory.CreateDirectory(Path.Combine(eski, "alt"));
            PlaylistFile.Write(Path.Combine(eski, "l.m3u8"), Girdi(Path.Combine(eski, "a.mp4"), Path.Combine(eski, "alt", "b.mp4"), disari));

            Directory.Move(eski, yeni);

            Assert.Equal(new[] { Path.Combine(yeni, "a.mp4"), Path.Combine(yeni, "alt", "b.mp4"), disari }, PlaylistFile.Read(Path.Combine(yeni, "l.m3u8")));
        }
        finally
        {
            Sil(kok);
        }
    }

    [Fact]
    public void SureVeBaslikExtinfSatirinaYazilirOkumayiBozmaz()
    {
        var klasor = Path.GetFullPath(Path.Combine(TestPaths.OutputRoot, "oynatici-liste-kaydet-sanal"));
        var girdiler = new[]
        {
            new PlaylistEntry(Path.Combine(klasor, "a.mp3"), 239.6, "Sanatci - Parca"),
            new PlaylistEntry(Path.Combine(klasor, "b.mp3")),
            new PlaylistEntry(Path.Combine(klasor, "c.mp3"), null, "iki\r\nsatir"),
            new PlaylistEntry("http://h.example/akis"),
            new PlaylistEntry("http://h.example/radyo", 0, "Radyo"),
        };

        var metin = PlaylistFile.Format(girdiler, klasor);

        Assert.Equal(new[]
        {
            "#EXTM3U",
            "#EXTINF:240,Sanatci - Parca", "a.mp3",
            "#EXTINF:-1,b", "b.mp3",
            "#EXTINF:-1,iki  satir", "c.mp3",
            "http://h.example/akis",
            "#EXTINF:-1,Radyo", "http://h.example/radyo",
        }, metin.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(girdiler.Select(girdi => girdi.Path), PlaylistFile.Parse(metin, "m3u8", klasor));
    }

    [Fact]
    public void YazmaEskiListeninYerineGecerArtikBirakmaz()
    {
        var klasor = Klasor("ustune");
        try
        {
            var liste = Path.Combine(klasor, "l.m3u8");
            File.WriteAllText(liste, "#EXTM3U\neski1.mp3\neski2.mp3\neski3.mp3\neski4.mp3\n");

            PlaylistFile.Write(liste, Girdi(Path.Combine(klasor, "yeni.mp3")));

            Assert.Equal(new[] { Path.Combine(klasor, "yeni.mp3") }, PlaylistFile.Read(liste));
            Assert.Equal(new[] { liste }, Directory.GetFiles(klasor));
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void DusenYazmaHatayiCikarirGeciciDosyaBirakmaz()
    {
        var klasor = Klasor("dusen");
        try
        {
            var hedef = Path.Combine(klasor, "klasor.m3u8");
            Directory.CreateDirectory(hedef);
            File.WriteAllText(Path.Combine(hedef, "icerik.txt"), "dokunma");

            var hata = Record.Exception(() => PlaylistFile.Write(hedef, Girdi(Path.Combine(klasor, "a.mp3"))));

            Assert.True(hata is IOException or UnauthorizedAccessException, hata?.GetType().Name ?? "hata yok");
            Assert.Equal("dokunma", File.ReadAllText(Path.Combine(hedef, "icerik.txt")));
            Assert.Empty(Artik(klasor));
            Assert.Single(Directory.GetFileSystemEntries(klasor));
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void GorunenSiraDosyayaYazilirAcilincaAyniKuyrukOlur()
    {
        var klasor = L.Klasor("a1.mp4", "a2.mp4", "a3.mp4", "a4.mp4");
        try
        {
            var hedef = Path.Combine(klasor, "alt", "kayit.m3u8");
            Directory.CreateDirectory(Path.GetDirectoryName(hedef)!);
            AppHost.Run(() =>
            {
                var view = L.Ac(Path.Combine(klasor, "a2.mp4"), out var window);
                try
                {
                    Assert.True(view.PlayNext(Path.Combine(klasor, "a4.mp4")));
                    Assert.True(view.RemoveFromList(Path.Combine(klasor, "a1.mp4")));
                    var sira = view.PlaylistFiles().ToArray();
                    Assert.Equal(new[] { "a2.mp4", "a4.mp4", "a3.mp4" }, sira.Select(Path.GetFileName).ToArray());

                    Assert.True(view.SavePlaylist(hedef));

                    Assert.Equal(sira, PlaylistFile.Read(hedef));
                    Assert.Contains(LanguageCatalog.Display(Strings.Get("player.list.saved", hedef)), view.ViewStateText);
                    Assert.Equal("listsave -> 3", view.Trace[^1]);
                    Assert.Contains("#EXTINF:600,a2", Satirlar(hedef));
                    Assert.Contains("#EXTINF:-1,a4", Satirlar(hedef));
                    Assert.Contains(Path.Combine(klasor, "a4.mp4"), Satirlar(hedef));

                    var acilis = view.OpenAsync(hedef);
                    DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    Assert.Equal(sira, view.Queue);
                    Assert.Equal(sira[0], view.LoadedPath);
                    Assert.Equal("kayit.m3u8", view.PlaylistSaveName());
                }
                finally
                {
                    L.Kapat(view, window);
                }
            });
        }
        finally
        {
            L.Sil(klasor);
        }
    }

    [Fact]
    public void CalanParcaninEtiketiVeSuresiYazilir()
    {
        var klasor = L.Klasor("b1.mp3", "b2.mp3");
        try
        {
            var hedef = Path.Combine(klasor, "kayit.m3u8");
            AppHost.Run(() =>
            {
                var view = new PlayerView { EngineFactory = () => new SesMotoru(new MediaTags("Parca", "Sanatci", null)) };
                var window = new Window { Width = 640, Height = 360, Content = view };
                window.Show();
                try
                {
                    var acilis = view.OpenAsync(Path.Combine(klasor, "b1.mp3"));
                    DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();

                    Assert.True(view.SavePlaylist(hedef));

                    Assert.Equal(new[] { "#EXTM3U", "#EXTINF:240,Sanatci - Parca", "b1.mp3", "#EXTINF:-1,b2", "b2.mp3" }, Satirlar(hedef));
                    Assert.Equal(Path.GetFileName(klasor) + ".m3u8", view.PlaylistSaveName());
                }
                finally
                {
                    L.Kapat(view, window);
                }
            });
        }
        finally
        {
            L.Sil(klasor);
        }
    }

    [Fact]
    public void YazilamayanHedefIstisnaSizdirmazDurumSatirinaDuser()
    {
        var klasor = L.Klasor("c1.mp4", "c2.mp4");
        try
        {
            var hedef = Path.Combine(klasor, "olmayan-klasor", "kayit.m3u8");
            var dizin = Path.Combine(klasor, "dizin.m3u8");
            Directory.CreateDirectory(dizin);
            AppHost.Run(() =>
            {
                var view = L.Ac(Path.Combine(klasor, "c1.mp4"), out var window);
                try
                {
                    var hata = Record.Exception(() => Assert.False(view.SavePlaylist(hedef)));
                    Assert.Null(hata);
                    Assert.False(File.Exists(hedef));
                    Assert.Contains(LanguageCatalog.Display(Strings.Get("player.list.save-failed")), view.ViewStateText);
                    Assert.Equal("listsave -> no", view.Trace[^1]);

                    Assert.True(view.SavePlaylist(Path.Combine(klasor, "olur.m3u8")));
                    Assert.DoesNotContain(LanguageCatalog.Display(Strings.Get("player.list.save-failed")), view.ViewStateText);

                    Assert.Null(Record.Exception(() => Assert.False(view.SavePlaylist(dizin))));
                    Assert.Contains(LanguageCatalog.Display(Strings.Get("player.list.save-failed")), view.ViewStateText);
                    Assert.Empty(Artik(klasor));
                }
                finally
                {
                    L.Kapat(view, window);
                }
            });
        }
        finally
        {
            L.Sil(klasor);
        }
    }

    [Fact]
    public void BosListedeKaydetSatiriPasifDoluListedeEtkin()
    {
        var klasor = L.Klasor("d1.mp4", "d2.mp4");
        try
        {
            var hedef = Path.Combine(klasor, "bos.m3u8");
            AppHost.Run(() =>
            {
                var bos = L.Ac(null, out var bosPencere);
                try
                {
                    var satir = bos.BuildItemMenu(Path.Combine(klasor, "d1.mp4")).Items.OfType<MenuItem>().Single(oge => (string?)oge.Tag == "save");
                    Assert.False(satir.IsEnabled, "bos listede kaydet satiri etkin");
                    Assert.False(bos.CanSavePlaylist);
                    Assert.False(bos.SavePlaylist(hedef));
                    Assert.False(File.Exists(hedef));
                    var secim = bos.PickPlaylistTargetAsync();
                    Assert.True(secim.IsCompleted, "bos listede secici acildi");
                }
                finally
                {
                    L.Kapat(bos, bosPencere);
                }

                var dolu = L.Ac(Path.Combine(klasor, "d1.mp4"), out var doluPencere);
                try
                {
                    var ogeler = dolu.BuildItemMenu(Path.Combine(klasor, "d2.mp4")).Items;
                    var satir = ogeler.OfType<MenuItem>().Last();
                    Assert.Equal("save", satir.Tag);
                    Assert.Equal(Strings.Get("player.list.save"), satir.Header);
                    Assert.NotEqual("player.list.save", satir.Header);
                    Assert.True(satir.IsEnabled, "dolu listede kaydet satiri pasif");
                    Assert.IsType<Separator>(ogeler[^2]);
                }
                finally
                {
                    L.Kapat(dolu, doluPencere);
                }
            });
        }
        finally
        {
            L.Sil(klasor);
        }
    }
}
