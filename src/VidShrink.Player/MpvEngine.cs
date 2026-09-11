using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static VidShrink.Player.Native;

namespace VidShrink.Player;

public sealed class MpvEngine : IPlaybackEngine
{
    public const string FailedKey = "main.error.unusable";

    private const ulong OpenTag = 1;
    private const ulong ControlTag = 2;
    private const ulong SeekTag = 1UL << 40;
    private const ulong EofId = 1;
    private const ulong TimeId = 2;
    private const ulong PauseId = 3;
    private const int LogCapacity = 32;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly IntPtr SwType = Marshal.StringToCoTaskMemUTF8("sw");
    private static readonly IntPtr BgraFormat = Marshal.StringToCoTaskMemUTF8("bgra");

    private readonly PlaybackOptions _options;
    private readonly AutoResetEvent _update = new(false);
    private readonly object _frameGate = new();
    private readonly object _seekGate = new();
    private readonly ConcurrentQueue<string> _log = new();
    private readonly Thread _events;
    private readonly Thread _renderer;

    private GCHandle _updateHandle;
    private IntPtr _mpv;
    private IntPtr _render;
    private volatile bool _stopping;
    private int _disposed;

    private FrameBuffer? _front;
    private FrameBuffer? _back;
    private long _serial;
    private long _framesRendered;
    private long _restarts;
    private int _videoWidth;
    private int _videoHeight;

    private TaskCompletionSource? _open;
    private volatile bool _isOpen;
    private volatile bool _hasAudio;
    private volatile bool _paused = true;
    private volatile bool _eof;
    private double _duration;
    private double _position;

    private long _seekGen;
    private long _replyGen = -1;
    private long _armedGen = -1;
    private double _armedAt;
    private double _issuedAt;
    private TaskCompletionSource<SeekResult>? _seekDone;

    public unsafe MpvEngine(PlaybackOptions? options = null)
    {
        _options = options ?? new PlaybackOptions();
        LibMpvLocator.EnsureLoaded();

        _mpv = mpv_create();
        if (_mpv == IntPtr.Zero) throw new PlaybackEngineUnavailableException("mpv_create returned NULL.");

        try
        {
            foreach (var (name, value) in OptionsFor(_options))
                Check(mpv_set_option_string(_mpv, name, value), $"option {name}={value}");

            Check(mpv_initialize(_mpv), "mpv_initialize");
            Check(mpv_request_log_messages(_mpv, "warn"), "mpv_request_log_messages");
            Check(mpv_observe_property(_mpv, EofId, "eof-reached", MPV_FORMAT_FLAG), "observe eof-reached");
            Check(mpv_observe_property(_mpv, TimeId, "time-pos", MPV_FORMAT_DOUBLE), "observe time-pos");
            Check(mpv_observe_property(_mpv, PauseId, "pause", MPV_FORMAT_FLAG), "observe pause");

            var parameters = stackalloc MpvRenderParam[2];
            parameters[0] = new MpvRenderParam(MPV_RENDER_PARAM_API_TYPE, SwType);
            parameters[1] = new MpvRenderParam(MPV_RENDER_PARAM_INVALID, IntPtr.Zero);
            IntPtr render;
            Check(mpv_render_context_create(&render, _mpv, parameters), "mpv_render_context_create");
            _render = render;

            _updateHandle = GCHandle.Alloc(_update);
            mpv_render_context_set_update_callback(_render, &OnUpdate, GCHandle.ToIntPtr(_updateHandle));
        }
        catch
        {
            Release();
            throw;
        }

        _events = new Thread(EventLoop) { IsBackground = true, Name = "mpv-events" };
        _renderer = new Thread(RenderLoop) { IsBackground = true, Name = "mpv-render" };
        _events.Start();
        _renderer.Start();
    }

