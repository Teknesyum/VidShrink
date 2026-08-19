using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public readonly record struct PacketSample(double PtsSeconds, long Size);

public static class ComplexityProbe
{
    private const double WindowSeconds = 2.0;
    private const int MaxWindows = 3;
    private const int MinProfileSeconds = 4;

    public static async Task<ComplexityProfile> RunAsync(MediaInfo info, CancellationToken ct = default)
    {
        try
        {
            long fullBytes = 0, halfBytes = 0;
            long fullFrames = 0, halfFrames = 0;
            var sampled = 0.0;

            var halfWidth = EvenDown((int)Math.Round(info.Width * ComplexityProfile.ProbeScale));
            var halfHeight = EvenDown((int)Math.Round(info.Height * ComplexityProfile.ProbeScale));
            var canProbeHalf = halfWidth >= 64 && halfHeight >= 64;

            foreach (var start in Windows(info.DurationSeconds))
            {
                var (bytes, frames) = await SampleAsync(info.FilePath, start, WindowSeconds, null, ct);
                if (frames <= 0) continue;
                fullBytes += bytes;
                fullFrames += frames;
                sampled += WindowSeconds;

                if (!canProbeHalf) continue;
                var (hb, hf) = await SampleAsync(info.FilePath, start, WindowSeconds, $"scale={halfWidth}:{halfHeight}", ct);
                if (hf <= 0) continue;
                halfBytes += hb;
                halfFrames += hf;
            }

            if (fullFrames <= 0 || fullBytes <= 0)
                return ComplexityProfile.FromSourceBitrate(info);

            var fullBppf = fullBytes * 8.0 / ((double)info.Width * info.Height * fullFrames);
            var halfBppf = halfFrames > 0 && halfBytes > 0
                ? halfBytes * 8.0 / ((double)halfWidth * halfHeight * halfFrames)
                : 0.0;

            var bias = await MeasureWindowBiasAsync(info, ct);

            return ComplexityProfile.FromProbe(fullBppf, halfBppf, sampled, fullFrames, bias);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return ComplexityProfile.FromSourceBitrate(info);
        }
    }

    public static IEnumerable<double> Windows(double duration)
    {
        if (duration <= WindowSeconds * 1.5)
        {
            yield return 0;
            yield break;
        }

        var usable = Math.Max(0.0, duration - WindowSeconds);
        var count = duration < WindowSeconds * 6 ? 2 : MaxWindows;
        for (var i = 0; i < count; i++)
            yield return usable * (i + 0.5) / count;
    }

    public static double ComputeWindowBias(IReadOnlyList<PacketSample> packets, double duration)
    {
        var profile = SecondProfile(packets, duration);
        if (profile.Length < MinProfileSeconds) return 0.0;

        var selected = new HashSet<int>();
        foreach (var start in Windows(duration))
        {
            var first = (int)Math.Floor(start);
            var last = (int)Math.Floor(start + WindowSeconds) - 1;
            for (var i = first; i <= last; i++)
                if (i >= 0 && i < profile.Length) selected.Add(i);
        }

        if (selected.Count == 0 || selected.Count >= profile.Length) return 0.0;

        var windowMean = selected.Sum(i => profile[i]) / selected.Count;
        var fileMean = profile.Sum() / profile.Length;
        if (windowMean <= 0 || fileMean <= 0) return 0.0;

        var bias = windowMean / fileMean;
        return double.IsFinite(bias) ? bias : 0.0;
    }

    private static double[] SecondProfile(IReadOnlyList<PacketSample> packets, double duration)
    {
        var seconds = (int)Math.Floor(duration);
        if (seconds < MinProfileSeconds || packets.Count == 0) return Array.Empty<double>();

        var buckets = new double[seconds];
        foreach (var packet in packets)
        {
            if (!double.IsFinite(packet.PtsSeconds) || packet.PtsSeconds < 0 || packet.Size <= 0) continue;
            var index = (int)packet.PtsSeconds;
            if (index < buckets.Length) buckets[index] += packet.Size * 8.0;
        }

        var end = buckets.Length;
        while (end > 0 && buckets[end - 1] <= 0) end--;
        if (end < MinProfileSeconds) return Array.Empty<double>();
        return end == buckets.Length ? buckets : buckets[..end];
    }

    private static async Task<double> MeasureWindowBiasAsync(MediaInfo info, CancellationToken ct)
    {
        try
        {
            var packets = await ReadPacketsAsync(info.FilePath, ct);
            return ComputeWindowBias(packets, info.DurationSeconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return 0.0;
        }
    }

    private static async Task<IReadOnlyList<PacketSample>> ReadPacketsAsync(string path, CancellationToken ct)
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

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        var stdout = await stdoutTask;
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0) return Array.Empty<PacketSample>();
        return ParsePackets(stdout);
    }

    public static IReadOnlyList<PacketSample> ParsePackets(string csv)
    {
        var samples = new List<PacketSample>();
        foreach (var line in csv.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            var comma = trimmed.IndexOf(',');
            if (comma <= 0) continue;

            if (!double.TryParse(trimmed[..comma], NumberStyles.Float, CultureInfo.InvariantCulture, out var pts)) continue;
            if (!long.TryParse(trimmed[(comma + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)) continue;

            samples.Add(new PacketSample(pts, size));
        }
        return samples;
    }

    private static int EvenDown(int value) => value % 2 == 0 ? value : value - 1;

    private static async Task<(long Bytes, long Frames)> SampleAsync(string path, double start, double length, string? filter, CancellationToken ct)
    {
        var args = new List<string>
        {
            "-hide_banner", "-nostdin",
            "-ss", start.ToString("0.###", CultureInfo.InvariantCulture),
            "-t", length.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", path,
            "-an", "-sn", "-dn"
        };

        if (filter is not null)
        {
            args.Add("-vf");
            args.Add(filter);
        }

        args.AddRange(new[]
        {
            "-c:v", "libx264",
            "-crf", ComplexityProfile.ProbeCrf.ToString("0", CultureInfo.InvariantCulture),
            "-preset", ComplexityProfile.ProbePreset,
            "-f", "null", "-"
        });

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args.ToArray()) };
        process.Start();
        using var cancellationRegistration = ct.Register(() => TryKill(process));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        var stderr = await stderrTask;
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0) return (0, 0);

        var bytes = ParseVideoBytes(stderr);
        var frames = ParseFrames(stderr);
        return (bytes, frames);
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    private static long ParseVideoBytes(string stderr)
    {
        var match = Regex.Match(stderr, @"video:\s*([0-9.]+)\s*([kKmMgG])?i?B", RegexOptions.RightToLeft);
        if (!match.Success) return 0;
        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return 0;

        var multiplier = match.Groups[2].Value.ToLowerInvariant() switch
        {
            "k" => 1024.0,
            "m" => 1024.0 * 1024.0,
            "g" => 1024.0 * 1024.0 * 1024.0,
            _ => 1.0
        };
        return (long)(value * multiplier);
    }

    private static long ParseFrames(string stderr)
    {
        var matches = Regex.Matches(stderr, @"frame=\s*(\d+)");
        if (matches.Count == 0) return 0;
        return long.TryParse(matches[^1].Groups[1].Value, out var frames) ? frames : 0;
    }
}
