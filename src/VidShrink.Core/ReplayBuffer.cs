using System.Globalization;
using System.Text;

namespace VidShrink.Core;

public sealed record ReplayPart(string Path, DateTime Written, long Length);

public static class ReplayBuffer
{
    public static readonly int[] SecondsChoices = { 15, 30, 60, 120 };

    public const int DefaultSeconds = 30;

    public const int SegmentSeconds = 2;

    public const string PartPrefix = "replay_";

    public const string PartPattern = PartPrefix + "%03d.mkv";

    public const string ListName = "list.txt";

    public static int PartsFor(int seconds)
    {
        if (seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        return (seconds + SegmentSeconds - 1) / SegmentSeconds + 1;
    }

    public static IReadOnlyList<string> BuildCapture(RecorderRequest request, string folder, int seconds)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Buffer folder is required.", nameof(folder));

        var capture = request with
        {
            Container = RecorderContainer.Mkv,
            MaxDuration = null,
            MaxMegabytes = null,
            Split = null,
            PreviewPath = null
        };
        var placeholder = Path.Combine(folder, "replay.mkv");
        var args = RecorderArguments.Build(capture, placeholder).ToList();
        args.RemoveAt(args.Count - 1);
        var interval = SegmentSeconds.ToString(CultureInfo.InvariantCulture);
        args.AddRange(new[]
        {
            "-force_key_frames", $"expr:gte(t,n_forced*{interval})",
            "-f", "segment",
            "-segment_time", interval,
            "-segment_wrap", (PartsFor(seconds) + 1).ToString(CultureInfo.InvariantCulture),
            "-segment_format", "matroska",
            "-reset_timestamps", "1",
            Path.Combine(folder, PartPattern)
        });
        return args;
    }

    public static IReadOnlyList<string> Pick(IEnumerable<ReplayPart> parts, int seconds)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return parts
            .Where(p => p.Length > 0 && Path.GetFileName(p.Path).StartsWith(PartPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Written)
            .Take(PartsFor(seconds))
            .OrderBy(p => p.Written)
            .Select(p => p.Path)
            .ToList();
    }

    public static string ConcatList(IReadOnlyList<string> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var text = new StringBuilder();
        foreach (var part in parts)
            text.Append("file '").Append(part.Replace("'", "'\\''", StringComparison.Ordinal)).Append("'\n");
        return text.ToString();
    }

    public static IReadOnlyList<string> BuildSave(string listPath, string target)
    {
        if (string.IsNullOrWhiteSpace(listPath)) throw new ArgumentException("List path is required.", nameof(listPath));
        if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("Target path is required.", nameof(target));
        var args = new List<string>
        {
            "-hide_banner", "-y", "-nostdin",
            "-f", "concat", "-safe", "0", "-i", listPath,
            "-map", "0", "-c", "copy"
        };
        if (Path.GetExtension(target).ToLowerInvariant() is ".mp4" or ".mov") args.AddRange(new[] { "-movflags", "+faststart" });
        args.Add(target);
        return args;
    }
}
