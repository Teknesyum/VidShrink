using System.Net;
using System.Net.Http.Headers;
using VidShrink.Core;

namespace VidShrink.Launcher;

/// <summary>
/// Sessiz fark güncellemesi. Manifest tek başına çekilir, yalnız özeti tutmayan dosyalar
/// arşivden aralık isteğiyle indirilir, hepsi doğrulandıktan sonra uygulama klasörüne
/// geçer. Hiçbir hata açılışı engellemez.
///
/// Manifestin <c>launcher</c> alanı kurulum kökündeki başlatıcıyı da sayar; o satırlar
/// kendi arşivinden inip <see cref="LauncherUpdate"/> üzerinden yerine geçer. Uygulama
/// dosyaları önce yerleşir, başlatıcı en son kurulur: sıra tersine dönerse yeni başlatıcı
/// eski uygulamayı açar. Sıranın kendisi <see cref="UpdateRollout"/> içinde.
///
/// Manifestin <c>shell</c> alanı kurulum kökündeki kabuk klasörünü sayar. O satırlar da
/// başlatıcının arşivinden iner ama geçiş dansına girmez: çalışan süreç onları tutmadığı
/// için <see cref="ShellUpdate"/> doğrudan üstlerine yazar.
///
/// Başlatıcı geçişi burada yapılmaz, yalnız kurulur. Geçişi çıkışta yerine geçecek ikili
/// yapar; döndürülen değer o çağrının gerekip gerekmediğidir.
///
/// Bu çağrı açılış yolunda değil: uygulama ekrana geldikten sonra koşar
/// (<c>Program.cs</c>). Eskiden açılış kapısının içindeydi ve bu yüzden bütçesi 90
/// saniyeydi; ölçülen 0.3.0 → 0.4.1 farkı 375 dosya ve 134,8 MB, yani o bütçede
/// bitmesi mümkün değildi. Yarıda kalan sahne de silindiği için her açılış sıfırdan
/// başlıyor, kurulum hiç yakınsamıyordu.
/// </summary>
internal static class Updater
{
    /// <summary>
    /// İndirme dahil tüm güncellemenin üst sınırı. Açılış yolunda olmadığı için geniş:
    /// yavaş hatta yarım kalan iş sahnede kalır, sonraki tur kaldığı yerden sürer.
    /// </summary>
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Aynı anda inen dosya sayısı. Dosyalar tek tek inerken hattın kendisi değil her
    /// isteğin gidiş dönüşü sınırdı: ölçülen 0.3.0 → 0.4.1 farkı 375 dosya, yani 375 ayrı
    /// tur. Şerit sayısı bunu böler; sayı bant genişliğini doyurmaya değil gecikmeyi
    /// örtmeye yetecek kadar, sunucuya yüklenmeyecek kadar küçük.
    /// </summary>
    private const int Lanes = 6;

    private const string StageDirectoryName = "update-stage";
    private const string HashCacheName = ".update-hashes.json";

    /// <summary>Sahnenin hangi sürüm için toplandığını söyleyen işaret.</summary>
    private const string StageVersionName = ".stage-version";

    /// <summary>İki başlatıcı aynı sahneye yazmasın; ikincisi hiç başlamaz.</summary>
    private const string MutexName = @"Global\Teknesyum.VidShrink.Update";

