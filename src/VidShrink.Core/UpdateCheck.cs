using System.Buffers.Binary;
using System.IO.Compression;
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
    IReadOnlyList<ManifestFile> Files);

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

    public static string Rid
    {
        get
        {
            var arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                _ => "x64"
            };
            if (OperatingSystem.IsWindows()) return "win-" + arch;
            if (OperatingSystem.IsMacOS()) return "osx-" + arch;
            return "linux-" + arch;
        }
    }

    public static string ManifestAssetName(string rid) => $"manifest-{rid}.json";

    public static string ArchiveAssetName(string rid) => $"vidshrink-{rid}.zip";

    public static string LatestAssetUrl(string asset) =>
        $"https://github.com/Teknesyum/VidShrink/releases/latest/download/{asset}";

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
            var files = new List<ManifestFile>();
            if (root.TryGetProperty("files", out var fileArray) && fileArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in fileArray.EnumerateArray())
                {
                    var path = element.GetProperty("path").GetString();
                    var sha = element.GetProperty("sha256").GetString();
                    var size = element.GetProperty("size").GetInt64();
                    if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(sha))
                        throw new InvalidDataException("Manifestte path veya sha256 boş.");
                    files.Add(new ManifestFile(path!.Replace('\\', '/'), sha!.ToLowerInvariant(), size));
                }
            }

            var built = root.TryGetProperty("built", out var builtElement) && builtElement.ValueKind == JsonValueKind.String
                ? DateTimeOffset.Parse(builtElement.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind)
                : DateTimeOffset.MinValue;

            return new ReleaseManifest(
                root.GetProperty("version").GetString() ?? throw new InvalidDataException("Manifestte version yok."),
                root.TryGetProperty("commit", out var commit) ? commit.GetString() ?? "" : "",
                built,
                root.TryGetProperty("rid", out var rid) ? rid.GetString() ?? "" : "",
                files);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        {
            throw new InvalidDataException("Manifest çözümlenemedi.", exception);
        }
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
    public static IReadOnlyList<ManifestFile> Diff(string appDirectory, ReleaseManifest manifest, HashCache? cache = null)
    {
        var changed = new List<ManifestFile>();
        foreach (var file in manifest.Files)
        {
            var local = LocalPath(appDirectory, file.Path);
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
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // Okunamayan ayar varsayılana düşer; açılış hiçbir koşulda durmaz.
        }
        return settings;
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
        writer.WriteEndObject();
        writer.Flush();
    }
}

/// <summary>
/// Güncelleme denetiminin sıklığı. Ağsız çalışan biri her açılışta zaman aşımı kadar
/// beklemesin diye denetim günde en çok bir kez yapılır; ara açılışlarda ağa hiç
/// çıkılmaz.
/// </summary>
public static class UpdateSchedule
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public const string FileName = "last-check.json";

    public static string DefaultPath
    {
        get
        {
            var folder = Path.GetDirectoryName(UpdateSettings.DefaultPath);
            return string.IsNullOrEmpty(folder) ? FileName : Path.Combine(folder, FileName);
        }
    }

    /// <summary>Son denetimden bu yana 24 saat geçtiyse ağa çıkılır.</summary>
    public static bool DueNow(DateTimeOffset now, string? path = null)
    {
        var last = ReadLastCheck(path);
        if (last is null) return true;

        // Saat geriye alınmışsa kayıt gelecekte kalır; o durumda beklemek yerine denetle.
        if (last > now) return true;
        return now - last >= Interval;
    }

    public static DateTimeOffset? ReadLastCheck(string? path = null)
    {
        var file = path ?? DefaultPath;
        try
        {
            if (!File.Exists(file)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (!document.RootElement.TryGetProperty("lastCheck", out var value)) return null;
            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text)
                ? null
                : DateTimeOffset.Parse(text!, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }
        catch (Exception exception) when (exception is JsonException or IOException or FormatException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Denetim denemesini işaretler. Sonucu değil denemeyi yazar: ağ yokken de tur
    /// harcandı sayılır, yoksa ağsız makine her açılışta yeniden bekler.
    /// </summary>
    public static void Record(DateTimeOffset now, string? path = null)
    {
        var file = path ?? DefaultPath;
        try
        {
            var folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            using var stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WriteString("lastCheck", now.ToString("o"));
            writer.WriteEndObject();
            writer.Flush();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Yazılamazsa en fazla bir sonraki açılışta yeniden denetlenir; açılış durmaz.
        }
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
