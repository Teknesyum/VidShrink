using System.Net;
using System.Net.Http.Headers;

namespace VidShrink.Core;

/// <summary>Sahneye indirmenin aşamaları; her aşama panele bir satır düşürür.</summary>
public enum UpdateStagePhase
{
    /// <summary>Sürüm listesi çekiliyor.</summary>
    Manifest,

    /// <summary>Sürüm listesine ulaşılamadı.</summary>
    Unreachable,

    /// <summary>Kurulu sürüm zaten en yenisi.</summary>
    Current,

    /// <summary>Yeni sürüm bulundu, yenilenecek dosyalar sayıldı.</summary>
    Found,

    /// <summary>Bir dosya indi ve özeti tuttu.</summary>
    Downloaded,

    /// <summary>Bütün dosyalar sahnede.</summary>
    Staged
}

/// <summary>
/// Sahneleme aşamasının tek bildirimi. <see cref="Part"/> ve <see cref="Roof"/> işin
/// 0–1 arası payıdır; çağıran taraf kendi taban ve tavanına ölçekler.
/// </summary>
public sealed record UpdateStageReport(
    UpdateStagePhase Phase,
    double Part,
    double Roof,
    string? Version = null,
    string? File = null,
    int Done = 0,
    int Total = 0);

/// <summary>Sahnede kurulmayı bekleyen sürüm ve üç dosya kümesi.</summary>
public sealed record StagedUpdate(
    ReleaseManifest Manifest,
    string Stage,
    IReadOnlyList<ManifestFile> App,
    IReadOnlyList<ManifestFile> Launcher,
    IReadOnlyList<ManifestFile> Shell)
{
    public int Total => App.Count + Launcher.Count + Shell.Count;
}

/// <summary>
/// Güncellemenin indirme yarısı: manifest, fark, sahneye indirme. Kurulum yarısı yok;
/// yerine taşıma çalışan süreç kapandıktan sonra başlatıcıda yapılır.
///
/// <para>İki çağıranı var. Başlatıcı (<c>Updater</c>) indirip hemen kurar. Uygulama elle
/// akışta yalnız indirir: kullanıcı "Yükle"ye bastığında başlatıcı aynı sahneyi bulur,
/// özeti tutan dosyaları atlar ve doğrudan kurar. İkisinin aynı sahneyi paylaşması bu
/// sınıfın sabitlerinden geliyor.</para>
/// </summary>
public static class UpdateStaging
{
    /// <summary>İndirilenlerin toplandığı klasör, kurulum kökünde.</summary>
    public const string StageDirectoryName = "update-stage";

    /// <summary>Özet önbelleği, uygulama klasöründe.</summary>
    public const string HashCacheName = ".update-hashes.json";

    /// <summary>Sahnenin hangi sürüm için toplandığını söyleyen işaret.</summary>
    public const string StageVersionName = ".stage-version";

    /// <summary>İki süreç aynı sahneye yazmasın; ikincisi hiç başlamaz.</summary>
    public const string MutexName = @"Global\Teknesyum.VidShrink.Update";

    /// <summary>Başlatıcının indirme ve doğrulama şeridi sayısı.</summary>
    public const int LauncherLanes = 6;

    /// <summary>Yazılırken dosyanın taşıdığı ek; yarıda kalan indirme tam dosya gibi durmaz.</summary>
    public const string PartialSuffix = ".part";

