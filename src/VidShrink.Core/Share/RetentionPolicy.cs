using System.Text.Json;

namespace VidShrink.Core.Share;

/// <summary>
/// Yüklenen dosyaların ne kadar süre Drive'da kalacağı.
/// </summary>
/// <remarks>
/// Varsayılan <b>sonsuz değildir</b>: sonsuz varsayılan kullanıcının Drive'ını sessizce
/// doldurur ve o 15 GB Gmail ile Photos'a da aittir, dolduğunda postası da etkilenir.
/// Varsayılan <see cref="DefaultMaxAge"/> (30 gün). Kullanıcı süreyi kapatabilir; kapatınca
/// <see cref="OffConsequence"/> gösterilmelidir.
/// </remarks>
public sealed record RetentionPolicy(TimeSpan? MaxAge)
{
    /// <summary>Varsayılan saklama süresi: 30 gün.</summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromDays(30);

    /// <summary>Süre kapatıldığında kullanıcıya söylenecek tek satır.</summary>
    public const string OffConsequence =
        "Saklama süresi kapalı: yüklediğiniz dosyalar siz silene kadar Drive'ınızda kalır. " +
        "Drive'ın 15 GB'ı Gmail ve Photos ile paylaşımlıdır, dolduğunda postanız da etkilenir.";

    public static RetentionPolicy Default => new(DefaultMaxAge);

    /// <summary>Süre yok: hiçbir şey kendiliğinden silinmez.</summary>
    public static RetentionPolicy Off => new((TimeSpan?)null);

    public bool Enabled => MaxAge is { } age && age > TimeSpan.Zero;

    public bool IsExpired(UploadedFile file, DateTimeOffset now) =>
        Enabled && file.Age(now) >= MaxAge!.Value;

    /// <summary>Süresi geçmiş yüklemeler. Süre kapalıysa liste boştur.</summary>
    public IReadOnlyList<UploadedFile> Expired(IEnumerable<UploadedFile> files, DateTimeOffset now) =>
        Enabled ? files.Where(f => IsExpired(f, now)).ToList() : Array.Empty<UploadedFile>();

    /// <summary>Ayarın yanında, <c>%APPDATA%</c> altında.</summary>
    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VidShrink",
            "saklama.json");

    /// <summary>Ayar yoksa veya okunamıyorsa varsayılana düşülür — sonsuza değil.</summary>
    public static RetentionPolicy Load(string? path = null)
    {
        var file = path ?? DefaultPath;
        try
        {
            if (!File.Exists(file)) return Default;
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (!document.RootElement.TryGetProperty("saklamaGunu", out var days)) return Default;
            return days.ValueKind switch
            {
                JsonValueKind.Null => Off,
                JsonValueKind.Number => days.GetDouble() > 0 ? new RetentionPolicy(TimeSpan.FromDays(days.GetDouble())) : Off,
                _ => Default
            };
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or FormatException)
        {
            return Default;
        }
    }

    public void Save(string? path = null)
    {
        var file = path ?? DefaultPath;
        var folder = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        var days = Enabled ? MaxAge!.Value.TotalDays.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null";
        File.WriteAllText(file, $"{{\n  \"saklamaGunu\": {days}\n}}\n");
    }
}

/// <summary>Silinemeyen bir dosya ve sebebi.</summary>
public sealed record CleanupFailure(UploadedFile File, ShareFailure Failure, string Detail);

/// <summary>
/// Bir temizliğin sonucu. Silmeden önce kullanıcıya sorulmaz — süre onun kararıdır — ama
/// <b>ne silindiği</b> buradan görülebilir.
/// </summary>
public sealed record CleanupReport(
    bool Ran,
    string SkipReason,
    IReadOnlyList<UploadedFile> Deleted,
    IReadOnlyList<CleanupFailure> Failed)
{
    public long FreedBytes => Deleted.Sum(f => f.SizeBytes);

    public static CleanupReport Skipped(string reason) =>
        new(false, reason, Array.Empty<UploadedFile>(), Array.Empty<CleanupFailure>());

    public static CleanupReport Done(IReadOnlyList<UploadedFile> deleted, IReadOnlyList<CleanupFailure> failed) =>
        new(true, string.Empty, deleted, failed);
}

