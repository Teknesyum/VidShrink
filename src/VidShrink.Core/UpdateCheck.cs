using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VidShrink.Core;

/// <summary>
/// Manifestteki tek dosya. <see cref="Path"/> yayın klasörüne göreli ve ayracı her zaman '/'.
/// </summary>
public sealed record ManifestFile(string Path, string Sha256, long Size);

public sealed record ReleaseManifest(
    string Version,
    string Commit,
    DateTimeOffset Built,
    string Rid,
    IReadOnlyList<ManifestFile> Files)
{
    /// <summary>
    /// Başlatıcının dosyaları. <see cref="Files"/> uygulama klasörüne göreli, bu liste
    /// kurulum köküne göreli; ikisi ayrı alanda çünkü ayrı arşivlerden iniyorlar ve ayrı
    /// yerlere yazılıyorlar. Alanı tanımayan eski bir güncelleyici burayı hiç görmez ve
    /// eskisi gibi yalnız <c>app/</c> klasörünü günceller.
    /// </summary>
    public IReadOnlyList<ManifestFile> Launcher { get; init; } = Array.Empty<ManifestFile>();
}

/// <summary>Mimarinin nasıl belirlendiği. Kullanıcıya ne söyleneceğini bu ayırıyor.</summary>
public enum ArchitectureOutcome
{
    /// <summary>Bir kaynak tanınan bir mimari adı verdi.</summary>
    Read,

    /// <summary>Hiçbir kaynak ad vermedi; mimari işletim sisteminin bit genişliğinden varsayıldı.</summary>
    Assumed
}

/// <summary>
/// Mimari kararı. <c>Architecture</c> hiçbir zaman boş değil — okunamayan bir değer kullanıcıya
/// boş basılmasın diye. <c>Note</c> yalnız varsayım yapıldığında dolu.
/// </summary>
public sealed record ArchitectureDecision(ArchitectureOutcome Outcome, string Architecture, string Note);

/// <summary>
/// Mimari tek bir kuralla belirleniyor ve iki taraf da bu kuralı izliyor: <see cref="UpdateCheck.Rid"/>
/// ile <c>Install-VidShrink.ps1</c>. Ayrıştıkları sürüm bir kullanıcının kurulumunu düşürdü —
/// kurucu boş okumayı "desteklenmeyen mimari" sayıp reddederken güncelleyici aynı boşluğu
/// sessizce x64 kabul ediyordu.
/// </summary>
public static class ArchitectureChoice
{
    /// <summary>
    /// Tanınan adlar. Aynı mimari kaynağa göre başka yazılıyor: .NET "X64" der,
    /// <c>PROCESSOR_ARCHITECTURE</c> "AMD64", <c>uname -m</c> "x86_64". Tanınmayan ad
    /// <c>null</c> döner; tanınmamak reddedilmek değildir.
    /// </summary>
    public static string? Recognize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToUpperInvariant() switch
        {
            "X64" or "AMD64" or "X86_64" or "EM64T" => "x64",
            "ARM64" or "AARCH64" => "arm64",
            "X86" or "I386" or "I486" or "I586" or "I686" => "x86",
            "ARM" or "ARMV6L" or "ARMV7L" => "arm",
            _ => null
        };
    }

    /// <summary>
    /// Kaynaklar sırayla deneniyor. Sıranın gerekçesi:
    /// <list type="number">
    /// <item><c>RuntimeInformation.OSArchitecture</c> — işletim sisteminin kendi mimarisi;
    /// 64 bit Windows üzerinde koşan 32 bit bir süreçte bile doğrusunu verir. Ama her yerde
    /// okunamıyor: Windows PowerShell 5.1'in altındaki .NET Framework 4.7.1'den eskiyse tip
    /// hiç yoktur, kısıtlı dil kipinde de statik üyeye erişilemez. İkisinde de elde boş kalır.</item>
    /// <item><c>PROCESSOR_ARCHITEW6432</c> — yalnız WOW64 altında dolu ve işletim sisteminin
    /// mimarisini söyler; doluysa bir alttakinden daha doğrudur.</item>
    /// <item><c>PROCESSOR_ARCHITECTURE</c> — sürecin mimarisi. WOW64 altında "x86" der, bu
    /// yüzden ancak yukarıdaki ikisi susunca kullanılıyor.</item>
    /// <item>Hiçbiri ad vermezse geriye bit genişliği kalıyor. Bu bir okuma değil varsayım,
    /// ve varsayıldığı söyleniyor.</item>
    /// </list>
    /// </summary>
    public static ArchitectureDecision Decide(
        string? runtimeArchitecture,
        string? processorArchitew6432,
        string? processorArchitecture,
        bool is64BitOperatingSystem)
    {
        foreach (var candidate in new[] { runtimeArchitecture, processorArchitew6432, processorArchitecture })
        {
            var name = Recognize(candidate);
            if (name is not null) return new ArchitectureDecision(ArchitectureOutcome.Read, name, string.Empty);
        }

        if (is64BitOperatingSystem)
            return new ArchitectureDecision(
                ArchitectureOutcome.Assumed,
                "x64",
                "Mimari okunamadı; işletim sistemi 64 bit olduğu için x64 varsayıldı.");

        return new ArchitectureDecision(
            ArchitectureOutcome.Assumed,
            "x86",
            "Mimari okunamadı; işletim sistemi 32 bit olduğu için x86 kabul edildi.");
    }

    public static ArchitectureDecision Decide() => Decide(
        RuntimeInformation.OSArchitecture.ToString(),
        Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"),
        Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"),
        Environment.Is64BitOperatingSystem);
}

/// <summary>
/// Sürüm karşılaştırması, manifest okuma ve fark hesabı. Platformdan bağımsız; indirme ve
/// dosya değiştirme burada değil, başlatıcıda ve yalnız Windows'ta yaşıyor.
/// </summary>
public static class UpdateCheck
{
    public const string VersionMarkerName = ".update-version";

    /// <summary>Uygulamanın kendini güncelleyebildiği tek platform Windows.</summary>
    public static bool CanSelfUpdate => OperatingSystem.IsWindows();

    /// <summary>
    /// Sessiz güncelleme yürürlükte mi. Kapalıysa ve Windows dışında, uygulama yalnız
    /// haber verir: aynı dal, iki eksen.
    /// </summary>
    public static bool AutoUpdateEnabled(UpdateSettings? settings = null) =>
        CanSelfUpdate && (settings ?? UpdateSettings.Load()).AutoUpdate;

