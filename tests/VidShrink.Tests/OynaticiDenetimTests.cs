using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class DenetimKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "dalga1");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
        => File.WriteAllText(Path.Combine(Folder, name), body, new UTF8Encoding(false));

    internal static string N(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}

internal static class DenetimSurucu
{
    internal static void Pump(PlayerView view, Func<bool> done, double seconds)
    {
        var saat = Stopwatch.StartNew();
        while (!done() && saat.Elapsed.TotalSeconds < seconds)
        {
            Dispatcher.UIThread.RunJobs();
            view.RenderLatest();
            Thread.Sleep(5);
        }
    }

    internal static void Wait(PlayerView view, double seconds)
    {
        var saat = Stopwatch.StartNew();
        Pump(view, () => saat.Elapsed.TotalSeconds >= seconds, seconds + 1);
    }

    internal static PlayerView Ac(string clip, out Window window, string? history = null)
    {
        var view = new PlayerView
        {
            EngineFactory = () =>
            {
                var engine = new MpvEngine();
                engine.SetProperty("ao", "null");
                return engine;
            }
        };
        if (history is not null) view.HistoryPath = () => history;
        window = new Window { Width = 640, Height = 480, Content = view };

        var open = view.OpenAsync(clip);
        Pump(view, () => open.IsCompleted, 20);
        open.GetAwaiter().GetResult();
        Pump(view, () => view.Seek.Idle.IsCompleted && view.Engine!.FramesRendered > 0, 10);
        return view;
    }

    internal static MpvEngine Motor(PlayerView view) => (MpvEngine)view.Engine!;

    internal static double Konum(PlayerView view) => MotorKanit.ReadDouble(Motor(view), "time-pos");

    internal static void Duraklat(PlayerView view)
    {
        if (view.IsPlaying) GirdiSurucu.Key(view, Key.Space);
        Pump(view, () => Motor(view).IsPaused, 3);
    }

    internal static void Git(PlayerView view, double at)
    {
        view.Seek.GoTo(at);
        Bekle(view);
    }

    internal static void Bekle(PlayerView view)
    {
        Pump(view, () => view.Seek.Idle.IsCompleted, 10);
        Wait(view, 0.15);
    }
}

public sealed class KeymapTests
{
    private static readonly Dictionary<PlayerCommandKind, string> Iz = new()
    {
        [PlayerCommandKind.Seek] = "seek ",
        [PlayerCommandKind.Zoom] = "zoom ",
        [PlayerCommandKind.TogglePlay] = "play -> ",
        [PlayerCommandKind.ToggleFullscreen] = "fullscreen -> ",
        [PlayerCommandKind.ContextMenu] = "menu",
        [PlayerCommandKind.LeaveFullscreen] = "leavefullscreen -> ",
        [PlayerCommandKind.ResetZoom] = "zoomreset -> ",
        [PlayerCommandKind.Volume] = "volume ",
        [PlayerCommandKind.ToggleMute] = "mute -> ",
        [PlayerCommandKind.Speed] = "speed ",
        [PlayerCommandKind.SpeedReset] = "speedreset -> ",
        [PlayerCommandKind.FrameStep] = "frame ",
        [PlayerCommandKind.LoopStart] = "loop a -> ",
        [PlayerCommandKind.LoopEnd] = "loop b -> ",
        [PlayerCommandKind.LoopClear] = "loopclear",
        [PlayerCommandKind.BookmarkAdd] = "bookmarkadd -> ",
        [PlayerCommandKind.BookmarkNext] = "bookmarknext -> "
    };

