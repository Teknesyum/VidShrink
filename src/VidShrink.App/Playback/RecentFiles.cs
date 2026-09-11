using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VidShrink.App.Playback;

internal sealed class RecentFiles
{
    internal const int Capacity = 10;
    internal const string FileName = "player-recent.json";

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly List<string> _items = new();

    internal IReadOnlyList<string> Items => _items.ToArray();

    internal void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var full = Path.GetFullPath(path);
        _items.RemoveAll(item => PathComparer.Equals(item, full));
        _items.Insert(0, full);
        if (_items.Count > Capacity) _items.RemoveRange(Capacity, _items.Count - Capacity);
    }

    internal bool Remove(string path)
        => _items.RemoveAll(item => PathComparer.Equals(item, Path.GetFullPath(path))) > 0;

    internal static RecentFiles Load(string? file)
    {
        var recent = new RecentFiles();
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return recent;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root) return recent;
            if (root["files"] is not JsonArray files) return recent;
            for (var index = files.Count - 1; index >= 0; index--)
                if ((string?)files[index] is { Length: > 0 } path) recent.Add(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            return new RecentFiles();
        }

        return recent;
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
                writer.WriteStartArray("files");
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
