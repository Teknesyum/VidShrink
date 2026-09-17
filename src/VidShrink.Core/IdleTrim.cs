using System.Globalization;
using System.Text.RegularExpressions;

namespace VidShrink.Core;

public sealed record IdleSpan(double Start, double End)
{
    public double Length => End - Start;
}

public static class IdleTrim
{
    public const double DefaultMinIdleSeconds = 2;

    public const double DefaultKeepSeconds = 0.5;

    public const string FreezeNoise = "0.003";

    public const int MaxCuts = 100;

    public const string TargetSuffix = "-trimmed";

    private static readonly Regex FreezeLine = new(
        @"lavfi\.freezedetect\.freeze_(?<kind>start|end):\s*(?<value>[0-9]+(?:\.[0-9]+)?)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static IReadOnlyList<string> BuildDetect(string source, double minIdleSeconds = DefaultMinIdleSeconds)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source path is required.", nameof(source));
        if (!(minIdleSeconds > 0)) throw new ArgumentOutOfRangeException(nameof(minIdleSeconds));
        return new[]
        {
            "-hide_banner", "-nostdin", "-nostats",
            "-i", source,
            "-map", "0:v:0",
            "-vf", $"freezedetect=n={FreezeNoise}:d={Number(minIdleSeconds)}",
            "-an", "-f", "null", "-"
        };
    }

    public static IReadOnlyList<IdleSpan> ParseFreezes(string standardError, double durationSeconds)
    {
        var spans = new List<IdleSpan>();
        double? open = null;
        foreach (Match match in FreezeLine.Matches(standardError ?? string.Empty))
        {
            var value = double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
            if (match.Groups["kind"].Value == "start")
            {
                open = value;
            }
            else if (open is { } start)
            {
                if (value > start) spans.Add(new IdleSpan(start, value));
                open = null;
            }
        }

        if (open is { } tail && durationSeconds > tail) spans.Add(new IdleSpan(tail, durationSeconds));
        return spans;
    }

    public static IReadOnlyList<IdleSpan> KeptRanges(IReadOnlyList<IdleSpan> freezes, double durationSeconds, double keepSeconds = DefaultKeepSeconds)
    {
        ArgumentNullException.ThrowIfNull(freezes);
        if (keepSeconds < 0) throw new ArgumentOutOfRangeException(nameof(keepSeconds));

        var cuts = freezes
            .Where(f => f.Length > keepSeconds)
            .OrderByDescending(f => f.Length)
            .Take(MaxCuts)
            .Select(f => new IdleSpan(Math.Max(0, f.Start + keepSeconds), Math.Min(durationSeconds, f.End)))
            .Where(c => c.End > c.Start)
            .OrderBy(c => c.Start)
            .ToList();

        var kept = new List<IdleSpan>();
        var cursor = 0d;
        foreach (var cut in cuts)
        {
            if (cut.Start > cursor) kept.Add(new IdleSpan(cursor, cut.Start));
            cursor = Math.Max(cursor, cut.End);
        }

        if (durationSeconds > cursor) kept.Add(new IdleSpan(cursor, durationSeconds));
        return kept;
    }

    public static double RemovedSeconds(IReadOnlyList<IdleSpan> kept, double durationSeconds)
        => Math.Max(0, durationSeconds - kept.Sum(k => k.Length));

    public static string TrimTarget(string source, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var folder = Path.GetDirectoryName(source) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(source) + TargetSuffix;
        var extension = Path.GetExtension(source).ToLowerInvariant() is ".mp4" or ".mov" or ".mkv" ? Path.GetExtension(source) : ".mkv";
        var candidate = Path.Combine(folder, stem + extension);
        for (var index = 2; exists(candidate); index++)
            candidate = Path.Combine(folder, stem + "_" + index.ToString(CultureInfo.InvariantCulture) + extension);
        return candidate;
    }

    public static IReadOnlyList<string> BuildTrim(string source, string target, IReadOnlyList<IdleSpan> kept, bool hasAudio)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source path is required.", nameof(source));
        if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("Target path is required.", nameof(target));
        ArgumentNullException.ThrowIfNull(kept);
        if (kept.Count == 0) throw new ArgumentException("At least one range must be kept.", nameof(kept));

        var graph = new List<string>();
        var inputs = string.Empty;
        for (var i = 0; i < kept.Count; i++)
        {
            var range = $"start={Number(kept[i].Start)}:end={Number(kept[i].End)}";
            graph.Add($"[0:v]trim={range},setpts=PTS-STARTPTS[v{i}]");
            inputs += $"[v{i}]";
            if (!hasAudio) continue;
            graph.Add($"[0:a]atrim={range},asetpts=PTS-STARTPTS[a{i}]");
            inputs += $"[a{i}]";
        }

        var concat = $"{inputs}concat=n={kept.Count}:v=1:a={(hasAudio ? 1 : 0)}[vout]" + (hasAudio ? "[aout]" : string.Empty);
        graph.Add(concat);

        var args = new List<string>
        {
            "-hide_banner", "-y", "-nostdin",
            "-i", source,
            "-filter_complex", string.Join(";", graph),
            "-map", "[vout]",
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p"
        };
        if (hasAudio) args.AddRange(new[] { "-map", "[aout]", "-c:a", "aac", "-b:a", "160k" });
        if (Path.GetExtension(target).ToLowerInvariant() is ".mp4" or ".mov") args.AddRange(new[] { "-movflags", "+faststart" });
        args.Add(target);
        return args;
    }
}
