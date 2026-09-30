using System.Globalization;
using Avalonia.Media;

namespace VidShrink.App;

/// <summary>
/// Kullanicinin videoya yazdigi rengin (<c>0xRRGGBB</c>) arayuzdeki ornegi. Bu renk tema rengi
/// degil, belgenin verisidir; palete girmez, palet degisince de degismez.
/// </summary>
internal static class KullaniciRengi
{
    internal static IBrush Firca(uint rgb) =>
        new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));

    internal static string Yaz(uint rgb) => "#" + (rgb & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture);

    /// <summary><c>#RRGGBB</c>, <c>RRGGBB</c> ya da <c>#RGB</c> okur.</summary>
    internal static bool Coz(string? metin, out uint rgb)
    {
        rgb = 0;
        var yalin = (metin ?? string.Empty).Trim().TrimStart('#');
        if (yalin.Length == 3) yalin = string.Concat(yalin[0], yalin[0], yalin[1], yalin[1], yalin[2], yalin[2]);
        return yalin.Length == 6 && uint.TryParse(yalin, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out rgb);
    }
}
