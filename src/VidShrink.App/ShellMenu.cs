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

    /// <summary>
    /// Etiketlerin kabuk uzantısına bırakıldığı yer. Windows 11'in üst düzey menüsü ayrı bir
    /// süreçte, C++ tarafında çizilir ve uygulamanın dil ayarını göremez; açık kutu yazıldığı
    /// anda seçili dildeki etiket buraya da düşer, uzantı başlığı oradan okur.
    /// </summary>
    internal const string LabelKey = "Software\\Teknesyum\\VidShrink\\ShellLabels";

    /// <summary>
    /// Testlerin yazdığı kök; <c>HKEY_CURRENT_USER</c> altında görecelidir. Boşken yazma
    /// yalnız kurulu uygulamanın sürecinden gerçek kayıt defterine gider: test konağı ya da
    /// ölçüm aracı kullanıcının menüsünü kendi exe'sine çeviremez.
    /// </summary>
    internal static string? TestRoot { get; set; }

    internal static string? WriteRoot(string? processPath, string? testRoot)
        => testRoot is not null ? testRoot + "\\"
            : Integration.RegistryWriteGate.Allows(processPath) ? string.Empty
            : null;

    private static string? Root => WriteRoot(Environment.ProcessPath, TestRoot);

    private static string ReadRoot => TestRoot is null ? string.Empty : TestRoot + "\\";

    [SupportedOSPlatform("windows")]
    internal static bool Installed(string menu)
    {
        using var key = Registry.CurrentUser.OpenSubKey(ReadRoot + Branch(Extensions[0]) + "\\" + menu);
        return key is not null;
    }

    /// <summary>
    /// Açma girdisini yazar. Küçültme alt menüsüne dokunmaz: kullanıcı birini isteyip
    /// diğerini istemeyebilir, iki kutu iki ayrı kayıt defteri koluna bakar.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static int InstallOpen(string executable, string label)
    {
        if (Root is not { } root) return 0;
        Drop(MenuKey);
        WriteLabel("open", label);

        var written = 0;
        foreach (var extension in Extensions)
        {
            using var open = Registry.CurrentUser.CreateSubKey(root + Branch(extension) + "\\" + MenuKey);
            open.SetValue("MUIVerb", label, RegistryValueKind.String);
            open.SetValue("Icon", executable, RegistryValueKind.String);
            using var command = open.CreateSubKey("command");
            command.SetValue(string.Empty, $"\"{executable}\" \"%1\"", RegistryValueKind.String);
            written++;
        }

        return written;
    }

    /// <summary>Küçültme alt menüsünü yazar; açma girdisine dokunmaz.</summary>
    [SupportedOSPlatform("windows")]
    internal static int InstallShrink(string executable, string label)
    {
        if (Root is not { } root) return 0;
        RemoveShrink();
        WriteLabel("shrink", label);

        var written = 0;
        foreach (var extension in Extensions)
        {
            using var verb = Registry.CurrentUser.CreateSubKey(root + Branch(extension) + "\\" + ShrinkMenuKey);
            verb.SetValue("MUIVerb", label, RegistryValueKind.String);
            verb.SetValue("Icon", executable, RegistryValueKind.String);
            verb.SetValue("SubCommands", string.Empty, RegistryValueKind.String);
            verb.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);

            foreach (var target in ShrinkTargets)
            {
                using var entry = verb.CreateSubKey("shell\\" + target.ToString(CultureInfo.InvariantCulture));
                entry.SetValue("MUIVerb", TargetLabel(target), RegistryValueKind.String);
                entry.SetValue("Icon", executable, RegistryValueKind.String);
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
    private static void WriteLabel(string name, string label)
    {
        if (Root is not { } root) return;
        using var key = Registry.CurrentUser.CreateSubKey(root + LabelKey);
        key.SetValue(name, label, RegistryValueKind.String);
    }

    [SupportedOSPlatform("windows")]
    internal static int Relabel(string menu, string label)
    {
        if (Root is not { } root) return 0;
        var name = menu == MenuKey ? "open" : "shrink";
        var written = 0;
        using (var labels = Registry.CurrentUser.OpenSubKey(root + LabelKey))
        {
            if (labels?.GetValue(name) as string != label)
            {
                WriteLabel(name, label);
                written++;
            }
        }

        foreach (var extension in Extensions)
        {
            using var verb = Registry.CurrentUser.OpenSubKey(root + Branch(extension) + "\\" + menu, writable: true);
            if (verb is null || verb.GetValue("MUIVerb") as string == label) continue;
            verb.SetValue("MUIVerb", label, RegistryValueKind.String);
            written++;
        }

        return written;
    }

    [SupportedOSPlatform("windows")]
    internal static int RemoveOpen()
    {
        if (Root is null) return 0;
        var removed = Drop(MenuKey);
        if (TestRoot is null) RemovePackage();
        return removed;
    }

    [SupportedOSPlatform("windows")]
    internal static int RemoveShrink() => Drop(ShrinkMenuKey);

    [SupportedOSPlatform("windows")]
    private static int Drop(string menu)
    {
        if (Root is not { } root) return 0;
        var removed = 0;
        foreach (var extension in Extensions)
        {
            var path = root + Branch(extension) + "\\" + menu;
            using var probe = Registry.CurrentUser.OpenSubKey(path);
            if (probe is null) continue;
            probe.Dispose();
            Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
            removed++;
        }

        return removed;
    }

    [SupportedOSPlatform("windows")]
    internal static int Remove() => RemoveShrink() + RemoveOpen();

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
