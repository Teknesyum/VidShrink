using System.Globalization;
using System.Text.RegularExpressions;

namespace VidShrink.Player;

public sealed partial class MpvEngine
{
    private const string ColorLabel = "vscolor";
    private const string HueLabel = "vshue";
    private const string SharpLabel = "vssharp";
    private const string CropLabel = "vscrop";
    private const string EqualizerLabel = "vseq";
    private const string NormalizeLabel = "vsnorm";

    private const double HueSpan = 1.8;
    private const double SharpSpan = 0.015;

    /// <summary>
    /// Renk, keskinlik ve kirpma suzgec zincirinde durur: <c>vo=libmpv</c> yazilim render
    /// yolunda <c>brightness</c>/<c>saturation</c> gibi gpu-only ozellikler kareye dokunmaz.
    /// Her ayar etiketli bir libavfilter halkasidir, geri okuma <c>vf</c> dizgesinden yapilir.
    /// </summary>
    public PictureAdjust Picture
    {
        get
        {
            var chain = GetProperty("vf") ?? "";
            var color = Segment(chain, ColorLabel);
            var hue = Segment(chain, HueLabel);
            var sharp = Segment(chain, SharpLabel);
            var crop = Segment(chain, CropLabel);
            return new PictureAdjust(
                Percent(Field(color, "brightness", 0)),
                Percent(Field(color, "contrast", 1) - 1),
                Percent(Field(color, "saturation", 1) - 1),
                Percent(Field(color, "gamma", 1) - 1),
                Scaled(Field(hue, "h", 0), HueSpan),
                Scaled(Field(sharp, "luma_amount", 0), SharpSpan),
                GetProperty("deinterlace") is "yes" or "auto",
                Percent(Factor(crop, "x")));
        }
    }

    /// <summary>
    /// Ekolayzer on <c>equalizer</c> halkasi, normallestirme <c>dynaudnorm</c>; ikisi de
    /// etiketli <c>af</c> halkasi. Kazanclar zincirdeki <c>g=</c> alanlarindan sirayla okunur.
    /// </summary>
    public SoundAdjust Sound
    {
        get
        {
            var chain = GetProperty("af") ?? "";
            return new SoundAdjust(
                Gains(Segment(chain, EqualizerLabel)),
                Segment(chain, NormalizeLabel) is not null,
                VolumeCeiling >= SoundAdjust.BoostCeiling);
        }
    }

    public SubtitleStyle SubtitleLook => new(
        GetProperty("sub-font"),
        GetProperty("sub-color"),
        Reading(BorderName),
        Reading("sub-shadow-offset"),
        GetProperty("sub-back-color"));

    public double VolumeCeiling => Finite(GetDouble("volume-max"), SoundAdjust.PlainCeiling);

    public void SetPicture(PictureAdjust picture)
    {
        var wanted = picture.Clamped();
        Swap(ColorLabel, wanted.Brightness == 0 && wanted.Contrast == 0 && wanted.Saturation == 0 && wanted.Gamma == 0
            ? null
            : "eq=brightness=" + F(wanted.Brightness / 100.0)
                + ":contrast=" + F(1 + wanted.Contrast / 100.0)
                + ":saturation=" + F(1 + wanted.Saturation / 100.0)
                + ":gamma=" + F(1 + wanted.Gamma / 100.0));

        Swap(HueLabel, wanted.Hue == 0 ? null : "hue=h=" + F(wanted.Hue * HueSpan));
        Swap(SharpLabel, wanted.Sharpness == 0
            ? null
            : "unsharp=luma_msize_x=5:luma_msize_y=5:luma_amount=" + F(wanted.Sharpness * SharpSpan));

        var keep = 1 - 2 * wanted.Crop / 100.0;
        var edge = wanted.Crop / 100.0;
        Swap(CropLabel, wanted.Crop == 0
            ? null
            : "crop=w=iw*" + F(keep) + ":h=ih*" + F(keep) + ":x=iw*" + F(edge) + ":y=ih*" + F(edge));

        TrySet("deinterlace", wanted.Deinterlace ? "yes" : "no");
    }

