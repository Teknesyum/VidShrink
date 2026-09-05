using System.Diagnostics;
using VidShrink.Core;

namespace VidShrink.Ffmpeg.Playback;

public sealed record DecoderPipeFrame(byte[] Bgra, int Width, int Height, double PresentationSeconds);

public sealed record PipeFault(string ReasonTr, string ReasonEn);

public sealed class DecoderPipe : IDisposable
{
    public const long DefaultCacheByteCeiling = 128L * 1024 * 1024;

    public static readonly TimeSpan ForwardWaitTimeout = TimeSpan.FromSeconds(4);

    private readonly long _cacheByteCeiling;
    private readonly object _gate = new();
    private readonly Dictionary<int, byte[]> _cache = new();
    private readonly LinkedList<int> _lru = new();
    private long _cacheBytes;

    private List<double> _stamps = new();
    private string _path = "";
    private int _width;
    private int _height;
    private long _frameBytes;
    private double _durationSeconds;

    private Process? _videoProcess;
    private Thread? _videoReader;
    private CancellationTokenSource? _videoLife;
    private int _decodeStartIndex;
    private int _decodeCursorIndex;
    private bool _videoAlive;

    private Process? _audioProcess;
    private Thread? _audioReader;
    private CancellationTokenSource? _audioLife;
    private AudioSink? _sink;

    private int _processesStarted;
    private bool _disposed;

    public DecoderPipe(long cacheByteCeiling = DefaultCacheByteCeiling)
    {
        if (cacheByteCeiling <= 0) throw new ArgumentOutOfRangeException(nameof(cacheByteCeiling));
        _cacheByteCeiling = cacheByteCeiling;
    }

    public int ProcessesStarted => Volatile.Read(ref _processesStarted);

    public bool HasAudio { get; private set; }

    public double DurationSeconds => _durationSeconds;

    public int CacheCount { get { lock (_gate) return _cache.Count; } }

    public event EventHandler<PipeFault>? Faulted;

    public async Task OpenAsync(string path, KeyframeIndex? keyframes = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await StopAsync();

        var info = await FfprobeClient.ProbeAsync(path, ct);
        var index = keyframes ?? await KeyframeIndex.BuildAsync(path, ct);

        List<double> stamps;
        if (index is { IsEmpty: false } ki)
        {
            stamps = ki.Stamps.ToList();
        }
        else
        {
            stamps = new List<double>();
            for (var t = 0.0; t < info.DurationSeconds; t += 2.0) stamps.Add(t);
            if (stamps.Count == 0) stamps.Add(0);
        }

        lock (_gate)
        {
            _path = path;
            _stamps = stamps;
            _width = info.Width;
            _height = info.Height;
            _frameBytes = (long)_width * _height * 4;
            _durationSeconds = info.DurationSeconds;
            HasAudio = info.HasAudio;
            _cache.Clear();
            _lru.Clear();
            _cacheBytes = 0;
        }
    }

    public void AttachAudioSink(AudioSink sink)
    {
        lock (_gate) _sink = sink;
    }

    public async Task<DecoderPipeFrame?> SeekAsync(double atSeconds, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        List<double> stamps;
        lock (_gate) stamps = _stamps;
        if (stamps.Count == 0) return null;

        var targetIndex = FloorIndex(stamps, atSeconds);

        var deadline = DateTime.UtcNow + ForwardWaitTimeout;
        while (true)
        {
            bool needRestart;
            lock (_gate)
            {
                if (_cache.TryGetValue(targetIndex, out var hit))
                {
                    Touch(targetIndex);
                    return FrameFor(hit, stamps[targetIndex]);
                }

                needRestart = !_videoAlive || targetIndex < _decodeStartIndex || targetIndex < _decodeCursorIndex - 0;
                if (_videoAlive && targetIndex >= _decodeStartIndex && targetIndex >= _decodeCursorIndex)
                    needRestart = false;
            }

            if (needRestart)
            {
                RestartVideo(targetIndex, stamps);
                deadline = DateTime.UtcNow + ForwardWaitTimeout;
            }
            else if (DateTime.UtcNow >= deadline)
            {
                RestartVideo(targetIndex, stamps);
                deadline = DateTime.UtcNow + ForwardWaitTimeout;
            }

            lock (_gate)
            {
                if (_cache.TryGetValue(targetIndex, out var value))
                {
                    Touch(targetIndex);
                    return FrameFor(value, stamps[targetIndex]);
                }
                Monitor.Wait(_gate, TimeSpan.FromMilliseconds(25));
            }

            await Task.Yield();

            if (ct.IsCancellationRequested) return null;
        }
    }

