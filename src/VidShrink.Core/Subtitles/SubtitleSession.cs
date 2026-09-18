using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VidShrink.Core.Subtitles;

/// <summary>
/// <c>/login</c>den dönen kullanıcı oturumu.
/// </summary>
/// <param name="Token">JWT. Parola değildir ve paroladan türetilemez.</param>
/// <param name="Host">Sağlayıcının bundan sonrası için bildirdiği konak.</param>
/// <param name="Expires">JWT'nin <c>exp</c> alanı; yoksa girişten 12 saat sonrası.</param>
public sealed record SubtitleSession(string Token, string Host, DateTimeOffset Expires)
{
    /// <summary>Varsayılan konak; <c>/login</c> başka bir şey demediyse bu geçerlidir.</summary>
    public const string DefaultHost = "api.opensubtitles.com";

    /// <summary>VIP konağı. Şartname bu konakta her isteğe JWT eklenmesini istiyor.</summary>
    public const string VipHost = "vip-api.opensubtitles.com";

    /// <summary>Belirteç var ve süresi dolmamış.</summary>
    public bool Valid(DateTimeOffset now) => Token.Length > 0 && now < Expires;

    /// <summary>Konak VIP mi; <c>Authorization</c> başlığının her istekte gitmesi buna bağlı.</summary>
    public bool Vip => Host.StartsWith("vip", StringComparison.OrdinalIgnoreCase);

    /// <summary>Bu oturumun konağına göre API taban adresi.</summary>
    public string BaseUrl => "https://" + (Host.Length == 0 ? DefaultHost : Host) + "/api/v1";
}

/// <summary>Giriş denemesinin sonucu.</summary>
/// <param name="Outcome">Kol.</param>
/// <param name="Session">Başarılıysa oturum.</param>
/// <param name="Detail">Sağlayıcının ilettiği açıklama; parola ve belirteç maskelenmiştir.</param>
public sealed record SubtitleLoginResult(SubtitleOutcome Outcome, SubtitleSession? Session, string Detail = "")
{
    /// <summary>Oturumsuz bir kol.</summary>
    public static SubtitleLoginResult Failed(SubtitleOutcome outcome, string detail = "")
        => new(outcome, null, detail);
}

/// <summary>
/// Oturumun saklandığı yer. Uygulama katmanı bunu işletim sisteminin koruma yüzeyiyle
/// uygular; testler kendi belleğini koyar.
/// </summary>
public interface ISubtitleSessionStore
{
    /// <summary>Saklanan oturum; yoksa ya da okunamıyorsa <c>null</c>.</summary>
    SubtitleSession? Read();

    /// <summary>Oturumu saklar. Saklayamayan uygulama sessizce vazgeçebilir.</summary>
    void Write(SubtitleSession session);

    /// <summary>Saklanan oturumu siler.</summary>
    void Clear();
}

/// <summary>Süreç ömrü kadar yaşayan oturum kutusu. Diske hiçbir şey yazmaz.</summary>
public sealed class MemorySessionStore : ISubtitleSessionStore
{
    private SubtitleSession? _session;

    public SubtitleSession? Read() => _session;

    public void Write(SubtitleSession session) => _session = session;

    public void Clear() => _session = null;
}

/// <summary>JWT gövdesinden okunan alanlar. İmza doğrulanmaz; bu bir yetki kararı değil.</summary>
public static class JwtClaims
{
    /// <summary>
    /// Belirtecin <c>exp</c> alanı. Üç parçalı değilse, gövde base64url değilse ya da
    /// <c>exp</c> yoksa <c>null</c> döner; çağıran o zaman kendi ömrünü koyar.
    /// </summary>
    public static DateTimeOffset? Expiry(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        try
        {
            using var document = JsonDocument.Parse(Decode(parts[1]));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!document.RootElement.TryGetProperty("exp", out var value)) return null;
            if (!value.TryGetInt64(out var seconds)) return null;
            if (seconds <= 0) return null;
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static byte[] Decode(string segment)
    {
        var padded = segment.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}

/// <summary>
/// Kullanıcının gizli bilgisini arayüze ve günlüğe dönen metinden siler.
/// </summary>
/// <remarks>
/// Sağlayıcı hata gövdesinde gönderileni yankılayabiliyor; maskesiz geçirilen bir gövde
/// parolayı kullanıcının ekranına taşır. Belirteç de gizlidir: elde eden kullanıcının
/// kotasını harcar.
/// </remarks>
public static class Secrets
{
    private static readonly Regex JwtLike =
        new(@"eyJ[A-Za-z0-9_-]{4,}\.[A-Za-z0-9_-]{4,}\.[A-Za-z0-9_-]{4,}", RegexOptions.Compiled);

    private static readonly Regex PasswordField =
        new("\"password\"\\s*:\\s*\"[^\"]*\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Maskenin yerine konan işaret.</summary>
    public const string Hidden = "***";

    /// <summary>
    /// <paramref name="secrets"/> içindeki her değeri, parola alanını ve JWT biçimindeki her
    /// diziyi <see cref="Hidden"/> ile değiştirir.
    /// </summary>
    public static string Mask(string? text, params string?[] secrets)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var masked = new StringBuilder(text);
        foreach (var secret in secrets)
        {
            if (string.IsNullOrEmpty(secret)) continue;
            masked.Replace(secret, Hidden);
        }

        return JwtLike.Replace(PasswordField.Replace(masked.ToString(), "\"password\":\"" + Hidden + "\""), Hidden);
    }
}
