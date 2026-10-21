using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VidShrink.Core.Editing;

/// <summary>
/// Kaynak dosyanin kimligi: tam yol, bayt boyu ve son yazma zamani. Kayitli proje ancak
/// boy ve zaman ayniysa o kaynaga aittir; yol tasinmis olabilir, o yuzden eslesmeye girmez.
/// </summary>
public sealed record SourceStamp(string Path, long Size, long ModifiedUtcTicks)
{
    public static SourceStamp? Of(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? new SourceStamp(info.FullName, info.Length, info.LastWriteTimeUtc.Ticks) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public bool Matches(SourceStamp other) => Size == other.Size && ModifiedUtcTicks == other.ModifiedUtcTicks;
}

/// <summary>Sonradan eklenen kaynagin kimligi ve suresi; sirasi listedeki yerinin bir fazlasidir.</summary>
public sealed record ExtraSource(SourceStamp Stamp, long Duration);

/// <summary>Projeden pencere acmadan kurulan teslim isi: kaynak yollari (ilk kaynak basta), cizelge, kip ve ses izi.</summary>
public sealed record EditProjectJob(IReadOnlyList<string> Sources, EditTimeline Timeline, ExportMode Mode, int AudioTrack);

/// <summary>
/// Duzenleyicinin diske yazilan durumu: kaynak kimligi, kesim listesi, klip ayarlari ve metin
/// katmani. Geri al / ileri al yigini yazilmaz. Okuma hicbir kosulda firlatmaz: bozuk dosya,
/// eksik alan, gecersiz deger ve <see cref="CurrentVersion"/>'dan buyuk surum <c>null</c> doner.
/// Tek kaynakli proje surum 1 olarak yazilir ve eski surumle ayni baytlari tasir; ek kaynak varsa
/// surum 2 yazilir (<c>sources</c> dizisi ve parca basina <c>source</c>).
/// </summary>
public sealed class EditProject
{
    public const int CurrentVersion = 2;

    public const string Extension = ".vsproj.json";

    private static readonly UTF8Encoding Utf8 = new(false);

    private EditProject(SourceStamp source, long sourceDuration, IReadOnlyList<EditClip> clips, IReadOnlyList<TextLayer> texts, string? exportMode,
        IReadOnlyList<ExtraSource> extraSources, IReadOnlyList<EditMarker> markers, int audioTrack, EditMusic? music, EditFrame frame)
    {
        Frame = frame.Normalized();
        Music = music;
        AudioTrack = audioTrack;
        Markers = markers;
        ExtraSources = extraSources;
        Source = source;
        SourceDuration = sourceDuration;
        Clips = clips;
        Texts = texts;
        ExportMode = exportMode;
    }

    public SourceStamp Source { get; }

    public long SourceDuration { get; }

    public IReadOnlyList<EditClip> Clips { get; }

    public IReadOnlyList<TextLayer> Texts { get; }

    public string? ExportMode { get; }

    public IReadOnlyList<ExtraSource> ExtraSources { get; }

    /// <summary>Cizelgenin isaretcileri, ana gore sirali. Bos liste dosyaya yazilmaz.</summary>
    public IReadOnlyList<EditMarker> Markers { get; }

    /// <summary>Teslime giden ses izi (<see cref="EditAudioTrack"/>). Varsayilan iz dosyaya yazilmaz.</summary>
    public int AudioTrack { get; }

    /// <summary>Muzik izi (<see cref="EditMusic"/>). Yoksa dosyaya yazilmaz; alani olmayan proje muziksiz acilir.</summary>
    public EditMusic? Music { get; }

    public EditFrame Frame { get; }

    public IReadOnlyList<string> MissingLuts { get; private init; } = Array.Empty<string>();

    public IReadOnlyList<EditAudioClip> AudioClips { get; private init; } = Array.Empty<EditAudioClip>();

    public IReadOnlyDictionary<EditTrack, EditTrackState> Tracks { get; private init; } = new Dictionary<EditTrack, EditTrackState>();

    public void ApplyTracks(EditTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        foreach (var track in EditTracks.All) timeline.SetTrack(track, Tracks.TryGetValue(track, out var state) ? state : default);
    }

