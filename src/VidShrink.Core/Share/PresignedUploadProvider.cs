using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VidShrink.Core.Share;

/// <summary>
/// Üç adımlı ön imzalı yükleme: <c>init</c> ile imzalı adres al, o adrese <c>PUT</c> ile
/// dosyayı koy, <c>confirm</c> ile kaydı tamamla. storage.to bu protokolü konuşur.
/// </summary>
/// <remarks>
/// <para>Hiçbir adımda kimlik doğrulaması yoktur; anonim istemci kendi ürettiği rastgele bir
/// <c>X-Visitor-Token</c> gönderir. Bu jeton sunucudan alınmaz ve ikilinin içinde gizli bir
/// anahtar oluşturmaz — kaynak yayımlanan bir uygulamada tek çalışabilir yol budur.</para>
/// <para><b>Paylaşılacak bağlantı <c>file.url</c>'dir.</b> Sunucu medyayı ayrı bir CDN alan
/// adından imzalı ve ~30 dakikada sona eren bir adresle verir; paylaşım sayfası her ziyarette
/// yeniden imzalar. CDN adresini kullanıcıya vermek sessizce kırılan bir hata üretir: bağlantı
/// kopyalandığı anda çalışır, yarım saat sonra ölür ve kimse nedenini görmez. Bu yüzden bu
/// sınıf CDN adresini hiç okumaz.</para>
/// </remarks>
public sealed class PresignedUploadProvider : IShareProvider
{
    private readonly IHttpTransport _transport;
    private readonly string _visitorToken;

    public PresignedUploadProvider(ShareTarget target, IHttpTransport transport, string? visitorToken = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _visitorToken = visitorToken ?? NewVisitorToken();
    }

    public ShareTarget Target { get; }

    public bool CanDelete => Target.CanDelete && Target.Endpoint("delete") is not null;

    /// <summary>Anonim ziyaretçi jetonu. Her uygulama açılışında yenidir; sunucudan alınmaz.</summary>
    public string VisitorToken => _visitorToken;

    private static string NewVisitorToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public async Task<ShareResult> UploadAsync(
        string filePath,
        int? retentionDays = null,
        IProgress<UploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var step = "hazırlık";
        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists)
                return ShareResult.Failed(ShareErrorClassifier.FromException(
                    Target, new FileNotFoundException("Yüklenecek dosya yok.", filePath), step));

            if (ShareErrorClassifier.CheckSize(Target, info.Length) is { } tooLarge)
                return ShareResult.Failed(tooLarge);

            var name = info.Name;
            var size = info.Length;
            var contentType = MediaTypes.ForFile(filePath);
            var days = ClampRetention(retentionDays);

            step = "init";
            var initBody = new Dictionary<string, object>
            {
                ["filename"] = name,
                ["size"] = size,
                ["content_type"] = contentType,
                ["expiry_days"] = days
            };

            using var initRequest = Json(HttpMethod.Post, Target.Endpoint("init")!, initBody);
            initRequest.Headers.TryAddWithoutValidation("X-Visitor-Token", _visitorToken);

            using var initResponse = await _transport.SendAsync(initRequest, cancellationToken).ConfigureAwait(false);
            var initText = await ReadAsync(initResponse, cancellationToken).ConfigureAwait(false);
            if (!initResponse.IsSuccessStatusCode)
                return ShareResult.Failed(ShareErrorClassifier.FromResponse(Target, initResponse, initText, step));

            using var init = JsonDocument.Parse(initText);
            var uploadUrl = Text(init.RootElement, "upload_url");
            var key = Text(init.RootElement, "r2_key");
            if (uploadUrl is null || key is null)
                return ShareResult.Failed(ShareErrorClassifier.FromResponse(Target, initResponse, initText, step));

            step = "yükleme";
            // Content-Type init'te bildirilenle birebir aynı olmalı; imza onu da kapsıyor.
            using var putRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
            {
                Content = new ProgressFileContent(filePath, contentType, progress, cancellationToken)
            };
            ShareIdentity.Stamp(putRequest);

            using var putResponse = await _transport.SendAsync(putRequest, cancellationToken).ConfigureAwait(false);
            if (!putResponse.IsSuccessStatusCode)
            {
                var putText = await ReadAsync(putResponse, cancellationToken).ConfigureAwait(false);
                return ShareResult.Failed(ShareErrorClassifier.FromResponse(Target, putResponse, putText, step));
            }

