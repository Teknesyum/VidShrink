using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class YolKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-yol-haritasi");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
        => File.WriteAllText(Path.Combine(Folder, name), body, new UTF8Encoding(false));

    internal static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

internal sealed class YolMotoru : IPlaybackEngine
{
    private readonly byte[] _piksel;
    private readonly int _w;
    private readonly int _h;

    internal YolMotoru(byte parlaklik = 128, int w = 64, int h = 36)
    {
        _w = w;
        _h = h;
        _piksel = new byte[4 * w * h];
        for (var i = 0; i < _piksel.Length; i += 4)
        {
            _piksel[i] = parlaklik;
            _piksel[i + 1] = parlaklik;
            _piksel[i + 2] = parlaklik;
            _piksel[i + 3] = 255;
        }
    }

    internal List<string> Eklenen { get; } = new();

    internal double Hiz { get; private set; } = 1;

    public string Name => "yol-motoru";

    public bool IsOpen { get; private set; }

    public double DurationSeconds => 600;

    public bool HasAudio => false;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached => false;

    public double PositionSeconds { get; private set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered { get; private set; }

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public double Speed => Hiz;

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        IsOpen = true;
        FramesRendered = 1;
        return Task.CompletedTask;
    }

    public void Play() => IsPaused = false;

    public void Pause() => IsPaused = true;

    public void SetSpeed(double speed) => Hiz = speed;

    public bool AddSubtitle(string path)
    {
        if (!IsOpen || !File.Exists(path)) return false;
        Eklenen.Add(path);
        return true;
    }

    public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        PositionSeconds = seconds;
        FramesRendered++;
        return Task.FromResult(new SeekResult(SeekOutcome.Shown, 1));
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy)
    {
        if (seen == FramesRendered) return false;
        seen = FramesRendered;
        var handle = GCHandle.Alloc(_piksel, GCHandleType.Pinned);
        try
        {
            copy(handle.AddrOfPinnedObject(), _w, _h, _w * 4);
        }
        finally
        {
            handle.Free();
        }

        return true;
    }

    public void Dispose() => IsOpen = false;
}

internal sealed class ElleSaat : IHoverClock
{
    private Action? _fire;

    internal TimeSpan? Bekleyen { get; private set; }

    public void Start(TimeSpan delay, Action fire)
    {
        Bekleyen = delay;
        _fire = fire;
    }

    public void Stop()
    {
        Bekleyen = null;
        _fire = null;
    }

    internal bool Ates()
    {
        var fire = _fire;
        Stop();
        fire?.Invoke();
        return fire is not null;
    }
}

