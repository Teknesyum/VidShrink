namespace VidShrink.Core;

public sealed record HdrResolution(string PixelFormat, string? VideoFilter, IReadOnlyList<string> ColorArgs, bool PolicyChanged)
{
    /// <summary>
    /// Kodlayicinin HDR10 destegi henuz olculmedi; karar gecici ve olcum gelince yenilenmeli.
    /// <see cref="PolicyChanged"/> bu durumda kurulmaz: olculmemis bir kodlayici
    /// "10 bit tasiyamiyor" demek degildir.
    /// </summary>
    public bool NotMeasured { get; init; }

    /// <summary>
    /// Kaynagin DV 8.1 RPU'su kodlayiciya <see cref="HdrResolver.DolbyVisionArgs"/> ile
    /// tasinacak (<c>docs/olcumler/b4-hdr-dinamik.md</c>). MP4 ailesinde <c>dvcC</c> kutusu
    /// ayrica <see cref="HdrResolver.Mp4DolbyVisionArgs"/> ister.
    /// </summary>
    public bool DolbyVisionCarried { get; init; }

    /// <summary>
    /// Kaynaktaki dinamik HDR verisi (HDR10+ ya da tasinamayan DV) ciktida olmayacak; statik
    /// HDR10 katmani kalir. Ton eslemede kurulmaz: orada zaten SDR'a inilir ve ayri not duser.
    /// </summary>
    public bool DynamicMetadataDropped { get; init; }
}

public interface IHdr10EncoderAvailability
{
    string? Hdr10PixelFormat(string codec);
}

public static class HdrResolver
{
    private static readonly HashSet<string> SoftwareHdr10Codecs = new(StringComparer.OrdinalIgnoreCase) { "libx265", "libsvtav1" };

    public const string TonemapFilter = "zscale=t=linear:npl=100,tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=limited,format=yuv420p";

    /// <summary>
    /// DV tasiyan kodlamanin argumanlari. Renk argumanlarindan ayri durur: kalibrasyon
    /// ornekleri VBV'siz CRF ile kodlanir ve x265 orada DV bayragiyla acilmaz.
    /// </summary>
    public static readonly IReadOnlyList<string> DolbyVisionArgs = new[] { "-dolbyvision", "1" };

    /// <summary>MP4 ailesi <c>dvcC</c>/<c>dvvC</c> kutusunu bu olmadan yazmaz (ffmpeg 9 uyarisi).</summary>
    public static readonly IReadOnlyList<string> Mp4DolbyVisionArgs = new[] { "-strict", "unofficial" };

    /// <summary>
    /// <paramref name="dolbyVisionCarriable"/> cagiranin bildigi engeli tasir: renk matrisi
    /// donusumu (zscale RPU'yu dusurur, kodlama "frame without DOVI metadata" ile durur) ya da
    /// x265'te VBV'siz oran denetimi ("Dolby Vision requires VBV settings").
    /// </summary>
    public static HdrResolution Resolve(MediaInfo info, HdrPolicy requested, string codec, IEncoderAvailability? availability,
        bool dolbyVisionCarriable = true)
    {
        if (!info.IsHdr)
            return new HdrResolution("yuv420p", null, Array.Empty<string>(), false);

        var effective = requested;
        var policyChanged = false;
        var notMeasured = false;
        if (effective == HdrPolicy.Preserve && !SupportsHdr10(codec, availability, out notMeasured))
        {
            effective = HdrPolicy.TonemapToSdr;
            policyChanged = true;
        }

        if (effective == HdrPolicy.TonemapToSdr)
        {
            var colorArgs = new List<string> { "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709" };
            return new HdrResolution("yuv420p", TonemapFilter, colorArgs, policyChanged) { NotMeasured = notMeasured };
        }

        var preserveArgs = new List<string>();
        if (info.ColorPrimaries is { } primaries) preserveArgs.AddRange(new[] { "-color_primaries", primaries });
        if (info.ColorTransfer is { } transfer) preserveArgs.AddRange(new[] { "-color_trc", transfer });
        if (info.ColorSpace is { } space) preserveArgs.AddRange(new[] { "-colorspace", space });

        if (codec.Equals("libx265", StringComparison.OrdinalIgnoreCase))
        {
            var x265Params = "hdr10-opt=1";
            if (info.ColorPrimaries is { } x265Primaries) x265Params += $":colorprim={x265Primaries}";
            if (info.ColorTransfer is { } x265Transfer) x265Params += $":transfer={x265Transfer}";
            if (info.ColorSpace is { } x265Space) x265Params += $":colormatrix={x265Space}";
            if (info.MasteringDisplayMetadata is { } mastering) x265Params += $":master-display={mastering}";
            if (info.ContentLightLevel is { } cll) x265Params += $":max-cll={cll}";
            preserveArgs.AddRange(new[] { "-x265-params", x265Params });
        }

        var carried = dolbyVisionCarriable && CarriesDolbyVision(info, codec);

        return new HdrResolution(Hdr10PixelFormat(codec, availability) ?? "yuv420p10le", null, preserveArgs, false)
        {
            NotMeasured = notMeasured,
            DolbyVisionCarried = carried,
            DynamicMetadataDropped = info.HasHdr10Plus || (info.HasDolbyVision && !carried)
        };
    }

    /// <summary>
    /// Yalniz profil 8.1 (HDR10 uyumlu taban katman) ve yalniz yazilim HDR10 kodlayicilari:
    /// ffmpeg'in DV yazicisi libx265 ve libsvtav1'de var, donanim kodlayicilarinda yok.
    /// Profil 5 kapsam disi (<c>docs/handbrake/handbrake-yanit.md</c>).
    /// </summary>
    public static bool CarriesDolbyVision(MediaInfo info, string codec)
        => info.DolbyVisionProfile == 8 && info.DolbyVisionCompatibilityId == 1 && SoftwareHdr10Codecs.Contains(codec);

    /// <summary>
    /// Olculmemis kodlayici "destekliyor" sayilir ve <paramref name="notMeasured"/> kurulur:
    /// karar gecici, olcum gelince yeniden hesaplanir. Alternatifi — olculmemisi destegi yok
    /// saymak — kullaniciya HDR'i kaybettigini soyler ve o cumle yanlis olurdu.
    /// </summary>
    private static bool SupportsHdr10(string codec, IEncoderAvailability? availability, out bool notMeasured)
    {
        notMeasured = false;
        if (SoftwareHdr10Codecs.Contains(codec))
        {
            if (availability is null) return true;
            var encoderState = availability.KnownState(codec);
            if (encoderState == EncoderProbeState.Unmeasured)
            {
                notMeasured = true;
                return true;
            }
            return encoderState == EncoderProbeState.Working;
        }

        if (availability is IHdr10ProbeAvailability probe && probe.Hdr10State(codec) == EncoderProbeState.Unmeasured)
        {
            notMeasured = true;
            return true;
        }
        return Hdr10PixelFormat(codec, availability) is not null;
    }

    private static string? Hdr10PixelFormat(string codec, IEncoderAvailability? availability)
        => availability is IHdr10EncoderAvailability hdr ? hdr.Hdr10PixelFormat(codec) : null;
}
