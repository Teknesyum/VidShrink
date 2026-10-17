using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace VidShrink.Core.Editing;

/// <summary>
/// Resim, duz renk ve dondurulmus kare icin kisa ara video: cizelgeye bildik bir kaynak gibi girer, onizleme
/// ve teslim yolu degismez. Kare olcusu ve hizi ilk kaynaktan gelir; resim olcuye sigdirilip siyahla doldurulur.
/// Ses akisi yoktur, teslim sessizligi kendisi ekler.
/// </summary>
public static class StillClip
{
    public const double ImageSeconds = 5;

    public const double FreezeSeconds = 2;

    public const string FolderName = "ara-klipler";

    public const int FallbackWidth = 1920;

    public const int FallbackHeight = 1080;

    public const double FallbackFps = 30;

    /// <summary>Ara videonun kare olcusu: ilk kaynagin gorunen olcusu, cift sayiya yuvarlanir.</summary>
    public static (int Width, int Height) Frame(MediaInfo? info)
        => info is { Width: > 0, Height: > 0 }
            ? (Math.Max(2, ClipFilters.DisplayWidth(info.Width, info.ParNum, info.ParDen) / 2 * 2), Math.Max(2, info.Height / 2 * 2))
            : (FallbackWidth, FallbackHeight);

    public static double Rate(MediaInfo? info) => info is { Fps: > 0 } ? info.Fps : FallbackFps;

    /// <summary><c>#RRGGBB</c> ya da <c>RRGGBB</c> metnini ffmpeg'in <c>0xRRGGBB</c> bicimine cevirir.</summary>
    public static bool TryColor(string? text, out string color)
    {
        color = string.Empty;
        var hex = text?.Trim().TrimStart('#') ?? string.Empty;
        if (hex.Length != 6 || !hex.All(Uri.IsHexDigit)) return false;
        color = "0x" + hex.ToUpperInvariant();
        return true;
    }

    public static IReadOnlyList<string> FromImage(string image, string output, int width, int height, double fps, double seconds)
    {
        var size = Size(width, height);
        var args = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-f", "image2", "-pattern_type", "none", "-loop", "1", "-framerate", Number(fps), "-i", image,
            "-t", Number(seconds),
            "-vf", "scale=" + size + ":force_original_aspect_ratio=decrease,pad=" + size + ":(ow-iw)/2:(oh-ih)/2:color=black,setsar=1,format=yuv420p"
        };
        args.AddRange(Encode(output, fps));
        return args;
    }

    public static IReadOnlyList<string> FromColor(string color, string output, int width, int height, double fps, double seconds)
    {
        if (!TryColor(color, out var value)) throw new ArgumentException("Renk #RRGGBB olmali", nameof(color));
        var args = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-f", "lavfi", "-i", "color=c=" + value + ":s=" + width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture)
                + ":r=" + Number(fps) + ":d=" + Number(seconds),
            "-vf", "setsar=1,format=yuv420p"
        };
        args.AddRange(Encode(output, fps));
        return args;
    }

    /// <summary>Kaynagin <paramref name="seconds"/> anindaki kareyi tek resme cikarir; arama kare dogrulugundadir.</summary>
    public static IReadOnlyList<string> GrabFrame(string source, double seconds, string output) => new[]
    {
        "-hide_banner", "-loglevel", "error", "-y",
        "-ss", Number(Math.Max(0, seconds)), "-i", source,
        "-map", "0:v:0", "-frames:v", "1", "-an", "-threads", "2",
        "-f", "image2", "-update", "1", output
    };

    /// <summary>Ayni girdi ve olcu ayni adi verir; dosya varsa yeniden kodlanmaz.</summary>
    public static string OutputName(string kind, params object[] key)
    {
        var text = string.Join("|", key.Select(part => Convert.ToString(part, CultureInfo.InvariantCulture)));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return kind + "-" + Convert.ToHexString(hash, 0, 8).ToLowerInvariant() + ".mp4";
    }

    private static IEnumerable<string> Encode(string output, double fps) => new[]
    {
        "-r", Number(fps),
        "-c:v", "libx264", "-preset", "veryfast", "-tune", "stillimage", "-crf", "18",
        "-g", Math.Max(1, (int)Math.Round(fps)).ToString(CultureInfo.InvariantCulture),
        "-pix_fmt", "yuv420p", "-threads", "2", "-an",
        "-movflags", "+faststart", "-f", "mp4", output
    };

    private static string Size(int width, int height)
        => width.ToString(CultureInfo.InvariantCulture) + ":" + height.ToString(CultureInfo.InvariantCulture);

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