    private static void Tetikle(PlayerView view, PlayerInput input)
    {
        switch (input.Kind)
        {
            case PlayerInputKind.Wheel:
                GirdiSurucu.Wheel(view, 1, input.Modifiers);
                break;
            case PlayerInputKind.DoubleClick:
                GirdiSurucu.Press(view, PointerUpdateKind.LeftButtonPressed, RawInputModifiers.LeftMouseButton, 2);
                break;
            case PlayerInputKind.Press when input.Button == PlayerButton.Middle:
                GirdiSurucu.Press(view, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
                break;
            case PlayerInputKind.Press:
                GirdiSurucu.Press(view, PointerUpdateKind.RightButtonPressed, RawInputModifiers.RightMouseButton);
                break;
            default:
                GirdiSurucu.Key(view, input.Key, input.Modifiers, input.Symbol);
                break;
        }
    }

    private static string Durum(PlayerView view) => FormattableString.Invariant(
        $"konum {view.PositionSeconds:0.###} ses {view.VolumeLevel:0.###} sessiz {view.IsMuted} hiz {view.SpeedFactor:0.##} oynatma {view.IsPlaying} tam {view.Fullscreen.IsFullscreen} buyutme {view.ZoomScale:0.###} A {view.LoopStart:0.###} B {view.LoopEnd:0.###}");

    private static string? Etki(PlayerView view, PlayerAction action, Action tetik)
    {
        const double Baslangic = 1000;
        view.Seek.Duration = 100000;
        view.Seek.GoTo(Baslangic);
        view.Apply(new PlayerCommand(PlayerCommandKind.Volume, -50));
        if (action.Command == PlayerCommandKind.SpeedReset) view.Apply(new PlayerCommand(PlayerCommandKind.Speed, 0.5));
        if (action.Command == PlayerCommandKind.LeaveFullscreen) view.Apply(new PlayerCommand(PlayerCommandKind.ToggleFullscreen, 0));
        if (action.Command == PlayerCommandKind.LoopClear) view.Apply(new PlayerCommand(PlayerCommandKind.LoopStart, 0));

        var izOnce = view.Trace.Count;
        var konum = view.PositionSeconds;
        var ses = view.VolumeLevel;
        var hiz = view.SpeedFactor;
        var sessiz = view.IsMuted;
        var oynatma = view.IsPlaying;
        var tam = view.Fullscreen.IsFullscreen;
        var buyutme = view.ZoomScale;

        tetik();

        var yeni = view.Trace.Skip(izOnce).ToList();
        if (yeni.Count != 1) return $"iz satiri {yeni.Count}: {string.Join(" | ", yeni)}";
        if (!yeni[0].StartsWith(Iz[action.Command], StringComparison.Ordinal)) return $"iz '{yeni[0]}' beklenen onek '{Iz[action.Command]}'";

        return action.Command switch
        {
            PlayerCommandKind.Seek when Math.Abs(view.PositionSeconds - konum - action.Amount) > 1e-9 => $"konum {konum} -> {view.PositionSeconds}, beklenen fark {action.Amount}",
            PlayerCommandKind.Volume when Math.Abs(view.VolumeLevel - ses - action.Amount) > 1e-9 => $"ses {ses} -> {view.VolumeLevel}, beklenen fark {action.Amount}",
            PlayerCommandKind.Speed when Math.Abs(view.SpeedFactor - hiz - action.Amount) > 1e-9 => $"hiz {hiz} -> {view.SpeedFactor}, beklenen fark {action.Amount}",
            PlayerCommandKind.SpeedReset when view.SpeedFactor != 1 || hiz == 1 => $"hiz {hiz} -> {view.SpeedFactor}",
            PlayerCommandKind.ToggleMute when view.IsMuted == sessiz => "sessiz degismedi",
            PlayerCommandKind.TogglePlay when view.IsPlaying == oynatma => "oynatma degismedi",
            PlayerCommandKind.ToggleFullscreen when view.Fullscreen.IsFullscreen == tam => "tam ekran degismedi",
            PlayerCommandKind.LeaveFullscreen when !tam || view.Fullscreen.IsFullscreen => $"tam ekran {tam} -> {view.Fullscreen.IsFullscreen}",
            PlayerCommandKind.Zoom when view.ZoomScale == buyutme => "buyutme degismedi",
            PlayerCommandKind.FrameStep when yeni[0] != "frame " + action.Amount.ToString("0.###") => $"iz '{yeni[0]}' yon {action.Amount}",
            PlayerCommandKind.LoopStart when view.LoopStart != konum => $"A {view.LoopStart}, konum {konum}",
            PlayerCommandKind.LoopEnd when view.LoopEnd != konum => $"B {view.LoopEnd}, konum {konum}",
            PlayerCommandKind.LoopClear when double.IsFinite(view.LoopStart) || double.IsFinite(view.LoopEnd) => "dongu kalkmadi",
            _ => null
        };
    }

    [Fact]
    public void HerKisayolSatiriGercekOlaylaKendiKomutunaBaglanir()
    {
        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var hatalar = new List<string>();

            foreach (var row in Keymap.Rows)
            {
                var view = GirdiSurucu.Kur(out var window);
                var once = Durum(view);
                var hata = Etki(view, row.Action, () => Tetikle(view, row.Input));
                body.AppendLine($"{Keymap.Gesture(row.Input),-18} {row.Action.Command,-16} {DenetimKanit.N(row.Action.Amount),6} | iz {view.Trace[^1]} | {Durum(view)}");
                if (hata is not null) hatalar.Add($"{Keymap.Gesture(row.Input)} -> {row.Action.Command}: {hata}");
                window.Close();
            }

            body.AppendLine($"satir: {Keymap.Rows.Count}, hata: {hatalar.Count}");
            foreach (var hata in hatalar) body.AppendLine("HATA " + hata);
            return (body.ToString(), hatalar);
        });

