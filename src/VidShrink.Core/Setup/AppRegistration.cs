using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace VidShrink.Core.Setup;

/// <summary>Kullanıcı PATH değerinin saf işlemleri; kayıt defterine dokunmaz.</summary>
public static class UserPath
{
    public static IReadOnlyList<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool Same(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    public static bool Contains(string? value, string directory) => Split(value).Any(entry => Same(entry, directory));

    public static string Add(string? value, string directory)
    {
        if (Contains(value, directory)) return value!;
        if (string.IsNullOrWhiteSpace(value)) return directory;
        return value.TrimEnd().TrimEnd(';') + ";" + directory;
    }

    public static string Remove(string? value, string directory)
    {
        if (!Contains(value, directory)) return value ?? "";
        return string.Join(";", Split(value).Where(entry => !Same(entry, directory)));
    }

    private static string Normalize(string entry) => entry.Trim().Trim('"').TrimEnd('\\', '/');
}

/// <summary>PATH'e konan <c>bin</c> klasöründeki tek satırlık sarmal; kurulu CLI'yı çağırır.</summary>
public static class CommandShim
{
    public const string FolderName = "bin";

    public const string FileName = "vidshrink.cmd";

    public const string Line = "@\"%~dp0..\\app\\vidshrink.exe\" %*";

    public static string Folder(string root) => Path.Combine(root, FolderName);

    public static string Write(string root)
    {
        var folder = Folder(root);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, FileName);
        File.WriteAllText(path, Line + "\r\n", new UTF8Encoding(false));
        return path;
    }
}

[SupportedOSPlatform("windows")]
public static class UserPathRegistry
{
    public const string ValueName = "Path";

    public static string? Read(string environmentKey)
    {
        using var key = Registry.CurrentUser.OpenSubKey(environmentKey);
        return key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    public static bool Add(string environmentKey, string directory)
    {
        using var key = Registry.CurrentUser.CreateSubKey(environmentKey, writable: true);
        var raw = key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (raw is not null and not string) return false;
        var current = raw as string;
        var next = UserPath.Add(current, directory);
        if (string.Equals(current, next, StringComparison.Ordinal)) return false;
        var kind = raw is null ? RegistryValueKind.ExpandString : key.GetValueKind(ValueName);
        key.SetValue(ValueName, next, kind);
        return true;
    }

    public static bool Remove(string environmentKey, string directory)
    {
        using var key = Registry.CurrentUser.OpenSubKey(environmentKey, writable: true);
        if (key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not string current) return false;
        var next = UserPath.Remove(current, directory);
        if (string.Equals(current, next, StringComparison.Ordinal)) return false;
        if (next.Length == 0) key.DeleteValue(ValueName, throwOnMissingValue: false);
        else key.SetValue(ValueName, next, key.GetValueKind(ValueName));
        return true;
    }
}

/// <summary>
/// Windows Ayarlar'daki "Yüklü uygulamalar" kaydı (kullanıcı düzeyi). Kaldırma komutu kurucunun
/// <c>%LOCALAPPDATA%\VidShrink</c> altındaki kopyasını çağırır, çünkü indirilen kurucu silinebilir.
/// </summary>
[SupportedOSPlatform("windows")]
public static class UninstallEntry
{
    public const string DisplayName = AppIdentity.Name;

    public const string Publisher = "Teknesyum";

    public static string SetupCopy(string localAppData) =>
        Path.Combine(localAppData, "VidShrink", ShellRegistration.SetupExecutableName);

    public static string UninstallCommand(string setupCopy, string root) =>
        $"\"{setupCopy}\" --uninstall --install-root \"{root}\"";

    public static string? Write(SetupOptions options, SetupHost host, string root, string version)
    {
        ShellRegistration.RequireWriteAllowed(options.ClassesRoot);
        var source = host.SetupExecutable;
        if (string.IsNullOrEmpty(source) || !File.Exists(source))
        {
            host.Log(SetupText.Get("setup.uninstall-entry.no-setup"));
            return null;
        }

        var copy = SetupCopy(options.LocalAppData);
        if (!PathEquality.Same(source, copy, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(source, copy, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (!File.Exists(copy)) throw;
            }
        }

        using var key = Registry.CurrentUser.CreateSubKey(options.UninstallKey, writable: true);
        var command = UninstallCommand(copy, root);
        key.SetValue("DisplayName", DisplayName);
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", Publisher);
        key.SetValue("DisplayIcon", Path.Combine(root, ShellRegistration.LauncherExecutableName) + ",0");
        key.SetValue("InstallLocation", root);
        key.SetValue("UninstallString", command);
        key.SetValue("QuietUninstallString", command + " --silent");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        return copy;
    }

    public static bool Remove(SetupOptions options)
    {
        ShellRegistration.RequireWriteAllowed(options.ClassesRoot);
        using (var existing = Registry.CurrentUser.OpenSubKey(options.UninstallKey))
        {
            if (existing is null) return false;
        }
        Registry.CurrentUser.DeleteSubKeyTree(options.UninstallKey, throwOnMissingSubKey: false);
        return true;
    }
}
