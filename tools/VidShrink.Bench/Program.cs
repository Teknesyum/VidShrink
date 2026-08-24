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
        "play" => await PlayAsync(args),
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
    Console.WriteLine("  bench panel <klip,...> --only o1,o2,o3,o4,o5,o6 [--panel-width 960] [--zoom 4] [--samples 12] [--target 20]");
    Console.WriteLine("  bench play <klipA,klipB> --only k2,p1,p2,p3,p5,p6 [--seconds 10] [--fps 60] [--target 20] [--matrix klip,...]");
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
        Console.Error.WriteLine("usage: bench panel <klip,...> --only o1,o2,o3,o4,o5,o6");
        return 1;
    }

    var clips = args[1].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    var only = new HashSet<string> { "o1", "o2", "o3", "o4", "o5", "o6" };
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
    Console.WriteLine($"<!-- makine: {Environment.MachineName} Â· ffmpeg {ToolLocator.GetFfmpegVersion()} Â· panel {panelWidth}px Â· zoom {zoom}x Â· n={samples} -->");

    if (only.Contains("o1")) await Measure1Async(clips, panelWidth, samples);
    if (only.Contains("o4")) await Measure4Async(clips);
    if (only.Contains("o5")) await Measure5Async(clips, panelWidth, zoom, samples);
    if (only.Contains("o6")) await Measure6Async(clips, panelWidth, samples);
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

// --- T32/K1: anahtar kare hizali cekme -----------------------------------------------
// T30 p95'i medyanin 4-5 kati buldu ve sebebi anahtar kare uzakligina bagladi ama olcmedi.
// Bu olcum ayni damgalari iki kez cekiyor: bir kez istenen anda (hizasiz), bir kez o andan
// once gelen en yakin anahtar karede (hizali).

/// <summary>
/// Anahtar kare damgalarini ffprobe paket listesinden cikarir. stdout ve stderr **ayni anda**
/// bosaltilir; ayni boru tuzagi (docs/taramalar/RAPOR.md:27) ffprobe icin de gecerli.
/// </summary>
static async Task<(List<double> Stamps, double Ms, string Error)> KeyframeStampsAsync(string path)
{
    var a = new[]
    {
        "-hide_banner", "-v", "error",
        "-select_streams", "v:0",
        "-show_entries", "packet=pts_time,flags",
        "-of", "csv=p=0",
        path
    };

    var psi = new ProcessStartInfo
    {
        FileName = ToolLocator.Ffprobe,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    foreach (var arg in a) psi.ArgumentList.Add(arg);

    var stopwatch = Stopwatch.StartNew();
    using var process = new Process { StartInfo = psi };
    process.Start();
    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();
    var stdout = await stdoutTask;
    var error = await stderrTask;
    await process.WaitForExitAsync();
    stopwatch.Stop();

    var stamps = new List<double>();
    foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = line.Trim().Split(',');
        if (parts.Length < 2) continue;
        if (!parts[1].Contains('K')) continue;
        if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var pts)) stamps.Add(pts);
    }

    stamps.Sort();
    return (stamps, stopwatch.Elapsed.TotalMilliseconds, process.ExitCode == 0 ? "" : error.Trim());
}

