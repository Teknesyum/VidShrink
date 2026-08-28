using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

/// <summary>
/// "Bu makinede kodlama nereye düşüyor ve maliyeti ne" sorusunun ölçüm tarafı.
/// Kararı vermez; sayıları toplar ve <see cref="PerformanceCheck.Evaluate"/>'e verir.
///
/// Her kodlama geçişi <c>-threads 1</c> ile koşar. Bunun sebebi ölçümü makine yükünden
/// yalıtmak: tek iş parçacığına kısılmış bir geçişte duvar saati doğrudan çekirdek
/// talebine eşittir — bir saniyelik görüntüyü işlemek 0,7 saniye sürüyorsa, gerçek
/// zamanlı kayıt 0,7 çekirdek ister. Kaç çekirdeğe yayıldığı değişse de toplam iş
/// değişmez.
///
/// Süreç işlemci zamanı (<c>TotalProcessorTime</c>) da okunur ve sonuca yazılır, ama
/// karar ona bağlı değil: bu sayaç her makinede doğru değil. Sapma
/// <see cref="CalibrateCpuClock"/> ile her koşuda yeniden ölçülür ve sonuçla birlikte
/// bildirilir. Ölçülen değerler burada değil, o koşumun ham günlüğünde durur.
/// </summary>
public static class PerformanceProbe
{
    /// <summary>
    /// Örnek klip: 1080p60, altı saniye. Kayıt araçlarının tipik düzeni, ve süreç
    /// başlatma maliyetinin ölçümü boğmayacağı kadar uzun: 360 kare kodlanıyor.
    /// Daha kısa bir klipte (720p60, iki saniye) ffmpeg başlatma ve çözücü kurulumu
    /// kodlamanın kendisinden büyük çıkıyor ve iki yol arasındaki fark kayboluyordu.
    /// </summary>
    public const int SampleWidth = 1920;
    public const int SampleHeight = 1080;
    public const double SampleFps = 60;
    public const double SampleSeconds = 6.0;

    /// <summary>
    /// Bütün ölçümün duvar saati üst sınırı. <see cref="HardwareVerdict.ProbeBudgetMs"/>
    /// tek karelik bir varlık yoklamasının bütçesi; burada altı geçiş var — sayaç
    /// kalibrasyonu, örnek klip üretimi, taban çözme, donanım kodlamasının iki geçişi
    /// (tek iş parçacıklı ve serbest) ve yazılım kodlaması — ve kodlama geçişleri tek
    /// iş parçacığıyla örnek klibin tamamını işliyor. Sınır, ölçülen sürenin birkaç
    /// katında tutuldu ki makine meşgulken de yetsin; gerçekleşen süre her koşumda
    /// ham günlüğe yazılır. Aşılırsa ölçüm yarıda kesilir ve eksik bacak
    /// <see cref="PerformanceFindingCode.BudgetExhausted"/> ile bildirilir.
    /// </summary>
    public const long BudgetMs = 20_000;

    /// <summary>
    /// İşlemci zamanı sayacının kalibrasyonu için yakılan süre. Windows sayacı
    /// 15,625 ms adımlarla yazıyor; daha kısa bir yakımda katsayının kendisi
    /// gürültülü çıkıyordu.
    /// </summary>
    internal const int CpuCalibrationMs = 600;

    /// <summary>Geçici dosya kalıbı. Ölçüm bitince kendi silinir.</summary>
    internal const string TempPrefix = "vidshrink_perfcheck_";

    /// <summary>Kayıt araçlarının kullandığı sırayla donanım kodlayıcı adayları.</summary>
    internal static readonly string[] HardwareCandidates =
    {
        "h264_nvenc", "h264_qsv", "h264_amf",
        "hevc_nvenc", "hevc_qsv", "hevc_amf",
        "av1_nvenc"
    };

    /// <summary>Yazılım yolu. OBS'in varsayılanı da budur.</summary>
    internal const string SoftwareCodec = "libx264";
    internal const string SoftwarePreset = "veryfast";

    public static Task<PerformanceCheckResult> RunAsync(
        IEncoderAvailability? availability = null,
        long budgetMs = BudgetMs,
        CancellationToken ct = default)
        => RunAsync(availability ?? EncoderCapabilities.Instance, Environment.ProcessorCount, budgetMs, ct);

