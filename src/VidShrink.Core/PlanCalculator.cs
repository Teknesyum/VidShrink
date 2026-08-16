namespace VidShrink.Core;

public sealed class PlanOptions
{
    public double TargetMb { get; set; } = 25;
    public Intent Intent { get; set; } = Intent.Sharing;
    public CodecPreference Codec { get; set; } = CodecPreference.Compatible;
    public bool AllowResolutionDrop { get; set; } = true;
    public bool AllowFpsDrop { get; set; } = true;
}

public static class PlanCalculator
{
    private const double ContainerOverhead = 0.97;

    private static readonly int[] LadderHeights = { 2160, 1440, 1080, 720, 540, 480, 360 };

    public static EncodePlan Build(MediaInfo info, PlanOptions options)
    {
        var audioK = info.HasAudio ? PickAudioBitrateK(info, options) : 0;
        var totalK = options.TargetMb * 8192.0 / Math.Max(info.DurationSeconds, 0.1);
        var videoK = totalK * ContainerOverhead - audioK;

        if (videoK < 60)
            videoK = Math.Max(60, totalK * ContainerOverhead - (audioK = info.HasAudio ? 48 : 0));

        var width = info.Width;
        var height = info.Height;
        var fps = info.Fps;
        var reason = new List<string>();

        var steps = 0;
        while (steps < 3 && options.AllowResolutionDrop && BitsPerPixel(videoK, width, height, fps) < 0.030)
        {
            var next = NextLadderStep(height);
            if (next is null) break;
            (width, height) = ScaleTo(info.Width, info.Height, next.Value);
            steps++;
        }

        if (options.AllowFpsDrop && fps > 45 && BitsPerPixel(videoK, width, height, fps) < 0.050)
        {
            fps /= 2;
            reason.Add($"fps halved to {fps:0.##} to protect per-frame quality");
        }

        if (steps > 0) reason.Add($"downscaled to {width}x{height} ({steps} ladder step(s)) because the bitrate budget was too thin for the source resolution");

        var bpp = BitsPerPixel(videoK, width, height, fps);
        var codec = PickCodec(options.Codec);
        var mode = bpp >= 0.10 ? EncodeMode.Crf : EncodeMode.TwoPass;

        if (mode == EncodeMode.Crf)
            reason.Add($"budget is generous (bpp {bpp:0.###}), using quality-first CRF so the file may come out even smaller than the target");
        else
            reason.Add($"bpp {bpp:0.###}, using 2-pass VBR to land precisely on the target size");

        var plan = new EncodePlan
        {
            Codec = codec,
            Mode = mode == EncodeMode.Crf ? "crf" : "2pass",
            VideoBitrateK = (int)Math.Round(videoK),
            Crf = mode == EncodeMode.Crf ? DefaultCrf(codec, options.Intent) : null,
            AudioCodec = info.HasAudio ? PickAudioCodec(options.Codec) : null,
            AudioBitrateK = audioK,
            Width = width,
            Height = height,
            Fps = fps,
            Preset = PickPreset(options.Codec),
            Reason = string.Join("; ", reason)
        };

        return plan;
    }

    public static EncodePlan Correct(EncodePlan plan, double actualMb, double targetMb)
    {
        var corrected = plan.Clone();
        var factor = targetMb * 0.94 / Math.Max(actualMb, 0.01);
        corrected.Mode = "2pass";
        corrected.Crf = null;
        corrected.VideoBitrateK = Math.Max(60, (int)Math.Round(plan.VideoBitrateK * factor));
        corrected.Reason = $"retry: first pass produced {actualMb:0.0} MB against a {targetMb:0.0} MB target, video bitrate scaled by {factor:0.###}";
        return corrected;
    }

    public static double BitsPerPixel(double videoK, int width, int height, double fps)
        => videoK * 1000.0 / Math.Max(1.0, (double)width * height * fps);

    public static double EstimatedMb(EncodePlan plan, double durationSeconds)
        => (plan.VideoBitrateK + plan.AudioBitrateK) * durationSeconds / 8192.0 / ContainerOverhead;

    private static int PickAudioBitrateK(MediaInfo info, PlanOptions options)
    {
        var baseK = info.AudioChannels >= 2 ? 128 : 96;
        if (options.Intent == Intent.Archive) baseK = info.AudioChannels >= 2 ? 160 : 112;
        if (info.AudioBitrateBps > 0)
            baseK = Math.Min(baseK, (int)Math.Round(info.AudioBitrateBps / 1000.0));
        var budgetK = options.TargetMb * 8192.0 / Math.Max(info.DurationSeconds, 0.1);
        if (baseK > budgetK * 0.25) baseK = Math.Max(48, (int)(budgetK * 0.25));
        return baseK;
    }

    private static string PickCodec(CodecPreference pref) => pref switch
    {
        CodecPreference.MaxCompression => "libx265",
        CodecPreference.Fast => "h264_nvenc",
        _ => "libx264"
    };

    private static string PickAudioCodec(CodecPreference pref) => "aac";

    private static string PickPreset(CodecPreference pref) => pref switch
    {
        CodecPreference.Fast => "p4",
        CodecPreference.MaxCompression => "slow",
        _ => "slow"
    };

    private static int DefaultCrf(string codec, Intent intent)
    {
        var baseCrf = codec.Contains("265") ? 24 : 20;
        return intent switch
        {
            Intent.Archive => baseCrf - 2,
            Intent.SocialMedia => baseCrf + 2,
            _ => baseCrf
        };
    }

    private static int? NextLadderStep(int height)
    {
        foreach (var h in LadderHeights)
            if (h < height) return h;
        return null;
    }

    private static (int, int) ScaleTo(int srcWidth, int srcHeight, int targetHeight)
    {
        var width = (int)Math.Round(srcWidth * (double)targetHeight / srcHeight);
        if (width % 2 != 0) width++;
        var height = targetHeight % 2 == 0 ? targetHeight : targetHeight + 1;
        return (width, height);
    }
}
