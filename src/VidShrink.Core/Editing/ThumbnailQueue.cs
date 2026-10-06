using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace VidShrink.Core.Editing;

public interface IThumbnailSource
{
    /// <summary>Kaynagin <paramref name="sourceTick"/> anindaki kareyi kodlanmis resim olarak verir; kare yoksa <c>null</c>.</summary>
    Task<byte[]?> ReadAsync(string source, long sourceTick, int height, CancellationToken ct);
}

/// <summary>
/// Kucuk resim istekleri icin tek sirali kuyruk. Ayni anda tek okuma kosar; yeni istek listesi
/// bekleyenlerin yerine gecer ve listede olmayan ucustaki okumayi iptal eder. Uretilen kare bayt
/// sinirli onbellekte durur, en eski kullanilan once atilir; dusen kare ayni kaynakta yeniden denenmez.
/// Kaynak degisince onbellek bosalir.
/// </summary>
public sealed class ThumbnailQueue : IDisposable
{
    public const long DefaultByteLimit = 16L * 1024 * 1024;

    private readonly object _gate = new();
    private readonly IThumbnailSource _reader;
    private readonly long _byteLimit;
    private readonly Dictionary<long, LinkedListNode<(long Tick, byte[] Data)>> _cache = new();
    private readonly LinkedList<(long Tick, byte[] Data)> _order = new();
    private readonly HashSet<long> _failed = new();
    private readonly List<long> _pending = new();
    private string? _source;
    private int _height;
    private int _generation;
    private long _bytes;
    private CancellationTokenSource? _flight;
    private long _flightTick;
    private bool _pumping;
    private bool _disposed;
    private Task _pump = Task.CompletedTask;

    public ThumbnailQueue(IThumbnailSource reader, long byteLimit = DefaultByteLimit)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (byteLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(byteLimit), byteLimit, "Onbellek siniri pozitif olmalidir");
        _reader = reader;
        _byteLimit = byteLimit;
    }

    /// <summary>Bir kare onbellege girdiginde, okumayi yapan is parcaciginda yayilir.</summary>
    public event Action<long>? Ready;

    public string? Source { get { lock (_gate) return _source; } }

    public int Height { get { lock (_gate) return _height; } }

    /// <summary>Sira bosalinca tamamlanan gorev; yeni istek yeni gorev dogurur.</summary>
    public Task Idle { get { lock (_gate) return _pump; } }

    /// <summary>Kaynagi ya da kare yuksekligini degistirir: bekleyenler, ucustaki okuma ve onbellek birakilir.</summary>
    public void Open(string? source, int height)
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (string.Equals(source, _source, StringComparison.Ordinal) && height == _height) return;
            _source = source;
            _height = height;
            Reset();
        }
    }

    /// <summary>
    /// Istenen kaynak tick'leri, oncelik sirasiyla. Onbellekteki ve dusmus kareler siraya girmez;
    /// onceki istekten kalan bekleyenler dusurulur.
    /// </summary>
    public void Request(IReadOnlyList<long> ticks)
    {
        ArgumentNullException.ThrowIfNull(ticks);
        lock (_gate)
        {
            if (_disposed) return;
            _pending.Clear();
            var flying = false;
            if (_source is not null && _height > 0)
            {
                var seen = new HashSet<long>();
                foreach (var tick in ticks)
                {
                    if (!seen.Add(tick)) continue;
                    if (_flight is not null && tick == _flightTick)
                    {
                        flying = true;
                        continue;
                    }

                    if (!_cache.ContainsKey(tick) && !_failed.Contains(tick)) _pending.Add(tick);
                }
            }

            if (_flight is not null && !flying) Cancel(_flight);
            if (_pending.Count == 0 || _pumping) return;
            _pumping = true;
            _pump = Task.Run(PumpAsync);
        }
    }

    public bool TryGet(long tick, out byte[] data)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(tick, out var node))
            {
                _order.Remove(node);
                _order.AddLast(node);
                data = node.Value.Data;
                return true;
            }
        }

        data = Array.Empty<byte>();
        return false;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _source = null;
            Reset();
        }
    }

    private void Reset()
    {
        _generation++;
        _pending.Clear();
        _cache.Clear();
        _order.Clear();
        _failed.Clear();
        _bytes = 0;
        if (_flight is not null) Cancel(_flight);
    }

    private static void Cancel(CancellationTokenSource source)
    {
        try { source.Cancel(); }
        catch (ObjectDisposedException) { }
        catch (AggregateException) { }
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            string source;
            int height;
            int generation;
            long tick;
            CancellationTokenSource flight;
            lock (_gate)
            {
                if (_pending.Count == 0 || _source is null)
                {
                    _pumping = false;
                    return;
                }

                tick = _pending[0];
                _pending.RemoveAt(0);
                source = _source;
                height = _height;
                generation = _generation;
                flight = new CancellationTokenSource();
                _flight = flight;
                _flightTick = tick;
            }

            byte[]? data = null;
            var failed = false;
            try
            {
                data = await _reader.ReadAsync(source, tick, height, flight.Token).ConfigureAwait(false);
                failed = data is not { Length: > 0 };
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or Win32Exception)
            {
                failed = true;
            }

            var stored = false;
            lock (_gate)
            {
                _flight = null;
                var live = generation == _generation && !flight.IsCancellationRequested;
                if (live && failed) _failed.Add(tick);
                if (live && !failed && data is { Length: > 0 }) stored = Store(tick, data);
            }

            flight.Dispose();
            if (stored) Ready?.Invoke(tick);
        }
    }

    private bool Store(long tick, byte[] data)
    {
        if (_cache.ContainsKey(tick)) return false;
        _cache[tick] = _order.AddLast((tick, data));
        _bytes += data.Length;
        while (_bytes > _byteLimit && _order.Count > 1)
        {
            var oldest = _order.First!;
            _order.RemoveFirst();
            _cache.Remove(oldest.Value.Tick);
            _bytes -= oldest.Value.Data.Length;
        }

        return true;
    }
}

