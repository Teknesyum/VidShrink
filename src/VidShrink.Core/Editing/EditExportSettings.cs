using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VidShrink.Core.Editing;

/// <summary>
/// Duzenleyici tesliminin Tam kip ayarlari (DZB-54, DZB-58). Varsayilan deger bugunku sabitlerdir:
/// kaynak boyutu ve kare hizi, libx264 <c>medium</c>, CRF 18, AAC 192k. <see cref="Width"/>/<see cref="Height"/>
/// 0 ise kaynak (ya da klip geometrisinin tuvali), <see cref="Fps"/> 0 ise kaynak. <see cref="AudioOnly"/>
/// goruntuyu birakip yalniz sesi bir ses kabina yazar.
/// </summary>
public sealed record EditExportSettings
{
    public const string DefaultCodec = "libx264";
    public const int DefaultCrf = 18;
    public const int DefaultAudioKbps = 192;

    public static readonly IReadOnlyList<string> VideoCodecs = new[] { "libx264", "libx265", "libsvtav1" };
    public static readonly IReadOnlyList<double> FpsChoices = new[] { 24d, 25, 30, 50, 60 };
    public static readonly IReadOnlyList<int> CrfChoices = new[] { 18, 23, 28 };
    public static readonly IReadOnlyList<int> AudioKbpsChoices = new[] { 96, 128, 160, 192, 256, 320 };
    public static readonly IReadOnlyList<string> AudioOnlyExtensions = new[] { ".m4a", ".mp3" };

    public static EditExportSettings Default { get; } = new();

    public int Width { get; init; }
    public int Height { get; init; }
    public double Fps { get; init; }
    public string VideoCodec { get; init; } = DefaultCodec;
    public int Crf { get; init; } = DefaultCrf;
    public int AudioKbps { get; init; } = DefaultAudioKbps;
    public bool AudioOnly { get; init; }

    public bool IsDefault => Equals(Default);

    public bool HasSize => Width > 0 && Height > 0;

    /// <summary>Kodlayicinin hiz on ayari: SVT-AV1 sayi alir, x264/x265 <c>medium</c>.</summary>
    public string Preset => VideoCodec == "libsvtav1" ? "8" : "medium";

    public string AudioBitrate => AudioKbps.ToString(CultureInfo.InvariantCulture) + "k";

    /// <summary>Gecersiz alanin adi; gecerliyse <c>null</c>.</summary>
    public string? Invalid()
    {
        if (Width < 0 || Height < 0 || (Width == 0) != (Height == 0) || Width % 2 != 0 || Height % 2 != 0 || Width > 8192 || Height > 8192)
            return "width";
        if (!double.IsFinite(Fps) || Fps < 0 || Fps > 240) return "fps";
        if (!VideoCodecs.Contains(VideoCodec)) return "videoCodec";
        if (Crf < 0 || Crf > 51) return "crf";
        if (AudioKbps < 32 || AudioKbps > 512) return "audioKbps";
        return null;
    }

    /// <summary>Yalniz ses tesliminde kodlayici: <c>.mp3</c> icin libmp3lame, oteki her kapta AAC.</summary>
    public static string AudioOnlyCodec(string output)
        => string.Equals(Path.GetExtension(output), ".mp3", StringComparison.OrdinalIgnoreCase) ? "libmp3lame" : EditExport.AudioCodec;
}

/// <summary>Teslim on ayari: hazir hedef ya da kullanicinin kaydettigi ad.</summary>
public sealed record EditExportPreset
{
    public string Id { get; init; } = string.Empty;
    public string? Name { get; init; }
    public EditExportSettings Settings { get; init; } = EditExportSettings.Default;
}

/// <summary>
/// Teslim on ayarlarinin dosyasi (DZB-55). Bicim ve klasor Kucult'un on ayar dosyasininki
/// (<see cref="PresetLibrary.DefaultUserPath"/>, <c>schema</c>/<c>kind</c>/<c>presets</c>), dosya ayri ve
/// <c>kind</c> zorunlu: duzenleyici on ayari Kucult yongalarina, Kucult'unki buraya okunmaz.
/// </summary>
public static class EditExportPresetLibrary
{
    public const int SchemaVersion = 1;
    public const string FileKind = "vidshrink-editor-export-presets";
    public const string UserFileName = "editor-export-presets.json";

