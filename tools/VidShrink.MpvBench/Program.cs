using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace VidShrink.MpvBench;

internal static class Program
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            return Usage();
        }

        var o = Args.Parse(args.Skip(1));
        try
        {
            return args[0] switch
            {
                "fps" => RunFps(o),
                "timed" => RunTimed(o),
                "suite" => Suite.Run(o),
                "summarize" => Summary.Run(o),
                _ => Usage(),
            };
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("VidShrink.MpvBench fps|timed --file F [--hwdec no|auto-copy] [--lib libmpv-2.dll]");
        Console.Error.WriteLine("  fps:   --cap 25 (seconds of untimed rendering)");
        Console.Error.WriteLine("  timed: --warm 2 --window 15 --seeks 20 --seed 1");
        Console.Error.WriteLine("VidShrink.MpvBench suite --media DIR --out DIR [--reps 3] [--files a.mp4,b.mp4] [--hwdecs no,auto-copy]");
        Console.Error.WriteLine("VidShrink.MpvBench summarize --in runs.jsonl --out summary.md");
        Console.Error.WriteLine("libmpv path: --lib, else the VIDSHRINK_LIBMPV environment variable.");
        return 2;
    }

    private static (double LoadMs, ulong Api) LoadLibrary(Dictionary<string, string> o)
    {
        var lib = Args.LibPath(o);
        var t = Session.Now;
        Native.Load(lib);
        var api = Native.mpv_client_api_version();
        return (Session.Now - t, api);
    }

    private static Dictionary<string, object?> Common(Session s, Dictionary<string, string> o, string mode, List<(string, string)> options)
    {
        return new Dictionary<string, object?>
        {
            ["mode"] = mode,
            ["file"] = Path.GetFileName(o["file"]),
            ["hwdec"] = Args.Get(o, "hwdec", "no"),
            ["rep"] = int.Parse(Args.Get(o, "rep", "0"), Inv),
            ["options"] = string.Join(" ", options.Select(x => $"{x.Item1}={x.Item2}")),
            ["mpvVersion"] = s.GetString("mpv-version"),
            ["ffmpegVersion"] = s.GetString("ffmpeg-version"),
            ["swFast"] = s.GetString("sw-fast"),
            ["swsFast"] = s.GetString("sws-fast"),
            ["swsAllowZimg"] = s.GetString("sws-allow-zimg"),
            ["vdLavcThreads"] = s.GetString("vd-lavc-threads"),
            ["zimgThreads"] = s.GetString("zimg-threads"),
            ["zimgFast"] = s.GetString("zimg-fast"),
            ["width"] = s.Width,
            ["height"] = s.Height,
            ["stride"] = s.Stride,
            ["bufferAlign64"] = s.BufferAddress % 64 == 0,
            ["hwdecCurrent"] = s.Info.GetValueOrDefault("hwdec-current"),
            ["videoCodec"] = s.Info.GetValueOrDefault("video-codec"),
            ["videoFormat"] = s.Info.GetValueOrDefault("video-format"),
            ["containerFps"] = s.Info.GetValueOrDefault("container-fps"),
            ["duration"] = s.Info.GetValueOrDefault("duration"),
            ["currentAo"] = s.Info.GetValueOrDefault("current-ao"),
            ["currentVo"] = s.Info.GetValueOrDefault("current-vo"),
            ["logicalCores"] = Environment.ProcessorCount,
        };
    }

    private static void Finish(Session s, Dictionary<string, object?> r, MemorySampler mem, long baseWs)
    {
        mem.Stop();
        using var proc = Process.GetCurrentProcess();
        r["baseWsMb"] = baseWs / 1048576.0;
        r["peakWsSampledMb"] = mem.Max / 1048576.0;
        r["peakWsProcessMb"] = proc.PeakWorkingSet64 / 1048576.0;
        r["frameInfoRc"] = s.FrameInfoRc;
        r["renderErrors"] = s.RenderErrors;
        r["lastRenderRc"] = s.LastRenderRc;
        r["mpvLog"] = s.Log.ToArray();
        foreach (var line in s.Log)
        {
            Console.Error.WriteLine("mpv: " + line);
        }

        Console.WriteLine(JsonSerializer.Serialize(r));
    }

    private static int RunFps(Dictionary<string, string> o)
    {
        var (loadMs, api) = LoadLibrary(o);
        var file = o["file"];
        var hwdec = Args.Get(o, "hwdec", "no");
        var capMs = Args.D(o, "cap", 25) * 1000;
        long baseWs;
        using (var p = Process.GetCurrentProcess())
        {
            baseWs = p.WorkingSet64;
        }

        var mem = new MemorySampler();
        var options = new List<(string, string)>
        {
            ("vo", "libmpv"), ("untimed", "yes"), ("audio", "no"), ("hwdec", hwdec),
            ("framedrop", "no"), ("keep-open", "yes"), ("terminal", "no"),
        };

        var sys0 = Cpu.SystemTimes();
        var t0 = Session.Now;
        using var s = new Session(options);
        s.Command("loadfile", file);
        double first = -1, last = -1;
        var frames = 0;
        var redraws = 0;
        var renderMs = new List<double>();
        var stop = "timeout";
        var hardStop = t0 + capMs + 60000;
        while (true)
        {
            Session.WaitUpdate(20);
            var r = s.Step(false);
            if (r.Rendered)
            {
                if (r.IsNew)
                {
                    frames++;
                    if (first < 0)
                    {
                        first = r.EndMs;
                    }

                    last = r.EndMs;
                    renderMs.Add(r.EndMs - r.StartMs);
                }
                else
                {
                    redraws++;
                }
            }

            var now = Session.Now;
            if (first >= 0 && now - first >= capMs)
            {
                stop = "cap";
                break;
            }

            if (s.Eof && !r.Rendered)
            {
                stop = "eof";
                break;
            }

            if (now > hardStop)
            {
                break;
            }
        }

        var res = Common(s, o, "fps", options);
        var seconds = (last - first) / 1000.0;
        res["apiVersion"] = api;
        res["dllLoadMs"] = loadMs;
        res["startupMs"] = first - t0;
        res["frames"] = frames;
        res["seconds"] = seconds;
        res["fps"] = frames > 1 ? (frames - 1) / seconds : 0;
        res["renderMsMedian"] = Stats.Median(renderMs);
        res["renderMsP90"] = Stats.Percentile(renderMs, 0.9);
        res["renderMsMean"] = renderMs.Count > 0 ? renderMs.Average() : double.NaN;
        res["renderMsMax"] = renderMs.Count > 0 ? renderMs.Max() : double.NaN;
        res["redraws"] = redraws;
        res["stopReason"] = stop;
        res["sysBusyPct"] = Cpu.BusyPct(sys0, Cpu.SystemTimes());
        res["dropFrame"] = s.GetLong("frame-drop-count");
        res["dropDecoder"] = s.GetLong("decoder-frame-drop-count");
        res["estimatedFrameNumber"] = s.GetLong("estimated-frame-number");
        res["timePosAtStop"] = s.GetDouble("time-pos");
        if (o.TryGetValue("dump", out var dump))
        {
            s.Dump(dump);
            res["dump"] = dump;
        }

        Finish(s, res, mem, baseWs);
        return 0;
    }

    private static int RunTimed(Dictionary<string, string> o)
    {
        var (loadMs, api) = LoadLibrary(o);
        var file = o["file"];
        var hwdec = Args.Get(o, "hwdec", "no");
        var warmMs = Args.D(o, "warm", 2) * 1000;
        var windowMs = Args.D(o, "window", 15) * 1000;
        var seekCount = (int)Args.D(o, "seeks", 20);
        var seed = (int)Args.D(o, "seed", 1);
        var cycleHz = Cpu.CalibrateHz();
        ulong cyc0 = 0, cyc1 = 0;
        (long Idle, long Total) sysW0 = default, sysW1 = default;
        long baseWs;
        using (var p = Process.GetCurrentProcess())
        {
            baseWs = p.WorkingSet64;
        }

        var mem = new MemorySampler();
        var options = new List<(string, string)>
        {
            ("vo", "libmpv"), ("ao", "null"), ("hwdec", hwdec), ("keep-open", "yes"), ("terminal", "no"),
        };

        var t0 = Session.Now;
        using var s = new Session(options);
        s.Command("loadfile", file);

        var phase = 0;
        double first = -1, w0 = 0, w1 = 0, nextSeekAt = 0, tCmd = 0;
        TimeSpan cpu0 = default, cpu1 = default;
        long drop0 = 0, dec0 = 0, drop1 = 0, dec1 = 0;
        int frames = 0, frames0 = 0, frames1 = 0, seekEv0 = 0, stale = 0, idx = 0;
        var pending = false;
        var targets = new List<double>();
        var seeks = new List<Dictionary<string, object?>>();
        var hardStop = t0 + warmMs + windowMs + seekCount * 6000 + 60000;
        var stop = "timeout";
        while (Session.Now < hardStop)
        {
            Session.WaitUpdate(20);
            var seenBefore = pending && s.SeekEvents > seekEv0;
            var r = s.Step(true);
            if (r.Rendered && r.IsNew)
            {
                frames++;
                if (first < 0)
                {
                    first = r.EndMs;
                }
            }

            var now = Session.Now;
            if (phase == 0 && first >= 0)
            {
                phase = 1;
            }

            if (phase == 1 && now - first >= warmMs)
            {
                using var p = Process.GetCurrentProcess();
                cpu0 = p.TotalProcessorTime;
                cyc0 = Cpu.ProcessCycles();
                sysW0 = Cpu.SystemTimes();
                w0 = Session.Now;
                frames0 = frames;
                drop0 = s.GetLong("frame-drop-count");
                dec0 = s.GetLong("decoder-frame-drop-count");
                phase = 2;
            }
            else if (phase == 2 && now - w0 >= windowMs)
            {
                using var p = Process.GetCurrentProcess();
                cpu1 = p.TotalProcessorTime;
                cyc1 = Cpu.ProcessCycles();
                sysW1 = Cpu.SystemTimes();
                w1 = Session.Now;
                frames1 = frames;
                drop1 = s.GetLong("frame-drop-count");
                dec1 = s.GetLong("decoder-frame-drop-count");
                var duration = s.GetDouble("duration");
                var rng = new Random(seed);
                for (var i = 0; i < seekCount; i++)
                {
                    targets.Add(Math.Round(1.0 + rng.NextDouble() * (duration - 4.0), 3));
                }

                nextSeekAt = Session.Now + 300;
                phase = 3;
            }
            else if (phase == 3)
            {
                if (pending)
                {
                    if (r.Rendered && r.IsNew)
                    {
                        if (seenBefore)
                        {
                            var latency = r.EndMs - tCmd;
                            var timePos = s.GetDouble("time-pos");
                            seeks.Add(new Dictionary<string, object?>
                            {
                                ["target"] = targets[idx],
                                ["latencyMs"] = latency,
                                ["renderStartMs"] = r.StartMs - tCmd,
                                ["timePos"] = timePos,
                                ["staleFrames"] = stale,
                                ["tCmd"] = tCmd,
                            });
                            pending = false;
                            idx++;
                            nextSeekAt = Session.Now + 400;
                        }
                        else
                        {
                            stale++;
                        }
                    }
                    else if (now - tCmd > 5000)
                    {
                        seeks.Add(new Dictionary<string, object?> { ["target"] = targets[idx], ["latencyMs"] = null, ["staleFrames"] = stale, ["tCmd"] = tCmd });
                        pending = false;
                        idx++;
                        nextSeekAt = Session.Now + 400;
                    }
                }
                else if (now >= nextSeekAt)
                {
                    if (idx >= seekCount)
                    {
                        stop = "done";
                        break;
                    }

                    stale = 0;
                    seekEv0 = s.SeekEvents;
                    tCmd = Session.Now;
                    s.CommandAsync((ulong)(100 + idx), "seek", targets[idx].ToString("F3", Inv), "absolute+exact");
                    pending = true;
                }
            }
        }

        var sysEnd = Cpu.SystemTimes();
        var restarts = s.RestartTimes.ToArray();
        var seekEvents = s.SeekEventTimes.ToArray();
        for (var i = 0; i < seeks.Count; i++)
        {
            var tc = (double)seeks[i]["tCmd"]!;
            var next = i + 1 < seeks.Count ? (double)seeks[i + 1]["tCmd"]! : double.MaxValue;
            var rs = restarts.Where(t => t > tc && t < next).DefaultIfEmpty(double.NaN).First();
            var se = seekEvents.Where(t => t > tc && t < next).DefaultIfEmpty(double.NaN).First();
            seeks[i]["restartEventMs"] = rs - tc;
            seeks[i]["seekEventMs"] = se - tc;
            seeks[i].Remove("tCmd");
        }

        var lat = seeks.Where(x => x["latencyMs"] is double).Select(x => (double)x["latencyMs"]!).ToList();
        var res = Common(s, o, "timed", options);
        var wallS = (w1 - w0) / 1000.0;
        var cpuS = (cpu1 - cpu0).TotalSeconds;
        res["apiVersion"] = api;
        res["dllLoadMs"] = loadMs;
        res["startupMs"] = first - t0;
        res["stopReason"] = stop;
        res["windowSeconds"] = wallS;
        res["windowFrames"] = frames1 - frames0;
        res["windowFps"] = (frames1 - frames0) / wallS;
        res["windowDropFrame"] = drop1 - drop0;
        res["windowDropDecoder"] = dec1 - dec0;
        var cycS = (cyc1 - cyc0) / cycleHz;
        res["cycleHz"] = cycleHz;
        res["cpuCycleSeconds"] = cycS;
        res["cpuPctOfAllCores"] = cycS / wallS / Environment.ProcessorCount * 100;
        res["cpuPctOfOneCore"] = cycS / wallS * 100;
        res["cpuTickSeconds"] = cpuS;
        res["cpuTickPctOfAllCores"] = cpuS / wallS / Environment.ProcessorCount * 100;
        res["sysBusyPctWindow"] = Cpu.BusyPct(sysW0, sysW1);
        res["sysBusyPctSeeks"] = Cpu.BusyPct(sysW1, sysEnd);
        res["seekOk"] = lat.Count;
        res["seekFailed"] = seeks.Count - lat.Count;
        res["seekMedianMs"] = Stats.Median(lat);
        res["seekP90Ms"] = Stats.Percentile(lat, 0.9);
        res["seeks"] = seeks;
        Finish(s, res, mem, baseWs);
        return 0;
    }
}

