using System.Globalization;
using System.Text;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

public sealed class CalmaListesiTests
{
    private static readonly string Kok = Path.Combine(TestPaths.OutputRoot, "calma-listesi-sanal", "muzik");

    private static string K(params string[] parca) => Path.GetFullPath(Path.Combine(parca.Prepend(Kok).ToArray()));

    [Fact]
    public void M3u_yorumu_atar_goreli_mutlak_ve_adresi_cozer()
    {
        var mutlak = K("baska", "x.flac");
        var dosyaUri = new Uri(K("bosluk li.mp3")).AbsoluteUri;
        var metin = string.Join("\r\n",
            "#EXTM3U",
            "#EXTINF:120,Sanatci - Parca",
            "a.mp3",
            "  alt\\b.ogg  ",
            "",
            mutlak,
            dosyaUri,
            "http://radyo.example/akis",
            "ic.m3u8",
            "A.mp3");

        var girdiler = PlaylistFile.Parse(metin, "m3u", Kok);

        var beklenen = new List<string> { K("a.mp3"), K("alt", "b.ogg"), mutlak, K("bosluk li.mp3"), "http://radyo.example/akis" };
        if (!OperatingSystem.IsWindows()) beklenen.Add(K("A.mp3"));
        Assert.Equal(beklenen, girdiler);
    }

    [Fact]
    public void Pls_girdileri_sira_numarasina_gore_dizer()
    {
        var metin = "[playlist]\nFile2=iki.mp3\nTitle2=Iki\nfile10 = on.mp3\nFile1=bir.mp3\nNumberOfEntries=3\nVersion=2\n";

        var girdiler = PlaylistFile.Parse(metin, "pls", Kok);

        Assert.Equal(new[] { K("bir.mp3"), K("iki.mp3"), K("on.mp3") }, girdiler);
    }

    [Fact]
    public void Wpl_media_src_kodunu_acar_ust_klasoru_cozer()
    {
        var metin = """
            <?wpl version="1.0"?>
            <smil><head><title>Liste</title></head><body><seq>
              <media src="..\ust\a&amp;b.mp3" tid="{1}"/>
              <media tid="{2}" src='c.wma'/>
            </seq></body></smil>
            """;

        var girdiler = PlaylistFile.Parse(metin, "wpl", Kok);

        Assert.Equal(new[] { K("..", "ust", "a&b.mp3"), K("c.wma") }, girdiler);
    }

    [Fact]
    public void Asx_ref_href_buyuk_harfte_ve_tek_tirnakta_okunur()
    {
        var metin = "<ASX version=\"3.0\"><Entry><REF HREF='x.wma'/></Entry><entry><Ref href=\"http://h.example/y\"/></entry></ASX>";

        var girdiler = PlaylistFile.Parse(metin, "asx", Kok);

        Assert.Equal(new[] { K("x.wma"), "http://h.example/y" }, girdiler);
    }

    [Fact]
    public void Bomsuz_ansi_liste_bolgenin_kod_sayfasiyla_okunur()
    {
        var bayt = new byte[] { 0xFE, 0x61, 0x72, 0x6B, 0xFD, 0x2E, 0x6D, 0x70, 0x33 };
        var onceki = CultureInfo.CurrentCulture;
        string tr, en;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            tr = PlaylistFile.Decode(bayt);
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            en = PlaylistFile.Decode(bayt);
        }
        finally { CultureInfo.CurrentCulture = onceki; }

