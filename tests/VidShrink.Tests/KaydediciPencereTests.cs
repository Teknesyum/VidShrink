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

    /// <summary>Son asertten sonra çağrılır; kuralı <see cref="KanitKapanisi"/> anlatıyor.</summary>
    private static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Kanit, adlar);

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

        Kapat("mac-pencere.txt");
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

    private static RecorderView Pencereli(string ayarYolu, RecorderPlatform platform, Func<string, DesktopWindow?> bul, params (string, string)[] ortam)
    {
        var view = new RecorderView(ayarYolu)
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
            var linux = Pencereli(ayarYolu, RecorderPlatform.Linux, t => t == "xlogo" ? xlogo : null, ("DISPLAY", ":99"));
            var linuxIstek = linux.BuildRequest(applyAuto: false);
            var linuxArgs = RecorderArguments.Build(linuxIstek! with { Container = RecorderContainer.Mkv }, "/tmp/l.mkv");

            var wayland = Pencereli(ayarYolu, RecorderPlatform.Linux, _ => xlogo, ("XDG_SESSION_TYPE", "wayland"), ("DISPLAY", ":0"));
            var waylandIstek = wayland.BuildRequest(applyAuto: false);

            var kapanmis = Pencereli(ayarYolu, RecorderPlatform.Linux, _ => null, ("DISPLAY", ":99"));
            var kapanmisIstek = kapanmis.BuildRequest(applyAuto: false);

            var mac = Pencereli(ayarYolu, RecorderPlatform.MacOs, _ => xlogo with { Title = "xlogo" });
            var macIstek = mac.BuildRequest(applyAuto: false);
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

        Kapat("secici.txt");
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

        Kapat("x11-liste.txt");
        KayitKanit.Kapat("x11-pencere.mkv", "x11-pencere.ffprobe.txt", "x11-olmayan.mkv");
    }

    /// <summary>
    /// Linux turu, iki kaydedici hatası: (1) oturumun <c>DISPLAY</c>'i yalnız pencere koluna
    /// yazılıyordu, ekran ve bölge sabit <c>:0.0</c>'a gidiyordu (GNOME'un Xorg oturumu :1'de);
    /// (2) ekran seçimi x11grab'da yok sayılıyor, "Ekran 2" bütün masaüstünü kaydediyordu.
    /// Şimdi ekran seçimi gdigrab'daki gibi o monitörün dikdörtgenine iner ve adres DISPLAY'den gelir.
    /// Olumsuz kontroller: DISPLAY yokken varsayılan <c>:0.0</c>, tek monitörde ekranın tamamı.
    /// </summary>
    [Fact]
    public void LinuxEkranVeBolgeKaydiOturumunEkraniniVeSecilenMonitoruKullanir()
    {
        IReadOnlyList<ScreenBounds> IkiEkran() => new[] { new ScreenBounds(0, 0, 0, 1024, 768), new ScreenBounds(1, 1024, 0, 1920, 1080) };
        IReadOnlyList<ScreenBounds> TekEkran() => new[] { new ScreenBounds(0, 0, 0, 1920, 1080) };

        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            RecorderView Kur(Func<IReadOnlyList<ScreenBounds>> ekranlar, params (string, string)[] ortam)
            {
                var view = new RecorderView(ayarYolu)
                {
                    SkipAutoMeasure = true,
                    CapturePlatform = RecorderPlatform.Linux,
                    ScreenSource = ekranlar,
                    ReadEnvironment = ad => ortam.FirstOrDefault(d => d.Item1 == ad).Item2,
                    DrawRegion = _ => Task.FromResult<Avalonia.PixelRect?>(new Avalonia.PixelRect(100, 50, 640, 360))
                };
                Elle(view);
                view.RefreshScreens();
                return view;
            }

            string Girdi(RecorderView view)
            {
                var istek = view.BuildRequest(applyAuto: false)!;
                var args = RecorderArguments.Build(istek with { Container = RecorderContainer.Mkv }, "/tmp/l.mkv");
                var i = args.ToList().IndexOf("-i");
                var boyut = args.ToList().IndexOf("-video_size");
                return (boyut >= 0 ? args[boyut + 1] + " " : "") + args[i + 1];
            }

            var ekran = Kur(IkiEkran, ("DISPLAY", ":1"));
            ekran.FindControl<ComboBox>("CmbTarget")!.SelectedIndex = (int)RecorderTargetKind.Screen;
            ekran.FindControl<ComboBox>("CmbScreen")!.SelectedIndex = 1;
            var ikinci = Girdi(ekran);
            ekran.FindControl<ComboBox>("CmbScreen")!.SelectedIndex = 0;
            var birinci = Girdi(ekran);

            var tek = Kur(TekEkran, ("DISPLAY", ":1"));
            tek.FindControl<ComboBox>("CmbTarget")!.SelectedIndex = (int)RecorderTargetKind.Screen;
            var tamami = Girdi(tek);

            var bolge = Kur(TekEkran, ("DISPLAY", ":1"));
            bolge.DrawRegionAsync().GetAwaiter().GetResult();
            var bolgeGirdi = Girdi(bolge);

            var ortamsiz = Kur(TekEkran);
            ortamsiz.FindControl<ComboBox>("CmbTarget")!.SelectedIndex = (int)RecorderTargetKind.Screen;
            var varsayilan = Girdi(ortamsiz);

            return (ikinci, birinci, tamami, bolgeGirdi, varsayilan);
        }));

        Assert.Equal("1920x1080 :1+1024,0", olcu.ikinci);
        Assert.Equal("1024x768 :1+0,0", olcu.birinci);
        Assert.Equal(":1", olcu.tamami);
        Assert.Equal("640x360 :1+100,50", olcu.bolgeGirdi);
        Assert.Equal(RecorderArguments.DefaultDisplay, olcu.varsayilan);
    }

    /// <summary>
    /// Linux turu: tıklama halkası, tıklama sesi, tuş gösterimi ve büyüteç yalnız Win32'de
    /// çalışıyor; başka sistemde kutular işaretlenebiliyor ama kayıtta hiçbir şey olmuyordu.
    /// Desteklenmeyen yerde dört kutu sayfada ve mini şeridin seçeneklerinde gizli; imleç
    /// ve klasör kutusu kalır. Olumlu kontrol: destek açıkken dördü de görünür.
    /// </summary>
    [Fact]
    public void GirdiEtkileriDesteklenmeyenSistemdeGizlenir()
    {
        var onceki = RecorderView.InputEffectsAvailable;
        try
        {
            (bool[] sayfa, bool[] mini, bool imlec) Olc(bool destek) => AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
            {
                RecorderView.InputEffectsAvailable = destek;
                var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
                var mini = new RecorderMini();
                string[] adlar = { "ChkShowClicks", "ChkClickSound", "ChkShowKeys", "ChkMagnifier" };
                return (adlar.Select(a => view.FindControl<CheckBox>(a)!.IsVisible).ToArray(),
                        adlar.Select(a => mini.FindControl<CheckBox>(a)!.IsVisible).ToArray(),
                        view.FindControl<CheckBox>("ChkCursor")!.IsVisible && mini.FindControl<CheckBox>("ChkOpenFolder")!.IsVisible);
            }));

            var yok = Olc(false);
            var var_ = Olc(true);

            Assert.All(yok.sayfa, g => Assert.False(g));
            Assert.All(yok.mini, g => Assert.False(g));
            Assert.True(yok.imlec);
            Assert.All(var_.sayfa, g => Assert.True(g));
            Assert.All(var_.mini, g => Assert.True(g));
        }
        finally
        {
            RecorderView.InputEffectsAvailable = onceki;
        }
    }

    /// <summary>
    /// Linux turu 2, canlı X11: kayıt çerçevesi X Shape ile dört şeride iner ve girdiyi geçirir.
    /// Önceden pencere bölgenin tamamını kaplıyordu; birleştiricisiz masaüstünde kaydedilen
    /// monitör siyah kalıyor, video da ilk saniyelerden sonra siyah çıkıyordu (elle ölçüldü:
    /// ortalama parlaklık 90'dan 1,6'ya). Olumsuz kontrol: biçim verilmeyen pencere tek dikdörtgen.
    /// </summary>
    [X11KayitFact]
    public void CerceveX11deKenarSeridineInerVeTiklamayiGecirir()
    {
        var ekran = X11.XOpenDisplay(IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, ekran);
        try
        {
            var kok = X11.XDefaultRootWindow(ekran);
            var bicimli = X11.XCreateSimpleWindow(ekran, kok, 0, 0, 644, 484, 0, 0, 0);
            var bicimsiz = X11.XCreateSimpleWindow(ekran, kok, 0, 0, 644, 484, 0, 0, 0);
            X11.XSync(ekran, false);

            Assert.True(RecorderFrame.ShapeX11(bicimli, RecorderFrame.Ring(new Avalonia.PixelSize(644, 484), 2)));
            X11.XSync(ekran, false);

            Assert.Equal(4, X11.Dikdortgenler(ekran, bicimli, 0));
            Assert.Equal(0, X11.Dikdortgenler(ekran, bicimli, 2));
            Assert.Equal(1, X11.Dikdortgenler(ekran, bicimsiz, 0));
            Assert.Equal(1, X11.Dikdortgenler(ekran, bicimsiz, 2));

            X11.XDestroyWindow(ekran, bicimli);
            X11.XDestroyWindow(ekran, bicimsiz);
        }
        finally
        {
            X11.XCloseDisplay(ekran);
        }
    }

    /// <summary>
    /// Bölge çizerken masaüstü dondurulur: seçici birleştiricisiz X11'de saydam değil siyah
    /// çiziliyordu. Canlı kol gerçek ekranın karesini alır (ekran boyunda), sahte kare zemine
    /// fırça olarak gider; kare yoksa zemin saydam kalır (olumsuz kontrol).
    /// </summary>
    [X11KayitFact]
    public void BolgeSeciciMasaustununDondurulmusKaresiniGosterir()
    {
        var olcu = AppHost.Run(() =>
        {
            var kare = RecorderRegionPicker.FreezeX11Async(System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            var boyut = kare?.PixelSize;
            var dolu = new RecorderRegionPicker { Frozen = kare };
            dolu.ApplyFrozen();
            var bos = new RecorderRegionPicker();
            bos.ApplyFrozen();
            var zemin = dolu.FindControl<Canvas>("Surface")!.Background;
            var bosZemin = bos.FindControl<Canvas>("Surface")!.Background;
            kare?.Dispose();
            return (boyut, dolu: zemin is Avalonia.Media.ImageBrush, bos: bosZemin is Avalonia.Media.ImageBrush);
        });

        Assert.NotNull(olcu.boyut);
        Assert.True(olcu.boyut!.Value.Width > 0 && olcu.boyut.Value.Height > 0);
        Assert.True(olcu.dolu);
        Assert.False(olcu.bos);
    }

    /// <summary>Çerçeve Windows'ta her zaman; X11'de yalnız biçim verildiyse ve şerit bölgenin dışındaysa (içteki şerit kayda girer).</summary>
    [Fact]
    public void CerceveYalnizKaydaGirmeyecekVeBicimliyseGosterilir()
    {
        Assert.True(RecorderFrameHost.Showable(windows: true, inside: true, shaped: false));
        Assert.True(RecorderFrameHost.Showable(windows: false, inside: false, shaped: true));
        Assert.False(RecorderFrameHost.Showable(windows: false, inside: true, shaped: true));
        Assert.False(RecorderFrameHost.Showable(windows: false, inside: false, shaped: false));
    }

    /// <summary>
    /// Video klasörü yoksa (.NET Linux'ta <c>~/Videos</c> yoksa MyVideos için boş dönüyor) kayıt
    /// çalışma klasörüne değil <c>~/Videos/VidShrink</c>'e gider; masaüstünden açılınca çalışma
    /// klasörü <c>/</c> olabiliyor. Sistem klasörü varsa o kullanılır (olumlu kontrol).
    /// </summary>
    [Fact]
    public void VideoKlasoruYoksaEvKlasorununAltinaDuser()
    {
        var onceki = KullaniciKlasoru.Bul;
        try
        {
            KullaniciKlasoru.Bul = k => k == Environment.SpecialFolder.UserProfile ? "/home/ali" : "";
            Assert.Equal(Path.Combine("/home/ali", "Videos", "VidShrink"), new RecorderSettings().ResolveFolder());
            Assert.Equal(Path.Combine("/home/ali", "Pictures", "VidShrink"), KullaniciKlasoru.Resim());

            KullaniciKlasoru.Bul = k => k == Environment.SpecialFolder.MyVideos ? "/home/ali/Filmler" : "/home/ali";
            Assert.Equal(Path.Combine("/home/ali/Filmler", "VidShrink"), new RecorderSettings().ResolveFolder());
            Assert.Equal("/elle", new RecorderSettings { OutputFolder = "/elle" }.ResolveFolder());
        }
        finally
        {
            KullaniciKlasoru.Bul = onceki;
        }
    }

    private static class X11
    {
        [System.Runtime.InteropServices.DllImport("libX11.so.6")]
        internal static extern IntPtr XOpenDisplay(IntPtr name);

        [System.Runtime.InteropServices.DllImport("libX11.so.6")]
        internal static extern int XCloseDisplay(IntPtr display);

        [System.Runtime.InteropServices.DllImport("libX11.so.6")]
        internal static extern IntPtr XDefaultRootWindow(IntPtr display);

        [System.Runtime.InteropServices.DllImport("libX11.so.6")]
        internal static extern IntPtr XCreateSimpleWindow(IntPtr display, IntPtr parent, int x, int y, uint width, uint height, uint border, ulong borderColor, ulong background);

        [System.Runtime.InteropServices.DllImport("libX11.so.6")]
        internal static extern int XDestroyWindow(IntPtr display, IntPtr window);

        [System.Runtime.InteropServices.DllImport("libX11.so.6")]
        internal static extern int XSync(IntPtr display, bool discard);

        [System.Runtime.InteropServices.DllImport("libX11.so.6")]
        private static extern int XFree(IntPtr data);

        [System.Runtime.InteropServices.DllImport("libXext.so.6")]
        private static extern IntPtr XShapeGetRectangles(IntPtr display, IntPtr window, int kind, out int count, out int ordering);

        internal static int Dikdortgenler(IntPtr display, IntPtr window, int kind)
        {
            var veri = XShapeGetRectangles(display, window, kind, out var sayi, out _);
            if (veri != IntPtr.Zero) XFree(veri);
            return sayi;
        }
    }
}
