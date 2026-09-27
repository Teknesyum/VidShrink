namespace VidShrink.Core.Setup;

/// <summary>
/// Kurulum ve güncelleme sonunda masaüstü ve Başlat menüsü kısayollarının simgesini kurulu
/// exe'nin ilk simgesine yeniden yazar ve kabuğa haber verir; yoksa Windows simge önbelleği
/// eski simgeyi gösterir. Hedefi kurulu exe olmayan kısayola dokunulmaz.
/// <see cref="DirectoryVariable"/> kısayol klasörünü değiştirir (testler gerçek kısayolları
/// görmesin diye).
/// </summary>
public static class ShortcutIcons
{
    public const string DirectoryVariable = "VIDSHRINK_SHORTCUT_DIR";
    public const string ShortcutName = "VidShrink.lnk";

    public static IReadOnlyList<string> Locations()
    {
        var overridden = Environment.GetEnvironmentVariable(DirectoryVariable);
        if (!string.IsNullOrWhiteSpace(overridden))
            return new[]
            {
                Path.Combine(overridden, ShortcutName),
                Path.Combine(overridden, "Programs", "VidShrink", ShortcutName)
            };
        return new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "VidShrink", ShortcutName)
        };
    }

    public static int Refresh(IShortcutWriter writer, IEnumerable<string> shortcuts, string installedExe, Action notify)
    {
        var exe = Path.GetFullPath(installedExe);
        var rewritten = 0;
        foreach (var shortcut in shortcuts)
        {
            if (!File.Exists(shortcut)) continue;
            try
            {
                var target = writer.ReadTarget(shortcut);
                if (string.IsNullOrEmpty(target) || !string.Equals(Path.GetFullPath(target), exe, StringComparison.OrdinalIgnoreCase)) continue;
                writer.SetIcon(shortcut, exe + ",0");
                rewritten++;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
            }
        }
        if (rewritten > 0) notify();
        return rewritten;
    }

    public static int RefreshInstalled(string baseDirectory)
    {
        if (!OperatingSystem.IsWindows()) return 0;
        try
        {
            return Refresh(new ShellShortcuts(), Locations(), Path.Combine(baseDirectory, LauncherUpdate.ExecutableName),
                ShellRegistration.NotifyAssociationChanged);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return 0;
        }
    }
}
