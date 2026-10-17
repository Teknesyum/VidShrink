using System.Globalization;
using System.Text.Json;

namespace VidShrink.Core.Editing;

/// <summary>
/// Ileri rengin libavfilter karsiligi ve proje dosyasindaki alanlari. <see cref="ClipFilters.Color"/> otomatik rengi
/// zincirin basina, egri ile tekerleri sonuna koyar; onizleme ve disa aktarim ayni dizgeyi kullanir.
/// </summary>
public static class ClipGrade
{
    public const string AutoColorFilter = "normalize=smoothing=24:independence=1:strength=1";

    /// <summary>Otomatik renk acik degilse bos.</summary>
    public static string Auto(ClipEffects effects) => effects.AutoColor ? AutoColorFilter : string.Empty;

    /// <summary>Egri ve teker zinciri, basinda virgul olmadan; ikisi de notrse bos. Virgul icermez, onizlemenin
    /// <c>enable</c> penceresi her suzgece ayri eklenebilir.</summary>
    public static string Filters(ClipEffects effects)
    {
        var parts = new List<string>();
        if (effects.HasCurves)
            parts.Add("curves=master='0/0 " + Point(ClipEffects.ShadowPoint, effects.CurveShadows) + " "
                      + Point(ClipEffects.MidPoint, effects.CurveMids) + " " + Point(ClipEffects.HighlightPoint, effects.CurveHighlights) + " 1/1'");
        if (effects.HasWheels)
        {
            var values = new List<string>();
            Zone(values, "s", effects.ShadowHue, effects.ShadowAmount);
            Zone(values, "m", effects.MidHue, effects.MidAmount);
            Zone(values, "h", effects.HighlightHue, effects.HighlightAmount);
            values.Add("pl=1");
            parts.Add("colorbalance=" + string.Join(":", values));
        }

        return string.Join(",", parts);
    }

    /// <summary>Teker acisi ve miktarindan kirmizi, yesil, mavi kaymasi; uc kanalin toplami sifir.</summary>
    public static (double Red, double Green, double Blue) Shift(int hue, double amount)
    {
        var angle = hue * Math.PI / 180;
        return (Round(amount * Math.Cos(angle)), Round(amount * Math.Cos(angle - 2 * Math.PI / 3)), Round(amount * Math.Cos(angle - 4 * Math.PI / 3)));
    }

    internal static void Write(Utf8JsonWriter writer, ClipEffects effects)
    {
        if (!effects.HasGrade) return;
        writer.WriteBoolean("autoColor", effects.AutoColor);
        writer.WriteNumber("curveShadows", effects.CurveShadows);
        writer.WriteNumber("curveMids", effects.CurveMids);
        writer.WriteNumber("curveHighlights", effects.CurveHighlights);
        writer.WriteNumber("shadowHue", effects.ShadowHue);
        writer.WriteNumber("shadowAmount", effects.ShadowAmount);
        writer.WriteNumber("midHue", effects.MidHue);
        writer.WriteNumber("midAmount", effects.MidAmount);
        writer.WriteNumber("highlightHue", effects.HighlightHue);
        writer.WriteNumber("highlightAmount", effects.HighlightAmount);
    }

    internal static ClipEffects Read(JsonElement e, ClipEffects effects)
        => effects with
        {
            AutoColor = e.TryGetProperty("autoColor", out var auto) && auto.GetBoolean(),
            CurveShadows = e.TryGetProperty("curveShadows", out var cs) ? cs.GetDouble() : ClipEffects.ShadowPoint,
            CurveMids = e.TryGetProperty("curveMids", out var cm) ? cm.GetDouble() : ClipEffects.MidPoint,
            CurveHighlights = e.TryGetProperty("curveHighlights", out var ch) ? ch.GetDouble() : ClipEffects.HighlightPoint,
            ShadowHue = e.TryGetProperty("shadowHue", out var sh) ? sh.GetInt32() : 0,
            ShadowAmount = e.TryGetProperty("shadowAmount", out var sa) ? sa.GetDouble() : 0,
            MidHue = e.TryGetProperty("midHue", out var mh) ? mh.GetInt32() : 0,
            MidAmount = e.TryGetProperty("midAmount", out var ma) ? ma.GetDouble() : 0,
            HighlightHue = e.TryGetProperty("highlightHue", out var hh) ? hh.GetInt32() : 0,
            HighlightAmount = e.TryGetProperty("highlightAmount", out var ha) ? ha.GetDouble() : 0,
        };

    private static void Zone(List<string> values, string zone, int hue, double amount)
    {
        if (amount == 0) return;
        var (red, green, blue) = Shift(hue, amount);
        values.Add("r" + zone + "=" + Number(red));
        values.Add("g" + zone + "=" + Number(green));
        values.Add("b" + zone + "=" + Number(blue));
    }

    private static string Point(double input, double output) => Number(input) + "/" + Number(output);

    private static double Round(double value)
    {
        var rounded = Math.Round(value, 3);
        return rounded == 0 ? 0 : rounded;
    }

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
