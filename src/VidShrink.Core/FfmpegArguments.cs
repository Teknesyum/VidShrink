using System.Globalization;
using System.Text;

namespace VidShrink.Core;

public static class FfmpegArguments
{
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
            a.AddRange(new[] { "-an", "-f", "null" });
            a.Add(OperatingSystem.IsWindows() ? "NUL" : "/dev/null");
            a.AddRange(plan.ExtraArgs);
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
