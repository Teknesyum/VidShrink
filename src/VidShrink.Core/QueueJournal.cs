using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VidShrink.Core;

/// <summary>SIK-P9: kuyruktaki bir işin saklanan hâli. Tavanlı hedef dosyanın kendi kalite tavanından yeniden hesaplanır.</summary>
public sealed record SavedJob(string Path, double TargetMb, bool CeilingTarget);

/// <summary>
/// SIK-P9: yarım kalan kuyruğun defteri. Her süreç kendi dosyasını yazar
/// (<c>queue/&lt;pid&gt;.json</c>, ayarın yanında); aynı süreçteki pencerelerin işleri tek dosyada
/// birleşir. Yazım önce geçici dosyaya, sonra yer değiştirmeyle olur ki yarım yazılmış dosya kuyruğu
/// bozmasın. Kuyruk boşalınca dosya silinir; süreç ölünce kalan dosya açılışta
/// <see cref="TakeOrphans"/> ile alınır.
/// </summary>
public sealed class QueueJournal
{
    public const string FolderName = "queue";
    private const string Extension = ".json";
    private const string TempSuffix = ".tmp";

    private readonly object _gate = new();
    private readonly List<(object Owner, IReadOnlyList<SavedJob> Jobs)> _owners = new();
    private readonly int _processId;

    public QueueJournal(string folder, int processId)
    {
        Folder = folder;
        _processId = processId;
    }

    /// <summary>Test konağı gerçek defteri kapatır; pencereler o zaman deftere hiç yazmaz.</summary>
    public static bool Disabled { get; set; }

    public static string DefaultFolder => Path.Combine(Path.GetDirectoryName(UpdateSettings.DefaultPath) ?? "", FolderName);

    private static QueueJournal? _shared;

    /// <summary>Bu sürecin defteri; kapalıysa <c>null</c>.</summary>
    public static QueueJournal? Shared
        => Disabled ? null : LazyInitializer.EnsureInitialized(ref _shared, () => new QueueJournal(DefaultFolder, Environment.ProcessId));

    public string Folder { get; }

    /// <summary>Süreç hâlâ koşuyor mu; kimliği yoksa ya da çıkmışsa değil.</summary>
    public static bool IsAlive(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return true; }
    }

    public string FilePath => Path.Combine(Folder, _processId.ToString(CultureInfo.InvariantCulture) + Extension);

    /// <summary>Bir pencerenin işlerini yazar: önce koşanlar, sonra bekleyenler. Boş liste o pencereyi defterden çıkarır.</summary>
    public void Update(object owner, IReadOnlyList<SavedJob> jobs)
    {
        lock (_gate)
        {
            var index = _owners.FindIndex(entry => ReferenceEquals(entry.Owner, owner));
            if (jobs.Count == 0)
            {
                if (index >= 0) _owners.RemoveAt(index);
            }
            else if (index >= 0) _owners[index] = (owner, jobs.ToArray());
            else _owners.Add((owner, jobs.ToArray()));
            Write(_owners.SelectMany(entry => entry.Jobs).ToList());
        }
    }

    public void Remove(object owner) => Update(owner, Array.Empty<SavedJob>());

    private void Write(IReadOnlyList<SavedJob> jobs)
    {
        try
        {
            if (jobs.Count == 0)
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                return;
            }
            Directory.CreateDirectory(Folder);
            var temp = FilePath + TempSuffix;
            File.WriteAllText(temp, Serialize(jobs));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public static string Serialize(IReadOnlyList<SavedJob> jobs)
    {
        var array = new JsonArray();
        foreach (var job in jobs)
            array.Add(new JsonObject
            {
                ["path"] = job.Path,
                ["targetMb"] = job.TargetMb,
                ["ceiling"] = job.CeilingTarget
            });
        return new JsonObject { ["version"] = 1, ["jobs"] = array }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Dosyadaki işler. Okunamayan ya da bozuk dosya boş liste döner; tanınmayan satır atlanır.</summary>
    public static IReadOnlyList<SavedJob> Read(string file)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root || root["jobs"] is not JsonArray array)
                return Array.Empty<SavedJob>();
            var jobs = new List<SavedJob>();
            foreach (var item in array)
            {
                if (item is not JsonObject entry) continue;
                if (entry["path"] is not JsonValue pathValue || !pathValue.TryGetValue<string>(out var path) || string.IsNullOrWhiteSpace(path)) continue;
                var target = entry["targetMb"] is JsonValue targetValue && targetValue.TryGetValue<double>(out var mb) && double.IsFinite(mb) && mb > 0 ? mb : 0;
                var ceiling = entry["ceiling"] is JsonValue ceilingValue && ceilingValue.TryGetValue<bool>(out var flag) && flag;
                if (target <= 0 && !ceiling) continue;
                jobs.Add(new SavedJob(path, target, ceiling));
            }
            return jobs;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return Array.Empty<SavedJob>();
        }
    }

    /// <summary>
    /// Ölmüş süreçlerin bıraktığı kuyruk. Kendi dosyası ve yaşayan sürecin dosyası alınmaz; alınan dosya
    /// silinir, bozuk olan da. Kaynağı artık olmayan iş elenir, aynı yol bir kez gelir.
    /// </summary>
    public IReadOnlyList<SavedJob> TakeOrphans(Func<int, bool> alive, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        var taken = new List<SavedJob>();
        string[] files;
        try
        {
            if (!Directory.Exists(Folder)) return taken;
            files = Directory.GetFiles(Folder, "*" + Extension);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return taken; }

        foreach (var file in files.OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!int.TryParse(Path.GetFileNameWithoutExtension(file), NumberStyles.None, CultureInfo.InvariantCulture, out var pid)) continue;
            if (pid == _processId || alive(pid)) continue;
            taken.AddRange(Read(file));
            try { File.Delete(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return taken.Where(job => exists(job.Path) && seen.Add(job.Path)).ToList();
    }
}