            step = "confirm";
            var confirmBody = new Dictionary<string, object>
            {
                ["r2_key"] = key,
                ["filename"] = name,
                ["size"] = size,
                ["content_type"] = contentType,
                ["expiry_days"] = days
            };

            using var confirmRequest = Json(HttpMethod.Post, Target.Endpoint("confirm")!, confirmBody);
            confirmRequest.Headers.TryAddWithoutValidation("X-Visitor-Token", _visitorToken);

            using var confirmResponse = await _transport.SendAsync(confirmRequest, cancellationToken).ConfigureAwait(false);
            var confirmText = await ReadAsync(confirmResponse, cancellationToken).ConfigureAwait(false);
            if (!confirmResponse.IsSuccessStatusCode)
                return ShareResult.Failed(ShareErrorClassifier.FromResponse(Target, confirmResponse, confirmText, step));

            using var confirm = JsonDocument.Parse(confirmText);
            var root = confirm.RootElement;
            var file = root.TryGetProperty("file", out var f) ? f : root;

            // Paylaşılacak bağlantı budur. CDN adresi burada okunmaz — açıklama sınıfın başında.
            var url = Text(file, "url");
            if (url is null)
                return ShareResult.Failed(ShareErrorClassifier.FromResponse(Target, confirmResponse, confirmText, step));

            var id = Text(file, "id") ?? IdFromUrl(url);
            var ownerToken = Text(root, "owner_token") ?? Text(file, "owner_token");
            var expires = Time(file, "expires_at");

            return ShareResult.Success(new ShareLink(
                TargetId: Target.Id,
                FileId: id,
                Url: url,
                FileName: name,
                SharedAt: DateTimeOffset.UtcNow,
                ExpiresAt: expires,
                OwnerToken: ownerToken));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return ShareResult.Failed(ShareErrorClassifier.FromException(Target, e, step));
        }
    }

    public async Task<ShareResult> DeleteAsync(ShareLink link, CancellationToken cancellationToken = default)
    {
        if (!CanDelete)
            return ShareResult.Failed(ShareErrorClassifier.DeleteUnsupported(Target));

        if (string.IsNullOrEmpty(link.OwnerToken))
            return ShareResult.Failed(new ShareDiagnosis(
                ShareFailure.TokenExpired,
                $"Bu dosyanın silme jetonu elimizde yok, {Target.DisplayName} üzerinde erken kapatılamaz. " +
                "Ömrü dolduğunda kendiliğinden silinecek.",
                "owner_token yok"));

        try
        {
            var url = Target.Endpoint("delete")!.Replace("{id}", Uri.EscapeDataString(link.FileId));
            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.TryAddWithoutValidation("Authorization", $"Owner {link.OwnerToken}");
            request.Headers.TryAddWithoutValidation("X-Visitor-Token", _visitorToken);
            ShareIdentity.Stamp(request);

            using var response = await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var text = await ReadAsync(response, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return ShareResult.Failed(ShareErrorClassifier.FromResponse(Target, response, text, "silme"));

            return ShareResult.Success(link);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return ShareResult.Failed(ShareErrorClassifier.FromException(Target, e, "silme"));
        }
    }

    public Task<ShareResult> CheckHealthAsync(CancellationToken cancellationToken = default) =>
        ShareHealth.HeadAsync(Target, _transport, Target.Endpoint("init")!, cancellationToken);

    private int ClampRetention(int? requested)
    {
        var options = Target.RetentionDays;
        var fallback = Target.DefaultRetentionDays ?? (options.Count > 0 ? options[0] : 1);
        if (requested is not { } days) return fallback;
        return options.Count == 0 || options.Contains(days) ? days : fallback;
    }

    private static HttpRequestMessage Json(HttpMethod method, string url, object body)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        return ShareIdentity.Stamp(request);
    }

    internal static async Task<string> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null) return string.Empty;
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset? Time(JsonElement element, string name) =>
        Text(element, name) is { } text && DateTimeOffset.TryParse(text, out var parsed) ? parsed : null;

    private static string IdFromUrl(string url)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url;
        return path.Trim('/').Split('/').LastOrDefault() ?? string.Empty;
    }
}
