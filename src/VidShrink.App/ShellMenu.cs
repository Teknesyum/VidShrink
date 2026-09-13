using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace VidShrink.App;

/// <summary>
/// Sağ tık menüsünün kayıt defterindeki karşılığı. Kurucunun yazdığı düzenin aynısını
/// yazar ve siler: kullanıcının menüyü açıp kapatmak için kurulum betiğine ve kod satırına
/// dönmesi gerekmiyor, Ayarlar sekmesindeki tek kutu yetiyor.
///
/// <para>Anahtar adları <c>Install-VidShrink.ps1</c> ile birebir aynı olmak zorunda; iki
/// taraf ayrılırsa betikle yazılan menü buradan silinemez. <c>KabukMenusuTests</c> ikisini
/// karşılaştırır.</para>
/// </summary>
internal static class ShellMenu
{
    internal const string MenuKey = "VidShrink";

    internal const string ShrinkMenuKey = "VidShrinkKucult";

    internal const string PackageName = "Teknesyum.VidShrink.Shell";

    internal const string ShrinkFlag = "--kucult";

    internal static readonly int[] ShrinkTargets = { 100, 250, 500, 1024, 2048 };

    internal static readonly string[] Extensions =
    {
        "mp4", "mkv", "mov", "avi", "webm", "wmv", "flv", "m4v", "mpg", "mpeg", "ts", "m2ts",
        "3gp", "ogv", "vob", "asf", "rm", "rmvb", "divx", "mxf", "f4v", "mts", "dav", "gif"
    };

    internal static bool Supported => OperatingSystem.IsWindows();

    [SupportedOSPlatform("windows")]
    internal static bool Installed()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Branch(Extensions[0]) + "\\" + MenuKey);
        return key is not null;
    }

    [SupportedOSPlatform("windows")]
    internal static int Install(string executable, string openLabel, string shrinkLabel)
    {
        Remove();

        var written = 0;
        foreach (var extension in Extensions)
        {
            using (var open = Registry.CurrentUser.CreateSubKey(Branch(extension) + "\\" + MenuKey))
            {
                open.SetValue("MUIVerb", openLabel, RegistryValueKind.String);
                open.SetValue("Icon", executable, RegistryValueKind.String);
                using var command = open.CreateSubKey("command");
                command.SetValue(string.Empty, $"\"{executable}\" \"%1\"", RegistryValueKind.String);
            }

            using var verb = Registry.CurrentUser.CreateSubKey(Branch(extension) + "\\" + ShrinkMenuKey);
            verb.SetValue("MUIVerb", shrinkLabel, RegistryValueKind.String);
            verb.SetValue("Icon", executable, RegistryValueKind.String);
            verb.SetValue("SubCommands", string.Empty, RegistryValueKind.String);
            verb.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);

            foreach (var target in ShrinkTargets)
            {
                using var entry = verb.CreateSubKey("shell\\" + target.ToString(CultureInfo.InvariantCulture));
                entry.SetValue("MUIVerb", TargetLabel(target), RegistryValueKind.String);
                entry.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);
                using var command = entry.CreateSubKey("command");
                command.SetValue(
                    string.Empty,
                    string.Join(" ", $"\"{executable}\"", ShrinkFlag,
                        target.ToString(CultureInfo.InvariantCulture), "\"%1\""),
                    RegistryValueKind.String);
            }

            written++;
        }

        return written;
    }

    [SupportedOSPlatform("windows")]
    internal static int Remove()
    {
        var removed = 0;
        foreach (var extension in Extensions)
        {
            foreach (var menu in new[] { MenuKey, ShrinkMenuKey })
            {
                var path = Branch(extension) + "\\" + menu;
                using var probe = Registry.CurrentUser.OpenSubKey(path);
                if (probe is null) continue;
                probe.Dispose();
                Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
                removed++;
            }
        }

        RemovePackage();
        return removed;
    }

    /// <summary>
    /// Windows 11'in üst düzey menüsü bir Appx paketidir; kayıt defterinden silinmez.
    /// Paket kuruluysa kaldırılır, kurulu değilse sessizce geçilir — kullanıcıya kalan
    /// tek girdi bırakmamak için.
    /// </summary>
    private static void RemovePackage()
    {
        if (!OperatingSystem.IsWindows()) return;

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(
            string.Join(" ", "Get-AppxPackage", "-Name", PackageName, "-ErrorAction", "SilentlyContinue",
                "|", "Remove-AppxPackage", "-ErrorAction", "SilentlyContinue"));

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit(PackageTimeoutMs);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    private const int PackageTimeoutMs = 30_000;

    internal static string TargetLabel(int megabytes)
        => megabytes >= 1024 && megabytes % 1024 == 0
            ? (megabytes / 1024).ToString(CultureInfo.InvariantCulture) + " GB"
            : megabytes.ToString(CultureInfo.InvariantCulture) + " MB";

    private static string Branch(string extension)
        => "Software\\Classes\\SystemFileAssociations\\." + extension + "\\shell";

    internal static IReadOnlyList<string> MenuKeys => new[] { MenuKey, ShrinkMenuKey };
}
