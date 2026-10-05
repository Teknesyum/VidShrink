using System.Collections.Concurrent;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// Kuyruk penceresinin izleme klasörü. Dosyanın yazımının bittiğine <see cref="WatchFolder.Poll"/>
/// karar verir (iki ardışık taramada sabit damga, oturma süresi, kilit); burada ikinci bir
/// kararlılık kuralı yoktur. Bu sınıf yalnız taramaya neyin girdiğini süzer: izleme başlarken
/// klasörde duran dosya, uygulamanın kendi çıktısı ve ara dosyası, kuyruğa bir kez verilmiş
/// dosya <see cref="WatchFolder"/>'a hiç gösterilmez.
/// </summary>
internal sealed class QueueWatch : IDisposable
{
    internal const string PartialPrefix = "vidshrink_partial_";
    internal const int ShortPathLength = 44;

    private static readonly StringComparer PathComparer = StringComparer.FromComparison(WatchFolder.PathComparison);
    private static readonly ConcurrentDictionary<string, byte> Own = new(PathComparer);

    private readonly IWatchFileSystem _fs;
    private readonly IWatchClock _clock;
    private readonly Sieve _sieve;
    private readonly WatchFolder _watch;
    private readonly CancellationTokenSource _cts = new();

    internal QueueWatch(string folder, TimeSpan? pollInterval = null, TimeSpan? stableFor = null,
        IWatchFileSystem? fs = null, IWatchClock? clock = null)
    {
        WatchedFolder = Path.GetFullPath(folder);
        _fs = fs ?? PhysicalWatchFileSystem.Instance;
        _clock = clock ?? SystemWatchClock.Instance;
        _sieve = new Sieve(_fs, WatchedFolder);
        var options = new WatchOptions { WatchDirectory = WatchedFolder, OutputDirectory = WatchedFolder };
        if (pollInterval is { } interval) options = options with { PollInterval = interval };
        if (stableFor is { } stable) options = options with { StableFor = stable };
        _watch = new WatchFolder(options, _sieve, _clock);
    }

    internal string WatchedFolder { get; }

    /// <summary>
    /// Uygulamanın yazacağı çıktı. Kodlama başlamadan bildirilir: çıktı izlenen klasöre düşüyorsa
    /// büyürken de bitince de aday olmaz, yoksa her çıktı yeniden küçültülürdü.
    /// </summary>
    internal static void MarkOwn(string path) => Own[Path.GetFullPath(path)] = 0;

    internal static bool IsOwn(string path)
        => Path.GetFileName(path).StartsWith(PartialPrefix, StringComparison.OrdinalIgnoreCase)
            || Own.ContainsKey(Path.GetFullPath(path));

    /// <summary>
    /// Tek tarama. Klasör yoksa <c>false</c> döner ve hiçbir şey vermez; hazır olan her dosya
    /// <paramref name="found"/>'a bir kez verilir ve damgası değişmedikçe bir daha verilmez.
    /// </summary>
    internal bool Step(Action<string> found)
    {
        if (!_fs.DirectoryExists(WatchedFolder)) return false;
        foreach (var path in _watch.Poll())
        {
            _sieve.Remember(path);
            found(path);
        }
        return true;
    }

    /// <summary>Taramayı arka planda sürdürür; klasör kaybolunca <paramref name="lost"/> bir kez çağrılır ve döngü biter.</summary>
    internal Task Start(Action<string> found, Action lost)
        => Task.Run(() => LoopAsync(found, lost));

    private async Task LoopAsync(Action<string> found, Action lost)
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                if (!Step(found))
                {
                    lost();
                    return;
                }
                await _clock.Delay(_watch.Options.PollInterval, _cts.Token);
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose() => _cts.Cancel();

    /// <summary>Uzun yolun kökünü ve sonunu tutar, ortasını <c>…</c> ile değiştirir; son klasör hiç düşmez.</summary>
    internal static string Shorten(string path, int max = ShortPathLength)
    {
        if (path.Length <= max) return path;
        var root = Path.GetPathRoot(path) ?? "";
        var parts = path[root.Length..].Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        var tail = "";
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            var next = Path.DirectorySeparatorChar + parts[i] + tail;
            if (tail.Length > 0 && root.Length + 1 + next.Length > max) break;
            tail = next;
        }
        return root + "…" + tail;
    }

    private sealed class Sieve : IWatchFileSystem
    {
        private readonly IWatchFileSystem _inner;
        private readonly ConcurrentDictionary<string, WatchFileStamp> _known = new(PathComparer);

        internal Sieve(IWatchFileSystem inner, string folder)
        {
            _inner = inner;
            foreach (var path in inner.EnumerateFiles(folder)) Remember(path);
        }

        internal void Remember(string path)
        {
            if (_inner.Stat(path) is { } stamp) _known[path] = stamp;
        }

        public IEnumerable<string> EnumerateFiles(string directory)
            => _inner.EnumerateFiles(directory).Where(Fresh).ToList();

        private bool Fresh(string path)
        {
            if (IsOwn(path)) return false;
            return !(_known.TryGetValue(path, out var known) && _inner.Stat(path) == known);
        }

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
        public void CreateDirectory(string path) => _inner.CreateDirectory(path);
        public WatchFileStamp? Stat(string path) => _inner.Stat(path);
        public bool IsLocked(string path) => _inner.IsLocked(path);
        public bool FileExists(string path) => _inner.FileExists(path);
        public string ReadAllText(string path) => _inner.ReadAllText(path);
        public void WriteAllTextAtomic(string path, string content) => _inner.WriteAllTextAtomic(path, content);
        public void Move(string source, string destination) => _inner.Move(source, destination);
        public void Delete(string path) => _inner.Delete(path);
    }
}
