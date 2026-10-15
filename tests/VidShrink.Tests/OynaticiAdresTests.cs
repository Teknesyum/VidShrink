using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Ffmpeg;
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

    [Theory]
    [InlineData("http://127.0.0.1:9/canli/yayin.m3u8")]
    [InlineData("https://ornek.test/radyo/dinle.pls")]
    [InlineData("http://127.0.0.1:9/liste.M3U")]
    [InlineData("rtsp://127.0.0.1:9/akis.asx")]
    public void ListeUzantiliAdresKuyrukKurmadanMotoraGider(string adres)
    {
        var rapor = AppHost.Run(() =>
        {
            var view = Ac(adres, out var window);
            try
            {
                return (yol: view.LoadedPath, kuyruk: view.Queue, kaynak: view.QueueSource, hata: view.TxtStall.IsVisible, metin: view.TxtStall.Text);
            }
            finally
            {
                Kapat(view, window);
            }
        });

        Assert.False(rapor.hata, "adres liste dosyasi gibi okundu: " + rapor.metin);
        Assert.Equal(adres, rapor.yol);
        Assert.Null(rapor.kuyruk);
        Assert.Null(rapor.kaynak);
    }

    [Fact]
    public void ListeIcindekiListeUzantiliAdresAtilmazYerelAltListeAtilir()
    {
        var klasor = Gecici();
        try
        {
            const string hls = "http://127.0.0.1:9/canli/yayin.m3u8";
            const string radyo = "https://ornek.test/radyo/dinle.pls?kanal=2";
            var klip = Path.Combine(klasor, "klip.mp4");
            File.WriteAllBytes(klip, Array.Empty<byte>());
            File.WriteAllText(Path.Combine(klasor, "alt.m3u"), "klip.mp4\n");
            var liste = Path.Combine(klasor, "liste.m3u8");
            File.WriteAllText(liste, "#EXTM3U\n" + hls + "\nalt.m3u\nklip.mp4\n" + radyo + "\nfile:///" + klasor.Replace('\\', '/') + "/alt.m3u\n");

            Assert.Equal(new[] { hls, klip, radyo }, PlaylistFile.ReadPlayable(liste));
            Assert.Equal(new[] { hls }, PlaylistFile.Parse("[playlist]\nFile1=" + hls + "\nFile2=yerel.pls\n", "pls", klasor));

            var rapor = AppHost.Run(() =>
            {
                var view = Kur(out var window);
                try
                {
                    var acilis = view.OpenAsync(liste);
                    DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
                    return (yol: view.LoadedPath, kuyruk: view.Queue?.ToList(), kaynak: view.QueueSource);
                }
                finally
                {
                    Kapat(view, window);
                }
            });

            Assert.Equal(hls, rapor.yol);
            Assert.Equal(new[] { hls, klip, radyo }, rapor.kuyruk);
            Assert.Equal(Path.GetFullPath(liste), rapor.kaynak);
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void YerelSunucudakiHlsAdresiAcilirParcalarIstenir()
    {
        var klasor = Gecici();
        try
        {
            HlsUret(MotorKlipleri.Kucuk, klasor);
            using var sunucu = new KlasorSunucu(klasor);
            var adres = sunucu.Adres("yayin.m3u8");

            var rapor = AppHost.Run(() =>
            {
                var view = new PlayerView { EngineFactory = GercekMotor };
                var window = new Window { Width = 640, Height = 480, Content = view };
                try
                {
                    var kabul = view.OpenAddress(adres);
                    DenetimSurucu.Pump(view, () => view.Navigation.IsCompleted, 30);
                    DenetimSurucu.Pump(view, () => view.Engine is { IsOpen: true }, 10);
                    var motor = view.Engine as MpvEngine;
                    return (kabul, acik: view.Engine is { IsOpen: true }, motorYolu: motor?.GetProperty("path"), sure: view.Engine?.DurationSeconds ?? 0,
                        yol: view.LoadedPath, kuyruk: view.Queue, hata: view.TxtStall.IsVisible, metin: view.TxtStall.Text, ad: view.MediaTitleText);
                }
                finally
                {
                    view.Close();
                    window.Close();
                }
            });

            Assert.True(rapor.kabul, "adres kabul edilmedi");
            Assert.False(rapor.hata, "hata satiri: " + rapor.metin);
            Assert.True(rapor.acik, "HLS adresi acilmadi");
            Assert.Equal(adres, rapor.motorYolu);
            Assert.Equal(adres, rapor.yol);
            Assert.Null(rapor.kuyruk);
            Assert.Equal("yayin.m3u8", rapor.ad);
            Assert.True(rapor.sure > 4, $"HLS suresi {rapor.sure}");
            Assert.Contains(sunucu.Satirlar, satir => satir.StartsWith("GET /yayin.m3u8 ", StringComparison.Ordinal));
            Assert.Contains(sunucu.Satirlar, satir => satir.Contains(".ts ", StringComparison.Ordinal));
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Theory]
    [InlineData("[playlist]\nFile1=http://a/b\nTitle1= Radyo Bir \n", "Radyo Bir")]
    [InlineData("[playlist]\nfile1=http://a/b\ntitle1=kucuk\n", "kucuk")]
    [InlineData("[playlist]\nFile2=http://a/c\nTitle2=Iki\nFile1=http://a/b\nTitle1=Bir\n", "Bir")]
    [InlineData("[playlist]\nFile3=http://a/c\nTitle3=Uc\nTitle1=Sahipsiz\n", "Uc")]
    [InlineData("[playlist]\nFile1=http://a/b\nTitle2=Baska\n", null)]
    [InlineData("[playlist]\nFile1=http://a/b\nTitle1=\n", null)]
    [InlineData("[playlist]\nFile1=http://a/b\n", null)]
    public void PlsBasligiIlkGirdininBasligidir(string metin, string? beklenen)
    {
        Assert.Equal(beklenen, PlaylistFile.PlsTitle(metin));
        Assert.Equal(beklenen, PlaylistFile.PlsTitle(System.Text.Encoding.UTF8.GetBytes(metin)));
    }

    [Theory]
    [InlineData("https://ornek.test/radyo.pls", "Yayin", "Yayin")]
    [InlineData("https://ornek.test/radyo.pls?k=1", "Yayin", "Yayin")]
    [InlineData("https://ornek.test/radyo.pls", null, null)]
    [InlineData("https://ornek.test/radyo.pls", "", null)]
    [InlineData("https://ornek.test/klip.mp4", "Yayin", null)]
    [InlineData("C:\\yerel\\liste.pls", "Yayin", null)]
    public void MotorunYayinAdiYalnizUzakListedeAdOlur(string yol, string? motorAdi, string? beklenen)
    {
        var motor = AdliMotor.Kur(motorAdi);
        Assert.Equal(beklenen, PlayerView.StreamTitle(yol, motor));
        Assert.Null(PlayerView.StreamTitle(yol, null));
    }

    public class AdliMotor : System.Reflection.DispatchProxy
    {
        private string? _ad;

        internal static IPlaybackEngine Kur(string? ad)
        {
            var motor = Create<IPlaybackEngine, AdliMotor>();
            ((AdliMotor)(object)motor)._ad = ad;
            return motor;
        }

        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args)
            => method?.Name == "get_MediaTitle" ? _ad : throw new NotSupportedException(method?.Name);
    }

    [Theory]
    [InlineData("Title1=Radyo\n", "Radyo")]
    [InlineData("", "radyo.pls")]
    public void UzakPlsAdresiMotorunListesineDuserIlkGirdiCalar(string baslikSatiri, string beklenenAd)
    {
        var klasor = Gecici();
        try
        {
            File.Copy(MotorKlipleri.Kucuk, Path.Combine(klasor, "klip.mp4"));
            using var sunucu = new KlasorSunucu(klasor);
            var girdi = sunucu.Adres("klip.mp4");
            File.WriteAllText(Path.Combine(klasor, "radyo.pls"), "[playlist]\nNumberOfEntries=1\nFile1=" + girdi + "\n" + baslikSatiri + "Length1=-1\nVersion=2\n");
            var adres = sunucu.Adres("radyo.pls");

            var rapor = AppHost.Run(() =>
            {
                var okundu = false;
                var view = new PlayerView { EngineFactory = GercekMotor };
                view.ListTitleReader = async (yol, iptal) =>
                {
                    try { return await PlayerView.ReadListTitleAsync(yol, iptal); }
                    finally { okundu = true; }
                };
                var window = new Window { Width = 640, Height = 480, Content = view };
                try
                {
                    var kabul = view.OpenAddress(adres);
                    DenetimSurucu.Pump(view, () => view.Navigation.IsCompleted, 30);
                    DenetimSurucu.Pump(view, () => view.Engine is { IsOpen: true }, 10);
                    DenetimSurucu.Pump(view, () => okundu && view.MediaTitleText == beklenenAd, 10);
                    var motor = view.Engine as MpvEngine;
                    return (kabul, acik: view.Engine is { IsOpen: true }, motorYolu: motor?.GetProperty("path"), listeSayisi: motor?.GetProperty("playlist-count"),
                        yol: view.LoadedPath, kuyruk: view.Queue, hata: view.TxtStall.IsVisible, metin: view.TxtStall.Text, ad: view.MediaTitleText,
                        ipucu: ToolTip.GetTip(view.FindControl<Border>("MediaTitle")!) as string, okundu);
                }
                finally
                {
                    view.Close();
                    window.Close();
                }
            });

            Assert.True(rapor.kabul, "adres kabul edilmedi");
            Assert.False(rapor.hata, "hata satiri: " + rapor.metin);
            Assert.True(rapor.acik, "pls adresi acilmadi");
            Assert.Equal(girdi, rapor.motorYolu);
            Assert.Equal("1", rapor.listeSayisi);
            Assert.Equal(adres, rapor.yol);
            Assert.Null(rapor.kuyruk);
            Assert.True(rapor.okundu, "liste basligi okunmadi");
            Assert.Equal(beklenenAd, rapor.ad);
            Assert.Equal(adres, rapor.ipucu);
            Assert.Contains(sunucu.Satirlar, satir => satir.StartsWith("GET /radyo.pls ", StringComparison.Ordinal));
            Assert.Contains(sunucu.Satirlar, satir => satir.StartsWith("GET /klip.mp4 ", StringComparison.Ordinal));
        }
        finally
        {
            Sil(klasor);
        }
    }

    private static IPlaybackEngine GercekMotor()
    {
        var engine = new MpvEngine();
        engine.SetProperty("ao", "null");
        return engine;
    }

    private static void HlsUret(string kaynak, string klasor)
    {
        Assert.True(ToolLocator.IsAvailable(out var eksik), $"HLS uretimi icin {eksik} gerekli");
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { "-y", "-hide_banner", "-threads", "2", "-i", kaynak, "-t", "6", "-c", "copy", "-f", "hls", "-hls_time", "2", "-hls_list_size", "0",
                     "-hls_segment_filename", Path.Combine(klasor, "parca%d.ts"), Path.Combine(klasor, "yayin.m3u8") })
            psi.ArgumentList.Add(arg);

        using var surec = Process.Start(psi)!;
        var cikti = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        Assert.True(surec.WaitForExit(60_000), "HLS uretimi 60 sn icinde bitmedi");
        cikti.GetAwaiter().GetResult();
        var gunluk = hata.GetAwaiter().GetResult();
        Assert.True(surec.ExitCode == 0, $"HLS uretilemedi (kod {surec.ExitCode}): {gunluk[Math.Max(0, gunluk.Length - 600)..]}");
    }
}

