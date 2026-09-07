using System.Text.Json;
using System.Text.Json.Nodes;
using VidShrink.Core;

namespace VidShrink.App.Integration;

/// <summary>
/// Öneri şeridinin kararı ve o kararın kalıcı yarısı. Öneri bir kez reddedilirse bir daha
/// gösterilmez; ret <c>settings.json</c>'a yazılır, dolayısıyla uygulamanın bir sonraki
/// açılışında da durur.
///
/// <para>Dosya <see cref="AppSettings"/> ile paylaşıldığı için yazma tarafı önce var olan
/// nesneyi okur, yalnız kendi anahtarını değiştirir ve tümünü geri yazar; başka hiçbir
/// anahtar silinmez.</para>
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
    {
        var file = path ?? UpdateSettings.DefaultPath;
        try
        {
            if (!File.Exists(file)) return false;
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            return document.RootElement.TryGetProperty(DismissedKey, out var value)
                && value.ValueKind == JsonValueKind.True;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Reti kalıcı yazar. Dosyadaki diğer anahtarlara dokunmaz.</summary>
    internal static void Dismiss(string? path = null)
    {
        var file = path ?? UpdateSettings.DefaultPath;
        var folder = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        JsonObject root;
        try
        {
            root = File.Exists(file) && JsonNode.Parse(File.ReadAllText(file)) is JsonObject existing
                ? existing
                : new JsonObject();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            root = new JsonObject();
        }

        root[DismissedKey] = true;

        using var stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        root.WriteTo(writer);
    }
}
