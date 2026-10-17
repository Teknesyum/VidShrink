namespace VidShrink.Core.Editing;

/// <summary>
/// Ileri renk: tek tikla otomatik renk (<c>normalize</c>), ana egri ve uc bolgeli renk tekerleri (<c>colorbalance</c>).
/// Egri, giris 0,25 / 0,5 / 0,75 noktalarinin cikis degeridir; uclar 0 ve 1'de sabittir. Teker, bolgeye ton acisi
/// (derece, 0 kirmizi, 120 yesil, 240 mavi) ve 0..1 miktar olarak kayar.
/// </summary>
public sealed partial record ClipEffects
{
    public const double ShadowPoint = 0.25;
    public const double MidPoint = 0.5;
    public const double HighlightPoint = 0.75;

    public bool AutoColor { get; init; }

    public double CurveShadows { get; init; } = ShadowPoint;

    public double CurveMids { get; init; } = MidPoint;

    public double CurveHighlights { get; init; } = HighlightPoint;

    public int ShadowHue { get; init; }

    public double ShadowAmount { get; init; }

    public int MidHue { get; init; }

    public double MidAmount { get; init; }

    public int HighlightHue { get; init; }

    public double HighlightAmount { get; init; }

    public bool HasCurves => CurveShadows != ShadowPoint || CurveMids != MidPoint || CurveHighlights != HighlightPoint;

    public bool HasWheels => ShadowAmount != 0 || MidAmount != 0 || HighlightAmount != 0;

    public bool HasGrade => AutoColor || HasCurves || HasWheels;

    /// <summary>Yalniz ileri renk alanlarini <paramref name="grade"/>'den alir.</summary>
    public ClipEffects WithGrade(ClipEffects grade)
        => this with
        {
            AutoColor = grade.AutoColor,
            CurveShadows = grade.CurveShadows,
            CurveMids = grade.CurveMids,
            CurveHighlights = grade.CurveHighlights,
            ShadowHue = grade.ShadowHue,
            ShadowAmount = grade.ShadowAmount,
            MidHue = grade.MidHue,
            MidAmount = grade.MidAmount,
            HighlightHue = grade.HighlightHue,
            HighlightAmount = grade.HighlightAmount,
        };

    /// <summary>Egri noktalari 0..1'e, miktarlar 0..1'e kirpilip iki ondaliga yuvarlanir; aci 0..359'a sarilir; miktari sifir olan bolgenin
    /// acisi kalir, suzgece girmez. Sayi olmayan deger notre doner.</summary>
    private ClipEffects GradeNormalized()
    {
        var shadow = Amount(ShadowAmount);
        var mid = Amount(MidAmount);
        var highlight = Amount(HighlightAmount);
        return this with
        {
            CurveShadows = Color(CurveShadows, ShadowPoint, 0, 1),
            CurveMids = Color(CurveMids, MidPoint, 0, 1),
            CurveHighlights = Color(CurveHighlights, HighlightPoint, 0, 1),
            ShadowAmount = shadow,
            MidAmount = mid,
            HighlightAmount = highlight,
            ShadowHue = Hue(ShadowHue),
            MidHue = Hue(MidHue),
            HighlightHue = Hue(HighlightHue),
        };
    }

    private static double Amount(double value) => Color(value, 0, 0, 1);

    private static int Hue(int value) => (value % 360 + 360) % 360;
}
