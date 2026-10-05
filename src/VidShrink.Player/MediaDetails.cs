using System.Globalization;

namespace VidShrink.Player;

/// <summary>
/// Calan dosyanin kimligi. Renk alanlari motorun cozdugu kareden gelir (<c>video-params</c>);
/// kare gelmeden once bos kalirlar. <see cref="BitDepth"/> 0 ve <see cref="DolbyVisionProfile"/> 0 bilinmiyor demektir.
/// </summary>
public sealed record MediaDetails(
    string? VideoCodec,
    int Width,
    int Height,
    double FramesPerSecond,
    double BitsPerSecond,
    string? AudioCodec,
    int AudioChannels,
    int AudioSampleRate,
    string? Primaries = null,
    string? Transfer = null,
    int BitDepth = 0,
    int DolbyVisionProfile = 0)
{
    /// <summary>
    /// Dinamik aralik etiketi: Dolby Vision, HDR10 (PQ), HLG ya da SDR; aktarim egrisi bilinmiyorsa <c>null</c>.
    /// </summary>
    public string? DynamicRange
    {
        get
        {
            if (DolbyVisionProfile > 0) return "Dolby Vision";
            if (string.IsNullOrWhiteSpace(Transfer)) return null;
            return Transfer.Trim().ToLowerInvariant() switch
            {
                "pq" => "HDR10",
                "hlg" => "HLG",
                _ => "SDR"
            };
        }
    }

    /// <summary>Renk satirinin parcalari: renk birincilleri, aktarim egrisi, bit derinligi; bilinmeyen parca yazilmaz.</summary>
    public IReadOnlyList<string> ColorParts
    {
        get
        {
            var parts = new List<string>(3);
            if (ColorName(Primaries) is { } primaries) parts.Add(primaries);
            if (ColorName(Transfer) is { } transfer) parts.Add(transfer);
            if (BitDepth > 0) parts.Add(BitDepth.ToString(CultureInfo.InvariantCulture) + " bit");
            return parts;
        }
    }

    /// <summary>mpv'nin kucuk harfli renk adini okunur yazima cevirir (<c>bt.2020</c> → <c>BT.2020</c>, <c>pq</c> → <c>PQ</c>).</summary>
    public static string? ColorName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var name = raw.Trim().ToLowerInvariant();
        if (name is "unknown" or "auto") return null;
        if (name.StartsWith("bt.", StringComparison.Ordinal)) return "BT." + name[3..];
        return name switch
        {
            "pq" => "PQ",
            "hlg" => "HLG",
            "srgb" => "sRGB",
            "dci-p3" => "DCI-P3",
            "display-p3" => "Display P3",
            _ => name
        };
    }

    /// <summary>
    /// Piksel bicimi adindan bilesen basina bit: <c>yuv420p10</c> → 10, <c>p010</c> → 10, <c>yuv420p</c> ve <c>nv12</c> → 8;
    /// taninmayan ad 0.
    /// </summary>
    public static int BitDepthOf(string? pixelFormat)
    {
        if (string.IsNullOrWhiteSpace(pixelFormat)) return 0;
        var name = pixelFormat.Trim().ToLowerInvariant();
        if (name.EndsWith("le", StringComparison.Ordinal) || name.EndsWith("be", StringComparison.Ordinal)) name = name[..^2];

        if (name.Length == 4 && name.StartsWith("p0", StringComparison.Ordinal)
            && int.TryParse(name[2..], NumberStyles.None, CultureInfo.InvariantCulture, out var packed) && packed is >= 9 and <= 16)
            return packed;

        var planar = name.StartsWith("yuv", StringComparison.Ordinal) || name.StartsWith("gbr", StringComparison.Ordinal);
        var at = name.LastIndexOf('p');
        if (planar && at > 0)
        {
            var tail = name[(at + 1)..];
            if (tail.Length == 0) return 8;
            return int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var bits) && bits is >= 9 and <= 16 ? bits : 0;
        }

        return name is "nv12" or "nv21" or "rgb24" or "bgr24" or "rgba" or "bgra" or "rgb0" or "bgr0" or "gray" or "yuyv422" or "uyvy422"
            ? 8
            : 0;
    }
}
