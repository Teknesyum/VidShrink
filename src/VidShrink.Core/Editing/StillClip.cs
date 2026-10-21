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
    public const string Black = "#000000";

    public const string White = "#FFFFFF";

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

    public const double MinSeconds = 0.5;

    public const double MaxSeconds = 60;

    public static readonly TimeSpan PruneGrace = TimeSpan.FromHours(1);

    public static double ClampSeconds(double seconds)
        => double.IsFinite(seconds) ? Math.Round(Math.Clamp(seconds, MinSeconds, MaxSeconds), 2) : ImageSeconds;

    public static (int Width, int Height) ImageFrame(MediaInfo? info)
    {
        if (info is not { Width: > 0, Height: > 0 }) return (FallbackWidth, FallbackHeight);
        var portrait = info.Height > info.Width;
        var boxWidth = portrait ? FallbackHeight : FallbackWidth;
        var boxHeight = portrait ? FallbackWidth : FallbackHeight;
        var scale = Math.Min(1, Math.Min((double)boxWidth / info.Width, (double)boxHeight / info.Height));
        return (Math.Max(2, (int)Math.Round(info.Width * scale) / 2 * 2), Math.Max(2, (int)Math.Round(info.Height * scale) / 2 * 2));
    }

    public static string FromHsl(double hue, double saturation, double lightness)
    {
        var h = (double.IsFinite(hue) ? (hue % 360 + 360) % 360 : 0) / 60;
        var s = double.IsFinite(saturation) ? Math.Clamp(saturation, 0, 1) : 0;
        var l = double.IsFinite(lightness) ? Math.Clamp(lightness, 0, 1) : 0;
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = (int)h switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        var m = l - c / 2;
        static int Byte(double v) => (int)Math.Round(Math.Clamp(v, 0, 1) * 255);
        return "#" + Byte(r + m).ToString("X2", CultureInfo.InvariantCulture) + Byte(g + m).ToString("X2", CultureInfo.InvariantCulture) + Byte(b + m).ToString("X2", CultureInfo.InvariantCulture);
    }

    public static bool TryHsl(string? text, out double hue, out double saturation, out double lightness)
    {
        hue = saturation = lightness = 0;
        if (!TryColor(text, out var value)) return false;
        var rgb = Convert.ToInt32(value[2..], 16);
        var r = (rgb >> 16 & 0xFF) / 255d;
        var g = (rgb >> 8 & 0xFF) / 255d;
        var b = (rgb & 0xFF) / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        lightness = (max + min) / 2;
        if (delta == 0) return true;
        saturation = delta / (1 - Math.Abs(2 * lightness - 1));
        hue = max == r ? 60 * ((g - b) / delta % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        if (hue < 0) hue += 360;
        return true;
    }

    public static StringComparer PathComparer => StringComparer.FromComparison(WatchFolder.PathComparison);

    public static string PathKey(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    public static int Prune(string folder, IEnumerable<string> keep, DateTime nowUtc, TimeSpan grace)
    {
        var kept = new HashSet<string>(keep.Where(path => !string.IsNullOrEmpty(path)).Select(PathKey), PathComparer);
        var removed = 0;
        try
        {
            if (!Directory.Exists(folder)) return 0;
            foreach (var file in new DirectoryInfo(folder).EnumerateFiles())
            {
                if (kept.Contains(file.FullName) || nowUtc - file.LastWriteTimeUtc < grace) continue;
                try
                {
                    file.Delete();
                    removed++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return removed;
    }

    public static int Prune(EditProjectStore store, IEnumerable<string>? inUse = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        var folder = Path.Combine(store.Folder, FolderName);
        var keep = new List<string>(inUse ?? Array.Empty<string>());
        try
        {
            if (!Directory.Exists(folder)) return 0;
            foreach (var file in Directory.EnumerateFiles(store.Folder, "*" + EditProject.Extension))
            {
                if (EditProject.Read(file) is not { } project) return 0;
                keep.Add(project.Source.Path);
                keep.AddRange(project.ExtraSources.Select(extra => extra.Stamp.Path));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }

        return Prune(folder, keep, DateTime.UtcNow, PruneGrace);
    }

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
