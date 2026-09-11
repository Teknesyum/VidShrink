using System.Reflection;
using System.Runtime.InteropServices;

namespace VidShrink.Player;

public static class LibMpvLocator
{
    public const string EnvironmentVariable = "VIDSHRINK_LIBMPV";

    private static readonly object Gate = new();
    private static IntPtr _handle;
    private static string? _loadedFrom;

    public static string? LoadedFrom
    {
        get { lock (Gate) return _loadedFrom; }
    }

    public static IReadOnlyList<string> FileNames
    {
        get
        {
            if (OperatingSystem.IsWindows()) return new[] { "libmpv-2.dll", "mpv-2.dll" };
            if (OperatingSystem.IsMacOS()) return new[] { "libmpv.2.dylib", "libmpv.dylib" };
            return new[] { "libmpv.so.2", "libmpv.so" };
        }
    }

    public static readonly string[] MacLibraryDirectories = { "/opt/homebrew/lib", "/usr/local/lib", "/opt/local/lib" };

    public static IReadOnlyList<string> AppDirectories(string baseDirectory)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory));
        var list = new List<string> { full, Path.Combine(full, "tools", "libmpv") };
        var parent = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(parent)) list.Add(Path.Combine(parent, "tools", "libmpv"));
        return list;
    }

    public static IReadOnlyList<string> Candidates(string? environmentValue, string baseDirectory, bool? macOs = null)
    {
        var list = new List<string>();
        var hasEnvironment = !string.IsNullOrWhiteSpace(environmentValue);
        if (hasEnvironment)
        {
            var value = environmentValue!.Trim().Trim('"');
            if (Directory.Exists(value)) list.AddRange(FileNames.Select(name => Path.Combine(value, name)));
            else list.Add(value);
        }

        foreach (var directory in AppDirectories(baseDirectory))
            list.AddRange(FileNames.Select(name => Path.Combine(directory, name)));

        if (!hasEnvironment && (macOs ?? OperatingSystem.IsMacOS()))
            foreach (var directory in MacLibraryDirectories)
                list.AddRange(FileNames.Select(name => Path.Combine(directory, name)));

        return list;
    }

    public static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_handle != IntPtr.Zero) return;

            var environmentValue = Environment.GetEnvironmentVariable(EnvironmentVariable);
            var tried = new List<string>();
            foreach (var candidate in Candidates(environmentValue, AppContext.BaseDirectory))
            {
                tried.Add(candidate);
                if (!File.Exists(candidate)) continue;
                if (!NativeLibrary.TryLoad(candidate, out var handle)) continue;
                Bind(handle, candidate);
                return;
            }

            if (string.IsNullOrWhiteSpace(environmentValue))
            {
                foreach (var name in FileNames)
                {
                    tried.Add(name + " (system search path)");
                    if (!NativeLibrary.TryLoad(name, out var handle)) continue;
                    Bind(handle, name);
                    return;
                }
            }

            throw new PlaybackEngineUnavailableException(
                $"libmpv was not found. Set {EnvironmentVariable} to the libmpv file or its folder. Tried: {string.Join("; ", tried)}");
        }
    }

    private static void Bind(IntPtr handle, string path)
    {
        _handle = handle;
        _loadedFrom = path;
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), (name, _, _) =>
            name == Native.Lib ? _handle : IntPtr.Zero);
    }
}
