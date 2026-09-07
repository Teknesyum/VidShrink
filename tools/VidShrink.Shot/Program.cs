using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Shot;

/// <summary>
/// README gorsellerini ureten cekim duzenegi. Pencere masaustunde acilmaz: uygulama
/// Avalonia'nin bassiz platformunda kurulur, <see cref="MainWindow"/> olculup
/// yerlestirilir ve secilen gorsel <see cref="RenderTargetBitmap"/> uzerine cizilir.
/// Ayni sonuc her kosumda cikar; ekran cozunurluguna, pencere yoneticisine ve masaustu
/// olceklemesine bagli degildir.
/// </summary>
public static class Program
{
    private const int Width = 1600;
    private const int Height = 1000;
    private const string Contract = "T189";

    private static readonly Size Viewport = new(Width, Height);

    private static readonly string[] Languages = { "en", "tr" };

    public static int Main(string[] args)
    {
        var outDir = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.Combine(RepoRoot(), "docs", "gorseller");

        Directory.CreateDirectory(outDir);

        var given = args.Length > 1 ? Path.GetFullPath(args[1]) : null;
        if (given is not null && !File.Exists(given))
        {
            Console.Error.WriteLine($"Klip bulunamadi: {given}");
            return 1;
        }

        var clip = EnsureClip(given);
        Console.WriteLine($"klip\t{clip}");

        var written = new List<string>();
        foreach (var language in Languages)
            written.AddRange(Capture(language, outDir, clip));

        foreach (var path in written)
            Console.WriteLine($"{Path.GetFileName(path)}\t{new FileInfo(path).Length}");

        Console.WriteLine($"toplam\t{written.Count}");
        return written.Count == 0 ? 1 : 0;
    }

    /// <summary>Bir dilin butun kareleri. Her kare kendi penceresinde cekilir.</summary>
    private static IReadOnlyList<string> Capture(string language, string outDir, string clip)
    {
        var made = new List<string>
        {
            Shot(language, outDir, "kucult", window =>
            {
                Load(window);
                SetTarget(window, "24");
                SelectTab(window, "main.tab.shrink");
            }),

            Shot(language, outDir, "donustur", window =>
            {
                Load(window);
                SelectTab(window, "main.tab.convert");
            }),

            Shot(language, outDir, "ayarlar", window => SelectTab(window, "main.tab.settings")),
            Shot(language, outDir, "gelismis", window => SelectTab(window, "main.tab.advanced")),
            Shot(language, outDir, "hakkinda", window => SelectTab(window, "main.tab.about")),

            Part(language, outDir, "onizleme", "Preview", window =>
            {
                LoadClip(window, clip);
                SelectTab(window, "main.tab.shrink");
                Relayout(window);
                Pump(() => Named(window, "Preview").GetVisualDescendants()
                    .OfType<Image>()
                    .Any(image => image.Source is not null), PanelWaitSeconds);
            }),

            Shot(language, outDir, "oynatici", window =>
            {
                SelectTab(window, "main.tab.player");
                Relayout(window);
                OpenInPlayer(window, clip);
            })
        };

        return made;
    }

    /// <summary>Panelin ilk karesini beklerken verilen ust sinir.</summary>
    private const int PanelWaitSeconds = 60;

    /// <summary>
    /// Arayuz is parcacigini elle surer: bassiz kosumda kuyrugu kimse bosaltmiyor, dolayisiyla
    /// kod cozme ve kodlama borularindan donen isler kendiliginden ilerlemiyor.
    /// </summary>
    private static bool Pump(Func<bool> until, int seconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (until()) return true;
            Thread.Sleep(25);
        }

