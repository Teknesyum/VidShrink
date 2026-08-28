using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// Basarim denetcisinin olculeri. VidShrink kayit yapmaz; burada olculen sey kayit
/// araci degil, bu makinede kodlamanin nereye dustugu ve maliyeti. Saf karar olculeri
/// <c>[Fact]</c>, gercek kodlama kosturanlar <c>[FfmpegFact]</c>: CI makinesinde ne
/// ffmpeg ne de donanim kodlayicisi var.
/// </summary>
public sealed class PerformanceCheckTests
{
    private static readonly string MeasurementLog =
        Path.Combine(TipSources.Root, ".calisma", "t63", "olcum.txt");

    private static void Log(string line)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MeasurementLog)!);
        lock (MeasurementLog) File.AppendAllText(MeasurementLog, line + Environment.NewLine);
    }

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Kodlayici listesini istedigimiz gibi kuran sahte. Yokluk yolu bununla olculur.</summary>
    private sealed class FakeAvailability : IEncoderAvailability
    {
        private readonly HashSet<string> _encoders;

        public FakeAvailability(params string[] encoders)
            => _encoders = new HashSet<string>(encoders, StringComparer.OrdinalIgnoreCase);

        public bool HasEncoder(string name) => _encoders.Contains(name);
        public bool WorksAsEncoder(string codec) => _encoders.Contains(codec);
    }

    private const double VideoMs = 2000;

    /// <summary>
    /// Gercek zaman cekirdegi duvar saatinden turuyor: tek is parcacikli gecis
    /// <paramref name="cores"/> kadar cekirdek istiyorsa, klibi islemek klibin
    /// suresinin o kati kadar surer.
    /// </summary>
    private static EncoderCost Cost(string codec, double cores, double cpuMs = 100)
        => new(codec, true, cpuMs, (long)(cores * VideoMs), VideoMs);

    /// <summary>Saglam sayacli, butcesi asilmamis bir kosum — gurultusuz taban.</summary>
    private static PerformanceCheckResult Degerlendir(
        EncoderCost? yazilim,
        EncoderCost? donanimTek = null,
        EncoderCost? donanimSerbest = null,
        int cekirdek = 8,
        bool donanimVar = false,
        long gecen = 4000,
        long butce = 30_000,
        double sayacKatsayisi = 1)
        => PerformanceCheck.Evaluate(yazilim, donanimTek, donanimSerbest, cekirdek, gecen, butce,
            donanimVar, sayacKatsayisi);

    // --- K5: her cumle bir sayiya bagli, karar veriden turuyor ---

    [Fact]
    public void OlcumYoksaKararYok()
    {
        var result = Degerlendir(null);

        Assert.Equal(RecordingImpact.Unknown, result.Impact);
        Assert.Contains(result.Findings, f => f.Code == PerformanceFindingCode.NotMeasured);
    }

    /// <summary>
    /// Her bulgu kodu ya sayisiz sayilir ya da tasimasi gereken sayi adiyla
    /// listelenir; listelenmemis bir kod eklenirse bu olcu kirmizi doner. Kodlarin
    /// hepsinin gercekten uretildigi de olculuyor, yani kapsam tam.
    /// </summary>
    [Fact]
    public void HerBulguTasimasiGerekenSayiyiTasiyor()
    {
        var sayisiz = new[]
        {
            PerformanceFindingCode.NotMeasured,
            PerformanceFindingCode.NoHardwareEncoder,
            PerformanceFindingCode.HardwareEncoderFailed,
            PerformanceFindingCode.HardwarePathWorks,
            PerformanceFindingCode.HardwareCpuCostNotMeasured
        };

        static double Buyukluk(PerformanceFinding f) => f.Code switch
        {
            PerformanceFindingCode.HardwareNotCpuBound => f.Factor,
            PerformanceFindingCode.HardwarePipelineHeadroom => f.RealtimeFactor,
            PerformanceFindingCode.SoftwareRealtimeCost => f.RealtimeCores,
            PerformanceFindingCode.SoftwareCostsCores => f.RealtimeCores,
            PerformanceFindingCode.SoftwareCostIsSmall => f.RealtimeCores,
            PerformanceFindingCode.CpuAccountingUnreliable => 1,
            PerformanceFindingCode.BudgetExhausted => f.BudgetMs,
            _ => throw new Xunit.Sdk.XunitException($"{f.Code} ne sayisiz listesinde ne de sayi listesinde")
        };

        var senaryolar = new[]
        {
            Degerlendir(null),
            Degerlendir(Cost("libx264", 0.4)),
            Degerlendir(Cost("libx264", 2.0)),
            Degerlendir(Cost("libx264", 0.4), Cost("h264_nvenc", 0.14), Cost("h264_nvenc", 0.13), donanimVar: true),
            Degerlendir(Cost("libx264", 0.4), Cost("h264_nvenc", 0.9), Cost("h264_nvenc", 0.2), donanimVar: true),
            Degerlendir(Cost("libx264", 0.4), new EncoderCost("h264_qsv", false, 0, 0, VideoMs), donanimVar: true),
            Degerlendir(Cost("libx264", 0.4), gecen: 40_000),
            Degerlendir(Cost("libx264", 0.4), sayacKatsayisi: 5.8)
        };

        var gorulen = new HashSet<PerformanceFindingCode>();
        foreach (var senaryo in senaryolar)
            foreach (var bulgu in senaryo.Findings)
            {
                gorulen.Add(bulgu.Code);
                if (sayisiz.Contains(bulgu.Code)) continue;
                Assert.True(Buyukluk(bulgu) > 0, $"{bulgu.Code} sayisiz kaldi");
            }

        var eksik = Enum.GetValues<PerformanceFindingCode>().Except(gorulen).ToArray();
        Assert.True(eksik.Length == 0, "hic uretilmeyen bulgu kodu: " + string.Join(", ", eksik));
    }

    // --- K2: donanim yoksa arac "sorun yok" demiyor ---

    [Fact]
    public void DonanimYokVeYazilimBirCekirdekIstiyorsaAgirYukDeniyor()
    {
        var result = Degerlendir(Cost("libx264", 1.4));

        Assert.Equal(RecordingImpact.SoftwareHeavyLoad, result.Impact);
        Assert.Contains(result.Findings, f => f.Code == PerformanceFindingCode.NoHardwareEncoder);
        Assert.Contains(result.Findings, f => f.Code == PerformanceFindingCode.SoftwareCostsCores);
        Assert.DoesNotContain(result.Findings, f => f.Code == PerformanceFindingCode.HardwarePathWorks);
        Assert.Equal(1.4, result.SoftwareRealtimeCores, 3);
    }

    [Fact]
    public void DonanimYokAmaYazilimUcuzsaSuclanmiyor()
    {
        var result = Degerlendir(Cost("libx264", 0.4));

        Assert.Equal(RecordingImpact.SoftwareLightLoad, result.Impact);
        Assert.Contains(result.Findings, f => f.Code == PerformanceFindingCode.SoftwareCostIsSmall);
    }

    /// <summary>
    /// Esik mutlak, cekirdek sayisinin orani degil: ayni maliyet 4 cekirdekli ve
    /// 64 cekirdekli makinede ayni karari vermeli. Oransal esikte 64 cekirdekte
    /// esik 16'ya cikiyor ve bir cekirdegi tam yiyen kodlayici "hafif" sayiliyordu.
    /// </summary>
    [Fact]
    public void EsikCekirdekSayisiylaGevsemiyor()
    {
        var maliyet = Cost("libx264", 1.2);

        Assert.Equal(RecordingImpact.SoftwareHeavyLoad, Degerlendir(maliyet, cekirdek: 4).Impact);
        Assert.Equal(RecordingImpact.SoftwareHeavyLoad, Degerlendir(maliyet, cekirdek: 64).Impact);
    }

    [Fact]
    public void KodlayiciListedeVarAmaKodlamiyorsaYoklukTanBaskaSoyleniyor()
    {
        var result = Degerlendir(
            Cost("libx264", 1.4),
            new EncoderCost("h264_qsv", false, 0, 0, VideoMs),
            donanimVar: true);

        Assert.Contains(result.Findings, f => f.Code == PerformanceFindingCode.HardwareEncoderFailed);
        Assert.DoesNotContain(result.Findings, f => f.Code == PerformanceFindingCode.NoHardwareEncoder);
        Assert.Equal(RecordingImpact.SoftwareHeavyLoad, result.Impact);
    }

    // --- D4: donanim yolunun maliyeti olculdugu kadariyla soyleniyor ---

    /// <summary>
    /// Donanim gecisi is parcacigi sayisindan etkilenmiyorsa is islemcide degildir;
    /// karar HardwareOffload olur ama islemci maliyeti icin sayi <b>uretilmez</b>.
    /// </summary>
    [Fact]
    public void DonanimIslemciyeBagliDegilseMaliyetiUydurulmuyor()
    {
        var result = Degerlendir(
            Cost("libx264", 0.5),
            Cost("h264_nvenc", 0.14),
            Cost("h264_nvenc", 0.135),
            donanimVar: true);

        Assert.Equal(RecordingImpact.HardwareOffload, result.Impact);
        Assert.Contains(result.Findings, f => f.Code == PerformanceFindingCode.HardwareNotCpuBound);
        Assert.Contains(result.Findings, f => f.Code == PerformanceFindingCode.HardwareCpuCostNotMeasured);
        Assert.DoesNotContain(result.Findings,
            f => f.Code == PerformanceFindingCode.HardwareCpuCostNotMeasured && f.RealtimeCores > 0);
    }

    /// <summary>
    /// Donanim gecisi is parcacigi sayisiyla hizlaniyorsa is islemcidedir: o zaman
    /// "kodlama islemcinin disinda" denemez, karar yazilim olcusune duser.
    /// </summary>
    [Fact]
    public void DonanimIslemciyeBagliCiktiysaOffloadDenmiyor()
    {
        var result = Degerlendir(
            Cost("libx264", 0.5),
            Cost("h264_nvenc", 0.9),
            Cost("h264_nvenc", 0.2),
            donanimVar: true);

        Assert.NotEqual(RecordingImpact.HardwareOffload, result.Impact);
        Assert.DoesNotContain(result.Findings, f => f.Code == PerformanceFindingCode.HardwareNotCpuBound);
        Assert.DoesNotContain(result.Findings, f => f.Code == PerformanceFindingCode.HardwareCpuCostNotMeasured);
    }

    // --- K1: karar olcumun kendi suresine bagli degil ---

    [Fact]
    public void OlcumunToplamSuresiKarariDegistirmiyor()
    {
        var maliyet = Cost("libx264", 1.4);

        var cabuk = Degerlendir(maliyet, gecen: 4000);
        var yavas = Degerlendir(maliyet, gecen: 20_000);

        Assert.Equal(cabuk.Impact, yavas.Impact);
        Assert.Equal(cabuk.SoftwareRealtimeCores, yavas.SoftwareRealtimeCores, 6);
    }

    /// <summary>
    /// Islemci zamani sayaci bozuksa karar degismez — karar sayaca bakmiyor — ama
    /// sapma kullaniciya bildirilir. Olculemeyen sayac da (0) guvenilmez sayilir.
    /// </summary>
    [Fact]
    public void BozukIslemciSayaciKarariDegistirmiyorAmaBildiriliyor()
    {
        var maliyet = Cost("libx264", 1.4);

        var saglam = Degerlendir(maliyet, sayacKatsayisi: 1);
        var bozuk = Degerlendir(maliyet, sayacKatsayisi: 5.8);
        var olculemedi = Degerlendir(maliyet, sayacKatsayisi: 0);

        Assert.Equal(saglam.Impact, bozuk.Impact);
        Assert.Equal(saglam.Impact, olculemedi.Impact);

        Assert.True(saglam.CpuAccountingTrustworthy);
        Assert.False(bozuk.CpuAccountingTrustworthy);
        Assert.False(olculemedi.CpuAccountingTrustworthy);

        Assert.DoesNotContain(saglam.Findings, f => f.Code == PerformanceFindingCode.CpuAccountingUnreliable);
        var bulgu = Assert.Single(bozuk.Findings, f => f.Code == PerformanceFindingCode.CpuAccountingUnreliable);
        Assert.Equal(5.8, bulgu.Factor, 3);
        Assert.Contains(olculemedi.Findings, f => f.Code == PerformanceFindingCode.CpuAccountingUnreliable);
    }

    // --- K3: butce ---

    [Fact]
    public void ButceAsilirsaBildiriliyor()
    {
        var result = Degerlendir(Cost("libx264", 1.4), gecen: 12_000, butce: 10_000);

        var finding = Assert.Single(result.Findings, f => f.Code == PerformanceFindingCode.BudgetExhausted);
        Assert.Equal(10_000, finding.BudgetMs);
        Assert.Equal(12_000, finding.WallMs);
    }

    [Fact]
    public void TabanGecisKodlayicidanDusuluyor()
    {
        var olculen = new EncoderCost("libx264", true, 1000, 1600, VideoMs);
        var taban = new EncoderCost("taban", true, 250, 200, VideoMs);

        var net = PerformanceProbe.Subtract(olculen, taban);

        Assert.Equal(750, net.CpuMs, 6);
        Assert.Equal(1400, net.WallMs);
        Assert.Equal(0.7, net.RealtimeCores, 6);
        Assert.Equal("libx264", net.Codec);
    }

    // --- Canli olcumler ---

    /// <summary>
    /// Bu makinede kodlamanin gercek maliyeti. Sayilar <c>.calisma/t63/olcum.txt</c>'ye
    /// yazilir; rapora giren her sayi oradan cikar.
    /// </summary>
    [FfmpegFact]
    public async Task BuMakinedeKodlamaNereyeDusuyor()
    {
        var result = await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, 30_000);

        Log($"[gercek] cekirdek={result.LogicalCores} karar={result.Impact} " +
            $"yazilim={result.SoftwareCodec}:{N(result.SoftwareRealtimeCores)} cekirdek " +
            $"donanim={result.HardwareCodec}:{N(result.HardwarePipelineRealtimeFactor)}x gercek zaman " +
            $"sure={result.ElapsedMs}ms sayac={N(result.CpuAccountingFactor)}x " +
            $"sayac-guvenilir={result.CpuAccountingTrustworthy}");

        foreach (var f in result.Findings)
            Log($"[bulgu] {f.Code} {f.Codec} cekirdek={N(f.RealtimeCores)} gercekzaman={N(f.RealtimeFactor)}x " +
                $"katsayi={N(f.Factor)} cpu={N(f.CpuMs)}ms duvar={f.WallMs}ms");

        Assert.NotEqual(RecordingImpact.Unknown, result.Impact);
        Assert.True(result.SoftwareRealtimeCores > 0, "yazilim yolu olculemedi");
    }

    /// <summary>
    /// K1: olcum makine yukune ne kadar dayanikli, ve <b>nerede dayanmiyor</b>.
    ///
    /// Iki sey ayri olculuyor:
    ///
    /// (1) Bos makinede tekrarlanabilirlik — ayni olcum art arda ayni karari vermeli.
    ///     Bu gercek bir degismezlik ve burada iddia ediliyor.
    ///
    /// (2) Yuk altinda sinir. Olcunun para birimi tek is parcacikli gecisin duvar
    ///     saati; makine mesgulken o is parcacigi cekirdegi tam bulamaz, gecis uzar
    ///     ve olculen maliyet <b>gercekten</b> yukselir. Yani yeterince agir bir yuk
    ///     altinda karar degisir. Bu bir kusur degil, olcunun sinirdir; sozlesme de
    ///     "karari degistiren bir yuk seviyesi varsa onu yaz" diyor.
    ///
    ///     Olculdu: 15 mesgul is parcacigi altinda yazilim maliyeti bos makinedeki
    ///     ~0,6 cekirdekten 0,9-1,2 araligina cikiyor, yani 1,0 esigini kimi kosumda
    ///     asiyor ve karar SoftwareLightLoad'dan SoftwareHeavyLoad'a doner. Sinir bu
    ///     makinede 15 rakip is parcacigi civarinda. Sayilar her kosumda ham dosyaya
    ///     yaziliyor.
    ///
    /// Burada iddia edilen sey, kararin yuk altinda <i>degismemesi</i> degil —
    /// olculdu, degisiyor — <b>yonun dogru olmasi</b>: yuk maliyeti yalniz artirabilir,
    /// dolayisiyla karar ancak agirlasabilir. Yuk altinda "daha hafif" bir karar ya da
    /// sebepsiz bir Unknown, olcunun bozuldugu anlamina gelir.
    /// </summary>
    [FfmpegFact]
    public async Task OlcumYukAltindaYalnizAgirlasiyor()
    {
        var yukleyici = Math.Max(1, Environment.ProcessorCount - 1);
        var kapali = new FakeAvailability();

        var bos1 = await PerformanceProbe.RunAsync(kapali, 30_000);
        var bos2 = await PerformanceProbe.RunAsync(kapali, 30_000);

        PerformanceCheckResult yuklu, yukluDonanim;
        PerformanceCheckResult bosDonanim = await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, 30_000);
        using (new CpuLoad(yukleyici))
        {
            yuklu = await PerformanceProbe.RunAsync(kapali, 30_000);
            yukluDonanim = await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, 30_000);
        }

        var sapma = bos1.SoftwareRealtimeCores <= 0
            ? 0
            : (yuklu.SoftwareRealtimeCores - bos1.SoftwareRealtimeCores) / bos1.SoftwareRealtimeCores;

        Log($"[yuk] yukleyici={yukleyici} esik={N(PerformanceCheck.HeavyLoadCores)} | " +
            $"bos tekrar: {bos1.Impact}/{N(bos1.SoftwareRealtimeCores)} ve {bos2.Impact}/{N(bos2.SoftwareRealtimeCores)} | " +
            $"yuklu: {yuklu.Impact}/{N(yuklu.SoftwareRealtimeCores)} sapma=%{N(sapma * 100)} " +
            $"karar-degisti={bos1.Impact != yuklu.Impact} | " +
            $"donanim: bos={bosDonanim.Impact} yuklu={yukluDonanim.Impact}");

        // (1) Bos makinede ayni olcum ayni karari veriyor.
        Assert.Equal(bos1.Impact, bos2.Impact);

        // (2) Yuk maliyeti yalniz artirabilir. Kucuk bir olcum gurultusu payi var,
        // ama yuk altinda maliyetin belirgin sekilde dusmesi olcunun bozuldugudur.
        Assert.True(yuklu.SoftwareRealtimeCores >= bos1.SoftwareRealtimeCores * 0.8,
            $"yuk altinda maliyet dustu: bos {N(bos1.SoftwareRealtimeCores)}, yuklu {N(yuklu.SoftwareRealtimeCores)}");

        // Karar ancak agirlasabilir; hafifleyemez ve sebepsiz kaybolamaz.
        Assert.NotEqual(RecordingImpact.Unknown, yuklu.Impact);
        Assert.False(bos1.Impact == RecordingImpact.SoftwareHeavyLoad
                     && yuklu.Impact == RecordingImpact.SoftwareLightLoad,
            "yuk altinda karar hafifledi");

        // Donanim kodlayicisi yuk altinda da calisiyor; "islemciye bagli degil"
        // saptamasi yuk altinda dusebilir, ama kodlayicinin kendisi kaybolmaz.
        Assert.Contains(yukluDonanim.Findings, f => f.Code == PerformanceFindingCode.HardwarePathWorks);
        Assert.Equal(bosDonanim.HardwareCodec, yukluDonanim.HardwareCodec);
    }

    /// <summary>
    /// K3'un canli yarisi: butce gercekten bagliyor <b>ve</b> sebebini soyluyor.
    /// Yarida kalan olcum sebepsiz "olculemedi" donmemeli; butce dolduysa sonuc
    /// BudgetExhausted tasimali, yoksa kullanici neden sonuc alamadigini bilemez.
    /// </summary>
    [FfmpegFact]
    public async Task ButceGercektenBagliyorVeSebebiniSoyluyor()
    {
        const long dar = 900;
        var basladi = System.Diagnostics.Stopwatch.StartNew();
        var result = await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, dar);
        basladi.Stop();

        var bulgular = string.Join(",", result.Findings.Select(f => f.Code));
        Log($"[butce] sinir={dar}ms gecen={basladi.ElapsedMilliseconds}ms karar={result.Impact} bulgular={bulgular}");

        Assert.True(basladi.ElapsedMilliseconds < dar * 3,
            $"butce {dar}ms iken olcum {basladi.ElapsedMilliseconds}ms surdu");

        var butce = Assert.Single(result.Findings, f => f.Code == PerformanceFindingCode.BudgetExhausted);
        Assert.Equal(dar, butce.BudgetMs);
        Assert.True(butce.WallMs > dar, "butce bulgusu gecen sureyi tasimiyor");
    }

    /// <summary>
    /// Ayni sey saf tarafta: hicbir bacak olculemediyse karar yok, ama butce
    /// dolduysa sebebi bildiriliyor.
    /// </summary>
    [Fact]
    public void HicbirBacakOlculemediyseButceSebebiBildiriliyor()
    {
        var butceDolu = Degerlendir(null, gecen: 12_000, butce: 10_000);
        var butceVar = Degerlendir(null, gecen: 4000, butce: 10_000);

        Assert.Equal(RecordingImpact.Unknown, butceDolu.Impact);
        Assert.Contains(butceDolu.Findings, f => f.Code == PerformanceFindingCode.NotMeasured);
        var bulgu = Assert.Single(butceDolu.Findings, f => f.Code == PerformanceFindingCode.BudgetExhausted);
        Assert.Equal(10_000, bulgu.BudgetMs);
        Assert.Equal(12_000, bulgu.WallMs);

        Assert.Equal(RecordingImpact.Unknown, butceVar.Impact);
        Assert.DoesNotContain(butceVar.Findings, f => f.Code == PerformanceFindingCode.BudgetExhausted);
    }

    /// <summary>K4: olcum bittiginde artik kalmiyor.</summary>
    [FfmpegFact]
    public async Task OlcumArtikBirakmiyor()
    {
        var temp = Path.GetTempPath();
        var once = Directory.GetDirectories(temp, PerformanceProbe.TempPrefix + "*").Length;

        await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, 30_000);

        var sonra = Directory.GetDirectories(temp, PerformanceProbe.TempPrefix + "*").Length;
        Log($"[artik] once={once} sonra={sonra}");
        Assert.Equal(once, sonra);
    }

    /// <summary>
    /// K6 mutasyon: donanim yolu zorla kapatilir. Ayni makinede, ayni ffmpeg ile,
    /// yalniz kodlayici listesi bostur. Karar donanim yolundan yazilim yoluna dusmeli.
    /// </summary>
    [FfmpegFact]
    public async Task DonanimYoluKapatilincaKararDegisiyor()
    {
        var acik = await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, 30_000);
        var kapali = await PerformanceProbe.RunAsync(new FakeAvailability(), 30_000);

        Log($"[mutasyon] acik={acik.Impact}/{acik.HardwareCodec} kapali={kapali.Impact}/{kapali.HardwareCodec} " +
            $"yazilim acik={N(acik.SoftwareRealtimeCores)} kapali={N(kapali.SoftwareRealtimeCores)} " +
            $"donanim-gercekzaman acik={N(acik.HardwarePipelineRealtimeFactor)}x");

        Assert.NotEqual(RecordingImpact.HardwareOffload, kapali.Impact);
        Assert.Equal(string.Empty, kapali.HardwareCodec);
        Assert.Contains(kapali.Findings, f => f.Code == PerformanceFindingCode.NoHardwareEncoder);
    }

    /// <summary>
    /// D1/D2'nin cevabini uretir ve ham dosyaya yazar: bu makinenin islemci zamani
    /// sayaci dogru mu okuyor. Uc bagimsiz kanit alinir ve hepsi kaydedilir.
    ///
    /// (a) Bilinen yuk: tek bir is parcacigi olculen bir sure boyunca cekirdegi
    ///     doldurur, cekirdegin o is parcacigina yazdigi sure okunur. Oran 1'e
    ///     yakinsa sayac saglamdir.
    /// (b) Ayni yuk bir cocuk surecte (ffmpeg, tek is parcacikli kodlama): sureci
    ///     disaridan okuyan TotalProcessorTime duvar saatiyle karsilastirilir.
    /// (c) Ayni kodlama serbest is parcacigiyla: sayac saglamsa toplam islemci
    ///     zamani kabaca sabit kalmali, yalniz duvar saati kisalmali.
    ///
    /// Neyi koruyor: kalibratorun <b>olcmeye devam ettigini</b>. Tur 1'de kalibrator
    /// butun surecin CPU deltasini tek is parcacigina yaziyor ve kirpma yuzunden hep
    /// 1 donuyordu; yani "saglam sayac" ile "mesgul surec" ayirt edilemiyordu ve onu
    /// pinleyen olcu de kirpma yuzunden curutulemezdi. Buradaki iki iddia o hataya
    /// kapiyi kapatir: katsayi pozitif donmeli (0 = "olculemedi", yani sapma
    /// iddiasinin dayanagi yok) ve yakan is parcacigi istenen sureyi gercekten
    /// yakmali (kisa kalirsa katsayi olcum degil gurultudur). Kos ayrica her ffmpeg
    /// gecisinin sifir cikis koduyla bittigini dogrular: basarisiz bir gecisin
    /// sayilari kanit sayilmaz.
    /// </summary>
    [FfmpegFact]
    public void IslemciZamaniSayaciDogruOkuyorMu()
    {
        var saat = System.Diagnostics.Stopwatch.StartNew();
        var katsayi = PerformanceProbe.CalibrateCpuClock(1500);
        saat.Stop();
        Log($"[sayac] (a) is parcacigi duzeyi: duzeltme={N(katsayi)}x " +
            $"yakim-duvar={saat.ElapsedMilliseconds}ms (1 = saglam sayac, 0 = olculemedi)");

        // Kalibrator gercekten olcuyor mu: Windows'ta pozitif donmeli. Sifir donmesi
        // "olculemedi" demek ve o zaman sapma iddiasinin dayanagi kalmaz.
        Assert.True(katsayi > 0, "kalibrator Windows'ta olcemedi");

        // Yakan is parcacigi istenen sureyi gercekten yakmali: cok kisa kalirsa
        // katsayi olculen degil gurultudur. Ust sinir konak mesgulken de bagli
        // kalacak kadar genis, alt sinir istenen sureye kilitli.
        Assert.InRange(saat.ElapsedMilliseconds, 1500, 15_000);

        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_t63tani_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var sample = Path.Combine(dir, "ornek.mp4");
            Kos("uretim", new[] { "-y", "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i",
                $"testsrc2=size={PerformanceProbe.SampleWidth}x{PerformanceProbe.SampleHeight}:rate=60:duration=6",
                "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18", "-pix_fmt", "yuv420p", sample });

            for (var i = 0; i < 2; i++)
                Kos($"(b) x264 -threads 1 #{i}", new[] { "-hide_banner", "-loglevel", "error", "-threads", "1", "-i", sample,
                    "-an", "-c:v", "libx264", "-threads", "1", "-preset", "veryfast", "-f", "null", "-" });
            for (var i = 0; i < 2; i++)
                Kos($"(c) x264 serbest  #{i}", new[] { "-hide_banner", "-loglevel", "error", "-i", sample,
                    "-an", "-c:v", "libx264", "-preset", "veryfast", "-f", "null", "-" });
            for (var i = 0; i < 2; i++)
                Kos($"(d) nvenc -threads 1 #{i}", new[] { "-hide_banner", "-loglevel", "error", "-threads", "1", "-i", sample,
                    "-an", "-c:v", "h264_nvenc", "-threads", "1", "-f", "null", "-" });
            for (var i = 0; i < 2; i++)
                Kos($"(e) nvenc serbest  #{i}", new[] { "-hide_banner", "-loglevel", "error", "-i", sample,
                    "-an", "-c:v", "h264_nvenc", "-f", "null", "-" });
            for (var i = 0; i < 2; i++)
                Kos($"(f) taban -threads 1 #{i}", new[] { "-hide_banner", "-loglevel", "error", "-threads", "1", "-i", sample, "-f", "null", "-" });

            for (var i = 0; i < 3; i++)
                Log($"[sayac] (g) tekrar {i}: is parcacigi duzeltmesi={N(PerformanceProbe.CalibrateCpuClock(1500))}x");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static void Kos(string etiket, string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = new System.Diagnostics.Process { StartInfo = psi };
        var clock = System.Diagnostics.Stopwatch.StartNew();
        p.Start();
        var so = p.StandardOutput.ReadToEndAsync();
        var se = p.StandardError.ReadToEndAsync();
        Task.WaitAll(so, se);
        p.WaitForExit();
        clock.Stop();

        double cpu;
        try { cpu = p.TotalProcessorTime.TotalMilliseconds; }
        catch { cpu = -1; }

        Log($"[sayac] {etiket} cikis={p.ExitCode} cpu={N(cpu)}ms duvar={clock.ElapsedMilliseconds}ms " +
            $"cpu/duvar={N(clock.ElapsedMilliseconds > 0 ? cpu / clock.ElapsedMilliseconds : 0)}");

        Assert.True(p.ExitCode == 0, $"{etiket} kosumu {p.ExitCode} ile dondu: {se.Result.Trim()}");
        Assert.True(clock.ElapsedMilliseconds > 0, $"{etiket} olculebilir bir sure kosmadi");
    }

    /// <summary>Butun mantiksal cekirdekleri mesgul tutan yapay yuk.</summary>
    private sealed class CpuLoad : IDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private readonly Task[] _workers;

        public CpuLoad(int workers)
        {
            _workers = new Task[workers];
            for (var i = 0; i < workers; i++)
                _workers[i] = Task.Factory.StartNew(Burn, _stop.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void Burn()
        {
            var x = 1.0;
            while (!_stop.IsCancellationRequested) x = Math.Sqrt(x + 1.0) * 1.000001;
            GC.KeepAlive(x);
        }

        public void Dispose()
        {
            _stop.Cancel();
            try { Task.WaitAll(_workers, 5000); } catch { }
            _stop.Dispose();
        }
    }
}
