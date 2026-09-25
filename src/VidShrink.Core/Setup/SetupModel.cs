namespace VidShrink.Core.Setup;

public sealed class SetupException : Exception
{
    public SetupException(string message) : base(message) { }

    public SetupException(string message, Exception inner) : base(message, inner) { }
}

public sealed record PinnedEntry(string EntryPath, string FileName, string Sha256);

public sealed record LibMpvPin(
    IReadOnlyList<string> Urls, string ArchiveSha256, string FileName, string DllSha256,
    string? ZipUrl = null, string? ZipSha256 = null, VulkanLoaderPin? Vulkan = null)
{
    public static LibMpvPin X64 { get; } = new(
        new[]
        {
            "https://github.com/Teknesyum/VidShrink/releases/download/deps-libmpv-20260903/mpv-dev-x86_64-20260903-git-69e63f425a.7z",
            "https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260903/mpv-dev-x86_64-20260903-git-69e63f425a.7z"
        },
        "fac135c68a35b7639e39d72c0c365104edbaebdea39a0dfdd8c36e8c8e80faef",
        "libmpv-2.dll",
        "673e6397920ab64a9c5b3a618f7f16d38854efe72b58665f1f84e4e873b763a4",
        "https://github.com/Teknesyum/VidShrink/releases/download/deps-libmpv-20260903/libmpv-2-x86_64-20260903.zip",
        "1fc71846bd6e63d280e4f52d212f6f80695339e98632698930cf4ffe9f4bdbad",
        VulkanLoaderPin.X64);

    public static LibMpvPin Arm64 { get; } = new(
        new[]
        {
            "https://github.com/Teknesyum/VidShrink/releases/download/deps-libmpv-20260903/mpv-dev-aarch64-20260903-git-69e63f425a.7z",
            "https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260903/mpv-dev-aarch64-20260903-git-69e63f425a.7z"
        },
        "9d4e0cf7370fd1dd9a91a9d8139f24a88ece9e58b00f5a9ca50b391d03114f2f",
        "libmpv-2.dll",
        "3bfc5a042cc6ebe45ace74992dbc135ee84e3e1b33afac070f8902a2d64a22e9",
        "https://github.com/Teknesyum/VidShrink/releases/download/deps-libmpv-20260903/libmpv-2-aarch64-20260903.zip",
        "a0dfcf27fa8468e4c52170779b27cec3a6bf2be57eac2e021ec6d39d36228156",
        VulkanLoaderPin.Arm64);

    public static LibMpvPin Default => X64;

    public static LibMpvPin For(string architecture) =>
        string.Equals(architecture, "arm64", StringComparison.OrdinalIgnoreCase) ? Arm64 : X64;
}

/// <summary>
/// Khronos Vulkan Loader (LunarG VulkanRT 1.4.357.0 bileşenleri, Apache-2.0/MIT), libmpv'nin
/// yanına <c>tools\libmpv\vulkan-1.dll</c> olarak konur. libmpv <c>vulkan-1.dll</c>'yi doğrudan
/// içe aktarıyor ve <c>vkGetPhysicalDeviceProperties2</c> ister; eski sürücülerin System32'ye
/// bıraktığı 1.0 yükleyicisinde bu işlev yok ve libmpv hiç yüklenmiyor. Kitaplık tam yoluyla
/// yüklendiği için yanındaki yükleyici System32'dekinden önce gelir. Zip, DLL'yi ve lisans
/// metnini taşır; ikisi de <c>deps-libmpv-20260903</c> sürümünde.
/// </summary>
public sealed record VulkanLoaderPin(string Url, string ZipSha256, string DllSha256)
{
    public const string FileName = "vulkan-1.dll";
    public const string LicenseFileName = "VulkanRT-License.txt";

    public static VulkanLoaderPin X64 { get; } = new(
        "https://github.com/Teknesyum/VidShrink/releases/download/deps-libmpv-20260903/vulkan-1-x86_64-1.4.357.0.zip",
        "38a05198c4bb467bf81dc17731a5a9f7f79147e80fdae638a18da23f4d9a8ae9",
        "cd862090370454630b31b174e3d4eb474fda38ea034998d1fe1767b0c99a8696");

    public static VulkanLoaderPin Arm64 { get; } = new(
        "https://github.com/Teknesyum/VidShrink/releases/download/deps-libmpv-20260903/vulkan-1-aarch64-1.4.357.0.zip",
        "2495aea7ef927005cb34609a74a379537f9a154beb9f308b2fefd13c5b72c38b",
        "cc5dd0bec8a7afef013c61ddd511d31500a3b3a45c229150a9ae73ead708ab76");
}

/// <summary>
/// ffmpeg arm64'te başka bir kaynaktan geliyor: GyanD yalnız x86_64 derliyor. BtbN'in
/// winarm64 GPL derlemesi kullanılıyor ama kayan <c>latest</c> etiketinden değil —
/// o etiket her gün üstüne yazılıyor ve sabitleme anlamını yitiriyor. Ay sonu autobuild
/// etiketleri kalıcı: 2024-10-31'den bugüne duruyorlar, gün içi etiketler ise budanıyor.
/// </summary>
public sealed record FfmpegPin(string Url, IReadOnlyList<PinnedEntry> Entries)
{
    public static FfmpegPin X64 { get; } = new(
        "https://github.com/GyanD/codexffmpeg/releases/download/9.0/ffmpeg-9.0-full_build.zip",
        new[]
        {
            new PinnedEntry("ffmpeg-9.0-full_build/bin/ffmpeg.exe", "ffmpeg.exe",
                "05f4251bce9293c2ab492cb17ca7724a0ffd0d06c881ba2ee83b82a89c2fc740"),
            new PinnedEntry("ffmpeg-9.0-full_build/bin/ffprobe.exe", "ffprobe.exe",
                "51e0780cd881f83749b029ed716cbb841c2eac6289f418050f2f2961b158896b")
        });

    public static FfmpegPin Arm64 { get; } = new(
        "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-08-31-13-27/ffmpeg-n9.0.1-11-ge47273f4d9-winarm64-gpl-9.0.zip",
        new[]
        {
            new PinnedEntry("ffmpeg-n9.0.1-11-ge47273f4d9-winarm64-gpl-9.0/bin/ffmpeg.exe", "ffmpeg.exe",
                "a169b9d26b2380be66211022525c9ac16affc923ba05b561024e57c5ed4281f9"),
            new PinnedEntry("ffmpeg-n9.0.1-11-ge47273f4d9-winarm64-gpl-9.0/bin/ffprobe.exe", "ffprobe.exe",
                "6b0b738a2df0186811f240a7036cd1279c0a08991981adecceefe6a2c2b2236c")
        });

    public static FfmpegPin Default => X64;

    public static FfmpegPin For(string architecture) =>
        string.Equals(architecture, "arm64", StringComparison.OrdinalIgnoreCase) ? Arm64 : X64;
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

    public LibMpvPin? LibMpv { get; init; }

    public FfmpegPin? Ffmpeg { get; init; }

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

    /// <summary>
    /// Kurulumun vardığı yer: yüzde, bir sonraki adıma kadar sürünülebilecek tavan ve adım
    /// cümlesi. Panel yalnız bunu dinler; konsol yok sayar. Çağrılar tekdüze artar.
    /// </summary>
    public Action<int, int, string> Step { get; init; } = (_, _, _) => { };

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
