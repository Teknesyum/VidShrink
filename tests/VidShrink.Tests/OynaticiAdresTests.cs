using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Oynatıcıda ağ adresi: panodan, sürüklemeyle ve açılış argümanından açma, sorgunun kayda
/// sızmaması, yola bağlı özelliklerin adreste nedenini söylemesi. Ağa çıkan ölçü yok; gerçek
/// motorlu iki ölçü <see cref="MiniSunucu"/>'ya (127.0.0.1) bağlanır.
/// </summary>
public sealed class OynaticiAdresTests
{
    private const string Gizli = "gizli-anahtar-7f3a";
    private const string Ornek = "http://127.0.0.1:9/yol/klip%20bir.mp4";

    private static string Gecici()
    {
        var path = Path.Combine(GirdiKanit.Root, ".calisma", "url-oynatma", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Sil(string klasor)
    {
        try
        {
            Directory.Delete(klasor, true);
            var ust = Path.GetDirectoryName(klasor)!;
            if (!Directory.EnumerateFileSystemEntries(ust).Any()) Directory.Delete(ust);
        }
        catch (IOException)
        {
        }
    }

    private static PlayerView Kur(out Window window)
    {
        var view = new PlayerView { EngineFactory = () => new YolMotoru() };
        window = new Window { Width = 960, Height = 540, Content = view };
        window.Show();
        return view;
    }

    private static PlayerView Ac(string adres, out Window window)
    {
        var view = Kur(out window);
        Assert.True(view.OpenAddress(adres));
        DenetimSurucu.Pump(view, () => view.Navigation.IsCompleted, 10);
        DenetimSurucu.Wait(view, 0.2);
        return view;
    }

    private static void Kapat(PlayerView view, Window window)
    {
        view.Close();
        window.Close();
        DenetimSurucu.Wait(view, 0.1);
    }

    private static List<MenuItem> Satirlar(IEnumerable<object?> ogeler)
    {
        var liste = new List<MenuItem>();
        foreach (var item in ogeler.OfType<MenuItem>())
        {
            liste.Add(item);
            liste.AddRange(Satirlar(item.Items));
        }

        return liste;
    }

    [Fact]
    public void SemaKumesiMotorunGecirdigiKumeyleAyni()
    {
        Assert.Equal(MpvEngine.RemoteSchemes.OrderBy(s => s, StringComparer.Ordinal), MediaAddress.Schemes.OrderBy(s => s, StringComparer.Ordinal));
        foreach (var sema in MediaAddress.Schemes)
        {
            var adres = sema + "://sunucu/klip.mp4";
            Assert.True(MediaAddress.IsAddress(adres), sema);
            Assert.Equal(adres, MpvEngine.Target(adres));
        }
    }

    [Theory]
    [InlineData("ftp://sunucu/klip.mp4")]
    [InlineData("file:///C:/klip.mp4")]
    [InlineData(@"C:\videolar\klip.mp4")]
    [InlineData("klip.mp4")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AdresOlmayanAdresSayilmaz(string? deger) => Assert.False(MediaAddress.IsAddress(deger));

    [Theory]
    [InlineData("https://ornek.test/v/klip.mp4?token=abc&x=1", "https://ornek.test/v/klip.mp4")]
    [InlineData("https://ornek.test/v/klip.mp4#t=10", "https://ornek.test/v/klip.mp4")]
    [InlineData("https://ornek.test/v/klip.mp4?a=1#t=10", "https://ornek.test/v/klip.mp4")]
    [InlineData("rtsp://kamera:parola@10.0.0.5:554/akis?kanal=1", "rtsp://10.0.0.5:554/akis")]
    [InlineData("http://ad@ornek.test", "http://ornek.test")]
    [InlineData("  http://ornek.test/a@b/klip.mp4  ", "http://ornek.test/a@b/klip.mp4")]
    [InlineData("http://ornek.test/klip.mp4", "http://ornek.test/klip.mp4")]
    [InlineData(@"C:\videolar\klip?.mp4", @"C:\videolar\klip?.mp4")]
    public void SorguParcaVeKullaniciBilgisiKayittanDuser(string girdi, string beklenen)
        => Assert.Equal(beklenen, MediaAddress.WithoutQuery(girdi));

    [Theory]
    [InlineData("https://ornek.test/v/klip%20bir.mp4?token=abc", "klip bir.mp4")]
    [InlineData("https://ornek.test/v/canli/", "canli")]
    [InlineData("rtsp://kamera:parola@10.0.0.5:554", "10.0.0.5")]
    [InlineData("https://ornek.test/?id=7", "ornek.test")]
    public void AdYolunSonParcasiYoksaSunucu(string girdi, string beklenen)
    {
        Assert.Equal(beklenen, MediaAddress.Name(girdi));
        Assert.Equal(beklenen, PlayerView.MediaName(girdi));
    }

    [Fact]
    public void MetindenVeArgumandanIlkAdresAlinir()
    {
        Assert.Equal("https://ornek.test/a.mp4?x=1", MediaAddress.FromText("bak:\r\n  \"https://ornek.test/a.mp4?x=1\"  \r\nhttp://ornek.test/b.mp4"));
        Assert.Null(MediaAddress.FromText("merhaba\nftp://ornek.test/a.mp4"));
        Assert.Null(MediaAddress.FromText("bak https://ornek.test/a.mp4"));
        Assert.Null(MediaAddress.FromText(null));

        Assert.Equal("rtsp://kamera/akis", MediaAddress.FromArguments(new[] { "--sessiz", "\"rtsp://kamera/akis\"", "http://ornek.test/b.mp4" }));
        Assert.Null(MediaAddress.FromArguments(new[] { "--sessiz", "klip.mp4" }));
        Assert.Null(MediaAddress.FromArguments(null));
    }

    [Fact]
    public void AcilisHedefiVarOlanDosyaYoksaAdres()
    {
        var klasor = Gecici();
        try
        {
            var dosya = Path.Combine(klasor, "yerel.mp4");
            File.WriteAllBytes(dosya, Array.Empty<byte>());
            const string adres = "https://ornek.test/v/klip.mp4?token=abc";

            Assert.Equal(adres, Program.StartupTarget(new[] { adres }));
            Assert.Equal(adres, Program.StartupTarget(new[] { "--bilinmeyen", adres }));
            Assert.Equal(dosya, Program.StartupTarget(new[] { adres, dosya }));
            Assert.Null(Program.StartupTarget(new[] { "ftp://ornek.test/klip.mp4" }));
            Assert.Null(Program.StartupTarget(Array.Empty<string>()));
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void GecmisAnahtariSorgusuzYazilirAyniAdresTekKayit()
    {
        var klasor = Gecici();
        try
        {
            var dosya = Path.Combine(klasor, "history.json");
            const string taban = "https://ornek.test/v/klip.mp4";
            var gecmis = PlaybackHistory.Load(dosya);
            gecmis.Remember(taban + "?token=" + Gizli, 12, false, 100);
            Assert.True(gecmis.AddBookmark(taban + "?token=" + Gizli, 30));
            gecmis.Remember(taban + "?token=baska", 20, false, 100);
            gecmis.Save(dosya);

            var metin = File.ReadAllText(dosya);
            Assert.Equal(1, gecmis.Count);
            Assert.Contains(taban, metin, StringComparison.Ordinal);
            Assert.DoesNotContain(Gizli, metin, StringComparison.Ordinal);
            Assert.DoesNotContain("token", metin, StringComparison.Ordinal);
            Assert.Equal(new[] { 30.0 }, PlaybackHistory.Load(dosya).Bookmarks(taban + "?token=yeni"));
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void AracAyariVeGoruntuAdiSorguTasimaz()
    {
        var klasor = Gecici();
        try
        {
            var dosya = Path.Combine(klasor, ToolsOptions.FileName);
            var arac = new ToolsOptions();
            arac.UseUrl("  rtsp://kamera:" + Gizli + "@10.0.0.5/akis?anahtar=" + Gizli + " ");
            arac.Save(dosya);

            Assert.Equal("rtsp://10.0.0.5/akis", arac.LastUrl);
            Assert.DoesNotContain(Gizli, File.ReadAllText(dosya), StringComparison.Ordinal);
            Assert.Equal("rtsp://10.0.0.5/akis", ToolsOptions.Load(dosya).LastUrl);

            var ad = new PlayerSettings().FileStem("https://ornek.test/v/klip%20bir.mp4?token=" + Gizli, 5);
            Assert.StartsWith("klip bir", ad, StringComparison.Ordinal);
            Assert.DoesNotContain(Gizli, ad, StringComparison.Ordinal);
            Assert.DoesNotContain("token", ad, StringComparison.Ordinal);
            Assert.Empty(FolderNavigator.Siblings(Ornek));
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void CtrlVPanodakiAdresiAcarAdresYoksaSoyler()
    {
        const string adres = Ornek + "?token=abc";
        var rapor = AppHost.Run(() =>
        {
            var view = Kur(out var window);
            try
            {
                var okuma = 0;
                var pano = "bak:\r\n\"" + adres + "\"\r\n";
                view.AddressReader = _ =>
                {
                    okuma++;
                    return Task.FromResult<string?>(pano);
                };

                GirdiSurucu.Key(view, Key.V);
                var duzV = okuma;

                GirdiSurucu.Key(view, Key.V, KeyModifiers.Control);
                DenetimSurucu.Pump(view, () => view.LastPaste.IsCompleted, 5);
                DenetimSurucu.Pump(view, () => view.Navigation.IsCompleted, 10);
                var acti = view.LastPaste.Result;
                var yol = view.LoadedPath;
                var iz = view.Trace.Contains("paste -> clipboard");
                var sonAdres = view.Tools.LastUrl;

                pano = "merhaba";
                view.Apply(ToolsOptions.PasteUrl.ToCommand());
                DenetimSurucu.Pump(view, () => view.LastPaste.IsCompleted, 5);
                var bos = view.LastPaste.Result;
                var bildirim = view.ViewStateText;
                var bosYol = view.LoadedPath;

                view.AddressReader = _ => throw new InvalidOperationException("pano kilitli");
                view.Apply(ToolsOptions.PasteUrl.ToCommand());
                DenetimSurucu.Pump(view, () => view.LastPaste.IsCompleted, 5);
                var hata = view.LastPaste.IsCompletedSuccessfully && !view.LastPaste.Result;

                return (duzV, okuma, acti, yol, iz, sonAdres, bos, bildirim, bosYol, hata, beklenen: Strings.Get("player.tools.paste-none"));
            }
            finally
            {
                Kapat(view, window);
            }
        });

        Assert.Equal(0, rapor.duzV);
        Assert.Equal(2, rapor.okuma);
        Assert.True(rapor.acti, "panodaki adres acilmadi");
        Assert.Equal(adres, rapor.yol);
        Assert.True(rapor.iz, "pano izi yok");
        Assert.Equal(Ornek, rapor.sonAdres);
        Assert.False(rapor.bos, "adres olmayan pano acildi sayildi");
        Assert.Contains(rapor.beklenen, rapor.bildirim, StringComparison.Ordinal);
        Assert.Equal(adres, rapor.bosYol);
        Assert.True(rapor.hata, "okunamayan pano sessizce dusmedi");
    }

    [Fact]
    public void SuruklenenAdresMetniAcilirDosyaYoluMetniAcilmaz()
    {
        const string adres = Ornek + "?token=abc";
        var rapor = AppHost.Run(() =>
        {
            var view = Kur(out var window);
            try
            {
                DragEventArgs Olay(RoutedEventKim kim, string metin)
                {
                    var aktarim = new DataTransfer();
                    aktarim.Add(DataTransferItem.CreateText(metin));
                    var args = new DragEventArgs(kim == RoutedEventKim.Uzerinde ? DragDrop.DragOverEvent : DragDrop.DropEvent, aktarim, view, new Point(1, 1), KeyModifiers.None);
                    view.RaiseEvent(args);
                    return args;
                }

                var yolEtki = Olay(RoutedEventKim.Uzerinde, @"C:\videolar\klip.mp4").DragEffects;
                Olay(RoutedEventKim.Birak, @"C:\videolar\klip.mp4");
                var yolSonrasi = view.LoadedPath;

                var adresEtki = Olay(RoutedEventKim.Uzerinde, adres + "\r\n").DragEffects;
                Olay(RoutedEventKim.Birak, adres + "\r\n");
                DenetimSurucu.Pump(view, () => view.Navigation.IsCompleted, 10);
                var yol = view.LoadedPath;
                var iz = view.Trace.Count(satir => satir == "drop -> address");

                return (yolEtki, yolSonrasi, adresEtki, yol, iz, bos: view.OpenDroppedAddress(null), yerel: view.OpenDroppedAddress(@"C:\videolar\klip.mp4"));
            }
            finally
            {
                Kapat(view, window);
            }
        });

        Assert.Equal(DragDropEffects.None, rapor.yolEtki);
        Assert.Null(rapor.yolSonrasi);
        Assert.Equal(DragDropEffects.Copy, rapor.adresEtki);
        Assert.Equal(adres, rapor.yol);
        Assert.Equal(1, rapor.iz);
        Assert.False(rapor.bos);
        Assert.False(rapor.yerel);
    }

    private enum RoutedEventKim
    {
        Uzerinde,
        Birak
    }

    [Fact]
    public void AdresteBaslikVeListeAdiSorgusuzIpucuAnahtarTasimaz()
    {
        const string adres = Ornek + "?token=" + Gizli;
        var rapor = AppHost.Run(() =>
        {
            var view = Ac(adres, out var window);
            try
            {
                var kopya = new List<string>();
                view.PathCopier = (_, metin) =>
                {
                    kopya.Add(metin);
                    return Task.CompletedTask;
                };
                var baslik = view.FindControl<Border>("MediaTitle")!;
                var menu = view.BuildTitleMenu(adres).Items.OfType<MenuItem>().ToList();
                foreach (var satir in menu.Where(satir => satir.Tag is "copy-name" or "copy-path"))
                    satir.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                return (ad: view.MediaTitleText, ipucu: ToolTip.GetTip(baslik) as string, kopya, yol: view.LoadedPath);
            }
            finally
            {
                Kapat(view, window);
            }
        });

        Assert.Equal("klip bir.mp4", rapor.ad);
        Assert.Equal(Ornek, rapor.ipucu);
        Assert.Equal(adres, rapor.yol);
        Assert.Equal(new[] { "klip bir.mp4", adres }, rapor.kopya);
    }

    [Fact]
    public void YolaBagliOzelliklerAdresteDevreDisiKalirVeNedeniniSoyler()
    {
        const string adres = Ornek + "?token=abc";
        var rapor = AppHost.Run(() =>
        {
            var view = Ac(adres, out var window);
            try
            {
                var neden = Strings.Get(PlayerView.AddressUnsupportedKey);
                var duzenle = 0;
                view.EditRequested = (_, _) =>
                {
                    duzenle++;
                    return Task.CompletedTask;
                };

                var satirlar = Satirlar(view.BuildMenu().Items.Where(oge => oge is not MenuItem ust || !ReferenceEquals(ust.Tag, Keymap.Settings))).Concat(Satirlar(view.BuildTitleMenu(adres).Items)).ToList();
                var konum = Strings.Get("player.menu.reveal");
                var yolaBagli = satirlar.Where(satir => ReferenceEquals(satir.Tag, ToolsOptions.Clip) || ReferenceEquals(satir.Tag, ToolsOptions.Gif)
                    || ReferenceEquals(satir.Tag, Keymap.Edit) || (string?)satir.Header == konum).ToList();
                var kusurlu = yolaBagli
                    .Where(satir => satir.IsEnabled || (ToolTip.GetTip(satir) as string) != neden || !ToolTip.GetShowOnDisabled(satir))
                    .Select(satir => satir.Header?.ToString() ?? "?")
                    .ToList();
                var adresSatirlari = satirlar.Where(satir => ReferenceEquals(satir.Tag, ToolsOptions.OpenUrl) || ReferenceEquals(satir.Tag, ToolsOptions.PasteUrl)).ToList();
                var seritDugmesi = view.FindControl<Button>("BtnSeritEdit")!.IsEnabled;

                view.Apply(ToolsOptions.Clip.ToCommand());
                var klipIz = view.Trace[^1];
                var klipBildirim = view.ViewStateText;

                view.Apply(Keymap.Edit.ToCommand());
                var duzenleIz = view.Trace[^1];
                var duzenleBildirim = view.TrackNotice;

                view.Apply(Keymap.Info.ToCommand());
                var indirme = view.DownloadSubtitleAsync();
                DenetimSurucu.Pump(view, () => indirme.IsCompleted, 5);
                var altyaziIz = view.Trace[^1];
                var altyaziBildirim = view.TrackNotice;

                var onizleme = 0;
                view.PreviewFactory = () =>
                {
                    onizleme++;
                    return new YolMotoru();
                };
                var kare = view.ShowThumbnailAsync(5);
                DenetimSurucu.Pump(view, () => kare.IsCompleted, 5);
                var kareMs = kare.Result;
                var saatGorunur = view.ThumbnailVisible;

                view.Apply(Keymap.NextFile.ToCommand());
                var adimIz = view.Trace[^1];
                var adimBildirim = view.ViewStateText;

                return (neden, yolaBagliSayisi: yolaBagli.Count, kusurlu, adresAcik: adresSatirlari.Count(satir => satir.IsEnabled), seritDugmesi,
                    klipIz, klipBildirim, duzenle, duzenleIz, duzenleBildirim, altyaziIz, altyaziBildirim, onizleme, kareMs, saatGorunur,
                    adimIz, adimBildirim, son: LanguageCatalog.Display(Strings.Get("player.list.end")), yol: view.LoadedPath);
            }
            finally
            {
                Kapat(view, window);
            }
        });

        Assert.True(rapor.yolaBagliSayisi >= 5, $"yola bagli satir {rapor.yolaBagliSayisi}");
        Assert.Empty(rapor.kusurlu);
        Assert.Equal(2, rapor.adresAcik);
        Assert.False(rapor.seritDugmesi, "serit Duzenle dugmesi adreste acik");
        Assert.Equal("clip -> no", rapor.klipIz);
        Assert.Contains(rapor.neden, rapor.klipBildirim, StringComparison.Ordinal);
        Assert.Equal(0, rapor.duzenle);
        Assert.Equal("edit -> address", rapor.duzenleIz);
        Assert.Equal(PlayerView.AddressUnsupportedKey, rapor.duzenleBildirim);
        Assert.Equal("subdl -> address", rapor.altyaziIz);
        Assert.Equal(PlayerView.AddressUnsupportedKey, rapor.altyaziBildirim);
        Assert.Equal(0, rapor.onizleme);
        Assert.Equal(0, rapor.kareMs);
        Assert.True(rapor.saatGorunur, "adreste saat cipi gorunmedi");
        Assert.Equal("file 1 -> no", rapor.adimIz);
        Assert.Contains(rapor.son, rapor.adimBildirim, StringComparison.Ordinal);
        Assert.Equal(adres, rapor.yol);
    }

    [Fact]
    public void SorguluAdresMotoraOlduguGibiGiderDiskeSorgusuzYazilir()
    {
        var clip = MotorKlipleri.Kucuk;
        var klasor = Gecici();
        try
        {
            var history = Path.Combine(klasor, "history.json");
            using var sunucu = new MiniSunucu(clip);
            var temiz = sunucu.Adres;
            var adres = temiz + "?token=" + Gizli;

            var rapor = AppHost.Run(() =>
            {
                var view = new PlayerView
                {
                    HistoryPath = () => history,
                    EngineFactory = () =>
                    {
                        var engine = new MpvEngine();
                        engine.SetProperty("ao", "null");
                        return engine;
                    }
                };
                var window = new Window { Width = 640, Height = 480, Content = view };

                var kabul = view.OpenAddress(adres);
                DenetimSurucu.Pump(view, () => view.Navigation.IsCompleted, 30);
                DenetimSurucu.Pump(view, () => view.Engine is { IsOpen: true }, 10);
                var acildi = view.Engine is { IsOpen: true };
                var ytdl = (view.Engine as MpvEngine)?.GetProperty("ytdl");
                var motorYolu = (view.Engine as MpvEngine)?.GetProperty("path");
                var sonAdres = view.Tools.LastUrl;
                var ipucu = ToolTip.GetTip(view.FindControl<Border>("MediaTitle")!) as string;
                var isaret = view.Engine is { } motor && motor.DurationSeconds > 2;
                view.Apply(Keymap.BookmarkAdd.ToCommand());
                var sonAcilanlar = view.Recent.Items.Count;

                view.Close();
                window.Close();
                return (kabul, acildi, ytdl, motorYolu, sonAdres, ipucu, isaret, sonAcilanlar);
            });

            var diskteki = new StringBuilder();
            foreach (var dosya in Directory.EnumerateFiles(klasor, "*", SearchOption.AllDirectories))
                diskteki.AppendLine(Path.GetFileName(dosya)).AppendLine(File.ReadAllText(dosya));
            var disk = diskteki.ToString();

            Assert.True(rapor.kabul, "adres kabul edilmedi");
            Assert.True(rapor.acildi, "sorgulu adres acilmadi");
            Assert.Contains(sunucu.Satirlar, satir => satir.Contains("/klip.mp4?token=" + Gizli, StringComparison.Ordinal));
            Assert.Equal(adres, rapor.motorYolu);
            Assert.Equal("no", rapor.ytdl);
            Assert.Equal(temiz, rapor.sonAdres);
            Assert.Equal(temiz, rapor.ipucu);
            Assert.Equal(0, rapor.sonAcilanlar);
            Assert.Contains(temiz, disk, StringComparison.Ordinal);
            Assert.DoesNotContain(Gizli, disk, StringComparison.Ordinal);
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void BulunmayanAdresHataSatiriniGosterirOynaticiAcikKalir()
    {
        var clip = MotorKlipleri.Kucuk;
        using var sunucu = new MiniSunucu(clip, bulunamadi: true);
        var adres = sunucu.Adres;

        var rapor = AppHost.Run(() =>
        {
            var view = new PlayerView
            {
                EngineFactory = () =>
                {
                    var engine = new MpvEngine();
                    engine.SetProperty("ao", "null");
                    return engine;
                }
            };
            var window = new Window { Width = 640, Height = 480, Content = view };

            var saat = System.Diagnostics.Stopwatch.StartNew();
            var kabul = view.OpenAddress(adres);
            DenetimSurucu.Pump(view, () => view.Navigation.IsCompleted, 30);
            var sure = saat.Elapsed.TotalSeconds;
            var bitti = view.Navigation.IsCompleted;
            var gorunur = view.TxtStall.IsVisible;
            var metin = view.TxtStall.Text ?? "";
            var acik = view.Engine is { IsOpen: true };
            var beklenen = LanguageCatalog.Display(Strings.Get(MpvEngine.FailedKey));

            view.Close();
            window.Close();
            return (kabul, bitti, sure, gorunur, metin, acik, beklenen);
        });

        Assert.True(rapor.kabul, "adres kabul edilmedi");
        Assert.True(rapor.bitti, $"acma {rapor.sure:0.0} sn icinde donmedi");
        Assert.True(sunucu.Istekler > 0, "yerel sunucuya istek gelmedi");
        Assert.False(rapor.acik, "404 donen adres acik sayildi");
        Assert.True(rapor.gorunur, "hata satiri gorunmedi");
        Assert.StartsWith(rapor.beklenen, rapor.metin, StringComparison.Ordinal);
        Assert.True(rapor.sure < 15, $"404 {rapor.sure:0.0} sn surdu");
    }
}
