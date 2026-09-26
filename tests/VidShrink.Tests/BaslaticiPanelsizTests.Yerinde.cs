using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Launcher;

namespace VidShrink.Tests;

/// <summary>
/// Yükle'nin hızlı yolu (<see cref="YerindeGuncelleme"/>, <see cref="InPlaceUpdate"/>): değişen
/// dosya <c>.old</c> olur, yenisi adını alır, yeni sürüm açılır. Eski yolun eşi olan ölçü
/// (<see cref="ElleYukleArkaPlanIndirirkenAcilisiGeciktirmez"/>) burada yeni yoldan ölçülür;
/// kilitli dosya geri almayı tetikler ve eski yol aynı sahneyle yine kurar.
/// </summary>
public sealed partial class BaslaticiPanelsizTests
{
    private const int OlcumTekrari = 10;

    [SahteKurulumFact]
    public void YerindeYukleUygulamayiBirSaniyeninAltindaAcar()
    {
        var sureler = new List<long>();
        for (var tur = 0; tur < OlcumTekrari; tur++)
        {
            using var kurulum = new SahteKurulum();
            var kaynak = kurulum.SahteYayin("9.9.9", hepsi: true);
            var sahne = Sahnele(kurulum, kaynak);
            Assert.True(File.Exists(Path.Combine(sahne.Stage, StageSeal.FileName)), "indirme sahne mührünü yazmadı");

            var saat = Stopwatch.StartNew();
            Assert.True(YerindeGuncelleme.Uygula(kurulum.Kok, kurulum.App, sahne, TestKilidi()), "hızlı yol uygulanmadı");
            using (YerindeGuncelleme.YeniSurumuAc(kurulum.Kok, kurulum.App, kurulum.SahteOrtam(300))) { }
            var dogdu = Bekle(() => kurulum.Olaylar().Any(o => o.Olay == "acildi"), 15000);
            var ms = saat.ElapsedMilliseconds;
            sureler.Add(ms);
            _cikti.WriteLine($"yerinde-yukle-app-dogumu-ms\t{tur + 1}\t{ms}\tdogdu\t{dogdu}");
            Assert.True(dogdu, "hızlı yolda yeni sürüm açılmadı");

            kurulum.HepsiniBekle(30000);
            var olay = kurulum.Olaylar().First(o => o.Olay == "acildi");
            Assert.Equal("1", olay.Deger);
            Assert.Equal("v2", kurulum.Olaylar().First(o => o.Olay == "kilit").Deger);
            kurulum.HepsiYeniSurum();
            Assert.Equal("9.9.9", UpdateCheck.ReadVersionMarker(kurulum.App));
            Assert.False(InPlaceUpdate.HasPending(kurulum.App));
            Assert.Equal(3, InPlaceUpdate.SweepRetired(kurulum.App));
            Assert.Empty(Directory.EnumerateFiles(kurulum.App, "*" + InPlaceUpdate.RetiredSuffix, SearchOption.AllDirectories));
        }

        var ortanca = Ortanca(sureler);
        _cikti.WriteLine($"yerinde-yukle-ortanca-ms\t{ortanca}\tn\t{sureler.Count}\taralik\t{sureler.Min()}-{sureler.Max()}");
        Assert.True(ortanca < 1000, $"hızlı yolun ortancası {ortanca} ms");
    }

    [SahteKurulumFact]
    public void EskiYolElleYukleOlcumu()
    {
        var sureler = new List<long>();
        for (var tur = 0; tur < OlcumTekrari; tur++)
        {
            using var kurulum = new SahteKurulum();
            var kaynak = kurulum.SahteYayin("9.9.9", hepsi: true);
            Sahnele(kurulum, kaynak);

            var saat = Stopwatch.StartNew();
            using var baslatici = kurulum.Baslatici(
                gecikme: 0, omur: 300, kaynak: kaynak, args: new[] { LauncherUpdate.UpdateNowArgument, "999999" });
            var dogdu = Bekle(() => kurulum.Olaylar().Any(o => o.Olay == "acildi"), 25000);
            var ms = saat.ElapsedMilliseconds;
            sureler.Add(ms);
            _cikti.WriteLine($"eski-yol-app-dogumu-ms\t{tur + 1}\t{ms}\tdogdu\t{dogdu}");
            Assert.True(dogdu, "eski yolda uygulama açılmadı");
            Assert.True(baslatici.WaitForExit(60000), "başlatıcı çıkmadı");
            kurulum.HepsiniBekle(30000);
            kurulum.HepsiYeniSurum();
        }

        _cikti.WriteLine($"eski-yol-ortanca-ms\t{Ortanca(sureler)}\tn\t{sureler.Count}\taralik\t{sureler.Min()}-{sureler.Max()}");
    }

