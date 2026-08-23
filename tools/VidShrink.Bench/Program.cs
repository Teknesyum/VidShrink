using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

try
{
    return args[0] switch
    {
        "measure" => await MeasureAsync(args),
        "shrink" => await ShrinkAsync(args),
        "compare" => Compare(args),
        "panel" => await PanelAsync(args),
        _ => Unknown(args[0])
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  bench measure <referans> <test>");
    Console.WriteLine("  bench shrink <kaynak> <hedefMb,...> --out <klasor>");
    Console.WriteLine("  bench compare <a.json> <b.json>");
    Console.WriteLine("  bench panel <klip,...> --only o1,o2,o3,o4,o5 [--panel-width 960] [--zoom 4] [--samples 12] [--target 20]");
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Bilinmeyen komut: {command}");
    PrintUsage();
    return 1;
}

static async Task<int> MeasureAsync(string[] args)
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("usage: bench measure <referans> <test>");
        return 1;
    }

    var score = await QualityMeter.MeasureAsync(args[1], args[2], CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(score));
    return 0;
}

static async Task<int> ShrinkAsync(string[] args)
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("usage: bench shrink <kaynak> <hedefMb,...> --out <klasor>");
        return 1;
    }

    var source = args[1];
    var targets = args[2]
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(t => double.Parse(t, CultureInfo.InvariantCulture))
        .ToList();

    string? outDir = null;
    for (var i = 3; i < args.Length; i++)
        if (args[i] == "--out" && i + 1 < args.Length) outDir = args[++i];

    if (outDir is null)
    {
        Console.Error.WriteLine("--out <klasor> gerekli");
        return 1;
    }

    Directory.CreateDirectory(outDir);

    var info = await FfprobeClient.ProbeAsync(source);
    var complexity = await ComplexityProbe.RunAsync(info, SpeedMode.Quality);
    var results = new List<BenchResult>();

    foreach (var targetMb in targets)
    {
        var options = new PlanOptions { TargetMb = targetMb };
        var planResult = PlanCalculator.BuildDetailed(info, options, complexity, EncoderCapabilities.Instance);
        var plan = planResult.Plan;

        var outputPath = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(source)}_{targetMb.ToString("0.#", CultureInfo.InvariantCulture)}mb.mp4");
        var stopwatch = Stopwatch.StartNew();
        var encodeResult = await new EncodeRunner().RunAsync(info, plan, outputPath, targetMb, null, CancellationToken.None);
        stopwatch.Stop();

        var score = await QualityMeter.MeasureAsync(source, outputPath, CancellationToken.None);

        var result = new BenchResult(
            targetMb,
            encodeResult.OutputMb,
            encodeResult.OutputMb / targetMb * 100.0,
            plan.Width,
            plan.Height,
            plan.Fps,
            plan.Codec,
            plan.Mode,
            plan.ModeEnum == EncodeMode.Crf ? $"crf {plan.Crf}" : $"{plan.VideoBitrateK}k",
            stopwatch.Elapsed.TotalSeconds,
            score.VmafNegHarmonic,
            score.VmafNegP10,
            score.Xpsnr);
        results.Add(result);

        Console.WriteLine(
            $"{result.TargetMb:0.##} MB -> {result.ActualMb:0.##} MB ({result.FillPercent:0.#}%), " +
            $"{result.Width}x{result.Height}@{result.Fps:0.##}, {result.Codec}/{result.Mode}, " +
            $"VMAF-NEG harm={Fmt(result.VmafNegHarmonic)} p10={Fmt(result.VmafNegP10)}, XPSNR={Fmt(result.Xpsnr)}");
    }

    var resultsPath = Path.Combine(outDir, $"results-{DateTime.Now:yyyyMMdd-HHmmss}.json");
    await File.WriteAllTextAsync(resultsPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"sonuclar: {resultsPath}");
    return 0;
}