        DenetimKanit.Write("keymap-olay-pini.txt", rapor.Item1);
        Assert.True(rapor.hatalar.Count == 0, string.Join(Environment.NewLine, rapor.hatalar));
    }

    [Fact]
    public void KisayolTablosundaCakismaYokVeHerKomutErisilebilir()
    {
        var akorlar = Keymap.Rows
            .GroupBy(row => (row.Input.Kind, row.Input.Key, row.Input.Button, row.Input.Modifiers))
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(r => r.Action.Command))}")
            .ToList();

        var semboller = Keymap.Rows
            .Where(row => row.Input.Symbol is not null)
            .GroupBy(row => row.Input.Symbol)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key!)
            .ToList();

        var golgelenen = Keymap.Rows
            .Where(row => row.Input.Kind == PlayerInputKind.Key)
            .Where(row => Keymap.ForKey(row.Input.Key, row.Input.Modifiers, row.Input.Symbol) != row.Action.ToCommand())
            .Select(row => Keymap.Gesture(row.Input))
            .ToList();

        var erisilen = Keymap.Rows.Select(row => row.Action.Command)
            .Concat(Keymap.MenuActions.Select(action => action.Command))
            .ToHashSet();
        var erisilmeyen = Enum.GetValues<PlayerCommandKind>()
            .Where(kind => kind != PlayerCommandKind.None && !erisilen.Contains(kind))
            .ToList();
        var izsiz = Enum.GetValues<PlayerCommandKind>()
            .Where(kind => kind != PlayerCommandKind.None && !Iz.ContainsKey(kind))
            .ToList();

        DenetimKanit.Write("keymap-cakisma.txt",
            $"satir: {Keymap.Rows.Count}{Environment.NewLine}"
            + $"ayni akor: {(akorlar.Count == 0 ? "yok" : string.Join(" | ", akorlar))}{Environment.NewLine}"
            + $"ayni sembol: {(semboller.Count == 0 ? "yok" : string.Join(" | ", semboller))}{Environment.NewLine}"
            + $"golgelenen tus satiri: {(golgelenen.Count == 0 ? "yok" : string.Join(" | ", golgelenen))}{Environment.NewLine}"
            + $"erisilmeyen komut: {(erisilmeyen.Count == 0 ? "yok" : string.Join(", ", erisilmeyen))}{Environment.NewLine}");

        Assert.Empty(akorlar);
        Assert.Empty(semboller);
        Assert.Empty(golgelenen);
        Assert.Empty(erisilmeyen);
        Assert.Empty(izsiz);
    }

    [Fact]
    public void SembolSatirlariKlavyeDuzenindenBagimsizCalisir()
    {
        var rapor = AppHost.Run(() =>
        {
            var view = new PlayerView();
            var kutu = new TextBox();
            var window = new Window { Width = 640, Height = 480, Content = new StackPanel { Children = { kutu, view } } };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            view.Seek.Duration = 100;
            view.Seek.GoTo(4);
            var body = new StringBuilder();

            void Bas(string ad, Key key, KeyModifiers mods, string symbol)
            {
                var once = view.Trace.Count;
                GirdiSurucu.Key(view, key, mods, symbol);
                body.AppendLine($"{ad}: {(view.Trace.Count > once ? view.Trace[^1] : "IZ YOK")}");
            }

            Bas("tr q AltGr+8 '['", Key.D8, KeyModifiers.Control | KeyModifiers.Alt, "[");
            view.Seek.GoTo(9);
            Bas("tr q AltGr+9 ']'", Key.D9, KeyModifiers.Control | KeyModifiers.Alt, "]");
            Bas("tr q Shift+7 '/'", Key.D7, KeyModifiers.Shift, "/");

            var once = view.Trace.Count;
            kutu.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F });
            body.AppendLine($"metin kutusunda F: {(view.Trace.Count > once ? view.Trace[^1] : "yutulmadi")}");
            var yutuldu = view.Trace.Count > once;

            GirdiSurucu.Key(view, Key.Q);
            body.AppendLine($"bagsiz Q: {(view.Trace.Count > once ? view.Trace[^1] : "iz yok")}");
            var bagsiz = view.Trace.Count > once;

            window.Close();
            return (body.ToString(), view.Trace.ToList(), yutuldu, bagsiz);
        });

        DenetimKanit.Write("keymap-sembol.txt", rapor.Item1);
        Assert.Contains("loop a -> 4", rapor.Item2);
        Assert.Contains("loop b -> 9", rapor.Item2);
        Assert.Equal("loopclear", rapor.Item2[^1]);
        Assert.False(rapor.yutuldu);
        Assert.False(rapor.bagsiz);
    }

    [Fact]
    public void MenuVeAyarlarSayfasiAyniTablodanOkur()
    {
        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var view = GirdiSurucu.Kur(out var window);
            var panel = new PlayerShortcutsPanel();
            var host = new Window { Width = 900, Height = 1400, Content = panel };
            host.Show();
            var sonuc = new Dictionary<string, List<(string, string)>>();

            foreach (var dil in new[] { "en", "tr" })
            {
                Strings.Use(dil);
                Dispatcher.UIThread.RunJobs();

                var menu = view.BuildMenu().Items.OfType<MenuItem>().ToList();
                body.AppendLine($"[{dil}] menu {menu.Count} satir");
                Assert.Equal(Keymap.MenuActions.Count, menu.Count);
                foreach (var (item, action) in menu.Zip(Keymap.MenuActions))
                {
                    var satir = Keymap.FirstKeyRow(action);
                    body.AppendLine($"  {item.Header} | {item.InputGesture?.ToString() ?? "-"} | p: {item.InputGesture?.ToString("p", null) ?? "-"}");
                    Assert.Same(action, item.Tag);
                    Assert.Equal(Strings.Get(action.LabelKey), item.Header);
                    if (satir is not null) Assert.Equal(satir.Input.Symbol ?? new KeyGesture(satir.Input.Key, satir.Input.Modifiers).ToString("p", null), item.InputGesture?.ToString("p", null));
                    else Assert.Null(item.InputGesture);
                }

                var gorunen = panel.Shown.ToList();
                sonuc[dil] = gorunen;
                body.AppendLine($"[{dil}] ayarlar {gorunen.Count} satir");
                Assert.Equal(Keymap.Rows.Count, gorunen.Count);
                foreach (var ((jest, etiket), row) in gorunen.Zip(Keymap.Rows))
                {
                    body.AppendLine($"  {jest} | {etiket}");
                    Assert.Equal(Keymap.Gesture(row.Input), jest);
                    Assert.Equal(Keymap.Label(row), etiket);
                }
            }

            Strings.Use("en");
            var ayar = new MainWindow();
            var yerlesik = ayar.FindControl<PlayerShortcutsPanel>("PlayerShortcutsPanel");
            body.AppendLine($"ayarlar sayfasinda panel: {(yerlesik is null ? "yok" : yerlesik.Shown.Count + " satir")}");
            Assert.NotNull(yerlesik);
            Assert.Equal(Keymap.Rows.Count, yerlesik!.Shown.Count);

            ayar.Close();
            host.Close();
            window.Close();
            Assert.NotEqual(sonuc["en"][0], sonuc["tr"][0]);
            return body.ToString();
        });

        DenetimKanit.Write("keymap-menu-ayarlar.txt", rapor);
    }

    [Fact]
    public void SekmeAltKisayollariVePencereBaglariTabloylaCakismaz()
    {
        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var window = new MainWindow();
            var tablo = Keymap.Rows
                .Where(row => row.Input.Kind == PlayerInputKind.Key)
                .Select(row => (row.Input.Key, row.Input.Modifiers))
                .ToHashSet();
            var bulunan = new List<(string Kaynak, Key Key, KeyModifiers Mods)>();

            foreach (var dil in new[] { "en", "tr" })
            {
                Strings.Use(dil);
                Dispatcher.UIThread.RunJobs();
                foreach (var tab in window.Tabs.Items.OfType<TabItem>())
                {
                    var baslik = MainWindow.TabHeaderText(tab);
                    var alt = baslik.IndexOf('_');
                    body.AppendLine($"[{dil}] sekme '{baslik}' alt: {(alt >= 0 && alt + 1 < baslik.Length ? baslik[alt + 1].ToString() : "yok")}");
                    if (alt >= 0 && alt + 1 < baslik.Length && Enum.TryParse<Key>(char.ToUpperInvariant(baslik[alt + 1]).ToString(), out var harf))
                        bulunan.Add(($"{dil} sekme {baslik}", harf, KeyModifiers.Alt));
                }
            }

            foreach (var access in window.GetLogicalDescendants().OfType<AccessText>())
                if (!string.IsNullOrEmpty(access.AccessKey?.ToString()) && Enum.TryParse<Key>(access.AccessKey!.ToString()!.ToUpperInvariant(), out var harf))
                    bulunan.Add(($"AccessText '{access.Text}'", harf, KeyModifiers.Alt));

            foreach (var control in window.GetLogicalDescendants().OfType<Control>())
            {
                var hot = control switch
                {
                    Button button => button.HotKey,
                    MenuItem item => item.HotKey,
                    _ => null
                };
                if (hot is not null) bulunan.Add(($"HotKey {control.Name}", hot.Key, hot.KeyModifiers));
            }

            foreach (var binding in window.KeyBindings)
                if (binding.Gesture is { } gesture) bulunan.Add(("KeyBinding", gesture.Key, gesture.KeyModifiers));

            var carpisan = bulunan.Where(item => tablo.Contains((item.Key, item.Mods))).ToList();
            body.AppendLine($"pencere kisayolu: {bulunan.Count}");
            foreach (var item in bulunan) body.AppendLine($"  {item.Kaynak}: {item.Mods}+{item.Key}{(tablo.Contains((item.Key, item.Mods)) ? " CAKISIYOR" : "")}");
            body.AppendLine($"cakisan: {carpisan.Count}");

            Strings.Use("en");
            window.Close();
            return (body.ToString(), carpisan.Count);
        });

        DenetimKanit.Write("keymap-alt-cakisma.txt", rapor.Item1);
        Assert.Equal(0, rapor.Item2);
    }
}