    [SahteKurulumFact]
    public void KilitliDosyaGeriAlinirEskiYolYineKurar()
    {
        using var kurulum = new SahteKurulum();
        var kaynak = kurulum.SahteYayin("9.9.9", hepsi: true);
        var sahne = Sahnele(kurulum, kaynak);

        using (new FileStream(Path.Combine(kurulum.App, "z.txt"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(YerindeGuncelleme.Uygula(kurulum.Kok, kurulum.App, sahne, TestKilidi()), "kilitli dosyaya rağmen uygulandı");
        }

        foreach (var dosya in kurulum.Dosyalari()) Assert.Equal("v1", File.ReadAllText(dosya));
        Assert.Empty(Directory.EnumerateFiles(kurulum.App, "*" + InPlaceUpdate.RetiredSuffix, SearchOption.AllDirectories));
        Assert.False(InPlaceUpdate.HasPending(kurulum.App), "geri alınan yer değiştirmenin günlüğü kaldı");
        Assert.Null(UpdateStage.FindMismatch(sahne.Stage, sahne.App));
        Assert.Null(UpdateCheck.ReadVersionMarker(kurulum.App));

        using var baslatici = kurulum.Baslatici(
            gecikme: 0, omur: 300, kaynak: kaynak, args: new[] { LauncherUpdate.UpdateNowArgument, "999999" });
        Assert.True(baslatici.WaitForExit(60000), "eski yolda başlatıcı çıkmadı");
        kurulum.HepsiniBekle(30000);
        kurulum.Dokum(_cikti);
        kurulum.HepsiYeniSurum();
        Assert.Equal("9.9.9", UpdateCheck.ReadVersionMarker(kurulum.App));
    }

    [Fact]
    public void KilitliDosyadaYerDegistirmeGeriAlinir()
    {
        using var klasor = new YerindeKlasoru();
        var sahne = klasor.Sahne("9.9.9", "a.txt", "b.dll", "c.txt");

        using (new FileStream(Path.Combine(klasor.App, "b.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsAny<IOException>(() => InPlaceUpdate.Swap(sahne.Stage, klasor.App, sahne.App));
        }

        _cikti.WriteLine($"geri-alma\t{string.Join(",", klasor.Icerik())}");
        Assert.Equal(new[] { "a.txt=v1", "b.dll=v1" }, klasor.Icerik());
        Assert.False(InPlaceUpdate.HasPending(klasor.App));
        Assert.Null(UpdateStage.FindMismatch(sahne.Stage, sahne.App));

        InPlaceUpdate.Swap(sahne.Stage, klasor.App, sahne.App);
        Assert.Equal(new[] { "a.txt=v2", "b.dll=v2", "c.txt=v2" }, klasor.Icerik());
        Assert.Equal(2, InPlaceUpdate.SweepRetired(klasor.App));
    }

    [Fact]
    public void KapiYaDaKilitTutulurkenHizliYolDenenmezYuvaEngellemez()
    {
        using var klasor = new YerindeKlasoru();
        var sahne = klasor.Sahne("9.9.9", "a.txt");
        var kilit = TestKilidi();

        using (MutexTutucu.Baslat(UygulamaKlasoruKapisi.Ad(klasor.App)))
            Assert.False(YerindeGuncelleme.Uygula(klasor.Kok, klasor.App, sahne, kilit));
        using (MutexTutucu.Baslat(kilit))
            Assert.False(YerindeGuncelleme.Uygula(klasor.Kok, klasor.App, sahne, kilit));
        Assert.Equal(new[] { "a.txt=v1", "b.dll=v1" }, klasor.Icerik());

        using (MutexTutucu.Baslat(KurulumBekleyeni.Ad(klasor.App)))
            Assert.True(YerindeGuncelleme.Uygula(klasor.Kok, klasor.App, sahne, kilit));
        Assert.Equal(new[] { "a.txt=v2", "b.dll=v1" }, klasor.Icerik());
        Assert.Equal("9.9.9", UpdateCheck.ReadVersionMarker(klasor.App));
        Assert.Equal("9.9.9", File.ReadAllText(Path.Combine(klasor.App, AppliedUpdateNotice.MarkerFileName)));
        Assert.False(Directory.Exists(sahne.Stage));
    }

    /// <summary>
    /// Önceki sürümün başlatıcısıyla geçiş anı: o başlatıcı uygulamayı açmış, arka planda
    /// sahneyi indirmiş ve kurmak için uygulamanın kapanmasını bekliyor; bekleyen yuvası onun
    /// elinde. Bu sürümün başlatıcısı o turu koşmuyor; bekleyen süreç içinde önceki sürümün
    /// çağrısıyla (<see cref="KurulumBekleyeni.Calistir"/>) canlandırılır. Sahte uygulama bu
    /// anda "Yükle"ye basar. Süre basıştan yeni sürümün açılışına kadar. Sonra bekleyen kapıyı
    /// alır, kurulu sürümü görür ve dokunmadan çekilir: dosyalar ve yazılma anları takastan
    /// sonraki gibi kalır, hata işareti yok.
    /// </summary>
    [SahteKurulumFact]
    public void ArkaPlanBaslaticisiBeklerkenYukleHizliYoldanAcar()
    {
        var sureler = new List<long>();
        var kollar = new List<string>();
        for (var tur = 0; tur < OlcumTekrari; tur++)
        {
            using var kurulum = new SahteKurulum();
            var (kol, ms) = ArkaPlanBeklerkenYukle(kurulum, kurulum.SahteYayin("9.9.9", hepsi: true));
            sureler.Add(ms);
            kollar.Add(kol);
            _cikti.WriteLine($"arka-plan-bekler-yukle-ms\t{tur + 1}\t{ms}\tkol\t{kol}");
        }

        var ortanca = Ortanca(sureler);
        _cikti.WriteLine($"arka-plan-bekler-yukle-ortanca-ms\t{ortanca}\tn\t{sureler.Count}\taralik\t{sureler.Min()}-{sureler.Max()}");
        Assert.All(kollar, kol => Assert.Equal("hizli", kol));
        Assert.True(ortanca < 1000, $"arka plan başlatıcısı beklerken hızlı yolun ortancası {ortanca} ms");
    }

    /// <summary>
    /// Sahnede başlatıcının kendi dosyası da var ve başlatıcı tam o dosyadan hâlâ koşuyor
    /// (bakım gecikmesi onu açık tutar). Hızlı yol koşan ikilinin üstüne yazamaz; yeni
    /// başlatıcı yan adda bekler, geçişi yapan süreç başlatıcı çıkınca adı devralır.
    /// </summary>
    [SahteKurulumFact]
    public void KosanBaslaticininKendiDosyasiSahnedeykenGecisOnunCikisindaTamamlanir()
    {
        using var kurulum = new SahteKurulum();
        var hedef = Path.Combine(kurulum.Kok, LauncherUpdate.ExecutableName);
        var yeni = File.ReadAllBytes(hedef).Concat(new byte[] { 0 }).ToArray();
        var (kol, ms) = ArkaPlanBeklerkenYukle(kurulum, kurulum.SahteYayin("9.9.9", hepsi: true, baslatici: yeni), gecikme: 12000);
        _cikti.WriteLine($"baslatici-sahnede-yukle-ms\t{ms}\tkol\t{kol}");
        Assert.Equal("hizli", kol);

        var ozet = Convert.ToHexString(SHA256.HashData(yeni));
        Assert.True(Bekle(() => !File.Exists(Path.Combine(kurulum.Kok, LauncherUpdate.JournalName))
            && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(hedef))) == ozet, 40000), "başlatıcı geçişi tamamlanmadı");
        Assert.True(Bekle(() => Process.GetProcessesByName("VidShrink.new").Length == 0, 10000), "geçişi yapan süreç çıkmadı");
        Assert.Equal("9.9.9", LauncherUpdate.ReadVersionMarker(kurulum.Kok));
        Assert.False(File.Exists(LauncherUpdate.Incoming(kurulum.Kok, LauncherUpdate.ExecutableName)));
    }

    private (string Kol, long Ms) ArkaPlanBeklerkenYukle(SahteKurulum kurulum, string kaynak, int gecikme = 0)
    {
        using var baslatici = kurulum.Baslatici(gecikme: gecikme, omur: 30000, kaynak: kaynak, ayar: kurulum.AyarDosyasi(),
            ek: new Dictionary<string, string> { ["VIDSHRINK_SAHTE_YUKLE"] = "1", ["VIDSHRINK_SAHTE_YENI_OMUR_MS"] = "1500" });
        Assert.True(Bekle(() => kurulum.Olaylar().Any(o => o.Olay == "acildi"), 25000), "başlatıcı uygulamayı açmadı");
        var bekleyen = Task.Factory.StartNew(
            () => KurulumBekleyeni.Calistir(kurulum.Kok, kurulum.App, false, UpdateStaging.MutexName,
                () => UpdateStaging.StageAsync(kurulum.Kok, kurulum.App, kaynak, UpdateStaging.LauncherLanes, null,
                    "VidShrink-OncekiBaslatici", null, CancellationToken.None).GetAwaiter().GetResult(), false),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Assert.True(Bekle(() => kurulum.Olaylar().Any(o => o.Olay == "yukle"), 40000), "sahte uygulama Yükle'ye basmadı");
        var olaylar = kurulum.Olaylar();
        var basis = olaylar.Single(o => o.Olay == "yukle-basladi");
        var kol = olaylar.Single(o => o.Olay == "yukle").Deger;
        Assert.Equal("yuva-dolu", basis.Deger);

        SahteOlay? acilis = null;
        Assert.True(Bekle(() => (acilis = kurulum.Olaylar().FirstOrDefault(o => o.Olay == "acildi" && o.Pid != basis.Pid)) is not null, 25000),
            "yeni sürüm açılmadı");
        var ms = (acilis!.Zaman - basis.Zaman) / TimeSpan.TicksPerMillisecond;
        var anlar = kol == "hizli" ? kurulum.Dosyalari().ToDictionary(y => y, File.GetLastWriteTimeUtc) : null;

        Assert.True(bekleyen.Wait(60000), "önceki sürümün bekleyeni çekilmedi");
        Assert.True(baslatici.WaitForExit(60000), "başlatıcı çıkmadı");
        kurulum.HepsiniBekle(30000);
        kurulum.Dokum(_cikti);
        kurulum.HepsiYeniSurum();
        Assert.Equal("9.9.9", UpdateCheck.ReadVersionMarker(kurulum.App));
        if (anlar is not null)
        {
            foreach (var (yol, an) in anlar) Assert.Equal(an, File.GetLastWriteTimeUtc(yol));
            Assert.False(InPlaceUpdate.HasPending(kurulum.App));
            Assert.Equal(3, InPlaceUpdate.SweepRetired(kurulum.App));
        }
        return (kol, ms);
    }

    /// <summary>
    /// Kapanışta kurmanın kapanışa eklediği süre, en kötü kol: uygulamanın Release çıktısındaki her dosya
    /// (2026-09-26, `bin/Release/net8.0` 424 dosya) değişmiş sayılır. Yeniden adlandırma boyuttan bağımsız, dosyalar küçük.
    /// Süre <c>Uygula</c>'nın tamamı: kapı, kilit, koşan süreç yoklaması, takas, işaret.
    /// </summary>
    [Fact]
    public void KapanistaTakasGercekDosyaSayisindaOlculur()
    {
        const int DosyaSayisi = 424;
        var sureler = new List<long>();
        for (var tur = 0; tur < OlcumTekrari; tur++)
        {
            using var klasor = new YerindeKlasoru();
            var adlar = Enumerable.Range(0, DosyaSayisi).Select(i => $"d{i:D3}.dll").ToArray();
            foreach (var ad in adlar) File.WriteAllText(Path.Combine(klasor.App, ad), "v1");
            var sahne = klasor.Sahne("9.9.9", adlar);

            var saat = Stopwatch.StartNew();
            var oturdu = YerindeGuncelleme.Uygula(klasor.Kok, klasor.App, sahne, "Local\\Teknesyum.VidShrink.Test." + Guid.NewGuid().ToString("N"));
            saat.Stop();
            Assert.True(oturdu, "takas oturmadı");
            Assert.Equal("9.9.9", UpdateCheck.ReadVersionMarker(klasor.App));
            sureler.Add(saat.ElapsedMilliseconds);
            _cikti.WriteLine($"kapanista-takas-ms\t{tur + 1}\t{saat.ElapsedMilliseconds}\tdosya\t{DosyaSayisi}");
        }

        var ortanca = Ortanca(sureler);
        _cikti.WriteLine($"kapanista-takas-ortanca-ms\t{ortanca}\tn\t{sureler.Count}\taralik\t{sureler.Min()}-{sureler.Max()}");
        Assert.True(ortanca < 3000, $"kapanışta takasın ortancası {ortanca} ms");
    }

    [Fact]
    public void YarimKalanYerDegistirmeGeriAlinir()
    {
        using var klasor = new YerindeKlasoru();
        var sahne = klasor.Sahne("9.9.9", "a.txt", "b.dll", "c.txt");
        var adim = (string ad, bool vardi) => new InPlaceUpdate.Step(
            Path.Combine(klasor.App, ad), Path.Combine(klasor.App, ad + InPlaceUpdate.RetiredSuffix), Path.Combine(sahne.Stage, ad), vardi);
        var adimlar = new[] { adim("a.txt", true), adim("c.txt", false), adim("b.dll", true) };

        File.Move(adimlar[0].Target, adimlar[0].Retired);
        File.Move(adimlar[0].Staged, adimlar[0].Target);
        File.Move(adimlar[1].Staged, adimlar[1].Target);
        File.WriteAllText(Path.Combine(klasor.App, InPlaceUpdate.JournalName), System.Text.Json.JsonSerializer.Serialize(new
        {
            stage = sahne.Stage,
            steps = adimlar.Select(a => new { target = a.Target, retired = a.Retired, staged = a.Staged, existed = a.Existed })
        }));

        Assert.Equal(0, InPlaceUpdate.SweepRetired(klasor.App));
        Assert.True(File.Exists(adimlar[0].Retired), "günlük dururken .old silindi");

        Assert.True(InPlaceUpdate.Recover(klasor.App));
        _cikti.WriteLine($"kurtarma\t{string.Join(",", klasor.Icerik())}");
        Assert.Equal(new[] { "a.txt=v1", "b.dll=v1" }, klasor.Icerik());
        Assert.False(InPlaceUpdate.HasPending(klasor.App));
        Assert.Null(UpdateStage.FindMismatch(sahne.Stage, sahne.App));
    }

    [Fact]
    public void EmekliDosyalarSilinir()
    {
        using var klasor = new YerindeKlasoru();
        Directory.CreateDirectory(Path.Combine(klasor.App, "alt"));
        File.WriteAllText(Path.Combine(klasor.App, "x.dll" + InPlaceUpdate.RetiredSuffix), "eski");
        File.WriteAllText(Path.Combine(klasor.App, "alt", "y.exe" + InPlaceUpdate.RetiredSuffix), "eski");

        Assert.Equal(2, InPlaceUpdate.SweepRetired(klasor.App));
        Assert.Equal(new[] { "a.txt=v1", "b.dll=v1" }, klasor.Icerik());
        Assert.Empty(Directory.EnumerateFiles(klasor.App, "*" + InPlaceUpdate.RetiredSuffix, SearchOption.AllDirectories));
    }

    [Fact]
    public void SahneMuhruDegismeyenDosyayiYenidenOzetlemez()
    {
        using var klasor = new YerindeKlasoru();
        var sahne = klasor.Sahne("9.9.9", "a.txt");
        var yol = Path.Combine(sahne.Stage, "a.txt");
        var muhur = StageSeal.Load(sahne.Stage);
        muhur.Record(yol, sahne.App[0].Sha256);
        muhur.Save();
        Assert.Null(UpdateStage.FindMismatch(sahne.Stage, sahne.App));

        var zaman = File.GetLastWriteTimeUtc(yol);
        File.WriteAllText(yol, "vX");
        File.SetLastWriteTimeUtc(yol, zaman);
        Assert.Null(UpdateStage.FindMismatch(sahne.Stage, sahne.App));

        File.SetLastWriteTimeUtc(yol, zaman.AddSeconds(2));
        Assert.NotNull(UpdateStage.FindMismatch(sahne.Stage, sahne.App));

        File.SetLastWriteTimeUtc(yol, zaman);
        File.Delete(Path.Combine(sahne.Stage, StageSeal.FileName));
        Assert.NotNull(UpdateStage.FindMismatch(sahne.Stage, sahne.App));
    }

    private static StagedUpdate Sahnele(SahteKurulum kurulum, string kaynak)
    {
        var sahne = UpdateStaging.StageAsync(
            kurulum.Kok, kurulum.App, kaynak, UpdateStaging.LauncherLanes, null, "test", null, CancellationToken.None)
            .GetAwaiter().GetResult();
        Assert.NotNull(sahne);
        Assert.Equal(3, sahne.App.Count);
        return sahne;
    }

    private static string TestKilidi() => @"Local\Teknesyum.VidShrink.Test." + Guid.NewGuid().ToString("N");

    private static long Ortanca(List<long> sureler)
    {
        var sirali = sureler.OrderBy(s => s).ToList();
        var orta = sirali.Count / 2;
        return sirali.Count % 2 == 1 ? sirali[orta] : (sirali[orta - 1] + sirali[orta]) / 2;
    }

    private sealed class YerindeKlasoru : IDisposable
    {
        internal string Kok { get; } = Path.Combine(Root, ".calisma", "yol-d", "yerinde-" + Guid.NewGuid().ToString("N")[..8]);
        internal string App => Path.Combine(Kok, "app");

        internal YerindeKlasoru()
        {
            Directory.CreateDirectory(App);
            File.WriteAllText(Path.Combine(App, "a.txt"), "v1");
            File.WriteAllText(Path.Combine(App, "b.dll"), "v1");
            UpdateCheck.WriteVersionMarker(App, "9.9.8");
        }

        internal StagedUpdate Sahne(string surum, params string[] adlar)
        {
            var sahne = Path.Combine(Kok, UpdateStaging.StageDirectoryName);
            Directory.CreateDirectory(sahne);
            var icerik = Encoding.UTF8.GetBytes("v2");
            var ozet = Convert.ToHexString(SHA256.HashData(icerik));
            var dosyalar = new List<ManifestFile>();
            foreach (var ad in adlar)
            {
                File.WriteAllBytes(Path.Combine(sahne, ad), icerik);
                dosyalar.Add(new ManifestFile(ad, ozet, icerik.Length));
            }
            var manifest = new ReleaseManifest(surum, "test", DateTimeOffset.UnixEpoch, UpdateCheck.Rid, dosyalar);
            return new StagedUpdate(manifest, sahne, dosyalar, Array.Empty<ManifestFile>(), Array.Empty<ManifestFile>());
        }

        internal string[] Icerik() => Directory.EnumerateFiles(App)
            .Select(Path.GetFileName)
            .Where(ad => !ad!.StartsWith('.') && !ad.EndsWith(InPlaceUpdate.RetiredSuffix, StringComparison.Ordinal))
            .OrderBy(ad => ad, StringComparer.Ordinal)
            .Select(ad => $"{ad}={File.ReadAllText(Path.Combine(App, ad!))}")
            .ToArray();

        public void Dispose()
        {
            if (Directory.Exists(Kok)) Directory.Delete(Kok, true);
        }
    }
}
