using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using VidShrink.Core;
using VidShrink.Core.Setup;

namespace VidShrink.App;

internal static class EskiAdGocu
{
    internal static void Calistir()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH"))) return;
            var appDirectory = AppContext.BaseDirectory;
            if (PortableMode.IsActive(appDirectory)) return;
            if (!File.Exists(Path.Combine(appDirectory, UpdateCheck.VersionMarkerName))) return;

            var copied = LegacyMigration.MigrateData(LegacyMigration.DataFolders(Environment.GetFolderPath), null, DateTimeOffset.UtcNow);

            if (copied && OperatingSystem.IsWindows()) EskiKurulumuKaldir(appDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            UpdateHealth.RecordFailure(null, DateTimeOffset.UtcNow, LegacyMigration.DataCopyFailed + "=" + exception.GetType().Name);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void EskiKurulumuKaldir(string appDirectory)
    {
        using (var current = Registry.CurrentUser.OpenSubKey(SetupOptions.DefaultUninstallKey))
        {
            if (current is null) return;
        }

        bool legacyKey;
        string? location;
        using (var legacy = Registry.CurrentUser.OpenSubKey(SetupOptions.LegacyUninstallKey))
        {
            legacyKey = legacy is not null;
            location = legacy?.GetValue("InstallLocation") as string;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var install = LegacyMigration.FindLegacyInstall(legacyKey, location, localAppData, Directory.Exists);
        if (install is null || LegacyMigration.RunsFrom(install, appDirectory)) return;

        var host = new LegacyCleanupHost
        {
            OldProcessesRunning = EskiSurecVar,
            AcquireUpdateLock = KilidiAl,
            Run = KaldiriciyiCalistir
        };
        _ = Task.Run(() => LegacyMigration.RemoveLegacyInstallAsync(install, host, null, () => DateTimeOffset.UtcNow));
    }

    private static bool EskiSurecVar()
    {
        foreach (var name in new[] { AppIdentity.LegacyAppProcessName, AppIdentity.LegacyLauncherProcessName })
        {
            var processes = Process.GetProcessesByName(name);
            var found = processes.Length > 0;
            foreach (var process in processes) process.Dispose();
            if (found) return true;
        }
        return false;
    }

    private static IDisposable? KilidiAl()
    {
        var mutex = new Mutex(false, UpdateStaging.MutexName);
        try
        {
            if (mutex.WaitOne(TimeSpan.FromSeconds(30))) return new Kilit(mutex);
        }
        catch (AbandonedMutexException)
        {
            return new Kilit(mutex);
        }
        mutex.Dispose();
        return null;
    }

    private static int? KaldiriciyiCalistir(string executable, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start);
        if (process is null) return null;
        if (!process.WaitForExit(timeout)) return null;
        return process.ExitCode;
    }

    private sealed class Kilit(Mutex mutex) : IDisposable
    {
        public void Dispose()
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
