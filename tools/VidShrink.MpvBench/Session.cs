using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static VidShrink.MpvBench.Native;

namespace VidShrink.MpvBench;

internal readonly record struct FrameResult(bool Rendered, bool IsNew, double StartMs, double EndMs, ulong Flags);

internal sealed unsafe class Session : IDisposable
{
    private static readonly AutoResetEvent s_update = new(false);
    private static readonly Stopwatch s_clock = Stopwatch.StartNew();
    private static readonly IntPtr s_swType = Marshal.StringToCoTaskMemUTF8("sw");
    private static readonly IntPtr s_bgra = Marshal.StringToCoTaskMemUTF8("bgra");

    public static double Now => s_clock.Elapsed.TotalMilliseconds;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnUpdate(IntPtr ctx) => s_update.Set();

    private readonly Thread _eventThread;
    private volatile bool _stopping;
    private volatile bool _eof;
    private int _seekEvents;
    private int _restarts;
    private byte* _buffer;
    private int _width;
    private int _height;
    private nuint _stride;

    public IntPtr Mpv { get; }
    public IntPtr Ctx { get; }
    public bool Eof => _eof;
    public int SeekEvents => Volatile.Read(ref _seekEvents);
    public int Restarts => Volatile.Read(ref _restarts);
    public int Width => _width;
    public int Height => _height;
    public int FrameInfoRc { get; private set; } = int.MinValue;
    public int RenderErrors { get; private set; }
    public int LastRenderRc { get; private set; }
    public ConcurrentQueue<double> SeekEventTimes { get; } = new();
    public ConcurrentQueue<double> RestartTimes { get; } = new();
    public ConcurrentQueue<string> Log { get; } = new();
    public ConcurrentDictionary<string, string?> Info { get; } = new();
    public double FileLoadedMs { get; private set; } = double.NaN;
    public int EndFileReason { get; private set; } = -1;

    public Session(IEnumerable<(string Name, string Value)> options)
    {
        Mpv = mpv_create();
        if (Mpv == IntPtr.Zero)
        {
            throw new InvalidOperationException("mpv_create returned NULL");
        }

        foreach (var (name, value) in options)
        {
            Check(mpv_set_option_string(Mpv, name, value), $"option {name}={value}");
        }

        Check(mpv_initialize(Mpv), "mpv_initialize");
        Check(mpv_request_log_messages(Mpv, "warn"), "mpv_request_log_messages");
        Check(mpv_observe_property(Mpv, 1, "eof-reached", MPV_FORMAT_FLAG), "observe eof-reached");

        var parameters = stackalloc MpvRenderParam[2];
        parameters[0] = new MpvRenderParam(MPV_RENDER_PARAM_API_TYPE, s_swType);
        parameters[1] = new MpvRenderParam(MPV_RENDER_PARAM_INVALID, IntPtr.Zero);
        IntPtr ctx;
        Check(mpv_render_context_create(&ctx, Mpv, parameters), "mpv_render_context_create");
        Ctx = ctx;
        mpv_render_context_set_update_callback(Ctx, &OnUpdate, IntPtr.Zero);

        _eventThread = new Thread(EventLoop) { IsBackground = true, Name = "mpv-events" };
        _eventThread.Start();
    }

    public void Command(params string[] args) => Check(RunCommand(args, false, 0), "command " + args[0]);

    public void CommandAsync(ulong id, params string[] args) => Check(RunCommand(args, true, id), "command_async " + args[0]);

