using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public static class ComplexityProbe
{
    private const double WindowSeconds = 2.0;
    private const int MaxWindows = 3;

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

            return ComplexityProfile.FromProbe(fullBppf, halfBppf, sampled, fullFrames);
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

    private static IEnumerable<double> Windows(double duration)
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
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0) return (0, 0);

        var bytes = ParseVideoBytes(stderr);
        var frames = ParseFrames(stderr);
        return (bytes, frames);
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
