using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class KisayolKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-kisayol");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static string Liste
    {
        get
        {
            var path = Path.Combine(Folder, "liste");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
    {
        var path = Path.Combine(Folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body, new UTF8Encoding(false));
    }

    /// <summary>
    /// Son asertten sonra çağrılır: yeşil koşum kendi bıraktığını siler, kırmızı koşum
    /// kanıtını korur çünkü düşen asert buraya hiç gelmez. Klasör boşalınca o da gider.
    /// Ad bir klasörse (testin geçici kökü) ağacıyla birlikte gider.
    /// </summary>
    internal static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-kisayol"), adlar);

    private static string Uret(string path, params string[] args)
    {
        if (File.Exists(path) && new FileInfo(path).Length > 0) return path;
        Assert.True(ToolLocator.IsAvailable(out var missing), $"klip uretimi icin {missing} gerekli");
        var partial = Path.Combine(Path.GetDirectoryName(path)!, "part-" + Path.GetFileName(path));
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-hide_banner");
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        psi.ArgumentList.Add(partial);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(180_000))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException(Path.GetFileName(path) + " 180 sn icinde uretilmedi");
        }

        stdout.GetAwaiter().GetResult();
        var log = stderr.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, log[Math.Max(0, log.Length - 600)..]);
        File.Move(partial, path, true);
        return path;
    }

    internal static string IkiRenk => Uret(Path.Combine(Folder, "kirmizi-sol-mavi-sag-320x180.mp4"),
        "-f", "lavfi", "-i", "color=c=blue:size=320x180:rate=25:duration=6",
        "-vf", "drawbox=x=0:y=0:w=160:h=180:color=red:t=fill",
        "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p");

    internal static string Uzun
    {
        get
        {
            var srt = Path.Combine(Folder, "uzun.srt");
            if (!File.Exists(srt)) File.WriteAllText(srt, "1\r\n00:00:00,000 --> 00:11:40,000\r\nalt yazi\r\n\r\n", new UTF8Encoding(false));
            return Uret(Path.Combine(Liste, "a-uzun-700sn.mkv"),
                "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=5:duration=700",
                "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=8000:duration=700",
                "-f", "lavfi", "-i", "sine=frequency=880:sample_rate=8000:duration=700",
                "-i", srt,
                "-map", "0:v", "-map", "1:a", "-map", "2:a", "-map", "3:s",
                "-c:v", "libx264", "-preset", "ultrafast", "-g", "5", "-pix_fmt", "yuv420p",
                "-c:a", "aac", "-b:a", "16k", "-ac", "1", "-c:s", "srt");
        }
    }

    internal static string Ikinci
    {
        get
        {
            var path = Path.Combine(Liste, "b-iki-renk.mp4");
            if (!File.Exists(path)) File.Copy(IkiRenk, path, true);
            return path;
        }
    }

    internal static (int W, int H, string Ust, string Alt, string Sol, string Sag) Kare(PlayerView view)
    {
        var frame = view.FindControl<Image>("Frame")!;
        if (frame.Source is not WriteableBitmap bitmap) return (0, 0, "-", "-", "-", "-");
        using var buffer = bitmap.Lock();
        var w = buffer.Size.Width;
        var h = buffer.Size.Height;
        string Renk(int x, int y)
        {
            var offset = y * buffer.RowBytes + x * 4;
            var b = Marshal.ReadByte(buffer.Address, offset);
            var r = Marshal.ReadByte(buffer.Address, offset + 2);
            return r > 150 && b < 100 ? "kirmizi" : b > 150 && r < 100 ? "mavi" : $"r{r}b{b}";
        }

        return (w, h, Renk(w / 2, h / 6), Renk(w / 2, h * 5 / 6), Renk(w / 6, h / 2), Renk(w * 5 / 6, h / 2));
    }
}

internal sealed class KisayolOrtam
{
    internal required PlayerView View { get; init; }
    internal required Window Window { get; init; }
    internal required TextBox Kutu { get; init; }
    internal required StringBuilder Kayit { get; init; }

    internal MpvEngine Motor => (MpvEngine)View.Engine!;

    internal string Oku(string name) => Motor.GetProperty(name) ?? "";

    internal double Sayi(string name) => MotorKanit.ReadDouble(Motor, name);

    internal Control Odak => View.FindControl<Slider>("SliderSeritVolume")!;

