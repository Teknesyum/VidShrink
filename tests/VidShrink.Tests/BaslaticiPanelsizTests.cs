using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Launcher;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class LauncherBinaryFactAttribute : FactAttribute
{
    public LauncherBinaryFactAttribute()
    {
        if (BaslaticiPanelsizTests.LauncherPath is null)
            Skip = "No built launcher was found under src/VidShrink.Launcher/bin, so the binary was not inspected.";
    }
}

public sealed class SahteKurulumFactAttribute : FactAttribute
{
    public SahteKurulumFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Başlatıcı yalnız Windows'ta koşuyor.";
        else if (BaslaticiPanelsizTests.LauncherPath is null || BaslaticiPanelsizTests.SahteUygulamaPath is null)
            Skip = "Başlatıcı ya da tools/VidShrink.SahteUygulama derlenmemiş.";
    }
}

public sealed class BaslaticiPanelsizTests
{
    private readonly ITestOutputHelper _cikti;

    public BaslaticiPanelsizTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static string Root => TipSources.Root;

    public static string? LauncherPath => EnSon(Path.Combine(Root, "src", "VidShrink.Launcher", "bin"), "VidShrink.exe");

    public static string? SahteUygulamaPath => EnSon(Path.Combine(Root, "tools", "VidShrink.SahteUygulama", "bin"), "VidShrink.App.exe");

