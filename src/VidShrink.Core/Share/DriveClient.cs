using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VidShrink.Core.Share;

/// <summary>
/// HTTP katmanı. Testler bunun yerine kendi uygulamasını koyar; ağa çıkılmaz.
/// </summary>
public interface IHttpTransport
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}

/// <summary>Gerçek ağa çıkan taşıyıcı.</summary>
public sealed class HttpClientTransport : IHttpTransport, IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _owned;

    public HttpClientTransport(HttpClient? client = null)
    {
        _owned = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    public void Dispose()
    {
        if (_owned) _client.Dispose();
    }
}

/// <summary>Drive'ın bildirdiği depolama durumu. 15 GB Gmail ve Photos ile paylaşımlıdır.</summary>
public sealed record StorageQuota(long? Limit, long Usage, long UsageInDrive)
{
    public long? Free => Limit is null ? null : Math.Max(0, Limit.Value - Usage);

    /// <summary>Drive dışındaki (Gmail, Photos) kullanım. Kullanıcıya kotanın neden dolduğunu anlatır.</summary>
    public long UsageOutsideDrive => Math.Max(0, Usage - UsageInDrive);
}

/// <summary>Yüklenmesi yarıda kalmış bir oturum. Aynı adresten devam edilebilir.</summary>
public sealed record ResumableUpload(string ResumeUri, string FilePath, long TotalBytes, long BytesSent);

/// <summary>
/// Drive API v3 üzerinden yükleme, paylaşma ve yayını kapatma.
/// </summary>
/// <remarks>
/// Yükleme her zaman <c>uploadType=resumable</c> ile yapılır: büyük dosya yarıda kesilirse
/// baştan başlanmaz, sunucunun onayladığı bayttan devam edilir.
/// </remarks>
public sealed class DriveClient
{
    /// <summary>Parça boyu. Google ara parçaların 256 KiB'nin katı olmasını ister.</summary>
    public const int DefaultChunkSize = 8 * 1024 * 1024;

    /// <summary>Parça boyunun uyması gereken kat. Ara parçalar bunun tam katı olmalı.</summary>
    public const int ChunkAlignment = 256 * 1024;

    private const string UploadEndpoint = "https://www.googleapis.com/upload/drive/v3/files";
    private const string FilesEndpoint = "https://www.googleapis.com/drive/v3/files";
    private const string AboutEndpoint = "https://www.googleapis.com/drive/v3/about";

    private readonly IHttpTransport _transport;
    private readonly IAccessTokenProvider _tokens;
    private readonly ShareLedger? _ledger;
    private readonly Func<DateTimeOffset> _clock;
    private readonly int _chunkSize;
    private readonly SharedFileLog? _files;

    public DriveClient(
        IHttpTransport transport,
        IAccessTokenProvider tokens,
        ShareLedger? ledger = null,
        Func<DateTimeOffset>? clock = null,
        int chunkSize = DefaultChunkSize,
        SharedFileLog? files = null)
    {
        if (chunkSize <= 0 || chunkSize % ChunkAlignment != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Parça boyu 256 KiB'nin katı olmalı.");
        }
        _transport = transport;
        _tokens = tokens;
        _ledger = ledger;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _chunkSize = chunkSize;
        _files = files;
    }

    /// <summary>Bu istemcinin kullandığı parça boyu.</summary>
    public int ChunkSize => _chunkSize;

    /// <summary>
    /// Üç adım: sürdürülebilir yükleme, bağlantısı olana görüntüleme izni, <c>webViewLink</c>.
    /// </summary>
    public async Task<ShareResult> UploadAndShareAsync(
        string filePath,
        IProgress<UploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        FileInfo info;
        try
        {
            info = new FileInfo(filePath);
            if (!info.Exists) return ShareResult.Failed(ShareFailure.FileUnreadable, filePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return ShareResult.Failed(ShareFailure.FileUnreadable, e.Message);
        }

        string resumeUri;
        try
        {
            resumeUri = await StartUploadAsync(info.Name, info.Length, cancellationToken);
        }
        catch (DriveAuthException e)
        {
            return ShareResult.Failed(e.Failure, e.Message);
        }
        catch (DriveApiException e)
        {
            return ShareResult.Failed(e.Failure, e.Message);
        }

        return await ContinueAsync(new ResumableUpload(resumeUri, filePath, info.Length, 0), progress, cancellationToken);
    }

    /// <summary>Kesilmiş bir yüklemeyi kaldığı yerden sürdürür.</summary>
    public async Task<ShareResult> ResumeAsync(
        ResumableUpload upload,
        IProgress<UploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var confirmed = await QueryOffsetAsync(upload, cancellationToken);
            return await ContinueAsync(upload with { BytesSent = confirmed }, progress, cancellationToken);
        }
        catch (DriveAuthException e)
        {
            return ShareResult.Failed(e.Failure, e.Message);
        }
        catch (DriveApiException e)
        {
            return ShareResult.Interrupted(e.Failure, e.Message, upload.ResumeUri, upload.BytesSent);
        }
    }

