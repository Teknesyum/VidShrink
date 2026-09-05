using System.Text.Json;
using System.Text.Json.Nodes;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// Uygulama katmanının kendi kalıcı seçimleri. <see cref="UpdateSettings"/>
/// <c>VidShrink.Core</c>'da durur ve T173 motor kararını değiştirmediği için oraya yeni
/// alan eklemez; bunun yerine aynı <c>settings.json</c> dosyasına ek anahtarlar yazar.
/// <see cref="UpdateSettings.Save"/> dosyayı <see cref="System.IO.FileMode.Create"/> ile
/// tümden yeniden yazdığından, <see cref="Save"/> her zaman <see cref="UpdateSettings.Save"/>
/// çağrısından <b>sonra</b> koşup dosyayı okuyup birleştirir; aksi halde bu sınıfın
/// anahtarları bir sonraki kayıtta silinir.
/// </summary>
public sealed class AppSettings
{
    public int AdvMode { get; set; }
    public int AdvCrf { get; set; }
    public int AdvPreset { get; set; }
    public int AdvAudioKbps { get; set; }
    public int AdvAudioChannels { get; set; }
    public int AdvMinResolution { get; set; }
    public int AdvMinFps { get; set; }
    public int AdvEncoderPath { get; set; }
    public int AdvCodecLock { get; set; }

    /// <summary>0 = kaynağın yanı, 1 = sabit klasör.</summary>
    public int OutputFolderMode { get; set; }
    public string OutputFolder { get; set; } = "";

    public bool AdvancedDefaultOpen { get; set; }

    /// <summary>0 = otomatik, 1 = elle.</summary>
    public int FfmpegPathMode { get; set; }
    public string FfmpegPath { get; set; } = "";

    public static AppSettings Load(string? path = null)
    {
        var file = path ?? UpdateSettings.DefaultPath;
        var settings = new AppSettings();
        try
        {
            if (!File.Exists(file)) return settings;
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return settings;
            var root = document.RootElement;

            ReadInt(root, "advMode", value => settings.AdvMode = value);
            ReadInt(root, "advCrf", value => settings.AdvCrf = value);
            ReadInt(root, "advPreset", value => settings.AdvPreset = value);
            ReadInt(root, "advAudioKbps", value => settings.AdvAudioKbps = value);
            ReadInt(root, "advAudioChannels", value => settings.AdvAudioChannels = value);
            ReadInt(root, "advMinResolution", value => settings.AdvMinResolution = value);
            ReadInt(root, "advMinFps", value => settings.AdvMinFps = value);
            ReadInt(root, "advEncoderPath", value => settings.AdvEncoderPath = value);
            ReadInt(root, "advCodecLock", value => settings.AdvCodecLock = value);
            ReadInt(root, "outputFolderMode", value => settings.OutputFolderMode = value);
            ReadString(root, "outputFolder", value => settings.OutputFolder = value);
            ReadBool(root, "advancedDefaultOpen", value => settings.AdvancedDefaultOpen = value);
            ReadInt(root, "ffmpegPathMode", value => settings.FfmpegPathMode = value);
            ReadString(root, "ffmpegPath", value => settings.FfmpegPath = value);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // Okunamayan ayar varsayılana düşer; açılış hiçbir koşulda durmaz.
        }
        return settings;
    }

    private static void ReadBool(JsonElement root, string name, Action<bool> apply)
    {
        if (root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False) apply(value.GetBoolean());
    }

    private static void ReadInt(JsonElement root, string name, Action<int> apply)
    {
        if (root.TryGetProperty(name, out var value) && value.TryGetInt32(out var found)) apply(found);
    }

    private static void ReadString(JsonElement root, string name, Action<string> apply)
    {
        if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) apply(value.GetString() ?? "");
    }

    /// <summary>
    /// Var olan dosyayı okuyup kendi anahtarlarını üstüne yazar; <see cref="UpdateSettings"/>'in
    /// yazdığı 25 anahtara dokunmaz, siler de değiştirmez de.
    /// </summary>
    public void Save(string? path = null)
    {
        var file = path ?? UpdateSettings.DefaultPath;
        var folder = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        JsonObject root;
        try
        {
            root = File.Exists(file) && JsonNode.Parse(File.ReadAllText(file)) is JsonObject existing
                ? existing
                : new JsonObject();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            root = new JsonObject();
        }

        root["advMode"] = AdvMode;
        root["advCrf"] = AdvCrf;
        root["advPreset"] = AdvPreset;
        root["advAudioKbps"] = AdvAudioKbps;
        root["advAudioChannels"] = AdvAudioChannels;
        root["advMinResolution"] = AdvMinResolution;
        root["advMinFps"] = AdvMinFps;
        root["advEncoderPath"] = AdvEncoderPath;
        root["advCodecLock"] = AdvCodecLock;
        root["outputFolderMode"] = OutputFolderMode;
        root["outputFolder"] = OutputFolder;
        root["advancedDefaultOpen"] = AdvancedDefaultOpen;
        root["ffmpegPathMode"] = FfmpegPathMode;
        root["ffmpegPath"] = FfmpegPath;

        using var stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        root.WriteTo(writer);
    }
}
