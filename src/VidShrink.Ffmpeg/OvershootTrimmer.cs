using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public sealed record PacketMap(IReadOnlyList<MediaPacket> Packets, double DurationSeconds, long FileBytes);

public sealed record TrimOutcome(TrimPlan? Plan, long Bytes, bool Landed, int Rounds, string? Error);

public static class OvershootTrimmer
{
    private const int MaxRounds = 6;
    private const double SeekNudgeSeconds = 0.001;

    public static async Task<PacketMap?> ReadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return null;
        var args = new[]
        {
            "-hide_banner", "-v", "error",
            "-show_entries", "packet=codec_type,pts_time,duration_time,size,flags",
            "-of", "compact=p=0",
            path
        };

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, args) };
        process.Start();
        using var registration = ct.Register(() => TryKill(process));
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdout = await stdoutTask;
        _ = await stderrTask;
        await process.WaitForExitAsync(CancellationToken.None);
        ct.ThrowIfCancellationRequested();
        if (process.ExitCode != 0) return null;

        var packets = Parse(stdout, out var duration);
        if (packets.Count == 0 || duration <= 0) return null;
        return new PacketMap(packets, duration, new FileInfo(path).Length);
    }

    internal static List<MediaPacket> Parse(string text, out double durationSeconds)
    {
        var packets = new List<MediaPacket>();
        var first = double.MaxValue;
        var last = double.MinValue;
        foreach (var raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string? kind = null, flags = null;
            double pts = double.NaN, length = 0;
            long size = -1;
            foreach (var field in raw.Trim().Split('|'))
            {
                var eq = field.IndexOf('=');
                if (eq <= 0) continue;
                var key = field[..eq];
                var value = field[(eq + 1)..];
                switch (key)
                {
                    case "codec_type": kind = value; break;
                    case "pts_time": double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out pts); break;
                    case "duration_time": double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out length); break;
                    case "size": long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out size); break;
                    case "flags": flags = value; break;
                }
            }
            if (kind is not ("video" or "audio") || !double.IsFinite(pts) || size < 0) continue;
            packets.Add(new MediaPacket(kind == "video", pts, size, flags?.Contains('K') == true));
            first = Math.Min(first, pts);
            last = Math.Max(last, pts + (double.IsFinite(length) ? Math.Max(0, length) : 0));
        }
        durationSeconds = packets.Count == 0 ? 0 : last - first;
        return packets;
    }

    public static IReadOnlyList<string> Arguments(string input, string output, TrimPlan plan)
    {
        var args = new List<string> { "-hide_banner", "-v", "error", "-y" };
        var cutsStart = plan.StartSeconds > 0;
        if (cutsStart) args.AddRange(new[] { "-ss", Seconds(plan.StartSeconds + SeekNudgeSeconds) });
        args.AddRange(new[] { "-i", input, "-map", "0:v?", "-map", "0:a?", "-c", "copy" });
        if (plan.EndSeconds < plan.DurationSeconds) args.AddRange(new[] { "-t", Seconds(plan.KeptSeconds) });
        args.AddRange(new[] { "-avoid_negative_ts", "make_zero", "-movflags", "+faststart", output });
        return args;
    }

    public static async Task<TrimOutcome> TrimAsync(string input, string output, long targetBytes, TrimSide side, CancellationToken ct)
    {
        var map = await ReadAsync(input, ct);
        if (map is null) return new TrimOutcome(null, 0, false, 0, "packet map unavailable");

        long extra = 0;
        TrimPlan? plan = null;
        long bytes = 0;
        for (var round = 1; round <= MaxRounds; round++)
        {
            plan = OvershootTrim.Plan(map.Packets, map.FileBytes, targetBytes, map.DurationSeconds, side, extra);
            if (plan is null) break;

            var run = await FfmpegRunner.RunAsync(Arguments(input, output, plan), ct);
            if (!run.Ok)
            {
                TryDelete(output);
                return new TrimOutcome(plan, 0, false, round, run.StandardError);
            }

            bytes = new FileInfo(output).Length;
            if (bytes <= targetBytes) return new TrimOutcome(plan, bytes, true, round, null);
            extra += bytes - targetBytes + Math.Max(1, targetBytes / 1000);
        }

        TryDelete(output);
        return new TrimOutcome(plan, bytes, false, MaxRounds, "trim did not land under the target");
    }

    public static IReadOnlyList<TrimPlan> PlanAll(PacketMap map, long targetBytes) =>
        new[] { TrimSide.End, TrimSide.Start, TrimSide.Both }
            .Select(side => OvershootTrim.Plan(map.Packets, map.FileBytes, targetBytes, map.DurationSeconds, side))
            .OfType<TrimPlan>()
            .ToArray();

    private static string Seconds(double value) => Math.Max(0, value).ToString("0.######", CultureInfo.InvariantCulture);

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