        return false;
    }

    /// <summary>Tum pencereyi cizer.</summary>
    private static string Shot(string language, string outDir, string topic, Action<MainWindow> arrange)
        => Draw(language, outDir, topic, arrange, window => (Visual)window.GetVisualChildren().Single());

    /// <summary>Pencerenin tek bir adlandirilmis parcasini kendi olcusunde cizer.</summary>
    private static string Part(string language, string outDir, string topic, string name, Action<MainWindow> arrange)
        => Draw(language, outDir, topic, arrange, window => Named(window, name));

    /// <summary>
    /// Pencereyi kurar, istenen hale getirir ve secilen gorseli dosyaya cizer.
    ///
    /// <para>Pencere once <b>karsi</b> dilde kurulup sonra istenen dile geciriliyor:
    /// <c>Strings.Changed</c> yalniz deger degisince atesleniyor ve karsilastirma paneli
    /// metnini yalniz o olayda tazeliyor. Dogrudan hedef dilde kurulursa panel Ingilizce
    /// kalir.</para>
    /// </summary>
    private static string Draw(
        string language,
        string outDir,
        string topic,
        Action<MainWindow> arrange,
        Func<MainWindow, Visual> pick)
    {
        var path = Path.Combine(outDir, $"{Contract}-{topic}-{language}.png");

        Host.Run(() =>
        {
            Strings.Use(language == "tr" ? "en" : "tr");

            var window = new MainWindow { Width = double.NaN, Height = double.NaN };
            try
            {
                Invoke(window, "UseLanguage", language);

                LayOut(window);
                arrange(window);
                Settle(window);
                Relayout(window);
                ClearEntrance(window);

                var target = pick(window);
                var bounds = target.Bounds;
                var w = (int)Math.Round(bounds.Width);
                var h = (int)Math.Round(bounds.Height);
                if (w <= 0 || h <= 0)
                    throw new InvalidOperationException($"{topic}/{language}: cizilecek alan {w}x{h}.");

                using var bitmap = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
                bitmap.Render(target);
                bitmap.Save(path);
            }
            finally
            {
                window.Close();
            }
        });

        return path;
    }

    /// <summary>
    /// Yerlesim pencereye degil kok gorsel cocuguna verilir: <c>Window.ArrangeSetBounds</c>
    /// verilen olcuyu degil <c>ClientSize</c>'i dondurur, bassiz kosumda o da sifirdir.
    /// </summary>
    private static void LayOut(MainWindow window)
    {
        window.Width = double.NaN;
        window.Height = double.NaN;

        window.Measure(Viewport);
        window.Arrange(new Rect(Viewport));
        window.UpdateLayout();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.Measure(Viewport);
        root.Arrange(new Rect(Viewport));
    }

    /// <summary>
    /// Sekme secimi degistikten sonraki gecis. Secili olmayan sekmenin icerigi ilk kez
    /// burada olculur; agac gecersizlestirilip yalniz kok surulur.
    /// </summary>
    private static void Relayout(MainWindow window)
    {
        foreach (var node in window.GetVisualDescendants().OfType<Layoutable>()) node.InvalidateMeasure();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.InvalidateMeasure();
        root.Measure(Viewport);
        root.Arrange(new Rect(Viewport));
    }

    /// <summary>
    /// Acilis gecisleri masaustunde saniyenin ucte biri surer ve bassiz kosumda hic
    /// bitmez: giris siniflari panelleri saydam birakir, yavaslama posta kuyrugunda
    /// bekler, solan denetimler gizlenmez. Cekim kullanicinin yarim saniye sonra gordugu
    /// yerlesimi istiyor, o yuzden ucu de burada elle oturtulur.
    /// </summary>
    private static void Settle(MainWindow window)
    {
        foreach (var node in window.GetVisualDescendants().OfType<Control>())
            node.Transitions = null;

        foreach (var panel in (Control[])Call(window, "EntrancePanels"))
        {
            panel.Transitions = null;
            panel.Classes.Remove("enter");
            panel.Classes.Remove("enter-flat");
            panel.Opacity = 1;
        }

        foreach (var node in window.GetVisualDescendants().OfType<Control>())
        {
            node.Classes.Remove("enter");
            node.Classes.Remove("enter-flat");
        }

        Dispatcher.UIThread.RunJobs();
        Invoke(window, "SettleFades");
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Giris canlandirmasi panellere <c>translateY</c> uyguluyor ve bassiz kosumda geri
    /// alinmiyor; her blok on piksel asagida cizilirdi.
    /// </summary>
    private static void ClearEntrance(MainWindow window)
    {
        foreach (var node in window.GetVisualDescendants().OfType<Visual>())
            node.RenderTransform = null;
    }

    /// <summary>
    /// Sekmeyi baslik metninden secer. Oynatici sekmesinin basligi metin degil bir
    /// yerlesim oldugu icin o sekme kendi <c>x:Name</c>'inden bulunur.
    ///
    /// <para>Sekme icerigi bir <see cref="TransitioningContentControl"/> icinde degisiyor
    /// ve gecis bassiz kosumda hic bitmiyor: eski sekme yeninin altinda cizili kaliyordu.
    /// Gecis o yuzden secimden once bosaltilir.</para>
    /// </summary>
    private static void SelectTab(MainWindow window, string headerKey)
    {
        var tabs = (TabControl)Named(window, "Tabs");

        foreach (var host in tabs.GetVisualDescendants().OfType<TransitioningContentControl>())
            host.PageTransition = null;

        if (headerKey == "main.tab.player")
        {
            tabs.SelectedItem = Named(window, "TabPlayer");
            return;
        }

        var wanted = Strings.Get(headerKey);
        for (var index = 0; index < tabs.ItemCount; index++)
        {
            var header = (tabs.ContainerFromIndex(index) as TabItem)?.Header?.ToString();
            if (!string.Equals(header, wanted, StringComparison.Ordinal)) continue;
            tabs.SelectedIndex = index;
            return;
        }

        throw new InvalidOperationException($"Sekme bulunamadi: {headerKey} ({wanted}).");
    }

    private static void SetTarget(MainWindow window, string megabytes)
        => ((TextBox)Named(window, "TxtTarget")).Text = megabytes;

    /// <summary>
    /// Oynatici sekmesindeki kareyi uretim yolundan alir: <c>PlayerView.OpenAsync</c> ayni
    /// ffmpeg borusunu acar, ilk kareyi cozer ve <c>Frame</c> gorseline yazar. Bekleme
    /// suresince kuyruk elle surulur; bassiz kosumda arayuz is parcacigini kimse surmuyor.
    /// Ilk kare acilisin kendisinden degil, onun ardindan gelen sar isleminden dogar; o
    /// yuzden goruntu kaynagi dolana kadar ikinci bir bekleme var.
    /// </summary>
    private static void OpenInPlayer(MainWindow window, string clip)
    {
        var player = Named(window, "Player");
        var open = player.GetType().GetMethod(
            "OpenAsync",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new MissingMethodException("PlayerView", "OpenAsync");

        var task = (Task)open.Invoke(player, new object?[] { clip, CancellationToken.None })!;

        if (!Pump(() => task.IsCompleted, PanelWaitSeconds))
            throw new TimeoutException($"Oynatici klibi {PanelWaitSeconds} saniyede acamadi.");

        task.GetAwaiter().GetResult();

        Pump(() => player.GetVisualDescendants().OfType<Image>().Any(image => image.Source is not null),
            PanelWaitSeconds);
    }

    /// <summary>
    /// Kaynak yoklamadan yuklenir: kucultme kareleri diskteki hicbir dosyaya ve ffmpeg'e
    /// bagli degil, dolayisiyla gorsellerdeki sayilar her makinede ayni.
    /// </summary>
    private static void Load(MainWindow window)
        => Invoke(window, "LoadWithoutProbing", SamplePath, Sample());

    /// <summary>
    /// Gercek dosya yukler. Karsilastirma paneli kaynagi diskte bulamazsa kendini kapatiyor
    /// (<c>PanelHost.SetFiles</c>), dolayisiyla onizleme karesi ancak var olan bir dosyayla
    /// cekilebilir.
    /// </summary>
    private static void LoadClip(MainWindow window, string clip)
        => Invoke(window, "LoadWithoutProbing", clip, ClipInfo(clip));

    private const int ClipSeconds = 12;

    /// <summary>Uretilen klibin bilgisi; degerler klibi ureten ffmpeg cagrisiyla ayni.</summary>
    private static MediaInfo ClipInfo(string clip)
    {
        var bytes = new FileInfo(clip).Length;
        return new MediaInfo
        {
            FilePath = clip,
            FileSizeBytes = bytes,
            DurationSeconds = ClipSeconds,
            Width = 1280,
            Height = 720,
            Fps = 30,
            VideoCodec = "h264",
            TotalBitrateBps = bytes * 8 / ClipSeconds,
            AudioCodec = "aac",
            AudioBitrateBps = 128_000,
            AudioChannels = 1,
            PixelFormat = "yuv420p"
        };
    }

    /// <summary>
    /// Cekimlerin oynatilabilir kaynagi. Disaridan bir yol verilmezse ffmpeg'in kendi sinama
    /// deseninden uretilir: kare her makinede ayni cikar ve depoya video girmez.
    /// </summary>
    private static string EnsureClip(string? given)
    {
        if (given is not null) return given;

        var path = Path.Combine(RepoRoot(), ".calisma", "T189", "klip.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) return path;

        var duration = ClipSeconds.ToString(CultureInfo.InvariantCulture);
        var start = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (var argument in new[]
        {
            "-y",
            "-f", "lavfi", "-i", $"testsrc2=size=1280x720:rate=30:duration={duration}",
            "-f", "lavfi", "-i", $"sine=frequency=440:duration={duration}",
            "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-g", "30",
            "-c:a", "aac", "-b:a", "128k",
            path
        }) start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("ffmpeg baslatilamadi.");
        process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 || !File.Exists(path))
            throw new InvalidOperationException($"Klip uretilemedi, ffmpeg cikis kodu {process.ExitCode}.");

        return path;
    }

    private const string SamplePath = @"C:\Kayitlar\tatil-cekimi-2160p60.mkv";

    private static MediaInfo Sample() => new()
    {
        FilePath = SamplePath,
        FileSizeBytes = 420L * 1024 * 1024,
        DurationSeconds = 187.5,
        Width = 3840,
        Height = 2160,
        Fps = 59.94,
        VideoCodec = "hevc",
        TotalBitrateBps = 18_800_000,
        AudioCodec = "aac",
        AudioBitrateBps = 192_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    /// <summary>
    /// <see cref="MainWindow"/> yukleme, dil ve solma yardimcilari <c>internal</c> ya da
    /// <c>private</c>; gorunurluk yalniz VidShrink.Tests'e acik. Uretim kodunu bu arac
    /// icin genisletmemek adina yansimayla cagriliyor.
    /// </summary>
    private static void Invoke(object target, string method, params object?[] arguments)
        => Call(target, method, arguments);

    private static object Call(object target, string method, params object?[] arguments)
    {
        var member = target.GetType().GetMethod(
            method,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new MissingMethodException(target.GetType().Name, method);

        return member.Invoke(target, arguments)!;
    }

    private static Visual Named(MainWindow window, string name)
    {
        var field = typeof(MainWindow).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new MissingFieldException(nameof(MainWindow), name);

        return (Visual)(field.GetValue(window) ?? throw new InvalidOperationException($"{name} bos."));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VidShrink.sln")))
            dir = dir.Parent;

        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}

/// <summary>
/// Avalonia'yi kendi is parcaciginda bir kez kurar ve cekimleri oraya gonderir. Kurulum
/// cagiran is parcacigini arayuz is parcacigi ilan ettigi icin butun cizim ayni yerden
/// kosmak zorunda.
/// </summary>
internal static class Host
{
    private static readonly BlockingCollection<Action> Queue = new();
    private static Thread? _thread;

    private static void Ensure()
    {
        if (_thread is not null) return;

        var started = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            AppBuilder.Configure<VidShrink.App.App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .SetupWithoutStarting();

            started.Set();

            foreach (var work in Queue.GetConsumingEnumerable()) work();
        })
        {
            IsBackground = true,
            Name = "vidshrink-shot"
        };

        if (OperatingSystem.IsWindows()) thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        started.Wait();

        _thread = thread;
    }

    internal static void Run(Action work)
    {
        Ensure();

        var done = new ManualResetEventSlim();
        ExceptionDispatchInfo? failure = null;

        Queue.Add(() =>
        {
            try { work(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
            finally { done.Set(); }
        });

        done.Wait();
        failure?.Throw();
    }
}
