using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Playback;
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

        Warm();

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
                SettlePlan(window, "kucult");
            }),

            Shot(language, outDir, "donustur", window =>
            {
                Load(window);
                SelectTab(window, "main.tab.convert");
                SettlePlan(window, "donustur");
            }),

            Shot(language, outDir, "ayarlar", window => SelectTab(window, "main.tab.settings")),
            Shot(language, outDir, "gelismis", window => SelectTab(window, "main.tab.advanced")),
            Shot(language, outDir, "hakkinda", window => SelectTab(window, "main.tab.about")),

            Part(language, outDir, "onizleme", "Preview", window =>
            {
                LoadClip(window, clip);
                SelectTab(window, "main.tab.shrink");
                Relayout(window);
                FreezePreview(window);
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

    /// <summary>
    /// <see cref="Pump"/>'in bekleyen bicimi. Sure dolarsa cekim yarim kareyle surmez,
    /// durur: sure asimini yutan bir bekleme bos panel cizip cikis kodunu 0 birakiyordu.
    /// </summary>
    private static void Await(Func<bool> until, string what, int seconds = PanelWaitSeconds)
    {
        if (!Pump(until, seconds))
            throw new TimeoutException($"{what}: {seconds} saniyede gelmedi.");
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
    ///
    /// <para>Temizlik <b>secici</b>. Giris bicemi degeri <c>TransformOperations</c> olarak
    /// kuruyor (<c>Themes/Controls.axaml</c>, <c>^.enter</c>), kalici konum tasiyicilari ise
    /// kod arkasinda tutulan <see cref="TranslateTransform"/> ornekleri: karsilastirma
    /// panelinin ayiricisi (<c>ComparisonPanel.SeparatorGrip</c>) ile serit tutamagi ve
    /// kodlama imleci (<c>ControlStrip.Thumb</c>, <c>ControlStrip.EncodeCursor</c>)
    /// konumlarini bunlardan aliyor. Hepsini birden silmek ayiriciyi panonun soluna,
    /// x=0'a dusuruyordu; kare kullanicinin hic gormedigi bir durumu gosteriyordu.</para>
    /// </summary>
    private static void ClearEntrance(MainWindow window)
    {
        foreach (var node in window.GetVisualDescendants().OfType<Visual>())
        {
            if (node.RenderTransform is TranslateTransform) continue;
            node.RenderTransform = null;
        }
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

        Await(() => task.IsCompleted, "Oynatici klibi acamadi");

        task.GetAwaiter().GetResult();

        Await(() => player.GetVisualDescendants().OfType<Image>().Any(image => image.Source is not null),
            "Oynaticinin ilk karesi");
    }

    /// <summary>
    /// Sunum sayacinin degismeden gecmesi gereken yoklama sayisi. Yoklama araligi
    /// <see cref="Pump"/>'te 25 ms; kirk yoklama bir saniye eder ve bassiz kosumun
    /// yaklasik 5 Hz'lik sunum turundan rahatca uzundur.
    /// </summary>
    private const int PreviewIdleTicks = 40;

    /// <summary>
    /// Onizleme panelini kaynagin <b>terminal penceresinin son karesinde</b> dondurur.
    ///
    /// <para>Tur 2'nin yolu pencereyi <c>[0, 5)</c>'te sabit tuttugunu soyluyordu ve
    /// tutmuyordu. Olculdu (<c>.calisma/T189/oncesi/iz.txt</c>): donan kare bir kosumda
    /// <c>[5, 10)</c> penceresinin 9,967 sn'lik karesi, digerinde <c>[7, 12)</c>
    /// penceresinin 11,967 sn'lik karesi. Ilk kare gelene kadar gecen surede boru bir ya da
    /// iki pencere devrediyor; kacinci pencerede yakalandigi makinenin o anki yukune bagli
    /// ve <c>IsPlaying</c>'i sonradan <c>false</c> yapmak yalniz devri o noktada kesiyor.</para>
    ///
    /// <para>Belirlenimli olan nokta borunun <b>terminal penceresi</b>:
    /// <c>PanelHost.FreezesAtWindowEnd</c> dogruyken ilerlenecek pencere yoktur — ne devir
    /// ne on hazirlik. O pencereye devirleri bekleyerek degil, <c>LoadClipAsync</c> ile
    /// dogrudan gecilir: yurumeyi beklemek hem yavas hem kirilgan, bir kosumda 60 saniyede
    /// terminal pencereye varilamadi (<c>.calisma/T189/kosumlar/r3.stderr.txt</c>, atilan
    /// kol). Pencere kurulduktan sonra sunum sayaci duruncaya kadar beklenir; pencerenin son
    /// karesi halkanin en yenisidir, dusurulmez ve tuketici onu almadan halka bosalmaz.</para>
    ///
    /// <para>En sonda oynatma durdurulur ve denetim seridi elle yerlestirilir: serit
    /// <c>!IsPlaying</c> iken aciliyor, ama acilmasi bir zamanlayici gecikmesi ve 360 ms'lik
    /// opaklik gecisi uzerinden yuruyor. Kare bu gecisin ortasina dustugu icin serit bir
    /// kosumda kareye giriyor digerinde girmiyordu; gecis silinip opaklik dogrudan yaziliyor.</para>
    ///
    /// <para><c>Controls.IsPlaying</c> dogrudan yaziliyor, dugmeye basilmis gibi degil:
    /// <c>PanelHost.ApplyPlayState</c> boruyu da duraklatirdi.</para>
    /// </summary>
    private static void FreezePreview(MainWindow window)
    {
        var host = FieldValue(window, "_preview")
            ?? throw new InvalidOperationException("Onizleme barindiricisi kurulmamis.");

        var strip = Read(Named(window, "Preview"), "Controls");

        Await(() => Read(host, "SourceStatus") is ComparisonSourceStatus { State: ComparisonSourceState.Oynuyor },
            "Onizleme borusu");

        var load = (Task)Call(host, "LoadClipAsync", (double)ClipSeconds);
        Await(() => load.IsCompleted, "Terminal pencerenin kodlanmasi");
        load.GetAwaiter().GetResult();

        if (!(bool)Read(host, "FreezesAtWindowEnd"))
            throw new InvalidOperationException($"Terminal pencere kurulamadi: {Read(host, "ActiveClip")}");

        Await(() => Read(host, "SourceStatus") is ComparisonSourceStatus { State: ComparisonSourceState.Oynuyor },
            "Terminal pencerenin borusu");

        var last = -1L;
        var idle = 0;
        Await(() =>
        {
            var frames = (long)Read(host, "PresentedFrames");
            if (frames != last)
            {
                last = frames;
                idle = 0;
                return false;
            }

            return ++idle >= PreviewIdleTicks;
        }, "Terminal pencerenin son karesi");

        SetProperty(strip, "IsPlaying", false);
        SetField(host, "_generation", (int)FieldValue(host, "_generation")! + 1);
        DrainSurface(Read(Named(window, "Preview"), "Frames"));
        RevealStrip(strip);
        TracePreview(window, host, strip);
    }

    /// <summary>
    /// Yuzeyin kendi halkasini bosaltir.
    ///
    /// <para>Iki ayri sunum turu var: <c>PanelHost.Drain</c> kareyi
    /// <c>ComparisonSurface</c>'in halkasina <b>birakir</b>, yuzeyin kendi turu
    /// (<c>Round</c>) onu bitmap'e <b>cizer</b>. Barindiriciyi durdurmak yalniz birakmayi
    /// durduruyor; halkada cizilmemis kare kalirsa kareye giren goruntu son birakilan degil
    /// son <i>cizilen</i> oluyor ve kacinin cizildigi kuyrugun ne kadar suruldugune bagli.
    /// Olculdu: bes kosumun besinde de son birakilan kare 11,967 sn'ydi (iz satirlari ayni)
    /// ama ilk kosumun karesinde kaynagin kendi damgasi 11,167 sn goruyordu.</para>
    ///
    /// <para>Beklemek ise yaramiyor: yuzeyin turunu <c>RequestAnimationFrame</c> suruyor ve
    /// bassiz kosumda o dongu kendiliginden donmuyor (60 saniye beklendi, sayaclar hic
    /// artmadi). Tur bir kez elle cevriliyor — <c>Round</c> halkadaki butun kareleri bir
    /// seferde tuketip en yenisini ciziyor — ve halkanin bosaldigi dogrulaniyor.</para>
    /// </summary>
    private static void DrainSurface(object surface)
    {
        Call(surface, "Round", TimeSpan.Zero);

        var ring = FieldValue(surface, "_ring")!;
        var left = (int)Read(ring, "Count");
        if (left > 0) throw new InvalidOperationException($"Yuzeyin halkasi bosalmadi: {left} kare.");
    }

    /// <summary>
    /// Denetim seridini gecise birakmadan acar. Opaklik gecisi silinip deger dogrudan
    /// yazilir; boylece kare gecisin neresine denk gelirse gelsin serit ayni cikar.
    /// </summary>
    private static void RevealStrip(object strip)
    {
        var bar = (Control)FieldValue(strip, "Bar")!;
        bar.Transitions = null;
        bar.Opacity = 1;
    }

    /// <summary>
    /// Olcum izi: donmus karenin gercekte hangi kare oldugunu stderr'e yazar. K9 iki tur
    /// boyunca bu satir olmadigi icin yanlis anlatildi.
    /// </summary>
    private static void TracePreview(MainWindow window, object host, object strip)
    {
        var bar = (Control)FieldValue(strip, "Bar")!;
        Console.Error.WriteLine(
            $"iz	onizleme	pozisyon={Read(strip, "Position")}	sunulan={Read(host, "PresentedFrames")}"
            + $"	terminal={Read(host, "FreezesAtWindowEnd")}	seritGorunur={Read(strip, "IsRevealed")}"
            + $"	seritOpaklik={bar.Opacity}	yuzeyCizen={Read(Read(Named(window, "Preview"), "Frames"), "PresentedFrames")}"
            + $"	pencere={Read(host, "ActiveClip")}	durum={Read(host, "SourceStatus")}");
    }

    /// <summary>
    /// Yetenek onbellegini ilk kareden <b>once</b> isitir. <c>EncoderCapabilities</c>
    /// yoklamalarini <c>ProbeKillMs</c> ile kesiyor: sogukta yavas donen ilk ffmpeg cagrisi
    /// olduruluyor ve cevap <c>Unmeasured</c> kaliyor, isinmis cagri gercek cevabi veriyor.
    /// Ilk cizilen kare (<c>T189-kucult-en.png</c>) bu yarisi kaybedebilecek tek kareydi:
    /// denetcinin ilk kosumunda ayni kare %0,19 farkli, ikinciden besinciye kadar ayniydi.
    /// Isinma cagrisi donene kadar beklenir, cizim ondan sonra baslar.
    /// </summary>
    private static void Warm()
    {
        var warm = typeof(MainWindow).GetMethod(
            "WarmPsychovisualProbe",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new MissingMethodException(nameof(MainWindow), "WarmPsychovisualProbe");

        var clock = Stopwatch.StartNew();
        warm.Invoke(null, new object?[] { EncoderCapabilities.Instance });
        Console.Error.WriteLine($"iz	isinma	sure={clock.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Plan kartini <b>yerlestirir</b>: gecikmeli yeniden hesabi zorlar, sonra zamanlayiciyi
    /// durdurur ve kartin hangi cevapla cizildigini stderr'e yazar.
    ///
    /// <para>K9'un ucuncu kaynagi buydu. Hedef kutusuna yazmak plani hemen degil,
    /// <c>MainWindow.ScheduleRecalculate</c>'in 160 ms'lik <c>DispatcherTimer</c>'i
    /// uzerinden yeniliyor; bassiz kosumda o zamanlayici ancak kuyruk suruldugunde ilerliyor
    /// ve cizime kadar surulen sure makinenin yukune bagli. Zamanlayici cizimden once
    /// tiklarsa kart yeni hedefin planini, tiklamazsa bir onceki plani gosteriyordu — ayni
    /// ikilinin ilk kosumunda <c>T189-kucult-en.png</c> %0,19 boyle farkliydi. Hesap
    /// dogrudan kosturulunca iki kol da ayni sonuca varir ve zamanlayicinin tiklamasi
    /// kareye giren hicbir seyi degistirmez.</para>
    ///
    /// <para>Bassiz kosumda <c>MainWindow.ProbeHardwareEncodersAsync</c> hic calismiyor —
    /// <c>_hardwareProbed</c> her kosumda <c>false</c>, <c>_planEncoders</c> geciti her
    /// kosumda yok — yani kart yalniz <see cref="Warm"/>'in isittigi onbellege bakiyor.</para>
    /// </summary>
    private static void SettlePlan(MainWindow window, string topic)
    {
        Invoke(window, "RecalculateForTest");
        (FieldValue(window, "_recalculateTimer") as DispatcherTimer)?.Stop();
        Console.Error.WriteLine(
            $"iz	plan	kare={topic}	donanimKarari={FieldValue(window, "_hardwareProbed")}"
            + $"	gecit={(FieldValue(window, "_planEncoders") is null ? "yok" : "var")}"
            + $"	yoklamaSayaci={Read(window, "PlanProbeCount")}	yerlesmedi={Read(window, "PlanProbeUnsettled")}");
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

    private static FieldInfo FieldOf(object target, string name)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
           ?? throw new MissingFieldException(target.GetType().Name, name);

    private static object? FieldValue(object target, string name) => FieldOf(target, name).GetValue(target);

    private static void SetField(object target, string name, object value)
        => FieldOf(target, name).SetValue(target, value);

    private static PropertyInfo PropertyOf(object target, string name)
        => target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
           ?? throw new MissingMemberException(target.GetType().Name, name);

    private static object Read(object target, string property) => PropertyOf(target, property).GetValue(target)!;

    private static void SetProperty(object target, string property, object value)
        => PropertyOf(target, property).SetValue(target, value);

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
