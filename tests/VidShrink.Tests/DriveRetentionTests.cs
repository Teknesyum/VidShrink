using System.Net;
using System.Text;
using VidShrink.Core.Share;

namespace VidShrink.Tests;

/// <summary>
/// T34: Drive'da yer yönetimi — silme, saklama süresi, toplu temizlik, kota.
/// Hiçbir sınama ağa çıkmaz ve gerçek bir Drive hesabı istemez; HTTP katmanı
/// <see cref="IHttpTransport"/> arkasında sahte bir taşıyıcıyla değiştiriliyor.
/// </summary>
public class DriveRetentionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vidshrink-t34-" + Guid.NewGuid().ToString("N"));

    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // Geçici klasör temizlenemediyse sınamayı düşürmeye değmez.
        }
    }

    // ---------- K3: saklama süresi ----------

    [Fact]
    public void Default_retention_is_not_forever()
    {
        // Sonsuz varsayılan kullanıcının Drive'ını sessizce doldurur; varsayılan sınırlı olmalı.
        Assert.True(RetentionPolicy.Default.Enabled);
        Assert.Equal(TimeSpan.FromDays(30), RetentionPolicy.Default.MaxAge);

        // Kullanıcı kapatabilir, ama sonucu tek satırla yazılır.
        Assert.False(RetentionPolicy.Off.Enabled);
        Assert.False(string.IsNullOrWhiteSpace(RetentionPolicy.OffConsequence));
    }

    [Fact]
    public void Expired_uploads_are_the_ones_past_the_age()
    {
        var policy = RetentionPolicy.Default;
        var old = Upload("eski", uploadedAt: Now - TimeSpan.FromDays(31));
        var fresh = Upload("yeni", uploadedAt: Now - TimeSpan.FromDays(29));

        var expired = policy.Expired(new[] { old, fresh }, Now);

        Assert.Equal(new[] { "eski" }, expired.Select(f => f.FileId));
        Assert.True(policy.IsExpired(old, Now));
        Assert.False(policy.IsExpired(fresh, Now));

        // Süre kapalıysa hiçbir şey süresi geçmiş sayılmaz.
        Assert.Empty(RetentionPolicy.Off.Expired(new[] { old, fresh }, Now));
    }

    [Fact]
    public void Retention_setting_survives_a_restart_and_falls_back_to_the_default()
    {
        var path = Path.Combine(_folder, "saklama.json");

        new RetentionPolicy(TimeSpan.FromDays(7)).Save(path);
        Assert.Equal(TimeSpan.FromDays(7), RetentionPolicy.Load(path).MaxAge);

        RetentionPolicy.Off.Save(path);
        Assert.False(RetentionPolicy.Load(path).Enabled);

        // Ayar yoksa ya da bozuksa sonsuza değil, varsayılana düşülür.
        File.Delete(path);
        Assert.Equal(RetentionPolicy.DefaultMaxAge, RetentionPolicy.Load(path).MaxAge);
        File.WriteAllText(path, "{ bozuk");
        Assert.Equal(RetentionPolicy.DefaultMaxAge, RetentionPolicy.Load(path).MaxAge);
    }

    // ---------- K2: silme gerçekten siler ----------

    [Fact]
    public async Task Deleting_a_file_asks_drive_to_delete_it()
    {
        var transport = new FakeTransport();
        transport.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = Client(transport);

        var outcome = await client.DeleteFileAsync("dosya-1");

        Assert.True(outcome.Ok);
        Assert.False(outcome.AlreadyGone);
        var sent = Assert.Single(transport.Sent);
        Assert.Equal("DELETE", sent.Method);
        Assert.Equal("https://www.googleapis.com/drive/v3/files/dosya-1", sent.Url);
    }

    [Fact]
    public async Task Deleting_a_file_that_is_already_gone_counts_as_success()
    {
        var transport = new FakeTransport();
        transport.Enqueue(Json(HttpStatusCode.NotFound, "{\"error\":{\"message\":\"File not found\"}}"));
        var log = new SharedFileLog(Path.Combine(_folder, "yuklemeler.json"));
        log.Record(Upload("dosya-1"));

        var outcome = await Client(transport, log).DeleteFileAsync("dosya-1");

        Assert.True(outcome.Ok);
        Assert.True(outcome.AlreadyGone);
        Assert.Empty(log.Load());
    }

    [Fact]
    public async Task A_failed_delete_says_why()
    {
        var transport = new FakeTransport();
        transport.Enqueue(Json(
            HttpStatusCode.Unauthorized,
            "{\"error\":{\"errors\":[{\"reason\":\"authError\"}],\"message\":\"Invalid Credentials\"}}"));

        var outcome = await Client(transport).DeleteFileAsync("dosya-1");

        Assert.False(outcome.Ok);
        Assert.Equal(ShareFailure.TokenExpired, outcome.Failure);
        Assert.Equal("Invalid Credentials", outcome.Detail);
    }

    [Fact]
    public async Task Stopping_the_share_keeps_the_file_but_deleting_removes_it()
    {
        var log = new SharedFileLog(Path.Combine(_folder, "yuklemeler.json"));
        log.Record(Upload("dosya-1", shared: true, permissionId: "izin-1"));

        var transport = new FakeTransport();
        transport.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent)); // permissions.delete
        transport.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent)); // files.delete
        var client = Client(transport, log);

        // Yayını kapat: bağlantı ölür, dosya Drive'da kalır.
        var stopped = await client.StopSharingAsync(
            new ShareLink("dosya-1", "izin-1", "https://drive.google.com/file/d/dosya-1/view", "video.mp4", Now));

        Assert.True(stopped.Ok);
        var kept = Assert.Single(log.Load());
        Assert.False(kept.Shared);
        Assert.Null(kept.PermissionId);
        Assert.Equal(1_000_000, kept.SizeBytes);
        Assert.EndsWith("/permissions/izin-1", transport.Sent[0].Url);

        // Dosyayı sil: yer boşalır, kayıt da gider.
        Assert.True((await client.DeleteFileAsync("dosya-1")).Ok);
        Assert.Empty(log.Load());
        Assert.Equal("https://www.googleapis.com/drive/v3/files/dosya-1", transport.Sent[1].Url);
    }

    // ---------- K1 ve K5: doğruluk kaynağı Drive ----------

    [Fact]
    public async Task Log_is_rebuilt_from_drive_when_it_is_lost()
    {
        var path = Path.Combine(_folder, "yuklemeler.json");
        var log = new SharedFileLog(path);
        Directory.CreateDirectory(_folder);
        File.WriteAllText(path, "yarısı silinmiş bozuk kayıt {");

        Assert.Empty(log.Load()); // bozuk kayıt hata değil, boş sayılır

        var transport = new FakeTransport();
        transport.Enqueue(Json(HttpStatusCode.OK, Listing(
            DriveFile("dosya-1", "video.mp4", 1_000, Now - TimeSpan.FromDays(2), shared: true),
            DriveFile("dosya-2", "klip.mp4", 2_000, Now - TimeSpan.FromDays(3), shared: false))));

        var files = await Maintenance(transport, log).RefreshAsync();

        Assert.NotNull(files);
        Assert.Equal(new[] { "dosya-1", "dosya-2" }, log.Load().Select(f => f.FileId));
        var restored = log.Load().First();
        Assert.Equal("video.mp4", restored.FileName);
        Assert.Equal(1_000, restored.SizeBytes);
        Assert.True(restored.Shared);
        Assert.Equal("izin-dosya-1", restored.PermissionId);
        Assert.False(log.Load().Last().Shared);
    }

    [Fact]
    public async Task Listing_walks_every_page()
    {
        var transport = new FakeTransport();
        transport.Enqueue(Json(HttpStatusCode.OK,
            "{\"nextPageToken\":\"sayfa2\",\"files\":[" + DriveFile("dosya-1", "a.mp4", 10, Now) + "]}"));
        transport.Enqueue(Json(HttpStatusCode.OK, Listing(DriveFile("dosya-2", "b.mp4", 20, Now))));

        var files = await Client(transport).ListAppFilesAsync();

        Assert.NotNull(files);
        Assert.Equal(new[] { "dosya-1", "dosya-2" }, files!.Select(f => f.FileId));
        Assert.Contains("pageToken=sayfa2", transport.Sent[1].Url);
    }

    [Fact]
    public async Task Delete_all_uses_the_drive_listing_not_the_local_log()
    {
        // Yerel kayıt yanlış: Drive'da olmayan bir dosyayı gösteriyor, gerçek dosyayı bilmiyor.
        var log = new SharedFileLog(Path.Combine(_folder, "yuklemeler.json"));
        log.Record(Upload("hayalet"));

        var transport = new FakeTransport();
        transport.Enqueue(Json(HttpStatusCode.OK, Listing(DriveFile("gercek", "video.mp4", 5_000, Now))));
        transport.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        var report = await Maintenance(transport, log).DeleteAllAsync();

        Assert.True(report.Ran);
        Assert.Equal(new[] { "gercek" }, report.Deleted.Select(f => f.FileId));
        Assert.Equal(5_000, report.FreedBytes);
        Assert.Empty(report.Failed);
        Assert.Empty(log.Load());
        Assert.Equal("https://www.googleapis.com/drive/v3/files/gercek", transport.Sent[1].Url);
    }

    // ---------- K3: temizlik ----------

    [Fact]
    public async Task Sweep_deletes_only_the_upload_that_is_past_its_time()
    {
        var log = new SharedFileLog(Path.Combine(_folder, "yuklemeler.json"));
        var transport = new FakeTransport();
        transport.Enqueue(Json(HttpStatusCode.OK, Listing(
            DriveFile("eski", "eski.mp4", 3_000, Now - TimeSpan.FromDays(40)),
            DriveFile("yeni", "yeni.mp4", 4_000, Now - TimeSpan.FromDays(1)))));
        transport.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        var report = await Maintenance(transport, log).SweepAsync(RetentionPolicy.Default);

        Assert.True(report.Ran);
        // Ne silindiği görülebilmeli: ad ve boyut raporda duruyor.
        var deleted = Assert.Single(report.Deleted);
        Assert.Equal("eski.mp4", deleted.FileName);
        Assert.Equal(3_000, report.FreedBytes);
        Assert.Equal(new[] { "yeni" }, log.Load().Select(f => f.FileId));
        Assert.Equal(2, transport.Sent.Count);
    }

    [Fact]
    public async Task Sweep_is_skipped_when_drive_cannot_be_reached()
    {
        var log = new SharedFileLog(Path.Combine(_folder, "yuklemeler.json"));
        log.Record(Upload("dosya-1", uploadedAt: Now - TimeSpan.FromDays(90)));

        var transport = new FakeTransport();
        transport.EnqueueThrow(new HttpRequestException("Ağ yok."));

        var report = await Maintenance(transport, log).SweepAsync(RetentionPolicy.Default);

        Assert.False(report.Ran);
        Assert.False(string.IsNullOrWhiteSpace(report.SkipReason));
        Assert.Empty(report.Deleted);
        // Ağ yokken hiçbir şey silinmez ve yerel kayda dokunulmaz; sonraki açılışta denenir.
        Assert.Single(log.Load());
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task Sweep_does_nothing_when_retention_is_off()
    {
        var transport = new FakeTransport();

        var report = await Maintenance(transport, new SharedFileLog(Path.Combine(_folder, "yuklemeler.json")))
            .SweepAsync(RetentionPolicy.Off);

        Assert.False(report.Ran);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task A_delete_that_fails_during_the_sweep_is_reported_not_swallowed()
    {
        var log = new SharedFileLog(Path.Combine(_folder, "yuklemeler.json"));
        var transport = new FakeTransport();
        transport.Enqueue(Json(HttpStatusCode.OK, Listing(DriveFile("eski", "eski.mp4", 3_000, Now - TimeSpan.FromDays(40)))));
        transport.Enqueue(Json(
            HttpStatusCode.Forbidden,
            "{\"error\":{\"errors\":[{\"reason\":\"insufficientFilePermissions\"}],\"message\":\"Yetki yok.\"}}"));

        var report = await Maintenance(transport, log).SweepAsync(RetentionPolicy.Default);

        Assert.True(report.Ran);
        Assert.Empty(report.Deleted);
        var failed = Assert.Single(report.Failed);
        Assert.Equal("eski", failed.File.FileId);
        Assert.Equal(ShareFailure.NotAuthorized, failed.Failure);
        Assert.Equal("Yetki yok.", failed.Detail);
        // Silinemeyen dosya kayıttan düşmez.
        Assert.Single(log.Load());
    }

    // ---------- K4: yer durumu ----------

    [Fact]
    public async Task Storage_report_adds_up_what_the_app_uploaded()
    {
        var transport = new FakeTransport();
        transport.Enqueue(Json(HttpStatusCode.OK,
            "{\"storageQuota\":{\"limit\":\"16106127360\",\"usage\":\"8000000000\",\"usageInDrive\":\"5000000000\"}}"));
        transport.Enqueue(Json(HttpStatusCode.OK, Listing(
            DriveFile("dosya-1", "a.mp4", 1_000_000_000, Now),
            DriveFile("dosya-2", "b.mp4", 1_000_000_000, Now))));

        var report = await Maintenance(transport, new SharedFileLog(Path.Combine(_folder, "yuklemeler.json")))
            .GetStorageReportAsync();

        Assert.Equal(2_000_000_000, report.AppUsage);
        Assert.Equal(2, report.AppFileCount);
        Assert.NotNull(report.Quota);
        Assert.Equal(8_000_000_000, report.Quota!.Usage);
        Assert.Equal(8_106_127_360, report.Quota.Free);
        // 15 GB Gmail ve Photos ile paylaşımlı: Drive dışı kullanım ayrı görünür.
        Assert.Equal(3_000_000_000, report.Quota.UsageOutsideDrive);
        Assert.Equal(0.25, report.AppShareOfUsage!.Value, 3);
    }

    [Fact]
    public async Task Storage_report_still_answers_when_the_quota_cannot_be_read()
    {
        var log = new SharedFileLog(Path.Combine(_folder, "yuklemeler.json"));
        log.Record(Upload("dosya-1"));

        var transport = new FakeTransport();
        transport.EnqueueThrow(new HttpRequestException("Ağ yok.")); // about.get
        transport.EnqueueThrow(new HttpRequestException("Ağ yok.")); // files.list

        var report = await Maintenance(transport, log).GetStorageReportAsync();

        Assert.Null(report.Quota);
        Assert.Null(report.AppShareOfUsage);
        // Drive susarsa elde kalan yerel kayıttır.
        Assert.Equal(1_000_000, report.AppUsage);
    }

    // ---------- yardımcılar ----------

    private DriveClient Client(FakeTransport transport, SharedFileLog? log = null) =>
        new(transport, new FixedToken("jeton"), ledger: null, clock: () => Now, files: log);

    private DriveMaintenance Maintenance(FakeTransport transport, SharedFileLog log) =>
        new(Client(transport, log), log, () => Now);

    private static UploadedFile Upload(
        string fileId,
        DateTimeOffset? uploadedAt = null,
        bool shared = false,
        string? permissionId = null) =>
        new(fileId, fileId + ".mp4", 1_000_000, uploadedAt ?? Now, shared, permissionId);

    private static string DriveFile(string id, string name, long size, DateTimeOffset created, bool shared = true)
    {
        var permissions = shared
            ? $",\"permissions\":[{{\"id\":\"izin-{id}\",\"type\":\"anyone\",\"role\":\"reader\"}}]"
            : ",\"permissions\":[{\"id\":\"sahip\",\"type\":\"user\",\"role\":\"owner\"}]";
        return $"{{\"id\":\"{id}\",\"name\":\"{name}\",\"size\":\"{size}\","
            + $"\"createdTime\":\"{created:yyyy-MM-ddTHH:mm:ss.fffZ}\","
            + $"\"webViewLink\":\"https://drive.google.com/file/d/{id}/view\"{permissions}}}";
    }

    private static string Listing(params string[] files) => "{\"files\":[" + string.Join(",", files) + "]}";

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record SentRequest(string Method, string Url, string Body);

    private sealed class FakeTransport : IHttpTransport
    {
        private readonly Queue<object> _responses = new();

        public List<SentRequest> Sent { get; } = new();

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        public void EnqueueThrow(Exception error) => _responses.Enqueue(error);

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Sent.Add(new SentRequest(request.Method.Method, request.RequestUri!.ToString(), body));

            if (_responses.Count == 0) throw new InvalidOperationException("Sahte taşıyıcıda yanıt kalmadı.");
            var next = _responses.Dequeue();
            if (next is Exception error) throw error;
            return (HttpResponseMessage)next;
        }
    }

    private sealed class FixedToken : IAccessTokenProvider
    {
        private readonly string _token;

        public FixedToken(string token) => _token = token;

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult(_token);
    }
}
