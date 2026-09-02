using System.Diagnostics;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// T130/K1: <c>Recalculate</c> yolundan ffmpeg surecinin dogdugu iddiasinin kaniti.
///
/// Olcu iki katmanda duruyor. Ustte arayuz: gercek <see cref="MainWindow"/> arayuz is
/// parcaciginda kuruluyor ve yoklamayi taklit eden bir yetenek nesnesiyle besleniyor;
/// olculen sey o is parcaciginin ne kadar beklediği. Altta gercek ffmpeg: taze bir
/// <see cref="EncoderCapabilities"/> ile plan kuruluyor ve o sirada dogan ffmpeg
/// surecleri PID'leriyle sayiliyor.
/// </summary>
public sealed class PlanCalculatorProbeTests
{
    private static readonly MediaInfo HdrSource = new()
    {
        FilePath = "hdr.mp4",
        FileSizeBytes = 200L * 1024 * 1024,
        DurationSeconds = 120,
        Width = 3840,
        Height = 2160,
        Fps = 30,
        VideoCodec = "hevc",
        TotalBitrateBps = 40_000_000,
        IsHdr = true,
        BitDepth = 10,
        ColorPrimaries = "bt2020",
        ColorTransfer = "smpte2084",
        ColorSpace = "bt2020nc"
    };

    private static readonly MediaInfo SdrSource = HdrSource with
    {
        FilePath = "sdr.mp4",
        IsHdr = false,
        BitDepth = 8,
        ColorPrimaries = null,
        ColorTransfer = null,
        ColorSpace = null
    };

    private static PlanOptions FastPreserve => new()
    {
        TargetMb = 16,
        Codec = CodecPreference.Auto,
        SpeedMode = SpeedMode.Fast,
        HdrPolicy = HdrPolicy.Preserve
    };

    /// <summary>
    /// Yoklamayi taklit eder ve her cagriyi kaydeder. <paramref name="delay"/> gercek
    /// ffmpeg surecinin yerini tutar: cagiran taraf beklerse olcu bunu suresinden gorur.
    /// </summary>
    private sealed class RecordingAvailability : IEncoderAvailability, IHdr10EncoderAvailability
    {
        private readonly TimeSpan _delay;
        private readonly Func<string, bool> _works;
        private readonly List<string> _calls = new();

        internal RecordingAvailability(TimeSpan delay, Func<string, bool>? works = null)
        {
            _delay = delay;
            _works = works ?? (_ => false);
        }

        internal IReadOnlyList<string> Calls
        {
            get { lock (_calls) return _calls.ToArray(); }
        }

        internal int Count(string prefix) => Calls.Count(c => c.StartsWith(prefix, StringComparison.Ordinal));

        public bool HasEncoder(string name) => true;

        public bool WorksAsEncoder(string codec)
        {
            Record($"works:{codec}");
            return _works(codec);
        }

        public string? Hdr10PixelFormat(string codec)
        {
            Record($"hdr10:{codec}");
            return null;
        }

        private void Record(string call)
        {
            lock (_calls) _calls.Add(call);
            if (_delay > TimeSpan.Zero) Thread.Sleep(_delay);
        }
    }

    // --- K1: zincir gercek mi ---

    /// <summary>
    /// Zincirin ilk halkasi: <c>BuildDetailed</c> hizli modda donanim adaylarini tek tek
    /// yetenek nesnesine soruyor, HDR kaynakta ustune piksel bicimini soruyor. Adaylarin
    /// sayisi <c>PlanCalculator.FastHardwareOrder</c>'in uzunlugu.
    /// </summary>
    [Fact]
    public void TheFastPathAsksTheAvailabilityForEveryHardwareCandidate()
    {
        var availability = new RecordingAvailability(TimeSpan.Zero);

        PlanCalculator.BuildDetailed(HdrSource, FastPreserve, null, availability);

        Assert.Equal(
            new[]
            {
                "works:av1_nvenc", "works:hevc_nvenc", "works:av1_qsv", "works:hevc_qsv",
                "works:av1_amf", "works:hevc_amf", "works:h264_nvenc",
                "works:libx265"
            },
            availability.Calls);
    }

