using System.IO.Compression;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;
using VidShrink.Core;
using VidShrink.Core.Setup;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Sessiz kurulum ve uygulama kaydı: Ayarlar'daki kaldırma kaydı, PATH sarmalı ve ayar klasörünün silinmesi.
/// Kayıtlar yalnız test anahtarının altına (<c>Uninstall</c>, <c>Environment</c>) yazılır; gerçek HKCU ve gerçek PATH okunmaz.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SessizKurulumTests : IDisposable
{
    private const string Rid = "win-x64";

    private readonly string _work;
    private readonly string _testKey;

    public SessizKurulumTests()
    {
        var id = Guid.NewGuid().ToString("n");
        _work = Path.Combine(TestPaths.OutputRoot, "sessiz-kurulum", id);
        _testKey = $@"Software\VidShrink-Test-{Environment.ProcessId}-{id}";
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows()) Registry.CurrentUser.DeleteSubKeyTree(_testKey, false);
        try { Directory.Delete(_work, true); } catch (IOException) { }
    }

    [Fact]
    public async Task KurulumAyarlarKaydiniYaziyorKaldirmaSiliyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (options, host) = Setup(FakeRelease());
        var root = options.InstallRoot!;
        var copy = UninstallEntry.SetupCopy(options.LocalAppData);

        await SetupRunner.InstallAsync(options, host, CancellationToken.None);

        Assert.StartsWith(_testKey + @"\", options.UninstallKey, StringComparison.Ordinal);
        using (var key = Registry.CurrentUser.OpenSubKey(options.UninstallKey))
        {
            Assert.NotNull(key);
            Assert.Equal(VidShrink.Core.AppIdentity.Name, key!.GetValue("DisplayName"));
            Assert.Equal("1.2.3", key.GetValue("DisplayVersion"));
            Assert.Equal("Teknesyum", key.GetValue("Publisher"));
            Assert.Equal(Path.Combine(root, AppIdentity.LauncherExecutable) + ",0", key.GetValue("DisplayIcon"));
            Assert.Equal(root, key.GetValue("InstallLocation"));
            var command = $"\"{copy}\" --uninstall --install-root \"{root}\"";
            Assert.Equal(command, key.GetValue("UninstallString"));
            Assert.Equal(command + " --silent", key.GetValue("QuietUninstallString"));
            Assert.Equal(1, key.GetValue("NoModify"));
            Assert.Equal(RegistryValueKind.DWord, key.GetValueKind("NoModify"));
            Assert.Equal(1, key.GetValue("NoRepair"));
        }
        Assert.Equal("kurucu", File.ReadAllText(copy));

        await SetupRunner.UninstallAsync(options, host, CancellationToken.None);

        using (var gone = Registry.CurrentUser.OpenSubKey(options.UninstallKey)) Assert.Null(gone);
        Assert.False(File.Exists(copy));
    }

    [Fact]
    public async Task KayitKapaliykenAnahtarYazilmiyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (options, host) = Setup(FakeRelease());
        options = options with { RegisterUninstall = false };

        await SetupRunner.InstallAsync(options, host, CancellationToken.None);

        using (var key = Registry.CurrentUser.OpenSubKey(options.UninstallKey)) Assert.Null(key);
        Assert.False(File.Exists(UninstallEntry.SetupCopy(options.LocalAppData)));
    }

    [Fact]
    public async Task KurucuYoluBilinmiyorsaKayitYazilmiyorKurulumSuruyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var lines = new List<string>();
        var (options, host) = Setup(FakeRelease(), lines.Add, setupExecutable: false);

        await SetupRunner.InstallAsync(options, host, CancellationToken.None);

        using (var key = Registry.CurrentUser.OpenSubKey(options.UninstallKey)) Assert.Null(key);
        Assert.Contains(SetupText.Get("setup.uninstall-entry.no-setup"), lines);
        Assert.True(File.Exists(Path.Combine(options.InstallRoot!, AppIdentity.LauncherExecutable)));
    }

    [Fact]
    public void ProvaKayitPathVeAyarSilmeyiKapatiyor()
    {
        var options = new SetupOptions
        {
            InstallRoot = Path.Combine(_work, "kok"),
            LocalAppData = Path.Combine(_work, "local"),
            WorkRoot = Path.Combine(_work, "tmp"),
            RegisterUninstall = true,
            AddToPath = true,
            RemoveSettings = true
        };
        var rehearsal = SetupLaunch.Rehearsal(options, Path.Combine(_work, "prova"));
        Assert.False(rehearsal.RegisterUninstall);
        Assert.False(rehearsal.AddToPath);
        Assert.False(rehearsal.RemoveSettings);
        var defaults = options with { RegisterUninstall = true, AddToPath = false, RemoveSettings = false };
        Assert.Equal(defaults, new SetupOptions { InstallRoot = options.InstallRoot, LocalAppData = options.LocalAppData, WorkRoot = options.WorkRoot });
    }

    [Fact]
    public void VarsayilanKokteTestKonagiKayitYazamiyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var options = new SetupOptions
        {
            InstallRoot = Path.Combine(_work, "kok"),
            LocalAppData = Path.Combine(_work, "local"),
            WorkRoot = Path.Combine(_work, "tmp")
        };
        var host = new SetupHost { SetupExecutable = Environment.ProcessPath };
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\VidShrinkPlayer", options.UninstallKey);
        Assert.Equal("Environment", options.EnvironmentKey);
        Assert.Throws<SetupException>(() => UninstallEntry.Write(options, host, Path.Combine(_work, "kok"), "1.2.3"));
        Assert.Throws<SetupException>(() => UninstallEntry.Remove(options));
    }

    [Fact]
    public async Task AyarSilmeSecilinceAyarKlasoruGidiyorSecilmezseKaliyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (options, host) = Setup(FakeRelease());
        var settings = Path.Combine(_work, "roaming", AppIdentity.FolderName);
        Directory.CreateDirectory(settings);
        File.WriteAllText(Path.Combine(settings, "settings.json"), "{}");
        options = options with { SettingsFolder = settings };

        await SetupRunner.InstallAsync(options, host, CancellationToken.None);
        await SetupRunner.UninstallAsync(options, host, CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(settings, "settings.json")));

        await SetupRunner.InstallAsync(options, host, CancellationToken.None);
        await SetupRunner.UninstallAsync(options with { RemoveSettings = true }, host, CancellationToken.None);
        Assert.False(Directory.Exists(settings));
    }

    [Fact]
    public async Task PathSecilinceBinVeSarmalYaziliyorKaldirmaCikariyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var changes = 0;
        var (options, host) = Setup(FakeRelease(), environmentChanged: () => changes++);
        options = options with { AddToPath = true };
        using (var env = Registry.CurrentUser.CreateSubKey(options.EnvironmentKey))
            env.SetValue("Path", @"%USERPROFILE%\araclar;C:\eski", RegistryValueKind.ExpandString);
        var bin = Path.Combine(options.InstallRoot!, "bin");

        await SetupRunner.InstallAsync(options, host, CancellationToken.None);

        Assert.StartsWith(_testKey + @"\", options.EnvironmentKey, StringComparison.Ordinal);
        Assert.Equal(@"%USERPROFILE%\araclar;C:\eski;" + bin, RawPath(options.EnvironmentKey));
        using (var env = Registry.CurrentUser.OpenSubKey(options.EnvironmentKey))
            Assert.Equal(RegistryValueKind.ExpandString, env!.GetValueKind("Path"));
        Assert.Equal(CommandShim.Line + "\r\n", File.ReadAllText(Path.Combine(bin, "vidshrink.cmd")));
        Assert.Equal(0, changes);

        await SetupRunner.InstallAsync(options, host, CancellationToken.None);
        Assert.Equal(@"%USERPROFILE%\araclar;C:\eski;" + bin, RawPath(options.EnvironmentKey));

        await SetupRunner.UninstallAsync(options with { AddToPath = false }, host, CancellationToken.None);
        Assert.Equal(@"%USERPROFILE%\araclar;C:\eski", RawPath(options.EnvironmentKey));
        Assert.False(Directory.Exists(bin));
    }

    [Fact]
    public async Task PathVarsayilanKapaliyBinVePathYok()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (options, host) = Setup(FakeRelease());

        await SetupRunner.InstallAsync(options, host, CancellationToken.None);

        Assert.Null(RawPath(options.EnvironmentKey));
        Assert.False(Directory.Exists(Path.Combine(options.InstallRoot!, "bin")));
    }

    [Fact]
    public async Task PathYokkenYeniDegerGenisletilebilirYaziliyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (options, host) = Setup(FakeRelease());
        options = options with { AddToPath = true };
        var bin = Path.Combine(options.InstallRoot!, "bin");

        await SetupRunner.InstallAsync(options, host, CancellationToken.None);
        Assert.Equal(bin, RawPath(options.EnvironmentKey));
        using (var env = Registry.CurrentUser.OpenSubKey(options.EnvironmentKey))
            Assert.Equal(RegistryValueKind.ExpandString, env!.GetValueKind("Path"));

        await SetupRunner.UninstallAsync(options, host, CancellationToken.None);
        Assert.Null(RawPath(options.EnvironmentKey));
    }

    [Fact]
    public void DuzStringPathTurunuKoruyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var envKey = $@"{_testKey}\Environment";
        using (var env = Registry.CurrentUser.CreateSubKey(envKey))
            env.SetValue("Path", @"C:\a", RegistryValueKind.String);

        Assert.True(UserPathRegistry.Add(envKey, @"C:\b"));
        Assert.False(UserPathRegistry.Add(envKey, @"c:\B\"));
        using (var env = Registry.CurrentUser.OpenSubKey(envKey))
        {
            Assert.Equal(RegistryValueKind.String, env!.GetValueKind("Path"));
            Assert.Equal(@"C:\a;C:\b", env.GetValue("Path"));
        }
        Assert.True(UserPathRegistry.Remove(envKey, @"C:\b"));
        Assert.False(UserPathRegistry.Remove(envKey, @"C:\b"));
        Assert.Equal(@"C:\a", RawPath(envKey));
    }

    [Theory]
    [InlineData(null, @"C:\v\bin", @"C:\v\bin")]
    [InlineData("", @"C:\v\bin", @"C:\v\bin")]
    [InlineData(@"C:\a", @"C:\v\bin", @"C:\a;C:\v\bin")]
    [InlineData(@"C:\a;", @"C:\v\bin", @"C:\a;C:\v\bin")]
    [InlineData(@"C:\a;;C:\b", @"C:\v\bin", @"C:\a;;C:\b;C:\v\bin")]
    [InlineData(@"C:\a;""C:\V\BIN\""", @"C:\v\bin", @"C:\a;""C:\V\BIN\""")]
    [InlineData(@"C:\v\bin\;C:\a", @"C:\v\bin", @"C:\v\bin\;C:\a")]
    public void UserPathEklemesiTekrarlamiyor(string? value, string directory, string expected) =>
        Assert.Equal(expected, UserPath.Add(value, directory));

    [Theory]
    [InlineData(@"C:\a;C:\v\bin", @"C:\v\bin", @"C:\a")]
    [InlineData(@"C:\a;;""c:\V\Bin\"";C:\b", @"C:\v\bin", @"C:\a;C:\b")]
    [InlineData(@"C:\a;C:\b", @"C:\v\bin", @"C:\a;C:\b")]
    [InlineData(@"C:\v\bin", @"C:\v\bin", "")]
    [InlineData(null, @"C:\v\bin", "")]
    [InlineData(@"C:\v\binx;C:\v", @"C:\v\bin", @"C:\v\binx;C:\v")]
    public void UserPathCikarmasiYalnizAyniKlasoruSiliyor(string? value, string directory, string expected) =>
        Assert.Equal(expected, UserPath.Remove(value, directory));

    [Fact]
    public void SarmalYalnizKuruluCliyiCagiriyor()
    {
        Assert.Equal("@\"%~dp0..\\app\\vidshrink.exe\" %*", CommandShim.Line);
        var path = CommandShim.Write(Path.Combine(_work, "kok"));
        Assert.Equal(Path.Combine(_work, "kok", "bin", "vidshrink.cmd"), path);
        var bytes = File.ReadAllBytes(path);
        Assert.NotEqual(0xEF, bytes[0]);
    }

    [Theory]
    [InlineData(new[] { "--silent" }, false)]
    [InlineData(new[] { "--SILENT", "--panel" }, false)]
    [InlineData(new[] { "--panel", "--add-to-path" }, true)]
    [InlineData(new string[0], true)]
    public void SessizBayrakPaneliAcmiyor(string[] args, bool panel) =>
        Assert.Equal(panel, SetupLaunch.UsePanel(args));

    [Fact]
    public void SessizBayrakHatadaBeklemiyor()
    {
        Assert.True(SetupLaunch.IsSilent(new[] { "--uninstall", "--Silent" }));
        Assert.False(SetupLaunch.IsSilent(new[] { "--uninstall", "--silently" }));
        Assert.True(SetupLaunch.PauseOnError(true, true, false));
        Assert.False(SetupLaunch.PauseOnError(true, true, false, silent: true));
    }

    [Theory]
    [InlineData("tr")]
    [InlineData("en")]
    public void YardimYeniSecenekleriAnlatiyor(string language)
    {
        var help = SetupText.GetIn(language, "setup.help");
        Assert.Contains("--silent", help);
        Assert.Contains("--add-to-path", help);
        Assert.Contains("--remove-settings", help);
        Assert.Contains(@"\bin", help);
    }

    private static string? RawPath(string environmentKey) => UserPathRegistry.Read(environmentKey);

    private (SetupOptions Options, SetupHost Host) Setup(
        string release, Action<string>? gunluk = null, bool setupExecutable = true, Action? environmentChanged = null)
    {
        var local = Path.Combine(_work, "local");
        var ffmpegZip = Path.Combine(release, "ffmpeg.zip");
        var libMpvZip = Path.Combine(release, "mpv-dev.zip");
        var vulkanZip = Path.Combine(release, "vulkan-1.zip");
        var setupExe = Path.Combine(_work, "indirilen", AppIdentity.SetupExecutable);
        Directory.CreateDirectory(Path.GetDirectoryName(setupExe)!);
        File.WriteAllText(setupExe, "kurucu");
        var options = new SetupOptions
        {
            InstallRoot = Path.Combine(local, "Programs", AppIdentity.FolderName),
            LocalAppData = local,
            WorkRoot = Path.Combine(_work, "tmp"),
            ClassesRoot = $@"{_testKey}\Classes",
            NoLaunch = true,
            MenuLanguage = "tr",
            ShortcutDirectory = Path.Combine(_work, "kisayol"),
            AssetSource = release,
            Tag = "v1.2.3",
            ForceFfmpegDownload = true,
            LibMpv = new LibMpvPin(new[] { libMpvZip }, UpdateCheck.HashFile(libMpvZip), "libmpv-2.dll", Sha("libmpv ikilisi"),
                Vulkan: new VulkanLoaderPin(vulkanZip, UpdateCheck.HashFile(vulkanZip), Sha("vulkan ikilisi"))),
            Ffmpeg = new FfmpegPin(ffmpegZip, new[]
            {
                new PinnedEntry("ffmpeg-9.0-full_build/bin/ffmpeg.exe", "ffmpeg.exe", Sha("ffmpeg ikilisi")),
                new PinnedEntry("ffmpeg-9.0-full_build/bin/ffprobe.exe", "ffprobe.exe", Sha("ffprobe ikilisi"))
            })
        };
        Directory.CreateDirectory(options.WorkRoot);
        var host = new SetupHost
        {
            Architecture = () => new ArchitectureDecision(ArchitectureOutcome.Read, "x64", ""),
            Shortcuts = new FakeShortcuts(),
            Delay = (_, _) => Task.CompletedTask,
            Log = gunluk ?? (_ => { }),
            Step = (_, _, _) => { },
            AssociationChanged = () => { },
            EnvironmentChanged = environmentChanged ?? (() => throw new InvalidOperationException("Test kökünde ortam yayını yapılmaz.")),
            SetupExecutable = setupExecutable ? setupExe : null,
            Launch = _ => throw new InvalidOperationException("NoLaunch verildi.")
        };
        return (options, host);
    }

    private string FakeRelease()
    {
        var release = Path.Combine(_work, "yayin-" + Guid.NewGuid().ToString("n")[..6]);
        Directory.CreateDirectory(release);

        var app = Path.Combine(release, UpdateCheck.ArchiveAssetName(Rid));
        Zip(app, (AppIdentity.AppExecutable, "uygulama"), ("alt/veri.txt", "veri"));
        var launcher = Path.Combine(release, UpdateCheck.LauncherArchiveAssetName(Rid));
        Zip(launcher, (AppIdentity.LauncherExecutable, "baslatici"));
        File.WriteAllText(Path.Combine(release, $"checksums-{Rid}.txt"),
            $"{UpdateCheck.HashFile(app)}  {Path.GetFileName(app)}\n{UpdateCheck.HashFile(launcher)}  {Path.GetFileName(launcher)}\n");

        Zip(Path.Combine(release, "ffmpeg.zip"),
            ("ffmpeg-9.0-full_build/bin/ffmpeg.exe", "ffmpeg ikilisi"),
            ("ffmpeg-9.0-full_build/bin/ffprobe.exe", "ffprobe ikilisi"));
        Zip(Path.Combine(release, "mpv-dev.zip"), ("libmpv-2.dll", "libmpv ikilisi"));
        Zip(Path.Combine(release, "vulkan-1.zip"), (VulkanLoaderPin.FileName, "vulkan ikilisi"), (VulkanLoaderPin.LicenseFileName, "vulkan lisansi"));
        return release;
    }

    private static void Zip(string path, params (string Name, string Text)[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, text) in entries)
        {
            using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
            stream.Write(Encoding.UTF8.GetBytes(text));
        }
    }

    private static string Sha(string text) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private sealed class FakeShortcuts : IShortcutWriter
    {
        public void Write(string shortcutPath, string target, string workingDirectory, string icon) => File.WriteAllText(shortcutPath, target);

        public string? ReadTarget(string shortcutPath) => File.ReadAllText(shortcutPath);

        public void SetIcon(string shortcutPath, string icon) { }
    }
}
