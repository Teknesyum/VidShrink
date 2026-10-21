using System.Text;
using VidShrink.Core;
using VidShrink.Core.Setup;
using Xunit;

namespace VidShrink.Tests;

public sealed partial class SessizKurulumTests
{
    [Fact]
    public async Task KokKaldiriciKurulupKaldirmaSonundaKoktenHicbirSeyKalmiyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (options, host) = Setup(FakeRelease());
        var root = options.InstallRoot!;
        var startMenu = Path.Combine(options.ShortcutDirectory!, "Programs", AppIdentity.FolderName);

        await SetupRunner.InstallAsync(options, host, CancellationToken.None);

        var uninstaller = RootUninstaller.PathIn(root);
        Assert.Equal(Path.Combine(root, "Uninstall VidShrink Player.exe"), uninstaller);
        Assert.Equal("kurucu", File.ReadAllText(uninstaller));
        var link = SetupRunner.UninstallShortcut(startMenu);
        Assert.Equal(Path.Combine(startMenu, "VidShrink Player'ı kaldır.lnk"), link);
        Assert.Equal(uninstaller, File.ReadAllText(link));
        Assert.True(File.Exists(Path.Combine(startMenu, AppIdentity.ShortcutName)));

        await SetupRunner.UninstallAsync(options, host, CancellationToken.None);

        Assert.False(Directory.Exists(root));
        Assert.False(File.Exists(link));
        Assert.False(Directory.Exists(startMenu));
    }

    [Fact]
    public async Task KaldirmaBaskaHedefliKisayoluBirakiyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (options, host) = Setup(FakeRelease());
        var startMenu = Path.Combine(options.ShortcutDirectory!, "Programs", AppIdentity.FolderName);

        await SetupRunner.InstallAsync(options, host, CancellationToken.None);
        var foreign = Path.Combine(startMenu, "Baska.lnk");
        File.WriteAllText(foreign, Path.Combine(_work, "baska", "baska.exe"));
        await SetupRunner.UninstallAsync(options, host, CancellationToken.None);

        Assert.True(File.Exists(foreign));
        Assert.False(File.Exists(SetupRunner.UninstallShortcut(startMenu)));
        Assert.False(File.Exists(Path.Combine(startMenu, AppIdentity.ShortcutName)));
    }

    [Fact]
    public async Task ArsivdekiKaldiriciKurucuKopyasiylaEzilmiyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (options, host) = Setup(FakeRelease(uninstallerInArchive: true));

        await SetupRunner.InstallAsync(options, host, CancellationToken.None);

        Assert.Equal("arsivdeki kaldirici", File.ReadAllText(RootUninstaller.PathIn(options.InstallRoot!)));
    }

    [Fact]
    public async Task KurucuYokVeArsivTasimiyorsaKaldiriciVeKisayolYok()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (options, host) = Setup(FakeRelease(), setupExecutable: false);
        var startMenu = Path.Combine(options.ShortcutDirectory!, "Programs", AppIdentity.FolderName);

        await SetupRunner.InstallAsync(options, host, CancellationToken.None);

        Assert.False(File.Exists(RootUninstaller.PathIn(options.InstallRoot!)));
        Assert.False(File.Exists(SetupRunner.UninstallShortcut(startMenu)));
        Assert.True(File.Exists(Path.Combine(startMenu, AppIdentity.ShortcutName)));
    }

    [Theory]
    [InlineData(@"C:\Kok\Uninstall VidShrink Player.exe", true)]
    [InlineData(@"C:\Kok\UNINSTALL VIDSHRINK PLAYER.EXE", true)]
    [InlineData(@"C:\Kok\VidShrinkPlayer-Setup.exe", false)]
    [InlineData(@"C:\Kok\Uninstall VidShrink Player.exe.old", false)]
    [InlineData(null, false)]
    public void KokKaldiriciKendiAdindanTaniniyor(string? path, bool expected)
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Equal(expected, RootUninstaller.IsRootUninstaller(path));
    }

    [Fact]
    public void KokKaldiriciKurucuKopyasinaYoksaGeciciKopyayaAktariyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var local = Path.Combine(_work, "local");
        var temp = Path.Combine(_work, "gecici");
        var self = Path.Combine(_work, "kok", RootUninstaller.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(self)!);
        File.WriteAllText(self, "kok kaldirici");

        var fromTemp = RootUninstaller.RelayTarget(local, temp, self);
        Assert.Equal(ShellRegistration.SetupExecutableName, Path.GetFileName(fromTemp));
        Assert.StartsWith(Path.Combine(temp, RootUninstaller.TempFolderPrefix), fromTemp, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("kok kaldirici", File.ReadAllText(fromTemp));
        Assert.True(ShellRegistration.WriteAllowed(fromTemp, @"Software\Classes"));
        Assert.False(ShellRegistration.WriteAllowed(self, @"Software\Classes"));

        var copy = UninstallEntry.SetupCopy(local);
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.WriteAllText(copy, "kurucu kopyasi");
        Assert.Equal(copy, RootUninstaller.RelayTarget(local, temp, self));
        Assert.Single(Directory.GetDirectories(temp));

        Assert.Equal(@"--uninstall --install-root ""C:\Kok""", RootUninstaller.CommandLine(@"C:\Kok\"));
    }

    [Fact]
    public void GuncellemeEksikKaldiriciyiKokeKoyuyor()
    {
        var root = Path.Combine(_work, "guncel-kok");
        var stage = Path.Combine(_work, "sahne");
        Directory.CreateDirectory(root);
        var bytes = Encoding.UTF8.GetBytes("yeni kaldirici");
        var file = new ManifestFile(RootUninstaller.FileName, UpdateCheck.HashBytes(bytes), bytes.Length);
        var staged = ShellUpdate.StagePath(stage, file.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        File.WriteAllBytes(staged, bytes);
        var manifest = new ReleaseManifest("1.2.3", "", DateTimeOffset.MinValue, Rid, Array.Empty<ManifestFile>()) { Shell = new[] { file } };

        Assert.False(ShellUpdate.Installed(root, manifest));
        Assert.Single(ShellUpdate.Apply(stage, root, new[] { file }));
        Assert.Equal("yeni kaldirici", File.ReadAllText(RootUninstaller.PathIn(root)));
        Assert.True(ShellUpdate.Installed(root, manifest));
    }

    [Fact]
    public void YayinKaldiriciyiBaslaticiArsivineVeShellDizisineKoyuyor()
    {
        var workflow = File.ReadAllText(Path.Combine(TipSources.Root, ".github", "workflows", "release.yml")).ReplaceLineEndings("\n");
        Assert.Contains($"cp publish-setup/{AppIdentity.SetupExecutable} \"publish-launcher/{RootUninstaller.FileName}\"", workflow, StringComparison.Ordinal);
        Assert.Contains($"or .path == \"{RootUninstaller.FileName}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("if: startsWith(matrix.rid, 'win-')\n        run: |\n          dotnet publish src/VidShrink.Setup/", workflow, StringComparison.Ordinal);
        Assert.Contains($"if [ \"${{{{ matrix.rid }}}}\" = win-x64 ] && [ -f publish-setup/{AppIdentity.SetupExecutable} ]", workflow, StringComparison.Ordinal);
    }
}
