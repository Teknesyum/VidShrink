using System.Net;
using System.Net.Sockets;

namespace VidShrink.Core.Share;

/// <summary>
/// Yüklemenin hangi adımında olduğumuz. Eskiden bu bir dizgeydi ve değerleri karışıktı:
/// <c>"yükleme"</c>, <c>"hazırlık"</c> Türkçe, <c>"init"</c> ve <c>"confirm"</c> ham
/// protokol sözcüğüydü — ikisi de kullanıcıya gösterilen cümlenin ortasına giriyordu.
/// Adım artık hüküm; adı arayüzün dilinde yazılır.
/// </summary>
public enum ShareStep
{
    Prepare,
    Init,
    Upload,
    Confirm,
    Probe,
    Delete
}

/// <summary>
/// Sınıflandırılmış bir başarısızlık: ne oldu, kullanıcı ne yapabilir, ne zaman tekrar
/// denenebilir ve varsa hangi hedef bu işi görür.
/// </summary>
/// <remarks>
/// <see cref="Key"/> ve <see cref="Args"/> cümlenin yerine geçti. Core dil katmanını
/// göremez (<c>Strings</c> arayüzdedir), o yüzden burada kurulan her cümle İngilizce
/// arayüzde de Türkçe çıkıyordu. Aynı sorunu <c>Core/Subtitles</c> hüküm döndürerek
/// çözmüştü; paylaşım tarafı da artık cümle kurmuyor.
/// </remarks>
public sealed record ShareDiagnosis(
    ShareFailure Failure,
    string Key,
    IReadOnlyList<object> Args,
    TimeSpan? RetryAfter = null,
    string? SuggestedTargetId = null,
    ShareStep Step = ShareStep.Prepare)
{
    public ShareDiagnosis(ShareFailure failure, string key)
        : this(failure, key, Array.Empty<object>()) { }
}

/// <summary>
/// HTTP durumunu ve gövdesini kullanıcının <b>yapabileceği bir şeye</b> çevirir.
/// Ham durum kodu kullanıcıya gösterilecek bir cümle değildir.
/// </summary>
/// <remarks>
/// <see cref="ShareFailure.Unknown"/>'a düşen oran <see cref="UnknownRate"/> ile ölçülür.
/// Oran %10'u geçiyorsa bu tablo yetersizdir: eksik durumu buraya eklemek gerekir.
/// </remarks>
public static class ShareErrorClassifier
{
    private static long _classified;
    private static long _unknown;

    /// <summary>Şimdiye kadar sınıflandırılan hata sayısı.</summary>
    public static long ClassifiedCount => Interlocked.Read(ref _classified);

    /// <summary><see cref="ShareFailure.Unknown"/>'a düşen hata sayısı.</summary>
    public static long UnknownCount => Interlocked.Read(ref _unknown);

    /// <summary>Sınıflandırılamayan hataların oranı, 0-1. Hiç hata yoksa 0.</summary>
    public static double UnknownRate
    {
        get
        {
            var total = ClassifiedCount;
            return total == 0 ? 0.0 : (double)UnknownCount / total;
        }
    }

    /// <summary>Sayaçları sıfırlar. Ölçüm ve testler için.</summary>
    public static void ResetCounters()
    {
        Interlocked.Exchange(ref _classified, 0);
        Interlocked.Exchange(ref _unknown, 0);
    }

    /// <summary>
    /// Dosya hedefin tavanını aşıyor mu; aşıyorsa hangi hedefin yeteceğini de söyler.
    /// Yükleme hiç başlatılmadan önce çağrılır, boşuna bayt harcanmaz.
    /// </summary>
    /// <remarks>
    /// Boyutlar ham bayt olarak geçer; biçimi arayüz yazar. Burada yazılsaydı makinenin
    /// kültürüyle yazılırdı, arayüzün diliyle değil.
    /// </remarks>
    public static ShareDiagnosis? CheckSize(ShareTarget target, long bytes, ShareTargetTable? table = null)
    {
        if (target.Accepts(bytes)) return null;

        var bigger = table?.SmallestAccepting(bytes, target.Id);

        return Count(bigger is null
            ? new ShareDiagnosis(
                ShareFailure.FileTooLarge,
                "share.error.too-large-no-target",
                new object[] { bytes, target.DisplayName, target.MaxBytes })
            : new ShareDiagnosis(
                ShareFailure.FileTooLarge,
                "share.error.too-large-try",
                new object[] { bytes, target.DisplayName, target.MaxBytes, bigger.DisplayName, bigger.MaxBytes },
                SuggestedTargetId: bigger.Id));
    }

