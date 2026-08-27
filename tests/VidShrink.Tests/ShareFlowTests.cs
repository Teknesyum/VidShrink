using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Core.Share;

// Arayüzün kendi ShareTarget kaydı ayrı bir tür: ayarlar listesinin gördüğü satır. Yükleme
// motorun tablosunu konuşur, bu yüzden bu ölçümde adlar motorun türlerine bağlanır.
using ShareTarget = VidShrink.Core.Share.ShareTarget;
using ShareTargetTable = VidShrink.Core.Share.ShareTargetTable;

namespace VidShrink.Tests;

/// <summary>
/// T54: paylaşım motoru arayüze bağlandı, sürüm dizesi okunur hale geldi ve güncelleme
/// şeridi kullanıcı kapatana kadar duruyor.
///
/// Yükleme ölçümü ağa çıkmaz: taşıyıcı sahte, sağlayıcı gerçek. Böylece tavan denetimi,
/// hata sınıflandırması ve ilerleme bildirimi uygulamanın koştuğu kodla ölçülür.
/// Pencerenin kendisi açılamıyor (<c>Avalonia.Headless</c> bağlı değil), bu yüzden
/// düğmenin bağlandığı kaynak metinden okunuyor — <see cref="SettingsTabTests"/> ile aynı yol.
/// </summary>
public sealed class ShareFlowTests : IDisposable
{
    private readonly string _ledgerPath =
        Path.Combine(Path.GetTempPath(), "vidshrink-share-" + Guid.NewGuid().ToString("N"), "paylasimlar.json");

    private readonly string _filePath =
        Path.Combine(Path.GetTempPath(), "vidshrink-share-" + Guid.NewGuid().ToString("N") + ".mp4");

    public ShareFlowTests() => File.WriteAllBytes(_filePath, new byte[64 * 1024]);

    public void Dispose()
    {
        try { File.Delete(_filePath); } catch (IOException) { }
        try { Directory.Delete(Path.GetDirectoryName(_ledgerPath)!, true); } catch (IOException) { }
    }

    private ShareLedger Ledger => new(_ledgerPath);

    /// <summary>Tek adımlı protokol konuşan bir hedef. Tavanı ölçüme göre verilir.</summary>
    private static ShareTargetTable TableWithCeiling(long maxBytes) => ShareTargetTable.Parse($$"""
    {
      "version": 1,
      "default": "sahte.example",
      "targets": [
        { "id": "sahte.example", "displayName": "sahte", "maxBytes": {{maxBytes}},
          "retentionDays": [], "fixedRetentionHours": 3,
          "canDelete": false, "playsInBrowser": true,
          "endpoints": { "upload": "https://sahte.example/upload" } }
      ]
    }
    """);

    private ShareFlow FlowOver(FakeTransport transport, ShareTargetTable table) =>
        new(target => ShareProviderFactory.Create(target, transport, table), Ledger);

    // ---- K1: paylaş düğmesi ------------------------------------------------------------

    [Fact]
    public async Task AFinishedFileIsUploadedAndTheLinkComesBack()
    {
        var table = TableWithCeiling(1024 * 1024);
        var transport = new FakeTransport();
        transport.Enqueue(HttpStatusCode.OK, "https://sahte.example/abc.mp4");
        var flow = FlowOver(transport, table);

        var result = await flow.ShareAsync(table.DefaultTarget!, _filePath);

        Assert.True(result.Ok, result.Message);
        Assert.Equal("https://sahte.example/abc.mp4", result.Link!.Url);
        Assert.Equal("https://sahte.example/abc.mp4", flow.Link!.Url);
        Assert.Single(transport.Requests);
    }

    /// <summary>Kayıt defterine yazılır, yoksa uygulama kapanınca yayın kapatılamaz.</summary>
    [Fact]
    public async Task ASuccessfulShareIsWrittenToTheLedger()
    {
        var table = TableWithCeiling(1024 * 1024);
        var transport = new FakeTransport();
        transport.Enqueue(HttpStatusCode.OK, "https://sahte.example/abc.mp4");

        await FlowOver(transport, table).ShareAsync(table.DefaultTarget!, _filePath);

        var saved = Assert.Single(Ledger.Load());
        Assert.Equal("https://sahte.example/abc.mp4", saved.Url);
        Assert.Equal("sahte.example", saved.TargetId);
    }

