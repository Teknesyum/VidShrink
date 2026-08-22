namespace VidShrink.Core;

public static class CodecModel
{
    public const double QualityAtReference = 93.0;
    public const double QualityPerHalving = 6.0;
    public const double DetailConcentrationExponent = 0.25;
    public const double FpsBitrateExponent = 0.75;
    public const double ScalePenaltyScale = 10.0;
    public const double ScalePenaltyExponent = 1.1;
    public const double FpsPenaltyPerHalving = 5.0;

    public static double ReferenceCrf(string codec) => Family(codec) switch
    {
        "hevc" => 28,
        "av1" => 35,
        _ => 23
    };

    public static double RelativeBitrateNeed(string codec) => codec.ToLowerInvariant() switch
    {
        "libx265" => 0.68,
        "libsvtav1" => 0.55,
        "h264_nvenc" => 1.28,
        "hevc_nvenc" => 0.88,
        "h264_qsv" => 1.25,
        "hevc_qsv" => 0.90,
        "av1_nvenc" => 0.60,
        "av1_qsv" => 0.62,
        "av1_amf" => 0.66,
        "hevc_amf" => 0.95,
        "h264_amf" => 1.30,
        _ => 1.0
    };

    public static double CrfHalvingStep(string codec) => Family(codec) switch
    {
        "av1" => 7.0,
        _ => 6.0
    };

    public static double QualityLimit(string codec)
    {
        if (!IsHardware(codec)) return 99.0;
        return codec.Equals("av1_nvenc", StringComparison.OrdinalIgnoreCase) ? 98.0 : 96.0;
    }

    public static bool IsHardware(string codec)
    {
        var c = codec.ToLowerInvariant();
        return c.Contains("nvenc") || c.Contains("qsv") || c.Contains("amf");
    }

    public static bool UsesCq(string codec) => IsHardware(codec);

    public static (int Min, int Max) CrfRange(string codec) => Family(codec) switch
    {
        "av1" => (18, 55),
        _ => (10, 45)
    };

    public static string SourceFamily(string sourceCodec) => Family(sourceCodec);

    public static double SourceBitrateNeed(string sourceCodec) => Family(sourceCodec) switch
    {
        "av1" => 0.55,
        "hevc" => 0.68,
        "vp9" => 0.72,
        "h264" => 1.0,
        "vp8" => 1.35,
        "mpeg4" => 1.8,
        "mpeg2video" => 2.4,
        "wmv3" or "vc1" => 1.5,
        _ => 1.15
    };

    private static string Family(string codec)
    {
        var c = codec.ToLowerInvariant();
        if (c.Contains("av1")) return "av1";
        if (c.Contains("265") || c.Contains("hevc")) return "hevc";
        if (c.Contains("vp9")) return "vp9";
        if (c.Contains("vp8")) return "vp8";
        if (c.Contains("264") || c.Contains("avc")) return "h264";
        return c;
    }
}