    /// <summary>
    /// Donanim yolu calisiyorsa aday taramasi ilkinde duruyor, ama HDR kaynak bu kez
    /// piksel bicimi yoklamasini aciyor. <see cref="EncoderCapabilities.Hdr10PixelFormat"/>
    /// iki piksel bicimi deniyor, yani tek cagri iki ffmpeg sureci demek.
    /// </summary>
    [Fact]
    public void WorkingHardwareStillOpensThePixelFormatProbe()
    {
        var availability = new RecordingAvailability(TimeSpan.Zero, codec => codec == "av1_nvenc");

        PlanCalculator.BuildDetailed(HdrSource, FastPreserve, null, availability);

        Assert.Equal(new[] { "works:av1_nvenc", "hdr10:av1_nvenc" }, availability.Calls);
    }

    /// <summary>
    /// Ikinci halka: cagri senkron. Yetenek nesnesi her soruda beklerse <c>BuildDetailed</c>
    /// da bekliyor, yani yoklamanin suresi cagiranin uzerine biniyor.
    /// </summary>
    [Fact]
    public void TheCallerWaitsForEveryProbe()
    {
        var delay = TimeSpan.FromMilliseconds(20);
        var availability = new RecordingAvailability(delay);

        var clock = Stopwatch.StartNew();
        PlanCalculator.BuildDetailed(HdrSource, FastPreserve, null, availability);
        clock.Stop();

        var calls = availability.Calls.Count;
        Assert.True(
            clock.Elapsed >= delay * (calls - 1),
            $"{calls} yoklama, {clock.ElapsedMilliseconds} ms gecti; beklenen alt sinir {delay.TotalMilliseconds * (calls - 1)} ms");
    }

    /// <summary>
    /// Ucuncu halka ve asil iddia: bekleyen taraf arayuz is parcacigiydi. Bu yolun
    /// duzeltmeden onceki hali <c>docs/olcumler/ui-yoklama-donmasi.md</c>'de tabloda;
    /// bugunku hali <see cref="TheWindowThreadNoLongerWaitsForTheProbe"/>. Ayni olcunun
    /// iki yonu ayni dosyada durmasin diye eski surumu burada tutulmuyor: duzeltme geri
    /// alindiginda kirilan olcu asagidakidir.
    /// </summary>

    // --- K2 ve K4: duzeltmenin olculeri ---

    /// <summary>
    /// Hicbir kodlayici olculmemis diyen yetenek nesnesi. <see cref="PlanCalculator"/> bunu
    /// gorunce "donanim yok" dememeli, "heniz olculmedi" demeli.
    /// </summary>
    private sealed class UnmeasuredAvailability : IEncoderAvailability, IHdr10EncoderAvailability, IEncoderMeasurementState
    {
        public bool HasEncoder(string name) => true;
        public bool WorksAsEncoder(string codec) => false;
        public string? Hdr10PixelFormat(string codec) => null;
        public bool IsMeasured(string codec) => false;
        public bool IsHdr10Measured(string codec) => false;
    }

    /// <summary>
    /// K2. Yoklama 400 ms suruyor, arayuz beklemiyor. Olctugu sey onbellegin varligi degil
    /// arayuz is parcaciginin gecen suresi: yoklama arka plana alinmazsa bu sure yoklama
    /// suresinin altina inemez.
    /// </summary>
    [Fact]
    public void TheWindowThreadNoLongerWaitsForTheProbe()
    {
        var delay = TimeSpan.FromMilliseconds(400);
        var availability = new RecordingAvailability(delay);

        var (elapsed, _) = OnWindow(availability, window =>
        {
            var clock = Stopwatch.StartNew();
            window.LoadWithoutProbing(HdrSource.FilePath, HdrSource);
            clock.Stop();
            return clock.Elapsed;
        });

        Assert.True(elapsed < delay, $"arayuz is parcacigi {elapsed.TotalMilliseconds} ms bekledi, tek yoklama {delay.TotalMilliseconds} ms");
    }

