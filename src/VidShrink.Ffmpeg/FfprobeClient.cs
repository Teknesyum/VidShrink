using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public static class FfprobeClient
{
    public static async Task<MediaInfo> ProbeAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Input file not found.", filePath);

        var args = new[]
        {
            "-hide_banner", "-v", "error",
            "-print_format", "json",
            "-show_format", "-show_streams",
            filePath
        };

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, args) };
        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffprobe failed ({process.ExitCode}): {stderr.Trim()}");

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        var format = root.GetProperty("format");
        var streams = root.GetProperty("streams");

        JsonElement? video = null, audio = null;
        foreach (var s in streams.EnumerateArray())
        {
            var type = s.TryGetProperty("codec_type", out var t) ? t.GetString() : null;
            if (type == "video" && video is null && !IsAttachedPicture(s)) video = s;
            else if (type == "audio" && audio is null) audio = s;
        }

        if (video is null)
            throw new InvalidOperationException("The file contains no video stream.");

        var v = video.Value;
        var fileSize = new FileInfo(filePath).Length;
        var duration = ParseDouble(format, "duration") ?? ParseDouble(v, "duration") ?? 0;
        if (duration <= 0)
            throw new InvalidOperationException("Duration could not be determined; the file may be corrupt.");

        var pixFmt = GetString(v, "pix_fmt");
        var colorTransfer = GetString(v, "color_transfer");
        var fieldOrder = GetString(v, "field_order");

        return new MediaInfo
        {
            FilePath = filePath,
            FileSizeBytes = fileSize,
            DurationSeconds = duration,
            Width = DisplayDimensions(v).width,
            Height = DisplayDimensions(v).height,
            Fps = ParseFraction(GetString(v, "avg_frame_rate")) ?? ParseFraction(GetString(v, "r_frame_rate")) ?? 30,
            VideoCodec = GetString(v, "codec_name") ?? "unknown",
            TotalBitrateBps = ParseLong(format, "bit_rate") ?? (long)(fileSize * 8 / duration),
            PixelFormat = pixFmt,
            IsHdr = colorTransfer is "smpte2084" or "arib-std-b67" || (pixFmt?.Contains("10le") ?? false),
            ColorPrimaries = GetString(v, "color_primaries"),
            ColorTransfer = colorTransfer,
            ColorSpace = GetString(v, "color_space"),
            ColorRange = GetString(v, "color_range"),
            BitDepth = GetInt(v, "bits_per_raw_sample") ?? BitDepthFromPixFmt(pixFmt),
            MasteringDisplayMetadata = ParseMasteringDisplay(v),
            ContentLightLevel = ParseContentLightLevel(v),
            IsInterlaced = fieldOrder is not null and not "progressive" and not "unknown",
            AudioCodec = audio is null ? null : GetString(audio.Value, "codec_name"),
            AudioBitrateBps = audio is null ? 0 : ParseLong(audio.Value, "bit_rate") ?? 128_000,
            AudioChannels = audio is null ? 0 : GetInt(audio.Value, "channels") ?? 2
        };
    }

    private static int BitDepthFromPixFmt(string? pixFmt)
    {
        if (pixFmt is null) return 8;
        if (pixFmt.Contains("12le") || pixFmt.Contains("12be")) return 12;
        if (pixFmt.Contains("10le") || pixFmt.Contains("10be")) return 10;
        return 8;
    }

    private static string? ParseMasteringDisplay(JsonElement stream)
    {
        if (!stream.TryGetProperty("side_data_list", out var list)) return null;
        foreach (var item in list.EnumerateArray())
        {
            if (GetString(item, "side_data_type") != "Mastering display metadata") continue;
            var rx = ParseFraction(GetString(item, "red_x"));
            var ry = ParseFraction(GetString(item, "red_y"));
            var gx = ParseFraction(GetString(item, "green_x"));
            var gy = ParseFraction(GetString(item, "green_y"));
            var bx = ParseFraction(GetString(item, "blue_x"));
            var by = ParseFraction(GetString(item, "blue_y"));
            var wx = ParseFraction(GetString(item, "white_point_x"));
            var wy = ParseFraction(GetString(item, "white_point_y"));
            var maxLum = ParseFraction(GetString(item, "max_luminance"));
            var minLum = ParseFraction(GetString(item, "min_luminance"));
            if (rx is null || ry is null || gx is null || gy is null || bx is null || by is null || wx is null || wy is null || maxLum is null || minLum is null)
                return null;

            int Chroma(double value) => (int)Math.Round(value * 50000);
            int Luma(double value) => (int)Math.Round(value * 10000);
            return $"G({Chroma(gx.Value)},{Chroma(gy.Value)})B({Chroma(bx.Value)},{Chroma(by.Value)})R({Chroma(rx.Value)},{Chroma(ry.Value)})WP({Chroma(wx.Value)},{Chroma(wy.Value)})L({Luma(maxLum.Value)},{Luma(minLum.Value)})";
        }
        return null;
    }

    private static string? ParseContentLightLevel(JsonElement stream)
    {
        if (!stream.TryGetProperty("side_data_list", out var list)) return null;
        foreach (var item in list.EnumerateArray())
        {
            if (GetString(item, "side_data_type") != "Content light level metadata") continue;
            var max = GetInt(item, "max_content");
            var avg = GetInt(item, "average_content");
            if (max is null || avg is null) return null;
            return $"{max},{avg}";
        }
        return null;
    }

    private static (int width, int height) DisplayDimensions(JsonElement stream)
    {
        var width = GetInt(stream, "width") ?? 0;
        var height = GetInt(stream, "height") ?? 0;
        var rotation = 0;
        if (stream.TryGetProperty("tags", out var tags))
            rotation = GetInt(tags, "rotate") ?? 0;
        if (stream.TryGetProperty("side_data_list", out var sideData))
            foreach (var item in sideData.EnumerateArray())
                rotation = GetInt(item, "rotation") ?? rotation;
        return Math.Abs(rotation) % 180 == 90 ? (height, width) : (width, height);
    }

    private static bool IsAttachedPicture(JsonElement stream)
        => stream.TryGetProperty("disposition", out var d)
           && d.TryGetProperty("attached_pic", out var a)
           && a.GetInt32() == 1;

    private static string? GetString(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? GetInt(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetInt32();
        return int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
    }

    private static long? ParseLong(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetInt64();
        return long.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : null;
    }

    private static double? ParseDouble(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        return double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static double? ParseFraction(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split('/');
        if (parts.Length != 2) return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var single) ? single : null;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num)) return null;
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var den) || den == 0) return null;
        var fps = num / den;
        return fps > 0.1 && fps < 1000 ? fps : null;
    }
}
