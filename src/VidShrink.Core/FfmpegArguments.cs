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

/// <summary>
/// Anahtar kare aralığı: alt sınır, üst sınır ve aralığın hangi yoldan çıktığı.
/// <see cref="FromSceneMap"/> yalnız sahne haritasından türetilen aralıkta doğrudur.
/// </summary>
public readonly record struct KeyframeRange(int MinFrames, int MaxFrames, bool FromSceneMap);

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

    // Keyframe interval. This used to be a single number, -g = 2 s, written on every encode. A
    // fixed short interval forces I-frames where no scene starts, and the software encoders
    // already place one at every scene on their own, so the forced frames only cost bits.
    // What replaces it is a range: a floor the encoder may not cut below, a ceiling it may not
    // run past, and the placement in between left to the encoder's scene-cut decision. The
    // floor is one second, as in HandBrake (encx264.c:386-391, encx265.c:188-190: min-keyint
    // = fps, keyint = 10*fps).
    //
    // The ceiling is what the measurement decides, and it is not a single number either. The
    // ceiling only binds inside a scene longer than itself; below that the scene cut fires
    // first. So the ceiling is read off the content: the SceneMap's mean scene length divided
    // by SceneMapMergeFactor, clamped between KeyframeCeilingMinSeconds and
    // KeyframeCeilingMaxSeconds. Without a map the default is HandBrake's 10 s.
    //
    // SceneMapMergeFactor is not a tuning constant; it is the map's measured recall, written as
    // the two counts it was measured from. T101 checked the map against ground truth: in the
    // 144.2-333.3 s window the source carries 28 real cuts and the map reports 10, with zero
    // false positives; every one of the 18 missed cuts scored 0.112-0.199, below the threshold
    // the map ran at. Dividing the mapped mean by 28/10 turns "mean length of a map scene" back
    // into "mean length of a real shot", which is the quantity the ceiling wants.
    //
    // That recall belongs to one threshold, SceneMapThresholdOfRecord. If the threshold moves,
    // the map splits differently and the recall is no longer 28/10 - applying the old divider
    // on top of a corrected threshold would shorten the ceiling twice. The threshold is owned
    // elsewhere (SceneMap.DefaultThreshold), so the guard is a measure, not a comment:
    // Az_bolme_duzeltmesi_olculdugu_esikte_kalir turns red the moment the two diverge, and the
    // recall has to be measured again before the ceiling can be trusted.
    //
    // What the map is trusted for is the range, not the boundaries: the placement is left to
    // the encoder's scene cut. That the encoder actually places on content, and what the
    // placement is worth, are measured rather than assumed. On a cut-bearing 20 s window of the
    // source (7 detected cuts) libx264 at a 10 s ceiling put 6 I-frames where the ceiling
    // demanded 3, four of them landing exactly on a detected cut and one within 83 ms. Holding
    // the count fixed at 8 and changing only where they go - a plain grid against
    // -force_key_frames on the cuts, both with scenecut=0, 2-pass, same bitrate - gives
    //
    //   grid          -> 20.0119 MiB  mean 85.693  p10 76.014
    //   cut-aligned   -> 19.9705 MiB  mean 85.836  p10 76.192
    //
    // so alignment is worth +0.144 mean / +0.179 p10 at equal count and slightly less file.
    // Real, but second order: the interval itself is worth several times that (2 s -> 5 s alone
    // is +0.708 p10 in the sweep below). The claim sometimes made in the other direction - that
    // the cause is where the I-frames go rather than how many there are - is not what these
    // numbers say, and is not relied on here.
    //
    // The clamp comes from a ceiling sweep on parca-1-20sn (1920x1080@60 HDR PQ, libx264,
    // 2-pass, 20 MiB target, VMAF-NEG over the whole clip, same colour space and stream count
    // on both sides). Delivered size stays inside 0.3% across the whole sweep, so the sweep is
    // read as quality at equal size:
    //
    //   ceiling  2 s -> mean 88.637  p10 85.933  I-frames 13  realized interval 1.539 s
    //   ceiling  5 s -> mean 88.878  p10 86.641  I-frames  6  realized interval 3.334 s
    //   ceiling 10 s -> mean 88.951  p10 86.674  I-frames  3  realized interval 6.667 s
    //   ceiling 20 s -> mean 88.954  p10 86.751  I-frames  3  realized interval 6.667 s
    //
    // 5 s already carries 87% of the p10 gain between 2 s and 20 s, which is why the floor of
    // the clamp is 5 s and not shorter. Above 10 s the ceiling stops binding at all - 10 s and
    // 20 s produce the same three I-frames at the same places - so 10 s is the ceiling of the
    // clamp, and it agrees with HandBrake's keyint = 10*fps.
    //
    // Hardware is a different mechanism and gets its own ceiling. NVENC only inserts an I-frame
    // at a scene cut when lookahead is on (ffmpeg -h encoder=hevc_nvenc: "-no-scenecut ... When
    // lookahead is enabled"), and this project does not turn lookahead on, so on hardware the
    // ceiling is the whole placement rule: the realized interval is exactly the ceiling and the
    // seek cost is exactly what the ceiling says. HardwareKeyframeCeilingSeconds is therefore
    // the seek budget itself, not a content-derived number.
    // The software CRF path keeps a VBV cap where HandBrake leaves one out (encx265.c:514-522
    // fills VBV only on user request or for DoVi). Measured on parca-1-20sn at CRF 23, libx264,
    // same colour space and stream count on both sides, machine shared:
    //
    //   VBV on  -> 14.731 MiB   mean 86.977   p10 84.999
    //   VBV off -> 15.312 MiB   mean 87.287   p10 85.598
    //
    // Dropping it is a real quality gain and it lands exactly in the tail (p10 +0.599), but it
    // costs 3.9% more file at the same CRF. HandBrake can afford that because its CRF is an
    // open-ended quality mode; here CRF is a target-landing mode - PlanCalculator's fill policy
    // picks a CRF aimed at the band centre - so a systematic +3.9% eats the band. Measured and
    // needed, therefore kept. Loosening it to something between 2x and off was not measured.
    public const double CrfVbvPeakFactor = 2.0;
    public const double CrfVbvBufferFactor = 4.0;

    public const double KeyframeFloorSeconds = 1.0;
    public const double KeyframeCeilingDefaultSeconds = 10.0;
    public const double SceneMapThresholdOfRecord = 0.2;
    public const double SceneMapGroundTruthCuts = 28.0;
    public const double SceneMapReportedScenes = 10.0;
    public const double SceneMapMergeFactor = SceneMapGroundTruthCuts / SceneMapReportedScenes;
    public const double KeyframeCeilingMinSeconds = 5.0;
    public const double KeyframeCeilingMaxSeconds = 10.0;
    public const double HardwareKeyframeCeilingSeconds = 5.0;

    /// <summary>
    /// Sahne haritasından üst sınırı türetir. Harita yoksa ya da hiç sahne taşımıyorsa
    /// HandBrake'in 10 saniyesine düşer.
    /// </summary>
    public static double KeyframeCeilingSeconds(SceneMap? scenes)
    {
        if (scenes is null || scenes.Scenes.Count == 0 || scenes.Duration <= 0)
            return KeyframeCeilingDefaultSeconds;
        var mappedMeanSeconds = scenes.Duration / scenes.Scenes.Count;
        return Math.Clamp(
            mappedMeanSeconds / SceneMapMergeFactor,
            KeyframeCeilingMinSeconds,
            KeyframeCeilingMaxSeconds);
    }

    /// <summary>
    /// Kodlayıcının uyacağı anahtar kare aralığı. Yerleşim kararı aralığın içinde kodlayıcıya
    /// bırakılır; donanımda sahne kesimi olmadığı için üst sınır aralığın kendisidir.
    /// </summary>
    public static KeyframeRange KeyframeInterval(string codec, double fps, SceneMap? scenes = null)
    {
        var rate = double.IsFinite(fps) && fps > 0 ? fps : 30.0;
        var hardware = CodecModel.IsHardware(codec);
        var fromMap = !hardware && scenes is not null && scenes.Scenes.Count > 0 && scenes.Duration > 0;
        var ceilingSeconds = hardware ? HardwareKeyframeCeilingSeconds : KeyframeCeilingSeconds(scenes);
        var min = Math.Max(1, (int)Math.Round(rate * KeyframeFloorSeconds));
        var max = Math.Max(min, (int)Math.Round(rate * ceilingSeconds));
        return new KeyframeRange(min, max, fromMap);
    }

    /// <summary>
    /// Aralığı kodlayıcının kendi diliyle yazar. Sahne kesimi her yolda açık kalır: x265'te
    /// <c>scenecut=40</c>, SVT-AV1'de <c>scd=1</c>, x264/VP9'da varsayılan.
    /// </summary>
    public static IReadOnlyList<string> KeyframeArgs(string codec, double fps, SceneMap? scenes = null)
    {
        var range = KeyframeInterval(codec, fps, scenes);
        var max = range.MaxFrames.ToString(CultureInfo.InvariantCulture);
        var min = range.MinFrames.ToString(CultureInfo.InvariantCulture);

        if (codec.Equals("libx265", StringComparison.OrdinalIgnoreCase))
            return new[] { "-g", max, "-x265-params", $"keyint={max}:min-keyint={min}:scenecut=40" };
        if (codec.Equals("libsvtav1", StringComparison.OrdinalIgnoreCase))
            return new[] { "-g", max, "-svtav1-params", $"keyint={max}:scd=1" };
        if (CodecModel.IsHardware(codec))
            return new[] { "-g", max };
        return new[] { "-g", max, "-keyint_min", min };
    }

    public static bool SupportsRateLimits(string codec)
        => !string.Equals(codec, "libsvtav1", StringComparison.OrdinalIgnoreCase);

    public static bool NeedsTwoPasses(string codec) => !CodecModel.IsHardware(codec);

    public static bool IsValidPreset(string codec, string preset)
        => Presets.TryGetValue(codec, out var values) && values.Contains(preset, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Build(MediaInfo info, EncodePlan plan, string outputPath, int pass, string? passLogPrefix, IEncoderAvailability? availability = null, SceneMap? scenes = null)
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
                a.AddRange(new[] { "-maxrate", $"{(int)(plan.VideoBitrateK * CrfVbvPeakFactor)}k", "-bufsize", $"{(int)(plan.VideoBitrateK * CrfVbvBufferFactor)}k" });
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

        a.AddRange(KeyframeArgs(plan.Codec, plan.Fps, scenes));
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
    public static IReadOnlyList<string> BuildSegment(MediaInfo info, EncodePlan plan, double startSeconds, double durationSeconds, string outputPath, IEncoderAvailability? availability = null, SceneMap? scenes = null)
    {
        var a = new List<string>(Build(info, plan, outputPath, 0, null, availability, scenes));
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
