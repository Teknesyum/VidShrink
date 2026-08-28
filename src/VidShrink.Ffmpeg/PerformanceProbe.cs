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
/// karar ona bağlı değil: bu sayaç her makinede doğru değil. Ölçüldü — bu makinede
/// iki saniye boyunca tek bir çekirdeği doldurmuş bir iş parçacığına 0,34 saniye
/// yazıyor, yani gerçeğin kabaca altıda birini. Sapma <see cref="CalibrateCpuClock"/>
/// ile her koşuda yeniden ölçülür ve sonuçla birlikte bildirilir.
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
    /// (1500 ms) tek karelik bir varlık yoklamasının bütçesi; burada beş geçiş var —
    /// sayaç kalibrasyonu, örnek klip üretimi, taban çözme, donanım kodlaması, yazılım
    /// kodlaması — ve kodlama geçişleri tek iş parçacığıyla altı saniyelik 1080p60
    /// görüntü işliyor. Bu makinede toplam ölçülen süre 6-7 saniye; sınır kullanıcının
    /// düğmeye basıp bekleyebileceği yerde, onun kabaca üç katında tutuldu: yirmi
    /// saniye. Aşılırsa ölçüm yarıda kesilir ve eksik bacak
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
        var costs = new List<EncoderCost>();
        var cpuFactor = 1.0;

        try
        {
            cpuFactor = CalibrateCpuClock();

            Directory.CreateDirectory(directory);
            var sample = Path.Combine(directory, "ornek.mp4");
            if (!await GenerateSampleAsync(sample, Remaining(total, budgetMs), ct).ConfigureAwait(false))
                return PerformanceCheckResult.NotMeasured;

            var baseline = await MeasureAsync(sample, null, Remaining(total, budgetMs), ct).ConfigureAwait(false);
            if (!baseline.Succeeded)
                return PerformanceCheckResult.NotMeasured;

            if (hardwareCodec is not null && Remaining(total, budgetMs) > 0)
                costs.Add(Subtract(await MeasureAsync(sample, hardwareCodec, Remaining(total, budgetMs), ct).ConfigureAwait(false), baseline));

            if (Remaining(total, budgetMs) > 0)
                costs.Add(Subtract(await MeasureAsync(sample, SoftwareCodec, Remaining(total, budgetMs), ct).ConfigureAwait(false), baseline));
        }
        catch (OperationCanceledException)
        {
            return PerformanceCheckResult.NotMeasured;
        }
        catch
        {
            return PerformanceCheckResult.NotMeasured;
        }
        finally
        {
            total.Stop();
            Cleanup(directory);
        }

        return PerformanceCheck.Evaluate(
            costs, logicalCores, total.ElapsedMilliseconds, budgetMs, hardwareCodec is not null, cpuFactor);
    }

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
    /// parçacığı belirli bir süre çekirdeği doldurur, sayacın ona yazdığı süre duvar
    /// saatiyle karşılaştırılır. Sağlam bir makinede oran 1'e yakındır. Sayaç bozuksa
    /// karar değişmez — karar zaten sayaca bakmıyor — ama kullanıcıya gösterilen
    /// işlemci zamanının ne kadar eksik olduğu bilinir.
    /// </summary>
    internal static double CalibrateCpuClock(int durationMs = CpuCalibrationMs)
    {
        var self = Process.GetCurrentProcess();
        var before = self.TotalProcessorTime;
        var clock = Stopwatch.StartNew();

        var burner = new Thread(() =>
        {
            var x = 1.0;
            while (clock.ElapsedMilliseconds < durationMs) x = Math.Sqrt(x + 1.0) * 1.000001;
            GC.KeepAlive(x);
        }) { IsBackground = true, Priority = ThreadPriority.Normal };

        burner.Start();
        burner.Join();
        clock.Stop();

        self.Refresh();
        var charged = (self.TotalProcessorTime - before).TotalMilliseconds;
        if (charged <= 0 || clock.ElapsedMilliseconds <= 0) return 1;

        return Math.Clamp(clock.ElapsedMilliseconds / charged, 1, 64);
    }

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
    private static async Task<EncoderCost> MeasureAsync(string sample, string? codec, long timeoutMs, CancellationToken ct)
    {
        var name = codec ?? "taban";
        if (timeoutMs <= 0) return EncoderCost.Missing(name);

        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-threads", "1", "-i", sample };
        if (codec is not null)
        {
            args.AddRange(new[] { "-an", "-c:v", codec, "-threads", "1" });
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
