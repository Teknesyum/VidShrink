using System.Globalization;

namespace VidShrink.Core;

public static class GifPalette
{
    public const int MaxFps = 50;

    public static string Filter(int fps, int? width)
    {
        var scale = width is { } w
            ? ",scale=" + w.ToString(CultureInfo.InvariantCulture) + ":-1:flags=lanczos"
            : string.Empty;
        return string.Concat(
            "fps=", fps.ToString(CultureInfo.InvariantCulture),
            scale,
            ",split[a][b];[a]palettegen[p];[b][p]paletteuse");
    }

    public static IReadOnlyList<string> Build(string source, string target, int fps, int? width = null)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source path is required.", nameof(source));
        if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("Target path is required.", nameof(target));
        return new[]
        {
            "-hide_banner", "-y", "-nostdin",
            "-i", source,
            "-vf", Filter(fps, width),
            "-loop", "0",
            target
        };
    }
}
