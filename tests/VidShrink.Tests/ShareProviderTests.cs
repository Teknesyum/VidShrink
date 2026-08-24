using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using VidShrink.Core.Share;

namespace VidShrink.Tests;

/// <summary>
/// İki paylaşım sağlayıcısının protokolü. HTTP katmanı <see cref="IHttpTransport"/> arkasında
/// ve burada sahte bir taşıyıcıyla değiştiriliyor; hiçbir test ağa çıkmaz.
/// </summary>
public sealed class ShareProviderTests
{
    // ---- Tablo -------------------------------------------------------------------------

    /// <summary>Depodaki gerçek tablo. Şema T36 ile ortak; alan adları burada da sınanır.</summary>
    private static ShareTargetTable RealTable()
    {
        var path = ShareTargetTable.Locate();
        Assert.NotNull(path);
        return ShareTargetTable.Load(path);
    }

    [Fact]
    public void TableShipsWithTheRepositoryAndCarriesBothTargets()
    {
        var table = RealTable();

        Assert.Equal(1, table.Version);
        Assert.Equal("storage.to", table.Default);
        Assert.Equal(new[] { "storage.to", "uguu.se" }, table.Targets.Select(t => t.Id));
        Assert.Same(table.Find("storage.to"), table.DefaultTarget);
    }

    [Fact]
    public void TableCarriesTheMeasuredCapsRetentionAndDeleteFlags()
    {
        var table = RealTable();

        var storage = table.Find("storage.to")!;
        Assert.Equal(26_843_545_600L, storage.MaxBytes);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, storage.RetentionDays);
        Assert.Equal(3, storage.DefaultRetentionDays);
        Assert.True(storage.CanDelete);
        Assert.True(storage.PlaysInBrowser);
        Assert.False(storage.HasFixedRetention);
        Assert.Equal("https://storage.to/api/upload/init", storage.Endpoint("init"));
        Assert.Equal("https://storage.to/api/upload/confirm", storage.Endpoint("confirm"));
        Assert.Equal("https://storage.to/api/file/{id}", storage.Endpoint("delete"));

