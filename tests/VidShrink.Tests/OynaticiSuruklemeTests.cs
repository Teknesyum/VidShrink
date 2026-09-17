using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;
using static VidShrink.Tests.OynaticiOdakYoluTests;

namespace VidShrink.Tests;

[CollectionDefinition(CiftTikKaynagi.Ad, DisableParallelization = true)]
public sealed class CiftTikKaynagi
{
    public const string Ad = "cift-tik-kaynagi";
}

[Collection(CiftTikKaynagi.Ad)]
public sealed class OynaticiCiftTikSuresiTests
{
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

public sealed class OynaticiSuruklemeTests
{
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, ref PlayerView.NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out PlayerView.NativeRect rect);

    [Fact]
    public void TussuzHareketVeYakalamaKaybiPencereyiTasimaz()
    {
        var body = new StringBuilder();
        var hatalar = new List<string>();
        try
        {
            AppHost.Run(() =>
            {
                var view = new PlayerView { NativeMoveDrag = false };
                var window = new Window { Width = 640, Height = 360, Content = view };
                window.Show();
                DenetimSurucu.Wait(view, 0.2);
                window.Position = new PixelPoint(40, 30);
                DenetimSurucu.Wait(view, 0.2);

                IPointer? isaretci = null;
                view.AddHandler(InputElement.PointerMovedEvent, (_, e) => isaretci = e.Pointer, RoutingStrategies.Tunnel, true);
                var tutma = window.PointToScreen(new Point(view.Bounds.Width / 2, view.Bounds.Height / 2));
                void Olay(RawPointerEventType tur, int dx, int dy, RawInputModifiers tus)
                {
                    HamFare(window, tur, window.PointToClient(new PixelPoint(tutma.X + dx, tutma.Y + dy)), tus);
                    DenetimSurucu.Wait(view, 0.05);
                }

                void Denetle(string ad, PixelPoint beklenen, bool surukleniyor)
                {
                    var satir = $"{ad}: konum {window.Position}, beklenen {beklenen}, surukleniyor {view.WindowDragging} (beklenen {surukleniyor})";
                    body.AppendLine(satir);
                    if (window.Position != beklenen || view.WindowDragging != surukleniyor) hatalar.Add(satir);
                }

                var bas = window.Position;
                Olay(RawPointerEventType.Move, 0, 0, RawInputModifiers.None);
                Olay(RawPointerEventType.LeftButtonDown, 0, 0, RawInputModifiers.LeftMouseButton);
                Olay(RawPointerEventType.Move, 60, 40, RawInputModifiers.None);
                Olay(RawPointerEventType.Move, 120, 80, RawInputModifiers.None);
                Denetle("basili kalmis basis + tussuz hareket", bas, false);
                Olay(RawPointerEventType.LeftButtonUp, 120, 80, RawInputModifiers.None);

                DenetimSurucu.Wait(view, ClickArbiter.DoubleWindowMs / 1000 + 0.3);
                bas = window.Position;
                Olay(RawPointerEventType.Move, 0, 0, RawInputModifiers.None);
                Olay(RawPointerEventType.LeftButtonDown, 0, 0, RawInputModifiers.LeftMouseButton);
                Olay(RawPointerEventType.Move, 30, 20, RawInputModifiers.LeftMouseButton);
                var tasindi = window.Position;
                Denetle("pozitif kontrol: basili surukleme", new PixelPoint(bas.X + 30, bas.Y + 20), true);
                Olay(RawPointerEventType.Move, 90, 60, RawInputModifiers.None);
                Olay(RawPointerEventType.Move, 150, 100, RawInputModifiers.None);
                Denetle("surukleme sirasinda tus kayboldu (Alt+Tab) + tussuz hareket", tasindi, false);
                Olay(RawPointerEventType.LeftButtonUp, 150, 100, RawInputModifiers.None);

                DenetimSurucu.Wait(view, ClickArbiter.DoubleWindowMs / 1000 + 0.3);
                bas = window.Position;
                tutma = window.PointToScreen(new Point(view.Bounds.Width / 2, view.Bounds.Height / 2));
                Olay(RawPointerEventType.Move, 0, 0, RawInputModifiers.None);
                Olay(RawPointerEventType.LeftButtonDown, 0, 0, RawInputModifiers.LeftMouseButton);
                Olay(RawPointerEventType.Move, 30, 20, RawInputModifiers.LeftMouseButton);
                tasindi = window.Position;
                Denetle("pozitif kontrol: ikinci surukleme", new PixelPoint(bas.X + 30, bas.Y + 20), true);
                isaretci!.Capture(null);
                DenetimSurucu.Wait(view, 0.05);
                Denetle("yakalama iptal edildi", tasindi, false);
                Olay(RawPointerEventType.Move, 90, 60, RawInputModifiers.LeftMouseButton);
                Olay(RawPointerEventType.Move, 150, 100, RawInputModifiers.LeftMouseButton);
                Denetle("yakalama iptalinden sonra basili hareket", tasindi, false);
                Olay(RawPointerEventType.LeftButtonUp, 150, 100, RawInputModifiers.None);

                body.AppendLine("iz: " + string.Join(" | ", view.Trace.Where(s => s.StartsWith("drag", StringComparison.Ordinal) || s.StartsWith("move", StringComparison.Ordinal) || s.StartsWith("snap", StringComparison.Ordinal))));
                window.Close();
                return 0;
            });
        }
        finally
        {
            KisayolKanit.Write("surukleme-yakalama.txt", body.ToString());
        }

        Assert.True(hatalar.Count == 0, body.ToString());
    }