    internal void Bas(Key key, KeyModifiers mods = KeyModifiers.None, string? symbol = null, Control? hedef = null)
    {
        (hedef ?? Odak).RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = mods,
            KeySymbol = symbol
        });
        Kayit.AppendLine($"  tus {mods}+{key} '{symbol}' -> iz {(View.Trace.Count > 0 ? View.Trace[^1] : "-")}");
    }

    internal void Bekle(double seconds = 0.3) => DenetimSurucu.Wait(View, seconds);

    internal void Bekle(Func<bool> done, double seconds = 5) => DenetimSurucu.Pump(View, done, seconds);

    internal void Aramayi() => DenetimSurucu.Bekle(View);

    internal void Git(double at) => DenetimSurucu.Git(View, at);

    internal void Not(string line) => Kayit.AppendLine("  " + line);

    internal static KisayolOrtam Ac(string clip, string ad)
    {
        var history = Path.Combine(KisayolKanit.Folder, "gecmis", ad + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(history)!);
        if (File.Exists(history)) File.Delete(history);

        var view = new PlayerView
        {
            EngineFactory = () =>
            {
                var engine = new MpvEngine();
                engine.SetProperty("ao", "null");
                return engine;
            },
            HistoryPath = () => history
        };
        var kutu = new TextBox();
        var panel = new DockPanel();
        DockPanel.SetDock(kutu, Dock.Top);
        panel.Children.Add(kutu);
        panel.Children.Add(view);
        var window = new Window { Width = 640, Height = 480, Content = panel };
        window.Show();

        var open = view.OpenAsync(clip);
        DenetimSurucu.Pump(view, () => open.IsCompleted, 20);
        open.GetAwaiter().GetResult();
        DenetimSurucu.Pump(view, () => view.Seek.Idle.IsCompleted && view.Engine!.FramesRendered > 0, 10);
        var ortam = new KisayolOrtam { View = view, Window = window, Kutu = kutu, Kayit = new StringBuilder() };
        if (view.IsPlaying) view.Apply(new PlayerCommand(PlayerCommandKind.TogglePlay, 0));
        ortam.Bekle(() => ortam.Motor.IsPaused, 3);
        return ortam;
    }

    internal void Kapat()
    {
        foreach (var owned in Window.OwnedWindows.ToList()) owned.Close();
        View.Close();
        Window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}

public sealed class OynaticiKisayolTests
{
    private const double Tolerans = 0.35;

    public static TheoryData<string> Tuslar => new()
    {
        "Bosluk", "CtrlP", "CtrlBosluk", "Geri", "Enter", "AltEnter", "Esc", "Menu",
        "Sag", "Sol", "CtrlSag", "CtrlSol", "ShiftSag", "ShiftSol", "AltSag", "AltSol",
        "Yukari", "Asagi", "M", "C", "X", "Z", "CtrlShiftF", "CtrlShiftB", "CtrlShiftN",
        "F", "ShiftF", "CtrlBuyuktur", "CtrlKucuktur",
        "KoseliAc", "KoseliKapa", "KoseliAcTrQ", "EgikCizgi",
        "A", "S", "Buyuktur", "Kucuktur", "CtrlNokta", "CtrlVirgul",
        "CtrlF5", "CtrlShiftS", "CtrlH", "CtrlA", "CtrlF1", "CtrlE",
        "PgDn", "PgUp", "CtrlAltShiftF", "CtrlAltShiftB",
        "N", "B", "ShiftPgDn", "ShiftPgUp",
        "CtrlK", "CtrlShiftG", "CtrlM", "CtrlU",
        "Teker", "CtrlTeker", "ShiftTeker", "CtrlShiftTeker", "AltTeker", "SolTik", "SagTik", "CiftTik", "OrtaTik", "CtrlT"
    };

    [Theory]
    [MemberData(nameof(Tuslar))]
    public void KisayolMotoraUlasirVeGeriOkunur(string ad)
    {
        var (kayit, hata) = AppHost.Run(() =>
        {
            var ortam = KisayolOrtam.Ac(Klip(ad), ad);
            try
            {
                ortam.Kayit.AppendLine("[" + ad + "]");
                var sonuc = Olc(ad, ortam);
                ortam.Kayit.AppendLine(sonuc is null ? "  GECTI" : "  KALDI " + sonuc);
                return (ortam.Kayit.ToString(), sonuc);
            }
            finally
            {
                ortam.Kapat();
            }
        });

        KisayolKanit.Write(Path.Combine("tuslar", ad + ".txt"), kayit);
        Assert.True(hata is null, kayit);
        KisayolKanit.Kapat(Path.Combine("tuslar", ad + ".txt"), Path.Combine("gecmis", ad + ".json"), "goruntu", "klip-" + ad);
    }

    private static string Klip(string ad) => ad switch
    {
        "CtrlShiftS" or "CtrlH" or "PgUp" => KisayolKanit.Ikinci,
        _ => KisayolKanit.Uzun
    };

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string? Fark(KisayolOrtam o, Key key, KeyModifiers mods, double beklenen, string? symbol = null)
    {
        o.Git(350);
        var once = o.Sayi("time-pos");
        o.Bas(key, mods, symbol);
        o.Aramayi();
        o.Bekle(() => Math.Abs(o.Sayi("time-pos") - once - beklenen) < Tolerans, 3);
        var sonra = o.Sayi("time-pos");
        o.Not($"time-pos {F(once)} -> {F(sonra)}, beklenen fark {F(beklenen)}");
        return Math.Abs(sonra - once - beklenen) < Tolerans ? null : $"time-pos farki {F(sonra - once)}";
    }

