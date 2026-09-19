using VidShrink.Core.Share;

namespace VidShrink.App.Share;

/// <summary>
/// Yeniden deneme düğmesinin o anki hali: görünür mü, ne yazıyor, basılabilir mi ve
/// basılınca hangi hedefe gidiyor.
/// </summary>
/// <remarks>
/// Saf hesap. Üç gösterim yüzeyi (ana pencere, kaydedici, küçültme işi) aynı düğmeyi
/// kendi XAML'ında taşıyor ama kararı burada soruyor; kural üç yerde ayrışamaz.
/// </remarks>
internal readonly record struct ShareRetryPrompt(
    bool Visible,
    string Key,
    IReadOnlyList<object> Args,
    bool Enabled,
    string? RetryTargetId)
{
    private static readonly ShareRetryPrompt Gizli =
        new(false, string.Empty, Array.Empty<object>(), false, null);

    /// <summary>
    /// <paramref name="secondsLeft"/> sunucunun istediği beklemeden geriye kalan saniyedir;
    /// geri sayımı yürüten arayüz her tikte yeniden sorar.
    /// </summary>
    public static ShareRetryPrompt For(ShareResult result, ShareTargetTable? targets, int secondsLeft)
    {
        if (result.Ok) return Gizli;

        if (result.SuggestedTarget() is { } id && targets?.Find(id) is { } bigger)
        {
            return new ShareRetryPrompt(
                true, "settings.share.retry-with", new object[] { bigger.DisplayName }, true, id);
        }

        if (!result.IsRetryable()) return Gizli;

        return secondsLeft > 0
            ? new ShareRetryPrompt(
                true, "settings.share.retry-in", new object[] { secondsLeft }, false, null)
            : new ShareRetryPrompt(
                true, "settings.share.retry", Array.Empty<object>(), true, null);
    }

    /// <summary>Sunucunun istediği bekleme, saniye. İstemediyse 0.</summary>
    public static int InitialSeconds(ShareResult result) =>
        result.RetryAfter is { } wait && wait > TimeSpan.Zero
            ? (int)Math.Ceiling(wait.TotalSeconds)
            : 0;
}
