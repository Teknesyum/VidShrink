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
        Assert.False(Directory.Exists(Path.Combine(Root, "tools", "VidShrink.SplashGen")));
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

        var saat = Stopwatch.StartNew();
        var elle = Arka.Baslat(
            () => KurulumBekleyeni.Calistir(kurulum.Kok, kurulum.App, true, kurulum.Kilit, () => sahne, false));
        var sinir = KurulumBekleyeni.ElleBekleme + TimeSpan.FromSeconds(5);
        var bitti = elle.Bekle(sinir);
        _cikti.WriteLine($"elle-bekleme-ms\t{saat.ElapsedMilliseconds}\tbitti\t{bitti}");

        kapi.Birak();
        Assert.True(elle.Bekle(20000));
        Assert.True(bitti, $"elle Yükle {sinir.TotalSeconds} sn içinde bırakmadı");
        Assert.True(sinir < TimeSpan.FromMinutes(1));
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

        internal Process Baslatici(int gecikme, int omur)
            => Baslat(Path.Combine(Kok, "VidShrink.exe"), gecikme, omur, baslaticidan: false);

        internal Process UygulamaDogrudan(bool baslaticidan, int omur)
            => Baslat(Path.Combine(App, "VidShrink.App.exe"), 0, omur, baslaticidan);

        private Process Baslat(string dosya, int gecikme, int omur, bool baslaticidan)
        {
            var start = new ProcessStartInfo { FileName = dosya, WorkingDirectory = Path.GetDirectoryName(dosya)!, UseShellExecute = false };
            start.Environment["VIDSHRINK_UPDATE_DISABLED"] = "1";
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
