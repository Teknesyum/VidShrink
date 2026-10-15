using System.Globalization;
using System.Text;

namespace VidShrink.Core.Editing;

/// <summary>
/// Kesim listesinin duz metin bicimleri: CSV (<c>bas,son,ad</c>; saniye ya da <c>sa:dk:sn.ms</c>), YouTube bolum metni
/// (teslim zamani, bir saatten kisa kurguda <c>dk:sn</c>) ve CUE izleri (<c>INDEX 01 dk:sn:kare</c>, saniyede 75 kare).
/// </summary>
internal static class CutListText
{
    private const long CueFrameTicks = EditTime.TicksPerSecond / 75;

    public static string WriteCsv(IReadOnlyList<CutListEntry> entries)
    {
        var text = new StringBuilder();
        foreach (var entry in entries)
        {
            text.Append(CutList.Seconds(entry.Start)).Append(',').Append(CutList.Seconds(entry.End)).Append(',');
            text.Append(Quote(entry.Label ?? string.Empty)).Append('\n');
        }

        return text.ToString();
    }

    public static IReadOnlyList<CutListEntry>? ReadCsv(string text)
    {
        var list = new List<CutListEntry>();
        var first = true;
        foreach (var raw in Lines(text))
        {
            if (raw.Trim().Length == 0) continue;
            var fields = Fields(raw);
            if (fields is null) return null;
            var header = first;
            first = false;
            if (fields.Count < 2 || !TryTime(fields[0], 0, out var start) || !TryTime(fields[1], long.MaxValue, out var end))
            {
                if (header) continue;
                return null;
            }

            list.Add(new CutListEntry(start, end, fields.Count > 2 ? fields[2] : null));
        }

        return list;
    }

    public static string WriteYouTube(EditTimeline timeline, string chapterTitle)
    {
        var starts = new List<long>(timeline.Clips.Count);
        long at = 0;
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            at -= timeline.TransitionLength(i);
            starts.Add(at);
            at += timeline.Clips[i].TimelineLength;
        }

        var hours = at >= 3600L * EditTime.TicksPerSecond;
        var text = new StringBuilder();
        for (var i = 0; i < starts.Count; i++)
        {
            var seconds = Math.Max(0, starts[i]) / EditTime.TicksPerSecond;
            var stamp = hours
                ? string.Create(CultureInfo.InvariantCulture, $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}")
                : string.Create(CultureInfo.InvariantCulture, $"{seconds / 60:00}:{seconds % 60:00}");
            var title = timeline.Clips[i].Label ?? string.Format(CultureInfo.InvariantCulture, chapterTitle, i + 1);
            text.Append(stamp).Append(' ').Append(title).Append('\n');
        }

        return text.ToString();
    }

    public static IReadOnlyList<CutListEntry>? ReadCue(string text)
    {
        var tracks = new List<(long Start, string? Title)>();
        var inTrack = false;
        string? title = null;
        long? index = null;

        void Close()
        {
            if (inTrack && index is { } start) tracks.Add((start, title));
        }

        foreach (var raw in Lines(text))
        {
            var line = raw.Trim();
            var space = line.IndexOf(' ');
            var keyword = (space < 0 ? line : line[..space]).ToUpperInvariant();
            var rest = space < 0 ? string.Empty : line[(space + 1)..].Trim();
            switch (keyword)
            {
                case "TRACK":
                    Close();
                    inTrack = true;
                    title = null;
                    index = null;
                    break;
                case "TITLE" when inTrack:
                    title = Unquote(rest);
                    break;
                case "INDEX" when inTrack:
                    var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2) return null;
                    if (parts[0] != "01") break;
                    if (!TryCueTime(parts[1], out var at)) return null;
                    index = at;
                    break;
            }
        }

        Close();
        if (tracks.Count == 0) return null;
        var list = new List<CutListEntry>(tracks.Count);
        for (var i = 0; i < tracks.Count; i++)
            list.Add(new CutListEntry(tracks[i].Start, i + 1 < tracks.Count ? tracks[i + 1].Start : long.MaxValue, tracks[i].Title));
        return list;
    }

    internal static IEnumerable<string> Lines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static bool TryCueTime(string text, out long time)
    {
        time = 0;
        var parts = text.Split(':');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var mm)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ss)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var ff)
            || ss >= 60 || ff >= 75) return false;
        time = ((long)mm * 60 + ss) * EditTime.TicksPerSecond + ff * CueFrameTicks;
        return true;
    }

    private static bool TryTime(string text, long empty, out long time)
    {
        time = empty;
        var value = text.Trim();
        if (value.Length == 0) return true;
        var parts = value.Split(':');
        if (parts.Length > 3) return false;
        double seconds = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            var last = i == parts.Length - 1;
            if (!double.TryParse(parts[i], last ? NumberStyles.AllowDecimalPoint : NumberStyles.None, CultureInfo.InvariantCulture, out var part))
                return false;
            if (!last && i > 0 && part >= 60) return false;
            if (last && parts.Length > 1 && part >= 60) return false;
            seconds = seconds * 60 + part;
        }

        if (!double.IsFinite(seconds) || seconds > long.MaxValue / (double)EditTime.TicksPerSecond) return false;
        time = EditTime.FromSeconds(seconds);
        return true;
    }

    private static string Quote(string value)
        => value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";

    private static string Unquote(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    private static List<string>? Fields(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c != '"') field.Append(c);
                else if (i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                else quoted = false;
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }

        if (quoted) return null;
        fields.Add(field.ToString());
        return fields;
    }
}
