using System.ComponentModel;

namespace VidShrink.Core;

public sealed record LegacyDataFolder(string Old, string New);

public sealed record LegacyInstall(string Root, string Uninstaller);

public sealed class LegacyCleanupHost
{
    public required Func<bool> OldProcessesRunning { get; init; }

    public required Func<IDisposable?> AcquireUpdateLock { get; init; }

    public required Func<string, IReadOnlyList<string>, TimeSpan, int?> Run { get; init; }

    public Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;

    public Func<string, bool> FileExists { get; init; } = File.Exists;

    public TimeSpan ProcessWait { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan RunTimeout { get; init; } = TimeSpan.FromMinutes(2);
}

public static class LegacyMigration
{
    public const string MovedMarkerName = "moved-to.txt";

    public const string DataCopyFailed = "goc-veri-dustu";

    public const string UninstallerMissing = "goc-eski-kaldirici-yok";

    public const string ProcessesStillRunning = "goc-eski-surec-kapanmadi";

    public const string UpdateLockBusy = "goc-kilit-alinamadi";

    public const string UninstallFailed = "goc-kaldirma-dustu";

    public static IReadOnlyList<LegacyDataFolder> DataFolders(Func<Environment.SpecialFolder, string> folderPath)
    {
        var folders = new List<LegacyDataFolder>();
        foreach (var special in new[] { Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData })
        {
            var root = folderPath(special);
            if (string.IsNullOrWhiteSpace(root)) continue;
            var pair = new LegacyDataFolder(Path.Combine(root, AppIdentity.LegacyFolderName), Path.Combine(root, AppIdentity.FolderName));
            if (!folders.Contains(pair)) folders.Add(pair);
        }
        return folders;
    }

    public static bool Excluded(string fileName) =>
        string.Equals(fileName, UpdateHealth.FileName, StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith("." + UpdateHealth.FileName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, AppliedUpdateNotice.MarkerFileName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, MovedMarkerName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, AppIdentity.LegacySetupExecutable, StringComparison.OrdinalIgnoreCase);

    public static bool MigrateData(IEnumerable<LegacyDataFolder> folders, string? healthSettingsPath, DateTimeOffset now) =>
        folders.Select(folder => CopyData(folder, healthSettingsPath, now)).ToArray().All(copied => copied);

    public static bool CopyData(LegacyDataFolder folder, string? healthSettingsPath, DateTimeOffset now)
    {
        try
        {
            if (!Directory.Exists(folder.Old)) return true;
            if (File.Exists(Path.Combine(folder.Old, MovedMarkerName))) return true;
            if (SamePath(folder.Old, folder.New)) return true;

            CopyTree(folder.Old, folder.New);
            File.WriteAllText(Path.Combine(folder.Old, MovedMarkerName), folder.New);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            UpdateHealth.RecordFailure(healthSettingsPath, now, DataCopyFailed + "=" + exception.GetType().Name);
            return false;
        }
    }

    private static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            var name = Path.GetFileName(file);
            if (Excluded(name)) continue;
            var destination = Path.Combine(target, name);
            if (File.Exists(destination)) continue;
            File.Copy(file, destination, overwrite: false);
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            if (new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            CopyTree(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    public static string LegacyUninstaller(string localAppData) =>
        Path.Combine(localAppData, AppIdentity.LegacyFolderName, AppIdentity.LegacySetupExecutable);

    public static string LegacyDefaultRoot(string localAppData) =>
        Path.Combine(localAppData, "Programs", AppIdentity.LegacyFolderName);

    public static LegacyInstall? FindLegacyInstall(bool legacyKeyExists, string? registeredLocation, string localAppData, Func<string, bool> directoryExists)
    {
        var fallback = LegacyDefaultRoot(localAppData);
        var registered = string.IsNullOrWhiteSpace(registeredLocation) ? null : registeredLocation.Trim().Trim('"');
        if (!legacyKeyExists && !directoryExists(fallback)) return null;
        return new LegacyInstall(registered ?? fallback, LegacyUninstaller(localAppData));
    }

    public static bool RunsFrom(LegacyInstall install, string appDirectory)
    {
        var root = Normalize(install.Root);
        var app = Normalize(appDirectory);
        return string.Equals(root, app, StringComparison.OrdinalIgnoreCase)
            || app.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> UninstallArguments(LegacyInstall install) =>
        new[] { "--uninstall", "--install-root", install.Root };

    public static async Task<bool> RemoveLegacyInstallAsync(
        LegacyInstall install, LegacyCleanupHost host, string? healthSettingsPath, Func<DateTimeOffset> now, CancellationToken cancellationToken = default)
    {
        if (!host.FileExists(install.Uninstaller)) return Fail(UninstallerMissing);

        var waited = TimeSpan.Zero;
        while (host.OldProcessesRunning())
        {
            if (waited >= host.ProcessWait) return Fail(ProcessesStillRunning);
            await host.Delay(host.PollInterval, cancellationToken).ConfigureAwait(false);
            waited += host.PollInterval;
        }

        using var gate = host.AcquireUpdateLock();
        if (gate is null) return Fail(UpdateLockBusy);

        int? exitCode;
        try
        {
            exitCode = host.Run(install.Uninstaller, UninstallArguments(install), host.RunTimeout);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or Win32Exception or UnauthorizedAccessException)
        {
            exitCode = null;
        }

        return exitCode == 0 || Fail(UninstallFailed + "=" + (exitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "yok"));

        bool Fail(string reason)
        {
            UpdateHealth.RecordFailure(healthSettingsPath, now(), reason);
            return false;
        }
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
