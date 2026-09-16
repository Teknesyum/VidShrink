namespace VidShrink.Core.Setup;

public sealed class SetupException : Exception
{
    public SetupException(string message) : base(message) { }

    public SetupException(string message, Exception inner) : base(message, inner) { }
}

public sealed record PinnedEntry(string EntryPath, string FileName, string Sha256);

public sealed record LibMpvPin(IReadOnlyList<string> Urls, string ArchiveSha256, string FileName, string DllSha256)
{
    public static LibMpvPin Default { get; } = new(
        new[]
        {
            "https://github.com/Teknesyum/VidShrink/releases/download/deps-libmpv-20260903/mpv-dev-x86_64-20260903-git-69e63f425a.7z",
            "https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260903/mpv-dev-x86_64-20260903-git-69e63f425a.7z"
        },
        "fac135c68a35b7639e39d72c0c365104edbaebdea39a0dfdd8c36e8c8e80faef",
        "libmpv-2.dll",
        "673e6397920ab64a9c5b3a618f7f16d38854efe72b58665f1f84e4e873b763a4");
}

public sealed record FfmpegPin(string Url, IReadOnlyList<PinnedEntry> Entries)
{
    public static FfmpegPin Default { get; } = new(
        "https://github.com/GyanD/codexffmpeg/releases/download/9.0/ffmpeg-9.0-full_build.zip",
        new[]
        {
            new PinnedEntry("ffmpeg-9.0-full_build/bin/ffmpeg.exe", "ffmpeg.exe",
                "05f4251bce9293c2ab492cb17ca7724a0ffd0d06c881ba2ee83b82a89c2fc740"),
            new PinnedEntry("ffmpeg-9.0-full_build/bin/ffprobe.exe", "ffprobe.exe",
                "51e0780cd881f83749b029ed716cbb841c2eac6289f418050f2f2961b158896b")
        });
}

public sealed record SetupOptions
{
    public const string DefaultClassesRoot = @"Software\Classes";

    public const string Repository = "Teknesyum/VidShrink";

    public required string InstallRoot { get; init; }

    public required string LocalAppData { get; init; }

    public required string WorkRoot { get; init; }

    public string ClassesRoot { get; init; } = DefaultClassesRoot;

    public bool SkipShortcuts { get; init; }

    public bool NoLaunch { get; init; }

    public string MenuLanguage { get; init; } = "auto";

    public string? ShortcutDirectory { get; init; }

    public string? AssetSource { get; init; }

    public string? Tag { get; init; }

    public bool ForceFfmpegDownload { get; init; }

    public LibMpvPin LibMpv { get; init; } = LibMpvPin.Default;

    public FfmpegPin Ffmpeg { get; init; } = FfmpegPin.Default;

    public bool DefaultRegistry => IsDefaultClassesRoot(ClassesRoot);

    public static bool IsDefaultClassesRoot(string root) =>
        string.Equals(root.TrimEnd('\\'), DefaultClassesRoot, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeRegistryRoot(string root)
    {
        var trimmed = root.Trim().TrimEnd('\\');
        foreach (var prefix in new[] { @"HKCU:\", @"HKEY_CURRENT_USER\", @"HKCU\" })
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return trimmed[prefix.Length..];
        }
        throw new SetupException($"Kayit koku HKCU:\\ altinda olmali: {root}");
    }
}

public interface IRootHolder
{
    string Name { get; }

    int Id { get; }

    void Kill();

    bool WaitForExit(TimeSpan timeout);
}

public interface IShortcutWriter
{
    void Write(string shortcutPath, string target, string workingDirectory, string icon);

    string? ReadTarget(string shortcutPath);
}

public interface IShellPackage
{
    bool Registered();

    void Remove();

    bool Register(string manifestPath, string externalLocation);
}

public sealed class SetupHost
{
    public Action<string> Log { get; init; } = _ => { };

    public Func<string, IReadOnlyList<IRootHolder>> FindHolders { get; init; } = _ => Array.Empty<IRootHolder>();

    public Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;

    public Func<string, string?> FindTool { get; init; } = _ => null;

    public IShortcutWriter? Shortcuts { get; init; }

    public IShellPackage? ShellPackage { get; init; }

    public bool Windows11 { get; init; }

    public Func<string> UiLanguage { get; init; } = () => "en";

    public Action<string> Launch { get; init; } = _ => { };

    public Action AssociationChanged { get; init; } = () => { };

    public string DesktopDirectory { get; init; } = "";

    public string ProgramsDirectory { get; init; } = "";

    public Func<ArchitectureDecision> Architecture { get; init; } = ArchitectureChoice.Decide;

    public TimeSpan HolderWait { get; init; } = LockedFolder.HolderWait;
}
