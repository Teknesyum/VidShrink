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

    // --- K5: her cumle bir sayiya bagli, karar veriden turuyor ---

    [Fact]
    public void OlcumYoksaKararYok()
    {
        var result = PerformanceCheck.Evaluate(Array.Empty<EncoderCost>(), 8, 0, 10_000, false);

        Assert.Equal(RecordingImpact.Unknown, result.Impact);
        Assert.Contains(result.Findings, f => f.Code == PerformanceFindingCode.NotMeasured);
    }

    [Fact]
    public void SayiTasiyanHerBulgununSayisiVar()
    {
        var result = PerformanceCheck.Evaluate(
            new[] { Cost("h264_nvenc", 0.06), Cost("libx264", 0.9) }, 8, 4000, 10_000, true);

        foreach (var finding in result.Findings)
        {
            var magnitude = finding.Code switch
            {
                PerformanceFindingCode.HardwareRealtimeCost => finding.RealtimeCores,
                PerformanceFindingCode.SoftwareRealtimeCost => finding.RealtimeCores,
                PerformanceFindingCode.HardwareSavesCpu => finding.Factor,
                PerformanceFindingCode.SoftwareCostsCores => finding.RealtimeCores,
                PerformanceFindingCode.SoftwareCostIsSmall => finding.RealtimeCores,
                PerformanceFindingCode.BudgetExhausted => finding.BudgetMs,
                PerformanceFindingCode.CpuAccountingUnreliable => finding.Factor,
                _ => 1
            };
            Assert.True(magnitude > 0, $"{finding.Code} sayisiz kaldi");
        }
    }

    // --- K2: donanim yoksa arac "sorun yok" demiyor ---

    [Fact]
    public void DonanimYokVeYazilimPahaliysaAgirYukDeniyor()
    {
        var result = PerformanceCheck.Evaluate(
            new[] { Cost("libx264", 3.0) }, 8, 4000, 10_000, hardwareEncoderPresent: false);

        Assert.Equal(RecordingImpact.SoftwareHeavyLoad, result.Impact);
        Assert.Contains(result.Findings, f => f.Code == PerformanceFindingCode.NoHardwareEncoder);
        Assert.Contains(result.Findings, f => f.Code == PerformanceFindingCode.SoftwareCostsCores);
        Assert.DoesNotContain(result.Findings, f => f.Code == PerformanceFindingCode.HardwarePathWorks);
        Assert.Equal(3.0, result.SoftwareRealtimeCores, 3);
    }

    [Fact]
    public void DonanimYokAmaYazilimUcuzsaSuclanmiyor()
    {
        var result = PerformanceCheck.Evaluate(
            new[] { Cost("libx264", 0.4) }, 8, 4000, 10_000, hardwareEncoderPresent: false);

        Assert.Equal(RecordingImpact.SoftwareLightLoad, result.Impact);
        Assert.Contains(result.Findings, f => f.Code == PerformanceFindingCode.SoftwareCostIsSmall);
    }

    [Fact]
    public void KodlayiciListedeVarAmaKodlamiyorsaYoklukTanBaskaSoyleniyor()
    {
        var costs = new[] { new EncoderCost("h264_qsv", false, 0, 0, VideoMs), Cost("libx264", 3.0) };
        var result = PerformanceCheck.Evaluate(costs, 8, 4000, 10_000, hardwareEncoderPresent: true);

        Assert.Contains(result.Findings, f => f.Code == PerformanceFindingCode.HardwareEncoderFailed);
        Assert.DoesNotContain(result.Findings, f => f.Code == PerformanceFindingCode.NoHardwareEncoder);
        Assert.Equal(RecordingImpact.SoftwareHeavyLoad, result.Impact);
    }

    [Fact]
    public void DonanimCalisiyorsaKodlamaIslemcininDisindaSayiliyor()
    {
        var result = PerformanceCheck.Evaluate(
            new[] { Cost("h264_nvenc", 0.06), Cost("libx264", 0.9) }, 8, 4000, 10_000, true);

        Assert.Equal(RecordingImpact.HardwareOffload, result.Impact);
        Assert.Equal("h264_nvenc", result.HardwareCodec);
        Assert.Equal(15.0, result.CpuSavingFactor, 3);
    }

    // --- K1: karar makine yukune dayanikli, cunku duvar saatine bagli degil ---

    [Fact]
    public void OlcumunToplamSuresiKarariDegistirmiyor()
    {
        var maliyet = new[] { Cost("libx264", 3.0) };

        var cabuk = PerformanceCheck.Evaluate(maliyet, 8, 4000, 30_000, false);
        var yavas = PerformanceCheck.Evaluate(maliyet, 8, 20_000, 30_000, false);

        Assert.Equal(cabuk.Impact, yavas.Impact);
        Assert.Equal(cabuk.SoftwareRealtimeCores, yavas.SoftwareRealtimeCores, 6);
    }

    /// <summary>
    /// Islemci zamani sayaci bozuksa karar degismez — karar sayaca bakmiyor — ama
    /// sapma kullaniciya bildirilir.
    /// </summary>
    [Fact]
    public void BozukIslemciSayaciKarariDegistirmiyorAmaBildiriliyor()
    {
        var maliyet = new[] { Cost("libx264", 3.0) };

        var saglam = PerformanceCheck.Evaluate(maliyet, 8, 4000, 30_000, false, cpuAccountingFactor: 1);
        var bozuk = PerformanceCheck.Evaluate(maliyet, 8, 4000, 30_000, false, cpuAccountingFactor: 5.8);

        Assert.Equal(saglam.Impact, bozuk.Impact);
        Assert.DoesNotContain(saglam.Findings, f => f.Code == PerformanceFindingCode.CpuAccountingUnreliable);
        var bulgu = Assert.Single(bozuk.Findings, f => f.Code == PerformanceFindingCode.CpuAccountingUnreliable);
        Assert.Equal(5.8, bulgu.Factor, 3);
    }

    // --- K3: butce ---

    [Fact]
    public void ButceAsilirsaBildiriliyor()
    {
        var result = PerformanceCheck.Evaluate(
            new[] { Cost("libx264", 3.0) }, 8, 12_000, 10_000, false);

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
            $"donanim={result.HardwareCodec}:{N(result.HardwareRealtimeCores)} " +
            $"yazilim={result.SoftwareCodec}:{N(result.SoftwareRealtimeCores)} " +
            $"kazanc={N(result.CpuSavingFactor)}x sure={result.ElapsedMs}ms " +
            $"sayac-duzeltmesi={N(result.CpuAccountingFactor)}x");

        foreach (var f in result.Findings.Where(f => f.CpuMs > 0 || f.WallMs > 0))
            Log($"[ham] {f.Code} {f.Codec} cpu={N(f.CpuMs)}ms duvar={f.WallMs}ms " +
                $"paralellik={N(f.WallMs <= 0 ? 0 : f.CpuMs / f.WallMs)}");

        Assert.NotEqual(RecordingImpact.Unknown, result.Impact);
        Assert.True(result.SoftwareRealtimeCores > 0, "yazilim yolu olculemedi");
    }

    /// <summary>
    /// K1: ayni olcum, makine bostayken ve yapay yuk altinda, ayni karara varmali.
    /// Iki durum da olculuyor — donanim acikken ve donanim kapaliyken. Kapali durum
    /// asil sinav: karar orada bir esige, yazilim yolunun gercek zaman cekirdegine
    /// bakiyor; acik durumda karar zaten donanimin varligiyla belirleniyor.
    ///
    /// Sapma da gunluge yaziliyor. Sifir degil: yuk altinda libx264'un is parcacigi
    /// havuzu daha cok bekleme ve daha cok baglam degistirme uretir, islemci zamani
    /// gercekten yukselir. Onemli olan yukselisin esigi asmamasi.
    /// </summary>
    [FfmpegFact]
    public async Task OlcumMakineYukuneDayanikli()
    {
        var yukleyici = Math.Max(1, Environment.ProcessorCount - 1);
        var kapali = new FakeAvailability();

        var bos = await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, 30_000);
        var bosYazilim = await PerformanceProbe.RunAsync(kapali, 30_000);

        PerformanceCheckResult yuklu, yukluYazilim;
        using (new CpuLoad(yukleyici))
        {
            yuklu = await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, 30_000);
            yukluYazilim = await PerformanceProbe.RunAsync(kapali, 30_000);
        }

        var sapma = bosYazilim.SoftwareRealtimeCores <= 0
            ? 0
            : (yukluYazilim.SoftwareRealtimeCores - bosYazilim.SoftwareRealtimeCores) / bosYazilim.SoftwareRealtimeCores;
        var esik = bosYazilim.LogicalCores * PerformanceCheck.HeavyLoadCoreFraction;

        Log($"[yuk] yukleyici={yukleyici} cekirdek esik={N(esik)} | " +
            $"donanim acik: bos={bos.Impact}/{N(bos.SoftwareRealtimeCores)} yuklu={yuklu.Impact}/{N(yuklu.SoftwareRealtimeCores)} | " +
            $"donanim kapali: bos={bosYazilim.Impact}/{N(bosYazilim.SoftwareRealtimeCores)} " +
            $"yuklu={yukluYazilim.Impact}/{N(yukluYazilim.SoftwareRealtimeCores)} | " +
            $"sapma=%{N(sapma * 100)} sure bos={bosYazilim.ElapsedMs}ms yuklu={yukluYazilim.ElapsedMs}ms");

        Assert.Equal(bos.Impact, yuklu.Impact);
        Assert.Equal(bos.HardwareCodec, yuklu.HardwareCodec);
        Assert.Equal(bosYazilim.Impact, yukluYazilim.Impact);
        Assert.True(yukluYazilim.SoftwareRealtimeCores < esik,
            $"yuk altinda yazilim maliyeti {N(yukluYazilim.SoftwareRealtimeCores)}, esik {N(esik)}");
    }

    /// <summary>K3'un canli yarisi: butce gercekten bagliyor.</summary>
    [FfmpegFact]
    public async Task ButceGercektenBagliyor()
    {
        const long dar = 900;
        var basladi = System.Diagnostics.Stopwatch.StartNew();
        var result = await PerformanceProbe.RunAsync(EncoderCapabilities.Instance, dar);
        basladi.Stop();

        Log($"[butce] sinir={dar}ms gecen={basladi.ElapsedMilliseconds}ms karar={result.Impact}");

        Assert.True(basladi.ElapsedMilliseconds < dar * 3,
            $"butce {dar}ms iken olcum {basladi.ElapsedMilliseconds}ms surdu");
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
            $"yazilim acik={N(acik.SoftwareRealtimeCores)} kapali={N(kapali.SoftwareRealtimeCores)}");

        Assert.NotEqual(RecordingImpact.HardwareOffload, kapali.Impact);
        Assert.Equal(string.Empty, kapali.HardwareCodec);
        Assert.Contains(kapali.Findings, f => f.Code == PerformanceFindingCode.NoHardwareEncoder);
    }

    /// <summary>
    /// Bu olcunun neden duvar saatine dayandigini kayit altina alir: tek bir is
    /// parcacigi bilinen bir sure boyunca bir cekirdegi doldurur ve isletim
    /// sisteminin surece yazdigi islemci zamani okunur. Saglam bir makinede oran
    /// 1'e yakindir; bu makinede 0,2 civari cikiyor, yani sayac gercegin kabaca
    /// besde birini gosteriyor. Karar bu yuzden <c>TotalProcessorTime</c>'a degil,
    /// tek is parcacikli gecisin duvar saatine bakiyor.
    /// </summary>
    [Fact]
    public void IslemciZamaniSayacininSapmasiOlculuyor()
    {
        var katsayi = PerformanceProbe.CalibrateCpuClock(600);
        Log($"[sayac] duzeltme={N(katsayi)}x (1 = saglam sayac)");

        Assert.True(katsayi >= 1, "duzeltme katsayisi 1'in altina dusemez");
        Assert.True(double.IsFinite(katsayi));
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
