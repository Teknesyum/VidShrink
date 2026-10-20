using System.Globalization;

namespace VidShrink.Core;

public enum FrameBackdropKind { Solid, Gradient, Image }

public sealed record FrameBackdrop(FrameBackdropKind Kind, string Color0, string? Color1 = null, string? ImagePath = null)
{
    public static FrameBackdrop Solid(string color) => new(FrameBackdropKind.Solid, color);

    public static FrameBackdrop Gradient(string from, string to) => new(FrameBackdropKind.Gradient, from, to);

    public static FrameBackdrop Image(string path) => new(FrameBackdropKind.Image, "0x000000", null, path);
}

public sealed record FrameOptions(int Padding, int Corner, bool Shadow, FrameBackdrop Backdrop);

public static class RecordingFrame
{
    public const string TargetSuffix = "-cerceveli";

    public const int MaxPadding = 512;

    public const double ShadowOpacity = 0.5;

    public static readonly int[] PaddingChoices = [32, 64, 96, 128];

    public static readonly int[] CornerChoices = [0, 12, 24, 48];

    public static (int Width, int Height) OutputSize(int width, int height, int padding)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "goruntu olcusu pozitif olmali.");
        if (padding < 0 || padding > MaxPadding) throw new ArgumentOutOfRangeException(nameof(padding), padding, $"dolgu 0 ile {MaxPadding} arasinda olmali.");
        return (Even(width + 2 * padding), Even(height + 2 * padding));
    }

    public static int EffectiveCorner(int width, int height, int corner)
        => Math.Clamp(corner, 0, Math.Min(width, height) / 2);

    public static int ShadowOffset(int padding) => Math.Max(2, padding / 8);

    public static int ShadowBlur(int padding) => Math.Max(2, padding / 4);

    public static string Color(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var hex = value.Trim();
        if (hex.StartsWith('#')) hex = hex[1..];
        else if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex[2..];
        if (hex.Length == 8) hex = hex[2..];
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            throw new ArgumentException($"renk RRGGBB ya da AARRGGBB olmali: {value}", nameof(value));
        return "0x" + hex.ToUpperInvariant();
    }

    public static string HoleAlpha(int width, int height, int padding, int corner)
    {
        var r = EffectiveCorner(width, height, corner);
        var x0 = padding;
        var y0 = padding;
        var x1 = padding + width - 1;
        var y1 = padding + height - 1;
        var inside = $"between(X,{x0},{x1})*between(Y,{y0},{y1})";
        if (r == 0) return $"if({inside},0,255)";

        var left = x0 + r;
        var right = x1 - r;
        var top = y0 + r;
        var bottom = y1 - r;
        return $"if({inside}*lte(pow(max(max({left}-X,X-{right}),0),2)+pow(max(max({top}-Y,Y-{bottom}),0),2),{r * r}),0,255)";
    }

    public static IReadOnlyList<string> BackdropInput(FrameBackdrop backdrop, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(backdrop);
        var size = $"{width}x{height}";
        return backdrop.Kind switch
        {
            FrameBackdropKind.Solid => ["-f", "lavfi", "-i", $"color=c={Color(backdrop.Color0)}:s={size}:r=1:d=1"],
            FrameBackdropKind.Gradient => ["-f", "lavfi", "-i",
                $"gradients=s={size}:c0={Color(backdrop.Color0)}:c1={Color(backdrop.Color1 ?? backdrop.Color0)}:nb_colors=2:x0=0:y0=0:x1={width - 1}:y1={height - 1}:speed=0:r=1:d=1"],
            FrameBackdropKind.Image when !string.IsNullOrWhiteSpace(backdrop.ImagePath) => ["-i", backdrop.ImagePath],
            FrameBackdropKind.Image => throw new ArgumentException("gorsel arka plan icin dosya secilmedi.", nameof(backdrop)),
            _ => throw new ArgumentOutOfRangeException(nameof(backdrop), backdrop.Kind, "tanimsiz arka plan.")
        };
    }

    public static string Graph(int width, int height, FrameOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var (w, h) = OutputSize(width, height, options.Padding);
        var p = options.Padding;

        var background = options.Backdrop.Kind == FrameBackdropKind.Image
            ? $"[1:v]scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},setsar=1,format=yuva444p[bg]"
            : "[1:v]format=yuva444p[bg]";

        var parts = new List<string> { background };
        var layerSource = "bg";
        if (options.Shadow && p > 0)
        {
            var opacity = ShadowOpacity.ToString("0.##", CultureInfo.InvariantCulture);
            parts.Add($"color=c=black@{opacity}:s={width}x{height}:r=1:d=1,format=yuva444p,pad={w}:{h}:{p}:{p + ShadowOffset(p)}:color=black@0,boxblur={ShadowBlur(p)}:1[sh]");
            parts.Add("[bg][sh]overlay=0:0:format=yuv444[bgs]");
            layerSource = "bgs";
        }

        parts.Add($"[{layerSource}]format=yuva444p,geq=lum='p(X,Y)':cb='p(X,Y)':cr='p(X,Y)':a='{HoleAlpha(width, height, p, options.Corner)}'[layer]");
        parts.Add($"[0:v]setsar=1,pad={w}:{h}:{p}:{p}[base]");
        parts.Add("[base][layer]overlay=0:0:eof_action=repeat,format=yuv420p[v]");
        return string.Join(';', parts);
    }

    public static IReadOnlyList<string> Arguments(string input, string output, int width, int height, FrameOptions options, int? threads = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(output);
        ArgumentNullException.ThrowIfNull(options);

        var (w, h) = OutputSize(width, height, options.Padding);
        var args = new List<string> { "-hide_banner", "-y" };
        if (threads is { } n) args.AddRange(["-filter_complex_threads", n.ToString(CultureInfo.InvariantCulture)]);
        args.AddRange(["-i", input]);
        args.AddRange(BackdropInput(options.Backdrop, w, h));
        args.AddRange(["-filter_complex", Graph(width, height, options), "-map", "[v]", "-map", "0:a?"]);
        args.AddRange(VideoCodec(output));
        args.AddRange(["-c:a", "copy"]);
        if (threads is { } t) args.AddRange(["-threads", t.ToString(CultureInfo.InvariantCulture)]);
        args.Add(output);
        return args;
    }

    public static IReadOnlyList<string> VideoCodec(string output)
    {
        var extension = Path.GetExtension(output).ToLowerInvariant();
        return extension switch
        {
            ".webm" => ["-c:v", "libvpx-vp9", "-crf", "32", "-b:v", "0", "-row-mt", "1"],
            ".mp4" or ".mov" => ["-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart"],
            _ => ["-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p"]
        };
    }

    public static string TargetFor(string source, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var folder = Path.GetDirectoryName(source) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(source) + TargetSuffix;
        var extension = Path.GetExtension(source);
        var candidate = Path.Combine(folder, stem + extension);
        for (var index = 2; exists(candidate); index++)
            candidate = Path.Combine(folder, stem + "-" + index.ToString(CultureInfo.InvariantCulture) + extension);
        return candidate;
    }

    private static int Even(int value) => value + (value & 1);
}
