namespace VidShrink.Core.Share;

/// <summary>
/// Tek adımlı çok parçalı yükleme: dosya <c>files[]</c> alanıyla tek <c>POST</c>'ta gider,
/// yanıt gövdesi doğrudan paylaşılacak adrestir. uguu.se ve aynı protokolü konuşan klonları
/// bu sınıfla çalışır.
/// </summary>
/// <remarks>
/// <b>Silme jetonu yoktur.</b> Bu bir eksiklik, hata değil: gönderen de alıcı da yayını erken
/// kapatamaz, sabit ömür (uguu.se'de 3 saat) onun yerine geçer. <see cref="CanDelete"/> bunu
/// <c>false</c> döndürerek bildirir — <c>NotSupportedException</c> atılmaz, arayüz bayrağa
/// bakıp silme düğmesini hiç göstermez.
/// </remarks>
public sealed class MultipartUploadProvider : IShareProvider
{
    /// <summary>Sunucunun beklediği çok parçalı alan adı.</summary>
    private const string FieldName = "files[]";

    private readonly IHttpTransport _transport;
    private readonly ShareTargetTable? _table;

    public MultipartUploadProvider(ShareTarget target, IHttpTransport transport, ShareTargetTable? table = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _table = table;
    }

    public ShareTarget Target { get; }

    public bool CanDelete => Target.CanDelete && Target.Endpoint("delete") is not null;

    public async Task<ShareResult> UploadAsync(
        string filePath,
        int? retentionDays = null,
        IProgress<UploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        const string step = "yükleme";
        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists)
                return ShareResult.Failed(ShareErrorClassifier.FromException(
                    Target, new FileNotFoundException("Yüklenecek dosya yok.", filePath), step));

            if (ShareErrorClassifier.CheckSize(Target, info.Length, _table) is { } tooLarge)
                return ShareResult.Failed(tooLarge);

            // retentionDays yok sayılır: bu hedefte ömür sabittir, sunucuya söylenemez.
            using var form = new MultipartFormDataContent();
            var file = new ProgressFileContent(filePath, MediaTypes.ForFile(filePath), progress, cancellationToken);
            form.Add(file, FieldName, info.Name);

            using var request = new HttpRequestMessage(HttpMethod.Post, Target.Endpoint("upload")!) { Content = form };
            request.Headers.TryAddWithoutValidation("Accept", "text/plain");
            ShareIdentity.Stamp(request);

            using var response = await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var text = (await PresignedUploadProvider.ReadAsync(response, cancellationToken).ConfigureAwait(false)).Trim();

            if (!response.IsSuccessStatusCode)
                return ShareResult.Failed(ShareErrorClassifier.FromResponse(Target, response, text, step));

            var url = FirstUrl(text);
            if (url is null)
                return ShareResult.Failed(ShareErrorClassifier.FromResponse(Target, response, text, step));

            var expires = Target.FixedRetentionHours is { } hours
                ? DateTimeOffset.UtcNow.AddHours(hours)
                : (DateTimeOffset?)null;

            return ShareResult.Success(new ShareLink(
                TargetId: Target.Id,
                FileId: IdFromUrl(url),
                Url: url,
                FileName: info.Name,
                SharedAt: DateTimeOffset.UtcNow,
                ExpiresAt: expires,
                OwnerToken: null));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return ShareResult.Failed(ShareErrorClassifier.FromException(Target, e, step));
        }
    }

    /// <summary>
    /// Ağa çıkmaz: bu protokolde silme yoktur. Neden silinemediğini anlatan bir başarısızlık
    /// döner, istisna atmaz.
    /// </summary>
    public Task<ShareResult> DeleteAsync(ShareLink link, CancellationToken cancellationToken = default) =>
        Task.FromResult(ShareResult.Failed(ShareErrorClassifier.DeleteUnsupported(Target)));

    public Task<ShareResult> CheckHealthAsync(CancellationToken cancellationToken = default) =>
        ShareHealth.HeadAsync(Target, _transport, Target.Endpoint("upload")!, cancellationToken);

    /// <summary>Düz metin yanıttan ilk adresi alır. Sunucu birden çok dosyada satır satır yazar.</summary>
    private static string? FirstUrl(string body) =>
        body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.StartsWith("http", StringComparison.OrdinalIgnoreCase));

    private static string IdFromUrl(string url)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url;
        return path.Trim('/').Split('/').LastOrDefault() ?? string.Empty;
    }
}

/// <summary>
/// Uç nokta yoklaması. Tek <c>HEAD</c> isteği, gövde yok.
/// </summary>
/// <remarks>
/// <b>Açılışta çağrılmaz.</b> Uygulama açılırken sessizce ağa çıkmaz; yoklama ancak kullanıcı
/// paylaş düğmesine bastığında yapılır. Hedef ölmüşse arayüz onu gri gösterir ve sebebini
/// <see cref="ShareResult.Message"/> alanından yazar.
/// </remarks>
public static class ShareHealth
{
    public static async Task<ShareResult> HeadAsync(
        ShareTarget target,
        IHttpTransport transport,
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            ShareIdentity.Stamp(request);

            using var response = await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // Ölçülen: storage.to'nun init yolu HEAD'e 404, uguu.se 200 döndü. Yoklamanın ölçtüğü
            // şey uç noktanın POST'u kabul edip etmediği değil, sunucunun yanıt verip vermediği:
            // yalnız POST kabul eden bir yol 404/405, Cloudflare arkasındaki bir yol 403 döndürür
            // ve üçünde de servis ayaktadır. Ölü hedefin işareti bunlar değil, yanıtın hiç
            // gelmemesi ya da 5xx olmasıdır.
            if (response.IsSuccessStatusCode || (int)response.StatusCode is 400 or 401 or 403 or 404 or 405 or 410)
                return ShareResult.Success(new ShareLink(
                    target.Id, string.Empty, url, string.Empty, DateTimeOffset.UtcNow));

            var text = await PresignedUploadProvider.ReadAsync(response, cancellationToken).ConfigureAwait(false);
            return ShareResult.Failed(ShareErrorClassifier.FromResponse(target, response, text, "yoklama"));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return ShareResult.Failed(ShareErrorClassifier.FromException(target, e, "yoklama"));
        }
    }
}