    /// <summary>
    /// Sahneyi toplar. Kurulacak bir şey yoksa (liste yok, zaten güncel) null döner.
    /// <paramref name="lanes"/> 1 ise dosyalar sırayla iner. Birden çoksa en çok o kadar
    /// dosya aynı anda iner; çağıranın eşzamanlama bağlamı varsa şeritlerin gövdeleri de o
    /// bağlamda koşar. Düşük öncelikli iş parçacığında koşan çağıran böylece özet ve açma
    /// işini o iş parçacığında tutar, yalnız ağ beklemeleri üst üste biner.
    ///
    /// <para>Bitince (iptalde de) sahneye <see cref="StageSeal"/> yazılır: doğrulanan her
    /// dosyanın özeti, boyu ve yazma zamanı. Sonraki doğrulamalar mühre uyan dosyayı
    /// yeniden özetlemez.</para>
    /// </summary>
    public static async Task<StagedUpdate?> StageAsync(
        string baseDirectory,
        string appDirectory,
        string? source,
        int lanes,
        DownloadThrottle? throttle,
        string userAgent,
        Action<UpdateStageReport>? report,
        CancellationToken cancellationToken)
    {
        void Say(UpdateStageReport value) => report?.Invoke(value);

        var stage = Path.Combine(baseDirectory, StageDirectoryName);
        var rid = UpdateCheck.Rid;

        Say(new UpdateStageReport(UpdateStagePhase.Manifest, 0, 0.10));
        var json = await FetchManifestAsync(rid, source, cancellationToken);
        if (json is null)
        {
            Say(new UpdateStageReport(UpdateStagePhase.Unreachable, 0.10, 0.10));
            return null;
        }

        var manifest = UpdateCheck.ParseManifest(json);
        if (manifest.Files.Count == 0) return null;

        if (UpdateCheck.AlreadyCurrent(baseDirectory, appDirectory, manifest))
        {
            Say(new UpdateStageReport(UpdateStagePhase.Current, 1, 1, manifest.Version));
            return null;
        }

        var cache = new HashCache(Path.Combine(appDirectory, HashCacheName));
        var changed = UpdateCheck.Diff(appDirectory, manifest, cache);
        var launcherChanged = UpdateCheck.Diff(baseDirectory, manifest.Launcher, cache);
        var shellChanged = UpdateCheck.Diff(baseDirectory, manifest.Shell, cache);
        cache.Save();

        var total = changed.Count + launcherChanged.Count + shellChanged.Count;
        Say(new UpdateStageReport(UpdateStagePhase.Found, 0.12, 0.20, manifest.Version, Total: total));
        if (total == 0)
        {
            Say(new UpdateStageReport(UpdateStagePhase.Current, 1, 1, manifest.Version));
            UpdateCheck.WriteVersionMarker(appDirectory, manifest.Version);
            LauncherUpdate.MarkVerified(baseDirectory, manifest);
            return null;
        }

        if (StageVersion(stage) != manifest.Version)
        {
            UpdateStage.Discard(stage);
            Directory.CreateDirectory(stage);
            WriteStageVersion(stage, manifest.Version);
        }
        else Directory.CreateDirectory(stage);

        var seal = StageSeal.Load(stage);
        var done = 0;

        void Downloaded(ManifestFile file)
        {
            var count = Interlocked.Increment(ref done);
            var part = 0.20 + 0.70 * count / Math.Max(1, total);
            Say(new UpdateStageReport(
                UpdateStagePhase.Downloaded, part, Math.Min(0.90, part + 0.70 / Math.Max(1, total)),
                manifest.Version, file.Path, count, total));
        }

        async Task Fetch(RemoteZip archive, IReadOnlyList<ManifestFile> files, Func<ManifestFile, string> target)
        {
            if (files.Count == 0) return;
            if (lanes <= 1)
            {
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await StageFileAsync(archive, file, target(file), seal, cancellationToken);
                    Downloaded(file);
                }
                return;
            }

            var options = new ParallelOptions { MaxDegreeOfParallelism = lanes, CancellationToken = cancellationToken };
            if (SynchronizationContext.Current is not null)
                options.TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            await Parallel.ForEachAsync(
                files,
                options,
                async (file, token) =>
                {
                    await StageFileAsync(archive, file, target(file), seal, token);
                    Downloaded(file);
                });
        }

        try
        {
            if (changed.Count > 0)
            {
                var archive = await RemoteZip.OpenAsync(Source(UpdateCheck.ArchiveAssetName(rid), source, throttle, userAgent), cancellationToken);
                await Fetch(archive, changed, file => UpdateCheck.LocalPath(stage, file.Path));
            }

            if (launcherChanged.Count > 0 || shellChanged.Count > 0)
            {
                var archive = await RemoteZip.OpenAsync(Source(UpdateCheck.LauncherArchiveAssetName(rid), source, throttle, userAgent), cancellationToken);
                await Fetch(archive, launcherChanged, file => LauncherUpdate.StagePath(stage, file.Path));
                await Fetch(archive, shellChanged, file => ShellUpdate.StagePath(stage, file.Path));
            }
        }
        finally
        {
            seal.Save();
        }