    /// <summary>
    /// <paramref name="extraSources"/> cizelgenin ek kaynaklariyla ayni sirada ve ayni sayida olmalidir.
    /// </summary>
    public static EditProject From(SourceStamp source, EditTimeline timeline, string? exportMode = null, IReadOnlyList<SourceStamp>? extraSources = null,
        IReadOnlyList<EditMarker>? markers = null, int audioTrack = EditAudioTrack.Default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timeline);
        var stamps = extraSources ?? Array.Empty<SourceStamp>();
        if (stamps.Count != timeline.ExtraSources.Count)
            throw new ArgumentException("Her ek kaynaga bir kimlik gerekir", nameof(extraSources));
        var duration = timeline.SourceDuration ?? timeline.Clips.Where(c => c.Source == 0).Select(c => c.SourceEnd).DefaultIfEmpty(1).Max();
        var extras = stamps.Select((stamp, i) => new ExtraSource(stamp, timeline.ExtraSources[i])).ToArray();
        return new EditProject(source, duration, timeline.Clips.ToArray(), timeline.Texts.ToArray(), exportMode, extras, Sorted(markers ?? Array.Empty<EditMarker>()),
            audioTrack, timeline.Music, timeline.Frame)
        {
            AudioClips = timeline.AudioClips.ToArray(),
            Tracks = timeline.TrackStates.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
    }

    /// <summary>
    /// Ilk kaynagin parcalari <paramref name="sourceDuration"/> icinde, ek kaynaklarin parcalari kendi
    /// kayitli surelerinin icinde kaliyorsa <c>true</c>.
    /// </summary>
    public bool FitsIn(long sourceDuration)
        => Clips.All(c => c.Source == 0
            ? c.SourceEnd <= sourceDuration
            : c.Source <= ExtraSources.Count && c.SourceEnd <= ExtraSources[c.Source - 1].Duration);