/// <summary>Dosyayi acar, hicbir kare cozmez. Kuyrugun surec acilisindan mi geldigini ayirir.</summary>
static async Task<(double Ms, string Error)> OpenOnlyAsync(string path)
{
    var psi = new ProcessStartInfo
    {
        FileName = ToolLocator.Ffprobe,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    foreach (var arg in new[] { "-hide_banner", "-v", "error", "-show_format", "-of", "csv=p=0", path })
        psi.ArgumentList.Add(arg);

    var stopwatch = Stopwatch.StartNew();
    using var process = new Process { StartInfo = psi };
    process.Start();
    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();
    _ = await stdoutTask;
    var error = await stderrTask;
    await process.WaitForExitAsync();
    stopwatch.Stop();
    return (stopwatch.Elapsed.TotalMilliseconds, process.ExitCode == 0 ? "" : error.Trim());
}

static double FloorKeyframe(List<double> keyframes, double at)
{
    var chosen = keyframes.Count > 0 ? keyframes[0] : 0;
    foreach (var k in keyframes)
    {
        if (k > at + 1e-6) break;
        chosen = k;
    }
    return chosen;
}

static async Task Measure6Async(List<string> clips, int panelWidth, int samples)
{
    Console.WriteLine();
    Console.WriteLine("## O6 - Anahtar kare dizini cikarma maliyeti");
    Console.WriteLine();
    Console.WriteLine("| Klip | Anahtar kare | Ortalama aralik s | Ilk cikarma ms | Tekrar ms |");
    Console.WriteLine("|---|---|---|---|---|");

    var index = new Dictionary<string, List<double>>();
    foreach (var clip in clips)
    {
        var first = await KeyframeStampsAsync(clip);
        if (first.Error.Length > 0) { Console.Error.WriteLine($"{Label(clip)}: {first.Error}"); continue; }
        var second = await KeyframeStampsAsync(clip);
        index[clip] = first.Stamps;

        var info = await FfprobeClient.ProbeAsync(clip);
        var gap = first.Stamps.Count > 1 ? info.DurationSeconds / (first.Stamps.Count - 1) : double.NaN;
        Console.WriteLine($"| {Label(clip)} | {first.Stamps.Count} | {gap:0.##} | {first.Ms:0.#} | {second.Ms:0.#} |");
    }

    // Kuyruk kod cozmeden mi geliyor yoksa surec acilisindan mi? Ayni dosyayi acan ama hic
    // kare cozmeyen bir cagri tabani verir; kuyruk burada da varsa kod cozmenin sucu degil.
    Console.WriteLine();
    Console.WriteLine("## O6 - Kod cozmesiz taban (ffprobe -show_format, ayni dosya)");
    Console.WriteLine();
    Console.WriteLine("| Klip | n | p50 ms | p90 ms | p95 ms | max ms | >400ms |");
    Console.WriteLine("|---|---|---|---|---|---|---|");

    foreach (var clip in clips)
    {
        var times = new List<double>();
        for (var i = 0; i < samples; i++)
        {
            var probe = await OpenOnlyAsync(clip);
            if (probe.Error.Length == 0) times.Add(probe.Ms);
        }

        Console.WriteLine(
            $"| {Label(clip)} | {times.Count} | {Percentile(times, 50):0.#} | {Percentile(times, 90):0.#} | " +
            $"{Percentile(times, 95):0.#} | {(times.Count > 0 ? times.Max() : double.NaN):0.#} | {times.Count(v => v > 400)} |");
    }

    Console.WriteLine();
    Console.WriteLine("## O6 - Hizali ve hizasiz kare cekme gecikmesi (PNG, stdout)");
    Console.WriteLine();
    Console.WriteLine("| Klip | Cozunurluk | Codec | Hizalama | Durum | n | p50 ms | p90 ms | p95 ms | max ms | >400ms |");
    Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|");

    var pooled = new Dictionary<string, List<double>>();

    foreach (var clip in clips)
    {
        if (!index.TryGetValue(clip, out var keyframes)) continue;
        var info = await FfprobeClient.ProbeAsync(clip);
        var stamps = Timestamps(info.DurationSeconds, samples).ToList();
        var aligned = stamps.Select(t => FloorKeyframe(keyframes, t)).ToList();

        foreach (var (mode, targets) in new[] { ("hizasiz", stamps), ("hizali", aligned) })
        {
            var poolKey = Label(clip) + "/" + mode;
            if (!pooled.ContainsKey(poolKey)) pooled[poolKey] = new List<double>();
            var cold = new List<double>();
            var warm = new List<double>();

            foreach (var t in targets)
            {
                var grab = await GrabAsync(clip, t, panelWidth, "png");
                if (grab.Error.Length > 0) { Console.Error.WriteLine($"{Label(clip)} {mode} @{t:0.##}: {grab.Error}"); continue; }
                cold.Add(grab.Ms);
            }

            foreach (var t in targets)
            {
                var grab = await GrabAsync(clip, t, panelWidth, "png");
                if (grab.Error.Length > 0) continue;
                warm.Add(grab.Ms);
            }

            pooled[poolKey].AddRange(cold);
            pooled[poolKey].AddRange(warm);
            Row("soguk", cold);
            Row("sicak", warm);

            void Row(string state, List<double> times) => Console.WriteLine(
                $"| {Label(clip)} | {info.Width}x{info.Height} | {info.VideoCodec} | {mode} | {state} | {times.Count} | " +
                $"{Percentile(times, 50):0.#} | {Percentile(times, 90):0.#} | {Percentile(times, 95):0.#} | " +
                $"{(times.Count > 0 ? times.Max() : double.NaN):0.#} | {times.Count(v => v > 400)} |");
        }
    }

    // Soguk ve sicak gecis ayni dagilimdan geliyor; kapi karari icin ikisi havuzlanir.
    // Kuyruk bandi (600-1000 ms) ayri sayilir: kod cozmesiz tabanda da gorunen durakalma.
    Console.WriteLine();
    Console.WriteLine("## O6 - Havuzlanmis (soguk+sicak) ve kuyruk bandi");
    Console.WriteLine();
    Console.WriteLine("| Klip/hizalama | n | p50 ms | p90 ms | p95 ms | p99 ms | 600-1000ms | >400ms % |");
    Console.WriteLine("|---|---|---|---|---|---|---|---|");

    foreach (var (key, times) in pooled)
        Console.WriteLine(
            $"| {key} | {times.Count} | {Percentile(times, 50):0.#} | {Percentile(times, 90):0.#} | " +
            $"{Percentile(times, 95):0.#} | {Percentile(times, 99):0.#} | " +
            $"{times.Count(v => v is > 600 and < 1000)} | {100.0 * times.Count(v => v > 400) / Math.Max(1, times.Count):0.#} |");

    var dumpPath = Path.Combine(Path.GetTempPath(), "vidshrink-o6-ornekler.csv");
    var lines = new List<string> { "klip_hizalama,ms" };
    foreach (var (key, times) in pooled)
        lines.AddRange(times.Select(v => $"{key},{v.ToString("0.###", CultureInfo.InvariantCulture)}"));
    await File.WriteAllLinesAsync(dumpPath, lines);
    Console.WriteLine();
    Console.WriteLine($"Ham ornekler: {dumpPath}");
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

// --- T33: oynatma mimarisi olcum kapisi ----------------------------------------------
// Aday A: tek ffmpeg sureci, iki girdi, hstack, BGRA boru.
// Aday B (libmpv) bu araca girmiyor; ikili indirilemedigi icin raporda ayrica anlatiliyor.
// Bu komut da yalniz sayi uretir, src/ altina hicbir sey yazmaz.

static async Task<int> PlayAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("usage: bench play <klipA,klipB> --only k2,p1,p2,p3,p5,p6");
        return 1;
    }

    var clips = args[1].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    var only = new HashSet<string> { "k2", "p1", "p2", "p3", "p5", "p6" };
    var seconds = 10.0;
    var fps = 60;
    var targetMb = 20.0;
    var matrix = new List<string>();
    var outDir = Path.Combine(Path.GetTempPath(), "vidshrink-play");

    for (var i = 2; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--only" when i + 1 < args.Length:
                only = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLowerInvariant()).ToHashSet();
                break;
            case "--seconds" when i + 1 < args.Length:
                seconds = double.Parse(args[++i], CultureInfo.InvariantCulture);
                break;
            case "--fps" when i + 1 < args.Length:
                fps = int.Parse(args[++i], CultureInfo.InvariantCulture);
                break;
            case "--target" when i + 1 < args.Length:
                targetMb = double.Parse(args[++i], CultureInfo.InvariantCulture);
                break;
            case "--matrix" when i + 1 < args.Length:
                matrix = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                break;
            case "--out" when i + 1 < args.Length:
                outDir = args[++i];
                break;
        }
    }

    Directory.CreateDirectory(outDir);
    var left = clips[0];
    var right = clips.Count > 1 ? clips[1] : clips[0];
    var frames = (int)Math.Round(seconds * fps);

    Console.WriteLine($"<!-- makine: {Environment.MachineName} Â· {Environment.ProcessorCount} mantiksal cekirdek Â· ffmpeg {ToolLocator.GetFfmpegVersion()} Â· hedef {fps} fps Â· {frames} kare -->");

    if (only.Contains("k2")) await ProbeFiltersAsync(left);
    if (only.Contains("p1")) await PlayPipeSizesAsync(left, right, fps, frames);
    if (only.Contains("p6")) await PlayAllocAsync(left, right, fps, frames);
    if (only.Contains("p3")) await PlayHwAsync(left, right, fps, frames);
    if (only.Contains("p5")) await PlayMatrixAsync(matrix.Count > 0 ? matrix : clips, fps, frames);
    if (only.Contains("p2")) await PlayDuringEncodeAsync(left, right, fps, frames, targetMb, outDir);
    return 0;
}