    /// <summary>Sürdürülebilir yükleme oturumunu açar ve devam adresini döndürür.</summary>
    public async Task<string> StartUploadAsync(string fileName, long totalBytes, CancellationToken cancellationToken)
    {
        var metadata = JsonSerializer.Serialize(new Dictionary<string, object> { ["name"] = fileName });
        using var request = await AuthorizedAsync(
            HttpMethod.Post,
            $"{UploadEndpoint}?uploadType=resumable&fields=id,name,webViewLink",
            cancellationToken);
        request.Content = new StringContent(metadata, Encoding.UTF8, "application/json");
        request.Headers.TryAddWithoutValidation("X-Upload-Content-Type", "video/*");
        request.Headers.TryAddWithoutValidation("X-Upload-Content-Length", totalBytes.ToString());

        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response, cancellationToken);

        var location = response.Headers.Location?.ToString()
            ?? Header(response, "Location")
            ?? throw new DriveApiException(ShareFailure.ServiceError, "Sunucu devam adresi (Location) vermedi.");
        return location;
    }

    /// <summary>Sunucunun kaç baytı onayladığını sorar. Kesintiden sonraki ilk adım budur.</summary>
    public async Task<long> QueryOffsetAsync(ResumableUpload upload, CancellationToken cancellationToken)
    {
        using var request = await AuthorizedAsync(HttpMethod.Put, upload.ResumeUri, cancellationToken);
        request.Content = new ByteArrayContent(Array.Empty<byte>());
        request.Content.Headers.ContentRange = new ContentRangeHeaderValue(upload.TotalBytes);

        using var response = await SendAsync(request, cancellationToken);
        if (IsIncomplete(response)) return ConfirmedBytes(response);
        if (response.IsSuccessStatusCode) return upload.TotalBytes;
        throw await ErrorAsync(response, cancellationToken);
    }

    /// <summary>Bağlantısı olan herkese görüntüleme izni verir ve izin kimliğini döndürür.</summary>
    public async Task<string> ShareAsync(string fileId, CancellationToken cancellationToken)
    {
        // expirationTime burada kullanılamaz: Drive bu alanı yalnız user ve group izinlerinde
        // kabul eder. Süreli bağlantı bu platformda yoktur.
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["role"] = "reader",
            ["type"] = "anyone"
        });
        using var request = await AuthorizedAsync(
            HttpMethod.Post,
            $"{FilesEndpoint}/{Uri.EscapeDataString(fileId)}/permissions?fields=id",
            cancellationToken);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response, cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ReadString(json, "id") ?? "anyoneWithLink";
    }

    /// <summary>
    /// Yayını kapatır. İzin silindiği an bağlantı ölür; dosya kullanıcının Drive'ında kalır.
    /// </summary>
    public async Task<ShareResult> StopSharingAsync(ShareLink link, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = await AuthorizedAsync(
                HttpMethod.Delete,
                $"{FilesEndpoint}/{Uri.EscapeDataString(link.FileId)}/permissions/{Uri.EscapeDataString(link.PermissionId)}",
                cancellationToken);
            using var response = await SendAsync(request, cancellationToken);

            // İzin zaten yoksa amaç gerçekleşmiş sayılır.
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                throw await ErrorAsync(response, cancellationToken);
            }

            _ledger?.Remove(link.FileId);
            // Yayın kapandı, dosya durmaya devam ediyor: kayıt silinmez, yalnız durumu düşer.
            _files?.MarkUnshared(link.FileId);
            return ShareResult.Success(link);
        }
        catch (DriveAuthException e)
        {
            return ShareResult.Failed(e.Failure, e.Message);
        }
        catch (DriveApiException e)
        {
            return ShareResult.Failed(e.Failure, e.Message);
        }
    }

    /// <summary>
    /// Depolama durumunu sorar. Arayüz sabit bir rakam yazmamalı; 15 GB Gmail ve Photos ile
    /// paylaşımlıdır ve gerçekte ne kadarının boşta olduğu ancak sunucudan öğrenilir.
    /// </summary>
    public async Task<StorageQuota?> GetQuotaAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = await AuthorizedAsync(
                HttpMethod.Get,
                $"{AboutEndpoint}?fields=storageQuota",
                cancellationToken);
            using var response = await SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("storageQuota", out var quota)) return null;

            return new StorageQuota(
                Number(quota, "limit"),
                Number(quota, "usage") ?? 0,
                Number(quota, "usageInDrive") ?? 0);
        }
        catch (Exception e) when (e is DriveAuthException or DriveApiException or JsonException or HttpRequestException)
        {
            return null;
        }
    }

    private async Task<ShareResult> ContinueAsync(
        ResumableUpload upload,
        IProgress<UploadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var sent = upload.BytesSent;
        progress?.Report(new UploadProgress(sent, upload.TotalBytes));

        try
        {
            using var stream = new FileStream(upload.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[_chunkSize];

            while (sent < upload.TotalBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                stream.Seek(sent, SeekOrigin.Begin);
                var length = (int)Math.Min(_chunkSize, upload.TotalBytes - sent);
                var read = await ReadExactAsync(stream, buffer, length, cancellationToken);
                if (read == 0) throw new DriveApiException(ShareFailure.FileUnreadable, upload.FilePath);

                using var request = await AuthorizedAsync(HttpMethod.Put, upload.ResumeUri, cancellationToken);
                request.Content = new ByteArrayContent(buffer, 0, read);
                request.Content.Headers.ContentRange = new ContentRangeHeaderValue(sent, sent + read - 1, upload.TotalBytes);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                using var response = await SendAsync(request, cancellationToken);

                if (IsIncomplete(response))
                {
                    var confirmed = ConfirmedBytes(response);
                    // Sunucu daha azını onayladıysa oradan devam edilir; sonsuz döngüye
                    // düşmemek için hiç ilerlemediğinde hata verilir.
                    if (confirmed <= sent) throw new DriveApiException(ShareFailure.NetworkFailure, "Yükleme ilerlemedi.");
                    sent = confirmed;
                    progress?.Report(new UploadProgress(sent, upload.TotalBytes));
                    continue;
                }

                if (!response.IsSuccessStatusCode) throw await ErrorAsync(response, cancellationToken);

                sent += read;
                progress?.Report(new UploadProgress(sent, upload.TotalBytes));

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                return await FinishAsync(json, upload, cancellationToken);
            }

            // Bütün baytlar zaten gitmişti: dosya kimliği için durum sorulur.
            throw new DriveApiException(ShareFailure.ServiceError, "Yükleme tamamlandı ama dosya kimliği alınamadı.");
        }
        catch (OperationCanceledException)
        {
            return ShareResult.Interrupted(ShareFailure.Cancelled, "Kullanıcı iptal etti.", upload.ResumeUri, sent);
        }
        catch (DriveAuthException e)
        {
            return ShareResult.Interrupted(e.Failure, e.Message, upload.ResumeUri, sent);
        }
        catch (DriveApiException e)
        {
            return ShareResult.Interrupted(e.Failure, e.Message, upload.ResumeUri, sent);
        }
        catch (HttpRequestException e)
        {
            return ShareResult.Interrupted(ShareFailure.NetworkFailure, e.Message, upload.ResumeUri, sent);
        }
        catch (IOException e)
        {
            return ShareResult.Interrupted(MapIo(e), e.Message, upload.ResumeUri, sent);
        }
    }

    private async Task<ShareResult> FinishAsync(string json, ResumableUpload upload, CancellationToken cancellationToken)
    {
        var fileId = ReadString(json, "id")
            ?? throw new DriveApiException(ShareFailure.ServiceError, "Yanıtta dosya kimliği yok.");
        var permissionId = await ShareAsync(fileId, cancellationToken);
        var webViewLink = ReadString(json, "webViewLink") ?? await GetWebViewLinkAsync(fileId, cancellationToken);

        var link = new ShareLink(
            fileId,
            permissionId,
            webViewLink,
            Path.GetFileName(upload.FilePath),
            _clock());
        _ledger?.Add(link);
        _files?.Record(new UploadedFile(
            fileId,
            link.FileName,
            upload.TotalBytes,
            link.SharedAt,
            Shared: true,
            permissionId,
            webViewLink));
        return ShareResult.Success(link);
    }

    /// <summary>
    /// Dosyayı Drive'dan siler. Yayını kapatmaktan farklıdır: bu çağrı yeri boşaltır, geri
    /// dönüşü yoktur. Dosya zaten yoksa (404) amaç gerçekleşmiş sayılır.
    /// </summary>
    public async Task<DeleteOutcome> DeleteFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = await AuthorizedAsync(
                HttpMethod.Delete,
                $"{FilesEndpoint}/{Uri.EscapeDataString(fileId)}",
                cancellationToken);
            using var response = await SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Forget(fileId);
                return DeleteOutcome.Missing(fileId);
            }
            if (!response.IsSuccessStatusCode) throw await ErrorAsync(response, cancellationToken);

            Forget(fileId);
            return DeleteOutcome.Deleted(fileId);
        }
        catch (DriveAuthException e)
        {
            return DeleteOutcome.Failed(fileId, e.Failure, e.Message);
        }
        catch (DriveApiException e)
        {
            return DeleteOutcome.Failed(fileId, e.Failure, e.Message);
        }
    }

    /// <summary>
    /// Uygulamanın yüklediği dosyaları Drive'dan listeler. <c>drive.file</c> kapsamı
    /// kullanıcının Drive'ının geri kalanını göstermez. Ulaşılamazsa <c>null</c> döner.
    /// </summary>
    public async Task<IReadOnlyList<UploadedFile>?> ListAppFilesAsync(CancellationToken cancellationToken = default)
    {
        var found = new List<UploadedFile>();
        string? pageToken = null;

        try
        {
            do
            {
                var url = $"{FilesEndpoint}?spaces=drive&pageSize=100&fields="
                    + Uri.EscapeDataString("nextPageToken,files(id,name,size,createdTime,webViewLink,trashed,permissions(id,type,role))");
                if (pageToken is not null) url += $"&pageToken={Uri.EscapeDataString(pageToken)}";

                using var request = await AuthorizedAsync(HttpMethod.Get, url, cancellationToken);
                using var response = await SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode) throw await ErrorAsync(response, cancellationToken);

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
                {
                    foreach (var file in files.EnumerateArray())
                    {
                        var entry = ReadFile(file);
                        if (entry is not null) found.Add(entry);
                    }
                }

                pageToken = root.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
            }
            while (!string.IsNullOrEmpty(pageToken));

            return found;
        }
        catch (Exception e) when (e is DriveAuthException or DriveApiException or JsonException or HttpRequestException)
        {
            return null;
        }
    }

    private void Forget(string fileId)
    {
        _ledger?.Remove(fileId);
        _files?.Remove(fileId);
    }

    private UploadedFile? ReadFile(JsonElement file)
    {
        if (!file.TryGetProperty("id", out var id) || id.GetString() is not { Length: > 0 } fileId) return null;
        if (file.TryGetProperty("trashed", out var trashed) && trashed.ValueKind == JsonValueKind.True) return null;

        var name = file.TryGetProperty("name", out var value) ? value.GetString() ?? fileId : fileId;
        var size = Number(file, "size") ?? 0;
        var created = file.TryGetProperty("createdTime", out var time)
                      && DateTimeOffset.TryParse(time.GetString(), out var parsed)
            ? parsed
            // Zamanı bilinmiyorsa dosya yeni sayılır; saklama süresi yüzünden erken silinmesin.
            : _clock();
        var link = file.TryGetProperty("webViewLink", out var web) ? web.GetString() : null;

        string? permissionId = null;
        if (file.TryGetProperty("permissions", out var permissions) && permissions.ValueKind == JsonValueKind.Array)
        {
            foreach (var permission in permissions.EnumerateArray())
            {
                if (permission.TryGetProperty("type", out var type) && type.GetString() == "anyone")
                {
                    permissionId = permission.TryGetProperty("id", out var pid) ? pid.GetString() : "anyoneWithLink";
                    break;
                }
            }
        }

        return new UploadedFile(fileId, name, size, created, permissionId is not null, permissionId, link);
    }

    private async Task<string> GetWebViewLinkAsync(string fileId, CancellationToken cancellationToken)
    {
        using var request = await AuthorizedAsync(
            HttpMethod.Get,
            $"{FilesEndpoint}/{Uri.EscapeDataString(fileId)}?fields=webViewLink",
            cancellationToken);
        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response, cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ReadString(json, "webViewLink")
            ?? throw new DriveApiException(ShareFailure.ServiceError, "Yanıtta webViewLink yok.");
    }

    private async Task<HttpRequestMessage> AuthorizedAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        var token = await _tokens.GetAccessTokenAsync(cancellationToken);
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _transport.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new DriveApiException(ShareFailure.NetworkFailure, e.Message);
        }
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int length, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read, length - read), cancellationToken);
            if (count == 0) break;
            read += count;
        }
        return read;
    }

    private static async Task<DriveApiException> ErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var reason = ReadReason(body);
        var message = ReadMessage(body);
        return new DriveApiException(
            MapStatus(response.StatusCode, reason),
            string.IsNullOrEmpty(message) ? $"{(int)response.StatusCode} {reason}" : message);
    }

    /// <summary>HTTP durumunu ve Drive'ın <c>reason</c> alanını anlaşılır bir sonuca çevirir.</summary>
    public static ShareFailure MapStatus(HttpStatusCode status, string reason) => (status, reason) switch
    {
        (HttpStatusCode.Unauthorized, _) => ShareFailure.TokenExpired,
        (_, "storageQuotaExceeded") => ShareFailure.QuotaExceeded,
        (_, "quotaExceeded") => ShareFailure.QuotaExceeded,
        (_, "rateLimitExceeded") => ShareFailure.RateLimited,
        (_, "userRateLimitExceeded") => ShareFailure.RateLimited,
        (_, "dailyLimitExceeded") => ShareFailure.RateLimited,
        (_, "sharingRateLimitExceeded") => ShareFailure.RateLimited,
        (HttpStatusCode.TooManyRequests, _) => ShareFailure.RateLimited,
        (HttpStatusCode.Forbidden, _) => ShareFailure.NotAuthorized,
        (HttpStatusCode.InsufficientStorage, _) => ShareFailure.QuotaExceeded,
        (HttpStatusCode.RequestTimeout, _) => ShareFailure.NetworkFailure,
        (HttpStatusCode.BadGateway, _) => ShareFailure.NetworkFailure,
        (HttpStatusCode.ServiceUnavailable, _) => ShareFailure.NetworkFailure,
        (HttpStatusCode.GatewayTimeout, _) => ShareFailure.NetworkFailure,
        _ => (int)status >= 500 ? ShareFailure.ServiceError : ShareFailure.Unknown
    };

    /// <summary>Yerel dosya sistemi hatasını ayırır: yer kalmadıysa bu ayrıca söylenir.</summary>
    public static ShareFailure MapIo(IOException error)
    {
        const int WindowsDiskFull = 0x70;
        const int WindowsHandleDiskFull = 0x27;
        const int UnixNoSpace = 28;
        var code = error.HResult & 0xFFFF;
        return code is WindowsDiskFull or WindowsHandleDiskFull or UnixNoSpace
            ? ShareFailure.LocalDiskFull
            : ShareFailure.FileUnreadable;
    }

    /// <summary>308 Resume Incomplete: parça kabul edildi, yükleme sürüyor.</summary>
    public static bool IsIncomplete(HttpResponseMessage response) => (int)response.StatusCode == 308;

    /// <summary>
    /// 308 yanıtındaki <c>Range: bytes=0-N</c> başlığından sunucunun onayladığı bayt sayısı.
    /// Başlık yoksa hiçbir bayt onaylanmamış demektir.
    /// </summary>
    public static long ConfirmedBytes(HttpResponseMessage response)
    {
        var range = Header(response, "Range");
        if (string.IsNullOrEmpty(range)) return 0;

        var dash = range.LastIndexOf('-');
        if (dash < 0 || dash == range.Length - 1) return 0;
        return long.TryParse(range[(dash + 1)..], out var last) ? last + 1 : 0;
    }

    private static string? Header(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var values)) return values.FirstOrDefault();
        return response.Content.Headers.TryGetValues(name, out var contentValues)
            ? contentValues.FirstOrDefault()
            : null;
    }

    public static string ReadReason(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("error", out var error)) return string.Empty;
            if (error.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            {
                var first = errors[0];
                if (first.TryGetProperty("reason", out var reason)) return reason.GetString() ?? string.Empty;
            }
            return error.TryGetProperty("status", out var status) ? status.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    public static string ReadMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error)
                   && error.TryGetProperty("message", out var message)
                ? message.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string? ReadString(string json, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long? Number(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt64(),
            JsonValueKind.String => long.TryParse(value.GetString(), out var parsed) ? parsed : null,
            _ => null
        };
    }
}

/// <summary>Drive API'sinden dönen, sınıflandırılmış hata.</summary>
public sealed class DriveApiException : Exception
{
    public DriveApiException(ShareFailure failure, string detail) : base(detail) => Failure = failure;

    public ShareFailure Failure { get; }
}
