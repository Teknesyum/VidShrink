using System.Text.Json;
using VidShrink.Core;
using System.Text.Json.Serialization;

namespace VidShrink.Core.Share;

/// <summary>
/// Bir paylaşım hedefi. Uç noktalar, boyut tavanı, ömür seçenekleri ve silme yeteneği
/// <c>paylasim-hedefleri.json</c> dosyasından okunur, koda gömülmez: bir servis ölür ya da
/// tavanını değiştirirse düzeltme JSON düzenlemesidir, sürüm çıkmaz.
/// </summary>
/// <remarks>
/// Şema T35 ve T36 arasında sabittir. Alan adları değiştirilirse arayüz tarafı sessizce
/// boş liste okur — değiştirmeden önce iki sözleşmeye birden bak.
/// </remarks>
public sealed record ShareTarget
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Dosya başına boyut tavanı, bayt.</summary>
    [JsonPropertyName("maxBytes")]
    public long MaxBytes { get; init; }

    /// <summary>Kullanıcının seçebileceği gün seçenekleri. Boşsa ömür seçilemiyordur.</summary>
    [JsonPropertyName("retentionDays")]
    public IReadOnlyList<int> RetentionDays { get; init; } = Array.Empty<int>();

    [JsonPropertyName("defaultRetentionDays")]
    public int? DefaultRetentionDays { get; init; }

    /// <summary>Ömür seçilemiyorsa sabit ömür, saat. uguu.se'de 3.</summary>
    [JsonPropertyName("fixedRetentionHours")]
    public int? FixedRetentionHours { get; init; }

    /// <summary>
    /// Gönderenin elinde silme jetonu oluyor mu. <c>false</c> ise arayüz silme düğmesini
    /// göstermez — istisna atılmaz, bayrağa bakılır.
    /// </summary>
    [JsonPropertyName("canDelete")]
    public bool CanDelete { get; init; }

    [JsonPropertyName("playsInBrowser")]
    public bool PlaysInBrowser { get; init; }

    [JsonPropertyName("endpoints")]
    public IReadOnlyDictionary<string, string> Endpoints { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Ömür seçilemiyorsa <c>true</c>.</summary>
    [JsonIgnore]
    public bool HasFixedRetention => RetentionDays.Count == 0 && FixedRetentionHours is not null;

    /// <summary>Bu hedefin verilen bayt sayısını kabul edip etmeyeceği.</summary>
    public bool Accepts(long bytes) => MaxBytes <= 0 || bytes <= MaxBytes;

    /// <summary>Adı verilen uç noktayı döndürür; tabloda yoksa boş.</summary>
    public string? Endpoint(string name) =>
        Endpoints.TryGetValue(name, out var url) && !string.IsNullOrWhiteSpace(url) ? url : null;
}