    private static string? Hiz(KisayolOrtam o, Key key, KeyModifiers mods, double beklenen, double baslangic = 1)
    {
        o.View.Apply(new PlayerCommand(PlayerCommandKind.Speed, baslangic - o.View.SpeedFactor));
        o.Bekle(0.2);
        var once = o.Sayi("speed");
        o.Bas(key, mods);
        o.Bekle(() => Math.Abs(o.Sayi("speed") - beklenen) < 1e-6, 2);
        var sonra = o.Sayi("speed");
        o.Not($"speed {F(once)} -> {F(sonra)}, beklenen {F(beklenen)}");
        return Math.Abs(sonra - beklenen) < 1e-6 ? null : $"speed {F(sonra)}";
    }

    private static string? Kare(KisayolOrtam o, Key key, KeyModifiers mods, int yon, string? symbol = null)
    {
        o.Git(350);
        o.Bekle(0.3);
        var once = o.Sayi("time-pos");
        o.Bas(key, mods, symbol);
        o.Bekle(() => Math.Abs(o.Sayi("time-pos") - once) > 0.1, 3);
        o.Bekle(0.2);
        var sonra = o.Sayi("time-pos");
        var fark = sonra - once;
        o.Not($"time-pos {F(once)} -> {F(sonra)}, pause {o.Oku("pause")}, bir kare 0.2 sn, yon {yon}");
        return Math.Abs(fark - yon * 0.2) < 0.05 && o.Oku("pause") == "yes" ? null : $"kare farki {F(fark)}";
    }

    private static string? Gecikme(KisayolOrtam o, string ozellik, Key key, KeyModifiers mods, string? symbol, double beklenen)
    {
        var once = o.Sayi(ozellik);
        o.Bas(key, mods, symbol);
        o.Bekle(() => Math.Abs(o.Sayi(ozellik) - once - beklenen) < 1e-6, 2);
        var sonra = o.Sayi(ozellik);
        o.Not($"{ozellik} {F(once)} -> {F(sonra)}, beklenen fark {F(beklenen)}");
        return Math.Abs(sonra - once - beklenen) < 1e-6 ? null : $"{ozellik} farki {F(sonra - once)}";
    }

    private static string? Dongu(KisayolOrtam o, Key key, KeyModifiers mods, string? symbol, string ozellik, double at)
    {
        o.Git(at);
        o.Bas(key, mods, symbol);
        o.Bekle(() => Math.Abs(o.Sayi(ozellik) - at) < Tolerans, 2);
        var deger = o.Oku(ozellik);
        o.Not($"{ozellik} {deger}, konum {F(at)}");
        return Math.Abs(o.Sayi(ozellik) - at) < Tolerans ? null : $"{ozellik} {deger}";
    }

    private static string? Pencere(KisayolOrtam o, Key key, KeyModifiers mods, WindowState beklenen)
    {
        var once = o.Window.WindowState;
        o.Bas(key, mods);
        o.Bekle(0.2);
        o.Not($"WindowState {once} -> {o.Window.WindowState}, beklenen {beklenen}");
        return o.Window.WindowState == beklenen ? null : $"WindowState {o.Window.WindowState}";
    }

    private static string? Teker(KisayolOrtam o, KeyModifiers mods, double beklenen)
    {
        o.Git(350);
        var once = o.Sayi("time-pos");
        GirdiSurucu.Wheel(o.View, 1, mods);
        o.Aramayi();
        o.Bekle(() => Math.Abs(o.Sayi("time-pos") - once - beklenen) < Tolerans, 3);
        var sonra = o.Sayi("time-pos");
        o.Not($"teker {mods}: time-pos {F(once)} -> {F(sonra)}, beklenen fark {F(beklenen)}");
        return Math.Abs(sonra - once - beklenen) < Tolerans ? null : $"time-pos farki {F(sonra - once)}";
    }

