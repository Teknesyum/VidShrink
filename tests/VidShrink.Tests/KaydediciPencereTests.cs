using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// Canlı X11 kolu: Linux'ta <c>DISPLAY</c> ve ffmpeg varken koşar. <c>VIDSHRINK_X11_CANLI=1</c>
/// verilince (CI'nın Linux işi) önkoşul eksik olsa da atlanmaz, kırmızı olur.
/// </summary>
public sealed class X11KayitFactAttribute : FactAttribute
{
    public X11KayitFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("VIDSHRINK_X11_CANLI") == "1") return;
        if (!OperatingSystem.IsLinux())
            Skip = "canli pencere kaydi x11grab ile olculur; bu makine Linux degil (CI'nin Linux isi kosturur).";
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            Skip = "DISPLAY yok, X sunucusu olmadan x11grab olculemez.";
        else if (!ToolLocator.IsAvailable(out var missing))
            Skip = $"{missing} bulunamadi.";
    }
}

/// <summary>
/// R2: macOS ve Linux pencere kaydı. macOS'ta <c>avfoundation</c> pencere vermediği için pencere
/// dikdörtgeni ekran kırpmasına çevrilir (yalnız argüman ölçüsü: CI'da ekran izni yok); Linux'ta
/// pencere kimliği <c>xwininfo</c>'dan ya da <c>_NET_CLIENT_LIST</c>'ten, Wayland açıkça reddedilir.
/// Kanıt <c>.calisma/kaydedici-pencere/</c> ve canlı kolda <c>.calisma/dalga8a/</c>.
/// </summary>
public sealed class KaydediciPencereTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "kaydedici-pencere");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static RecorderRequest Mac(RecorderRegion? pencere) => new()
    {
        Platform = RecorderPlatform.MacOs,
        Target = RecorderTargetKind.Window,
        WindowTitle = "Safari",
        WindowRegion = pencere,
        ScreenIndex = 1,
        Container = RecorderContainer.Mov
    };

    [Fact]
    public void MacPenceresiEkranKirpmasinaDuser()
    {
        var args = RecorderArguments.Build(Mac(new RecorderRegion(200, 100, 1280, 720)) with { PreviewPath = "/tmp/o.jpg" }, "/tmp/p.mov");
        var bolge = RecorderArguments.Build(Mac(null) with { Target = RecorderTargetKind.Region, Region = new RecorderRegion(200, 100, 1280, 720) }, "/tmp/b.mov");
        var ham = RecorderArguments.Validate(Mac(null), "/tmp/p.mov");
        var tek = RecorderArguments.Validate(Mac(new RecorderRegion(0, 0, 641, 480)), "/tmp/p.mov");
        var eksi = RecorderArguments.Validate(Mac(new RecorderRegion(-2, 0, 640, 480)), "/tmp/p.mov");
        File.WriteAllLines(Path.Combine(Kanit, "mac-pencere.txt"), new[] { string.Join(' ', args), string.Join(' ', ham), string.Join(' ', tek), string.Join(' ', eksi) });

        Assert.Equal("1:none", Deger(args, "-i"));
        Assert.Equal("avfoundation", Deger(args, "-f"));
        Assert.Equal("crop=1280:720:200:100", Deger(args, "-vf"));
        Assert.Contains("crop=1280:720:200:100,fps=1,scale=320:-2", args);
        Assert.Equal(Deger(bolge, "-vf"), Deger(args, "-vf"));

        Assert.Contains(ham, s => s.Contains("avfoundation") && s.Contains("not single windows"));
        Assert.Contains(tek, s => s.Contains("even"));
        Assert.Contains(eksi, s => s.Contains("negative"));
        Assert.DoesNotContain("-vf", RecorderArguments.Build(Mac(null) with { Target = RecorderTargetKind.Screen }, "/tmp/e.mov"));
    }

    [Fact]
    public void PencereKirpmasiOlcekVeEkranSinirinaUyar()
    {
        Assert.Equal(new RecorderRegion(200, 100, 1280, 720), RecorderArguments.WindowCrop(100, 50, 640, 360, 0, 0, 1440, 900, 2));
        Assert.Equal(new RecorderRegion(0, 0, 200, 300), RecorderArguments.WindowCrop(-50, -20, 150, 170, 0, 0, 1440, 900, 2));
        Assert.Equal(new RecorderRegion(1340, 700, 100, 200), RecorderArguments.WindowCrop(1340, 700, 400, 400, 0, 0, 1440, 900, 1));
        Assert.Equal(new RecorderRegion(10, 20, 300, 200), RecorderArguments.WindowCrop(1450, 20, 301, 201, 1440, 0, 1920, 1080, 1));
        Assert.Equal(new RecorderRegion(15, 15, 150, 150), RecorderArguments.WindowCrop(10, 10, 100.5, 100.5, 0, 0, 800, 600, 1.5));
        Assert.Null(RecorderArguments.WindowCrop(2000, 0, 100, 100, 0, 0, 1440, 900, 2));
        Assert.Null(RecorderArguments.WindowCrop(10, 10, 100, 100, 0, 0, 1440, 900, 0));
        Assert.Null(RecorderArguments.WindowCrop(1439.6, 10, 100, 100, 0, 0, 1440, 900, 1));

        var pencere = new DesktopWindow("Safari", "42", 1500, 100, 400, 300);
        var ekranlar = new[] { (new ScreenBounds(0, 0, 0, 2880, 1800), 2.0), (new ScreenBounds(1, 2880, 0, 1920, 1080), 1.0) };
        Assert.Equal((1, new RecorderRegion(60, 100, 400, 300)), RecorderWindows.MacCrop(pencere with { X = 2940 }, ekranlar));
        Assert.Equal((0, new RecorderRegion(2600, 200, 280, 600)), RecorderWindows.MacCrop(pencere with { X = 1300 }, ekranlar));
        Assert.Null(RecorderWindows.MacCrop(pencere with { X = 9000 }, ekranlar));
    }

    private const string Agac = """
        xwininfo: Window id: 0x1d3 (the root window) (has no name)

          Root window id: 0x1d3 (the root window) (has no name)
          Parent window id: 0x0 (none)
             4 children:
             0x200001 "xlogo": ("xlogo" "XLogo")  320x240+10+20  +10+20
                1 child:
                0x200002 (has no name): ()  320x240+0+0  +10+20
             0x400001 "Terminal — bash": ("gnome-terminal" "Gnome-terminal")  800x600+-5+100  +-5+100
             0x600001 (has no name): ()  1x1+-1+-1  +-1+-1
             0x600002 "gizli": ()  1x1+0+0  +0+0

        """;

    [Fact]
    public void XwininfoAgaciPencereKimligineVeKonumaAyrisir()
    {
        var pencereler = RecorderWindowsX11.ParseTree(Agac.Replace("\n", "\r\n"));

        Assert.Equal(new[]
        {
            new DesktopWindow("xlogo", "0x200001", 10, 20, 320, 240),
            new DesktopWindow("Terminal — bash", "0x400001", -5, 100, 800, 600)
        }, pencereler);
        Assert.Empty(RecorderWindowsX11.ParseTree("xwininfo: error: unable to open display"));
    }

    [Fact]
    public void WaylandOturumuAcikcaAyrilir()
    {
        static Func<string, string?> Ortam(params (string, string)[] degerler) => ad => degerler.FirstOrDefault(d => d.Item1 == ad).Item2;

        Assert.True(RecorderWindowsX11.IsWayland(Ortam(("XDG_SESSION_TYPE", "wayland"), ("DISPLAY", ":0"), ("WAYLAND_DISPLAY", "wayland-0"))));
        Assert.True(RecorderWindowsX11.IsWayland(Ortam(("WAYLAND_DISPLAY", "wayland-0"))));
        Assert.False(RecorderWindowsX11.IsWayland(Ortam(("XDG_SESSION_TYPE", "x11"), ("DISPLAY", ":0"))));
        Assert.False(RecorderWindowsX11.IsWayland(Ortam(("DISPLAY", ":99"))));
    }

    private static string Metin(string anahtar, params object?[] args)
        => string.Format(Strings.Culture, LanguageCatalog.Display(Strings.Get(anahtar)), args);

    private static RecorderView Pencereli(RecorderPlatform platform, Func<string, DesktopWindow?> bul, params (string, string)[] ortam)
    {
        var view = new RecorderView
        {
            SkipAutoMeasure = true,
            CapturePlatform = platform,
            ListWindows = () => new[] { "xlogo" },
            FindWindow = bul,
            ReadEnvironment = ad => ortam.FirstOrDefault(d => d.Item1 == ad).Item2,
            ScaledScreens = () => new[] { (new ScreenBounds(0, 0, 0, 2880, 1800), 2.0) }
        };
        Elle(view);
        view.FindControl<ComboBox>("CmbTarget")!.SelectedIndex = (int)RecorderTargetKind.Window;
        view.RefreshWindowList();
        view.FindControl<ComboBox>("CmbWindow")!.SelectedIndex = 0;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return view;
    }

    [Fact]
    public void SeciciLinuxtaKimlikVeEkranMacteKirpmaYazarWaylandiReddeder()
    {
        var xlogo = new DesktopWindow("xlogo", "0x200001", 100, 50, 320, 240);
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var linux = Pencereli(RecorderPlatform.Linux, t => t == "xlogo" ? xlogo : null, ("DISPLAY", ":99"));
            var linuxIstek = linux.BuildRequest(applyAuto: false);
            var linuxArgs = RecorderArguments.Build(linuxIstek! with { Container = RecorderContainer.Mkv }, "/tmp/l.mkv");

            var wayland = Pencereli(RecorderPlatform.Linux, _ => xlogo, ("XDG_SESSION_TYPE", "wayland"), ("DISPLAY", ":0"));
            var waylandIstek = wayland.BuildRequest(applyAuto: false);

            var kapanmis = Pencereli(RecorderPlatform.Linux, _ => null, ("DISPLAY", ":99"));
            var kapanmisIstek = kapanmis.BuildRequest(applyAuto: false);

            var mac = Pencereli(RecorderPlatform.MacOs, _ => xlogo with { Title = "xlogo" });
            var macIstek = mac.BuildRequest(applyAuto: false);
            // Scale = null: olcunun konusu pencere kirpmasi, kalici olcek degil. Gorunum
            // kurucuda paylasilan recorder-settings.json'u okuyor; baska bir sinif oraya
            // 1280x720 birakirsa -vf "crop=...,scale=1280:720" oluyor ve bu olcu sira
            // bagimlisi kiriliyordu (docs/olcumler/aot-dalgasi.md, CI 35291760780).
            var macArgs = RecorderArguments.Build(macIstek! with { Container = RecorderContainer.Mov, Scale = null }, "/tmp/m.mov");

            return (linuxIstek, linuxArgs, linuxHata: linux.ErrorText, waylandIstek, waylandHata: wayland.ErrorText,
                kapanmisIstek, kapanmisHata: kapanmis.ErrorText, macIstek, macArgs);
        }));

        File.WriteAllLines(Path.Combine(Kanit, "secici.txt"), new[]
        {
            "linux: " + string.Join(' ', olcu.linuxArgs),
            "wayland: " + olcu.waylandHata,
            "kapanmis: " + olcu.kapanmisHata,
            "mac: " + string.Join(' ', olcu.macArgs)
        });

        Assert.Equal(string.Empty, olcu.linuxHata);
        Assert.Equal("0x200001", Deger(olcu.linuxArgs, "-window_id"));
        Assert.Equal(":99", Deger(olcu.linuxArgs, "-i"));

        Assert.Null(olcu.waylandIstek);
        Assert.Equal(Metin("recorder.error.window-wayland"), olcu.waylandHata);
        Assert.Contains("Wayland", olcu.waylandHata);

        Assert.Null(olcu.kapanmisIstek);
        Assert.Equal(Metin("recorder.error.window-missing", "xlogo"), olcu.kapanmisHata);
        Assert.Contains("xlogo", olcu.kapanmisHata);

        Assert.Equal(new RecorderRegion(200, 100, 640, 480), olcu.macIstek!.WindowRegion);
        Assert.Equal("crop=640:480:200:100", Deger(olcu.macArgs, "-vf"));
        Assert.Equal("0:none", Deger(olcu.macArgs, "-i"));
    }

    [X11KayitFact]
    public async Task X11PenceresiListedenBulunupIkiSaniyeKaydedilir()
    {
        var araclu = RecorderWindowsX11.FromTool();
        var istemci = RecorderWindowsX11.ClientList();
        File.WriteAllLines(Path.Combine(Kanit, "x11-liste.txt"),
            araclu.Select(w => "xwininfo " + w).Concat(istemci.Select(w => "_NET_CLIENT_LIST " + w)));

        var xlogo = araclu.First(w => w.Title == "xlogo");
        var ayni = istemci.First(w => w.Title == "xlogo");
        Assert.Equal(xlogo.Id, ayni.Id);
        Assert.Equal((xlogo.Width, xlogo.Height), (ayni.Width, ayni.Height));

        var istek = new RecorderRequest
        {
            Platform = RecorderPlatform.Linux,
            Target = RecorderTargetKind.Window,
            WindowId = xlogo.Id,
            Display = Environment.GetEnvironmentVariable("DISPLAY"),
            Fps = 15,
            Container = RecorderContainer.Mkv,
            VideoCodec = "libx264",
            Preset = "ultrafast"
        };

        var cikti = KayitKanit.Path_("x11-pencere.mkv");
        var oturum = await RecorderSession.StartAsync(istek, cikti);
        await Task.Delay(2000);
        var sonuc = await oturum.StopAsync();
        var (kod, metin) = KayitKanit.Ffprobe(cikti, "x11-pencere.ffprobe.txt");

        var bogus = KayitKanit.Path_("x11-olmayan.mkv");
        var red = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var yanlis = await RecorderSession.StartAsync(istek with { WindowId = "0x7ffffff0" }, bogus);
            await Task.Delay(1000);
            var s = await yanlis.StopAsync();
            if (!s.Ok) throw new InvalidOperationException(s.StandardError);
        });
        File.AppendAllText(Path.Combine(Kanit, "x11-liste.txt"), "olmayan pencere: " + red.Message + Environment.NewLine);

        Assert.True(sonuc.Ok, $"kayit 0 ile kapanmali; stderr: {sonuc.StandardError}");
        Assert.Equal(0, kod);
        Assert.Contains("codec_type=video", metin);
        Assert.Contains($"width={(int)xlogo.Width - (int)xlogo.Width % 2}", metin);
        Assert.Contains($"height={(int)xlogo.Height - (int)xlogo.Height % 2}", metin);
        Assert.InRange(KayitKanit.Duration(metin) ?? 0, 1.2, 4.0);
    }
}