/// <summary>
/// Yalnız 127.0.0.1'de dinleyen, verilen klasördeki dosyaları adıyla sunan sınama sunucusu:
/// HLS listesi ve parçaları, uzak pls gibi birden çok dosyalı adresler için. Aralık isteğine
/// 206 döner, olmayan dosyaya 404.
/// </summary>
internal sealed class KlasorSunucu : IDisposable
{
    private readonly TcpListener _yuva;
    private readonly string _klasor;
    private readonly CancellationTokenSource _iptal = new();

    internal KlasorSunucu(string klasor)
    {
        _klasor = klasor;
        _yuva = new TcpListener(IPAddress.Loopback, 0);
        _yuva.Start();
        _ = Task.Run(DinleAsync);
    }

    internal System.Collections.Concurrent.ConcurrentQueue<string> Satirlar { get; } = new();

    internal string Adres(string ad) => FormattableString.Invariant(
        $"http://127.0.0.1:{((IPEndPoint)_yuva.LocalEndpoint).Port}/{ad}");

    private async Task DinleAsync()
    {
        while (!_iptal.IsCancellationRequested)
        {
            TcpClient istemci;
            try
            {
                istemci = await _yuva.AcceptTcpClientAsync(_iptal.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = Task.Run(() => YanitlaAsync(istemci));
        }
    }

    private async Task YanitlaAsync(TcpClient istemci)
    {
        using (istemci)
        {
            try
            {
                using var akis = istemci.GetStream();
                var tampon = new byte[8192];
                var okunan = await akis.ReadAsync(tampon, _iptal.Token);
                if (okunan <= 0) return;
                var istek = Encoding.ASCII.GetString(tampon, 0, okunan);
                var satir = istek.Split('\r')[0];
                Satirlar.Enqueue(satir);

                var parcalar = satir.Split(' ');
                var ad = parcalar.Length > 1 ? Uri.UnescapeDataString(parcalar[1].TrimStart('/').Split('?')[0]) : "";
                var dosya = Path.Combine(_klasor, ad);
                if (ad.Length == 0 || ad.Contains("..", StringComparison.Ordinal) || !File.Exists(dosya))
                {
                    await akis.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), _iptal.Token);
                    return;
                }

                var govde = await File.ReadAllBytesAsync(dosya, _iptal.Token);
                var (bas, son) = Aralik(istek, govde.Length);
                var uzunluk = son - bas + 1;
                var tur = Path.GetExtension(dosya).ToLowerInvariant() switch
                {
                    ".m3u8" => "application/vnd.apple.mpegurl",
                    ".ts" => "video/mp2t",
                    ".pls" => "audio/x-scpls",
                    _ => "video/mp4"
                };
                var basliklar = new StringBuilder();
                basliklar.Append(bas == 0 && uzunluk == govde.Length ? "HTTP/1.1 200 OK\r\n" : "HTTP/1.1 206 Partial Content\r\n");
                basliklar.Append(CultureInfo.InvariantCulture, $"Content-Type: {tur}\r\n");
                basliklar.Append(CultureInfo.InvariantCulture, $"Content-Length: {uzunluk}\r\n");
                basliklar.Append("Accept-Ranges: bytes\r\n");
                if (bas != 0 || uzunluk != govde.Length)
                    basliklar.Append(CultureInfo.InvariantCulture, $"Content-Range: bytes {bas}-{son}/{govde.Length}\r\n");
                basliklar.Append("Connection: close\r\n\r\n");

                await akis.WriteAsync(Encoding.ASCII.GetBytes(basliklar.ToString()), _iptal.Token);
                if (!istek.StartsWith("HEAD", StringComparison.Ordinal))
                    await akis.WriteAsync(govde.AsMemory(bas, uzunluk), _iptal.Token);
                await akis.FlushAsync(_iptal.Token);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
            {
            }
        }
    }

    private static (int Bas, int Son) Aralik(string istek, int uzunluk)
    {
        foreach (var satir in istek.Split("\r\n"))
        {
            if (!satir.StartsWith("Range: bytes=", StringComparison.OrdinalIgnoreCase)) continue;
            var deger = satir["Range: bytes=".Length..].Split('-');
            if (int.TryParse(deger[0], NumberStyles.None, CultureInfo.InvariantCulture, out var bas) && bas < uzunluk)
            {
                var son = deger.Length > 1 && int.TryParse(deger[1], NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? Math.Min(s, uzunluk - 1) : uzunluk - 1;
                return (bas, son);
            }
        }

        return (0, uzunluk - 1);
    }

    public void Dispose()
    {
        _iptal.Cancel();
        _yuva.Stop();
        _iptal.Dispose();
    }
}
