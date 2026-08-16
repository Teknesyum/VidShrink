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
            AudioCodec = audio is null ? null : GetString(audio.Value, "codec_name"),
            AudioBitrateBps = audio is null ? 0 : ParseLong(audio.Value, "bit_rate") ?? 128_000,
            AudioChannels = audio is null ? 0 : GetInt(audio.Value, "channels") ?? 2
        };
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
