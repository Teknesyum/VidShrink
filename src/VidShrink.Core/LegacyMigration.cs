using System.ComponentModel;
using VidShrink.Core.Setup;

namespace VidShrink.Core;

public sealed record LegacyDataFolder(string Old, string New);

public sealed record LegacyInstall(string Root, string Uninstaller, string? Registered = null);

public sealed class LegacyLeftoverHost
{
    public required string LocalAppData { get; init; }

    public required string AppDirectory { get; init; }

    public required string MovedMarker { get; init; }

    public required Func<bool> LegacyKeyExists { get; init; }

    public required Func<bool> OldProcessesRunning { get; init; }

    public Func<string, bool> FileExists { get; init; } = File.Exists;

    public Func<string, bool> DirectoryExists { get; init; } = Directory.Exists;

    public Func<string, IEnumerable<string>> Files { get; init; } = Directory.EnumerateFiles;

    public Func<string, IEnumerable<string>> Directories { get; init; } = Directory.EnumerateDirectories;

    public Func<string, bool> IsLink { get; init; } = path => File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);

    public Action<string> DeleteFile { get; init; } = path =>
    {
        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
    };

    public Action<string> DeleteDirectory { get; init; } = path => Directory.Delete(path, recursive: false);

    public IReadOnlyList<string> Shortcuts { get; init; } = Array.Empty<string>();

    public Func<string, string?> ReadTarget { get; init; } = _ => null;
}

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

    public const string LeftoverProcessRunning = "goc-artik-eski-surec-acik";

    public const string LeftoverLocked = "goc-artik-kilitli";

    public const string CleanupCrashed = "goc-temizlik-coktu";

    public static string MovedMarker(string roamingAppData) =>
        Path.Combine(roamingAppData, AppIdentity.LegacyFolderName, MovedMarkerName);

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
        return new LegacyInstall(registered ?? fallback, LegacyUninstaller(localAppData), registered);
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

    public static async Task<bool> CleanUpLegacyAsync(
        LegacyInstall install, LegacyCleanupHost host, LegacyLeftoverHost leftover, string? healthSettingsPath, Func<DateTimeOffset> now, CancellationToken cancellationToken = default)
    {
        var uninstalled = true;
        if (leftover.LegacyKeyExists() || host.FileExists(install.Uninstaller))
            uninstalled = await RemoveLegacyInstallAsync(install, host, healthSettingsPath, now, cancellationToken).ConfigureAwait(false);
        var cleared = RemoveLeftover(install, leftover, healthSettingsPath, now());
        return uninstalled && cleared;
    }

    public static IReadOnlyList<string> IdentityFiles(string root) => new[]
    {
        Path.Combine(root, AppIdentity.LegacyLauncherExecutable),
        Path.Combine(root, "app", AppIdentity.LegacyAppExecutable)
    };

    public static string? LeftoverRoot(LegacyInstall install, LegacyLeftoverHost host)
    {
        var root = Normalize(install.Root);
        var known = SamePath(root, LegacyDefaultRoot(host.LocalAppData))
            || (!string.IsNullOrWhiteSpace(install.Registered) && SamePath(root, install.Registered));
        if (!known) return null;
        if (!SetupRunner.UnderPrograms(root, host.LocalAppData)) return null;
        if (RunsFrom(install, host.AppDirectory)) return null;
        var app = Normalize(host.AppDirectory);
        if (Within(root, app) || (Path.GetDirectoryName(app) is { } own && Within(root, own))) return null;
        if (host.LegacyKeyExists()) return null;
        if (!host.FileExists(host.MovedMarker)) return null;
        if (!IdentityFiles(root).Any(host.FileExists)) return null;
        return root;
    }

    public static bool RemoveLeftover(LegacyInstall install, LegacyLeftoverHost host, string? healthSettingsPath, DateTimeOffset now)
    {
        if (!host.DirectoryExists(install.Root)) return true;
        if (LeftoverRoot(install, host) is not { } root) return true;
        if (host.OldProcessesRunning()) return Fail(LeftoverProcessRunning);

        var failed = RemoveShortcuts(root, host);
        var keep = new HashSet<string>(IdentityFiles(root).Where(host.FileExists), StringComparer.OrdinalIgnoreCase);
        failed += Sweep(root, keep, host);
        if (failed == 0)
        {
            foreach (var file in keep) failed += Attempt(() => host.DeleteFile(file));
            if (failed == 0) failed += Sweep(root, new HashSet<string>(StringComparer.OrdinalIgnoreCase), host);
        }
        return failed == 0 || Fail(LeftoverLocked + "=" + failed.ToString(System.Globalization.CultureInfo.InvariantCulture));

        bool Fail(string reason)
        {
            UpdateHealth.RecordFailure(healthSettingsPath, now, reason);
            return false;
        }
    }

    public static IReadOnlyList<string> LegacyShortcuts(string desktop, string programs) => new[]
    {
        Path.Combine(desktop, AppIdentity.LegacyShortcutName),
        Path.Combine(programs, AppIdentity.LegacyFolderName, AppIdentity.LegacyShortcutName)
    };

    private static int RemoveShortcuts(string root, LegacyLeftoverHost host)
    {
        var failed = 0;
        foreach (var shortcut in host.Shortcuts)
        {
            if (!host.FileExists(shortcut)) continue;
            string? target;
            try { target = host.ReadTarget(shortcut); }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failed++;
                continue;
            }
            if (string.IsNullOrWhiteSpace(target) || !Within(target, root)) continue;
            var removed = Attempt(() => host.DeleteFile(shortcut));
            failed += removed;
            if (removed != 0) continue;
            var folder = Path.GetDirectoryName(shortcut);
            if (folder is null || !string.Equals(Path.GetFileName(folder), AppIdentity.LegacyFolderName, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                if (!host.Files(folder).Any() && !host.Directories(folder).Any()) failed += Attempt(() => host.DeleteDirectory(folder));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
        }
        return failed;
    }

    private static int Sweep(string directory, HashSet<string> keep, LegacyLeftoverHost host)
    {
        string[] directories;
        string[] files;
        try
        {
            directories = host.Directories(directory).ToArray();
            files = host.Files(directory).ToArray();
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException)
        {
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 1;
        }

        var failed = 0;
        foreach (var child in directories)
        {
            bool link;
            try { link = host.IsLink(child); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failed++;
                continue;
            }
            failed += link ? Attempt(() => host.DeleteDirectory(child)) : Sweep(child, keep, host);
        }
        foreach (var file in files)
        {
            if (keep.Contains(file)) continue;
            failed += Attempt(() => host.DeleteFile(file));
        }
        if (failed == 0 && !keep.Any(file => Within(file, directory))) failed += Attempt(() => host.DeleteDirectory(directory));
        return failed;
    }

    private static int Attempt(Action action)
    {
        try
        {
            action();
            return 0;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 1;
        }
    }

    private static bool Within(string path, string folder)
    {
        var inner = Normalize(path);
        var outer = Normalize(folder);
        return string.Equals(inner, outer, StringComparison.OrdinalIgnoreCase)
            || inner.StartsWith(outer + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
