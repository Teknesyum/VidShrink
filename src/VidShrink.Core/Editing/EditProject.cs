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

/// <summary>
/// Duzenleyicinin diske yazilan durumu: kaynak kimligi, kesim listesi, klip ayarlari ve metin
/// katmani. Geri al / ileri al yigini yazilmaz. Okuma hicbir kosulda firlatmaz: bozuk dosya,
/// eksik alan, gecersiz deger ve <see cref="CurrentVersion"/>'dan buyuk surum <c>null</c> doner.
/// </summary>
public sealed class EditProject
{
    public const int CurrentVersion = 1;

    public const string Extension = ".vsproj.json";

    private static readonly UTF8Encoding Utf8 = new(false);

    private EditProject(SourceStamp source, long sourceDuration, IReadOnlyList<EditClip> clips, IReadOnlyList<TextLayer> texts, string? exportMode)
    {
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

    public static EditProject From(SourceStamp source, EditTimeline timeline, string? exportMode = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timeline);
        var duration = timeline.SourceDuration ?? timeline.Clips.Select(c => c.SourceEnd).DefaultIfEmpty(1).Max();
        return new EditProject(source, duration, timeline.Clips.ToArray(), timeline.Texts.ToArray(), exportMode);
    }

    /// <summary>Kayitli her parca <paramref name="sourceDuration"/> icinde kaliyorsa <c>true</c>.</summary>
    public bool FitsIn(long sourceDuration) => Clips.All(c => c.SourceEnd <= sourceDuration);

    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", CurrentVersion);
            writer.WriteStartObject("source");
            writer.WriteString("path", Source.Path);
            writer.WriteNumber("size", Source.Size);
            writer.WriteNumber("modifiedUtcTicks", Source.ModifiedUtcTicks);
            writer.WriteEndObject();
            writer.WriteNumber("sourceDuration", SourceDuration);
            if (ExportMode is { Length: > 0 } mode) writer.WriteString("exportMode", mode);

            writer.WriteStartArray("clips");
            foreach (var clip in Clips) WriteClip(writer, clip);
            writer.WriteEndArray();

            writer.WriteStartArray("texts");
            foreach (var text in Texts) WriteText(writer, text);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Utf8.GetString(stream.ToArray());
    }

    public static EditProject? Parse(string? json)
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
            var clips = root.GetProperty("clips").EnumerateArray().Select(ReadClip).ToArray();
            var texts = root.TryGetProperty("texts", out var layers) ? layers.EnumerateArray().Select(ReadText).ToArray() : Array.Empty<TextLayer>();
            var mode = root.TryGetProperty("exportMode", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

            _ = new EditTimeline(clips, duration, texts);
            var project = new EditProject(stamp, duration, clips, texts, mode);
            return project.FitsIn(duration) ? project : null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    public static EditProject? Read(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path, Utf8)) : null;
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
        var effects = clip.Effects;
        if (!effects.IsNeutral)
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
            writer.WriteNumber("fadeIn", effects.FadeIn);
            writer.WriteNumber("fadeOut", effects.FadeOut);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static EditClip ReadClip(JsonElement item)
    {
        var clip = new EditClip(item.GetProperty("start").GetInt64(), item.GetProperty("end").GetInt64(),
            item.GetProperty("speed").GetDecimal(), item.GetProperty("reversed").GetBoolean());
        if (!item.TryGetProperty("effects", out var e)) return clip;

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
        };
        return clip with { Effects = effects.Normalized() };
    }

    private static void WriteText(Utf8JsonWriter writer, TextLayer text)
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

    private static TextLayer ReadText(JsonElement item)
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
            Keyframes = item.GetProperty("keyframes").EnumerateArray()
                .Select(k => new TextKeyframe(k.GetProperty("offset").GetInt64(), k.GetProperty("x").GetDouble(), k.GetProperty("y").GetDouble()))
                .ToArray(),
        };
        text.Validate();
        return text;
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
