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

    // Peak rate headroom. WidePeakFactor is the historical 1.5x and stays in force for the
    // processor encoders, which hit a two-pass target regardless of the peak. Hardware VBR does
    // not, and the peak is what caps the overspend. Measured on this machine with av1_nvenc
    // (-rc vbr -multipass fullres, preset p5) over a 400 s 1080p60 source, delivered over
    // requested bitrate:
    //   requested 2088k: peak 1.50 -> 1.098   peak 1.20 -> 1.093   peak 1.10 -> 1.084   peak 1.05 -> 1.034
    //   requested 1044k: peak 1.50 -> 1.218   peak 1.20 -> 1.186   peak 1.10 -> 1.085   peak 1.05 -> 1.042
    // The overspend grows as the average falls and follows the peak once the peak binds, so the
    // peak has to be tight where the encoder overshoots and may stay wide where it does not.
    // A line through the two 1.50 points reaches 1.000 at 2941 kbit/s, so PeakScaleBitrateK is
    // that crossing: at or below it the peak sits at TightPeakFactor, above it it opens back up
    // to WidePeakFactor over the same span.
    // TightPeakFactor is the tightest peak that still lands under the request, measured at the
    // shape the plan actually picks for this source (1266x712@60, av1_nvenc p5):
    //   requested  902k: peak 1.00 -> 0.978   peak 1.02 -> 0.995   peak 1.03 -> 1.013   peak 1.05 -> 1.028
    //   requested 1930k: peak 1.00 -> 0.986   peak 1.02 -> 1.009
    // 1.02 is the last peak on both rows that leaves the delivered size at or under the aim, and
    // the aim is the band centre, so 1.009 of it is still inside the band.
    public const double WidePeakFactor = 1.5;
    public const double TightPeakFactor = 1.02;
    public const int PeakScaleBitrateK = 2941;

    public static double PeakRateFactor(string codec, int videoBitrateK)
    {
        if (!CodecModel.IsHardware(codec)) return WidePeakFactor;
        var above = (double)videoBitrateK / PeakScaleBitrateK - 1.0;
        return Math.Clamp(TightPeakFactor + (WidePeakFactor - TightPeakFactor) * above, TightPeakFactor, WidePeakFactor);
    }

    public static double BufferFactor(double peakFactor) => 1.0 + 2.0 * (peakFactor - 1.0);

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
            a.AddRange(CodecModel.QualityArgs(plan.Codec, plan.Crf!.Value));
            if (SupportsRateLimits(plan.Codec) && !CodecModel.IsHardware(plan.Codec))
                a.AddRange(new[] { "-maxrate", $"{plan.VideoBitrateK * 2}k", "-bufsize", $"{plan.VideoBitrateK * 4}k" });
        }
        else
        {
            a.AddRange(new[] { "-b:v", $"{plan.VideoBitrateK}k" });
            if (SupportsRateLimits(plan.Codec))
            {
                var peak = PeakRateFactor(plan.Codec, plan.VideoBitrateK);
                a.AddRange(new[] { "-maxrate", $"{(int)(plan.VideoBitrateK * peak)}k", "-bufsize", $"{(int)(plan.VideoBitrateK * BufferFactor(peak))}k" });
            }
            if (CodecModel.IsHardware(plan.Codec))
                a.AddRange(CodecModel.BitrateRateControlArgs(plan.Codec));
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
