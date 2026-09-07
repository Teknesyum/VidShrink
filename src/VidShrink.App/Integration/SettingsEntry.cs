using System.Text.Json;
using System.Text.Json.Nodes;

namespace VidShrink.App.Integration;

/// <summary>
/// <c>settings.json</c> içindeki tek bir anahtarı okuyup yazan ortak taban. Dosya
/// <see cref="AppSettings"/> ile paylaşıldığı için yazma tarafı önce var olan nesneyi okur,
/// yalnız verilen anahtarı değiştirir ve tümünü geri yazar; başka hiçbir anahtar silinmez.
///
/// <para>Okuma tarafı hiç fırlatmaz: dosya yoksa, bozuksa ya da açılamıyorsa
/// <c>null</c> döner ve çağıran "kayıt yok" davranışına düşer.</para>
/// </summary>
internal static class SettingsEntry
{
    /// <summary>Anahtarın değeri; dosya yoksa ya da okunamazsa <c>null</c>.</summary>
    internal static JsonElement? Read(string file, string key)
    {
        try
        {
            if (!File.Exists(file)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            return document.RootElement.TryGetProperty(key, out var value) ? value.Clone() : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Anahtarı yazar; dosyadaki diğer anahtarlara dokunmaz.</summary>
    internal static void Write(string file, string key, JsonNode? value)
    {
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

        root[key] = value;

        using var stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        root.WriteTo(writer);
    }
}
