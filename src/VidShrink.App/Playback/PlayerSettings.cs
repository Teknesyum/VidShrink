using System;
using System.Collections.Generic;
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

internal enum SeekStep
{
    None,
    Fine,
    Small,
    Medium,
    Large
}

/// <summary>Ok tuşlarının ve şerit düğmelerinin atlama adımları; varsayılan 1, 10, 60 ve 300 sn.</summary>
internal sealed record SeekSteps(double Fine, double Small, double Medium, double Large)
{
    internal const double Minimum = 0.1;
    internal const double Maximum = 3600;

    internal static readonly SeekSteps Default = new(Keymap.SeekFine, Keymap.SeekSmall, Keymap.SeekMedium, Keymap.SeekLarge);

    internal static readonly IReadOnlyList<SeekStep> Slots = new[] { SeekStep.Fine, SeekStep.Small, SeekStep.Medium, SeekStep.Large };

    internal double Of(SeekStep step) => step switch
    {
        SeekStep.Fine => Fine,
        SeekStep.Small => Small,
        SeekStep.Medium => Medium,
        SeekStep.Large => Large,
        _ => 0
    };

    internal SeekSteps With(SeekStep step, double seconds)
    {
        if (!Valid(seconds)) return this;
        return step switch
        {
            SeekStep.Fine => this with { Fine = seconds },
            SeekStep.Small => this with { Small = seconds },
            SeekStep.Medium => this with { Medium = seconds },
            SeekStep.Large => this with { Large = seconds },
            _ => this
        };
    }

    internal static bool Valid(double seconds) => double.IsFinite(seconds) && seconds >= Minimum && seconds <= Maximum;

    internal static IReadOnlyList<double> Presets(SeekStep step) => step switch
    {
        SeekStep.Fine => new[] { 0.5, 1d, 2d, 3d, 5d },
        SeekStep.Small => new[] { 3d, 5d, 10d, 15d, 30d },
        SeekStep.Medium => new[] { 30d, 60d, 90d, 120d },
        SeekStep.Large => new[] { 120d, 300d, 600d, 900d },
        _ => Array.Empty<double>()
    };
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

    internal string? AudioDevice { get; set; }

    internal AudioOutput AudioOutput { get; set; } = AudioOutput.Plain;

    internal const double DefaultVolume = 100;

    internal double Volume { get; set; } = DefaultVolume;

    internal bool Muted { get; set; }

    internal bool KeepHistory { get; set; } = true;

    internal bool Resume { get; set; } = true;

    internal bool AudioInInterfaceLanguage { get; set; } = true;

    internal bool SubtitleInInterfaceLanguage { get; set; } = true;

    internal bool PauseOnMinimize { get; set; }

    internal bool TopmostWhilePlaying { get; set; }

    internal SeekSteps SeekSteps { get; set; } = SeekSteps.Default;

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
            settings.AudioDevice = (string?)root["audioDevice"] is { Length: > 0 } device && device != VidShrink.Player.AudioDevice.Auto ? device : null;
            settings.AudioOutput = new AudioOutput(
                (bool?)root["audioPassthrough"] ?? false,
                (bool?)root["audioExclusive"] ?? false,
                (bool?)root["audioStereo"] ?? false);
            settings.Volume = (double?)root["volume"] is { } volume && double.IsFinite(volume) && volume >= 0 && volume <= SoundAdjust.BoostCeiling ? volume : DefaultVolume;
            settings.Muted = (bool?)root["muted"] ?? false;
            settings.KeepHistory = (bool?)root["keepHistory"] ?? true;
            settings.Resume = (bool?)root["resume"] ?? true;
            settings.AudioInInterfaceLanguage = (bool?)root["audioInInterfaceLanguage"] ?? true;
            settings.SubtitleInInterfaceLanguage = (bool?)root["subtitleInInterfaceLanguage"] ?? true;
            settings.PauseOnMinimize = (bool?)root["pauseOnMinimize"] ?? false;
            settings.TopmostWhilePlaying = (bool?)root["topmostWhilePlaying"] ?? false;
            if (root["seekSteps"] is JsonObject steps) settings.SeekSteps = ReadSteps(steps);
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
                if (AudioDevice is null) writer.WriteNull("audioDevice");
                else writer.WriteString("audioDevice", AudioDevice);
                writer.WriteBoolean("audioPassthrough", AudioOutput.Passthrough);
                writer.WriteBoolean("audioExclusive", AudioOutput.Exclusive);
                writer.WriteBoolean("audioStereo", AudioOutput.Stereo);
                writer.WriteNumber("volume", Volume);
                writer.WriteBoolean("muted", Muted);
                writer.WriteBoolean("keepHistory", KeepHistory);
                writer.WriteBoolean("resume", Resume);
                writer.WriteBoolean("audioInInterfaceLanguage", AudioInInterfaceLanguage);
                writer.WriteBoolean("subtitleInInterfaceLanguage", SubtitleInInterfaceLanguage);
                writer.WriteBoolean("pauseOnMinimize", PauseOnMinimize);
                writer.WriteBoolean("topmostWhilePlaying", TopmostWhilePlaying);
                writer.WriteStartObject("seekSteps");
                writer.WriteNumber("fine", SeekSteps.Fine);
                writer.WriteNumber("small", SeekSteps.Small);
                writer.WriteNumber("medium", SeekSteps.Medium);
                writer.WriteNumber("large", SeekSteps.Large);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            File.Move(temp, file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static SeekSteps ReadSteps(JsonObject node)
    {
        var steps = SeekSteps.Default;
        foreach (var (slot, name) in new[] { (SeekStep.Fine, "fine"), (SeekStep.Small, "small"), (SeekStep.Medium, "medium"), (SeekStep.Large, "large") })
            if ((double?)node[name] is { } seconds) steps = steps.With(slot, seconds);
        return steps;
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