    private DecoderPipeFrame FrameFor(byte[] bytes, double pts)
    {
        int w, h;
        lock (_gate) { w = _width; h = _height; }
        return new DecoderPipeFrame((byte[])bytes.Clone(), w, h, pts);
    }

    private static int FloorIndex(List<double> stamps, double atSeconds)
    {
        var chosen = 0;
        for (var i = 0; i < stamps.Count; i++)
        {
            if (stamps[i] > atSeconds + 1e-6) break;
            chosen = i;
        }
        return chosen;
    }

    private void Touch(int index)
    {
        _lru.Remove(index);
        _lru.AddFirst(index);
    }

    private void RestartVideo(int startIndex, List<double> stamps)
    {
        StopVideoOnly();

        var life = new CancellationTokenSource();
        var startAt = stamps[startIndex];
        var args = new[]
        {
            "-hide_banner", "-nostdin", "-loglevel", "error",
            "-ss", startAt.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "-skip_frame", "nokey",
            "-i", _path,
            "-vsync", "0", "-an", "-sn", "-dn",
            "-f", "rawvideo", "-pix_fmt", "bgra", "-"
        };

        var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        try
        {
            process.Start();
            Interlocked.Increment(ref _processesStarted);
        }
        catch
        {
            lock (_gate) _videoAlive = false;
            RaiseFault("ffmpeg baslatilamadi, boru bosta kaldi.", "ffmpeg could not start, the pipe is idle.");
            return;
        }

        lock (_gate)
        {
            _videoProcess = process;
            _videoLife = life;
            _decodeStartIndex = startIndex;
            _decodeCursorIndex = startIndex;
            _videoAlive = true;
        }

        var reader = new Thread(() => PumpVideo(process, life.Token, stamps))
        {
            IsBackground = true,
            Name = "vidshrink-decoder-pipe"
        };
        lock (_gate) _videoReader = reader;
        reader.Start();
    }

    private void PumpVideo(Process process, CancellationToken ct, List<double> stamps)
    {
        long frameBytes;
        lock (_gate) frameBytes = _frameBytes > 0 ? _frameBytes : 1;

        var stream = process.StandardOutput.BaseStream;
        var buffer = new byte[frameBytes];
        int index;
        lock (_gate) index = _decodeStartIndex;

        try
        {
            while (!ct.IsCancellationRequested && index < stamps.Count)
            {
                if (!ReadFull(stream, buffer, ct)) break;

                var copy = (byte[])buffer.Clone();
                lock (_gate)
                {
                    StoreLocked(index, copy);
                    _decodeCursorIndex = index + 1;
                    Monitor.PulseAll(_gate);
                }
                index++;
            }
        }
        catch { }
        finally
        {
            var expectedMore = index < stamps.Count - 1;
            int exitCode;
            try { process.WaitForExit(1000); exitCode = process.HasExited ? process.ExitCode : -1; }
            catch { exitCode = -1; }

            lock (_gate)
            {
                if (ReferenceEquals(_videoProcess, process)) _videoAlive = false;
            }

            if (expectedMore && !ct.IsCancellationRequested && exitCode != 0)
            {
                RaiseFault(
                    "kod cozucu surec beklenmeden dustu; bir sonraki aramada boru kendini yeniden kuracak.",
                    "the decoder process died unexpectedly; the pipe will rebuild itself on the next seek.");
            }

            try { if (!process.HasExited) process.Kill(true); } catch { }
            process.Dispose();
        }
    }

