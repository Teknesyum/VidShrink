using System.Net;
using System.Net.Sockets;

namespace VidShrink.Core.Share;

/// <summary>
/// Sınıflandırılmış bir başarısızlık: ne oldu, kullanıcı ne yapabilir, ne zaman tekrar
/// denenebilir ve varsa hangi hedef bu işi görür.
/// </summary>
public sealed record ShareDiagnosis(
    ShareFailure Failure,
    string Message,
    string Detail = "",
    TimeSpan? RetryAfter = null,
    string? SuggestedTargetId = null);

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
    public static ShareDiagnosis? CheckSize(ShareTarget target, long bytes, ShareTargetTable? table = null)
    {
        if (target.Accepts(bytes)) return null;

        var bigger = table?.Accepting(bytes).FirstOrDefault(t => t.Id != target.Id);
        var message =
            $"Dosya {Size(bytes)}, {target.DisplayName} en fazla {Size(target.MaxBytes)} kabul ediyor." +
            (bigger is null
                ? " Listedeki hiçbir hedefin tavanı yetmiyor; hedef boyutu küçültmek gerekiyor."
                : $" {bigger.DisplayName} bu boyutu kabul ediyor ({Size(bigger.MaxBytes)}).");

        return Count(new ShareDiagnosis(ShareFailure.FileTooLarge, message, SuggestedTargetId: bigger?.Id));
    }

    /// <summary>Sunucu yanıtını sınıflandırır. <paramref name="body"/> okunmuş gövde metnidir.</summary>
    public static ShareDiagnosis FromResponse(
        ShareTarget target,
        HttpResponseMessage response,
        string body,
        string step)
    {
        var status = (int)response.StatusCode;
        var retryAfter = RetryAfterOf(response);
        var detail = Trim(body);
        var name = target.DisplayName;

        var diagnosis = response.StatusCode switch
        {
            HttpStatusCode.RequestEntityTooLarge => new ShareDiagnosis(
                ShareFailure.FileTooLarge,
                $"{name} dosyayı büyük buldu; tavanı {Size(target.MaxBytes)}. Hedef boyutu düşürüp yeniden deneyin.",
                detail),

            HttpStatusCode.TooManyRequests => new ShareDiagnosis(
                ShareFailure.RateLimited,
                retryAfter is null
                    ? $"{name} çok fazla istek aldı. Anonim yüklemede günlük bir sayı sınırı var; bir süre sonra yeniden deneyin."
                    : $"{name} çok fazla istek aldı. {Wait(retryAfter.Value)} sonra yeniden denenebilir.",
                detail,
                retryAfter),

            HttpStatusCode.Forbidden => new ShareDiagnosis(
                ShareFailure.NotAuthorized,
                $"{name} isteği reddetti. Silme jetonu bu dosyaya ait değilse ya da dosya kaldırıldıysa bu olur; " +
                "yayın zaten kapalı olabilir.",
                detail,
                retryAfter),

            HttpStatusCode.Unauthorized => new ShareDiagnosis(
                ShareFailure.NotAuthorized,
                $"{name} isteği yetkisiz saydı. Bu sürümde anonim yükleme bekleniyor; servis kural değiştirmiş olabilir.",
                detail),

            HttpStatusCode.NotFound or HttpStatusCode.Gone => new ShareDiagnosis(
                ShareFailure.TokenExpired,
                $"Dosya {name} üzerinde artık yok — ömrü dolmuş ya da zaten silinmiş. Kayıttan düşürüldü.",
                detail),

            HttpStatusCode.RequestTimeout => new ShareDiagnosis(
                ShareFailure.NetworkFailure,
                $"{name} zamanında yanıt vermedi. Bağlantınızı kontrol edip yeniden deneyin.",
                detail,
                retryAfter),

            HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout =>
                new ShareDiagnosis(
                    ShareFailure.ServiceError,
                    retryAfter is null
                        ? $"{name} şu an hizmet vermiyor. Diğer hedefi deneyin ya da biraz sonra tekrar bakın."
                        : $"{name} şu an hizmet vermiyor. {Wait(retryAfter.Value)} sonra yeniden denenebilir.",
                    detail,
                    retryAfter),

            HttpStatusCode.InsufficientStorage => new ShareDiagnosis(
                ShareFailure.QuotaExceeded,
                $"{name} deposu dolu. Diğer hedefi deneyin.",
                detail,
                retryAfter),

            _ when status >= 500 => new ShareDiagnosis(
                ShareFailure.ServiceError,
                $"{name} {step} adımında sunucu hatası verdi ({status}). Diğer hedefi deneyebilirsiniz.",
                detail,
                retryAfter),

            _ when status is 400 or 422 => new ShareDiagnosis(
                ShareFailure.ServiceError,
                $"{name} isteği anlamadı ({status}). Servis arayüzünü değiştirmiş olabilir; diğer hedefi deneyin.",
                detail),

            _ => new ShareDiagnosis(
                ShareFailure.Unknown,
                $"{name} {step} adımında beklenmeyen bir yanıt verdi ({status}). Sunucunun kendi açıklaması: " +
                (string.IsNullOrWhiteSpace(detail) ? "yok." : detail),
                detail,
                retryAfter)
        };

        return Count(diagnosis);
    }

    /// <summary>Ağ katmanından gelen istisnayı sınıflandırır.</summary>
    public static ShareDiagnosis FromException(ShareTarget target, Exception exception, string step)
    {
        var name = target.DisplayName;

        var diagnosis = exception switch
        {
            OperationCanceledException => new ShareDiagnosis(
                ShareFailure.Cancelled,
                "Yükleme iptal edildi. Sunucuya yarım dosya bırakılmadı.",
                exception.Message),

            HttpRequestException { InnerException: SocketException socket } => new ShareDiagnosis(
                ShareFailure.NetworkFailure,
                socket.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData
                    ? $"{name} adresi çözülemedi. Servis kapanmış ya da bu ağdan erişilemiyor olabilir; diğer hedefi deneyin."
                    : $"{name} sunucusuna bağlanılamadı. Ağ bağlantınızı kontrol edip yeniden deneyin.",
                exception.Message),

            HttpRequestException => new ShareDiagnosis(
                ShareFailure.NetworkFailure,
                $"{name} sunucusuna ulaşılamadı. Ağ bağlantınızı kontrol edip yeniden deneyin.",
                exception.Message),

            FileNotFoundException or DirectoryNotFoundException => new ShareDiagnosis(
                ShareFailure.FileUnreadable,
                "Yüklenecek dosya bulunamadı. Dönüştürme çıktısı taşınmış ya da silinmiş olabilir.",
                exception.Message),

            UnauthorizedAccessException => new ShareDiagnosis(
                ShareFailure.FileUnreadable,
                "Dosya okunamadı; başka bir program onu açık tutuyor olabilir.",
                exception.Message),

            IOException io when IsDiskFull(io) => new ShareDiagnosis(
                ShareFailure.LocalDiskFull,
                "Yerel diskte yer kalmadı.",
                exception.Message),

            IOException => new ShareDiagnosis(
                ShareFailure.NetworkFailure,
                $"{name} ile bağlantı {step} adımında koptu. Yeniden deneyin.",
                exception.Message),

            _ => new ShareDiagnosis(
                ShareFailure.Unknown,
                $"{step} adımında beklenmeyen bir hata oldu: {exception.Message}",
                exception.Message)
        };

        return Count(diagnosis);
    }

    /// <summary>Silme desteklemeyen hedef için tanı. Ağa çıkılmaz.</summary>
    public static ShareDiagnosis DeleteUnsupported(ShareTarget target)
    {
        var life = target.FixedRetentionHours is { } hours
            ? $"{hours} saatlik otomatik silme onun yerine geçiyor"
            : "dosya kendi ömrü dolunca siliniyor";

        return Count(new ShareDiagnosis(
            ShareFailure.NotAuthorized,
            $"{target.DisplayName} silme jetonu vermiyor, bu yüzden yayın erken kapatılamıyor; {life}.",
            "canDelete=false"));
    }

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

    private static string Wait(TimeSpan span) =>
        span.TotalMinutes < 1
            ? $"{Math.Max(1, (int)Math.Ceiling(span.TotalSeconds))} saniye"
            : $"{(int)Math.Ceiling(span.TotalMinutes)} dakika";

    private static string Trim(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        var flat = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flat.Length <= 300 ? flat : flat[..300] + "…";
    }

    private static string Size(long bytes)
    {
        if (bytes <= 0) return "sınırsız";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return value >= 100 || unit == 0
            ? $"{Math.Round(value)} {units[unit]}"
            : $"{value:0.#} {units[unit]}";
    }
}
