using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kuyruk penceresinin izleme klasörü: klasöre düşen video sıraya girer. Kararlılık çekirdeğin
/// (<see cref="WatchFolder.Poll"/>) işi; burada ölçülen, <see cref="QueueWatch"/>'ın taramaya neyi
/// soktuğu ve pencerenin bunu sıraya, ayara ve durum satırına nasıl taşıdığı. Tarama elle yürütülür
/// (oturma süresi sıfır, ardışık adım); zamana bağlı tek kol koşul bekler, sabit süre beklemez.
/// </summary>
public sealed class KuyrukIzlemeTests : IDisposable
{
    private const int HazirAdim = WatchFolder.StableConfirmations + 1;

    private readonly string _kok = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        ".calisma", "test-ciktilari", "kuyruk-izleme", Guid.NewGuid().ToString("N")[..8]));
    private readonly string _izlenen;
    private readonly string _ayar;

    public KuyrukIzlemeTests()
    {
        _izlenen = Path.Combine(_kok, "izlenen");
        _ayar = Path.Combine(_kok, "ayar", "settings.json");
        Directory.CreateDirectory(_izlenen);
        Directory.CreateDirectory(Path.GetDirectoryName(_ayar)!);
    }

    public void Dispose()
    {
        try { Directory.Delete(_kok, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class SahteEylem : IQueueEndActions
    {
        public void Reveal(string path) { }
        public void Sleep() { }
        public void PowerOff() { }
    }

    private string Dusur(string ad, string icerik = "video")
    {
        var yol = Path.Combine(_izlenen, ad);
        File.WriteAllText(yol, icerik);
        return yol;
    }

    private QueueWatch Izleyici() => new(_izlenen, TimeSpan.FromMilliseconds(20), TimeSpan.Zero);

    private static List<string> Adimla(QueueWatch izleyici, int adim)
    {
        var bulunan = new List<string>();
        for (var i = 0; i < adim; i++) Assert.True(izleyici.Step(bulunan.Add));
        return bulunan;
    }

    private static void KlasoruSil(string klasor)
    {
        var saat = Stopwatch.StartNew();
        while (Directory.Exists(klasor))
        {
            try { Directory.Delete(klasor, true); }
            catch (IOException) when (saat.Elapsed < TimeSpan.FromSeconds(10)) { Thread.Sleep(20); }
            catch (UnauthorizedAccessException) when (saat.Elapsed < TimeSpan.FromSeconds(10)) { Thread.Sleep(20); }
        }
    }

    private static bool Bekle(Func<bool> kosul, Action? pompa = null, int saniye = 30)
    {
        var saat = Stopwatch.StartNew();
        while (saat.Elapsed < TimeSpan.FromSeconds(saniye))
        {
            pompa?.Invoke();
            if (kosul()) return true;
            Thread.Sleep(20);
        }
        return false;
    }

    private T Pencerede<T>(Func<ShrinkJobWindow, T> govde, bool arkaPlan = false, string? secilecek = null,
        PlanOptions? sablon = null)
        => AppHost.Run(() =>
        {
            var eskiAyar = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
            var kultur = CultureInfo.CurrentUICulture;
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", _ayar);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            ShrinkJobWindow? window = null;
            try
            {
                var elle = sablon is null
                    ? new[] { Path.Combine(_kok, "elle-a.mp4"), Path.Combine(_kok, "elle-b.mp4") }
                    : Array.Empty<string>();
                window = new ShrinkJobWindow(elle, sablon ?? new PlanOptions { TargetMb = 25 }, false, null)
                {
                    Actions = new SahteEylem(),
                    PencereEtkin = () => true,
                    WatchInBackground = arkaPlan,
                    WatchPollInterval = TimeSpan.FromMilliseconds(20),
                    WatchStableFor = TimeSpan.Zero,
                    PickWatchFolder = () => Task.FromResult(secilecek)
                };
                window.SetPaused(sablon is null);
                window.Begin();
                return govde(window);
            }
            finally
            {
                window?.Close();
                Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", eskiAyar);
                CultureInfo.CurrentUICulture = kultur;
                Strings.Use("en");
            }
        });

    private static string[] Bekleyen(ShrinkJobWindow window) => window.Pending.Select(r => Path.GetFileName(r.Path)).ToArray();

    private static void Adimla(ShrinkJobWindow window, int adim)
    {
        for (var i = 0; i < adim; i++) window.WatchStep();
    }

    /// <summary>Düşen video hazır sayılınca verilir; video olmayan ve gizli dosya hiç verilmez.</summary>
    [Fact]
    public void DusenVideoVerilirVideoOlmayanVerilmez()
    {
        using var izleyici = Izleyici();
        var video = Dusur("klip.mp4");
        Dusur("notlar.txt");
        Dusur("kapak.jpg");
        Dusur(".gizli.mp4");

        var erken = Adimla(izleyici, HazirAdim - 1);
        var sonra = Adimla(izleyici, 1);

        Assert.Empty(erken);
        Assert.Equal(new[] { video }, sonra);
    }

    /// <summary>Yazımı süren dosya (boyu taramalar arasında değişen) çekirdeğin kuralıyla bekler; durunca verilir.</summary>
    [Fact]
    public void BuyuyenDosyaDurmadanVerilmez()
    {
        using var izleyici = Izleyici();
        var video = Dusur("buyuyen.mkv", "a");
        var bulunan = new List<string>();
        for (var i = 0; i < HazirAdim * 2; i++)
        {
            File.AppendAllText(video, "daha");
            Assert.True(izleyici.Step(bulunan.Add));
        }
        var buyurken = bulunan.ToArray();
        bulunan.AddRange(Adimla(izleyici, HazirAdim));

        Assert.Empty(buyurken);
        Assert.Equal(new[] { video }, bulunan);
    }

    /// <summary>İzleme başlarken klasörde duran dosya sıraya girmez; sonradan düşen girer.</summary>
    [Fact]
    public void BaslarkenDuranDosyaVerilmez()
    {
        Dusur("eski.mp4");
        using var izleyici = Izleyici();
        var yeni = Dusur("yeni.mp4");

        Assert.Equal(new[] { yeni }, Adimla(izleyici, HazirAdim * 2));
    }

    /// <summary>Verilen dosya sonraki taramalarda yeniden verilmez; içeriği değişirse yeni dosya sayılır.</summary>
    [Fact]
    public void AyniDosyaIkiKezVerilmez()
    {
        using var izleyici = Izleyici();
        var video = Dusur("tek.mp4");

        var ilk = Adimla(izleyici, HazirAdim);
        var tekrar = Adimla(izleyici, HazirAdim * 3);
        File.WriteAllText(video, "bambaska ve daha uzun icerik");
        var degisince = Adimla(izleyici, HazirAdim);

        Assert.Equal(new[] { video }, ilk);
        Assert.Empty(tekrar);
        Assert.Equal(new[] { video }, degisince);
    }

    /// <summary>
    /// Döngü: çıktı izlenen klasöre düşer. Bildirilen çıktı ve kodlayıcının ara dosyası aday olmaz;
    /// aynı anda düşen bildirilmemiş video olur (olumlu kontrol).
    /// </summary>
    [Fact]
    public void KendiCiktisiVeAraDosyasiVerilmez()
    {
        using var izleyici = Izleyici();
        var kaynak = Dusur("kaynak.mp4");
        var cikti = ShrinkJobWindow.UniqueOutputPath(kaynak, new AppSettings(), null, 25);
        QueueWatch.MarkOwn(cikti);

        Assert.Equal(_izlenen, Path.GetDirectoryName(cikti));
        File.WriteAllText(cikti, "kucultulmus");
        Dusur(QueueWatch.PartialPrefix + Guid.NewGuid().ToString("N") + ".mp4");

        Assert.Equal(new[] { kaynak }, Adimla(izleyici, HazirAdim * 2));
        Assert.True(QueueWatch.IsOwn(cikti));
        Assert.False(QueueWatch.IsOwn(kaynak));
    }

    /// <summary>Kuyruk çıktıyı kodlama başlamadan bildirir: ara dosya adını değiştirip çıktı olduğu an tarama onu tanımalı.</summary>
    [Fact]
    public void KuyrukCiktiyiKodlamadanOnceBildirir()
    {
        var kaynak = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "ShrinkJobWindow.axaml.cs"));
        var bildirim = kaynak.IndexOf("QueueWatch.MarkOwn(output);", StringComparison.Ordinal);
        var kodlama = kaynak.IndexOf("new EncodeRunner().RunAsync(", StringComparison.Ordinal);

        Assert.True(bildirim > 0, "kuyruk çıktısını bildirmiyor");
        Assert.True(kodlama > bildirim, "bildirim kodlamadan sonra");
        Assert.Equal(QueueWatch.PartialPrefix, Path.GetFileName(EncodeRunner.PartialPathFor(Path.Combine(_izlenen, "x.mp4")))[..QueueWatch.PartialPrefix.Length]);
    }

    /// <summary>Klasör silinince adım <c>false</c> döner ve hiçbir şey vermez.</summary>
    [Fact]
    public void KlasorSilinceAdimDurur()
    {
        using var izleyici = Izleyici();
        Dusur("klip.mp4");
        Adimla(izleyici, 1);
        KlasoruSil(_izlenen);

        var bulunan = new List<string>();
        Assert.False(izleyici.Step(bulunan.Add));
        Assert.Empty(bulunan);
    }

    /// <summary>Arka plan döngüsü: düşen dosya bulunur, klasör silinince kayıp bir kez duyurulur ve döngü biter.</summary>
    [Fact]
    public async Task ArkaPlanDongusuBulurVeKaybiBirKezDuyurur()
    {
        using var izleyici = Izleyici();
        var bulunan = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var kayip = 0;
        var dongu = izleyici.Start(bulunan.Enqueue, () => Interlocked.Increment(ref kayip));

        var video = Dusur("arka.mp4");
        Assert.True(Bekle(() => !bulunan.IsEmpty), "dosya bulunmadı");
        KlasoruSil(_izlenen);
        await dongu.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(new[] { video }, bulunan.ToArray());
        Assert.Equal(1, Volatile.Read(ref kayip));
    }

    /// <summary>Bırakılan izleyicinin döngüsü biter; bu bir kayıp sayılmaz.</summary>
    [Fact]
    public async Task BirakilanIzleyiciTaramaz()
    {
        var izleyici = Izleyici();
        var bulunan = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var kayip = 0;
        var dongu = izleyici.Start(bulunan.Enqueue, () => Interlocked.Increment(ref kayip));
        Dusur("once.mp4");
        Assert.True(Bekle(() => !bulunan.IsEmpty), "dosya bulunmadı");

        izleyici.Dispose();
        await dongu.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(dongu.IsCompletedSuccessfully);
        Assert.Equal(new[] { "once.mp4" }, bulunan.Select(Path.GetFileName).ToArray());
        Assert.Equal(0, Volatile.Read(ref kayip));
    }

    /// <summary>Uzun yol kökü ve son klasörü tutar; kısa yol olduğu gibi kalır; tek parça sığmasa da düşmez.</summary>
    [Fact]
    public void YolKisaltilir()
    {
        var ayrac = Path.DirectorySeparatorChar;
        var kok = Path.GetPathRoot(Path.GetFullPath(_kok))!;
        var uzun = kok + string.Join(ayrac, "Kullanicilar", "Birisi", "Videolar", "Arsiv", "2026", "Ekim", "Gelenler");
        var kisa = kok + "Gelenler";
        var tek = kok + new string('a', 80);

        var sonuc = QueueWatch.Shorten(uzun);

        Assert.True(uzun.Length > QueueWatch.ShortPathLength);
        Assert.True(sonuc.Length <= QueueWatch.ShortPathLength, sonuc);
        Assert.StartsWith(kok + "…" + ayrac, sonuc, StringComparison.Ordinal);
        Assert.EndsWith(ayrac + "Gelenler", sonuc, StringComparison.Ordinal);
        Assert.Equal(kisa, QueueWatch.Shorten(kisa));
        Assert.Equal(kok + "…" + ayrac + new string('a', 80), QueueWatch.Shorten(tek));
    }

    /// <summary>Anahtar açılınca klasöre düşen video elle eklenenlerin arkasına girer; video olmayan girmez; fazladan tarama çoğaltmaz.</summary>
    [Fact]
    public void PenceredeDusenVideoSirayaGirer()
    {
        var sonuc = Pencerede(window =>
        {
            var basta = (window.Watching, panel: window.FindControl<StackPanel>("WatchPanel")!.IsVisible, window.WatchFolderText);
            var ac = window.SetWatchAsync(true);
            var acildi = (ac.IsCompleted, window.Watching, window.WatchChecked, window.WatchFolderText);
            Dusur("dusen.mp4");
            Dusur("belge.pdf");
            Adimla(window, HazirAdim);
            var ilk = Bekleyen(window);
            Adimla(window, HazirAdim * 3);
            return (basta, acildi, ilk, son: Bekleyen(window), window.AcceptedCount, window.Paused,
                balon: ToolTip.GetTip(window.FindControl<TextBlock>("TxtWatchFolder")!) as string);
        }, secilecek: _izlenen);

        Assert.False(sonuc.basta.Watching);
        Assert.True(sonuc.basta.panel);
        Assert.NotEqual("", sonuc.basta.WatchFolderText);
        Assert.True(sonuc.acildi.IsCompleted);
        Assert.True(sonuc.acildi.Watching);
        Assert.True(sonuc.acildi.WatchChecked);
        Assert.Equal(QueueWatch.Shorten(_izlenen), sonuc.acildi.WatchFolderText);
        Assert.Equal(_izlenen, sonuc.balon);
        Assert.Equal(new[] { "elle-a.mp4", "elle-b.mp4", "dusen.mp4" }, sonuc.ilk);
        Assert.Equal(sonuc.ilk, sonuc.son);
        Assert.Equal(3, sonuc.AcceptedCount);
        Assert.True(sonuc.Paused);
    }

    /// <summary>Kutu işaretlenince izleme başlar, kaldırılınca durur ve sonradan düşen dosya girmez; seçiciden vazgeçilirse kutu kapalı kalır.</summary>
    [Fact]
    public void KutuIzlemeyiAcarVeKapatir()
    {
        var sonuc = Pencerede(window =>
        {
            var kutu = window.FindControl<CheckBox>("ChkWatch")!;
            kutu.IsChecked = true;
            var acik = window.Watching;
            kutu.IsChecked = false;
            var kapali = window.Watching;
            Dusur("gec.mp4");
            Adimla(window, HazirAdim * 2);
            return (acik, kapali, bekleyen: Bekleyen(window), AppSettings.Load(_ayar).WatchEnabled);
        }, secilecek: _izlenen);

        File.Delete(_ayar);
        var vazgecilen = Pencerede(window =>
        {
            window.FindControl<CheckBox>("ChkWatch")!.IsChecked = true;
            return (window.Watching, window.WatchChecked, window.WatchWarningText);
        });

        Assert.True(sonuc.acik);
        Assert.False(sonuc.kapali);
        Assert.Equal(new[] { "elle-a.mp4", "elle-b.mp4" }, sonuc.bekleyen);
        Assert.False(sonuc.WatchEnabled);
        Assert.False(vazgecilen.Watching);
        Assert.False(vazgecilen.WatchChecked);
        Assert.Equal("", vazgecilen.WatchWarningText);
    }

    /// <summary>Sırada bekleyen dosya izlemeden ikinci kez girmez.</summary>
    [Fact]
    public void SiradakiDosyaIzlemedenYenidenGirmez()
    {
        var sonuc = Pencerede(window =>
        {
            Assert.True(window.SetWatchAsync(true).IsCompleted);
            var video = Dusur("cift.mp4");
            Adimla(window, HazirAdim);
            File.WriteAllText(video, "yeniden yazildi ve boyu degisti");
            Adimla(window, HazirAdim * 2);
            return (Bekleyen(window), window.AcceptedCount);
        }, secilecek: _izlenen);

        Assert.Equal(new[] { "elle-a.mp4", "elle-b.mp4", "cift.mp4" }, sonuc.Item1);
        Assert.Equal(3, sonuc.AcceptedCount);
    }

    /// <summary>Klasör silinince izleme durur, kutu kapanır, uyarı satırı görünür; kayıtlı seçim silinmez.</summary>
    [Fact]
    public void KlasorKayboluncaIzlemeDururVeUyariGorunur()
    {
        var sonuc = Pencerede(window =>
        {
            Assert.True(window.SetWatchAsync(true).IsCompleted);
            var once = (window.Watching, window.WatchWarningText);
            KlasoruSil(_izlenen);
            window.WatchStep();
            var uyari = window.FindControl<TextBlock>("TxtWatchWarning")!;
            return (once, window.Watching, window.WatchChecked, window.WatchWarningText,
                tema: ReferenceEquals(uyari.Theme, window.FindResource("StatusWarning")), window.Language, kayit: AppSettings.Load(_ayar));
        }, secilecek: _izlenen);

        Assert.True(sonuc.once.Watching);
        Assert.Equal("", sonuc.once.WatchWarningText);
        Assert.False(sonuc.Watching);
        Assert.False(sonuc.WatchChecked);
        Assert.Equal(Locales.Values(sonuc.Language)["main.shrink-job.watch.lost"], sonuc.WatchWarningText, ignoreCase: true);
        Assert.True(sonuc.tema, "uyarı satırı StatusWarning temasında değil");
        Assert.True(sonuc.kayit.WatchEnabled);
        Assert.Equal(_izlenen, sonuc.kayit.WatchDirectory);
    }

    /// <summary>Seçim ayara yazılır ve geri okunur; ana pencerenin kendi kaydı izleme anahtarlarını silmez.</summary>
    [Fact]
    public void AyarGidisDonusu()
    {
        var bos = AppSettings.Load(_ayar);
        Pencerede(window => window.SetWatchAsync(true).IsCompleted, secilecek: _izlenen);
        var yazilan = AppSettings.Load(_ayar);
        new AppSettings { AdvCrf = 3 }.Save(_ayar);
        var korunan = AppSettings.Load(_ayar);
        AppSettings.SaveWatch(false, _izlenen, _ayar);
        var kapali = AppSettings.Load(_ayar);

        Assert.False(bos.WatchEnabled);
        Assert.Equal("", bos.WatchDirectory);
        Assert.True(yazilan.WatchEnabled);
        Assert.Equal(_izlenen, yazilan.WatchDirectory);
        Assert.True(korunan.WatchEnabled);
        Assert.Equal(_izlenen, korunan.WatchDirectory);
        Assert.Equal(3, korunan.AdvCrf);
        Assert.False(kapali.WatchEnabled);
        Assert.Equal(_izlenen, kapali.WatchDirectory);
        Assert.Equal(3, kapali.AdvCrf);
    }

    /// <summary>Kayıtlı seçim açıksa pencere izleyerek açılır; klasör yoksa sessizce kapalı, uyarısız ve kaydı bozmadan.</summary>
    [Fact]
    public void AcilistaKayitliSecimUygulanir()
    {
        AppSettings.SaveWatch(true, _izlenen, _ayar);
        var varken = Pencerede(window =>
        {
            Dusur("acilis.mp4");
            Adimla(window, HazirAdim);
            return (window.Watching, window.WatchChecked, Bekleyen(window));
        });

        var yok = Path.Combine(_kok, "hic-olmadi");
        AppSettings.SaveWatch(true, yok, _ayar);
        var yokken = Pencerede(window => (window.Watching, window.WatchChecked, window.WatchWarningText, window.WatchFolderText));
        var kayit = AppSettings.Load(_ayar);

        Assert.True(varken.Watching);
        Assert.True(varken.WatchChecked);
        Assert.Contains("acilis.mp4", varken.Item3);
        Assert.False(yokken.Watching);
        Assert.False(yokken.WatchChecked);
        Assert.Equal("", yokken.WatchWarningText);
        Assert.Equal(QueueWatch.Shorten(yok), yokken.WatchFolderText);
        Assert.True(kayit.WatchEnabled);
        Assert.Equal(yok, kayit.WatchDirectory);
    }

    /// <summary>"Klasör seç" izleme açıkken yeni klasöre geçer; kapalıyken yalnız yolu yazar, izlemeyi açmaz.</summary>
    [Fact]
    public void KlasorSecimiIzlemeyiYeniKlasoreTasir()
    {
        var ikinci = Path.Combine(_kok, "ikinci");
        Directory.CreateDirectory(ikinci);
        var hedef = _izlenen;

        var sonuc = Pencerede(window =>
        {
            window.PickWatchFolder = () => Task.FromResult<string?>(hedef);
            var kapaliykenSec = window.ChooseWatchFolderAsync().IsCompleted && !window.Watching;
            var yol = window.WatchFolderText;
            Assert.True(window.SetWatchAsync(true).IsCompleted);
            hedef = ikinci;
            Assert.True(window.ChooseWatchFolderAsync().IsCompleted);
            Dusur("eski-klasore.mp4");
            File.WriteAllText(Path.Combine(ikinci, "yeni-klasore.mp4"), "video");
            Adimla(window, HazirAdim * 2);
            return (kapaliykenSec, yol, window.Watching, Bekleyen(window), AppSettings.Load(_ayar).WatchDirectory);
        });

        Assert.True(sonuc.kapaliykenSec);
        Assert.Equal(QueueWatch.Shorten(_izlenen), sonuc.yol);
        Assert.True(sonuc.Watching);
        Assert.Equal(new[] { "elle-a.mp4", "elle-b.mp4", "yeni-klasore.mp4" }, sonuc.Item4);
        Assert.Equal(ikinci, sonuc.WatchDirectory);
    }

    /// <summary>Gerçek arka plan döngüsüyle: düşen dosya sıraya girer; pencere kapanınca izleyici bırakılır.</summary>
    [Fact]
    public void ArkaPlandaSirayaGirerVeKapanincaBirakilir()
    {
        ShrinkJobWindow? kapanan = null;
        var sonuc = Pencerede(window =>
        {
            kapanan = window;
            Assert.True(window.SetWatchAsync(true).IsCompleted);
            Dusur("canli.mp4");
            var girdi = Bekle(() => window.Pending.Count == 3, () => Dispatcher.UIThread.RunJobs());
            return (girdi, Bekleyen(window), window.Watching);
        }, arkaPlan: true, secilecek: _izlenen);


        Assert.True(sonuc.girdi, "dosya sıraya girmedi");
        Assert.Equal(new[] { "elle-a.mp4", "elle-b.mp4", "canli.mp4" }, sonuc.Item2);
        Assert.True(sonuc.Watching);
        Assert.False(kapanan!.Watching);
    }

    /// <summary>
    /// Uçtan uca, gerçek kodlamayla: izlenen klasöre yazılan klip yazımı bitmeden sıraya girmez,
    /// bitince bir kez girer ve kodlanır; çıktı aynı klasöre düşer ve ne ara dosyası ne kendisi
    /// yeniden sıraya girer. Tarama kodlama sürerken de yürür, ara dosya o sırada klasörde durur.
    /// </summary>
    [FfmpegFact]
    public async Task GercekKodlamadaCiktiYenidenSirayaGirmez()
    {
        var kaynak = Path.Combine(_kok, "kaynak.mp4");
        var uret = await FfmpegRunner.RunAsync(new[]
        {
            "-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=30:duration=1",
            "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "4", "-pix_fmt", "yuv420p", kaynak
        });
        Assert.True(uret.Ok, uret.StandardError);
        var bayt = File.ReadAllBytes(kaynak);
        var yari = bayt.Length / 2;
        var dusen = Path.Combine(_izlenen, "dusen.mp4");

        var o = Pencerede(window =>
        {
            var biten = new List<(string Kaynak, string? Cikti)>();
            window.JobFinished += (yol, cikti) => biten.Add((yol, cikti));
            Assert.True(window.SetWatchAsync(true).IsCompleted);

            int yarimken;
            using (var akis = new FileStream(dusen, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                akis.Write(bayt, 0, yari);
                akis.Flush(true);
                Adimla(window, HazirAdim * 2);
                yarimken = window.AcceptedCount;
                akis.Write(bayt, yari, bayt.Length - yari);
            }

            var araDosya = false;
            void Tur()
            {
                window.WatchStep();
                Dispatcher.UIThread.RunJobs();
                araDosya |= Directory.EnumerateFiles(_izlenen, QueueWatch.PartialPrefix + "*").Any();
            }

            var bitti = Bekle(() => biten.Count > 0, Tur, 180);
            for (var i = 0; i < HazirAdim * 4; i++)
            {
                Tur();
                Thread.Sleep(20);
            }
            var durdu = Bekle(() => window.State != ShrinkJobState.Kosuyor && window.Pending.Count == 0, Tur, 180);
            return (yarimken, bitti, durdu, araDosya, biten: biten.ToArray(), window.AcceptedCount, bekleyen: window.Pending.Count,
                ciktilar: window.Outputs.ToArray(), window.State, window.MessageText, window.Watching);
        }, secilecek: _izlenen, sablon: new PlanOptions { TargetMb = Megabayt.Oku(bayt.Length) / 2 });

        Assert.Equal(0, o.yarimken);
        Assert.True(o.bitti, "iş bitmedi");
        Assert.True(o.durdu, "kuyruk durmadı");
        Assert.True(o.State == ShrinkJobState.Bitti, o.MessageText);
        Assert.True(o.Watching);
        Assert.Equal(1, o.AcceptedCount);
        Assert.Equal(0, o.bekleyen);
        var is_ = Assert.Single(o.biten);
        var cikti = Assert.Single(o.ciktilar);
        Assert.Equal(dusen, is_.Kaynak);
        Assert.Equal(cikti, is_.Cikti);
        Assert.Equal(_izlenen, Path.GetDirectoryName(cikti));
        Assert.True(o.araDosya, "tarama kodlama sürerken ara dosyayı görmedi");
        Assert.Equal(new[] { dusen, cikti }.Order(), Directory.GetFiles(_izlenen).Where(WatchFolder.IsCandidate).Order());
        Assert.InRange(new FileInfo(cikti).Length, 1, bayt.Length - 1);
        var okunan = await FfprobeClient.ProbeAsync(cikti);
        Assert.InRange(okunan.DurationSeconds, 0.8, 1.3);
        Assert.True(QueueWatch.IsOwn(cikti));
    }

    /// <summary>İzleme yalnız bırakılan kuyrukta: kabuk menüsünün hızlı küçültme penceresinde panel yok.</summary>
    [Fact]
    public void KabukPenceresindeIzlemePaneliYok()
    {
        var gorunur = AppHost.Run(() =>
        {
            var eskiAyar = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", _ayar);
            AppSettings.SaveWatch(true, _izlenen, _ayar);
            var window = new ShrinkJobWindow(new ShellShrinkStartup(new[] { new ShrinkRequest(25, Path.Combine(_kok, "kabuk.mp4")) }, null, null), null)
            {
                Actions = new SahteEylem(),
                WatchInBackground = false
            };
            try
            {
                window.SetPaused(true);
                window.Begin();
                return (window.FindControl<StackPanel>("WatchPanel")!.IsVisible, window.Watching);
            }
            finally
            {
                window.Close();
                Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", eskiAyar);
                Strings.Use("en");
            }
        });

        Assert.False(gorunur.IsVisible);
        Assert.False(gorunur.Watching);
    }

    /// <summary>Beş anahtar 42 dilde dolu ve yer tutucusuz.</summary>
    [Fact]
    public void AnahtarlarButunDillerde()
    {
        var anahtarlar = new[]
        {
            "main.shrink-job.watch", "main.shrink-job.watch.choose", "main.shrink-job.watch.none",
            "main.shrink-job.watch.hint", "main.shrink-job.watch.lost"
        };
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in anahtarlar)
            {
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
                Assert.DoesNotContain("{", metin, StringComparison.Ordinal);
            }
        }
    }
}
