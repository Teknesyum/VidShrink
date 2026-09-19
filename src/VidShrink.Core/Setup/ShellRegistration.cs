using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace VidShrink.Core.Setup;

public static class ShellRegistration
{
    public const string MenuKey = "VidShrink";

    public const string ShrinkMenuKey = "VidShrinkKucult";

    public const string PackageName = "Teknesyum.VidShrink.Shell";

    public const string CommandClsid = "7B8B4A16-E3F5-4C4A-A8D2-26B2F895BE58";

    public const string ProgId = "Teknesyum.VidShrink.Video";

    public const string AssociationName = "VidShrink";

    public const string CapabilitiesPath = @"Teknesyum\VidShrink\Capabilities";

    public const string SetupExecutableName = "VidShrink-Setup.exe";

    public static bool WriteAllowed(string? processPath, string classesRoot) =>
        !SetupOptions.IsDefaultClassesRoot(classesRoot) ||
        string.Equals(Path.GetFileName(processPath), SetupExecutableName, StringComparison.OrdinalIgnoreCase);

    private static void RequireWriteAllowed(string classesRoot)
    {
        if (!WriteAllowed(Environment.ProcessPath, classesRoot))
            throw new SetupException($"Gerçek kayıt köküne yalnız {SetupExecutableName} yazar: {classesRoot}");
    }

    /// <summary>
    /// Kurulumda çevirilerin durduğu klasör. Menü yazıldığında (<c>dosyalar-yerinde</c>
    /// damgasından sonra) bu klasör diskte hazırdır.
    /// </summary>
    public static string LocalesFolder(string installRoot) =>
        Path.Combine(installRoot, "app", "Locales");

    /// <summary>
    /// Menü etiketinin yazılacağı dil. Seçim ya da işletim sisteminin etiketi
    /// <paramref name="localesFolder"/> altındaki klasör adlarıyla eşlenir; en uzun
    /// eşleşme kazanır (<c>zh-Hans-CN</c> → <c>zh-Hans</c>). Klasör verilmediğinde ya da
    /// yerinde olmadığında yalnız <c>tr</c> ve <c>en</c> tanınır — kurucu çevirileri
    /// okuyamadan menü yazmak zorunda kalırsa eski davranış geçerlidir.
    /// </summary>
    public static string ResolveLanguage(string choice, Func<string> uiLanguage, string? localesFolder = null)
    {
        if (Match(choice, localesFolder) is { } secilen) return secilen;

        string ui;
        try { ui = uiLanguage(); }
        catch (Exception) { ui = ""; }
        return Match(ui, localesFolder) ?? "en";
    }

    private static string? Match(string? tag, string? localesFolder)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        var parcalar = tag.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
        for (var uzunluk = parcalar.Length; uzunluk >= 1; uzunluk--)
        {
            var aday = string.Join('-', parcalar[..uzunluk]);
            if (localesFolder is null || !Directory.Exists(localesFolder))
            {
                if (aday.Equals("tr", StringComparison.OrdinalIgnoreCase) ||
                    aday.Equals("en", StringComparison.OrdinalIgnoreCase))
                    return aday.ToLowerInvariant();
                continue;
            }

            var klasor = Directory.EnumerateDirectories(localesFolder)
                .FirstOrDefault(d => string.Equals(Path.GetFileName(d), aday, StringComparison.OrdinalIgnoreCase));
            if (klasor is not null) return Path.GetFileName(klasor);
        }

