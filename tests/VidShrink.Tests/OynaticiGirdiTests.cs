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
            body.AppendLine($"menu izi: {view.Trace[^1]}");
            Assert.Equal("menu", view.Trace[^1]);

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
            var baslik = MainWindow.TabHeaderText((TabItem)window.Tabs.Items[oynaticiIndex]!);
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
                var goruldu = MainWindow.TabHeaderText(tab);
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
    public const int UretimTavaniMs = 60_000;

    public string? ClipPath { get; private set; }

    public async Task InitializeAsync()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var path = Path.Combine(GirdiKanit.Folder, "girdi-20sn.mkv");
        ClipPath = path;
        if (File.Exists(path)) return;

        await UretAsync(path, UretimTavaniMs);
    }

    public static async Task<int> UretAsync(string path, int timeoutMs)
    {
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in new[]
        {
            "-y", "-f", "lavfi", "-i", "testsrc=size=320x180:rate=30:duration=20",
            "-force_key_frames", "expr:gte(t,n_forced*1)",
            "-pix_fmt", "yuv420p", "-c:v", "libx264", "-preset", "ultrafast",
            "-an", path
        }) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException(
                $"klip uretimi {timeoutMs} ms icinde bitmedi: {Path.GetFileName(path)}");
        }

        await stdout;
        return (await stderr).Length;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public sealed class OynaticiGirdiTestsKlipUretimi
{
    [FfmpegAvailableFact]
    public async Task Klip_uretimi_stderr_borusu_dolsa_da_zamaninda_biter()
    {
        var yol = Path.Combine(GirdiKanit.Folder, "kalip-kanit-20sn.mkv");
        if (File.Exists(yol)) File.Delete(yol);

        var saat = Stopwatch.StartNew();
        var stderrBayt = await GirdiKlipFixture.UretAsync(yol, 30_000);
        saat.Stop();

        var body = new StringBuilder();
        body.AppendLine($"klip: {Path.GetFileName(yol)}");
        body.AppendLine($"uretim suresi: {saat.Elapsed.TotalMilliseconds:0.#} ms");
        body.AppendLine($"stderr bayt sayisi: {stderrBayt}");
        body.AppendLine("windows anonim boru tamponu: 4096 bayt");
        GirdiKanit.Write("k4-klip-uretimi.txt", body.ToString());

        Assert.True(File.Exists(yol), "klip uretilmedi");
        Assert.True(
            stderrBayt > 4096,
            $"stderr yalnizca {stderrBayt} bayt; boru tamponu dolmadan bu test kusuru yakalayamaz");
        Assert.True(
            saat.Elapsed.TotalSeconds < 30,
            $"klip uretimi {saat.Elapsed.TotalSeconds:0.#} sn surdu");
    }
}

public sealed class FfmpegAvailableFactAttribute : FactAttribute
{
    public FfmpegAvailableFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
            Skip = $"{missing} bulunamadi, ffmpeg isteyen test atlandi.";
    }
}

public sealed class OynaticiGirdiTestsMenuSatirlari
{
    private static MenuItem Satir(PlayerView view, int sira)
        => view.BuildMenu().Items.OfType<MenuItem>().ElementAt(sira);

