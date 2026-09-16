using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Şerit düğmelerini ham fare girdisiyle sınar: olay pencerenin platform girişinden verilir,
/// isabet testi, tünel işleyicileri ve düğmenin kendi basış mantığı gerçek yoldan geçer.
/// Sonuç motordan geri okunur.
/// </summary>
public sealed class OynaticiGercekGirdiTests
{
    private static readonly object Fare = Yeni(typeof(MouseDevice), new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true));

    private static object Yeni(Type tur, params object[] args)
        => Activator.CreateInstance(tur, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, args, null)!;

    private static void Gonder(Window window, RawPointerEventType tur, Point nokta, RawInputModifiers tuslar)
    {
        const System.Reflection.BindingFlags Her = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var impl = typeof(TopLevel).GetProperty("PlatformImpl", Her)!.GetValue(window)!;
        var kok = (IInputRoot)typeof(TopLevel).GetProperty("InputRoot", Her)!.GetValue(window)!;
        var giris = (Action<RawInputEventArgs>)impl.GetType().GetInterfaces()
            .Select(i => i.GetProperty("Input", Her)).First(p => p is not null)!.GetValue(impl)!;
        giris((RawInputEventArgs)Yeni(typeof(RawPointerEventArgs), Fare, (ulong)Environment.TickCount64, kok, tur, nokta, tuslar));
    }

    private static Point Merkez(Window window, Control control)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static void Tikla(Window window, PlayerView view, Point nokta)
    {
        Gonder(window, RawPointerEventType.Move, nokta, RawInputModifiers.None);
        DenetimSurucu.Wait(view, 0.05);
        Gonder(window, RawPointerEventType.LeftButtonDown, nokta, RawInputModifiers.LeftMouseButton);
        DenetimSurucu.Wait(view, 0.05);
        Gonder(window, RawPointerEventType.LeftButtonUp, nokta, RawInputModifiers.None);
        DenetimSurucu.Wait(view, 0.15);
    }

    private static string Kanit(string ad)
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "girdi");
        Directory.CreateDirectory(klasor);
        return Path.Combine(klasor, ad);
    }

    private static void Surukle(Window window, PlayerView view, Point bas, Point son)
    {
        Gonder(window, RawPointerEventType.Move, bas, RawInputModifiers.None);
        Gonder(window, RawPointerEventType.LeftButtonDown, bas, RawInputModifiers.LeftMouseButton);
        DenetimSurucu.Wait(view, 0.05);
        for (var i = 1; i <= 8; i++)
        {
            var p = new Point(bas.X + (son.X - bas.X) * i / 8, bas.Y);
            Gonder(window, RawPointerEventType.Move, p, RawInputModifiers.LeftMouseButton);
            DenetimSurucu.Wait(view, 0.02);
        }
        Gonder(window, RawPointerEventType.LeftButtonUp, son, RawInputModifiers.None);
        DenetimSurucu.Wait(view, 0.15);
    }

    [Fact]
    public void SeritDugmeleri_HamFareIle_MotoraUlasiyor()
    {
        var clip = MotorKlipleri.Kucuk;
        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var view = DenetimSurucu.Ac(clip, out var window);
            window.Width = 1280;
            window.Height = 720;
            window.Show();
            DenetimSurucu.Wait(view, 0.3);
            _ = view.SeritZone;
            var motor = DenetimSurucu.Motor(view);
            string Oku(string ad) => motor.GetProperty(ad) ?? "";

            var alt = new Point(view.Bounds.Width / 2, view.Bounds.Height - 10);
            Gonder(window, RawPointerEventType.Move, new Point(alt.X, 50), RawInputModifiers.None);
            Gonder(window, RawPointerEventType.Move, alt, RawInputModifiers.None);
            DenetimSurucu.Wait(view, 0.3);
            body.AppendLine($"serit acik: {view.SeritRevealed}");

            var mute = view.FindControl<Button>("BtnSeritMute")!;
            var muteOnce = Oku("mute");
            Tikla(window, view, Merkez(window, mute));
            var muteSonra = Oku("mute");
            body.AppendLine($"mute {muteOnce} -> {muteSonra}");

            var ses = view.FindControl<Slider>("SliderSeritVolume")!;
            var sesOnce = Oku("volume");
            var sesMerkez = Merkez(window, ses);
            var basparmak = ses.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Thumb>().First();
            var bp = Merkez(window, basparmak);
            Surukle(window, view, bp, new Point(bp.X - ses.Bounds.Width / 2, bp.Y));
            body.AppendLine($"ses {sesOnce} -> {Oku("volume")} (slider {ses.Value})");
            Tikla(window, view, new Point(sesMerkez.X - ses.Bounds.Width / 4, sesMerkez.Y));
            body.AppendLine($"ses tik -> {Oku("volume")} (slider {ses.Value})");
            var glif = view.FindControl<Avalonia.Controls.Shapes.Path>("GlyphSeritVolume")!;
            body.AppendLine($"sessiz simgesi: {ReferenceEquals(glif.Data, view.FindResource("IconVolumeMute"))}");
            body.AppendLine($"hiz etiketi: {view.SeritSpeedText}");

            var hiz = view.FindControl<Slider>("SliderSeritSpeed")!;
            var hizOnce = Oku("speed");
            var hizMerkez = Merkez(window, hiz);
            Tikla(window, view, new Point(hizMerkez.X + hiz.Bounds.Width / 3, hizMerkez.Y));
            var hizSonra = Oku("speed");
            body.AppendLine($"hiz {hizOnce} -> {hizSonra} (slider {hiz.Value})");

            var oynat = view.FindControl<Button>("BtnSeritPlay")!;
            var duraklatOnce = Oku("pause");
            Tikla(window, view, Merkez(window, oynat));
            body.AppendLine($"pause {duraklatOnce} -> {Oku("pause")}");
            body.AppendLine("iz: " + string.Join(" | ", view.Trace));

            window.Close();
            return body.ToString();
        });

        File.WriteAllText(Kanit("serit-ham-fare.txt"), rapor, new UTF8Encoding(false));
        Assert.Contains("serit acik: True", rapor);
        Assert.Contains("mute no -> yes", rapor);
        Assert.DoesNotMatch(@"ses (\S+) -> \1 ", rapor);
        Assert.DoesNotMatch(@"hiz (\S+) -> \1 ", rapor);
        Assert.Contains("sessiz simgesi: True", rapor);
        Assert.Contains("hiz etiketi: 1.00×", rapor);
        Assert.Contains("pause no -> yes", rapor);
    }

    [Fact]
    public void SeritDugmeleri_AnaPencerede_HamFareIle_MotoraUlasiyor()
    {
        var clip = MotorKlipleri.Kucuk;
        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var window = new VidShrink.App.MainWindow { WindowState = WindowState.Normal, Width = 1280, Height = 720 };
            var view = window.PlayerTab;
            view.EngineFactory = () =>
            {
                var engine = new VidShrink.Player.MpvEngine();
                engine.SetProperty("ao", "null");
                return engine;
            };
            window.FindControl<TabControl>("Tabs")!.SelectedIndex = 0;
            window.Show();
            var open = view.OpenAsync(clip);
            DenetimSurucu.Pump(view, () => open.IsCompleted, 20);
            DenetimSurucu.Pump(view, () => view.Engine is { FramesRendered: > 0 }, 10);
            DenetimSurucu.Wait(view, 0.5);
            _ = view.SeritZone;
            var motor = DenetimSurucu.Motor(view);
            string Oku(string ad) => motor.GetProperty(ad) ?? "";

            var alt = view.TranslatePoint(new Point(view.Bounds.Width / 2, view.Bounds.Height - 10), window)!.Value;
            Gonder(window, RawPointerEventType.Move, new Point(alt.X, 300), RawInputModifiers.None);
            Gonder(window, RawPointerEventType.Move, alt, RawInputModifiers.None);
            DenetimSurucu.Wait(view, 0.3);
            body.AppendLine($"oynuyor: {view.IsPlaying} serit acik: {view.SeritRevealed} view {view.Bounds} alt {alt}");
            var isabet = window.InputHitTest(alt);
            body.AppendLine($"isabet: {isabet?.GetType().Name} {(isabet as Control)?.Name}");

            var mute = view.FindControl<Button>("BtnSeritMute")!;
            var mp = Merkez(window, mute);
            var isabet2 = window.InputHitTest(mp);
            body.AppendLine($"mute isabet: {isabet2?.GetType().Name} {(isabet2 as Control)?.Name}");
            var muteOnce = Oku("mute");
            Tikla(window, view, mp);
            body.AppendLine($"mute {muteOnce} -> {Oku("mute")}");

            var ses = view.FindControl<Slider>("SliderSeritVolume")!;
            var sm = Merkez(window, ses);
            Tikla(window, view, new Point(sm.X - ses.Bounds.Width / 4, sm.Y));
            body.AppendLine($"ses tik -> {Oku("volume")} (slider {ses.Value})");
            body.AppendLine("iz: " + string.Join(" | ", view.Trace));
            window.Classes.Remove("chrome-hidden");
            DenetimSurucu.Wait(view, 0.4);
            var boyut = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
            using (var kare = new Avalonia.Media.Imaging.RenderTargetBitmap(boyut, new Vector(96, 96)))
            {
                kare.Render(window);
                kare.Save(Kanit("serit-ana-pencere.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
            window.Close();
            return body.ToString();
        });

        File.WriteAllText(Kanit("serit-ana-pencere.txt"), rapor, new UTF8Encoding(false));
        Assert.Contains("serit acik: True", rapor);
        Assert.Contains("mute no -> yes", rapor);
        Assert.DoesNotContain("ses tik -> 100", rapor);
    }
}
