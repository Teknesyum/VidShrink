using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace VidShrink.Core.Editing;

public static class ProxyMedia
{
    public const string FolderName = "vekiller";

    public const string Extension = ".mp4";

    public const int TargetHeight = 540;

    public const int Gop = 12;

    public const int Threads = 2;

    private static readonly HashSet<string> HeavyCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "hevc", "av1", "vp9", "prores", "dnxhd", "cfhd", "ffv1"
    };

    public static string DefaultFolder => Path.Combine(EditProjectStore.DefaultFolder, FolderName);

    public static bool IsHeavy(MediaInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.Width <= 0 || info.Height <= 0) return false;
        return (long)info.Width * info.Height > 1920L * 1080 || info.BitDepth > 8 || HeavyCodecs.Contains(info.VideoCodec ?? string.Empty);
    }

    public static (int Width, int Height) Size(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Kare boyutu pozitif olmali");
        if (height <= TargetHeight) return (Even(width), Even(height));
        var scaled = (int)Math.Round(width * (double)TargetHeight / height, MidpointRounding.AwayFromZero);
        return (Math.Max(2, Even(scaled)), TargetHeight);
    }

    private static int Even(int value) => Math.Max(2, value - (value & 1));

    public static string KeyOf(string source)
    {
        var full = Path.GetFullPath(source);
        var key = OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
    }

    public static string PathFor(string folder, SourceStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        return Path.Combine(folder, KeyOf(stamp.Path) + "-" + stamp.Size.ToString(CultureInfo.InvariantCulture)
            + "-" + stamp.ModifiedUtcTicks.ToString(CultureInfo.InvariantCulture) + Extension);
    }

    public static string? Find(string folder, string source)
    {
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(source)) return null;
        if (SourceStamp.Of(source) is not { } stamp) return null;
        var path = PathFor(folder, stamp);
        try
        {
            return File.Exists(path) ? path : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public static int Delete(string folder, string source) => DeleteWhere(folder, source, _ => true);

    public static int DeleteStale(string folder, string source, string keep)
        => DeleteWhere(folder, source, file => !PathEquality.Same(file, keep));

    private static int DeleteWhere(string folder, string source, Func<string, bool> pick)
    {
        var removed = 0;
        try
        {
            if (!Directory.Exists(folder)) return 0;
            foreach (var file in Directory.EnumerateFiles(folder, KeyOf(source) + "-*"))
            {
                if (!pick(file)) continue;
                try
                {
                    File.Delete(file);
                    removed++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }

        return removed;
    }

    public static IReadOnlyList<string> Arguments(string source, string output, int width, int height)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentException.ThrowIfNullOrEmpty(output);
        var (w, h) = Size(width, height);
        var threads = Threads.ToString(CultureInfo.InvariantCulture);
        return new[]
        {
            "-hide_banner", "-nostdin", "-y",
            "-threads", threads, "-filter_threads", threads,
            "-i", source,
            "-map", "0:v:0", "-map", "0:a?",
            "-vf", "scale=" + w.ToString(CultureInfo.InvariantCulture) + ":" + h.ToString(CultureInfo.InvariantCulture),
            "-c:v", "libx264", "-preset", "veryfast", "-tune", "fastdecode", "-crf", "26",
            "-g", Gop.ToString(CultureInfo.InvariantCulture), "-keyint_min", Gop.ToString(CultureInfo.InvariantCulture),
            "-sc_threshold", "0", "-bf", "0", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "128k",
            "-sn", "-dn", "-map_metadata", "-1",
            "-threads", threads,
            "-movflags", "+faststart",
            output
        };
    }

    public static IReadOnlyList<string> PreviewPaths(string folder, IReadOnlyList<string> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return sources.Select(source => Find(folder, source) ?? source).ToArray();
    }
}
