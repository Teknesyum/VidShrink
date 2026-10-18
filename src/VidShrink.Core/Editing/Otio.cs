using System.Text;
using System.Text.Json;

namespace VidShrink.Core.Editing;

public sealed record OtioImport(
    string? Name,
    double Fps,
    IReadOnlyList<string> Sources,
    IReadOnlyList<long> Durations,
    IReadOnlyList<EditClip> Clips,
    IReadOnlyList<TextLayer> Texts,
    IReadOnlyList<EditMarker> Markers,
    EditMusic? Music,
    int Skipped)
{
    public EditTimeline ToTimeline()
    {
        var timeline = new EditTimeline(Array.Empty<EditClip>(), Durations[0], extraSources: Durations.Skip(1));
        timeline.Restore(Clips, Texts, Music);
        return timeline;
    }

    public EditProject? ToProject(Func<string, SourceStamp?> stampOf, out string? missing)
    {
        ArgumentNullException.ThrowIfNull(stampOf);
        missing = null;
        var stamps = new SourceStamp[Sources.Count];
        for (var i = 0; i < Sources.Count; i++)
        {
            if (stampOf(Sources[i]) is not { } stamp)
            {
                missing = Sources[i];
                return null;
            }

            stamps[i] = stamp;
        }

        return EditProject.From(stamps[0], ToTimeline(), extraSources: stamps.Skip(1).ToArray(), markers: Markers);
    }
}

public static class Otio
{
    public const string Extension = ".otio";

    private const string Namespace = "vidshrink";
    private const string MediaKey = "DEFAULT_MEDIA";
    private const string RoleSourceAudio = "source-audio";
    private const string RoleMusic = "music";

    private static readonly UTF8Encoding Utf8 = new(false);

    public static string Write(EditTimeline timeline, CutListContext context, IReadOnlyList<EditMarker>? markers = null)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(context);
        if (!double.IsFinite(context.Fps) || context.Fps < 1 || context.Fps > 240)
            throw new ArgumentOutOfRangeException(nameof(context), context.Fps, "Gecersiz kare hizi");
        var rate = context.Fps;
        var title = context.Title ?? (context.SourcePaths.Count > 0 ? Path.GetFileNameWithoutExtension(CutList.FileName(context.SourcePaths[0])) : "VidShrink");
        string PathOf(int source) => source < context.SourcePaths.Count ? context.SourcePaths[source] : string.Empty;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            void Time(string name, long ticks)
            {
                writer.WriteStartObject(name);
                writer.WriteString("OTIO_SCHEMA", "RationalTime.1");
                writer.WriteNumber("rate", rate);
                writer.WriteNumber("value", ticks * rate / EditTime.TicksPerSecond);
                writer.WriteEndObject();
            }

            void Range(string name, long start, long duration)
            {
                writer.WriteStartObject(name);
                writer.WriteString("OTIO_SCHEMA", "TimeRange.1");
                Time("duration", duration);
                Time("start_time", start);
                writer.WriteEndObject();
            }

            void Common(string schema, string name)
            {
                writer.WriteString("OTIO_SCHEMA", schema);
                writer.WriteString("name", name);
            }

            void Item()
            {
                writer.WriteStartArray("effects");
                writer.WriteEndArray();
                writer.WriteStartArray("markers");
                writer.WriteEndArray();
                writer.WriteBoolean("enabled", true);
            }

            void Reference(string path, long? duration)
            {
                writer.WriteStartObject("media_references");
                writer.WriteStartObject(MediaKey);
                Common("ExternalReference.1", CutList.FileName(path));
                writer.WriteStartObject("metadata");
                writer.WriteEndObject();
                if (duration is { } length) Range("available_range", 0, length);
                else writer.WriteNull("available_range");
                writer.WriteNull("available_image_bounds");
                writer.WriteString("target_url", FileUrl(path));
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteString("active_media_reference_key", MediaKey);
            }

            void Clip(EditClip clip)
            {
                writer.WriteStartObject();
                Common("Clip.2", clip.Label ?? CutList.FileName(PathOf(clip.Source)));
                writer.WriteStartObject("metadata");
                writer.WriteStartObject(Namespace);
                writer.WriteNumber("speed", clip.Speed);
                writer.WriteBoolean("reversed", clip.Reversed);
                if (clip.Label is { } label) writer.WriteString("label", label);
                if (clip.Group is { } group) writer.WriteNumber("group", group);
                if (clip.Transition is { } transition)
                {
                    writer.WriteStartObject("transition");
                    writer.WriteString("kind", ClipTransition.Key(transition.Kind));
                    writer.WriteNumber("duration", transition.Duration);
                    writer.WriteEndObject();
                }

                if (!clip.Effects.IsNeutral) EditProject.WriteEffects(writer, clip.Effects);
                writer.WriteEndObject();
                writer.WriteEndObject();
                Range("source_range", clip.SourceStart, clip.SourceLength);
                Item();
                Reference(PathOf(clip.Source), timeline.DurationOf(clip.Source));
                writer.WriteEndObject();
            }