        Say(new UpdateStageReport(UpdateStagePhase.Staged, 1, 1, manifest.Version, Done: total, Total: total));
        return new StagedUpdate(manifest, stage, changed, launcherChanged, shellChanged);
    }

    /// <summary>Sahnenin hangi sürüm için toplandığı; işaret yoksa null.</summary>
    private static string? StageVersion(string stage)
    {
        var marker = Path.Combine(stage, StageVersionName);
        if (!File.Exists(marker)) return null;
        try { return File.ReadAllText(marker).Trim(); }
        catch (IOException) { return null; }
    }

    private static void WriteStageVersion(string stage, string version) =>
        File.WriteAllText(Path.Combine(stage, StageVersionName), version);

    /// <summary>
    /// Sahnedeki dosya boyutu ve özetiyle manifeste oturuyor mu. Mühür dosyayı tanıyorsa
    /// yeniden özetlenmez; tanımıyorsa bir kez özetlenir ve tutarsa mühre girer.
    /// </summary>
    private static bool AlreadyStaged(string target, ManifestFile file, StageSeal seal)
    {
        if (!File.Exists(target)) return false;
        try
        {
            if (new FileInfo(target).Length != file.Size) return false;
            if (seal.Holds(target, file)) return true;
            if (!string.Equals(UpdateCheck.HashFile(target), file.Sha256, StringComparison.OrdinalIgnoreCase)) return false;
            seal.Record(target, file.Sha256);
            return true;
        }
        catch (IOException) { return false; }
    }

    private static async Task StageFileAsync(RemoteZip archive, ManifestFile file, string target, StageSeal seal, CancellationToken cancellationToken)
    {
        if (AlreadyStaged(target, file, seal)) return;

        var entry = archive.Resolve(file.Path)
            ?? throw new FileNotFoundException($"Arşivde yok: {file.Path}");
        var bytes = await archive.ExtractAsync(entry, cancellationToken);

        if (!string.Equals(UpdateCheck.HashBytes(bytes), file.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"İnen dosyanın özeti tutmadı: {file.Path}");

        var folder = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        var partial = target + PartialSuffix;
        try
        {
            await File.WriteAllBytesAsync(partial, bytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partial, target, overwrite: true);
            seal.Record(target, file.Sha256);
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    /// <summary>
    /// İptal edilen indirmenin sahnede bıraktığı yarım dosyaları siler. Özeti tutan tam
    /// dosyalar kalır; yeniden indirme onları atlar.
    /// </summary>
    public static void DiscardPartials(string baseDirectory)
    {
        var stage = Path.Combine(baseDirectory, StageDirectoryName);
        if (!Directory.Exists(stage)) return;
        foreach (var partial in Directory.EnumerateFiles(stage, "*" + PartialSuffix, SearchOption.AllDirectories))
            TryDelete(partial);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static async Task<string?> FetchManifestAsync(string rid, string? source, CancellationToken cancellationToken)
    {
        var asset = UpdateCheck.ManifestAssetName(rid);
        if (source is not null)
        {
            var local = Path.Combine(source, asset);
            return File.Exists(local) ? await File.ReadAllTextAsync(local, cancellationToken) : null;
        }

        return await UpdateCheck.FetchManifestAsync(AssetUrl(asset), cancellationToken);
    }

    private static IRangeSource Source(string asset, string? source, DownloadThrottle? throttle, string userAgent)
    {
        if (source is not null) return new FileRangeSource(Path.Combine(source, asset));
        return new HttpRangeSource(AssetUrl(asset), userAgent, throttle);
    }

    /// <summary>Yayın adresi; ölçüm ve deneme için VIDSHRINK_UPDATE_BASE_URL ile değiştirilebilir.</summary>
    public static string AssetUrl(string asset)
    {
        var baseUrl = Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_BASE_URL");
        return string.IsNullOrWhiteSpace(baseUrl)
            ? UpdateCheck.LatestAssetUrl(asset)
            : baseUrl.TrimEnd('/') + "/" + asset;
    }
}

/// <summary>
/// İndirmenin hız sınırı. Sınır yalnız <c>active</c> doğru dönerken işler (uygulamada:
/// oynatıcı oynarken); boşta indirme hattın hızıyla sürer. Pencere etkinlik değişince
/// sıfırlanır, yani oynatma başladığı an birikmiş bir borç ödenmez.
/// </summary>
public sealed class DownloadThrottle
{
    private readonly long _bytesPerSecond;
    private readonly Func<bool> _active;
    private readonly Func<TimeSpan> _clock;
    private readonly object _gate = new();
    private TimeSpan _windowStart;
    private long _windowBytes;
    private bool _wasActive;

    public DownloadThrottle(long bytesPerSecond, Func<bool>? active = null, Func<TimeSpan>? clock = null)
    {
        _bytesPerSecond = Math.Max(1, bytesPerSecond);
        _active = active ?? (() => true);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        _clock = clock ?? (() => watch.Elapsed);
    }

    /// <summary>Tek okumada istenecek en büyük parça; tamponlu okuma bu boyda ilerler.</summary>
    public const int ChunkBytes = 64 * 1024;

    /// <summary>
    /// <paramref name="bytes"/> okunduktan sonra beklenecek süre. Saf karar: sayaç ilerler,
    /// bekleme yapılmaz.
    /// </summary>
    public TimeSpan Account(int bytes)
    {
        lock (_gate)
        {
            var now = _clock();
            var active = _active();
            if (!active || !_wasActive)
            {
                _windowStart = now;
                _windowBytes = 0;
            }
            _wasActive = active;
            if (!active) return TimeSpan.Zero;

            _windowBytes += bytes;
            var allowed = TimeSpan.FromSeconds((double)_windowBytes / _bytesPerSecond);
            var wait = allowed - (now - _windowStart);
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }
    }

    public async Task PaceAsync(int bytes, CancellationToken cancellationToken)
    {
        var wait = Account(bytes);
        if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
    }
}

/// <summary>Yayın arşivinin yalnız istenen bayt aralığını çeken kaynak.</summary>
public sealed class HttpRangeSource : IRangeSource
{
    private readonly string _url;
    private readonly DownloadThrottle? _throttle;
    private readonly HttpClient _client = new(new SocketsHttpHandler
    {
        MaxConnectionsPerServer = 16,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    { Timeout = TimeSpan.FromSeconds(30) };
    private long _length = -1;

    public HttpRangeSource(string url, string userAgent = "VidShrink-Launcher", DownloadThrottle? throttle = null)
    {
        _url = url;
        _throttle = throttle;
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
    }

    public async Task<long> LengthAsync(CancellationToken cancellationToken)
    {
        if (_length >= 0) return _length;
        using var request = new HttpRequestMessage(HttpMethod.Head, _url);
        using var response = await _client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        _length = response.Content.Headers.ContentLength
            ?? throw new InvalidDataException("Arşivin boyutu bildirilmedi.");
        return _length;
    }

    /// <summary>
    /// Aralığı parça parça okur. Sınır verilmişse her parçadan sonra hesabını sorar;
    /// gövde başlıklardan sonra akıştan çekildiği için istek zaman aşımı yavaşlatılmış
    /// okumayı kesmez.
    /// </summary>
    public async Task<byte[]> ReadAsync(long offset, int length, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _url);
        request.Headers.Range = new RangeHeaderValue(offset, offset + length - 1);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode != HttpStatusCode.PartialContent)
            throw new InvalidDataException("Sunucu aralık isteğini karşılamadı.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[length];
        var read = 0;
        while (read < length)
        {
            var count = await stream.ReadAsync(
                buffer.AsMemory(read, Math.Min(DownloadThrottle.ChunkBytes, length - read)), cancellationToken);
            if (count == 0) break;
            read += count;
            if (_throttle is not null) await _throttle.PaceAsync(count, cancellationToken);
        }
        return read == length ? buffer : buffer[..read];
    }
}

/// <summary>
/// En düşük öncelikli, tek iş parçacıklı yürütücü. Verilen iş bu iş parçacığında başlar
/// ve her <c>await</c> devamı kendi kuyruğuyla yine buraya döner: açma, özet ve yazma
/// arayüz iş parçacığına da iş parçacığı havuzuna da düşmez, çekirdek kıtken ilk
/// bırakılan iş bu olur.
/// </summary>
public static class LowPriorityWork
{
    public static Task<T> Run<T>(string name, Func<Task<T>> work)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new QueueContext();
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                Task<T> task;
                try { task = work(); }
                catch (Exception exception) { task = Task.FromException<T>(exception); }
                task.ContinueWith(_ => context.Complete(), TaskScheduler.Default);
                context.Pump();
                if (task.IsCanceled) result.TrySetCanceled();
                else if (task.IsFaulted) result.TrySetException(task.Exception!.InnerExceptions);
                else result.TrySetResult(task.Result);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.Lowest,
            Name = name
        };
        thread.Start();
        return result.Task;
    }

    private sealed class QueueContext : SynchronizationContext, IDisposable
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback, object?)> _queue = new();

        public override void Post(SendOrPostCallback callback, object? state)
        {
            try { _queue.Add((callback, state)); }
            catch (InvalidOperationException) { ThreadPool.QueueUserWorkItem(_ => callback(state)); }
        }

        public override void Send(SendOrPostCallback callback, object? state) => callback(state);

        public void Complete() => _queue.CompleteAdding();

        public void Pump()
        {
            foreach (var (callback, state) in _queue.GetConsumingEnumerable()) callback(state);
        }

        public void Dispose() => _queue.Dispose();
    }
}