    /// <summary>
    /// K4. N kez yeniden hesap, yoklama sayisi N ile buyumuyor. Yetenek nesnesinin kendi
    /// onbellegi yok; sayilan sey gecidin gercek yoklamaya kac kez gittigi. Olcu T94
    /// oncesine (onbellekli) de sonrasina (onbelleksiz) de anlamli, cunku onbellegi degil
    /// arayuzun bekleyip beklemedigini pinliyor.
    ///
    /// Ust sinir sabit ve N'den bagimsiz: en cok 16 anahtar (8 kodlayici x works/hdr10)
    /// x <c>MaxAttempts</c>. Coalescing olmasa on tur 160 yoklama ederdi.
    /// </summary>
    [Fact]
    public void RepeatedRecalculatesDoNotRepeatTheProbe()
    {
        const int rounds = 10;
        const int keys = 16;
        var availability = new RecordingAvailability(TimeSpan.Zero);

        var (counts, _) = OnWindow(availability, window =>
        {
            var perRound = new List<int>();
            for (var i = 0; i < rounds; i++)
            {
                window.LoadWithoutProbing(HdrSource.FilePath, HdrSource);
                Settle(window);
                perRound.Add(availability.Calls.Count);
            }
            return perRound;
        });

        var trace = string.Join(", ", counts);
        Assert.Equal(rounds, counts.Count);

        Assert.True(counts[^1] <= keys * MainWindow.DeferredEncoderAvailability.MaxAttempts,
            $"yoklama sayisi sabitle sinirli degil: {trace}");
        Assert.True(counts[^1] < rounds * 2, $"yoklama sayisi tur sayisiyla buyuyor: {trace}");
        Assert.True(counts[^1] - counts[^2] <= 2, $"son turda hala yeni yoklama var: {trace}");
    }


    /// <summary>
    /// K3, hizli yol. Hicbir aday olculmemisken cevap "donanim yok" degil "heniz
    /// olculmedi": tarama ilk adayda duruyor, yazilim yedegine dusmuyor ve plan
    /// <c>HardwareNotMeasured</c> ile isaretleniyor. Isaret olmasa
    /// arayuz gecici bir cevabi kesin cevap gibi gosterirdi.
    /// </summary>
    [Fact]
    public void AnUnmeasuredFastPathDoesNotBecomeANoHardwareVerdict()
    {
        var unmeasured = PlanCalculator.BuildDetailed(SdrSource, FastPreserve, null, new UnmeasuredAvailability());
        var measuredFailure = PlanCalculator.BuildDetailed(SdrSource, FastPreserve, null, new RecordingAvailability(TimeSpan.Zero));

        Assert.True(unmeasured.HardwareNotMeasured);
        Assert.Equal("av1_nvenc", unmeasured.Plan.Codec);

        Assert.False(measuredFailure.HardwareNotMeasured);
        Assert.Equal("libx265", measuredFailure.Plan.Codec);
    }

    /// <summary>
    /// K2'nin dorduncu yasagi. Yerlesmeyen yoklama olcum sayilmiyor: <c>MaxAttempts</c>
    /// kadar deneniyor, sonrasinda deneme duruyor ama cevap <b>bilinmeyen</b> kaliyor ve
    /// <c>Unsettled</c> ile arayuze tasiniyor. Yerlesmeyeni "olculdu" saymak, oldurulmus bir
    /// denemeyi kalici "bu kodlayici 10 bit tasiyamiyor" kararina cevirmek olurdu — T94'un
    /// kaldirdigi kusurun aynisi. Ikinci iddia sonsuz dongunun yoklugu: uctan fazla soru,
    /// ikiden fazla yoklama dogurmuyor.
    /// </summary>
    [Fact]
    public void AnUnsettledProbeIsNeverPromotedToAMeasurement()
    {
        var attempts = MainWindow.DeferredEncoderAvailability.MaxAttempts;
        var slow = new RecordingAvailability(TimeSpan.FromMilliseconds(MainWindow.DeferredEncoderAvailability.UnsettledProbeMs + 300));
        var gate = new MainWindow.DeferredEncoderAvailability(slow, () => { });

        for (var i = 0; i < attempts + 1; i++)
        {
            Assert.False(gate.IsMeasured("av1_nvenc"));
            var clock = Stopwatch.StartNew();
            while (gate.Pending && clock.ElapsedMilliseconds < 15000) Thread.Sleep(10);
        }

        Assert.False(gate.IsMeasured("av1_nvenc"));
        Assert.True(gate.Unsettled);
        Assert.Equal(attempts, gate.Probes);
    }