internal static class Args
{
    public static Dictionary<string, string> Parse(IEnumerable<string> args)
    {
        var d = new Dictionary<string, string>();
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].StartsWith("--", StringComparison.Ordinal) && i + 1 < list.Count)
            {
                d[list[i][2..]] = list[++i];
            }
        }

        return d;
    }

    public static string Get(Dictionary<string, string> o, string key, string fallback) => o.TryGetValue(key, out var v) ? v : fallback;

    public static double D(Dictionary<string, string> o, string key, double fallback) =>
        o.TryGetValue(key, out var v) ? double.Parse(v, CultureInfo.InvariantCulture) : fallback;

    public static string LibPath(Dictionary<string, string> o)
    {
        var path = o.TryGetValue("lib", out var v) ? v : Environment.GetEnvironmentVariable("VIDSHRINK_LIBMPV");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            throw new FileNotFoundException("libmpv not found; pass --lib or set VIDSHRINK_LIBMPV", path);
        }

        return Path.GetFullPath(path);
    }
}

internal static class Cpu
{
    [DllImport("kernel32.dll")]
    private static extern bool QueryProcessCycleTime(IntPtr process, out ulong cycles);

    [DllImport("kernel32.dll")]
    private static extern bool QueryThreadCycleTime(IntPtr thread, out ulong cycles);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    public static (long Idle, long Total) SystemTimes()
    {
        GetSystemTimes(out var idle, out var kernel, out var user);
        return (idle, kernel + user);
    }

