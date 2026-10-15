using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using VidShrink.Core;

namespace VidShrink.App.Playback;

internal sealed class ToolsOptions
{
    internal const string FileName = "player-tools.json";
    internal const string ClipExtension = ".mp4";
    internal const string GifExtension = ".gif";
    internal const string FramesSuffix = "_frames";
    internal const string SheetSuffix = "_sheet";
    internal const string ImageExtension = ".png";

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
    internal const double DefaultFrameSeconds = 1;
    internal const double MinimumFrameSeconds = 0.1;
    internal const double MaximumFrameSeconds = 600;
    internal const int DefaultSheetColumns = 4;
    internal const int DefaultSheetRows = 4;
    internal const int MinimumSheetCells = 1;
    internal const int MaximumSheetCells = 10;
    internal const int DefaultSheetWidth = 320;
    internal const int MinimumSheetWidth = 80;
    internal const int MaximumSheetWidth = 1920;

    internal static readonly PlayerAction Clip = new(PlayerCommandKind.ClipExport, 0, "player.tools.clip");
    internal static readonly PlayerAction Gif = new(PlayerCommandKind.GifExport, 0, "player.tools.gif");
    internal static readonly PlayerAction Frames = new(PlayerCommandKind.FrameExport, 0, "player.tools.frames");
    internal static readonly PlayerAction Sheet = new(PlayerCommandKind.ContactSheet, 0, "player.tools.sheet");
    internal static readonly PlayerAction MiniMode = new(PlayerCommandKind.MiniMode, 0, "player.tools.mini");
    internal static readonly PlayerAction OpenUrl = new(PlayerCommandKind.OpenUrl, 0, "player.tools.url");
    internal static readonly PlayerAction PasteUrl = new(PlayerCommandKind.PasteUrl, 0, "player.tools.paste");

    internal double ClipSeconds { get; private set; } = DefaultClipSeconds;

    internal int GifFps { get; private set; } = DefaultGifFps;

    internal int GifWidth { get; private set; } = DefaultGifWidth;

    internal double MiniWidth { get; private set; } = DefaultMiniWidth;

    internal double MiniHeight { get; private set; } = DefaultMiniHeight;

    internal string LastUrl { get; private set; } = "";

    internal double FrameSeconds { get; private set; } = DefaultFrameSeconds;

    internal int SheetColumns { get; private set; } = DefaultSheetColumns;

    internal int SheetRows { get; private set; } = DefaultSheetRows;

    internal int SheetWidth { get; private set; } = DefaultSheetWidth;

    internal void UseClipSeconds(double seconds)
        => ClipSeconds = double.IsFinite(seconds) ? Math.Clamp(Math.Round(seconds, 3), MinimumClipSeconds, MaximumClipSeconds) : DefaultClipSeconds;

    internal void UseGifFps(int fps) => GifFps = Math.Clamp(fps, MinimumGifFps, MaximumGifFps);

    internal void UseGifWidth(int width) => GifWidth = Math.Clamp(EvenWidth(width), MinimumGifWidth, MaximumGifWidth);

    internal void UseMiniSize(double width, double height)
    {
        MiniWidth = double.IsFinite(width) ? Math.Clamp(width, MinimumMiniWidth, MaximumMiniWidth) : DefaultMiniWidth;
        MiniHeight = double.IsFinite(height) && height > 0 ? height : DefaultMiniHeight;
    }

    internal void UseFrameSeconds(double seconds)
        => FrameSeconds = double.IsFinite(seconds) ? Math.Clamp(Math.Round(seconds, 3), MinimumFrameSeconds, MaximumFrameSeconds) : DefaultFrameSeconds;

    internal void UseSheetGrid(int columns, int rows)
    {
        SheetColumns = Math.Clamp(columns, MinimumSheetCells, MaximumSheetCells);
        SheetRows = Math.Clamp(rows, MinimumSheetCells, MaximumSheetCells);
    }

    internal void UseSheetWidth(int width) => SheetWidth = Math.Clamp(EvenWidth(width), MinimumSheetWidth, MaximumSheetWidth);

    internal void UseUrl(string? url) => LastUrl = string.IsNullOrWhiteSpace(url) ? "" : MediaAddress.WithoutQuery(url.Trim());

    internal void Reset()
    {
        ClipSeconds = DefaultClipSeconds;
        GifFps = DefaultGifFps;
        GifWidth = DefaultGifWidth;
        MiniWidth = DefaultMiniWidth;
        MiniHeight = DefaultMiniHeight;
        LastUrl = "";
        FrameSeconds = DefaultFrameSeconds;
        SheetColumns = DefaultSheetColumns;
        SheetRows = DefaultSheetRows;
        SheetWidth = DefaultSheetWidth;
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
            if ((double?)root["frameSeconds"] is { } frame) options.UseFrameSeconds(frame);
            if ((int?)root["sheetColumns"] is { } columns && (int?)root["sheetRows"] is { } rows) options.UseSheetGrid(columns, rows);
            if ((int?)root["sheetWidth"] is { } sheetWidth) options.UseSheetWidth(sheetWidth);
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
                writer.WriteNumber("frameSeconds", FrameSeconds);
                writer.WriteNumber("sheetColumns", SheetColumns);
                writer.WriteNumber("sheetRows", SheetRows);
                writer.WriteNumber("sheetWidth", SheetWidth);
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
            FormattableString.Invariant($"frames={FrameSeconds:0.###}"),
            FormattableString.Invariant($"sheet={SheetColumns}x{SheetRows}/{SheetWidth}"),
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