    private static string? EnSon(string klasor, string ad) => !Directory.Exists(klasor)
        ? null
        : Directory.EnumerateFiles(klasor, ad, SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

    [LauncherBinaryFact]
    public void LauncherStaysAGuiBinary()
    {
        var bytes = File.ReadAllBytes(LauncherPath!);
        var pe = BitConverter.ToInt32(bytes, 0x3C);
        Assert.Equal("PE\0\0", Encoding.ASCII.GetString(bytes, pe, 4));
        var subsystem = BitConverter.ToUInt16(bytes, pe + 4 + 20 + 68);
        Assert.Equal(2, subsystem);
    }

    [Fact]
    public void PanelKaynaktanKalkti()
    {
        var baslatici = Path.Combine(Root, "src", "VidShrink.Launcher");
        Assert.False(File.Exists(Path.Combine(baslatici, "Splash.cs")));
        var splashGen = Path.Combine(Root, "tools", "VidShrink.SplashGen");
        var kaynaklar = Directory.Exists(splashGen)
            ? Directory.EnumerateFiles(splashGen, "*.*", SearchOption.AllDirectories)
                .Where(y => !y.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !y.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .ToList()
            : new List<string>();
        Assert.Empty(kaynaklar);
        foreach (var dosya in Directory.EnumerateFiles(baslatici, "*.cs*", SearchOption.TopDirectoryOnly))
        {
            var metin = File.ReadAllText(dosya);
            Assert.DoesNotContain("Splash", metin, StringComparison.Ordinal);
            Assert.DoesNotContain("InstallProgress", metin, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void UygulamaIlkSatirdaDevreder()
    {
        var kaynak = File.ReadAllText(Path.Combine(Root, "src", "VidShrink.App", "Program.cs"));
        var govde = kaynak[kaynak.IndexOf("public static int Main(string[] args)", StringComparison.Ordinal)..];
        var ilk = govde.IndexOf("BaslaticiyaDevret(AppContext.BaseDirectory, args)) return 0;", StringComparison.Ordinal);
        Assert.True(ilk > 0, "Main başlatıcıya devretmiyor");
        Assert.True(ilk < govde.IndexOf("StartupFor(args)", StringComparison.Ordinal), "devir ilk iş değil");
    }

    [Theory]
    [InlineData(null, true, true, false, true)]
    [InlineData(null, true, false, true, true)]
    [InlineData(null, true, false, false, false)]
    [InlineData("1", true, true, true, false)]
    [InlineData(null, false, true, true, false)]
    public void DevirKarari(string? baslaticidan, bool baslaticiVar, bool tutuluyor, bool bekleyen, bool beklenen)
        => Assert.Equal(beklenen, UygulamaKlasoruKapisi.Devretmeli(baslaticidan, baslaticiVar, tutuluyor, bekleyen));

    [SahteKurulumFact]
    public void YavasBakimdaPencereYokUygulamaHemenAcilir()
    {
        using var kurulum = new SahteKurulum();
        const int gecikme = 4000;
        var saat = Stopwatch.StartNew();
        using var baslatici = kurulum.Baslatici(gecikme, omur: 6000);

        var gorunenler = new HashSet<string>();
        double? dogum = null;
        while (saat.ElapsedMilliseconds < gecikme - 300)
        {
            foreach (var pencere in GorunurPencereler(baslatici.Id)) gorunenler.Add(pencere);
            if (dogum is null && kurulum.Olaylar().Any(o => o.Olay == "acildi" && o.Deger == "1"))
                dogum = saat.Elapsed.TotalMilliseconds;
            Thread.Sleep(20);
        }

        _cikti.WriteLine($"uygulama-dogumu-ms\t{dogum}");
        _cikti.WriteLine($"baslatici-pencereleri\t{gorunenler.Count}\t{string.Join(" | ", gorunenler)}");
        Assert.False(baslatici.HasExited, "başlatıcı gecikme bitmeden çıktı; kanca koşmadı");
        Assert.Empty(gorunenler);
        Assert.NotNull(dogum);
        Assert.True(dogum < gecikme / 2.0, $"uygulama {dogum} ms'de doğdu, bakım açılışı bekletti");
        Assert.True(baslatici.WaitForExit(15000));
    }

    [SahteKurulumFact]
    public void KapiTutulurkenAcilanUygulamaBaslaticiyaDevreder()
    {
        using var kurulum = new SahteKurulum();
        kurulum.YarimKopyaKur();

        using var baslatici = kurulum.Baslatici(gecikme: 2500, omur: 1500);
        Assert.True(Bekle(() => UygulamaKlasoruKapisi.Tutuluyor(kurulum.App), 15000), "başlatıcı kapıyı tutmadı");

        using var dogrudan = kurulum.UygulamaDogrudan(baslaticidan: false, omur: 1500);
        kurulum.HepsiniBekle(60000);
        kurulum.Dokum(_cikti);

        var olaylar = kurulum.Olaylar();
        Assert.Contains(olaylar, o => o.Pid == dogrudan.Id && o.Olay == "devretti");
        Assert.DoesNotContain(olaylar, o => o.Pid == dogrudan.Id && o.Olay == "kilit");
        Assert.DoesNotContain(olaylar, o => o.Olay == "kilit" && o.Deger != "v2");
        Assert.Contains(olaylar, o => o.Olay == "kilit" && o.Deger == "v2");
        kurulum.HepsiYeniSurum();
    }

    [SahteKurulumFact]
    public void KosanUygulamaKapanmadanKopyaBaslamaz()
    {
        using var kurulum = new SahteKurulum();
        kurulum.YarimKopyaKur();

        using var eski = kurulum.UygulamaDogrudan(baslaticidan: true, omur: 2500);
        Assert.True(Bekle(() => kurulum.Olaylar().Any(o => o.Pid == eski.Id && o.Olay == "kilit"), 15000), "eski uygulama kilidi almadı");

        using var baslatici = kurulum.Baslatici(gecikme: 0, omur: 1000);
        kurulum.HepsiniBekle(60000);
        kurulum.Dokum(_cikti);

        var olaylar = kurulum.Olaylar();
        Assert.Contains(olaylar, o => o.Pid == eski.Id && o.Olay == "kilit" && o.Deger == "v1");
        var eskiKapandi = olaylar.Single(o => o.Pid == eski.Id && o.Olay == "kapandi").Zaman;
        var yeni = olaylar.Single(o => o.Pid != eski.Id && o.Olay == "kilit");
        Assert.Equal("v2", yeni.Deger);
        Assert.True(yeni.Zaman > eskiKapandi);
        kurulum.HepsiYeniSurum();
    }

    [Fact]
    public void BakimHatasiUygulamadaBildirilir()
    {
        var klasor = Path.Combine(Root, ".calisma", "yol-d", "bakim-hatasi-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        try
        {
            var sonuc = AppHost.Run(() =>
            {
                var satirlar = new List<string>();
                foreach (var isaretli in new[] { false, true })
                {
                    if (isaretli) File.WriteAllText(Path.Combine(klasor, UygulamaKlasoruKapisi.HataIsareti), "deneme");
                    var pencere = new MainWindow { BakimKlasoru = klasor };
                    var olay = typeof(Window).GetEvent(nameof(Window.Opened))!;
                    olay.RemoveEventHandler(pencere, Delegate.CreateDelegate(typeof(EventHandler), pencere, "OnWindowLoaded"));
                    try
                    {
                        pencere.Show();
                        Dispatcher.UIThread.RunJobs();
                        pencere.UpdateFrame(TimeSpan.FromMilliseconds(InstallProgress.FrameMilliseconds));
                        var gecmis = pencere.UpdateLogLines.Children.OfType<TextBlock>().LastOrDefault()?.Text ?? "";
                        satirlar.Add($"{isaretli}:{pencere.UpdateNotice.IsVisible}:{pencere.BtnNoticeDismiss.IsVisible}:{gecmis}");
                    }
                    finally { pencere.Close(); }
                }
                return satirlar;
            });

            var cumle = LanguageCatalog.Display(Strings.Get("main.update.maintenance-failed"));
            Assert.Equal(new[] { "False:False:True:", $"True:True:True:{cumle}" }, sonuc);
            Assert.False(File.Exists(Path.Combine(klasor, UygulamaKlasoruKapisi.HataIsareti)));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void IkinciBekleyenIndirmezBeklemez()
    {
        using var kurulum = new BekleyenKlasoru();
        var sahne = kurulum.Sahne("9.9.9", bozuk: false);
        using var kapi = KapiTutucu.Baslat(kurulum.App);

        var indirme = 0;
        StagedUpdate? Indir()
        {
            Interlocked.Increment(ref indirme);
            return sahne;
        }

        var birinci = Arka.Baslat(
            () => KurulumBekleyeni.Calistir(kurulum.Kok, kurulum.App, false, kurulum.Kilit, Indir, false));
        Assert.True(Bekle(() => Volatile.Read(ref indirme) == 1, 5000), "ilk bekleyen indirmedi");
        Thread.Sleep(300);
        Assert.False(birinci.Bitti, "ilk bekleyen kapıyı beklemedi");

        var saat = Stopwatch.StartNew();
        var ikinci = Arka.Baslat(
            () => KurulumBekleyeni.Calistir(kurulum.Kok, kurulum.App, false, kurulum.Kilit, Indir, false));
        var ikinciHemenBitti = ikinci.Bekle(3000);
        _cikti.WriteLine($"ikinci-bekleyen-ms\t{saat.ElapsedMilliseconds}\tbitti\t{ikinciHemenBitti}\tindirme\t{Volatile.Read(ref indirme)}");

        kapi.Birak();
        Assert.True(birinci.Bekle(20000), "ilk bekleyen kapı bırakılınca bitmedi");
        Assert.True(ikinci.Bekle(20000));

        Assert.True(ikinciHemenBitti, "ikinci açılış bekleyene katıldı");
        Assert.False(ikinci.Sonuc);
        Assert.Equal(1, indirme);
        Assert.Equal("v2", File.ReadAllText(Path.Combine(kurulum.App, "a.txt")));
        Assert.False(File.Exists(Path.Combine(kurulum.App, UygulamaKlasoruKapisi.HataIsareti)));
    }

    [Fact]
    public void KurulmusSurumdeHataYazilmaz()
    {
        using var kurulum = new BekleyenKlasoru();
        var isaret = Path.Combine(kurulum.App, UygulamaKlasoruKapisi.HataIsareti);
        var sahne = kurulum.Sahne("9.9.9", bozuk: false);

        KurulumBekleyeni.Kur(kurulum.Kok, kurulum.App, sahne, kurulum.Kilit, TimeSpan.FromSeconds(5));
        Assert.Equal("v2", File.ReadAllText(Path.Combine(kurulum.App, "a.txt")));
        Assert.False(Directory.Exists(sahne.Stage), "ilk kurulum sahneyi silmedi");
        Assert.False(File.Exists(isaret));

        Assert.False(KurulumBekleyeni.Kur(kurulum.Kok, kurulum.App, sahne, kurulum.Kilit, TimeSpan.FromSeconds(5)));
        var ikinciSonra = File.Exists(isaret) ? File.ReadAllText(isaret) : null;
        _cikti.WriteLine($"ikinci-kurulum-isareti\t{ikinciSonra}");
        Assert.Null(ikinciSonra);

        var bozuk = kurulum.Sahne("9.9.10", bozuk: true);
        Assert.False(KurulumBekleyeni.Kur(kurulum.Kok, kurulum.App, bozuk, kurulum.Kilit, TimeSpan.FromSeconds(5)));
        Assert.True(File.Exists(isaret), "kurulmamış sürümün gerçek hatası yutuldu");
    }

    [Fact]
    public void ElleYukleBaskaKopyaAcikkenUzunBeklemez()
    {
        using var kurulum = new BekleyenKlasoru();
        var sahne = kurulum.Sahne("9.9.9", bozuk: false);
        using var kapi = KapiTutucu.Baslat(kurulum.App);

        var sinir = TimeSpan.FromSeconds(20);
        var saat = Stopwatch.StartNew();
        var elle = Arka.Baslat(
            () => KurulumBekleyeni.Calistir(kurulum.Kok, kurulum.App, true, kurulum.Kilit, () => sahne, false));
        var bitti = elle.Bekle(sinir);
        _cikti.WriteLine($"elle-bekleme-ms\t{saat.ElapsedMilliseconds}\tbitti\t{bitti}");

        kapi.Birak();
        Assert.True(elle.Bekle(20000));
        Assert.True(bitti, $"elle Yükle {sinir.TotalSeconds} sn içinde bırakmadı");
        Assert.Equal("v1", File.ReadAllText(Path.Combine(kurulum.App, "a.txt")));
    }

    [Fact]
    public void KurKendiBeklemeParametresiniAsmaz()
    {
        using var kurulum = new BekleyenKlasoru();
        var sahne = kurulum.Sahne("9.9.9", bozuk: false);
        using var kapi = KapiTutucu.Baslat(kurulum.App);

        var saat = Stopwatch.StartNew();
        var kurma = Arka.Baslat(
            () => KurulumBekleyeni.Kur(kurulum.Kok, kurulum.App, sahne, kurulum.Kilit, TimeSpan.FromSeconds(2)));
        var bitti = kurma.Bekle(TimeSpan.FromSeconds(8));
        _cikti.WriteLine($"kur-bekleme-ms\t{saat.ElapsedMilliseconds}\tbitti\t{bitti}");

        kapi.Birak();
        Assert.True(kurma.Bekle(20000));
        Assert.True(bitti, "Kur dışarıdan verilen beklemeyi aştı");
        Assert.False(kurma.Sonuc);
        Assert.Equal("v1", File.ReadAllText(Path.Combine(kurulum.App, "a.txt")));
    }

    [Fact]
    public void ElleBeklemeKurmaParametresineGidiyor()
    {
        Assert.Equal(KurulumBekleyeni.ElleBekleme, KurulumBekleyeni.KurulumBeklemesi(elle: true));
        Assert.Equal(KurulumBekleyeni.ArkaPlanBeklemesi, KurulumBekleyeni.KurulumBeklemesi(elle: false));
        Assert.True(KurulumBekleyeni.ElleBekleme <= TimeSpan.FromSeconds(15), "elle bekleme dakikalara çıktı");
        Assert.True(KurulumBekleyeni.ElleYuvaBeklemesi <= TimeSpan.FromSeconds(5));
        Assert.True(KurulumBekleyeni.ElleKilitBeklemesi <= TimeSpan.FromSeconds(30));

        var guncelleyici = File.ReadAllText(Path.Combine(Root, "src", "VidShrink.Launcher", "Updater.cs"));
        Assert.Contains("ElleButcesi = TimeSpan.FromSeconds(60)", guncelleyici, StringComparison.Ordinal);
        Assert.Contains("force ? ElleButcesi : Budget", guncelleyici, StringComparison.Ordinal);

        var kaynak = File.ReadAllText(Path.Combine(Root, "src", "VidShrink.Launcher", "KurulumBekleyeni.cs"));
        Assert.Contains("KurulumBeklemesi(elle), KurulumKilidi(elle)", kaynak, StringComparison.Ordinal);
        Assert.Contains("Tut(bekleyen, YuvaBeklemesi(elle))", kaynak, StringComparison.Ordinal);
        Assert.Contains("Tut(kilit, IndirmeKilidi(elle))", kaynak, StringComparison.Ordinal);
    }

    [Fact]
    public void YuvaButcesiVazgecmeSuresiniBelirler()
    {
        using var kurulum = new BekleyenKlasoru();
        var sahne = kurulum.Sahne("9.9.9", bozuk: false);
        var a = Path.Combine(kurulum.App, "a.txt");

        var indirme = 0;
        StagedUpdate? Indir()
        {
            Interlocked.Increment(ref indirme);
            return sahne;
        }

        long arkaMs;
        long elleMs;
        using (MutexTutucu.Baslat(KurulumBekleyeni.Ad(kurulum.App)))
        {
            var arkaSaat = Stopwatch.StartNew();
            var arka = Arka.Baslat(
                () => KurulumBekleyeni.Calistir(kurulum.Kok, kurulum.App, false, kurulum.Kilit, Indir, false));
            var arkaBitti = arka.Bekle(2000);
            arkaMs = arkaSaat.ElapsedMilliseconds;

            var elleSaat = Stopwatch.StartNew();
            var elle = Arka.Baslat(
                () => KurulumBekleyeni.Calistir(kurulum.Kok, kurulum.App, true, kurulum.Kilit, Indir, false));
            var elleBitti = elle.Bekle(12000);
            elleMs = elleSaat.ElapsedMilliseconds;

            _cikti.WriteLine($"yuva-arka-ms\t{arkaMs}\tyuva-elle-ms\t{elleMs}\tindirme\t{Volatile.Read(ref indirme)}");
            Assert.True(arkaBitti, $"arka plan turu yuva tutuluyorken {arkaMs} ms sonra hâlâ bekliyordu");
            Assert.True(elleBitti, $"elle yol yuva bütçesini aştı, {elleMs} ms sonra hâlâ bekliyordu");
            Assert.False(arka.Sonuc);
            Assert.False(elle.Sonuc);
            Assert.Equal(0, Volatile.Read(ref indirme));
            Assert.Equal("v1", File.ReadAllText(a));
        }

        Assert.True(arkaMs < 900, $"arka plan turu yuva için {arkaMs} ms bekledi; bütçesi sıfır değil");
        Assert.InRange(elleMs, 2000, 9000);

        var serbest = KurulumBekleyeni.Calistir(kurulum.Kok, kurulum.App, true, kurulum.Kilit, Indir, false);
        _cikti.WriteLine($"yuva-bosken\tsonuc\t{serbest}\tindirme\t{Volatile.Read(ref indirme)}\ta.txt\t{File.ReadAllText(a)}\tisaret\t{Isaret(kurulum.App)}");
        Assert.Equal("9.9.9", UpdateCheck.ReadVersionMarker(kurulum.App));
        Assert.Equal(1, Volatile.Read(ref indirme));
        Assert.Equal("v2", File.ReadAllText(a));
    }

    [Fact]
    public void IndirmeKilidiButcesiVazgecmeSuresiniBelirler()
    {
        using var kurulum = new BekleyenKlasoru();
        var sahne = kurulum.Sahne("9.9.9", bozuk: false);
        var a = Path.Combine(kurulum.App, "a.txt");

        var indirme = 0;
        StagedUpdate? Indir()
        {
            Interlocked.Increment(ref indirme);
            return sahne;
        }

        long arkaMs;
        long elleMs;
        using (MutexTutucu.Baslat(kurulum.Kilit))
        {
            var arkaSaat = Stopwatch.StartNew();
            var arka = Arka.Baslat(
                () => KurulumBekleyeni.Calistir(kurulum.Kok, kurulum.App, false, kurulum.Kilit, Indir, false));
            var arkaBitti = arka.Bekle(2000);
            arkaMs = arkaSaat.ElapsedMilliseconds;

            var elleSaat = Stopwatch.StartNew();
            var elle = Arka.Baslat(
                () => KurulumBekleyeni.Calistir(kurulum.Kok, kurulum.App, true, kurulum.Kilit, Indir, false));
            var elleBitti = elle.Bekle(35000);
            elleMs = elleSaat.ElapsedMilliseconds;

            _cikti.WriteLine($"indirme-kilidi-arka-ms\t{arkaMs}\tindirme-kilidi-elle-ms\t{elleMs}\tindirme\t{Volatile.Read(ref indirme)}");
            Assert.True(arkaBitti, $"arka plan turu indirme kilidini {arkaMs} ms boyunca bekledi");
            Assert.True(elleBitti, $"elle yol indirme kilidi bütçesini aştı, {elleMs} ms sonra hâlâ bekliyordu");
            Assert.False(arka.Sonuc);
            Assert.False(elle.Sonuc);
            Assert.Equal(0, Volatile.Read(ref indirme));
            Assert.Equal("v1", File.ReadAllText(a));
        }

        Assert.True(arkaMs < 900, $"arka plan turu indirme kilidi için {arkaMs} ms bekledi; bütçesi sıfır değil");
        Assert.InRange(elleMs, 15000, 27000);

        var serbest = KurulumBekleyeni.Calistir(kurulum.Kok, kurulum.App, true, kurulum.Kilit, Indir, false);
        _cikti.WriteLine($"indirme-kilidi-bosken\tsonuc\t{serbest}\tindirme\t{Volatile.Read(ref indirme)}\ta.txt\t{File.ReadAllText(a)}\tisaret\t{Isaret(kurulum.App)}");
        Assert.Equal("9.9.9", UpdateCheck.ReadVersionMarker(kurulum.App));
        Assert.Equal(1, Volatile.Read(ref indirme));
        Assert.Equal("v2", File.ReadAllText(a));
    }

    [Fact]
    public void ElleKurulumKilidiButcesiVazgecmeSuresiniBelirler()
    {
        using var kurulum = new BekleyenKlasoru();
        var sahne = kurulum.Sahne("9.9.9", bozuk: false);
        var a = Path.Combine(kurulum.App, "a.txt");

        bool Kur() => KurulumBekleyeni.Kur(
            kurulum.Kok, kurulum.App, sahne, kurulum.Kilit,
            KurulumBekleyeni.KurulumBeklemesi(elle: true), KurulumBekleyeni.KurulumKilidi(elle: true));

        long ms;
        using (MutexTutucu.Baslat(kurulum.Kilit))
        {
            var saat = Stopwatch.StartNew();
            var kurma = Arka.Baslat(Kur);
            var bitti = kurma.Bekle(35000);
            ms = saat.ElapsedMilliseconds;

            _cikti.WriteLine($"kurulum-kilidi-elle-ms\t{ms}\tbitti\t{bitti}\ta.txt\t{File.ReadAllText(a)}\tisaret\t{UpdateCheck.ReadVersionMarker(kurulum.App)}");
            Assert.True(bitti, $"elle kurulum kilidi bütçesini aştı, {ms} ms sonra hâlâ bekliyordu");
            Assert.False(kurma.Sonuc);
            Assert.Equal("v1", File.ReadAllText(a));
            Assert.Null(UpdateCheck.ReadVersionMarker(kurulum.App));
            Assert.Null(Isaret(kurulum.App));
        }

        Assert.InRange(ms, 15000, 27000);

        var serbest = Kur();
        _cikti.WriteLine($"kurulum-kilidi-bosken\tsonuc\t{serbest}\ta.txt\t{File.ReadAllText(a)}\tisaret\t{UpdateCheck.ReadVersionMarker(kurulum.App)}");
        Assert.Equal("v2", File.ReadAllText(a));
        Assert.Equal("9.9.9", UpdateCheck.ReadVersionMarker(kurulum.App));
    }

    [Fact]
    public void ArkaPlanKurulumKilidiKisaTutmadaVazgecmez()
    {
        using var kurulum = new BekleyenKlasoru();
        var sahne = kurulum.Sahne("9.9.9", bozuk: false);
        var a = Path.Combine(kurulum.App, "a.txt");

        var saat = Stopwatch.StartNew();
        Arka kurma;
        using (MutexTutucu.Baslat(kurulum.Kilit))
        {
            kurma = Arka.Baslat(() => KurulumBekleyeni.Kur(
                kurulum.Kok, kurulum.App, sahne, kurulum.Kilit,
                KurulumBekleyeni.KurulumBeklemesi(elle: false), KurulumBekleyeni.KurulumKilidi(elle: false)));
            Assert.False(kurma.Bekle(6000), "arka plan kurulumu kilidi beklemeden vazgeçti");
            Assert.Equal("v1", File.ReadAllText(a));
        }

        Assert.True(kurma.Bekle(25000), "kilit bırakılınca arka plan kurulumu bitmedi");
        var ms = saat.ElapsedMilliseconds;
        _cikti.WriteLine($"arka-kurulum-kilidi-ms\t{ms}\ta.txt\t{File.ReadAllText(a)}\tisaret\t{UpdateCheck.ReadVersionMarker(kurulum.App)}");
        Assert.InRange(ms, 6000, 25000);
        Assert.Equal("v2", File.ReadAllText(a));
        Assert.Equal("9.9.9", UpdateCheck.ReadVersionMarker(kurulum.App));
    }


    [Fact]
    public void DahaYeniSurumEskiyeDusurulmez()
    {
        using var kurulum = new BekleyenKlasoru();
        var sahne = kurulum.Sahne("9.9.9", bozuk: false);
        UpdateCheck.WriteVersionMarker(kurulum.App, "9.9.11");

        Assert.False(KurulumBekleyeni.Kur(kurulum.Kok, kurulum.App, sahne, kurulum.Kilit, TimeSpan.FromSeconds(5)));
        _cikti.WriteLine($"a.txt\t{File.ReadAllText(Path.Combine(kurulum.App, "a.txt"))}\tisaret\t{UpdateCheck.ReadVersionMarker(kurulum.App)}");
        Assert.Equal("v1", File.ReadAllText(Path.Combine(kurulum.App, "a.txt")));
        Assert.Equal("9.9.11", UpdateCheck.ReadVersionMarker(kurulum.App));
        Assert.False(File.Exists(Path.Combine(kurulum.App, UygulamaKlasoruKapisi.HataIsareti)));

        Assert.True(KurulumBekleyeni.Kurulmus("9.9.9", "9.9.9"));
        Assert.True(KurulumBekleyeni.Kurulmus("9.9.11", "9.9.9"));
        Assert.False(KurulumBekleyeni.Kurulmus("9.9.8", "9.9.9"));
        Assert.False(KurulumBekleyeni.Kurulmus(null, "9.9.9"));
    }

    [Fact]
    public void ProvaKipiKurmaz()
    {
        using var kurulum = new BekleyenKlasoru();
        var sahne = kurulum.Sahne("9.9.9", bozuk: false);
        var a = Path.Combine(kurulum.App, "a.txt");

        Assert.False(KurulumBekleyeni.Calistir(kurulum.Kok, kurulum.App, false, kurulum.Kilit, () => sahne, true));
        _cikti.WriteLine($"prova-sonrasi\t{File.ReadAllText(a)}\tsahne\t{Directory.Exists(sahne.Stage)}");
        Assert.Equal("v1", File.ReadAllText(a));
        Assert.True(Directory.Exists(sahne.Stage), "prova sahneyi tüketti");
        Assert.Null(UpdateCheck.ReadVersionMarker(kurulum.App));

        KurulumBekleyeni.Calistir(kurulum.Kok, kurulum.App, false, kurulum.Kilit, () => sahne, false);
        _cikti.WriteLine($"prova-kapaliyken\t{File.ReadAllText(a)}\tisaret\t{UpdateCheck.ReadVersionMarker(kurulum.App)}");
        Assert.Equal("v2", File.ReadAllText(a));
        Assert.Equal("9.9.9", UpdateCheck.ReadVersionMarker(kurulum.App));
    }

    [SahteKurulumFact]
    public void ElleYukleArkaPlanIndirirkenAcilisiGeciktirmez()
    {
        using var kurulum = new SahteKurulum();
        var kaynak = kurulum.SahteYayin("9.9.9");
        using var yuva = MutexTutucu.Baslat(KurulumBekleyeni.Ad(kurulum.App));
        using var kilit = MutexTutucu.Baslat(UpdateStaging.MutexName);

        var saat = Stopwatch.StartNew();
        using var baslatici = kurulum.Baslatici(
            gecikme: 0, omur: 1500, kaynak: kaynak, args: new[] { LauncherUpdate.UpdateNowArgument, "999999" });
        var dogdu = Bekle(() => kurulum.Olaylar().Any(o => o.Olay == "acildi"), 25000);
        var ms = saat.ElapsedMilliseconds;
        _cikti.WriteLine($"elle-yukle-app-dogumu-ms\t{ms}\tdogdu\t{dogdu}");

        Assert.True(dogdu, "elle Yükle yolunda uygulama hiç açılmadı");
        Assert.True(ms < 15000, $"uygulama {ms} ms'de açıldı; elle yol arka plan turunu bekledi");
        kurulum.HepsiniBekle(60000);
    }

    [SahteKurulumFact]
    public void IkinciBaslaticiSurecindeYuvaAlinmaz()
    {
        using var kurulum = new SahteKurulum();
        var kaynak = kurulum.SahteYayin("9.9.9");
        var ayar = kurulum.AyarDosyasi();
        var a = Path.Combine(kurulum.App, "a.txt");

        using (MutexTutucu.Baslat(KurulumBekleyeni.Ad(kurulum.App)))
        {
            using var ilk = kurulum.Baslatici(gecikme: 0, omur: 1200, kaynak: kaynak, ayar: ayar);
            Assert.True(ilk.WaitForExit(60000), "yuva tutulurken başlatıcı çıkmadı");
            kurulum.HepsiniBekle(60000);
            _cikti.WriteLine($"yuva-tutulurken\t{File.ReadAllText(a)}\tisaret\t{UpdateCheck.ReadVersionMarker(kurulum.App)}");
            Assert.Equal("v1", File.ReadAllText(a));
            Assert.Null(UpdateCheck.ReadVersionMarker(kurulum.App));
        }

        using var ikinci = kurulum.Baslatici(gecikme: 0, omur: 1200, kaynak: kaynak, ayar: ayar);
        Assert.True(ikinci.WaitForExit(60000), "yuva boşken başlatıcı çıkmadı");
        kurulum.HepsiniBekle(60000);
        _cikti.WriteLine($"yuva-bosken\t{File.ReadAllText(a)}\tisaret\t{UpdateCheck.ReadVersionMarker(kurulum.App)}");
        Assert.Equal("v2", File.ReadAllText(a));
        Assert.Equal("9.9.9", UpdateCheck.ReadVersionMarker(kurulum.App));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("app", true)]
    [InlineData("baska", false)]
    public void OkunamayanSurecBizimSayilmaz(string? klasorAdi, bool beklenen)
    {
        var klasor = Path.Combine(Root, ".calisma", "yol-d", "app");
        var dosya = klasorAdi is null ? null : Path.Combine(Root, ".calisma", "yol-d", klasorAdi, "VidShrink.App.exe");
        Assert.Equal(beklenen, UygulamaKlasoruKapisi.KlasordenMi(dosya, klasor));
    }

    [Fact]
    public void YoluFirlatanSurecBizimSayilmaz()
    {
        var klasor = Path.Combine(Root, ".calisma", "yol-d", "app");
        var dosya = Path.Combine(klasor, "VidShrink.App.exe");
        var baska = Environment.ProcessId + 1;

        Assert.True(UygulamaKlasoruKapisi.Bizim(baska, () => dosya, klasor));
        Assert.False(UygulamaKlasoruKapisi.Bizim(Environment.ProcessId, () => dosya, klasor));
        foreach (var hata in new Exception[]
                 {
                     new InvalidOperationException("çıkmış süreç"),
                     new System.ComponentModel.Win32Exception(5),
                     new NotSupportedException()
                 })
        {
            Assert.False(UygulamaKlasoruKapisi.Bizim(baska, () => throw hata, klasor), hata.GetType().Name);
        }
    }

    private sealed class Arka
    {
        private readonly Thread _is;
        private volatile bool _bitti;

        private Arka(Func<bool> is_)
        {
            _is = new Thread(() =>
            {
                Sonuc = is_();
                _bitti = true;
            }) { IsBackground = true };
        }

        internal bool Sonuc { get; private set; }

        internal bool Bitti => _bitti;

        internal static Arka Baslat(Func<bool> is_)
        {
            var arka = new Arka(is_);
            arka._is.Start();
            return arka;
        }

        internal bool Bekle(int ms) => _is.Join(ms);

        internal bool Bekle(TimeSpan sure) => _is.Join(sure);
    }

    private sealed class KapiTutucu : IDisposable
    {
        private readonly ManualResetEventSlim _birak = new();
        private readonly Thread _is;

        private KapiTutucu(string app)
        {
            using var alindi = new ManualResetEventSlim();
            var tuttu = false;
            _is = new Thread(() =>
            {
                var kapi = UygulamaKlasoruKapisi.Al(app, TimeSpan.Zero);
                tuttu = kapi is not null;
                alindi.Set();
                _birak.Wait();
                UygulamaKlasoruKapisi.Birak(kapi);
            }) { IsBackground = true };
            _is.Start();
            Assert.True(alindi.Wait(5000) && tuttu, "test kapıyı tutamadı");
        }

        internal static KapiTutucu Baslat(string app) => new(app);

        internal void Birak()
        {
            _birak.Set();
            _is.Join(5000);
        }

        public void Dispose()
        {
            Birak();
            _birak.Dispose();
        }
    }

    private sealed class MutexTutucu : IDisposable
    {
        private readonly ManualResetEventSlim _birak = new();
        private readonly Thread _is;

        private MutexTutucu(string ad, TimeSpan bekleme)
        {
            using var alindi = new ManualResetEventSlim();
            var tuttu = false;
            _is = new Thread(() =>
            {
                using var mutex = new Mutex(initiallyOwned: false, ad);
                try { tuttu = mutex.WaitOne(bekleme); }
                catch (AbandonedMutexException) { tuttu = true; }
                alindi.Set();
                _birak.Wait();
                if (tuttu)
                {
                    try { mutex.ReleaseMutex(); }
                    catch (ApplicationException) { }
                }
            }) { IsBackground = true };
            _is.Start();
            Assert.True(alindi.Wait(bekleme + TimeSpan.FromSeconds(5)) && tuttu, $"test {ad} kilidini tutamadı");
        }

        internal static MutexTutucu Baslat(string ad) => new(ad, TimeSpan.Zero);

        internal static MutexTutucu Baslat(string ad, TimeSpan bekleme) => new(ad, bekleme);

        public void Dispose()
        {
            _birak.Set();
            _is.Join(5000);
            _birak.Dispose();
        }
    }

    private sealed class BekleyenKlasoru : IDisposable
    {
        internal string Kok { get; } = Path.Combine(Root, ".calisma", "yol-d", "bekleyen-" + Guid.NewGuid().ToString("N")[..8]);
        internal string App => Path.Combine(Kok, "app");
        internal string Kilit { get; } = @"Local\Teknesyum.VidShrink.Test." + Guid.NewGuid().ToString("N");

        internal BekleyenKlasoru()
        {
            Directory.CreateDirectory(App);
            File.WriteAllText(Path.Combine(App, "a.txt"), "v1");
        }

        internal StagedUpdate Sahne(string surum, bool bozuk)
        {
            var sahne = Path.Combine(Kok, UpdateStaging.StageDirectoryName);
            Directory.CreateDirectory(sahne);
            var icerik = Encoding.UTF8.GetBytes("v2");
            if (!bozuk) File.WriteAllBytes(Path.Combine(sahne, "a.txt"), icerik);
            var dosyalar = new[] { new ManifestFile("a.txt", Convert.ToHexString(SHA256.HashData(icerik)), icerik.Length) };
            var manifest = new ReleaseManifest(surum, "test", DateTimeOffset.UnixEpoch, UpdateCheck.Rid, dosyalar);
            return new StagedUpdate(manifest, sahne, dosyalar, Array.Empty<ManifestFile>(), Array.Empty<ManifestFile>());
        }

        public void Dispose()
        {
            if (Directory.Exists(Kok)) Directory.Delete(Kok, true);
        }
    }

    private static string? Isaret(string app)
    {
        var yol = Path.Combine(app, UygulamaKlasoruKapisi.HataIsareti);
        return File.Exists(yol) ? File.ReadAllText(yol) : null;
    }

    private static bool Bekle(Func<bool> kosul, int ms)
    {
        var saat = Stopwatch.StartNew();
        while (saat.ElapsedMilliseconds < ms)
        {
            if (kosul()) return true;
            Thread.Sleep(20);
        }
        return kosul();
    }

    private static List<string> GorunurPencereler(int pid)
    {
        var bulunan = new List<string>();
        EnumWindows((pencere, _) =>
        {
            GetWindowThreadProcessId(pencere, out var sahip);
            if (sahip == pid && IsWindowVisible(pencere))
            {
                var sinif = new StringBuilder(256);
                GetClassNameW(pencere, sinif, sinif.Capacity);
                bulunan.Add(sinif.ToString());
            }
            return true;
        }, IntPtr.Zero);
        return bulunan;
    }

    private delegate bool PencereGezici(IntPtr pencere, IntPtr deger);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(PencereGezici gezici, IntPtr deger);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr pencere, out int pid);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr pencere);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr pencere, StringBuilder sinif, int boyut);

    internal sealed record SahteOlay(long Zaman, int Pid, string Olay, string Deger);

    private sealed class SahteKurulum : IDisposable
    {
        private static readonly string[] Dosyalar = { "a.txt", "kilit.dll", "z.txt" };

        internal string Kok { get; }
        internal string App => Path.Combine(Kok, "app");
        private string Isaret => Path.Combine(Kok, "isaret");

        internal SahteKurulum()
        {
            Kok = Path.Combine(Root, ".calisma", "yol-d", "kurulum-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(Isaret);
            Bagla(Path.GetDirectoryName(LauncherPath!)!, Kok);
            Bagla(Path.GetDirectoryName(SahteUygulamaPath!)!, App);
            var ffmpeg = Path.Combine(Kok, "tools", "ffmpeg");
            Directory.CreateDirectory(ffmpeg);
            File.WriteAllText(Path.Combine(ffmpeg, "ffmpeg.exe"), "");
            File.WriteAllText(Path.Combine(ffmpeg, "ffprobe.exe"), "");
            foreach (var dosya in Dosyalar) File.WriteAllText(Path.Combine(App, dosya), "v1");
        }

        internal void YarimKopyaKur()
        {
            var sahne = Path.Combine(Kok, UpdateStaging.StageDirectoryName);
            Directory.CreateDirectory(sahne);
            var satirlar = new List<string>();
            foreach (var dosya in Dosyalar)
            {
                var yol = Path.Combine(sahne, dosya);
                File.WriteAllText(yol, "v2");
                var ozet = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(yol)));
                satirlar.Add($"{{\"path\":\"{dosya}\",\"sha256\":\"{ozet}\",\"size\":{new FileInfo(yol).Length}}}");
            }
            var gunluk = $"{{\"stage\":{System.Text.Json.JsonSerializer.Serialize(sahne)},\"files\":[{string.Join(",", satirlar)}]}}";
            File.WriteAllText(Path.Combine(App, UpdateStage.JournalName), gunluk);
        }

        internal void HepsiYeniSurum()
        {
            foreach (var dosya in Dosyalar) Assert.Equal("v2", File.ReadAllText(Path.Combine(App, dosya)));
            Assert.False(UpdateStage.HasPending(App), "yarım kopya günlüğü duruyor");
            Assert.False(Directory.Exists(Path.Combine(Kok, UpdateStaging.StageDirectoryName)));
            Assert.False(File.Exists(Path.Combine(App, UygulamaKlasoruKapisi.HataIsareti)));
        }

        /// <summary>
        /// Yerel sahte yayın: manifestin saydığı tek dosya <c>a.txt</c>, arşiv onun v2 halini
        /// taşıyor. <c>VIDSHRINK_UPDATE_SOURCE</c> bu klasörü gösterince gerçek başlatıcı
        /// gerçek indirme yolundan geçer, ağ olmadan.
        /// </summary>
        internal string SahteYayin(string surum)
        {
            var kaynak = Path.Combine(Kok, "yayin");
            var icerik = Path.Combine(Kok, "yayin-icerik");
            Directory.CreateDirectory(kaynak);
            Directory.CreateDirectory(icerik);
            var bayt = Encoding.UTF8.GetBytes("v2");
            File.WriteAllBytes(Path.Combine(icerik, "a.txt"), bayt);
            var zip = Path.Combine(kaynak, UpdateCheck.ArchiveAssetName(UpdateCheck.Rid));
            if (File.Exists(zip)) File.Delete(zip);
            System.IO.Compression.ZipFile.CreateFromDirectory(icerik, zip);
            var ozet = Convert.ToHexString(SHA256.HashData(bayt));
            File.WriteAllText(Path.Combine(kaynak, UpdateCheck.ManifestAssetName(UpdateCheck.Rid)),
                $"{{\"version\":\"{surum}\",\"commit\":\"test\",\"built\":\"2026-09-16T00:00:00Z\"," +
                $"\"rid\":\"{UpdateCheck.Rid}\",\"files\":[{{\"path\":\"a.txt\",\"sha256\":\"{ozet}\",\"size\":{bayt.Length}}}]}}");
            return kaynak;
        }

        internal string AyarDosyasi()
        {
            var yol = Path.Combine(Kok, "ayar.json");
            File.WriteAllText(yol, "{\"autoUpdate\":true}");
            return yol;
        }

        internal Process Baslatici(int gecikme, int omur, string? kaynak = null, string? ayar = null, string[]? args = null)
            => Baslat(Path.Combine(Kok, "VidShrink.exe"), gecikme, omur, baslaticidan: false, kaynak, ayar, args);

        internal Process UygulamaDogrudan(bool baslaticidan, int omur)
            => Baslat(Path.Combine(App, "VidShrink.App.exe"), 0, omur, baslaticidan);

        private Process Baslat(
            string dosya, int gecikme, int omur, bool baslaticidan,
            string? kaynak = null, string? ayar = null, string[]? args = null)
        {
            var start = new ProcessStartInfo { FileName = dosya, WorkingDirectory = Path.GetDirectoryName(dosya)!, UseShellExecute = false };
            start.Environment["VIDSHRINK_UPDATE_DISABLED"] = "1";
            start.Environment.Remove("VIDSHRINK_UPDATE_PROVA");
            if (kaynak is not null)
            {
                start.Environment.Remove("VIDSHRINK_UPDATE_DISABLED");
                start.Environment["VIDSHRINK_UPDATE_SOURCE"] = kaynak;
            }
            if (ayar is not null) start.Environment["VIDSHRINK_SETTINGS_PATH"] = ayar;
            foreach (var arguman in args ?? Array.Empty<string>()) start.ArgumentList.Add(arguman);
            start.Environment["VIDSHRINK_SAHTE_ISARET"] = Isaret;
            start.Environment["VIDSHRINK_SAHTE_OMUR_MS"] = omur.ToString(System.Globalization.CultureInfo.InvariantCulture);
            start.Environment[UygulamaKlasoruKapisi.GecikmeDegiskeni] = gecikme.ToString(System.Globalization.CultureInfo.InvariantCulture);
            start.Environment.Remove("VIDSHRINK_ACILIS_IZI");
            if (baslaticidan) start.Environment[LauncherUpdate.LaunchedVariable] = "1";
            else start.Environment.Remove(LauncherUpdate.LaunchedVariable);
            return Process.Start(start)!;
        }

        internal List<SahteOlay> Olaylar()
        {
            var olaylar = new List<SahteOlay>();
            foreach (var yol in Directory.EnumerateFiles(Isaret, "*.txt"))
            {
                var parca = Path.GetFileNameWithoutExtension(yol).Split('-', 3);
                string deger;
                try { deger = File.ReadAllText(yol); }
                catch (IOException) { continue; }
                olaylar.Add(new SahteOlay(long.Parse(parca[0]), int.Parse(parca[1]), parca[2], deger));
            }
            return olaylar.OrderBy(o => o.Zaman).ToList();
        }

        internal void Dokum(ITestOutputHelper cikti)
        {
            foreach (var olay in Olaylar()) cikti.WriteLine($"{olay.Zaman}\t{olay.Pid}\t{olay.Olay}\t{olay.Deger}");
            foreach (var dosya in Dosyalar) cikti.WriteLine($"{dosya}\t{File.ReadAllText(Path.Combine(App, dosya))}");
        }

        internal void HepsiniBekle(int ms)
            => Assert.True(Bekle(() => Surecler().Count == 0, ms), "sahte kurulumdan koşan süreç kaldı");

        private List<Process> Surecler()
        {
            var bulunan = new List<Process>();
            foreach (var ad in new[] { "VidShrink", "VidShrink.App" })
            {
                foreach (var surec in Process.GetProcessesByName(ad))
                {
                    string? yol = null;
                    try { yol = surec.MainModule?.FileName; }
                    catch (Exception) { }
                    if (yol is not null && yol.StartsWith(Kok, StringComparison.OrdinalIgnoreCase)) bulunan.Add(surec);
                    else surec.Dispose();
                }
            }
            return bulunan;
        }

        public void Dispose()
        {
            foreach (var surec in Surecler())
            {
                using (surec)
                {
                    try { surec.Kill(); surec.WaitForExit(5000); }
                    catch (Exception) { }
                }
            }
            for (var deneme = 0; deneme < 20; deneme++)
            {
                try
                {
                    if (Directory.Exists(Kok)) Directory.Delete(Kok, true);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Thread.Sleep(100);
                }
            }
        }

        private static void Bagla(string kaynak, string hedef)
        {
            foreach (var dosya in Directory.EnumerateFiles(kaynak, "*", SearchOption.AllDirectories))
            {
                var goreli = Path.GetRelativePath(kaynak, dosya);
                var yeni = Path.Combine(hedef, goreli);
                Directory.CreateDirectory(Path.GetDirectoryName(yeni)!);
                if (!CreateHardLinkW(yeni, dosya, IntPtr.Zero)) File.Copy(dosya, yeni);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLinkW(string yeni, string var, IntPtr guvenlik);
    }
}
