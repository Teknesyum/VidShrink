using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.Versioning;

namespace VidShrink.Core.Setup;

public sealed record SetupStep(string Name, long Milliseconds);

public sealed record SetupResult(string Version, string InstallRoot, bool LibMpvReused, bool FfmpegDownloaded, bool ModernMenu, IReadOnlyList<SetupStep> Steps);

public static class SetupRunner
{
    public const string AppFolder = "app";

    public const string AppExecutable = "VidShrink.App.exe";

    public const string AsideSuffix = ".eski-";

    /// <summary>Windows tarafında yayımlanan mimariler; <see cref="UpdateCheck.ReleasedRids"/> ile aynı küme.</summary>
    private static readonly string[] WindowsArchitectures = { "x64", "arm64" };

    public static string RuntimeIdentifier(ArchitectureDecision decision, Action<string> log)
    {
        if (!WindowsArchitectures.Contains(decision.Architecture))
        {
            if (decision.Outcome == ArchitectureOutcome.Read)
                throw new SetupException(SetupText.Get("setup.arch.unsupported", decision.Architecture));
            throw new SetupException(SetupText.Get("setup.arch.32bit"));
        }
        if (!string.IsNullOrEmpty(decision.NoteKey)) log(SetupText.Get(decision.NoteKey));
        return UpdateCheck.RidFor("windows", decision.Architecture);
    }