    public static IReadOnlyList<(string Name, string Value)> OptionsFor(PlaybackOptions options) => new[]
    {
        ("vo", "libmpv"),
        ("hwdec", options.Hardware == HardwareDecoding.AutoCopy ? "auto-copy" : "no"),
        ("keep-open", "yes"),
        ("idle", "yes"),
        ("pause", "yes"),
        ("terminal", "no"),
        ("input-default-bindings", "no"),
        ("input-vo-keyboard", "no"),
        ("load-scripts", "no"),
        ("osd-level", "0"),
        ("osd-bar", "no"),
        ("sub-auto", "no"),
        ("sid", "no"),
        ("audio-display", "no"),
        ("audio-fallback-to-null", "yes")
    };

    public event EventHandler<PlaybackFault>? Faulted;

    public string Name => "libmpv";

    public bool IsOpen => _isOpen;

    public double DurationSeconds => Volatile.Read(ref _duration);

    public bool HasAudio => _hasAudio;

    public bool IsPaused => _paused;

    public bool EndReached => _eof;

    public double PositionSeconds => Volatile.Read(ref _position);

    public long FramesRendered => Interlocked.Read(ref _framesRendered);

    public long PlaybackRestarts => Interlocked.Read(ref _restarts);

    public IReadOnlyList<string> RecentLog => _log.ToArray();

    public double AudioVideoOffsetSeconds
    {
        get
        {
            if (!_hasAudio || Volatile.Read(ref _disposed) != 0) return double.NaN;
            var video = GetDouble("time-pos");
            var audio = GetDouble("audio-pts");
            return double.IsFinite(video) && double.IsFinite(audio) ? video - audio : double.NaN;
        }
    }

    public string? GetProperty(string name)
    {
        if (Volatile.Read(ref _disposed) != 0) return null;
        var p = mpv_get_property_string(_mpv, name);
        if (p == IntPtr.Zero) return null;
        var value = Marshal.PtrToStringUTF8(p);
        mpv_free(p);
        return value;
    }

    public void SetProperty(string name, string value)
        => Check(mpv_set_property_string(_mpv, name, value), $"set {name}={value}");

    public async Task OpenAsync(string path, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var open = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write(ref _open, open);
        Command(OpenTag, "loadfile", Path.GetFullPath(path));

        var timeout = Task.Delay(_options.OpenTimeout, ct);
        var finished = await Task.WhenAny(open.Task, timeout).ConfigureAwait(false);
        if (finished != open.Task)
        {
            Interlocked.CompareExchange(ref _open, null, open);
            ct.ThrowIfCancellationRequested();
            throw new PlaybackOpenException(Describe($"libmpv did not load the file within {_options.OpenTimeout.TotalSeconds:0} s."));
        }

        await open.Task.ConfigureAwait(false);
    }

    public void Play() => Command(ControlTag, "set", "pause", "no");

    public void Pause() => Command(ControlTag, "set", "pause", "yes");

    public async Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        if (!_isOpen || Volatile.Read(ref _disposed) != 0) return new SeekResult(SeekOutcome.Failed, 0);

        var done = new TaskCompletionSource<SeekResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        long gen;
        TaskCompletionSource<SeekResult>? previous;
        lock (_seekGate)
        {
            gen = ++_seekGen;
            previous = _seekDone;
            _seekDone = done;
            _issuedAt = Now;
        }

        previous?.TrySetResult(new SeekResult(SeekOutcome.Superseded, 0));

        var target = Math.Max(0, seconds).ToString("0.######", CultureInfo.InvariantCulture);
        var flags = precision == SeekPrecision.Exact ? "absolute+exact" : "absolute+keyframes";
        var rc = CommandRc(SeekTag + (ulong)gen, "seek", target, flags);
        if (rc < 0) Finish(gen, new SeekResult(SeekOutcome.Failed, 0));

