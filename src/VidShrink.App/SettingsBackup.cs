using System.Globalization;
using System.Text.Json;

namespace VidShrink.App;

/// <summary>Ayar dosyası açılışta okunduğunda ne olduğu.</summary>
internal enum SettingsRecovery
{
    None,
    Restored,
    Defaulted,
}

/// <summary>
/// settings.json'un yedeği. Her başarılı okumadan sonra dosya yanındaki <c>.yedek</c> kopyasına
/// yazılır. Dosya JSON nesnesi olarak okunamazsa silinmez: yanına <c>.bozuk-&lt;zaman&gt;</c> adıyla
/// ayrılır, yedek okunabiliyorsa yerine o konur, okunamıyorsa varsayılanlar kalır. Sonuç
/// pencere alana dek dosyanın yoluyla bekletilir.
///
/// Okuma ya da izin hatası bozulma sayılmaz: dosya o an kilitli olabilir, ona dokunulmaz.
/// </summary>
internal static class SettingsBackup
{
    internal const string BackupSuffix = ".yedek";
    internal const string BrokenInfix = ".bozuk-";

    private static readonly object Gate = new();
    private static readonly Dictionary<string, SettingsRecovery> Pending = new(StringComparer.OrdinalIgnoreCase);

    internal static string BackupPath(string file) => file + BackupSuffix;

    /// <summary>Dosyanın yanına ayrılmış bozuk kopyalar.</summary>
    internal static IReadOnlyList<string> BrokenCopies(string file)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(file));
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return Array.Empty<string>();
        return Directory.EnumerateFiles(folder, Path.GetFileName(file) + BrokenInfix + "*").ToList();
    }

    internal static SettingsRecovery Check(string file)
    {
        if (!File.Exists(file)) return SettingsRecovery.None;
        string text;
        try { text = File.ReadAllText(file); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return SettingsRecovery.None; }

        if (IsObject(text))
        {
            try { File.Copy(file, BackupPath(file), overwrite: true); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            return SettingsRecovery.None;
        }

        var broken = file + BrokenInfix + DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        try { File.Move(file, broken); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return SettingsRecovery.None; }

        var result = SettingsRecovery.Defaulted;
        var backup = BackupPath(file);
        try
        {
            if (File.Exists(backup) && IsObject(File.ReadAllText(backup)))
            {
                File.Copy(backup, file, overwrite: true);
                result = SettingsRecovery.Restored;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }

        lock (Gate) Pending[Path.GetFullPath(file)] = result;
        return result;
    }

    /// <summary>Dosya için bekleyen sonucu verir ve siler; yoksa <see cref="SettingsRecovery.None"/>.</summary>
    internal static SettingsRecovery TakePending(string file)
    {
        lock (Gate)
        {
            var key = Path.GetFullPath(file);
            if (!Pending.Remove(key, out var result)) return SettingsRecovery.None;
            return result;
        }
    }

    internal static bool IsObject(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool IsObject(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
