using System.Globalization;
using System.Text.RegularExpressions;

namespace VidShrink.Core;

/// <summary>
/// ARC-P1: kaynagin ilk ses izinin <c>ebur128</c> ozeti. <see cref="IntegratedLufs"/> tumlesik yukseklik,
/// <see cref="TruePeakDbfs"/> gercek tepe; sessiz kaynakta ffmpeg tepeyi <c>-inf</c> yazar, deger o zaman <c>null</c>.
/// </summary>
public sealed record LoudnessReport(double IntegratedLufs, double? TruePeakDbfs)
{
    public const string FilterName = "ebur128";

    private static readonly Regex Integrated = new(@"I:\s+(-?\d+(?:\.\d+)?)\s+LUFS", RegexOptions.CultureInvariant);
    private static readonly Regex Peak = new(@"Peak:\s+(-?(?:\d+(?:\.\d+)?|inf))\s+dBFS", RegexOptions.CultureInvariant);

    /// <summary>Tek ses izini iki is parcacigiyla cozer, cikti yazmaz; kare kaydi kapali, yalniz ozet kalir.</summary>
    public static IReadOnlyList<string> Arguments(string path) => new[]
    {
        "-hide_banner", "-nostdin", "-nostats", "-threads", "2",
        "-i", path, "-map", "0:a:0", "-vn", "-sn", "-dn",
        "-af", FilterName + "=peak=true:framelog=quiet", "-f", "null", "-"
    };

    /// <summary>Ozetten sonraki ilk <c>I:</c> ve <c>Peak:</c> satirlari; ozet yoksa <c>null</c>.</summary>
    public static LoudnessReport? Parse(string standardError)
    {
        if (string.IsNullOrEmpty(standardError)) return null;
        var start = standardError.LastIndexOf("Summary:", StringComparison.Ordinal);
        if (start < 0) return null;
        var summary = standardError[start..];
        var integrated = Integrated.Match(summary);
        if (!integrated.Success) return null;
        var lufs = double.Parse(integrated.Groups[1].Value, CultureInfo.InvariantCulture);
        var peak = Peak.Match(summary);
        double? dbfs = peak.Success && double.TryParse(peak.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value
            : null;
        return new LoudnessReport(lufs, dbfs);
    }
}