/// <summary>
/// Tek ffmpeg sureci, iki girdi, hstack, BGRA ham boru. Tuketici kareyi **sabit havuzdan**
/// okur: kare basina yeni tampon ayrilmaz. stderr ayni anda bosaltilir, yoksa boru dolar
/// ve ffmpeg asilir (docs/taramalar/RAPOR.md:27).
/// Sure olcumu **ilk kare geldikten sonra** baslar; surec acilisi ayri raporlanir, cunku
/// kalici tek surecte o bedel bir kez odenir.
/// </summary>
static async Task<PipeStats> PipeAsync(
    string left, string right, int width, int height, int fps, int maxFrames,
    string? hwaccel = null, bool explicitDownload = false, bool naiveAlloc = false)
{
    var a = new List<string> { "-hide_banner", "-nostdin", "-loglevel", "error" };

    void AddInput(string path)
    {
        if (hwaccel is { } hw)
        {
            a.Add("-hwaccel");
            a.Add(hw);
            if (explicitDownload)
            {
                a.Add("-hwaccel_output_format");
                a.Add(hw == "d3d11va" ? "d3d11" : hw);
            }
        }

        a.Add("-i");
        a.Add(path);
    }

    AddInput(left);
    AddInput(right);

    var head = explicitDownload ? "hwdownload,format=nv12," : "";
    var chain = $"{head}fps={fps},scale={width}:{height}:flags=bilinear,format=bgra";
    a.Add("-filter_complex");
    a.Add($"[0:v]{chain}[l];[1:v]{chain}[r];[l][r]hstack=inputs=2[o]");
    a.AddRange(new[] { "-map", "[o]", "-an", "-sn", "-dn", "-f", "rawvideo", "-pix_fmt", "bgra", "-" });

    var psi = new ProcessStartInfo
    {
        FileName = ToolLocator.Ffmpeg,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    foreach (var arg in a) psi.ArgumentList.Add(arg);

    var frameBytes = width * 2 * height * 4;
    var pool = new byte[frameBytes];
    var intervals = new List<double>(maxFrames);

    using var process = new Process { StartInfo = psi };
    var launch = Stopwatch.StartNew();
    process.Start();
    var stderrTask = process.StandardError.ReadToEndAsync();
    var stream = process.StandardOutput.BaseStream;

    var allocBefore = GC.GetTotalAllocatedBytes(precise: true);
    var count = 0;
    var startupMs = double.NaN;
    var clock = new Stopwatch();
    var previous = 0.0;

    while (count < maxFrames)
    {
        var buffer = naiveAlloc ? new byte[frameBytes] : pool;
        var read = await stream.ReadAtLeastAsync(buffer.AsMemory(0, frameBytes), frameBytes, throwOnEndOfStream: false);
        if (read < frameBytes) break;

        if (count == 0)
        {
            startupMs = launch.Elapsed.TotalMilliseconds;
            clock.Start();
        }
        else
        {
            var now = clock.Elapsed.TotalMilliseconds;
            intervals.Add(now - previous);
            previous = now;
        }

        count++;
    }

    clock.Stop();
    var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocBefore;

    var cpu = TimeSpan.Zero;
    try { cpu = process.TotalProcessorTime; } catch { /* surec kapanmis olabilir */ }

    try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    var error = await stderrTask;
    await process.WaitForExitAsync();

    var elapsed = clock.Elapsed.TotalSeconds;
    var achieved = elapsed > 0 ? (count - 1) / elapsed : 0;
    var mbps = achieved * frameBytes / 1024.0 / 1024.0;
    var cpuPercent = elapsed > 0 ? cpu.TotalSeconds / elapsed * 100.0 : 0;

    return new PipeStats(
        count, startupMs, elapsed, achieved,
        Percentile(intervals, 50), Percentile(intervals, 95), Percentile(intervals, 99),
        intervals.Count > 0 ? intervals.Max() : double.NaN,
        mbps, cpuPercent,
        count > 0 ? (double)allocated / count : double.NaN,
        count == 0 ? error.Trim() : "");
}

/// <summary>K2: filtreyi liste sorgusuyla degil kucuk bir girdiyle grafigi kurarak sinar.</summary>
static async Task<(bool Ok, string Error)> TryGraphAsync(IEnumerable<string> arguments)
{
    var psi = new ProcessStartInfo
    {
        FileName = ToolLocator.Ffmpeg,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    foreach (var arg in arguments) psi.ArgumentList.Add(arg);

    using var process = new Process { StartInfo = psi };
    process.Start();
    var stdoutTask = DrainAsync(process.StandardOutput.BaseStream);
    var stderrTask = process.StandardError.ReadToEndAsync();
    _ = await stdoutTask;
    var error = await stderrTask;
    await process.WaitForExitAsync();

    var text = error.Trim();
    var bad = process.ExitCode != 0
        || text.Contains("Error parsing option", StringComparison.OrdinalIgnoreCase)
        || text.Contains("No such filter", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Unrecognized option", StringComparison.OrdinalIgnoreCase);

    var firstLine = text.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "";
    return (!bad, bad ? firstLine : "");
}

static async Task ProbeFiltersAsync(string clip)
{
    Console.WriteLine();
    Console.WriteLine("## K2 - Bu makinedeki ffmpeg'de gercekten ne var");
    Console.WriteLine();
    Console.WriteLine("| Yetenek | Deneme | Sonuc | stderr |");
    Console.WriteLine("|---|---|---|---|");

    var tiny = new[] { "-f", "lavfi", "-i", "testsrc2=s=64x64:r=10:d=0.2" };

    var cases = new List<(string Name, string What, string[] Args)>
    {
        ("hstack", "iki lavfi girdi hstack=inputs=2",
            tiny.Concat(tiny).Concat(new[] { "-filter_complex", "[0:v][1:v]hstack=inputs=2[o]", "-map", "[o]", "-frames:v", "1", "-f", "null", "-" }).ToArray()),
        ("scale+format=bgra", "scale=64:36,format=bgra",
            tiny.Concat(new[] { "-vf", "scale=64:36,format=bgra", "-frames:v", "1", "-f", "null", "-" }).ToArray()),
        ("fps", "fps=60",
            tiny.Concat(new[] { "-vf", "fps=60", "-frames:v", "1", "-f", "null", "-" }).ToArray()),
        ("zscale", "zscale=w=32:h=32",
            tiny.Concat(new[] { "-vf", "zscale=w=32:h=32", "-frames:v", "1", "-f", "null", "-" }).ToArray()),
        // T32: libx264/libx265 uzerinden verilen -color_trc/-color_primaries sessizce dusuyor.
        // HDR girdisi setparams ile kuruluyor; yoksa zscale "no path between colorspaces" der
        // ve tonemap yokmus gibi gorunur.
        ("tonemap", "setparams=HDR -> zscale=t=linear,tonemap=hable,zscale=t=bt709",
            tiny.Concat(new[]
            {
                "-vf",
                "setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc," +
                "zscale=t=linear:npl=100,tonemap=hable,zscale=t=bt709:m=bt709:p=bt709:r=tv",
                "-frames:v", "1", "-f", "null", "-"
            }).ToArray()),
        ("rawvideo/bgra boru", "-f rawvideo -pix_fmt bgra -",
            tiny.Concat(new[] { "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "bgra", "-" }).ToArray())
    };

    foreach (var hw in new[] { "d3d11va", "dxva2", "qsv", "cuda", "vulkan" })
    {
        cases.Add(($"-hwaccel {hw}", "gercek dosyada tek kare",
            new[] { "-hide_banner", "-v", "error", "-hwaccel", hw, "-i", clip, "-frames:v", "1", "-f", "null", "-" }));
    }

    cases.Add(("d3d11va + hwdownload", "-hwaccel_output_format d3d11 -vf hwdownload,format=nv12",
        new[] { "-hide_banner", "-v", "error", "-hwaccel", "d3d11va", "-hwaccel_output_format", "d3d11", "-i", clip, "-vf", "hwdownload,format=nv12", "-frames:v", "1", "-f", "null", "-" }));

    foreach (var (name, what, arguments) in cases)
    {
        var full = arguments[0] == "-hide_banner" ? arguments : new[] { "-hide_banner", "-v", "error" }.Concat(arguments).ToArray();
        var result = await TryGraphAsync(full);
        var note = result.Error.Length > 80 ? result.Error[..80] + "..." : result.Error;
        Console.WriteLine($"| {name} | {what} | {(result.Ok ? "**var**" : "yok")} | {(note.Length == 0 ? "-" : note)} |");
    }
}

static string Row(string label, PipeStats s) =>
    $"| {label} | {s.Frames} | {s.StartupMs:0.#} | {s.Fps:0.#} | {s.P50:0.##} | {s.P95:0.##} | {s.P99:0.##} | {s.MaxMs:0.#} | {s.MBps:0.#} | {s.CpuPercent:0.#} |" +
    (s.Error.Length > 0 ? $" <!-- {s.Error} -->" : "");

static void Header(string first)
{
    Console.WriteLine($"| {first} | Kare | Acilis ms | Surdurulen fps | Aralik p50 ms | p95 ms | p99 ms | max ms | MB/s | CPU % |");
    Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
}

/// <summary>Sozlesmedeki uc panel boyutu. Kapilar G1 ilkine, G2 sonuncusuna bakiyor.</summary>
static (int W, int H)[] PanelSizes() => new[] { (960, 540), (1280, 720), (1920, 1080) };

static async Task PlayPipeSizesAsync(string left, string right, int fps, int frames)
{
    Console.WriteLine();
    Console.WriteLine("## P1 - Boru akisinin hizi (tek surec, iki girdi, hstack, BGRA)");
    Console.WriteLine();
    Header("Boyut");

    foreach (var (w, h) in PanelSizes())
    {
        var stats = await PipeAsync(left, right, w, h, fps, frames);
        Console.WriteLine(Row($"2x{w}x{h}", stats));
    }
}

static async Task PlayAllocAsync(string left, string right, int fps, int frames)
{
    Console.WriteLine();
    Console.WriteLine("## P6 - Kare basina bellek ayirma");
    Console.WriteLine();
    Console.WriteLine("| Boyut | Okuma bicimi | Kare | Kare basi ayrilan bayt | Kare boyutu bayt | Surdurulen fps |");
    Console.WriteLine("|---|---|---|---|---|---|");

    foreach (var (w, h) in PanelSizes())
    {
        foreach (var naive in new[] { false, true })
        {
            var stats = await PipeAsync(left, right, w, h, fps, frames, naiveAlloc: naive);
            var name = naive ? "kare basi yeni tampon" : "**sabit havuz**";
            Console.WriteLine($"| 2x{w}x{h} | {name} | {stats.Frames} | {stats.BytesPerFrame:0} | {w * 2 * h * 4} | {stats.Fps:0.#} |");
        }
    }
}

static async Task PlayHwAsync(string left, string right, int fps, int frames)
{
    Console.WriteLine();
    Console.WriteLine("## P3 - Donanim kod cozme");
    Console.WriteLine();
    Header("Yol");

    var (w, h) = (1920, 1080);
    var variants = new (string Name, string? Hw, bool Download)[]
    {
        ("yazilim (taban)", null, false),
        ("d3d11va ortuk indirme", "d3d11va", false),
        ("d3d11va + hwdownload", "d3d11va", true),
        ("dxva2 ortuk indirme", "dxva2", false)
    };

    foreach (var (name, hw, download) in variants)
    {
        var stats = await PipeAsync(left, right, w, h, fps, frames, hw, download);
        Console.WriteLine(Row($"2x{w}x{h} Â· {name}", stats));
    }
}

static async Task PlayMatrixAsync(List<string> clips, int fps, int frames)
{
    Console.WriteLine();
    Console.WriteLine("## P5 - Codec matrisi (ayni klip iki kez, 2x960x540 panel)");
    Console.WriteLine();
    Header("Klip");

    foreach (var clip in clips)
    {
        var info = await FfprobeClient.ProbeAsync(clip);
        var stats = await PipeAsync(clip, clip, 960, 540, fps, frames);
        Console.WriteLine(Row($"{Label(clip)} ({info.Width}x{info.Height} {info.VideoCodec})", stats));
    }

    Console.WriteLine();
    Console.WriteLine("### P5 - Ayni klipler 2x1920x1080 panelde");
    Console.WriteLine();
    Header("Klip");

    foreach (var clip in clips)
    {
        var stats = await PipeAsync(clip, clip, 1920, 1080, fps, frames);
        Console.WriteLine(Row(Label(clip), stats));
    }
}

static async Task PlayDuringEncodeAsync(string left, string right, int fps, int frames, double targetMb, string outDir)
{
    Console.WriteLine();
    Console.WriteLine("## P2 - Kodlama koserken");
    Console.WriteLine();
    Console.WriteLine("| Boyut | Bos kodlama s | Boru koserken kodlama s | Kodlama yavaslamasi % | Akis fps | fps kaybi % | p99 ms | CPU % |");
    Console.WriteLine("|---|---|---|---|---|---|---|---|");

    var info = await FfprobeClient.ProbeAsync(left);
    var complexity = await ComplexityProbe.RunAsync(info, SpeedMode.Quality);
    var options = new PlanOptions { TargetMb = targetMb };
    var plan = PlanCalculator.BuildDetailed(info, options, complexity, EncoderCapabilities.Instance).Plan;
    var outputPath = Path.Combine(outDir, Label(left) + "_p2.mp4");

    var baseline = Stopwatch.StartNew();
    await new EncodeRunner().RunAsync(info, plan, outputPath, targetMb, null, CancellationToken.None);
    baseline.Stop();

    foreach (var (w, h) in PanelSizes())
    {
        var alone = await PipeAsync(left, right, w, h, fps, frames);

        using var stop = new CancellationTokenSource();
        PipeStats? during = null;
        var pipeTask = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                var s = await PipeAsync(left, right, w, h, fps, frames);
                during ??= s;
            }
        });

        var withPipe = Stopwatch.StartNew();
        await new EncodeRunner().RunAsync(info, plan, outputPath, targetMb, null, CancellationToken.None);
        withPipe.Stop();
        stop.Cancel();
        await pipeTask;

        var slowdown = (withPipe.Elapsed.TotalSeconds / baseline.Elapsed.TotalSeconds - 1.0) * 100.0;
        var loss = alone.Fps > 0 && during is not null ? (1.0 - during.Fps / alone.Fps) * 100.0 : double.NaN;
        Console.WriteLine(
            $"| 2x{w}x{h} | {baseline.Elapsed.TotalSeconds:0.##} | {withPipe.Elapsed.TotalSeconds:0.##} | {slowdown:0.#} | " +
            $"{during?.Fps ?? double.NaN:0.#} | {loss:0.#} | {during?.P99 ?? double.NaN:0.##} | {during?.CpuPercent ?? double.NaN:0.#} |");
    }
}

sealed record PipeStats(
    int Frames,
    double StartupMs,
    double Seconds,
    double Fps,
    double P50,
    double P95,
    double P99,
    double MaxMs,
    double MBps,
    double CpuPercent,
    double BytesPerFrame,
    string Error);

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

