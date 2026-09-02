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
    /// Ucuncu halka ve asil iddia: bekleyen taraf arayuz is parcacigi. Pencere gercek,
    /// yol gercek (<c>LoadWithoutProbing</c> -> <c>ApplyLoaded</c> -> <c>Recalculate</c>);
    /// yalniz ffmpeg'in yerinde bekleyen bir taklit duruyor.
    /// </summary>
    [Fact]
    public void TheWindowThreadWaitsWhileTheProbesRun()
    {
        var delay = TimeSpan.FromMilliseconds(20);
        var availability = new RecordingAvailability(delay);

        var (elapsed, calls) = OnWindow(availability, window =>
        {
            var clock = Stopwatch.StartNew();
            window.LoadWithoutProbing(HdrSource.FilePath, HdrSource);
            clock.Stop();
            return clock.Elapsed;
        });

        Assert.True(calls > 0, "arayuz yolu yetenek nesnesine hic sormadi");
        Assert.True(
            elapsed >= delay * (calls - 1),
            $"arayuz is parcacigi {elapsed.TotalMilliseconds} ms bekledi, {calls} yoklama");
    }

    // --- K4: tekrar sayisi ---

    /// <summary>
    /// N kez yeniden hesap, yoklama sayisi N ile buyuyor mu. Olctugu sey arayuzun bekleyip
    /// beklemedigi: yetenek nesnesi surec dogurmayan bir taklit, onbellegi yok, dolayisiyla
    /// sayilar dogrudan arayuzun kac kez yoklamayi bekledigini veriyor.
    /// </summary>
    [Fact]
    public void EveryRecalculateRepeatsTheProbes()
    {
        const int rounds = 4;
        var availability = new RecordingAvailability(TimeSpan.Zero);

        var (perRound, _) = OnWindow(availability, window =>
        {
            var counts = new List<int>();
            var before = 0;
            for (var i = 0; i < rounds; i++)
            {
                window.LoadWithoutProbing(HdrSource.FilePath, HdrSource);
                var now = availability.Calls.Count;
                counts.Add(now - before);
                before = now;
            }
            return counts;
        });

        Assert.Equal(rounds, perRound.Count);
        Assert.All(perRound, c => Assert.True(c > 0, "bir turda hic yoklama olmadi"));
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
