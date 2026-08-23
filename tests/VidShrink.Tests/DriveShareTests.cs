using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using VidShrink.Core.Share;

namespace VidShrink.Tests;

/// <summary>
/// T31: Drive paylaşım katmanı. Hiçbir sınama ağa çıkmaz ve gerçek bir Drive hesabı istemez —
/// HTTP katmanı <see cref="IHttpTransport"/> arkasında ve burada sahte bir taşıyıcıyla
/// değiştiriliyor.
/// </summary>
public class DriveShareTests
{
    // ---------- K1: PKCE ve yetkilendirme adresi ----------

    [Fact]
    public void Pkce_verifier_is_base64url_and_long_enough()
    {
        var pkce = DriveAuth.CreatePkce();

        // RFC 7636: 43-128 karakter, yalnız unreserved küme.
        Assert.InRange(pkce.Verifier.Length, 43, 128);
        Assert.All(pkce.Verifier, c => Assert.True(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or '~'));
    }

    [Fact]
    public void Pkce_challenge_matches_rfc7636_vector()
    {
        // RFC 7636 Ek B'deki örnek. Meydan okuma S256 ile üretiliyorsa bu değeri verir.
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", DriveAuth.ChallengeFor(verifier));
    }

    [Fact]
    public void Pkce_challenge_is_sha256_of_verifier()
    {
        var pkce = DriveAuth.CreatePkce();
        var expected = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(pkce.Verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.Equal(expected, pkce.Challenge);
    }

    [Fact]
    public void Pkce_pairs_are_not_reused()
    {
        Assert.NotEqual(DriveAuth.CreatePkce().Verifier, DriveAuth.CreatePkce().Verifier);
    }

    [Fact]
    public void Authorization_url_asks_for_drive_file_only()
    {
        var pkce = new PkcePair("verifier", "challenge");
        var url = DriveAuth.BuildAuthorizationUrl("istemci.apps.googleusercontent.com", "http://127.0.0.1:9999/", pkce, "durum");

        Assert.Contains("scope=https%3a%2f%2fwww.googleapis.com%2fauth%2fdrive.file", url.ToLowerInvariant());
        Assert.Contains("code_challenge=challenge", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("access_type=offline", url);
        // Kapsam genişletilmiş olmasın: drive.file dışında hiçbir Drive kapsamı geçmemeli.
        Assert.DoesNotContain("auth%2fdrive&", url.ToLowerInvariant());
        Assert.DoesNotContain("drive.readonly", url);
        Assert.DoesNotContain("drive.metadata", url);
        Assert.Equal("https://www.googleapis.com/auth/drive.file", DriveAuth.Scope);
    }

    [Fact]
    public void Authorization_url_carries_no_client_secret()
    {
        var url = DriveAuth.BuildAuthorizationUrl("istemci", "http://127.0.0.1:9999/", DriveAuth.CreatePkce(), "durum");

        Assert.DoesNotContain("client_secret", url);
    }

    [Fact]
    public async Task Token_exchange_sends_verifier_and_no_client_secret()
    {
        var transport = new FakeTransport();
        transport.Enqueue(Json(HttpStatusCode.OK,
            """{"access_token":"erisim","refresh_token":"yenileme","expires_in":3599}"""));

        var tokens = await DriveAuth.ExchangeCodeAsync(
            transport,
            "istemci",
            new AuthorizationCode("kod", "dogrulayici", "http://127.0.0.1:9999/"),
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        var body = transport.Sent[0].Body;
        Assert.Contains("code_verifier=dogrulayici", body);
        Assert.Contains("grant_type=authorization_code", body);
        Assert.DoesNotContain("client_secret", body);
        Assert.Equal("yenileme", tokens.RefreshToken);
        Assert.Equal("erisim", tokens.AccessToken);
        Assert.Equal(new DateTimeOffset(2026, 8, 24, 12, 59, 59, TimeSpan.Zero), tokens.AccessExpiresAt);
    }

    [Fact]
    public void Share_sources_contain_no_client_secret()
    {
        var folder = ShareSourceFolder();
        Assert.NotNull(folder);

        foreach (var file in Directory.GetFiles(folder!, "*.cs"))
        {
            var text = File.ReadAllText(file);
            var line = text.Split('\n').FirstOrDefault(l =>
                l.Contains("client_secret", StringComparison.Ordinal) && !l.TrimStart().StartsWith("//", StringComparison.Ordinal));
            Assert.True(line is null, $"{Path.GetFileName(file)} içinde client_secret geçiyor: {line}");
        }
    }

    // ---------- K2: jeton saklama ----------

    [Fact]
    public void Platform_store_persists_only_on_windows()
    {
        var store = TokenStore.ForCurrentPlatform(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        // Windows dışında karşılığı olmadığı için jeton hiç saklanmaz: her oturumda yeniden izin.
        Assert.Equal(OperatingSystem.IsWindows(), store.Persists);
    }

    [Fact]
    public void Ephemeral_store_forgets_after_clear()
    {
        var store = new EphemeralTokenStore();
        store.Save(new DriveTokens("yenileme", "erisim", DateTimeOffset.UtcNow.AddHours(1)));

        Assert.NotNull(store.Load());
        store.Clear();
        Assert.Null(store.Load());
        Assert.False(store.Persists);
    }

    [Fact]
    public void Dpapi_file_holds_no_plain_text_token()
    {
        if (!OperatingSystem.IsWindows()) return;

        var path = Path.Combine(Path.GetTempPath(), $"vidshrink-jeton-{Guid.NewGuid():N}.bin");
        try
        {
            var store = new DpapiTokenStore(path);
            store.Save(new DriveTokens("COK-GIZLI-YENILEME-JETONU", "COK-GIZLI-ERISIM", DateTimeOffset.UtcNow.AddHours(1)));

            var raw = File.ReadAllBytes(path);
            var asText = Encoding.UTF8.GetString(raw);
            var asUtf16 = Encoding.Unicode.GetString(raw);

            Assert.DoesNotContain("COK-GIZLI-YENILEME-JETONU", asText, StringComparison.Ordinal);
            Assert.DoesNotContain("COK-GIZLI-YENILEME-JETONU", asUtf16, StringComparison.Ordinal);
            Assert.DoesNotContain("refresh_token", asText, StringComparison.Ordinal);

            // Aynı kullanıcı geri okuyabilmeli.
            Assert.Equal("COK-GIZLI-YENILEME-JETONU", store.Load()!.RefreshToken);

            store.Clear();
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Session_refreshes_expired_access_token_and_saves_it()
    {
        var now = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        var store = new EphemeralTokenStore();
        store.Save(new DriveTokens("yenileme", "eski", now.AddSeconds(-1)));

        var transport = new FakeTransport();
        transport.Enqueue(Json(HttpStatusCode.OK, """{"access_token":"taze","expires_in":3600}"""));

        var session = new DriveSession(transport, "istemci", store, () => now);
        var token = await session.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("taze", token);
        Assert.Contains("grant_type=refresh_token", transport.Sent[0].Body);
        Assert.DoesNotContain("client_secret", transport.Sent[0].Body);
        // Yenileme jetonu korunur, taze erişim jetonu saklanır.
        Assert.Equal("yenileme", store.Load()!.RefreshToken);
        Assert.Equal("taze", store.Load()!.AccessToken);
    }

    [Fact]
    public async Task Session_reuses_a_still_valid_access_token()
    {
        var now = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        var store = new EphemeralTokenStore();
        store.Save(new DriveTokens("yenileme", "gecerli", now.AddMinutes(30)));

        var transport = new FakeTransport();
        var session = new DriveSession(transport, "istemci", store, () => now);

        Assert.Equal("gecerli", await session.GetAccessTokenAsync(CancellationToken.None));
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task Revoked_refresh_token_becomes_token_expired()
    {
        var transport = new FakeTransport();
        transport.Enqueue(Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));

        var store = new EphemeralTokenStore();
        store.Save(new DriveTokens("olu-jeton", null, DateTimeOffset.UtcNow));
        var session = new DriveSession(transport, "istemci", store);

        var error = await Assert.ThrowsAsync<DriveAuthException>(
            () => session.GetAccessTokenAsync(CancellationToken.None));
        Assert.Equal(ShareFailure.TokenExpired, error.Failure);
    }

    // ---------- K3: sürdürülebilir yükleme, paylaşma, bağlantı ----------

    [Fact]
    public async Task Upload_sends_aligned_chunks_and_returns_the_web_view_link()
    {
        var path = TempFile(3 * Alignment);
        try
        {
            var transport = new FakeTransport();
            transport.Enqueue(WithLocation(HttpStatusCode.OK, "https://upload.example/oturum/1"));
            transport.Enqueue(WithRange((HttpStatusCode)308, Alignment - 1));
            transport.Enqueue(WithRange((HttpStatusCode)308, 2 * Alignment - 1));
            transport.Enqueue(Json(HttpStatusCode.OK,
                """{"id":"dosya-1","webViewLink":"https://drive.google.com/file/d/dosya-1/view"}"""));
            transport.Enqueue(Json(HttpStatusCode.OK, """{"id":"anyoneWithLink"}"""));

            var client = new DriveClient(transport, new FixedToken("erisim"), chunkSize: Alignment);
            var seen = new List<UploadProgress>();
            var result = await client.UploadAndShareAsync(path, new SyncProgress(seen));

            Assert.True(result.Ok, result.Detail);
            Assert.Equal("dosya-1", result.Link!.FileId);
            Assert.Equal("anyoneWithLink", result.Link.PermissionId);
            Assert.Equal("https://drive.google.com/file/d/dosya-1/view", result.Link.WebViewLink);

            // Oturum açılışı sürdürülebilir olmalı.
            Assert.Contains("uploadType=resumable", transport.Sent[0].Url);

            // Parçalar sırayla ve doğru aralıklarla gitmeli.
            Assert.Equal($"bytes 0-{Alignment - 1}/{3 * Alignment}", transport.Sent[1].ContentRange);
            Assert.Equal($"bytes {Alignment}-{2 * Alignment - 1}/{3 * Alignment}", transport.Sent[2].ContentRange);
            Assert.Equal($"bytes {2 * Alignment}-{3 * Alignment - 1}/{3 * Alignment}", transport.Sent[3].ContentRange);

            // İlerleme çağırana bildirilmeli ve sonunda tamamlanmalı.
            Assert.Equal(1.0, seen[^1].Fraction);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Sharing_grants_reader_to_anyone_and_never_sets_an_expiry()
    {
        var transport = new FakeTransport();
        transport.Enqueue(Json(HttpStatusCode.OK, """{"id":"izin-1"}"""));

        var client = new DriveClient(transport, new FixedToken("erisim"));
        var permissionId = await client.ShareAsync("dosya-1", CancellationToken.None);

        Assert.Equal("izin-1", permissionId);
        Assert.Contains("\"role\":\"reader\"", transport.Sent[0].Body);
        Assert.Contains("\"type\":\"anyone\"", transport.Sent[0].Body);
        // Drive expirationTime'ı yalnız user/group izinlerinde kabul eder; bağlantı izninde değil.
        Assert.DoesNotContain("expirationTime", transport.Sent[0].Body);
        Assert.False(ShareLink.SupportsExpiry);
    }

    [Fact]
    public void Chunk_size_must_stay_aligned()
    {
        Assert.Equal(0, DriveClient.DefaultChunkSize % DriveClient.ChunkAlignment);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DriveClient(new FakeTransport(), new FixedToken("t"), chunkSize: 1000));
    }

    // ---------- K4: yayını kapatma ----------

    [Fact]
    public async Task Stop_sharing_deletes_the_permission_and_drops_the_ledger_row()
    {
        var ledgerPath = Path.Combine(Path.GetTempPath(), $"vidshrink-paylasim-{Guid.NewGuid():N}.json");
        try
        {
            var link = new ShareLink("dosya-1", "izin-1", "https://drive.google.com/x", "video.mp4", DateTimeOffset.UtcNow);
            var ledger = new ShareLedger(ledgerPath);
            ledger.Add(link);
            Assert.Single(ledger.Load());

            var transport = new FakeTransport();
            transport.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent) { Content = new StringContent("") });

            var client = new DriveClient(transport, new FixedToken("erisim"), ledger);
            var result = await client.StopSharingAsync(link);

            Assert.True(result.Ok, result.Detail);
            Assert.Equal("DELETE", transport.Sent[0].Method);
            Assert.EndsWith("/files/dosya-1/permissions/izin-1", transport.Sent[0].Url);
            Assert.Empty(ledger.Load());
        }
        finally
        {
            if (File.Exists(ledgerPath)) File.Delete(ledgerPath);
        }
    }

    [Fact]
    public void Ledger_survives_a_restart_so_sharing_can_be_stopped_later()
    {
        var ledgerPath = Path.Combine(Path.GetTempPath(), $"vidshrink-paylasim-{Guid.NewGuid():N}.json");
        try
        {
            new ShareLedger(ledgerPath).Add(
                new ShareLink("dosya-9", "izin-9", "https://drive.google.com/y", "kedi.mp4", DateTimeOffset.UtcNow));

            // Yeni bir nesne — yani uygulama kapanıp açılmış gibi.
            var reloaded = new ShareLedger(ledgerPath).Load();

            Assert.Single(reloaded);
            Assert.Equal("izin-9", reloaded[0].PermissionId);
            Assert.Equal("kedi.mp4", reloaded[0].FileName);
        }
        finally
        {
            if (File.Exists(ledgerPath)) File.Delete(ledgerPath);
        }
    }

    // ---------- K5: hatalar yutulmayacak ----------

    [Fact]
    public async Task Full_drive_reports_quota_exceeded_with_the_server_message()
    {
        var path = TempFile(Alignment);
        try
        {
            const string body = """
                {"error":{"code":403,"message":"The user's Drive storage quota has been exceeded.",
                "errors":[{"reason":"storageQuotaExceeded","message":"The user's Drive storage quota has been exceeded."}]}}
                """;
            var transport = new FakeTransport();
            transport.Enqueue(WithLocation(HttpStatusCode.OK, "https://upload.example/oturum/2"));
            transport.Enqueue(Json(HttpStatusCode.Forbidden, body));

            var client = new DriveClient(transport, new FixedToken("erisim"), chunkSize: Alignment);
            var result = await client.UploadAndShareAsync(path);

            Assert.False(result.Ok);
            Assert.Equal(ShareFailure.QuotaExceeded, result.Failure);
            // Sunucunun kendi metni aynen taşınır; "yükleme başarısız" demek yetmez.
            Assert.Contains("storage quota has been exceeded", result.Detail);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(401, "authError", ShareFailure.TokenExpired)]
    [InlineData(403, "storageQuotaExceeded", ShareFailure.QuotaExceeded)]
    [InlineData(403, "rateLimitExceeded", ShareFailure.RateLimited)]
    [InlineData(403, "userRateLimitExceeded", ShareFailure.RateLimited)]
    [InlineData(403, "insufficientFilePermissions", ShareFailure.NotAuthorized)]
    [InlineData(429, "", ShareFailure.RateLimited)]
    [InlineData(503, "", ShareFailure.NetworkFailure)]
    [InlineData(500, "", ShareFailure.ServiceError)]
    public void Each_server_condition_maps_to_its_own_result(int status, string reason, ShareFailure expected)
    {
        Assert.Equal(expected, DriveClient.MapStatus((HttpStatusCode)status, reason));
    }

    [Fact]
    public void A_full_local_disk_is_told_apart_from_an_unreadable_file()
    {
        Assert.Equal(ShareFailure.LocalDiskFull, DriveClient.MapIo(new IOException("dolu", unchecked((int)0x80070070))));
        Assert.Equal(ShareFailure.FileUnreadable, DriveClient.MapIo(new IOException("başka", unchecked((int)0x80070002))));
    }

    [Fact]
    public async Task A_missing_file_is_reported_before_any_request_goes_out()
    {
        var transport = new FakeTransport();
        var client = new DriveClient(transport, new FixedToken("erisim"));

        var result = await client.UploadAndShareAsync(Path.Combine(Path.GetTempPath(), "olmayan-dosya-31.mp4"));

        Assert.Equal(ShareFailure.FileUnreadable, result.Failure);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task A_broken_connection_leaves_a_resumable_upload_behind()
    {
        var path = TempFile(3 * Alignment);
        try
        {
            var transport = new FakeTransport();
            transport.Enqueue(WithLocation(HttpStatusCode.OK, "https://upload.example/oturum/3"));
            transport.Enqueue(WithRange((HttpStatusCode)308, Alignment - 1));
            transport.EnqueueThrow(new HttpRequestException("bağlantı koptu"));

            var client = new DriveClient(transport, new FixedToken("erisim"), chunkSize: Alignment);
            var result = await client.UploadAndShareAsync(path);

            Assert.Equal(ShareFailure.NetworkFailure, result.Failure);
            Assert.Equal("https://upload.example/oturum/3", result.ResumeUri);
            Assert.Equal(Alignment, result.BytesSent);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Resume_asks_the_server_where_it_stopped_and_continues_from_there()
    {
        var path = TempFile(3 * Alignment);
        try
        {
            var transport = new FakeTransport();
            // Durum sorgusu: sunucu ilk parçayı onaylamış.
            transport.Enqueue(WithRange((HttpStatusCode)308, Alignment - 1));
            transport.Enqueue(WithRange((HttpStatusCode)308, 2 * Alignment - 1));
            transport.Enqueue(Json(HttpStatusCode.OK, """{"id":"dosya-3","webViewLink":"https://drive.google.com/z"}"""));
            transport.Enqueue(Json(HttpStatusCode.OK, """{"id":"izin-3"}"""));

            var client = new DriveClient(transport, new FixedToken("erisim"), chunkSize: Alignment);
            var upload = new ResumableUpload("https://upload.example/oturum/3", path, 3 * Alignment, 0);
            var result = await client.ResumeAsync(upload);

            Assert.True(result.Ok, result.Detail);

            // İlk istek yalnız durum sorgusu: gövdesiz, "bytes */toplam".
            Assert.Equal($"bytes */{3 * Alignment}", transport.Sent[0].ContentRange);
            // Devam ilk parçadan değil, ikinci parçadan başlamalı — baştan başlanmıyor.
            Assert.Equal($"bytes {Alignment}-{2 * Alignment - 1}/{3 * Alignment}", transport.Sent[1].ContentRange);
            Assert.Equal($"bytes {2 * Alignment}-{3 * Alignment - 1}/{3 * Alignment}", transport.Sent[2].ContentRange);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Confirmed_byte_count_is_read_from_the_range_header()
    {
        Assert.Equal(262144, DriveClient.ConfirmedBytes(WithRange((HttpStatusCode)308, 262143)));
        // Başlık yoksa hiçbir bayt onaylanmamıştır — baştan gönderilir.
        Assert.Equal(0, DriveClient.ConfirmedBytes(new HttpResponseMessage((HttpStatusCode)308) { Content = new StringContent("") }));
    }

    [Fact]
    public async Task Quota_reading_separates_drive_use_from_gmail_and_photos()
    {
        var transport = new FakeTransport();
        transport.Enqueue(Json(HttpStatusCode.OK,
            """{"storageQuota":{"limit":"16106127360","usage":"10737418240","usageInDrive":"4294967296"}}"""));

        var client = new DriveClient(transport, new FixedToken("erisim"));
        var quota = await client.GetQuotaAsync();

        Assert.NotNull(quota);
        Assert.Equal(16106127360L, quota!.Limit);
        Assert.Equal(4294967296L, quota.UsageInDrive);
        // 15 GB Gmail ve Photos ile paylaşımlı: farkı gösterebilmek kullanıcıya sebebi anlatır.
        Assert.Equal(6442450944L, quota.UsageOutsideDrive);
        Assert.Equal(5368709120L, quota.Free);
    }

    // ---------- yardımcılar ----------

    private const int Alignment = DriveClient.ChunkAlignment;

    private static string? ShareSourceFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VidShrink.sln")))
        {
            directory = directory.Parent;
        }
        if (directory is null) return null;
        var folder = Path.Combine(directory.FullName, "src", "VidShrink.Core", "Share");
        return Directory.Exists(folder) ? folder : null;
    }

    private static string TempFile(int size)
    {
        var path = Path.Combine(Path.GetTempPath(), $"vidshrink-yukleme-{Guid.NewGuid():N}.bin");
        var bytes = new byte[size];
        Random.Shared.NextBytes(bytes);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage WithLocation(HttpStatusCode status, string location)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent("") };
        response.Headers.Location = new Uri(location);
        return response;
    }

    private static HttpResponseMessage WithRange(HttpStatusCode status, long lastByte)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent("") };
        response.Headers.TryAddWithoutValidation("Range", $"bytes=0-{lastByte}");
        return response;
    }

    private sealed record SentRequest(string Method, string Url, string Body, string? ContentRange, string? Authorization);

    /// <summary>Sıraya konmuş yanıtları veren ve gideni kaydeden sahte HTTP katmanı.</summary>
    private sealed class FakeTransport : IHttpTransport
    {
        private readonly Queue<object> _responses = new();

        public List<SentRequest> Sent { get; } = new();

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        public void EnqueueThrow(Exception error) => _responses.Enqueue(error);

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Sent.Add(new SentRequest(
                request.Method.Method,
                request.RequestUri!.ToString(),
                body,
                Format(request.Content?.Headers.ContentRange),
                request.Headers.Authorization?.ToString()));

            if (_responses.Count == 0) throw new InvalidOperationException("Sahte taşıyıcıda yanıt kalmadı.");
            var next = _responses.Dequeue();
            if (next is Exception error) throw error;
            return (HttpResponseMessage)next;
        }

        private static string? Format(ContentRangeHeaderValue? range)
        {
            if (range is null) return null;
            var span = range.From is null || range.To is null ? "*" : $"{range.From}-{range.To}";
            return $"bytes {span}/{range.Length}";
        }
    }

    private sealed class FixedToken : IAccessTokenProvider
    {
        private readonly string _token;

        public FixedToken(string token) => _token = token;

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult(_token);
    }

    private sealed class SyncProgress : IProgress<UploadProgress>
    {
        private readonly List<UploadProgress> _seen;

        public SyncProgress(List<UploadProgress> seen) => _seen = seen;

        public void Report(UploadProgress value) => _seen.Add(value);
    }
}
