using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Playback;
using Xunit;
using static VidShrink.Tests.OynaticiOdakYoluTests;

namespace VidShrink.Tests;

public sealed class OynaticiTikTasimaTests
{
    private static PlayerView Ac(YolMotoru motor, out Window window, double en = 960, double boy = 540)
    {
        var view = new PlayerView { EngineFactory = () => motor, NativeMoveDrag = false };
        window = new Window { Width = en, Height = boy, Content = view };
        window.Show();
        ImlectenUzak(window);
        var open = view.OpenAsync(Path.Combine(YolKanit.Folder, "yok-sahte.mp4"));
        DenetimSurucu.Pump(view, () => open.IsCompleted, 10);
        open.GetAwaiter().GetResult();
        DenetimSurucu.Wait(view, 0.3);
        return view;
    }

    private static void Kapat(PlayerView view, Window window)
    {
        view.Close();
        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    private static int Cevirme(PlayerView view) => view.Trace.Count(s => s.StartsWith("play -> ", StringComparison.Ordinal));

    private static void Olay(Window window, RawPointerEventType tur, Point nokta, RawInputModifiers tus)
    {
        HamFare(window, tur, nokta, tus);
        Dispatcher.UIThread.RunJobs();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EkranNoktasi
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", EntryPoint = "GetCursorPos")]
    private static extern bool ImlecKonumu(out EkranNoktasi nokta);

    private static void ImlectenUzak(Window window)
    {
        if (!OperatingSystem.IsWindows() || !ImlecKonumu(out var imlec)) return;
        window.Position = new PixelPoint(imlec.X + 64, imlec.Y + 64);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void BasipBirakmakOynatmayiBirKezCevirir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var motor = new YolMotoru();
            var view = Ac(motor, out var window);
            try
            {
                var orta = Orta(window, view);
                var once = (Cevirme(view), view.IsPlaying);
                Olay(window, RawPointerEventType.Move, orta, RawInputModifiers.None);
                Olay(window, RawPointerEventType.LeftButtonDown, orta, RawInputModifiers.LeftMouseButton);
                var basili = Cevirme(view);
                Olay(window, RawPointerEventType.LeftButtonUp, orta, RawInputModifiers.None);
                var ilk = (Cevirme(view), view.IsPlaying, motor.IsPaused);

                var yan = orta + new Vector(40, 0);
                Olay(window, RawPointerEventType.Move, yan, RawInputModifiers.None);
                Olay(window, RawPointerEventType.LeftButtonDown, yan, RawInputModifiers.LeftMouseButton);
                Olay(window, RawPointerEventType.LeftButtonUp, yan, RawInputModifiers.None);
                var ikinci = (Cevirme(view), view.IsPlaying, motor.IsPaused);
                return (once, basili, ilk, ikinci, iz: string.Join(" | ", view.Trace));
            }
            finally
            {
                Kapat(view, window);
            }
        });

        Assert.Equal(sonuc.once.Item1, sonuc.basili);
        Assert.True(sonuc.ilk.Item1 == sonuc.once.Item1 + 1, sonuc.iz);
        Assert.Equal(!sonuc.once.IsPlaying, sonuc.ilk.IsPlaying);
        Assert.Equal(sonuc.ilk.IsPlaying, !sonuc.ilk.IsPaused);
        Assert.True(sonuc.ikinci.Item1 == sonuc.once.Item1 + 2, sonuc.iz);
        Assert.Equal(sonuc.once.IsPlaying, sonuc.ikinci.IsPlaying);
        Assert.Equal(sonuc.ikinci.IsPlaying, !sonuc.ikinci.IsPaused);
    }