        using var registration = ct.Register(() => Finish(gen, new SeekResult(SeekOutcome.Canceled, 0)));
        var timeout = Task.Delay(_options.SeekTimeout);
        var finished = await Task.WhenAny(done.Task, timeout).ConfigureAwait(false);
        if (finished != done.Task) Finish(gen, new SeekResult(SeekOutcome.TimedOut, _options.SeekTimeout.TotalMilliseconds));
        return await done.Task.ConfigureAwait(false);
    }

    public unsafe bool TryCopyLatest(ref long seen, FrameCopy copy)
    {
        lock (_frameGate)
        {
            if (_front is not { } front || _serial == seen) return false;
            copy((IntPtr)front.Pixels, front.Width, front.Height, front.Stride);
            seen = _serial;
            return true;
        }
    }

    private static double Now => Clock.Elapsed.TotalMilliseconds;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnUpdate(IntPtr state)
    {
        if (GCHandle.FromIntPtr(state).Target is AutoResetEvent signal) signal.Set();
    }

    private void Finish(long gen, SeekResult result)
    {
        TaskCompletionSource<SeekResult>? done = null;
        lock (_seekGate)
        {
            if (gen == _seekGen && _seekDone is not null)
            {
                done = _seekDone;
                _seekDone = null;
            }
        }

        done?.TrySetResult(result);
    }

    private unsafe void EventLoop()
    {
        while (!_stopping)
        {
            var ev = mpv_wait_event(_mpv, 0.25);
            switch (ev->EventId)
            {
                case MPV_EVENT_SHUTDOWN:
                    return;
                case MPV_EVENT_LOG_MESSAGE:
                    OnLog((MpvEventLogMessage*)ev->Data);
                    break;
                case MPV_EVENT_COMMAND_REPLY:
                    OnReply(ev->ReplyUserdata, ev->Error);
                    break;
                case MPV_EVENT_FILE_LOADED:
                    OnLoaded();
                    break;
                case MPV_EVENT_END_FILE:
                    OnEnded(*(MpvEventEndFile*)ev->Data);
                    break;
                case MPV_EVENT_VIDEO_RECONFIG:
                    ReadVideoSize();
                    break;
                case MPV_EVENT_SEEK:
                    OnSeek();
                    break;
                case MPV_EVENT_PLAYBACK_RESTART:
                    Interlocked.Increment(ref _restarts);
                    break;
                case MPV_EVENT_PROPERTY_CHANGE:
                    OnProperty(ev->ReplyUserdata, (MpvEventProperty*)ev->Data);
                    break;
            }
        }
    }

    private unsafe void OnLog(MpvEventLogMessage* message)
    {
        var line = $"{Marshal.PtrToStringUTF8(message->Prefix)}/{Marshal.PtrToStringUTF8(message->Level)}: {Marshal.PtrToStringUTF8(message->Text)?.TrimEnd()}";
        _log.Enqueue(line);
        while (_log.Count > LogCapacity) _log.TryDequeue(out _);
    }

    private void OnReply(ulong tag, int error)
    {
        if (tag == OpenTag)
        {
            if (error < 0) Volatile.Read(ref _open)?.TrySetException(new PlaybackOpenException(Describe(Error(error))));
            return;
        }

        if (tag < SeekTag) return;
        var gen = (long)(tag - SeekTag);
        if (error < 0)
        {
            Finish(gen, new SeekResult(SeekOutcome.Failed, 0));
            return;
        }

        lock (_seekGate)
        {
            if (gen == _seekGen) _replyGen = gen;
        }
    }

    private void OnSeek()
    {
        lock (_seekGate)
        {
            if (_seekDone is null || _replyGen != _seekGen || _armedGen == _seekGen) return;
            _armedGen = _seekGen;
            _armedAt = Now;
        }
    }

    private void OnLoaded()
    {
        var duration = GetDouble("duration");
        Volatile.Write(ref _duration, double.IsFinite(duration) && duration > 0 ? duration : 0);
        _hasAudio = CountAudioTracks() > 0;
        ReadVideoSize();
        _isOpen = true;
        Interlocked.Exchange(ref _open, null)?.TrySetResult();
    }

    private unsafe int CountAudioTracks()
    {
        long count;
        if (mpv_get_property(_mpv, "track-list/count", MPV_FORMAT_INT64, &count) < 0) return 0;
        var audio = 0;
        for (var i = 0; i < count; i++)
            if (GetProperty($"track-list/{i}/type") == "audio") audio++;
        return audio;
    }

    private void OnEnded(MpvEventEndFile end)
    {
        if (end.Reason != MPV_END_FILE_REASON_ERROR) return;
        var reason = Error(end.Error);
        var open = Interlocked.Exchange(ref _open, null);
        if (open is not null)
        {
            open.TrySetException(new PlaybackOpenException(Describe(reason)));
            return;
        }

        _isOpen = false;
        Faulted?.Invoke(this, new PlaybackFault(FailedKey, reason));
    }

    private unsafe void OnProperty(ulong id, MpvEventProperty* property)
    {
        if (property->Data == IntPtr.Zero) return;
        switch (id)
        {
            case EofId when property->Format == MPV_FORMAT_FLAG:
                _eof = *(int*)property->Data != 0;
                break;
            case PauseId when property->Format == MPV_FORMAT_FLAG:
                _paused = *(int*)property->Data != 0;
                break;
            case TimeId when property->Format == MPV_FORMAT_DOUBLE:
                Volatile.Write(ref _position, *(double*)property->Data);
                break;
        }
    }

    private unsafe void ReadVideoSize()
    {
        long w, h;
        if (mpv_get_property(_mpv, "dwidth", MPV_FORMAT_INT64, &w) < 0) return;
        if (mpv_get_property(_mpv, "dheight", MPV_FORMAT_INT64, &h) < 0) return;
        if (w <= 0 || h <= 0) return;
        Volatile.Write(ref _videoWidth, (int)w);
        Volatile.Write(ref _videoHeight, (int)h);
        _update.Set();
    }

    private void RenderLoop()
    {
        while (!_stopping)
        {
            _update.WaitOne(100);
            if (_stopping) break;
            if ((mpv_render_context_update(_render) & MPV_RENDER_UPDATE_FRAME) == 0) continue;
            RenderFrame();
        }
    }

    private unsafe void RenderFrame()
    {
        var width = Volatile.Read(ref _videoWidth);
        var height = Volatile.Read(ref _videoHeight);
        if (width <= 0 || height <= 0) return;

        var back = _back;
        if (back is null || back.Width != width || back.Height != height)
        {
            back?.Dispose();
            back = new FrameBuffer(width, height);
            _back = back;
        }

        MpvRenderFrameInfo info = default;
        var infoRc = mpv_render_context_get_info(_render, new MpvRenderParam(MPV_RENDER_PARAM_NEXT_FRAME_INFO, (IntPtr)(&info)));

        var size = stackalloc int[2];
        size[0] = width;
        size[1] = height;
        var stride = (nuint)back.Stride;
        var block = 1;
        var parameters = stackalloc MpvRenderParam[6];
        parameters[0] = new MpvRenderParam(MPV_RENDER_PARAM_SW_SIZE, (IntPtr)size);
        parameters[1] = new MpvRenderParam(MPV_RENDER_PARAM_SW_FORMAT, BgraFormat);
        parameters[2] = new MpvRenderParam(MPV_RENDER_PARAM_SW_STRIDE, (IntPtr)(&stride));
        parameters[3] = new MpvRenderParam(MPV_RENDER_PARAM_SW_POINTER, (IntPtr)back.Pixels);
        parameters[4] = new MpvRenderParam(MPV_RENDER_PARAM_BLOCK_FOR_TARGET_TIME, (IntPtr)(&block));
        parameters[5] = new MpvRenderParam(MPV_RENDER_PARAM_INVALID, IntPtr.Zero);

        var start = Now;
        if (mpv_render_context_render(_render, parameters) < 0) return;
        var end = Now;

        lock (_frameGate)
        {
            _back = _front;
            _front = back;
            _serial++;
        }

        var isNew = infoRc < 0
            || ((info.Flags & MPV_RENDER_FRAME_INFO_PRESENT) != 0 && (info.Flags & MPV_RENDER_FRAME_INFO_REDRAW) == 0);
        if (!isNew) return;

        Interlocked.Increment(ref _framesRendered);

        TaskCompletionSource<SeekResult>? done = null;
        double latency = 0;
        lock (_seekGate)
        {
            if (_seekDone is not null && _armedGen == _seekGen && start >= _armedAt)
            {
                done = _seekDone;
                _seekDone = null;
                latency = end - _issuedAt;
            }
        }

        done?.TrySetResult(new SeekResult(SeekOutcome.Shown, latency));
    }

    private unsafe double GetDouble(string name)
    {
        double value;
        return mpv_get_property(_mpv, name, MPV_FORMAT_DOUBLE, &value) < 0 ? double.NaN : value;
    }

    private void Command(ulong tag, params string[] args)
        => Check(CommandRc(tag, args), "command " + args[0]);

    private unsafe int CommandRc(ulong tag, params string[] args)
    {
        if (Volatile.Read(ref _disposed) != 0) return -1;
        var pointers = new IntPtr[args.Length + 1];
        try
        {
            for (var i = 0; i < args.Length; i++) pointers[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
            fixed (IntPtr* p = pointers) return mpv_command_async(_mpv, tag, p);
        }
        finally
        {
            foreach (var pointer in pointers)
                if (pointer != IntPtr.Zero) Marshal.FreeCoTaskMem(pointer);
        }
    }

    private string Describe(string reason)
    {
        var log = _log.ToArray();
        return log.Length == 0 ? reason : reason + " | " + string.Join(" | ", log.TakeLast(4));
    }

    private static void Check(int rc, string what)
    {
        if (rc < 0) throw new PlaybackEngineUnavailableException($"libmpv {what} failed: {Error(rc)}");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _stopping = true;
        if (_mpv != IntPtr.Zero) mpv_wakeup(_mpv);
        _update.Set();
        _events?.Join();
        _renderer?.Join();

        Interlocked.Exchange(ref _open, null)?.TrySetException(new ObjectDisposedException(nameof(MpvEngine)));
        TaskCompletionSource<SeekResult>? pending;
        lock (_seekGate)
        {
            pending = _seekDone;
            _seekDone = null;
        }

        pending?.TrySetResult(new SeekResult(SeekOutcome.Canceled, 0));
        _isOpen = false;
        Release();
    }

    private void Release()
    {
        if (_render != IntPtr.Zero)
        {
            mpv_render_context_free(_render);
            _render = IntPtr.Zero;
        }

        if (_mpv != IntPtr.Zero)
        {
            mpv_terminate_destroy(_mpv);
            _mpv = IntPtr.Zero;
        }

        if (_updateHandle.IsAllocated) _updateHandle.Free();

        lock (_frameGate)
        {
            _front?.Dispose();
            _back?.Dispose();
            _front = null;
            _back = null;
        }

        _update.Dispose();
    }

    private sealed unsafe class FrameBuffer : IDisposable
    {
        public FrameBuffer(int width, int height)
        {
            Width = width;
            Height = height;
            Stride = 4 * width;
            Pixels = (byte*)NativeMemory.AlignedAlloc((nuint)Stride * (nuint)height, 64);
        }

        public int Width { get; }

        public int Height { get; }

        public int Stride { get; }

        public byte* Pixels { get; private set; }

        public void Dispose()
        {
            if (Pixels == null) return;
            NativeMemory.AlignedFree(Pixels);
            Pixels = null;
        }
    }
}
