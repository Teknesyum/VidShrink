using VidShrink.App.Localization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using VidShrink.Core;
using System.Text.Json.Nodes;

namespace VidShrink.App.Playback;

internal sealed record HistoryRow(string Path, DateTimeOffset Seen, double Position);

/// <summary>Dosya başına hatırlanan seçimler: ses izi, altyazı izi, ikincil altyazı (0 kapalı), iki gecikme ve hız.</summary>
internal sealed record TrackMemory(long Audio, long Subtitle, long SecondarySubtitle, double SubtitleDelay, double AudioDelay, double Speed);

internal sealed class PlaybackHistory
{
    internal const int MaxAgeDays = 365;
    internal const double MinimumResumeSeconds = 1;
    internal const double EndMarginSeconds = 1;
    internal const double BookmarkMergeSeconds = 0.5;
    internal const double DurationToleranceSeconds = 0.5;

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly Dictionary<string, Entry> _entries = new(PathComparer);

    internal int Count => _entries.Count;

    internal Func<long> Now { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Geçmiş penceresinin satırları: en son izlenen başta, adres ve yalnız yer imi taşıyan kayıt yok. Arama dosya adında ve yolda, büyük-küçük harf ayırmadan.</summary>
    internal IReadOnlyList<HistoryRow> Rows(string? search)
    {
        var text = search?.Trim() ?? "";
        var compare = Strings.Culture.CompareInfo;
        return _entries
            .Where(pair => pair.Value.Seen > 0 && !MediaAddress.IsAddress(pair.Key))
            .Where(pair => text.Length == 0 || compare.IndexOf(pair.Key, text, CompareOptions.IgnoreCase) >= 0)
            .OrderByDescending(pair => pair.Value.Seen)
            .Select(pair => new HistoryRow(pair.Key, DateTimeOffset.FromUnixTimeMilliseconds((long)pair.Value.Seen), pair.Value.Position))
            .ToList();
    }

    /// <summary>İzleme kaydını ve kaldığı yeri siler; yer imi kullanıcının kendi kaydı olduğu için kalır, ama dosya geçmişte görünmez.</summary>
    internal void Clear()
    {
        foreach (var key in _entries.Where(pair => pair.Value.Bookmarks.Count == 0).Select(pair => pair.Key).ToList())
            _entries.Remove(key);
        foreach (var entry in _entries.Values)
        {
            entry.Seen = 0;
            entry.Position = 0;
            entry.Size = -1;
            entry.Modified = -1;
            entry.Duration = double.NaN;
            entry.Tracks = null;
        }
    }

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
                if (item["tracks"] is JsonObject tracks) entry.Tracks = ReadTracks(tracks);
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
                var oldest = Now() - MaxAgeDays * 24d * 60 * 60 * 1000;
                foreach (var (media, entry) in _entries.Where(pair => pair.Value.Bookmarks.Count > 0 || pair.Value.Seen >= oldest).OrderByDescending(pair => pair.Value.Seen))
                {
                    writer.WriteStartObject(media);
                    writer.WriteNumber("position", Math.Round(entry.Position, 3));
                    writer.WriteNumber("seen", entry.Seen);
                    if (entry.Size >= 0) writer.WriteNumber("size", entry.Size);
                    if (entry.Modified >= 0) writer.WriteNumber("modified", entry.Modified);
                    if (double.IsFinite(entry.Duration) && entry.Duration > 0) writer.WriteNumber("duration", Math.Round(entry.Duration, 3));
                    if (entry.Tracks is { } tracks)
                    {
                        writer.WriteStartObject("tracks");
                        writer.WriteNumber("audio", tracks.Audio);
                        writer.WriteNumber("subtitle", tracks.Subtitle);
                        writer.WriteNumber("subtitle2", tracks.SecondarySubtitle);
                        writer.WriteNumber("subtitleDelay", Math.Round(tracks.SubtitleDelay, 3));
                        writer.WriteNumber("audioDelay", Math.Round(tracks.AudioDelay, 3));
                        writer.WriteNumber("speed", Math.Round(tracks.Speed, 2));
                        writer.WriteEndObject();
                    }

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
        if (!SameFile(media, durationSeconds, entry)) return 0;
        var at = entry.Position;
        if (!double.IsFinite(at) || at < MinimumResumeSeconds) return 0;
        if (durationSeconds > 0 && at >= durationSeconds - EndMarginSeconds) return 0;
        return at;
    }

    /// <summary>Kayıt dosyası aynı dosyaya döndüyse o dosyada en son seçilen izler, gecikmeler ve hız; eski kayıtta ya da başka dosyada yok.</summary>
    internal TrackMemory? TracksFor(string media, double durationSeconds)
        => _entries.TryGetValue(Normalize(media), out var entry) && entry.Tracks is { } tracks && SameFile(media, durationSeconds, entry) ? tracks : null;

    internal void Remember(string media, double positionSeconds, bool finished, double durationSeconds = double.NaN, TrackMemory? tracks = null)
    {
        var entry = Touch(media);
        if (tracks is not null) entry.Tracks = tracks;
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

    /// <summary>Liste satırının ilerlemesi: kalınan yer ve bilinen süre. İzlenmemiş ya da geçmişi silinmiş dosyada yok.</summary>
    internal (double Position, double Duration)? Progress(string media)
        => _entries.TryGetValue(Normalize(media), out var entry) && entry.Seen > 0 ? (entry.Position, entry.Duration) : null;

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

        entry.Seen = Now();
        return entry;
    }

    private static string Normalize(string media)
    {
        if (string.IsNullOrEmpty(media)) return "";
        if (MediaAddress.IsAddress(media)) return MediaAddress.WithoutQuery(media);
        try { return Path.GetFullPath(media); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return media; }
    }

    private static double Read(JsonNode? node)
    {
        try { return node?.GetValue<double>() ?? double.NaN; }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException) { return double.NaN; }
    }

    private static TrackMemory ReadTracks(JsonObject node)
    {
        var subtitleDelay = Read(node["subtitleDelay"]);
        var audioDelay = Read(node["audioDelay"]);
        return new TrackMemory(
            ReadLong(node["audio"]),
            ReadLong(node["subtitle"]),
            ReadLong(node["subtitle2"]),
            double.IsFinite(subtitleDelay) ? subtitleDelay : 0,
            double.IsFinite(audioDelay) ? audioDelay : 0,
            Read(node["speed"]));
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

        internal TrackMemory? Tracks { get; set; }
    }
}