    public void SetSound(SoundAdjust sound)
    {
        if (!sound.Valid) return;
        var wanted = sound.Clamped();
        Swap(EqualizerLabel, wanted.Bands.All(gain => gain == 0)
            ? null
            : "lavfi=[" + string.Join(",", wanted.Bands.Select((gain, index) =>
                "equalizer=f=" + SoundAdjust.BandHertz[index].ToString(CultureInfo.InvariantCulture)
                    + ":t=q:w=1.4:g=" + F(gain))) + "]", "af");

        Swap(NormalizeLabel, wanted.Normalize ? "lavfi=[dynaudnorm=f=150:g=15:p=0.9]" : null, "af");
        TrySet("volume-max", F(wanted.Boost ? SoundAdjust.BoostCeiling : SoundAdjust.PlainCeiling));
        if (!wanted.Boost && Volume > SoundAdjust.PlainCeiling) SetVolume(SoundAdjust.PlainCeiling);
    }

    public void SetSubtitleStyle(SubtitleStyle style)
    {
        Write("sub-font", style.Font);
        Write("sub-color", style.Color);
        Write(BorderName, style.Outline is { } outline ? F(outline) : null);
        Write("sub-shadow-offset", style.Shadow is { } shadow ? F(shadow) : null);
        Write("sub-back-color", style.Background);
    }

    /// <summary>
    /// mpv 0.39 <c>sub-border-size</c>'i <c>sub-outline-size</c> yapti; ad calisma aninda
    /// secilir, yeni ad yoksa eskisi kullanilir.
    /// </summary>
    private string BorderName => GetProperty("sub-outline-size") is not null ? "sub-outline-size" : "sub-border-size";

    private double? Reading(string name)
    {
        var value = GetDouble(name);
        return double.IsFinite(value) ? value : null;
    }

    /// <summary>Deger <c>null</c> ise ozellik <c>option-info</c>'daki varsayilanina doner.</summary>
    private void Write(string name, string? value)
    {
        if (value is not null)
        {
            TrySet(name, value);
            return;
        }

        if (GetProperty("option-info/" + name + "/default-value") is { } fallback) TrySet(name, fallback);
    }

    private void Swap(string label, string? filter, string property = "vf")
    {
        CommandSync(property, "remove", "@" + label);
        if (filter is not null) CommandSync(property, "add", "@" + label + ":" + filter);
    }

    private static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static int Percent(double value) => (int)Math.Round(value * 100, MidpointRounding.AwayFromZero);

    private static int Scaled(double value, double span) => (int)Math.Round(value / span, MidpointRounding.AwayFromZero);

    private static string? Segment(string chain, string label)
    {
        var mark = "@" + label;
        var at = chain.IndexOf(mark, StringComparison.Ordinal);
        if (at < 0) return null;
        var rest = chain[(at + mark.Length)..];
        var end = rest.IndexOf(",@", StringComparison.Ordinal);
        return end < 0 ? rest : rest[..end];
    }

    private static double Field(string? segment, string name, double fallback)
    {
        if (segment is null) return fallback;
        var match = Regex.Match(segment, "(?<![\\w-])" + Regex.Escape(name) + "=" + Escape + "(-?[0-9]+(?:\\.[0-9]+)?)");
        return match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    /// <summary>
    /// mpv suzgec dizgesini geri verirken degerleri <c>%uzunluk%</c> onekiyle kacirir
    /// (<c>brightness=%3%0.1</c>); okuma bu oneki istege bagli gecer.
    /// </summary>
    private const string Escape = "(?:%[0-9]+%)?";

    private static double Factor(string? segment, string name)
    {
        if (segment is null) return 0;
        var match = Regex.Match(segment, "(?<![\\w-])" + Regex.Escape(name) + "=" + Escape + "i[wh]\\*(-?[0-9]+(?:\\.[0-9]+)?)");
        return match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static IReadOnlyList<int> Gains(string? segment)
    {
        var bands = new int[SoundAdjust.BandCount];
        if (segment is null) return bands;
        var matches = Regex.Matches(segment, "(?<![\\w-])g=(-?[0-9]+(?:\\.[0-9]+)?)");
        for (var index = 0; index < bands.Length && index < matches.Count; index++)
            if (double.TryParse(matches[index].Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var gain))
                bands[index] = (int)Math.Round(gain, MidpointRounding.AwayFromZero);

        return bands;
    }
}
