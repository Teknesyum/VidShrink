using System.Globalization;

namespace VidShrink.Core;

public enum EncoderVendor { Software, Nvenc, Qsv, Amf }

public static class CodecModel
{
    public const double QualityAtReference = 93.0;
    public const double QualityPerHalving = 6.0;
    public const double DetailConcentrationExponent = 0.25;
    public const double FpsBitrateExponent = 0.75;
    public const double ScalePenaltyScale = 10.0;
    public const double ScalePenaltyExponent = 1.1;
    public const double FpsPenaltyPerHalving = 5.0;
    public const double SoftwareQualityCeiling = 99.0;
    public const double HardwareQualityCeiling = 96.0;
    public const double HardwareAv1QualityCeiling = 98.0;
    public const double HardwareBitrateYield = 0.877;
    public const double HardwareFloorFactor = 1.25;

    public static double FloorBppf(string codec)
    {
        var baseFloor = Family(codec) switch
        {
            "av1" => 0.020,
            "hevc" => 0.025,
            _ => 0.035
        };
        return IsHardware(codec) ? baseFloor * HardwareFloorFactor : baseFloor;
    }

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
        if (!IsHardware(codec)) return SoftwareQualityCeiling;
        return codec.Equals("av1_nvenc", StringComparison.OrdinalIgnoreCase)
            ? HardwareAv1QualityCeiling
            : HardwareQualityCeiling;
    }

    public static EncoderVendor Vendor(string codec)
    {
        var c = codec.ToLowerInvariant();
        if (c.Contains("nvenc")) return EncoderVendor.Nvenc;
        if (c.Contains("qsv")) return EncoderVendor.Qsv;
        if (c.Contains("amf")) return EncoderVendor.Amf;
        return EncoderVendor.Software;
    }

    public static bool IsHardware(string codec) => Vendor(codec) != EncoderVendor.Software;

    public static bool CostsQualityInHardware(string codec)
        => IsHardware(codec)
           && !codec.Equals("av1_nvenc", StringComparison.OrdinalIgnoreCase)
           && !codec.Equals("av1_qsv", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> QualityArgs(string codec, double quality)
    {
        var exact = quality.ToString("0.#", CultureInfo.InvariantCulture);
        var whole = Math.Round(quality).ToString("0", CultureInfo.InvariantCulture);
        return Vendor(codec) switch
        {
            EncoderVendor.Nvenc => new[] { "-rc", "vbr", "-multipass", "fullres", "-cq", exact },
            EncoderVendor.Qsv => codec.Equals("h264_qsv", StringComparison.OrdinalIgnoreCase)
                ? new[] { "-global_quality", whole, "-look_ahead", "1" }
                : new[] { "-global_quality", whole },
            EncoderVendor.Amf => new[] { "-rc", "cqp", "-qp_i", whole, "-qp_p", whole, "-qp_b", whole },
            _ => new[] { "-crf", exact }
        };
    }

    public static IReadOnlyList<string> BitrateRateControlArgs(string codec) => Vendor(codec) switch
    {
        EncoderVendor.Nvenc => new[] { "-rc", "vbr", "-multipass", "fullres" },
        EncoderVendor.Amf => new[] { "-rc", "vbr_peak" },
        EncoderVendor.Qsv => codec.Equals("h264_qsv", StringComparison.OrdinalIgnoreCase)
            ? new[] { "-look_ahead", "1" }
            : Array.Empty<string>(),
        _ => Array.Empty<string>()
    };

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