    public static double BusyPct((long Idle, long Total) a, (long Idle, long Total) b)
    {
        var total = b.Total - a.Total;
        return total <= 0 ? double.NaN : (1.0 - (double)(b.Idle - a.Idle) / total) * 100;
    }

    public static ulong ProcessCycles()
    {
        QueryProcessCycleTime(GetCurrentProcess(), out var c);
        return c;
    }

    public static double CalibrateHz()
    {
        var best = 0.0;
        for (var i = 0; i < 3; i++)
        {
            QueryThreadCycleTime(GetCurrentThread(), out var c0);
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 200)
            {
            }

            QueryThreadCycleTime(GetCurrentThread(), out var c1);
            best = Math.Max(best, (c1 - c0) / sw.Elapsed.TotalSeconds);
        }

        return best;
    }
}

internal sealed class MemorySampler
{
    private readonly Thread _thread;
    private volatile bool _stop;

    public long Max { get; private set; }

    public MemorySampler()
    {
        _thread = new Thread(() =>
        {
            using var p = Process.GetCurrentProcess();
            while (!_stop)
            {
                p.Refresh();
                if (p.WorkingSet64 > Max)
                {
                    Max = p.WorkingSet64;
                }

                Thread.Sleep(50);
            }
        }) { IsBackground = true };
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
        _thread.Join();
    }
}

internal static class Stats
{
    public static double Median(IReadOnlyCollection<double> values)
    {
        if (values.Count == 0)
        {
            return double.NaN;
        }

        var a = values.OrderBy(x => x).ToArray();
        return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2;
    }

    public static double Percentile(IReadOnlyCollection<double> values, double p)
    {
        if (values.Count == 0)
        {
            return double.NaN;
        }

        var a = values.OrderBy(x => x).ToArray();
        var rank = (int)Math.Ceiling(p * a.Length) - 1;
        return a[Math.Clamp(rank, 0, a.Length - 1)];
    }
}
