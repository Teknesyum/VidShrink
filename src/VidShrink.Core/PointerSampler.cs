using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VidShrink.Core;

public sealed class PointerSampler : IDisposable
{
    private readonly Func<(int X, int Y)?> _read;
    private readonly Action<int, int> _sink;
    private readonly int _periodMs;
    private Thread? _thread;
    private volatile bool _stop;
    private long _reads;

    public PointerSampler(Func<(int X, int Y)?> read, Action<int, int> sink, int hz = PointerTrackRecorder.SampleHz)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        if (hz <= 0 || hz > 1000) throw new ArgumentOutOfRangeException(nameof(hz));
        _periodMs = Math.Max(1, 1000 / hz);
    }

    public long Reads => Interlocked.Read(ref _reads);

    public bool Running => _thread is not null;

    public void Start()
    {
        if (_thread is not null) return;
        _stop = false;
        _thread = new Thread(Loop) { IsBackground = true, Name = "PointerSampler", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    public void Stop()
    {
        if (_thread is not { } thread) return;
        _stop = true;
        thread.Join();
        _thread = null;
    }

    public void Dispose() => Stop();

    private void Loop()
    {
        var clock = Stopwatch.StartNew();
        var next = 0L;
        while (!_stop)
        {
            if (_read() is { } at) _sink(at.X, at.Y);
            Interlocked.Increment(ref _reads);
            next += _periodMs;
            var wait = next - clock.ElapsedMilliseconds;
            if (wait < -_periodMs) next = clock.ElapsedMilliseconds;
            Thread.Sleep((int)Math.Max(1, wait));
        }
    }

    public static (int X, int Y)? ReadSystem()
    {
        if (!OperatingSystem.IsWindows()) return null;
        return GetPhysicalCursorPos(out var point) ? (point.X, point.Y) : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalCursorPos(out NativePoint point);
}
