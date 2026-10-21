using System.Text;
using Avalonia.Controls;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;
using Xunit;
using L = VidShrink.Tests.OynaticiListeTests;

namespace VidShrink.Tests;

/// <summary>
/// later 138 / OYB-09: adlı oynatma listeleri. Liste ayar klasörünün altında m3u8 dosyasıdır; kaydetme,
/// adlandırma, silme, m3u içe ve dışa aktarma ve eksik dosyanın işaretli kalıp listeyi bozmaması ölçülür.
/// Pencere ölçüsü sahte motorla başsız koşar; ayar klasörü test kökündedir.
/// </summary>
public sealed class AdliListelerTests : IDisposable
{
    private readonly string _kok = Path.Combine(GirdiKanit.Root, ".calisma", "adli-listeler", Guid.NewGuid().ToString("N")[..8]);

    public AdliListelerTests() => Directory.CreateDirectory(_kok);

    public void Dispose() => L.Sil(_kok);

    private string Medya(string ad)
    {
        var klasor = Path.Combine(_kok, "medya");
        Directory.CreateDirectory(klasor);
        var yol = Path.Combine(klasor, ad);
        File.WriteAllBytes(yol, Array.Empty<byte>());
        return yol;
    }

    private string Ayar => Path.Combine(_kok, "ayar");

    private NamedPlaylists Depo() => NamedPlaylists.ForSettingsFolder(Ayar)!;

    private static PlaylistEntry[] Girdi(params string[] yollar) => yollar.Select(yol => new PlaylistEntry(yol)).ToArray();

    [Fact]
    public void ListeAyarKlasorununAltindaDururVeGocleTasinir()
    {
        var depo = Depo();
        Assert.Equal(Path.Combine(Path.GetFullPath(Ayar), NamedPlaylists.FolderName), depo.Folder);
        Assert.Null(NamedPlaylists.ForSettingsFolder(null));
        Assert.Null(NamedPlaylists.ForSettingsFolder(" "));

        Assert.Equal("Akşam", depo.Save("Akşam", Girdi(Medya("a.mp4"))));
        var yeni = Path.Combine(_kok, "yeni-ayar");
        Assert.True(LegacyMigration.CopyData(new LegacyDataFolder(Ayar, yeni), null, DateTimeOffset.UtcNow));

        var tasinan = NamedPlaylists.ForSettingsFolder(yeni)!.List();
        Assert.Equal("Akşam", Assert.Single(tasinan).Name);
        Assert.Equal(1, tasinan[0].Count);
    }

    [Fact]
    public void KaydedilirAdlandirilirSilinir()
    {
        var depo = Depo();
        var a = Medya("a.mp4");
        var b = Medya("b.mp4");
        Assert.Empty(depo.List());
        Assert.Null(depo.Save("Boş", Array.Empty<PlaylistEntry>()));
        Assert.Null(depo.Save("  ", Girdi(a)));

        Assert.Equal("Gece", depo.Save("Gece", Girdi(a, b)));
        Assert.Equal("Sabah", depo.Save("Sabah", Girdi(b)));
        Assert.Equal(new[] { "Gece", "Sabah" }, depo.List().Select(liste => liste.Name).ToArray());
        Assert.Equal(new[] { a, b }, depo.Entries(depo.Find("gece")!.File).Select(girdi => girdi.Path).ToArray());

        Assert.Equal("Gece", depo.Save("GECE", Girdi(b, a)));
        Assert.Equal(2, depo.List().Count);
        Assert.Equal(new[] { b, a }, depo.Entries(depo.Find("Gece")!.File).Select(girdi => girdi.Path).ToArray());

        Assert.Null(depo.Rename("Gece", "Sabah"));
        Assert.Null(depo.Rename("Yok", "Başka"));
        Assert.Equal("Gece Yarısı", depo.Rename("Gece", "Gece Yarısı"));
        Assert.Equal("gece yarısı", depo.Rename("Gece Yarısı", "gece yarısı"));
        Assert.Equal(new[] { "gece yarısı", "Sabah" }, depo.List().Select(liste => liste.Name).ToArray());

        Assert.True(depo.Delete("Sabah"));
        Assert.False(depo.Delete("Sabah"));
        Assert.Equal("gece yarısı", Assert.Single(depo.List()).Name);
        Assert.Empty(Directory.GetFiles(depo.Folder, "*.tmp"));
    }

    [Theory]
    [InlineData("a/b:c", "a_b_c")]
    [InlineData("  Yol  ", "Yol")]
    [InlineData("son nokta. ", "son nokta")]
    [InlineData("CON", "_CON")]
    [InlineData("nul.liste", "_nul.liste")]
    [InlineData("   ", null)]
    [InlineData("///", null)]
    [InlineData("...", null)]
    public void AdDosyaAdinaUyar(string ad, string? beklenen) => Assert.Equal(beklenen, NamedPlaylists.CleanName(ad));

