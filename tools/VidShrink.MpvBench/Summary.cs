using System.Globalization;
using System.Text;
using System.Text.Json;

namespace VidShrink.MpvBench;

internal static class Summary
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(Dictionary<string, string> o)
    {
        var runs = File.ReadAllLines(o["in"])
            .Where(l => l.StartsWith('{'))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .ToList();
        var sb = new StringBuilder();
        var fps = runs.Where(r => Str(r, "mode") == "fps").ToList();
        var timed = runs.Where(r => Str(r, "mode") == "timed").ToList();

        sb.AppendLine("## Max SW Render Rate (untimed)");
        sb.AppendLine();
        sb.AppendLine("| file | hwdec | hwdec-current | fps per rep | median | min | max | threshold | result | render ms/frame (median of reps) | frames per rep | stop | drops (vo/dec) | system busy % per rep |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var g in Groups(fps))
        {
            var v = g.Select(r => Num(r, "fps")).ToList();
            var h = Num(g[0], "height");
            var threshold = h >= 2000 ? 24 : 55;
            var med = Stats.Median(v);
            sb.AppendLine($"| {g.Key.File} | {g.Key.Hw} | {Distinct(g, "hwdecCurrent")} | {Raw(v)} | {F(med)} | {F(v.Min())} | {F(v.Max())} | >= {threshold} | {(med >= threshold ? "PASS" : "FAIL")} | {F(Stats.Median(g.Select(r => Num(r, "renderMsMedian")).ToList()), 2)} | {string.Join(" / ", g.Select(r => Num(r, "frames").ToString(Inv)))} | {Distinct(g, "stopReason")} | {string.Join(" / ", g.Select(r => $"{Num(r, "dropFrame")}/{Num(r, "dropDecoder")}"))} | {Raw(g.Select(r => Num(r, "sysBusyPct")).ToList())} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Seek Latency (timed, `seek <t> absolute+exact`, command to first rendered frame)");
        sb.AppendLine();
        sb.AppendLine("| file | hwdec | seeks ok/failed | median of all | p90 of all | min | max | per-rep medians | threshold | result | restart-event median | time-pos - target ms (min..max) | stale frames | system busy % during seeks per rep |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var g in Groups(timed))
        {
            var seeks = g.SelectMany(r => r.GetProperty("seeks").EnumerateArray()).ToList();
            var lat = seeks.Where(x => x.GetProperty("latencyMs").ValueKind == JsonValueKind.Number).Select(x => x.GetProperty("latencyMs").GetDouble()).ToList();
            var restart = seeks.Where(x => x.TryGetProperty("restartEventMs", out var e) && e.ValueKind == JsonValueKind.Number).Select(x => x.GetProperty("restartEventMs").GetDouble()).ToList();
            var dev = seeks.Where(x => x.TryGetProperty("timePos", out var e) && e.ValueKind == JsonValueKind.Number)
                .Select(x => (x.GetProperty("timePos").GetDouble() - x.GetProperty("target").GetDouble()) * 1000).ToList();
            var stale = seeks.Sum(x => x.GetProperty("staleFrames").GetInt32());
            var med = Stats.Median(lat);
            sb.AppendLine($"| {g.Key.File} | {g.Key.Hw} | {lat.Count}/{seeks.Count - lat.Count} | {F(med)} | {F(Stats.Percentile(lat, 0.9))} | {F(lat.Min())} | {F(lat.Max())} | {Raw(g.Select(r => Num(r, "seekMedianMs")).ToList())} | <= 60 | {(med <= 60 ? "PASS" : "FAIL")} | {F(Stats.Median(restart))} | {F(dev.DefaultIfEmpty(double.NaN).Min())}..{F(dev.DefaultIfEmpty(double.NaN).Max())} | {stale} | {Raw(g.Select(r => Num(r, "sysBusyPctSeeks")).ToList())} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Startup (`mpv_create` to first rendered frame, ms; DLL load excluded)");
        sb.AppendLine();
        sb.AppendLine("| file | hwdec | mode | per rep | median | min | max | DLL load ms (median) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var g in Groups(runs, true))
        {
            var v = g.Select(r => Num(r, "startupMs")).ToList();
            sb.AppendLine($"| {g.Key.File} | {g.Key.Hw} | {g.Key.Mode} | {Raw(v)} | {F(Stats.Median(v))} | {F(v.Min())} | {F(v.Max())} | {F(Stats.Median(g.Select(r => Num(r, "dllLoadMs")).ToList()))} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Memory (process working set, MB; sampled every 50 ms, and PeakWorkingSet64)");
        sb.AppendLine();
        sb.AppendLine("| file | hwdec | mode | sampled peak per rep | median | min | max | PeakWorkingSet64 median | baseline before mpv_create (median) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var g in Groups(runs, true))
        {
            var v = g.Select(r => Num(r, "peakWsSampledMb")).ToList();
            sb.AppendLine($"| {g.Key.File} | {g.Key.Hw} | {g.Key.Mode} | {Raw(v)} | {F(Stats.Median(v))} | {F(v.Min())} | {F(v.Max())} | {F(Stats.Median(g.Select(r => Num(r, "peakWsProcessMb")).ToList()))} | {F(Stats.Median(g.Select(r => Num(r, "baseWsMb")).ToList()))} |");
        }

        sb.AppendLine();
        var cores = runs.Count > 0 ? Num(runs[0], "logicalCores") : double.NaN;
        var maxHz = timed.Count > 0 ? timed.Max(r => Num(r, "cycleHz")) : double.NaN;
        sb.AppendLine($"## CPU (timed playback window; QueryProcessCycleTime cycles / {F(maxHz / 1e6, 0)} MHz, the highest calibrated cycle rate of all runs / wall time; {cores} logical cores)");
        sb.AppendLine();
        sb.AppendLine("| file | hwdec | % of all cores per rep | median | min | max | % of one core (median) | tick-based GetProcessTimes % of all cores (median) | calibrated MHz per rep | window s (median) | frames rendered in window per rep | window fps (median) | drops vo/dec per rep | system busy % in window per rep |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var g in Groups(timed))
        {
            var v = g.Select(r => Num(r, "cpuCycleSeconds") * Num(r, "cycleHz") / maxHz / Num(r, "windowSeconds") / cores * 100).ToList();
            sb.AppendLine($"| {g.Key.File} | {g.Key.Hw} | {Raw(v, 2)} | {F(Stats.Median(v), 2)} | {F(v.Min(), 2)} | {F(v.Max(), 2)} | {F(Stats.Median(v) * cores)} | {F(Stats.Median(g.Select(r => Num(r, "cpuTickPctOfAllCores")).ToList()), 2)} | {string.Join(" / ", g.Select(r => F(Num(r, "cycleHz") / 1e6, 0)))} | {F(Stats.Median(g.Select(r => Num(r, "windowSeconds")).ToList()), 2)} | {string.Join(" / ", g.Select(r => Num(r, "windowFrames").ToString(Inv)))} | {F(Stats.Median(g.Select(r => Num(r, "windowFps")).ToList()))} | {string.Join(" / ", g.Select(r => $"{Num(r, "windowDropFrame")}/{Num(r, "windowDropDecoder")}"))} | {Raw(g.Select(r => Num(r, "sysBusyPctWindow")).ToList())} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Sanity");
        sb.AppendLine();
        sb.AppendLine($"- runs: {runs.Count} (fps {fps.Count}, timed {timed.Count})");
        var rejectedPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(o["in"]))!, "rejected.jsonl");
        var rejectedCount = File.Exists(rejectedPath) ? File.ReadAllLines(rejectedPath).Count(l => l.StartsWith('{')) : 0;
        sb.AppendLine($"- quiet gate: attempts per accepted run {string.Join(", ", runs.GroupBy(r => Num(r, "attempts")).OrderBy(g => g.Key).Select(g => $"{F(g.Key, 0)}x{g.Count()}"))}; runs started without reaching the quiet threshold: {runs.Count(r => Str(r, "quietReached") == "false")}; rejected attempts (rejected.jsonl): {rejectedCount}; pre-run system busy % max {F(runs.Select(r => Num(r, "preRunBusyPct")).DefaultIfEmpty(double.NaN).Max())}");
        sb.AppendLine($"- mpv-version: {string.Join(", ", runs.Select(r => Str(r, "mpvVersion")).Distinct())}");
        sb.AppendLine($"- ffmpeg-version: {string.Join(", ", runs.Select(r => Str(r, "ffmpegVersion")).Distinct())}");
        sb.AppendLine($"- sw-fast: {string.Join(", ", runs.Select(r => Str(r, "swFast")).Distinct())}; sws-fast: {string.Join(", ", runs.Select(r => Str(r, "swsFast")).Distinct())}; sws-allow-zimg: {string.Join(", ", runs.Select(r => Str(r, "swsAllowZimg")).Distinct())}; vd-lavc-threads: {string.Join(", ", runs.Select(r => Str(r, "vdLavcThreads")).Distinct())}");
        sb.AppendLine($"- current-vo: {string.Join(", ", runs.Select(r => Str(r, "currentVo")).Distinct())}; current-ao (timed): {string.Join(", ", timed.Select(r => Str(r, "currentAo")).Distinct())}");
        sb.AppendLine($"- render size / stride / 64-byte aligned buffer: {string.Join(", ", runs.Select(r => $"{Num(r, "width")}x{Num(r, "height")}/{Num(r, "stride")}/{Str(r, "bufferAlign64")}").Distinct())}");
        sb.AppendLine($"- NEXT_FRAME_INFO rc: {string.Join(", ", runs.Select(r => Num(r, "frameInfoRc").ToString(Inv)).Distinct())}; render errors total: {runs.Sum(r => Num(r, "renderErrors"))}");
        sb.AppendLine($"- options fps: {string.Join(" | ", fps.Select(r => Str(r, "options")).Distinct())}");
        sb.AppendLine($"- options timed: {string.Join(" | ", timed.Select(r => Str(r, "options")).Distinct())}");
        sb.AppendLine($"- mpv warnings/errors logged: {runs.Sum(r => r.GetProperty("mpvLog").GetArrayLength())}");
        foreach (var line in runs.SelectMany(r => r.GetProperty("mpvLog").EnumerateArray().Select(x => x.GetString())).Distinct().Take(20))
        {
            sb.AppendLine($"  - `{line}`");
        }

        File.WriteAllText(o["out"], sb.ToString());
        Console.WriteLine(sb.ToString());
        return 0;
    }

    private static IEnumerable<Group> Groups(List<JsonElement> runs, bool byMode = false) =>
        runs.GroupBy(r => (File: Str(r, "file"), Hw: Str(r, "hwdec"), Mode: byMode ? Str(r, "mode") : ""))
            .OrderBy(g => g.Key.File, StringComparer.Ordinal).ThenBy(g => g.Key.Hw == "no" ? 0 : 1).ThenBy(g => g.Key.Mode, StringComparer.Ordinal)
            .Select(g => new Group(g.Key, g.OrderBy(r => Num(r, "rep")).ToList()));

    private sealed class Group : List<JsonElement>
    {
        public (string File, string Hw, string Mode) Key { get; }

        public Group((string, string, string) key, List<JsonElement> items) : base(items) => Key = key;
    }

    private static string Str(JsonElement r, string name) =>
        r.TryGetProperty(name, out var v) ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString()) : "";

    private static double Num(JsonElement r, string name) =>
        r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN;

    private static string Distinct(IEnumerable<JsonElement> g, string name) => string.Join(", ", g.Select(r => Str(r, name)).Distinct());

    private static string F(double v, int digits = 1) => double.IsNaN(v) ? "n/a" : v.ToString("F" + digits, Inv);

    private static string Raw(IEnumerable<double> v, int digits = 1) => string.Join(" / ", v.Select(x => F(x, digits)));
}
