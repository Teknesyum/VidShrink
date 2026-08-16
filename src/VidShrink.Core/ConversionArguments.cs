using System.Globalization;

namespace VidShrink.Core;

public static class ConversionArguments
{
    public static IReadOnlyList<string> Validate(MediaInfo info, ConversionPlan plan)
    {
        var errors = new List<string>();
        if (plan.End is { } end && plan.Start is { } start && end <= start) errors.Add("End time must be after start time.");
        if (plan.VideoCodec == "copy" && (plan.Height is not null || plan.Width is not null || plan.Fps is not null)) errors.Add("Stream copy cannot change resolution or frame rate.");
        if (plan.VideoCodec == "copy" && plan.Container == "gif") errors.Add("GIF requires video encoding and cannot use stream copy.");
        if (plan.AudioCodec == "copy" && !info.HasAudio) errors.Add("The source has no audio stream to copy.");
        if (plan.AudioOnly && !info.HasAudio) errors.Add("The source has no audio stream to extract.");

        var source = info.VideoCodec.ToLowerInvariant();
        if (plan.VideoCodec == "copy" && !VideoCopyCompatible(plan.Container, source))
            errors.Add($"The {plan.Container.ToUpperInvariant()} container does not support copying the source {source} video stream.");
        if (plan.AudioCodec == "copy" && !AudioCopyCompatible(plan.Container, info.AudioCodec))
            errors.Add($"The {plan.Container.ToUpperInvariant()} container does not support copying the source {info.AudioCodec} audio stream.");
        return errors;
    }

    public static IReadOnlyList<string> Build(MediaInfo info, ConversionPlan plan, string outputPath, string? palettePath = null)
    {
        var errors = Validate(info, plan);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        var a = new List<string> { "-hide_banner", "-y" };
        if (plan.Start is { } start) a.AddRange(new[] { "-ss", FormatTime(start) });
        a.AddRange(new[] { "-i", info.FilePath });
        if (plan.End is { } end) a.AddRange(new[] { "-to", FormatTime(end) });

        if (plan.AudioOnly)
        {
            a.Add("-vn");
            AddAudio(a, plan);
            a.Add(outputPath);
            return a;
        }

        var filters = VideoFilters(info, plan);
        if (plan.Gif)
        {
            if (palettePath is null)
                a.AddRange(new[] { "-vf", string.Join(',', filters.Append("palettegen=stats_mode=diff")), palettePath = outputPath });
            else
            {
                a.AddRange(new[] { "-i", palettePath, "-lavfi", $"{string.Join(',', filters)}[x];[x][1:v]paletteuse=dither=sierra2_4a" });
                a.Add(outputPath);
            }
            return a;
        }

        if (filters.Count > 0) a.AddRange(new[] { "-vf", string.Join(',', filters) });
        if (plan.VideoCodec == "copy") a.AddRange(new[] { "-c:v", "copy" });
        else
        {
            a.AddRange(new[] { "-c:v", plan.VideoCodec, "-preset", FfmpegArguments.DefaultPreset(plan.VideoCodec) });
            a.AddRange(plan.QualityMode == ConversionQualityMode.Crf
                ? new[] { plan.VideoCodec.Contains("nvenc") || plan.VideoCodec.Contains("qsv") ? "-cq" : "-crf", plan.Crf.ToString(CultureInfo.InvariantCulture) }
                : new[] { "-b:v", $"{plan.VideoBitrateK}k" });
            a.AddRange(new[] { "-pix_fmt", "yuv420p" });
        }
        AddAudio(a, plan);
        if (plan.Container is "mp4" or "mov" or "m4a") a.AddRange(new[] { "-movflags", "+faststart" });
        a.Add(outputPath);
        return a;
    }

    private static List<string> VideoFilters(MediaInfo info, ConversionPlan plan)
    {
        var filters = new List<string>();
        if (plan.Width is { } width && plan.Height is { } height) filters.Add($"scale={width}:{height}:flags=lanczos");
        else if (plan.Height is { } h) filters.Add($"scale=-2:{h}:flags=lanczos");
        if (plan.Fps is { } fps && Math.Abs(fps - info.Fps) > 0.01) filters.Add($"fps={fps.ToString("0.###", CultureInfo.InvariantCulture)}");
        return filters;
    }

    private static void AddAudio(List<string> args, ConversionPlan plan)
    {
        if (plan.AudioCodec is null) args.Add("-an");
        else if (plan.AudioCodec == "copy") args.AddRange(new[] { "-c:a", "copy" });
        else args.AddRange(new[] { "-c:a", plan.AudioCodec, "-b:a", $"{plan.AudioBitrateK}k" });
    }

    private static bool VideoCopyCompatible(string container, string codec) => container switch
    {
        "mp4" or "mov" => codec is "h264" or "hevc" or "mpeg4" or "av1",
        "webm" => codec is "vp8" or "vp9" or "av1",
        "avi" => codec is "h264" or "mpeg4" or "mpeg2video" or "mjpeg",
        "mkv" => true,
        _ => false
    };

    private static bool AudioCopyCompatible(string container, string? codec) => container switch
    {
        "mp4" or "mov" or "m4a" => codec is "aac" or "alac" or "mp3",
        "webm" => codec is "opus" or "vorbis",
        "mp3" => codec == "mp3",
        "wav" => codec is "pcm_s16le" or "pcm_s24le" or "pcm_f32le",
        "avi" => codec is "mp3" or "aac" or "pcm_s16le",
        "mkv" => true,
        _ => false
    };

    private static string FormatTime(TimeSpan value) => value.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
}
