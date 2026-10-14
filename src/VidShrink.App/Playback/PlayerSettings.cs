using System;
using System.Globalization;
using VidShrink.Core;
using VidShrink.Player;
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

internal enum ScreenshotFormat
{
    Png,
    Jpg
}

internal sealed class PlayerSettings
{
    internal const string FileName = "player-settings.json";
    internal const string DefaultPattern = "{name}_{time}";
    internal const string PngExtension = ".png";
    internal const string JpgExtension = ".jpg";

    internal string? ScreenshotFolder { get; set; }

    internal string ScreenshotPattern { get; set; } = DefaultPattern;

    internal ScreenshotFormat ScreenshotFormat { get; set; }

    internal string ScreenshotExtension => ScreenshotFormat == ScreenshotFormat.Jpg ? JpgExtension : PngExtension;

    internal RepeatMode Repeat { get; set; }

    internal bool Shuffle { get; set; }

    internal const double DefaultSpeedA = 1;
    internal const double DefaultSpeedB = 1.8;

    internal bool ShowRemaining { get; set; }

    internal double SpeedA { get; set; } = DefaultSpeedA;

    internal double SpeedB { get; set; } = DefaultSpeedB;

    internal bool SpeedModeB { get; set; }

    internal HardwareDecoding Hardware { get; set; }

    internal const double DefaultVolume = 100;

    internal double Volume { get; set; } = DefaultVolume;

    internal bool Muted { get; set; }

    internal bool KeepHistory { get; set; } = true;

    internal bool Resume { get; set; } = true;

    internal bool PauseOnMinimize { get; set; }

    internal bool TopmostWhilePlaying { get; set; }

    internal static PlayerSettings Load(string? file)
    {
        var settings = new PlayerSettings();
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return settings;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root) return settings;
            settings.ScreenshotFolder = (string?)root["screenshotFolder"];
            if ((string?)root["screenshotPattern"] is { Length: > 0 } pattern) settings.ScreenshotPattern = pattern;
            if (Enum.TryParse<ScreenshotFormat>((string?)root["screenshotFormat"], true, out var format) && Enum.IsDefined(format)) settings.ScreenshotFormat = format;
            if (Enum.TryParse<RepeatMode>((string?)root["repeat"], true, out var repeat)) settings.Repeat = repeat;
            settings.Shuffle = (bool?)root["shuffle"] ?? false;
            settings.ShowRemaining = (bool?)root["showRemaining"] ?? false;
            settings.SpeedA = Speed((double?)root["speedA"], DefaultSpeedA);
            settings.SpeedB = Speed((double?)root["speedB"], DefaultSpeedB);
            settings.SpeedModeB = string.Equals((string?)root["speedMode"], "B", StringComparison.OrdinalIgnoreCase);
            if (Enum.TryParse<HardwareDecoding>((string?)root["hardwareDecoding"], true, out var hardware) && Enum.IsDefined(hardware))
                settings.Hardware = hardware;
            settings.Volume = (double?)root["volume"] is { } volume && double.IsFinite(volume) && volume >= 0 && volume <= SoundAdjust.BoostCeiling ? volume : DefaultVolume;
            settings.Muted = (bool?)root["muted"] ?? false;
            settings.KeepHistory = (bool?)root["keepHistory"] ?? true;
            settings.Resume = (bool?)root["resume"] ?? true;
            settings.PauseOnMinimize = (bool?)root["pauseOnMinimize"] ?? false;
            settings.TopmostWhilePlaying = (bool?)root["topmostWhilePlaying"] ?? false;
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
                writer.WriteString("screenshotFormat", ScreenshotFormat.ToString());
                writer.WriteString("repeat", Repeat.ToString());
                writer.WriteBoolean("shuffle", Shuffle);
                writer.WriteBoolean("showRemaining", ShowRemaining);
                writer.WriteNumber("speedA", SpeedA);
                writer.WriteNumber("speedB", SpeedB);
                writer.WriteString("speedMode", SpeedModeB ? "B" : "A");
                writer.WriteString("hardwareDecoding", Hardware.ToString());
                writer.WriteNumber("volume", Volume);
                writer.WriteBoolean("muted", Muted);
                writer.WriteBoolean("keepHistory", KeepHistory);
                writer.WriteBoolean("resume", Resume);
                writer.WriteBoolean("pauseOnMinimize", PauseOnMinimize);
                writer.WriteBoolean("topmostWhilePlaying", TopmostWhilePlaying);
                writer.WriteEndObject();
            }
            File.Move(temp, file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static double Speed(double? value, double fallback) =>
        value is { } speed && double.IsFinite(speed) && speed >= Keymap.MinimumSpeed && speed <= Keymap.MaximumSpeed ? speed : fallback;

    internal string ResolveFolder(string media)
    {
        if (!string.IsNullOrWhiteSpace(ScreenshotFolder)) return ScreenshotFolder!;
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (!string.IsNullOrEmpty(pictures)) return Path.Combine(pictures, "VidShrink");
        if (MediaAddress.IsAddress(media)) return Environment.CurrentDirectory;
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
        var time = Saat.DosyaAdi(at);
        var pattern = string.IsNullOrWhiteSpace(ScreenshotPattern) ? DefaultPattern : ScreenshotPattern;
        var stem = pattern
            .Replace("{name}", Path.GetFileNameWithoutExtension(MediaAddress.IsAddress(media) ? MediaAddress.Name(media) : media), StringComparison.Ordinal)
            .Replace("{time}", time, StringComparison.Ordinal);
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(stem.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return clean.Length == 0 ? "screenshot_" + time : clean;
    }
}
