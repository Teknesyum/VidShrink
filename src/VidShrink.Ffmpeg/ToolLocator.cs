using System.Diagnostics;

namespace VidShrink.Ffmpeg;

public static class ToolLocator
{
    private static string? _ffmpeg;
    private static string? _ffprobe;

    public static string Ffmpeg => _ffmpeg ??= Locate("ffmpeg");
    public static string Ffprobe => _ffprobe ??= Locate("ffprobe");

    public static bool IsAvailable(out string? missing)
    {
        try { _ = Ffmpeg; } catch { missing = "ffmpeg"; return false; }
        try { _ = Ffprobe; } catch { missing = "ffprobe"; return false; }
        missing = null;
        return true;
    }

    private static string Locate(string name)
    {
        var exe = OperatingSystem.IsWindows() ? name + ".exe" : name;
        var baseDir = AppContext.BaseDirectory;

        var candidates = new[]
        {
            Path.Combine(baseDir, "tools", "ffmpeg", exe),
            Path.Combine(baseDir, exe)
        };

        foreach (var candidate in candidates)
            if (File.Exists(candidate)) return candidate;

        var onPath = FindOnPath(exe);
        if (onPath is not null) return onPath;

        throw new FileNotFoundException($"{name} was not found. Place it in tools\\ffmpeg next to the executable, or install it on PATH.", exe);
    }

    private static string? FindOnPath(string exe)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(dir.Trim('"'), exe);
                if (File.Exists(full)) return full;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    internal static ProcessStartInfo StartInfo(string fileName, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return psi;
    }
}