        return null;
    }

    public static string OpenLabel(string language, string? localesFolder = null) =>
        Label(language, localesFolder, "shell.menu.open",
            "Bu Videoyu VidShrink ile Aç", "Open this video with VidShrink");

    public static string ShrinkLabel(string language, string? localesFolder = null) =>
        Label(language, localesFolder, "shell.menu.shrink",
            "VidShrink ile Küçült", "Shrink with VidShrink");

    /// <summary>
    /// Etiketi yayına kopyalanan <c>main.json</c>'dan okur. Dosya yoksa ya da anahtar
    /// boşsa gömülü iki metne düşer: kurucu yarım bir ağaçta da menü yazabilmeli.
    /// </summary>
    private static string Label(string language, string? localesFolder, string key, string tr, string en)
    {
        if (localesFolder is not null)
        {
            var dosya = Path.Combine(localesFolder, language, "main.json");
            try
            {
                if (File.Exists(dosya))
                {
                    using var belge = JsonDocument.Parse(File.ReadAllText(dosya));
                    if (belge.RootElement.TryGetProperty(key, out var deger) &&
                        deger.ValueKind == JsonValueKind.String &&
                        deger.GetString() is { Length: > 0 } metin)
                        return metin;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
            }
        }

        return language.Equals("tr", StringComparison.OrdinalIgnoreCase) ? tr : en;
    }

    public static string SoftwareRoot(string classesRoot) =>
        SetupOptions.IsDefaultClassesRoot(classesRoot) ? "Software" : classesRoot.TrimEnd('\\');

    public static string RenderPackageManifest(string template)
    {
        var verbs = ShellIntegration.MediaExtensions.Select(extension =>
            $"            <desktop5:ItemType Type=\".{extension}\"><desktop5:Verb Id=\"VidShrink{extension}\" Clsid=\"{CommandClsid}\" /></desktop5:ItemType>");
        return template.Replace("__ITEM_TYPES__", string.Join(Environment.NewLine, verbs));
    }

    [SupportedOSPlatform("windows")]
    public static int WriteMenus(string classesRoot, string executable, string language, string? localesFolder = null)
    {
        RequireWriteAllowed(classesRoot);
        RemoveMenus(classesRoot);
        var associations = classesRoot.TrimEnd('\\') + @"\SystemFileAssociations";
        var openLabel = OpenLabel(language, localesFolder);
        var shrinkLabel = ShrinkLabel(language, localesFolder);
        var user = Registry.CurrentUser;

        foreach (var extension in ShellIntegration.MediaExtensions)
        {
            using var open = user.CreateSubKey($@"{associations}\.{extension}\shell\{MenuKey}");
            open.SetValue("MUIVerb", openLabel, RegistryValueKind.String);
            open.SetValue("Icon", executable, RegistryValueKind.String);
            using (var command = open.CreateSubKey("command"))
                command.SetValue("", $"\"{executable}\" \"%1\"", RegistryValueKind.String);
        }

        var written = 0;
        foreach (var extension in ShellIntegration.MediaExtensions)
        {
            using var verb = user.CreateSubKey($@"{associations}\.{extension}\shell\{ShrinkMenuKey}");
            verb.SetValue("MUIVerb", shrinkLabel, RegistryValueKind.String);
            verb.SetValue("Icon", executable, RegistryValueKind.String);
            verb.SetValue("SubCommands", "", RegistryValueKind.String);
            verb.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);

            foreach (var target in ShellIntegration.QuickShrinkTargetsMegabytes)
            {
                var name = target.ToString(CultureInfo.InvariantCulture);
                using var entry = verb.CreateSubKey($@"shell\{name}");
                entry.SetValue("MUIVerb", ShellIntegration.FormatQuickShrinkLabel(target), RegistryValueKind.String);
                entry.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);
                using var command = entry.CreateSubKey("command");
                command.SetValue("", $"\"{executable}\" {ShellIntegration.ShrinkFlag} {name} \"%1\"", RegistryValueKind.String);
                written++;
            }
        }

        return written;
    }

    [SupportedOSPlatform("windows")]
    public static int RemoveMenus(string classesRoot)
    {
        RequireWriteAllowed(classesRoot);
        var associationsPath = classesRoot.TrimEnd('\\') + @"\SystemFileAssociations";
        var user = Registry.CurrentUser;
        using var associations = user.OpenSubKey(associationsPath);
        if (associations is null) return 0;

        var removed = 0;
        foreach (var association in associations.GetSubKeyNames())
        {
            var associationPath = $@"{associationsPath}\{association}";
            var shellPath = associationPath + @"\shell";
            var touched = false;
            foreach (var menu in new[] { MenuKey, ShrinkMenuKey })
            {
                using (var probe = user.OpenSubKey($@"{shellPath}\{menu}"))
                {
                    if (probe is null) continue;
                }
                user.DeleteSubKeyTree($@"{shellPath}\{menu}", false);
                touched = true;
            }
            if (touched) removed++;

            foreach (var parent in new[] { shellPath, associationPath })
            {
                using (var key = user.OpenSubKey(parent))
                {
                    if (key is null || key.SubKeyCount > 0 || key.ValueCount > 0) break;
                }
                user.DeleteSubKey(parent, false);
            }
        }

        return removed;
    }

    [SupportedOSPlatform("windows")]
    public static int WriteFileAssociation(string classesRoot, string executable)
    {
        RequireWriteAllowed(classesRoot);
        var classes = classesRoot.TrimEnd('\\');
        var software = SoftwareRoot(classesRoot);
        var command = $"\"{ShellIntegration.OpenCommandTarget(executable)}\" \"%1\"";
        var progId = $@"{classes}\{ProgId}";
        var application = $@"{classes}\Applications\{Path.GetFileName(executable)}";
        var capabilities = $@"{software}\{CapabilitiesPath}";

        SetString(progId, "", AssociationName);
        SetString(progId, "FriendlyTypeName", AssociationName);
        SetString(progId + @"\DefaultIcon", "", $"{executable},0");
        SetString(progId + @"\shell\open\command", "", command);

        SetString(application, "FriendlyAppName", AssociationName);
        SetString(application + @"\shell\open\command", "", command);

        SetString(capabilities, "ApplicationName", AssociationName);
        SetString(capabilities, "ApplicationDescription", AssociationName);

        foreach (var extension in ShellIntegration.MediaExtensions)
        {
            SetString(application + @"\SupportedTypes", "." + extension, "");
            SetString(capabilities + @"\FileAssociations", "." + extension, ProgId);
            using var list = Registry.CurrentUser.CreateSubKey($@"{classes}\.{extension}\OpenWithProgids");
            list.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
        }

        SetString(software + @"\RegisteredApplications", AssociationName, capabilities);
        return ShellIntegration.MediaExtensions.Count;
    }

    [SupportedOSPlatform("windows")]
    public static int RemoveFileAssociation(string classesRoot)
    {
        RequireWriteAllowed(classesRoot);
        var classes = classesRoot.TrimEnd('\\');
        var software = SoftwareRoot(classesRoot);
        var user = Registry.CurrentUser;
        var removed = 0;

        foreach (var extension in ShellIntegration.MediaExtensions)
        {
            var listPath = $@"{classes}\.{extension}\OpenWithProgids";
            using (var list = user.OpenSubKey(listPath, true))
            {
                if (list is null) continue;
                if (list.GetValueNames().Contains(ProgId))
                {
                    list.DeleteValue(ProgId, false);
                    removed++;
                }
            }
            RemoveEmpty(listPath);
            RemoveEmpty($@"{classes}\.{extension}");
        }

        user.DeleteSubKeyTree($@"{classes}\{ProgId}", false);
        user.DeleteSubKeyTree($@"{classes}\Applications\VidShrink.exe", false);
        RemoveEmpty($@"{classes}\Applications");
        user.DeleteSubKeyTree($@"{software}\{CapabilitiesPath}", false);
        RemoveEmpty($@"{software}\Teknesyum\VidShrink");
        RemoveEmpty($@"{software}\Teknesyum");

        using (var registered = user.OpenSubKey($@"{software}\RegisteredApplications", true))
        {
            registered?.DeleteValue(AssociationName, false);
        }
        if (!SetupOptions.IsDefaultClassesRoot(classesRoot)) RemoveEmpty($@"{software}\RegisteredApplications");

        return removed;
    }

    [SupportedOSPlatform("windows")]
    public static bool PackageRegistered()
    {
        const string packages = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
        using var key = Registry.CurrentUser.OpenSubKey(packages);
        return key?.GetSubKeyNames().Any(name => name.StartsWith(PackageName + "_", StringComparison.OrdinalIgnoreCase)) ?? false;
    }

    [SupportedOSPlatform("windows")]
    public static void NotifyAssociationChanged() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

    [SupportedOSPlatform("windows")]
    private static void SetString(string subKey, string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKey);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveEmpty(string subKey)
    {
        bool empty;
        using (var key = Registry.CurrentUser.OpenSubKey(subKey))
        {
            if (key is null) return;
            empty = key.SubKeyCount == 0 && key.ValueCount == 0;
        }
        if (empty) Registry.CurrentUser.DeleteSubKey(subKey, false);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr first, IntPtr second);

    internal static byte[] Utf8NoBom(string text) => new UTF8Encoding(false).GetBytes(text);
}