static int Compare(string[] args)
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("usage: bench compare <a.json> <b.json>");
        return 1;
    }

    var a = JsonSerializer.Deserialize<List<BenchResult>>(File.ReadAllText(args[1])) ?? new List<BenchResult>();
    var b = JsonSerializer.Deserialize<List<BenchResult>>(File.ReadAllText(args[2])) ?? new List<BenchResult>();

    var count = Math.Max(a.Count, b.Count);
    for (var i = 0; i < count; i++)
    {
        var ar = i < a.Count ? a[i] : null;
        var br = i < b.Count ? b[i] : null;
        if (ar is null || br is null)
        {
            Console.WriteLine($"[{i}] eslesme yok (a:{ar is not null} b:{br is not null})");
            continue;
        }

        Console.WriteLine($"[{i}] hedef {ar.TargetMb:0.##}MB -> {br.TargetMb:0.##}MB");
        Console.WriteLine($"  gercekMb   {ar.ActualMb:0.##} -> {br.ActualMb:0.##}");
        Console.WriteLine($"  doluluk%   {ar.FillPercent:0.#} -> {br.FillPercent:0.#}");
        Console.WriteLine($"  cozunurluk {ar.Width}x{ar.Height} -> {br.Width}x{br.Height}");
        Console.WriteLine($"  fps        {ar.Fps:0.##} -> {br.Fps:0.##}");
        Console.WriteLine($"  codec/mod  {ar.Codec}/{ar.Mode} -> {br.Codec}/{br.Mode}");
        Console.WriteLine($"  crf/bitrate {ar.CrfOrBitrate} -> {br.CrfOrBitrate}");
        Console.WriteLine($"  sure(s)    {ar.EncodeSeconds:0.#} -> {br.EncodeSeconds:0.#}");
        Console.WriteLine($"  vmaf harm  {Fmt(ar.VmafNegHarmonic)} -> {Fmt(br.VmafNegHarmonic)}");
        Console.WriteLine($"  vmaf p10   {Fmt(ar.VmafNegP10)} -> {Fmt(br.VmafNegP10)}");
        Console.WriteLine($"  xpsnr      {Fmt(ar.Xpsnr)} -> {Fmt(br.Xpsnr)}");
    }

    return 0;
}

static string Fmt(double? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "-";

// --- T30: karsilastirma paneli olcum kapisi -----------------------------------------
// Bu komut yalniz sayi uretir. src/ altina hicbir sey yazmaz.

static async Task<int> PanelAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("usage: bench panel <klip,...> --only o1,o2,o3,o4,o5");
        return 1;
    }

    var clips = args[1].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    var only = new HashSet<string> { "o1", "o2", "o3", "o4", "o5" };
    var panelWidth = 960;
    var zoom = 4;
    var samples = 12;
    var targetMb = 20.0;
    var outDir = Path.Combine(Path.GetTempPath(), "vidshrink-panel");

    for (var i = 2; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--only" when i + 1 < args.Length:
                only = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLowerInvariant()).ToHashSet();
                break;
            case "--panel-width" when i + 1 < args.Length:
                panelWidth = int.Parse(args[++i], CultureInfo.InvariantCulture);
                break;
            case "--zoom" when i + 1 < args.Length:
                zoom = int.Parse(args[++i], CultureInfo.InvariantCulture);
                break;
            case "--samples" when i + 1 < args.Length:
                samples = int.Parse(args[++i], CultureInfo.InvariantCulture);
                break;
            case "--target" when i + 1 < args.Length:
                targetMb = double.Parse(args[++i], CultureInfo.InvariantCulture);
                break;
            case "--out" when i + 1 < args.Length:
                outDir = args[++i];
                break;
        }
    }

    Directory.CreateDirectory(outDir);
    Console.WriteLine($"<!-- makine: {Environment.MachineName} · ffmpeg {ToolLocator.GetFfmpegVersion()} · panel {panelWidth}px · zoom {zoom}x · n={samples} -->");

    if (only.Contains("o1")) await Measure1Async(clips, panelWidth, samples);
    if (only.Contains("o4")) await Measure4Async(clips);
    if (only.Contains("o5")) await Measure5Async(clips, panelWidth, zoom, samples);
    if (only.Contains("o3")) await Measure3Async(clips);
    if (only.Contains("o2")) await Measure2Async(clips, panelWidth, targetMb, outDir);
    return 0;
}

