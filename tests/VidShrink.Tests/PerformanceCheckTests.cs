using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// Gercek kodlama kosturan olculerin koleksiyonu; paralellestirme kapali. Bu olculer
/// duvar saati okuyor: oteki olculerle ayni anda kosarsa onlarin yukunu kendi sonucuna
/// yaziyor ve ayni makinede ard arda iki okuma esigin iki yanina dusebiliyor. Olcen
/// sey olctugu ortami kirletmesin diye koleksiyon tek basina kosar.
/// </summary>
[CollectionDefinition(BasarimOlculeri.Ad, DisableParallelization = true)]
public sealed class BasarimOlculeri
{
    public const string Ad = "basarim-olculeri";
}

/// <summary>
/// Basarim denetcisinin olculeri. VidShrink kayit yapmaz; burada olculen sey kayit
/// araci degil, bu makinede kodlamanin nereye dustugu ve maliyeti. Saf karar olculeri
/// <c>[Fact]</c>, gercek kodlama kosturanlar <c>[FfmpegFact]</c>: CI makinesinde ne
/// ffmpeg ne de donanim kodlayicisi var.
/// </summary>
[Collection(BasarimOlculeri.Ad)]
public sealed class PerformanceCheckTests
{
    private static readonly string MeasurementLog =
        Path.Combine(TipSources.Root, ".calisma", "t63", "olcum.txt");

    private readonly Xunit.Abstractions.ITestOutputHelper _cikti;

    public PerformanceCheckTests(Xunit.Abstractions.ITestOutputHelper cikti) => _cikti = cikti;

