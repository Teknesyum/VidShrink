using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public sealed record CropDetection(CropRect? Rect, IReadOnlyList<CropRect> Samples, TimeSpan Elapsed);

public static class CropProbe
{
    public const int SamplePoints = 10;
    public const int FramesPerPoint = 2;
    public const int Limit = 24;
    public static readonly string Filter = $"cropdetect=limit={Limit}:round=2:skip=0:reset=0";

    private static readonly Regex CropLine = new(@"crop=(\d+):(\d+):(\d+):(\d+)", RegexOptions.CultureInvariant);

    public static IReadOnlyList<double> SampleTimes(double durationSeconds)
    {
        var duration = double.IsFinite(durationSeconds) && durationSeconds > 0 ? durationSeconds : 0;
        return Enumerable.Range(0, SamplePoints)
            .Select(i => Math.Round(duration * (i + 0.5) / SamplePoints, 3))
            .ToArray();
    }

    public static IReadOnlyList<string> Arguments(string path, double seconds) => new[]
    {
        "-hide_banner", "-nostdin", "-nostats",
        "-ss", seconds.ToString("0.###", CultureInfo.InvariantCulture),
        "-i", path, "-map", "0:v:0", "-frames:v", FramesPerPoint.ToString(CultureInfo.InvariantCulture),
        "-vf", Filter, "-an", "-sn", "-dn", "-f", "null", "-"
    };

    public static CropRect? ParseLast(string standardError)
    {
        CropRect? last = null;
        foreach (Match m in CropLine.Matches(standardError ?? ""))
            last = new CropRect(
                int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture));
        return last;
    }

    public static CropRect? Decide(IReadOnlyList<CropRect> samples, int sourceWidth, int sourceHeight)
    {
        if (samples.Count == 0) return null;
        var mode = samples
            .GroupBy(r => r)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => (long)g.Key.Width * g.Key.Height)
            .First().Key;
        if (mode.Width <= 0 || mode.Height <= 0) return null;
        if (mode.Width >= sourceWidth && mode.Height >= sourceHeight) return null;
        return mode;
    }

    public static async Task<CropDetection> RunAsync(MediaInfo info, CancellationToken ct = default)
    {
        var clock = Stopwatch.StartNew();
        var samples = new List<CropRect>();
        foreach (var t in SampleTimes(info.DurationSeconds))
        {
            var run = await ProbeProcess.RunAsync(Arguments(info.FilePath, t), ct);
            if (run.ExitCode == 0 && ParseLast(run.StandardError) is { } rect) samples.Add(rect);
        }
        clock.Stop();
        return new CropDetection(Decide(samples, info.Width, info.Height), samples, clock.Elapsed);
    }
}
