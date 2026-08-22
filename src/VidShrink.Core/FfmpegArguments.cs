using System.Globalization;
using System.Text;

namespace VidShrink.Core;

public static class FfmpegArguments
{
    private static readonly IReadOnlyDictionary<string, string[]> Presets = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["libx264"] = new[] { "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow" },
        ["libx265"] = new[] { "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow" },
        ["libvpx-vp9"] = new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8" },
        ["libsvtav1"] = new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13" },
        ["h264_nvenc"] = new[] { "p1", "p2", "p3", "p4", "p5", "p6", "p7" },
        ["hevc_nvenc"] = new[] { "p1", "p2", "p3", "p4", "p5", "p6", "p7" },
        ["h264_qsv"] = new[] { "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow" },
        ["hevc_qsv"] = new[] { "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow" },
        ["av1_nvenc"] = new[] { "p1", "p2", "p3", "p4", "p5", "p6", "p7" },
        ["av1_qsv"] = new[] { "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow" },
        ["h264_amf"] = new[] { "speed", "balanced", "quality", "high_quality" },
        ["hevc_amf"] = new[] { "speed", "balanced", "quality", "high_quality" },
        ["av1_amf"] = new[] { "speed", "balanced", "quality", "high_quality" }
    };

    public static string DefaultPreset(string codec) => codec.ToLowerInvariant() switch
    {
        "libsvtav1" => "8",
        "libvpx-vp9" => "4",
        "h264_nvenc" or "hevc_nvenc" => "p4",
        "av1_nvenc" => "p6",
        "h264_qsv" or "hevc_qsv" or "av1_qsv" => "medium",
        "h264_amf" or "hevc_amf" or "av1_amf" => "quality",
        _ => "slow"
    };

    public static bool SupportsRateLimits(string codec)
        => !string.Equals(codec, "libsvtav1", StringComparison.OrdinalIgnoreCase);

    public static bool NeedsTwoPasses(string codec) => !CodecModel.IsHardware(codec);

    public static bool IsValidPreset(string codec, string preset)
        => Presets.TryGetValue(codec, out var values) && values.Contains(preset, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Build(MediaInfo info, EncodePlan plan, string outputPath, int pass, string? passLogPrefix)
    {
        var a = new List<string> { "-hide_banner", "-y", "-hwaccel", "auto", "-i", info.FilePath };

        var filters = new List<string>();
        if (plan.Width != info.Width || plan.Height != info.Height)
            filters.Add($"scale={plan.Width}:{plan.Height}:flags=lanczos");
        if (!string.IsNullOrEmpty(plan.HdrVideoFilter))
            filters.Add(plan.HdrVideoFilter);
        if (filters.Count > 0)
            a.AddRange(new[] { "-vf", string.Join(',', filters) });

        if (plan.Fps < info.Fps - 0.01)
            a.AddRange(new[] { "-r", plan.Fps.ToString("0.###", CultureInfo.InvariantCulture) });

        a.AddRange(new[] { "-c:v", plan.Codec });
        a.AddRange(new[] { "-preset", plan.Preset });

        if (plan.ModeEnum == EncodeMode.Crf)
        {
            var qualityFlag = CodecModel.UsesCq(plan.Codec) ? "-cq" : "-crf";
            a.AddRange(new[] { qualityFlag, plan.Crf!.Value.ToString(CultureInfo.InvariantCulture) });
            if (SupportsRateLimits(plan.Codec))
                a.AddRange(new[] { "-maxrate", $"{plan.VideoBitrateK * 2}k", "-bufsize", $"{plan.VideoBitrateK * 4}k" });
        }
        else
        {
            a.AddRange(new[] { "-b:v", $"{plan.VideoBitrateK}k" });
            if (SupportsRateLimits(plan.Codec))
                a.AddRange(new[] { "-maxrate", $"{(int)(plan.VideoBitrateK * 1.5)}k", "-bufsize", $"{plan.VideoBitrateK * 2}k" });
            if (CodecModel.IsHardware(plan.Codec))
                a.AddRange(HardwareRateControl(plan.Codec));
            else if (pass > 0)
            {
                a.AddRange(new[] { "-pass", pass.ToString(CultureInfo.InvariantCulture) });
                if (passLogPrefix is not null) a.AddRange(new[] { "-passlogfile", passLogPrefix });
            }
        }

        a.AddRange(new[] { "-g", Math.Max(2, (int)Math.Round(plan.Fps * 2)).ToString(CultureInfo.InvariantCulture) });
        a.AddRange(new[] { "-pix_fmt", plan.PixelFormat });
        if (plan.HdrColorArgs.Count > 0)
            a.AddRange(plan.HdrColorArgs);

        if (pass == 1)
        {
            a.AddRange(plan.ExtraArgs);
            a.AddRange(new[] { "-an", "-f", "null" });
            a.Add(OperatingSystem.IsWindows() ? "NUL" : "/dev/null");
            return a;
        }

        if (plan.AudioCodec is null)
            a.Add("-an");
        else if (plan.AudioCodec == "copy")
            a.AddRange(new[] { "-c:a", "copy" });
        else
        {
            a.AddRange(new[] { "-c:a", plan.AudioCodec, "-b:a", $"{plan.AudioBitrateK}k" });
            if (plan.AudioChannels is > 0) a.AddRange(new[] { "-ac", plan.AudioChannels.Value.ToString() });
        }

        a.AddRange(new[] { "-movflags", "+faststart" });
        a.AddRange(plan.ExtraArgs);
        a.Add(outputPath);
        return a;
    }

    private static IReadOnlyList<string> HardwareRateControl(string codec)
    {
        var c = codec.ToLowerInvariant();
        if (c.Contains("nvenc")) return new[] { "-rc", "vbr", "-multipass", "fullres" };
        if (c.Contains("amf")) return new[] { "-rc", "vbr_peak" };
        if (c.Equals("h264_qsv")) return new[] { "-look_ahead", "1" };
        return Array.Empty<string>();
    }

    public static string ToCommandLine(IEnumerable<string> args)
    {
        var sb = new StringBuilder("ffmpeg");
        foreach (var arg in args)
        {
            sb.Append(' ');
            sb.Append(arg.Contains(' ') ? $"\"{arg}\"" : arg);
        }
        return sb.ToString();
    }
}