    [Fact]
    public void EksikDosyaIsaretliKalirListeBozulmaz()
    {
        var depo = Depo();
        var a = Medya("a.mp4");
        var b = Medya("b.mp4");
        var adres = "https://ornek.example/akis.m3u8";
        depo.Save("Karışık", Girdi(a, b, adres));
        var dosya = depo.Find("Karışık")!.File;
        var once = File.ReadAllBytes(dosya);

        File.Delete(b);

        var ozet = Assert.Single(depo.List());
        Assert.Equal(3, ozet.Count);
        Assert.Equal(1, ozet.Missing);
        Assert.Equal(new[] { false, true, false }, depo.Entries(dosya).Select(girdi => girdi.Missing).ToArray());
        Assert.Equal(once, File.ReadAllBytes(dosya));
        Assert.Equal(new[] { a, adres }, PlaylistFile.ReadPlayable(dosya));
    }

    [Fact]
    public void M3uIceVeDisaAktarilirEksikGirdiKalir()
    {
        var depo = Depo();
        var a = Medya("a.mp4");
        var dis = Path.Combine(_kok, "dis");
        Directory.CreateDirectory(dis);
        var kaynak = Path.Combine(dis, "Yolculuk.m3u");
        File.WriteAllText(kaynak, "#EXTM3U\n#EXTINF:12,A\n../medya/a.mp4\n../medya/yok.mp4\nhttp://radyo.example/canli\n", new UTF8Encoding(false));
        var yok = Path.GetFullPath(Path.Combine(_kok, "medya", "yok.mp4"));

        Assert.Equal("Yolculuk", depo.Import(kaynak));
        Assert.Equal("Yolculuk (2)", depo.Import(kaynak));
        var girdiler = depo.Entries(depo.Find("Yolculuk")!.File);
        Assert.Equal(new[] { a, yok, "http://radyo.example/canli" }, girdiler.Select(girdi => girdi.Path).ToArray());
        Assert.Equal(new[] { false, true, false }, girdiler.Select(girdi => girdi.Missing).ToArray());

        var bos = Path.Combine(dis, "bos.m3u");
        File.WriteAllText(bos, "#EXTM3U\n");
        Assert.Null(depo.Import(bos));
        Assert.Null(depo.Import(Path.Combine(dis, "hic.m3u")));

        var hedef = Path.Combine(_kok, "medya", "disari.m3u8");
        Assert.True(depo.Export("Yolculuk", hedef));
        Assert.False(depo.Export("Yok", hedef));
        var satirlar = File.ReadAllText(hedef).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("a.mp4", satirlar);
        Assert.Contains("yok.mp4", satirlar);
        Assert.Equal(girdiler.Select(girdi => girdi.Path).ToArray(), PlaylistFile.Read(hedef));
    }

    [Fact]
    public void PencereKuyruguKaydederAcarVeEksigiIsaretler()
    {
        var a = Medya("a1.mp4");
        var b = Medya("a2.mp4");
        var c = Medya("a3.mp4");
        Directory.CreateDirectory(Ayar);
        var gecmis = Path.Combine(Ayar, "history.json");
        AppHost.Run(() =>
        {
            var view = new PlayerView { EngineFactory = () => new YolMotoru(), HistoryPath = () => gecmis };
            var window = new Window { Width = 960, Height = 540, Content = view };
            window.Show();
            PlaylistsWindow? pencere = null;
            try
            {
                var acilis = view.OpenAsync(b);
                DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
                acilis.GetAwaiter().GetResult();

                pencere = view.ShowPlaylists()!;
                Assert.Empty(pencere.Shown);
                pencere.NameField.Text = "Üçlü";
                Assert.True(pencere.SaveCurrent());
                var kayit = Assert.Single(pencere.Shown);
                Assert.Equal("Üçlü", kayit.Name);
                Assert.Equal(3, kayit.Count);
                Assert.StartsWith(Path.Combine(Path.GetFullPath(Ayar), NamedPlaylists.FolderName), kayit.File);

                File.Delete(c);
                pencere.Close();
                pencere = view.ShowPlaylists()!;
                pencere.Select(0);
                Assert.Equal(PlaylistsWindow.Describe(pencere.Shown[0]), Strings.Get("player.playlists.row-missing", "Üçlü", 3, 1));
                Assert.Equal(new[] { false, false, true }, pencere.ShownItems.Select(girdi => girdi.Missing).ToArray());
                Assert.Equal(Strings.Get("player.playlists.missing", "a3.mp4"), pencere.ShownItemTexts[2]);

                Assert.True(pencere.OpenSelected());
                DenetimSurucu.Pump(view, () => view.QueueSource is not null && view.Queue?.Count == 2, 10);
                Assert.Equal(new[] { a, b }, view.Queue);
                Assert.Equal(kayit.File, view.QueueSource);
                Assert.Equal(3, PlaylistFile.Read(kayit.File).Count);
                Assert.Contains("namedlist -> Üçlü", view.Trace);
            }
            finally
            {
                pencere?.Close();
                L.Kapat(view, window);
                Strings.Reset();
            }
        });
    }

    [Fact]
    public void MenulerdeListelerSatiriVar()
    {
        AppHost.Run(() =>
        {
            var view = L.Ac(null, out var window);
            try
            {
                var menu = view.BuildMenu();
                var satir = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Tag, "playlists"));
                Assert.Equal(Strings.Get("player.playlists.menu"), satir.Header);
                Assert.Null(view.ShowPlaylists());
            }
            finally
            {
                L.Kapat(view, window);
            }
        });
    }
}
