namespace VidShrink.Core;

public static class DarkContentSwitch
{
    public const double MeanLumaThreshold = 44.0;
    public const string EngineCodec = "libsvtav1";
    public const string Codec = "libx265";

    public static bool IsDark(double? meanLuma)
        => meanLuma is double luma && double.IsFinite(luma) && luma > 0 && luma < MeanLumaThreshold;

    public static bool IsHdrSource(MediaInfo info)
        => info.IsHdr
           || string.Equals(info.ColorTransfer, "smpte2084", StringComparison.OrdinalIgnoreCase)
           || string.Equals(info.ColorTransfer, "arib-std-b67", StringComparison.OrdinalIgnoreCase);

    public static bool Applies(CodecPreference requested, string? lockedCodec, CompressionRegime regime, string engineCodec, double? meanLuma, bool hdrSource)
        => requested == CodecPreference.Auto
           && !hdrSource
           && lockedCodec is null
           && regime is CompressionRegime.Aggressive or CompressionRegime.Extreme
           && string.Equals(engineCodec, EngineCodec, StringComparison.OrdinalIgnoreCase)
           && IsDark(meanLuma);
}