    internal static async Task<PerformanceCheckResult> RunAsync(
        IEncoderAvailability availability,
        int logicalCores,
        long budgetMs,
        CancellationToken ct)
    {
        var hardwareCodec = HardwareCandidates.FirstOrDefault(availability.HasEncoder);
        var directory = Path.Combine(Path.GetTempPath(), TempPrefix + Guid.NewGuid().ToString("N"));
        var total = Stopwatch.StartNew();
        EncoderCost? software = null, hardwareSingle = null, hardwareFree = null;
        var cpuFactor = 0.0;

        try
        {
            cpuFactor = CalibrateCpuClock();

            Directory.CreateDirectory(directory);
            var sample = Path.Combine(directory, "ornek.mp4");
            if (!await GenerateSampleAsync(sample, Remaining(total, budgetMs), ct).ConfigureAwait(false))
                return Incomplete(logicalCores, total, budgetMs, hardwareCodec, cpuFactor);

            var baseline = await MeasureAsync(sample, null, true, Remaining(total, budgetMs), ct).ConfigureAwait(false);
            if (!baseline.Succeeded)
                return Incomplete(logicalCores, total, budgetMs, hardwareCodec, cpuFactor);

            if (hardwareCodec is not null && Remaining(total, budgetMs) > 0)
            {
                hardwareSingle = Subtract(
                    await MeasureAsync(sample, hardwareCodec, true, Remaining(total, budgetMs), ct).ConfigureAwait(false),
                    baseline);

                if (Remaining(total, budgetMs) > 0)
                    hardwareFree = Subtract(
                        await MeasureAsync(sample, hardwareCodec, false, Remaining(total, budgetMs), ct).ConfigureAwait(false),
                        baseline);
            }

            if (Remaining(total, budgetMs) > 0)
                software = Subtract(
                    await MeasureAsync(sample, SoftwareCodec, true, Remaining(total, budgetMs), ct).ConfigureAwait(false),
                    baseline);
        }
        catch (OperationCanceledException)
        {
            return Incomplete(logicalCores, total, budgetMs, hardwareCodec, cpuFactor);
        }
        catch
        {
            return Incomplete(logicalCores, total, budgetMs, hardwareCodec, cpuFactor);
        }
        finally
        {
            total.Stop();
            Cleanup(directory);
        }

        return PerformanceCheck.Evaluate(
            software, hardwareSingle, hardwareFree,
            logicalCores, total.ElapsedMilliseconds, budgetMs, hardwareCodec is not null, cpuFactor);
    }

    /// <summary>
    /// Ölçüm yarıda kaldı. Kararı yine <see cref="PerformanceCheck.Evaluate"/> üretir,
    /// çünkü sebebi o biliyor: bütçe dolduysa sonuç
    /// <see cref="PerformanceFindingCode.BudgetExhausted"/> taşır ve kullanıcı
    /// sebepsiz "ölçülemedi" görmez.
    /// </summary>
    private static PerformanceCheckResult Incomplete(
        int logicalCores, Stopwatch total, long budgetMs, string? hardwareCodec, double cpuFactor)
        => PerformanceCheck.Evaluate(
            null, null, null, logicalCores, total.ElapsedMilliseconds, budgetMs,
            hardwareCodec is not null, cpuFactor);

    /// <summary>
    /// Kodlayıcı geçişinden taban çözme geçişini düşer. Her iki geçiş de aynı klibi
    /// aynı tek iş parçacığıyla çözdüğü için çözme payı ortaktır; kalan yalnız
    /// kodlamanın maliyetidir.
    /// </summary>
    internal static EncoderCost Subtract(EncoderCost measured, EncoderCost baseline)
        => measured with
        {
            CpuMs = Math.Max(0, measured.CpuMs - baseline.CpuMs),
            WallMs = Math.Max(0, measured.WallMs - baseline.WallMs)
        };

    /// <summary>
    /// Makinenin işlemci zamanı sayacının kaç kat eksik okuduğunu ölçer: tek bir iş
    /// parçacığı belirli bir süre çekirdeği doldurur, çekirdeğin <b>o iş parçacığına</b>
    /// yazdığı süre duvar saatiyle karşılaştırılır. Sağlam bir sayaçta oran 1'e yakındır;
    /// 1'in üstü sayacın o kat kadar eksik okuduğu anlamına gelir.
    ///
    /// Ölçü iş parçacığı düzeyinde (<c>GetThreadTimes</c>) alınıyor, süreç düzeyinde
    /// değil. Süreç düzeyinde alındığında ölçüm yapısal olarak çalışmıyordu: paralel
    /// koşan bir test konağında aynı anda başka iş parçacıkları da işlemci yakıyor,
    /// süreç deltası yakım süresinden büyük çıkıyor ve oran her zaman 1'e kırpılıyordu —
    /// yani "sağlam sayaç" ile "meşgul süreç" ayırt edilemiyordu.
    ///
    /// Windows dışında iş parçacığı sayacı okunamıyor; orada 0 döner, yani "ölçülemedi".
    /// </summary>
    internal static double CalibrateCpuClock(int durationMs = CpuCalibrationMs)
    {
        if (!OperatingSystem.IsWindows()) return 0;

        double charged = 0;
        long wall = 0;

        var burner = new Thread(() =>
        {
            var clock = Stopwatch.StartNew();
            var before = ThreadCpuMs();
            var x = 1.0;
            while (clock.ElapsedMilliseconds < durationMs) x = Math.Sqrt(x + 1.0) * 1.000001;
            GC.KeepAlive(x);
            clock.Stop();
            charged = ThreadCpuMs() - before;
            wall = clock.ElapsedMilliseconds;
        }) { IsBackground = true };

        burner.Start();
        burner.Join();

        if (charged <= 0 || wall <= 0) return 0;
        return wall / charged;
    }

