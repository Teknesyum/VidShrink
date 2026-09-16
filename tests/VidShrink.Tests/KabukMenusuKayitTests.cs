using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;
using VidShrink.App;
using VidShrink.App.Integration;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Sağ tık menüsünün kayıt defterine yazdığı değerler, yalnız <see cref="ShellMenu.TestRoot"/>
/// altında okunur. Gerçek HKCU'ya dokunulmadığı her testin sonunda ayrıca sınanır.
/// </summary>
public class KabukMenusuKayitTests
{
    private const string RealCommand = @"Software\Classes\SystemFileAssociations\.mp4\shell\" + ShellMenu.MenuKey + @"\command";

    private static string Branch(string root, string menu)
        => root + @"\Software\Classes\SystemFileAssociations\.mp4\shell\" + menu;

    [SupportedOSPlatform("windows")]
    private static string? RealValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RealCommand);
        return key?.GetValue(string.Empty) as string;
    }

    [SupportedOSPlatform("windows")]
    private static void WithTestRoot(Action<string> body)
    {
        var root = @"Software\Teknesyum\VidShrinkTest\KabukMenusu-" + Guid.NewGuid().ToString("N");
        var before = RealValue();
        ShellMenu.TestRoot = root;
        try
        {
            body(root);
        }
        finally
        {
            ShellMenu.TestRoot = null;
            Registry.CurrentUser.DeleteSubKeyTree(root, throwOnMissingSubKey: false);
        }
        Assert.Equal(before, RealValue());
    }

    private static (string Launcher, string App, string Folder) FakeInstall()
    {
        var folder = Path.Combine(TipSources.Root, ".calisma", "kabuk-menusu", Guid.NewGuid().ToString("N"));
        var app = Path.Combine(folder, "app", RegistryWriteGate.AppExecutable);
        Directory.CreateDirectory(Path.GetDirectoryName(app)!);
        File.WriteAllBytes(app, Array.Empty<byte>());
        var launcher = Path.Combine(folder, FileAssociation.LauncherName);
        File.WriteAllBytes(launcher, Array.Empty<byte>());
        return (launcher, app, folder);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void KutuKomutaBaslaticiyiYaziyor()
    {
        if (!OperatingSystem.IsWindows()) return;

        var install = FakeInstall();
        try
        {
            WithTestRoot(root =>
            {
                MainWindow.InstallShellMenu(ShellMenu.InstallOpen, install.App, "Ac");
                MainWindow.InstallShellMenu(ShellMenu.InstallShrink, install.App, "Kucult");

                using (var command = Registry.CurrentUser.OpenSubKey(Branch(root, ShellMenu.MenuKey) + @"\command"))
                    Assert.Equal($"\"{install.Launcher}\" \"%1\"", command?.GetValue(string.Empty));

                using (var shrink = Registry.CurrentUser.OpenSubKey(Branch(root, ShellMenu.ShrinkMenuKey) + @"\shell\100\command"))
                {
                    var value = shrink?.GetValue(string.Empty) as string;
                    Assert.StartsWith($"\"{install.Launcher}\" {ShellMenu.ShrinkFlag} 100", value);
                    Assert.DoesNotContain(RegistryWriteGate.AppExecutable, value);
                }
            });
        }
        finally
        {
            Directory.Delete(install.Folder, recursive: true);
        }
    }
}
