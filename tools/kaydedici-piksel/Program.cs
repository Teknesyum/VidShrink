using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using VidShrink.App.Recorder;

namespace VidShrink.KaydediciPiksel;

internal sealed class ProbeApp : Application
{
    private static readonly string[] Dictionaries =
    {
        "Themes/Palette/Neon/Theme.axaml", "Themes/Theme.axaml", "Themes/Icons.axaml", "Themes/Playback.axaml", "Themes/Recorder.axaml"
    };

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        foreach (var path in Dictionaries)
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://VidShrink.KaydediciPiksel/"))
            {
                Source = new Uri("avares://VidShrink.App/" + path)
            });
        Styles.Add(new FluentTheme());
    }
}

public static class Program
{
    internal static string Out = "";
    internal static readonly StringBuilder Log = new();

    [STAThread]
    public static int Main(string[] args)
    {
        var root = RepoRoot();
        Out = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(root, ".calisma", "paket-2b", "piksel");
        Directory.CreateDirectory(Out);
        var settings = Path.Combine(Out, "ayar", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", settings);

        AppBuilder.Configure<ProbeApp>().UsePlatformDetect().SetupWithoutStarting();

        var code = 1;
        var done = new CancellationTokenSource();
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                code = await Probe.Run();
            }
            catch (Exception e)
            {
                Say("HATA\t" + e);
            }
            finally
            {
                done.Cancel();
            }
        });
        Dispatcher.UIThread.MainLoop(done.Token);

        var sound = Task.Run(Sound.Run);
        if (!sound.Wait(TimeSpan.FromSeconds(30))) Say("ses\tzaman asimi");

        File.WriteAllText(Path.Combine(Out, "sonuc.tsv"), Log.ToString().Replace("\r\n", "\n"));
        return code;
    }

    internal static void Say(string line)
    {
        Console.WriteLine(line);
        Log.Append(line).Append('\n');
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "VidShrink.App"))) dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}

internal readonly record struct Rgb(byte R, byte G, byte B)
{
    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";

    internal bool Near(Rgb other, int tolerance)
        => Math.Abs(R - other.R) <= tolerance && Math.Abs(G - other.G) <= tolerance && Math.Abs(B - other.B) <= tolerance;
}

internal sealed class Capture
{
    internal const int Width = 520;
    internal const int Height = 420;

    private readonly byte[] _raw;

    internal Capture(string name, byte[] raw, int exit)
    {
        Name = name;
        _raw = raw;
        Exit = exit;
    }

    internal string Name { get; }

    internal int Exit { get; }

    internal int Frames => _raw.Length / (Width * Height * 4);

    internal Rgb At(int frame, int x, int y)
    {
        var o = (frame * Width * Height + y * Width + x) * 4;
        return new Rgb(_raw[o + 2], _raw[o + 1], _raw[o]);
    }

    internal Rgb Last(int x, int y) => At(Frames - 1, x, y);
}

internal static class Probe
{
    internal static readonly Rgb Red = new(255, 0, 0);
    internal static readonly Rgb Lime = new(0, 255, 0);
    internal static readonly Rgb Blue = new(0, 0, 255);
    internal static readonly Rgb White = new(255, 255, 255);
    internal static readonly Rgb Ember = new(255, 0, 51);

    private static readonly PixelPoint Origin = new(100, 100);

    internal static async Task<int> Run()
    {
        var pattern = Pattern();
        pattern.Show();
        await Task.Delay(1200);
        var scaling = pattern.Screens.Primary?.Scaling ?? 0;
        Program.Say($"olcek\t{scaling.ToString(CultureInfo.InvariantCulture)}\tbirincil\t{pattern.Screens.Primary?.Bounds}");
        if (Math.Abs(scaling - 1) > 0.001)
        {
            Program.Say("olcek 1 degil, koordinatlar gecersiz");
            pattern.Close();
            return 2;
        }

        var baseline = await Grab("desen");
        Report(baseline, "desen", new[] { ("kirmizi", 140, 140, Red), ("yesil", 380, 140, Lime), ("mavi", 140, 300, Blue), ("beyaz", 380, 300, White) });

        await FrameCase(excluded: true);
        await FrameCase(excluded: false);
        var noFrame = await Grab("cerceve-yok");
        Report(noFrame, "cerceve-yok", FramePoints(Red, Lime));

        await RingCase();
        await MagnifierCase();

        pattern.Close();
        await Task.Delay(300);
        return 0;
    }