        Assert.Equal("şarkı.mp3", tr);
        Assert.NotEqual(tr, en);
        Assert.Equal("şarkı.mp3", PlaylistFile.Decode(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("şarkı.mp3")).ToArray()));
        Assert.Equal("şarkı.mp3", PlaylistFile.Decode(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("şarkı.mp3")).ToArray()));
        Assert.Equal("şarkı.mp3", PlaylistFile.Decode(Encoding.UTF8.GetBytes("şarkı.mp3")));
    }

    [Fact]
    public void Calinabilir_girdi_diskte_olmayani_atar_adresi_tutar()
    {
        var klasor = Klasor("calinabilir");
        File.WriteAllBytes(Path.Combine(klasor, "var.mp3"), new byte[1]);
        var liste = Path.Combine(klasor, "liste.m3u8");
        File.WriteAllText(liste, "var.mp3\nyok.mp3\nhttps://h.example/z.mp3\n");

        var girdiler = PlaylistFile.ReadPlayable(liste);
        Directory.Delete(klasor, true);

        Assert.Equal(new[] { Path.Combine(klasor, "var.mp3"), "https://h.example/z.mp3" }, girdiler);
    }

    [Fact]
    public void Liste_uzantilari_oynaticiya_aittir_videoyla_sesle_cakismaz()
    {
        Assert.Empty(ShellIntegration.PlaylistExtensions.Intersect(ShellIntegration.MediaExtensions));
        Assert.Empty(ShellIntegration.PlaylistExtensions.Intersect(ShellIntegration.AudioExtensions));
        Assert.True(ShellIntegration.IsPlayerOnly(@"C:\m\Liste.M3U8"));
        Assert.True(ShellIntegration.IsPlayerOnly("k.wpl"));
        Assert.False(ShellIntegration.IsPlayerOnly("film.mp4"));
    }

    [Fact]
    public void Acilan_liste_kuyruk_olur_sonraki_klasoru_degil_listeyi_izler()
    {
        var klasor = Klasor("kuyruk");
        foreach (var ad in new[] { "a.mp3", "b.mp3", "c.mp3" }) File.WriteAllBytes(Path.Combine(klasor, ad), new byte[16]);
        var liste = Path.Combine(klasor, "liste.m3u");
        File.WriteAllText(liste, "#EXTM3U\nc.mp3\neksik.mp3\na.mp3\n");
        string Y(string ad) => Path.Combine(klasor, ad);

        var sonuc = AppHost.Run(() =>
        {
            var view = new PlayerView { EngineFactory = () => new SesMotoru(MediaTags.Empty) };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            try
            {
                Bekle(view, view.OpenAsync(liste));
                var ilk = (view.LoadedPath, view.Queue?.ToArray(), view.QueueSource, view.PlaylistFiles().ToArray());

                view.Apply(new PlayerCommand(PlayerCommandKind.FileStep, 1));
                Bekle(view, view.Navigation);
                var ikinci = view.LoadedPath;

                view.Apply(new PlayerCommand(PlayerCommandKind.FileStep, 1));
                Bekle(view, view.Navigation);
                var sonda = view.LoadedPath;

                Bekle(view, view.OpenAsync(Y("b.mp3")));
                var disari = (view.LoadedPath, view.Queue, view.PlaylistFiles().ToArray());
                return (ilk, ikinci, sonda, disari);
            }
            finally { pencere.Close(); }
        });
        Directory.Delete(klasor, true);

        Assert.Equal(Y("c.mp3"), sonuc.ilk.LoadedPath);
        Assert.Equal(new[] { Y("c.mp3"), Y("a.mp3") }, sonuc.ilk.Item2);
        Assert.Equal(liste, sonuc.ilk.QueueSource);
        Assert.Equal(new[] { Y("c.mp3"), Y("a.mp3") }, sonuc.ilk.Item4);
        Assert.Equal(Y("a.mp3"), sonuc.ikinci);
        Assert.Equal(Y("a.mp3"), sonuc.sonda);
        Assert.Equal(Y("b.mp3"), sonuc.disari.LoadedPath);
        Assert.Null(sonuc.disari.Queue);
        Assert.Equal(new[] { Y("a.mp3"), Y("b.mp3"), Y("c.mp3") }, sonuc.disari.Item3);
    }

    [Fact]
    public void Calinabilir_girdisi_olmayan_liste_acilis_hatasi_verir()
    {
        var klasor = Klasor("bos");
        var liste = Path.Combine(klasor, "bos.pls");
        File.WriteAllText(liste, "[playlist]\nFile1=yok.mp3\nNumberOfEntries=1\n");

        var hata = AppHost.Run(() =>
        {
            var view = new PlayerView { EngineFactory = () => new SesMotoru(MediaTags.Empty) };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            try
            {
                var acilis = view.OpenAsync(liste);
                DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
                return (acilis.Exception?.InnerException, view.LoadedPath, view.Queue);
            }
            finally { pencere.Close(); }
        });
        Directory.Delete(klasor, true);

        Assert.IsType<PlaybackOpenException>(hata.InnerException);
        Assert.Null(hata.LoadedPath);
        Assert.Null(hata.Queue);
    }

    [Fact]
    public void Acilista_verilen_liste_oynaticida_calar_kucultmeye_yuklenmez()
    {
        var klasor = Klasor("acilis");
        File.WriteAllBytes(Path.Combine(klasor, "s.mp3"), new byte[16]);
        var liste = Path.Combine(klasor, "liste.wpl");
        File.WriteAllText(liste, "<smil><body><seq><media src=\"s.mp3\"/></seq></body></smil>");

        var sonuc = AppHost.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                window.PlayerTab.EngineFactory = () => new SesMotoru(MediaTags.Empty);
                window.Show();
                var is1 = window.LoadStartupFileAsync(liste);
                DenetimSurucu.Pump(window.PlayerTab, () => is1.IsCompleted, 30);
                return (window.SourceStatusVisible, window.ShrinkLoadedPath, window.PlayerTab.LoadedPath, window.PlayerTab.QueueSource);
            }
            finally { window.Close(); }
        });
        Directory.Delete(klasor, true);

        Assert.False(sonuc.SourceStatusVisible);
        Assert.Null(sonuc.ShrinkLoadedPath);
        Assert.Equal(Path.Combine(klasor, "s.mp3"), sonuc.LoadedPath);
        Assert.Equal(liste, sonuc.QueueSource);
    }

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "calma-listesi", ad, Guid.NewGuid().ToString("N"));
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