    /// <summary>
    /// Yerlesen yoklama olcum sayiliyor: ayni soru ikinci kez gercek yoklamaya gitmiyor
    /// ve cevap okunabiliyor. <see cref="AnUnsettledProbeIsNeverPromotedToAMeasurement"/>
    /// ile birlikte gecidin iki yonunu de pinliyor.
    /// </summary>
    [Fact]
    public void ASettledProbeIsReadWithoutSpawningAgain()
    {
        var fast = new RecordingAvailability(TimeSpan.Zero, codec => codec == "av1_nvenc");
        var gate = new MainWindow.DeferredEncoderAvailability(fast, () => { });

        Assert.False(gate.IsMeasured("av1_nvenc"));
        var clock = Stopwatch.StartNew();
        while (gate.Pending && clock.ElapsedMilliseconds < 5000) Thread.Sleep(5);

        Assert.True(gate.IsMeasured("av1_nvenc"));
        Assert.True(gate.WorksAsEncoder("av1_nvenc"));
        Assert.False(gate.Unsettled);
        Assert.Equal(1, gate.Probes);
    }

    /// <summary>Arka plandaki yoklamalar bitene kadar bekler; kilitlenmemek icin ustten sinirli.</summary>
    private static void Settle(MainWindow window)
    {
        var clock = Stopwatch.StartNew();
        while (window.PlanProbePending && clock.ElapsedMilliseconds < 5000) Thread.Sleep(5);
    }

    // --- K3: ara durum yalan soylemiyor ---

    /// <summary>
    /// Olculmemis kodlayici "10 bit tasiyamiyor" sayilmiyor: ilke degismiyor, ton esleme
    /// suzgeci kurulmuyor, cevap "heniz olculmedi" olarak isaretleniyor.
    /// </summary>
    [Fact]
    public void AnUnmeasuredEncoderDoesNotBecomeATonemapVerdict()
    {
        var unmeasured = HdrResolver.Resolve(HdrSource, HdrPolicy.Preserve, "libsvtav1", new UnmeasuredAvailability());

        Assert.False(unmeasured.PolicyChanged);
        Assert.True(unmeasured.NotMeasured);
        Assert.Null(unmeasured.VideoFilter);
    }

    /// <summary>
    /// Ayni iddia donanim kodlayicisi icin. Yazilim kodlayicisi <c>WorksAsEncoder</c>'a,
    /// donanim kodlayicisi <c>Hdr10PixelFormat</c>'a soruyor — iki ayri dal, iki ayri olcu.
    /// K6'nin ilk turunda ikinci dal olcusuzdu: mutasyon kirilmadi, bu olcu o yuzden var.
    /// </summary>
    [Fact]
    public void AnUnmeasuredHardwareEncoderDoesNotBecomeATonemapVerdict()
    {
        var unmeasured = HdrResolver.Resolve(HdrSource, HdrPolicy.Preserve, "av1_nvenc", new UnmeasuredAvailability());
        var measuredFailure = HdrResolver.Resolve(HdrSource, HdrPolicy.Preserve, "av1_nvenc", new RecordingAvailability(TimeSpan.Zero));

        Assert.True(unmeasured.NotMeasured);
        Assert.False(unmeasured.PolicyChanged);
        Assert.Null(unmeasured.VideoFilter);

        Assert.False(measuredFailure.NotMeasured);
        Assert.True(measuredFailure.PolicyChanged);
        Assert.Equal(HdrResolver.TonemapFilter, measuredFailure.VideoFilter);
    }