            void Track(string name, string kind, string? role, Action children)
            {
                writer.WriteStartObject();
                Common("Track.1", name);
                writer.WriteStartObject("metadata");
                if (role is not null)
                {
                    writer.WriteStartObject(Namespace);
                    writer.WriteString("role", role);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                writer.WriteNull("source_range");
                Item();
                writer.WriteString("kind", kind);
                writer.WriteStartArray("children");
                children();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            void Gap(long duration)
            {
                writer.WriteStartObject();
                Common("Gap.1", string.Empty);
                writer.WriteStartObject("metadata");
                writer.WriteEndObject();
                Range("source_range", 0, duration);
                Item();
                writer.WriteEndObject();
            }

            void Music(EditMusic music)
            {
                if (music.Start > 0) Gap(music.Start);
                writer.WriteStartObject();
                Common("Clip.2", CutList.FileName(music.Path));
                writer.WriteStartObject("metadata");
                writer.WriteStartObject(Namespace);
                writer.WriteNumber("level", music.LevelDb);
                writer.WriteEndObject();
                writer.WriteEndObject();
                Range("source_range", 0, music.Duration);
                Item();
                Reference(music.Path, music.Duration);
                writer.WriteEndObject();
            }

            writer.WriteStartObject();
            Common("Timeline.1", title);
            writer.WriteStartObject("metadata");
            writer.WriteStartObject(Namespace);
            writer.WriteNumber("version", 1);
            if (timeline.Texts.Count > 0)
            {
                writer.WriteStartArray("texts");
                foreach (var text in timeline.Texts) EditProject.WriteText(writer, text);
                writer.WriteEndArray();
            }

            if (markers is { Count: > 0 })
            {
                writer.WriteStartArray("markers");
                foreach (var marker in markers.OrderBy(m => m.Time))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("time", marker.Time);
                    if (marker.Name is { } name) writer.WriteString("name", name);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteNull("global_start_time");
            writer.WriteStartObject("tracks");
            Common("Stack.1", "tracks");
            writer.WriteStartObject("metadata");
            writer.WriteEndObject();
            writer.WriteNull("source_range");
            Item();
            writer.WriteStartArray("children");
            Track("V1", "Video", null, () =>
            {
                foreach (var clip in timeline.Clips) Clip(clip);
            });
            if (context.HasAudio)
                Track("A1", "Audio", RoleSourceAudio, () =>
                {
                    foreach (var clip in timeline.Clips) Clip(clip);
                });
            if (timeline.Music is { } music) Track("A2", "Audio", RoleMusic, () => Music(music));
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Utf8.GetString(stream.ToArray());
    }

    public static OtioImport? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!Schema(root).StartsWith("Timeline.", StringComparison.Ordinal)) return null;
            if (!root.TryGetProperty("tracks", out var stack) || stack.ValueKind != JsonValueKind.Object) return null;
            if (!stack.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array) return null;

            var tracks = children.EnumerateArray().Where(t => Schema(t).StartsWith("Track.", StringComparison.Ordinal) && Enabled(t)).ToArray();
            var skipped = children.GetArrayLength() - tracks.Length;
            var main = Array.FindIndex(tracks, t => Kind(t) == "Video");
            if (main < 0) main = Array.FindIndex(tracks, t => Kind(t) == "Audio" && Role(t) != RoleMusic);
            if (main < 0) return null;

            var sources = new List<string>();
            var durations = new List<long>();
            var clips = new List<EditClip>();
            double fps = 0;
            foreach (var item in Children(tracks[main]))
            {
                if (!Schema(item).StartsWith("Clip.", StringComparison.Ordinal) || !Enabled(item) || Media(item) is not { } media)
                {
                    skipped++;
                    continue;
                }

                var available = media.Available;
                var range = TryRange(item, "source_range") ?? available;
                if (range is not { } span)
                {
                    skipped++;
                    continue;
                }

                if (fps <= 0) fps = span.Rate;
                var origin = available?.Start ?? 0;
                var start = Math.Max(0, span.Start - origin);
                var end = span.Start - origin + span.Duration;
                if (end <= start)
                {
                    skipped++;
                    continue;
                }

                var source = sources.FindIndex(p => string.Equals(p, media.Path, StringComparison.Ordinal));
                if (source < 0)
                {
                    sources.Add(media.Path);
                    durations.Add(0);
                    source = sources.Count - 1;
                }

                durations[source] = Math.Max(durations[source], Math.Max(end, available?.Duration ?? 0));
                clips.Add(Clip(item, start, end, source, media.Path));
            }

            if (clips.Count == 0) return null;

            EditMusic? music = null;
            for (var i = 0; i < tracks.Length; i++)
            {
                if (i == main) continue;
                var track = tracks[i];
                var role = Role(track);
                var audio = Kind(track) == "Audio";
                if (role == RoleSourceAudio) continue;
                if (audio && music is null && Music(track, sources, role == RoleMusic) is { } found)
                {
                    music = found.Music;
                    skipped += found.Others;
                    continue;
                }

                if (audio && role is null && Mirrors(track, sources)) continue;
                skipped += Children(track).Count(c => !Schema(c).StartsWith("Gap.", StringComparison.Ordinal));
            }

            var (texts, markers) = TimelineExtras(root);
            var name = root.TryGetProperty("name", out var title) && title.ValueKind == JsonValueKind.String ? title.GetString() : null;
            return new OtioImport(name, fps, sources, durations, clips, texts, markers, music, skipped);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    internal static string FileUrl(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        var parts = path.Replace('\\', '/').Split('/');
        var escaped = string.Join('/', parts.Select(p => p.Length == 2 && char.IsAsciiLetter(p[0]) && p[1] == ':' ? p : Uri.EscapeDataString(p)));
        return escaped.StartsWith('/') ? "file://" + escaped : IsDrive(escaped) ? "file:///" + escaped : escaped;
    }

    internal static string LocalPath(string url)
    {
        if (!url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) return url;
        var rest = url[5..];
        if (rest.StartsWith("//", StringComparison.Ordinal))
        {
            rest = rest[2..];
            if (rest.StartsWith("localhost/", StringComparison.OrdinalIgnoreCase)) rest = rest[9..];
        }

        rest = Uri.UnescapeDataString(rest);
        if (rest.Length >= 3 && rest[0] == '/' && IsDrive(rest[1..])) rest = rest[1..];
        return IsDrive(rest) ? rest.Replace('/', '\\') : rest;
    }

    private static bool IsDrive(string path) => path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';

    private static string Schema(JsonElement item)
        => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("OTIO_SCHEMA", out var schema) && schema.ValueKind == JsonValueKind.String
            ? schema.GetString() ?? string.Empty
            : string.Empty;

    private static bool Enabled(JsonElement item)
        => !item.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.False;

    private static string? Kind(JsonElement track)
        => track.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String ? kind.GetString() : null;

    private static JsonElement[] Children(JsonElement track)
        => track.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array ? children.EnumerateArray().ToArray() : Array.Empty<JsonElement>();

    private static JsonElement? Ours(JsonElement item)
        => item.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object
            && metadata.TryGetProperty(Namespace, out var ours) && ours.ValueKind == JsonValueKind.Object
                ? ours
                : null;

    private static string? Role(JsonElement track)
        => Ours(track) is { } ours && ours.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.String ? role.GetString() : null;

    private readonly record struct Span(long Start, long Duration, double Rate);

    private sealed record MediaRef(string Path, Span? Available);

    private static long Ticks(JsonElement time)
    {
        var rate = time.GetProperty("rate").GetDouble();
        var value = time.GetProperty("value").GetDouble();
        if (!double.IsFinite(rate) || rate <= 0 || !double.IsFinite(value)) throw new FormatException("Gecersiz zaman");
        return checked((long)Math.Round(value / rate * EditTime.TicksPerSecond, MidpointRounding.AwayFromZero));
    }

    private static Span? TryRange(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var range) || range.ValueKind != JsonValueKind.Object) return null;
        var duration = range.GetProperty("duration");
        var start = range.TryGetProperty("start_time", out var at) && at.ValueKind == JsonValueKind.Object ? Ticks(at) : 0;
        return new Span(start, Ticks(duration), duration.GetProperty("rate").GetDouble());
    }

    private static MediaRef? Media(JsonElement clip)
    {
        JsonElement reference;
        if (clip.TryGetProperty("media_references", out var references) && references.ValueKind == JsonValueKind.Object)
        {
            var key = clip.TryGetProperty("active_media_reference_key", out var active) && active.ValueKind == JsonValueKind.String ? active.GetString() ?? MediaKey : MediaKey;
            if (!references.TryGetProperty(key, out reference)) return null;
        }
        else if (!clip.TryGetProperty("media_reference", out reference))
        {
            return null;
        }

        if (!Schema(reference).StartsWith("ExternalReference.", StringComparison.Ordinal)) return null;
        if (!reference.TryGetProperty("target_url", out var url) || url.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(url.GetString())) return null;
        return new MediaRef(LocalPath(url.GetString()!), TryRange(reference, "available_range"));
    }

    private static EditClip Clip(JsonElement item, long start, long end, int source, string path)
    {
        var name = item.TryGetProperty("name", out var title) && title.ValueKind == JsonValueKind.String ? EditClip.NormalizeLabel(title.GetString()) : null;
        var plain = new EditClip(start, end) { Source = source, Label = name == CutList.FileName(path) ? null : name };
        if (Ours(item) is not { } ours) return plain;
        T Field<T>(string key, Func<JsonElement, T> read, T fallback)
        {
            if (!ours.TryGetProperty(key, out var value)) return fallback;
            try
            {
                return read(value);
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException or OverflowException)
            {
                return fallback;
            }
        }

        return new EditClip(start, end, Field("speed", v => CutList.Speed(v.GetDecimal()), 1m), Field("reversed", v => v.GetBoolean(), false))
        {
            Source = source,
            Label = Field("label", v => v.GetString(), (string?)null),
            Group = Field("group", v => (int?)v.GetInt32(), null),
            Transition = Field("transition", EditProject.ReadTransition, null),
            Effects = Field("effects", EditProject.ReadEffects, ClipEffects.None),
        };
    }

    private static bool Mirrors(JsonElement track, IReadOnlyList<string> sources)
        => Children(track).Select(Media).OfType<MediaRef>().Any(m => sources.Contains(m.Path, StringComparer.Ordinal));

    private static (EditMusic Music, int Others)? Music(JsonElement track, IReadOnlyList<string> sources, bool declared)
    {
        if (!declared && Mirrors(track, sources)) return null;
        long at = 0;
        var items = Children(track);
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var schema = Schema(item);
            var span = TryRange(item, "source_range");
            if (schema.StartsWith("Gap.", StringComparison.Ordinal))
            {
                at += span?.Duration ?? 0;
                continue;
            }

            if (!schema.StartsWith("Clip.", StringComparison.Ordinal) || !Enabled(item) || Media(item) is not { } media)
            {
                at += span?.Duration ?? 0;
                continue;
            }

            var duration = media.Available?.Duration ?? (span is { } s ? s.Start + s.Duration : 0);
            if (duration <= 0) return null;
            var level = Ours(item) is { } ours && ours.TryGetProperty("level", out var db) && db.ValueKind == JsonValueKind.Number ? EditMusic.ClampLevel(db.GetDouble()) : 0;
            var rest = items.Skip(i + 1).Count(c => !Schema(c).StartsWith("Gap.", StringComparison.Ordinal));
            return (new EditMusic(media.Path, duration, at, level), rest);
        }

        return null;
    }

    private static (IReadOnlyList<TextLayer> Texts, IReadOnlyList<EditMarker> Markers) TimelineExtras(JsonElement root)
    {
        if (Ours(root) is not { } ours) return (Array.Empty<TextLayer>(), Array.Empty<EditMarker>());
        IReadOnlyList<TextLayer> texts = Array.Empty<TextLayer>();
        IReadOnlyList<EditMarker> markers = Array.Empty<EditMarker>();
        try
        {
            if (ours.TryGetProperty("texts", out var layers) && layers.ValueKind == JsonValueKind.Array)
                texts = layers.EnumerateArray().Select(EditProject.ReadText).ToArray();
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException or OverflowException)
        {
            texts = Array.Empty<TextLayer>();
        }

        try
        {
            if (ours.TryGetProperty("markers", out var marks) && marks.ValueKind == JsonValueKind.Array)
                markers = marks.EnumerateArray().Select(EditProject.ReadMarker).OrderBy(m => m.Time).ToArray();
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException or OverflowException)
        {
            markers = Array.Empty<EditMarker>();
        }

        return (texts, markers);
    }
}
