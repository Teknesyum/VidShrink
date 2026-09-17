using System.Globalization;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
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
}