    /// <summary>Hazir hedefler: 1080p, 720p ve dikey 1080x1920; geri kalan alanlar varsayilan.</summary>
    public static readonly IReadOnlyList<EditExportPreset> BuiltIn = new[]
    {
        new EditExportPreset { Id = "1080p", Settings = new EditExportSettings { Width = 1920, Height = 1080 } },
        new EditExportPreset { Id = "720p", Settings = new EditExportSettings { Width = 1280, Height = 720 } },
        new EditExportPreset { Id = "vertical", Settings = new EditExportSettings { Width = 1080, Height = 1920 } }
    };

    public static string DefaultUserPath
    {
        get
        {
            var folder = Path.GetDirectoryName(PresetLibrary.DefaultUserPath);
            return Path.Combine(string.IsNullOrEmpty(folder) ? "." : folder, UserFileName);
        }
    }

    public static IReadOnlyList<EditExportPreset> Parse(string json)
    {
        var documentOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: documentOptions);
            if (root is JsonObject probe) _ = probe.Count;
        }
        catch (JsonException ex)
        {
            throw new PresetFileException(PresetFileError.NotJson, $"line {ex.LineNumber + 1}", ex);
        }
        catch (ArgumentException ex)
        {
            throw new PresetFileException(PresetFileError.NotJson, "duplicate key", ex);
        }

        if (root is not JsonObject obj || obj["presets"] is not JsonArray items)
            throw new PresetFileException(PresetFileError.NotPresetFile, "presets");

        if (obj["kind"] is not JsonValue kindValue || !kindValue.TryGetValue<string>(out var kind) ||
            !string.Equals(kind, FileKind, StringComparison.Ordinal))
            throw new PresetFileException(PresetFileError.NotPresetFile, "kind");

        if (obj["schema"] is not JsonValue schemaValue || !schemaValue.TryGetValue<int>(out var schema))
            throw new PresetFileException(PresetFileError.SchemaMissing, "schema");

        if (schema > SchemaVersion)
            throw new PresetFileException(PresetFileError.SchemaTooNew, schema.ToString(CultureInfo.InvariantCulture));

        var list = new List<EditExportPreset>();
        for (var i = 0; i < items.Count; i++)
        {
            EditExportPreset? preset;
            try
            {
                preset = items[i].Deserialize<EditExportPreset>(PresetLibrary.Options);
            }
            catch (JsonException ex)
            {
                throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{i}]{ex.Path?.TrimStart('$')}", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{i}]", ex);
            }

            if (preset is null)
                throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{i}]");

            Validate(preset, i);
            list.Add(preset);
        }

        return list;
    }

    public static string Serialize(IEnumerable<EditExportPreset> presets)
    {
        var root = new JsonObject
        {
            ["schema"] = SchemaVersion,
            ["kind"] = FileKind,
            ["presets"] = JsonSerializer.SerializeToNode(presets.ToList(), PresetLibrary.Options)
        };
        return root.ToJsonString(PresetLibrary.Options);
    }

    public static IReadOnlyList<EditExportPreset> LoadUser(string? path = null)
    {
        path ??= DefaultUserPath;
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : Array.Empty<EditExportPreset>();
    }

    public static IReadOnlyList<EditExportPreset> SaveUser(EditExportPreset preset, string? path = null)
    {
        path ??= DefaultUserPath;
        Validate(preset, 0);
        var list = LoadUser(path)
            .Where(existing => !string.Equals(existing.Id, preset.Id, StringComparison.OrdinalIgnoreCase))
            .Append(preset)
            .ToList();
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(path, Serialize(list), new UTF8Encoding(false));
        return list;
    }

    private static void Validate(EditExportPreset preset, int index)
    {
        if (string.IsNullOrWhiteSpace(preset.Id) || BuiltIn.Any(b => string.Equals(b.Id, preset.Id, StringComparison.OrdinalIgnoreCase)))
            throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{index}].id");
        if (preset.Settings?.Invalid() is { } field)
            throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{index}].settings.{field}");
        if (preset.Settings is null)
            throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{index}].settings");
    }
}

/// <summary>Yalniz ses teslimi istendi ama cizelgenin kaynaklarinda ses yok.</summary>
public sealed class EditExportNoAudioException : InvalidOperationException
{
    public const string LocaleKey = "editor.export.audio-only.no-audio";

    public EditExportNoAudioException() : base("Kaynakta ses yok")
    {
    }
}
