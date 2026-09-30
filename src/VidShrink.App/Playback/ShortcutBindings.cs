using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Input;
using VidShrink.App.Editing;
using VidShrink.Core;

namespace VidShrink.App.Playback;

internal enum ShortcutMap
{
    Player,
    Editor
}

internal sealed record ShortcutSlot(ShortcutMap Map, string Id, PlayerInput Default, PlayerInput? Current, string Label)
{
    internal bool Rebindable => Default.Kind != PlayerInputKind.Wheel;
}

/// <summary>
/// Kullanıcının değiştirdiği kısayollar. Yalnız varsayılandan ayrılan satırlar yazılır; satırın
/// kimliği varsayılan girdisinin yazımıdır (<see cref="Canon"/>), boş değer "atanmamış" demektir.
/// Çakışmada yeni atama kazanır: aynı girdiyi taşıyan öbür satır atanmamış kalır ve adı döner.
/// </summary>
internal static class ShortcutBindings
{
    internal const string FileName = "shortcuts.json";

    private static readonly Dictionary<string, string> PlayerOverrides = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> EditorOverrides = new(StringComparer.Ordinal);
    private static readonly object Gate = new();
    private static string? _file;
    private static bool _useDefault = true;
    private static bool _loaded;
    private static int _version;

    internal static event EventHandler? Changed;

    internal static string? DefaultFile
        => Path.GetDirectoryName(UpdateSettings.DefaultPath) is { Length: > 0 } folder ? Path.Combine(folder, FileName) : null;

    internal static string? Location => _useDefault ? DefaultFile : _file;

    internal static int Version
    {
        get
        {
            lock (Gate)
            {
                if (!_loaded) Read();
                return _version;
            }
        }
    }

    internal static void UseFile(string? file)
    {
        lock (Gate)
        {
            _useDefault = file is null;
            _file = file;
            Read();
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    internal static void Load()
    {
        lock (Gate) Read();
        Changed?.Invoke(null, EventArgs.Empty);
    }

    internal static bool HasOverrides(ShortcutMap map)
    {
        lock (Gate)
        {
            if (!_loaded) Read();
            return Overrides(map).Count > 0;
        }
    }

    internal static bool TryOverride(ShortcutMap map, string id, out PlayerInput? input)
    {
        lock (Gate)
        {
            if (!_loaded) Read();
            if (!Overrides(map).TryGetValue(id, out var value))
            {
                input = null;
                return false;
            }

            input = value.Length == 0 ? null : Parse(value);
            return true;
        }
    }

    internal static IReadOnlyList<ShortcutSlot> Slots(ShortcutMap map)
        => map == ShortcutMap.Player ? Keymap.ShortcutSlots() : EditorKeymap.ShortcutSlots();

    internal static IReadOnlyList<ShortcutSlot> Assign(ShortcutMap map, string id, PlayerInput input)
    {
        var slots = Slots(map);
        var target = slots.FirstOrDefault(s => s.Id == id) ?? throw new ArgumentException("Bilinmeyen kısayol satırı: " + id, nameof(id));
        var stolen = slots.Where(s => s.Id != id && s.Current is { } current && Same(current, input)).ToList();
        lock (Gate)
        {
            var overrides = Overrides(map);
            foreach (var slot in stolen) overrides[slot.Id] = "";
            if (Same(target.Default, input) && (target.Default.Symbol is null) == (input.Symbol is null)) overrides.Remove(id);
            else overrides[id] = Canon(input);
            Commit();
        }

        Changed?.Invoke(null, EventArgs.Empty);
        return stolen;
    }

    internal static void Reset(ShortcutMap map)
    {
        lock (Gate)
        {
            if (!_loaded) Read();
            Overrides(map).Clear();
            Commit();
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    internal static bool Same(PlayerInput a, PlayerInput b)
    {
        if (a.Kind != b.Kind) return false;
        return a.Kind switch
        {
            PlayerInputKind.Press => a.Button == b.Button,
            PlayerInputKind.Wheel => a.Modifiers == b.Modifiers,
            _ => a.Key == b.Key && a.Modifiers == b.Modifiers
        };
    }

    internal static string Canon(PlayerInput input) => input.Kind switch
    {
        PlayerInputKind.Press => "Press/" + input.Button,
        PlayerInputKind.Wheel => "Wheel/" + ((int)input.Modifiers).ToString(CultureInfo.InvariantCulture),
        _ => "Key/" + input.Key + "/" + ((int)input.Modifiers).ToString(CultureInfo.InvariantCulture) + (input.Symbol is null ? "" : "/" + input.Symbol)
    };

    internal static PlayerInput? Parse(string text)
    {
        var parts = text.Split('/', 4);
        switch (parts[0])
        {
            case "Press" when parts.Length == 2 && Enum.TryParse<PlayerButton>(parts[1], false, out var button) && Enum.IsDefined(button):
                return PlayerInput.OnPress(button);
            case "Wheel" when parts.Length == 2 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var wheel):
                return PlayerInput.OnWheel((KeyModifiers)wheel);
            case "Key" when parts.Length >= 3
                            && Enum.TryParse<Key>(parts[1], false, out var key) && Enum.IsDefined(key) && key != Key.None
                            && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var modifiers):
                var symbol = parts.Length == 4 && parts[3].Length > 0 ? parts[3] : null;
                return symbol is null ? PlayerInput.OnKey(key, (KeyModifiers)modifiers) : PlayerInput.OnSymbol(symbol, key, (KeyModifiers)modifiers);
            default:
                return null;
        }
    }

    private static Dictionary<string, string> Overrides(ShortcutMap map)
        => map == ShortcutMap.Player ? PlayerOverrides : EditorOverrides;

    private static void Read()
    {
        PlayerOverrides.Clear();
        EditorOverrides.Clear();
        _loaded = true;
        _version++;
        var file = Location;
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root) return;
            Fill(root["player"], PlayerOverrides);
            Fill(root["editor"], EditorOverrides);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            PlayerOverrides.Clear();
            EditorOverrides.Clear();
        }
    }

    private static void Fill(JsonNode? node, Dictionary<string, string> target)
    {
        if (node is not JsonObject map) return;
        foreach (var (id, value) in map)
        {
            if (value is not JsonValue text || !text.TryGetValue<string>(out var canon)) continue;
            if (canon.Length == 0 || Parse(canon) is not null) target[id] = canon;
        }
    }

    private static void Commit()
    {
        _loaded = true;
        _version++;
        var file = Location;
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
                Write(writer, "player", PlayerOverrides);
                Write(writer, "editor", EditorOverrides);
                writer.WriteEndObject();
            }

            File.Move(temp, file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void Write(Utf8JsonWriter writer, string name, Dictionary<string, string> map)
    {
        writer.WriteStartObject(name);
        foreach (var (id, value) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteString(id, value);
        writer.WriteEndObject();
    }
}
