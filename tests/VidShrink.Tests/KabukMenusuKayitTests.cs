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

                using (var shrink = Registry.CurrentUser.OpenSubKey(Branch(root, ShellMenu.ShrinkMenuKey) + @"\shell\06-size-100\command"))
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

    [Fact]
    [SupportedOSPlatform("windows")]
    public void EtiketYenilemesiYalnizMuiVerbYaziyor()
    {
        if (!OperatingSystem.IsWindows()) return;

        WithTestRoot(root =>
        {
            const string target = @"C:\sahte\VidShrinkPlayer.exe";
            var extensions = ShellMenu.ExtensionsFor(ShellMenu.MenuKey);
            Assert.Equal(extensions.Count, ShellMenu.InstallOpen(target, "Ac"));

            foreach (var extension in extensions)
            {
                using var command = Registry.CurrentUser.OpenSubKey(
                    root + @"\Software\Classes\SystemFileAssociations\." + extension + @"\shell\" + ShellMenu.MenuKey + @"\command",
                    writable: true);
                command!.SetValue("Isaret", extension, RegistryValueKind.String);
            }

            Assert.Equal(extensions.Count + 1, ShellMenu.Relabel(ShellMenu.MenuKey, "Open"));

            foreach (var extension in extensions)
            {
                var path = root + @"\Software\Classes\SystemFileAssociations\." + extension + @"\shell\" + ShellMenu.MenuKey;
                using var verb = Registry.CurrentUser.OpenSubKey(path);
                using var command = Registry.CurrentUser.OpenSubKey(path + @"\command");
                Assert.Equal("Open", verb?.GetValue("MUIVerb"));
                Assert.Equal(target, verb?.GetValue("Icon"));
                Assert.Equal(extension, command?.GetValue("Isaret"));
                Assert.Equal($"\"{target}\" \"%1\"", command?.GetValue(string.Empty));
            }

            using (var labels = Registry.CurrentUser.OpenSubKey(root + @"\" + ShellMenu.LabelKey))
                Assert.Equal("Open", labels?.GetValue("open"));

            Assert.Equal(0, ShellMenu.Relabel(ShellMenu.MenuKey, "Open"));
        });
    }

    /// <summary>
    /// Açma kutusu iki fiil yazar: "aç" ve "sıraya ekle". İkisi videoya, sese ve çalma listesine
    /// gider, çoklu seçimde düşmesinler diye <c>Player</c> modelini taşır; küçültme alt menüsü
    /// sese ve listeye yazılmaz. Kaldırma iki fiili birlikte siler.
    /// </summary>
    [Fact]
    [SupportedOSPlatform("windows")]
    public void SirayaEkleFiiliSesVeListeyeYaziliyorKucultmeYazilmiyor()
    {
        if (!OperatingSystem.IsWindows()) return;

        WithTestRoot(root =>
        {
            const string target = @"C:\sahte\VidShrinkPlayer.exe";
            Assert.Equal(ShellMenu.ExtensionsFor(ShellMenu.MenuKey).Count, ShellMenu.InstallOpen(target, "Ac"));
            ShellMenu.InstallShrink(target, "Kucult");

            string Verb(string extension, string menu)
                => root + @"\Software\Classes\SystemFileAssociations\." + extension + @"\shell\" + menu;

            foreach (var extension in new[] { "mp4", "mp3", "flac", "m3u", "m3u8" })
            {
                using (var open = Registry.CurrentUser.OpenSubKey(Verb(extension, ShellMenu.MenuKey)))
                using (var openCommand = Registry.CurrentUser.OpenSubKey(Verb(extension, ShellMenu.MenuKey) + @"\command"))
                {
                    Assert.Equal("Ac", open?.GetValue("MUIVerb"));
                    Assert.Equal("Player", open?.GetValue("MultiSelectModel"));
                    Assert.Equal($"\"{target}\" \"%1\"", openCommand?.GetValue(string.Empty));
                }

                using (var enqueue = Registry.CurrentUser.OpenSubKey(Verb(extension, ShellMenu.EnqueueMenuKey)))
                using (var enqueueCommand = Registry.CurrentUser.OpenSubKey(Verb(extension, ShellMenu.EnqueueMenuKey) + @"\command"))
                {
                    Assert.Equal(ShellMenu.EnqueueLabel(), enqueue?.GetValue("MUIVerb"));
                    Assert.Equal(target, enqueue?.GetValue("Icon"));
                    Assert.Equal("Player", enqueue?.GetValue("MultiSelectModel"));
                    Assert.Equal($"\"{target}\" \"%1\" {ShellMenu.EnqueueFlag}", enqueueCommand?.GetValue(string.Empty));
                }
            }

            using (var videoShrink = Registry.CurrentUser.OpenSubKey(Verb("mp4", ShellMenu.ShrinkMenuKey)))
                Assert.NotNull(videoShrink);
            foreach (var extension in new[] { "mp3", "flac", "m3u", "m3u8" })
            {
                using var shrink = Registry.CurrentUser.OpenSubKey(Verb(extension, ShellMenu.ShrinkMenuKey));
                Assert.Null(shrink);
            }

            Assert.Equal(ShellMenu.ExtensionsFor(ShellMenu.MenuKey).Count, ShellMenu.RemoveOpen());
            foreach (var extension in new[] { "mp4", "mp3", "m3u" })
            {
                using var open = Registry.CurrentUser.OpenSubKey(Verb(extension, ShellMenu.MenuKey));
                using var enqueue = Registry.CurrentUser.OpenSubKey(Verb(extension, ShellMenu.EnqueueMenuKey));
                Assert.Null(open);
                Assert.Null(enqueue);
            }

            using (var stillShrink = Registry.CurrentUser.OpenSubKey(Verb("mp4", ShellMenu.ShrinkMenuKey)))
                Assert.NotNull(stillShrink);
        });
    }
}
