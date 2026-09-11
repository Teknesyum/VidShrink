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
                    Seen = Read(item["seen"])
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
        var at = entry.Position;
        if (!double.IsFinite(at) || at < MinimumResumeSeconds) return 0;
        if (durationSeconds > 0 && at >= durationSeconds - EndMarginSeconds) return 0;
        return at;
    }

    internal void Remember(string media, double positionSeconds, bool finished)
    {
        var entry = Touch(media);
        entry.Position = finished || !double.IsFinite(positionSeconds) ? 0 : Math.Max(0, positionSeconds);
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

    private sealed class Entry
    {
        internal double Position { get; set; }

        internal double Seen { get; set; }

        internal List<double> Bookmarks { get; } = new();
    }
}
