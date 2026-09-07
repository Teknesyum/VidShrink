using System.Runtime.Versioning;
using System.Text.Json;
using VidShrink.Core;

namespace VidShrink.App.Integration;

/// <summary>
/// "Birlikte aç" kaydını gerçek çalıştırma yolundan bir kez tetikleyen kapı.
/// <see cref="FileAssociation.Register"/> kayıt defterine yazdığı için her açılışta
/// koşturulmaz: yazılan çalıştırılabilirin yolu <c>settings.json</c>'a not düşülür ve
/// sonraki açılışlarda aynı yol görülürse hiç yazılmaz.
///
/// <para>Yol değiştiğinde — taşınan kurulum, yeni sürüm başka klasöre kurulduğunda —
/// kayıt bir kez daha yazılır; eski komut satırı artık olmayan bir dosyayı gösterdiği için
/// bu tazeleme gereklidir.</para>
/// </summary>
internal static class FileAssociationSetup
{
    /// <summary>Kaydın hangi çalıştırılabilir için yazıldığını tutan anahtar.</summary>
    internal const string RegisteredKey = "fileAssociationRegisteredFor";

    /// <summary>
    /// Kayıt yazılmalı mı. Not düşülen yol bugünkü çalıştırılabilirle aynıysa yazılmaz;
    /// hiç not yoksa ya da yol değiştiyse yazılır.
    /// </summary>
    internal static bool Needed(string? recorded, string executablePath)
        => !string.IsNullOrEmpty(executablePath)
            && !string.Equals(recorded, executablePath, StringComparison.OrdinalIgnoreCase);

    /// <summary>Not düşülen çalıştırılabilir yolu; kayıt hiç yazılmadıysa <c>null</c>.</summary>
    internal static string? Recorded(string? settingsPath = null)
        => SettingsEntry.Read(settingsPath ?? UpdateSettings.DefaultPath, RegisteredKey) is { ValueKind: JsonValueKind.String } value
            ? value.GetString()
            : null;

    /// <summary>Kaydın bu yol için yazıldığını not düşer.</summary>
    internal static void Record(string executablePath, string? settingsPath = null)
        => SettingsEntry.Write(settingsPath ?? UpdateSettings.DefaultPath, RegisteredKey, executablePath);

    /// <summary>
    /// Gerekiyorsa kaydı yazar ve başarılıysa not düşer. Yazılan satır sayısı değil,
    /// bu çağrının kayıt defterine dokunup dokunmadığı döner. Yazma başarısız olursa not
    /// düşülmez: sonraki açılış yeniden dener.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static bool Ensure(string executablePath, string? settingsPath = null)
    {
        if (!Needed(Recorded(settingsPath), executablePath)) return false;

        var failed = FileAssociation.Register(executablePath);
        if (failed.Count > 0) return true;

        Record(executablePath, settingsPath);
        return true;
    }
}
