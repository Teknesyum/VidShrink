using System.Text.Json;
using System.Text.Json.Serialization;

namespace VidShrink.Core.Share;

/// <summary>
/// Paylaşım işleminin nasıl bittiği. Arayüz bu değeri kendi diline çevirir; kullanıcıya
/// gösterilecek eyleme dönüşebilir cümle <see cref="ShareDiagnosis.Message"/> alanındadır,
/// ham sunucu metni <see cref="ShareResult.Detail"/> alanında ayrıca taşınır.
/// </summary>
public enum ShareFailure
{
    /// <summary>Hata yok.</summary>
    None = 0,

    /// <summary>Sunucu isteği reddetti (403). Sahiplik jetonu yanlış ya da içerik engellenmiş.</summary>
    NotAuthorized,

    /// <summary>
    /// Sahiplik jetonu artık geçerli değil ya da dosyanın ömrü dolmuş. Silinecek bir şey kalmadı;
    /// kayıt defterindeki satır düşürülmelidir.
    /// </summary>
    TokenExpired,

    /// <summary>
    /// Hedefin kotası doldu: anonim yükleme sayısı ya da bant genişliği eşiği aşıldı.
    /// Dosyanın kendi boyutu için <see cref="FileTooLarge"/> kullanılır.
    /// </summary>
    QuotaExceeded,

    /// <summary>Dosya hedefin boyut tavanının üstünde. Yükleme hiç başlatılmaz.</summary>
    FileTooLarge,

    /// <summary>Çok fazla istek (429). Bir süre sonra yeniden denenebilir.</summary>
    RateLimited,

    /// <summary>Ağ koptu veya uç noktaya ulaşılamadı.</summary>
    NetworkFailure,

    /// <summary>Yerel diskte yer kalmadı.</summary>
    LocalDiskFull,

    /// <summary>Yüklenecek dosya bulunamadı veya okunamadı.</summary>
    FileUnreadable,

    /// <summary>Kullanıcı iptal etti.</summary>
    Cancelled,

    /// <summary>Sunucu tarafında beklenmeyen bir durum (5xx).</summary>
    ServiceError,

    /// <summary>Sınıflandırılamayan durum.</summary>
    Unknown
}

/// <summary>
/// Yüklenip paylaşılmış bir dosya.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Url"/> <b>kullanıcıya verilecek bağlantıdır</b> — sağlayıcının paylaşım sayfası.
/// Medyanın kendisi ayrı bir CDN alan adından, imzalı ve kısa ömürlü (storage.to'da ~30 dakika)
/// bir adresle gelir; paylaşım sayfası her ziyarette yeniden imzalar. <b>CDN adresini asla
/// paylaşma:</b> kopyalandığı anda çalışır, yarım saat sonra sessizce ölür ve hata hiçbir yerde
/// görünmez. Bu yüzden bu kayıtta CDN adresi için alan bile yoktur.
/// </para>
/// <para>
/// <see cref="OwnerToken"/> dosyaya özgü bir silme yetkisidir, hesap kimliği değil. Kayıt
/// defterinde düz metin durur: uygulama kapanıp açıldıktan sonra da yayının kapatılabilmesi,
/// jetonu saklamamaktan daha değerli. Jeton kaybolursa dosya ömrü dolana kadar silinemez.
/// Silme desteklemeyen hedeflerde (uguu.se) boştur.
/// </para>
/// </remarks>
public sealed record ShareLink(
    string TargetId,
    string FileId,
    string Url,
    string FileName,
    DateTimeOffset SharedAt,
    DateTimeOffset? ExpiresAt = null,
    string? OwnerToken = null)
{
    /// <summary>Bu kaydın silinebilmesi için elde jeton var mı.</summary>
    [JsonIgnore]
    public bool CanDelete => !string.IsNullOrEmpty(OwnerToken);
}

/// <summary>Yükleme ilerlemesi. Arayüz bunu bir çubuğa bağlar.</summary>
public sealed record UploadProgress(long BytesSent, long TotalBytes)
{
    public double Fraction => TotalBytes <= 0 ? 0.0 : Math.Clamp((double)BytesSent / TotalBytes, 0.0, 1.0);
}

/// <summary>
/// Bir paylaşım adımının sonucu. Başarıda <see cref="Link"/> doludur; başarısızlıkta
/// <see cref="Failure"/> hangi durum olduğunu, <see cref="Message"/> kullanıcının
/// yapabileceği şeyi, <see cref="Detail"/> sunucunun kendi metnini verir.
/// </summary>
public sealed record ShareResult
{
    private ShareResult() { }

    public bool Ok => Failure == ShareFailure.None;

    public ShareFailure Failure { get; private init; } = ShareFailure.None;

    /// <summary>Sunucudan gelen ham açıklama. Tanı kurulamadığında olduğu gibi gösterilebilir.</summary>
    public string Detail { get; private init; } = string.Empty;

    /// <summary>Kullanıcının yapabileceği bir şeye çevrilmiş cümle. Başarıda boştur.</summary>
    public string Message { get; private init; } = string.Empty;

    /// <summary>Yeniden denemeden önce beklenecek süre. Sunucu söylemediyse boştur.</summary>
    public TimeSpan? RetryAfter { get; private init; }

    /// <summary>Bu hata için önerilen başka hedef (örneğin tavanı yeten). Yoksa boştur.</summary>
    public string? SuggestedTargetId { get; private init; }

    public ShareLink? Link { get; private init; }

    public static ShareResult Success(ShareLink link) => new() { Link = link };

    public static ShareResult Failed(ShareDiagnosis diagnosis) => new()
    {
        Failure = diagnosis.Failure,
        Detail = diagnosis.Detail,
        Message = diagnosis.Message,
        RetryAfter = diagnosis.RetryAfter,
        SuggestedTargetId = diagnosis.SuggestedTargetId
    };
}

/// <summary>
/// Paylaşılmış dosyaların kaydı. Amaç tek: kullanıcı uygulamayı kapatıp açtıktan sonra da
/// yayını kapatabilsin. Bu yüzden silme jetonu da burada durur — ayrıntı
/// <see cref="ShareLink.OwnerToken"/> açıklamasında.
/// </summary>
public sealed class ShareLedger
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _path;

    public ShareLedger(string? path = null) => _path = path ?? DefaultPath;

    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VidShrink",
            "paylasimlar.json");

    public IReadOnlyList<ShareLink> Load()
    {
        try
        {
            if (!File.Exists(_path)) return Array.Empty<ShareLink>();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<ShareLink>>(json, Options) ?? new List<ShareLink>();
        }
        catch (JsonException)
        {
            Quarantine();
            return Array.Empty<ShareLink>();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<ShareLink>();
        }
    }

    /// <summary>
    /// Bozuk kayıt dosyasının bir kopyasını yanına <c>.bozuk-<i>zaman</i></c> adıyla bırakır.
    /// Üstüne yazılırsa eski <see cref="ShareLink.OwnerToken"/> değerleri kalıcı olarak gider
    /// ve o yayınlar bir daha kapatılamaz.
    /// </summary>
    private void Quarantine()
    {
        try
        {
            var backup = $"{_path}.bozuk-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
            File.Copy(_path, backup, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Add(ShareLink link)
    {
        var all = Load().Where(x => x.FileId != link.FileId).ToList();
        all.Add(link);
        Write(all);
    }

    public void Remove(string fileId)
    {
        var all = Load().Where(x => x.FileId != fileId).ToList();
        Write(all);
    }

    private void Write(IReadOnlyList<ShareLink> links)
    {
        var folder = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(_path, JsonSerializer.Serialize(links, Options));
    }
}
