using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;
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
    internal const string MenuKey = Core.Setup.ShellRegistration.MenuKey;

    internal const string ShrinkMenuKey = Core.Setup.ShellRegistration.ShrinkMenuKey;

    internal const string EnqueueMenuKey = Core.Setup.ShellRegistration.EnqueueMenuKey;

    internal const string EnqueueFlag = "--siraya-ekle";

    internal const string PackageName = AppIdentity.ShellPackageName;

    internal const string ShrinkFlag = "--kucult";

    internal static readonly string[] Extensions =
    {
        "mp4", "mkv", "mov", "avi", "webm", "wmv", "flv", "m4v", "mpg", "mpeg", "ts", "m2ts",
        "3gp", "ogv", "vob", "asf", "rm", "rmvb", "divx", "mxf", "f4v", "mts", "dav", "gif"
    };

    /// <summary>
    /// Yalnız "aç" ve "sıraya ekle" fiillerini alan ses ve çalma listesi uzantıları; küçültme
    /// alt menüsü bunlara yazılmaz.
    /// </summary>
    internal static readonly string[] PlayerExtensions =
    {
        "mp3", "m4a", "m4b", "aac", "adt", "adts", "flac", "wav", "wma", "ogg", "oga", "opus",
        "mka", "aif", "aifc", "aiff", "au", "snd", "mp2", "mpa", "ac3", "ape", "wv", "weba",
        "m3u", "m3u8", "pls", "wpl", "zpl", "asx", "wax", "wvx", "wmx"
    };

    /// <summary>Bir menünün yazıldığı uzantılar: küçültme yalnız videoda, öteki iki fiil seste ve listede de.</summary>
    internal static IReadOnlyList<string> ExtensionsFor(string menu)
        => menu == ShrinkMenuKey ? Extensions : OpenExtensions;

    private static readonly string[] OpenExtensions = [.. Extensions, .. PlayerExtensions];

    internal static bool Supported => OperatingSystem.IsWindows();

    /// <summary>
    /// Etiketlerin kabuk uzantısına bırakıldığı yer. Windows 11'in üst düzey menüsü ayrı bir
    /// süreçte, C++ tarafında çizilir ve uygulamanın dil ayarını göremez; açık kutu yazıldığı
    /// anda seçili dildeki etiket buraya da düşer, uzantı başlığı oradan okur.
    /// </summary>
    internal const string LabelKey = "Software\\" + AppIdentity.RegistryVendorPath + "\\ShellLabels";

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
    /// Açma ve sıraya ekleme girdilerini yazar; ikisi aynı kutuya bağlı. Küçültme alt menüsüne
    /// dokunmaz: kullanıcı birini isteyip diğerini istemeyebilir, iki kutu iki ayrı kayıt
    /// defteri koluna bakar. Çoklu seçimde girdi kaybolmasın diye ikisi de <c>Player</c>
    /// modelini taşır.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static int InstallOpen(string executable, string label)
    {
        if (Root is not { } root) return 0;
        Drop(MenuKey);
        Drop(EnqueueMenuKey);
        WriteLabel(Core.Setup.ShellRegistration.OpenLabelName, label);
        var enqueueLabel = EnqueueLabel();
        WriteLabel(Core.Setup.ShellRegistration.EnqueueLabelName, enqueueLabel);

        var written = 0;
        foreach (var extension in OpenExtensions)
        {
            using (var open = Registry.CurrentUser.CreateSubKey(root + Branch(extension) + "\\" + MenuKey))
            {
                open.SetValue("MUIVerb", label, RegistryValueKind.String);
                open.SetValue("Icon", executable, RegistryValueKind.String);
                open.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);
                using var command = open.CreateSubKey("command");
                command.SetValue(string.Empty, $"\"{executable}\" \"%1\"", RegistryValueKind.String);
            }

            using (var enqueue = Registry.CurrentUser.CreateSubKey(root + Branch(extension) + "\\" + EnqueueMenuKey))
            {
                enqueue.SetValue("MUIVerb", enqueueLabel, RegistryValueKind.String);
                enqueue.SetValue("Icon", executable, RegistryValueKind.String);
                enqueue.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);
                using var command = enqueue.CreateSubKey("command");
                command.SetValue(string.Empty, $"\"{executable}\" \"%1\" " + EnqueueFlag, RegistryValueKind.String);
            }

            written++;
        }

        return written;
    }

    /// <summary>"Sıraya ekle" girdisinin arayüz dilindeki etiketi; çözülmezse kurucunun İngilizce yedeği.</summary>
    internal static string EnqueueLabel()
    {
        var text = Localization.Strings.Get("shell.menu.enqueue");
        return string.IsNullOrWhiteSpace(text) || text == "shell.menu.enqueue" ? Core.Setup.SetupText.GetIn("en", "shell.menu.enqueue") : text;
    }

    /// <summary>Küçültme alt menüsünü yazar; açma girdisine dokunmaz.</summary>
    [SupportedOSPlatform("windows")]
    internal static int InstallShrink(string executable, string label)
    {
        if (Root is not { } root) return 0;
        RemoveShrink();
        WriteLabel(Core.Setup.ShellRegistration.ShrinkLabelName, label);
        var entryLabels = EntryLabels();
        WriteEntryLabels(entryLabels);

        var written = 0;
        foreach (var extension in Extensions)
        {
            using var verb = Registry.CurrentUser.CreateSubKey(root + Branch(extension) + "\\" + ShrinkMenuKey);
            verb.SetValue("MUIVerb", label, RegistryValueKind.String);
            verb.SetValue("Icon", executable, RegistryValueKind.String);
            verb.SetValue("SubCommands", string.Empty, RegistryValueKind.String);
            verb.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);

            for (var i = 0; i < ShellIntegration.QuickShrinkMenu.Count; i++)
            {
                var target = ShellIntegration.QuickShrinkMenu[i];
                using var entry = verb.CreateSubKey("shell\\" + target.Key);
                entry.SetValue("MUIVerb", entryLabels[i], RegistryValueKind.String);
                entry.SetValue("Icon", executable, RegistryValueKind.String);
                entry.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);
                using var command = entry.CreateSubKey("command");
                command.SetValue(
                    string.Empty,
                    string.Join(" ", $"\"{executable}\"", ShrinkFlag,
                        target.Megabytes.ToString(CultureInfo.InvariantCulture), "\"%1\""),
                    RegistryValueKind.String);
            }

            written++;
        }

        return written;
    }

    [SupportedOSPlatform("windows")]
    private static int WriteLabel(string name, string label, bool onlyChanged = false)
        => Root is { } root ? Core.Setup.ShellRegistration.WriteShellLabels(root + LabelKey, [(name, label)], onlyChanged) : 0;

    [SupportedOSPlatform("windows")]
    internal static int Relabel(string menu, string label)
    {
        if (Root is not { } root) return 0;
        var name = menu == MenuKey ? Core.Setup.ShellRegistration.OpenLabelName
            : menu == EnqueueMenuKey ? Core.Setup.ShellRegistration.EnqueueLabelName
            : Core.Setup.ShellRegistration.ShrinkLabelName;
        var written = WriteLabel(name, label, onlyChanged: true);

        var entryLabels = menu == ShrinkMenuKey ? EntryLabels() : null;
        if (entryLabels is not null) written += WriteEntryLabels(entryLabels, onlyChanged: true);
        foreach (var extension in ExtensionsFor(menu))
        {
            using var verb = Registry.CurrentUser.OpenSubKey(root + Branch(extension) + "\\" + menu, writable: true);
            if (verb is null) continue;
            if (entryLabels is not null) written += RelabelEntries(verb, entryLabels);
            if (verb.GetValue("MUIVerb") as string == label) continue;
            verb.SetValue("MUIVerb", label, RegistryValueKind.String);
            written++;
        }

        return written;
    }

    /// <summary>
    /// Alt menü girdilerinin etiketini Windows 11 kabuk eklentisine bırakır: eklenti
    /// <c>ShellLabels</c> altında <see cref="QuickShrinkTarget.ShellLabelName"/> değerini okur.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static int WriteEntryLabels(IReadOnlyList<string> entryLabels, bool onlyChanged = false)
    {
        if (Root is not { } root) return 0;
        return Core.Setup.ShellRegistration.WriteShellLabels(
            root + LabelKey, Core.Setup.ShellRegistration.TargetLabelValues(entryLabels), onlyChanged);
    }

    /// <summary>
    /// Alt menü girdilerinin etiketini yeni dile çevirir. Yalnız <c>MUIVerb</c> yazılır;
    /// anahtar silinip kurulmaz, girdinin komutu ve simgesi olduğu gibi kalır.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static int RelabelEntries(RegistryKey verb, IReadOnlyList<string> entryLabels)
    {
        var written = 0;
        for (var i = 0; i < ShellIntegration.QuickShrinkMenu.Count; i++)
        {
            using var entry = verb.OpenSubKey("shell\\" + ShellIntegration.QuickShrinkMenu[i].Key, writable: true);
            if (entry is null || entry.GetValue("MUIVerb") as string == entryLabels[i]) continue;
            entry.SetValue("MUIVerb", entryLabels[i], RegistryValueKind.String);
            written++;
        }

        return written;
    }

    /// <summary>
    /// Alt menü girdisinin arayüz dilindeki etiketi, kurucuların okuduğu
    /// <c>shell.menu.target.*</c> anahtarından. Anahtar çözülmezse girdinin yedeği yazılır.
    /// </summary>
    internal static string EntryLabel(QuickShrinkTarget target)
    {
        var text = Localization.Strings.Get(target.LabelKey);
        return string.IsNullOrWhiteSpace(text) || text == target.LabelKey ? target.Fallback : text;
    }

    private static string[] EntryLabels()
    {
        var labels = new string[ShellIntegration.QuickShrinkMenu.Count];
        for (var i = 0; i < labels.Length; i++) labels[i] = EntryLabel(ShellIntegration.QuickShrinkMenu[i]);
        return labels;
    }

    [SupportedOSPlatform("windows")]
    internal static int RemoveOpen()
    {
        if (Root is null) return 0;
        var removed = Drop(MenuKey);
        Drop(EnqueueMenuKey);
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
        foreach (var extension in ExtensionsFor(menu))
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

    private static string Branch(string extension)
        => "Software\\Classes\\SystemFileAssociations\\." + extension + "\\shell";

    internal static IReadOnlyList<string> MenuKeys => new[] { MenuKey, ShrinkMenuKey, EnqueueMenuKey };
}