    public static bool UnderPrograms(string root, string localAppData)
    {
        var programs = Path.GetFullPath(Path.Combine(localAppData, "Programs")).TrimEnd(Path.DirectorySeparatorChar);
        return !string.Equals(root, programs, StringComparison.OrdinalIgnoreCase)
            && root.StartsWith(programs + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    [SupportedOSPlatform("windows")]
    public static async Task<SetupResult> InstallAsync(SetupOptions options, SetupHost host, CancellationToken cancellationToken)
    {
        var steps = new List<SetupStep>();
        var clock = Stopwatch.StartNew();
        void Mark(string name)
        {
            steps.Add(new SetupStep(name, clock.ElapsedMilliseconds));
        }

        SetupText.Use(ShellRegistration.ResolveLanguage(options.MenuLanguage, host.UiLanguage));
        host.Log(SetupText.Get("setup.preparing"));
        var decision = host.Architecture();
        var rid = RuntimeIdentifier(decision, host.Log);
        var libMpvPin = options.LibMpv ?? LibMpvPin.For(decision.Architecture);
        var ffmpegPin = options.Ffmpeg ?? FfmpegPin.For(decision.Architecture);
        var root = Path.GetFullPath(options.InstallRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (!UnderPrograms(root, options.LocalAppData))
            throw new SetupException(SetupText.Get("setup.root.outside-programs", root));

        var ffmpeg = options.ForceFfmpegDownload ? null : host.FindTool("ffmpeg");
        var ffprobe = options.ForceFfmpegDownload ? null : host.FindTool("ffprobe");

        var work = Path.Combine(options.WorkRoot, "vidshrink-setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        string? aside = null;
        try
        {
            using var client = SetupDownloads.CreateClient();
            var archiveName = UpdateCheck.ArchiveAssetName(rid);
            var launcherName = UpdateCheck.LauncherArchiveAssetName(rid);
            var checksumsName = $"checksums-{rid}.txt";

            var existingLibMpv = Path.Combine(root, "tools", "libmpv", libMpvPin.FileName);
            var libMpvTask = Task.Run(() => SetupDownloads.PrepareLibMpvAsync(client, libMpvPin, existingLibMpv, work, host.Log, cancellationToken), cancellationToken);
            var vulkanTask = libMpvPin.Vulkan is { } vulkanPin
                ? Task.Run(() => SetupDownloads.PrepareVulkanLoaderAsync(client, vulkanPin, Path.GetDirectoryName(existingLibMpv), work, cancellationToken), cancellationToken)
                : null;
            var ffmpegTask = ffmpeg is null || ffprobe is null
                ? Task.Run(() => SetupDownloads.FetchFfmpegAsync(ffmpegPin, work, cancellationToken), cancellationToken)
                : null;
            if (ffmpegTask is not null) host.Log(SetupText.Get("setup.ffmpeg.downloading"));

            host.Log(SetupText.Get("setup.release.searching"));
            var tag = options.Tag ?? await SetupDownloads.ResolveLatestTagAsync(cancellationToken);
            var version = tag.TrimStart('v');
            host.Log(SetupText.Get("setup.version.installing", version));
            Mark("etiket");

            host.Log(SetupText.Get("setup.package.downloading"));
            var checksumsTask = SetupDownloads.FetchAssetAsync(client, options, tag, checksumsName, work, cancellationToken);
            var archiveTask = SetupDownloads.FetchAssetAsync(client, options, tag, archiveName, work, cancellationToken);
            var launcherTask = SetupDownloads.FetchAssetAsync(client, options, tag, launcherName, work, cancellationToken);
            await Task.WhenAll(checksumsTask, archiveTask, launcherTask);
            Mark("yayin-indirildi");

            var checksums = SetupDownloads.ParseChecksums(await File.ReadAllTextAsync(checksumsTask.Result.Path, cancellationToken));
            SetupDownloads.AssertChecksum(checksums, archiveName, archiveTask.Result.Sha256);
            SetupDownloads.AssertChecksum(checksums, launcherName, launcherTask.Result.Sha256);
            host.Log(SetupText.Get("setup.downloads.verified"));

            var libMpv = await libMpvTask;
            host.Log(SetupText.Get("setup.libmpv.ready",
                SetupText.Get(libMpv.Reused ? "setup.libmpv.reused" : "setup.libmpv.downloaded")));
            var vulkan = vulkanTask is null ? null : await vulkanTask;
            IReadOnlyDictionary<string, string>? fetchedFfmpeg = ffmpegTask is null ? null : await ffmpegTask;
            if (fetchedFfmpeg is not null)
            {
                ffmpeg = fetchedFfmpeg["ffmpeg.exe"];
                ffprobe = fetchedFfmpeg["ffprobe.exe"];
            }
            Mark("araclar-hazir");

            LockedFolder.CloseHolders(root, host);
            if (options.DefaultRegistry && host.ShellPackage is { } package && package.Registered()) package.Remove();

            if (Directory.Exists(root))
            {
                var target = root + AsideSuffix + Guid.NewGuid().ToString("N")[..8];
                await LockedFolder.RunAsync(root, path => Directory.Move(path, target), host, cancellationToken);
                aside = target;
            }
            Mark("eski-kenara");

            try
            {
                Directory.CreateDirectory(root);
                var appDirectory = Path.Combine(root, AppFolder);
                await Task.WhenAll(
                    Task.Run(() => ZipFile.ExtractToDirectory(archiveTask.Result.Path, appDirectory, overwriteFiles: true), cancellationToken),
                    Task.Run(() => ZipFile.ExtractToDirectory(launcherTask.Result.Path, root, overwriteFiles: true), cancellationToken));

                UpdateCheck.WriteVersionMarker(appDirectory, version);
                LauncherUpdate.WriteVersionMarker(root, version);

                var toolsFfmpeg = Path.Combine(root, "tools", "ffmpeg");
                Directory.CreateDirectory(toolsFfmpeg);
                Place(ffmpeg!, Path.Combine(toolsFfmpeg, "ffmpeg.exe"), move: fetchedFfmpeg is not null);
                Place(ffprobe!, Path.Combine(toolsFfmpeg, "ffprobe.exe"), move: fetchedFfmpeg is not null);

                var toolsLibMpv = Path.Combine(root, "tools", "libmpv");
                Directory.CreateDirectory(toolsLibMpv);
                var libMpvSource = libMpv.Reused && aside is not null
                    ? Path.Combine(aside, "tools", "libmpv", libMpvPin.FileName)
                    : libMpv.Path;
                Place(libMpvSource, Path.Combine(toolsLibMpv, libMpvPin.FileName), move: true);
                if (vulkan is not null)
                {
                    var vulkanFrom = vulkan.Reused && aside is not null ? Path.Combine(aside, "tools", "libmpv") : null;
                    Place(vulkanFrom is null ? vulkan.Dll : Path.Combine(vulkanFrom, VulkanLoaderPin.FileName),
                        Path.Combine(toolsLibMpv, VulkanLoaderPin.FileName), move: true);
                    var license = vulkanFrom is null ? vulkan.License : Path.Combine(vulkanFrom, VulkanLoaderPin.LicenseFileName);
                    if (license is not null && File.Exists(license))
                        Place(license, Path.Combine(toolsLibMpv, VulkanLoaderPin.LicenseFileName), move: true);
                }

                if (!File.Exists(Path.Combine(root, LauncherUpdate.ExecutableName))) throw new SetupException(SetupText.Get("setup.launcher.missing"));
                if (!File.Exists(Path.Combine(appDirectory, AppExecutable))) throw new SetupException(SetupText.Get("setup.app.missing"));
            }
            catch
            {
                await RestoreAsync(root, aside, host);
                aside = null;
                throw;
            }
            Mark("dosyalar-yerinde");

            var installedExe = Path.Combine(root, LauncherUpdate.ExecutableName);
            var modern = false;
            if (!options.SkipShortcuts)
            {
                WriteShortcuts(options, host, root, installedExe);
                var locales = ShellRegistration.LocalesFolder(root);
                var language = ShellRegistration.ResolveLanguage(options.MenuLanguage, host.UiLanguage, locales);
                var modernTask = Task.Run(() => RegisterModernMenu(options, host, root), cancellationToken);
                var shrinkWritten = ShellRegistration.WriteMenus(options.ClassesRoot, installedExe, language, locales);
                var associated = ShellRegistration.WriteFileAssociation(options.ClassesRoot, installedExe);
                if (options.DefaultRegistry) host.AssociationChanged();
                modern = await modernTask;
                var path = SetupText.Get(modern ? "setup.menu.modern-and-classic" : "setup.menu.classic");
                host.Log(SetupText.Get("setup.menu.written", ShellIntegration.MediaExtensions.Count, shrinkWritten, path));
                host.Log(SetupText.Get("setup.association.written", associated));
            }
            Mark("kabuk");

            if (aside is not null) TryDelete(aside);
            SweepAsides(root);

            host.Log(SetupText.Get("setup.installed", version, root));
            if (!options.NoLaunch) host.Launch(installedExe);
            Mark("bitti");
            return new SetupResult(version, root, libMpv.Reused, fetchedFfmpeg is not null, modern, steps);
        }
        finally
        {
            TryDelete(work);
        }
    }

    [SupportedOSPlatform("windows")]
    public static async Task UninstallAsync(SetupOptions options, SetupHost host, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(options.InstallRoot).TrimEnd(Path.DirectorySeparatorChar);
        LockedFolder.CloseHolders(root, host);

        var cleared = ShellRegistration.RemoveMenus(options.ClassesRoot);
        var packages = 0;
        if (options.DefaultRegistry && host.ShellPackage is { } package && package.Registered())
        {
            package.Remove();
            packages = 1;
        }
        var unlinked = ShellRegistration.RemoveFileAssociation(options.ClassesRoot);
        if (options.DefaultRegistry) host.AssociationChanged();
        host.Log(SetupText.Get("setup.uninstall.menus-removed", cleared, packages, unlinked));

        if (!options.SkipShortcuts && host.Shortcuts is { } shortcuts)
        {
            var (desktop, startMenu) = ShortcutPaths(options, host);
            RemoveShortcut(shortcuts, Path.Combine(desktop, "VidShrink.lnk"), root);
            if (RemoveShortcut(shortcuts, Path.Combine(startMenu, "VidShrink.lnk"), root) &&
                Directory.Exists(startMenu) && !Directory.EnumerateFileSystemEntries(startMenu).Any())
            {
                Directory.Delete(startMenu);
            }
        }

        if (UnderPrograms(root, options.LocalAppData))
        {
            if (Directory.Exists(root))
                await LockedFolder.RunAsync(root, path => Directory.Delete(path, recursive: true), host, cancellationToken);
            SweepAsides(root);
            host.Log(SetupText.Get("setup.uninstalled", root));
        }
        else
        {
            host.Log(SetupText.Get("setup.uninstall.outside-programs", root));
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool RegisterModernMenu(SetupOptions options, SetupHost host, string root)
    {
        if (!host.Windows11) return false;
        var shell = Path.Combine(root, "shell");
        var template = Path.Combine(shell, "AppxManifest.template.xml");
        if (!File.Exists(template) || !File.Exists(Path.Combine(shell, "VidShrink.ShellExtension.dll")))
        {
            host.Log(SetupText.Get("setup.shell.no-package"));
            return false;
        }
        if (!options.DefaultRegistry || host.ShellPackage is not { } package) return false;

        var manifest = Path.Combine(shell, "AppxManifest.xml");
        File.WriteAllBytes(manifest, ShellRegistration.Utf8NoBom(ShellRegistration.RenderPackageManifest(File.ReadAllText(template))));
        if (package.Registered()) package.Remove();
        if (package.Register(manifest, root)) return true;

        host.Log(SetupText.Get("setup.shell.package-failed"));
        return false;
    }

    private static (string Desktop, string StartMenu) ShortcutPaths(SetupOptions options, SetupHost host) =>
        options.ShortcutDirectory is { } directory
            ? (directory, Path.Combine(directory, "Programs", "VidShrink"))
            : (host.DesktopDirectory, Path.Combine(host.ProgramsDirectory, "VidShrink"));

    private static void WriteShortcuts(SetupOptions options, SetupHost host, string root, string installedExe)
    {
        if (host.Shortcuts is not { } shortcuts) return;
        var (desktop, startMenu) = ShortcutPaths(options, host);
        Directory.CreateDirectory(desktop);
        Directory.CreateDirectory(startMenu);
        shortcuts.Write(Path.Combine(desktop, "VidShrink.lnk"), installedExe, root, installedExe + ",0");
        shortcuts.Write(Path.Combine(startMenu, "VidShrink.lnk"), installedExe, root, installedExe + ",0");
    }

    private static bool RemoveShortcut(IShortcutWriter shortcuts, string path, string root)
    {
        if (!File.Exists(path)) return false;
        var target = shortcuts.ReadTarget(path);
        if (string.IsNullOrEmpty(target) || !target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
        File.Delete(path);
        return true;
    }

    private static void Place(string source, string destination, bool move)
    {
        if (move) File.Move(source, destination, overwrite: true);
        else File.Copy(source, destination, overwrite: true);
    }

    /// <summary>
    /// Yarım kalan kurulumu geri alır: yeni kökü siler, kenara alınan eski kurulumu yerine koyar.
    /// </summary>
    /// <remarks>
    /// Silme tek denemeyle yapılmıyor. Kökü kenara alan ileri yol <see cref="LockedFolder"/>
    /// merdivenini kullanıyordu, geri koyma ise çıplak tek çağrıydı; kök o anda kilitliyse
    /// (tarayıcı yeni açılan dosyaları okuyor) silme düşüyor, taşıma hiç denenmiyor ve
    /// kullanıcının kurulumu <c>VidShrink.eski-*</c> altında sessizce kalıyordu. Kurtaramazsak
    /// hiç değilse susmuyoruz: klasörün yeri günlüğe yazılıyor.
    /// </remarks>
    internal static async Task RestoreAsync(string root, string? aside, SetupHost host)
    {
        try
        {
            await LockedFolder.RunAsync(root, path => Directory.Delete(path, recursive: true), host, CancellationToken.None);
        }
        catch (Exception) { }

        if (aside is null || !Directory.Exists(aside)) return;
        try
        {
            if (!Directory.Exists(root)) Directory.Move(aside, root);
        }
        catch (Exception) { }

        if (Directory.Exists(aside)) host.Log(SetupText.Get("setup.restore.aside-left", aside, root));
    }

    public static void SweepAsides(string root)
    {
        var parent = Path.GetDirectoryName(root);
        if (parent is null || !Directory.Exists(parent)) return;
        foreach (var directory in Directory.EnumerateDirectories(parent, Path.GetFileName(root) + AsideSuffix + "*"))
            TryDelete(directory);
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (Exception) { }
    }
}