    [Fact]
    public void WindowsYerelTasimaWmMovingDikdortgeniniMerkezeCeker()
    {
        if (!OperatingSystem.IsWindows()) return;
        var body = new StringBuilder();
        var hatalar = new List<string>();
        try
        {
            AppHost.Run(() =>
            {
                var view = new PlayerView();
                var window = new Window { Width = 640, Height = 360, Content = view };
                window.Show();
                DenetimSurucu.Wait(view, 0.2);
                body.AppendLine($"yerel yol {view.NativeMoveDrag}, WM_MOVING kancasi {view.MovingHookInstalled}");
                if (!view.NativeMoveDrag || !view.MovingHookInstalled) hatalar.Add("windows yerel yol kurulu degil");

                var hwnd = window.TryGetPlatformHandle()!.Handle;
                var ekran = window.Screens.ScreenFromWindow(window)!;
                GetWindowRect(hwnd, out var gercek);
                var w = gercek.Right - gercek.Left;
                var h = gercek.Bottom - gercek.Top;
                var esikDip = view.FindResource("PlaybackBadgeMargin") is Thickness m ? m.Left : 0;
                var esikPx = (int)Math.Ceiling(esikDip * ekran.Scaling);
                var merkez = new PixelPoint(ekran.WorkingArea.X + (ekran.WorkingArea.Width - w) / 2, ekran.WorkingArea.Y + (ekran.WorkingArea.Height - h) / 2);
                body.AppendLine($"ekran {ekran.WorkingArea} olcek {ekran.Scaling}, pencere {w}x{h}, merkez {merkez}, esik {esikPx} px");

                foreach (var (ad, oneri, beklenen) in new[]
                {
                    ("bolgede", new PixelPoint(merkez.X + esikPx, merkez.Y - esikPx), merkez),
                    ("bolge-disi", new PixelPoint(merkez.X + esikPx + 1, merkez.Y - esikPx - 1), new PixelPoint(merkez.X + esikPx + 1, merkez.Y - esikPx - 1)),
                    ("tek-eksen", new PixelPoint(merkez.X + 1, merkez.Y + 200), new PixelPoint(merkez.X, merkez.Y + 200))
                })
                {
                    var rect = new PlayerView.NativeRect { Left = oneri.X, Top = oneri.Y, Right = oneri.X + w, Bottom = oneri.Y + h };
                    SendMessage(hwnd, PlayerView.WmMoving, IntPtr.Zero, ref rect);
                    var satir = $"WM_MOVING {ad}: oneri {oneri}, donen {rect.Left},{rect.Top} {rect.Right - rect.Left}x{rect.Bottom - rect.Top}, beklenen {beklenen}";
                    body.AppendLine(satir);
                    if (rect.Left != beklenen.X || rect.Top != beklenen.Y || rect.Right - rect.Left != w || rect.Bottom - rect.Top != h) hatalar.Add(satir);
                }

                var tutma = new Point(window.ClientSize.Width / 2, window.ClientSize.Height / 2);
                body.AppendLine($"gorunum {view.Bounds.Width}x{view.Bounds.Height}, istemci {window.ClientSize.Width}x{window.ClientSize.Height}, tutma {tutma}, durum {window.WindowState}, etkin {window.IsActive}");
                var saat = Stopwatch.StartNew();
                HamFare(window, RawPointerEventType.Move, tutma, RawInputModifiers.None);
                HamFare(window, RawPointerEventType.LeftButtonDown, tutma, RawInputModifiers.LeftMouseButton);
                HamFare(window, RawPointerEventType.Move, tutma + new Vector(40, 30), RawInputModifiers.LeftMouseButton);
                DenetimSurucu.Wait(view, 0.1);
                HamFare(window, RawPointerEventType.LeftButtonUp, tutma + new Vector(40, 30), RawInputModifiers.None);
                var yerel = view.Trace.Contains("movedrag -> " + PlayerView.NativeMode);
                body.AppendLine("iz: " + string.Join(" | ", view.Trace));
                body.AppendLine($"ham surukleme: yerel tasima {yerel}, kendi dongu {view.WindowDragging}, {saat.ElapsedMilliseconds} ms");
                if (!yerel || view.WindowDragging) hatalar.Add("ham surukleme yerel tasimaya gitmedi");

                window.Close();
                return 0;
            });
        }
        finally
        {
            KisayolKanit.Write("surukleme-wm-moving.txt", body.ToString());
        }

        Assert.True(hatalar.Count == 0, body.ToString());
    }
}
