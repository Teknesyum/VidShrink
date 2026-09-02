namespace VidShrink.Core;

public sealed record HdrResolution(string PixelFormat, string? VideoFilter, IReadOnlyList<string> ColorArgs, bool PolicyChanged)
{
    /// <summary>
    /// Kodlayicinin HDR10 destegi henuz olculmedi; karar gecici ve olcum gelince yenilenmeli.
    /// <see cref="PolicyChanged"/> bu durumda kurulmaz: olculmemis bir kodlayici
    /// "10 bit tasiyamiyor" demek degildir.
    /// </summary>
    public bool NotMeasured { get; init; }
}

public interface IHdr10EncoderAvailability
{
    string? Hdr10PixelFormat(string codec);
}

public static class HdrResolver
{
    private static readonly HashSet<string> SoftwareHdr10Codecs = new(StringComparer.OrdinalIgnoreCase) { "libx265", "libsvtav1" };

    public const string TonemapFilter = "zscale=t=linear:npl=100,tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=limited,format=yuv420p";

    public static HdrResolution Resolve(MediaInfo info, HdrPolicy requested, string codec, IEncoderAvailability? availability)
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

        return new HdrResolution(Hdr10PixelFormat(codec, availability) ?? "yuv420p10le", null, preserveArgs, false)
        {
            NotMeasured = notMeasured
        };
    }

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
            if (availability is IEncoderMeasurementState software && !software.IsMeasured(codec))
            {
                notMeasured = true;
                return true;
            }
            return availability.WorksAsEncoder(codec);
        }

        if (availability is IEncoderMeasurementState state && !state.IsHdr10Measured(codec))
        {
            notMeasured = true;
            return true;
        }
        return Hdr10PixelFormat(codec, availability) is not null;
    }

    private static string? Hdr10PixelFormat(string codec, IEncoderAvailability? availability)
        => availability is IHdr10EncoderAvailability hdr ? hdr.Hdr10PixelFormat(codec) : null;
}