    private static bool ReadFull(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            if (ct.IsCancellationRequested) return false;
            var read = stream.Read(buffer, offset, buffer.Length - offset);
            if (read <= 0) return false;
            offset += read;
        }
        return true;
    }

    private void StoreLocked(int index, byte[] bytes)
    {
        if (_cache.ContainsKey(index)) return;
        while (_cacheBytes + bytes.LongLength > _cacheByteCeiling && _lru.Last is { } oldest)
        {
            _cache.Remove(oldest.Value, out var evicted);
            _cacheBytes -= evicted?.LongLength ?? 0;
            _lru.RemoveLast();
        }
        if (bytes.LongLength > _cacheByteCeiling) return;
        _cache[index] = bytes;
        _lru.AddFirst(index);
        _cacheBytes += bytes.LongLength;
    }

    private void RaiseFault(string tr, string en) => Faulted?.Invoke(this, new PipeFault(tr, en));

    public void SeekAudio(double atSeconds)
    {
        AudioSink? sink;
        lock (_gate) sink = _sink;
        if (sink is null || !HasAudio) return;

        StopAudioOnly();
        sink.Reset();

        var life = new CancellationTokenSource();
        var args = new[]
        {
            "-hide_banner", "-nostdin", "-loglevel", "error",
            "-ss", atSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "-i", _path,
            "-vn", "-sn", "-dn",
            "-f", "s16le", "-acodec", "pcm_s16le", "-ar", "48000", "-ac", "2", "-"
        };

        var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        try { process.Start(); } catch { return; }

        lock (_gate) _audioProcess = process;
        var reader = new Thread(() => PumpAudio(process, life.Token, sink)) { IsBackground = true, Name = "vidshrink-audio-pipe" };
        lock (_gate) { _audioLife = life; _audioReader = reader; }
        reader.Start();
        sink.Play();
    }

    private static void PumpAudio(Process process, CancellationToken ct, AudioSink sink)
    {
        var stream = process.StandardOutput.BaseStream;
        var chunk = new byte[16 * 1024];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var read = stream.Read(chunk, 0, chunk.Length);
                if (read <= 0) break;
                sink.Write(chunk.AsSpan(0, read));
            }
        }
        catch { }
        finally
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
            process.Dispose();
        }
    }

    private void StopVideoOnly()
    {
        Process? process;
        Thread? reader;
        CancellationTokenSource? life;
        lock (_gate)
        {
            process = _videoProcess;
            reader = _videoReader;
            life = _videoLife;
            _videoAlive = false;
            _videoProcess = null;
            _videoReader = null;
            _videoLife = null;
        }
        life?.Cancel();
        try { if (process is { HasExited: false }) process.Kill(true); } catch { }
        reader?.Join(2000);
        life?.Dispose();
    }

    private void StopAudioOnly()
    {
        Process? process;
        Thread? reader;
        CancellationTokenSource? life;
        lock (_gate)
        {
            process = _audioProcess;
            reader = _audioReader;
            life = _audioLife;
            _audioProcess = null;
            _audioReader = null;
            _audioLife = null;
        }
        life?.Cancel();
        try { if (process is { HasExited: false }) process.Kill(true); } catch { }
        reader?.Join(2000);
        life?.Dispose();
    }

    public Task StopAsync()
    {
        StopVideoOnly();
        StopAudioOnly();
        return Task.CompletedTask;
    }

    internal bool TestOnly_KillVideoProcess()
    {
        Process? process;
        lock (_gate) process = _videoProcess;
        if (process is null || process.HasExited) return false;
        try { process.Kill(true); return true; } catch { return false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopVideoOnly();
        StopAudioOnly();
    }
}