    /// <summary>Sunucu yanıtını sınıflandırır. <paramref name="body"/> okunmuş gövde metnidir.</summary>
    public static ShareDiagnosis FromResponse(
        ShareTarget target,
        HttpResponseMessage response,
        string body,
        ShareStep step)
    {
        var status = (int)response.StatusCode;
        var retryAfter = RetryAfterOf(response);
        var detail = Trim(body);
        var name = target.DisplayName;

        var diagnosis = response.StatusCode switch
        {
            HttpStatusCode.RequestEntityTooLarge => new ShareDiagnosis(
                ShareFailure.FileTooLarge,
                "share.error.server-too-large",
                new object[] { name, target.MaxBytes }),

            HttpStatusCode.TooManyRequests => new ShareDiagnosis(
                ShareFailure.RateLimited,
                retryAfter is null ? "share.error.rate-limited" : "share.error.rate-limited-wait",
                retryAfter is null ? new object[] { name } : new object[] { name, retryAfter.Value },
                retryAfter),

            HttpStatusCode.Forbidden => new ShareDiagnosis(
                ShareFailure.NotAuthorized,
                "share.error.forbidden",
                new object[] { name },
                retryAfter),

            HttpStatusCode.Unauthorized => new ShareDiagnosis(
                ShareFailure.NotAuthorized,
                "share.error.unauthorized",
                new object[] { name }),

            HttpStatusCode.NotFound or HttpStatusCode.Gone => new ShareDiagnosis(
                ShareFailure.TokenExpired,
                "share.error.gone",
                new object[] { name }),

            HttpStatusCode.RequestTimeout => new ShareDiagnosis(
                ShareFailure.NetworkFailure,
                "share.error.timeout",
                new object[] { name },
                retryAfter),

            HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout =>
                new ShareDiagnosis(
                    ShareFailure.ServiceError,
                    retryAfter is null ? "share.error.unavailable" : "share.error.unavailable-wait",
                    retryAfter is null ? new object[] { name } : new object[] { name, retryAfter.Value },
                    retryAfter),

            HttpStatusCode.InsufficientStorage => new ShareDiagnosis(
                ShareFailure.QuotaExceeded,
                "share.error.storage-full",
                new object[] { name },
                retryAfter),

            _ when status >= 500 => new ShareDiagnosis(
                ShareFailure.ServiceError,
                "share.error.server-fault",
                new object[] { name, step, status },
                retryAfter),

            _ when status is 400 or 422 => new ShareDiagnosis(
                ShareFailure.ServiceError,
                "share.error.bad-request",
                new object[] { name, status }),

            _ => new ShareDiagnosis(
                ShareFailure.Unknown,
                string.IsNullOrWhiteSpace(detail) ? "share.error.unexpected" : "share.error.unexpected-detail",
                string.IsNullOrWhiteSpace(detail)
                    ? new object[] { name, step, status }
                    : new object[] { name, step, status, detail },
                retryAfter)
        };

        return Count(diagnosis with { Step = step });
    }

    /// <summary>Ağ katmanından gelen istisnayı sınıflandırır.</summary>
    public static ShareDiagnosis FromException(ShareTarget target, Exception exception, ShareStep step)
    {
        var name = target.DisplayName;

        var diagnosis = exception switch
        {
            OperationCanceledException => new ShareDiagnosis(
                ShareFailure.Cancelled,
                "share.error.cancelled"),

            HttpRequestException { InnerException: SocketException socket } => new ShareDiagnosis(
                ShareFailure.NetworkFailure,
                socket.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData
                    ? "share.error.host-not-found"
                    : "share.error.connect-failed",
                new object[] { name }),

            HttpRequestException => new ShareDiagnosis(
                ShareFailure.NetworkFailure,
                "share.error.unreachable",
                new object[] { name }),

            FileNotFoundException or DirectoryNotFoundException => new ShareDiagnosis(
                ShareFailure.FileUnreadable,
                "share.error.file-missing"),

            UnauthorizedAccessException => new ShareDiagnosis(
                ShareFailure.FileUnreadable,
                "share.error.file-locked"),

            IOException io when IsDiskFull(io) => new ShareDiagnosis(
                ShareFailure.LocalDiskFull,
                "share.error.disk-full"),

            IOException => new ShareDiagnosis(
                ShareFailure.NetworkFailure,
                "share.error.connection-dropped",
                new object[] { name, step }),

            _ => new ShareDiagnosis(
                ShareFailure.Unknown,
                "share.error.unexpected-exception",
                new object[] { step, exception.Message })
        };

        return Count(diagnosis with { Step = step });
    }

    /// <summary>Silme desteklemeyen hedef için tanı. Ağa çıkılmaz.</summary>
    public static ShareDiagnosis DeleteUnsupported(ShareTarget target) =>
        Count(target.FixedRetentionHours is { } hours
            ? new ShareDiagnosis(
                ShareFailure.NotAuthorized,
                "share.error.no-delete-token-hours",
                new object[] { target.DisplayName, hours },
                Step: ShareStep.Delete)
            : new ShareDiagnosis(
                ShareFailure.NotAuthorized,
                "share.error.no-delete-token",
                new object[] { target.DisplayName },
                Step: ShareStep.Delete));

    private static ShareDiagnosis Count(ShareDiagnosis diagnosis)
    {
        Interlocked.Increment(ref _classified);
        if (diagnosis.Failure == ShareFailure.Unknown) Interlocked.Increment(ref _unknown);
        return diagnosis;
    }

    private static TimeSpan? RetryAfterOf(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header is null) return null;
        if (header.Delta is { } delta) return delta;
        if (header.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }

    private static bool IsDiskFull(IOException io)
    {
        // ERROR_DISK_FULL (0x70) ve ERROR_HANDLE_DISK_FULL (0x27); HRESULT 0x80070000 taşır.
        var code = io.HResult & 0xFFFF;
        return code is 0x70 or 0x27;
    }

    private static string Trim(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        var flat = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flat.Length <= 300 ? flat : flat[..300] + "…";
    }
}
