using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// EKB-P4 gizlilik maskesi. Maske masaüstü pikselinde saklanır, yakalama alanına kırpılıp
/// kare koordinatına çevrilir ve <c>split</c>, <c>crop</c>, <c>boxblur</c>, <c>overlay</c>
/// dörtlüsüyle kayda yakılır. Bileşik kare <see cref="KaydediciArkaPlanTests"/> düzeninde lavfi
/// girdisiyle koşturulur: maske içi bulanık (ızgaranın sapması düşer), maske dışı ham kareyle
/// bayt bayt aynı. Kamera kolunda kıyas maskesiz kamera karesiyle. Windows kaydının BT.709
/// bağı da kamera bindirmesi de kareyi 4:2:0'a çevirdiği için kutunun 2 px çevresi renk taşmasıyla farklı (ölçüm: 400 piksel), o halka dışarıda. Kanıt <c>.calisma/worktree-agent-afe1c767c9d909a33/maske/</c>.
/// </summary>
public sealed class KaydediciMaskeTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-afe1c767c9d909a33", "maske");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Kanit, adlar);

    private static string Sayac(int n)
        => string.Format(Strings.Culture, LanguageCatalog.Display(Strings.Get("recorder.mask.count")), n);

    private static RecorderRequest Istek(params RecorderRegion[] maskeler) => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Region = new RecorderRegion(100, 50, 320, 240),
        Fps = 10,
        Preset = "ultrafast",
        Container = RecorderContainer.Mkv,
        PrivacyMasks = maskeler
    };

    [Fact]
    public void MaskeZinciriKirpmadanSonraOlceklemedenOnceGirer()
    {
        var args = RecorderArguments.Build(Istek(new RecorderRegion(140, 90, 80, 60)) with { Scale = new RecorderScale(160, 120) }, "a.mkv");

        Assert.Equal(
            "split[pm0][pb0];[pb0]crop=80:60:40:40,boxblur=luma_radius=14:luma_power=3[pk0];[pm0][pk0]overlay=40:40:format=auto,scale=160:120,scale=out_color_matrix=bt709:out_range=tv,format=yuv420p,setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709:range=tv",
            Deger(args, "-vf"));
        Assert.DoesNotContain("split", Deger(RecorderArguments.Build(Istek(), "a.mkv"), "-vf"));
    }

    [Fact]
    public void MaskeYakalamaAlaninaKirpilirCiftlenirVeBuyutulur()
    {
        var kutular = RecorderArguments.MaskBoxes(Istek(
            new RecorderRegion(60, 30, 100, 100),
            new RecorderRegion(400, 280, 50, 50),
            new RecorderRegion(500, 500, 10, 10),
            new RecorderRegion(200, 100, 3, 3),
            new RecorderRegion(141, 91, 79, 59),
            new RecorderRegion(417, 60, 3, 3)));

        Assert.Equal(new[]
        {
            new RecorderRegion(0, 0, 60, 80),
            new RecorderRegion(300, 230, 20, 10),
            new RecorderRegion(100, 50, 8, 8),
            new RecorderRegion(40, 40, 80, 60),
            new RecorderRegion(312, 10, 8, 8)
        }, kutular);
    }

    [Fact]
    public void GecersizMaskeVeAlaniBilinmeyenHedefReddedilir()
    {
        var bos = RecorderArguments.Validate(Istek(new RecorderRegion(140, 90, 0, 60)), "a.mkv");
        Assert.Contains(bos, h => h.Contains("privacy mask", StringComparison.OrdinalIgnoreCase));

        var pencere = Istek(new RecorderRegion(140, 90, 80, 60)) with
        {
            Target = RecorderTargetKind.Window,
            WindowTitle = "Not Defteri",
            Region = null,
            WindowRegion = new RecorderRegion(0, 0, 640, 480)
        };
        Assert.Contains(RecorderArguments.Validate(pencere, "a.mkv"), h => h.Contains("privacy mask", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(RecorderArguments.Validate(pencere with { PrivacyMasks = Array.Empty<RecorderRegion>() }, "a.mkv"));

        var ekran = Istek(new RecorderRegion(140, 90, 80, 60)) with
        {
            Target = RecorderTargetKind.Screen,
            Region = null,
            Screens = new[] { new ScreenBounds(0, 0, 0, 640, 480) }
        };
        Assert.Empty(RecorderArguments.Validate(ekran, "a.mkv"));
        Assert.Equal(new[] { new RecorderRegion(140, 90, 80, 60) }, RecorderArguments.MaskBoxes(ekran));
    }

    private const string Izgara = "color=c=white:s=320x240:r=10:d=0.3,drawgrid=w=8:h=8:t=4:c=red,format=bgra";

    private static byte[] Kare(string ad, string graf, bool kamera = false)
    {
        var raw = Path.Combine(Kanit, ad + ".rgb");
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        var args = new List<string> { "-hide_banner", "-y", "-f", "lavfi", "-i", Izgara };
        if (kamera) args.AddRange(new[] { "-f", "lavfi", "-i", "color=c=0x0000FF:s=64x48:r=10:d=0.3" });
        args.AddRange(kamera
            ? new[] { "-filter_complex", graf, "-map", "[" + RecorderArguments.WebcamOutputLabel + "]" }
            : new[] { "-vf", graf });
        args.AddRange(new[] { "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "-threads", "2", raw });
        foreach (var a in args) psi.ArgumentList.Add(a);

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

    private static double Sapma(byte[] b, RecorderRegion kutu)
    {
        var degerler = new List<double>();
        for (var y = kutu.Y; y < kutu.Y + kutu.Height; y++)
        for (var x = kutu.X; x < kutu.X + kutu.Width; x++)
            degerler.Add(b[(y * 320 + x) * 3 + 1]);
        var ort = degerler.Average();
        return Math.Sqrt(degerler.Average(d => (d - ort) * (d - ort)));
    }

    private static int DisFarki(byte[] ham, byte[] maskeli, IReadOnlyList<RecorderRegion> kutular, int pay = 0)
    {
        var fark = 0;
        for (var y = 0; y < 240; y++)
        for (var x = 0; x < 320; x++)
        {
            bool Icinde(RecorderRegion k) => x >= k.X - pay && x < k.X + k.Width + pay && y >= k.Y - pay && y < k.Y + k.Height + pay;
            if (kutular.Any(Icinde)) continue;
            var i = (y * 320 + x) * 3;
            if (ham[i] != maskeli[i] || ham[i + 1] != maskeli[i + 1] || ham[i + 2] != maskeli[i + 2]) fark++;
        }

        return fark;
    }

    [Fact]
    public void BilesikKaredeMaskeIciBulanikDisiDegismemis()
    {
        var istek = Istek(new RecorderRegion(140, 90, 80, 60), new RecorderRegion(400, 60, 40, 40));
        var kutular = RecorderArguments.MaskBoxes(istek);
        var ham = Kare("ham", RecorderArguments.SdrColourLink(istek));
        var maskeli = Kare("maskeli", Deger(RecorderArguments.Build(istek, "a.mkv"), "-vf")!);

        var kameraIstek = istek with { Webcam = new RecorderWebcam("Kam", 64) };
        var kameraHam = Kare("kameraham", RecorderArguments.WebcamGraph(kameraIstek with { PrivacyMasks = Array.Empty<RecorderRegion>() })!, kamera: true);
        var kameraKaresi = Kare("kamerali", RecorderArguments.WebcamGraph(kameraIstek)!, kamera: true);

        var olcu = kutular.Select(k => (k, ham: Sapma(ham, k), maskeli: Sapma(maskeli, k), kamerali: Sapma(kameraKaresi, k))).ToList();
        var dis = DisFarki(ham, maskeli, kutular, pay: 2);
        var kameraDis = DisFarki(kameraHam, kameraKaresi, kutular, pay: 2);
        var kameraHalka = DisFarki(kameraHam, kameraKaresi, kutular) - kameraDis;
        File.WriteAllLines(Path.Combine(Kanit, "olcum.txt"),
            olcu.Select(o => $"{o.k}\tham sapma={o.ham:0.0}\tmaskeli={o.maskeli:0.0}\tkamerali={o.kamerali:0.0}")
                .Append($"maske disi farkli piksel: vf={dis} kamerali={kameraDis} kamerali 2 px halka={kameraHalka}"));

        Assert.Equal(2, olcu.Count);
        Assert.All(olcu, o =>
        {
            Assert.True(o.ham > 100, $"{o.k} ham {o.ham}");
            Assert.True(o.maskeli < 20, $"{o.k} maskeli {o.maskeli}");
            Assert.True(o.kamerali < 20, $"{o.k} kamerali {o.kamerali}");
        });
        Assert.Equal(0, dis);
        Assert.Equal(0, kameraDis);

        Kapat("ham.rgb", "ham.log", "maskeli.rgb", "maskeli.log", "kameraham.rgb", "kameraham.log", "kamerali.rgb", "kamerali.log", "olcum.txt");
    }

    [Fact]
    public void CizilenMaskeDosyayaVeSonrakiIstegeGecerTemizleSiler()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var once = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
            Elle(once);
            Bul<ComboBox>(once, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Region;
            Yaz(once, "TxtRegionX", "100");
            Yaz(once, "TxtRegionY", "50");
            Yaz(once, "TxtRegionWidth", "320");
            Yaz(once, "TxtRegionHeight", "240");
            var bosSayi = Bul<TextBlock>(once, "TxtMaskCount").Text;
            once.DrawRegion = _ => Task.FromResult<PixelRect?>(new PixelRect(140, 90, 80, 60));
            var cizildi = once.DrawMaskAsync().GetAwaiter().GetResult();
            once.DrawRegion = _ => Task.FromResult<PixelRect?>(null);
            var vazgecildi = once.DrawMaskAsync().GetAwaiter().GetResult();
            var sayi = Bul<TextBlock>(once, "TxtMaskCount").Text;
            var json = JsonNode.Parse(File.ReadAllText(ayarYolu))?["privacyMasks"]?.ToJsonString();

            var sonra = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
            Elle(sonra);
            var istek = sonra.BuildRequest()?.PrivacyMasks.ToArray();
            var temizleAcik = Bul<Button>(sonra, "BtnClearMasks").IsEnabled;
            Bul<Button>(sonra, "BtnClearMasks").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var temiz = JsonNode.Parse(File.ReadAllText(ayarYolu))?["privacyMasks"]?.ToJsonString();
            return (bosSayi, cizildi, vazgecildi, sayi, json, istek, temizleAcik, temiz,
                temizleSonra: Bul<Button>(sonra, "BtnClearMasks").IsEnabled);
        }));

        Assert.Equal(Sayac(0), olcu.bosSayi);
        Assert.True(olcu.cizildi);
        Assert.False(olcu.vazgecildi);
        Assert.Equal(Sayac(1), olcu.sayi);
        Assert.Equal("[{\"x\":140,\"y\":90,\"width\":80,\"height\":60}]", olcu.json);
        Assert.Equal(new[] { new RecorderRegion(140, 90, 80, 60) }, olcu.istek);
        Assert.True(olcu.temizleAcik);
        Assert.Equal("[]", olcu.temiz);
        Assert.False(olcu.temizleSonra);
    }
}
