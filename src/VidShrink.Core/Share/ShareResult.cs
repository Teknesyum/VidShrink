using System.Text.Json;
using System.Text.Json.Serialization;

namespace VidShrink.Core.Share;

/// <summary>
/// Paylaşım işleminin nasıl bittiği. Arayüz bu değeri kendi diline çevirir; ham sunucu
/// metni <see cref="ShareResult.Detail"/> alanında ayrıca taşınır.
/// </summary>
public enum ShareFailure
{
    /// <summary>Hata yok.</summary>
    None = 0,

    /// <summary>Kullanıcı izin vermedi ya da izni geri çekti. Yeniden izin istenmeli.</summary>
    NotAuthorized,

    /// <summary>Jeton geçersiz veya süresi doldu. Tarayıcı akışı yeniden açılmalı.</summary>
    TokenExpired,

    /// <summary>
    /// Drive deposu dolu. Drive'ın 15 GB'ı Gmail ve Photos ile paylaşımlıdır; kullanıcıya
    /// "yükleme başarısız" değil, deponun neden dolduğu anlatılmalı.
    /// </summary>
    QuotaExceeded,

    /// <summary>Çok fazla istek. Bir süre sonra yeniden denenebilir.</summary>
    RateLimited,

    /// <summary>Ağ koptu veya sunucuya ulaşılamadı. Sürdürülebilir yükleme devam ettirilebilir.</summary>
    NetworkFailure,

    /// <summary>Yerel diskte yer kalmadı.</summary>
    LocalDiskFull,

    /// <summary>Yüklenecek dosya bulunamadı veya okunamadı.</summary>
    FileUnreadable,

    /// <summary>Kullanıcı iptal etti.</summary>
    Cancelled,

    /// <summary>Sunucu tarafında beklenmeyen bir durum.</summary>
    ServiceError,

    /// <summary>Sınıflandırılamayan durum.</summary>
    Unknown
}

/// <summary>
/// Yüklenip paylaşılmış bir dosya. <see cref="PermissionId"/> saklanır çünkü yayını kapatmak
/// <c>permissions.delete</c> çağrısıdır ve uygulama kapanıp açıldıktan sonra da yapılabilmelidir.
/// </summary>
/// <remarks>
/// Süreli bağlantı yoktur. Drive'ın <c>expirationTime</c> alanı yalnız <c>user</c> ve <c>group</c>
/// izinlerinde çalışır, <c>anyone</c> (bağlantısı olan herkes) izninde çalışmaz. Yani "30 dakika
/// sonra kendiliğinden kapansın" kurulamaz; bağlantı <see cref="ShareFailure"/> olmadan
/// kapatılana kadar açık kalır. Kapatma <c>permissions.delete</c> ile anında etkilidir.
/// </remarks>
public sealed record ShareLink(
    string FileId,
    string PermissionId,
    string WebViewLink,
    string FileName,
    DateTimeOffset SharedAt)
{
    /// <summary>
    /// Bu platformda süreli bağlantı kurulamaz. Arayüz bunu kullanıcıya açıkça söylemelidir.
    /// </summary>
    public static bool SupportsExpiry => false;
}

/// <summary>Yükleme ilerlemesi. Arayüz bunu bir çubuğa bağlar.</summary>
public sealed record UploadProgress(long BytesSent, long TotalBytes)
{
    public double Fraction => TotalBytes <= 0 ? 0.0 : Math.Clamp((double)BytesSent / TotalBytes, 0.0, 1.0);
}

/// <summary>
/// Bir paylaşım adımının sonucu. Başarıda <see cref="Link"/> doludur; başarısızlıkta
/// <see cref="Failure"/> hangi durum olduğunu, <see cref="Detail"/> sunucunun kendi metnini verir.
/// </summary>
public sealed record ShareResult
{
    private ShareResult() { }

    public bool Ok => Failure == ShareFailure.None;

    public ShareFailure Failure { get; private init; } = ShareFailure.None;

    /// <summary>Sunucudan gelen ham açıklama. Kota hatasında olduğu gibi aynen gösterilebilir.</summary>
    public string Detail { get; private init; } = string.Empty;

    public ShareLink? Link { get; private init; }

    /// <summary>Yükleme yarıda kaldıysa devam adresi. Boşsa devam edilemez.</summary>
    public string? ResumeUri { get; private init; }

    /// <summary>Yarıda kalan yüklemede sunucunun onayladığı bayt sayısı.</summary>
    public long BytesSent { get; private init; }

    public static ShareResult Success(ShareLink link) => new() { Link = link };

    public static ShareResult Failed(ShareFailure failure, string detail) =>
        new() { Failure = failure, Detail = detail };

    /// <summary>Yükleme kesildi ama sürdürülebilir: devam adresi ve onaylanmış bayt taşınır.</summary>
    public static ShareResult Interrupted(ShareFailure failure, string detail, string? resumeUri, long bytesSent) =>
        new() { Failure = failure, Detail = detail, ResumeUri = resumeUri, BytesSent = bytesSent };
}

/// <summary>
/// Paylaşılmış dosyaların kaydı. Gizli bilgi taşımaz (jeton burada durmaz), bu yüzden düz JSON.
/// Amaç tek: kullanıcı uygulamayı kapatıp açtıktan sonra da yayını kapatabilsin.
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
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return Array.Empty<ShareLink>();
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