    /// <summary>Çağıran iş parçacığına yazılmış çekirdek + kullanıcı süresi, ms.</summary>
    private static double ThreadCpuMs()
    {
        if (!GetThreadTimes(GetCurrentThread(), out _, out _, out var kernel, out var user)) return 0;
        return (kernel + user) / 10_000.0;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetThreadTimes(IntPtr thread, out long creation, out long exit, out long kernel, out long user);

    private static long Remaining(Stopwatch total, long budgetMs)
        => budgetMs <= 0 ? int.MaxValue : Math.Max(0, budgetMs - total.ElapsedMilliseconds);

    private static async Task<bool> GenerateSampleAsync(string output, long timeoutMs, CancellationToken ct)
    {
        if (timeoutMs <= 0) return false;
        var seconds = SampleSeconds.ToString(CultureInfo.InvariantCulture);
        var rate = SampleFps.ToString("0.###", CultureInfo.InvariantCulture);
        var args = new[]
        {
            "-y", "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", $"testsrc2=size={SampleWidth}x{SampleHeight}:rate={rate}:duration={seconds}",
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18", "-pix_fmt", "yuv420p",
            output
        };
        var run = await ExecuteAsync(args, timeoutMs, ct).ConfigureAwait(false);
        return run.Succeeded && File.Exists(output);
    }

    /// <summary>
    /// <paramref name="codec"/> null ise klip yalnız çözülür — taban geçiş.
    /// Çıkış her zaman <c>null</c> muxer'ıdır: diske yazma maliyeti ölçüme karışmasın.
    /// </summary>
    private static async Task<EncoderCost> MeasureAsync(
        string sample, string? codec, bool singleThread, long timeoutMs, CancellationToken ct)
    {
        var name = codec ?? "taban";
        if (timeoutMs <= 0) return EncoderCost.Missing(name);

        var args = new List<string> { "-hide_banner", "-loglevel", "error" };
        if (singleThread) args.AddRange(new[] { "-threads", "1" });
        args.AddRange(new[] { "-i", sample });
        if (codec is not null)
        {
            args.AddRange(new[] { "-an", "-c:v", codec });
            if (singleThread) args.AddRange(new[] { "-threads", "1" });
            if (!CodecModel.IsHardware(codec))
            {
                args.Add("-preset");
                args.Add(SoftwarePreset);
            }
        }
        args.AddRange(new[] { "-f", "null", "-" });

        var run = await ExecuteAsync(args.ToArray(), timeoutMs, ct).ConfigureAwait(false);
        return new EncoderCost(name, run.Succeeded, run.CpuMs, run.WallMs, SampleSeconds * 1000);
    }

    private readonly record struct Run(bool Succeeded, double CpuMs, long WallMs);

    private static async Task<Run> ExecuteAsync(string[] args, long timeoutMs, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
        var token = deadline.Token;

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        var clock = Stopwatch.StartNew();
        process.Start();
        using var cancellationRegistration = token.Register(() => TryKill(process));

        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            clock.Stop();
            return new Run(process.ExitCode == 0, CpuMsOf(process), clock.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            clock.Stop();
            return new Run(false, 0, clock.ElapsedMilliseconds);
        }
    }

    private static double CpuMsOf(Process process)
    {
        try { return process.TotalProcessorTime.TotalMilliseconds; }
        catch { return 0; }
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    /// <summary>
    /// Kendi bıraktığını kendi siler. <c>TempCleanup</c> sahiplik esasına göre
    /// çalıştığı için bu süreç ayaktayken bu klasöre dokunmaz; temizlik burada.
    /// </summary>
    private static void Cleanup(string directory)
    {
        try { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); } catch { }
    }
}
