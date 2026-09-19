namespace VidShrink.Core.Share;

/// <summary>
/// "Bu hata yeniden denenebilir mi" kararı. Tek gövde: üç gösterim yüzeyi de buraya sorar.
/// </summary>
/// <remarks>
/// Ölçü hatanın <b>adı</b> değil, baytların işlenip işlenmediği. Sunucu isteği baştan
/// reddettiyse (429) yeniden deneme aynı işi tekrar başlatır ve güvenlidir. Ağ hatası ya
/// da 5xx yüklemenin ortasında geldiyse dosyanın karşıya geçip geçmediği bilinmez;
/// düğmeyi orada açmak kullanıcıya ikinci bir kopya yükletir. Bu yüzden o iki hata yalnız
/// baytların henüz akmadığı <see cref="ShareStep.Prepare"/> ve <see cref="ShareStep.Init"/>
/// adımlarında açılır.
/// </remarks>
public static class ShareRetry
{
    /// <summary>Yeniden deneme düğmesi bu sonuçta görünsün mü.</summary>
    public static bool IsRetryable(ShareFailure failure, ShareStep step) => failure switch
    {
        ShareFailure.RateLimited => true,
        ShareFailure.NetworkFailure or ShareFailure.ServiceError =>
            step is ShareStep.Prepare or ShareStep.Init,
        _ => false
    };

    /// <inheritdoc cref="IsRetryable(ShareFailure, ShareStep)"/>
    public static bool IsRetryable(this ShareResult result) =>
        !result.Ok && IsRetryable(result.Failure, result.Step);

    /// <summary>
    /// Yeniden deneme için önerilen hedef; yoksa boş. Dosya hedefin tavanını aştığında
    /// sınıflandırıcı tavanı yeten en küçük hedefi buraya koyar.
    /// </summary>
    public static string? SuggestedTarget(this ShareResult result) =>
        result.Ok ? null : result.SuggestedTargetId;
}
