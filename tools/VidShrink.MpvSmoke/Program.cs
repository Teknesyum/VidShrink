using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VidShrink.MpvBench;
using static VidShrink.MpvBench.Native;

namespace VidShrink.MpvSmoke;

internal static unsafe class Program
{
    private static readonly AutoResetEvent s_update = new(false);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnUpdate(IntPtr ctx) => s_update.Set();

    private static int Main(string[] args)
    {
        string? lib = null;
        string? file = null;
        var timeoutSec = 30;
        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--lib": lib = args[++i]; break;
                case "--file": file = args[++i]; break;
                case "--timeout": timeoutSec = int.Parse(args[++i]); break;
            }
        }

        if (lib is null || file is null)
        {
            Console.Error.WriteLine("usage: VidShrink.MpvSmoke --lib <libmpv> --file <video> [--timeout 30]");
            return 2;
        }

        try
        {
            return Run(Path.GetFullPath(lib), Path.GetFullPath(file), timeoutSec);
        }
        catch (Exception e)
        {
            Console.WriteLine($"smoke: FAIL {e.GetType().Name}: {e.Message}");
            return 1;
        }
    }

    private static int Run(string lib, string file, int timeoutSec)
    {
        Console.WriteLine($"smoke: os={RuntimeInformation.OSDescription} arch={RuntimeInformation.ProcessArchitecture} runtime={RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"smoke: lib={lib}");
        Load(lib);
        var api = mpv_client_api_version();
        Console.WriteLine($"smoke: client-api={api >> 16}.{api & 0xffff}");

        var mpv = mpv_create();
        if (mpv == IntPtr.Zero)
        {
            throw new InvalidOperationException("mpv_create returned NULL");
        }

        foreach (var (name, value) in new[] { ("config", "no"), ("vo", "libmpv"), ("hwdec", "no"), ("ao", "null"), ("audio", "no"), ("keep-open", "yes"), ("idle", "yes") })
        {
            Check(mpv_set_option_string(mpv, name, value), $"option {name}={value}");
        }

        Check(mpv_initialize(mpv), "mpv_initialize");
        Check(mpv_request_log_messages(mpv, "warn"), "mpv_request_log_messages");
        Console.WriteLine($"smoke: mpv-version={GetString(mpv, "mpv-version")}");
        Console.WriteLine($"smoke: ffmpeg-version={GetString(mpv, "ffmpeg-version")}");

        var sw = Marshal.StringToCoTaskMemUTF8("sw");
        var bgra = Marshal.StringToCoTaskMemUTF8("bgra");
        var create = stackalloc MpvRenderParam[2];
        create[0] = new MpvRenderParam(MPV_RENDER_PARAM_API_TYPE, sw);
        create[1] = new MpvRenderParam(MPV_RENDER_PARAM_INVALID, IntPtr.Zero);
        IntPtr ctx;
        Check(mpv_render_context_create(&ctx, mpv, create), "mpv_render_context_create");
        mpv_render_context_set_update_callback(ctx, &OnUpdate, IntPtr.Zero);

        Command(mpv, "loadfile", file);

        byte* buffer = null;
        var width = 0;
        var height = 0;
        nuint stride = 0;
        var frames = 0;
        var lastRc = 0;
        long nonZero = 0;
        var loaded = false;
        var clock = Stopwatch.StartNew();
        var ok = false;
        var size = stackalloc int[2];
        nuint st = 0;
        var render = stackalloc MpvRenderParam[5];

        while (clock.Elapsed.TotalSeconds < timeoutSec)
        {
            while (true)
            {
                var ev = mpv_wait_event(mpv, 0);
                if (ev->EventId == MPV_EVENT_NONE)
                {
                    break;
                }

                if (ev->EventId == MPV_EVENT_LOG_MESSAGE)
                {
                    var m = (MpvEventLogMessage*)ev->Data;
                    Console.WriteLine($"mpv: {Marshal.PtrToStringUTF8(m->Prefix)}/{Marshal.PtrToStringUTF8(m->Level)}: {Marshal.PtrToStringUTF8(m->Text)?.TrimEnd()}");
                }
                else if (ev->EventId == MPV_EVENT_FILE_LOADED)
                {
                    loaded = true;
                    Console.WriteLine($"smoke: file-loaded at {clock.Elapsed.TotalMilliseconds:F0} ms");
                }
                else if (ev->EventId == MPV_EVENT_END_FILE)
                {
                    Console.WriteLine($"smoke: end-file reason={*(int*)ev->Data} error={*((int*)ev->Data + 1)}");
                }
            }

            s_update.WaitOne(50);
            var flags = mpv_render_context_update(ctx);
            if ((flags & MPV_RENDER_UPDATE_FRAME) == 0)
            {
                continue;
            }

            if (buffer == null)
            {
                width = (int)GetLong(mpv, "dwidth");
                height = (int)GetLong(mpv, "dheight");
                if (width <= 0 || height <= 0)
                {
                    continue;
                }

                stride = (nuint)(4 * width);
                buffer = (byte*)NativeMemory.AlignedAlloc(stride * (nuint)height, 64);
                NativeMemory.Clear(buffer, stride * (nuint)height);
                Console.WriteLine($"smoke: buffer {width}x{height} stride={stride} aligned64={((ulong)buffer % 64) == 0}");
            }

            size[0] = width;
            size[1] = height;
            st = stride;
            render[0] = new MpvRenderParam(MPV_RENDER_PARAM_SW_SIZE, (IntPtr)size);
            render[1] = new MpvRenderParam(MPV_RENDER_PARAM_SW_FORMAT, bgra);
            render[2] = new MpvRenderParam(MPV_RENDER_PARAM_SW_STRIDE, (IntPtr)(&st));
            render[3] = new MpvRenderParam(MPV_RENDER_PARAM_SW_POINTER, (IntPtr)buffer);
            render[4] = new MpvRenderParam(MPV_RENDER_PARAM_INVALID, IntPtr.Zero);
            lastRc = mpv_render_context_render(ctx, render);
            if (lastRc < 0)
            {
                Console.WriteLine($"smoke: render rc={lastRc} {Error(lastRc)}");
                continue;
            }

            frames++;
            nonZero = CountNonZero(buffer, width, height, stride);
            if (loaded && frames >= 3 && nonZero > 0)
            {
                ok = true;
                break;
            }
        }

        var total = (long)width * height;
        Console.WriteLine($"smoke: frames-rendered={frames} last-rc={lastRc} nonzero-pixels={nonZero}/{total}");
        if (buffer != null && total > 0)
        {
            var c = buffer + (nuint)(height / 2) * stride + (nuint)(width / 2) * 4;
            Console.WriteLine($"smoke: center-pixel bgra=({c[0]},{c[1]},{c[2]},{c[3]}) fnv1a={Hash(buffer, stride * (nuint)height):x16}");
        }

        mpv_render_context_free(ctx);
        mpv_terminate_destroy(mpv);
        if (buffer != null)
        {
            NativeMemory.AlignedFree(buffer);
        }

        Console.WriteLine(ok ? "smoke: PASS" : "smoke: FAIL no non-zero frame within timeout");
        return ok ? 0 : 1;
    }

    private static long CountNonZero(byte* buffer, int width, int height, nuint stride)
    {
        long n = 0;
        for (var y = 0; y < height; y++)
        {
            var row = buffer + (nuint)y * stride;
            for (var x = 0; x < width; x++)
            {
                var p = row + x * 4;
                if ((p[0] | p[1] | p[2]) != 0)
                {
                    n++;
                }
            }
        }

        return n;
    }

    private static ulong Hash(byte* data, nuint length)
    {
        var h = 14695981039346656037UL;
        for (nuint i = 0; i < length; i++)
        {
            h = (h ^ data[i]) * 1099511628211UL;
        }

        return h;
    }

    private static string? GetString(IntPtr mpv, string name)
    {
        var p = mpv_get_property_string(mpv, name);
        if (p == IntPtr.Zero)
        {
            return null;
        }

        var s = Marshal.PtrToStringUTF8(p);
        mpv_free(p);
        return s;
    }

    private static long GetLong(IntPtr mpv, string name)
    {
        long v;
        return mpv_get_property(mpv, name, MPV_FORMAT_INT64, &v) < 0 ? -1 : v;
    }

    private static void Command(IntPtr mpv, params string[] args)
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
                Check(mpv_command(mpv, p), "command " + args[0]);
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
}