    private static Window Pattern()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("160,320"), RowDefinitions = new RowDefinitions("120,120") };
        void Add(IBrush brush, int row, int column)
        {
            var cell = new Border { Background = brush };
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }

        Add(new SolidColorBrush(Color.FromRgb(255, 0, 0)), 0, 0);
        Add(new SolidColorBrush(Color.FromRgb(0, 255, 0)), 0, 1);
        Add(new SolidColorBrush(Color.FromRgb(0, 0, 255)), 1, 0);
        Add(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 1, 1);

        return new Window
        {
            Title = "VidShrinkDesen",
            WindowDecorations = WindowDecorations.None,
            CanResize = false,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            Width = 480,
            Height = 240,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = Origin,
            Background = Brushes.Black,
            Content = grid
        };
    }

    private static readonly PixelRect FrameRegion = new(140, 130, 240, 180);

    private static (string, int, int, Rgb)[] FramePoints(Rgb left, Rgb top)
        => new[]
        {
            ("sol-kenar", 139, 160, left),
            ("ust-kenar", 320, 129, top),
            ("ic", 200, 160, Red)
        };

    private static async Task FrameCase(bool excluded)
    {
        var name = excluded ? "cerceve-affinity" : "cerceve-affinitysiz";
        var frame = new RecorderFrame { ExcludeFromCapture = excluded };
        frame.Place(FrameRegion);
        frame.Show();
        await Task.Delay(900);
        Program.Say($"{name}\tCaptureExcluded\t{frame.CaptureExcluded}\tkonum\t{frame.Position}\tboyut\t{frame.Bounds.Width}x{frame.Bounds.Height}");
        var capture = await Grab(name);
        frame.Close();
        await Task.Delay(500);
        Report(capture, name, excluded ? FramePoints(Red, Lime) : FramePoints(Ember, Ember));
    }

    private static readonly PixelPoint Click = new(180, 160);

    private static (string, int, int, Rgb)[] RingPoints(Rgb stroke)
        => new[]
        {
            ("sol-cizgi", Click.X - 22, Click.Y, stroke),
            ("ust-cizgi", Click.X, Click.Y - 22, stroke),
            ("merkez", Click.X, Click.Y, Red)
        };

    private static async Task RingCase()
    {
        var ring = new RecorderClickRing();
        ring.Place(Click);
        ring.Show();
        await Task.Delay(900);
        Program.Say($"halka\tkonum\t{ring.Position}\tboyut\t{ring.Bounds.Width}x{ring.Bounds.Height}");
        var shown = await Grab("halka");
        ring.Close();
        await Task.Delay(500);
        Report(shown, "halka", RingPoints(Ember));

        var none = await Grab("halka-yok");
        Report(none, "halka-yok", RingPoints(Red));

        var overlay = new RecorderInputOverlay();
        var capturing = Grab("halka-overlay", 2.5);
        await Task.Delay(1000);
        var stamp = Stopwatch.StartNew();
        overlay.Ring(Click);
        var visible = await WaitVisible(overlay);
        Program.Say($"halka-overlay\tRing cagrildi\tpencere gorunur\t{visible}\tgizlenme ms\t{stamp.ElapsedMilliseconds}");
        var path = await capturing;
        overlay.Hide();
        var hits = new List<int>();
        for (var f = 0; f < path.Frames; f++)
            if (path.At(f, Click.X - 22, Click.Y).Near(Ember, 40)) hits.Add(f);
        Program.Say($"halka-overlay\tkare\t{path.Frames}\tember kare\t{hits.Count}\tilk\t{(hits.Count > 0 ? hits[0] : -1)}\tson\t{(hits.Count > 0 ? hits[^1] : -1)}\tson kare sol-cizgi\t{(path.Frames > 0 ? path.Last(Click.X - 22, Click.Y) : default)}");
    }

    private static async Task<bool> WaitVisible(RecorderInputOverlay overlay)
    {
        var seen = false;
        for (var i = 0; i < 100; i++)
        {
            var window = overlay.RingWindow;
            if (window is not null && window.IsVisible) seen = true;
            else if (seen) return true;
            await Task.Delay(10);
        }

        return seen;
    }

    private static readonly PixelPoint Cursor = new(250, 160);

    private static async Task MagnifierCase()
    {
        var lens = new RecorderMagnifier();
        lens.Follow(Cursor);
        lens.Show();
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => lens.Follow(Cursor));
        timer.Start();
        await Task.Delay(900);
        Program.Say($"buyutec\tkaynak\t{lens.LastSource}\tLastGrab\t{lens.LastGrab}\tkonum\t{lens.Position}\tboyut\t{lens.Bounds.Width}x{lens.Bounds.Height}");
        var p = lens.Position;
        var shown = await Grab("buyutec");
        timer.Stop();
        lens.Close();
        await Task.Delay(500);
        Report(shown, "buyutec", new[]
        {
            ("kenar", p.X + 1, p.Y + 72, Ember),
            ("mercek-sol", p.X + 40, p.Y + 72, Red),
            ("mercek-sag", p.X + 130, p.Y + 72, Lime)
        });

        var none = await Grab("buyutec-yok");
        Report(none, "buyutec-yok", new[]
        {
            ("kenar", p.X + 1, p.Y + 72, White),
            ("mercek-sol", p.X + 40, p.Y + 72, White),
            ("mercek-sag", p.X + 130, p.Y + 72, White)
        });
    }

    private static void Report(Capture capture, string name, IEnumerable<(string Label, int X, int Y, Rgb Expected)> points)
    {
        foreach (var (label, x, y, expected) in points)
        {
            if (capture.Frames == 0)
            {
                Program.Say($"{name}\t{label}\t({x},{y})\tbeklenen\t{expected}\tokunan\tkare yok\tcikis\t{capture.Exit}");
                continue;
            }

            var read = capture.Last(x, y);
            Program.Say($"{name}\t{label}\t({x},{y})\tbeklenen\t{expected}\tokunan\t{read}\tuyar\t{read.Near(expected, 40)}\tkare\t{capture.Frames}");
        }
    }

    internal static async Task<Capture> Grab(string name, double seconds = 1.5)
    {
        var raw = Path.Combine(Program.Out, name + ".bgra");
        var png = Path.Combine(Program.Out, name + ".png");
        var err = Path.Combine(Program.Out, name + ".ffmpeg.txt");
        var info = new ProcessStartInfo("ffmpeg")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };
        var t = seconds.ToString(CultureInfo.InvariantCulture);
        foreach (var a in new[]
                 {
                     "-hide_banner", "-y", "-filter_complex",
                     $"gfxcapture=monitor_idx=0:capture_cursor=0:max_framerate=20,hwdownload,format=bgra,crop={Capture.Width}:{Capture.Height}:0:0,split[a][b]",
                     "-map", "[a]", "-t", t, "-f", "rawvideo", raw,
                     "-map", "[b]", "-t", t, "-update", "1", png
                 })
            info.ArgumentList.Add(a);

        using var process = Process.Start(info)!;
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var exited = await Task.Run(() => process.WaitForExit(15000));
        if (!exited)
        {
            try { process.Kill(true); } catch { }
            process.WaitForExit(5000);
        }

        await File.WriteAllTextAsync(err, (exited ? "" : "BEKCI: 15 sn asildi, oldurildu\n") + await stderr);
        await stdout;
        var exit = exited ? process.ExitCode : -999;
        var bytes = File.Exists(raw) ? await File.ReadAllBytesAsync(raw) : Array.Empty<byte>();
        var capture = new Capture(name, bytes, exit);
        Program.Say($"{name}\tyakalama\tcikis\t{exit}\tkare\t{capture.Frames}");
        return capture;
    }
}
