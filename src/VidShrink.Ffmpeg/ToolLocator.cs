using System.Diagnostics;

namespace VidShrink.Ffmpeg;

public static class ToolLocator
{
    internal static readonly string[] MacToolDirectories = { "/opt/homebrew/bin", "/usr/local/bin", "/opt/local/bin" };

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

    public static string GetFfmpegVersion()
    {
        using var process = new Process { StartInfo = StartInfo(Ffmpeg, new[] { "-version" }) };
        process.Start();
        var line = process.StandardOutput.ReadLine();
        process.WaitForExit(3000);
        return line?.Replace("ffmpeg version ", "", StringComparison.OrdinalIgnoreCase) ?? "unknown";
    }

    internal static string Locate(string name, string? searchPath = null)
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

        var onPath = FindOnPath(exe, searchPath ?? Environment.GetEnvironmentVariable("PATH") ?? "");
        if (onPath is not null) return onPath;

        if (OperatingSystem.IsMacOS())
            foreach (var directory in MacToolDirectories)
            {
                var full = Path.Combine(directory, exe);
                if (File.Exists(full)) return full;
            }

        throw new FileNotFoundException($"{name} was not found. Place it in {Path.Combine("tools", "ffmpeg")} next to the executable, or install it on PATH.", exe);
    }

    private static string? FindOnPath(string exe, string path)
    {
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
