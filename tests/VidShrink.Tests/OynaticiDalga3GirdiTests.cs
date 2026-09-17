using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public class SahteKlasorSecici : DispatchProxy
{
    internal IStorageProvider Gercek = null!;
    internal IReadOnlyList<IStorageFolder> Donus = Array.Empty<IStorageFolder>();
    internal int Cagri;

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "get_CanPickFolder") return true;
        if (method.Name == nameof(IStorageProvider.OpenFolderPickerAsync))
        {
            Cagri++;
            return Task.FromResult(Donus);
        }

        return method.Invoke(Gercek, args);
    }
}

public sealed class OynaticiDalga3GirdiTests
{
    private const BindingFlags Her = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly object Fare = Yeni(typeof(MouseDevice), new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, true));

    private static object Yeni(Type tur, params object?[] args)
        => Activator.CreateInstance(tur, Her, null, args, null)!;

    private static object Impl(TopLevel top) => typeof(TopLevel).GetProperty("PlatformImpl", Her)!.GetValue(top)!;

    private static IInputRoot Kok(TopLevel top) => (IInputRoot)typeof(TopLevel).GetProperty("InputRoot", Her)!.GetValue(top)!;

    private static Action<RawInputEventArgs>? Giris(object impl)
        => (Action<RawInputEventArgs>?)impl.GetType().GetInterfaces()
            .Select(i => i.GetProperty("Input", Her)).First(p => p is not null)!.GetValue(impl);

    private static void Ham(TopLevel top, RawInputEventArgs args) => Giris(Impl(top))!(args);

    private static void Fareyle(TopLevel top, RawPointerEventType tur, Point nokta, RawInputModifiers tuslar)
        => Ham(top, (RawInputEventArgs)Yeni(typeof(RawPointerEventArgs), Fare, (ulong)Environment.TickCount64, Kok(top), tur, nokta, tuslar));

    private static void Tus(TopLevel top, Key key, RawInputModifiers tuslar = RawInputModifiers.None)
    {
        var klavye = typeof(KeyboardDevice).GetProperty("Instance", Her)!.GetValue(null)!;
        foreach (var tur in new[] { RawKeyEventType.KeyDown, RawKeyEventType.KeyUp })
            Ham(top, (RawInputEventArgs)Yeni(typeof(RawKeyEventArgs), klavye, (ulong)Environment.TickCount64, Kok(top), tur, key, tuslar, PhysicalKey.None, null, KeyDeviceType.Keyboard));
    }

    private static void Birak(TopLevel top, Point nokta, IStorageItem oge, StringBuilder body)
    {
        var aktarim = new DataTransfer();
        aktarim.Add(DataTransferItem.CreateFile(oge));
        var aygit = typeof(DragDropDevice).GetField("Instance", Her)!.GetValue(null)!;
        foreach (var tur in new[] { RawDragEventType.DragEnter, RawDragEventType.DragOver, RawDragEventType.Drop })
        {
            var olay = (RawInputEventArgs)Yeni(typeof(RawDragEvent), aygit, tur, Kok(top), nokta, aktarim, DragDropEffects.Copy, RawInputModifiers.None);
            Ham(top, olay);
            body.AppendLine($"{tur}: etki {olay.GetType().GetProperty("Effects", Her)!.GetValue(olay)}");
        }
    }

    private static Point Merkez(TopLevel top, Visual control)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), top)!.Value;

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

    private static PopupRoot? AcikMenu(IReadOnlyCollection<PopupRoot> onceki, string baslik)
        => Acilirlar().FirstOrDefault(k => !onceki.Contains(k) && k.GetVisualDescendants().OfType<MenuItem>().Any(m => Equals(m.Header, baslik)));

    private static PopupRoot Inis(PlayerView view, Window window, List<PopupRoot> bilinen, StringBuilder body, string baslik, Key ac)
    {
        PopupRoot? kok = null;
        DenetimSurucu.Pump(view, () => (kok = AcikMenu(bilinen, baslik)) is not null, 5);
        Assert.True(kok is not null, baslik + " satiri tasiyan menu acilmadi" + Environment.NewLine + body);
        DenetimSurucu.Wait(view, 0.2);
        var satir = kok!.GetVisualDescendants().OfType<MenuItem>().First(m => Equals(m.Header, baslik));
        var tur = 0;
        for (; tur < 80 && !satir.IsSelected; tur++)
        {
            Tus(kok, Key.Down);
            DenetimSurucu.Wait(view, 0.02);
        }

        body.AppendLine($"'{baslik}': asagi ok {tur}, secili {satir.IsSelected}, tus {ac}");
        Assert.True(satir.IsSelected, baslik + " okla secilemedi" + Environment.NewLine + body);
        bilinen.Add(kok);
        var klavye = typeof(KeyboardDevice).GetProperty("Instance", Her)!.GetValue(null)!;
        foreach (var olay in new[] { RawKeyEventType.KeyDown, RawKeyEventType.KeyUp })
        {
            TopLevel hedef = typeof(TopLevel).GetProperty("PlatformImpl", Her)!.GetValue(kok) is null ? window : kok;
            Ham(hedef, (RawInputEventArgs)Yeni(typeof(RawKeyEventArgs), klavye, (ulong)Environment.TickCount64, Kok(hedef), olay, ac, RawInputModifiers.None, PhysicalKey.None, null, KeyDeviceType.Keyboard));
        }

        DenetimSurucu.Wait(view, 0.2);
        return kok;
    }

    private static string Kanit(string ad, string body)
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "girdi-dalga3");
        Directory.CreateDirectory(klasor);
        var yol = Path.Combine(klasor, ad);
        File.WriteAllText(yol, body, new UTF8Encoding(false));
        return yol;
    }

    private static string Klip(string klasor, string ad)
    {
        Assert.True(ToolLocator.IsAvailable(out var eksik), $"klip uretimi icin {eksik} gerekli");
        var yol = Path.Combine(klasor, ad);
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, "-y", "-hide_banner", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=25", "-t", "1", "-pix_fmt", "yuv420p", "-c:v", "libx264", "-g", "5", yol);
        Assert.True(kod == 0, $"ffmpeg {ad} uretemedi: {hata[Math.Max(0, hata.Length - 400)..]}");
        return yol;
    }

    private static (string rapor, int cagri, string? ayar, string? dosyada, string? goruntu) KlasorSec(bool secimVar)
    {
        var clip = MotorKlipleri.Kucuk;
        var ayarKlasoru = GorunumKanit.Gecici("secici-ayar");
        var hedef = GorunumKanit.Gecici("secici-hedef");
        return AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var view = DenetimSurucu.Ac(clip, out var window, Path.Combine(ayarKlasoru, "history.json"));
            window.Show();
            DenetimSurucu.Wait(view, 0.3);

            var gercek = window.StorageProvider;
            var sahte = DispatchProxy.Create<IStorageProvider, SahteKlasorSecici>();
            var secici = (SahteKlasorSecici)(object)sahte;
            secici.Gercek = gercek;
            if (secimVar)
            {
                var bul = gercek.TryGetFolderFromPathAsync(new Uri(hedef));
                DenetimSurucu.Pump(view, () => bul.IsCompleted, 5);
                secici.Donus = new[] { bul.Result! };
            }
            typeof(TopLevel).GetField("_storageProvider", Her)!.SetValue(window, sahte);

            var baslik = Strings.Get("player.view.screenshot-folder");
            var onceki = Acilirlar();
            body.AppendLine($"onceden acik acilir pencere {onceki.Count}");
            var yuzey = Merkez(window, view);
            Fareyle(window, RawPointerEventType.Move, yuzey, RawInputModifiers.None);
            Fareyle(window, RawPointerEventType.RightButtonDown, yuzey, RawInputModifiers.RightMouseButton);
            Fareyle(window, RawPointerEventType.RightButtonUp, yuzey, RawInputModifiers.None);
            var ayarBaslik = Strings.Get("main.player.menu.settings");
            PopupRoot? kok = null;
            DenetimSurucu.Pump(view, () => (kok = AcikMenu(onceki, ayarBaslik)) is not null, 5);
            body.AppendLine($"kok menu acildi: {kok is not null}");
            Assert.NotNull(kok);
            DenetimSurucu.Wait(view, 0.3);
            var ayarSatiri = kok!.GetVisualDescendants().OfType<MenuItem>().First(m => Equals(m.Header, ayarBaslik));
            var kokAcik = Acilirlar();
            var ayarNoktasi = Merkez(kok, ayarSatiri);
            Fareyle(kok, RawPointerEventType.Move, ayarNoktasi, RawInputModifiers.None);
            DenetimSurucu.Wait(view, 0.05);
            Fareyle(kok, RawPointerEventType.LeftButtonDown, ayarNoktasi, RawInputModifiers.LeftMouseButton);
            DenetimSurucu.Wait(view, 0.05);
            Fareyle(kok, RawPointerEventType.LeftButtonUp, ayarNoktasi, RawInputModifiers.None);
            PopupRoot? menu = null;
            DenetimSurucu.Pump(view, () => (menu = AcikMenu(kokAcik, baslik)) is not null, 5);
            body.AppendLine($"menu acildi: {menu is not null}, capa {view.MenuAnchor}");
            Assert.NotNull(menu);
            DenetimSurucu.Wait(view, 0.3);

            var satir = menu!.GetVisualDescendants().OfType<MenuItem>().First(m => Equals(m.Header, baslik));
            bool Gorunur() => menu.InputHitTest(Merkez(menu, satir)) is Visual v && (ReferenceEquals(v, satir) || v.GetVisualAncestors().Contains(satir));
            body.AppendLine($"kaydirma oncesi satir {Merkez(menu, satir)}, menu {menu.Bounds.Size}, gorunur {Gorunur()}");
            var tur = 0;
            for (; tur < 40 && !satir.IsSelected && !Gorunur(); tur++)
            {
                Tus(menu, Key.Up);
                DenetimSurucu.Wait(view, 0.03);
            }
            DenetimSurucu.Pump(view, Gorunur, 3);
            var nokta = Merkez(menu, satir);
            body.AppendLine($"satir noktasi {nokta}, menu yuksekligi {menu.Bounds.Height}, isabet satirda {Gorunur()}, yukari ok {tur}, secili {satir.IsSelected}");
            satir.AddHandler(InputElement.PointerPressedEvent, (_, e) => body.AppendLine("satir basildi"), Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
            satir.AddHandler(InputElement.PointerReleasedEvent, (_, e) => body.AppendLine("satir birakildi"), Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
            Fareyle(menu, RawPointerEventType.Move, nokta, RawInputModifiers.None);
            DenetimSurucu.Wait(view, 0.05);
            Fareyle(menu, RawPointerEventType.LeftButtonDown, nokta, RawInputModifiers.LeftMouseButton);
            DenetimSurucu.Wait(view, 0.05);
            Fareyle(menu, RawPointerEventType.LeftButtonUp, nokta, RawInputModifiers.None);
            DenetimSurucu.Pump(view, () => secici.Cagri > 0 && view.Settings.ScreenshotFolder is not null, 3);
            DenetimSurucu.Wait(view, 0.2);
            body.AppendLine($"secici cagrisi {secici.Cagri}, menu kapandi {!Acilirlar().Contains(menu)}");

            var ayar = view.Settings.ScreenshotFolder;
            var ayarDosyasi = Path.Combine(ayarKlasoru, PlayerSettings.FileName);
            var dosyada = File.Exists(ayarDosyasi) ? PlayerSettings.Load(ayarDosyasi).ScreenshotFolder : null;
            body.AppendLine($"ayar {ayar ?? "-"}, dosyada {dosyada ?? "-"}");

            string? goruntu = null;
            if (secimVar)
            {
                Tus(window, Key.E, RawInputModifiers.Control);
                GorunumKanit.Bekle(view, () => view.LastScreenshot, 15);
                goruntu = view.LastScreenshot.IsCompleted ? view.LastScreenshot.Result : null;
                body.AppendLine($"Ctrl+E goruntu {goruntu ?? "-"}");
            }

            body.AppendLine("iz: " + string.Join(" | ", view.Trace));
            view.Close();
            window.Close();
            return (body.ToString(), secici.Cagri, ayar, dosyada, goruntu);
        });
    }

    [Fact]
    public void GoruntuKlasoruHamSagTikMenusundenSecilirVeCtrlEOraYazar()
    {
        var hedef = "secici-hedef";
        var sonuc = KlasorSec(true);
        Kanit("klasor-secici.txt", sonuc.rapor);
        Assert.True(sonuc.cagri == 1, sonuc.rapor);
        Assert.NotNull(sonuc.ayar);
        Assert.Contains(hedef, Path.GetFileName(sonuc.ayar!));
        Assert.Equal(sonuc.ayar, sonuc.dosyada);
        Assert.NotNull(sonuc.goruntu);
        Assert.Equal(Path.GetFullPath(sonuc.ayar!), Path.GetDirectoryName(Path.GetFullPath(sonuc.goruntu!)));
        Assert.True(File.Exists(sonuc.goruntu), sonuc.rapor);
    }

    /// <summary>
    /// P3: ham sağ tık menüyü açar, ham ok ve Enter Ayarlar › Gelişmiş › Görüntü ›
    /// Taramalı görüntüyü düzelt satırına iner ve anahtarı çevirir; değer libmpv'den
    /// okunur. Aynı alt menüde sekmeye götüren satır bulunmaz.
    /// </summary>
    [Fact]
    public void P3HamSagTikVeOklarlaMenudenAyarDegisirMotordanOkunur()
    {
        var clip = MotorKlipleri.Kucuk;
        var body = new StringBuilder();
        try
        {
            AppHost.Run(() =>
            {
                var view = DenetimSurucu.Ac(clip, out var window);
                window.Show();
                DenetimSurucu.Wait(view, 0.3);
                DenetimSurucu.Duraklat(view);
                var motor = DenetimSurucu.Motor(view);
                body.AppendLine($"once: deinterlace {motor.GetProperty("deinterlace")}, ayar {view.Advanced.Picture.Deinterlace}");

                var onceki = Acilirlar();
                var yuzey = Merkez(window, view);
                Fareyle(window, RawPointerEventType.Move, yuzey, RawInputModifiers.None);
                Fareyle(window, RawPointerEventType.RightButtonDown, yuzey, RawInputModifiers.RightMouseButton);
                Fareyle(window, RawPointerEventType.RightButtonUp, yuzey, RawInputModifiers.None);

                var bilinen = new List<PopupRoot>(onceki);
                Inis(view, window, bilinen, body, Strings.Get("main.player.menu.settings"), Key.Right);
                var ayarMenusu = Inis(view, window, bilinen, body, Strings.Get("player.advanced.menu"), Key.Right);
                var basliklar = ayarMenusu.GetVisualDescendants().OfType<MenuItem>().Select(m => m.Header as string).ToList();
                body.AppendLine("ayarlar alt menusu: " + string.Join(" | ", basliklar));
                Assert.Contains(Strings.Get("settings.player-shortcuts.title"), basliklar);
                Assert.DoesNotContain(ayarMenusu.GetVisualDescendants().OfType<MenuItem>(), m => ReferenceEquals(m.Tag, Keymap.Settings));

                Inis(view, window, bilinen, body, Strings.Get("player.advanced.picture"), Key.Right);
                Inis(view, window, bilinen, body, Strings.Get("player.advanced.deinterlace"), Key.Enter);
                DenetimSurucu.Pump(view, () => view.Advanced.Picture.Deinterlace, 3);
                DenetimSurucu.Wait(view, 0.3);

                var okunan = motor.GetProperty("deinterlace");
                body.AppendLine($"sonra: deinterlace {okunan}, motor ayari {motor.Picture.Deinterlace}, gorunum {view.Advanced.Picture.Deinterlace}");
                Assert.Equal("yes", okunan);
                Assert.True(motor.Picture.Deinterlace);

                view.ResetPicture();
                view.Close();
                window.Close();
                return 0;
            });
        }
        finally
        {
            Kanit("p3-ham-menu-ayar.txt", body.ToString());
        }
    }

    /// <summary>
    /// P3: Kısayollar alt menüsü tabloyu göstermekle kalmaz, satırı da çalıştırır. Ham sağ tık
    /// menüyü açar, oklar Ayarlar › Kısayollar'a iner, Yavaşlat satırına ham fare tıklaması
    /// gider; hız libmpv'nin speed özelliğinden okunur, tablonun 0,05 adımıyla 1'den 0,95'e düşer.
    /// </summary>
    [Fact]
    public void P3KisayollarSatirinaHamTikEylemiOynaticidaUygular()
    {
        var clip = MotorKlipleri.Kucuk;
        var body = new StringBuilder();
        try
        {
            AppHost.Run(() =>
            {
                var view = DenetimSurucu.Ac(clip, out var window);
                window.Show();
                DenetimSurucu.Wait(view, 0.3);
                DenetimSurucu.Duraklat(view);
                var motor = DenetimSurucu.Motor(view);
                var onceHiz = MotorKanit.ReadDouble(motor, "speed");
                body.AppendLine($"once: speed {onceHiz}, gorunum {view.SpeedFactor}");

                var onceki = Acilirlar();
                var yuzey = Merkez(window, view);
                Fareyle(window, RawPointerEventType.Move, yuzey, RawInputModifiers.None);
                Fareyle(window, RawPointerEventType.RightButtonDown, yuzey, RawInputModifiers.RightMouseButton);
                Fareyle(window, RawPointerEventType.RightButtonUp, yuzey, RawInputModifiers.None);

                var bilinen = new List<PopupRoot>(onceki);
                Inis(view, window, bilinen, body, Strings.Get("main.player.menu.settings"), Key.Right);
                Inis(view, window, bilinen, body, Strings.Get("settings.player-shortcuts.title"), Key.Right);

                var hedef = Keymap.Rows.First(r => ReferenceEquals(r.Action, Keymap.Slower) && r.Input.Kind == PlayerInputKind.Key);
                var baslik = Keymap.Label(hedef);
                PopupRoot? kisayollar = null;
                DenetimSurucu.Pump(view, () => (kisayollar = AcikMenu(bilinen, baslik)) is not null, 5);
                Assert.True(kisayollar is not null, "kisayollar alt menusu acilmadi" + Environment.NewLine + body);
                DenetimSurucu.Wait(view, 0.2);

                var satirlar = kisayollar!.GetVisualDescendants().OfType<MenuItem>().ToList();
                body.AppendLine($"kisayol satiri {satirlar.Count}, tablo {Keymap.Rows.Count}");
                var satir = satirlar.First(m => Equals(m.Header, baslik));
                var tur = 0;
                for (; tur < 90 && !satir.IsSelected; tur++)
                {
                    Tus(kisayollar, Key.Down);
                    DenetimSurucu.Wait(view, 0.02);
                }

                bool Isabet(Point p) => kisayollar.InputHitTest(p) is Visual v && (ReferenceEquals(v, satir) || v.GetVisualAncestors().Contains(satir));
                DenetimSurucu.Pump(view, () => Isabet(Merkez(kisayollar, satir)), 3);
                var nokta = Merkez(kisayollar, satir);
                body.AppendLine($"'{baslik}': asagi ok {tur}, secili {satir.IsSelected}, nokta {nokta}, isabet {Isabet(nokta)}, menu {kisayollar.Bounds.Size}");
                Assert.True(satir.IsSelected, baslik + " satiri secilemedi" + Environment.NewLine + body);
                satir.AddHandler(InputElement.PointerPressedEvent, (_, _) => body.AppendLine("satir basildi"), Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
                satir.AddHandler(InputElement.PointerReleasedEvent, (_, _) => body.AppendLine("satir birakildi"), Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
                Fareyle(kisayollar, RawPointerEventType.Move, nokta, RawInputModifiers.None);
                DenetimSurucu.Wait(view, 0.05);
                DenetimSurucu.Pump(view, () => Isabet(Merkez(kisayollar, satir)), 3);
                nokta = Merkez(kisayollar, satir);
                body.AppendLine($"tiklama noktasi {nokta}, isabet {Isabet(nokta)}");
                Assert.True(Isabet(nokta), baslik + " satiri noktada degil" + Environment.NewLine + body);
                Fareyle(kisayollar, RawPointerEventType.LeftButtonDown, nokta, RawInputModifiers.LeftMouseButton);
                DenetimSurucu.Wait(view, 0.05);
                Fareyle(kisayollar, RawPointerEventType.LeftButtonUp, nokta, RawInputModifiers.None);
                DenetimSurucu.Pump(view, () => view.SpeedFactor < 1, 3);
                DenetimSurucu.Wait(view, 0.3);

                var sonraHiz = MotorKanit.ReadDouble(motor, "speed");
                body.AppendLine($"sonra: speed {sonraHiz}, gorunum {view.SpeedFactor}, menu kapandi {!Acilirlar().Contains(kisayollar)}");
                body.AppendLine("iz: " + string.Join(" | ", view.Trace));

                Assert.InRange(onceHiz, 0.995, 1.005);
                Assert.InRange(sonraHiz, 0.945, 0.955);
                Assert.InRange(view.SpeedFactor, 0.945, 0.955);
                Assert.Contains("satir basildi", body.ToString());

                view.Close();
                window.Close();
                return 0;
            });
        }
        finally
        {
            Kanit("p3-kisayol-ham-tik.txt", body.ToString());
        }
    }

    [Fact]
    public void GoruntuKlasoruSecimIptalindeAyarDegismez()
    {
        var sonuc = KlasorSec(false);
        Kanit("klasor-secici-iptal.txt", sonuc.rapor);
        Assert.True(sonuc.cagri == 1, sonuc.rapor);
        Assert.Null(sonuc.ayar);
        Assert.Null(sonuc.dosyada);
    }

    private static (string rapor, string? acilan, string ilk, string ikinci) DosyaSonu(RepeatMode kip)
    {
        var klasor = GorunumKanit.Gecici("otomatik-" + kip);
        var ilk = Klip(klasor, "a-ilk.mp4");
        var ikinci = Klip(klasor, "b-ikinci.mp4");
        var ayar = Path.Combine(klasor, "ayar", "history.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ayar)!);
        var s = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var view = DenetimSurucu.Ac(ilk, out var window, ayar);
            window.Show();
            view.Settings.Repeat = kip;
            DenetimSurucu.Wait(view, 0.2);
            body.AppendLine($"Space oncesi oynuyor: {view.IsPlaying}");
            Tus(window, Key.Space);
            DenetimSurucu.Wait(view, 0.1);
            if (!view.IsPlaying) Tus(window, Key.Space);
            DenetimSurucu.Pump(view, () => view.IsPlaying, 3);
            body.AppendLine($"Space sonrasi oynuyor: {view.IsPlaying}");
            DenetimSurucu.Pump(view, () => view.LoadedPath == ikinci, 8);
            GorunumKanit.Bekle(view, () => view.Navigation, 10);
            if (kip == RepeatMode.Off) DenetimSurucu.Wait(view, 1);
            var acilan = view.LoadedPath;
            body.AppendLine($"kip {kip}, acilan {Path.GetFileName(acilan)}, dosya sonu {view.Engine?.EndReached}");
            body.AppendLine("iz: " + string.Join(" | ", view.Trace));
            view.Close();
            window.Close();
            return (body.ToString(), acilan);
        });
        return (s.Item1, s.acilan, ilk, ikinci);
    }

    [Fact]
    public void GercekDosyaSonundaTumunuTekrarlaSonrakiDosyayiAcar()
    {
        var sonuc = DosyaSonu(RepeatMode.All);
        Kanit("otomatik-sonraki.txt", sonuc.rapor);
        Assert.Contains("Space sonrasi oynuyor: True", sonuc.rapor);
        Assert.Contains("play -> True", sonuc.rapor);
        Assert.Contains("autonext -> b-ikinci.mp4", sonuc.rapor);
        Assert.Equal(sonuc.ikinci, sonuc.acilan);
    }

    [Fact]
    public void GercekDosyaSonundaTekrarKapaliykenIlkDosyadaKalir()
    {
        var sonuc = DosyaSonu(RepeatMode.Off);
        Kanit("otomatik-sonraki-kapali.txt", sonuc.rapor);
        Assert.Contains("Space sonrasi oynuyor: True", sonuc.rapor);
        Assert.Contains("play -> True", sonuc.rapor);
        Assert.Contains("dosya sonu True", sonuc.rapor);
        Assert.DoesNotContain("autonext", sonuc.rapor);
        Assert.Equal(sonuc.ilk, sonuc.acilan);
    }

    [Fact]
    public void HamSurukleBirakDosyayiAcarKlasorVeSilinenDosyaAcmaz()
    {
        var clip = MotorKlipleri.Kucuk;
        var klasor = GorunumKanit.Gecici("birak");
        var ikinci = Klip(klasor, "birakilan.mp4");
        var silinen = Klip(klasor, "silinen.mp4");
        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var view = DenetimSurucu.Ac(clip, out var window);
            window.Show();
            DenetimSurucu.Wait(view, 0.3);
            var nokta = Merkez(window, view);

            body.AppendLine("klasor:");
            var klasorOgesi = window.StorageProvider.TryGetFolderFromPathAsync(new Uri(klasor));
            DenetimSurucu.Pump(view, () => klasorOgesi.IsCompleted, 5);
            Birak(window, nokta, klasorOgesi.Result!, body);
            DenetimSurucu.Wait(view, 0.3);
            body.AppendLine($"klasorden sonra acik: {Path.GetFileName(view.LoadedPath)}");

            body.AppendLine("silinen:");
            var silinenOgesi = ParcaKanit.Dosya(silinen);
            File.Delete(silinen);
            Birak(window, nokta, silinenOgesi, body);
            DenetimSurucu.Wait(view, 0.3);
            body.AppendLine($"silinenden sonra acik: {Path.GetFileName(view.LoadedPath)}");

            body.AppendLine("dosya:");
            Birak(window, nokta, ParcaKanit.Dosya(ikinci), body);
            DenetimSurucu.Pump(view, () => view.LoadedPath == ikinci, 10);
            GorunumKanit.Bekle(view, () => view.Navigation, 10);
            body.AppendLine($"dosyadan sonra acik: {Path.GetFileName(view.LoadedPath)}");
            body.AppendLine("iz: " + string.Join(" | ", view.Trace));
            view.Close();
            window.Close();
            return body.ToString();
        });

        Kanit("ham-birak.txt", rapor);
        var clipAdi = Path.GetFileName(clip);
        Assert.Contains($"klasorden sonra acik: {clipAdi}", rapor);
        Assert.Contains($"silinenden sonra acik: {clipAdi}", rapor);
        Assert.Contains("dosyadan sonra acik: birakilan.mp4", rapor);
        Assert.Contains("DragOver: etki Copy", rapor.Split("dosya:")[1]);
        Assert.DoesNotContain("DragOver: etki Copy", rapor.Split("dosya:")[0]);
        Assert.Contains("drop -> birakilan.mp4", rapor);
        Assert.DoesNotContain("drop -> silinen.mp4", rapor);
    }
}
