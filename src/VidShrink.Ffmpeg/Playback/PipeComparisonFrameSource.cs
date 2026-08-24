using System.Diagnostics;
using VidShrink.Core.Playback;

namespace VidShrink.Ffmpeg.Playback;

/// <summary>
/// Kalici tek ffmpeg surecinden BGRA kare besleyen kaynak.
/// </summary>
/// <remarks>
/// Uc olculmus karar koda birebir gecti:
/// <list type="bullet">
/// <item>T33/P8: boru <b>64 KB parcalarla</b> okunur. Kareyi tek okumada istemek 70,9 fps,
/// ayni kareyi parcalardan toplamak 148,0 fps veriyor.</item>
/// <item>T37: kare tamponu <b>havuzdan</b> gelir; kare basina yeni tampon kopya suresini
/// 0,77 ms'ten 1,47 ms'e cikariyor.</item>
/// <item>T37: boru ile sunum arasinda <b>cok gozlu halka</b> vardir; tek gozlu tampon
/// 361 karenin 15'ini dusurmustu.</item>
/// </list>
/// T32'nin 692-800 ms'lik surec acilis maliyeti bir kez odenir: oynat/duraklat sureci
/// oldurmez, yalnizca boru okumasini keser.
/// </remarks>
public sealed class PipeComparisonFrameSource : IComparisonFrameSource
{
    /// <summary>T33/P8'in olctugu okuma parcasi. 256 KB ile ayni (148,3), 1 MB ile daha kotu (143,9).</summary>
    public const int ChunkBytes = 64 * 1024;

    /// <summary>Halkanin varsayilan goz sayisi.</summary>
    public const int DefaultRingCapacity = 4;

    private readonly int _ringCapacity;
    private readonly object _gate = new();
    private readonly ManualResetEventSlim _resume = new(true);
    private readonly Queue<string> _stderrTail = new();

    private ComparisonFrameRequest? _request;
    private FramePool? _pool;
    private FrameRing? _ring;
    private Process? _process;
    private Thread? _reader;
    private CancellationTokenSource? _life;
    private EventHandler? _processExitHook;

    private ComparisonSourceState _state = ComparisonSourceState.Bosta;
    private string? _messageTr;
    private string? _messageEn;
    private long _produced;
    private long _discarded;
    private int _readErrors;
    private long _sequence;
    private double _feedFps;
    private long _windowStartTicks;
    private long _windowFrames;
    private bool _disposed;

    public PipeComparisonFrameSource(int ringCapacity = DefaultRingCapacity)
    {
        if (ringCapacity < FrameRing.MinimumCapacity)
            throw new ArgumentOutOfRangeException(nameof(ringCapacity), $"Halka en az {FrameRing.MinimumCapacity} gozlu olmali.");
        _ringCapacity = ringCapacity;
    }

    public event EventHandler<ComparisonSourceStatus>? StatusChanged;

    public ComparisonSourceStatus Status
    {
        get
        {
            lock (_gate)
            {
                return new ComparisonSourceStatus(
                    _state,
                    Interlocked.Read(ref _produced),
                    (_ring?.Dropped ?? 0) + Interlocked.Read(ref _discarded),
                    _feedFps,
                    Volatile.Read(ref _readErrors),
                    _pool?.Allocations ?? 0,
                    _messageTr,
                    _messageEn);
            }
        }
    }

    /// <summary>Havuz — olcum ve dogrulama icin disaridan okunur.</summary>
    public FramePool? Pool { get { lock (_gate) return _pool; } }

    public async Task StartAsync(ComparisonFrameRequest request, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        await StopAsync();

        if (!ToolLocator.IsAvailable(out var missing))
        {
            SetUnavailable(
                $"{missing} bulunamadi; karsilastirma oynaticisi calisamaz.",
                $"{missing} was not found, so the comparison player cannot run.");
            return;
        }

        var works = await Task.Run(ComparisonGraph.HstackWorks, ct);
        if (!works)
        {
            SetUnavailable(
                "Bu ffmpeg yapisinda hstack filtresi calismiyor; karsilastirma oynaticisi acilamaz.",
                "The hstack filter does not work in this ffmpeg build, so the comparison player cannot open.");
            return;
        }

        lock (_gate)
        {
            _request = request;
            // Havuz halkadan iki fazla: bir kare tuketicinin elinde, bir kare okunmakta olabilir.
            _pool = new FramePool(_ringCapacity + 2, request.FrameBytes);
            _ring = new FrameRing(_ringCapacity, _pool);
            _messageTr = null;
            _messageEn = null;
            Interlocked.Exchange(ref _produced, 0);
            Interlocked.Exchange(ref _discarded, 0);
            Volatile.Write(ref _readErrors, 0);
            _sequence = 0;
            _feedFps = 0;
        }

        _resume.Set();
        StartProcess(request);
    }

