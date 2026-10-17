using System.Globalization;

namespace VidShrink.Core.Editing;

/// <summary>
/// Hiz rampasi ve akici agir cekim. Rampa klibi kaynakta esit uzun dilimlere boler; dilim hizlari
/// <c>from</c>'dan <c>to</c>'ya dogrusal iner ya da cikar ve her dilim disa aktarimda kendi
/// <c>setpts</c>/<c>atempo</c> zincirini, onizlemede kendi oynatma hizini tasir. Akici agir cekim
/// yavas klipte kare tekrari yerine ara kareyi <c>minterpolate</c> ile uretir; pahalidir ve
/// onizleme onu tasimaz, yalniz disa aktarim uygular.
/// </summary>
public static class SpeedRamp
{
    public const int DefaultSlices = 5;

    public const decimal DefaultFactor = 4m;

    public const long MinSlice = EditTime.TicksPerSecond / 10;

    /// <summary><paramref name="count"/> dilimin hizlari, oynatma sirasiyla; uclar tam <paramref name="from"/> ve <paramref name="to"/>, aradakiler 0,01'e yuvarlanir.</summary>
    public static IReadOnlyList<decimal> Speeds(decimal from, decimal to, int count)
    {
        Check(from, nameof(from));
        Check(to, nameof(to));
        if (count < 2) throw new ArgumentOutOfRangeException(nameof(count), count, "Rampa en az iki dilim ister");

        var speeds = new decimal[count];
        for (var i = 0; i < count; i++)
        {
            var value = from + (to - from) * i / (count - 1);
            speeds[i] = Math.Clamp(decimal.Round(value, 2, MidpointRounding.AwayFromZero), EditClip.MinSpeed, EditClip.MaxSpeed);
        }

        return speeds;
    }

    /// <summary>
    /// Klibin rampali hali, oynatma sirasiyla. Dilim sayisi her dilim en az <see cref="MinSlice"/>
    /// kaynak tick'i kalacak kadar azalir; ikinin altina inerse klibin kendisi doner. Solma yalniz
    /// kenar dilimlerde, gecis ilk dilimde kalir; yon, kaynak, etiket, grup ve akicilik tasinir.
    /// </summary>
    public static IReadOnlyList<EditClip> Pieces(EditClip clip, decimal from, decimal to, int slices = DefaultSlices)
    {
        ArgumentNullException.ThrowIfNull(clip);
        var count = (int)Math.Min(slices, clip.SourceLength / MinSlice);
        if (count < 2) return new[] { clip };

        var speeds = Speeds(from, to, count);
        var ranges = new (long Start, long End)[count];
        for (var k = 0; k < count; k++)
            ranges[k] = (clip.SourceStart + clip.SourceLength * k / count, clip.SourceStart + clip.SourceLength * (k + 1) / count);
        if (clip.Reversed) Array.Reverse(ranges);

        var pieces = new EditClip[count];
        for (var i = 0; i < count; i++)
        {
            var effects = clip.Effects;
            if (i > 0) effects = effects with { FadeIn = 0 };
            if (i < count - 1) effects = effects with { FadeOut = 0 };
            pieces[i] = new EditClip(ranges[i].Start, ranges[i].End, speeds[i], clip.Reversed)
            {
                Effects = effects, Source = clip.Source, Label = clip.Label, Group = clip.Group,
                Transition = i == 0 ? clip.Transition : null, Smooth = clip.Smooth
            };
        }

        return pieces;
    }

    /// <summary>Ara kare uretilir mi: akicilik acik ve klip yavas.</summary>
    public static bool Interpolates(EditClip clip) => clip.Smooth && clip.Speed < 1m;

    /// <summary>
    /// Yavaslatilmis karelerden <paramref name="fps"/> hizinda hareket dengelemeli ara kare ureten suzgec.
    /// <c>minterpolate</c> son girdi karesinden sonrasini uretmez (olculen: 0,5x'te 60 yerine 57 kare, ardindan konan
    /// <c>tpad</c> bunu kurtarmiyor); girdiye iki klon kare eklenir ve cikti klibin cizelge uzunluguna kesilir, ses kaymaz.
    /// </summary>
    public static string SmoothFilter(double fps, EditClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return "tpad=stop_mode=clone:stop=2,minterpolate=fps=" + Number(fps > 0 ? fps : 30) + ":mi_mode=mci:mc_mode=aobmc:me_mode=bidir:vsbmc=1"
            + ",trim=duration=" + Number(EditTime.ToSeconds(clip.TimelineLength));
    }

    private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static void Check(decimal speed, string name)
    {
        if (speed < EditClip.MinSpeed || speed > EditClip.MaxSpeed)
            throw new ArgumentOutOfRangeException(name, speed, "Hiz 0,01 ile 100 arasinda olmalidir");
    }
}