    /// <summary>
    /// Çalışılan platformun yayın kimliği. Mimari kararı <see cref="ArchitectureChoice"/>'da:
    /// okunamayan mimaride 64 bitlik sistemde x64 varsayılıyor. Eskiden bu varsayım burada
    /// tanımsız bir <c>_ =&gt; "x64"</c> dalıydı ve kurucu aynı durumu reddediyordu; artık ikisi
    /// de aynı kuralı okuyor, çünkü ayrıştıklarında biri kuruluyor öteki hiç güncelleme bulamıyor.
    /// </summary>
    public static string Rid
    {
        get
        {
            var arch = ArchitectureChoice.Decide().Architecture;
            if (OperatingSystem.IsWindows()) return "win-" + arch;
            if (OperatingSystem.IsMacOS()) return "osx-" + arch;
            return "linux-" + arch;
        }
    }

    public static string ManifestAssetName(string rid) => $"manifest-{rid}.json";

    public static string ArchiveAssetName(string rid) => $"vidshrink-{rid}.zip";

    /// <summary>
    /// Başlatıcı uygulama arşivinin içinde değil, kendi arşivinde. Kurulum betiği zaten bu
    /// varlığı indiriyordu; güncelleyici de aynı adı kullanıyor ki yayında ikinci bir kopya
    /// taşınmasın.
    /// </summary>
    public static string LauncherArchiveAssetName(string rid) => $"vidshrink-launcher-{rid}.zip";

    public static string LatestAssetUrl(string asset) =>
        $"https://github.com/Teknesyum/VidShrink/releases/latest/download/{asset}";

    /// <summary>Manifest çekmenin zaman aşımı; açılışın gecikebileceği en uzun süre budur.</summary>
    public static readonly TimeSpan ManifestTimeout = TimeSpan.FromMilliseconds(800);

    public static async Task<string?> FetchManifestAsync(string url, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = ManifestTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VidShrink-Launcher");
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public static bool AlreadyCurrent(string appDirectory, ReleaseManifest manifest) =>
        ReadVersionMarker(appDirectory) == manifest.Version;

    /// <summary>
    /// Aynı kapı, başlatıcıyı da sayarak. Tek işarete bakan sürüm, uygulama yeni sürüme
    /// geçtikten sonra başlatıcı eski kalsa bile "zaten güncel" diyor ve o farkı bir daha
    /// hiç görmüyordu; kurulu başlatıcıların düzeltme alamamasının sebebi buydu.
    ///
    /// İnen başlatıcı yan adda bekliyorsa da güncel sayılır (<see cref="LauncherUpdate.Armed"/>):
    /// sürüm işaretini ancak geçişin oturması yazıyor, o yüzden yalnız işarete bakmak
    /// bekleyen geçiş boyunca her açılışta aynı arşivi yeniden indirmek demekti.
    /// </summary>
    public static bool AlreadyCurrent(string baseDirectory, string appDirectory, ReleaseManifest manifest) =>
        ReadVersionMarker(appDirectory) == manifest.Version
        && (manifest.Launcher.Count == 0
            || LauncherUpdate.ReadVersionMarker(baseDirectory) == manifest.Version
            || LauncherUpdate.Armed(baseDirectory, manifest));

    /// <summary>Kendini güncelleyemeyen platformlarda kullanıcıya gösterilecek komut.</summary>
    public static string UpdateInstruction()
    {
        if (OperatingSystem.IsWindows())
            return "irm https://raw.githubusercontent.com/Teknesyum/VidShrink/main/Install-VidShrink.ps1 | iex";
        return "curl -fsSL https://raw.githubusercontent.com/Teknesyum/VidShrink/main/install-vidshrink.sh | sh";
    }

    public static string CurrentVersion(Assembly? assembly = null)
    {
        var target = assembly ?? Assembly.GetEntryAssembly();
        var informational = target?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational)) return informational!;
        return target?.GetName().Version?.ToString() ?? "0.0.0";
    }

    /// <summary>Aday sürüm yereldekinden yeni mi. Çözümlenemeyen sürümde güncelleme yapılmaz.</summary>
    public static bool IsNewer(string candidate, string current)
    {
        if (!TryParseVersion(candidate, out var candidateParts, out var candidatePre)) return false;
        if (!TryParseVersion(current, out var currentParts, out var currentPre)) return false;

        for (var i = 0; i < 4; i++)
        {
            if (candidateParts[i] > currentParts[i]) return true;
            if (candidateParts[i] < currentParts[i]) return false;
        }

        // Sayısal kısım aynıysa yayın öncesi sürüm yayınlanmış sürümden eskidir.
        if (candidatePre is null && currentPre is not null) return true;
        if (candidatePre is not null && currentPre is not null)
            return string.CompareOrdinal(candidatePre, currentPre) > 0;
        return false;
    }

    private static bool TryParseVersion(string text, out int[] parts, out string? prerelease)
    {
        parts = new int[4];
        prerelease = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var value = text.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value[1..];

        var plus = value.IndexOf('+');
        if (plus >= 0) value = value[..plus];

        var dash = value.IndexOf('-');
        if (dash >= 0)
        {
            prerelease = value[(dash + 1)..];
            value = value[..dash];
        }

        var segments = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return false;
        for (var i = 0; i < segments.Length && i < 4; i++)
        {
            if (!int.TryParse(segments[i], out var number)) return false;
            parts[i] = number;
        }
        return true;
    }

    public static ReleaseManifest ParseManifest(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var built = root.TryGetProperty("built", out var builtElement) && builtElement.ValueKind == JsonValueKind.String
                ? DateTimeOffset.Parse(builtElement.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind)
                : DateTimeOffset.MinValue;

            return new ReleaseManifest(
                root.GetProperty("version").GetString() ?? throw new InvalidDataException("Manifestte version yok."),
                root.TryGetProperty("commit", out var commit) ? commit.GetString() ?? "" : "",
                built,
                root.TryGetProperty("rid", out var rid) ? rid.GetString() ?? "" : "",
                ReadFileList(root, "files"))
            {
                Launcher = ReadFileList(root, "launcher")
            };
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        {
            throw new InvalidDataException("Manifest çözümlenemedi.", exception);
        }
    }

    private static IReadOnlyList<ManifestFile> ReadFileList(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
            return Array.Empty<ManifestFile>();

        var files = new List<ManifestFile>();
        foreach (var element in array.EnumerateArray())
        {
            var path = element.GetProperty("path").GetString();
            var sha = element.GetProperty("sha256").GetString();
            var size = element.GetProperty("size").GetInt64();
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(sha))
                throw new InvalidDataException("Manifestte path veya sha256 boş.");
            files.Add(new ManifestFile(path!.Replace('\\', '/'), sha!.ToLowerInvariant(), size));
        }
        return files;
    }

    public static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024);
        return HashStream(stream);
    }

    public static string HashStream(Stream stream)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    public static string HashBytes(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>Yereldeki klasörle manifesti karşılaştırır; indirilmesi gereken dosyaları döndürür.</summary>
    public static IReadOnlyList<ManifestFile> Diff(string appDirectory, ReleaseManifest manifest, HashCache? cache = null) =>
        Diff(appDirectory, manifest.Files, cache);

    /// <summary>
    /// Aynı karşılaştırma, listesi dışarıdan verilerek. Başlatıcının satırları kurulum
    /// köküne göreli olduğu için uygulama klasörüyle aynı kapıdan geçemiyor.
    /// </summary>
    public static IReadOnlyList<ManifestFile> Diff(string directory, IReadOnlyList<ManifestFile> files, HashCache? cache = null)
    {
        var changed = new List<ManifestFile>();
        foreach (var file in files)
        {
            var local = LocalPath(directory, file.Path);
            var info = new FileInfo(local);
            if (!info.Exists || info.Length != file.Size)
            {
                changed.Add(file);
                continue;
            }

            var sha = cache is null ? HashFile(local) : cache.Hash(local);
            if (!string.Equals(sha, file.Sha256, StringComparison.OrdinalIgnoreCase)) changed.Add(file);
        }
        return changed;
    }

    public static string LocalPath(string directory, string manifestPath) =>
        Path.Combine(directory, manifestPath.Replace('/', Path.DirectorySeparatorChar));

    public static string? ReadVersionMarker(string appDirectory)
    {
        var marker = Path.Combine(appDirectory, VersionMarkerName);
        try { return File.Exists(marker) ? File.ReadAllText(marker).Trim() : null; }
        catch (IOException) { return null; }
    }

    public static void WriteVersionMarker(string appDirectory, string version) =>
        File.WriteAllText(Path.Combine(appDirectory, VersionMarkerName), version);
}