/// <summary>
/// Tek kare cekme. stdout'tan ikili veri gelir, stderr **ayni anda** bosaltilir; iki gorev de
/// beklemeden once baslatilir. Bosaltilmazsa boru dolar ve ffmpeg asilir (Jellyfin #17429,
/// docs/taramalar/RAPOR.md:27).
/// </summary>
static async Task<(long Bytes, double Ms, string Error)> GrabAsync(string path, double at, int? width, string format)
{
    var a = new List<string>
    {
        "-hide_banner", "-nostdin", "-loglevel", "error",
        "-ss", at.ToString("0.###", CultureInfo.InvariantCulture),
        "-i", path, "-frames:v", "1", "-an", "-sn", "-dn"
    };

    if (width is { } w)
    {
        a.Add("-vf");
        a.Add($"scale={w}:-2:flags=bilinear");
    }

    if (format == "png") a.AddRange(new[] { "-f", "image2pipe", "-vcodec", "png", "-" });
    else a.AddRange(new[] { "-f", "rawvideo", "-pix_fmt", "bgra", "-" });

    var psi = new ProcessStartInfo
    {
        FileName = ToolLocator.Ffmpeg,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    foreach (var arg in a) psi.ArgumentList.Add(arg);

    var stopwatch = Stopwatch.StartNew();
    using var process = new Process { StartInfo = psi };
    process.Start();

    var stdoutTask = DrainAsync(process.StandardOutput.BaseStream);
    var stderrTask = process.StandardError.ReadToEndAsync();
    var bytes = await stdoutTask;
    var error = await stderrTask;
    await process.WaitForExitAsync();
    stopwatch.Stop();

    return (bytes, stopwatch.Elapsed.TotalMilliseconds, process.ExitCode == 0 ? "" : error.Trim());
}

static async Task<long> DrainAsync(Stream stream)
{
    var buffer = new byte[128 * 1024];
    long total = 0;
    int read;
    while ((read = await stream.ReadAsync(buffer)) > 0) total += read;
    return total;
}

static double Percentile(IEnumerable<double> values, double percent)
{
    var sorted = values.OrderBy(v => v).ToList();
    if (sorted.Count == 0) return double.NaN;
    var index = (int)Math.Ceiling(percent / 100.0 * sorted.Count) - 1;
    return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
}

static IEnumerable<double> Timestamps(double duration, int count)
{
    var usable = Math.Max(0.5, duration - 1.5);
    for (var i = 0; i < count; i++) yield return 0.5 + usable * (i + 0.5) / count;
}

static string Label(string path) => Path.GetFileNameWithoutExtension(path);

static async Task Measure1Async(List<string> clips, int panelWidth, int samples)
{
    Console.WriteLine();
    Console.WriteLine("## O1 - Tek kare cekme gecikmesi (PNG, stdout)");
    Console.WriteLine();
    Console.WriteLine("| Klip | Cozunurluk | Codec | Durum | n | Medyan ms | p95 ms |");
    Console.WriteLine("|---|---|---|---|---|---|---|");

    foreach (var clip in clips)
    {
        var info = await FfprobeClient.ProbeAsync(clip);
        var stamps = Timestamps(info.DurationSeconds, samples).ToList();

        var cold = new List<double>();
        var warm = new List<double>();
        foreach (var t in stamps)
        {
            var first = await GrabAsync(clip, t, panelWidth, "png");
            if (first.Error.Length > 0) { Console.Error.WriteLine($"{Label(clip)} @{t:0.##}: {first.Error}"); continue; }
            cold.Add(first.Ms);
        }

        foreach (var t in stamps)
        {
            var second = await GrabAsync(clip, t, panelWidth, "png");
            if (second.Error.Length > 0) continue;
            warm.Add(second.Ms);
        }

        Console.WriteLine($"| {Label(clip)} | {info.Width}x{info.Height} | {info.VideoCodec} | soguk | {cold.Count} | {Percentile(cold, 50):0.#} | {Percentile(cold, 95):0.#} |");
        Console.WriteLine($"| {Label(clip)} | {info.Width}x{info.Height} | {info.VideoCodec} | sicak | {warm.Count} | {Percentile(warm, 50):0.#} | {Percentile(warm, 95):0.#} |");
    }
}

static async Task Measure4Async(List<string> clips)
{
    Console.WriteLine();
    Console.WriteLine("## O4 - Bitmap bellek maliyeti (BGRA)");
    Console.WriteLine();
    Console.WriteLine("| Klip | Cozunurluk | Olculen bayt | MB | Beklenen WxHx4 |");
    Console.WriteLine("|---|---|---|---|---|");

    foreach (var clip in clips)
    {
        var info = await FfprobeClient.ProbeAsync(clip);
        var raw = await GrabAsync(clip, info.DurationSeconds / 2.0, null, "bgra");
        var expected = (long)info.Width * info.Height * 4;
        Console.WriteLine($"| {Label(clip)} | {info.Width}x{info.Height} | {raw.Bytes} | {raw.Bytes / 1024.0 / 1024.0:0.##} | {expected} |");
    }
}

static async Task Measure5Async(List<string> clips, int panelWidth, int zoom, int samples)
{
    Console.WriteLine();
    Console.WriteLine("## O5 - Yakinlastirmanin kare talebine etkisi");
    Console.WriteLine();
    Console.WriteLine("| Klip | Istenen px | Teslim px | Kaynak tavani | n | Medyan ms | p95 ms |");
    Console.WriteLine("|---|---|---|---|---|---|---|");

    foreach (var clip in clips)
    {
        var info = await FfprobeClient.ProbeAsync(clip);
        var stamps = Timestamps(info.DurationSeconds, samples).ToList();

        foreach (var requested in new[] { panelWidth, panelWidth * zoom })
        {
            var delivered = Math.Min(requested, info.Width);
            var capped = delivered < requested;
            var times = new List<double>();
            foreach (var t in stamps)
            {
                var grab = await GrabAsync(clip, t, delivered, "png");
                if (grab.Error.Length > 0) { Console.Error.WriteLine($"{Label(clip)} @{t:0.##}/{delivered}: {grab.Error}"); continue; }
                times.Add(grab.Ms);
            }

            Console.WriteLine($"| {Label(clip)} | {requested} | {delivered} | {(capped ? "evet" : "hayir")} | {times.Count} | {Percentile(times, 50):0.#} | {Percentile(times, 95):0.#} |");
        }
    }
}

static async Task Measure3Async(List<string> clips)
{
    Console.WriteLine();
    Console.WriteLine("## O3 - Ornek kodlama suresi (CalibrationProbe)");
    Console.WriteLine();
    Console.WriteLine("| Klip | Plan | Kalibrasyon toplam s | Ornek fps | Kare | 2 sn pencere s |");
    Console.WriteLine("|---|---|---|---|---|---|");

    foreach (var clip in clips)
    {
        var info = await FfprobeClient.ProbeAsync(clip);
        var complexity = await ComplexityProbe.RunAsync(info, SpeedMode.Quality);
        var options = new PlanOptions { TargetMb = 20 };
        var draft = PlanCalculator.BuildDetailed(info, options, complexity, EncoderCapabilities.Instance).Plan;

        var stopwatch = Stopwatch.StartNew();
        var calibrated = await CalibrationProbe.RunAsync(info, draft, complexity, SpeedMode.Quality, CancellationToken.None);
        stopwatch.Stop();

        var speed = calibrated.Speed;
        var window = speed is { FramesPerSecond: > 0 } ? (2.0 * draft.Fps / speed.FramesPerSecond).ToString("0.##", CultureInfo.InvariantCulture) : "olculemedi";
        Console.WriteLine(
            $"| {Label(clip)} | {draft.Width}x{draft.Height}@{draft.Fps:0.##} {draft.Codec}/{draft.Preset} | " +
            $"{stopwatch.Elapsed.TotalSeconds:0.##} | {Fmt(speed?.FramesPerSecond)} | {speed?.Frames.ToString() ?? "-"} | {window} |");
    }
}

static async Task Measure2Async(List<string> clips, int panelWidth, double targetMb, string outDir)
{
    Console.WriteLine();
    Console.WriteLine("## O2 - Kodlama surerken ayni islem");
    Console.WriteLine();
    Console.WriteLine("| Klip | Bos kodlama s | Kare cekerken kodlama s | Kodlama yavaslamasi % | Cekim n | Medyan ms | p95 ms |");
    Console.WriteLine("|---|---|---|---|---|---|---|");

    foreach (var clip in clips)
    {
        var info = await FfprobeClient.ProbeAsync(clip);
        var complexity = await ComplexityProbe.RunAsync(info, SpeedMode.Quality);
        var options = new PlanOptions { TargetMb = targetMb };
        var plan = PlanCalculator.BuildDetailed(info, options, complexity, EncoderCapabilities.Instance).Plan;
        var outputPath = Path.Combine(outDir, Label(clip) + "_o2.mp4");

        var baseline = Stopwatch.StartNew();
        await new EncodeRunner().RunAsync(info, plan, outputPath, targetMb, null, CancellationToken.None);
        baseline.Stop();

        var times = new List<double>();
        using var stop = new CancellationTokenSource();
        var during = Stopwatch.StartNew();
        var encodeTask = new EncodeRunner().RunAsync(info, plan, outputPath, targetMb, null, CancellationToken.None);
        var grabTask = Task.Run(async () =>
        {
            var index = 0;
            while (!stop.IsCancellationRequested)
            {
                var at = 0.5 + (index++ * 1.7) % Math.Max(1.0, info.DurationSeconds - 1.5);
                var grab = await GrabAsync(clip, at, panelWidth, "png");
                if (grab.Error.Length == 0) times.Add(grab.Ms);
            }
        });

        await encodeTask;
        during.Stop();
        stop.Cancel();
        await grabTask;

        var slowdown = (during.Elapsed.TotalSeconds / baseline.Elapsed.TotalSeconds - 1.0) * 100.0;
        Console.WriteLine(
            $"| {Label(clip)} | {baseline.Elapsed.TotalSeconds:0.##} | {during.Elapsed.TotalSeconds:0.##} | {slowdown:0.#} | " +
            $"{times.Count} | {Percentile(times, 50):0.#} | {Percentile(times, 95):0.#} |");
    }
}

sealed record BenchResult(
    double TargetMb,
    double ActualMb,
    double FillPercent,
    int Width,
    int Height,
    double Fps,
    string Codec,
    string Mode,
    string CrfOrBitrate,
    double EncodeSeconds,
    double? VmafNegHarmonic,
    double? VmafNegP10,
    double? Xpsnr);
