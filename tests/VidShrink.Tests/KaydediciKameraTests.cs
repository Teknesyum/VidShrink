using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

public sealed class KameraFactAttribute : FactAttribute
{
    internal const string Aygit = "OBS Virtual Camera";

    public KameraFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
            Skip = $"{missing} bulunamadi, canli kamera olcusu kosturulmadi.";
        else if (!OperatingSystem.IsWindows())
            Skip = "kamera bindirmesi dshow ile olculdu; bu makine Windows degil.";
        else if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
            Skip = "CI kosucusunda kamera aygiti yok.";
        else if (!CaptureDevices.Instance.Video.Contains(Aygit, StringComparer.OrdinalIgnoreCase))
            Skip = $"{Aygit} aygiti bu makinede yok.";
    }
}

public sealed class KaydediciKameraTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "paket-2");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    /// <summary>Son asertten sonra çağrılır; kuralı <see cref="KanitKapanisi"/> anlatıyor.</summary>
    private static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Kanit, adlar);

    private static RecorderRequest Istek() => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Region = new RecorderRegion(0, 0, 320, 240),
        Fps = 15,
        Preset = "ultrafast",
        Container = RecorderContainer.Mkv
    };

    private static readonly AudioCaptureDevice Mikrofon =
        new("Mikrofon", CaptureBackend.DirectShow, AudioSourceRole.Microphone);

    private static string? Dosyada(string dosya, string anahtar)
        => File.Exists(dosya) ? JsonNode.Parse(File.ReadAllText(dosya))?[anahtar]?.ToJsonString() : null;

    [Fact]
    public void KameraDshowGirdisiVeBindirmeGrafigiUretirKamerasizUretmez()
    {
        var kamerali = RecorderArguments.Build(Istek() with { Webcam = new RecorderWebcam("Kam A", 240, WebcamCorner.TopLeft) }, "a.mkv");
        var kamerasiz = RecorderArguments.Build(Istek(), "a.mkv");

        var metin = string.Join(" ", kamerali);
        Assert.Contains("-f dshow -rtbufsize 256M -i video=Kam A", metin);
        Assert.True(kamerali.ToList().IndexOf("gdigrab") < kamerali.ToList().IndexOf("dshow"));
        Assert.Equal("[0:v]null[base];[1:v]scale=240:-2[cam];[base][cam]overlay=16:16:eof_action=repeat[vout]", Deger(kamerali, "-filter_complex"));
        Assert.Equal("[vout]", Deger(kamerali, "-map"));
        Assert.Contains("-an", kamerali);
        Assert.Null(Deger(kamerali, "-vf"));
        Assert.Empty(RecorderArguments.Validate(Istek() with { Webcam = new RecorderWebcam("Kam A", 240, WebcamCorner.TopLeft) }, "a.mkv"));

        Assert.DoesNotContain("dshow", kamerasiz);
        Assert.Null(Deger(kamerasiz, "-filter_complex"));
        Assert.Null(Deger(kamerasiz, "-map"));
    }

    [Fact]
    public void KameraOlcekVeSesleAyniGraftaBirlesir()
    {
        var ses = AudioCaptureArguments.Build(new AudioCaptureSelection(Mikrofon), new[] { Mikrofon }, RecorderArguments.AudioFirstInputIndex);
        var args = RecorderArguments.Build(
            Istek() with { Scale = new RecorderScale(640, 480), Audio = ses, Webcam = new RecorderWebcam("Kam A", 160) }, "a.mkv");
        var girdiler = args.Select((a, i) => (a, i)).Where(x => x.a == "-i").Select(x => args[x.i + 1]).ToList();
        var graf = Deger(args, "-filter_complex")!;

        Assert.Equal(3, girdiler.Count);
        Assert.Equal("video=Kam A", girdiler[2]);
        Assert.StartsWith("[0:v]scale=640:480[base];[2:v]scale=160:-2[cam];", graf);
        Assert.Contains("overlay=main_w-overlay_w-16:main_h-overlay_h-16", graf);
        Assert.Equal(1, args.Count(a => a == "-filter_complex"));
        Assert.Null(Deger(args, "-vf"));
        var eslemler = args.Select((a, i) => (a, i)).Where(x => x.a == "-map").Select(x => args[x.i + 1]).ToList();
        Assert.Equal("[vout]", eslemler[0]);
        Assert.Contains("1:a", eslemler);
        Assert.DoesNotContain("-an", args);
    }

    [Theory]
    [InlineData(WebcamCorner.BottomRight, "main_w-overlay_w-16:main_h-overlay_h-16")]
    [InlineData(WebcamCorner.BottomLeft, "16:main_h-overlay_h-16")]
    [InlineData(WebcamCorner.TopRight, "main_w-overlay_w-16:16")]
    [InlineData(WebcamCorner.TopLeft, "16:16")]
    public void DortKoseKendiKonumunuVerir(WebcamCorner kose, string beklenen)
        => Assert.Equal(beklenen, RecorderArguments.WebcamPosition(kose));

    [Fact]
    public void GecersizKameraIstegiReddedilir()
    {
        string[] Hata(RecorderRequest r) => RecorderArguments.Validate(r, "a.mkv").ToArray();

        Assert.Empty(Hata(Istek() with { Webcam = new RecorderWebcam("Kam", 320) }));
        Assert.Contains(Hata(Istek() with { Platform = RecorderPlatform.MacOs, Region = null, Target = RecorderTargetKind.Screen, Webcam = new RecorderWebcam("Kam", 320) }), h => h.Contains("DirectShow"));
        Assert.Single(Hata(Istek() with { Webcam = new RecorderWebcam(" ", 320) }));
        Assert.Single(Hata(Istek() with { Webcam = new RecorderWebcam("Kam\" -y", 320) }));
        Assert.Single(Hata(Istek() with { Webcam = new RecorderWebcam("Kam", 241) }));
        Assert.Single(Hata(Istek() with { Webcam = new RecorderWebcam("Kam", 62) }));
        Assert.Single(Hata(Istek() with { Webcam = new RecorderWebcam("Kam", 1922) }));
        Assert.Single(Hata(Istek() with { Webcam = new RecorderWebcam("Kam", 320, (WebcamCorner)9) }));
    }

    [Fact]
    public void KameraSecimiDosyayaVeSonrakiKaydinIstegineGecer()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            IReadOnlyList<string> iki = new[] { "Kam A", "Kam B" };
            var once = new RecorderView(ayarYolu) { SkipAutoMeasure = true, CameraSource = () => iki };
            Elle(once);
            var bosIstek = once.PrepareRecording()!.Value.Request.Webcam;
            var ogeler = Bul<ComboBox>(once, "CmbWebcam").ItemsSource!.Cast<object>().Count();
            Bul<ComboBox>(once, "CmbWebcam").SelectedIndex = 2;
            Bul<ComboBox>(once, "CmbWebcamSize").SelectedIndex = 2;
            Bul<ComboBox>(once, "CmbWebcamCorner").SelectedIndex = Array.IndexOf(RecorderView.WebcamCorners, WebcamCorner.TopRight);
            var dosya = (Dosyada(ayarYolu, "webcamName"), Dosyada(ayarYolu, "webcamWidth"), Dosyada(ayarYolu, "webcamCorner"));

            var sonra = new RecorderView(ayarYolu) { SkipAutoMeasure = true, CameraSource = () => iki };
            var istek = sonra.PrepareRecording()!.Value.Request.Webcam;

            var yok = new RecorderView(ayarYolu) { SkipAutoMeasure = true, CameraSource = () => new[] { "Kam A" } };
            var yokIstek = yok.PrepareRecording()!.Value.Request.Webcam;
            var adKaldi = Dosyada(ayarYolu, "webcamName");
            return (bosIstek, ogeler, dosya, istek, yokIstek, adKaldi);
        }));

        File.WriteAllLines(Path.Combine(Kanit, "kamera-ayar.txt"), new[]
        {
            $"secimsiz istek webcam={olcu.bosIstek?.ToString() ?? "yok"} kutu ogesi={olcu.ogeler}",
            $"json webcamName={olcu.dosya.Item1} webcamWidth={olcu.dosya.Item2} webcamCorner={olcu.dosya.Item3}",
            $"yeni gorunum istegi={olcu.istek}",
            $"aygit listede yok: istek={olcu.yokIstek?.ToString() ?? "yok"} json ad={olcu.adKaldi}"
        });

        Assert.Null(olcu.bosIstek);
        Assert.Equal(3, olcu.ogeler);
        Assert.Equal(("\"Kam B\"", "320", "\"TopRight\""), olcu.dosya);
        Assert.Equal(new RecorderWebcam("Kam B", 320, WebcamCorner.TopRight), olcu.istek);
        Assert.Null(olcu.yokIstek);
        Assert.Equal("\"Kam B\"", olcu.adKaldi);

        Kapat("kamera-ayar.txt");
    }

    [KameraFact]
    public async Task GercekKameraKaydinKosesineBinerKarsiKoseBosKalir()
    {
        var cikti = Path.Combine(Kanit, "kamera-canli.mkv");
        if (File.Exists(cikti)) File.Delete(cikti);

        var istek = Istek() with { Webcam = new RecorderWebcam(KameraFactAttribute.Aygit, 96) };
        var oturum = await RecorderSession.StartAsync(istek, cikti);
        await Task.Delay(3000);
        var sonuc = await oturum.StopAsync();
        var (kod, metin) = KayitKanit.Ffprobe(cikti, "kamera-canli.ffprobe.txt");
        var kose = Parlaklik(cikti, "iw-112:ih-70");
        var karsi = Parlaklik(cikti, "0:0");

        File.WriteAllLines(Path.Combine(Kanit, "kamera-canli.txt"), new[]
        {
            string.Join(" ", RecorderArguments.Build(istek, cikti)),
            $"ok={sonuc.Ok} probe={kod} kam kosesi parlaklik={kose} karsi kose={karsi}",
            metin
        });
        if (File.Exists(cikti)) File.Delete(cikti);

        Assert.True(sonuc.Ok, sonuc.StandardError);
        Assert.Equal(0, kod);
        Assert.Contains("width=320", metin);
        Assert.Contains("height=240", metin);
        Assert.True(kose - karsi > 8, $"kam kosesi {kose}, karsi kose {karsi}");

        Kapat("kamera-canli.txt");
        KayitKanit.Kapat("kamera-canli.ffprobe.txt");
    }

    private static int Parlaklik(string dosya, string konum)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in new[] { "-v", "error", "-ss", "1.5", "-i", dosya, "-vf", $"crop=96:54:{konum},scale=1:1", "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "gray", "-" })
            psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var bayt = surec.StandardOutput.BaseStream.ReadByte();
        surec.StandardOutput.ReadToEnd();
        surec.WaitForExit(20_000);
        _ = hata.Result;
        return bayt;
    }

    [Theory]
    [InlineData(500, 400, 464, 364, 72)]
    [InlineData(10, 10, 0, 0, 72)]
    [InlineData(1915, 1075, 1848, 1008, 72)]
    public void BuyutecKaynagiImleciOrtalarEkranaKisilir(int x, int y, int bx, int by, int kenar)
    {
        var ekran = new PixelRect(0, 0, 1920, 1080);
        var kaynak = MagnifierPlace.Source(new PixelPoint(x, y), 144, 2, ekran);
        Assert.Equal(new PixelRect(bx, by, kenar, kenar), kaynak);
    }

    [Fact]
    public void BuyutecPenceresiKaynakDikdortgeniniOrtmez()
    {
        var ekran = new PixelRect(0, 0, 1920, 1080);
        var olcumler = new List<string>();
        foreach (var imlec in new[] { new PixelPoint(500, 400), new PixelPoint(1915, 1075), new PixelPoint(5, 1075), new PixelPoint(1915, 5) })
        {
            var kaynak = MagnifierPlace.Source(imlec, 144, 2, ekran);
            var konum = MagnifierPlace.Window(kaynak, 144, 24, ekran);
            var pencere = new PixelRect(konum.X, konum.Y, 144, 144);
            olcumler.Add($"imlec={imlec} kaynak={kaynak} pencere={pencere}");
            Assert.False(pencere.Intersects(kaynak), olcumler[^1]);
            Assert.False(pencere.Contains(imlec), olcumler[^1]);
            Assert.True(ekran.Contains(pencere), olcumler[^1]);
        }

        var ortada = MagnifierPlace.Window(MagnifierPlace.Source(new PixelPoint(500, 400), 144, 2, ekran), 144, 24, ekran);
        Assert.Equal(new PixelPoint(560, 460), ortada);
        File.WriteAllLines(Path.Combine(Kanit, "buyutec-yer.txt"), olcumler);

        Kapat("buyutec-yer.txt");
    }

    [Fact]
    public void GercekBuyutecPenceresiImleciIzlerVeEkraniKopyalar()
    {
        var olcu = AppHost.Run(() =>
        {
            var pencere = new RecorderMagnifier();
            pencere.Show();
            pencere.Follow(new PixelPoint(400, 300));
            var ilk = (pencere.Position, pencere.LastSource, pencere.LastGrab, kaynak: pencere.Lens.Source is not null, pencere.Width, pencere.Topmost, pencere.RenderScaling);
            pencere.Follow(new PixelPoint(600, 350));
            var ikinci = (pencere.Position, pencere.LastSource);
            pencere.Close();
            return (ilk, ikinci);
        });

        File.WriteAllLines(Path.Combine(Kanit, "buyutec-pencere.txt"), new[]
        {
            $"400,300: konum={olcu.ilk.Position} kaynak={olcu.ilk.LastSource} kopya={olcu.ilk.LastGrab} goruntu={olcu.ilk.kaynak} genislik={olcu.ilk.Width} ustte={olcu.ilk.Topmost} olcek={olcu.ilk.RenderScaling}",
            $"600,350: konum={olcu.ikinci.Position} kaynak={olcu.ikinci.LastSource}"
        });

        var lens = (int)Math.Ceiling(144 * olcu.ilk.RenderScaling);
        Assert.True(olcu.ilk.LastGrab);
        Assert.True(olcu.ilk.kaynak);
        Assert.True(olcu.ilk.Topmost);
        Assert.Equal(144, olcu.ilk.Width);
        Assert.Equal(new PixelPoint(400, 300), olcu.ilk.LastSource.Center);
        Assert.InRange(olcu.ilk.LastSource.Width, lens / 2 - 1, lens / 2 + 1);
        Assert.Equal(new PixelPoint(olcu.ilk.LastSource.Right + (int)Math.Ceiling(24 * olcu.ilk.RenderScaling), olcu.ilk.LastSource.Bottom + (int)Math.Ceiling(24 * olcu.ilk.RenderScaling)), olcu.ilk.Position);
        Assert.Equal(200, olcu.ikinci.Position.X - olcu.ilk.Position.X);
        Assert.Equal(50, olcu.ikinci.Position.Y - olcu.ilk.Position.Y);

        Kapat("buyutec-pencere.txt");
    }

    private sealed class SahteBuyutec : IMagnifier
    {
        public List<string> Cagrilar { get; } = new();
        public bool Running { get; private set; }
        public void Start() { Running = true; Cagrilar.Add("ac"); }
        public void Stop() { Running = false; Cagrilar.Add("kapa"); }
    }

    [Fact]
    public void BuyutecKutusuDosyayaYaziliriYalnizKayittaAcar()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var kapali = new SahteBuyutec();
            var bos = new RecorderView(ayarYolu) { SkipAutoMeasure = true, Magnifier = kapali };
            bos.SyncInput(true);
            bos.SyncInput(false);

            var buyutec = new SahteBuyutec();
            var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true, Magnifier = buyutec };
            Bul<CheckBox>(view, "ChkMagnifier").IsChecked = true;
            var dosya = Dosyada(ayarYolu, "showMagnifier");
            view.SyncInput(false);
            var bostaAcik = buyutec.Running;
            view.SyncInput(true);
            view.SyncInput(true);
            view.ShrinkToMini();
            var mini = view.Mini!.ChkMagnifier.IsChecked;
            view.Mini!.ChkMagnifier.IsChecked = false;
            var ana = Bul<CheckBox>(view, "ChkMagnifier").IsChecked;
            view.ExpandFromMini();
            view.SyncInput(true);

            var yeni = new RecorderView(ayarYolu) { SkipAutoMeasure = true, Magnifier = new SahteBuyutec() };
            var yeniKutu = Bul<CheckBox>(yeni, "ChkMagnifier").IsChecked;
            return (kapali: string.Join(" | ", kapali.Cagrilar), dosya, bostaAcik, cagri: string.Join(" | ", buyutec.Cagrilar), mini, ana, yeniKutu, son: Dosyada(ayarYolu, "showMagnifier"));
        }));

        File.WriteAllLines(Path.Combine(Kanit, "buyutec-ayar.txt"), new[]
        {
            $"kutu kapali kayitta cagri=[{olcu.kapali}]",
            $"kutu acik json={olcu.dosya} bosta acik={olcu.bostaAcik} cagrilar=[{olcu.cagri}]",
            $"mini yansima={olcu.mini} miniden kapatinca ana={olcu.ana} yeni gorunum kutusu={olcu.yeniKutu} json={olcu.son}"
        });

        Assert.Equal(string.Empty, olcu.kapali);
        Assert.Equal("true", olcu.dosya);
        Assert.False(olcu.bostaAcik);
        Assert.Equal("ac | kapa", olcu.cagri);
        Assert.True(olcu.mini);
        Assert.False(olcu.ana);
        Assert.False(olcu.yeniKutu);
        Assert.Equal("false", olcu.son);

        Kapat("buyutec-ayar.txt");
    }
}
