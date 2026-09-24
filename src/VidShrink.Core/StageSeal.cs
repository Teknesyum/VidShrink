using System.Text.Json;

namespace VidShrink.Core;

/// <summary>
/// Sahnenin mührü: doğrulanmış her dosyanın özeti, boyu ve son yazma zamanı. Dosya
/// mühürdeki boy ve zamanla duruyorsa özeti yeniden hesaplanmaz; mühür yoksa ya da dosya
/// değişmişse bir kez özetlenir. Mühür yalnız hızlandırır: okunamayan mühür boş sayılır.
/// </summary>
public sealed class StageSeal
{
    /// <summary>Mühür dosyasının adı, sahne klasöründe.</summary>
    public const string FileName = ".stage-seal.json";

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly string _stage;
    private bool _dirty;

    private sealed record Entry(string Sha256, long Size, long Ticks);

    private StageSeal(string stage) => _stage = Path.GetFullPath(stage);

    /// <summary>Sahnenin mührünü okur; yoksa ya da bozuksa boş mühür döner.</summary>
    public static StageSeal Load(string stage)
    {
        var seal = new StageSeal(stage);
        var path = Path.Combine(stage, FileName);
        if (!File.Exists(path)) return seal;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value;
                seal._entries[property.Name] = new Entry(
                    value.GetProperty("sha256").GetString() ?? "",
                    value.GetProperty("size").GetInt64(),
                    value.GetProperty("ticks").GetInt64());
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or KeyNotFoundException
            or InvalidOperationException or UnauthorizedAccessException)
        {
            seal._entries.Clear();
        }
        return seal;
    }

    /// <summary>
    /// Dosya manifestin özetiyle mühre girmiş ve o günden beri boyu da yazma zamanı da
    /// değişmemiş mi.
    /// </summary>
    public bool Holds(string path, ManifestFile file)
    {
        Entry? entry;
        lock (_gate)
        {
            if (!_entries.TryGetValue(Key(path), out entry)) return false;
        }
        if (!string.Equals(entry.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase)) return false;
        if (entry.Size != file.Size) return false;
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length == entry.Size && info.LastWriteTimeUtc.Ticks == entry.Ticks;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Özeti doğrulanmış dosyayı diskteki boyu ve zamanıyla mühre yazar.</summary>
    public void Record(string path, string sha256)
    {
        var info = new FileInfo(path);
        if (!info.Exists) return;
        var entry = new Entry(sha256, info.Length, info.LastWriteTimeUtc.Ticks);
        lock (_gate)
        {
            _entries[Key(path)] = entry;
            _dirty = true;
        }
    }

    /// <summary>Değişiklik varsa mührü diske yazar; yazamamak işi bozmaz.</summary>
    public void Save()
    {
        lock (_gate)
        {
            if (!_dirty || !Directory.Exists(_stage)) return;
            try
            {
                using var stream = new FileStream(Path.Combine(_stage, FileName), FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = new Utf8JsonWriter(stream);
                writer.WriteStartObject();
                foreach (var pair in _entries)
                {
                    writer.WriteStartObject(pair.Key);
                    writer.WriteString("sha256", pair.Value.Sha256);
                    writer.WriteNumber("size", pair.Value.Size);
                    writer.WriteNumber("ticks", pair.Value.Ticks);
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
                writer.Flush();
                _dirty = false;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private string Key(string path) =>
        Path.GetRelativePath(_stage, Path.GetFullPath(path)).Replace('\\', '/');
}
