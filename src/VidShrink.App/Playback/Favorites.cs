using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using VidShrink.Core;

namespace VidShrink.App.Playback;

internal enum FavoriteKind
{
    File,
    Folder,
    Address
}

/// <summary>
/// OYB-16: dosya, klasör ve adres tutan kalıcı sık kullanılanlar listesi. Adres diske
/// <see cref="MediaAddress.WithoutQuery"/> biçimiyle yazılır: sorgu, parça ve parola kayda girmez.
/// </summary>
internal sealed class Favorites
{
    internal const string FileName = "player-favorites.json";

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly List<string> _items = new();

    internal IReadOnlyList<string> Items => _items.ToArray();

    internal static FavoriteKind KindOf(string item)
        => MediaAddress.IsAddress(item) ? FavoriteKind.Address : Directory.Exists(item) ? FavoriteKind.Folder : FavoriteKind.File;

    internal static string? Normalize(string? item)
    {
        if (string.IsNullOrWhiteSpace(item)) return null;
        var text = item.Trim();
        if (MediaAddress.IsAddress(text)) return MediaAddress.WithoutQuery(text);
        try { return Path.GetFullPath(text); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    internal bool Contains(string? item)
        => Normalize(item) is { } key && _items.Any(existing => PathComparer.Equals(existing, key));

    internal bool Add(string? item)
    {
        if (Normalize(item) is not { } key || Contains(key)) return false;
        _items.Add(key);
        return true;
    }

    internal bool Remove(string? item)
        => Normalize(item) is { } key && _items.RemoveAll(existing => PathComparer.Equals(existing, key)) > 0;

    internal static Favorites Load(string? file)
    {
        var favorites = new Favorites();
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return favorites;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root) return favorites;
            if (root["items"] is not JsonArray items) return favorites;
            foreach (var node in items)
                if (node is JsonValue value && value.TryGetValue<string>(out var text)) favorites.Add(text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return new Favorites();
        }

        return favorites;
    }

    internal void Save(string? file)
    {
        if (string.IsNullOrEmpty(file)) return;
        try
        {
            var folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            var temp = file + ".tmp";
            using (var stream = File.Create(temp))
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteStartArray("items");
                foreach (var item in _items) writer.WriteStringValue(item);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            File.Move(temp, file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