    /// <param name="force">
    /// Kullanıcı "Yükle" düğmesine bastı: kendiliğinden güncelleme ayarı okunmaz. Ayar yine
    /// yazılmaz, yani elle bir kez yüklemek tercihi değiştirmez.
    /// </param>
    /// <param name="progress">
    /// Paneli besleyen köprü. Verilmezse güncelleme sessiz koşar (açılıştan sonraki tur);
    /// verilirse her aşama kullanıcıya bir cümleyle görünür — indirilen dosya adı dahil.
    /// Kullanıcı elle "Yükle"ye bastığında ne olduğunu adım adım izlemesi bunun üstünden.
    /// </param>
    /// <param name="floor">Panelde bu işin başladığı yüzde.</param>
    /// <param name="ceiling">Panelde bu işin bitiş tavanı.</param>
    public static bool Run(
        string baseDirectory,
        string appDirectory,
        bool force = false,
        InstallProgress? progress = null,
        double floor = 0,
        double ceiling = 100)
    {
        // Ayar kapalıyken manifest bile çekilmez: kapatan kullanıcı ağ turunu da istemiyor.
        if (!force && !UpdateCheck.AutoUpdateEnabled()) return false;
        if (Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_DISABLED") == "1") return false;

        using var only = new Mutex(initiallyOwned: false, MutexName);
        var held = false;
        try { held = only.WaitOne(TimeSpan.Zero); }
        catch (AbandonedMutexException) { held = true; }
        if (!held) return false;

        using var cancellation = new CancellationTokenSource(Budget);
        try { return RunAsync(baseDirectory, appDirectory, progress, floor, ceiling, cancellation.Token).GetAwaiter().GetResult(); }
        catch (Exception) { return false; }
        finally { try { only.ReleaseMutex(); } catch (ApplicationException) { } }
    }

    /// <summary>Prova kipi: panel birebir aynı koşar ama inen sahne yerine taşınmaz.</summary>
    internal const string RehearsalVariable = "VIDSHRINK_UPDATE_PROVA";

    internal static bool Rehearsing =>
        Environment.GetEnvironmentVariable(RehearsalVariable) == "1";

    private static async Task<bool> RunAsync(
        string baseDirectory,
        string appDirectory,
        InstallProgress? progress,
        double floor,
        double ceiling,
        CancellationToken cancellationToken)
    {
        void Step(double part, double roof, string sentence) =>
            progress?.Step(floor + (ceiling - floor) * part, floor + (ceiling - floor) * roof, sentence);

        var stage = Path.Combine(baseDirectory, StageDirectoryName);
        var rid = UpdateCheck.Rid;
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_SOURCE");

        Step(0, 0.10, "Sürüm listesi alınıyor");
        var json = await FetchManifestAsync(rid, source, cancellationToken);
        if (json is null)
        {
            Step(0.10, 0.10, "Sürüm listesine ulaşılamadı, güncelleme atlandı");
            return false;
        }

        var manifest = UpdateCheck.ParseManifest(json);
        if (manifest.Files.Count == 0) return false;

        // Aynı sürüm zaten uygulanmışsa hiçbir dosya özetlenmez. Kapı başlatıcının
        // sürümüne de bakıyor; yoksa geride kalan bir başlatıcı hiç fark edilmiyor.
        if (UpdateCheck.AlreadyCurrent(baseDirectory, appDirectory, manifest))
        {
            Step(1, 1, "Program zaten güncel");
            return false;
        }

        var cache = new HashCache(Path.Combine(appDirectory, HashCacheName));
        var changed = UpdateCheck.Diff(appDirectory, manifest, cache);
        var launcherChanged = UpdateCheck.Diff(baseDirectory, manifest.Launcher, cache);
        var shellChanged = UpdateCheck.Diff(baseDirectory, manifest.Shell, cache);
        cache.Save();
        Step(0.12, 0.20, "Sürüm " + manifest.Version + " bulundu, " +
            (changed.Count + launcherChanged.Count + shellChanged.Count) + " dosya yenilenecek");
        if (changed.Count == 0 && launcherChanged.Count == 0 && shellChanged.Count == 0)
        {
            Step(1, 1, "Program zaten güncel");
            UpdateCheck.WriteVersionMarker(appDirectory, manifest.Version);
            LauncherUpdate.MarkVerified(baseDirectory, manifest);
            return false;
        }

        // Sahne yalnız başka bir sürüm için toplandıysa atılır. Aynı sürümün yarım kalmış
        // sahnesi duruyorsa korunur: özeti tutan dosya bir daha indirilmez, yavaş hatta
        // güncelleme turlar boyunca yakınsar.
        if (StageVersion(stage) != manifest.Version)
        {
            UpdateStage.Discard(stage);
            Directory.CreateDirectory(stage);
            WriteStageVersion(stage, manifest.Version);
        }
        else Directory.CreateDirectory(stage);

        // Hata sahneyi silmez: zaman aşımı ya da kopan hat yarım bir indirme bırakır, o
        // dosyalar sonraki turda özetiyle sınanır ve tutanlar atlanır. Özeti tutmayanı
        // StageFileAsync hiç yazmıyor, yani yarım sahne yanlış bayt taşımıyor.
        var total = changed.Count + launcherChanged.Count + shellChanged.Count;
        var done = 0;

        // Şeritler paralel indiği için sayaç kilitli artıyor; panele düşen sıra inişin
        // bittiği sıradır, dosya listesinin sırası değil.
        void Downloaded(ManifestFile file)
        {
            var sira = Interlocked.Increment(ref done);
            var part = 0.20 + 0.70 * sira / Math.Max(1, total);
            Step(part, Math.Min(0.90, part + 0.70 / Math.Max(1, total)),
                Path.GetFileName(file.Path) + " indi (" + sira + "/" + total + ")");
        }

        async Task Fetch(RemoteZip archive, IReadOnlyList<ManifestFile> files, Func<ManifestFile, string> target)
        {
            if (files.Count == 0) return;
            await Parallel.ForEachAsync(
                files,
                new ParallelOptions { MaxDegreeOfParallelism = Lanes, CancellationToken = cancellationToken },
                async (file, token) =>
                {
                    await StageFileAsync(archive, file, target(file), token);
                    Downloaded(file);
                });
        }

        if (changed.Count > 0)
        {
            var archive = await RemoteZip.OpenAsync(ArchiveSource(rid, source), cancellationToken);
            await Fetch(archive, changed, file => UpdateCheck.LocalPath(stage, file.Path));
        }

        if (launcherChanged.Count > 0 || shellChanged.Count > 0)
        {
            var archive = await RemoteZip.OpenAsync(LauncherArchiveSource(rid, source), cancellationToken);
            await Fetch(archive, launcherChanged, file => LauncherUpdate.StagePath(stage, file.Path));
            await Fetch(archive, shellChanged, file => ShellUpdate.StagePath(stage, file.Path));
        }

        // Prova kipi: inen her şey sahnede durur, hiçbir dosya yerine taşınmaz ve
        // başlatıcı geçişi kurulmaz. Panelin kendisi birebir aynı koşar.
        if (Rehearsing)
        {
            Step(1, 1, "Prova kipi: " + total + " dosya indirildi, kurulum yapılmadı");
            return false;
        }

        // Başlatıcı yan klasörden çıkarılıyor: bir alttaki Apply yan klasörü siliyor.
        LauncherUpdate.Stage(stage, baseDirectory, launcherChanged);

        Step(0.92, 1, "Dosyalar yerine taşınıyor");
        var applied = UpdateRollout.Apply(stage, baseDirectory, appDirectory, changed, launcherChanged, manifest, shellChanged);
        Step(1, 1, "Sürüm " + manifest.Version + " kuruldu");
        return applied;
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

    /// <summary>Sahnedeki dosya boyutu ve özetiyle manifeste oturuyor mu.</summary>
    private static bool AlreadyStaged(string target, ManifestFile file)
    {
        if (!File.Exists(target)) return false;
        try
        {
            if (new FileInfo(target).Length != file.Size) return false;
            return string.Equals(UpdateCheck.HashFile(target), file.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException) { return false; }
    }

    private static async Task StageFileAsync(RemoteZip archive, ManifestFile file, string target, CancellationToken cancellationToken)
    {
        if (AlreadyStaged(target, file)) return;

        var entry = archive.Resolve(file.Path)
            ?? throw new FileNotFoundException($"Arşivde yok: {file.Path}");
        var bytes = await archive.ExtractAsync(entry, cancellationToken);

        // İnen her dosyanın özeti manifesttekiyle karşılaştırılır; tutmayan atılır.
        if (!string.Equals(UpdateCheck.HashBytes(bytes), file.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"İnen dosyanın özeti tutmadı: {file.Path}");

        var folder = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(target, bytes, cancellationToken);
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

    private static IRangeSource ArchiveSource(string rid, string? source) =>
        Source(UpdateCheck.ArchiveAssetName(rid), source);

    private static IRangeSource LauncherArchiveSource(string rid, string? source) =>
        Source(UpdateCheck.LauncherArchiveAssetName(rid), source);

    private static IRangeSource Source(string asset, string? source)
    {
        if (source is not null) return new FileRangeSource(Path.Combine(source, asset));
        return new HttpRangeSource(AssetUrl(asset));
    }

    /// <summary>Yayın adresi; ölçüm ve deneme için VIDSHRINK_UPDATE_BASE_URL ile değiştirilebilir.</summary>
    private static string AssetUrl(string asset)
    {
        var baseUrl = Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_BASE_URL");
        return string.IsNullOrWhiteSpace(baseUrl)
            ? UpdateCheck.LatestAssetUrl(asset)
            : baseUrl.TrimEnd('/') + "/" + asset;
    }
}

/// <summary>Yayın arşivinin yalnız istenen bayt aralığını çeken kaynak.</summary>
internal sealed class HttpRangeSource : IRangeSource
{
    private readonly string _url;
    private readonly HttpClient _client = new(new SocketsHttpHandler
    {
        // Şeritlerin hepsi aynı sunucuya bakıyor; varsayılan sınır bağlantıları sıraya
        // sokup paralelliği boşa çıkarırdı.
        MaxConnectionsPerServer = 16,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    { Timeout = TimeSpan.FromSeconds(30) };
    private long _length = -1;

    public HttpRangeSource(string url)
    {
        _url = url;
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("VidShrink-Launcher");
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

    public async Task<byte[]> ReadAsync(long offset, int length, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _url);
        request.Headers.Range = new RangeHeaderValue(offset, offset + length - 1);
        using var response = await _client.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.PartialContent)
            throw new InvalidDataException("Sunucu aralık isteğini karşılamadı.");
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
