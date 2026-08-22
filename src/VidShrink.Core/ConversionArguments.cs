using System.Globalization;

namespace VidShrink.Core;

public static class ConversionArguments
{
    public static IReadOnlyList<string> Validate(MediaInfo info, ConversionPlan plan)
    {
        var errors = new List<string>();
        if (plan.Start is { } startValue && startValue < TimeSpan.Zero) errors.Add("Start time cannot be negative.");
        if (plan.End is { } endValue && endValue <= TimeSpan.Zero) errors.Add("End time must be greater than zero.");
        if (plan.End is { } end && plan.Start is { } start && end <= start) errors.Add("End time must be after start time.");
        if (plan.Start is { } sourceEnd && sourceEnd.TotalSeconds >= info.DurationSeconds) errors.Add("Start time must be before the end of the source.");
        if (plan.Width is <= 0 || plan.Height is <= 0) errors.Add("Resolution dimensions must be positive.");
        if (plan.Width is { } width && width % 2 != 0 || plan.Height is { } height && height % 2 != 0) errors.Add("Resolution dimensions must be even for the selected pixel format.");
        if (plan.Fps is <= 0) errors.Add("Frame rate must be greater than zero.");
        if (plan.VideoCodec == "copy" && (plan.Height is not null || plan.Width is not null || plan.Fps is not null)) errors.Add("Stream copy cannot change resolution or frame rate.");
        if (plan.VideoCodec == "copy" && plan.Container == "gif") errors.Add("GIF requires video encoding and cannot use stream copy.");
        if (plan.AudioCodec == "copy" && !info.HasAudio) errors.Add("The source has no audio stream to copy.");
        if (plan.AudioOnly && !info.HasAudio) errors.Add("The source has no audio stream to extract.");

        if (!plan.AudioOnly && !plan.Gif && plan.VideoCodec != "copy" && !VideoEncodeCompatible(plan.Container, plan.VideoCodec))
            errors.Add($"The {plan.Container.ToUpperInvariant()} container does not support the selected {plan.VideoCodec} video encoder.");
        if (!plan.Gif && plan.AudioCodec is { } audioCodec && audioCodec != "copy" && !AudioEncodeCompatible(plan.Container, audioCodec))
            errors.Add($"The {plan.Container.ToUpperInvariant()} container does not support the selected {audioCodec} audio encoder.");

        var source = info.VideoCodec.ToLowerInvariant();
        if (plan.VideoCodec == "copy" && !VideoCopyCompatible(plan.Container, source))
            errors.Add($"The {plan.Container.ToUpperInvariant()} container does not support copying the source {source} video stream.");
        if (plan.AudioCodec == "copy" && !AudioCopyCompatible(plan.Container, info.AudioCodec))
            errors.Add($"The {plan.Container.ToUpperInvariant()} container does not support copying the source {info.AudioCodec} audio stream.");
        return errors;
    }

    public static IReadOnlyList<string> Build(MediaInfo info, ConversionPlan plan, string outputPath, string? palettePath = null, IEncoderAvailability? availability = null)
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
                var graph = filters.Count == 0
                    ? "[0:v][1:v]paletteuse=dither=sierra2_4a"
                    : $"{string.Join(',', filters)}[x];[x][1:v]paletteuse=dither=sierra2_4a";
                a.AddRange(new[] { "-i", palettePath, "-lavfi", graph });
                a.Add(outputPath);
            }
            return a;
        }

        if (plan.VideoCodec == "copy")
        {
            if (filters.Count > 0) a.AddRange(new[] { "-vf", string.Join(',', filters) });
            a.AddRange(new[] { "-c:v", "copy" });
        }
        else
        {
            var hdr = HdrResolver.Resolve(info, plan.HdrPolicy, plan.VideoCodec, availability);
            if (!string.IsNullOrEmpty(hdr.VideoFilter)) filters.Add(hdr.VideoFilter);
            if (filters.Count > 0) a.AddRange(new[] { "-vf", string.Join(',', filters) });

            a.AddRange(new[] { "-c:v", plan.VideoCodec, "-preset", FfmpegArguments.DefaultPreset(plan.VideoCodec) });
            a.AddRange(plan.QualityMode == ConversionQualityMode.Crf
                ? new[] { CodecModel.UsesCq(plan.VideoCodec) ? "-cq" : "-crf", plan.Crf.ToString(CultureInfo.InvariantCulture) }
                : new[] { "-b:v", $"{plan.VideoBitrateK}k" });
            a.AddRange(new[] { "-pix_fmt", hdr.PixelFormat });
            if (hdr.ColorArgs.Count > 0) a.AddRange(hdr.ColorArgs);
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
        "mp4" => codec is "h264" or "hevc" or "mpeg4" or "av1",
        "mov" => codec is "h264" or "hevc" or "mpeg4",
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

    private static bool VideoEncodeCompatible(string container, string codec) => container switch
    {
        "mp4" => codec is "libx264" or "libx265" or "libsvtav1" or "libvpx-vp9" or "h264_nvenc" or "hevc_nvenc" or "h264_qsv" or "hevc_qsv" or "av1_nvenc" or "av1_qsv" or "h264_amf" or "hevc_amf" or "av1_amf",
        "mov" => codec is "libx264" or "libx265" or "libvpx-vp9" or "h264_nvenc" or "hevc_nvenc" or "h264_qsv" or "hevc_qsv" or "h264_amf" or "hevc_amf",
        "webm" => codec is "libvpx-vp9" or "libsvtav1",
        "avi" => codec is "libx264",
        "mkv" => true,
        _ => false
    };

    private static bool AudioEncodeCompatible(string container, string codec) => container switch
    {
        "mp4" or "m4a" => codec is "aac" or "libmp3lame",
        "mov" => codec is "aac" or "libmp3lame" or "pcm_s16le",
        "webm" => codec is "libopus",
        "mp3" => codec is "libmp3lame",
        "wav" => codec is "pcm_s16le",
        "avi" => codec is "libmp3lame" or "pcm_s16le",
        "mkv" => codec is "aac" or "libopus" or "libmp3lame" or "pcm_s16le",
        _ => false
    };

    private static string FormatTime(TimeSpan value) => value.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
}
