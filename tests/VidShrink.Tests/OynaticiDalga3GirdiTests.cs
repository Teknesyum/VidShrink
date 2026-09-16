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

    private static PopupRoot? AcikMenu()
    {
        var tur = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Avalonia.Win32.WindowImpl")).FirstOrDefault(t => t is not null);
        if (tur?.GetField("s_instances", Her)?.GetValue(null) is not System.Collections.IEnumerable impller) return null;
        foreach (var impl in impller)
            if (Giris(impl)?.Target is { } kaynak && kaynak.GetType().GetProperties(Her).Where(p => p.GetIndexParameters().Length == 0 && typeof(Visual).IsAssignableFrom(p.PropertyType)).Select(p => p.GetValue(kaynak)).OfType<PopupRoot>().FirstOrDefault() is { IsVisible: true } kok) return kok;
        return null;
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

            var yuzey = Merkez(window, view);
            Fareyle(window, RawPointerEventType.Move, yuzey, RawInputModifiers.None);
            Fareyle(window, RawPointerEventType.RightButtonDown, yuzey, RawInputModifiers.RightMouseButton);
            Fareyle(window, RawPointerEventType.RightButtonUp, yuzey, RawInputModifiers.None);
            PopupRoot? menu = null;
            DenetimSurucu.Pump(view, () => (menu = AcikMenu()) is not null, 5);
            body.AppendLine($"menu acildi: {menu is not null}, capa {view.MenuAnchor}");
            Assert.NotNull(menu);
            DenetimSurucu.Wait(view, 0.3);

            var baslik = Strings.Get("player.view.screenshot-folder");
            var satir = menu!.GetVisualDescendants().OfType<MenuItem>().First(m => Equals(m.Header, baslik));
            var orta = new Point(menu.Bounds.Width / 2, menu.Bounds.Height / 2);
            for (var i = 0; i < 40 && Merkez(menu, satir).Y > menu.Bounds.Height - satir.Bounds.Height; i++)
            {
                Ham(menu, (RawInputEventArgs)Yeni(typeof(RawMouseWheelEventArgs), Fare, (ulong)Environment.TickCount64, Kok(menu), orta, new Vector(0, -1), RawInputModifiers.None));
                DenetimSurucu.Wait(view, 0.05);
            }
            var nokta = Merkez(menu, satir);
            body.AppendLine($"satir noktasi {nokta}, menu yuksekligi {menu.Bounds.Height}");
            Fareyle(menu, RawPointerEventType.Move, nokta, RawInputModifiers.None);
            DenetimSurucu.Wait(view, 0.05);
            Fareyle(menu, RawPointerEventType.LeftButtonDown, nokta, RawInputModifiers.LeftMouseButton);
            DenetimSurucu.Wait(view, 0.05);
            Fareyle(menu, RawPointerEventType.LeftButtonUp, nokta, RawInputModifiers.None);
            DenetimSurucu.Pump(view, () => secici.Cagri > 0 && view.Settings.ScreenshotFolder is not null, 3);
            DenetimSurucu.Wait(view, 0.2);
            body.AppendLine($"secici cagrisi {secici.Cagri}, menu kapandi {AcikMenu() is null}");

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
        Assert.Equal(1, sonuc.cagri);
        Assert.NotNull(sonuc.ayar);
        Assert.Contains(hedef, Path.GetFileName(sonuc.ayar!));
        Assert.Equal(sonuc.ayar, sonuc.dosyada);
        Assert.NotNull(sonuc.goruntu);
        Assert.Equal(Path.GetFullPath(sonuc.ayar!), Path.GetDirectoryName(Path.GetFullPath(sonuc.goruntu!)));
        Assert.True(File.Exists(sonuc.goruntu), sonuc.rapor);
    }

    [Fact]
    public void GoruntuKlasoruSecimIptalindeAyarDegismez()
    {
        var sonuc = KlasorSec(false);
        Kanit("klasor-secici-iptal.txt", sonuc.rapor);
        Assert.Equal(1, sonuc.cagri);
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
