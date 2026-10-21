using System.Runtime.Versioning;

namespace VidShrink.Core.Setup;

public static class RootUninstaller
{
    public const string FileName = "Uninstall " + AppIdentity.Name + ".exe";

    public const string TempFolderPrefix = "vidshrink-uninstall-";

    public static string PathIn(string root) => Path.Combine(root, FileName);

    public static bool IsRootUninstaller(string? processPath) =>
        !string.IsNullOrEmpty(processPath)
        && string.Equals(Path.GetFileName(processPath), FileName, StringComparison.OrdinalIgnoreCase);

    public static bool Place(string root, string? setupExecutable)
    {
        var target = PathIn(root);
        if (File.Exists(target)) return false;
        if (string.IsNullOrEmpty(setupExecutable) || !File.Exists(setupExecutable)) return false;
        try
        {
            File.Copy(setupExecutable, target, overwrite: false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    public static string RelayTarget(string localAppData, string tempRoot, string self)
    {
        var copy = UninstallEntry.SetupCopy(localAppData);
        if (File.Exists(copy)) return copy;
        var folder = Path.Combine(tempRoot, TempFolderPrefix + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, ShellRegistration.SetupExecutableName);
        File.Copy(self, target, overwrite: true);
        return target;
    }

    public static string CommandLine(string root) =>
        $"--uninstall --install-root \"{Path.TrimEndingDirectorySeparator(root)}\"";
}
