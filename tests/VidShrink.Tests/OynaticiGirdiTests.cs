using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Ffmpeg.Playback;
using Xunit;

namespace VidShrink.Tests;

internal static class GirdiKanit
{
    internal static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VidShrink.sln")))
                dir = dir.Parent;
            return dir?.FullName ?? AppContext.BaseDirectory;
        }
    }

    internal static string Folder
    {
        get
        {
            var path = Path.Combine(Root, ".calisma", "T176");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
        => File.WriteAllText(Path.Combine(Folder, name), body, new UTF8Encoding(false));
}

internal static class GirdiSurucu
{
    private static readonly Pointer Fare = new(1, PointerType.Mouse, true);

    internal static void Wheel(PlayerView view, double notches, KeyModifiers modifiers)
    {
        var args = new PointerWheelEventArgs(
            view,
            Fare,
            view,
            new Point(1, 1),
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
            modifiers,
            new Vector(0, notches))
        {
            RoutedEvent = InputElement.PointerWheelChangedEvent
        };

        view.RaiseEvent(args);
    }

    internal static void Press(PlayerView view, PointerUpdateKind kind, RawInputModifiers buttons)
    {
        var args = new PointerPressedEventArgs(
            view,
            Fare,
            view,
            new Point(1, 1),
            0,
            new PointerPointProperties(buttons, kind),
            KeyModifiers.None)
        {
            RoutedEvent = InputElement.PointerPressedEvent
        };

        view.RaiseEvent(args);
    }

    internal static void Key(PlayerView view, Key key)
        => view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    internal static PlayerView Kur(out Window window)
    {
        var view = new PlayerView();
        var host = new Window { Width = 640, Height = 480, Content = view };
        window = host;
        return view;
    }
}

public sealed class OynaticiGirdiTests
{
    [Fact]
    public void DokuzGirdininDokuzuIcinOncesiSonrasiOlculur()
    {
        var satirlar = AppHost.Run(() =>
        {
            var view = GirdiSurucu.Kur(out var window);
            view.Seek.Duration = 100000;
            var rows = new List<string>();

            void Tekerlek(string ad, KeyModifiers mods)
            {
                var before = view.PositionSeconds;
                GirdiSurucu.Wheel(view, 1, mods);
                rows.Add($"{ad}\tkonum {before:0.###} -> {view.PositionSeconds:0.###} sn");
            }

            Tekerlek("tekerlek", KeyModifiers.None);
            Tekerlek("ctrl+tekerlek", KeyModifiers.Control);
            Tekerlek("shift+tekerlek", KeyModifiers.Shift);
            Tekerlek("ctrl+shift+tekerlek", KeyModifiers.Control | KeyModifiers.Shift);

            var zoomBefore = view.ZoomScale;
            var posBeforeZoom = view.PositionSeconds;
            GirdiSurucu.Wheel(view, 1, KeyModifiers.Alt);
            rows.Add(FormattableString.Invariant($"alt+tekerlek\tyakinlastirma {zoomBefore:0.###} -> {view.ZoomScale:0.###} (konum {posBeforeZoom:0.###} -> {view.PositionSeconds:0.###} sn)"));

            var playBefore = view.IsPlaying;
            GirdiSurucu.Press(view, PointerUpdateKind.RightButtonPressed, RawInputModifiers.RightMouseButton);
            rows.Add($"sag tik\toynatma {playBefore} -> {view.IsPlaying}");

            var playBefore2 = view.IsPlaying;
            GirdiSurucu.Key(view, Avalonia.Input.Key.Space);
            rows.Add($"bosluk\toynatma {playBefore2} -> {view.IsPlaying}");

            var fsBefore = view.Fullscreen.IsFullscreen;
            GirdiSurucu.Press(view, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
            rows.Add($"orta tik\ttam ekran {fsBefore} -> {view.Fullscreen.IsFullscreen}");

            var snapshot = FormattableString.Invariant($"{view.PositionSeconds:0.###}|{view.ZoomScale:0.###}|{view.IsPlaying}|{view.Fullscreen.IsFullscreen}");
            var clicksBefore = view.LeftClicks;
            GirdiSurucu.Press(view, PointerUpdateKind.LeftButtonPressed, RawInputModifiers.LeftMouseButton);
            var after = FormattableString.Invariant($"{view.PositionSeconds:0.###}|{view.ZoomScale:0.###}|{view.IsPlaying}|{view.Fullscreen.IsFullscreen}");
            rows.Add($"sol tik\tdurum {snapshot} -> {after} (tiklama sayaci {clicksBefore} -> {view.LeftClicks})");

            Assert.Equal(snapshot, after);
            window.Close();
            return rows;
        });

        GirdiKanit.Write("k1-izgara.txt", string.Join(Environment.NewLine, satirlar) + Environment.NewLine);

        Assert.Equal(9, satirlar.Count);
        Assert.Contains("konum 0 -> 1 sn", satirlar[0]);
        Assert.Contains("konum 1 -> 11 sn", satirlar[1]);
        Assert.Contains("konum 11 -> 71 sn", satirlar[2]);
        Assert.Contains("konum 71 -> 371 sn", satirlar[3]);
        Assert.Contains("yakinlastirma 1 -> 1.24", satirlar[4]);
        Assert.Contains("konum 371 -> 371 sn", satirlar[4]);
        Assert.Contains("oynatma False -> True", satirlar[5]);
        Assert.Contains("oynatma True -> False", satirlar[6]);
        Assert.Contains("tam ekran False -> True", satirlar[7]);
        Assert.Contains("tiklama sayaci 0 -> 1", satirlar[8]);
    }

    [Fact]
    public void TekerlekAdimlariHaritadakiDortSayidir()
    {
        Assert.Equal(1, PlayerInputMap.StepFor(PlayerModifiers.None));
        Assert.Equal(10, PlayerInputMap.StepFor(PlayerModifiers.Ctrl));
        Assert.Equal(60, PlayerInputMap.StepFor(PlayerModifiers.Shift));
        Assert.Equal(300, PlayerInputMap.StepFor(PlayerModifiers.Ctrl | PlayerModifiers.Shift));
        Assert.Equal(PlayerCommandKind.Zoom, PlayerInputMap.Wheel(1, PlayerModifiers.Alt).Kind);
        Assert.Equal(PlayerCommandKind.None, PlayerInputMap.Press(PlayerButton.Left).Kind);
        Assert.Equal(PlayerCommandKind.TogglePlay, PlayerInputMap.Press(PlayerButton.Right).Kind);
        Assert.Equal(PlayerCommandKind.ToggleFullscreen, PlayerInputMap.Press(PlayerButton.Middle).Kind);
        Assert.Equal(PlayerCommandKind.TogglePlay, PlayerInputMap.Key(PlayerKey.Space).Kind);
    }

    [Fact]
    public async Task OnHizliTikTekHedefteBirikirVeOnAramaAcmaz()
    {
        var calls = new List<double>();
        var gate = new SemaphoreSlim(0);
        var coalescer = new SeekCoalescer(async at =>
        {
            lock (calls) calls.Add(at);
            await gate.WaitAsync();
        })
        { Duration = 1000 };

        for (var i = 0; i < 10; i++) coalescer.Nudge(PlayerInputMap.WheelStepSeconds);

        Assert.Equal(10, coalescer.Target);
        lock (calls) Assert.Single(calls);

        gate.Release();
        await Task.Delay(50);
        gate.Release();
        await coalescer.Idle;

        var body = new StringBuilder();
        body.AppendLine("10 hizli tik, tik basi 1 sn");
        body.AppendLine($"hedef konum: {coalescer.Target} sn");
        body.AppendLine($"tetiklenen arama sayisi: {coalescer.SeekCalls}");
        body.AppendLine("arama hedefleri: " + string.Join(", ", coalescer.IssuedTargets));
        GirdiKanit.Write("k3-birikme.txt", body.ToString());

        Assert.Equal(10, coalescer.Target);
        Assert.True(coalescer.SeekCalls < 10, $"birikme yok: {coalescer.SeekCalls} arama");
        Assert.Equal(10, coalescer.IssuedTargets[^1]);
    }

    [Fact]
    public void OrtaTikTamEkranaGecerIkincisiOncekiHaleDoner()
    {
        var rapor = AppHost.Run(() =>
        {
            var view = new PlayerView();
            view.CurrentTabIndex = () => 2;
            view.PlayerTabIndex = () => 5;
            var selected = new List<int>();
            view.SelectTab = index => selected.Add(index);

            var before = new WindowSnapshot(0, 40, 60, 900, 700, 2);
            var toFull = view.Fullscreen.Toggle(before, (int)WindowState.FullScreen, 5);
            var back = view.Fullscreen.Toggle(toFull, (int)WindowState.FullScreen, 5);

            var body = new StringBuilder();
            body.AppendLine($"once      : durum {before.State} dikdortgen {before.X},{before.Y} {before.Width}x{before.Height} sekme {before.TabIndex}");
            body.AppendLine($"tam ekran : durum {toFull.State} dikdortgen {toFull.X},{toFull.Y} {toFull.Width}x{toFull.Height} sekme {toFull.TabIndex}");
            body.AppendLine($"geri donus: durum {back.State} dikdortgen {back.X},{back.Y} {back.Width}x{back.Height} sekme {back.TabIndex}");

            Assert.Equal((int)WindowState.FullScreen, toFull.State);
            Assert.Equal(5, toFull.TabIndex);
            Assert.Equal(before, back);

            var window = new Window { Width = 640, Height = 480, Content = view };
            view.CurrentTabIndex = () => 2;
            GirdiSurucu.Press(view, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
            var fsPos = view.PositionSeconds;
            GirdiSurucu.Wheel(view, 1, KeyModifiers.None);
            GirdiSurucu.Key(view, Avalonia.Input.Key.Space);
            body.AppendLine($"tam ekranda tekerlek: konum {fsPos:0.###} -> {view.PositionSeconds:0.###} sn, bosluk: oynatma {view.IsPlaying}");
            Assert.True(view.Fullscreen.IsFullscreen);
            Assert.Equal(fsPos + 1, view.PositionSeconds);
            Assert.True(view.IsPlaying);

            GirdiSurucu.Press(view, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
            body.AppendLine($"ikinci orta tik: tam ekran {view.Fullscreen.IsFullscreen}, secilen sekmeler {string.Join(",", selected)}");
            Assert.False(view.Fullscreen.IsFullscreen);

            window.Close();
            return body.ToString();
        });

        GirdiKanit.Write("k4-tam-ekran.txt", rapor);
        Assert.Contains("geri donus", rapor);
    }

    [Fact]
    public void MenuDugmesiBaglamMenusunuAcarVeUcSatirTasir()
    {
        var rapor = AppHost.Run(() =>
        {
            var view = new PlayerView();
            var window = new Window { Width = 640, Height = 480, Content = view };

            var body = new StringBuilder();
            foreach (var dil in new[] { "en", "tr" })
            {
                Strings.Use(dil);
                var menu = view.BuildMenu();
                var basliklar = menu.Items.OfType<MenuItem>().Select(item => item.Header?.ToString() ?? "").ToList();
                body.AppendLine($"{dil}: {string.Join(" | ", basliklar)}");
                Assert.Equal(3, basliklar.Count);
                Assert.All(basliklar, baslik => Assert.False(string.IsNullOrWhiteSpace(baslik)));
            }

            Strings.Use("en");
            view.Apply(PlayerInputMap.MenuButton());
            body.AppendLine($"menu acildi: {view.OpenedMenu is not null}");
            Assert.NotNull(view.OpenedMenu);

            window.Close();
            return body.ToString();
        });

        GirdiKanit.Write("k5-menu.txt", rapor);
    }

    [Fact]
    public void KabukYolununActigiSekmeOynaticidir()
    {
        var rapor = AppHost.Run(() =>
        {
            var klip = Path.Combine(GirdiKanit.Folder, "kabuk-ornek.mp4");
            if (!File.Exists(klip)) File.WriteAllBytes(klip, new byte[] { 0, 1, 2, 3 });

            var argv = new[] { klip };
            var cozulen = VidShrink.Core.ShellIntegration.ResolveStartupPath(argv);
            Assert.Equal(klip, cozulen);
            Assert.Null(Program.StartupFor(argv));

            var window = new MainWindow(cozulen);
            var oynaticiIndex = window.PlayerTabIndex;
            var basta = window.Tabs.SelectedIndex;
            var yukleme = window.LoadStartupFileAsync();
            var sonra = window.Tabs.SelectedIndex;
            var baslik = ((TabItem)window.Tabs.Items[oynaticiIndex]!).Header?.ToString();
            _ = yukleme.ContinueWith(_ => { }, TaskScheduler.Default);
            Assert.NotEqual(basta, sonra);
            Assert.Equal(oynaticiIndex, sonra);
            window.Close();

            return $"argv: {klip}{Environment.NewLine}"
                 + $"ResolveStartupPath: {cozulen}{Environment.NewLine}"
                 + $"oynatici sekme sirasi: {oynaticiIndex}{Environment.NewLine}"
                 + $"acilistaki sekme: {basta} -> {sonra}{Environment.NewLine}"
                 + $"sekme basligi: {baslik}{Environment.NewLine}";
        });

        GirdiKanit.Write("k6-kabuk.txt", rapor);
        Assert.Contains("sekme basligi:", rapor);
    }
}

public sealed class PlayerTabTests
{
    [Fact]
    public void OynaticiSekmesiSekmeSeridindeVeIkiDildeBasligiVar()
    {
        var rapor = AppHost.Run(() =>
        {
            var window = new MainWindow();
            var index = window.PlayerTabIndex;
            var tab = (TabItem)window.Tabs.Items[index]!;
            var body = new StringBuilder();
            body.AppendLine($"sekme sayisi: {window.Tabs.ItemCount}");
            body.AppendLine($"oynatici sirasi: {index}");

            foreach (var dil in new[] { "en", "tr" })
            {
                Strings.Use(dil);
                var beklenen = Strings.Get("main.tab.player");
                var goruldu = tab.Header?.ToString();
                body.AppendLine($"{dil}: beklenen '{beklenen}' goruldu '{goruldu}'");
                Assert.Equal(beklenen, goruldu);
            }

            Strings.Use("en");
            var digerleri = window.Tabs.Items.OfType<TabItem>().Select(item => item.Theme?.ToString() ?? "yok").Distinct().ToList();
            body.AppendLine("sekme temalari: " + string.Join(" | ", digerleri));
            Assert.Single(digerleri);
            Assert.IsType<PlayerView>(tab.Content);

            window.Close();
            return body.ToString();
        });

        GirdiKanit.Write("k1-sekme.txt", rapor);
        Assert.Contains("oynatici sirasi:", rapor);
    }
}

public sealed class GirdiKlipFixture : IAsyncLifetime
{
    public string? ClipPath { get; private set; }

    public async Task InitializeAsync()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var path = Path.Combine(GirdiKanit.Folder, "girdi-20sn.mkv");
        ClipPath = path;
        if (File.Exists(path)) return;

        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var arg in new[]
        {
            "-y", "-f", "lavfi", "-i", "testsrc=size=320x180:rate=30:duration=20",
            "-force_key_frames", "expr:gte(t,n_forced*1)",
            "-pix_fmt", "yuv420p", "-c:v", "libx264", "-preset", "ultrafast",
            "-an", path
        }) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        await process.WaitForExitAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public sealed class OynaticiGirdiTestsGercekBoru : IClassFixture<GirdiKlipFixture>
{
    private readonly GirdiKlipFixture _klip;

    public OynaticiGirdiTestsGercekBoru(GirdiKlipFixture klip) => _klip = klip;

    [FfmpegAvailableFact]
    public async Task OnHizliTikGercekBoruyaKarsiBirikirVeAramalarSinirdaKalir()
    {
        var path = _klip.ClipPath!;
        using var pipe = new DecoderPipe();
        await pipe.OpenAsync(path);

        var bosKare = 0;
        var coalescer = new SeekCoalescer(async at =>
        {
            var frame = await pipe.SeekAsync(at);
            if (frame is null) Interlocked.Increment(ref bosKare);
        })
        { Duration = pipe.DurationSeconds };

        var oncekiSurec = pipe.ProcessesStarted;
        var saat = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++) coalescer.Nudge(PlayerInputMap.WheelStepSeconds);
        await coalescer.Idle;
        saat.Stop();

        var body = new StringBuilder();
        body.AppendLine($"klip: {Path.GetFileName(path)} sure {pipe.DurationSeconds:0.###} sn");
        body.AppendLine($"10 hizli tik (tik basi 1 sn) -> hedef {coalescer.Target} sn, ulasilan konum {coalescer.Position} sn");
        body.AppendLine($"tetiklenen arama sayisi: {coalescer.SeekCalls}");
        body.AppendLine("arama hedefleri: " + string.Join(", ", coalescer.IssuedTargets));
        body.AppendLine("arama gecikmeleri (ms): " + string.Join(", ", coalescer.LatenciesMs.Select(ms => ms.ToString("0.#", CultureInfo.InvariantCulture))));
        body.AppendLine($"toplam sure: {saat.Elapsed.TotalMilliseconds:0.#} ms");
        body.AppendLine($"ffmpeg surec sayisi: {oncekiSurec} -> {pipe.ProcessesStarted}");
        body.AppendLine($"T175 150 ms sinirini asan arama: {coalescer.LatenciesMs.Count(ms => ms > 150)}");
        body.AppendLine($"null donen arama (T175 yeniden baslatma tavani): {bosKare}");
        body.AppendLine("arama hatalari: " + (coalescer.Failures.Count == 0 ? "yok" : string.Join(" | ", coalescer.Failures)));
        GirdiKanit.Write("k3-gercek-boru.txt", body.ToString());

        Assert.Equal(10, coalescer.Target);
        Assert.Equal(10, coalescer.Position);
        Assert.True(coalescer.SeekCalls < 10, $"birikme yok: {coalescer.SeekCalls} arama");
    }
}
