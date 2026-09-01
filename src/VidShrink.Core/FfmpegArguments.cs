using System.Globalization;
using System.Text;

namespace VidShrink.Core;

/// <summary>
/// Bir kodlayıcı seçeneğinin desteklenip desteklenmediğini <b>ölçen</b> taraf. Ölçüm süreç
/// doğurabilir, bu yüzden argüman üretimi bu arayüzü çağırmaz; yalnız ısıtma yolu çağırır.
/// <see cref="IEncoderOptionAvailability.SupportsEncoderOption"/> ise ısıtılmış sonucu okur
/// ve süreç doğurmaz.
/// </summary>
public interface IEncoderOptionWarmup
{
    bool WarmEncoderOption(string codec, string option, string value);
}

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

    /// <summary>
    /// Argüman üretiminin tanıdığı bütün kodlayıcılar. Liste ön ayar tablosundan türer,
    /// ayrıca elle yazılmaz; yeni bir kodlayıcı eklendiğinde yoklamayı ısıtan yol da onu görür.
    /// </summary>
    public static IReadOnlyCollection<string> KnownCodecs => (IReadOnlyCollection<string>)Presets.Keys;

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
    // TightPeakFactor is the tightest peak that still lands under the request, measured at the
    // shape the plan actually picks for this source (1266x712@60, av1_nvenc p5):
    //   requested  902k: peak 1.00 -> 0.978   peak 1.02 -> 0.995   peak 1.03 -> 1.013   peak 1.05 -> 1.028
    //   requested 1930k: peak 1.00 -> 0.986   peak 1.02 -> 1.009
    //
    // What decides how tight the peak may be is not the absolute bitrate but how far the request
    // sits above the encoder floor of the layout (CodecModel.MinBitrateK). A tight peak stops the
    // encoder from spending past the request, but it also stops it from making up for the easy
    // stretches it cannot fill: the further above its floor the request is, the more of the clip
    // saturates and the further under the request the file lands. Measured on av1_nvenc p5 over a
    // 400 s 1080p60 source, delivered video over requested video:
    //   1266x712@60, floor 346k:  902k = 2,6x floor, peak 1.02 -> 0.995
    //                            1930k = 5,6x floor, peak 1.02 -> 1.009
    //    882x496@60, floor 168k:  890k = 5,3x floor, peak 1.02 -> 1.007
    //                            1750k = 10,4x floor, peak 1.02 -> 0.997
    //                            1850k = 11,0x floor, peak 1.02 -> 0.991
    //                            1918k = 11,4x floor, peak 1.02 -> 0.973, 1.10 -> 1.008, 1.50 -> 1.056
    //                            1956k = 11,6x floor, peak 1.02 -> 0.989
    //                            1994k = 11,9x floor, peak 1.02 -> 0.992
    //                            2100k = 12,5x floor, peak 1.02 -> 0.991
    // T87 repeated 1.50 against the curve on two 20 s moving clips (actual MiB / target MiB):
    //   1280x720@60: 2.92x floor 0.980 / 0.974, 7.80x 0.983 / 0.985,
    //                   11.90x 0.968 / 0.969       (1.50 / curve)
    //   1920x1080@30: 3.06x floor 0.938 / 0.976, 7.78x 0.986 / 0.989,
    //                   11.90x 0.993 / 0.994       (1.50 / curve)
    // Wide headroom was safe on those clips, explaining the real-HDR quality result, but the
    // earlier 882x496@60 11.4x measurement still overshoots at 1.056. The relation is content
    // and layout dependent; a safe global ceiling therefore cannot be raised from this sample.
    // Up to 5,6x the floor the tight peak lands on the request. At 11,4x it costs 2,7% and 1.10
    // puts the file back on the request, while 1.50 overshoots by 5,6%. So the peak holds at
    // TightPeakFactor up to PeakOpensAtFloorRatio and opens to HardwarePeakCeiling at
    // PeakWidestAtFloorRatio, the ratio where 1.10 was measured. The knee sits between the
    // highest ratio where the tight peak was still right (5,6x) and the lowest where it was not.
    // Above the widest ratio nothing was measured, so the peak stays at the widest value that was:
    // 1.50 is known to overshoot there and the ceiling is the guarantee this may not break.
    public const double WidePeakFactor = 1.5;
    public const double TightPeakFactor = 1.02;
    public const double HardwarePeakCeiling = 1.10;
    public const double PeakOpensAtFloorRatio = 6.0;
    public const double PeakWidestAtFloorRatio = 11.4;

    public static double PeakRateFactor(string codec, int videoBitrateK, int width, int height, double fps)
    {
        if (!CodecModel.IsHardware(codec)) return WidePeakFactor;
        var floorK = CodecModel.MinBitrateK(codec, width, height, fps);
        var opening = ((double)videoBitrateK / floorK - PeakOpensAtFloorRatio) / (PeakWidestAtFloorRatio - PeakOpensAtFloorRatio);
        return Math.Clamp(TightPeakFactor + (HardwarePeakCeiling - TightPeakFactor) * opening, TightPeakFactor, HardwarePeakCeiling);
    }

    public static double BufferFactor(double peakFactor) => 1.0 + 2.0 * (peakFactor - 1.0);

    public static bool SupportsRateLimits(string codec)
        => !string.Equals(codec, "libsvtav1", StringComparison.OrdinalIgnoreCase);

    public static bool NeedsTwoPasses(string codec) => !CodecModel.IsHardware(codec);

    public static bool IsValidPreset(string codec, string preset)
        => Presets.TryGetValue(codec, out var values) && values.Contains(preset, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Build(MediaInfo info, EncodePlan plan, string outputPath, int pass, string? passLogPrefix, IEncoderAvailability? availability = null)
    {
        var a = new List<string> { "-hide_banner", "-y", "-hwaccel", "auto", "-i", info.FilePath };

        var filters = new List<string>();
        if (plan.Width != info.Width || plan.Height != info.Height)
            filters.Add($"scale={plan.Width}:{plan.Height}:flags=lanczos");
        if (!string.IsNullOrEmpty(plan.HdrVideoFilter))
            filters.Add(plan.HdrVideoFilter);
        if (plan.Fps < info.Fps - 0.01)
            filters.Add($"fps={plan.Fps.ToString("0.###", CultureInfo.InvariantCulture)}");
        if (filters.Count > 0)
            a.AddRange(new[] { "-vf", string.Join(',', filters) });

        a.AddRange(new[] { "-c:v", plan.Codec });
        a.AddRange(new[] { "-preset", plan.Preset });
        var psychovisualArgs = CachedPsychovisualArgs(plan.Codec, availability);

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
                var peak = PeakRateFactor(plan.Codec, plan.VideoBitrateK, plan.Width, plan.Height, plan.Fps);
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
        a.AddRange(psychovisualArgs);
        a.AddRange(plan.HdrColorArgs);

        if (pass == 1)
        {
            a.AddRange(plan.ExtraArgs);
            a.AddRange(new[] { "-an", "-f", "null" });
            a.Add(OperatingSystem.IsWindows() ? "NUL" : "/dev/null");
            return MergeEncoderParams(a);
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
        return MergeEncoderParams(a);
    }

    /// <summary>
    /// ffmpeg bu bayrakların ikincisini görünce birincisini sessizce atar: son yazan kazanır.
    /// Psy/AQ, HDR renk ve kullanıcının <c>ExtraArgs</c>'ı ayrı ayrı üretildiği için hepsi
    /// buradan geçer ve bayrak başına tek dizgeye iner.
    /// </summary>
    private static readonly string[] JoinedParamFlags = { "-x264-params", "-x265-params", "-svtav1-params" };

    public static IReadOnlyList<string> MergeEncoderParams(IReadOnlyList<string> args)
    {
        var merged = new List<string>(args.Count);
        var valueAt = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Count; i++)
        {
            if (i + 1 >= args.Count || !JoinedParamFlags.Contains(args[i], StringComparer.OrdinalIgnoreCase))
            {
                merged.Add(args[i]);
                continue;
            }

            var flag = args[i];
            var value = args[++i];
            if (value.Length == 0) continue;

            if (valueAt.TryGetValue(flag, out var at))
            {
                merged[at] = $"{merged[at]}:{value}";
                continue;
            }

            merged.Add(flag);
            merged.Add(value);
            valueAt[flag] = merged.Count - 1;
        }

        return merged;
    }

    /// <summary>
    /// Seçenek desteğini <b>ölçerek</b> psy/AQ bayraklarını üretir: kabiliyet ölçebiliyorsa
    /// (<see cref="IEncoderOptionWarmup"/>) yoklama burada doğar ve sonuç ısınır. Süreç
    /// doğurabildiği için argüman üretimi bu yolu çağırmaz; ısıtma sorumluluğunu üstlenen
    /// çağıranlar çağırır. Saf okuma için <see cref="CachedPsychovisualArgs"/>.
    /// </summary>
    public static IReadOnlyList<string> PsychovisualArgs(string codec, IEncoderAvailability? availability)
        => Psychovisual(codec, availability, measure: true);

    /// <summary>
    /// Yalnız ısıtılmış sonucu okur, süreç doğurmaz. Argüman üretiminin kullandığı yol budur.
    /// Isıtılmamış bir seçenek desteklenmiyor sayılır, bu yüzden çağıran önce
    /// <see cref="WarmPsychovisual"/> koşturmalıdır; yoksa bayraklar sessizce düşer.
    /// </summary>
    public static IReadOnlyList<string> CachedPsychovisualArgs(string codec, IEncoderAvailability? availability)
        => Psychovisual(codec, availability, measure: false);

    /// <summary>
    /// Kodlayıcının psy/AQ seçeneklerini bir kez ölçer ve sonucu kabiliyetin önbelleğine
    /// yazar. Bundan sonra <see cref="CachedPsychovisualArgs"/> doğru cevabı süreç doğurmadan
    /// verir. Kodlama yolundaki çağıranların argümanı sessizce kaybetmemesi buna bağlı.
    /// </summary>
    public static void WarmPsychovisual(string codec, IEncoderAvailability? availability)
        => Psychovisual(codec, availability, measure: true);

    private static IReadOnlyList<string> Psychovisual(string codec, IEncoderAvailability? availability, bool measure)
    {
        var args = new List<string>();
        if (availability is not IEncoderOptionAvailability options) return args;

        bool Supported(string option, string value)
            => measure && availability is IEncoderOptionWarmup warmup
                ? warmup.WarmEncoderOption(codec, option, value)
                : options.SupportsEncoderOption(codec, option, value);

        if (codec.Equals("libx265", StringComparison.OrdinalIgnoreCase)
            && Supported("-x265-params", "psy-rd=2:psy-rdoq=1:aq-mode=2"))
            args.AddRange(new[] { "-x265-params", "psy-rd=2:psy-rdoq=1:aq-mode=2" });
        else if (codec.Equals("libsvtav1", StringComparison.OrdinalIgnoreCase)
                 && Supported("-svtav1-params", "tune=0:enable-variance-boost=1:variance-boost-strength=2"))
            args.AddRange(new[] { "-svtav1-params", "tune=0:enable-variance-boost=1:variance-boost-strength=2" });
        else if (codec.Contains("nvenc", StringComparison.OrdinalIgnoreCase))
        {
            if (Supported("-spatial-aq", "1"))
                args.AddRange(new[] { "-spatial-aq", "1" });
            if (Supported("-temporal-aq", "1"))
                args.AddRange(new[] { "-temporal-aq", "1" });
        }
        return args;
    }

    public static IReadOnlyList<string> PsychovisualAndColorArgs(string codec,
        IReadOnlyList<string> psychovisualArgs, IReadOnlyList<string> colorArgs)
        => MergeEncoderParams(psychovisualArgs.Concat(colorArgs).ToList());

    /// <summary>
    /// Kisa bir parcanin argumanlari. Ayni <paramref name="availability"/> verildiginde tam
    /// kodlamanin argumanlarindan yalnizca zaman penceresiyle ayrilir; olceklemeyi, fps'i, sesi,
    /// on ayari, psy/AQ ve piksel bicimini oldugu gibi tasir. <paramref name="availability"/>
    /// <c>null</c> ise psy/AQ bayraklari uretilmez ve parca tam kodlamadan ayrisir.
    /// <c>-ss</c> girdiden <b>once</b> gelir: sonra gelirse ffmpeg dosyayi bastan
    /// cozer ve 2 sn'lik bir parca saniyeler surer. Ikinci gecis uretilmez.
    /// </summary>
    public static IReadOnlyList<string> BuildSegment(MediaInfo info, EncodePlan plan, double startSeconds, double durationSeconds, string outputPath, IEncoderAvailability? availability = null)
    {
        var a = new List<string>(Build(info, plan, outputPath, 0, null, availability));
        var input = a.IndexOf("-i");
        if (input < 0) throw new InvalidOperationException("Arguman dizisinde girdi bayragi yok.");

        a.InsertRange(input, new[] { "-ss", startSeconds.ToString("0.###", CultureInfo.InvariantCulture) });
        a.InsertRange(input + 4, new[] { "-t", durationSeconds.ToString("0.###", CultureInfo.InvariantCulture) });
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
