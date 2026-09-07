using System.Text.Json;
using VidShrink.Core;

namespace VidShrink.App.Integration;

/// <summary>
/// Öneri şeridinin kararı ve o kararın kalıcı yarısı. Öneri bir kez reddedilirse bir daha
/// gösterilmez; ret <c>settings.json</c>'a yazılır, dolayısıyla uygulamanın bir sonraki
/// açılışında da durur. Dosyaya dokunma işi <see cref="SettingsEntry"/> üzerinden gider ve
/// başka hiçbir anahtar silinmez.
/// </summary>
internal static class DefaultAppSuggestion
{
    private const string DismissedKey = "defaultAppSuggestionDismissed";

    /// <summary>
    /// Şerit görünsün mü. Üç koşul da tutmalı: makine Windows olmalı, uygulama şu an
    /// varsayılan olmamalı ve öneri daha önce reddedilmemiş olmalı.
    /// </summary>
    internal static bool ShouldShow(bool onWindows, bool alreadyDefault, bool dismissed)
        => onWindows && !alreadyDefault && !dismissed;

    /// <summary>Ret kaydı okunur; dosya yoksa ya da okunamazsa reddedilmemiş sayılır.</summary>
    internal static bool Dismissed(string? path = null)
        => SettingsEntry.Read(path ?? UpdateSettings.DefaultPath, DismissedKey) is { ValueKind: JsonValueKind.True };

    /// <summary>Reti kalıcı yazar. Dosyadaki diğer anahtarlara dokunmaz.</summary>
    internal static void Dismiss(string? path = null)
        => SettingsEntry.Write(path ?? UpdateSettings.DefaultPath, DismissedKey, true);
}
