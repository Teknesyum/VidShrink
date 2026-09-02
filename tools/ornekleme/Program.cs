using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Ornekleme;

public static class Program
{
    private const string Preset = "medium";
    private const int Crf = 23;

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("ornekleme sapma <klip,...> --out <dosya> [--threads 4]");
            Console.WriteLine("ornekleme maliyet <klip,...> [--threads 4] [--tekrar 3]");
            Console.WriteLine("ornekleme plan <klip> [--threads 4]");
            return 1;
        }

        return args[0] switch
        {
            "sapma" => await DeviationAsync(args),
            "maliyet" => await CostAsync(args),
            "plan" => await PlanAsync(args),
            _ => Unknown(args[0])
        };
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Bilinmeyen komut: {command}");
        return 1;
    }

    private static string? Option(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    private static int Threads(string[] args)
        => int.TryParse(Option(args, "--threads"), out var value) && value > 0 ? value : 4;

    private static async Task<int> DeviationAsync(string[] args)
    {
        if (args.Length < 2) return Unknown("sapma");
        var clips = args[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var threads = Threads(args);
        var outPath = Option(args, "--out") ?? Path.Combine(".calisma", "t103", "sapma.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);

        var results = new List<ClipReport>();
        foreach (var clip in clips)
        {
            var report = await MeasureClipAsync(clip, threads);
            results.Add(report);
            Console.WriteLine(Line(report));
            await File.WriteAllTextAsync(outPath, JsonSerializer.Serialize(results, Json));
        }

        Console.WriteLine($"yazildi: {outPath}");
        return 0;
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private static string Line(ClipReport report)
    {
        var best = report.Variants.OrderBy(v => Math.Abs(v.Deviation)).First();
        return $"{report.Clip} sure={report.DurationSeconds:0.#} cv={report.Heterogeneity:0.000} " +
               $"referans-bppf={report.ReferenceBppf:0.00000} en-iyi={best.Name} sapma={best.Deviation:P2}";
    }

    private sealed record VariantReport(
        string Name,
        string Plan,
        int Windows,
        double WindowSeconds,
        double SampledSeconds,
        double Estimate,
        double Deviation,
        double PlanBias,
        double AppliedCorrection,
        IReadOnlyList<double> Starts);

    private sealed record ClipReport(
        string Clip,
        double DurationSeconds,
        int Width,
        int Height,
        double Fps,
        double ReferenceBppf,
        long ReferenceFrames,
        double Heterogeneity,
        int ProfileSeconds,
        int SceneCuts,
        double PacketReadSeconds,
        double SceneScanSeconds,
        double ScanBiasSeconds,
        double ScanBias,
        int AutoWindowCount,
        IReadOnlyList<VariantReport> Variants);

    private static async Task<ClipReport> MeasureClipAsync(string clip, int threads)
    {
        var info = await FfprobeClient.ProbeAsync(clip);
        var cache = new SampleCache(clip, info, threads);

        var reference = await cache.WholeAsync();
        var referenceBppf = Bppf(reference, info.Width, info.Height);

        var packetWatch = Stopwatch.StartNew();
        var packets = await ReadAllPacketsAsync(clip);
        packetWatch.Stop();
        var profile = ComplexityProbe.SecondBitProfile(packets, info.DurationSeconds);
        var cv = ComplexityProbe.Heterogeneity(profile);

        var sceneWatch = Stopwatch.StartNew();
        var scan = await SceneDetector.ScanAsync(clip);
        sceneWatch.Stop();
        var cuts = scan.Ok
            ? SceneMap.DerivedCutTimes(scan.Candidates, info.DurationSeconds, Math.Max(scan.Frames.Count / info.DurationSeconds, 1.0), ThresholdRule.Measured)
            : Array.Empty<double>();

        var scanBiasWatch = Stopwatch.StartNew();
        var scanBias = await ComplexityProbe.ScanBiasAsync(info, SpeedMode.Quality, default);
        scanBiasWatch.Stop();

        var variants = new List<VariantReport>();

        var fixedWindows = ComplexityProbe.PlanWindows(SamplingPlan.Fixed, info.DurationSeconds);
        variants.Add(await EvaluateAsync("bugun-duzeltmesiz", "Fixed", fixedWindows, cache, info, referenceBppf, profile, 0.0));
        variants.Add(await EvaluateAsync("bugun-scanpoints", "Fixed", fixedWindows, cache, info, referenceBppf, profile,
            ComplexityProfile.IsTrustedBias(scanBias) ? scanBias : 0.0));
        variants.Add(await EvaluateAsync("bugun-paket-orani", "Fixed", fixedWindows, cache, info, referenceBppf, profile,
            ComplexityProbe.PlanBias(fixedWindows, profile)));

        var autoCount = ComplexityProbe.PlanWindowCount(info.DurationSeconds, cv);

        foreach (var plan in new[] { SamplingPlan.Profile, SamplingPlan.Scene })
        {
            foreach (var count in new[] { 2, 3, 4, 6, 8, 12 })
            {
                var windows = ComplexityProbe.PlanWindows(plan, info.DurationSeconds, profile, cuts, count);
                var tag = plan.ToString().ToLowerInvariant();
                variants.Add(await EvaluateAsync($"{tag}-n{count}", plan.ToString(), windows, cache, info, referenceBppf, profile, 0.0));
                variants.Add(await EvaluateAsync($"{tag}-n{count}-oran", plan.ToString(), windows, cache, info, referenceBppf, profile,
                    ComplexityProbe.PlanBias(windows, profile)));
            }
        }

        foreach (var (count, length) in new[] { (3, 2.0), (6, 1.0), (12, 0.5), (2, 3.0), (1, 6.0) })
        {
            var windows = ComplexityProbe.PlanWindows(SamplingPlan.Profile, info.DurationSeconds, profile, cuts, count, length);
            variants.Add(await EvaluateAsync($"butce6-{count}x{length.ToString("0.#", CultureInfo.InvariantCulture)}",
                "Profile", windows, cache, info, referenceBppf, profile, ComplexityProbe.PlanBias(windows, profile)));
        }

        var auto = ComplexityProbe.PlanWindows(SamplingPlan.Profile, info.DurationSeconds, profile, cuts);
        variants.Add(await EvaluateAsync("profile-auto-oran", "Profile", auto, cache, info, referenceBppf, profile,
            ComplexityProbe.PlanBias(auto, profile)));

        return new ClipReport(
            Path.GetFileNameWithoutExtension(clip), info.DurationSeconds, info.Width, info.Height, info.Fps,
            referenceBppf, reference.Frames, cv, profile.Count, cuts.Count,
            packetWatch.Elapsed.TotalSeconds, sceneWatch.Elapsed.TotalSeconds, scanBiasWatch.Elapsed.TotalSeconds,
            scanBias, autoCount, variants);
    }

    private static async Task<VariantReport> EvaluateAsync(
        string name, string plan, IReadOnlyList<SampleWindow> windows, SampleCache cache,
        MediaInfo info, double referenceBppf, IReadOnlyList<double> profile, double correction)
    {
        var samples = new List<(long Bytes, long Frames)>(windows.Count);
        foreach (var window in windows)
            samples.Add(await cache.WindowAsync(window.Start, window.Length));

        var estimate = ComplexityProbe.WeightedBppf(windows, samples, info.Width, info.Height);
        var applied = correction > 0 ? correction : 1.0;
        var corrected = estimate / applied;

        return new VariantReport(
            name, plan, windows.Count, windows.Count > 0 ? windows[0].Length : 0,
            windows.Sum(w => w.Length), corrected,
            referenceBppf > 0 ? corrected / referenceBppf - 1.0 : double.NaN,
            ComplexityProbe.PlanBias(windows, profile), applied,
            windows.Select(w => w.Start).ToList());
    }

    private static double Bppf((long Bytes, long Frames) sample, int width, int height)
        => sample.Frames > 0 ? sample.Bytes * 8.0 / ((double)width * height * sample.Frames) : 0.0;

    private sealed class SampleCache(string path, MediaInfo info, int threads)
    {
        private readonly Dictionary<string, (long Bytes, long Frames)> cache = new();

        public Task<(long Bytes, long Frames)> WholeAsync() => WindowAsync(0, info.DurationSeconds);

        public async Task<(long Bytes, long Frames)> WindowAsync(double start, double length)
        {
            var key = FormattableString.Invariant($"{start:0.###}/{length:0.###}");
            if (cache.TryGetValue(key, out var hit)) return hit;
            var measured = await EncodeAsync(path, start, length, threads);
            cache[key] = measured;
            return measured;
        }
    }

    private static async Task<(long Bytes, long Frames)> EncodeAsync(string path, double start, double length, int threads)
    {
        var stats = Path.Combine(Path.GetTempPath(), $"t103_{Guid.NewGuid():N}.txt");
        try
        {
            var args = new List<string>
            {
                "-hide_banner", "-nostdin", "-v", "error", "-y", "-vstats_file", stats,
                "-ss", start.ToString("0.###", CultureInfo.InvariantCulture),
                "-t", length.ToString("0.###", CultureInfo.InvariantCulture),
                "-i", path, "-an", "-sn", "-dn",
                "-c:v", "libx264", "-crf", Crf.ToString(CultureInfo.InvariantCulture),
                "-preset", Preset, "-threads", threads.ToString(CultureInfo.InvariantCulture),
                "-f", "null", "-"
            };

            using var process = new Process { StartInfo = StartInfo(ToolLocator.Ffmpeg, args) };
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
            if (process.ExitCode != 0) throw new InvalidOperationException(await stderr);

            return ComplexityProbe.ParseVstats(await File.ReadAllTextAsync(stats), 0.0);
        }
        finally
        {
            try { File.Delete(stats); } catch { }
        }
    }

    private static async Task<IReadOnlyList<PacketSample>> ReadAllPacketsAsync(string path)
    {
        var args = new[]
        {
            "-v", "error", "-select_streams", "v:0",
            "-show_entries", "packet=pts_time,size", "-of", "csv=p=0", path
        };

        using var process = new Process { StartInfo = StartInfo(ToolLocator.Ffprobe, args) };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        return ComplexityProbe.ParsePackets(await stdout);
    }

    private static async Task<int> PlanAsync(string[] args)
    {
        if (args.Length < 2) return Unknown("plan");
        var info = await FfprobeClient.ProbeAsync(args[1]);
        var packets = await ReadAllPacketsAsync(args[1]);
        var profile = ComplexityProbe.SecondBitProfile(packets, info.DurationSeconds);
        var cv = ComplexityProbe.Heterogeneity(profile);
        var windows = ComplexityProbe.PlanWindows(SamplingPlan.Profile, info.DurationSeconds, profile);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            info.DurationSeconds,
            Heterogeneity = cv,
            Count = ComplexityProbe.PlanWindowCount(info.DurationSeconds, cv),
            Windows = windows,
            Bias = ComplexityProbe.PlanBias(windows, profile)
        }, Json));
        return 0;
    }

    private static async Task<int> CostAsync(string[] args)
    {
        if (args.Length < 2) return Unknown("maliyet");
        var clips = args[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var threads = Threads(args);
        var repeats = int.TryParse(Option(args, "--tekrar"), out var r) && r > 0 ? r : 3;
        var rows = new List<object>();

        foreach (var clip in clips)
        {
            var info = await FfprobeClient.ProbeAsync(clip);
            var packetMs = new List<double>();
            var sceneMs = new List<double>();
            var scanMs = new List<double>();
            var oldSampleMs = new List<double>();
            var newSampleMs = new List<double>();
            var oldEncodes = 0;
            var newEncodes = 0;

            for (var i = 0; i < repeats; i++)
            {
                var watch = Stopwatch.StartNew();
                var packets = await ReadAllPacketsAsync(clip);
                watch.Stop();
                packetMs.Add(watch.Elapsed.TotalMilliseconds);
                var profile = ComplexityProbe.SecondBitProfile(packets, info.DurationSeconds);

                watch.Restart();
                await SceneDetector.ScanAsync(clip);
                watch.Stop();
                sceneMs.Add(watch.Elapsed.TotalMilliseconds);

                watch.Restart();
                await ComplexityProbe.ScanBiasAsync(info, SpeedMode.Quality, default);
                watch.Stop();
                scanMs.Add(watch.Elapsed.TotalMilliseconds);

                var fixedWindows = ComplexityProbe.PlanWindows(SamplingPlan.Fixed, info.DurationSeconds);
                watch.Restart();
                foreach (var window in fixedWindows) await EncodeAsync(clip, window.Start, window.Length, threads);
                watch.Stop();
                oldSampleMs.Add(watch.Elapsed.TotalMilliseconds);
                oldEncodes = fixedWindows.Count + ComplexityProbe.ScanPoints(info.DurationSeconds).Count;

                var planned = ComplexityProbe.PlanWindows(SamplingPlan.Profile, info.DurationSeconds, profile);
                watch.Restart();
                foreach (var window in planned) await EncodeAsync(clip, window.Start, window.Length, threads);
                watch.Stop();
                newSampleMs.Add(watch.Elapsed.TotalMilliseconds);
                newEncodes = planned.Count;
            }

            rows.Add(new
            {
                Clip = Path.GetFileNameWithoutExtension(clip),
                info.DurationSeconds,
                PacketReadMs = Median(packetMs),
                SceneScanMs = Median(sceneMs),
                ScanBiasMs = Median(scanMs),
                OldWindowSampleMs = Median(oldSampleMs),
                NewWindowSampleMs = Median(newSampleMs),
                OldTotalMs = Median(oldSampleMs) + Median(scanMs),
                NewTotalMs = Median(newSampleMs) + Median(packetMs),
                OldEncodeCalls = oldEncodes,
                NewEncodeCalls = newEncodes,
                Repeats = repeats,
                Threads = threads
            });
            Console.WriteLine(JsonSerializer.Serialize(rows[^1], Json));
        }

        return 0;
    }

    private static ProcessStartInfo StartInfo(string fileName, IEnumerable<string> args)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0) return 0;
        return sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2.0;
    }
}
