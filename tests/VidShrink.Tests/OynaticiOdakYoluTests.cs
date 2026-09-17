using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

public sealed class OynaticiOdakYoluTests
{
    private const BindingFlags Her = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static object Klavye()
    {
        var tur = typeof(KeyboardDevice);
        return tur.GetProperty("Instance", Her)?.GetValue(null)
               ?? Activator.CreateInstance(tur, Her, null, Array.Empty<object>(), null)!;
    }

    private static void Gonder(Window window, RawKeyEventType tur, Key key, RawInputModifiers mods, string? symbol)
    {
        var impl = typeof(TopLevel).GetProperty("PlatformImpl", Her)!.GetValue(window)!;
        var kok = (IInputRoot)typeof(TopLevel).GetProperty("InputRoot", Her)!.GetValue(window)!;
        var giris = (Action<RawInputEventArgs>)impl.GetType().GetInterfaces()
            .Select(i => i.GetProperty("Input", Her)).First(p => p is not null)!.GetValue(impl)!;
        var args = Activator.CreateInstance(typeof(RawKeyEventArgs), Her, null,
            new object?[] { Klavye(), (ulong)Environment.TickCount64, kok, tur, key, mods, PhysicalKey.None, symbol, KeyDeviceType.Keyboard }, null)!;
        giris((RawInputEventArgs)args);
    }