/// <summary>Drive'ın kotası ve bunun ne kadarını uygulamanın yüklediği.</summary>
/// <remarks>
/// <see cref="StorageQuota.UsageOutsideDrive"/> Gmail ve Photos'un payını ayrı gösterir; kota
/// dolduğunda kullanıcı sebebini buradan görür. Kota okunamazsa <see cref="Quota"/> boştur.
/// </remarks>
public sealed record StorageReport(StorageQuota? Quota, long AppUsage, int AppFileCount)
{
    /// <summary>Kullanılan yerin ne kadarı VidShrink'in yüklediği dosyalar.</summary>
    public double? AppShareOfUsage =>
        Quota is null || Quota.Usage <= 0 ? null : Math.Clamp((double)AppUsage / Quota.Usage, 0.0, 1.0);

    /// <summary>Kotanın ne kadarı VidShrink'in yüklediği dosyalar.</summary>
    public double? AppShareOfLimit =>
        Quota?.Limit is not { } limit || limit <= 0 ? null : Math.Clamp((double)AppUsage / limit, 0.0, 1.0);
}

/// <summary>
/// Yer yönetimi: kaydı Drive'dan tazeler, süresi geçenleri temizler, toplu siler, kotayı okur.
/// </summary>
/// <remarks>
/// Listeleme her zaman Drive'dan yapılır, yerel kayıttan değil — kayıt bozulmuş olabilir.
/// <c>drive.file</c> kapsamı yalnız uygulamanın kendi yüklediklerini görür, kullanıcının
/// Drive'ının geri kalanı listeye girmez.
/// </remarks>
public sealed class DriveMaintenance
{
    private readonly DriveClient _client;
    private readonly SharedFileLog _log;
    private readonly Func<DateTimeOffset> _clock;

    public DriveMaintenance(DriveClient client, SharedFileLog log, Func<DateTimeOffset>? clock = null)
    {
        _client = client;
        _log = log;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Drive'dan listeler ve yerel kaydı bu listeyle baştan kurar. Drive'a ulaşılamazsa
    /// <c>null</c> döner ve yerel kayda dokunulmaz.
    /// </summary>
    public async Task<IReadOnlyList<UploadedFile>?> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var files = await _client.ListAppFilesAsync(cancellationToken);
        if (files is null) return null;
        _log.ReplaceAll(files);
        return files;
    }

    /// <summary>
    /// Süresi geçenleri sessizce siler. Açılışta arka planda çağrılır.
    /// Ağ yoksa atlanır; bir sonraki açılışta yeniden denenir.
    /// </summary>
    public async Task<CleanupReport> SweepAsync(RetentionPolicy policy, CancellationToken cancellationToken = default)
    {
        if (!policy.Enabled) return CleanupReport.Skipped("Saklama süresi kapalı.");

        var files = await RefreshAsync(cancellationToken);
        if (files is null)
        {
            return CleanupReport.Skipped("Drive'a ulaşılamadı; temizlik bir sonraki açılışta denenecek.");
        }

        return await DeleteAsync(policy.Expired(files, _clock()), cancellationToken);
    }

    /// <summary>
    /// Uygulamanın yüklediği her şeyi siler. Liste Drive'dan gelir, yerel kayıttan değil.
    /// Kullanıcının "hepsini temizle" dediği tek yer burasıdır.
    /// </summary>
    public async Task<CleanupReport> DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        var files = await RefreshAsync(cancellationToken);
        if (files is null) return CleanupReport.Skipped("Drive'a ulaşılamadı; liste alınamadı.");
        return await DeleteAsync(files, cancellationToken);
    }

    /// <summary>Verilen dosyaları teker teker siler. Zaten yok olan başarı sayılır.</summary>
    public async Task<CleanupReport> DeleteAsync(
        IReadOnlyList<UploadedFile> files,
        CancellationToken cancellationToken = default)
    {
        var deleted = new List<UploadedFile>();
        var failed = new List<CleanupFailure>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = await _client.DeleteFileAsync(file.FileId, cancellationToken);
            if (outcome.Ok) deleted.Add(file);
            else failed.Add(new CleanupFailure(file, outcome.Failure, outcome.Detail));
        }

        return CleanupReport.Done(deleted, failed);
    }

    /// <summary>Kota ve uygulamanın payı. Kota okunamazsa yalnız uygulamanın payı döner.</summary>
    public async Task<StorageReport> GetStorageReportAsync(CancellationToken cancellationToken = default)
    {
        var quota = await _client.GetQuotaAsync(cancellationToken);
        var files = await RefreshAsync(cancellationToken) ?? _log.Load();
        return new StorageReport(quota, files.Sum(f => f.SizeBytes), files.Count);
    }
}