    /// <summary>
    /// Olculmus ve gercekten calismayan kodlayici hala ton eslemeye dusuyor: duzeltme
    /// dogru negatifleri kaldirmadi.
    /// </summary>
    [Fact]
    public void AMeasuredFailureStillTonemaps()
    {
        var measuredFailure = new RecordingAvailability(TimeSpan.Zero);

        var resolved = HdrResolver.Resolve(HdrSource, HdrPolicy.Preserve, "libsvtav1", measuredFailure);

        Assert.True(resolved.PolicyChanged);
        Assert.False(resolved.NotMeasured);
        Assert.Equal(HdrResolver.TonemapFilter, resolved.VideoFilter);
    }

    /// <summary>
    /// T125'in yakaladigi urun kusuru, koda cevrilmis hali: ayni dosya, ayni kodlayici,
    /// tek degisen yoklamanin cevabi — cikti HDR ile SDR arasinda gidip geliyor. Kodlayici
    /// secimi degismiyor cunku o yol <c>HasEncoder</c>'a bakiyor.
    /// </summary>
    [Fact]
    public void TheSameSourceFlipsBetweenHdrAndSdrWhenOnlyTheProbeAnswerChanges()
    {
        var options = new PlanOptions
        {
            TargetMb = 16,
            Codec = CodecPreference.MaxCompression,
            SpeedMode = SpeedMode.Quality,
            HdrPolicy = HdrPolicy.Preserve
        };

        var probePassed = HdrResolver.Resolve(HdrSource, options.HdrPolicy, "libsvtav1",
            new RecordingAvailability(TimeSpan.Zero, codec => codec == "libsvtav1"));
        var probeKilled = HdrResolver.Resolve(HdrSource, options.HdrPolicy, "libsvtav1",
            new RecordingAvailability(TimeSpan.Zero));

        Assert.Equal("yuv420p10le", probePassed.PixelFormat);
        Assert.Equal("yuv420p", probeKilled.PixelFormat);
        Assert.True(probeKilled.PolicyChanged);

        var unmeasured = HdrResolver.Resolve(HdrSource, options.HdrPolicy, "libsvtav1", new UnmeasuredAvailability());
        Assert.Equal("yuv420p10le", unmeasured.PixelFormat);
        Assert.True(unmeasured.NotMeasured);
    }

    /// <summary>
    /// Yoklamanin gercek suresi. T125'in raporundaki 4 s'lik butce bu agacta yok
    /// (<c>EncoderCapabilities.ProbeKillMs = 15000</c>); asagidaki sayilar butcenin
    /// neresinde durdugumuzu soyluyor.
    /// </summary>
    [Fact]
    public void TheRealSoftwareProbeDurationIsMeasured()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        if (!MachineIsQuiet(nameof(PlanCalculatorProbeTests))) return;

        var durations = new List<long>();
        for (var i = 0; i < 8; i++)
        {
            var capabilities = FreshCapabilities();
            var clock = Stopwatch.StartNew();
            capabilities.Probe("libsvtav1");
            clock.Stop();
            durations.Add(clock.ElapsedMilliseconds);
        }