public sealed class OynaticiDenetimMotorTests
{
    [Fact]
    public void IkiKatHizdaIkiSaniyedeDortSaniyeIlerler()
    {
        var clip = MotorKlipleri.Kucuk;
        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(clip, out var window);
            for (var i = 0; i < 10; i++) GirdiSurucu.Key(view, Key.C);
            var motorHizi = DenetimSurucu.Motor(view).Speed;

            DenetimSurucu.Wait(view, 0.5);
            var saat = Stopwatch.StartNew();
            var p0 = DenetimSurucu.Konum(view);
            DenetimSurucu.Wait(view, 2.0);
            var p1 = DenetimSurucu.Konum(view);
            var duvar = saat.Elapsed.TotalSeconds;

            GirdiSurucu.Key(view, Key.Z);
            var sifir = DenetimSurucu.Motor(view).Speed;
            view.Close();
            window.Close();
            return (view.SpeedFactor, motorHizi, p0, p1, duvar, sifir);
        });

        var ilerleme = (rapor.p1 - rapor.p0) * 2.0 / rapor.duvar;
        DenetimKanit.Write("hiz.txt",
            $"C x10 -> gorunum hizi {DenetimKanit.N(rapor.Item1)}, mpv speed {DenetimKanit.N(rapor.motorHizi)}{Environment.NewLine}"
            + $"time-pos {DenetimKanit.N(rapor.p0)} -> {DenetimKanit.N(rapor.p1)} / duvar {DenetimKanit.N(rapor.duvar)} sn{Environment.NewLine}"
            + $"2 sn duvara olceklenmis ilerleme: {DenetimKanit.N(ilerleme)} sn (hedef 4 +-%5){Environment.NewLine}"
            + $"Z -> mpv speed {DenetimKanit.N(rapor.sifir)}{Environment.NewLine}");

        Assert.Equal(2, rapor.motorHizi, 3);
        Assert.InRange(ilerleme, 4 * 0.95, 4 * 1.05);
        Assert.Equal(1, rapor.sifir, 3);
    }

    [Fact]
    public void KareAdimiBirKareIleriVeGeriGider()
    {
        var clip = MotorKlipleri.Kucuk;
        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(clip, out var window);
            DenetimSurucu.Duraklat(view);
            DenetimSurucu.Git(view, 5);
            var fps = DenetimSurucu.Motor(view).FramesPerSecond;

            var p0 = DenetimSurucu.Konum(view);
            GirdiSurucu.Key(view, Key.F);
            DenetimSurucu.Pump(view, () => Math.Abs(DenetimSurucu.Konum(view) - p0) > 1e-4, 3);
            DenetimSurucu.Wait(view, 0.2);
            var p1 = DenetimSurucu.Konum(view);
            var oynuyor = view.IsPlaying;

            GirdiSurucu.Key(view, Key.F, KeyModifiers.Shift);
            DenetimSurucu.Pump(view, () => Math.Abs(DenetimSurucu.Konum(view) - p1) > 1e-4, 3);
            DenetimSurucu.Wait(view, 0.2);
            var p2 = DenetimSurucu.Konum(view);

            view.Close();
            window.Close();
            return (fps, p0, p1, p2, oynuyor);
        });

        var kare = 1 / rapor.fps;
        var ileri = rapor.p1 - rapor.p0;
        var geri = rapor.p2 - rapor.p1;
        DenetimKanit.Write("kare-adimi.txt",
            $"fps {DenetimKanit.N(rapor.fps)}, 1/fps {DenetimKanit.N(kare)} sn{Environment.NewLine}"
            + $"F: {DenetimKanit.N(rapor.p0)} -> {DenetimKanit.N(rapor.p1)} = {DenetimKanit.N(ileri)} sn ({DenetimKanit.N(ileri / kare)} kare), sonra oynatma {rapor.oynuyor}{Environment.NewLine}"
            + $"Shift+F: {DenetimKanit.N(rapor.p1)} -> {DenetimKanit.N(rapor.p2)} = {DenetimKanit.N(geri)} sn ({DenetimKanit.N(geri / kare)} kare){Environment.NewLine}");

        Assert.InRange(rapor.fps, 29, 31);
        Assert.InRange(ileri, kare * 0.9, kare * 1.1);
        Assert.InRange(geri, -kare * 1.1, -kare * 0.9);
        Assert.False(rapor.oynuyor);
    }

    [Fact]
    public void AbDongusuSinirlarinDisinaTasmaz()
    {
        var clip = MotorKlipleri.Kucuk;
        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(clip, out var window);
            DenetimSurucu.Duraklat(view);
            DenetimSurucu.Git(view, 5);
            GirdiSurucu.Key(view, Key.OemOpenBrackets, KeyModifiers.None, "[");
            DenetimSurucu.Git(view, 7);
            GirdiSurucu.Key(view, Key.OemCloseBrackets, KeyModifiers.None, "]");
            var motor = DenetimSurucu.Motor(view);
            var a = motor.LoopStartSeconds;
            var b = motor.LoopEndSeconds;
            var fps = motor.FramesPerSecond;

            GirdiSurucu.Key(view, Key.Space);
            var ornek = new List<double>();
            var saat = Stopwatch.StartNew();
            while (saat.Elapsed.TotalSeconds < 6)
            {
                Dispatcher.UIThread.RunJobs();
                view.RenderLatest();
                var p = DenetimSurucu.Konum(view);
                if (double.IsFinite(p)) ornek.Add(p);
                Thread.Sleep(10);
            }

            GirdiSurucu.Key(view, Key.Oem2, KeyModifiers.None, "/");
            var temiz = double.IsNaN(motor.LoopStartSeconds) && double.IsNaN(motor.LoopEndSeconds);
            view.Close();
            window.Close();
            return (a, b, fps, ornek, temiz);
        });

        var sonra = rapor.ornek.SkipWhile(p => p >= rapor.b - 0.5).ToList();
        var donus = rapor.ornek.Zip(rapor.ornek.Skip(1)).Count(pair => pair.Second < pair.First - 1);
        var ust = sonra.Count == 0 ? double.NaN : sonra.Max();
        var alt = sonra.Count == 0 ? double.NaN : sonra.Min();
        var kare = 1 / rapor.fps;
        DenetimKanit.Write("ab-dongu.txt",
            $"mpv ab-loop-a {DenetimKanit.N(rapor.a)}, ab-loop-b {DenetimKanit.N(rapor.b)}, 1/fps {DenetimKanit.N(kare)}{Environment.NewLine}"
            + $"6 sn ornek {rapor.ornek.Count}, donus {donus}{Environment.NewLine}"
            + $"ilk donusten sonra en kucuk {DenetimKanit.N(alt)}, en buyuk {DenetimKanit.N(ust)} (tavan B + 1 kare = {DenetimKanit.N(rapor.b + kare)}){Environment.NewLine}"
            + $"/ sonrasi dongu kalkti: {rapor.temiz}{Environment.NewLine}");

        Assert.Equal(5, rapor.a, 1);
        Assert.Equal(7, rapor.b, 1);
        Assert.True(donus >= 2, $"donus {donus}");
        Assert.True(rapor.ornek.Max() <= rapor.b + kare + 1e-3, $"en buyuk {rapor.ornek.Max()} > B + 1 kare");
        Assert.True(alt >= rapor.a - kare - 1e-3, $"en kucuk {alt} < A - 1 kare");
        Assert.True(rapor.temiz);
    }

    [Fact]
    public void YenidenAcinincaKaldigiYerdenDevamEder()
    {
        var clip = MotorKlipleri.Kucuk;
        var gecmis = Path.Combine(DenetimKanit.Folder, "gecmis-devam.json");
        if (File.Exists(gecmis)) File.Delete(gecmis);

        var rapor = AppHost.Run(() =>
        {
            var ilk = DenetimSurucu.Ac(clip, out var ilkPencere, gecmis);
            DenetimSurucu.Duraklat(ilk);
            DenetimSurucu.Git(ilk, 12.3);
            var kapanis = DenetimSurucu.Konum(ilk);
            ilk.Close();
            ilkPencere.Close();
            var dosya = File.Exists(gecmis) ? File.ReadAllText(gecmis) : "YOK";

            var ikinci = DenetimSurucu.Ac(clip, out var ikinciPencere, gecmis);
            var acilis = DenetimSurucu.Konum(ikinci);
            var iz = string.Join(" | ", ikinci.Trace);
            ikinci.Close();
            ikinciPencere.Close();
            return (kapanis, acilis, dosya, iz);
        });

        DenetimKanit.Write("devam.txt",
            $"kapanista time-pos {DenetimKanit.N(rapor.kapanis)}{Environment.NewLine}"
            + $"yeniden acilista time-pos {DenetimKanit.N(rapor.acilis)} (fark {DenetimKanit.N(rapor.acilis - rapor.kapanis)} sn){Environment.NewLine}"
            + $"iz: {rapor.iz}{Environment.NewLine}"
            + $"gecmis dosyasi:{Environment.NewLine}{rapor.dosya}{Environment.NewLine}");

        Assert.InRange(rapor.kapanis, 12.2, 12.4);
        Assert.InRange(rapor.acilis, rapor.kapanis - 1, rapor.kapanis + 1);
    }

    [Fact]
    public void YerImleriKaydedilirSiraylaGezilirVeKalir()
    {
        var clip = MotorKlipleri.Kucuk;
        var gecmis = Path.Combine(DenetimKanit.Folder, "gecmis-yerimi.json");
        if (File.Exists(gecmis)) File.Delete(gecmis);

        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(clip, out var window, gecmis);
            DenetimSurucu.Duraklat(view);
            DenetimSurucu.Git(view, 8);
            GirdiSurucu.Key(view, Key.N);
            DenetimSurucu.Git(view, 15);
            GirdiSurucu.Key(view, Key.N);
            DenetimSurucu.Git(view, 1);

            var gidilen = new List<double>();
            for (var i = 0; i < 3; i++)
            {
                GirdiSurucu.Key(view, Key.B);
                DenetimSurucu.Bekle(view);
                gidilen.Add(DenetimSurucu.Konum(view));
            }

            view.Close();
            window.Close();

            var ikinci = DenetimSurucu.Ac(clip, out var ikinciPencere, gecmis);
            var kalan = ikinci.History.Bookmarks(clip).ToList();
            ikinci.Close();
            ikinciPencere.Close();
            return (gidilen, kalan);
        });

        DenetimKanit.Write("yer-imi.txt",
            $"N @8, N @15, sonra 1'den B x3 -> {string.Join(", ", rapor.gidilen.Select(DenetimKanit.N))}{Environment.NewLine}"
            + $"yeniden acilista yer imleri: {string.Join(", ", rapor.kalan.Select(DenetimKanit.N))}{Environment.NewLine}");

        Assert.Equal(3, rapor.gidilen.Count);
        Assert.InRange(rapor.gidilen[0], 7.9, 8.1);
        Assert.InRange(rapor.gidilen[1], 14.9, 15.1);
        Assert.InRange(rapor.gidilen[2], 7.9, 8.1);
        Assert.Equal(2, rapor.kalan.Count);
    }

    [Fact]
    public void SesVeSessizMotordanGeriOkunur()
    {
        var clip = MotorKlipleri.Kucuk;
        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(clip, out var window);
            var motor = DenetimSurucu.Motor(view);
            var satirlar = new List<(string Ad, double Ses, bool Sessiz)>();
            void Oku(string ad) => satirlar.Add((ad, DenetimSurucu.Motor(view).Volume, DenetimSurucu.Motor(view).Muted));

            Oku("acilis");
            GirdiSurucu.Key(view, Key.Down);
            GirdiSurucu.Key(view, Key.Down);
            Oku("asagi x2");
            GirdiSurucu.Wheel(view, -1, KeyModifiers.None);
            Oku("tekerlek -1");
            GirdiSurucu.Key(view, Key.Up);
            Oku("yukari");
            GirdiSurucu.Key(view, Key.M);
            Oku("M");
            GirdiSurucu.Key(view, Key.M);
            Oku("M");

            var open = view.OpenAsync(clip);
            DenetimSurucu.Pump(view, () => open.IsCompleted, 20);
            open.GetAwaiter().GetResult();
            Oku("ikinci dosya");

            for (var i = 0; i < 5; i++) GirdiSurucu.Key(view, Key.Up);
            Oku("yukari x5");

            view.Close();
            window.Close();
            return satirlar;
        });

        DenetimKanit.Write("ses.txt", string.Join(Environment.NewLine,
            rapor.Select(s => $"{s.Ad}: mpv volume {DenetimKanit.N(s.Ses)}, mute {s.Sessiz}")) + Environment.NewLine);

        Assert.Equal(100, rapor[0].Ses, 3);
        Assert.Equal(90, rapor[1].Ses, 3);
        Assert.Equal(85, rapor[2].Ses, 3);
        Assert.Equal(90, rapor[3].Ses, 3);
        Assert.True(rapor[4].Sessiz);
        Assert.False(rapor[5].Sessiz);
        Assert.Equal(90, rapor[6].Ses, 3);
        Assert.Equal(100, rapor[7].Ses, 3);
    }

    [Fact]
    public void AtlamaAdimlariMotordaOlculur()
    {
        var clip = MotorKlipleri.Kucuk;
        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(clip, out var window);
            DenetimSurucu.Duraklat(view);
            DenetimSurucu.Git(view, 2);
            var satirlar = new List<(string Ad, double Beklenen, double Olculen)>();

            void Adim(string ad, double beklenen, Action girdi)
            {
                girdi();
                DenetimSurucu.Bekle(view);
                satirlar.Add((ad, beklenen, DenetimSurucu.Konum(view)));
            }

            Adim("sag ok", 12, () => GirdiSurucu.Key(view, Key.Right));
            Adim("sol ok", 2, () => GirdiSurucu.Key(view, Key.Left));
            Adim("alt+sag", 3, () => GirdiSurucu.Key(view, Key.Right, KeyModifiers.Alt));
            Adim("ctrl+tekerlek", 13, () => GirdiSurucu.Wheel(view, 1, KeyModifiers.Control));
            Adim("alt+sol", 12, () => GirdiSurucu.Key(view, Key.Left, KeyModifiers.Alt));
            Adim("ctrl+tekerlek geri", 2, () => GirdiSurucu.Wheel(view, -1, KeyModifiers.Control));
            Adim("ctrl+sol", 0, () => GirdiSurucu.Key(view, Key.Left, KeyModifiers.Control));

            view.Close();
            window.Close();
            return satirlar;
        });

        DenetimKanit.Write("atlama.txt", string.Join(Environment.NewLine,
            rapor.Select(s => $"{s.Ad}: beklenen {DenetimKanit.N(s.Beklenen)}, mpv time-pos {DenetimKanit.N(s.Olculen)}")) + Environment.NewLine);

        Assert.All(rapor, s => Assert.InRange(s.Olculen, s.Beklenen - 0.05, s.Beklenen + 0.05));
    }
}
