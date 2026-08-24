using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace VidShrink.PresentBench;

internal static class Program
{
    internal static BenchOptions Options = new();

    [STAThread]
    public static int Main(string[] args)
    {
        Options = BenchOptions.Parse(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(Array.Empty<string>());
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<BenchApp>().UsePlatformDetect().LogToTrace();
}

internal sealed class BenchOptions
{
    public int Width { get; init; } = 3840;
    public int Height { get; init; } = 1080;
    public int Seconds { get; init; } = 6;
    public string Mode { get; init; } = "raf";
    public string? Ffmpeg { get; init; }
    public string? Left { get; init; }
    public string? Right { get; init; }
    public int SourceFps { get; init; } = 60;
    public bool Realtime { get; init; }

    public static BenchOptions Parse(string[] a)
    {
        int w = 3840, h = 1080, sec = 6, sfps = 60;
        string mode = "raf";
        string? ff = null, left = null, right = null;
        var realtime = false;
        for (var i = 0; i < a.Length; i++)
        {
            switch (a[i])
            {
                case "--size" when i + 1 < a.Length:
                    var parts = a[++i].Split('x');
                    w = int.Parse(parts[0]); h = int.Parse(parts[1]);
                    break;
                case "--seconds" when i + 1 < a.Length: sec = int.Parse(a[++i]); break;
                case "--mode" when i + 1 < a.Length: mode = a[++i]; break;
                case "--ffmpeg" when i + 1 < a.Length: ff = a[++i]; break;
                case "--left" when i + 1 < a.Length: left = a[++i]; break;
                case "--right" when i + 1 < a.Length: right = a[++i]; break;
                case "--source-fps" when i + 1 < a.Length: sfps = int.Parse(a[++i]); break;
                case "--realtime": realtime = true; break;
            }
        }
        return new BenchOptions
        {
            Width = w, Height = h, Seconds = sec, Mode = mode,
            Ffmpeg = ff, Left = left, Right = right, SourceFps = sfps, Realtime = realtime
        };
    }
}

internal sealed class BenchApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new BenchWindow(Program.Options);
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class BenchWindow : Window
{
    private readonly BenchOptions _opt;
    private readonly WriteableBitmap _bitmap;
    private readonly Image _image;
    private readonly TextBlock _status;
    private readonly byte[][] _frames;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private int _presented;
    private long _copyTicks;
    private int _copyCount;
    private double _startedAt = -1;
    private int _frameIndex;
    private bool _done;

    private FrameFeeder? _feeder;
    private VidShrink.Core.Playback.IComparisonFrameSource? _source;
    private VidShrink.Core.Playback.PlaybackFrame? _pendingReturn;

    public BenchWindow(BenchOptions opt)
    {
        _opt = opt;
        Title = "VidShrink present bench";
        Width = 1280;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.Black;

        _bitmap = new WriteableBitmap(
            new PixelSize(opt.Width, opt.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        _image = new Image
        {
            Source = _bitmap,
            Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.LowQuality);

        _status = new TextBlock
        {
            Foreground = Brushes.Lime,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8)
        };

        var dock = new DockPanel();
        DockPanel.SetDock(_status, Dock.Bottom);
        dock.Children.Add(_status);
        dock.Children.Add(_image);
        Content = dock;

        _frames = BuildSyntheticFrames(opt.Width, opt.Height, 8);

        Opened += (_, _) => Start();
    }

    private static byte[][] BuildSyntheticFrames(int w, int h, int count)
    {
        var set = new byte[count][];
        var stride = w * 4;
        for (var f = 0; f < count; f++)
        {
            var buf = new byte[stride * h];
            for (var y = 0; y < h; y++)
            {
                var row = y * stride;
                for (var x = 0; x < w; x++)
                {
                    var i = row + x * 4;
                    buf[i + 0] = (byte)((x + f * 17) & 0xFF);
                    buf[i + 1] = (byte)((y + f * 31) & 0xFF);
                    buf[i + 2] = (byte)((x ^ y) & 0xFF);
                    buf[i + 3] = 0xFF;
                }
            }
            set[f] = buf;
        }
        return set;
    }

    private void Start()
    {
        if (_opt.Mode == "pipe")
        {
            _feeder = new FrameFeeder(_opt);
            _feeder.Start();
        }
        else if (_opt.Mode == "source")
        {
            var pipe = new VidShrink.Ffmpeg.Playback.PipeComparisonFrameSource();
            _source = pipe;
            _ = pipe.StartAsync(new VidShrink.Core.Playback.ComparisonFrameRequest
            {
                LeftPath = _opt.Left!,
                RightPath = _opt.Right!,
                PanelWidth = _opt.Width / 2,
                PanelHeight = _opt.Height,
                Fps = _opt.SourceFps,
                Realtime = _opt.Realtime,
                Loop = true
            });
        }

        var top = GetTopLevel(this)!;

        void Tick(TimeSpan _)
        {
            if (_done) return;
            var now = _clock.Elapsed.TotalSeconds;

            if (_startedAt < 0 && now > 1.5)
            {
                _startedAt = now;
                _presented = 0; _copyTicks = 0; _copyCount = 0;
                _feeder?.ResetCounters();
            }

            var frame = NextFrame();
            if (frame is not null)
            {
                var t0 = Stopwatch.GetTimestamp();
                Blit(frame);
                _copyTicks += Stopwatch.GetTimestamp() - t0;
                _copyCount++;
                _image.InvalidateVisual();

                if (_pendingReturn is not null)
                {
                    _source!.Return(_pendingReturn);
                    _pendingReturn = null;
                }
            }

            if (_startedAt >= 0) _presented++;

            if (_startedAt >= 0 && now - _startedAt >= _opt.Seconds)
            {
                _done = true;
                Report(now - _startedAt);
                return;
            }

            _status.Text = $"{_opt.Mode} {_opt.Width}x{_opt.Height} · t={now:F1}s · presented={_presented}";
            top.RequestAnimationFrame(Tick);
        }

        top.RequestAnimationFrame(Tick);
    }

    private byte[]? NextFrame()
    {
        if (_opt.Mode == "source")
        {
            if (!_source!.TryTake(out var live)) return null;
            _pendingReturn = live;
            return live.Buffer;
        }
        if (_opt.Mode == "pipe") return _feeder!.TryTake();
        if (_opt.Mode == "static") return _frameIndex++ == 0 ? _frames[0] : null;
        return _frames[_frameIndex++ % _frames.Length];
    }

    private unsafe void Blit(byte[] source)
    {
        using var fb = _bitmap.Lock();
        var bytes = Math.Min((long)source.Length, (long)fb.RowBytes * fb.Size.Height);
        fixed (byte* src = source)
        {
            Buffer.MemoryCopy(src, (void*)fb.Address, bytes, bytes);
        }
    }

    private void Report(double elapsed)
    {
        var fps = _presented / elapsed;
        var copyMs = _copyCount == 0
            ? 0
            : (_copyTicks / (double)Stopwatch.Frequency) * 1000.0 / _copyCount;
        var mib = _opt.Width * (double)_opt.Height * 4 / 1048576.0;

        var line =
            $"RESULT mode={_opt.Mode} size={_opt.Width}x{_opt.Height} frameMiB={mib:F2} " +
            $"elapsed={elapsed:F2} presented={_presented} fps={fps:F1} " +
            $"copies={_copyCount} copyMsAvg={copyMs:F3}" +
            (_feeder is null ? "" : $" fed={_feeder.Produced} starved={_feeder.Starved} feedFps={_feeder.Produced / elapsed:F1}");

        if (_source is not null)
        {
            var s = _source.Status;
            line += $" state={s.State} produced={s.ProducedFrames} dropped={s.DroppedFrames}" +
                    $" feedFps={s.FeedFps:F1} readErrors={s.ReadErrors} poolAlloc={s.PoolAllocations}" +
                    (s.MessageTr is null ? "" : $" msg={s.MessageTr}");
        }

        Console.Error.WriteLine(line);
        Console.Error.Flush();
        _status.Text = line;
        _feeder?.Stop();
        _source?.Dispose();
        DispatcherTimer.RunOnce(Close, TimeSpan.FromMilliseconds(300));
    }
}

internal sealed class FrameFeeder
{
    private readonly BenchOptions _opt;
    private readonly int _frameBytes;
    private Process? _proc;
    private Thread? _thread;
    private volatile bool _stop;
    private byte[]? _latest;
    private readonly object _gate = new();

    private int _produced;
    private int _starved;

    public int Produced => _produced;
    public int Starved => _starved;

    public FrameFeeder(BenchOptions opt)
    {
        _opt = opt;
        _frameBytes = opt.Width * opt.Height * 4;
    }

    public void ResetCounters()
    {
        Interlocked.Exchange(ref _produced, 0);
        Interlocked.Exchange(ref _starved, 0);
    }

    public void Start()
    {
        var half = _opt.Width / 2;
        var pace = _opt.Realtime ? "-re " : string.Empty;
        var args =
            "-hide_banner -loglevel error " +
            pace + "-stream_loop -1 -i \"" + _opt.Left + "\" " +
            pace + "-stream_loop -1 -i \"" + _opt.Right + "\" " +
            "-filter_complex \"[0:v]fps=" + _opt.SourceFps + ",scale=" + half + ":" + _opt.Height + "[l];" +
            "[1:v]fps=" + _opt.SourceFps + ",scale=" + half + ":" + _opt.Height + "[r];[l][r]hstack=inputs=2[v]\" " +
            "-map \"[v]\" -f rawvideo -pix_fmt bgra -";

        _proc = new Process
        {
            StartInfo = new ProcessStartInfo(_opt.Ffmpeg ?? "ffmpeg", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        _proc.Start();
        _proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.Error.WriteLine("FFMPEG " + e.Data); };
        _proc.BeginErrorReadLine();

        _thread = new Thread(Pump) { IsBackground = true, Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    private void Pump()
    {
        var stream = _proc!.StandardOutput.BaseStream;
        const int Chunk = 64 * 1024;
        while (!_stop)
        {
            var buf = new byte[_frameBytes];
            var read = 0;
            while (read < _frameBytes && !_stop)
            {
                var n = stream.Read(buf, read, Math.Min(Chunk, _frameBytes - read));
                if (n <= 0) return;
                read += n;
            }
            if (_stop) return;
            lock (_gate) { _latest = buf; }
            Interlocked.Increment(ref _produced);
        }
    }

    public byte[]? TryTake()
    {
        lock (_gate)
        {
            var f = _latest;
            _latest = null;
            if (f is null) Interlocked.Increment(ref _starved);
            return f;
        }
    }

    public void Stop()
    {
        _stop = true;
        try { if (_proc is { HasExited: false }) _proc.Kill(true); } catch { }
    }
}