    /// <summary>K1: tavanı aşan dosya yüklenmeye kalkışılmaz ve sebebi söylenir.</summary>
    [Fact]
    public async Task AFileOverTheCeilingIsRefusedBeforeAnythingIsSent()
    {
        var table = TableWithCeiling(8);
        var transport = new FakeTransport();
        var flow = FlowOver(transport, table);

        var result = await flow.ShareAsync(table.DefaultTarget!, _filePath);

        Assert.False(result.Ok);
        Assert.Equal(ShareFailure.FileTooLarge, result.Failure);
        Assert.NotEmpty(result.Message);
        Assert.Empty(transport.Requests);
        Assert.Null(flow.Link);
        Assert.Empty(Ledger.Load());
    }

    /// <summary>K1: ağ hatası çökme üretmez, sınıflandırılmış bir sonuç döner.</summary>
    [Fact]
    public async Task ANetworkFailureComesBackAsAResultNotAnException()
    {
        var table = TableWithCeiling(1024 * 1024);
        var transport = new FakeTransport();
        transport.Throw(new HttpRequestException("ağ yok"));

        var result = await FlowOver(transport, table).ShareAsync(table.DefaultTarget!, _filePath);

        Assert.False(result.Ok);
        Assert.Equal(ShareFailure.NetworkFailure, result.Failure);
        Assert.Null(Ledger.Load().FirstOrDefault());
    }

    /// <summary>K1: yükleme sırasında ilerleme görünür.</summary>
    [Fact]
    public async Task ProgressIsReportedWhileTheFileGoesUp()
    {
        var table = TableWithCeiling(1024 * 1024);
        var transport = new FakeTransport();
        transport.Enqueue(HttpStatusCode.OK, "https://sahte.example/abc.mp4");

        var steps = new List<UploadProgress>();
        var flow = FlowOver(transport, table);

        await flow.ShareAsync(table.DefaultTarget!, _filePath, progress: new DirectProgress(steps.Add));

        Assert.NotEmpty(steps);
        Assert.Equal(1.0, steps[^1].Fraction, 3);
    }

    /// <summary>K1: yükleme iptal edilebilir.</summary>
    [Fact]
    public async Task AnUploadCanBeCancelled()
    {
        var table = TableWithCeiling(1024 * 1024);
        var provider = new PausingProvider(table.DefaultTarget!);
        var flow = new ShareFlow(_ => provider, Ledger);

        var running = flow.ShareAsync(table.DefaultTarget!, _filePath);
        await provider.Entered.Task;
        Assert.True(flow.Running);
        flow.Cancel();

        var result = await running;

        Assert.False(result.Ok);
        Assert.Equal(ShareFailure.Cancelled, result.Failure);
        Assert.False(flow.Running);
        Assert.Null(flow.Link);
    }

    /// <summary>K1: silme jetonu vermeyen hedefte düğme bir başarısızlıkla döner, çökmez.</summary>
    [Fact]
    public async Task ATargetWithoutADeleteTokenRefusesTheDeleteWithoutGoingOut()
    {
        var table = TableWithCeiling(1024 * 1024);
        var transport = new FakeTransport();
        transport.Enqueue(HttpStatusCode.OK, "https://sahte.example/abc.mp4");
        var flow = FlowOver(transport, table);
        await flow.ShareAsync(table.DefaultTarget!, _filePath);

        Assert.False(flow.CanDelete);
        var result = await flow.DeleteAsync(table.DefaultTarget!);

        Assert.False(result.Ok);
        Assert.Single(transport.Requests);
        Assert.NotNull(flow.Link);
    }

    /// <summary>K1: silme jetonu veren hedefte yayın kapanır ve kayıt düşer.</summary>
    [Fact]
    public async Task ClosingAShareDropsTheLedgerRow()
    {
        var table = TableWithCeiling(1024 * 1024);
        var target = table.DefaultTarget!;
        var link = new ShareLink(target.Id, "abc", "https://sahte.example/abc.mp4", "abc.mp4",
            DateTimeOffset.UtcNow, OwnerToken: "owner-abc");
        var provider = new ScriptedProvider(target, ShareResult.Success(link), ShareResult.Success(link));
        var flow = new ShareFlow(_ => provider, Ledger);

        await flow.ShareAsync(target, _filePath);
        Assert.True(flow.CanDelete);
        Assert.Single(Ledger.Load());

        var result = await flow.DeleteAsync(target);

        Assert.True(result.Ok);
        Assert.Null(flow.Link);
        Assert.Empty(Ledger.Load());
    }