    private static readonly object Fare = Activator.CreateInstance(typeof(MouseDevice), Her, null, new object[] { new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, true) }, null)!;

    private static void Tikla(Window window, PlayerView view, Control hedef)
    {
        var impl = typeof(TopLevel).GetProperty("PlatformImpl", Her)!.GetValue(window)!;
        var kok = (IInputRoot)typeof(TopLevel).GetProperty("InputRoot", Her)!.GetValue(window)!;
        var giris = (Action<RawInputEventArgs>)impl.GetType().GetInterfaces()
            .Select(i => i.GetProperty("Input", Her)).First(p => p is not null)!.GetValue(impl)!;
        var nokta = hedef.TranslatePoint(new Point(hedef.Bounds.Width / 2, hedef.Bounds.Height / 2), window)!.Value;
        void Olay(RawPointerEventType tur, RawInputModifiers tuslar)
        {
            giris((RawInputEventArgs)Activator.CreateInstance(typeof(RawPointerEventArgs), Her, null,
                new object[] { Fare, (ulong)Environment.TickCount64, kok, tur, nokta, tuslar }, null)!);
            DenetimSurucu.Wait(view, 0.05);
        }
        Olay(RawPointerEventType.Move, RawInputModifiers.None);
        Olay(RawPointerEventType.LeftButtonDown, RawInputModifiers.LeftMouseButton);
        Olay(RawPointerEventType.LeftButtonUp, RawInputModifiers.None);
        DenetimSurucu.Wait(view, 0.2);
    }

    private static void Tus(Window window, PlayerView view, Key key, RawInputModifiers mods = RawInputModifiers.None, string? symbol = null)
    {
        Gonder(window, RawKeyEventType.KeyDown, key, mods, symbol);
        Gonder(window, RawKeyEventType.KeyUp, key, mods, symbol);
        DenetimSurucu.Wait(view, 0.15);
    }

    private static string Odakta(Window window)
    {
        var odak = window.FocusManager?.GetFocusedElement();
        return odak switch
        {
            null => "yok",
            Control c => c.GetType().Name + (string.IsNullOrEmpty(c.Name) ? "" : "#" + c.Name),
            _ => odak.GetType().Name
        };
    }

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    [Fact]
    public void AnaPencereOdakYolundanTuslarMotoraUlasir()
    {
        var kok = Path.Combine(KisayolKanit.Folder, "ana-pencere");
        if (Directory.Exists(kok)) Directory.Delete(kok, true);
        Directory.CreateDirectory(kok);

        var (kayit, hatalar) = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var hata = new List<string>();
            var window = new MainWindow { SettingsPathOverride = Path.Combine(kok, "settings.json"), Width = 1280, Height = 800 };
            var view = window.PlayerTab;
            view.EngineFactory = () =>
            {
                var engine = new MpvEngine();
                engine.SetProperty("ao", "null");
                return engine;
            };
            try
            {
                window.Show();
                DenetimSurucu.Wait(view, 0.3);
                var ac = window.OpenInPlayerAsync(KisayolKanit.Uzun);
                DenetimSurucu.Pump(view, () => ac.IsCompleted, 20);
                DenetimSurucu.Pump(view, () => view.Engine is { FramesRendered: > 0 }, 10);
                var motor = DenetimSurucu.Motor(view);
                string Oku(string ad) => motor.GetProperty(ad) ?? "";
                double Sayi(string ad) => double.Parse(Oku(ad), CultureInfo.InvariantCulture);
                DenetimSurucu.Duraklat(view);
                DenetimSurucu.Git(view, 300);

                void Olc(string durum, Action hazirla, Key key, RawInputModifiers mods, string? symbol, Func<string> oku, Func<string, string, bool> degisti)
                {
                    hazirla();
                    DenetimSurucu.Wait(view, 0.2);
                    var odak = Odakta(window);
                    var once = oku();
                    Tus(window, view, key, mods, symbol);
                    DenetimSurucu.Bekle(view);
                    DenetimSurucu.Pump(view, () => degisti(once, oku()), 3);
                    var sonra = oku();
                    var ok = degisti(once, sonra);
                    body.AppendLine($"{durum}: sekme {window.Tabs.SelectedIndex}, odak {odak}, tus {mods}+{key}: {once} -> {sonra} {(ok ? "GECTI" : "KALDI")}");
                    if (!ok) hata.Add(durum);
                }

                bool Farkli(string a, string b) => a != b;

                Olc("sekme-degisimi-combobox",
                    () =>
                    {
                        window.Tabs.SelectedItem = window.TabSettings;
                        DenetimSurucu.Wait(view, 0.3);
                        var kutu = window.GetVisualDescendants().OfType<ComboBox>().First(c => c.IsEffectivelyVisible && c.IsEnabled && c.Bounds.Width > 0);
                        Tikla(window, view, kutu);
                        body.AppendLine($"  ayarlar sekmesinde tiklandi {kutu.Name}, acik {kutu.IsDropDownOpen}, odak {Odakta(window)}");
                        kutu.IsDropDownOpen = false;
                        DenetimSurucu.Wait(view, 0.2);
                        body.AppendLine($"  acilir kapandi, odak {Odakta(window)}");
                        Tikla(window, view, window.TabPlayer);
                    },
                    Key.M, RawInputModifiers.None, "m", () => Oku("mute"), Farkli);

                Olc("sekme-basligi-odakta",
                    () => Tikla(window, view, window.TabPlayer),
                    Key.Right, RawInputModifiers.None, null, () => F(Math.Round(Sayi("time-pos"))) + " sekme " + window.Tabs.SelectedIndex,
                    (a, b) => b.EndsWith("sekme " + window.PlayerTabIndex, StringComparison.Ordinal) && a != b);

                Olc("ses-surgusu-odakta-sag-ok",
                    () => view.FindControl<Slider>("SliderSeritVolume")!.Focus(NavigationMethod.Tab),
                    Key.Right, RawInputModifiers.None, null, () => F(Math.Round(Sayi("time-pos"))) + " ses " + F(Sayi("volume")),
                    (a, b) => a.Split(' ')[0] != b.Split(' ')[0] && a.Split(' ')[2] == b.Split(' ')[2]);

                Olc("hiz-surgusu-odakta-bosluk",
                    () => view.FindControl<Slider>("SliderSeritSpeed")!.Focus(NavigationMethod.Tab),
                    Key.Space, RawInputModifiers.None, " ", () => Oku("pause"), Farkli);
                if (!motor.IsPaused) view.Apply(new PlayerCommand(PlayerCommandKind.TogglePlay, 0));

                Olc("oynat-dugmesi-odakta-enter",
                    () => view.FindControl<Button>("BtnSeritPlay")!.Focus(NavigationMethod.Tab),
                    Key.Enter, RawInputModifiers.None, "\r", () => window.WindowState + " pause " + Oku("pause"),
                    (a, b) => b.StartsWith("FullScreen", StringComparison.Ordinal) && a.Split(' ')[2] == b.Split(' ')[2]);

                Olc("tam-ekranda-sessiz",
                    () => { },
                    Key.M, RawInputModifiers.None, "m", () => Oku("mute"), Farkli);

                Olc("tam-ekranda-dondur",
                    () => { },
                    Key.S, RawInputModifiers.Control | RawInputModifiers.Shift, null, () => motor.Rotation.ToString(CultureInfo.InvariantCulture), Farkli);

                Olc("tam-ekrandan-esc",
                    () => { },
                    Key.Escape, RawInputModifiers.None, null, () => window.WindowState.ToString(), (a, b) => a == "FullScreen" && b != "FullScreen");

                body.AppendLine($"son: WindowState {window.WindowState}, Rotation {motor.Rotation}");
            }
            finally
            {
                view.Close();
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return (body.ToString(), hata);
        });

        KisayolKanit.Write("odak-yolu.txt", kayit);
        Assert.True(hatalar.Count == 0, kayit);
    }

    private static Action<RawInputEventArgs>? Giris(object impl)
        => (Action<RawInputEventArgs>?)impl.GetType().GetInterfaces()
            .Select(i => i.GetProperty("Input", Her)).First(p => p is not null)!.GetValue(impl);

    private static void Ham(TopLevel top, RawInputEventArgs args)
        => Giris(typeof(TopLevel).GetProperty("PlatformImpl", Her)!.GetValue(top)!)!(args);

    private static IInputRoot Kok(TopLevel top) => (IInputRoot)typeof(TopLevel).GetProperty("InputRoot", Her)!.GetValue(top)!;

    private static void HamFare(TopLevel top, RawPointerEventType tur, Point nokta, RawInputModifiers tuslar)
        => Ham(top, (RawInputEventArgs)Activator.CreateInstance(typeof(RawPointerEventArgs), Her, null,
            new object[] { Fare, (ulong)Environment.TickCount64, Kok(top), tur, nokta, tuslar }, null)!);

    private static void HamTus(TopLevel top, Key key, RawInputModifiers mods, string? symbol)
    {
        foreach (var tur in new[] { RawKeyEventType.KeyDown, RawKeyEventType.KeyUp })
        {
            if (typeof(TopLevel).GetProperty("PlatformImpl", Her)!.GetValue(top) is null) return;
            Ham(top, (RawInputEventArgs)Activator.CreateInstance(typeof(RawKeyEventArgs), Her, null,
                new object?[] { Klavye(), (ulong)Environment.TickCount64, Kok(top), tur, key, mods, PhysicalKey.None, symbol, KeyDeviceType.Keyboard }, null)!);
        }
    }

    private static Point Orta(TopLevel top, Visual control)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), top)!.Value;

    private static void Dongu(Func<bool> bitti, double saniye)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed.TotalSeconds < saniye)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }
    private static List<PopupRoot> Acilirlar()
    {
        var sonuc = new List<PopupRoot>();
        var tur = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Avalonia.Win32.WindowImpl")).FirstOrDefault(t => t is not null);
        if (tur?.GetField("s_instances", Her)?.GetValue(null) is not System.Collections.IEnumerable impller) return sonuc;
        foreach (var impl in impller)
            if (Giris(impl)?.Target is { } kaynak)
                sonuc.AddRange(kaynak.GetType().GetProperties(Her).Where(p => p.GetIndexParameters().Length == 0 && typeof(Visual).IsAssignableFrom(p.PropertyType)).Select(p => p.GetValue(kaynak)).OfType<PopupRoot>().Where(k => k.IsVisible));
        return sonuc;
    }

    [Fact]
    public void DondurmeTusuMenudeGorunurVeHamKlavyeyleKareyiDondurur()
    {
        var (kayit, hata) = AppHost.Run(() =>
        {
            var o = KisayolOrtam.Ac(KisayolKanit.Ikinci, "dondur-bulunur");
            string? sonuc = null;
            try
            {
                var gesture = Keymap.Gesture(Keymap.FirstKeyRow(Keymap.Rotate)!.Input);
                var baslik = Strings.Get("player.view.rotate");
                o.Not($"tus adi {gesture}, baslik {baslik}");

                var onceki = Acilirlar();
                var yuzey = Orta(o.Window, o.View);
                HamFare(o.Window, RawPointerEventType.Move, yuzey, RawInputModifiers.None);
                HamFare(o.Window, RawPointerEventType.RightButtonDown, yuzey, RawInputModifiers.RightMouseButton);
                HamFare(o.Window, RawPointerEventType.RightButtonUp, yuzey, RawInputModifiers.None);
                PopupRoot? menu = null;
                MenuItem? satir = null;
                o.Bekle(() => (menu = Acilirlar().FirstOrDefault(k => !onceki.Contains(k))) is not null
                              && (satir = menu.GetVisualDescendants().OfType<MenuItem>().FirstOrDefault(m => ReferenceEquals(m.Tag, Keymap.Rotate))) is not null, 5);
                o.Bekle(0.3);
                if (menu is null || satir is null) return (o.Kayit.ToString(), "menude dondurme satiri yok");

                o.Not($"menu acik {o.View.MenuOpen}, capa {o.View.MenuAnchor}, kok giris {(menu.PlatformImpl is { } pi && Giris(pi) is not null)}");
                var isabet = menu.InputHitTest(Orta(menu, satir)) is Visual v && (ReferenceEquals(v, satir) || v.GetVisualAncestors().Contains(satir));
                var tusMetni = satir.GetVisualDescendants().OfType<TextBlock>().Where(b => b.IsEffectivelyVisible && b.Bounds.Width > 0).Select(b => b.Text).ToList();
                var ipucu = ToolTip.GetTip(satir) as string;
                o.Not($"menu satiri: baslik '{satir.Header}', gorunur {isabet}, metinler [{string.Join(" | ", tusMetni)}], ipucu '{ipucu}'");
                if (!Equals(satir.Header, baslik) || !isabet) sonuc ??= "dondurme satiri gorunmuyor";
                if (!tusMetni.Contains(gesture)) sonuc ??= "satirda tus adi yazmiyor";
                if (ipucu is null || !ipucu.Contains(baslik, StringComparison.Ordinal) || !ipucu.Contains(gesture, StringComparison.Ordinal)) sonuc ??= "ipucunda tus adi yok";

                HamTus(menu.PlatformImpl is { } mi && Giris(mi) is not null ? menu : o.Window, Key.Escape, RawInputModifiers.None, null);
                o.Bekle(() => !o.View.MenuOpen, 3);
                o.Not($"menu kapandi {!o.View.MenuOpen}");

                var ctrlS = char.ConvertFromUtf32(19);
                var beklenen = new[]
                {
                    (90, 180, 320, "kirmizi", "mavi", (string?)null),
                    (180, 320, 180, "mavi", "kirmizi", (string?)null),
                    (270, 180, 320, "mavi", "kirmizi", ctrlS),
                    (0, 320, 180, "kirmizi", "mavi", ctrlS)
                };
                o.Not($"once kare {KisayolKanit.Kare(o.View)}");
                foreach (var (aci, w, h, ilk, ikinci, sembol) in beklenen)
                {
                    HamTus(o.Window, Key.S, RawInputModifiers.Control | RawInputModifiers.Shift, sembol);
                    o.Bekle(() => KisayolKanit.Kare(o.View) is var k && k.W == w && k.H == h, 3);
                    o.Bekle(0.3);
                    var kare = KisayolKanit.Kare(o.View);
                    o.Not($"ham Ctrl+Shift+S (sembol {(sembol is null ? "yok" : "U+0013")}): kare {kare}");
                    var konum = aci is 90 or 270 ? (kare.Ust, kare.Alt) : (kare.Sol, kare.Sag);
                    if (kare.W != w || kare.H != h || konum != (ilk, ikinci))
                        sonuc ??= $"{aci} derecede kare {kare}";
                }

                return (o.Kayit.ToString(), sonuc);
            }
            catch (Exception ex)
            {
                return (o.Kayit + ex.ToString(), "istisna");
            }
            finally
            {
                o.Kapat();
            }
        });

        KisayolKanit.Write("dondur-bulunur.txt", kayit);
        Assert.True(hata is null, hata + Environment.NewLine + kayit);
    }

    [Fact]
    public void HizAdimiVeCiftTikSuresiHamGirdiyle()
    {
        var sistem = SystemDoubleClick.Milliseconds();
        var windows = SystemDoubleClick.WindowsValue();
        var eski = ClickArbiter.Source;
        var varsayilan = ClickArbiter.DoubleWindowMs;
        (string, string?) sonucu;
        try
        {
            ClickArbiter.Source = () => 900;
            sonucu = AppHost.Run(() =>
            {
                var o = KisayolOrtam.Ac(KisayolKanit.Uzun, "hiz-cift-tik");
                string? sonuc = null;
                try
                {
                    o.Not($"sistem cift tik {F(sistem)} ms, GetDoubleClickTime {F(windows)} ms, arbiter varsayilani {F(varsayilan)} ms, testte 900 ms");
                    var hizlar = new List<string> { F(o.Sayi("speed")) };
                    foreach (var (key, sembol, beklenen) in new[] { (Key.C, "c", 1.05), (Key.C, "c", 1.1), (Key.X, "x", 1.05), (Key.X, "x", 1.0), (Key.X, "x", 0.95) })
                    {
                        HamTus(o.Window, key, RawInputModifiers.None, sembol);
                        o.Bekle(() => Math.Abs(o.Sayi("speed") - beklenen) < 1e-6, 2);
                        hizlar.Add(F(o.Sayi("speed")));
                        if (Math.Abs(o.Sayi("speed") - beklenen) >= 1e-6) sonuc ??= $"{key} sonrasi speed {F(o.Sayi("speed"))}, beklenen {F(beklenen)}";
                    }
                    o.Not("ham C,C,X,X,X motor speed: " + string.Join(" -> ", hizlar));

                    o.View.Apply(new PlayerCommand(PlayerCommandKind.SpeedReset, 0));
                    o.Bekle(() => o.Oku("pause") == "yes", 2);
                    var nokta = Orta(o.Window, o.View);
                    HamFare(o.Window, RawPointerEventType.Move, nokta, RawInputModifiers.None);
                    DenetimSurucu.Wait(o.View, 0.05);
                    HamFare(o.Window, RawPointerEventType.LeftButtonDown, nokta, RawInputModifiers.LeftMouseButton);
                    HamFare(o.Window, RawPointerEventType.LeftButtonUp, nokta, RawInputModifiers.None);
                    var saat = Stopwatch.StartNew();
                    Dongu(() => saat.ElapsedMilliseconds >= 600, 2);
                    var erken = o.Oku("pause");
                    var erkenMs = saat.ElapsedMilliseconds;
                    Dongu(() => o.Oku("pause") == "no", 3);
                    var gec = o.Oku("pause");
                    var gecMs = saat.ElapsedMilliseconds;
                    o.Not($"ham sol tik: {erkenMs} ms'de pause {erken}, {gecMs} ms'de pause {gec}");
                    if (erken != "yes") sonuc ??= $"tek tik {erkenMs} ms'de islendi, 900 ms beklenmedi";
                    if (gec != "no" || gecMs < 880) sonuc ??= $"tek tik {gecMs} ms'de pause {gec}";
                    return (o.Kayit.ToString(), sonuc);
                }
                finally
                {
                    o.Kapat();
                }
            });
        }
        finally
        {
            ClickArbiter.Source = eski;
        }

        KisayolKanit.Write("hiz-cift-tik.txt", sonucu.Item1);
        if (OperatingSystem.IsWindows()) Assert.Equal(windows, sistem);
        Assert.Equal(sistem, varsayilan);
        Assert.True(sonucu.Item2 is null, sonucu.Item2 + Environment.NewLine + sonucu.Item1);
    }
}