        WriteEvidence($"libsvtav1 yoklamasi 8 tekrar: {string.Join(", ", durations)} ms");
        Assert.All(durations, d => Assert.True(d >= 0));
    }

    // --- K1: gercek ffmpeg ---

    /// <summary>
    /// Taze bir <see cref="EncoderCapabilities"/> ile plan kuruluyor ve o sirada dogan
    /// ffmpeg surecleri PID'leriyle sayiliyor. Tekil nesne kullanilmiyor: onun onbellegi
    /// surec omru boyunca dolu kaliyor ve olcu hicbir sey gormezdi.
    /// </summary>
    [Fact]
    public void TheRealFastPathSpawnsFfmpegProcesses()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        if (!MachineIsQuiet(nameof(PlanCalculatorProbeTests))) return;

        var measured = MeasureRealSpawns(HdrSource);

        Assert.True(measured.Processes > 0, $"plan {measured.Ms} ms surdu ama hic ffmpeg sureci gorulmedi");
        WriteEvidence($"gercek ffmpeg HDR tek olcu: {measured.Processes} surec, {measured.Ms} ms, secilen kodlayici {measured.Codec}");
    }

    /// <summary>
    /// Ayni olcunun SDR karsiligi. HDR kaynak piksel bicimi yoklamasini da acıyor;
    /// aradaki fark HDR'nin getirdigi ek surec sayisi.
    /// </summary>
    [Fact]
    public void TheRealSdrPathSpawnsFewerProcessesThanHdr()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        if (!MachineIsQuiet(nameof(PlanCalculatorProbeTests))) return;

        var sdr = MeasureRealSpawns(SdrSource);
        var hdr = MeasureRealSpawns(HdrSource);

        WriteEvidence($"gercek ffmpeg SDR: {sdr.Processes} surec, {sdr.Ms} ms");
        WriteEvidence($"gercek ffmpeg HDR: {hdr.Processes} surec, {hdr.Ms} ms");
        Assert.True(hdr.Processes >= sdr.Processes, $"HDR {hdr.Processes}, SDR {sdr.Processes}");
    }

    /// <summary>
    /// Uygulamanin gercek sirasi: acilista arka planda SDR temsili kaynakla yoklama
    /// isitiliyor (<c>ProbeHardwareEncodersAsync</c>), sonra kullanici HDR dosya
    /// yukluyor. Isitmadan sonra arayuz tarafinda geriye kalan yoklama sayisi ve
    /// suresi budur.
    /// </summary>
    [Fact]
    public void TheWarmedStartupStillLeavesTheHdrProbeOnTheCaller()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        if (!MachineIsQuiet(nameof(PlanCalculatorProbeTests))) return;

        var capabilities = FreshCapabilities();
        var warm = MeasureSpawns(() => PlanCalculator.BuildDetailed(SdrSource, FastPreserve, null, capabilities));
        var afterWarm = MeasureSpawns(() => PlanCalculator.BuildDetailed(SdrSource, FastPreserve, null, capabilities));
        var hdr = MeasureSpawns(() => PlanCalculator.BuildDetailed(HdrSource, FastPreserve, null, capabilities));
        var hdrAgain = MeasureSpawns(() => PlanCalculator.BuildDetailed(HdrSource, FastPreserve, null, capabilities));

        WriteEvidence(
            $"isitma sirasi: SDR ilk {warm.Processes} surec {warm.Ms} ms | SDR ikinci {afterWarm.Processes} surec {afterWarm.Ms} ms | " +
            $"HDR ilk {hdr.Processes} surec {hdr.Ms} ms | HDR ikinci {hdrAgain.Processes} surec {hdrAgain.Ms} ms");

        Assert.Equal(0, afterWarm.Processes);
        Assert.True(hdr.Processes > 0, "isitilmis onbellekten sonra HDR yolu hic surec dogurmadi");
        Assert.Equal(0, hdrAgain.Processes);
    }

    private static (int Processes, long Ms, string Codec) MeasureRealSpawns(MediaInfo info)
    {
        var capabilities = FreshCapabilities();
        var codec = "";
        var measured = MeasureSpawns(() => codec = PlanCalculator.BuildDetailed(info, FastPreserve, null, capabilities).Plan.Codec);
        return (measured.Processes, measured.Ms, codec);
    }

    /// <summary>
    /// <paramref name="work"/> koserken dogan ffmpeg surecleri PID ile sayilir. Olcunun
    /// baslangicindan once acilmis surecler sayilmaz; ayni anda baska bir ffmpeg koseturan
    /// bir olcu varsa sayi yukari kayar, bu yuzden sifir olmasi gereken durumlar ayrica
    /// pinlenmistir.
    /// </summary>
    /// <summary>
    /// Sayac PID yoklamasiyla calisiyor, dolayisiyla o sirada makinede kosan baska bir
    /// ffmpeg de sayiya girer. Duzeltmeden sonra bunun en buyuk kaynagi olcunun kendisi
    /// oldu: arka plana alinan yoklamalar test sinirini asip bir sonraki olcumun icinde
    /// bitiyor. Bu yuzden her olcumden once makine sessizlesene kadar bekleniyor.
    /// </summary>
    private static bool WaitForQuietFfmpeg()
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 5000)
        {
            if (!RunningFfmpegPids(DateTime.MinValue).Any()) return true;
            Thread.Sleep(25);
        }
        return false;
    }

    /// <summary>
    /// Surec sayan olculer yalniz sessiz makinede anlamli. Makinede baska bir ffmpeg
    /// kosuyorsa olcu kirmizi vermez, <b>atlanir</b> ve atlandigi kanit dosyasina yazilir:
    /// yanlis kirmizi, olculmemis olmaktan daha kotudur.
    /// </summary>
    private static bool MachineIsQuiet(string test)
    {
        if (WaitForQuietFfmpeg()) return true;
        WriteEvidence($"{test}: atlandi, makinede baska ffmpeg kosuyor");
        return false;
    }

    private static (int Processes, long Ms) MeasureSpawns(Action work)
    {
        var start = DateTime.Now;
        var seen = new HashSet<int>();
        var running = true;
        var poll = new Thread(() =>
        {
            while (Volatile.Read(ref running))
            {
                foreach (var pid in RunningFfmpegPids(start)) lock (seen) seen.Add(pid);
                Thread.Sleep(10);
            }
            foreach (var pid in RunningFfmpegPids(start)) lock (seen) seen.Add(pid);
        }) { IsBackground = true };
        poll.Start();

        var clock = Stopwatch.StartNew();
        work();
        clock.Stop();
        Volatile.Write(ref running, false);
        poll.Join();
        lock (seen) return (seen.Count, clock.ElapsedMilliseconds);
    }

    private static IEnumerable<int> RunningFfmpegPids(DateTime after)
    {
        Process[] running;
        try { running = Process.GetProcessesByName("ffmpeg"); }
        catch { yield break; }

        foreach (var process in running)
        {
            int? pid = null;
            try { if (process.StartTime >= after) pid = process.Id; }
            catch { }
            finally { process.Dispose(); }
            if (pid is { } value) yield return value;
        }
    }

    /// <summary>
    /// Tekil nesnenin onbellegini tasimayan bir yetenek nesnesi. ffmpeg'in kendi
    /// listeleri okunuyor, sonra <see cref="EncoderCapabilities.Parse"/> ile yeni bir
    /// nesne kuruluyor; yoklama onbellegi bos basliyor.
    /// </summary>
    private static EncoderCapabilities FreshCapabilities()
        => EncoderCapabilities.Parse(
            Capture("-hide_banner", "-encoders"),
            Capture("-hide_banner", "-filters"),
            Capture("-version"));

    private static string Capture(params string[] args)
    {
        var info = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(10000);
        return output;
    }

    private static (T Result, int Calls) OnWindow<T>(RecordingAvailability availability, Func<MainWindow, T> work)
    {
        var settings = Path.Combine(Path.GetTempPath(), $"vidshrink-t130-{Guid.NewGuid():N}.json");
        try
        {
            var result = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = settings };
                window.ApplyHardwareVerdict(availability, true, UsableVerdict);
                window.ChkFastGpu.IsChecked = true;
                var before = availability.Calls.Count;
                var value = work(window);
                return (value, availability.Calls.Count - before);
            });
            return result;
        }
        finally
        {
            if (File.Exists(settings)) File.Delete(settings);
        }
    }

    private static readonly HardwareVerdict UsableVerdict =
        HardwareVerdict.Decide(new EncoderProbeResult("av1_nvenc", true, 193), 1074, 882, 496, 20);

    private static void WriteEvidence(string line)
    {
        var folder = TestPaths.LiveOut("t130-yoklama");
        Directory.CreateDirectory(folder);
        File.AppendAllText(Path.Combine(folder, "olcum.txt"), line + Environment.NewLine);
    }
}