    private int RunCommand(string[] args, bool async, ulong id)
    {
        var ptrs = new IntPtr[args.Length + 1];
        try
        {
            for (var i = 0; i < args.Length; i++)
            {
                ptrs[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
            }

            fixed (IntPtr* p = ptrs)
            {
                return async ? mpv_command_async(Mpv, id, p) : mpv_command(Mpv, p);
            }
        }
        finally
        {
            foreach (var ptr in ptrs)
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(ptr);
                }
            }
        }
    }

    public string? GetString(string name)
    {
        var p = mpv_get_property_string(Mpv, name);
        if (p == IntPtr.Zero)
        {
            return null;
        }

        var s = Marshal.PtrToStringUTF8(p);
        mpv_free(p);
        return s;
    }

    public double GetDouble(string name)
    {
        double v;
        return mpv_get_property(Mpv, name, MPV_FORMAT_DOUBLE, &v) < 0 ? double.NaN : v;
    }

    public long GetLong(string name)
    {
        long v;
        return mpv_get_property(Mpv, name, MPV_FORMAT_INT64, &v) < 0 ? -1 : v;
    }

    public static bool WaitUpdate(int ms) => s_update.WaitOne(ms);

    public FrameResult Step(bool block)
    {
        var flags = mpv_render_context_update(Ctx);
        if ((flags & MPV_RENDER_UPDATE_FRAME) == 0)
        {
            return default;
        }

        if (_buffer == null && !Allocate())
        {
            return default;
        }

        MpvRenderFrameInfo info = default;
        var rc = mpv_render_context_get_info(Ctx, new MpvRenderParam(MPV_RENDER_PARAM_NEXT_FRAME_INFO, (IntPtr)(&info)));
        if (FrameInfoRc == int.MinValue || rc < 0)
        {
            FrameInfoRc = rc;
        }

        var size = stackalloc int[2];
        size[0] = _width;
        size[1] = _height;
        var stride = _stride;
        var blockValue = block ? 1 : 0;
        var parameters = stackalloc MpvRenderParam[6];
        parameters[0] = new MpvRenderParam(MPV_RENDER_PARAM_SW_SIZE, (IntPtr)size);
        parameters[1] = new MpvRenderParam(MPV_RENDER_PARAM_SW_FORMAT, s_bgra);
        parameters[2] = new MpvRenderParam(MPV_RENDER_PARAM_SW_STRIDE, (IntPtr)(&stride));
        parameters[3] = new MpvRenderParam(MPV_RENDER_PARAM_SW_POINTER, (IntPtr)_buffer);
        parameters[4] = new MpvRenderParam(MPV_RENDER_PARAM_BLOCK_FOR_TARGET_TIME, (IntPtr)(&blockValue));
        parameters[5] = new MpvRenderParam(MPV_RENDER_PARAM_INVALID, IntPtr.Zero);

        var start = Now;
        var renderRc = mpv_render_context_render(Ctx, parameters);
        var end = Now;
        LastRenderRc = renderRc;
        if (renderRc < 0)
        {
            RenderErrors++;
            return new FrameResult(false, false, start, end, info.Flags);
        }

        var isNew = rc < 0 || ((info.Flags & MPV_RENDER_FRAME_INFO_PRESENT) != 0 && (info.Flags & MPV_RENDER_FRAME_INFO_REDRAW) == 0);
        return new FrameResult(true, isNew, start, end, info.Flags);
    }

    private bool Allocate()
    {
        var w = GetLong("dwidth");
        var h = GetLong("dheight");
        if (w <= 0 || h <= 0)
        {
            return false;
        }

        _width = (int)w;
        _height = (int)h;
        _stride = (nuint)(4 * _width);
        _buffer = (byte*)NativeMemory.AlignedAlloc(_stride * (nuint)_height, 64);
        return true;
    }

    public void Dump(string path)
    {
        using var f = File.Create(path);
        f.Write(new ReadOnlySpan<byte>(_buffer, (int)(_stride * (nuint)_height)));
    }

    public ulong BufferAddress => (ulong)_buffer;

    public ulong Stride => _stride;

    private void EventLoop()
    {
        while (true)
        {
            var ev = mpv_wait_event(Mpv, 0.5);
            var t = Now;
            switch (ev->EventId)
            {
                case MPV_EVENT_NONE:
                    if (_stopping)
                    {
                        return;
                    }

                    break;
                case MPV_EVENT_SHUTDOWN:
                    return;
                case MPV_EVENT_LOG_MESSAGE:
                    var m = (MpvEventLogMessage*)ev->Data;
                    Log.Enqueue($"{Marshal.PtrToStringUTF8(m->Prefix)}/{Marshal.PtrToStringUTF8(m->Level)}: {Marshal.PtrToStringUTF8(m->Text)?.TrimEnd()}");
                    break;
                case MPV_EVENT_COMMAND_REPLY:
                    if (ev->Error < 0)
                    {
                        Log.Enqueue($"command reply {ev->ReplyUserdata}: {Error(ev->Error)}");
                    }

                    break;
                case MPV_EVENT_FILE_LOADED:
                    FileLoadedMs = t;
                    break;
                case MPV_EVENT_END_FILE:
                    EndFileReason = *(int*)ev->Data;
                    _eof = true;
                    break;
                case MPV_EVENT_SEEK:
                    SeekEventTimes.Enqueue(t);
                    Interlocked.Increment(ref _seekEvents);
                    break;
                case MPV_EVENT_PLAYBACK_RESTART:
                    RestartTimes.Enqueue(t);
                    if (Interlocked.Increment(ref _restarts) == 1)
                    {
                        foreach (var name in new[] { "hwdec-current", "video-codec", "video-format", "width", "height", "container-fps", "duration", "audio-codec-name", "current-ao", "current-vo" })
                        {
                            Info[name] = GetString(name);
                        }
                    }

                    break;
                case MPV_EVENT_PROPERTY_CHANGE:
                    var prop = (MpvEventProperty*)ev->Data;
                    if (ev->ReplyUserdata == 1 && prop->Format == MPV_FORMAT_FLAG && prop->Data != IntPtr.Zero && *(int*)prop->Data != 0)
                    {
                        _eof = true;
                    }

                    break;
            }
        }
    }

    public void Dispose()
    {
        _stopping = true;
        mpv_wakeup(Mpv);
        _eventThread.Join(5000);
        mpv_render_context_free(Ctx);
        mpv_terminate_destroy(Mpv);
        if (_buffer != null)
        {
            NativeMemory.AlignedFree(_buffer);
            _buffer = null;
        }
    }
}