/// <summary>
/// Kullanıcı ayarı. Kurulum klasörünün yanında değil, %APPDATA%\VidShrink altında durur;
/// kurulum silinip yeniden yapılınca ayar kaybolmaz.
/// </summary>
public sealed class UpdateSettings
{
    public const string FolderName = "VidShrink";
    public const string FileName = "settings.json";

    /// <summary>Windows'ta varsayılan açık. Kapalıyken uygulama yalnız haber verir.</summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>
    /// Hızlı düşür (GPU) kutusunun durumu. Alan yoksa karar henüz verilmemiştir; ilk
    /// açılışta donanım yoklaması karar verir ve buraya yazar. Değer bir kez yazıldıktan
    /// sonra program üstüne yazmaz — kullanıcı kutuyu elle değiştirdiyse o karar kalır.
    /// </summary>
    public bool? FastGpu { get; set; }
    public string? Language { get; set; }
    public double TargetMb { get; set; } = 16;
    public double QualityTarget { get; set; } = 60;
    public int Intent { get; set; } = 1;
    public int Codec { get; set; }
    public bool MayLowerResolution { get; set; } = true;
    public bool MayLowerFps { get; set; } = true;
    public int FillPolicy { get; set; }
    public int HdrPolicy { get; set; }
    public int QualityMode { get; set; }
    public int QualityValue { get; set; } = 23;
    public int Container { get; set; }
    public int ConvertCodec { get; set; }
    public int Resolution { get; set; }
    public string CustomResolution { get; set; } = "1280x720";
    public int ConvertFps { get; set; }
    public string CustomFps { get; set; } = "25";
    public int ConvertAudio { get; set; }
    public string AudioBitrate { get; set; } = "128";
    public string TrimStart { get; set; } = "";
    public string TrimEnd { get; set; } = "";
    public int ShareTarget { get; set; }
    public int ShareRetention { get; set; }

    /// <summary>VIDSHRINK_SETTINGS_PATH yalnız ölçüm ve deneme için ayarı başka yere alır.</summary>
    public static string DefaultPath
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
            if (!string.IsNullOrWhiteSpace(overridden)) return overridden!;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                FolderName,
                FileName);
        }
    }

    public static UpdateSettings Load(string? path = null)
    {
        var file = path ?? DefaultPath;
        var settings = new UpdateSettings();
        try
        {
            if (!File.Exists(file)) return settings;
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.TryGetProperty("autoUpdate", out var value) &&
                (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
            {
                settings.AutoUpdate = value.GetBoolean();
            }
            if (document.RootElement.TryGetProperty("fastGpu", out var fastGpu) &&
                (fastGpu.ValueKind == JsonValueKind.True || fastGpu.ValueKind == JsonValueKind.False))
            {
                settings.FastGpu = fastGpu.GetBoolean();
            }
            ReadString(document.RootElement, "language", value => settings.Language = value);
            ReadDouble(document.RootElement, "targetMb", value => settings.TargetMb = value);
            ReadDouble(document.RootElement, "qualityTarget", value => settings.QualityTarget = value);
            ReadInt(document.RootElement, "intent", value => settings.Intent = value);
            ReadInt(document.RootElement, "codec", value => settings.Codec = value);
            ReadBool(document.RootElement, "mayLowerResolution", value => settings.MayLowerResolution = value);
            ReadBool(document.RootElement, "mayLowerFps", value => settings.MayLowerFps = value);
            ReadInt(document.RootElement, "fillPolicy", value => settings.FillPolicy = value);
            ReadInt(document.RootElement, "hdrPolicy", value => settings.HdrPolicy = value);
            ReadInt(document.RootElement, "qualityMode", value => settings.QualityMode = value);
            ReadInt(document.RootElement, "qualityValue", value => settings.QualityValue = value);
            ReadInt(document.RootElement, "container", value => settings.Container = value);
            ReadInt(document.RootElement, "convertCodec", value => settings.ConvertCodec = value);
            ReadInt(document.RootElement, "resolution", value => settings.Resolution = value);
            ReadString(document.RootElement, "customResolution", value => settings.CustomResolution = value);
            ReadInt(document.RootElement, "convertFps", value => settings.ConvertFps = value);
            ReadString(document.RootElement, "customFps", value => settings.CustomFps = value);
            ReadInt(document.RootElement, "convertAudio", value => settings.ConvertAudio = value);
            ReadString(document.RootElement, "audioBitrate", value => settings.AudioBitrate = value);
            ReadString(document.RootElement, "trimStart", value => settings.TrimStart = value);
            ReadString(document.RootElement, "trimEnd", value => settings.TrimEnd = value);
            ReadInt(document.RootElement, "shareTarget", value => settings.ShareTarget = value);
            ReadInt(document.RootElement, "shareRetention", value => settings.ShareRetention = value);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // Okunamayan ayar varsayılana düşer; açılış hiçbir koşulda durmaz.
        }
        return settings;
    }

    private static void ReadBool(JsonElement root, string name, Action<bool> apply)
    {
        if (root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False) apply(value.GetBoolean());
    }

    private static void ReadInt(JsonElement root, string name, Action<int> apply)
    {
        if (root.TryGetProperty(name, out var value) && value.TryGetInt32(out var found)) apply(found);
    }

    private static void ReadDouble(JsonElement root, string name, Action<double> apply)
    {
        if (root.TryGetProperty(name, out var value) && value.TryGetDouble(out var found) && double.IsFinite(found)) apply(found);
    }

    private static void ReadString(JsonElement root, string name, Action<string> apply)
    {
        if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) apply(value.GetString() ?? "");
    }

    public void Save(string? path = null)
    {
        var file = path ?? DefaultPath;
        var folder = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        using var stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteBoolean("autoUpdate", AutoUpdate);
        if (FastGpu.HasValue) writer.WriteBoolean("fastGpu", FastGpu.Value);
        if (!string.IsNullOrWhiteSpace(Language)) writer.WriteString("language", Language);
        writer.WriteNumber("targetMb", TargetMb);
        writer.WriteNumber("qualityTarget", QualityTarget);
        writer.WriteNumber("intent", Intent);
        writer.WriteNumber("codec", Codec);
        writer.WriteBoolean("mayLowerResolution", MayLowerResolution);
        writer.WriteBoolean("mayLowerFps", MayLowerFps);
        writer.WriteNumber("fillPolicy", FillPolicy);
        writer.WriteNumber("hdrPolicy", HdrPolicy);
        writer.WriteNumber("qualityMode", QualityMode);
        writer.WriteNumber("qualityValue", QualityValue);
        writer.WriteNumber("container", Container);
        writer.WriteNumber("convertCodec", ConvertCodec);
        writer.WriteNumber("resolution", Resolution);
        writer.WriteString("customResolution", CustomResolution);
        writer.WriteNumber("convertFps", ConvertFps);
        writer.WriteString("customFps", CustomFps);
        writer.WriteNumber("convertAudio", ConvertAudio);
        writer.WriteString("audioBitrate", AudioBitrate);
        writer.WriteString("trimStart", TrimStart);
        writer.WriteString("trimEnd", TrimEnd);
        writer.WriteNumber("shareTarget", ShareTarget);
        writer.WriteNumber("shareRetention", ShareRetention);
        writer.WriteEndObject();
        writer.Flush();
    }

    public static void Delete(string? path = null)
    {
        var file = path ?? DefaultPath;
        if (File.Exists(file)) File.Delete(file);
    }
}

