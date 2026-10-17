using System.Globalization;

namespace VidShrink.Core;

public sealed record HlsVariant(long Bandwidth, int Width, int Height);

/// <summary>
/// HLS ana listesinin katmanları (<c>#EXT-X-STREAM-INF</c>): bant genişliği ve çözünürlük,
/// yüksekten düşüğe. libmpv iz listesinde yalnız seçili katmanın izlerini gösterir, öbür
/// katmanlar orada yok; kalite menüsü katmanları bu listeden okur ve <c>hls-bitrate</c> ile seçer.
/// </summary>
public static class HlsVariants
{
    private const string StreamInf = "#EXT-X-STREAM-INF:";

    public static bool IsCandidate(string? path)
        => path is not null && MediaAddress.IsAddress(path)
           && string.Equals(Path.GetExtension(MediaAddress.WithoutQuery(path)), ".m3u8", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<HlsVariant> Parse(string text)
    {
        var variants = new List<HlsVariant>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith(StreamInf, StringComparison.OrdinalIgnoreCase)) continue;
            var attributes = line[StreamInf.Length..];
            if (Attribute(attributes, "BANDWIDTH") is not { } value
                || !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var bandwidth)
                || bandwidth <= 0
                || variants.Any(variant => variant.Bandwidth == bandwidth)) continue;
            var (width, height) = Resolution(Attribute(attributes, "RESOLUTION"));
            variants.Add(new HlsVariant(bandwidth, width, height));
        }

        return variants.OrderByDescending(variant => variant.Bandwidth).ToList();
    }

    private static (int Width, int Height) Resolution(string? value)
    {
        var parts = value?.Split('x', 'X');
        return parts is [var w, var h]
               && int.TryParse(w, NumberStyles.None, CultureInfo.InvariantCulture, out var width)
               && int.TryParse(h, NumberStyles.None, CultureInfo.InvariantCulture, out var height)
               && width > 0 && height > 0
            ? (width, height)
            : (0, 0);
    }

    private static string? Attribute(string attributes, string name)
    {
        var i = 0;
        while (i < attributes.Length)
        {
            var eq = attributes.IndexOf('=', i);
            if (eq < 0) return null;
            var key = attributes[i..eq].Trim();
            var start = eq + 1;
            int end;
            string value;
            if (start < attributes.Length && attributes[start] == '"')
            {
                var close = attributes.IndexOf('"', start + 1);
                if (close < 0) return null;
                value = attributes[(start + 1)..close];
                end = attributes.IndexOf(',', close);
            }
            else
            {
                end = attributes.IndexOf(',', start);
                value = (end < 0 ? attributes[start..] : attributes[start..end]).Trim();
            }

            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) return value;
            if (end < 0) return null;
            i = end + 1;
        }

        return null;
    }
}