    /// <summary>Aynı anda iki yükleme başlamaz.</summary>
    [Fact]
    public async Task ASecondUploadIsRefusedWhileTheFirstIsStillRunning()
    {
        var table = TableWithCeiling(1024 * 1024);
        var provider = new PausingProvider(table.DefaultTarget!);
        var flow = new ShareFlow(_ => provider, Ledger);

        var running = flow.ShareAsync(table.DefaultTarget!, _filePath);
        await provider.Entered.Task;

        var second = await flow.ShareAsync(table.DefaultTarget!, _filePath);
        Assert.False(second.Ok);

        flow.Cancel();
        await running;
    }

    /// <summary>
    /// K1: düğme gerçekten bağlı. Şikâyetin kendisi buydu — motor yazılmış, arayüzde
    /// <c>IShareProvider</c> geçen tek satır yoktu.
    /// </summary>
    [Fact]
    public void TheOutputPanelCarriesAShareButtonThatReachesTheProvider()
    {
        var xaml = File.ReadAllText(TipSources.WindowXamlPath);
        var code = File.ReadAllText(TipSources.WindowCodePath);

        Assert.Contains("x:Name=\"BtnShare\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnShare\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnShareCancel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnShareDelete\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TxtShareLink\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShareProgress\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ShareProviderFactory.Create", code, StringComparison.Ordinal);
        Assert.Contains("IShareProvider", code, StringComparison.Ordinal);
    }

    /// <summary>Yeni düğmelerin iki dilde karşılığı var.</summary>
    [Theory]
    [InlineData("Share the file", "Dosyayı paylaş")]
    [InlineData("Cancel the upload", "Yüklemeyi iptal et")]
    public void TheShareButtonsAreTranslated(string english, string turkish)
        => Assert.Equal(turkish, TipSources.ReadCatalogue()[english]);

    // ---- K2: sürüm dizesi --------------------------------------------------------------

    /// <summary>
    /// K2: ekrandaki sürüm kırk karakterlik özet taşımaz. Okuma yolu tek: uygulamanın
    /// <c>AppVersion()</c> çağrısı da bunu okuyor.
    /// </summary>
    [Fact]
    public void TheVersionOnScreenIsReadable()
    {
        var version = UpdateCheck.CurrentVersion(typeof(LanguageCatalog).Assembly);

        Assert.Matches(@"^\d+\.\d+\.\d+(\.\d+)?(\+[0-9A-Za-z]{1,7})?$", version);
        Assert.True(version.Length <= 20, $"Sürüm dizesi hâlâ uzun: {version}");
    }

    /// <summary>K2: sürüm için ikinci bir okuma yolu açılmadı.</summary>
    [Fact]
    public void TheVersionHasASingleReadPath()
    {
        var code = File.ReadAllText(TipSources.WindowCodePath);

        Assert.Contains("UpdateCheck.CurrentVersion(Assembly.GetExecutingAssembly())", code, StringComparison.Ordinal);
        Assert.DoesNotContain("AssemblyInformationalVersionAttribute", code, StringComparison.Ordinal);
    }

    /// <summary>K2: uzunluk kararı tek yerde ve açıklama artık doğruyu söylüyor.</summary>
    [Fact]
    public void TheBuildPropsTrimsTheRevisionAndSaysSo()
    {
        var props = File.ReadAllText(Path.Combine(TipSources.Root, "Directory.Build.props"));

        Assert.Contains("ShortenSourceRevisionId", props, StringComparison.Ordinal);
        Assert.Contains("<SourceRevisionLength>7</SourceRevisionLength>", props, StringComparison.Ordinal);
        Assert.DoesNotContain("A local build leaves it empty", props, StringComparison.Ordinal);
    }

    // ---- K3: güncelleme bildirimi -------------------------------------------------------

    /// <summary>
    /// K3: şerit kullanıcı kapatana kadar durur. İşaret dosyası yalnız kapatma
    /// düğmesinden silinir; şerit göründüğü anda silinseydi okunmadan kaybolurdu.
    /// </summary>
    [Fact]
    public void TheAppliedNoticeIsClearedOnlyWhenTheUserClosesIt()
    {
        var code = File.ReadAllText(TipSources.WindowCodePath);

        var calls = Regex.Matches(code, @"_appliedNotice\?\.Shown\(\)");
        Assert.Single(calls);

        var dismiss = code.IndexOf("private void OnDismissAppliedNotice", StringComparison.Ordinal);
        Assert.True(dismiss >= 0, "Kapatma işleyicisi yok.");
        Assert.True(calls[0].Index > dismiss, "İşaret kapatma işleyicisinin dışında siliniyor.");
        Assert.DoesNotContain("ClearAppliedMarker", code, StringComparison.Ordinal);
    }