/// <summary>
/// Yerel özetleri yol + boyut + son yazma tarihiyle önbelleğe alır; her açılışta bütün
/// kurulumun yeniden özetlenmesini engeller.
/// </summary>
public sealed class HashCache
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _path;
    private bool _dirty;

    public int Hits { get; private set; }
    public int Misses { get; private set; }

    private sealed record Entry(long Size, long Ticks, string Sha256);

    public HashCache(string? path = null)
    {
        _path = path;
        if (path is null || !File.Exists(path)) return;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value;
                _entries[property.Name] = new Entry(
                    value.GetProperty("size").GetInt64(),
                    value.GetProperty("ticks").GetInt64(),
                    value.GetProperty("sha256").GetString() ?? "");
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or KeyNotFoundException)
        {
            _entries.Clear();
        }
    }

    public string Hash(string fullPath)
    {
        var info = new FileInfo(fullPath);
        var ticks = info.LastWriteTimeUtc.Ticks;
        if (_entries.TryGetValue(fullPath, out var entry) && entry.Size == info.Length && entry.Ticks == ticks)
        {
            Hits++;
            return entry.Sha256;
        }

        Misses++;
        var sha = UpdateCheck.HashFile(fullPath);
        _entries[fullPath] = new Entry(info.Length, ticks, sha);
        _dirty = true;
        return sha;
    }

    public void Save()
    {
        if (_path is null || !_dirty) return;
        try
        {
            using var stream = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new Utf8JsonWriter(stream);
            writer.WriteStartObject();
            foreach (var pair in _entries)
            {
                writer.WriteStartObject(pair.Key);
                writer.WriteNumber("size", pair.Value.Size);
                writer.WriteNumber("ticks", pair.Value.Ticks);
                writer.WriteString("sha256", pair.Value.Sha256);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
            writer.Flush();
            _dirty = false;
        }
        catch (IOException) { }
    }
}

/// <summary>
/// İnen dosyalar önce yan bir klasörde toplanır, hepsi doğrulandıktan sonra uygulama
/// klasörüne kopyalanır. Kopyalama sırasında süreç ölürse günlük dosyası kalır ve bir
/// sonraki açılışta iş tamamlanır; yarım uygulanmış bir klasör dışarıya görünmez.
/// </summary>
public static class UpdateStage
{
    public const string JournalName = ".update-pending.json";

    /// <summary>Tamamlanmamış bir güncelleme duruyor mu.</summary>
    public static bool HasPending(string appDirectory) =>
        File.Exists(Path.Combine(appDirectory, JournalName));

    /// <summary>Özeti tutmayan ilk dosyayı döndürür; hepsi doğruysa null.</summary>
    public static ManifestFile? FindMismatch(string stageDirectory, IReadOnlyList<ManifestFile> files)
    {
        foreach (var file in files)
        {
            var staged = UpdateCheck.LocalPath(stageDirectory, file.Path);
            if (!File.Exists(staged)) return file;
            if (new FileInfo(staged).Length != file.Size) return file;
            if (!string.Equals(UpdateCheck.HashFile(staged), file.Sha256, StringComparison.OrdinalIgnoreCase))
                return file;
        }
        return null;
    }

    /// <summary>
    /// Bütün dosyaları doğrular, günlüğü yazar, kopyalar ve günlüğü siler. Tutmayan tek bir
    /// özet bile varsa hiçbir dosyaya dokunulmaz ve yan klasör atılır.
    /// </summary>
    public static void Apply(string stageDirectory, string appDirectory, IReadOnlyList<ManifestFile> files)
    {
        var mismatch = FindMismatch(stageDirectory, files);
        if (mismatch is not null)
        {
            Discard(stageDirectory);
            throw new InvalidDataException($"İnen dosyanın özeti tutmadı: {mismatch.Path}");
        }

        WriteJournal(appDirectory, stageDirectory, files);
        CopyAll(stageDirectory, appDirectory, files);
        ClearJournal(appDirectory);
        Discard(stageDirectory);
    }

