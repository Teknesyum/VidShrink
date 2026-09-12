using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VidShrink.App.Playback;

internal sealed class ToolsOptions
{
    internal const string FileName = "player-tools.json";
    internal const string ClipExtension = ".mp4";
    internal const string GifExtension = ".gif";

    internal const double DefaultClipSeconds = 10;
    internal const double MinimumClipSeconds = 1;
    internal const double MaximumClipSeconds = 600;
    internal const int DefaultGifFps = 10;
    internal const int MinimumGifFps = 1;
    internal const int MaximumGifFps = 30;
    internal const int DefaultGifWidth = 320;
    internal const int MinimumGifWidth = 80;
    internal const int MaximumGifWidth = 1920;
    internal const double DefaultMiniWidth = 480;
    internal const double DefaultMiniHeight = 270;
    internal const double MinimumMiniWidth = 240;
    internal const double MaximumMiniWidth = 1280;

    internal static readonly PlayerAction Clip = new(PlayerCommandKind.ClipExport, 0, "player.tools.clip", 0);
    internal static readonly PlayerAction Gif = new(PlayerCommandKind.GifExport, 0, "player.tools.gif", 0);
    internal static readonly PlayerAction MiniMode = new(PlayerCommandKind.MiniMode, 0, "player.tools.mini", 0);
    internal static readonly PlayerAction OpenUrl = new(PlayerCommandKind.OpenUrl, 0, "player.tools.url", 0);

    internal double ClipSeconds { get; private set; } = DefaultClipSeconds;

    internal int GifFps { get; private set; } = DefaultGifFps;

    internal int GifWidth { get; private set; } = DefaultGifWidth;

    internal double MiniWidth { get; private set; } = DefaultMiniWidth;

    internal double MiniHeight { get; private set; } = DefaultMiniHeight;

    internal string LastUrl { get; private set; } = "";

    internal void UseClipSeconds(double seconds)
        => ClipSeconds = double.IsFinite(seconds) ? Math.Clamp(Math.Round(seconds, 3), MinimumClipSeconds, MaximumClipSeconds) : DefaultClipSeconds;

    internal void UseGifFps(int fps) => GifFps = Math.Clamp(fps, MinimumGifFps, MaximumGifFps);

    internal void UseGifWidth(int width) => GifWidth = Math.Clamp(EvenWidth(width), MinimumGifWidth, MaximumGifWidth);

    internal void UseMiniSize(double width, double height)
    {
        MiniWidth = double.IsFinite(width) ? Math.Clamp(width, MinimumMiniWidth, MaximumMiniWidth) : DefaultMiniWidth;
        MiniHeight = double.IsFinite(height) && height > 0 ? height : DefaultMiniHeight;
    }

    internal void UseUrl(string? url) => LastUrl = string.IsNullOrWhiteSpace(url) ? "" : url.Trim();

    internal void Reset()
    {
        ClipSeconds = DefaultClipSeconds;
        GifFps = DefaultGifFps;
        GifWidth = DefaultGifWidth;
        MiniWidth = DefaultMiniWidth;
        MiniHeight = DefaultMiniHeight;
        LastUrl = "";
    }

    internal static ToolsOptions Load(string? file)
    {
        var options = new ToolsOptions();
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return options;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root) return options;
            if ((double?)root["clipSeconds"] is { } clip) options.UseClipSeconds(clip);
            if ((int?)root["gifFps"] is { } fps) options.UseGifFps(fps);
            if ((int?)root["gifWidth"] is { } width) options.UseGifWidth(width);
            if ((double?)root["miniWidth"] is { } miniWidth && (double?)root["miniHeight"] is { } miniHeight)
                options.UseMiniSize(miniWidth, miniHeight);
            options.UseUrl((string?)root["lastUrl"]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return new ToolsOptions();
        }

        return options;
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
                writer.WriteNumber("clipSeconds", ClipSeconds);
                writer.WriteNumber("gifFps", GifFps);
                writer.WriteNumber("gifWidth", GifWidth);
                writer.WriteNumber("miniWidth", MiniWidth);
                writer.WriteNumber("miniHeight", MiniHeight);
                writer.WriteString("lastUrl", LastUrl);
                writer.WriteEndObject();
            }
            File.Move(temp, file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal string Describe()
        => string.Join(" ", new[]
        {
            FormattableString.Invariant($"clip={ClipSeconds:0.###}"),
            FormattableString.Invariant($"gif={GifFps}/{GifWidth}"),
            FormattableString.Invariant($"mini={MiniWidth:0}x{MiniHeight:0}"),
            "url=" + (LastUrl.Length == 0 ? "-" : LastUrl)
        });

    private static int EvenWidth(int width) => width % 2 == 0 ? width : width + 1;

    internal static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

/// <summary>
/// Mini moda girip cikma. <see cref="FullscreenSwitch"/> ile ayni desen: giriste onceki
/// pencere durumu saklanir, cikista aynen geri konur. Cerceve ve "hep ustte" degeri de
/// pencere durumu kadar geri alinacak sayilir.
/// </summary>
internal sealed class MiniModeSwitch
{
    private WindowSnapshot? _previous;

    internal bool IsMini => _previous is not null;

    internal WindowSnapshot? Previous => _previous;

    internal int PreviousDecorations { get; private set; }

    internal bool PreviousTopmost { get; private set; }

    internal WindowSnapshot Enter(WindowSnapshot current, int decorations, bool topmost, double width, double height)
    {
        _previous = current;
        PreviousDecorations = decorations;
        PreviousTopmost = topmost;
        return current with { Width = width, Height = height };
    }

    internal WindowSnapshot? Leave()
    {
        if (_previous is not { } restore) return null;
        _previous = null;
        return restore;
    }
}
