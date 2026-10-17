using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VidShrink.App.Playback;

/// <summary>
/// OYB-08: oynatıcının saklanan kuyruğu. <see cref="Items"/> boşsa kuyruk yoktur, yalnız çalan dosya
/// saklanır ve açılışta klasör gezintisi ondan sürer.
/// </summary>
internal sealed record PlayerQueue(IReadOnlyList<string> Items, string Current, bool Arranged, string? Source)
{
    internal const string FileName = "player-queue.json";

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal string Serialize()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteString("current", Current);
            writer.WriteBoolean("arranged", Arranged);
            writer.WriteString("source", Source);
            writer.WriteStartArray("items");
            foreach (var item in Items) writer.WriteStringValue(item);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Önce geçici dosyaya yazar, sonra yer değiştirir ki yarım yazım kuyruğu bozmasın.
    /// <c>null</c> kuyruk dosyayı siler.
    /// </summary>
    internal static void Save(string? file, PlayerQueue? queue)
    {
        if (string.IsNullOrEmpty(file)) return;
        try
        {
            if (queue is null)
            {
                if (File.Exists(file)) File.Delete(file);
                return;
            }
            var folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            var temp = file + ".tmp";
            File.WriteAllText(temp, queue.Serialize());
            File.Move(temp, file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Diskteki kuyruk. Artık olmayan dosyalar atılır; çalan dosya yoksa kalan ilk dosya çalan olur.
    /// Bozuk, boş ya da hepsi silinmiş kuyruk <c>null</c> döner.
    /// </summary>
    internal static PlayerQueue? Load(string? file, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return null;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root) return null;
            var items = new List<string>();
            if (root["items"] is JsonArray array)
                foreach (var node in array)
                    if (node is JsonValue value && value.TryGetValue<string>(out var item) && !string.IsNullOrWhiteSpace(item)
                        && exists(item) && !items.Contains(item, PathComparer))
                        items.Add(item);

            var current = root["current"] is JsonValue currentValue && currentValue.TryGetValue<string>(out var path)
                && !string.IsNullOrWhiteSpace(path) && exists(path) ? path : null;
            if (current is not null && items.Count > 0 && !items.Contains(current, PathComparer)) current = null;
            current ??= items.FirstOrDefault();
            if (current is null) return null;

            var arranged = root["arranged"] is JsonValue arrangedValue && arrangedValue.TryGetValue<bool>(out var flag) && flag;
            var source = root["source"] is JsonValue sourceValue && sourceValue.TryGetValue<string>(out var text) ? text : null;
            return new PlayerQueue(items, current, arranged, source);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