    private void StartProcess(ComparisonFrameRequest request)
    {
        var life = new CancellationTokenSource();
        var args = ComparisonGraph.BuildArguments(request);
        var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        process.StartInfo.RedirectStandardInput = true;

        process.ErrorDataReceived += OnStandardError;
        process.Start();
        process.BeginErrorReadLine();

        // Uygulama duserse surec oksuz kalmasin.
        EventHandler hook = (_, _) => { try { if (!process.HasExited) process.Kill(true); } catch { } };
        AppDomain.CurrentDomain.ProcessExit += hook;

        var reader = new Thread(() => Pump(process, life.Token))
        {
            IsBackground = true,
            Name = "vidshrink-compare-pipe",
            Priority = ThreadPriority.AboveNormal
        };

        lock (_gate)
        {
            _process = process;
            _life = life;
            _reader = reader;
            _processExitHook = hook;
            _windowStartTicks = Stopwatch.GetTimestamp();
            _windowFrames = 0;
        }

        SetState(ComparisonSourceState.Aciliyor);
        reader.Start();
    }

    private void OnStandardError(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data)) return;
        lock (_stderrTail)
        {
            _stderrTail.Enqueue(e.Data);
            while (_stderrTail.Count > 8) _stderrTail.Dequeue();
        }
    }

    private void Pump(Process process, CancellationToken ct)
    {
        var stream = process.StandardOutput.BaseStream;
        FramePool pool;
        FrameRing ring;
        ComparisonFrameRequest request;
        lock (_gate)
        {
            if (_pool is null || _ring is null || _request is null) return;
            pool = _pool;
            ring = _ring;
            request = _request;
        }

        var discard = new byte[ChunkBytes];

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _resume.Wait(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!TryAcquire(pool, ring, out var frame))
            {
                // Tuketici butun tamponlari tutuyor: boruyu tikamamak icin kareyi okuyup atiyoruz.
                if (!Drain(stream, request.FrameBytes, discard, ct)) break;
                continue;
            }

            var filled = FillFrame(stream, frame.Buffer, request.FrameBytes, ChunkBytes, ct);
            if (filled != request.FrameBytes)
            {
                pool.Return(frame);
                if (filled > 0 && !ct.IsCancellationRequested) Interlocked.Increment(ref _readErrors);
                break;
            }

            var sequence = Interlocked.Increment(ref _sequence) - 1;
            var presentation = request.Position + TimeSpan.FromSeconds(sequence / (double)request.Fps);
            frame.Describe(request.FrameWidth, request.PanelHeight, request.PanelWidth, presentation, sequence);
            ring.Publish(frame);

            Interlocked.Increment(ref _produced);
            SampleFeedRate();

            if (sequence == 0) SetState(ComparisonSourceState.Oynuyor);
        }

        if (!ct.IsCancellationRequested) SetState(ComparisonSourceState.Durdu);
    }

    private static bool TryAcquire(FramePool pool, FrameRing ring, out PlaybackFrame frame)
    {
        if (pool.TryRent(out frame)) return true;
        // Havuz bos: en eski kareyi dusurup tamponunu geri kazan. Uretici beklemez.
        return ring.TryEvictOldest(out frame);
    }

    /// <summary>
    /// Kare tamponunu <paramref name="chunkSize"/> baytlik parcalarla doldurur ve okunan bayt
    /// sayisini doner. Kare boyunda tek okuma <b>istenmez</b> — T33/P8 bunun yari hiz oldugunu
    /// olctu. Donen sayi <paramref name="length"/> ise kare tamdir; 0 ise akis temiz bitmistir;
    /// arada bir sayi ise boru yarim kare vermistir, bu bir okuma hatasidir.
    /// </summary>
    public static int FillFrame(Stream stream, byte[] buffer, int length, int chunkSize, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(buffer);
        if (length <= 0 || length > buffer.Length) throw new ArgumentOutOfRangeException(nameof(length));
        if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));

        var read = 0;
        while (read < length)
        {
            if (ct.IsCancellationRequested) return read;
            int n;
            try
            {
                n = stream.Read(buffer, read, Math.Min(chunkSize, length - read));
            }
            catch (IOException)
            {
                return read;
            }
            catch (ObjectDisposedException)
            {
                return read;
            }
            if (n <= 0) return read;
            read += n;
        }
        return read;
    }

    private bool Drain(Stream stream, int length, byte[] discard, CancellationToken ct)
    {
        var read = 0;
        while (read < length)
        {
            if (ct.IsCancellationRequested) return false;
            int n;
            try
            {
                n = stream.Read(discard, 0, Math.Min(discard.Length, length - read));
            }
            catch (IOException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            if (n <= 0) return false;
            read += n;
        }
        Interlocked.Increment(ref _produced);
        Interlocked.Increment(ref _discarded);
        return true;
    }

    private void SampleFeedRate()
    {
        long frames;
        long startTicks;
        lock (_gate)
        {
            _windowFrames++;
            frames = _windowFrames;
            startTicks = _windowStartTicks;
        }

        var elapsed = Stopwatch.GetElapsedTime(startTicks);
        if (elapsed.TotalMilliseconds < 250) return;

        lock (_gate)
        {
            _feedFps = frames / elapsed.TotalSeconds;
            _windowFrames = 0;
            _windowStartTicks = Stopwatch.GetTimestamp();
        }
        RaiseStatus();
    }

    public bool TryTake(out PlaybackFrame frame)
    {
        FrameRing? ring;
        lock (_gate) ring = _ring;
        if (ring is null)
        {
            frame = null!;
            return false;
        }
        return ring.TryTake(out frame);
    }

    public void Return(PlaybackFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        FramePool? pool;
        lock (_gate) pool = _pool;
        pool?.Return(frame);
    }

    public void Play()
    {
        if (_disposed) return;
        lock (_gate)
        {
            if (_state is ComparisonSourceState.Kullanilamiyor or ComparisonSourceState.Bosta) return;
        }
        _resume.Set();
        SetState(ComparisonSourceState.Oynuyor);
    }

    public void Pause()
    {
        if (_disposed) return;
        lock (_gate)
        {
            if (_state is not (ComparisonSourceState.Oynuyor or ComparisonSourceState.Aciliyor)) return;
        }
        _resume.Reset();
        SetState(ComparisonSourceState.Duraklatildi);
    }

    /// <summary>
    /// Konuma atlar. ffmpeg akan boruda geriye saramaz, bu yuzden surec yeniden kurulur;
    /// havuz ve halka ayakta kalir, kare basina ayirma yine sifirdir.
    /// </summary>
    public async Task SeekAsync(TimeSpan position, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (position < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(position));

        ComparisonFrameRequest? request;
        lock (_gate) request = _request;
        if (request is null) return;

        await StopProcessAsync();

        lock (_gate)
        {
            _request = request with { Position = position };
            _ring?.Clear();
            _sequence = 0;
        }

        ct.ThrowIfCancellationRequested();
        _resume.Set();
        ComparisonFrameRequest next;
        lock (_gate) next = _request!;
        StartProcess(next);
    }

    public async Task StopAsync()
    {
        await StopProcessAsync();
        lock (_gate)
        {
            _ring?.Clear();
            if (_state is not (ComparisonSourceState.Bosta or ComparisonSourceState.Kullanilamiyor))
                _state = ComparisonSourceState.Durdu;
        }
        RaiseStatus();
    }

    private async Task StopProcessAsync()
    {
        Process? process;
        Thread? reader;
        CancellationTokenSource? life;
        EventHandler? hook;
        lock (_gate)
        {
            process = _process;
            reader = _reader;
            life = _life;
            hook = _processExitHook;
            _process = null;
            _reader = null;
            _life = null;
            _processExitHook = null;
        }

        if (process is null && life is null) return;

        life?.Cancel();
        _resume.Set();

        if (process is not null)
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
            try { await process.WaitForExitAsync(); } catch { }
            process.ErrorDataReceived -= OnStandardError;
            process.Dispose();
        }

        if (hook is not null) AppDomain.CurrentDomain.ProcessExit -= hook;
        if (reader is not null && reader.IsAlive) reader.Join(2000);
        life?.Dispose();
    }

    private void SetUnavailable(string tr, string en)
    {
        lock (_gate)
        {
            _state = ComparisonSourceState.Kullanilamiyor;
            _messageTr = tr;
            _messageEn = en;
        }
        RaiseStatus();
    }

    private void SetState(ComparisonSourceState state)
    {
        lock (_gate)
        {
            if (_state == state) return;
            _state = state;
        }
        RaiseStatus();
    }

    private void RaiseStatus() => StatusChanged?.Invoke(this, Status);

    /// <summary>ffmpeg'in stderr'inden tutulan son satirlar. Tani icin.</summary>
    public IReadOnlyList<string> ErrorTail
    {
        get { lock (_stderrTail) return _stderrTail.ToArray(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { StopProcessAsync().GetAwaiter().GetResult(); } catch { }
        lock (_gate)
        {
            _ring?.Clear();
            _ring = null;
            _pool = null;
        }
        _resume.Dispose();
    }
}
