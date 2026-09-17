using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// Paket 2b T13, kamera arka planını kaldırma (fable kararı: modelsiz, <c>backgroundkey</c> ve
/// <c>chromakey</c>). Argüman pimi, geçersiz kipin reddi, seçimin json'a ve sonraki isteğe geçmesi;
/// üretilen grafik lavfi girdileriyle gerçekten koşturulur ve bileşik karenin pikselleri okunur.
/// Negatif kontroller: <c>Keep</c> yeşili bırakır; kameranın dörtte üçünü kaplayan nesneyi
/// ffmpeg'in varsayılan eşiği (0,08) ve 0,5 sahne değişimi sayıp siler, seçilen 0,8 tutar.
/// Kanıt <c>.calisma/paket-2b/arka-plan/</c>.
/// </summary>
public sealed class KaydediciArkaPlanTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "paket-2b", "arka-plan");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static RecorderRequest Istek(WebcamBackground arka, int genislik = 160) => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Region = new RecorderRegion(0, 0, 320, 240),
        Fps = 10,
        Preset = "ultrafast",
        Container = RecorderContainer.Mkv,
        Webcam = new RecorderWebcam("Kam", genislik, WebcamCorner.TopLeft, arka)
    };

    [Theory]
    [InlineData(WebcamBackground.Keep, "[1:v]scale=240:-2[cam];")]
    [InlineData(WebcamBackground.Static, "[1:v]scale=240:-2,format=yuva420p,backgroundkey=threshold=0.8:similarity=0.1:blend=0[cam];")]
    [InlineData(WebcamBackground.Green, "[1:v]scale=240:-2,format=yuva420p,chromakey=color=0x00FF00:similarity=0.15:blend=0.05[cam];")]
    public void ArkaPlanKipiKameraDaliniAnahtarlar(WebcamBackground arka, string beklenen)
    {
        var args = RecorderArguments.Build(Istek(arka, 240), "a.mkv");
        var graf = Deger(args, "-filter_complex")!;

        Assert.Contains(beklenen, graf);
        Assert.True(graf.IndexOf("[cam];", StringComparison.Ordinal) < graf.IndexOf("overlay=", StringComparison.Ordinal));
        Assert.Empty(RecorderArguments.Validate(Istek(arka, 240), "a.mkv"));
    }

    [Fact]
    public void TanimsizArkaPlanKipiReddedilir()
    {
        var hatalar = RecorderArguments.Validate(Istek((WebcamBackground)7), "a.mkv");
        Assert.Contains(hatalar, h => h.Contains("background mode", StringComparison.Ordinal));
        Assert.Single(hatalar);
    }

    private static (string Kutu, string Yesil, string Dis) Bilesik(string ad, string graf, string kutu = "x=60:y=40:w=40:h=40")
    {
        var raw = Path.Combine(Kanit, ad + ".rgb");
        var kamera = $"color=c=0x00FF00:s=160x120:r=10:d=1.2,drawbox={kutu}:c=0x0000FF:t=fill:enable='gte(t,0.5)'";
        var psi = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in new[]
                 {
                     "-hide_banner", "-y", "-f", "lavfi", "-i", "color=c=0xFF0000:s=320x240:r=10:d=1.2",
                     "-f", "lavfi", "-i", kamera, "-filter_complex", graf, "-map", "[" + RecorderArguments.WebcamOutputLabel + "]",
                     "-ss", "0.9", "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "-threads", "1", raw
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

        string Px(int x, int y)
        {
            var i = (y * 320 + x) * 3;
            return b[i] > 200 && b[i + 1] < 50 && b[i + 2] < 50 ? "kirmizi"
                : b[i] < 50 && b[i + 1] > 200 && b[i + 2] < 50 ? "yesil"
                : b[i] < 50 && b[i + 1] < 50 && b[i + 2] > 200 ? "mavi"
                : $"{b[i]},{b[i + 1]},{b[i + 2]}";
        }

        return (Px(16 + 80, 16 + 60), Px(16 + 10, 16 + 10), Px(300, 220));
    }

    [Fact]
    public void AnahtarlananKameraKaresiPikseldeArkaPlaniBirakir()
    {
        var sonuc = new Dictionary<string, (string Kutu, string Yesil, string Dis)>();
        foreach (var arka in Enum.GetValues<WebcamBackground>())
            sonuc[arka.ToString()] = Bilesik(arka.ToString(), RecorderArguments.WebcamGraph(Istek(arka))!);

        const string buyukKutu = "x=20:y=0:w=120:h=120";
        var statik = RecorderArguments.WebcamGraph(Istek(WebcamBackground.Static))!;
        sonuc["StaticBuyukKutu"] = Bilesik("StaticBuyukKutu", statik, buyukKutu);
        foreach (var esik in new[] { "0.08", "0.5" })
            sonuc["StaticBuyukKutuEsik" + esik] = Bilesik("StaticBuyukKutuEsik" + esik,
                statik.Replace("threshold=0.8", "threshold=" + esik, StringComparison.Ordinal), buyukKutu);

        File.WriteAllLines(Path.Combine(Kanit, "pikseller.txt"),
            sonuc.Select(s => $"{s.Key}\tkutu={s.Value.Kutu}\tkamera arka plani={s.Value.Yesil}\tkamera disi={s.Value.Dis}"));

        Assert.Equal(("mavi", "yesil", "kirmizi"), sonuc["Keep"]);
        Assert.Equal(("mavi", "kirmizi", "kirmizi"), sonuc["Green"]);
        Assert.Equal(("mavi", "kirmizi", "kirmizi"), sonuc["Static"]);
        Assert.Equal(("mavi", "kirmizi", "kirmizi"), sonuc["StaticBuyukKutu"]);
        Assert.Equal(("kirmizi", "kirmizi", "kirmizi"), sonuc["StaticBuyukKutuEsik0.08"]);
        Assert.Equal(("kirmizi", "kirmizi", "kirmizi"), sonuc["StaticBuyukKutuEsik0.5"]);
    }

    [Fact]
    public void ArkaPlanSecimiDosyayaVeSonrakiIstegeGecer()
    {
        var olcu = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            IReadOnlyList<string> kameralar = new[] { "Kam A" };
            var once = new RecorderView { SkipAutoMeasure = true, CameraSource = () => kameralar };
            Elle(once);
            var ogeler = Bul<ComboBox>(once, "CmbWebcamBackground").ItemsSource!.Cast<object>().Count();
            var ilk = Bul<ComboBox>(once, "CmbWebcamBackground").SelectedIndex;
            Bul<ComboBox>(once, "CmbWebcam").SelectedIndex = 1;
            Bul<ComboBox>(once, "CmbWebcamBackground").SelectedIndex = Array.IndexOf(RecorderView.WebcamBackgrounds, WebcamBackground.Green);
            var json = JsonNode.Parse(File.ReadAllText(RecorderSettings.FilePath!))?["webcamBackground"]?.ToJsonString();

            var sonra = new RecorderView { SkipAutoMeasure = true, CameraSource = () => kameralar };
            return (ogeler, ilk, json, istek: sonra.PrepareRecording()!.Value.Request.Webcam);
        }));

        Assert.Equal(3, olcu.ogeler);
        Assert.Equal(0, olcu.ilk);
        Assert.Equal("\"Green\"", olcu.json);
        Assert.Equal(new RecorderWebcam("Kam A", 240, WebcamCorner.BottomRight, WebcamBackground.Green), olcu.istek);
    }
}