    private static string? Olc(string ad, KisayolOrtam o)
    {
        switch (ad)
        {
            case "Bosluk":
            case "CtrlP":
            {
                var mods = ad == "CtrlP" ? KeyModifiers.Control : KeyModifiers.None;
                var key = ad == "CtrlP" ? Key.P : Key.Space;
                var once = o.Oku("pause");
                o.Bas(key, mods, ad == "CtrlP" ? null : " ");
                o.Bekle(() => o.Oku("pause") == "no", 3);
                var bir = o.Oku("pause");
                o.Bas(key, mods, ad == "CtrlP" ? null : " ");
                o.Bekle(() => o.Oku("pause") == "yes", 3);
                var iki = o.Oku("pause");
                o.Not($"pause {once} -> {bir} -> {iki}");
                return once == "yes" && bir == "no" && iki == "yes" ? null : "pause gecisi yok";
            }
            case "CtrlBosluk":
            {
                o.Git(350);
                o.View.Apply(new PlayerCommand(PlayerCommandKind.TogglePlay, 0));
                o.Bekle(() => o.Oku("pause") == "no", 3);
                o.Bas(Key.Space, KeyModifiers.Control);
                o.Aramayi();
                o.Bekle(() => o.Oku("pause") == "yes" && o.Sayi("time-pos") < Tolerans, 3);
                o.Not($"pause {o.Oku("pause")}, time-pos {F(o.Sayi("time-pos"))}");
                return o.Oku("pause") == "yes" && o.Sayi("time-pos") < Tolerans ? null : "durmadi ya da basa donmedi";
            }
            case "Geri":
            {
                o.Git(350);
                o.Bas(Key.Back);
                o.Aramayi();
                o.Bekle(() => o.Sayi("time-pos") < Tolerans, 3);
                o.Not($"time-pos {F(o.Sayi("time-pos"))}");
                return o.Sayi("time-pos") < Tolerans ? null : "basa donmedi";
            }
            case "Enter": return Pencere(o, Key.Enter, KeyModifiers.None, WindowState.FullScreen);
            case "AltEnter": return Pencere(o, Key.Enter, KeyModifiers.Alt, WindowState.FullScreen);
            case "Esc":
                o.Bas(Key.Enter);
                o.Bekle(0.2);
                return Pencere(o, Key.Escape, KeyModifiers.None, WindowState.Normal);
            case "Menu":
                o.Bas(Key.Apps);
                o.Bekle(0.3);
                o.Not($"menu acik {o.View.MenuOpen}");
                return o.View.MenuOpen ? null : "menu acilmadi";
            case "Sag": return Fark(o, Key.Right, KeyModifiers.None, 10);
            case "Sol": return Fark(o, Key.Left, KeyModifiers.None, -10);
            case "CtrlSag": return Fark(o, Key.Right, KeyModifiers.Control, 60);
            case "CtrlSol": return Fark(o, Key.Left, KeyModifiers.Control, -60);
            case "ShiftSag": return Fark(o, Key.Right, KeyModifiers.Shift, 300);
            case "ShiftSol": return Fark(o, Key.Left, KeyModifiers.Shift, -300);
            case "AltSag": return Fark(o, Key.Right, KeyModifiers.Alt, 1);
            case "AltSol": return Fark(o, Key.Left, KeyModifiers.Alt, -1);
            case "Yukari":
            case "Asagi":
            {
                o.View.Apply(new PlayerCommand(PlayerCommandKind.Volume, 50 - o.View.VolumeLevel));
                o.Bekle(0.2);
                var beklenen = ad == "Yukari" ? 55 : 45;
                var once = o.Sayi("volume");
                o.Bas(ad == "Yukari" ? Key.Up : Key.Down);
                o.Bekle(() => Math.Abs(o.Sayi("volume") - beklenen) < 1e-6, 2);
                o.Not($"volume {F(once)} -> {F(o.Sayi("volume"))}, beklenen {beklenen}");
                return Math.Abs(o.Sayi("volume") - beklenen) < 1e-6 ? null : "volume degismedi";
            }
            case "M":
            {
                var once = o.Oku("mute");
                o.Bas(Key.M, KeyModifiers.None, "m");
                o.Bekle(() => o.Oku("mute") == "yes", 2);
                var bir = o.Oku("mute");
                o.Bas(Key.M, KeyModifiers.None, "m");
                o.Bekle(() => o.Oku("mute") == "no", 2);
                o.Not($"mute {once} -> {bir} -> {o.Oku("mute")}");
                return once == "no" && bir == "yes" && o.Oku("mute") == "no" ? null : "mute gecisi yok";
            }
            case "C": return Hiz(o, Key.C, KeyModifiers.None, 1.05);
            case "X": return Hiz(o, Key.X, KeyModifiers.None, 0.95);
            case "Z": return Hiz(o, Key.Z, KeyModifiers.None, 1, 1.5);
            case "CtrlShiftF": return Hiz(o, Key.F, KeyModifiers.Control | KeyModifiers.Shift, 1.05);
            case "CtrlShiftB": return Hiz(o, Key.B, KeyModifiers.Control | KeyModifiers.Shift, 0.95);
            case "CtrlShiftN": return Hiz(o, Key.N, KeyModifiers.Control | KeyModifiers.Shift, 1, 1.5);
            case "F": return Kare(o, Key.F, KeyModifiers.None, 1, "f");
            case "ShiftF": return Kare(o, Key.F, KeyModifiers.Shift, -1, "F");
            case "CtrlBuyuktur": return Kare(o, Key.OemPeriod, KeyModifiers.Control | KeyModifiers.Shift, 1, ">");
            case "CtrlKucuktur": return Kare(o, Key.OemComma, KeyModifiers.Control | KeyModifiers.Shift, -1, "<");
            case "KoseliAc": return Dongu(o, Key.OemOpenBrackets, KeyModifiers.None, "[", "ab-loop-a", 120);
            case "KoseliAcTrQ": return Dongu(o, Key.D8, KeyModifiers.Control | KeyModifiers.Alt, "[", "ab-loop-a", 130);
            case "KoseliKapa":
                o.Git(100);
                o.Bas(Key.OemOpenBrackets, KeyModifiers.None, "[");
                return Dongu(o, Key.OemCloseBrackets, KeyModifiers.None, "]", "ab-loop-b", 140);
            case "EgikCizgi":
            {
                o.Git(100);
                o.Bas(Key.OemOpenBrackets, KeyModifiers.None, "[");
                o.Bekle(() => o.Oku("ab-loop-a") != "no", 2);
                var once = o.Oku("ab-loop-a");
                o.Bas(Key.Oem2, KeyModifiers.None, "/");
                o.Bekle(() => o.Oku("ab-loop-a") == "no", 2);
                o.Not($"ab-loop-a {once} -> {o.Oku("ab-loop-a")}, ab-loop-b {o.Oku("ab-loop-b")}");
                return once != "no" && o.Oku("ab-loop-a") == "no" && o.Oku("ab-loop-b") == "no" ? null : "A-B kalkmadi";
            }
            case "A":
            {
                var once = o.Oku("aid");
                o.Bas(Key.A, KeyModifiers.None, "a");
                o.Bekle(() => o.Oku("aid") != once, 2);
                o.Not($"aid {once} -> {o.Oku("aid")}");
                return once == "1" && o.Oku("aid") == "2" ? null : "aid degismedi";
            }
            case "S":
            {
                var once = o.Oku("sid");
                o.Bas(Key.S, KeyModifiers.None, "s");
                o.Bekle(() => o.Oku("sid") != once, 2);
                o.Not($"sid {once} -> {o.Oku("sid")}");
                return o.Oku("sid") != once && o.Oku("sid") != "" ? null : "sid degismedi";
            }
            case "Buyuktur": return Gecikme(o, "sub-delay", Key.OemPeriod, KeyModifiers.Shift, ">", SubtitleOptions.SubtitleDelayStep);
            case "Kucuktur": return Gecikme(o, "sub-delay", Key.OemComma, KeyModifiers.Shift, "<", -SubtitleOptions.SubtitleDelayStep);
            case "CtrlNokta": return Gecikme(o, "audio-delay", Key.OemPeriod, KeyModifiers.Control, null, SubtitleOptions.AudioDelayStep);
            case "CtrlVirgul": return Gecikme(o, "audio-delay", Key.OemComma, KeyModifiers.Control, null, -SubtitleOptions.AudioDelayStep);
            case "CtrlF5":
            {
                var once = o.Oku("video-aspect-override");
                o.Bas(Key.F5, KeyModifiers.Control);
                o.Bekle(() => o.Oku("video-aspect-override") != once, 2);
                o.Not($"video-aspect-override {once} -> {o.Oku("video-aspect-override")}");
                return o.Oku("video-aspect-override") != once ? null : "oran degismedi";
            }
            case "CtrlShiftS":
            {
                var satirlar = new List<string>();
                var beklenen = new[]
                {
                    (90, 180, 320, "kirmizi", "mavi"),
                    (180, 320, 180, "mavi", "kirmizi"),
                    (270, 180, 320, "mavi", "kirmizi"),
                    (0, 320, 180, "kirmizi", "mavi")
                };
                string? hata = null;
                o.Not($"once {KisayolKanit.Kare(o.View)} Rotation {o.Motor.Rotation}");
                foreach (var (aci, w, h, ilk, ikinci) in beklenen)
                {
                    bool Oturdu()
                    {
                        if (o.Motor.Rotation != aci) return false;
                        var k = KisayolKanit.Kare(o.View);
                        var yer = aci is 90 or 270 ? (k.Ust, k.Alt) : (k.Sol, k.Sag);
                        return k.W == w && k.H == h && yer == (ilk, ikinci);
                    }

                    o.Bas(Key.S, KeyModifiers.Control | KeyModifiers.Shift);
                    o.Bekle(Oturdu, 5);
                    var kare = KisayolKanit.Kare(o.View);
                    o.Not($"Rotation {o.Motor.Rotation} vf '{o.Oku("vf")}' dwidth {o.Oku("dwidth")}x{o.Oku("dheight")} kare {kare}");
                    var konum = aci is 90 or 270 ? (kare.Ust, kare.Alt) : (kare.Sol, kare.Sag);
                    if (o.Motor.Rotation != aci || kare.W != w || kare.H != h || konum != (ilk, ikinci))
                        hata ??= $"{aci} derecede kare {kare}, Rotation {o.Motor.Rotation}";
                }

                return hata;
            }
            case "CtrlH":
            {
                var once = KisayolKanit.Kare(o.View);
                o.Bas(Key.H, KeyModifiers.Control);
                o.Bekle(() => KisayolKanit.Kare(o.View).Sol == "mavi", 3);
                var kare = KisayolKanit.Kare(o.View);
                o.Not($"once {once}, sonra {kare}, Mirrored {o.Motor.Mirrored}, vf '{o.Oku("vf")}'");
                return once.Sol == "kirmizi" && kare.Sol == "mavi" && kare.Sag == "kirmizi" && o.Motor.Mirrored ? null : "ayna kareye yansimadi";
            }
            case "CtrlA":
            case "CtrlT":
            {
                var once = o.Window.Topmost;
                o.Bas(ad == "CtrlA" ? Key.A : Key.T, KeyModifiers.Control);
                o.Not($"Topmost {once} -> {o.Window.Topmost}");
                return !once && o.Window.Topmost ? null : "Topmost degismedi";
            }
            case "CtrlF1":
            {
                o.Bas(Key.F1, KeyModifiers.Control);
                o.Bekle(0.2);
                var bilgi = o.View.InfoText;
                o.Not($"InfoPanel {o.View.InfoVisible}, metin '{bilgi.Replace(Environment.NewLine, " / ")}'");
                return o.View.InfoVisible && bilgi.Contains("h264", StringComparison.OrdinalIgnoreCase) ? null : "bilgi paneli motor bilgisini gostermedi";
            }
            case "CtrlE":
            {
                var klasor = Path.Combine(KisayolKanit.Folder, "goruntu");
                if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
                o.View.Settings.ScreenshotFolder = klasor;
                o.Bas(Key.E, KeyModifiers.Control);
                o.Bekle(() => o.View.LastScreenshot.IsCompleted, 15);
                var yol = o.View.LastScreenshot.IsCompleted ? o.View.LastScreenshot.Result : null;
                var boyut = yol is not null && File.Exists(yol) ? new FileInfo(yol).Length : 0;
                o.Not($"dosya {yol ?? "-"}, {boyut} bayt");
                return boyut > 0 ? null : "ekran goruntusu dosyasi yok";
            }
            case "PgDn":
            case "PgUp":
            {
                var hedef = ad == "PgDn" ? KisayolKanit.Ikinci : KisayolKanit.Uzun;
                var once = o.Oku("path");
                o.Bas(ad == "PgDn" ? Key.PageDown : Key.PageUp);
                o.Bekle(() => o.View.Navigation.IsCompleted && string.Equals(Path.GetFullPath(o.Oku("path")), hedef, StringComparison.OrdinalIgnoreCase), 15);
                o.Not($"path {Path.GetFileName(once)} -> {Path.GetFileName(o.Oku("path"))}");
                return string.Equals(Path.GetFileName(o.Oku("path")), Path.GetFileName(hedef), StringComparison.OrdinalIgnoreCase) ? null : "dosya degismedi";
            }
            case "CtrlAltShiftF":
            {
                var mods = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift;
                var once = o.View.Settings.Shuffle;
                o.Bas(Key.F, mods);
                var bir = o.View.Settings.Shuffle;
                o.Bas(Key.F, mods);
                o.Not($"Shuffle {once} -> {bir} -> {o.View.Settings.Shuffle}");
                return bir != once && o.View.Settings.Shuffle == once ? null : "karistirma degismedi";
            }
            case "CtrlAltShiftB":
            {
                var mods = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift;
                var degerler = new List<string> { o.View.Settings.Repeat + "/" + o.Oku("loop-file") };
                for (var i = 0; i < 3; i++)
                {
                    o.Bas(Key.B, mods);
                    o.Bekle(0.1);
                    degerler.Add(o.View.Settings.Repeat + "/" + o.Oku("loop-file"));
                }

                o.Not("Repeat/loop-file " + string.Join(" -> ", degerler));
                return degerler.SequenceEqual(new[] { "Off/no", "All/no", "One/inf", "Off/no" }) ? null : "tekrar dongusu motora ulasmadi";
            }
            case "N":
            case "B":
            case "ShiftPgDn":
            case "ShiftPgUp":
            {
                o.Git(100);
                o.Bas(Key.N, KeyModifiers.None, "n");
                o.Git(200);
                o.Bas(Key.N, KeyModifiers.None, "n");
                o.Git(300);
                o.Bas(Key.N, KeyModifiers.None, "n");
                var imler = o.View.History.Bookmarks(o.View.LoadedPath!);
                o.Not("yer imleri " + string.Join(", ", imler.Select(F)));
                if (ad == "N") return imler.Count == 3 && Math.Abs(imler[0] - 100) < Tolerans && Math.Abs(imler[2] - 300) < Tolerans ? null : "yer imi eklenmedi";

                o.Git(150);
                var (key, mods, beklenen) = ad switch
                {
                    "B" => (Key.B, KeyModifiers.None, 200.0),
                    "ShiftPgDn" => (Key.PageDown, KeyModifiers.Shift, 200.0),
                    _ => (Key.PageUp, KeyModifiers.Shift, 100.0)
                };
                o.Bas(key, mods, ad == "B" ? "b" : null);
                o.Aramayi();
                o.Bekle(() => Math.Abs(o.Sayi("time-pos") - beklenen) < Tolerans, 3);
                o.Not($"150 sn'den time-pos {F(o.Sayi("time-pos"))}, beklenen {F(beklenen)}, path {Path.GetFileName(o.Oku("path"))}");
                return Math.Abs(o.Sayi("time-pos") - beklenen) < Tolerans ? null : "yer imine gidilmedi";
            }
            case "CtrlK":
            case "CtrlShiftG":
            {
                var klasor = Path.Combine(KisayolKanit.Folder, "klip-" + ad);
                if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
                o.View.Settings.ScreenshotFolder = klasor;
                o.Git(20);
                if (ad == "CtrlK") o.Bas(Key.K, KeyModifiers.Control);
                else o.Bas(Key.G, KeyModifiers.Control | KeyModifiers.Shift);
                o.Bekle(() => o.View.LastExport.IsCompleted, 60);
                var sonuc = o.View.LastExport.IsCompleted ? o.View.LastExport.Result : null;
                var yol = sonuc?.Target;
                var boyut = yol is not null && File.Exists(yol) ? new FileInfo(yol).Length : 0;
                o.Not($"cikti {yol ?? "-"}, {boyut} bayt, ok {sonuc?.Ok}, hata {sonuc?.Error.Split('\n').LastOrDefault(s => s.Trim().Length > 0)?.Trim()}");
                var uzanti = ad == "CtrlK" ? ToolsOptions.ClipExtension : ToolsOptions.GifExtension;
                return boyut > 0 && yol!.EndsWith(uzanti, StringComparison.OrdinalIgnoreCase) ? null : "klip dosyasi yok";
            }
            case "CtrlM":
            {
                var once = (o.Window.Width, o.Window.WindowDecorations, o.Window.Topmost);
                o.Bas(Key.M, KeyModifiers.Control);
                o.Bekle(0.2);
                var mini = (o.Window.Width, o.Window.WindowDecorations, o.Window.Topmost);
                o.Not($"pencere {once} -> {mini}, IsMiniMode {o.View.IsMiniMode}");
                return o.View.IsMiniMode && mini.WindowDecorations == WindowDecorations.None && mini.Topmost && mini.Width < once.Width ? null : "mini kip pencereye yansimadi";
            }
            case "CtrlU":
            {
                var once = o.Window.OwnedWindows.Count;
                o.Bas(Key.U, KeyModifiers.Control);
                o.Bekle(() => o.Window.OwnedWindows.Count > once, 3);
                o.Not($"sahipli pencere {once} -> {o.Window.OwnedWindows.Count}");
                return o.Window.OwnedWindows.Count > once ? null : "adres penceresi acilmadi";
            }
            case "Teker":
            {
                o.View.Apply(new PlayerCommand(PlayerCommandKind.Volume, 50 - o.View.VolumeLevel));
                o.Bekle(0.2);
                GirdiSurucu.Wheel(o.View, 1, KeyModifiers.None);
                o.Bekle(() => Math.Abs(o.Sayi("volume") - 55) < 1e-6, 2);
                o.Not($"teker: volume {F(o.Sayi("volume"))}, beklenen 55");
                return Math.Abs(o.Sayi("volume") - 55) < 1e-6 ? null : "volume degismedi";
            }
            case "CtrlTeker": return Teker(o, KeyModifiers.Control, 10);
            case "ShiftTeker": return Teker(o, KeyModifiers.Shift, 60);
            case "CtrlShiftTeker": return Teker(o, KeyModifiers.Control | KeyModifiers.Shift, 300);
            case "AltTeker":
            {
                o.Bekle(0.3);
                var once = (Olcek: o.View.ZoomScale, Genislik: o.View.Frame.Bounds.Width);
                GirdiSurucu.Wheel(o.View, 1, KeyModifiers.Alt);
                o.Bekle(() => o.View.Frame.Bounds.Width > once.Genislik + 1, 2);
                var sonra = (Olcek: o.View.ZoomScale, Genislik: o.View.Frame.Bounds.Width);
                o.Not($"alt teker: ZoomScale {F(once.Olcek)} -> {F(sonra.Olcek)}, kare genisligi {F(once.Genislik)} -> {F(sonra.Genislik)}");
                for (var i = 0; i < 12; i++) GirdiSurucu.Wheel(o.View, 1, KeyModifiers.Alt);
                o.Bekle(0.3);
                var tavan = (Olcek: o.View.ZoomScale, Genislik: o.View.Frame.Bounds.Width);
                o.Not($"13 centik: ZoomScale {F(tavan.Olcek)}, kare genisligi {F(tavan.Genislik)}, beklenen {F(once.Genislik * tavan.Olcek)}");
                return sonra.Olcek > once.Olcek && sonra.Genislik > once.Genislik + 1
                       && Math.Abs(tavan.Genislik - once.Genislik * tavan.Olcek) < 1 ? null : "kare olcekle buyumedi";
            }
            case "SagTik":
            {
                var once = o.View.MenuOpen;
                GirdiSurucu.Press(o.View, PointerUpdateKind.RightButtonPressed, RawInputModifiers.RightMouseButton);
                o.Bekle(0.3);
                o.Not($"sag tik: menu acik {once} -> {o.View.MenuOpen}, dayanak {o.View.MenuAnchor}");
                return !once && o.View.MenuOpen ? null : "menu acilmadi";
            }
            case "SolTik":
            {
                var once = o.Oku("pause");
                GirdiSurucu.Press(o.View, PointerUpdateKind.LeftButtonPressed, RawInputModifiers.LeftMouseButton);
                o.View.FareRelease(0);
                o.View.FareDue(ClickArbiter.DoubleWindowMs);
                o.Bekle(() => o.Oku("pause") == "no", 3);
                o.Not($"sol tik: pause {once} -> {o.Oku("pause")}");
                return once == "yes" && o.Oku("pause") == "no" ? null : "sol tik oynatmadi";
            }
            case "CiftTik":
            case "OrtaTik":
            {
                var once = o.Window.WindowState;
                if (ad == "CiftTik") GirdiSurucu.Press(o.View, PointerUpdateKind.LeftButtonPressed, RawInputModifiers.LeftMouseButton, 2);
                else GirdiSurucu.Press(o.View, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
                o.Bekle(0.3);
                var sonra = o.Window.WindowState;
                o.Not($"WindowState {once} -> {sonra}");
                return sonra == WindowState.FullScreen ? null : "tam ekran olmadi";
            }
            default:
                return "bilinmeyen vaka " + ad;
        }
    }

    [Fact]
    public void MetinKutusundaKisayolYutulmazSurguOdagindaCalisir()
    {
        var (kayit, sonuc) = AppHost.Run(() =>
        {
            var o = KisayolOrtam.Ac(KisayolKanit.Uzun, "odak");
            try
            {
                o.Bas(Key.M, KeyModifiers.None, "m", o.Kutu);
                o.Bekle(0.3);
                var kutuda = o.Oku("mute");
                o.Bas(Key.M, KeyModifiers.None, "m", o.View.FindControl<Slider>("SliderSeritSpeed"));
                o.Bekle(() => o.Oku("mute") == "yes", 2);
                var surgude = o.Oku("mute");
                o.Git(350);
                var hiz = o.Sayi("speed");
                o.Bas(Key.Right, KeyModifiers.None, null, o.View.FindControl<Slider>("SliderSeritSpeed"));
                o.Aramayi();
                o.Bekle(() => o.Sayi("time-pos") > 355, 3);
                o.Not($"metin kutusunda M: mute {kutuda}; hiz surgusunde M: mute {surgude}; hiz surgusunde sag ok: time-pos {F(o.Sayi("time-pos"))}, speed {F(hiz)} -> {F(o.Sayi("speed"))}");
                return (o.Kayit.ToString(), kutuda == "no" && surgude == "yes" && o.Sayi("time-pos") > 355 && Math.Abs(o.Sayi("speed") - hiz) < 1e-6);
            }
            finally
            {
                o.Kapat();
            }
        });

        KisayolKanit.Write("odak.txt", kayit);
        Assert.True(sonuc, kayit);
        KisayolKanit.Kapat("odak.txt", Path.Combine("gecmis", "odak.json"));
    }

    [Fact]
    public void TamEkranVeMiniKipteKisayollarMotoraUlasir()
    {
        var (kayit, sonuc) = AppHost.Run(() =>
        {
            var o = KisayolOrtam.Ac(KisayolKanit.Uzun, "tam-mini");
            try
            {
                o.Bas(Key.Enter);
                o.Bekle(0.2);
                var tam = o.Window.WindowState;
                o.Bas(Key.M, KeyModifiers.None, "m");
                o.Bekle(() => o.Oku("mute") == "yes", 2);
                var tamSessiz = o.Oku("mute");
                o.Bas(Key.Escape);
                o.Bekle(0.2);

                o.Bas(Key.M, KeyModifiers.Control);
                o.Bekle(0.2);
                var mini = o.View.IsMiniMode;
                o.Bas(Key.Space, KeyModifiers.None, " ");
                o.Bekle(() => o.Oku("pause") == "no", 3);
                var miniOynat = o.Oku("pause");
                var ustteOnce = o.Window.Topmost;
                o.Bas(Key.A, KeyModifiers.Control);
                var ustteSonra = o.Window.Topmost;
                o.Bas(Key.M, KeyModifiers.Control);
                o.Bekle(0.2);
                o.Not($"tam ekran {tam}, tam ekranda M: mute {tamSessiz}; mini {mini}, mini kipte bosluk: pause {miniOynat}; mini kipte Ctrl+A: Topmost {ustteOnce} -> {ustteSonra}; mini kapaninca Topmost {o.Window.Topmost}");
                return (o.Kayit.ToString(), tam == WindowState.FullScreen && tamSessiz == "yes" && mini && miniOynat == "no" && ustteOnce && !ustteSonra);
            }
            finally
            {
                o.Kapat();
            }
        });

        KisayolKanit.Write("tam-mini.txt", kayit);
        Assert.True(sonuc, kayit);
        KisayolKanit.Kapat("tam-mini.txt", Path.Combine("gecmis", "tam-mini.json"));
    }
}
