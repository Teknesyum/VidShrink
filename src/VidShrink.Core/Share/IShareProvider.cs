namespace VidShrink.Core.Share;

/// <summary>
/// Bir paylaşım hedefine yükleme, bağlantı üretme ve — destekliyorsa — silme.
/// Sağlayıcıya özgü protokol bu arayüzün arkasında kalır; çağıran hangi servise
/// yüklediğini bilmek zorunda değildir.
/// </summary>
public interface IShareProvider
{
    /// <summary>Bu sağlayıcının okuduğu hedef satırı.</summary>
    ShareTarget Target { get; }

    /// <summary>
    /// Gönderenin elinde silme jetonu oluyor mu. uguu.se'de <c>false</c>; bu bir hata değil
    /// servisin kendisi böyle. <see cref="DeleteAsync"/> istisna atmaz, sınıflandırılmış bir
    /// başarısızlık döndürür — arayüz bu bayrağa bakıp düğmeyi hiç göstermez.
    /// </summary>
    bool CanDelete { get; }

    /// <summary>
    /// Dosyayı yükler ve paylaşılacak bağlantıyı döndürür.
    /// </summary>
    /// <param name="filePath">Yüklenecek dosya.</param>
    /// <param name="retentionDays">
    /// İstenen ömür, gün. Hedef ömür seçtirmiyorsa yok sayılır
    /// (<see cref="ShareTarget.HasFixedRetention"/>).
    /// </param>
    /// <param name="progress">Yükleme ilerlemesi. Gerekmiyorsa boş geçilebilir.</param>
    Task<ShareResult> UploadAsync(
        string filePath,
        int? retentionDays = null,
        IProgress<UploadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yayını kapatır. <see cref="CanDelete"/> <c>false</c> ise ağa çıkılmaz ve neden
    /// silinemediğini anlatan bir başarısızlık döner.
    /// </summary>
    Task<ShareResult> DeleteAsync(ShareLink link, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uç noktanın ayakta olup olmadığını tek <c>HEAD</c> isteğiyle yoklar.
    /// </summary>
    /// <remarks>
    /// <b>Açılışta çağrılmaz.</b> Uygulama açılırken sessizce ağa çıkmaz; bu yoklama ancak
    /// kullanıcı paylaş düğmesine bastığında yapılır.
    /// </remarks>
    Task<ShareResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Hedef satırından doğru sağlayıcıyı kurar.
/// </summary>
/// <remarks>
/// Seçim <b>kimliğe değil uç nokta şekline</b> bakar: <c>init</c> + <c>confirm</c> taşıyan
/// satır üç adımlı ön imzalı protokoldür, tek <c>upload</c> taşıyan satır tek adımlı çok
/// parçalı protokoldür. Böylece aynı protokolü konuşan yeni bir servis (uguu klonları çok)
/// yalnız JSON'a satır eklenerek çalışır, tek satır C# değişmez.
/// </remarks>
public static class ShareProviderFactory
{
    /// <param name="table">
    /// Sağlayıcının bağlı olduğu tablo. Tavan aşımında "hangi hedef yeter" sorusunun cevabı
    /// buradan çıkar; verilmezse kullanıcıya yalnız aşılan tavan söylenebilir.
    /// </param>
    public static IShareProvider Create(ShareTarget target, IHttpTransport transport, ShareTargetTable? table = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(transport);

        if (target.Endpoint("init") is not null && target.Endpoint("confirm") is not null)
            return new PresignedUploadProvider(target, transport, table: table);

        if (target.Endpoint("upload") is not null)
            return new MultipartUploadProvider(target, transport, table);

        throw new InvalidOperationException(
            $"'{target.Id}' hedefinde tanınan bir uç nokta takımı yok: üç adımlı protokol için " +
            "'init' ve 'confirm', tek adımlı protokol için 'upload' gerekiyor.");
    }

    /// <summary>Tablodaki her hedef için bir sağlayıcı kurar.</summary>
    public static IReadOnlyList<IShareProvider> CreateAll(ShareTargetTable table, IHttpTransport transport) =>
        table.Targets.Select(t => Create(t, transport, table)).ToList();
}
