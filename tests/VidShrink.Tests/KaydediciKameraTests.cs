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
        Assert.Equal("[0:v]null[base];[1:v]scale=240:-2[cam];[base][cam]overlay=16:16:eof_action=repeat,scale=out_color_matrix=bt709:out_range=tv,format=yuv420p,setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709:range=tv[vout]", Deger(kamerali, "-filter_complex"));
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
        var ses = AudioCaptureArguments.Build(new AudioCaptureSelection(Mikrofon), new[] { Mikrofon }, RecorderArguments.AudioFirstInputIndexFor(RecorderTargetKind.Screen));
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
        Assert.Empty(Hata(Istek() with { Platform = RecorderPlatform.MacOs, Region = null, Target = RecorderTargetKind.Screen, Webcam = new RecorderWebcam("Kam", 320) }));
        Assert.Single(Hata(Istek() with { Platform = RecorderPlatform.MacOs, Region = null, Target = RecorderTargetKind.Screen, Webcam = new RecorderWebcam("Kam:0", 320) }));
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
            var dosya = (Dosyada(ayarYolu, "webcamName"), Dosyada(ayarYolu, "webcamX"), Dosyada(ayarYolu, "webcamSize"));

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
            $"json webcamName={olcu.dosya.Item1} webcamX={olcu.dosya.Item2} webcamSize={olcu.dosya.Item3}",
            $"yeni gorunum istegi={olcu.istek}",
            $"aygit listede yok: istek={olcu.yokIstek?.ToString() ?? "yok"} json ad={olcu.adKaldi}"
        });

        Assert.Null(olcu.bosIstek);
        Assert.Equal(3, olcu.ogeler);
        Assert.Equal(("\"Kam B\"", "1", "0.2"), olcu.dosya);
        Assert.Equal(new RecorderWebcam("Kam B", RecorderArguments.DefaultWebcamWidth) { Placement = new WebcamPlacement(1, 1, 0.2) }, olcu.istek);
        Assert.Null(olcu.yokIstek);
        Assert.Equal("\"Kam B\"", olcu.adKaldi);

        Kapat("kamera-ayar.txt");
    }

    /// <summary>
    /// Kamera seçilmemişken biçim, ayna ve arka plan kapalı; aygıt seçilince açılır, "Kamera yok"a
    /// dönünce yine kapanır. Olumsuz kontrol aygıt kutusunun kendisi: o hep açık.
    /// </summary>
    [Fact]
    public void KameraSecilmeyinceKameraAyarlariKapali()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true, CameraSource = () => new[] { "Kam A" } };
            var kutular = new Control[] { Bul<ComboBox>(view, "CmbWebcamShape"), Bul<CheckBox>(view, "ChkWebcamMirror"), Bul<ComboBox>(view, "CmbWebcamBackground") };
            var aygit = Bul<ComboBox>(view, "CmbWebcam");
            var bos = kutular.Select(k => k.IsEnabled).ToArray();
            aygit.SelectedIndex = 1;
            var secili = kutular.Select(k => k.IsEnabled).ToArray();
            aygit.SelectedIndex = 0;
            var geri = kutular.Select(k => k.IsEnabled).ToArray();
            return (bos, secili, geri, aygitAcik: aygit.IsEnabled);
        }));

        Assert.All(olcu.bos, acik => Assert.False(acik));
        Assert.All(olcu.secili, acik => Assert.True(acik));
        Assert.All(olcu.geri, acik => Assert.False(acik));
        Assert.True(olcu.aygitAcik);
    }

    private const string DaireMaskesi =
        "format=yuva420p,geq=lum='lum(X,Y)':cb='cb(X,Y)':cr='cr(X,Y)':a='alpha(X,Y)*lte(hypot(max(abs(X-(W-1)/2)-W/2+min(W,H)/2,0),max(abs(Y-(H-1)/2)-H/2+min(W,H)/2,0)),min(W,H)/2)'";

    private const string KoseMaskesi =
        "format=yuva420p,geq=lum='lum(X,Y)':cb='cb(X,Y)':cr='cr(X,Y)':a='alpha(X,Y)*lte(hypot(max(abs(X-(W-1)/2)-W/2+min(W,H)/5,0),max(abs(Y-(H-1)/2)-H/2+min(W,H)/5,0)),min(W,H)/5)'";

    private static RecorderWebcam Kamera(WebcamShape bicim = WebcamShape.Rectangle, bool ayna = false, WebcamPlacement? yer = null,
        WebcamBackground arka = WebcamBackground.Keep)
        => new RecorderWebcam("Kam", 240, WebcamCorner.TopLeft, arka) { Shape = bicim, Mirror = ayna, Placement = yer };

    [Theory]
    [InlineData(WebcamShape.Rectangle, false, "[1:v]scale=240:-2[cam];")]
    [InlineData(WebcamShape.Rectangle, true, "[1:v]hflip,scale=240:-2[cam];")]
    [InlineData(WebcamShape.Circle, false, "[1:v]crop='min(iw,ih)':'min(iw,ih)',scale=240:-2," + DaireMaskesi + "[cam];")]
    [InlineData(WebcamShape.Rounded, true, "[1:v]hflip,scale=240:-2," + KoseMaskesi + "[cam];")]
    public void BicimVeAynaKameraDalinaSuzgecEkler(WebcamShape bicim, bool ayna, string beklenen)
    {
        var istek = Istek() with { Webcam = Kamera(bicim, ayna) };
        var graf = Deger(RecorderArguments.Build(istek, "a.mkv"), "-filter_complex")!;

        Assert.Contains(beklenen, graf);
        Assert.EndsWith("[base][cam]overlay=16:16:eof_action=repeat,scale=out_color_matrix=bt709:out_range=tv,format=yuv420p,setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709:range=tv[vout]", graf);
        Assert.Empty(RecorderArguments.Validate(istek, "a.mkv"));
    }

    [Fact]
    public void ArkaPlanAnahtariVeDaireTekBicimDonusumuyleBirlesir()
    {
        var graf = RecorderArguments.WebcamGraph(Istek() with { Webcam = Kamera(WebcamShape.Circle, arka: WebcamBackground.Green) })!;

        Assert.Contains(",format=yuva420p,chromakey=color=0x00FF00:similarity=0.15:blend=0.05,geq=", graf);
        Assert.Equal(1, graf.Split("format=yuva420p").Length - 1);
    }

    [Fact]
    public void SerbestKonumVeBoyutKareninOraniylaYazilir()
    {
        var istek = Istek() with { Scale = new RecorderScale(640, 480), Webcam = Kamera(WebcamShape.Circle, true, new WebcamPlacement(0.5, 1, 0.25)) };
        var graf = Deger(RecorderArguments.Build(istek, "a.mkv"), "-filter_complex");

        Assert.Equal("[0:v]scale=640:480,split[base][wref];"
                     + "[1:v]hflip,crop='min(iw,ih)':'min(iw,ih)'[wsrc];"
                     + "[wsrc][wref]scale=w='trunc(rw*0.25/2)*2':h=-2," + DaireMaskesi + "[cam];"
                     + "[base][cam]overlay=x='16+(main_w-overlay_w-32)*0.5':y='16+(main_h-overlay_h-32)*1':eof_action=repeat,scale=out_color_matrix=bt709:out_range=tv,format=yuv420p,setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709:range=tv[vout]", graf);
        Assert.Empty(RecorderArguments.Validate(istek, "a.mkv"));
    }

    [Fact]
    public void GecersizBicimVeKonumReddedilir()
    {
        string[] Hata(RecorderWebcam kam) => RecorderArguments.Validate(Istek() with { Webcam = kam }, "a.mkv").ToArray();

        Assert.Empty(Hata(Kamera(yer: new WebcamPlacement(0, 1, RecorderArguments.MaxWebcamSize))));
        Assert.Empty(Hata(Kamera(yer: new WebcamPlacement(1, 0, RecorderArguments.MinWebcamSize))));
        Assert.Contains(Hata(Kamera((WebcamShape)9)), h => h.Contains("webcam shape", StringComparison.Ordinal));
        Assert.Single(Hata(Kamera((WebcamShape)9)));
        foreach (var yer in new[]
                 {
                     new WebcamPlacement(-0.01, 0, 0.2), new WebcamPlacement(0, 1.01, 0.2), new WebcamPlacement(double.NaN, 0, 0.2),
                     new WebcamPlacement(0, 0, RecorderArguments.MinWebcamSize - 0.01), new WebcamPlacement(0, 0, RecorderArguments.MaxWebcamSize + 0.01)
                 })
            Assert.Single(Hata(Kamera(yer: yer)));
    }

    [Fact]
    public void KameraKutusuSurukleninceKonumOraniTasinirBoyutKisilir()
    {
        var alan = new RecorderRegion(100, 50, 832, 432);
        var yer = new WebcamPlacement(1, 1, 0.25);

        Assert.Equal(new RecorderRegion(100 + 832 - 16 - 208, 50 + 432 - 16 - 156, 208, 156), WebcamLayout.Rect(alan, yer, WebcamShape.Rectangle));
        Assert.Equal(new RecorderRegion(100 + 832 - 16 - 208, 50 + 432 - 16 - 208, 208, 208), WebcamLayout.Rect(alan, yer, WebcamShape.Circle));

        var orta = WebcamLayout.Move(alan, yer, WebcamShape.Rectangle, -(832 - 32 - 208) / 2, -(432 - 32 - 156) / 2);
        Assert.Equal(new WebcamPlacement(0.5, 0.5, 0.25), orta);
        Assert.Equal(new WebcamPlacement(0, 1, 0.25), WebcamLayout.Move(alan, yer, WebcamShape.Rectangle, -5000, 5000));

        var buyuk = WebcamLayout.Resize(alan, new WebcamPlacement(0, 0, 0.25), WebcamShape.Rectangle, 5000);
        Assert.Equal(RecorderArguments.MaxWebcamSize, buyuk.Size);
        Assert.Equal((0d, 0d), (buyuk.X, buyuk.Y));
        Assert.Equal(RecorderArguments.MinWebcamSize, WebcamLayout.Resize(alan, yer, WebcamShape.Rectangle, -5000).Size);

        var ust = WebcamLayout.Rect(alan, new WebcamPlacement(0.5, 0.5, 0.25), WebcamShape.Rectangle);
        var buyutulen = WebcamLayout.Resize(alan, new WebcamPlacement(0.5, 0.5, 0.25), WebcamShape.Rectangle, 40);
        var sonra = WebcamLayout.Rect(alan, buyutulen, WebcamShape.Rectangle);
        Assert.Equal((ust.X, ust.Y), (sonra.X, sonra.Y));
        Assert.Equal(ust.Width + 40, sonra.Width);
    }

    private static byte[] Kare(string ad, string graf)
    {
        var raw = Path.Combine(Kanit, ad + ".rgb");
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[]
                 {
                     "-hide_banner", "-y", "-f", "lavfi", "-i", "color=c=0xFF0000:s=320x240:r=10:d=1",
                     "-f", "lavfi", "-i", "color=c=0x00FF00:s=160x120:r=10:d=1,drawbox=x=0:y=0:w=60:h=120:c=0x0000FF:t=fill",
                     "-filter_complex", graf, "-map", "[" + RecorderArguments.WebcamOutputLabel + "]",
                     "-ss", "0.5", "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "-threads", "2", raw
                 })
            psi.ArgumentList.Add(a);

        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(20000))
        {
            surec.Kill(true);
            Assert.Fail($"{ad}: ffmpeg 20 sn icinde bitmedi.");
        }

        File.WriteAllText(Path.Combine(Kanit, ad + ".log"), graf + Environment.NewLine + hata.GetAwaiter().GetResult());
        Assert.True(surec.ExitCode == 0, $"{ad}: ffmpeg cikis {surec.ExitCode}");
        var b = File.ReadAllBytes(raw);
        Assert.Equal(320 * 240 * 3, b.Length);
        return b;
    }

    private static string Renk(byte[] b, int x, int y)
    {
        var i = (y * 320 + x) * 3;
        return b[i] > 200 && b[i + 1] < 50 && b[i + 2] < 50 ? "kirmizi"
            : b[i] < 50 && b[i + 1] > 200 && b[i + 2] < 50 ? "yesil"
            : b[i] < 50 && b[i + 1] < 50 && b[i + 2] > 200 ? "mavi"
            : $"{b[i]},{b[i + 1]},{b[i + 2]}";
    }

    private static RecorderRegion Kutu(byte[] b)
    {
        int x0 = 320, y0 = 240, x1 = -1, y1 = -1;
        for (var y = 0; y < 240; y++)
            for (var x = 0; x < 320; x++)
                if (Renk(b, x, y) != "kirmizi")
                {
                    x0 = Math.Min(x0, x);
                    y0 = Math.Min(y0, y);
                    x1 = Math.Max(x1, x);
                    y1 = Math.Max(y1, y);
                }
        return new RecorderRegion(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    /// <summary>
    /// Plan EKB-P5: lavfi bileşik karesinde kamera kutusu <see cref="WebcamLayout.Rect"/>'in düzenleyicide
    /// çizdiği yere oturur; daire ve yuvarlak köşede kutunun köşe pikseli saydam kalır (alttaki kırmızı
    /// görünür), dikdörtgende kalmaz. Ayna, kameranın sol şeridindeki maviyi sağa taşır.
    /// </summary>
    [Fact]
    public void BicimKonumVeAynaBilesikKaredePikseldeOlculur()
    {
        var alan = new RecorderRegion(0, 0, 320, 240);
        var durumlar = new (string Ad, WebcamShape Bicim, bool Ayna, WebcamPlacement Yer)[]
        {
            ("dikdortgen", WebcamShape.Rectangle, false, new WebcamPlacement(0, 0, 0.25)),
            ("ayna", WebcamShape.Rectangle, true, new WebcamPlacement(0, 0, 0.25)),
            ("daire", WebcamShape.Circle, false, new WebcamPlacement(0.5, 1, 0.25)),
            ("yuvarlak", WebcamShape.Rounded, false, new WebcamPlacement(1, 0, 0.25))
        };

        var satirlar = new List<string>();
        var olcu = new Dictionary<string, (RecorderRegion Beklenen, RecorderRegion Kutu, string Kose, string Orta, string Sol, string Sag)>();
        foreach (var d in durumlar)
        {
            var istek = Istek() with { Webcam = Kamera(d.Bicim, d.Ayna, d.Yer) };
            var b = Kare(d.Ad, RecorderArguments.WebcamGraph(istek)!);
            var r = WebcamLayout.Rect(alan, d.Yer, d.Bicim);
            olcu[d.Ad] = (r, Kutu(b), Renk(b, r.X, r.Y), Renk(b, r.X + r.Width / 2, r.Y + r.Height / 2),
                Renk(b, r.X + 2, r.Y + r.Height / 2), Renk(b, r.X + r.Width - 3, r.Y + r.Height / 2));
            satirlar.Add($"{d.Ad}\tbeklenen={r}\tolculen={olcu[d.Ad].Kutu}\tkose={olcu[d.Ad].Kose}\torta={olcu[d.Ad].Orta}\tsol={olcu[d.Ad].Sol}\tsag={olcu[d.Ad].Sag}");
        }

        File.WriteAllLines(Path.Combine(Kanit, "bicim-konum.txt"), satirlar);

        foreach (var d in durumlar) Assert.Equal(olcu[d.Ad].Beklenen, olcu[d.Ad].Kutu);
        Assert.Equal(new RecorderRegion(16, 16, 80, 60), olcu["dikdortgen"].Beklenen);
        Assert.Equal(("mavi", "yesil"), (olcu["dikdortgen"].Kose, olcu["dikdortgen"].Sag));
        Assert.Equal(("yesil", "mavi"), (olcu["ayna"].Sol, olcu["ayna"].Sag));
        Assert.Equal(("kirmizi", "yesil"), (olcu["daire"].Kose, olcu["daire"].Orta));
        Assert.Equal(("kirmizi", "yesil"), (olcu["yuvarlak"].Kose, olcu["yuvarlak"].Orta));
        Assert.Equal("yesil", olcu["yuvarlak"].Sag);

        Kapat(durumlar.SelectMany(d => new[] { d.Ad + ".rgb", d.Ad + ".log" }).Append("bicim-konum.txt").ToArray());
    }

    private sealed class SahteKameraDuzenleyici : IRegionEditorHost
    {
        public (WebcamPlacement? Yer, WebcamShape Bicim)? Kamera { get; private set; }

        public bool IsOpen { get; private set; }

        public event EventHandler<PixelRect>? Changed { add { } remove { } }

        public event EventHandler<PixelRect>? Committed { add { } remove { } }

        public event EventHandler<WebcamPlacement>? CameraCommitted;

        public event EventHandler? StartRequested { add { } remove { } }

        public event EventHandler? SettingsRequested { add { } remove { } }

        public event EventHandler? PauseRequested { add { } remove { } }

        public event EventHandler? ResumeRequested { add { } remove { } }

        public event EventHandler? StopRequested { add { } remove { } }

        public event EventHandler? SnapshotRequested { add { } remove { } }

        public event EventHandler? HideRequested { add { } remove { } }

        public event EventHandler? Dismissed { add { } remove { } }

        public void Show(PixelRect region, double? ratio, RegionEditorPhase phase) => IsOpen = true;

        public void ShowCamera(WebcamPlacement? placement, WebcamShape shape) => Kamera = (placement, shape);

        public void Hide()
        {
        }

        public void Close() => IsOpen = false;

        public void Birak(WebcamPlacement yer) => CameraCommitted?.Invoke(this, yer);
    }

    [Fact]
    public void KameraBicimiAynaVeSuruklenenKonumDosyayaVeSonrakiIstegeGecer()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            IReadOnlyList<string> kameralar = new[] { "Kam A" };
            var sahte = new SahteKameraDuzenleyici();
            var once = new RecorderView(ayarYolu) { SkipAutoMeasure = true, RegionEditorEnabled = true, RegionEditor = sahte, CameraSource = () => kameralar };
            Elle(once);
            var bicimler = Bul<ComboBox>(once, "CmbWebcamShape").ItemsSource!.Cast<object>().Count();
            Bul<ComboBox>(once, "CmbWebcam").SelectedIndex = 1;
            Bul<ComboBox>(once, "CmbWebcamShape").SelectedIndex = Array.IndexOf(RecorderView.WebcamShapes, WebcamShape.Circle);
            Bul<CheckBox>(once, "ChkWebcamMirror").IsChecked = true;
            once.DrawRegion = _ => Task.FromResult<PixelRect?>(new PixelRect(100, 50, 640, 360));
            once.DrawRegionAsync().GetAwaiter().GetResult();
            var gosterilen = sahte.Kamera;
            sahte.Birak(new WebcamPlacement(0.25, 0.75, 0.3));
            var json = (Dosyada(ayarYolu, "webcamShape"), Dosyada(ayarYolu, "webcamMirror"), Dosyada(ayarYolu, "webcamX"),
                Dosyada(ayarYolu, "webcamY"), Dosyada(ayarYolu, "webcamSize"));

            var sonra = new RecorderView(ayarYolu) { SkipAutoMeasure = true, CameraSource = () => kameralar };
            var istek = sonra.PrepareRecording()!.Value.Request.Webcam;

            var eski = JsonNode.Parse(File.ReadAllText(ayarYolu))!.AsObject();
            foreach (var ad in new[] { "webcamShape", "webcamMirror", "webcamX", "webcamY", "webcamSize" }) eski.Remove(ad);
            eski["webcamCorner"] = "TopLeft";
            eski["webcamWidth"] = 480;
            File.WriteAllText(ayarYolu, eski.ToJsonString());
            var gocen = new RecorderView(ayarYolu) { SkipAutoMeasure = true, CameraSource = () => kameralar }.PrepareRecording()!.Value.Request.Webcam;
            return (bicimler, gosterilen, json, istek, gocen);
        }));

        Assert.Equal(3, olcu.bicimler);
        Assert.Equal((new WebcamPlacement(1, 1, 0.2), WebcamShape.Circle), olcu.gosterilen);
        Assert.Equal(("\"Circle\"", "true", "0.25", "0.75", "0.3"), olcu.json);
        Assert.Equal(new RecorderWebcam("Kam A", RecorderArguments.DefaultWebcamWidth)
            { Shape = WebcamShape.Circle, Mirror = true, Placement = new WebcamPlacement(0.25, 0.75, 0.3) }, olcu.istek);
        Assert.Equal(new WebcamPlacement(0, 0, 0.2), olcu.gocen!.Placement);
        Assert.Equal((WebcamShape.Rectangle, false), (olcu.gocen.Shape, olcu.gocen.Mirror));
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

    [WindowsFact("büyüteç ekranı GDI ile kopyalar; başka sistemde seçenek gizli")]
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