        var uguu = table.Find("uguu.se")!;
        Assert.Equal(134_217_728L, uguu.MaxBytes);
        Assert.Empty(uguu.RetentionDays);
        Assert.Equal(3, uguu.FixedRetentionHours);
        Assert.False(uguu.CanDelete);
        Assert.True(uguu.HasFixedRetention);
        Assert.Equal("https://uguu.se/upload?output=text", uguu.Endpoint("upload"));
        Assert.Null(uguu.Endpoint("delete"));
    }

    /// <summary>
    /// Y1'in kabul kriteri: yeni bir hedef eklemek için tek satır C# değişmiyor. Sağlayıcı
    /// seçimi kimliğe değil uç nokta şekline bakar, bu yüzden JSON'a eklenen bir uguu klonu
    /// kod değişmeden çalışır.
    /// </summary>
    [Fact]
    public void ATargetAddedOnlyToTheJsonGetsAWorkingProviderWithoutCodeChanges()
    {
        var table = ShareTargetTable.Parse("""
        {
          "version": 1,
          "default": "yeni.example",
          "targets": [
            { "id": "yeni.example", "displayName": "yeni", "maxBytes": 1024,
              "retentionDays": [], "fixedRetentionHours": 6,
              "canDelete": false, "playsInBrowser": true,
              "endpoints": { "upload": "https://yeni.example/upload" } }
          ]
        }
        """);

        var target = table.DefaultTarget!;
        var provider = ShareProviderFactory.Create(target, new FakeTransport());

        Assert.IsType<MultipartUploadProvider>(provider);
        Assert.Equal("yeni.example", provider.Target.Id);
        Assert.False(provider.CanDelete);
    }

    [Fact]
    public void FactoryPicksTheProtocolFromTheEndpointShape()
    {
        var table = RealTable();
        var transport = new FakeTransport();

        Assert.IsType<PresignedUploadProvider>(ShareProviderFactory.Create(table.Find("storage.to")!, transport));
        Assert.IsType<MultipartUploadProvider>(ShareProviderFactory.Create(table.Find("uguu.se")!, transport));
        Assert.Equal(2, ShareProviderFactory.CreateAll(table, transport).Count);
    }

    [Fact]
    public void FactoryRefusesATargetWithNoRecognisedEndpoints()
    {
        var target = new ShareTarget { Id = "bos", DisplayName = "bos" };
        Assert.Throws<InvalidOperationException>(() => ShareProviderFactory.Create(target, new FakeTransport()));
    }

    // ---- storage.to: üç adımlı akış -----------------------------------------------------

    [Fact]
    public async Task PresignedUploadWalksInitPutConfirmAndReturnsTheShareUrl()
    {
        using var clip = new TempFile(2048);
        var transport = FakeTransport.StorageToSuccess();
        var provider = new PresignedUploadProvider(RealTable().Find("storage.to")!, transport, "vt-test");

        var progress = new Recorder();
        var result = await provider.UploadAsync(clip.Path, retentionDays: 1, progress: progress);

        Assert.True(result.Ok);
        Assert.Equal(3, transport.Requests.Count);

        var init = transport.Requests[0];
        Assert.Equal(HttpMethod.Post, init.Method);
        Assert.Equal("https://storage.to/api/upload/init", init.Url);
        Assert.Equal("vt-test", init.Header("X-Visitor-Token"));
        var initBody = JsonDocument.Parse(init.Body).RootElement;
        Assert.Equal(clip.Name, initBody.GetProperty("filename").GetString());
        Assert.Equal(2048, initBody.GetProperty("size").GetInt64());
        Assert.Equal("video/mp4", initBody.GetProperty("content_type").GetString());
        Assert.Equal(1, initBody.GetProperty("expiry_days").GetInt32());

        var put = transport.Requests[1];
        Assert.Equal(HttpMethod.Put, put.Method);
        Assert.Equal("https://r2.example/presigned", put.Url);
        Assert.Equal(2048, put.ByteCount);
        // İmza Content-Type'ı da kapsıyor: PUT init'te bildirilenle birebir aynı olmalı.
        Assert.Equal("video/mp4", put.ContentType);

        var confirm = JsonDocument.Parse(transport.Requests[2].Body).RootElement;
        Assert.Equal("r2/key/abc", confirm.GetProperty("r2_key").GetString());
        Assert.Equal(1, confirm.GetProperty("expiry_days").GetInt32());

        var link = result.Link!;
        Assert.Equal("storage.to", link.TargetId);
        Assert.Equal("nLcjsZGY0", link.FileId);
        Assert.Equal("owner-abc", link.OwnerToken);
        Assert.True(link.CanDelete);
        Assert.NotNull(link.ExpiresAt);

        Assert.NotEmpty(progress.Reports);
        Assert.Equal(1.0, progress.Reports[^1].Fraction);
    }

    /// <summary>
    /// Kullanıcıya verilecek bağlantı <c>file.url</c>'dir. CDN adresi imzalı ve ~30 dakikada
    /// sona eriyor; paylaşılırsa bağlantı yarım saat sonra sessizce ölür.
    /// </summary>
    [Fact]
    public async Task SharedLinkIsThePageUrlNeverTheSignedCdnUrl()
    {
        using var clip = new TempFile(512);
        var transport = FakeTransport.StorageToSuccess();
        var provider = new PresignedUploadProvider(RealTable().Find("storage.to")!, transport, "vt-test");

        var result = await provider.UploadAsync(clip.Path);

        Assert.Equal("https://storage.to/nLcjsZGY0", result.Link!.Url);
        Assert.DoesNotContain("cdn.storagetobox.com", result.Link.Url);
        Assert.DoesNotContain("sig=", result.Link.Url);
    }

    [Fact]
    public async Task RetentionOutsideTheOfferedDaysFallsBackToTheDefault()
    {
        using var clip = new TempFile(64);
        var transport = FakeTransport.StorageToSuccess();
        var provider = new PresignedUploadProvider(RealTable().Find("storage.to")!, transport, "vt-test");

        await provider.UploadAsync(clip.Path, retentionDays: 99);

        var body = JsonDocument.Parse(transport.Requests[0].Body).RootElement;
        Assert.Equal(3, body.GetProperty("expiry_days").GetInt32());
    }

    [Fact]
    public async Task DeleteSendsTheOwnerTokenAndReportsSuccess()
    {
        var transport = new FakeTransport();
        transport.Enqueue(HttpStatusCode.OK, """{"success":true}""");
        var provider = new PresignedUploadProvider(RealTable().Find("storage.to")!, transport, "vt-test");

        var link = new ShareLink("storage.to", "nLcjsZGY0", "https://storage.to/nLcjsZGY0",
            "klip.mp4", DateTimeOffset.UtcNow, OwnerToken: "owner-abc");

        var result = await provider.DeleteAsync(link);

        Assert.True(result.Ok);
        var request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("https://storage.to/api/file/nLcjsZGY0", request.Url);
        Assert.Equal("Owner owner-abc", request.Header("Authorization"));
    }

    [Fact]
    public async Task DeleteWithoutAnOwnerTokenNeverTouchesTheNetwork()
    {
        var transport = new FakeTransport();
        var provider = new PresignedUploadProvider(RealTable().Find("storage.to")!, transport, "vt-test");

        var link = new ShareLink("storage.to", "abc", "https://storage.to/abc", "klip.mp4", DateTimeOffset.UtcNow);
        var result = await provider.DeleteAsync(link);

        Assert.False(result.Ok);
        Assert.Equal(ShareFailure.TokenExpired, result.Failure);
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task AFailedInitStopsBeforeSendingAnyBytes()
    {
        using var clip = new TempFile(4096);
        var transport = new FakeTransport();
        transport.Enqueue(HttpStatusCode.ServiceUnavailable, "maintenance");
        var provider = new PresignedUploadProvider(RealTable().Find("storage.to")!, transport, "vt-test");

        var result = await provider.UploadAsync(clip.Path);

        Assert.Equal(ShareFailure.ServiceError, result.Failure);
        Assert.Single(transport.Requests);
        Assert.Contains("hizmet vermiyor", result.Message);
    }

    // ---- uguu.se: tek adımlı akış -------------------------------------------------------

    [Fact]
    public async Task MultipartUploadPostsOnceAndReadsTheUrlFromPlainText()
    {
        using var clip = new TempFile(1024);
        var transport = new FakeTransport();
        transport.Enqueue(HttpStatusCode.OK, "https://d.uguu.se/AbCdEfGh.mp4\n");
        var provider = new MultipartUploadProvider(RealTable().Find("uguu.se")!, transport);

        var progress = new Recorder();
        var result = await provider.UploadAsync(clip.Path, progress: progress);

        Assert.True(result.Ok);
        var request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://uguu.se/upload?output=text", request.Url);
        Assert.Contains("files[]", request.Body);

        var link = result.Link!;
        Assert.Equal("https://d.uguu.se/AbCdEfGh.mp4", link.Url);
        Assert.Equal("AbCdEfGh.mp4", link.FileId);
        Assert.Null(link.OwnerToken);
        Assert.False(link.CanDelete);
        // Ömür sabit 3 saat; sunucu söylemiyor, tablodan kuruluyor.
        Assert.NotNull(link.ExpiresAt);
        Assert.InRange((link.ExpiresAt!.Value - DateTimeOffset.UtcNow).TotalMinutes, 175, 181);
        Assert.NotEmpty(progress.Reports);
    }

    /// <summary>
    /// Silme desteklenmiyor. İstisna atılmaz — yetenek bayrağı <c>false</c> döner ve arayüz
    /// düğmeyi ona bakıp gizler.
    /// </summary>
    [Fact]
    public async Task UnsupportedDeleteReportsAFlagInsteadOfThrowing()
    {
        var transport = new FakeTransport();
        var provider = new MultipartUploadProvider(RealTable().Find("uguu.se")!, transport);

        Assert.False(provider.CanDelete);

        var link = new ShareLink("uguu.se", "AbCdEfGh.mp4", "https://d.uguu.se/AbCdEfGh.mp4",
            "klip.mp4", DateTimeOffset.UtcNow);
        var result = await provider.DeleteAsync(link);

        Assert.False(result.Ok);
        Assert.Contains("3 saatlik otomatik silme", result.Message);
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task AMultipartServerErrorIsClassifiedNotThrown()
    {
        using var clip = new TempFile(256);
        var transport = new FakeTransport();
        transport.Enqueue(HttpStatusCode.InternalServerError, "boom");
        var provider = new MultipartUploadProvider(RealTable().Find("uguu.se")!, transport);

        var result = await provider.UploadAsync(clip.Path);

        Assert.Equal(ShareFailure.ServiceError, result.Failure);
        Assert.Contains("boom", result.Detail);
    }

    // ---- Ortak davranış ------------------------------------------------------------------

    [Theory]
    [InlineData("storage.to")]
    [InlineData("uguu.se")]
    public async Task OversizeFilesAreRefusedBeforeTheFirstRequest(string id)
    {
        using var clip = new TempFile(4096);
        var table = ShareTargetTable.Parse(TinyCapTable());
        var transport = new FakeTransport();
        var provider = ShareProviderFactory.Create(table.Find(id)!, transport);

        var result = await provider.UploadAsync(clip.Path);

        Assert.Equal(ShareFailure.FileTooLarge, result.Failure);
        Assert.Empty(transport.Requests);
    }

    [Theory]
    [InlineData("storage.to")]
    [InlineData("uguu.se")]
    public async Task ANetworkFailureBecomesAnActionableMessage(string id)
    {
        using var clip = new TempFile(128);
        var transport = new FakeTransport();
        transport.Throw(new HttpRequestException("no route", new SocketException((int)SocketError.HostNotFound)));
        var provider = ShareProviderFactory.Create(RealTable().Find(id)!, transport);

        var result = await provider.UploadAsync(clip.Path);

        Assert.Equal(ShareFailure.NetworkFailure, result.Failure);
        Assert.Contains("çözülemedi", result.Message);
    }

    [Theory]
    [InlineData("storage.to")]
    [InlineData("uguu.se")]
    public async Task CancellationStopsTheUploadAndReportsItAsCancelled(string id)
    {
        using var clip = new TempFile(512);
        using var cancellation = new CancellationTokenSource();
        var transport = new FakeTransport();
        transport.Throw(new OperationCanceledException(cancellation.Token));
        var provider = ShareProviderFactory.Create(RealTable().Find(id)!, transport);

        cancellation.Cancel();
        var result = await provider.UploadAsync(clip.Path, cancellationToken: cancellation.Token);

        Assert.Equal(ShareFailure.Cancelled, result.Failure);
    }

    [Theory]
    [InlineData("storage.to")]
    [InlineData("uguu.se")]
    public async Task EveryRequestNamesTheApplicationAndItsVersion(string id)
    {
        using var clip = new TempFile(128);
        var transport = id == "storage.to"
            ? FakeTransport.StorageToSuccess()
            : Plain("https://d.uguu.se/x.mp4");
        var provider = ShareProviderFactory.Create(RealTable().Find(id)!, transport);

        await provider.UploadAsync(clip.Path);

        Assert.NotEmpty(transport.Requests);
        Assert.All(transport.Requests, r =>
        {
            var agent = r.Header("User-Agent");
            Assert.NotNull(agent);
            Assert.StartsWith("VidShrink/", agent);
            Assert.Contains("+https://", agent);
        });
    }

    [Theory]
    [InlineData("storage.to")]
    [InlineData("uguu.se")]
    public async Task HealthCheckIsASingleHeadRequest(string id)
    {
        var transport = new FakeTransport();
        transport.Enqueue(HttpStatusCode.MethodNotAllowed, string.Empty);
        var provider = ShareProviderFactory.Create(RealTable().Find(id)!, transport);

        var result = await provider.CheckHealthAsync();

        Assert.True(result.Ok);
        var request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Head, request.Method);
        Assert.Equal(string.Empty, request.Body);
    }

    [Fact]
    public async Task ADeadEndpointComesBackWithAReasonTheInterfaceCanShow()
    {
        var transport = new FakeTransport();
        transport.Throw(new HttpRequestException("dns", new SocketException((int)SocketError.HostNotFound)));
        var provider = ShareProviderFactory.Create(RealTable().Find("uguu.se")!, transport);

        var result = await provider.CheckHealthAsync();

        Assert.False(result.Ok);
        Assert.Equal(ShareFailure.NetworkFailure, result.Failure);
        Assert.Contains("diğer hedefi deneyin", result.Message);
    }

    // ---- Sınıflandırma -------------------------------------------------------------------

    [Fact]
    public void OversizeMessageNamesTheCapAndTheTargetThatWouldFit()
    {
        var table = RealTable();
        var uguu = table.Find("uguu.se")!;

        var diagnosis = ShareErrorClassifier.CheckSize(uguu, 200L * 1024 * 1024, table);

        Assert.NotNull(diagnosis);
        Assert.Equal(ShareFailure.FileTooLarge, diagnosis!.Failure);
        Assert.Equal("storage.to", diagnosis.SuggestedTargetId);
        Assert.Contains("128 MB", diagnosis.Message);
        Assert.Contains("storage.to", diagnosis.Message);
    }

    [Fact]
    public void RateLimitCarriesTheServersOwnRetryAfter()
    {
        var target = RealTable().Find("storage.to")!;
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.TryAddWithoutValidation("Retry-After", "120");

        var diagnosis = ShareErrorClassifier.FromResponse(target, response, "slow down", "init");

        Assert.Equal(ShareFailure.RateLimited, diagnosis.Failure);
        Assert.Equal(TimeSpan.FromSeconds(120), diagnosis.RetryAfter);
        Assert.Contains("2 dakika", diagnosis.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, ShareFailure.NotAuthorized)]
    [InlineData(HttpStatusCode.Gone, ShareFailure.TokenExpired)]
    [InlineData(HttpStatusCode.NotFound, ShareFailure.TokenExpired)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, ShareFailure.FileTooLarge)]
    [InlineData(HttpStatusCode.InsufficientStorage, ShareFailure.QuotaExceeded)]
    [InlineData(HttpStatusCode.BadGateway, ShareFailure.ServiceError)]
    [InlineData(HttpStatusCode.BadRequest, ShareFailure.ServiceError)]
    public void StatusCodesMapToActionableFailures(HttpStatusCode status, ShareFailure expected)
    {
        var target = RealTable().Find("storage.to")!;
        using var response = new HttpResponseMessage(status);

        var diagnosis = ShareErrorClassifier.FromResponse(target, response, "detay", "init");

        Assert.Equal(expected, diagnosis.Failure);
        Assert.NotEmpty(diagnosis.Message);
    }

    /// <summary>
    /// Y4: <c>Unknown</c>'a düşen oran ölçülüyor. Tablodaki durumlarda oran sıfır olmalı;
    /// %10'u geçmesi tablonun yetersiz olduğu anlamına gelir.
    /// </summary>
    [Fact]
    public void KnownStatusCodesLeaveTheUnknownRateAtZero()
    {
        var target = RealTable().Find("storage.to")!;
        ShareErrorClassifier.ResetCounters();
        try
        {
            HttpStatusCode[] known =
            {
                HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound,
                HttpStatusCode.Gone, HttpStatusCode.RequestTimeout, HttpStatusCode.TooManyRequests,
                HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.InsufficientStorage,
                HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity,
                HttpStatusCode.InternalServerError, HttpStatusCode.BadGateway,
                HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout
            };

            foreach (var status in known)
            {
                using var response = new HttpResponseMessage(status);
                ShareErrorClassifier.FromResponse(target, response, string.Empty, "init");
            }

            Assert.Equal(known.Length, ShareErrorClassifier.ClassifiedCount);
            Assert.Equal(0, ShareErrorClassifier.UnknownCount);
            Assert.Equal(0.0, ShareErrorClassifier.UnknownRate);

            using var teapot = new HttpResponseMessage((HttpStatusCode)418);
            Assert.Equal(ShareFailure.Unknown,
                ShareErrorClassifier.FromResponse(target, teapot, string.Empty, "init").Failure);
            Assert.Equal(1, ShareErrorClassifier.UnknownCount);
        }
        finally
        {
            ShareErrorClassifier.ResetCounters();
        }
    }

    // ---- Kayıt defteri --------------------------------------------------------------------

    [Fact]
    public void LedgerKeepsTheOwnerTokenSoTheShareCanBeClosedAfterARestart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vidshrink-ledger-{Guid.NewGuid():N}.json");
        try
        {
            var ledger = new ShareLedger(path);
            var link = new ShareLink("storage.to", "abc", "https://storage.to/abc", "klip.mp4",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), "owner-abc");

            ledger.Add(link);
            var reloaded = Assert.Single(new ShareLedger(path).Load());

            Assert.Equal("owner-abc", reloaded.OwnerToken);
            Assert.True(reloaded.CanDelete);
            Assert.Equal("https://storage.to/abc", reloaded.Url);

            ledger.Remove("abc");
            Assert.Empty(new ShareLedger(path).Load());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ---- Yardımcılar ----------------------------------------------------------------------

    private static string TinyCapTable() => """
    {
      "version": 1,
      "default": "storage.to",
      "targets": [
        { "id": "storage.to", "displayName": "storage.to", "maxBytes": 100,
          "retentionDays": [1,2,3], "defaultRetentionDays": 3, "canDelete": true,
          "playsInBrowser": true,
          "endpoints": { "init": "https://storage.to/api/upload/init",
                         "confirm": "https://storage.to/api/upload/confirm",
                         "delete": "https://storage.to/api/file/{id}" } },
        { "id": "uguu.se", "displayName": "uguu.se", "maxBytes": 100,
          "retentionDays": [], "fixedRetentionHours": 3, "canDelete": false,
          "playsInBrowser": true,
          "endpoints": { "upload": "https://uguu.se/upload?output=text" } }
      ]
    }
    """;

    private static FakeTransport Plain(string url)
    {
        var transport = new FakeTransport();
        transport.Enqueue(HttpStatusCode.OK, url);
        return transport;
    }

    /// <summary>İlerleme bildirimlerini olduğu iş parçacığında kaydeder; yarış yok.</summary>
    private sealed class Recorder : IProgress<UploadProgress>
    {
        public List<UploadProgress> Reports { get; } = new();

        public void Report(UploadProgress value) => Reports.Add(value);
    }

    /// <summary>Testin süresince yaşayan, verilen boyutta bir geçici dosya.</summary>
    private sealed class TempFile : IDisposable
    {
        public TempFile(int bytes)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"vidshrink-share-{Guid.NewGuid():N}.mp4");
            File.WriteAllBytes(Path, Enumerable.Range(0, bytes).Select(i => (byte)(i % 251)).ToArray());
        }

        public string Path { get; }

        public string Name => System.IO.Path.GetFileName(Path);

        public void Dispose()
        {
            if (File.Exists(Path)) File.Delete(Path);
        }
    }

    /// <summary>Kaydedilmiş bir istek. Gövde metin olarak tutulur.</summary>
    private sealed record Sent(HttpMethod Method, string Url, string Body, int ByteCount, string? ContentType,
        IReadOnlyDictionary<string, string> Headers)
    {
        public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Ağa çıkmayan taşıyıcı. Sıraya konan yanıtları verir ve istekleri kaydeder.</summary>
    private sealed class FakeTransport : IHttpTransport
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();
        private readonly List<Sent> _requests = new();
        private Exception? _throw;

        public IReadOnlyList<Sent> Requests => _requests;

        public void Enqueue(HttpStatusCode status, string body) => _responses.Enqueue((status, body));

        public void Throw(Exception exception) => _throw = exception;

        public static FakeTransport StorageToSuccess()
        {
            var transport = new FakeTransport();
            transport.Enqueue(HttpStatusCode.OK,
                """{"upload_url":"https://r2.example/presigned","r2_key":"r2/key/abc"}""");
            transport.Enqueue(HttpStatusCode.OK, string.Empty);
            transport.Enqueue(HttpStatusCode.OK, $$"""
            {
              "owner_token": "owner-abc",
              "file": {
                "id": "nLcjsZGY0",
                "url": "https://storage.to/nLcjsZGY0",
                "expires_at": "{{DateTimeOffset.UtcNow.AddDays(1):O}}",
                "cdn_url": "https://cdn.storagetobox.com/x.mp4?expires=1&sig=deadbeef"
              }
            }
            """);
            return transport;
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = string.Empty;
            var byteCount = 0;
            string? contentType = null;
            if (request.Content is not null)
            {
                contentType = request.Content.Headers.ContentType?.MediaType;
                var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                byteCount = bytes.Length;
                body = Encoding.UTF8.GetString(bytes);
            }

            var headers = request.Headers
                .ToDictionary(h => h.Key, h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase);

            _requests.Add(new Sent(request.Method, request.RequestUri!.ToString(), body, byteCount, contentType, headers));

            if (_throw is not null) throw _throw;

            var (status, text) = _responses.Count > 0
                ? _responses.Dequeue()
                : (HttpStatusCode.OK, string.Empty);

            return new HttpResponseMessage(status) { Content = new StringContent(text) };
        }
    }
}
