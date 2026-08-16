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
        ["hevc_qsv"] = new[] { "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow" }
    };

    public static string DefaultPreset(string codec) => codec.ToLowerInvariant() switch
    {
        "libsvtav1" => "8",
        "libvpx-vp9" => "4",
        "h264_nvenc" or "hevc_nvenc" => "p4",
        "h264_qsv" or "hevc_qsv" => "medium",
        _ => "slow"
    };

    public static bool IsValidPreset(string codec, string preset)
        => Presets.TryGetValue(codec, out var values) && values.Contains(preset, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Build(MediaInfo info, EncodePlan plan, string outputPath, int pass, string? passLogPrefix)
    {
        var a = new List<string> { "-hide_banner", "-y", "-i", info.FilePath };

        if (plan.Width != info.Width || plan.Height != info.Height)
            a.AddRange(new[] { "-vf", $"scale={plan.Width}:{plan.Height}:flags=lanczos" });

        if (plan.Fps < info.Fps - 0.01)
            a.AddRange(new[] { "-r", plan.Fps.ToString("0.###", CultureInfo.InvariantCulture) });

        a.AddRange(new[] { "-c:v", plan.Codec });
        a.AddRange(new[] { "-preset", plan.Preset });

        if (plan.ModeEnum == EncodeMode.Crf)
        {
            var qualityFlag = plan.Codec.Contains("nvenc") || plan.Codec.Contains("qsv") ? "-cq" : "-crf";
            a.AddRange(new[] { qualityFlag, plan.Crf!.Value.ToString(CultureInfo.InvariantCulture) });
            a.AddRange(new[] { "-maxrate", $"{plan.VideoBitrateK * 2}k", "-bufsize", $"{plan.VideoBitrateK * 4}k" });
        }
        else
        {
            a.AddRange(new[] { "-b:v", $"{plan.VideoBitrateK}k" });
            a.AddRange(new[] { "-maxrate", $"{(int)(plan.VideoBitrateK * 1.5)}k", "-bufsize", $"{plan.VideoBitrateK * 2}k" });
            if (pass > 0)
            {
                a.AddRange(new[] { "-pass", pass.ToString(CultureInfo.InvariantCulture) });
                if (passLogPrefix is not null) a.AddRange(new[] { "-passlogfile", passLogPrefix });
            }
        }

        a.AddRange(new[] { "-g", Math.Max(2, (int)Math.Round(plan.Fps * 2)).ToString(CultureInfo.InvariantCulture) });
        a.AddRange(new[] { "-pix_fmt", "yuv420p" });

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
            a.AddRange(new[] { "-c:a", plan.AudioCodec, "-b:a", $"{plan.AudioBitrateK}k" });

        a.AddRange(new[] { "-movflags", "+faststart" });
        a.AddRange(plan.ExtraArgs);
        a.Add(outputPath);
        return a;
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
