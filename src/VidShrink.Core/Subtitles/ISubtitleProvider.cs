namespace VidShrink.Core.Subtitles;

/// <summary>
/// Altyazı arama ve indirmenin sonucu. Her kol kullanıcıya ayrı bir metinle döner;
/// çağıran taraf istisna yakalamaz, bu değeri okur.
/// </summary>
public enum SubtitleOutcome
{
    /// <summary>İş bitti.</summary>
    Ok,

    /// <summary>Ayarlarda API anahtarı yok; özellik kapalı.</summary>
    NoKey,

    /// <summary>Sağlayıcı anahtarı tanımadı ya da reddetti.</summary>
    BadKey,

    /// <summary>Arama çalıştı, bu videoya altyazı yok.</summary>
    NoResult,

    /// <summary>Günlük indirme kotası doldu.</summary>
    QuotaExceeded,

    /// <summary>
    /// Saniyedeki istek sınırı aşıldı (IP başına 5/sn). Kotadan ayrı bir koldur: kota
    /// gece yarısı yenilenir, bu birkaç saniyede geçer.
    /// </summary>
    RateLimited,

    /// <summary>Ağa çıkılamadı ya da sağlayıcı beklenmedik cevap verdi.</summary>
    NetworkError,

    /// <summary>İndirilen dosya videonun yanına yazılamadı.</summary>
    WriteError
}

/// <summary>Bir arama isteği.</summary>
/// <param name="MediaPath">Videonun tam yolu. İndirilen dosya bunun yanına yazılır.</param>
/// <param name="MovieHash">Varsa moviehash; tam eşleşme araması bununla yapılır.</param>
/// <param name="Name">Hash tutmazsa kullanılan ad araması.</param>
/// <param name="Languages">Yeğleme sırasıyla dil kodları (arayüz dili, sonra İngilizce).</param>
public sealed record SubtitleQuery(string MediaPath, string? MovieHash, string Name, IReadOnlyList<string> Languages);

/// <summary>Arama sonucundaki tek bir altyazı dosyası.</summary>
public sealed record SubtitleCandidate(
    long FileId,
    string Language,
    string Release,
    string FileName,
    int DownloadCount,
    bool HashMatch);

/// <summary>Aramanın sonucu.</summary>
public sealed record SubtitleSearchResult(SubtitleOutcome Outcome, IReadOnlyList<SubtitleCandidate> Candidates)
{
    /// <summary>Sonuçsuz bir kol.</summary>
    public static SubtitleSearchResult Failed(SubtitleOutcome outcome)
        => new(outcome, Array.Empty<SubtitleCandidate>());
}

/// <summary>İndirmenin sonucu.</summary>
/// <param name="Outcome">Kol.</param>
/// <param name="Path">Başarılıysa videonun yanına yazılan dosyanın tam yolu.</param>
/// <param name="Remaining">Sağlayıcının bildirdiği kalan günlük hak; bilinmiyorsa -1.</param>
public sealed record SubtitleDownloadResult(SubtitleOutcome Outcome, string? Path, int Remaining)
{
    /// <summary>Dosyasız bir kol.</summary>
    public static SubtitleDownloadResult Failed(SubtitleOutcome outcome, int remaining = -1)
        => new(outcome, null, remaining);
}

/// <summary>
/// Altyazı sağlayıcısı. Ağ bu arayüzün arkasında kalır; testler kendi uygulamasını koyar
/// ya da gerçek sağlayıcıyı sahte <see cref="Share.IHttpTransport"/> ile kurar.
/// </summary>
public interface ISubtitleProvider
{
    /// <summary>Anahtar var mı; yoksa arayüz özelliği kapalı gösterir.</summary>
    bool IsConfigured { get; }

    /// <summary>Önce moviehash, tutmazsa ad ile arar.</summary>
    Task<SubtitleSearchResult> SearchAsync(SubtitleQuery query, CancellationToken cancellationToken);

    /// <summary>Seçilen adayı indirip videonun yanına <c>&lt;ad&gt;.&lt;dil&gt;.srt</c> yazar.</summary>
    Task<SubtitleDownloadResult> DownloadAsync(SubtitleCandidate candidate, string mediaPath, CancellationToken cancellationToken);
}
