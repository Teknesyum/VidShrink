using System.Diagnostics;

namespace VidShrink.Ffmpeg;

public static class ToolLocator
{
    internal static readonly string[] MacToolDirectories = { "/opt/homebrew/bin", "/usr/local/bin", "/opt/local/bin" };

    private static readonly object ManualGate = new();
    private static string? _manual;
    private static string? _ffmpeg;
    private static string? _ffprobe;
    private static string? _version;

    public static string Ffmpeg => _ffmpeg ??= Locate("ffmpeg", manualFfmpeg: _manual);
    public static string Ffprobe => _ffprobe ??= Locate("ffprobe", manualFfmpeg: _manual);

    /// <summary>Yürürlükteki elle verilmiş ffmpeg; yoksa null ve otomatik sıra geçerli.</summary>
    public static string? Manual => _manual;

    /// <summary>
    /// Elle ffmpeg geçerli mi: dosya var ve yanında aynı klasörde ffprobe duruyor.
    /// Yoklama ve süre okuması ffprobe'la yapıldığından tek başına ffmpeg yetmez.
    /// </summary>
    public static bool IsValidManual(string? ffmpegPath)
        => !string.IsNullOrWhiteSpace(ffmpegPath)
           && File.Exists(ffmpegPath)
           && File.Exists(SiblingFfprobe(ffmpegPath));

    /// <summary>
    /// Elle yolu yürürlüğe koyar. Geçersizse otomatik sıraya döner ve false verir; çağıran
    /// kullanıcıya bunu söyler. Yol değişince önbellekler boşalır, sonraki çağrı yeni ffmpeg'i görür.
    /// </summary>
    public static bool UseManual(string? ffmpegPath)
    {
        var valid = IsValidManual(ffmpegPath);
        Apply(valid ? Path.GetFullPath(ffmpegPath!) : null);
        return valid;
    }

    public static void UseAutomatic() => Apply(null);

    private static void Apply(string? manual)
    {
        lock (ManualGate)
        {
            if (string.Equals(_manual, manual, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                return;
            _manual = manual;
            _ffmpeg = null;
            _ffprobe = null;
            _version = null;
        }
        EncoderCapabilities.Forget();
        CaptureDevices.Invalidate();
    }

    internal static string SiblingFfprobe(string ffmpegPath)
        => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ffmpegPath)) ?? "", OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

    public static bool IsAvailable(out string? missing)
    {
        try { _ = Ffmpeg; } catch { missing = "ffmpeg"; return false; }
        try { _ = Ffprobe; } catch { missing = "ffprobe"; return false; }
        missing = null;
        return true;
    }

    /// <summary>
    /// Surum satiri surec acarak okunuyor; tani gunlugu her kosumda yaziyor, o yuzden
    /// bir kez okunup tutulur. Elle ffmpeg yolu degisince <see cref="Apply"/> onbellegi bosaltir.
    /// </summary>
    public static string FfmpegVersion => _version ??= GetFfmpegVersion();

    public static string GetFfmpegVersion()
    {
        using var process = new Process { StartInfo = StartInfo(Ffmpeg, new[] { "-version" }) };
        process.Start();
        var line = process.StandardOutput.ReadLine();
        process.WaitForExit(3000);
        return line?.Replace("ffmpeg version ", "", StringComparison.OrdinalIgnoreCase) ?? "unknown";
    }

    internal static string Locate(string name, string? searchPath = null, string? baseDirectory = null, string? manualFfmpeg = null)
    {
        if (IsValidManual(manualFfmpeg))
            return name == "ffprobe" ? SiblingFfprobe(manualFfmpeg!) : Path.GetFullPath(manualFfmpeg!);

        var exe = OperatingSystem.IsWindows() ? name + ".exe" : name;
        var baseDir = baseDirectory ?? AppContext.BaseDirectory;

        var candidates = new List<string>
        {
            Path.Combine(baseDir, "tools", "ffmpeg", exe),
            Path.Combine(baseDir, exe)
        };

        var trimmed = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(Path.GetFileName(trimmed), "app", StringComparison.OrdinalIgnoreCase) &&
            Path.GetDirectoryName(trimmed) is { Length: > 0 } installRoot)
            candidates.Add(Path.Combine(installRoot, "tools", "ffmpeg", exe));

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
