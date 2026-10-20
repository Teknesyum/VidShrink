using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

internal sealed partial class RecorderSettings
{
    internal const int DefaultFramePadding = 64;

    internal const int DefaultFrameCorner = 24;

    internal int FrameBackdrop { get; set; } = 1;

    internal int FramePadding { get; set; } = DefaultFramePadding;

    internal int FrameCorner { get; set; } = DefaultFrameCorner;

    internal bool FrameShadow { get; set; } = true;

    internal string? FrameImage { get; set; }

    private void ReadFrame(JsonObject root)
    {
        if (root["frame"] is not JsonObject frame) return;
        try
        {
            if ((int?)frame["backdrop"] is { } backdrop && backdrop >= 0 && backdrop < RecorderView.FrameBackdropCount) FrameBackdrop = backdrop;
            if ((int?)frame["padding"] is { } padding && Array.IndexOf(RecordingFrame.PaddingChoices, padding) >= 0) FramePadding = padding;
            if ((int?)frame["corner"] is { } corner && Array.IndexOf(RecordingFrame.CornerChoices, corner) >= 0) FrameCorner = corner;
            FrameShadow = (bool?)frame["shadow"] ?? FrameShadow;
            FrameImage = (string?)frame["image"];
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
        }
    }

    private void WriteFrame(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("frame");
        writer.WriteNumber("backdrop", FrameBackdrop);
        writer.WriteNumber("padding", FramePadding);
        writer.WriteNumber("corner", FrameCorner);
        writer.WriteBoolean("shadow", FrameShadow);
        if (FrameImage is null) writer.WriteNull("image");
        else writer.WriteString("image", FrameImage);
        writer.WriteEndObject();
    }
}
