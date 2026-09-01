using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public sealed record SceneScan(bool Ok, IReadOnlyList<SceneScore> Candidates, TimeSpan Elapsed, string Error);

public static class SceneDetector
{
    public const double BaseThreshold = 0.05;

    public static string[] ScanArgs(string path, double baseThreshold = BaseThreshold)
        => new[]
        {
            "-hide_banner", "-loglevel", "info", "-nostats",
            "-i", path,
            "-vf", FormattableString.Invariant($"select='gte(scene,{baseThreshold:0.###})',metadata=print"),
            "-an", "-sn", "-f", "null", "-"
        };

    public static async Task<SceneScan> ScanAsync(string path, double baseThreshold = BaseThreshold, CancellationToken ct = default)
    {
        var clock = Stopwatch.StartNew();
        Process process;
        try
        {
            process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, ScanArgs(path, baseThreshold)) };
            process.Start();
        }
        catch (Exception ex)
        {
            clock.Stop();
            return new SceneScan(false, Array.Empty<SceneScore>(), clock.Elapsed, ex.Message);
        }

        using (process)
        using (ct.Register(() => TryKill(process)))
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(CancellationToken.None);
            _ = await stdout;
            var log = await stderr;
            clock.Stop();
            ct.ThrowIfCancellationRequested();

            if (process.ExitCode != 0)
                return new SceneScan(false, Array.Empty<SceneScore>(), clock.Elapsed, FfmpegRunner.Tail(log));

            return new SceneScan(true, ParseScores(log), clock.Elapsed, string.Empty);
        }
    }

    public static IReadOnlyList<SceneScore> ParseScores(string log)
    {
        var scores = new List<SceneScore>();
        var time = double.NaN;
        foreach (var raw in log.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            var timeAt = line.IndexOf("pts_time:", StringComparison.Ordinal);
            if (timeAt >= 0)
            {
                var token = FirstToken(line[(timeAt + "pts_time:".Length)..]);
                time = double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? t : double.NaN;
                continue;
            }

            var scoreAt = line.IndexOf("lavfi.scene_score=", StringComparison.Ordinal);
            if (scoreAt < 0) continue;

            var value = FirstToken(line[(scoreAt + "lavfi.scene_score=".Length)..]);
            if (!double.IsNaN(time)
                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var score))
                scores.Add(new SceneScore(time, score));
            time = double.NaN;
        }
        return scores;
    }

    public static async Task<IReadOnlyList<SourcePacket>> ReadPacketsAsync(string path, CancellationToken ct = default)
    {
        var args = new[]
        {
            "-v", "error", "-select_streams", "v:0",
            "-show_entries", "packet=pts_time,size",
            "-of", "csv=p=0",
            path
        };

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, args) };
        process.Start();
        using var cancellationRegistration = ct.Register(() => TryKill(process));

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(CancellationToken.None);
        var csv = await stdout;
        _ = await stderr;
        ct.ThrowIfCancellationRequested();

        if (process.ExitCode != 0) return Array.Empty<SourcePacket>();
        return ComplexityProbe.ParsePackets(csv)
            .Select(p => new SourcePacket(p.PtsSeconds, p.Size))
            .ToList();
    }

    public static async Task<(SceneMap Map, TimeSpan Elapsed)> BuildMapAsync(
        string path,
        double duration,
        double threshold = SceneMap.DefaultThreshold,
        CancellationToken ct = default)
    {
        var clock = Stopwatch.StartNew();
        var scan = await ScanAsync(path, ct: ct);
        if (!scan.Ok) throw new InvalidOperationException($"Sahne taramasi basarisiz: {scan.Error}");
        var packets = await ReadPacketsAsync(path, ct);
        clock.Stop();
        return (SceneMap.Build(duration, scan.Candidates, threshold, packets), clock.Elapsed);
    }

    private static string FirstToken(string text)
    {
        var trimmed = text.TrimStart();
        var end = 0;
        while (end < trimmed.Length && !char.IsWhiteSpace(trimmed[end])) end++;
        return trimmed[..end];
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }
}
