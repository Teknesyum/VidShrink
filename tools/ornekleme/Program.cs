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

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("ornekleme sapma <klip,...> --out <dosya> [--threads 4] [--boy 2,1,0.5]");
            Console.WriteLine("ornekleme maliyet <klip,...> [--threads 4] [--tekrar 3]");
            Console.WriteLine("ornekleme plan <klip>");
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
        IReadOnlyList<double> Starts,
        IReadOnlyList<double> WindowBppf,
        IReadOnlyList<double> Weights);

    private sealed record ClipReport(
        string Clip,
        double DurationSeconds,
        int Width,
        int Height,
        double Fps,
        double WholeFileBppf,
        double CensusBppf,
        int CensusTiles,
        double WindowDomainOffset,
        double Heterogeneity,
        int ProfileSeconds,
        int SceneCuts,
        double PacketReadSeconds,
        double SceneScanSeconds,
        double ScanBiasSeconds,
        double ScanBias,
        int AutoWindowCount,
        IReadOnlyList<double> SecondBits,
        IReadOnlyList<VariantReport> Variants);

    private static async Task<int> DeviationAsync(string[] args)
    {
        if (args.Length < 2) return Unknown("sapma");
        var clips = args[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var threads = Threads(args);
        var lengths = (Option(args, "--boy") ?? "2")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        var outPath = Option(args, "--out") ?? Path.Combine(".calisma", "t103", "sapma.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);

        var results = new List<ClipReport>();
        foreach (var clip in clips)
        {
            var report = await MeasureClipAsync(clip, threads, lengths);
            results.Add(report);
            Console.WriteLine(Line(report));
            await File.WriteAllTextAsync(outPath, JsonSerializer.Serialize(results, Json));
        }

        Console.WriteLine($"yazildi: {outPath}");
        return 0;
    }

    private static string Line(ClipReport report)
    {
        var today = report.Variants.First(v => v.Name == "bugun-scanpoints");
        var best = report.Variants.Where(v => v.Plan != "Fixed").OrderBy(v => Math.Abs(v.Deviation)).First();
        return $"{report.Clip} sure={report.DurationSeconds:0.#} cv={report.Heterogeneity:0.000} " +
               $"nufus-bppf={report.CensusBppf:0.00000} alan-kaymasi={report.WindowDomainOffset:P1} " +
               $"bugun={today.Deviation:P2} en-iyi={best.Name}:{best.Deviation:P2}";
    }

    private static async Task<ClipReport> MeasureClipAsync(string clip, int threads, IReadOnlyList<double> lengths)
    {
        var info = await FfprobeClient.ProbeAsync(clip);
        var cache = new SampleCache(clip, threads);
        var duration = info.DurationSeconds;

        var whole = await cache.GetAsync(0, duration);
        var wholeBppf = Bppf(whole, info.Width, info.Height);

        foreach (var length in lengths)
            for (var start = 0.0; start + length <= duration + 1e-9; start += 1.0)
                await cache.GetAsync(start, length);

        var tiles = new List<(long, long)>();
        for (var start = 0.0; start + 2.0 <= duration + 1e-9; start += 2.0)
            tiles.Add(await cache.GetAsync(start, 2.0));
        var censusBppf = Bppf((tiles.Sum(t => t.Item1), tiles.Sum(t => t.Item2)), info.Width, info.Height);

        var packetWatch = Stopwatch.StartNew();
        var packets = await ReadAllPacketsAsync(clip);
        packetWatch.Stop();
        var profile = ComplexityProbe.SecondBitProfile(packets, duration);
        var cv = ComplexityProbe.Heterogeneity(profile);

        var sceneWatch = Stopwatch.StartNew();
        var scan = await SceneDetector.ScanAsync(clip);
        sceneWatch.Stop();
        var cuts = scan.Ok
            ? SceneMap.DerivedCutTimes(scan.Candidates, duration, Math.Max(scan.Frames.Count / duration, 1.0), ThresholdRule.Measured)
            : Array.Empty<double>();

        var scanBiasWatch = Stopwatch.StartNew();
        var scanBias = await ComplexityProbe.ScanBiasAsync(info, SpeedMode.Quality, default);
        scanBiasWatch.Stop();

        cache.Flush();
        var variants = new List<VariantReport>();

        async Task AddAsync(string name, string plan, IReadOnlyList<SampleWindow> windows, double correction)
        {
            var snapped = windows.Select(w => w with { Start = Snap(w.Start, w.Length, duration) }).ToList();
            var samples = new List<(long Bytes, long Frames)>(snapped.Count);
            foreach (var window in snapped) samples.Add(await cache.GetAsync(window.Start, window.Length));

            var estimate = ComplexityProbe.WeightedBppf(snapped, samples, info.Width, info.Height);
            var applied = correction > 0 ? correction : 1.0;
            var corrected = estimate / applied;
            variants.Add(new VariantReport(
                name, plan, snapped.Count, snapped.Count > 0 ? snapped[0].Length : 0,
                snapped.Sum(w => w.Length), corrected,
                censusBppf > 0 ? corrected / censusBppf - 1.0 : double.NaN,
                ComplexityProbe.PlanBias(snapped, profile), applied,
                snapped.Select(w => w.Start).ToList(),
                samples.Select(x => Bppf(x, info.Width, info.Height)).ToList(),
                snapped.Select(w => w.Weight).ToList()));
        }

        var fixedWindows = ComplexityProbe.PlanWindows(SamplingPlan.Fixed, duration);
        await AddAsync("bugun-duzeltmesiz", "Fixed", fixedWindows, 0.0);
        await AddAsync("bugun-scanpoints", "Fixed", fixedWindows,
            ComplexityProfile.IsTrustedBias(scanBias) ? scanBias : 0.0);
        await AddAsync("bugun-paket-orani", "Fixed", fixedWindows, ComplexityProbe.PlanBias(fixedWindows, profile));

        foreach (var plan in new[] { SamplingPlan.Profile, SamplingPlan.Scene })
        {
            var tag = plan.ToString().ToLowerInvariant();
            foreach (var count in new[] { 2, 3, 4, 5, 6, 8, 10, 12, 16, 24 })
            {
                var windows = ComplexityProbe.PlanWindows(plan, duration, profile, cuts, count);
                if (windows.Count != count) continue;
                await AddAsync($"{tag}-n{count}", plan.ToString(), windows, 0.0);
                await AddAsync($"{tag}-n{count}-oran", plan.ToString(), windows, ComplexityProbe.PlanBias(windows, profile));
            }
        }

        foreach (var length in lengths)
        {
            var count = (int)Math.Round(12.0 / length);
            var windows = ComplexityProbe.PlanWindows(SamplingPlan.Profile, duration, profile, cuts, count, length);
            if (windows.Count == 0) continue;
            await AddAsync($"butce12-{windows.Count}x{length.ToString("0.#", CultureInfo.InvariantCulture)}", "Profile", windows, 0.0);
        }

        var auto = ComplexityProbe.PlanWindows(SamplingPlan.Profile, duration, profile, cuts);
        await AddAsync("profile-auto", "Profile", auto, 0.0);

        return new ClipReport(
            Path.GetFileNameWithoutExtension(clip), duration, info.Width, info.Height, info.Fps,
            wholeBppf, censusBppf, tiles.Count,
            wholeBppf > 0 ? censusBppf / wholeBppf - 1.0 : double.NaN,
            cv, profile.Count, cuts.Count,
            packetWatch.Elapsed.TotalSeconds, sceneWatch.Elapsed.TotalSeconds, scanBiasWatch.Elapsed.TotalSeconds,
            scanBias, ComplexityProbe.PlanWindowCount(duration, cv), profile, variants);
    }

    private static double Snap(double start, double length, double duration)
        => Math.Clamp(Math.Round(start, MidpointRounding.AwayFromZero), 0, Math.Max(0, Math.Floor(duration - length)));

    private static double Bppf((long Bytes, long Frames) sample, int width, int height)
        => sample.Frames > 0 ? sample.Bytes * 8.0 / ((double)width * height * sample.Frames) : 0.0;

    private sealed class SampleCache
    {
        private readonly string path;
        private readonly int threads;
        private readonly string store;
        private readonly Dictionary<string, long[]> cache;
        private int unsaved;

        public SampleCache(string path, int threads)
        {
            this.path = path;
            this.threads = threads;
            var dir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".", ".onbellek");
            Directory.CreateDirectory(dir);
            store = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(path)}-crf{Crf:0}-{Preset}-t{threads}.json");
            cache = File.Exists(store)
                ? JsonSerializer.Deserialize<Dictionary<string, long[]>>(File.ReadAllText(store)) ?? new()
                : new();
        }

        public int Encodes { get; private set; }

        public int Reused { get; private set; }

        public async Task<(long Bytes, long Frames)> GetAsync(double start, double length)
        {
            var key = FormattableString.Invariant($"{start:0.###}/{length:0.###}");
            if (cache.TryGetValue(key, out var hit) && hit.Length == 2)
            {
                Reused++;
                return (hit[0], hit[1]);
            }

            var measured = await EncodeAsync(path, start, length, threads);
            Encodes++;
            cache[key] = new[] { measured.Bytes, measured.Frames };
            if (++unsaved >= 20) Flush();
            return measured;
        }

        public void Flush()
        {
            unsaved = 0;
            File.WriteAllText(store, JsonSerializer.Serialize(cache));
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
            var oldSeconds = 0.0;
            var newSeconds = 0.0;

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
                oldSeconds = fixedWindows.Sum(w => w.Length) + ComplexityProbe.ScanPoints(info.DurationSeconds).Count * 1.0;

                var planned = ComplexityProbe.PlanWindows(SamplingPlan.Profile, info.DurationSeconds, profile);
                watch.Restart();
                foreach (var window in planned) await EncodeAsync(clip, window.Start, window.Length, threads);
                watch.Stop();
                newSampleMs.Add(watch.Elapsed.TotalMilliseconds);
                newEncodes = planned.Count;
                newSeconds = planned.Sum(w => w.Length);
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
                OldEncodedSeconds = oldSeconds,
                NewEncodedSeconds = newSeconds,
                Repeats = repeats,
                Threads = threads
            });
            Console.WriteLine(JsonSerializer.Serialize(rows[^1], Json));
        }

        var outPath = Option(args, "--out");
        if (outPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
            await File.WriteAllTextAsync(outPath, JsonSerializer.Serialize(rows, Json));
        }
        return 0;
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