    [Fact]
    public void EsikAltiOynamaTiklamaEsikUstuPencereTasir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var view = Ac(new YolMotoru(), out var window);
            try
            {
                var orta = Orta(window, view);
                var once = Cevirme(view);
                var bas = window.Position;
                Olay(window, RawPointerEventType.Move, orta, RawInputModifiers.None);
                Olay(window, RawPointerEventType.LeftButtonDown, orta, RawInputModifiers.LeftMouseButton);
                Olay(window, RawPointerEventType.Move, orta + new Vector(3, 3), RawInputModifiers.LeftMouseButton);
                Olay(window, RawPointerEventType.LeftButtonUp, orta + new Vector(3, 3), RawInputModifiers.None);
                var alti = (Cevirme(view) - once, surukleme: view.Trace.Count(s => s.StartsWith("drag -> ", StringComparison.Ordinal)), konum: window.Position == bas);

                var yan = orta + new Vector(-60, 0);
                once = Cevirme(view);
                Olay(window, RawPointerEventType.Move, yan, RawInputModifiers.None);
                Olay(window, RawPointerEventType.LeftButtonDown, yan, RawInputModifiers.LeftMouseButton);
                Olay(window, RawPointerEventType.Move, yan + new Vector(5, 0), RawInputModifiers.LeftMouseButton);
                var tasiyor = view.WindowDragging;
                var olcek = window.RenderScaling;
                var tasinan = window.Position;
                Olay(window, RawPointerEventType.LeftButtonUp, yan + new Vector(5, 0), RawInputModifiers.None);
                var ustu = (Cevirme(view) - once, surukleme: view.Trace.Count(s => s == "drag -> window"), tasiyor, dx: tasinan.X - bas.X, olcek);
                return (alti, ustu, iz: string.Join(" | ", view.Trace));
            }
            finally
            {
                Kapat(view, window);
            }
        });

        Assert.True(sonuc.alti.Item1 == 1, sonuc.iz);
        Assert.Equal(0, sonuc.alti.surukleme);
        Assert.True(sonuc.alti.konum);
        Assert.True(sonuc.ustu.Item1 == 0, sonuc.iz);
        Assert.Equal(1, sonuc.ustu.surukleme);
        Assert.True(sonuc.ustu.tasiyor);
        Assert.InRange(sonuc.ustu.dx, (int)Math.Floor(5 * sonuc.ustu.olcek) - 1, (int)Math.Ceiling(5 * sonuc.ustu.olcek) + 1);
    }

    [Fact]
    public void SeritDugmesineBasisPencereyiTasimazOynatmayiIkiKezCevirmez()
    {
        var sonuc = AppHost.Run(() =>
        {
            var view = Ac(new YolMotoru(), out var window);
            try
            {
                var saat = new ElleSaat();
                view.SeritZone.Clock = saat;
                var surface = view.FindControl<Panel>("Surface")!;
                var bant = Orta(window, surface).WithY(surface.TranslatePoint(new Point(0, surface.Bounds.Height - 2), window)!.Value.Y);
                Olay(window, RawPointerEventType.Move, bant, RawInputModifiers.None);
                saat.Ates();
                DenetimSurucu.Wait(view, 0.2);
                var dugme = view.FindControl<Button>("BtnSeritPlay")!;
                var nokta = Orta(window, dugme);
                var bas = window.Position;
                var once = Cevirme(view);
                Olay(window, RawPointerEventType.Move, nokta, RawInputModifiers.None);
                Olay(window, RawPointerEventType.LeftButtonDown, nokta, RawInputModifiers.LeftMouseButton);
                Olay(window, RawPointerEventType.LeftButtonUp, nokta, RawInputModifiers.None);
                var tik = Cevirme(view) - once;

                var kaydirici = view.GetVisualDescendants().OfType<Slider>().First(s => s.IsEffectivelyVisible);
                var ks = Orta(window, kaydirici);
                Olay(window, RawPointerEventType.Move, ks, RawInputModifiers.None);
                Olay(window, RawPointerEventType.LeftButtonDown, ks, RawInputModifiers.LeftMouseButton);
                Olay(window, RawPointerEventType.Move, ks + new Vector(30, -20), RawInputModifiers.LeftMouseButton);
                var kayan = window.Position;
                Olay(window, RawPointerEventType.LeftButtonUp, ks + new Vector(30, -20), RawInputModifiers.None);
                return (acik: view.SeritRevealed, gorunur: dugme.IsEffectivelyVisible, tik, konum: kayan == bas, surukleme: view.Trace.Count(s => s.StartsWith("drag -> ", StringComparison.Ordinal)), iz: string.Join(" | ", view.Trace));
            }
            finally
            {
                Kapat(view, window);
            }
        });

        Assert.True(sonuc.acik && sonuc.gorunur, sonuc.iz);
        Assert.True(sonuc.tik == 1, sonuc.iz);
        Assert.True(sonuc.konum, sonuc.iz);
        Assert.True(sonuc.surukleme == 0, sonuc.iz);
    }

    [Fact]
    public void OrtaTusKucukPencereVideoOraninaOturur()
    {
        var sonuc = AppHost.Run(() =>
        {
            var view = Ac(new YolMotoru(w: 64, h: 36), out var window, 1300, 800);
            try
            {
                view.CurrentTabIndex = () => 5;
                view.PlayerTabIndex = () => 5;
                view.SelectTab = _ => { };
                DenetimSurucu.Pump(view, () => view.VideoAspect > 0, 3);
                var oran = view.VideoAspect;
                GirdiSurucu.Press(view, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
                DenetimSurucu.Wait(view, 0.2);
                GirdiSurucu.Press(view, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
                DenetimSurucu.Wait(view, 0.2);
                var kucuk = (view.IsCompact, window.WindowState, en: window.Width * window.RenderScaling, boy: window.Height * window.RenderScaling, istek: view.BorderColorRequest, yazim: view.BorderColorWrites);
                view.RestoreWindowMin();
                var geri = view.BorderColorRequest;
                return (oran, kucuk, geri, iz: string.Join(" | ", view.Trace));
            }
            finally
            {
                Kapat(view, window);
            }
        });

        Assert.Equal(64.0 / 36, sonuc.oran, 6);
        Assert.True(sonuc.kucuk.IsCompact, sonuc.iz);
        Assert.Equal(WindowState.Normal, sonuc.kucuk.WindowState);
        Assert.InRange(Math.Abs(sonuc.kucuk.boy - sonuc.kucuk.en / sonuc.oran), 0, 1);
        Assert.Equal(PencereKipi.DwmColorNone, sonuc.kucuk.istek);
        Assert.Contains("border -> none", sonuc.iz);
        if (OperatingSystem.IsWindows() && Environment.OSVersion.Version.Build >= 22000) Assert.True(sonuc.kucuk.yazim >= 1, sonuc.iz);
        Assert.Equal(PencereKipi.DwmColorDefault, sonuc.geri);
    }

    [Theory]
    [InlineData(1920, 1040, 16.0 / 9)]
    [InlineData(1920, 1040, 9.0 / 16)]
    [InlineData(2560, 1400, 4.0 / 3)]
    [InlineData(1366, 728, 2.39)]
    public void KucukPencereOraniVideoOrani(int w, int h, double oran)
    {
        var r = CompactWindow.Fit(0, 0, w, h, 1.0 / 3, oran);

        Assert.InRange(Math.Abs(r.Height - r.Width / oran), 0, 1);
        var pay = (double)r.Width * r.Height / ((double)w * h);
        Assert.InRange(pay, 0, 0.345);
        Assert.True(pay >= 0.32 || r.Width == w || r.Height == h, $"pay {pay}, {r}");
        Assert.InRange(Math.Abs(r.X + r.Width / 2.0 - w / 2.0), 0, 1);
        Assert.InRange(Math.Abs(r.Y + r.Height / 2.0 - h / 2.0), 0, 1);
        Assert.True(r.Width <= w && r.Height <= h);
    }

    [Fact]
    public void OransizVideodaEskiPayKullanilir()
    {
        Assert.Equal(CompactWindow.Fit(0, 0, 1920, 1040, 1.0 / 3), CompactWindow.Fit(0, 0, 1920, 1040, 1.0 / 3, 0));
    }

    [Fact]
    public void KenarRengiDwmIleYazilir()
    {
        if (!OperatingSystem.IsWindows() || Environment.OSVersion.Version.Build < 22000) return;
        var sonuc = AppHost.Run(() =>
        {
            var window = new Window { Width = 200, Height = 120 };
            window.Show();
            try
            {
                var hwnd = window.TryGetPlatformHandle()!.Handle;
                return (yok: PencereKipi.WriteBorderColor(hwnd, PencereKipi.DwmColorNone), varsayilan: PencereKipi.WriteBorderColor(hwnd, PencereKipi.DwmColorDefault), bos: PencereKipi.WriteBorderColor(IntPtr.Zero, PencereKipi.DwmColorNone));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(0, sonuc.yok);
        Assert.Equal(0, sonuc.varsayilan);
        Assert.NotEqual(0, sonuc.bos);
    }

    [Fact]
    public void DurakliykenSuruklemedenSonraFareGidinceSeritKapanir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var view = Ac(new YolMotoru(), out var window);
            try
            {
                if (view.IsPlaying) view.Apply(Keymap.PlayPause.ToCommand());
                DenetimSurucu.Wait(view, 0.1);
                var saat = new ElleSaat();
                view.SeritZone.Clock = saat;
                var surface = view.FindControl<Panel>("Surface")!;
                var serit = view.FindControl<Border>("StripBar")!;
                var bantUst = surface.Bounds.Height - view.RevealBand;
                var yuzeyY = surface.TranslatePoint(new Point(0, 0), window)!.Value.Y;
                var seritUst = serit.TranslatePoint(new Point(0, 0), window)!.Value.Y;
                var x = Orta(window, surface).X - 120;
                var bant = new Point(x, Math.Max(yuzeyY + bantUst + 1, Math.Min(seritUst - 3, yuzeyY + surface.Bounds.Height - 2)));
                Olay(window, RawPointerEventType.Move, bant, RawInputModifiers.None);
                saat.Ates();
                var acildi = view.SeritRevealed;

                var bas = window.Position;
                Olay(window, RawPointerEventType.LeftButtonDown, bant, RawInputModifiers.LeftMouseButton);
                Olay(window, RawPointerEventType.Move, bant + new Vector(40, -30), RawInputModifiers.LeftMouseButton);
                Olay(window, RawPointerEventType.Move, bant + new Vector(80, -60), RawInputModifiers.LeftMouseButton);
                var tasindi = window.Position != bas;
                Olay(window, RawPointerEventType.LeftButtonUp, bant + new Vector(80, -60), RawInputModifiers.None);
                Olay(window, RawPointerEventType.Move, bant, RawInputModifiers.None);
                saat.Ates();
                var suruklemeSonu = view.SeritRevealed;

                Olay(window, RawPointerEventType.Move, Orta(window, surface), RawInputModifiers.None);
                saat.Ates();
                return (duraklik: !view.IsPlaying, acildi, tasindi, suruklemeSonu, kapandi: !view.SeritRevealed, iz: string.Join(" | ", view.Trace));
            }
            finally
            {
                Kapat(view, window);
            }
        });

        Assert.True(sonuc.duraklik, sonuc.iz);
        Assert.True(sonuc.acildi, sonuc.iz);
        Assert.True(sonuc.tasindi, sonuc.iz);
        Assert.True(sonuc.suruklemeSonu, sonuc.iz);
        Assert.True(sonuc.kapandi, sonuc.iz);
    }
}

public sealed class ZeminTasimaEsikTests
{
    private static List<Point> Bul(Window window, Visual alan, Func<Visual, bool> uygun, int adet)
    {
        var sol = alan.TranslatePoint(new Point(0, 0), window)!.Value;
        var bulunan = new List<Point>();
        for (var y = 4.0; y < alan.Bounds.Height - 4; y += 3)
        for (var x = 4.0; x < alan.Bounds.Width - 10; x += 3)
        {
            var p = sol + new Vector(x, y);
            if (bulunan.Any(q => Math.Abs(q.X - p.X) < 10 && Math.Abs(q.Y - p.Y) < 10)) continue;
            if (!new[] { 0.0, 3, 5 }.All(dx => window.InputHitTest(p + new Vector(dx, 0)) is Visual v && uygun(v))) continue;
            bulunan.Add(p);
            if (bulunan.Count == adet) return bulunan;
        }

        return bulunan;
    }

    private static (List<string> iz, int baslatma, bool tasindi) Dene(MainWindow window, Point nokta, double oynama)
    {
        var baslatma = 0;
        window.TasimaBaslatici = _ => baslatma++;
        var once = window.ZeminIz.Count;
        HamFare(window, RawPointerEventType.Move, nokta, RawInputModifiers.None);
        HamFare(window, RawPointerEventType.LeftButtonDown, nokta, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        HamFare(window, RawPointerEventType.Move, nokta + new Vector(oynama, 0), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        HamFare(window, RawPointerEventType.LeftButtonUp, nokta + new Vector(oynama, 0), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        return (window.ZeminIz.Skip(once).ToList(), baslatma, window.ZeminBekliyor);
    }

    /// <summary>
    /// Zeminde ve başlıkta tık ile 3 px oynama taşıma başlatmaz, 5 px başlatır. Noktalar
    /// <c>InputHitTest</c> ile aranır; isabet çizim iş parçacığının son karesinin geri
    /// okumasından geldiği için arama, sahne çizildikten sonra (<see cref="KareSayaci.SahneCizilsin"/>)
    /// yapılır. Eski 0,3 sn bekleme CI yükünde çizimden önce kalıp "zemin 0, baslik 0" verdi.
    /// </summary>
    [Fact]
    public void ZeminVeBaslikEsikGecilmedenTasimaz()
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "zemin-esik", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var sonuc = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = Path.Combine(klasor, "settings.json"), Width = 1280, Height = 800, WindowState = WindowState.Normal };
                try
                {
                    window.Show();
                    var cizildi = KareSayaci.SahneCizilsin(window);
                    var zemin = Bul(window, window.TargetPanel, v => window.ZemindenTasinir(v), 3);
                    var baslik = Bul(window, window.TitleBar, v => ReferenceEquals(v, window.TitleBar) || window.ZemindenTasinir(v), 3);
                    var satirlar = new List<string>();
                    if (zemin.Count < 3 || baslik.Count < 3)
                    {
                        var orta = window.TargetPanel.TranslatePoint(new Point(window.TargetPanel.Bounds.Width / 2, window.TargetPanel.Bounds.Height / 2), window);
                        var isabet = orta is { } o ? window.InputHitTest(o) : null;
                        return new List<string> { $"nokta bulunamadi: zemin {zemin.Count}, baslik {baslik.Count}; cizildi {cizildi}, durum {window.WindowState}, gorunur {window.IsVisible}, pencere {window.Bounds.Size}, hedef {window.TargetPanel.Bounds}, baslik {window.TitleBar.Bounds}, ortadaki {isabet?.GetType().Name ?? "yok"}" };
                    }

                    foreach (var (ad, n) in new[] { ("zemin", zemin), ("baslik", baslik) })
                    {
                        var tik = Dene(window, n[0], 0);
                        if (tik.baslatma != 0 || !tik.iz.SequenceEqual(new[] { "press", "click" }) || tik.tasindi)
                            satirlar.Add($"{ad} tik: baslatma {tik.baslatma}, iz {string.Join(",", tik.iz)}");

                        var alti = Dene(window, n[1], 3);
                        if (alti.baslatma != 0 || !alti.iz.SequenceEqual(new[] { "press", "click" }))
                            satirlar.Add($"{ad} 3 dip: baslatma {alti.baslatma}, iz {string.Join(",", alti.iz)}");

                        var ustu = Dene(window, n[2], 5);
                        if (ustu.baslatma != 1 || !ustu.iz.SequenceEqual(new[] { "press", "movedrag" }))
                            satirlar.Add($"{ad} 5 dip: baslatma {ustu.baslatma}, iz {string.Join(",", ustu.iz)}");
                    }

                    if (window.WindowState != WindowState.Normal) satirlar.Add("baslik tiki pencere durumunu degistirdi: " + window.WindowState);
                    return satirlar;
                }
                finally
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }
            });

            Assert.True(sonuc.Count == 0, string.Join(Environment.NewLine, sonuc));
        }
        finally
        {
            try { Directory.Delete(klasor, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void OynaticiSekmesiKucukPenceredeKenarsiz()
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "zemin-esik", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var sonuc = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = Path.Combine(klasor, "settings.json"), Width = 1280, Height = 800, WindowState = WindowState.Normal };
                try
                {
                    window.Show();
                    DenetimSurucu.Wait(window.Player, 0.3);
                    GirdiSurucu.Press(window.Player, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
                    DenetimSurucu.Wait(window.Player, 0.2);
                    GirdiSurucu.Press(window.Player, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
                    DenetimSurucu.Wait(window.Player, 0.2);
                    return (window.Player.IsCompact, window.WindowState, window.WindowShell.BorderThickness, window.Player.BorderColorRequest);
                }
                finally
                {
                    window.Player.RestoreWindowMin();
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }
            });

            Assert.True(sonuc.IsCompact);
            Assert.Equal(WindowState.Normal, sonuc.WindowState);
            Assert.Equal(new Thickness(0), sonuc.BorderThickness);
            Assert.Equal(PencereKipi.DwmColorNone, sonuc.BorderColorRequest);
        }
        finally
        {
            try { Directory.Delete(klasor, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