/// <summary>
/// ffmpeg ile tek kare: girdide arama, tek kare, yukseklige olcek, PNG olarak boruya. Gecici dosya
/// yazmaz; surec dusuk oncelikli ve tek is parcacikli, iptalde oldurulur, stderr bosaltilir.
/// </summary>
public sealed class FfmpegThumbnailSource : IThumbnailSource
{
    private readonly Func<string> _ffmpeg;

    public FfmpegThumbnailSource(Func<string> ffmpegPath)
    {
        ArgumentNullException.ThrowIfNull(ffmpegPath);
        _ffmpeg = ffmpegPath;
    }

    public Action<Process>? Started { get; set; }

    public static IReadOnlyList<string> Arguments(string source, long sourceTick, int height) => new[]
    {
        "-hide_banner", "-nostdin", "-v", "error",
        "-threads", "1",
        "-ss", EditTime.ToSeconds(Math.Max(0, sourceTick)).ToString("0.######", CultureInfo.InvariantCulture),
        "-i", source,
        "-map", "0:v:0",
        "-an", "-sn", "-dn",
        "-frames:v", "1",
        "-vf", "scale=-2:" + height.ToString(CultureInfo.InvariantCulture) + ",format=rgb24",
        "-threads", "1",
        "-c:v", "png",
        "-f", "image2pipe",
        "pipe:1"
    };

    public async Task<byte[]?> ReadAsync(string source, long sourceTick, int height, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, "Yukseklik pozitif olmalidir");
        ct.ThrowIfCancellationRequested();

        var info = new ProcessStartInfo
        {
            FileName = _ffmpeg(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in Arguments(source, sourceTick, height)) info.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = info };
        process.Start();
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }

        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var image = new MemoryStream();
        using (ct.Register(() => AudioPeaks.Kill(process)))
        {
            try
            {
                Started?.Invoke(process);
                await process.StandardOutput.BaseStream.CopyToAsync(image, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
            }
            finally
            {
                if (ct.IsCancellationRequested) AudioPeaks.Kill(process);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        await stderr.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return process.ExitCode == 0 && image.Length > 0 ? image.ToArray() : null;
    }
}
