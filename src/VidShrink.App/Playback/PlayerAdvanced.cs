using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using VidShrink.Player;

namespace VidShrink.App.Playback;

/// <summary>
/// Gelismis ayarlarin kalici hali. 3. dalganin <c>PlayerSettings</c> duzeni: gecmis
/// dosyasinin klasorune yazilir, bozuk dosya sessizce varsayilana doner.
/// </summary>
internal sealed class PlayerAdvanced
{
    internal const string FileName = "player-advanced.json";

    internal PictureAdjust Picture { get; set; } = PictureAdjust.Neutral;

    internal SoundAdjust Sound { get; set; } = SoundAdjust.Neutral;

    internal SubtitleStyle Style { get; set; } = SubtitleStyle.Inherited;

    internal bool IsNeutral => Picture.IsNeutral && Sound.IsNeutral && Style.IsInherited;

    internal void Reset()
    {
        Picture = PictureAdjust.Neutral;
        Sound = SoundAdjust.Neutral;
        Style = SubtitleStyle.Inherited;
    }

    internal void ApplyTo(IPlaybackEngine engine)
    {
        engine.SetSound(Sound);
        engine.SetPicture(Picture);
        engine.SetSubtitleStyle(Style);
    }

    internal static PlayerAdvanced Load(string? file)
    {
        var settings = new PlayerAdvanced();
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return settings;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root) return settings;
            settings.Picture = new PictureAdjust(
                (int?)root["brightness"] ?? 0,
                (int?)root["contrast"] ?? 0,
                (int?)root["saturation"] ?? 0,
                (int?)root["gamma"] ?? 0,
                (int?)root["hue"] ?? 0,
                (int?)root["sharpness"] ?? 0,
                (bool?)root["deinterlace"] ?? false,
                (int?)root["crop"] ?? 0).Clamped();

            var bands = new int[SoundAdjust.BandCount];
            if (root["bands"] is JsonArray array)
                for (var index = 0; index < bands.Length && index < array.Count; index++)
                    bands[index] = (int?)array[index] ?? 0;

            settings.Sound = new SoundAdjust(bands, (bool?)root["normalize"] ?? false, (bool?)root["boost"] ?? false).Clamped();
            settings.Style = new SubtitleStyle(
                Text(root["subFont"]),
                Text(root["subColor"]),
                (double?)root["subOutline"],
                (double?)root["subShadow"],
                Text(root["subBack"]));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return new PlayerAdvanced();
        }

        return settings;
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
                writer.WriteNumber("brightness", Picture.Brightness);
                writer.WriteNumber("contrast", Picture.Contrast);
                writer.WriteNumber("saturation", Picture.Saturation);
                writer.WriteNumber("gamma", Picture.Gamma);
                writer.WriteNumber("hue", Picture.Hue);
                writer.WriteNumber("sharpness", Picture.Sharpness);
                writer.WriteBoolean("deinterlace", Picture.Deinterlace);
                writer.WriteNumber("crop", Picture.Crop);
                writer.WriteStartArray("bands");
                foreach (var gain in Sound.Bands) writer.WriteNumberValue(gain);
                writer.WriteEndArray();
                writer.WriteBoolean("normalize", Sound.Normalize);
                writer.WriteBoolean("boost", Sound.Boost);
                WriteText(writer, "subFont", Style.Font);
                WriteText(writer, "subColor", Style.Color);
                WriteNumber(writer, "subOutline", Style.Outline);
                WriteNumber(writer, "subShadow", Style.Shadow);
                WriteText(writer, "subBack", Style.Background);
                writer.WriteEndObject();
            }
            File.Move(temp, file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal IReadOnlyList<string> State()
    {
        var parts = new List<string>();
        if (!Picture.IsNeutral) parts.Add("picture");
        if (!Sound.IsNeutral) parts.Add("sound");
        if (!Style.IsInherited) parts.Add("subtitle");
        return parts;
    }

    internal static string Signed(int value)
        => (value > 0 ? "+" : "") + value.ToString(CultureInfo.CurrentCulture);

    private static string? Text(JsonNode? node)
    {
        var value = (string?)node;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static void WriteText(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }

    private static void WriteNumber(Utf8JsonWriter writer, string name, double? value)
    {
        if (value is { } number) writer.WriteNumber(name, number);
        else writer.WriteNull(name);
    }
}