    private static void Log(string line)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MeasurementLog)!);
        lock (MeasurementLog) File.AppendAllText(MeasurementLog, line + Environment.NewLine);
    }

    /// <summary>
    /// Kurulamayan bir iddianin sebebi. xunit 2 kosum sirasinda test atlayamiyor, bu
    /// yuzden sebep hem testin kendi ciktisina hem ham olcum gunlugune yaziliyor:
    /// atlanan iddia sessiz kalmiyor.
    /// </summary>
    private void Atlandi(string sebep)
    {
        _cikti.WriteLine("[atlandi] " + sebep);
        Log("[atlandi] " + sebep);
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


    /// <summary>
    /// Bu makinede kodlamanin gercek maliyeti. Sayilar <c>.calisma/t63/olcum.txt</c>'ye
    /// yazilir; rapora giren her sayi oradan cikar.
    ///
    /// Bacak alinamadiysa sebebi ayirt ediliyor: butce dolduysa bu makinenin o anki
    /// mesguliyetidir, iddia kurulmaz ve sebep yazilir. Butce dolmadan eksilen bacak
    /// kodun kusurudur ve kirmiziya doner.
    /// </summary>
    [FfmpegFact]
    public async Task BuMakinedeKodlamaNereyeDusuyor()
    {
        var result = await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, YukOlcumButcesiMs);

        Log($"[gercek] cekirdek={result.LogicalCores} karar={result.Impact} " +
            $"yazilim={result.SoftwareCodec}:{N(result.SoftwareRealtimeCores)} cekirdek " +
            $"donanim={result.HardwareCodec}:{N(result.HardwarePipelineRealtimeFactor)}x gercek zaman " +
            $"sure={result.ElapsedMs}ms sayac={N(result.CpuAccountingFactor)}x " +
            $"sayac-guvenilir={result.CpuAccountingTrustworthy}");

        foreach (var f in result.Findings)
            Log($"[bulgu] {f.Code} {f.Codec} cekirdek={N(f.RealtimeCores)} gercekzaman={N(f.RealtimeFactor)}x " +
                $"katsayi={N(f.Factor)} cpu={N(f.CpuMs)}ms duvar={f.WallMs}ms");

        if (!result.SoftwareMeasured)
        {
            Assert.True(result.BudgetExhausted,
                "yazilim yolu butce dolmadan olculemedi: " +
                string.Join(",", result.Findings.Select(f => f.Code)));
            Atlandi($"yazilim yolu butce doldugu icin olculemedi, gecen {result.ElapsedMs}ms");
            return;
        }

        Assert.NotEqual(RecordingImpact.Unknown, result.Impact);
        Assert.True(result.SoftwareRealtimeCores > 0, "yazilim yolu olculemedi");
    }

    /// <summary>
    /// Yuk olcumunun her bacagina verilen duvar saati siniri. Yuk altinda bir gecis
    /// bos makinedekinin birkac katini surer; olcumun kendi varsayilan butcesi bos
    /// makineye gore bicilmistir ve mesgul bir makinede bacaklar butce doldugu icin
    /// hic olculmeden duser. Burada butce genis tutuluyor ki eksik bacak istisna
    /// olsun; yine de dolarsa <see cref="PerformanceCheckResult.BudgetExhausted"/>
    /// ile ayirt ediliyor.
    /// </summary>
    private const long YukOlcumButcesiMs = 60_000;

    /// <summary>
    /// Iki bagimsiz bos okumanin ayni sessiz makineden geldigi sayilan bant. Disaridan
    /// gelen yuk okumayi yalniz yukari iter; bandi asan bir fark, iki okumadan en az
    /// birinin kirlendigini soyler.
    /// </summary>
    private const double TabanUyumBandi = 1.25;

    /// <summary>
    /// Yuk altinda maliyetin bos okumanin altina dusmedigi sayilan alt sinir.
    /// Olcum gurultusune pay; yon hatasi bu payin cok otesinde durur.
    /// </summary>
    private const double YonPayi = 0.8;

    /// <summary>
    /// K1: olcum makine yukune ne kadar dayanikli, ve <b>nerede dayanmiyor</b>.
    ///
    /// Iddia edilen sey <b>yonun dogru olmasi</b>: yuk maliyeti yalniz artirabilir,
    /// dolayisiyla karar ancak agirlasabilir. Yuk altinda "daha hafif" bir karar,
    /// olcunun bozuldugu anlamina gelir.
    ///
    /// Iddianin dayandigi bos okuma <b>dogrulanmadan</b> kullanilamaz. Olcunun para
    /// birimi tek is parcacikli gecisin duvar saati; makinede baska bir is kosuyorsa
    /// o gecis uzar ve "bos" diye alinan sayi sisar. Sismis bir tabana gore yuklu
    /// okuma dusuk gorunur ve olcu, gercekte olmayan bir gerilemeyi bildirir. Bu
    /// yuzden bos okuma birden fazla kez, biri de yuk kalktiktan sonra aliniyor:
    /// kirlenme sayiyi yalniz yukari itebildigi icin en dusuk okuma gercege en yakin
    /// olanidir, ve okumalar birbirini <see cref="TabanUyumBandi"/> icinde
    /// dogrulamiyorsa makine olcum boyunca sessiz degildi.
    ///
    /// Olculmeyen bacak da dusmus maliyet sayilmiyor. Bacak butce doldugu icin
    /// eksikse bu ortamin haberidir: iddia kurulmaz, sebebi yazilir. Butce dolmadan
    /// eksilen bacak ise kodun kusurudur ve kirmiziya doner.
    /// </summary>
    [FfmpegFact]
    public async Task OlcumYukAltindaYalnizAgirlasiyor()
    {
        var yukleyici = Math.Max(1, Environment.ProcessorCount - 1);
        var kapali = new FakeAvailability();

        var bos1 = await PerformanceProbe.RunAsync(kapali, YukOlcumButcesiMs);
        var bos2 = await PerformanceProbe.RunAsync(kapali, YukOlcumButcesiMs);
        var bosDonanim = await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, YukOlcumButcesiMs);

        PerformanceCheckResult yuklu, yukluDonanim;
        using (new CpuLoad(yukleyici))
        {
            yuklu = await PerformanceProbe.RunAsync(kapali, YukOlcumButcesiMs);
            yukluDonanim = await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, YukOlcumButcesiMs);
        }

        var bos3 = await PerformanceProbe.RunAsync(kapali, YukOlcumButcesiMs);

        var okumalar = new[] { bos1, bos2, bos3 };
        var olculen = okumalar.Where(r => r.SoftwareMeasured).ToArray();

        Log($"[yuk] yukleyici={yukleyici} esik={N(PerformanceCheck.HeavyLoadCores)} | " +
            $"bos okumalar: {string.Join(" ", okumalar.Select(r => $"{r.Impact}/{N(r.SoftwareRealtimeCores)}/olculdu={r.SoftwareMeasured}/butce={r.BudgetExhausted}"))} | " +
            $"yuklu: {yuklu.Impact}/{N(yuklu.SoftwareRealtimeCores)}/olculdu={yuklu.SoftwareMeasured}/butce={yuklu.BudgetExhausted} | " +
            $"donanim: bos={bosDonanim.Impact}/olculdu={bosDonanim.HardwareMeasured}/butce={bosDonanim.BudgetExhausted} " +
            $"yuklu={yukluDonanim.Impact}/olculdu={yukluDonanim.HardwareMeasured}/butce={yukluDonanim.BudgetExhausted}");

        foreach (var eksik in okumalar.Concat(new[] { yuklu }).Where(r => !r.SoftwareMeasured))
            Assert.True(eksik.BudgetExhausted,
                "yazilim bacagi butce dolmadan olculemedi: " + string.Join(",", eksik.Findings.Select(f => f.Code)));

        if (olculen.Length == 0)
        {
            Atlandi("hicbir bos okuma alinamadi, butce doldu");
            return;
        }

        var taban = olculen.Min(r => r.SoftwareRealtimeCores);
        var sessizler = olculen.Where(r => r.SoftwareRealtimeCores <= taban * TabanUyumBandi).ToArray();

        if (sessizler.Length < 2)
        {
            Atlandi($"bos okumalar birbirini dogrulamadi, makine sessiz degildi: " +
                    string.Join(" ", olculen.Select(r => N(r.SoftwareRealtimeCores))));
        }
        else
        {
            Assert.True(sessizler.Select(r => r.Impact).Distinct().Count() == 1,
                "ayni sessiz makinede art arda alinan okumalar farkli karar verdi: " +
                string.Join(" ", sessizler.Select(r => $"{r.Impact}/{N(r.SoftwareRealtimeCores)}")));
        }

        if (!yuklu.SoftwareMeasured)
        {
            Atlandi("yuk altinda yazilim bacagi butce doldugu icin alinamadi, yon iddiasi kurulmadi");
        }
        else
        {
            Assert.True(yuklu.SoftwareRealtimeCores >= taban * YonPayi,
                $"yuk altinda maliyet dustu: en dusuk bos okuma {N(taban)}, yuklu {N(yuklu.SoftwareRealtimeCores)}");

            Assert.NotEqual(RecordingImpact.Unknown, yuklu.Impact);
            Assert.False(sessizler.Any(r => r.Impact == RecordingImpact.SoftwareHeavyLoad)
                         && yuklu.Impact == RecordingImpact.SoftwareLightLoad,
                "yuk altinda karar hafifledi");
        }

        if (!bosDonanim.HardwareMeasured)
            Atlandi("bos kosumda donanim yolu olculemedi, yuk karsilastirmasi kurulmadi");
        else if (yukluDonanim.HardwareMeasured)
        {
            Assert.Contains(yukluDonanim.Findings, f => f.Code == PerformanceFindingCode.HardwarePathWorks);
            Assert.Equal(bosDonanim.HardwareCodec, yukluDonanim.HardwareCodec);
        }
        else
        {
            Assert.True(yukluDonanim.BudgetExhausted,
                "yuk altinda donanim bacagi butce dolmadan kayboldu: " +
                string.Join(",", yukluDonanim.Findings.Select(f => f.Code)));
            Atlandi("yuk altinda donanim bacagi butce doldugu icin alinamadi, karsilastirma kurulmadi");
        }
    }

    /// <summary>
    /// K3'un canli yarisi: butce gercekten bagliyor <b>ve</b> sebebini soyluyor.
    /// Yarida kalan olcum sebepsiz "olculemedi" donmemeli; butce dolduysa sonuc
    /// BudgetExhausted tasimali, yoksa kullanici neden sonuc alamadigini bilemez.
    ///
    /// Bagladigi, ayni makinede genis butceyle alinan bir kosumla karsilastirilarak
    /// okunuyor. Butcenin bir katiyla kurulmus mutlak bir sinir bunu olcemez: butce
    /// dolunca kosan surec oldurulur, baslatma ve oldurme butcenin disinda kalir ve
    /// makine mesgullestikce buyur. O sinir mesgul bir makinede, butce pekala
    /// bagliyorken kirmiziya doner.
    /// </summary>
    [FfmpegFact]
    public async Task ButceGercektenBagliyorVeSebebiniSoyluyor()
    {
        const long dar = 900;

        var genisSaat = System.Diagnostics.Stopwatch.StartNew();
        await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, YukOlcumButcesiMs);
        genisSaat.Stop();

        var basladi = System.Diagnostics.Stopwatch.StartNew();
        var result = await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, dar);
        basladi.Stop();

        var bulgular = string.Join(",", result.Findings.Select(f => f.Code));
        Log($"[butce] sinir={dar}ms gecen={basladi.ElapsedMilliseconds}ms " +
            $"genis={YukOlcumButcesiMs}ms gecen={genisSaat.ElapsedMilliseconds}ms " +
            $"karar={result.Impact} bulgular={bulgular}");

        Assert.True(basladi.ElapsedMilliseconds < genisSaat.ElapsedMilliseconds / 2,
            $"butce baglamadi: {dar}ms butceyle {basladi.ElapsedMilliseconds}ms, " +
            $"genis butceyle {genisSaat.ElapsedMilliseconds}ms");

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

        Assert.True(katsayi > 0, "kalibrator Windows'ta olcemedi");

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
