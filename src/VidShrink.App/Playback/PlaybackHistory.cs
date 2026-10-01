using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VidShrink.App.Playback;

internal sealed class PlaybackHistory
{
    internal const int Capacity = 200;
    internal const double MinimumResumeSeconds = 1;
    internal const double EndMarginSeconds = 1;
    internal const double BookmarkMergeSeconds = 0.5;
    internal const int ResumeRecent = 5;
    internal const double DurationToleranceSeconds = 0.5;

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly Dictionary<string, Entry> _entries = new(PathComparer);

    internal int Count => _entries.Count;

    internal static PlaybackHistory Load(string? file)
    {
        var history = new PlaybackHistory();
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return history;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root) return history;
            if (root["files"] is not JsonObject files) return history;

            foreach (var (media, node) in files)
            {
                if (node is not JsonObject item) continue;
                var entry = new Entry
                {
                    Position = Read(item["position"]),
                    Seen = Read(item["seen"]),
                    Size = ReadLong(item["size"]),
                    Modified = ReadLong(item["modified"]),
                    Duration = Read(item["duration"])
                };

                if (item["bookmarks"] is JsonArray marks)
                    foreach (var mark in marks)
                    {
                        var at = Read(mark);
                        if (double.IsFinite(at) && at >= 0) entry.Bookmarks.Add(at);
                    }

                entry.Bookmarks.Sort();
                history._entries[media] = entry;
            }
        }
        catch (JsonException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return history;
    }

    internal void Save(string? file)
    {
        if (string.IsNullOrEmpty(file)) return;

        try
        {
            var folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            var partial = file + ".tmp";
            using (var stream = File.Create(partial))
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteStartObject("files");
                foreach (var (media, entry) in _entries.OrderByDescending(pair => pair.Value.Seen).Take(Capacity))
                {
                    writer.WriteStartObject(media);
                    writer.WriteNumber("position", Math.Round(entry.Position, 3));
                    writer.WriteNumber("seen", entry.Seen);
                    if (entry.Size >= 0) writer.WriteNumber("size", entry.Size);
                    if (entry.Modified >= 0) writer.WriteNumber("modified", entry.Modified);
                    if (double.IsFinite(entry.Duration) && entry.Duration > 0) writer.WriteNumber("duration", Math.Round(entry.Duration, 3));
                    writer.WriteStartArray("bookmarks");
                    foreach (var mark in entry.Bookmarks) writer.WriteNumberValue(Math.Round(mark, 3));
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            File.Move(partial, file, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    internal double ResumeFor(string media, double durationSeconds)
    {
        if (!_entries.TryGetValue(Normalize(media), out var entry)) return 0;
        if (_entries.Values.Count(other => other.Seen > entry.Seen) >= ResumeRecent) return 0;
        if (!SameFile(media, durationSeconds, entry)) return 0;
        var at = entry.Position;
        if (!double.IsFinite(at) || at < MinimumResumeSeconds) return 0;
        if (durationSeconds > 0 && at >= durationSeconds - EndMarginSeconds) return 0;
        return at;
    }

    internal void Remember(string media, double positionSeconds, bool finished, double durationSeconds = double.NaN)
    {
        var entry = Touch(media);
        entry.Position = finished || !double.IsFinite(positionSeconds) ? 0 : Math.Max(0, positionSeconds);
        var (size, modified) = Stamp(media);
        entry.Size = size;
        entry.Modified = modified;
        entry.Duration = double.IsFinite(durationSeconds) && durationSeconds > 0 ? durationSeconds : double.NaN;
    }

    /// <summary>Kayıt yalnız aynı dosyaya döner: bayt boyu ve son yazma zamanı birebir, süre yarım saniye içinde tutmalı. İzi olmayan eski kayıt eşleşmez.</summary>
    private static bool SameFile(string media, double durationSeconds, Entry entry)
    {
        if (entry.Size < 0 || entry.Modified < 0) return false;
        var (size, modified) = Stamp(media);
        if (size != entry.Size || modified != entry.Modified) return false;
        if (double.IsFinite(entry.Duration) && durationSeconds > 0 && Math.Abs(durationSeconds - entry.Duration) > DurationToleranceSeconds) return false;
        return true;
    }

    private static (long Size, long Modified) Stamp(string media)
    {
        try
        {
            var info = new FileInfo(media);
            return info.Exists ? (info.Length, info.LastWriteTimeUtc.Ticks) : (-1, -1);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return (-1, -1); }
    }

    internal IReadOnlyList<double> Bookmarks(string media)
        => _entries.TryGetValue(Normalize(media), out var entry) ? entry.Bookmarks.ToArray() : Array.Empty<double>();

    internal bool AddBookmark(string media, double atSeconds)
    {
        if (!double.IsFinite(atSeconds) || atSeconds < 0) return false;
        var entry = Touch(media);
        if (entry.Bookmarks.Any(mark => Math.Abs(mark - atSeconds) < BookmarkMergeSeconds)) return false;
        entry.Bookmarks.Add(atSeconds);
        entry.Bookmarks.Sort();
        return true;
    }

    internal double? NextBookmark(string media, double afterSeconds)
    {
        var marks = Bookmarks(media);
        if (marks.Count == 0) return null;
        foreach (var mark in marks)
            if (mark > afterSeconds + BookmarkMergeSeconds) return mark;
        return marks[0];
    }

    internal double? PreviousBookmark(string media, double beforeSeconds)
    {
        var marks = Bookmarks(media);
        if (marks.Count == 0) return null;
        for (var i = marks.Count - 1; i >= 0; i--)
            if (marks[i] < beforeSeconds - BookmarkMergeSeconds) return marks[i];
        return marks[^1];
    }

    private Entry Touch(string media)
    {
        var key = Normalize(media);
        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = new Entry();
            _entries[key] = entry;
        }

        entry.Seen = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return entry;
    }

    private static string Normalize(string media)
    {
        if (string.IsNullOrEmpty(media)) return "";
        try { return Path.GetFullPath(media); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return media; }
    }

    private static double Read(JsonNode? node)
    {
        try { return node?.GetValue<double>() ?? double.NaN; }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException) { return double.NaN; }
    }

    private static long ReadLong(JsonNode? node)
    {
        try { return node?.GetValue<long>() ?? -1; }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException) { return -1; }
    }

    private sealed class Entry
    {
        internal long Size { get; set; } = -1;

        internal long Modified { get; set; } = -1;

        internal double Duration { get; set; } = double.NaN;

        internal double Position { get; set; }

        internal double Seen { get; set; }

        internal List<double> Bookmarks { get; } = new();
    }
}
