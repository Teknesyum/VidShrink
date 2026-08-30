using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// macOS kurulumunun ürettiği <c>~/Applications/VidShrink.app</c> paketini ölçer.
///
/// Paketi <c>macos-app-bundle.sh</c> kuruyor; ölçüm betiği gerçekten koşturuyor, metnini
/// okumuyor. Betik yalnız macOS'un kendi araçlarıyla (<c>sips</c>, <c>iconutil</c>,
/// <c>codesign</c>) çalıştığı için macOS dışında erken dönülüyor — <c>Skip</c> değil, ki
/// Windows'ta atlanan ölçü sayısı değişmesin.
/// </summary>
public sealed class MacOsBundleTests
{
    private static string Root => TipSources.Root;

    private static string DeclaredVersion()
    {
        var props = File.ReadAllText(Path.Combine(Root, "Directory.Build.props"));
        var match = Regex.Match(props, @"<Version>([^<]+)</Version>");
        Assert.True(match.Success, "Directory.Build.props içinde <Version> yok.");
        return match.Groups[1].Value;
    }

    private static int Shell(string arguments, string? home = null)
    {
        var start = new ProcessStartInfo("/bin/sh", arguments)
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (home is not null) start.Environment["HOME"] = home;

        using var process = Process.Start(start)!;
        process.WaitForExit();
        return process.ExitCode;
    }

    private static string Sandbox()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vidshrink-bundle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// Sürüm iki yerde yazılı olmamalı. Paketin <c>Info.plist</c>'i kurulum betiğinin
    /// yayın etiketinden okuduğu sürümü alıyor, etiketi de release.yml
    /// <c>Directory.Build.props</c>'takine eşitliyor; bu ölçü zincirin paketleyici
    /// ucunu tutuyor — verilen sürüm iki alana da olduğu gibi düşüyor mu.
    /// </summary>
    [Fact]
    public void TheBundleCarriesTheVersionTheTreeDeclares()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var version = DeclaredVersion();
        var sandbox = Sandbox();
        try
        {
            var payload = Path.Combine(sandbox, "payload");
            Directory.CreateDirectory(payload);
            File.WriteAllText(Path.Combine(payload, "VidShrink"), "#!/bin/sh\nexit 0\n");

            var bundle = Path.Combine(sandbox, "VidShrink.app");
            Assert.Equal(0, Shell($"macos-app-bundle.sh \"{payload}\" VidShrink {version} \"{bundle}\""));

            var plist = File.ReadAllText(Path.Combine(bundle, "Contents", "Info.plist"));
            foreach (var key in new[] { "CFBundleShortVersionString", "CFBundleVersion" })
            {
                var match = Regex.Match(plist, $@"<key>{key}</key>\s*<string>([^<]*)</string>");
                Assert.True(match.Success, $"Info.plist içinde {key} yok.");
                Assert.Equal(version, match.Groups[1].Value);
            }
        }
        finally
        {
            Directory.Delete(sandbox, true);
        }
    }

    /// <summary>
    /// Adı <c>.App</c> ile biten bir dosyayı çekirdek paket sanıp öldürüyor
    /// (docs/macos-ilk-kosum.md). Paketleyici eski yayınların başlatıcısını da
    /// <c>VidShrink</c> adına taşımalı, yoksa paket açılmaz.
    /// </summary>
    [Fact]
    public void TheBundleRenamesAnOldReleaseLauncher()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var sandbox = Sandbox();
        try
        {
            var payload = Path.Combine(sandbox, "payload");
            Directory.CreateDirectory(payload);
            File.WriteAllText(Path.Combine(payload, "VidShrink.App"), "#!/bin/sh\nexit 0\n");

            var bundle = Path.Combine(sandbox, "VidShrink.app");
            Assert.Equal(0, Shell($"macos-app-bundle.sh \"{payload}\" VidShrink.App 0.0.0 \"{bundle}\""));

            var macOs = Path.Combine(bundle, "Contents", "MacOS");
            Assert.True(File.Exists(Path.Combine(macOs, "VidShrink")));
            Assert.False(File.Exists(Path.Combine(macOs, "VidShrink.App")));
        }
        finally
        {
            Directory.Delete(sandbox, true);
        }
    }

    /// <summary>
    /// Kaldırma paketi, düz kurulum dizinini ve kısayolu birlikte siler; sonrasında
    /// <c>~/Applications</c> altında iz kalmaz. Ölçüm kendi <c>HOME</c>'unda koşuyor,
    /// makinedeki gerçek kuruluma dokunmuyor.
    /// </summary>
    [Fact]
    public void UninstallLeavesNothingBehind()
    {
        if (OperatingSystem.IsWindows()) return;

        var home = Sandbox();
        try
        {
            var bundle = Path.Combine(home, "Applications", "VidShrink.app");
            var installRoot = Path.Combine(home, ".local", "share", "vidshrink");
            var binDirectory = Path.Combine(home, ".local", "bin");
            Directory.CreateDirectory(Path.Combine(bundle, "Contents", "MacOS"));
            Directory.CreateDirectory(installRoot);
            Directory.CreateDirectory(binDirectory);
            File.WriteAllText(Path.Combine(bundle, "Contents", "MacOS", "VidShrink"), "");
            File.CreateSymbolicLink(
                Path.Combine(binDirectory, "vidshrink"),
                Path.Combine(bundle, "Contents", "MacOS", "VidShrink"));

            Assert.Equal(0, Shell("install-vidshrink.sh --uninstall", home));

            Assert.False(Directory.Exists(bundle));
            Assert.False(Directory.Exists(installRoot));
            Assert.False(File.Exists(Path.Combine(binDirectory, "vidshrink")));
            Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(home, "Applications")));
        }
        finally
        {
            Directory.Delete(home, true);
        }
    }

    /// <summary>
    /// Windows kurulum yolu değişmiyor. <c>irm | iex</c> betiği bayt olarak okuyor;
    /// başına bir bayt sırası işareti girerse PowerShell ilk komutu ayrıştıramıyor ve
    /// kurulum hiç başlamıyor. Dosya <c>param(</c> ile başlamalı.
    /// </summary>
    [Fact]
    public void TheWindowsInstallerStartsWithoutAByteOrderMark()
    {
        var head = new byte[3];
        using (var stream = File.OpenRead(Path.Combine(Root, "Install-VidShrink.ps1")))
            Assert.Equal(3, stream.Read(head, 0, 3));

        Assert.Equal(new byte[] { 0x70, 0x61, 0x72 }, head);
    }
}