    /// <summary>
    /// Pencere acmadan teslim isi. Her kaynagin kimligi <paramref name="stampOf"/> ile yeniden okunur; biri yoksa ya da
    /// boyu veya zamani degistiyse <c>null</c> doner ve <paramref name="stale"/> o kaynagin yoludur. Cizelge bossa
    /// <c>null</c> doner, <paramref name="stale"/> bos kalir. Kip projedeki addan duzenleyicinin kuraliyla okunur, taninmazsa Akilli.
    /// </summary>
    public EditProjectJob? ToJob(Func<string, SourceStamp?> stampOf, out string? stale)
    {
        ArgumentNullException.ThrowIfNull(stampOf);
        stale = null;
        foreach (var stamp in ExtraSources.Select(extra => extra.Stamp).Prepend(Source))
        {
            if (stampOf(stamp.Path) is { } now && stamp.Matches(now)) continue;
            stale = stamp.Path;
            return null;
        }

        if (Clips.Count == 0) return null;
        var timeline = new EditTimeline(Array.Empty<EditClip>(), SourceDuration, extraSources: ExtraSources.Select(extra => extra.Duration));
        timeline.Restore(Clips, Texts, Music, Frame, AudioClips);
        ApplyTracks(timeline);
        var mode = Enum.TryParse<Editing.ExportMode>(ExportMode, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed) ? parsed : Editing.ExportMode.Smart;
        return new EditProjectJob(ExtraSources.Select(extra => extra.Stamp.Path).Prepend(Source.Path).ToArray(), timeline, mode, AudioTrack);
    }

    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", ExtraSources.Count == 0 ? 1 : CurrentVersion);
            writer.WriteStartObject("source");
            writer.WriteString("path", Source.Path);
            writer.WriteNumber("size", Source.Size);
            writer.WriteNumber("modifiedUtcTicks", Source.ModifiedUtcTicks);
            writer.WriteEndObject();
            writer.WriteNumber("sourceDuration", SourceDuration);
            if (ExtraSources.Count > 0)
            {
                writer.WriteStartArray("sources");
                foreach (var extra in ExtraSources)
                {
                    writer.WriteStartObject();
                    writer.WriteString("path", extra.Stamp.Path);
                    writer.WriteNumber("size", extra.Stamp.Size);
                    writer.WriteNumber("modifiedUtcTicks", extra.Stamp.ModifiedUtcTicks);
                    writer.WriteNumber("duration", extra.Duration);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            if (ExportMode is { Length: > 0 } mode) writer.WriteString("exportMode", mode);
            if (AudioTrack != EditAudioTrack.Default) writer.WriteNumber("audioTrack", AudioTrack);

            writer.WriteStartArray("clips");
            foreach (var clip in Clips) WriteClip(writer, clip);
            writer.WriteEndArray();

            writer.WriteStartArray("texts");
            foreach (var text in Texts) WriteText(writer, text);
            writer.WriteEndArray();

            if (Markers.Count > 0)
            {
                writer.WriteStartArray("markers");
                foreach (var marker in Markers)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("time", marker.Time);
                    if (marker.Name is { } name) writer.WriteString("name", name);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            if (Music is { } music)
            {
                writer.WriteStartObject("music");
                writer.WriteString("path", music.Path);
                writer.WriteNumber("duration", music.Duration);
                writer.WriteNumber("start", music.Start);
                writer.WriteNumber("level", music.LevelDb);
                if (music.HasDuckSettings)
                {
                    writer.WriteBoolean("duck", music.Duck);
                    writer.WriteNumber("duckDb", music.DuckDb);
                    writer.WriteNumber("duckRelease", music.DuckReleaseMs);
                }
                writer.WriteEndObject();
            }

            if (AudioClips.Count > 0)
            {
                writer.WriteStartArray("audioClips");
                foreach (var clip in AudioClips)
                {
                    writer.WriteStartObject();
                    writer.WriteString("path", clip.Path);
                    writer.WriteNumber("duration", clip.Duration);
                    writer.WriteNumber("in", clip.In);
                    writer.WriteNumber("out", clip.Out);
                    writer.WriteNumber("start", clip.Start);
                    writer.WriteNumber("level", clip.LevelDb);
                    if (clip.Kind != EditAudioKind.Detached) writer.WriteString("kind", clip.Kind.ToString());
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            if (Tracks.Any(pair => !pair.Value.IsDefault))
            {
                writer.WriteStartObject("tracks");
                foreach (var (track, state) in Tracks.Where(pair => !pair.Value.IsDefault).OrderBy(pair => pair.Key))
                {
                    writer.WriteStartObject(EditTracks.Name(track));
                    if (state.Muted) writer.WriteBoolean("muted", true);
                    if (state.Hidden) writer.WriteBoolean("hidden", true);
                    if (state.Locked) writer.WriteBoolean("locked", true);
                    if (state.Solo) writer.WriteBoolean("solo", true);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            if (!Frame.IsSource)
            {
                writer.WriteStartObject("frame");
                writer.WriteNumber("num", Frame.Num);
                writer.WriteNumber("den", Frame.Den);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Utf8.GetString(stream.ToArray());
    }

    public static EditProject? Parse(string? json) => Parse(json, null);

    public static EditProject? Parse(string? json, string? folder)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var version = root.GetProperty("version").GetInt32();
            if (version < 1 || version > CurrentVersion) return null;

            var source = root.GetProperty("source");
            var path = source.GetProperty("path").GetString();
            if (string.IsNullOrWhiteSpace(path)) return null;
            var stamp = new SourceStamp(path, source.GetProperty("size").GetInt64(), source.GetProperty("modifiedUtcTicks").GetInt64());

            var duration = root.GetProperty("sourceDuration").GetInt64();
            var missing = new List<string>();
            var clips = root.GetProperty("clips").EnumerateArray().Select(ReadClip).Select(c => WithLut(c, folder, missing)).ToArray();
            var texts = root.TryGetProperty("texts", out var layers) ? layers.EnumerateArray().Select(ReadText).ToArray() : Array.Empty<TextLayer>();
            var mode = root.TryGetProperty("exportMode", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

            var extras = version >= 2 && root.TryGetProperty("sources", out var list)
                ? list.EnumerateArray().Select(ReadExtra).ToArray()
                : Array.Empty<ExtraSource>();

            var markers = root.TryGetProperty("markers", out var marks) ? Sorted(marks.EnumerateArray().Select(ReadMarker)) : Array.Empty<EditMarker>();

            _ = new EditTimeline(clips, duration, texts, extras.Select(e => e.Duration));
            var track = root.TryGetProperty("audioTrack", out var trackValue) && trackValue.ValueKind == JsonValueKind.Number
                && trackValue.TryGetInt32(out var parsed) ? parsed : EditAudioTrack.Default;
            var music = root.TryGetProperty("music", out var tune) && tune.ValueKind == JsonValueKind.Object
                ? new EditMusic(tune.GetProperty("path").GetString() ?? "", tune.GetProperty("duration").GetInt64(),
                    tune.TryGetProperty("start", out var at) ? at.GetInt64() : 0, tune.TryGetProperty("level", out var db) ? db.GetDouble() : 0)
                {
                    Duck = tune.TryGetProperty("duck", out var duck) && duck.GetBoolean(),
                    DuckDb = tune.TryGetProperty("duckDb", out var amount) ? amount.GetDouble() : EditMusic.DefaultDuckDb,
                    DuckReleaseMs = tune.TryGetProperty("duckRelease", out var release) ? release.GetInt32() : EditMusic.DefaultDuckReleaseMs
                }
                : null;
            var frame = root.TryGetProperty("frame", out var box) && box.ValueKind == JsonValueKind.Object
                ? new EditFrame(box.GetProperty("num").GetInt32(), box.GetProperty("den").GetInt32())
                : EditFrame.Source;
            var audio = root.TryGetProperty("audioClips", out var pieces) ? pieces.EnumerateArray().Select(ReadAudioClip).ToArray() : Array.Empty<EditAudioClip>();
            var tracks = root.TryGetProperty("tracks", out var lanes) && lanes.ValueKind == JsonValueKind.Object ? ReadTracks(lanes) : new Dictionary<EditTrack, EditTrackState>();
            var project = new EditProject(stamp, duration, clips, texts, mode, extras, markers, track, music, frame)
            {
                MissingLuts = missing.Distinct(StringComparer.Ordinal).ToArray(),
                AudioClips = audio,
                Tracks = tracks
            };
            return project.FitsIn(duration) ? project : null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    internal static EditAudioClip ReadAudioClip(JsonElement item)
        => new(item.GetProperty("path").GetString() ?? "", item.GetProperty("duration").GetInt64(), item.GetProperty("in").GetInt64(),
            item.GetProperty("out").GetInt64(), item.GetProperty("start").GetInt64(), item.TryGetProperty("level", out var db) ? db.GetDouble() : 0,
            item.TryGetProperty("kind", out var kind) && Enum.TryParse<EditAudioKind>(kind.GetString(), ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : EditAudioKind.Detached);

    internal static Dictionary<EditTrack, EditTrackState> ReadTracks(JsonElement lanes)
    {
        var tracks = new Dictionary<EditTrack, EditTrackState>();
        foreach (var lane in lanes.EnumerateObject())
        {
            if (EditTracks.Parse(lane.Name) is not { } track || lane.Value.ValueKind != JsonValueKind.Object) continue;
            bool Flag(string name) => lane.Value.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
            var state = EditTracks.Normalized(track, new EditTrackState(Flag("muted"), Flag("hidden"), Flag("locked"), Flag("solo")));
            if (!state.IsDefault) tracks[track] = state;
        }

        return tracks;
    }

    internal static EditMarker ReadMarker(JsonElement item)
        => new(item.GetProperty("time").GetInt64(), item.TryGetProperty("name", out var name) ? name.GetString() : null);

    private static EditMarker[] Sorted(IEnumerable<EditMarker> markers) => markers.OrderBy(m => m.Time).ToArray();

    private static ExtraSource ReadExtra(JsonElement item)
    {
        var path = item.GetProperty("path").GetString();
        if (string.IsNullOrWhiteSpace(path)) throw new FormatException("Kaynak yolu bos");
        return new ExtraSource(
            new SourceStamp(path, item.GetProperty("size").GetInt64(), item.GetProperty("modifiedUtcTicks").GetInt64()),
            item.GetProperty("duration").GetInt64());
    }

    public static EditProject? Read(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path, Utf8), System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Atomik yazim: icerik ayni klasordeki gecici dosyaya iner, sonra hedefin yerine tasinir.
    /// Yazim dusarse hedef eski haliyle kalir ve gecici dosya silinir.
    /// </summary>
    public bool Write(string path) => WriteAtomic(path, Utf8.GetBytes(ToJson()), null);

    internal static bool WriteAtomic(string path, byte[] bytes, Action<string>? beforeCommit)
    {
        string? temp = null;
        try
        {
            var full = System.IO.Path.GetFullPath(path);
            var folder = System.IO.Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temp, bytes);
            beforeCommit?.Invoke(temp);
            Commit(temp, full);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Discard(temp);
            return false;
        }
    }

    /// <summary>
    /// Hedef bir an baska bir surecin elindeyse (tarayici, dizinleyici) yer degistirme duser;
    /// kisa araliklarla uc kez denenir, sonuncusu hatayi yukari birakir.
    /// </summary>
    private static void Commit(string temp, string full)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temp, full, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < CommitAttempts && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(CommitRetryMs);
            }
        }
    }

    private const int CommitAttempts = 3;
    private const int CommitRetryMs = 20;

    private static void Discard(string? temp)
    {
        if (temp is null) return;
        try
        {
            File.Delete(temp);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void WriteClip(Utf8JsonWriter writer, EditClip clip)
    {
        writer.WriteStartObject();
        writer.WriteNumber("start", clip.SourceStart);
        writer.WriteNumber("end", clip.SourceEnd);
        writer.WriteNumber("speed", clip.Speed);
        writer.WriteBoolean("reversed", clip.Reversed);
        if (clip.Source != 0) writer.WriteNumber("source", clip.Source);
        if (clip.Label is { } label) writer.WriteString("label", label);
        if (clip.Group is { } group) writer.WriteNumber("group", group);
        if (clip.Smooth) writer.WriteBoolean("smooth", true);
        if (clip.Transition is { } transition)
        {
            writer.WriteStartObject("transition");
            writer.WriteString("kind", ClipTransition.Key(transition.Kind));
            writer.WriteNumber("duration", transition.Duration);
            writer.WriteEndObject();
        }

        if (!clip.Effects.IsNeutral) WriteEffects(writer, clip.Effects);

        writer.WriteEndObject();
    }

    internal static void WriteEffects(Utf8JsonWriter writer, ClipEffects effects)
    {
        writer.WriteStartObject("effects");
        writer.WriteNumber("cropLeft", effects.CropLeft);
        writer.WriteNumber("cropTop", effects.CropTop);
        writer.WriteNumber("cropRight", effects.CropRight);
        writer.WriteNumber("cropBottom", effects.CropBottom);
        writer.WriteNumber("rotation", effects.Rotation);
        writer.WriteBoolean("flipH", effects.FlipH);
        writer.WriteBoolean("flipV", effects.FlipV);
        writer.WriteNumber("volumeDb", effects.VolumeDb);
        writer.WriteBoolean("muted", effects.Muted);
        writer.WriteNumber("brightness", effects.Brightness);
        writer.WriteNumber("contrast", effects.Contrast);
        writer.WriteNumber("saturation", effects.Saturation);
        writer.WriteNumber("temperature", effects.Temperature);
        writer.WriteBoolean("denoise", effects.Denoise);
        writer.WriteBoolean("loudnorm", effects.Loudnorm);
        writer.WriteBoolean("compressor", effects.Compressor);
        writer.WriteBoolean("limiter", effects.Limiter);
        writer.WriteNumber("bassDb", effects.BassDb);
        writer.WriteNumber("midDb", effects.MidDb);
        writer.WriteNumber("trebleDb", effects.TrebleDb);
        writer.WriteNumber("fadeIn", effects.FadeIn);
        writer.WriteNumber("fadeOut", effects.FadeOut);
        if (effects.HasTransform)
        {
            writer.WriteNumber("scale", effects.Scale);
            writer.WriteNumber("offsetX", effects.OffsetX);
            writer.WriteNumber("offsetY", effects.OffsetY);
            writer.WriteNumber("angle", effects.Angle);
        }

        ClipGrade.Write(writer, effects);
        if (effects.HasFit)
        {
            writer.WriteNumber("fitNum", effects.FitNum);
            writer.WriteNumber("fitDen", effects.FitDen);
            writer.WriteString("padColor", effects.PadColor);
        }

        if (effects.HasBlur)
        {
            writer.WriteStartObject("blur");
            writer.WriteNumber("sigma", effects.BlurSigma);
            if (effects.HasBlurRegion)
            {
                writer.WriteNumber("x", effects.BlurX);
                writer.WriteNumber("y", effects.BlurY);
                writer.WriteNumber("width", effects.BlurWidth);
                writer.WriteNumber("height", effects.BlurHeight);
            }

            writer.WriteEndObject();
        }

        if (effects.VolumeKeys.Count > 0)
        {
            writer.WriteStartArray("volumeKeys");
            foreach (var key in effects.VolumeKeys)
            {
                writer.WriteStartObject();
                writer.WriteNumber("offset", key.Offset);
                writer.WriteNumber("db", key.Db);
                writer.WriteString("ease", KeyframeEaseKeys.Key(key.Ease));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        if (effects.CropKeys.Count > 0)
        {
            writer.WriteStartArray("cropKeys");
            foreach (var key in effects.CropKeys)
            {
                writer.WriteStartObject();
                writer.WriteNumber("offset", key.Offset);
                writer.WriteNumber("left", key.Left);
                writer.WriteNumber("top", key.Top);
                writer.WriteNumber("right", key.Right);
                writer.WriteNumber("bottom", key.Bottom);
                writer.WriteString("ease", KeyframeEaseKeys.Key(key.Ease));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        if (effects.Lut is { } lut)
        {
            writer.WriteString("lut", lut);
            writer.WriteNumber("lutMix", effects.LutMix);
        }

        writer.WriteEndObject();
    }

    private static EditClip WithLut(EditClip clip, string? folder, List<string> missing)
    {
        if (clip.Effects.Lut is not { } stored) return clip;
        if (LutFile.Resolve(stored, folder) is { } found) return clip with { Effects = clip.Effects with { Lut = found } };
        missing.Add(stored);
        return clip with { Effects = clip.Effects with { Lut = null, LutMix = ClipEffects.MaxLutMix } };
    }

    internal static ClipTransition ReadTransition(JsonElement item)
        => new(ClipTransition.FromKey(item.GetProperty("kind").GetString()) ?? throw new FormatException("Bilinmeyen gecis turu"),
            item.GetProperty("duration").GetInt64());

    private static EditClip ReadClip(JsonElement item)
    {
        var clip = new EditClip(item.GetProperty("start").GetInt64(), item.GetProperty("end").GetInt64(),
            item.GetProperty("speed").GetDecimal(), item.GetProperty("reversed").GetBoolean())
        {
            Source = item.TryGetProperty("source", out var source) ? source.GetInt32() : 0,
            Label = item.TryGetProperty("label", out var label) ? label.GetString() : null,
            Group = item.TryGetProperty("group", out var group) ? group.GetInt32() : null,
            Transition = item.TryGetProperty("transition", out var transition) ? ReadTransition(transition) : null,
            Smooth = item.TryGetProperty("smooth", out var smooth) && smooth.GetBoolean(),
        };
        return item.TryGetProperty("effects", out var e) ? clip with { Effects = ReadEffects(e) } : clip;
    }

    internal static ClipEffects ReadEffects(JsonElement e)
    {
        var effects = new ClipEffects
        {
            CropLeft = e.GetProperty("cropLeft").GetDouble(),
            CropTop = e.GetProperty("cropTop").GetDouble(),
            CropRight = e.GetProperty("cropRight").GetDouble(),
            CropBottom = e.GetProperty("cropBottom").GetDouble(),
            Rotation = e.GetProperty("rotation").GetInt32(),
            FlipH = e.GetProperty("flipH").GetBoolean(),
            FlipV = e.GetProperty("flipV").GetBoolean(),
            VolumeDb = e.GetProperty("volumeDb").GetDouble(),
            Muted = e.GetProperty("muted").GetBoolean(),
            FadeIn = e.GetProperty("fadeIn").GetInt64(),
            FadeOut = e.GetProperty("fadeOut").GetInt64(),
            Brightness = e.TryGetProperty("brightness", out var brightness) ? brightness.GetDouble() : 0,
            Contrast = e.TryGetProperty("contrast", out var contrast) ? contrast.GetDouble() : 1,
            Saturation = e.TryGetProperty("saturation", out var saturation) ? saturation.GetDouble() : 1,
            Temperature = e.TryGetProperty("temperature", out var temperature) ? temperature.GetInt32() : ClipEffects.NeutralTemperature,
            Denoise = e.TryGetProperty("denoise", out var denoise) && denoise.GetBoolean(),
            Loudnorm = e.TryGetProperty("loudnorm", out var loudnorm) && loudnorm.GetBoolean(),
            Compressor = e.TryGetProperty("compressor", out var compressor) && compressor.GetBoolean(),
            Limiter = e.TryGetProperty("limiter", out var limiter) && limiter.GetBoolean(),
            BassDb = e.TryGetProperty("bassDb", out var bass) ? bass.GetDouble() : 0,
            MidDb = e.TryGetProperty("midDb", out var mid) ? mid.GetDouble() : 0,
            TrebleDb = e.TryGetProperty("trebleDb", out var treble) ? treble.GetDouble() : 0,
            FitNum = e.TryGetProperty("fitNum", out var fitNum) ? fitNum.GetInt32() : 0,
            FitDen = e.TryGetProperty("fitDen", out var fitDen) ? fitDen.GetInt32() : 0,
            PadColor = e.TryGetProperty("padColor", out var padColor) ? padColor.GetString() ?? ClipEffects.DefaultPadColor : ClipEffects.DefaultPadColor,
            Scale = e.TryGetProperty("scale", out var scale) ? scale.GetDouble() : 1,
            OffsetX = e.TryGetProperty("offsetX", out var offsetX) ? offsetX.GetDouble() : 0,
            OffsetY = e.TryGetProperty("offsetY", out var offsetY) ? offsetY.GetDouble() : 0,
            Angle = e.TryGetProperty("angle", out var angle) ? angle.GetDouble() : 0,
            VolumeKeys = e.TryGetProperty("volumeKeys", out var volumeKeys)
                ? new KeyframeTrack<VolumeKey>(volumeKeys.EnumerateArray().Select(k => new VolumeKey(k.GetProperty("offset").GetInt64(), k.GetProperty("db").GetDouble(), Ease(k))))
                : KeyframeTrack<VolumeKey>.Empty,
            CropKeys = e.TryGetProperty("cropKeys", out var cropKeys)
                ? new KeyframeTrack<CropKey>(cropKeys.EnumerateArray().Select(k => new CropKey(k.GetProperty("offset").GetInt64(),
                    k.GetProperty("left").GetDouble(), k.GetProperty("top").GetDouble(), k.GetProperty("right").GetDouble(), k.GetProperty("bottom").GetDouble(), Ease(k))))
                : KeyframeTrack<CropKey>.Empty,
            Lut = e.TryGetProperty("lut", out var lut) && lut.ValueKind == JsonValueKind.String ? lut.GetString() : null,
            LutMix = e.TryGetProperty("lutMix", out var lutMix) && lutMix.ValueKind == JsonValueKind.Number ? lutMix.GetInt32() : ClipEffects.MaxLutMix,
        };
        if (e.TryGetProperty("blur", out var blur) && blur.ValueKind == JsonValueKind.Object)
            effects = effects with
            {
                BlurSigma = BlurNumber(blur, "sigma"),
                BlurX = BlurNumber(blur, "x"),
                BlurY = BlurNumber(blur, "y"),
                BlurWidth = BlurNumber(blur, "width"),
                BlurHeight = BlurNumber(blur, "height"),
            };

        return ClipGrade.Read(e, effects).Normalized();
    }

    private static double BlurNumber(JsonElement blur, string name)
        => blur.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;

    private static KeyframeEase Ease(JsonElement key)
        => KeyframeEaseKeys.Parse(key.TryGetProperty("ease", out var ease) ? ease.GetString() : null);

    internal static void WriteText(Utf8JsonWriter writer, TextLayer text)
    {
        writer.WriteStartObject();
        writer.WriteString("text", text.Text);
        writer.WriteNumber("start", text.Start);
        writer.WriteNumber("end", text.End);
        writer.WriteString("font", text.FontName);
        writer.WriteNumber("size", text.Size);
        writer.WriteNumber("color", text.Color);
        writer.WriteBoolean("bold", text.Bold);
        writer.WriteBoolean("italic", text.Italic);
        writer.WriteNumber("fadeIn", text.FadeIn);
        writer.WriteNumber("fadeOut", text.FadeOut);
        if (text.Outline is { } outline) writer.WriteNumber("outline", outline);
        writer.WriteNumber("shadow", text.Shadow);
        writer.WriteBoolean("box", text.Box);
        if (text.MotionIn != TextMotion.None || text.MotionInLength != 0)
        {
            writer.WriteString("motionIn", text.MotionIn.ToString().ToLowerInvariant());
            writer.WriteNumber("motionInLength", text.MotionInLength);
        }

        if (text.MotionOut != TextMotion.None || text.MotionOutLength != 0)
        {
            writer.WriteString("motionOut", text.MotionOut.ToString().ToLowerInvariant());
            writer.WriteNumber("motionOutLength", text.MotionOutLength);
        }

        writer.WriteStartArray("keyframes");
        foreach (var key in text.Keyframes)
        {
            writer.WriteStartObject();
            writer.WriteNumber("offset", key.Offset);
            writer.WriteNumber("x", key.X);
            writer.WriteNumber("y", key.Y);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    internal static TextLayer ReadText(JsonElement item)
    {
        var text = new TextLayer(item.GetProperty("text").GetString() ?? string.Empty, item.GetProperty("start").GetInt64(), item.GetProperty("end").GetInt64())
        {
            FontName = item.GetProperty("font").GetString() ?? TextLayer.DefaultFont,
            Size = item.GetProperty("size").GetDouble(),
            Color = item.GetProperty("color").GetUInt32(),
            Bold = item.GetProperty("bold").GetBoolean(),
            Italic = item.GetProperty("italic").GetBoolean(),
            FadeIn = item.GetProperty("fadeIn").GetInt64(),
            FadeOut = item.GetProperty("fadeOut").GetInt64(),
            Outline = item.TryGetProperty("outline", out var outline) && outline.ValueKind != JsonValueKind.Null ? outline.GetDouble() : null,
            Shadow = item.TryGetProperty("shadow", out var shadow) ? shadow.GetDouble() : 0,
            Box = item.TryGetProperty("box", out var box) && box.GetBoolean(),
            MotionIn = item.TryGetProperty("motionIn", out var motionIn) ? Motion(motionIn) : TextMotion.None,
            MotionInLength = item.TryGetProperty("motionInLength", out var inLength) ? inLength.GetInt64() : 0,
            MotionOut = item.TryGetProperty("motionOut", out var motionOut) ? Motion(motionOut) : TextMotion.None,
            MotionOutLength = item.TryGetProperty("motionOutLength", out var outLength) ? outLength.GetInt64() : 0,
            Keyframes = item.GetProperty("keyframes").EnumerateArray()
                .Select(k => new TextKeyframe(k.GetProperty("offset").GetInt64(), k.GetProperty("x").GetDouble(), k.GetProperty("y").GetDouble()))
                .ToArray(),
        };
        text.Validate();
        return text;
    }

    private static TextMotion Motion(JsonElement value)
    {
        var name = value.GetString();
        foreach (var motion in Enum.GetValues<TextMotion>())
            if (string.Equals(motion.ToString(), name, StringComparison.OrdinalIgnoreCase)) return motion;
        throw new FormatException("Bilinmeyen metin hareketi: " + name);
    }
}

/// <summary>
/// Otomatik kayit klasoru. Dosya adi kaynak yolunun ozetidir; her kaynagin tek kaydi olur.
/// Klasor ayar dosyasinin yanindadir, yani <c>VIDSHRINK_SETTINGS_PATH</c>'i izler. En yeni
/// <see cref="Cap"/> kayit tutulur, eskisi her yazimdan sonra silinir.
/// </summary>
public sealed class EditProjectStore
{
    public const string FolderName = "duzenleyici-projeleri";

    public const int DefaultCap = 50;

    public EditProjectStore(string folder, int cap = DefaultCap)
    {
        if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Klasor bos olamaz", nameof(folder));
        if (cap < 1) throw new ArgumentOutOfRangeException(nameof(cap), cap, "Tavan en az 1 olmalidir");
        Folder = folder;
        Cap = cap;
    }

    /// <summary>Son duzenlemeden sonra otomatik kaydin bekledigi sure.</summary>
    public static readonly TimeSpan AutosaveDelay = TimeSpan.FromSeconds(1);

    internal static bool Disabled { get; set; }

    public static string DefaultFolder => Path.Combine(Path.GetDirectoryName(UpdateSettings.DefaultPath) ?? ".", FolderName);

    public static EditProjectStore? Default => Disabled ? null : new EditProjectStore(DefaultFolder);

    public string Folder { get; }

    public int Cap { get; }

    public string PathFor(string source)
    {
        var full = Path.GetFullPath(source);
        var key = OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Path.Combine(Folder, Convert.ToHexString(hash, 0, 16).ToLowerInvariant() + EditProject.Extension);
    }

    public bool Save(EditProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.Write(PathFor(project.Source.Path))) return false;
        Prune();
        return true;
    }

    /// <summary>Kaynagin kaydi; yoksa, okunamiyorsa ya da baska bir yola aitse <c>null</c>.</summary>
    public EditProject? Load(string source)
    {
        if (EditProject.Read(PathFor(source)) is not { } project) return null;
        return PathEquality.Same(project.Source.Path, source) ? project : null;
    }

    /// <summary>En yeni <see cref="Cap"/> kaydin gerisini siler; silinen dosya sayisi doner.</summary>
    public int Prune()
    {
        var removed = 0;
        try
        {
            if (!Directory.Exists(Folder)) return 0;
            var old = new DirectoryInfo(Folder).EnumerateFiles("*" + EditProject.Extension)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ThenBy(f => f.Name, StringComparer.Ordinal)
                .Skip(Cap)
                .ToList();
            foreach (var file in old)
            {
                try
                {
                    file.Delete();
                    removed++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return removed;
    }
}