    /// <summary>
    /// Yarım kalmış bir kopyalama varsa tamamlar. Döndürdüğü değer bir iş yapılıp
    /// yapılmadığıdır.
    /// </summary>
    public static bool ResumePending(string appDirectory)
    {
        var journal = Path.Combine(appDirectory, JournalName);
        if (!File.Exists(journal)) return false;

        string stageDirectory;
        List<ManifestFile> files;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(journal));
            stageDirectory = document.RootElement.GetProperty("stage").GetString() ?? "";
            files = new List<ManifestFile>();
            foreach (var element in document.RootElement.GetProperty("files").EnumerateArray())
            {
                files.Add(new ManifestFile(
                    element.GetProperty("path").GetString()!,
                    element.GetProperty("sha256").GetString()!,
                    element.GetProperty("size").GetInt64()));
            }
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or IOException)
        {
            ClearJournal(appDirectory);
            return false;
        }

        // Yan klasör duruyorsa doğrulanmış kopyalar oradadır, iş tamamlanabilir.
        if (Directory.Exists(stageDirectory) && FindMismatch(stageDirectory, files) is null)
        {
            CopyAll(stageDirectory, appDirectory, files);
            ClearJournal(appDirectory);
            Discard(stageDirectory);
            return true;
        }

        // Yan klasör yoksa kopyalama zaten bitmiş demektir; günlük artığı temizlenir.
        ClearJournal(appDirectory);
        return false;
    }

    public static void Discard(string stageDirectory)
    {
        try
        {
            if (Directory.Exists(stageDirectory)) Directory.Delete(stageDirectory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void CopyAll(string stageDirectory, string appDirectory, IReadOnlyList<ManifestFile> files)
    {
        foreach (var file in files)
        {
            var source = UpdateCheck.LocalPath(stageDirectory, file.Path);
            var target = UpdateCheck.LocalPath(appDirectory, file.Path);
            var folder = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.Copy(source, target, overwrite: true);
        }
    }

    private static void WriteJournal(string appDirectory, string stageDirectory, IReadOnlyList<ManifestFile> files)
    {
        Directory.CreateDirectory(appDirectory);
        using var stream = new FileStream(Path.Combine(appDirectory, JournalName), FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("stage", stageDirectory);
        writer.WriteStartArray("files");
        foreach (var file in files)
        {
            writer.WriteStartObject();
            writer.WriteString("path", file.Path);
            writer.WriteString("sha256", file.Sha256);
            writer.WriteNumber("size", file.Size);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
    }

    private static void ClearJournal(string appDirectory)
    {
        var journal = Path.Combine(appDirectory, JournalName);
        try { if (File.Exists(journal)) File.Delete(journal); }
        catch (IOException) { }
    }
}

/// <summary>Kurulu başlatıcı ile kurulu uygulamanın sürümleri.</summary>
public sealed record LauncherSkew(string? Launcher, string? App)
{
    /// <summary>İkisi ayrıysa başlatıcı geride kalmıştır ve güncellenmelidir.</summary>
    public bool Mismatched => Launcher != App;
}

/// <summary>
/// Değişimin bekleyen hali: hangi dosyaların hangi özete gitmesi gerektiği ve varılacak
/// sürüm. Günlükten okunur; çalışan başlatıcı da yerine geçecek ikili de aynı kaydı görür.
/// </summary>
public sealed record PendingLauncherSwap(string Version, IReadOnlyList<ManifestFile> Files);

/// <summary>
/// Başlatıcının kendini değiştirmesi. Windows çalışan bir <c>.exe</c>'nin ne üstüne
/// yazdırır ne de sildirir; bu yüzden değişimi çalışan başlatıcı yapmaz. Çalışan başlatıcı
/// yalnız <em>kurar</em>: yeni ikili <c>VidShrink.new.exe</c> adına iner ve günlük yazılır.
/// Geçişi, başlatıcı çıktıktan sonra yerine geçecek ikilinin kendisi yapar
/// (<see cref="CommitArgument"/> ile açılır) — o an hedef adı kimse tutmadığı için tek bir
/// yeniden adlandırma yeter.
///
/// Kazanılan şey şu: hedef ad <b>hiçbir adımda</b> boşalmaz. Eski yordam önce eski ikiliyi
/// yana alıp sonra yenisini geçiriyordu; o iki çağrı arasında kurulum kökünde açılabilecek
/// bir <c>VidShrink.exe</c> yoktu ve tam orada kesilen bir kurulum kendini toparlayamıyordu.
/// Burada tek çağrı var: üstüne yazma kipindeki yeniden adlandırma aynı bölümde atomiktir,
/// ad ya eskiyi ya yeniyi gösterir.
///
/// <see cref="Repair"/> hedefe hiç dokunmaz; yalnız yarım kalmış bir değişimi okur ve
/// bekleyen geçişi bildirir. Geçiş her turda yeniden denenebilir, denenmediği turda da
/// kurulum açılabilir kalır.
/// </summary>
public static class LauncherUpdate
{
    /// <summary>Kurulum kökündeki başlatıcı; kısayolların gösterdiği dosya.</summary>
    public const string ExecutableName = "VidShrink.exe";

    /// <summary>Yerini bırakmış eski ikilinin, eski yapılardan kalmışsa, aldığı ad.</summary>
    public const string RetiredSuffix = ".old";

    /// <summary>Yerine geçmeyi bekleyen yeni ikilinin aldığı ad.</summary>
    public const string IncomingSuffix = ".new";

    /// <summary>
    /// Bekleyen geçişi yapan kip. Bu argümanla açılan ikili uygulamayı başlatmaz; eski
    /// başlatıcının çıkmasını bekler, geçişi yapar ve çıkar.
    /// </summary>
    public const string CommitArgument = "--commit-launcher";

    public const string JournalName = ".launcher-pending.json";

    /// <summary>Kurulu başlatıcının sürümü; uygulamanınki app klasöründe ayrı durur.</summary>
    public const string VersionMarkerName = ".launcher-version";

    /// <summary>İnen başlatıcının uygulama dosyalarına karışmadığı yan klasör.</summary>
    public const string StageFolderName = "launcher";

    /// <summary>Eski başlatıcının çıkması ve geçişin oturması için tanınan süre.</summary>
    public static readonly TimeSpan CommitWindow = TimeSpan.FromSeconds(30);

    public static string Target(string baseDirectory, string relativePath) =>
        UpdateCheck.LocalPath(baseDirectory, relativePath);

    /// <summary>
    /// Yan adlar uzantıdan <em>önce</em> işaretlenir: <c>VidShrink.new.exe</c>. Adın hâlâ
    /// bir <c>.exe</c> olması gerekiyor, çünkü geçişi yapan süreç bu dosyanın kendisidir.
    /// </summary>
    public static string Incoming(string baseDirectory, string relativePath) =>
        Mark(Target(baseDirectory, relativePath), IncomingSuffix);

    public static string Retired(string baseDirectory, string relativePath) =>
        Mark(Target(baseDirectory, relativePath), RetiredSuffix);

    private static string Mark(string path, string marker)
    {
        var extension = Path.GetExtension(path);
        return extension.Length == 0
            ? path + marker
            : path[..^extension.Length] + marker + extension;
    }

    public static string StagePath(string stageDirectory, string relativePath) =>
        UpdateCheck.LocalPath(Path.Combine(stageDirectory, StageFolderName), relativePath);

    /// <summary>
    /// <c>0.2.1+abc1234</c> gibi bir yapı sürümünden manifestteki <c>0.2.1</c>'i çıkarır.
    /// Karşılaştırılan iki değer aynı biçimde olmazsa kapı her açılışta yanlış çalar.
    /// </summary>
    public static string NormalizeVersion(string version)
    {
        var plus = version.IndexOf('+');
        return (plus < 0 ? version : version[..plus]).Trim();
    }

    public static string? ReadVersionMarker(string baseDirectory)
    {
        var marker = Path.Combine(baseDirectory, VersionMarkerName);
        try { return File.Exists(marker) ? File.ReadAllText(marker).Trim() : null; }
        catch (IOException) { return null; }
    }

    public static void WriteVersionMarker(string baseDirectory, string version) =>
        File.WriteAllText(Path.Combine(baseDirectory, VersionMarkerName), NormalizeVersion(version));

    /// <summary>
    /// Manifest başlatıcıyı gerçekten sayıyorsa sürüm işaretini yazar. Saymayan bir
    /// manifest için işaret yazmak hiç doğrulanmamış bir sürümü iddia etmektir: o manifeste
    /// bakarak kurulu başlatıcının o sürüm olduğu bilinemez.
    /// </summary>
    public static bool MarkVerified(string baseDirectory, ReleaseManifest manifest)
    {
        if (manifest.Launcher.Count == 0) return false;
        WriteVersionMarker(baseDirectory, manifest.Version);
        return true;
    }

    /// <summary>
    /// İşaret yoksa çalışan ikilinin kendi sürümüyle doldurur. Kurulumdan sonraki ilk
    /// açılışta bu olur; sonrasında işareti değişimin kendisi yazar.
    /// </summary>
    public static void SeedVersionMarker(string baseDirectory, string version)
    {
        if (ReadVersionMarker(baseDirectory) is not null) return;
        WriteVersionMarker(baseDirectory, version);
    }

    /// <summary>
    /// Manifestin saydığı başlatıcı diskte hazır mı: ya hedefte oturmuş ya da yan adda
    /// bekliyor ve günlüğü yazılmış. Sürüm işareti ancak geçiş oturunca yazıldığı için,
    /// yalnız işarete bakan bir kapı bekleyen geçiş boyunca her açılışta aynı arşivi
    /// yeniden indiriyordu.
    /// </summary>
    public static bool Armed(string baseDirectory, ReleaseManifest manifest)
    {
        if (manifest.Launcher.Count == 0) return false;
        if (!File.Exists(Path.Combine(baseDirectory, JournalName))) return false;
        return manifest.Launcher.All(file =>
            Matches(Target(baseDirectory, file.Path), file) || Matches(Incoming(baseDirectory, file.Path), file));
    }

    /// <summary>
    /// Kabul kriteri 4'ün kapısı: uygulama yeni, başlatıcı eski kaldıysa burada görünür.
    /// </summary>
    public static LauncherSkew Inspect(string baseDirectory, string appDirectory) =>
        new(ReadVersionMarker(baseDirectory), UpdateCheck.ReadVersionMarker(appDirectory));

    /// <summary>
    /// İnen ve doğrulanan başlatıcıyı kurulum kökünde yan ada taşır. Yerine geçen dosyaların
    /// listesini döndürür ve <b>hiçbir hâlde atmaz</b>: önceki turdan takılı kalmış bir
    /// <c>VidShrink.new.exe</c> taşımayı düşürdüğünde atılan istisna uygulama adımını da
    /// iptal ettiriyordu. Başlatıcı adımı düşse de uygulama dosyaları yerleşmelidir; oturmamış
    /// dosya listeden düşer ve o tur başlatıcı hiç kurulmaz.
    /// </summary>
    public static IReadOnlyList<ManifestFile> Stage(string stageDirectory, string baseDirectory, IReadOnlyList<ManifestFile> files)
    {
        var moved = new List<ManifestFile>();
        foreach (var file in files)
        {
            var staged = StagePath(stageDirectory, file.Path);
            if (!Matches(staged, file)) continue;

            var incoming = Incoming(baseDirectory, file.Path);
            var folder = Path.GetDirectoryName(incoming);
            try
            {
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                File.Move(staged, incoming, overwrite: true);
                moved.Add(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
        return moved;
    }

    /// <summary>
    /// Değişimi kurar: yan adda bekleyen ikili doğrulanır ve günlük yazılır. Hedef dosyaya
    /// dokunulmaz — bu satırları çalıştıran süreç odur. Döndürdüğü değer, çıkışta geçişi
    /// yapacak ikilinin çağrılması gerektiğidir.
    /// </summary>
    public static bool Arm(string baseDirectory, IReadOnlyList<ManifestFile> files, string version)
    {
        if (files.Count == 0) return false;

        foreach (var file in files)
        {
            if (!Matches(Incoming(baseDirectory, file.Path), file))
                throw new InvalidDataException($"Yerine geçecek başlatıcının özeti tutmadı: {file.Path}");
        }

        WriteJournal(baseDirectory, files, version);
        return true;
    }

    /// <summary>
    /// Geçişi yapan adım; yerine geçecek ikili kendi süreci içinde çağırır. Önce eski
    /// başlatıcının çıkması beklenir, sonra her dosya tek bir üstüne-yazan yeniden
    /// adlandırmayla yerine geçer. Bu çağrı ya olur ya olmaz: olmadığında hedefte eski
    /// ikili durur, günlük kalır ve bir sonraki açılış aynı geçişi yeniden kurar.
    ///
    /// <see cref="Repair"/>'deki sürüm kapısının eşi burada da var: bekleyen geçiş kurulu
    /// başlatıcının sürümünden yeni değilse geçiş yapılmaz. Kurulum betiği bu pencerede
    /// başlatıcıyı ileri taşımış olabilir ve o hâlde geçiş bir güncelleme değil, geri alma
    /// olurdu.
    /// </summary>
    public static bool Commit(string baseDirectory, int? launcherProcessId = null, TimeSpan? window = null)
    {
        var pending = Pending(baseDirectory);
        if (pending is null) return false;

        var installed = ReadVersionMarker(baseDirectory);
        if (installed is not null && !UpdateCheck.IsNewer(pending.Version, NormalizeVersion(installed)))
        {
            ClearJournal(baseDirectory);
            Sweep(baseDirectory, pending.Files);
            return false;
        }

        var deadline = DateTime.UtcNow + (window ?? CommitWindow);
        if (launcherProcessId is int id) WaitForExit(id, deadline);

        foreach (var file in pending.Files)
        {
            var target = Target(baseDirectory, file.Path);
            if (Matches(target, file)) continue;
            if (!Replace(Incoming(baseDirectory, file.Path), target, deadline)) return false;
        }

        ClearJournal(baseDirectory);
        WriteVersionMarker(baseDirectory, pending.Version);
        Sweep(baseDirectory, pending.Files);
        return true;
    }

    /// <summary>
    /// Açılışta koşar ve <b>hedefe hiç dokunmaz</b>: kısayolun gösterdiği dosya bu sürecin
    /// kendi görüntüsüdür. Geçiş zaten olmuşsa işaret yazılıp günlük kapanır; olmamışsa
    /// bekleyen geçiş bildirilir, geçişi çıkışta yerine geçecek ikili yapar. Elde sağlam
    /// bir yeni ikili yoksa artıklar süpürülür ve kurulum eski ikiliyle açılabilir kalır.
    ///
    /// <paramref name="runningVersion"/> verilirse bekleyen geçiş onunla karşılaştırılır:
    /// aradan kurulum betiği geçip başlatıcıyı elle yenilemiş olabilir ve o hâlde bekleyen
    /// kayıt artık bir güncelleme değil, geri alma olurdu.
    /// </summary>
    public static bool Repair(string baseDirectory, string? runningVersion = null)
    {
        var pending = Pending(baseDirectory);
        if (pending is null) return false;

        if (pending.Files.All(file => Matches(Target(baseDirectory, file.Path), file)))
        {
            ClearJournal(baseDirectory);
            WriteVersionMarker(baseDirectory, pending.Version);
            Sweep(baseDirectory, pending.Files);
            return false;
        }

        if (runningVersion is not null && !UpdateCheck.IsNewer(pending.Version, NormalizeVersion(runningVersion)))
        {
            ClearJournal(baseDirectory);
            Sweep(baseDirectory, pending.Files);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Günlükte bekleyen geçiş. Günlük yoksa, okunamıyorsa ya da elde geçirilecek sağlam
    /// bir ikili kalmamışsa kayıt kapatılır ve artıklar süpürülür — hedefe dokunmadan.
    /// </summary>
    public static PendingLauncherSwap? Pending(string baseDirectory)
    {
        var journal = Path.Combine(baseDirectory, JournalName);
        if (!File.Exists(journal))
        {
            Sweep(baseDirectory);
            return null;
        }

        string version;
        List<ManifestFile> files;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(journal));
            version = document.RootElement.GetProperty("version").GetString() ?? "";
            files = new List<ManifestFile>();
            foreach (var element in document.RootElement.GetProperty("files").EnumerateArray())
            {
                files.Add(new ManifestFile(
                    element.GetProperty("path").GetString()!,
                    element.GetProperty("sha256").GetString()!,
                    element.GetProperty("size").GetInt64()));
            }
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or IOException)
        {
            ClearJournal(baseDirectory);
            Sweep(baseDirectory);
            return null;
        }

        if (files.Count == 0 || version.Length == 0)
        {
            ClearJournal(baseDirectory);
            Sweep(baseDirectory);
            return null;
        }

        var swap = new PendingLauncherSwap(NormalizeVersion(version), files);

        // Geçiş oturmuşsa yan ad artık yok; bu hâl de okunabilmeli, elenmez.
        if (files.All(file => Matches(Target(baseDirectory, file.Path), file))) return swap;

        if (files.Any(file => !Matches(Incoming(baseDirectory, file.Path), file)))
        {
            ClearJournal(baseDirectory);
            Sweep(baseDirectory, files);
            return null;
        }

        return swap;
    }

    /// <summary>
    /// Tek atomik adım. Üstüne yazma kipindeki yeniden adlandırma aynı bölümde bölünmez:
    /// ad her an ya eski ikiliyi ya yeni ikiliyi gösterir, hiç boş kalmaz. Başarısızlıkta
    /// hiçbir şey değişmez — eski ikili yerinde durur.
    /// </summary>
    private static bool Replace(string incoming, string target, DateTime deadline)
    {
        while (true)
        {
            try
            {
                File.Move(incoming, target, overwrite: true);
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (DateTime.UtcNow >= deadline) return false;
                Thread.Sleep(100);
            }
        }
    }

    private static void WaitForExit(int processId, DateTime deadline)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var remaining = deadline - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero) process.WaitForExit((int)remaining.TotalMilliseconds);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
        }
    }

    public static bool Matches(string path, ManifestFile file)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length != file.Size) return false;
            return string.Equals(UpdateCheck.HashFile(path), file.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <summary>
    /// Artık adlar. Yeni geçen ikili henüz çalışırken kendi eski adı silinemez — o dosya bu
    /// sürecin kendi görüntüsüdür. Başarısızlık yutulur, iş bir sonraki açılışa kalır.
    /// </summary>
    public static void Sweep(string baseDirectory, IReadOnlyList<ManifestFile>? files = null)
    {
        var names = files is null || files.Count == 0
            ? new[] { ExecutableName }
            : files.Select(file => file.Path).ToArray();

        foreach (var name in names)
        {
            TryDelete(Retired(baseDirectory, name));
            TryDelete(Incoming(baseDirectory, name));
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void WriteJournal(string baseDirectory, IReadOnlyList<ManifestFile> files, string version)
    {
        Directory.CreateDirectory(baseDirectory);
        using var stream = new FileStream(Path.Combine(baseDirectory, JournalName), FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("version", NormalizeVersion(version));
        writer.WriteStartArray("files");
        foreach (var file in files)
        {
            writer.WriteStartObject();
            writer.WriteString("path", file.Path);
            writer.WriteString("sha256", file.Sha256);
            writer.WriteNumber("size", file.Size);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
    }

    private static void ClearJournal(string baseDirectory) =>
        TryDelete(Path.Combine(baseDirectory, JournalName));
}

/// <summary>
/// Bir güncellemenin diske inen kısmının sırası. Sıra kararı burada duruyor ki ölçülebilsin:
/// uygulama dosyaları önce yerleşir, başlatıcı ancak ondan sonra kurulur. Ters sırada yeni
/// bir başlatıcı eski bir uygulamayı açardı; uygulama adımı düşerse başlatıcı hiç kurulmaz.
/// Tersi geçerli değil: başlatıcı adımı düşse de uygulama dosyaları yerleşir, yalnız o tur
/// günlük yazılmaz.
/// </summary>
public static class UpdateRollout
{
    /// <summary>
    /// Döndürdüğü değer, çıkışta başlatıcı geçişinin çağrılması gerektiğidir.
    /// </summary>
    public static bool Apply(
        string stageDirectory,
        string baseDirectory,
        string appDirectory,
        IReadOnlyList<ManifestFile> appFiles,
        IReadOnlyList<ManifestFile> launcherFiles,
        ReleaseManifest manifest)
    {
        if (appFiles.Count > 0) UpdateStage.Apply(stageDirectory, appDirectory, appFiles);
        else UpdateStage.Discard(stageDirectory);
        UpdateCheck.WriteVersionMarker(appDirectory, manifest.Version);

        if (launcherFiles.Count > 0)
        {
            return launcherFiles.All(file =>
                       LauncherUpdate.Matches(LauncherUpdate.Incoming(baseDirectory, file.Path), file))
                   && LauncherUpdate.Arm(baseDirectory, launcherFiles, manifest.Version);
        }

        LauncherUpdate.MarkVerified(baseDirectory, manifest);
        return false;
    }
}

/// <summary>Bir arşivin istenen bayt aralığını veren kaynak.</summary>
public interface IRangeSource
{
    Task<long> LengthAsync(CancellationToken cancellationToken);
    Task<byte[]> ReadAsync(long offset, int length, CancellationToken cancellationToken);
}

public sealed class FileRangeSource : IRangeSource
{
    private readonly string _path;

    public FileRangeSource(string path) => _path = path;

    public Task<long> LengthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new FileInfo(_path).Length);

    public async Task<byte[]> ReadAsync(long offset, int length, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[length];
        var read = 0;
        while (read < length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read, length - read), cancellationToken);
            if (count == 0) break;
            read += count;
        }
        return read == length ? buffer : buffer[..read];
    }
}

/// <summary>
/// Arşivin tamamını indirmeden tek tek dosya çeken okuyucu. Merkezî dizin okunur, sonra
/// yalnız istenen girdinin baytları istenir. "Yalnız değişen dosyalar insin" isteğini
/// karşılayan parça budur.
/// </summary>
public sealed class RemoteZip
{
    private const uint EndOfCentralDirectorySignature = 0x06054b50;
    private const uint CentralFileHeaderSignature = 0x02014b50;
    private const uint Zip64Marker = 0xFFFFFFFF;

    private readonly IRangeSource _source;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    private sealed record Entry(string Path, ushort Method, long CompressedSize, long UncompressedSize, long LocalHeaderOffset);

    private RemoteZip(IRangeSource source) => _source = source;

    public IReadOnlyCollection<string> Paths => _entries.Keys;

    public static async Task<RemoteZip> OpenAsync(IRangeSource source, CancellationToken cancellationToken)
    {
        var zip = new RemoteZip(source);
        var length = await source.LengthAsync(cancellationToken);
        var tailLength = (int)Math.Min(length, 64 * 1024);
        var tail = await source.ReadAsync(length - tailLength, tailLength, cancellationToken);

        var eocd = -1;
        for (var i = tail.Length - 22; i >= 0; i--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i)) == EndOfCentralDirectorySignature)
            {
                eocd = i;
                break;
            }
        }
        if (eocd < 0) throw new InvalidDataException("Arşivin merkezî dizini bulunamadı.");

        var entryCount = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(eocd + 10));
        var directorySize = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(eocd + 12));
        var directoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(eocd + 16));
        if (directorySize == Zip64Marker || directoryOffset == Zip64Marker)
            throw new NotSupportedException("Zip64 arşivi desteklenmiyor.");

        var directory = await source.ReadAsync(directoryOffset, (int)directorySize, cancellationToken);
        var position = 0;
        for (var i = 0; i < entryCount && position + 46 <= directory.Length; i++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(position)) != CentralFileHeaderSignature) break;
            var method = BinaryPrimitives.ReadUInt16LittleEndian(directory.AsSpan(position + 10));
            var compressed = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(position + 20));
            var uncompressed = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(position + 24));
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(directory.AsSpan(position + 28));
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(directory.AsSpan(position + 30));
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(directory.AsSpan(position + 32));
            var localOffset = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(position + 42));
            var name = Encoding.UTF8.GetString(directory, position + 46, nameLength).Replace('\\', '/');

            if (compressed != Zip64Marker && uncompressed != Zip64Marker && localOffset != Zip64Marker &&
                !name.EndsWith('/'))
            {
                zip._entries[name] = new Entry(name, method, compressed, uncompressed, localOffset);
            }
            position += 46 + nameLength + extraLength + commentLength;
        }

        return zip;
    }

    /// <summary>Yayın klasörüne göreli yolu arşiv içindeki girdiye eşler.</summary>
    public string? Resolve(string manifestPath)
    {
        if (_entries.ContainsKey(manifestPath)) return manifestPath;
        var suffix = "/" + manifestPath;
        foreach (var key in _entries.Keys)
            if (key.EndsWith(suffix, StringComparison.Ordinal)) return key;
        return null;
    }

    public async Task<byte[]> ExtractAsync(string entryPath, CancellationToken cancellationToken)
    {
        if (!_entries.TryGetValue(entryPath, out var entry))
            throw new FileNotFoundException($"Arşivde bulunamadı: {entryPath}");

        var header = await _source.ReadAsync(entry.LocalHeaderOffset, 30, cancellationToken);
        if (header.Length < 30) throw new InvalidDataException("Yerel başlık okunamadı.");
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(26));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28));
        var dataOffset = entry.LocalHeaderOffset + 30 + nameLength + extraLength;

        var payload = await _source.ReadAsync(dataOffset, (int)entry.CompressedSize, cancellationToken);
        if (payload.Length != entry.CompressedSize) throw new InvalidDataException("Dosya eksik indi.");

        if (entry.Method == 0) return payload;
        if (entry.Method != 8) throw new NotSupportedException($"Desteklenmeyen sıkıştırma: {entry.Method}");

        using var compressed = new MemoryStream(payload);
        using var inflater = new DeflateStream(compressed, CompressionMode.Decompress);
        using var output = new MemoryStream((int)entry.UncompressedSize);
        await inflater.CopyToAsync(output, cancellationToken);
        return output.ToArray();
    }
}