public sealed class OynaticiYolHaritasiTests
{
    private const BindingFlags Her = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly object Fare = Activator.CreateInstance(typeof(MouseDevice), Her, null,
        new object[] { new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, true) }, null)!;

    private static void Hareket(Window window, PlayerView view, Point nokta)
    {
        var impl = typeof(TopLevel).GetProperty("PlatformImpl", Her)!.GetValue(window)!;
        var kok = (IInputRoot)typeof(TopLevel).GetProperty("InputRoot", Her)!.GetValue(window)!;
        var giris = (Action<RawInputEventArgs>)impl.GetType().GetInterfaces()
            .Select(i => i.GetProperty("Input", Her)).First(p => p is not null)!.GetValue(impl)!;
        giris((RawInputEventArgs)Activator.CreateInstance(typeof(RawPointerEventArgs), Her, null,
            new object[] { Fare, (ulong)Environment.TickCount64, kok, RawPointerEventType.Move, nokta, RawInputModifiers.None }, null)!);
        Dispatcher.UIThread.RunJobs();
        view.RenderLatest();
    }

    private static PlayerView Ac(YolMotoru motor, out Window window, string? path = null, double en = 960, double boy = 540)
    {
        var view = new PlayerView { EngineFactory = () => motor };
        window = new Window { Width = en, Height = boy, Content = view };
        window.Show();
        var open = view.OpenAsync(path ?? Path.Combine(YolKanit.Folder, "yok-sahte.mp4"));
        DenetimSurucu.Pump(view, () => open.IsCompleted, 10);
        open.GetAwaiter().GetResult();
        DenetimSurucu.Wait(view, 0.3);
        return view;
    }

    private static void Dongu(Func<bool> bitti, double saniye)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed.TotalSeconds < saniye)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }

    private static TimeSpan Sure(StyledElement element, string key)
        => element.TryFindResource(key, out var value) && value is TimeSpan span ? span : TimeSpan.Zero;

    private static void Tikla(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent) { Source = button });

    [Fact]
    public void P2PencereEkranOrtasinaYaklasincaYapisir()
    {
        var body = new StringBuilder();
        try
        {
        AppHost.Run(() =>
        {
            var view = new PlayerView { NativeMoveDrag = false };
            var window = new Window { Width = 640, Height = 360, Content = view };
            window.Show();
            DenetimSurucu.Wait(view, 0.2);

            var esikDip = view.FindResource("PlaybackBadgeMargin") is Thickness m ? m.Left : 0;
            Assert.True(esikDip >= 2, "esik belirteci okunamadi");

            var sol = new PlayerView.ScreenArea(new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), 1.0);
            var sag = new PlayerView.ScreenArea(new PixelRect(1920, -200, 3840, 2160), new PixelRect(1920, -200, 3840, 2110), 1.5);
            var ekranlar = new[] { sol, sag };
            var cerceveDip = new Size(640, 360);
            var pencereBas = new PixelPoint(100, 100);
            var imlecBas = new PixelPoint(400, 300);
            PixelPoint Imlec(PixelPoint serbest) => new(serbest.X - pencereBas.X + imlecBas.X, serbest.Y - pencereBas.Y + imlecBas.Y);
            PixelPoint Konum(PixelPoint serbest, double olcek) => PlayerView.DragPosition(pencereBas, imlecBas, Imlec(serbest), cerceveDip, olcek, ekranlar, esikDip);

            var sagEsik = (int)Math.Ceiling(esikDip * 1.5);
            var solEsik = (int)Math.Ceiling(esikDip);
            var sagMerkez = new PixelPoint(1920 + (3840 - 960) / 2, -200 + (2110 - 540) / 2);
            var solMerkez = new PixelPoint((1920 - 640) / 2, (1040 - 360) / 2);
            var sagYakin = Konum(new PixelPoint(sagMerkez.X + sagEsik, sagMerkez.Y - sagEsik), 1.5);
            var sagOlcekli = Konum(new PixelPoint(sagMerkez.X + solEsik + 1, sagMerkez.Y), 1.5);
            var sagUzak = Konum(new PixelPoint(sagMerkez.X + sagEsik + 1, sagMerkez.Y - sagEsik - 1), 1.5);
            var solYakin = Konum(new PixelPoint(solMerkez.X - solEsik, solMerkez.Y + solEsik), 1.0);
            var solUzak = Konum(new PixelPoint(solMerkez.X + solEsik + 1, solMerkez.Y), 1.0);
            var tekEksen = Konum(new PixelPoint(solMerkez.X + 1, solMerkez.Y + 200), 1.0);
            body.AppendLine($"saf: esik {YolKanit.N(esikDip)} dip, sol esik {solEsik} px (1,0), sag esik {sagEsik} px (1,5)");
            body.AppendLine($"  sag merkez {sagMerkez}: yakin {sagYakin}, olcekli ({solEsik + 1} px) {sagOlcekli}, uzak {sagUzak}");
            body.AppendLine($"  sol merkez {solMerkez}: yakin {solYakin}, uzak {solUzak}, tek eksen {tekEksen}");
            Assert.Equal(sagMerkez, sagYakin);
            Assert.Equal(sagMerkez, sagOlcekli);
            Assert.Equal(new PixelPoint(sagMerkez.X + sagEsik + 1, sagMerkez.Y - sagEsik - 1), sagUzak);
            Assert.Equal(solMerkez, solYakin);
            Assert.Equal(new PixelPoint(solMerkez.X + solEsik + 1, solMerkez.Y), solUzak);
            Assert.Equal(new PixelPoint(solMerkez.X, solMerkez.Y + 200), tekEksen);

            var karmaMerkez = new PixelPoint(1920 + (3840 - 640) / 2, -200 + (2110 - 360) / 2);
            var karma = Konum(new PixelPoint(karmaMerkez.X + solEsik + 1, karmaMerkez.Y), 1.0);
            var sinirImlec = new PixelPoint(1400 + 600, solMerkez.Y + solEsik + 180);
            var sinir = PlayerView.DragPosition(pencereBas, new PixelPoint(pencereBas.X + 600, pencereBas.Y + 180), sinirImlec, cerceveDip, 1.0, ekranlar, esikDip);
            body.AppendLine($"  karma olcek (pencere 1,0 sag ekranda) merkez {karmaMerkez}: {karma}; sinir (imlec {sinirImlec} sag ekranda, pencere merkezi solda): {sinir}");
            Assert.Equal(karmaMerkez, karma);
            Assert.Equal(new PixelPoint(1400, solMerkez.Y), sinir);

            var ekran = window.Screens.ScreenFromWindow(window);
            Assert.NotNull(ekran);
            var olcek = ekran!.Scaling;
            var esikPx = (int)Math.Ceiling(esikDip * olcek);
            var cerceve = PixelSize.FromSize(window.FrameSize ?? window.ClientSize, olcek);
            var merkez = new PixelPoint(
                ekran.WorkingArea.X + (ekran.WorkingArea.Width - cerceve.Width) / 2,
                ekran.WorkingArea.Y + (ekran.WorkingArea.Height - cerceve.Height) / 2);
            window.Position = new PixelPoint(merkez.X - 150, merkez.Y - 120);
            DenetimSurucu.Wait(view, 0.2);
            var baslangic = window.Position;

            var impl = typeof(TopLevel).GetProperty("PlatformImpl", Her)!.GetValue(window)!;
            var kok = (IInputRoot)typeof(TopLevel).GetProperty("InputRoot", Her)!.GetValue(window)!;
            var giris = (Action<RawInputEventArgs>)impl.GetType().GetInterfaces()
                .Select(i => i.GetProperty("Input", Her)).First(p => p is not null)!.GetValue(impl)!;
            void Olay(RawPointerEventType tur, PixelPoint ekranda, RawInputModifiers tuslar)
            {
                var istemci = window.PointToClient(ekranda);
                giris((RawInputEventArgs)Activator.CreateInstance(typeof(RawPointerEventArgs), Her, null,
                    new object[] { Fare, (ulong)Environment.TickCount64, kok, tur, istemci, tuslar }, null)!);
                DenetimSurucu.Wait(view, 0.05);
            }

            var tutma = window.PointToScreen(new Point(view.Bounds.Width / 2, view.Bounds.Height / 2));
            PixelPoint Tut(PixelPoint serbest) => new(tutma.X + serbest.X - baslangic.X, tutma.Y + serbest.Y - baslangic.Y);
            Olay(RawPointerEventType.Move, tutma, RawInputModifiers.None);
            Olay(RawPointerEventType.LeftButtonDown, tutma, RawInputModifiers.LeftMouseButton);

            var adimlar = new (string ad, PixelPoint serbest, PixelPoint beklenen)[]
            {
                ("ilk-adim", new PixelPoint(baslangic.X + 20, baslangic.Y + 10), new PixelPoint(baslangic.X + 20, baslangic.Y + 10)),
                ("yaklasirken", new PixelPoint(merkez.X - esikPx - 30, merkez.Y - 40), new PixelPoint(merkez.X - esikPx - 30, merkez.Y - 40)),
                ("bolgede", new PixelPoint(merkez.X + esikPx, merkez.Y - esikPx), merkez),
                ("bolge-disi", new PixelPoint(merkez.X + esikPx + 1, merkez.Y - esikPx - 1), new PixelPoint(merkez.X + esikPx + 1, merkez.Y - esikPx - 1)),
                ("geri-bolgede", new PixelPoint(merkez.X - 1, merkez.Y + 2), merkez)
            };
            var hatalar = new List<string>();
            foreach (var (ad, serbest, beklenen) in adimlar)
            {
                Olay(RawPointerEventType.Move, Tut(serbest), RawInputModifiers.LeftMouseButton);
                var konum = window.Position;
                body.AppendLine($"surukleme {ad}: serbest {serbest}, konum {konum}, beklenen {beklenen}, surukleniyor {view.WindowDragging}");
                if (konum != beklenen || !view.WindowDragging) hatalar.Add(ad);
            }

            Olay(RawPointerEventType.LeftButtonUp, Tut(adimlar[^1].serbest), RawInputModifiers.None);
            body.AppendLine($"birakis: konum {window.Position}, surukleniyor {view.WindowDragging}; ekran {ekran.WorkingArea} olcek {YolKanit.N(olcek)} merkez {merkez} esik {esikPx} px");
            body.AppendLine("iz: " + string.Join(" | ", view.Trace.Where(s => s.StartsWith("snap", StringComparison.Ordinal) || s.StartsWith("move", StringComparison.Ordinal) || s.StartsWith("drag", StringComparison.Ordinal))));
            Assert.False(view.WindowDragging);
            Assert.True(hatalar.Count == 0, body.ToString());

            window.Close();
            return 0;
        });
        }
        finally
        {
            YolKanit.Write("p2-merkez-miknatisi.txt", body.ToString());
        }
    }

    [Fact]
    public void P3SagTikAyarlarAltMenusuUygulamaAyarlariniTasir()
    {
        var body = new StringBuilder();
        try
        {
        AppHost.Run(() =>
        {
            var kok = Path.Combine(YolKanit.Folder, "p3");
            Directory.CreateDirectory(kok);
            var window = new MainWindow { SettingsPathOverride = Path.Combine(kok, "settings.json"), Width = 1280, Height = 800, WindowState = WindowState.Normal };
            var view = window.PlayerTab;
            window.Show();
            DenetimSurucu.Wait(view, 0.3);

            var menu = view.BuildMenu().Items.OfType<MenuItem>().ToList();
            var ayarlar = menu.Single(item => ReferenceEquals(item.Tag, Keymap.Settings));
            var cocuklar = ayarlar.Items.OfType<MenuItem>().ToList();
            body.AppendLine($"ayarlar alt menusu: {string.Join(" | ", cocuklar.Select(c => c.Header))}");

            var tema = cocuklar.Single(c => (string?)c.Header == Strings.Get("settings-tab.theme.label"));
            var dil = cocuklar.Single(c => (string?)c.Header == Strings.Get("settings-tab.language.label"));
            Assert.Contains(cocuklar, c => (string?)c.Header == Strings.Get("player.advanced.menu"));
            Assert.Contains(cocuklar, c => (string?)c.Header == Strings.Get("player.view.screenshot-folder"));

            var cmbTema = window.FindControl<ComboBox>("CmbTheme")!;
            var cmbDil = window.FindControl<ComboBox>("CmbLanguage")!;
            var temaSatirlari = tema.Items.OfType<MenuItem>().ToList();
            body.AppendLine($"tema satiri {temaSatirlari.Count}, kutu {cmbTema.ItemCount}, secili {cmbTema.SelectedIndex}; dil satiri {dil.Items.Count}, kutu {cmbDil.ItemCount}");
            Assert.Equal(cmbTema.ItemCount, temaSatirlari.Count);
            Assert.Equal(cmbDil.ItemCount, dil.Items.Count);
            Assert.True(temaSatirlari[cmbTema.SelectedIndex].IsChecked);

            var once = cmbTema.SelectedIndex;
            var hedef = once == 0 ? 1 : 0;
            temaSatirlari[hedef].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = temaSatirlari[hedef] });
            DenetimSurucu.Wait(view, 0.2);
            body.AppendLine($"tema satiri {hedef} tiklandi: kutu {once} -> {cmbTema.SelectedIndex}");
            var temaTutti = cmbTema.SelectedIndex == hedef;
            cmbTema.SelectedIndex = once;
            DenetimSurucu.Wait(view, 0.2);

            var sekmeyeGiden = cocuklar.Count(c => ReferenceEquals(c.Tag, Keymap.Settings));
            var kisayollar = cocuklar.SingleOrDefault(c => (string?)c.Header == Strings.Get("settings.player-shortcuts.title"));
            var kisayolSatiri = kisayollar?.Items.OfType<MenuItem>().Count() ?? 0;
            body.AppendLine($"sekmeye giden satir {sekmeyeGiden}; kisayollar alt menusu {kisayollar is not null}, satir {kisayolSatiri}, tablo {Keymap.Rows.Count(r => !ReferenceEquals(r.Action, Keymap.Settings))}");

            window.Close();
            Assert.True(temaTutti, "tema satiri ayarlar kutusunu degistirmedi");
            Assert.Equal(0, sekmeyeGiden);
            Assert.NotNull(kisayollar);
            Assert.Equal(Keymap.Rows.Count(r => !ReferenceEquals(r.Action, Keymap.Settings)), kisayolSatiri);
            return 0;
        });
        }
        finally
        {
            YolKanit.Write("p3-sag-tik-ayarlar.txt", body.ToString());
        }
    }

    [Fact]
    public void P12OnSaniyeDugmeleriAtladigiSayiyiYaziyor()
    {
        var body = new StringBuilder();
        try
        {
        AppHost.Run(() =>
        {
            var view = new PlayerView();
            var window = new Window { Width = 960, Height = 540, Content = view };
            window.Show();
            _ = view.SeritZone;
            DenetimSurucu.Wait(view, 0.3);
            view.Seek.Duration = 100000;
            view.Seek.GoTo(500);

            foreach (var (dugmeAdi, yaziAdi) in new[] { ("BtnSeritBack", "TxtSeritBack"), ("BtnSeritForward", "TxtSeritForward") })
            {
                var dugme = view.FindControl<Button>(dugmeAdi)!;
                var yazi = view.FindControl<TextBlock>(yaziAdi)!;
                var metin = yazi.Text ?? "";
                var sayi = double.TryParse(metin.Replace('−', '-'), NumberStyles.Float, CultureInfo.CurrentCulture, out var s) ? s : double.NaN;
                var once = view.PositionSeconds;
                Tikla(dugme);
                var sonra = view.PositionSeconds;
                var icinde = yazi.GetVisualAncestors().Contains(dugme);
                body.AppendLine($"{dugmeAdi}: yazi '{metin}' (sayi {YolKanit.N(sayi)}), gorunur {yazi.IsEffectivelyVisible}, en {YolKanit.N(yazi.Bounds.Width)}, dugmenin icinde {icinde}, konum {YolKanit.N(once)} -> {YolKanit.N(sonra)}");
                Assert.True(double.IsFinite(sayi) && sayi != 0, dugmeAdi + " yazisi sayi degil: '" + metin + "'");
                Assert.True(icinde && yazi.IsEffectivelyVisible && yazi.Bounds.Width > 0, dugmeAdi + " yazisi dugmede gorunmuyor");
                Assert.Equal(sayi, sonra - once, 3);
            }

            window.Close();
            return 0;
        });
        }
        finally
        {
            YolKanit.Write("p12-on-saniye-simgesi.txt", body.ToString());
        }
    }

    [Fact]
    public void P14UstBarAltBarlaAyniKurallaGizlenir()
    {
        var body = new StringBuilder();
        try
        {
        AppHost.Run(() =>
        {
            var hatalar = new List<string>();
            var kok = Path.Combine(YolKanit.Folder, "p14");
            Directory.CreateDirectory(kok);
            var klip = Path.Combine(kok, "sahte.mp4");
            File.WriteAllBytes(klip, new byte[16]);
            var motor = new YolMotoru();
            var window = new MainWindow { SettingsPathOverride = Path.Combine(kok, "settings.json"), Width = 1280, Height = 800, WindowState = WindowState.Normal };
            var view = window.PlayerTab;
            view.EngineFactory = () => motor;
            window.Show();
            DenetimSurucu.Wait(view, 0.3);
            var ac = window.OpenInPlayerAsync(klip);
            DenetimSurucu.Pump(view, () => ac.IsCompleted, 10);
            DenetimSurucu.Wait(view, 0.3);
            if (!view.IsPlaying) view.Apply(Keymap.PlayPause.ToCommand());

            var saat = new ElleSaat();
            window.ChromeZone.Clock = saat;
            var gecikme = Sure(window, "PlaybackStripHideDelay");
            var seritGecikme = Sure(view, "PlaybackStripHideDelay");
            body.AppendLine($"ust bar gecikmesi {gecikme.TotalMilliseconds} ms, alt serit gecikmesi {seritGecikme.TotalMilliseconds} ms");

            string Durum() => $"sekme {window.Tabs.SelectedIndex}, oynuyor {view.IsPlaying}, bar {(window.ChromeShown ? "acik" : "gizli")}, bekleyen {(saat.Bekleyen is { } b ? b.TotalMilliseconds + " ms" : "yok")}";
            void Adim(string ad, Action is_, bool acik, bool bekleyenVar)
            {
                is_();
                DenetimSurucu.Wait(view, 0.1);
                var ok = window.ChromeShown == acik && (saat.Bekleyen is not null) == bekleyenVar
                         && (!bekleyenVar || saat.Bekleyen == gecikme);
                body.AppendLine($"{ad}: {Durum()} {(ok ? "GECTI" : "KALDI")}");
                if (!ok) hatalar.Add(ad);
            }

            Adim("1 fare ustte", () => Hareket(window, view, new Point(640, 2)), true, false);
            Adim("2 fare asagida, gecikme bekliyor", () => Hareket(window, view, new Point(640, 420)), true, true);
            Adim("3 gecikme doldu", () => saat.Ates(), false, false);
            Adim("4 duraklatildi, fare asagida", () => view.Apply(Keymap.PlayPause.ToCommand()), true, false);
            Adim("5 duraklatilmisken fare kipirdadi", () => Hareket(window, view, new Point(600, 500)), true, false);
            Adim("6 oynatma surdu", () => view.Apply(Keymap.PlayPause.ToCommand()), true, true);
            Adim("7 gecikme doldu", () => saat.Ates(), false, false);
            Adim("8 ayarlar sekmesi", () => window.Tabs.SelectedItem = window.TabSettings, true, false);
            Adim("9 ayarlar sekmesinde fare asagida", () => { Hareket(window, view, new Point(600, 520)); saat.Ates(); }, true, false);

            window.Close();
            Assert.Equal(seritGecikme, gecikme);
            Assert.True(hatalar.Count == 0, "kalan adimlar: " + string.Join(", ", hatalar) + Environment.NewLine + body);
            return 0;
        });
        }
        finally
        {
            YolKanit.Write("p14-ust-bar-gizlenme.txt", body.ToString());
        }
    }

    [Fact]
    public void P14UstBarVeAltSeritAyniMesafedeAcilir()
    {
        var body = new StringBuilder();
        try
        {
        AppHost.Run(() =>
        {
            var kok = Path.Combine(YolKanit.Folder, "p14-esik");
            Directory.CreateDirectory(kok);
            var klip = Path.Combine(kok, "sahte.mp4");
            File.WriteAllBytes(klip, new byte[16]);
            var motor = new YolMotoru();
            var window = new MainWindow { SettingsPathOverride = Path.Combine(kok, "settings.json"), Width = 1280, Height = 800, WindowState = WindowState.Normal };
            var view = window.PlayerTab;
            view.EngineFactory = () => motor;
            window.Show();
            DenetimSurucu.Wait(view, 0.3);
            var ac = window.OpenInPlayerAsync(klip);
            DenetimSurucu.Pump(view, () => ac.IsCompleted, 10);
            DenetimSurucu.Wait(view, 0.3);
            if (!view.IsPlaying) view.Apply(Keymap.PlayPause.ToCommand());

            var ustSaat = new ElleSaat();
            var altSaat = new ElleSaat();
            window.ChromeZone.Clock = ustSaat;
            view.SeritZone.Clock = altSaat;
            var yuzey = view.FindControl<Panel>("Surface")!;
            var alt = yuzey.TranslatePoint(new Point(0, yuzey.Bounds.Height), window)!.Value.Y;
            var orta = new Point(640, alt / 2);
            body.AppendLine($"yuzey yuksekligi {YolKanit.N(yuzey.Bounds.Height)}, alt kenar {YolKanit.N(alt)}, baslik {YolKanit.N(window.TitleBar.Height)}, bant {YolKanit.N(view.RevealBand)}");

            int Esik(string ad, Func<int, Point> nokta, Func<bool> acik, ElleSaat saat)
            {
                bool Dene(int d)
                {
                    Hareket(window, view, nokta(1));
                    Hareket(window, view, orta);
                    saat.Ates();
                    DenetimSurucu.Wait(view, 0.02);
                    Assert.False(acik(), ad + " ortada kapanmadi");
                    Hareket(window, view, nokta(d));
                    DenetimSurucu.Wait(view, 0.02);
                    return acik();
                }

                var d = 1;
                Assert.True(Dene(d), ad + " kenarda acilmadi");
                while (d < 400 && Dene(d + 8)) d += 8;
                while (Dene(d + 1)) d++;
                body.AppendLine($"{ad}: son acan mesafe {d} px");
                return d;
            }

            var ust = Esik("ust bar", d => new Point(640, d), () => window.ChromeShown, ustSaat);
            var altEsik = Esik("alt serit", d => new Point(640, alt - d), () => view.SeritRevealed, altSaat);
            var bant = view.RevealBand;
            var baslik = window.TitleBar.Height;
            window.Close();

            Assert.True(Math.Abs(ust - altEsik) <= 1, $"ust {ust} px, alt {altEsik} px" + Environment.NewLine + body);
            Assert.True(Math.Abs(ust - bant) <= 1, $"ust {ust} px, bant {YolKanit.N(bant)}");
            Assert.True(ust > baslik + 1, $"ust esik baslik yuksekliginde kaldi: {ust} <= {YolKanit.N(baslik)}");
            return 0;
        });
        }
        finally
        {
            YolKanit.Write("p14-acilma-esigi.txt", body.ToString());
        }
    }

    [Fact]
    public void P18HizSimgesiBireVeSonHizaDoner()
    {
        var body = new StringBuilder();
        try
        {
        AppHost.Run(() =>
        {
            var motor = new YolMotoru();
            var view = Ac(motor, out var window);
            _ = view.SeritZone;
            var dugme = view.FindControl<Button>("BtnSeritSpeedReset")!;
            var okumalar = new List<string>();
            void Oku(string ad)
            {
                var satir = $"{ad}: gorunum {YolKanit.N(view.SpeedFactor)}, motor {YolKanit.N(motor.Hiz)}";
                okumalar.Add(YolKanit.N(view.SpeedFactor) + "/" + YolKanit.N(motor.Hiz));
                body.AppendLine(satir);
            }

            Oku("baslangic");
            Tikla(dugme);
            Oku("1x iken tik (onceki hiz yok)");
            view.Apply(new PlayerCommand(PlayerCommandKind.Speed, 0.5));
            Oku("hiz +0,5");
            Tikla(dugme);
            Oku("tik");
            Tikla(dugme);
            Oku("tik");
            view.Apply(new PlayerCommand(PlayerCommandKind.Speed, 0.25));
            Oku("hiz +0,25");
            Tikla(dugme);
            Oku("tik");
            Tikla(dugme);
            Oku("tik");

            window.Close();
            Assert.Equal(new[] { "1/1", "1/1", "1.5/1.5", "1/1", "1.5/1.5", "1.75/1.75", "1/1", "1.75/1.75" }, okumalar);
            return 0;
        });
        }
        finally
        {
            YolKanit.Write("p18-hiz-simgesi.txt", body.ToString());
        }
    }

    [Fact]
    public void P19DuraklatmaSimgesiYarimSaniyedeGirerVeCikar()
    {
        var body = new StringBuilder();
        try
        {
        AppHost.Run(() =>
        {
            var motor = new YolMotoru();
            var view = Ac(motor, out var window);
            var simge = view.FindControl<Border>("PauseGlyph")!;
            var yuzey = view.FindControl<Panel>("Surface")!;
            var tutma = Sure(view, "PauseGlyphHold");
            var hizli = Sure(view, "MotionFast");
            var hedefSaydam = view.FindResource("PauseGlyphOpacity") is double o ? o : double.NaN;
            body.AppendLine($"PauseGlyphHold {tutma.TotalMilliseconds} ms, MotionFast {hizli.TotalMilliseconds} ms, saydamlik {YolKanit.N(hedefSaydam)}");

            void Oynat()
            {
                if (!view.IsPlaying) view.Apply(Keymap.PlayPause.ToCommand());
                Dongu(() => !simge.IsVisible, 2);
            }

            var sureler = new List<double>();
            var girisAra = false;
            var cikisAra = false;
            for (var tur = 0; tur < 5; tur++)
            {
                Oynat();
                DenetimSurucu.Wait(view, 0.1);
                Assert.False(simge.IsVisible, "oynatmada simge gorunmemeli");
                var saat = Stopwatch.StartNew();
                view.Apply(Keymap.PlayPause.ToCommand());
                var ornekler = new List<(double Ms, double Op)>();
                while (simge.IsVisible && saat.Elapsed.TotalSeconds < 2)
                {
                    using (var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2)))
                        Dispatcher.UIThread.MainLoop(dilim.Token);
                    ornekler.Add((saat.Elapsed.TotalMilliseconds, simge.Opacity));
                }

                var sure = saat.Elapsed.TotalMilliseconds;
                sureler.Add(sure);
                var ilkYari = ornekler.Where(x => x.Ms < tutma.TotalMilliseconds / 2).Select(x => x.Op).ToList();
                var sonYari = ornekler.Where(x => x.Ms >= tutma.TotalMilliseconds / 2).Select(x => x.Op).ToList();
                girisAra |= ilkYari.Any(op => op > 0.01 && op < hedefSaydam - 0.01);
                cikisAra |= sonYari.Any(op => op > 0.01 && op < hedefSaydam - 0.01);
                body.AppendLine($"tur {tur}: ekranda {YolKanit.N(sure)} ms, ornek {ornekler.Count}, saydamlik izi {string.Join(" ", ornekler.Where((_, i) => i % 8 == 0).Select(x => YolKanit.N(x.Ms) + ":" + YolKanit.N(x.Op)))}");
            }

            Oynat();
            view.Apply(Keymap.PlayPause.ToCommand());
            var bekle = Stopwatch.StartNew();
            Dongu(() => bekle.Elapsed >= tutma - hizli + TimeSpan.FromMilliseconds(40), 2);
            view.Apply(Keymap.PlayPause.ToCommand());
            view.Apply(Keymap.PlayPause.ToCommand());
            bekle.Restart();
            Dongu(() => bekle.Elapsed >= hizli + TimeSpan.FromMilliseconds(80), 2);
            var ikinciGorunur = simge.IsVisible;
            body.AppendLine($"cikis sirasinda yeniden duraklatma: {YolKanit.N((hizli.TotalSeconds + 0.08) * 1000)} ms sonra simge gorunur {ikinciGorunur}");

            Dongu(() => simge.IsVisible && simge.Opacity >= hedefSaydam - 0.01, 1);
            var merkez = simge.TranslatePoint(new Point(simge.Bounds.Width / 2, simge.Bounds.Height / 2), yuzey);
            body.AppendLine($"simge {YolKanit.N(simge.Bounds.Width)}x{YolKanit.N(simge.Bounds.Height)}, merkez {merkez}, yuzey {yuzey.Bounds.Size}, tik gecirgen {!simge.IsHitTestVisible}");

            var enKisa = sureler.Min();
            body.AppendLine($"en kisa {YolKanit.N(enKisa)} ms, giriste ara saydamlik {girisAra}, cikista ara saydamlik {cikisAra}");
            window.Close();

            Assert.InRange(enKisa, tutma.TotalMilliseconds - 30, tutma.TotalMilliseconds + 90);
            Assert.True(ikinciGorunur, "eski cikisin gizlemesi yeni simgeyi kapatti");
            Assert.True(girisAra && cikisAra, "giris ya da cikis animasyonsuz");
            Assert.NotNull(merkez);
            Assert.Equal(yuzey.Bounds.Width / 2, merkez!.Value.X, 0);
            Assert.Equal(yuzey.Bounds.Height / 2, merkez.Value.Y, 0);
            Assert.False(simge.IsHitTestVisible);
            return 0;
        });
        }
        finally
        {
            YolKanit.Write("p19-duraklatma-simgesi.txt", body.ToString());
        }
    }

    [Fact]
    public void P20OynaticiSekmesindePencereAnahattiYok()
    {
        var body = new StringBuilder();
        try
        {
        AppHost.Run(() =>
        {
            var kok = Path.Combine(YolKanit.Folder, "p20");
            Directory.CreateDirectory(kok);
            var window = new MainWindow { SettingsPathOverride = Path.Combine(kok, "settings.json"), Width = 1280, Height = 800, WindowState = WindowState.Normal };
            var view = window.PlayerTab;
            window.Show();
            DenetimSurucu.Wait(view, 0.3);
            var kabuk = window.FindControl<Border>("WindowShell")!;
            var sahne = view.FindControl<Border>("Stage")!;
            var ince = window.FindResource("BorderThin") is Thickness t ? t : default;

            var olcum = new List<(string Sekme, Thickness Kabuk, Thickness Sahne)>();
            foreach (var (ad, sekme) in new (string, TabItem)[] { ("ayarlar", window.TabSettings), ("oynatici", window.TabPlayer), ("ayarlar", window.TabSettings) })
            {
                window.Tabs.SelectedItem = sekme;
                DenetimSurucu.Wait(view, 0.15);
                olcum.Add((ad, kabuk.BorderThickness, sahne.BorderThickness));
                body.AppendLine($"{ad}: pencere {window.WindowState}, kabuk anahatti {kabuk.BorderThickness}, sahne anahatti {sahne.BorderThickness}");
            }

            window.Close();
            Assert.True(ince != default, "BorderThin okunamadi");
            Assert.Equal(ince, olcum[0].Kabuk);
            Assert.Equal(new Thickness(0), olcum[1].Kabuk);
            Assert.Equal(new Thickness(0), olcum[1].Sahne);
            Assert.Equal(ince, olcum[2].Kabuk);
            return 0;
        });
        }
        finally
        {
            YolKanit.Write("p20-anahat.txt", body.ToString());
        }
    }

    private static (byte R, byte G, byte B)[,] Ciz(Window window)
    {
        var w = (int)window.Bounds.Width;
        var h = (int)window.Bounds.Height;
        using var kare = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        kare.Render(window);
        var stride = w * 4;
        var pixels = new byte[stride * h];
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            kare.CopyPixels(new PixelRect(0, 0, w, h), pinned.AddrOfPinnedObject(), pixels.Length, stride);
        }
        finally
        {
            pinned.Free();
        }

        var sonuc = new (byte, byte, byte)[w, h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                sonuc[x, y] = (pixels[y * stride + x * 4 + 2], pixels[y * stride + x * 4 + 1], pixels[y * stride + x * 4]);
        return sonuc;
    }

    private static double Fark((byte R, byte G, byte B) a, (byte R, byte G, byte B) b)
        => Math.Sqrt(Math.Pow(a.R - b.R, 2) + Math.Pow(a.G - b.G, 2) + Math.Pow(a.B - b.B, 2));

    [Fact]
    public void P24AltBarinUstAnahattiMedyaUstundeDeSeciliyor()
    {
        var body = new StringBuilder();
        try
        {
        AppHost.Run(() =>
        {
            var sonuc = new Dictionary<string, ((byte R, byte G, byte B) Renk, double Ust)>();

            foreach (var (ad, parlaklik) in new (string, byte?)[] { ("medyasiz", null), ("koyu-medya", 40), ("orta-medya", 128), ("acik-medya", 230) })
            {
                PlayerView view;
                Window window;
                if (parlaklik is { } p)
                {
                    view = Ac(new YolMotoru(p, 960, 540), out window);
                    view.Apply(Keymap.PlayPause.ToCommand());
                }
                else
                {
                    view = new PlayerView();
                    window = new Window { Width = 960, Height = 540, Content = view };
                    window.Show();
                }

                _ = view.SeritZone;
                DenetimSurucu.Pump(view, () => view.SeritRevealed, 2);
                DenetimSurucu.Wait(view, 0.5);
                var serit = view.FindControl<Border>("StripBar")!;
                var sol = serit.TranslatePoint(new Point(0, 0), window)!.Value;
                var x = (int)(sol.X + serit.Bounds.Width * 0.35);
                var y = (int)Math.Round(sol.Y);
                var piksel = Ciz(window);
                var ustte = piksel[x, y - 6];
                var anahat = Enumerable.Range(y - 1, 3).Select(yy => piksel[x, yy]).OrderByDescending(c => Fark(c, ustte)).First();
                var icte = piksel[x, y + 6];
                var ust = Fark(anahat, ustte);
                var ic = Fark(anahat, icte);
                sonuc[ad] = (anahat, ust);
                body.AppendLine($"{ad}: serit y {y}, ustte {ustte}, anahat {anahat}, icte {icte}, anahat-ust fark {YolKanit.N(ust)}, anahat-ic fark {YolKanit.N(ic)}");
                window.Close();
            }

            var taban = sonuc["medyasiz"];
            foreach (var (ad, olcu) in sonuc)
            {
                if (ad == "medyasiz") continue;
                var kayma = Fark(olcu.Renk, taban.Renk);
                body.AppendLine($"{ad}: anahat rengi medyasizdan {YolKanit.N(kayma)} kaydi");
                Assert.True(kayma <= 24 && olcu.Ust > 24, $"{ad}: anahat medyanin rengini aliyor (kayma {YolKanit.N(kayma)}, ustle fark {YolKanit.N(olcu.Ust)})");
            }

            return 0;
        });
        }
        finally
        {
            YolKanit.Write("p24-alt-bar-anahat.txt", body.ToString());
        }
    }

    /// <summary>
    /// <para>P26 kapısı kalıntı saydamlıkla ölçülür. Dalın <c>2f53535f</c> commit'i yalnız bu testin
    /// ön koşulunu <c>Opacity &lt;= 0</c>'dan <c>&lt; 0.001</c>'e gevşetmişti; üretimdeki
    /// <c>RevealSerit</c> kapısı <c>&lt;= 0</c> kalmıştı, yani gevşetme yerelde hiçbir şey ölçmüyordu.
    /// Üretim toleransı <c>main</c>'in <c>16572f03</c>'ünden birleşmeyle geldi.</para>
    ///
    /// <para>Yerelde kapanış geçişi tam 0'a iniyor, bu yüzden kusur görünmüyordu: CI koşumu
    /// 35257391781 kalıntıyı <c>9.18867375793824E-89</c> olarak ölçtü. Kalıntı geçişten
    /// üretilemiyor — <c>DoubleTransition</c> hedefe <c>from + (to-from)*1</c> ile varıp tam 0
    /// yazıyor. Bu yüzden <c>SeritAcilisi</c> kapanıştan sonra şeridin saydamlık geçişini kapatıp
    /// kalıntıyı doğrudan yazar; ölçüm artık kayan nokta kuyruğunun rastlantısına bağlı değil.
    /// A/B ölçüldü: kapı <c>&lt; 0.001</c> iken yeşil (<c>maskeli ornek 8, maske kalkti True</c>),
    /// kapı <c>&lt;= 0</c>'a döndürülünce kırmızı (<c>yayilma yok: serit bir anda acildi</c>,
    /// <c>maskeli ornek 0</c>). Gevşetilmiş ön koşul kalır: kalıntı zorlandığı için tek doğru
    /// biçim odur, <c>&lt;= 0</c> ön koşulu artık testin kendi girdisini reddeder.</para>
    /// </summary>
    [Fact]
    public void P26SeritFareyeYakinKisimdanYayilarakAcilir()
    {
        var body = new StringBuilder();
        var sistem = HoverZone.MotionReduced;
        try
        {
        AppHost.Run(() =>
        {
            body.AppendLine($"sistemin hareket azaltma ayari {sistem}");

            var acik = SeritAcilisi(false, body);
            var azaltilmis = SeritAcilisi(true, body);

            Assert.True(acik.Acildi, "serit acilmadi");
            Assert.Equal(0.2, acik.Oran, 1);
            Assert.True(acik.Maskeli.Count > 0, "yayilma yok: serit bir anda acildi");
            var ilk = acik.Maskeli[0];
            Assert.True(ilk.Sol <= acik.Oran && ilk.Sag >= acik.Oran && ilk.Sag < 1, $"ilk gorunen kisim fareyi icermiyor ya da tamami acik: [{ilk.Sol} .. {ilk.Sag}]");
            Assert.True(acik.MaskeKalkti, "yayilma bitmedi");
            Assert.All(acik.Maskeli, o => Assert.True(o.Sol <= acik.Oran + 1e-6 && o.Sag >= acik.Oran - 1e-6));

            Assert.True(azaltilmis.Acildi, "hareket azaltilmisken serit acilmadi");
            Assert.Empty(azaltilmis.Maskeli);
            return 0;
        });
        }
        finally
        {
            HoverZone.MotionReduced = sistem;
            YolKanit.Write("p26-yayilarak-acilma.txt", body.ToString());
        }
    }

    private static (bool Acildi, double Oran, List<(double Ms, double Yayilma, double Sol, double Sag)> Maskeli, bool MaskeKalkti) SeritAcilisi(bool azalt, StringBuilder body)
    {
        HoverZone.MotionReduced = azalt;
        var motor = new YolMotoru();
        var view = Ac(motor, out var window);
        var saat = new ElleSaat();
        var serit = view.FindControl<Border>("StripBar")!;
        var yuzey = view.FindControl<Panel>("Surface")!;
        Dongu(() => yuzey.Bounds.Height > 0, 5);
        if (!view.IsPlaying) view.Apply(Keymap.PlayPause.ToCommand());
        Dongu(() => view.IsPlaying, 5);
        view.SeritZone.Clock = saat;
        Hareket(window, view, new Point(480, 100));
        Dongu(() =>
        {
            if (serit.Opacity < 0.001) return true;
            if (saat.Bekleyen is null)
            {
                Hareket(window, view, new Point(480, yuzey.Bounds.Height - 4));
                Hareket(window, view, new Point(480, 100));
            }
            saat.Ates();
            return serit.Opacity < 0.001;
        }, 10);
        serit.Transitions = null;
        serit.Opacity = 9.18867375793824e-89;
        string Alan(string ad) => typeof(HoverZone).GetField(ad, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view.SeritZone)!.ToString()!;
        var neden = $"oynuyor {view.IsPlaying}, fare icinde {Alan("_pointerInside")}, tutuluyor {Alan("_held")}, gorunur {Alan("_visible")}, yuzey {YolKanit.N(yuzey.Bounds.Height)}";
        body.AppendLine($"[hareket azaltilmis {azalt}] kapali serit saydamligi {serit.Opacity:E3}, MotionInstant {Sure(view, "MotionInstant").TotalMilliseconds} ms, {neden}");
        Assert.True(serit.Opacity < 0.001, $"on kosul: serit kapanmadi, saydamlik {serit.Opacity}, {neden}");

        var seritSol = serit.TranslatePoint(new Point(0, 0), window)!.Value;
        var fareX = seritSol.X + serit.Bounds.Width * 0.2;
        var ornekler = new List<(double Ms, double Yayilma, double Sol, double Sag)>();
        var saatDuvar = Stopwatch.StartNew();
        void Ornekle()
        {
            var maske = serit.OpacityMask as LinearGradientBrush;
            var duraklar = maske?.GradientStops.Select(s => s.Offset).ToList();
            ornekler.Add((saatDuvar.Elapsed.TotalMilliseconds, view.SeritSpread, duraklar?.First() ?? double.NaN, duraklar?.Last() ?? double.NaN));
        }
        void Yayildi(object? _, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == PlayerView.SeritSpreadProperty) Ornekle();
        }
        view.PropertyChanged += Yayildi;
        Hareket(window, view, new Point(fareX, yuzey.Bounds.Height - 4));
        var oran = view.SeritPointerX / serit.Bounds.Width;
        Hareket(window, view, new Point(seritSol.X + serit.Bounds.Width * 0.8, yuzey.Bounds.Height - 4));
        while (saatDuvar.Elapsed.TotalMilliseconds < 400)
        {
            Ornekle();
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
        view.PropertyChanged -= Yayildi;

        body.AppendLine($"  fare serit uzerinde oran {YolKanit.N(oran)}, serit acik {view.SeritRevealed}, ornek {ornekler.Count}");
        foreach (var o in ornekler.Where((_, i) => i % 8 == 0))
            body.AppendLine($"  {YolKanit.N(o.Ms)} ms: yayilma {YolKanit.N(o.Yayilma)}, maske [{YolKanit.N(o.Sol)} .. {YolKanit.N(o.Sag)}]");

        var maskeli = ornekler.Where(o => !double.IsNaN(o.Sol)).ToList();
        var kalkti = maskeli.Count > 0 && ornekler.Any(o => o.Ms > maskeli[^1].Ms && double.IsNaN(o.Sol) && o.Yayilma >= 1);
        body.AppendLine($"  maskeli ornek {maskeli.Count}, maske kalkti {kalkti}");
        var acildi = view.SeritRevealed;
        window.Close();
        return (acildi, oran, maskeli, kalkti);
    }
    [Fact]
    public void P28YanindakiAltyazilarKendiligindenYuklenir()
    {
        var body = new StringBuilder();
        try
        {
        AppHost.Run(() =>
        {
            var kok = Path.Combine(YolKanit.Folder, "p28");
            if (Directory.Exists(kok)) Directory.Delete(kok, true);
            Directory.CreateDirectory(kok);
            var film = Path.Combine(kok, "film.mp4");
            File.WriteAllBytes(film, new byte[16]);
            foreach (var ad in new[] { "film.srt", "film.tr.srt", "film-en.ass", "filmler.srt", "baska.srt", "film.txt" })
                File.WriteAllText(Path.Combine(kok, ad), "1\r\n00:00:00,000 --> 00:00:05,000\r\nalt yazi\r\n\r\n", new UTF8Encoding(false));

            var motor = new YolMotoru();
            var view = Ac(motor, out var window, film);
            var eklenen = motor.Eklenen.Select(Path.GetFileName).ToList();
            body.AppendLine($"sahte motor: eklenen {string.Join(" | ", eklenen)}; iz {string.Join(" | ", view.Trace.Where(t => t.StartsWith("subauto", StringComparison.Ordinal)))}");
            window.Close();
            Assert.Equal(new[] { "film-en.ass", "film.tr.srt", "film.srt" }, eklenen);

            var klip = Path.Combine(kok, "klip.mp4");
            File.Copy(KisayolKanit.IkiRenk, klip, true);
            File.WriteAllText(Path.Combine(kok, "klip.srt"), "1\r\n00:00:00,000 --> 00:00:05,000\r\nalt yazi\r\n\r\n", new UTF8Encoding(false));
            var gercek = DenetimSurucu.Ac(klip, out var pencere);
            var mpv = DenetimSurucu.Motor(gercek);
            var izler = mpv.Tracks.Where(t => t.Kind == PlaybackTrackKind.Subtitle).ToList();
            body.AppendLine($"libmpv: altyazi izi {izler.Count} ({string.Join(" | ", izler.Select(t => $"{t.Id} dis {t.External}"))}), secili {mpv.SubtitleTrack}, motor sid {mpv.GetProperty("sid")}");
            var sayi = izler.Count(t => t.External);
            var secili = mpv.SubtitleTrack;
            gercek.Close();
            pencere.Close();

            Assert.Equal(1, sayi);
            Assert.True(secili > 0, "yuklenen altyazi secili degil");
            return 0;
        });
        }
        finally
        {
            YolKanit.Write("p28-altyazi-otomatik.txt", body.ToString());
        }
    }
}