    /// <summary>K3: şeridin zaman aşımı yok, kendiliğinden kaybolmaz.</summary>
    [Fact]
    public void TheAppliedNoticeHasNoTimeout()
    {
        var code = File.ReadAllText(TipSources.WindowCodePath);
        var start = code.IndexOf("private void ReportAppliedUpdate", StringComparison.Ordinal);
        var end = code.IndexOf("private void OnDismissAppliedNotice", StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        var body = code[start..end];
        Assert.DoesNotContain("Timer", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Delay", body, StringComparison.Ordinal);
    }

    /// <summary>K3: bilgi yeniden başlamayı aşar — okunmadan kapatılan şerit geri gelir.</summary>
    [Fact]
    public void AnUnreadNoticeSurvivesARestart()
    {
        var directory = Path.GetDirectoryName(_ledgerPath)!;
        Directory.CreateDirectory(directory);
        AppliedUpdateNotice.Write(directory, "0.2.0");

        var firstLaunch = new AppliedUpdateNotice(directory);
        Assert.True(firstLaunch.Load());

        var secondLaunch = new AppliedUpdateNotice(directory);
        Assert.True(secondLaunch.Load());
        Assert.Equal("0.2.0", secondLaunch.Version);

        secondLaunch.Shown();
        Assert.False(new AppliedUpdateNotice(directory).Load());
    }

    // ---- Sahteler ----------------------------------------------------------------------

    /// <summary>Ağa çıkmayan taşıyıcı. Sıraya konan yanıtları verir, istekleri sayar.</summary>
    private sealed class FakeTransport : IHttpTransport
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();
        private readonly List<string> _requests = new();
        private Exception? _throw;

        public IReadOnlyList<string> Requests => _requests;

        public void Enqueue(HttpStatusCode status, string body) => _responses.Enqueue((status, body));

        public void Throw(Exception exception) => _throw = exception;

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            _requests.Add($"{request.Method} {request.RequestUri}");

            if (_throw is not null) throw _throw;

            var (status, text) = _responses.Count > 0 ? _responses.Dequeue() : (HttpStatusCode.OK, string.Empty);
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8) };
        }
    }

    /// <summary>İptal edilene kadar bekleyen sağlayıcı. Ağ yok, zamanlama yok.</summary>
    private sealed class PausingProvider : IShareProvider
    {
        public PausingProvider(ShareTarget target) => Target = target;

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ShareTarget Target { get; }

        public bool CanDelete => false;

        public async Task<ShareResult> UploadAsync(
            string filePath,
            int? retentionDays = null,
            IProgress<UploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException e)
            {
                return ShareResult.Failed(ShareErrorClassifier.FromException(Target, e, "yükleme"));
            }

            return ShareResult.Failed(new ShareDiagnosis(ShareFailure.Unknown, "buraya gelinmez"));
        }

        public Task<ShareResult> DeleteAsync(ShareLink link, CancellationToken cancellationToken = default) =>
            Task.FromResult(ShareResult.Failed(ShareErrorClassifier.DeleteUnsupported(Target)));

        public Task<ShareResult> CheckHealthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ShareResult.Failed(new ShareDiagnosis(ShareFailure.Unknown, "yoklanmadı")));
    }

    /// <summary>Sırayla verilmiş sonuçları döndüren sağlayıcı.</summary>
    private sealed class ScriptedProvider : IShareProvider
    {
        private readonly ShareResult _upload;
        private readonly ShareResult _delete;

        public ScriptedProvider(ShareTarget target, ShareResult upload, ShareResult delete)
        {
            Target = target;
            _upload = upload;
            _delete = delete;
        }

        public ShareTarget Target { get; }

        public bool CanDelete => true;

        public Task<ShareResult> UploadAsync(
            string filePath,
            int? retentionDays = null,
            IProgress<UploadProgress>? progress = null,
            CancellationToken cancellationToken = default) => Task.FromResult(_upload);

        public Task<ShareResult> DeleteAsync(ShareLink link, CancellationToken cancellationToken = default) =>
            Task.FromResult(_delete);

        public Task<ShareResult> CheckHealthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_delete);
    }

    /// <summary>Raporu geldiği iş parçacığında ileten ilerleme; ölçüm sırası kaymaz.</summary>
    private sealed class DirectProgress : IProgress<UploadProgress>
    {
        private readonly Action<UploadProgress> _report;

        public DirectProgress(Action<UploadProgress> report) => _report = report;

        public void Report(UploadProgress value) => _report(value);
    }
}