/// <summary>
/// <c>paylasim-hedefleri.json</c> dosyasının tamamı.
/// </summary>
public sealed record ShareTargetTable
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    /// <summary>Varsayılan hedefin kimliği.</summary>
    [JsonPropertyName("default")]
    public string Default { get; init; } = string.Empty;

    [JsonPropertyName("targets")]
    public IReadOnlyList<ShareTarget> Targets { get; init; } = Array.Empty<ShareTarget>();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Tablonun dosya adı. Arama sırası <see cref="Locate"/> içinde.</summary>
    public const string FileName = "paylasim-hedefleri.json";

    /// <summary>Ölçülmüş tavan: uguu.se ana sayfası "Max upload size is 128 MiB" diyor.</summary>
    public const long UguuMaxBytes = 134_217_728;

    /// <summary>storage.to'nun ilan ettiği 25 GB tavanı.</summary>
    public const long StorageToMaxBytes = 26_843_545_600;

    /// <summary>
    /// Dosya yokken arayüzün göstereceği liste: şemada yazılı varsayılanlar, uydurma değer
    /// değil. Uç nokta taşımaz — adres koda gömülmez, bu yüzden bu tabloyla yükleme
    /// yapılamaz, yalnız şerit çizilir ve pencere açılmaya devam eder.
    /// </summary>
    public static ShareTargetTable Fallback { get; } = new()
    {
        Default = "storage.to",
        Targets =
        [
            new ShareTarget
            {
                Id = "storage.to",
                DisplayName = "storage.to",
                MaxBytes = StorageToMaxBytes,
                RetentionDays = [1, 2, 3, 4, 5, 6, 7],
                DefaultRetentionDays = 1,
                CanDelete = true,
                PlaysInBrowser = true
            },
            new ShareTarget
            {
                Id = "uguu.se",
                DisplayName = "uguu.se",
                MaxBytes = UguuMaxBytes,
                FixedRetentionHours = 3,
                PlaysInBrowser = true
            }
        ]
    };

    public ShareTarget? Find(string id) =>
        Targets.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Varsayılan hedef; tabloda yoksa listedeki ilk hedef.</summary>
    public ShareTarget? DefaultTarget => Find(Default) ?? Targets.FirstOrDefault();

    /// <summary>
    /// Verilen boyutu kabul eden hedefler, tablodaki sırayla. Tavan aşımı hatasında
    /// "hangi hedef daha büyük" sorusunun cevabı buradan gelir.
    /// </summary>
    public IEnumerable<ShareTarget> Accepting(long bytes) => Targets.Where(t => t.Accepts(bytes));

    /// <summary>
    /// Verilen boyutu kabul eden en küçük tavanlı hedef. Sığmayan bir hedef yerine bunu
    /// öneririz: gereğinden büyük bir servise yollamak yerine işi görecek olanı seçeriz.
    /// Tavanı <c>0</c> olan hedef sınırsızdır ve en sona düşer.
    /// </summary>
    public ShareTarget? SmallestAccepting(long bytes, string? excludeId = null) =>
        Accepting(bytes)
            .Where(t => excludeId is null || !string.Equals(t.Id, excludeId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.MaxBytes <= 0 ? long.MaxValue : t.MaxBytes)
            .FirstOrDefault();

    public static ShareTargetTable Parse(string json) =>
        JsonSerializer.Deserialize(json, GevsekJson.Default.ShareTargetTable) ?? new ShareTargetTable();

    /// <summary>
    /// Tabloyu diskten okur. Yol verilmezse <see cref="Locate"/> ile aranır.
    /// </summary>
    /// <exception cref="FileNotFoundException">Tablo hiçbir yerde bulunamazsa.</exception>
    public static ShareTargetTable Load(string? path = null)
    {
        path ??= Locate() ?? throw new FileNotFoundException(
            $"{FileName} bulunamadı. Paylaşım hedefleri koda gömülü değildir; bu dosya olmadan " +
            "hedef listesi kurulamaz.",
            FileName);

        return Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Tabloyu okur, okunamazsa <see cref="Fallback"/> döner. Arayüzün kolu budur: hedef
    /// şeridi çizilemediği için pencerenin açılmaması kabul edilemez. Yükleme yolu hâlâ
    /// <see cref="Load(string?)"/> kullanır — orada dosya yoksa iş durmalı.
    /// </summary>
    /// <param name="locate">Arama; verilmezse <see cref="Locate"/>.</param>
    public static ShareTargetTable LoadOrFallback(Func<string?>? locate = null)
    {
        try
        {
            var path = (locate ?? Locate)();
            if (path is null) return Fallback;
            var table = Parse(File.ReadAllText(path));
            return table.Targets.Count == 0 ? Fallback : table;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return Fallback;
        }
    }

    /// <summary>
    /// Tabloyu arar: önce kullanıcının kendi kopyası (<c>%APPDATA%\VidShrink</c>), sonra
    /// uygulamanın yanı, sonra kaynak ağacının kökü. Bulunamazsa boş döner.
    /// </summary>
    /// <remarks>
    /// Kullanıcı kopyası önce gelir ki bir uç nokta ölünce kullanıcı sürüm beklemeden
    /// düzeltebilsin. Kaynak ağacı araması geliştirme ve test içindir.
    /// </remarks>
    public static string? Locate()
    {
        foreach (var candidate in Candidates())
        {
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    /// <summary>
    /// Aramanın adayları, sırasıyla. Arayüz tarafı da bu sırayı kullanır; iki taraf ayrı
    /// arama tutarsa kullanıcının kendi kopyası yalnız birine görünür.
    /// </summary>
    public static IEnumerable<string> AramaSirasi() => Candidates();

    private static IEnumerable<string> Candidates()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VidShrink",
            FileName);

        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, FileName);

        // Geliştirme ağacında ikili bin/<yapı>/<hedef> altında durur; kök yukarıdadır.
        var dir = new DirectoryInfo(baseDir);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            yield return Path.Combine(dir.FullName, FileName);
        }
    }
}