    private static void Tikla(MenuItem item)
        => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = item });

    [Fact]
    public void MenuSatiriOynatDuraklatOynatmayiCevirir()
    {
        var rapor = AppHost.Run(() =>
        {
            var view = new PlayerView();
            var window = new Window { Width = 640, Height = 480, Content = view };

            var once = view.IsPlaying;
            var item = Satir(view, 0);
            Tikla(item);
            var sonra = view.IsPlaying;
            Tikla(Satir(view, 0));
            var geri = view.IsPlaying;

            var body = $"satir 0 basligi: {item.Header}{Environment.NewLine}"
                     + $"oynatma {once} -> {sonra} -> {geri}{Environment.NewLine}"
                     + $"iz: {string.Join(" | ", view.Trace)}{Environment.NewLine}";

            Assert.False(once);
            Assert.True(sonra);
            Assert.False(geri);
            Assert.Equal(new[] { "play -> True", "play -> False" }, view.Trace);

            window.Close();
            return body;
        });

        GirdiKanit.Write("k9-menu-oynat.txt", rapor);
    }

    [Fact]
    public void MenuSatiriTamEkranIkiYondeCalisir()
    {
        var rapor = AppHost.Run(() =>
        {
            var view = new PlayerView();
            view.CurrentTabIndex = () => 2;
            view.PlayerTabIndex = () => 5;
            var secilen = new List<int>();
            view.SelectTab = index => secilen.Add(index);
            var window = new Window { Width = 640, Height = 480, Content = view };

            var once = view.Fullscreen.IsFullscreen;
            var item = Satir(view, 1);
            Tikla(item);
            var acik = view.Fullscreen.IsFullscreen;
            Tikla(Satir(view, 1));
            var kapali = view.Fullscreen.IsFullscreen;

            var body = $"satir 1 basligi: {item.Header}{Environment.NewLine}"
                     + $"tam ekran {once} -> {acik} -> {kapali}{Environment.NewLine}"
                     + $"secilen sekmeler: {string.Join(",", secilen)}{Environment.NewLine}"
                     + $"iz: {string.Join(" | ", view.Trace)}{Environment.NewLine}";

            Assert.False(once);
            Assert.True(acik);
            Assert.False(kapali);
            Assert.Equal(new[] { 5, 2 }, secilen);

            window.Close();
            return body;
        });

        GirdiKanit.Write("k9-menu-tam-ekran.txt", rapor);
    }

    [Fact]
    public void MenuSatiriYakinlastirmayiSifirlar()
    {
        var rapor = AppHost.Run(() =>
        {
            var view = new PlayerView();
            var window = new Window { Width = 640, Height = 480, Content = view };

            GirdiSurucu.Wheel(view, 3, KeyModifiers.Alt);
            var buyutulmus = view.ZoomScale;
            var item = Satir(view, 2);
            Tikla(item);
            var sifirlanmis = view.ZoomScale;

            var body = FormattableString.Invariant(
                $"satir 2 basligi: {item.Header}{Environment.NewLine}yakinlastirma 1 -> {buyutulmus:0.###} -> {sifirlanmis:0.###}{Environment.NewLine}iz: {string.Join(" | ", view.Trace)}{Environment.NewLine}");

            Assert.True(buyutulmus > 1, $"alt+tekerlek buyutmedi: {buyutulmus}");
            Assert.Equal(1, sifirlanmis);
            Assert.Equal("zoomreset -> 1", view.Trace[^1]);

            window.Close();
            return body;
        });

        GirdiKanit.Write("k9-menu-sifirla.txt", rapor);
    }

    [Fact]
    public void UcMenuSatirininUcuDeAyriBirEtkiUretir()
    {
        var rapor = AppHost.Run(() =>
        {
            var view = new PlayerView();
            var window = new Window { Width = 640, Height = 480, Content = view };
            var menu = view.BuildMenu();
            var satirlar = menu.Items.OfType<MenuItem>().ToList();
            var body = new StringBuilder();

            foreach (var (item, sira) in satirlar.Select((item, sira) => (item, sira)))
            {
                var oncekiIz = view.Trace.Count;
                Tikla(item);
                var uretilen = view.Trace.Skip(oncekiIz).ToList();
                body.AppendLine($"satir {sira} '{item.Header}' -> {(uretilen.Count == 0 ? "ETKI YOK" : string.Join(" | ", uretilen))}");
                Assert.NotEmpty(uretilen);
                Assert.DoesNotContain("none", uretilen);
            }

            body.AppendLine($"satir sayisi: {satirlar.Count}");
            Assert.Equal(3, satirlar.Count);

            window.Close();
            return body.ToString();
        });

        GirdiKanit.Write("k9-menu-uc-satir.txt", rapor);
        Assert.Contains("satir sayisi: 3", rapor);
    }

    [Fact]
    public void OlcumKancasiUrunIkilisindeDerlenmez()
    {
        var yontem = typeof(PlayerView).GetMethod(
            "Echo",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!;
        var kosullar = yontem.GetCustomAttributes(typeof(ConditionalAttribute), false)
            .Cast<ConditionalAttribute>()
            .Select(a => a.ConditionString)
            .ToList();

        var alanlar = typeof(PlayerView).GetProperties(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            .Select(p => p.Name)
            .ToList();

        GirdiKanit.Write(
            "k13-olcum-kancasi.txt",
            $"PlayerView.Echo kosullari: {string.Join(", ", kosullar)}{Environment.NewLine}"
            + $"OpenedMenu ozelligi duruyor mu: {alanlar.Contains("OpenedMenu")}{Environment.NewLine}");

        Assert.Equal(new[] { "DEBUG" }, kosullar);
        Assert.DoesNotContain("OpenedMenu", alanlar);
    }
}

public sealed class OynaticiGirdiTestsPencereYazma
{
    [Fact]
    public void TamEkranGercekPencereDikdortgeniniGeriYazar()
    {
        var rapor = AppHost.Run(() =>
        {
            var view = new PlayerView();
            var window = new Window { Width = 900, Height = 700, Content = view };
            window.Show();
            window.Position = new PixelPoint(140, 90);
            window.Width = 900;
            window.Height = 700;

            string Oku(string ad) => FormattableString.Invariant(
                $"{ad}: durum {(int)window.WindowState} dikdortgen {window.Position.X},{window.Position.Y} {window.Width:0.###}x{window.Height:0.###}");

            var body = new StringBuilder();
            body.AppendLine(Oku("once     "));
            var once = ((int)window.WindowState, window.Position.X, window.Position.Y, window.Width, window.Height);

            GirdiSurucu.Press(view, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
            body.AppendLine(Oku("tam ekran"));
            var tamEkranDurumu = (int)window.WindowState;

            window.WindowState = WindowState.Normal;
            window.Position = new PixelPoint(500, 480);
            window.Width = 400;
            window.Height = 300;
            body.AppendLine(Oku("kaydirildi"));

            GirdiSurucu.Press(view, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
            body.AppendLine(Oku("geri     "));
            var geri = ((int)window.WindowState, window.Position.X, window.Position.Y, window.Width, window.Height);
            body.AppendLine($"arka uc: {AppHost.Backend}");

            Assert.Equal((int)WindowState.FullScreen, tamEkranDurumu);
            Assert.Equal(once, geri);

            window.Close();
            return body.ToString();
        });

        GirdiKanit.Write("k16-pencere-geri-yazma.txt", rapor);
        Assert.Contains("geri     ", rapor);
    }
}

public sealed class OynaticiGirdiTestsKabukHatasi
{
    [Fact]
    public void OynaticiAcilisHatasiSessizceYutulmaz()
    {
        var rapor = AppHost.Run(() =>
        {
            var bozuk = Path.Combine(GirdiKanit.Folder, "bozuk-ornek.mp4");
            File.WriteAllBytes(bozuk, new byte[] { 0, 1, 2, 3 });

            var window = new MainWindow(bozuk);
            var oncekiMetin = window.SourceStatusText;
            var oncekiHata = window.PlayerOpenFailure;

            var is_ = window.LoadStartupFileAsync();
            var saat = Stopwatch.StartNew();
            while (!is_.IsCompleted && saat.Elapsed < TimeSpan.FromSeconds(60))
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }

            var body = $"dosya: {Path.GetFileName(bozuk)} ({new FileInfo(bozuk).Length} bayt){Environment.NewLine}"
                     + $"LoadStartupFileAsync bitti mi: {is_.IsCompleted} ({saat.ElapsedMilliseconds} ms){Environment.NewLine}"
                     + $"once  : istisna {oncekiHata?.GetType().Name ?? "yok"} metin '{oncekiMetin}'{Environment.NewLine}"
                     + $"sonra : istisna {window.PlayerOpenFailure?.GetType().Name ?? "yok"}: {window.PlayerOpenFailure?.Message}{Environment.NewLine}"
                     + $"kaynak durumu gorunur {window.SourceStatusVisible}, metin '{window.SourceStatusText}'{Environment.NewLine}";

            Assert.True(is_.IsCompleted, "LoadStartupFileAsync 60 sn icinde bitmedi");
            Assert.Null(oncekiHata);
            Assert.NotNull(window.PlayerOpenFailure);
            Assert.True(window.SourceStatusVisible);
            Assert.NotEqual("", window.SourceStatusText);

            window.Close();
            return body;
        });

        GirdiKanit.Write("k12-acilis-hatasi.txt", rapor);
    }

    private static int DilAboneSayisi()
    {
        var alan = typeof(Strings).GetField(
            nameof(Strings.Changed),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (alan?.GetValue(null) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    /// <summary>
    /// T181: <see cref="Strings.Use"/> surec genelinde ve arayuz is parcaciginin disindan
    /// cagrilabiliyor. Iki sey olculuyor: gorsel agactan ayrilan oynatici aboneligini
    /// gercekten birakiyor mu (abone sayisi kurulus seviyesine doner), ve hala ekliyken
    /// olay baska bir is parcacigindan gelirse <c>RefreshState</c> istisna atiyor mu.
    /// </summary>
    [Fact]
    public void AyrilanOynaticiDilAboneliginiBirakir()
    {
        var onceki = Strings.Language;
        Window? pencere = null;

        try
        {
            var (kurulus, ekli) = AppHost.Run(() =>
            {
                var view = new PlayerView();
                pencere = new Window { Width = 640, Height = 480, Content = view };
                var a = DilAboneSayisi();
                pencere.Show();
                return (a, DilAboneSayisi());
            });

            Strings.Use("tr");

            var ayrik = AppHost.Run(() =>
            {
                pencere!.Close();
                return DilAboneSayisi();
            });

            Strings.Use("tr");

            Assert.Equal(kurulus + 1, ekli);
            Assert.Equal(kurulus, ayrik);
        }
        finally
        {
            AppHost.Run(() => pencere?.Close());
            Strings.Use(onceki);
        }
    }
}
