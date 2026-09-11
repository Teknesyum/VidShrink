using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VidShrink.App.Playback;

internal enum RepeatMode
{
    Off,
    All,
    One
}

internal sealed class PlayerSettings
{
    internal const string FileName = "player-settings.json";
    internal const string DefaultPattern = "{name}_{time}";
    internal const string ScreenshotExtension = ".png";

    internal string? ScreenshotFolder { get; set; }

    internal string ScreenshotPattern { get; set; } = DefaultPattern;

    internal RepeatMode Repeat { get; set; }

    internal bool Shuffle { get; set; }

    internal static PlayerSettings Load(string? file)
    {
        var settings = new PlayerSettings();
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return settings;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root) return settings;
            settings.ScreenshotFolder = (string?)root["screenshotFolder"];
            if ((string?)root["screenshotPattern"] is { Length: > 0 } pattern) settings.ScreenshotPattern = pattern;
            if (Enum.TryParse<RepeatMode>((string?)root["repeat"], true, out var repeat)) settings.Repeat = repeat;
            settings.Shuffle = (bool?)root["shuffle"] ?? false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return new PlayerSettings();
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
                if (ScreenshotFolder is null) writer.WriteNull("screenshotFolder");
                else writer.WriteString("screenshotFolder", ScreenshotFolder);
                writer.WriteString("screenshotPattern", ScreenshotPattern);
                writer.WriteString("repeat", Repeat.ToString());
                writer.WriteBoolean("shuffle", Shuffle);
                writer.WriteEndObject();
            }
            File.Move(temp, file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal string ResolveFolder(string media)
    {
        if (!string.IsNullOrWhiteSpace(ScreenshotFolder)) return ScreenshotFolder!;
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (!string.IsNullOrEmpty(pictures)) return Path.Combine(pictures, "VidShrink");
        return Path.GetDirectoryName(Path.GetFullPath(media)) ?? Environment.CurrentDirectory;
    }

    internal string ScreenshotPath(string media, double seconds)
    {
        var folder = ResolveFolder(media);
        var stem = FileStem(media, seconds);
        var candidate = Path.Combine(folder, stem + ScreenshotExtension);
        for (var index = 2; File.Exists(candidate); index++)
            candidate = Path.Combine(folder, stem + "_" + index.ToString(CultureInfo.InvariantCulture) + ScreenshotExtension);
        return candidate;
    }

    internal string FileStem(string media, double seconds)
    {
        var at = TimeSpan.FromSeconds(double.IsFinite(seconds) && seconds > 0 ? seconds : 0);
        var time = ((int)at.TotalHours).ToString("00", CultureInfo.InvariantCulture)
            + "-" + at.Minutes.ToString("00", CultureInfo.InvariantCulture)
            + "-" + at.Seconds.ToString("00", CultureInfo.InvariantCulture)
            + "-" + at.Milliseconds.ToString("000", CultureInfo.InvariantCulture);
        var pattern = string.IsNullOrWhiteSpace(ScreenshotPattern) ? DefaultPattern : ScreenshotPattern;
        var stem = pattern
            .Replace("{name}", Path.GetFileNameWithoutExtension(media), StringComparison.Ordinal)
            .Replace("{time}", time, StringComparison.Ordinal);
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(stem.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return clean.Length == 0 ? "screenshot_" + time : clean;
    }
}
