namespace VidShrink.Player;

/// <summary>
/// Goruntu ayarlari. Sayilar arayuz olcusudur (-100..100), motorun suzgec birimi degil;
/// cevirme <c>MpvEngine</c> icinde tek yerde durur. <c>Crop</c> her kenardan kirpilan yuzde.
/// </summary>
public sealed record PictureAdjust(
    int Brightness = 0,
    int Contrast = 0,
    int Saturation = 0,
    int Gamma = 0,
    int Hue = 0,
    int Sharpness = 0,
    bool Deinterlace = false,
    int Crop = 0)
{
    public const int MaximumCrop = 20;

    public static PictureAdjust Neutral { get; } = new();

    public bool IsNeutral => Equals(Neutral);

    public PictureAdjust Clamped() => new(
        Math.Clamp(Brightness, -100, 100),
        Math.Clamp(Contrast, -100, 100),
        Math.Clamp(Saturation, -100, 100),
        Math.Clamp(Gamma, -100, 100),
        Math.Clamp(Hue, -100, 100),
        Math.Clamp(Sharpness, -100, 100),
        Deinterlace,
        Math.Clamp(Crop, 0, MaximumCrop));
}

/// <summary>
/// Ses ayarlari: on band ekolayzer kazanci (dB), normallestirme ve %200 tavani.
/// <c>Bands</c> her zaman <see cref="BandHertz"/> uzunlugundadir; baska uzunluktaki bir
/// liste motor tarafindan reddedilir.
/// </summary>
public sealed record SoundAdjust(IReadOnlyList<int> Bands, bool Normalize = false, bool Boost = false)
{
    public const int MaximumGain = 12;

    public const double BoostCeiling = 200;

    public const double PlainCeiling = 100;

    public static IReadOnlyList<int> BandHertz { get; } = new[] { 60, 170, 310, 600, 1000, 3000, 6000, 12000, 14000, 16000 };

    public static SoundAdjust Neutral { get; } = new(new int[BandCount]);

    public static int BandCount => 10;

    public bool IsNeutral => !Normalize && !Boost && Bands.Count == BandCount && Bands.All(gain => gain == 0);

    public bool Valid => Bands.Count == BandCount;

    public SoundAdjust Clamped() => Valid
        ? new SoundAdjust(Bands.Select(gain => Math.Clamp(gain, -MaximumGain, MaximumGain)).ToArray(), Normalize, Boost)
        : this;

    public bool Same(SoundAdjust other)
        => Normalize == other.Normalize && Boost == other.Boost && Bands.SequenceEqual(other.Bands);
}

/// <summary>
/// Altyazi bicemi. <c>null</c> alan "motorun kendi varsayilani" demektir; sifirlama bu
/// kayitla yazilir ve motor her alani <c>option-info/&lt;ad&gt;/default-value</c>'dan geri koyar.
/// </summary>
public sealed record SubtitleStyle(
    string? Font = null,
    string? Color = null,
    double? Outline = null,
    double? Shadow = null,
    string? Background = null)
{
    public static SubtitleStyle Inherited { get; } = new();

    public bool IsInherited => Equals(Inherited);
}
