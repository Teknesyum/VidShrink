using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace VidShrink.MpvBench;

internal static class Suite
{
    public static int Run(Dictionary<string, string> o)
    {
        var lib = Args.LibPath(o);
        var media = o["media"];
        var outDir = o["out"];
        var reps = (int)Args.D(o, "reps", 3);
        var files = Args.Get(o, "files", "h264_1080p60.mp4,h264_2160p30.mp4,hevc_1080p60.mp4,hevc_2160p30.mp4").Split(',');
        var hwdecs = Args.Get(o, "hwdecs", "no,auto-copy").Split(',');
        var modes = Args.Get(o, "modes", "fps,timed").Split(',');
        var quietPct = Args.D(o, "quiet-pct", 10);
        var quietWaitMin = Args.D(o, "quiet-wait-min", 30);
        var maxBusyFps = Args.D(o, "max-busy-fps", 30);
        var maxBusyTimed = Args.D(o, "max-busy-timed", 15);
        var retries = (int)Args.D(o, "retries", 10);
        Directory.CreateDirectory(outDir);
        var jsonl = Path.Combine(outDir, "runs.jsonl");
        var rejected = Path.Combine(outDir, "rejected.jsonl");
        var log = Path.Combine(outDir, "runs.log");
        File.WriteAllText(jsonl, "");
        File.WriteAllText(rejected, "");
        File.WriteAllText(log, FormattableString.Invariant($"suite {DateTime.Now:O} lib={lib} reps={reps} quietPct={quietPct} quietWaitMin={quietWaitMin} maxBusyFps={maxBusyFps} maxBusyTimed={maxBusyTimed} retries={retries}{Environment.NewLine}"));

        var total = reps * files.Length * hwdecs.Length * modes.Length;
        var n = 0;
        for (var rep = 1; rep <= reps; rep++)
        {
            foreach (var file in files)
            {
                foreach (var hw in hwdecs)
                {
                    foreach (var mode in modes)
                    {
                        n++;
                        var args = new List<string>
                        {
                            mode, "--lib", lib, "--file", Path.GetFullPath(Path.Combine(media, file)),
                            "--hwdec", hw, "--rep", rep.ToString(), "--seed", rep.ToString(),
                        };
                        foreach (var key in new[] { "cap", "warm", "window", "seeks" })
                        {
                            if (o.TryGetValue(key, out var v))
                            {
                                args.Add("--" + key);
                                args.Add(v);
                            }
                        }

                        var limit = mode == "fps" ? maxBusyFps : maxBusyTimed;
                        for (var attempt = 1; ; attempt++)
                        {
                            var (quiet, waitS, lastBusy) = WaitQuiet(quietPct, quietWaitMin);
                            var (code, stdout, stderr, ms) = Child(args);
                            var lines = stdout.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith('{')).ToList();
                            var busy = lines.Count > 0 ? RunBusy(lines[^1], mode) : double.NaN;
                            var accept = attempt > retries || (!double.IsNaN(busy) && busy <= limit);
                            var tagged = lines.Select(l => l[..^1] + FormattableString.Invariant($",\"attempts\":{attempt},\"quietWaitS\":{waitS:F1},\"quietReached\":{(quiet ? "true" : "false")},\"preRunBusyPct\":{lastBusy:F1}}}")).ToList();
                            File.AppendAllLines(accept ? jsonl : rejected, tagged);
                            File.AppendAllText(log, FormattableString.Invariant($"--- [{n}/{total}] attempt={attempt} wait={waitS:F1}s quiet={quiet} preBusy={lastBusy:F1}% runBusy={busy:F1}% limit={limit} {(accept ? "ACCEPT" : "REJECT")} exit={code} {ms:F0} ms: {string.Join(" ", args)}{Environment.NewLine}{stderr}{Environment.NewLine}"));
                            Console.WriteLine(FormattableString.Invariant($"[{n}/{total}] {mode} {file} hwdec={hw} rep={rep} attempt={attempt} wait={waitS:F0}s busy={busy:F1}% {(accept ? "ACCEPT" : "REJECT")} exit={code} {ms / 1000:F1}s"));
                            if (accept)
                            {
                                break;
                            }
                        }
                    }
                }
            }
        }

        return Summary.Run(new Dictionary<string, string> { ["in"] = jsonl, ["out"] = Path.Combine(outDir, "summary.md") });
    }

    private static (bool Quiet, double WaitS, double LastBusy) WaitQuiet(double pct, double maxMin)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            var a = Cpu.SystemTimes();
            Thread.Sleep(3000);
            var busy = Cpu.BusyPct(a, Cpu.SystemTimes());
            if (busy <= pct)
            {
                return (true, sw.Elapsed.TotalSeconds, busy);
            }

            if (sw.Elapsed.TotalMinutes >= maxMin)
            {
                return (false, sw.Elapsed.TotalSeconds, busy);
            }
        }
    }

    private static double RunBusy(string line, string mode)
    {
        using var doc = JsonDocument.Parse(line);
        var r = doc.RootElement;
        double Get(string name) => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN;
        return mode == "fps" ? Get("sysBusyPct") : Math.Max(Get("sysBusyPctWindow"), Get("sysBusyPctSeeks"));
    }

    private static (int Code, string Stdout, string Stderr, double Ms) Child(List<string> args)
    {
        var psi = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        var sw = Stopwatch.StartNew();
        using var p = new Process { StartInfo = psi };
        var outSb = new StringBuilder();
        var errSb = new StringBuilder();
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (outSb)
                {
                    outSb.AppendLine(e.Data);
                }
            }
        };
        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (errSb)
                {
                    errSb.AppendLine(e.Data);
                }
            }
        };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        if (!p.WaitForExit(600000))
        {
            p.Kill(true);
        }

        p.WaitForExit();
        return (p.ExitCode, outSb.ToString(), errSb.ToString(), sw.Elapsed.TotalMilliseconds);
    }
}
