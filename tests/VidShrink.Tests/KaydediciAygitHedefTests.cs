using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// Later 69: web kamerası ya da yakalama kartı ekranın yerine ana görüntü. Gerçek aygıt açılmaz;
/// argüman ve sahte kamera listesiyle ölçülür, takılı kamerayla deneme Windows'ta elle yapılır.
/// </summary>
public sealed class KaydediciAygitHedefTests
{
    private static RecorderRequest Istek() => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Device,
        Device = "Kart A",
        Fps = 30,
        Preset = "ultrafast",
        Container = RecorderContainer.Mkv
    };

    private static List<string> Girdiler(IReadOnlyList<string> args)
        => args.Select((a, i) => (a, i)).Where(x => x.a == "-i").Select(x => args[x.i + 1]).ToList();

    private static string? Deger(IReadOnlyList<string> args, string bayrak)
    {
        var i = args.ToList().IndexOf(bayrak);
        return i < 0 || i + 1 >= args.Count ? null : args[i + 1];
    }

    private static string[] Hata(RecorderRequest r) => RecorderArguments.Validate(r, "a.mkv").ToArray();

    [Fact]
    public void AygitHedefiEkranGirdisininYerineDshowGirdisiKurar()
    {
        var args = RecorderArguments.Build(Istek(), "a.mkv");

        Assert.Empty(Hata(Istek()));
        Assert.Equal(RecorderArguments.DirectShowVideoInput("Kart A"), args.Skip(2).Take(6).ToArray());
        Assert.Equal(new[] { "video=Kart A" }, Girdiler(args));
        Assert.DoesNotContain("gdigrab", args);
        Assert.DoesNotContain("-offset_x", args);
        Assert.DoesNotContain("-framerate", args);
        Assert.DoesNotContain(args, a => a.Contains("ddagrab", StringComparison.Ordinal));
        Assert.Equal("libx264", Deger(args, "-c:v"));
        Assert.Equal("a.mkv", args[^1]);
    }

    [Fact]
    public void KameraKatmaniAyniKurucudanVeAygitinArdindanGelir()
    {
        var ekran = RecorderArguments.Build(Istek() with { Target = RecorderTargetKind.Screen, Device = null, Webcam = new RecorderWebcam("Kam B", 240) }, "a.mkv");
        var ana = RecorderArguments.Build(Istek() with { Webcam = new RecorderWebcam("Kam B", 240) }, "a.mkv");
        var katman = RecorderArguments.DirectShowVideoInput("Kam B");

        int Yer(IReadOnlyList<string> args) => args.ToList().IndexOf("video=Kam B") - 5;
        Assert.Equal(katman, ekran.Skip(Yer(ekran)).Take(6).ToArray());
        Assert.Equal(katman, ana.Skip(Yer(ana)).Take(6).ToArray());
        Assert.Equal(new[] { "video=Kart A", "video=Kam B" }, Girdiler(ana));
        Assert.StartsWith("[0:v]null[base];[1:v]scale=240:-2[cam];", Deger(ana, "-filter_complex"));
        Assert.Equal("[vout]", Deger(ana, "-map"));
    }

    [Fact]
    public void SesGirdisiAygittanSonraKameraSestenSonraSiralanir()
    {
        var mikrofon = new AudioCaptureDevice("Mikrofon", CaptureBackend.DirectShow, AudioSourceRole.Microphone);
        var ses = AudioCaptureArguments.Build(new AudioCaptureSelection(mikrofon), new[] { mikrofon },
            RecorderArguments.AudioFirstInputIndexFor(RecorderTargetKind.Device));
        var args = RecorderArguments.Build(Istek() with { Audio = ses, Webcam = new RecorderWebcam("Kam B", 240) }, "a.mkv");

        Assert.Equal(1, RecorderArguments.AudioFirstInputIndexFor(RecorderTargetKind.Device));
        Assert.Equal(new[] { "video=Kart A", "audio=Mikrofon", "video=Kam B" }, Girdiler(args));
        Assert.Contains("[2:v]scale=240:-2[cam]", Deger(args, "-filter_complex"));
        Assert.Contains("1:a", args);
    }

    [Fact]
    public void GecersizAygitIstegiReddedilir()
    {
        Assert.Contains(Hata(Istek() with { Device = null }), h => h.Contains("capture card name", StringComparison.Ordinal));
        Assert.Contains(Hata(Istek() with { Device = " " }), h => h.Contains("capture card name", StringComparison.Ordinal));
        Assert.Contains(Hata(Istek() with { Platform = RecorderPlatform.MacOs }), h => h.Contains("only available on Windows", StringComparison.Ordinal));
        Assert.Contains(Hata(Istek() with { Platform = RecorderPlatform.Linux }), h => h.Contains("only available on Windows", StringComparison.Ordinal));
        Assert.Contains(Hata(Istek() with { Webcam = new RecorderWebcam("kart a", 240) }), h => h.Contains("already the main picture", StringComparison.Ordinal));
        Assert.Empty(Hata(Istek() with { Webcam = new RecorderWebcam("Kam B", 240) }));
        Assert.Contains(Hata(Istek() with { Capture = RecorderCapture.Ddagrab }), h => h.Contains("DirectShow", StringComparison.Ordinal));
        Assert.Contains(Hata(Istek() with { PrivacyMasks = new[] { new RecorderRegion(0, 0, 64, 64) } }), h => h.Contains("privacy mask", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => RecorderArguments.Build(Istek() with { Device = null }, "a.mkv"));
    }

    [Fact]
    public void EkranYakalamaKararlariAygitHedefineUygulanmaz()
    {
        var hdr = new DdaOutput(0, 0, 0, 1920, 1080, Hdr: true);
        var istek = Istek() with
        {
            DdaOutputs = new[] { hdr },
            Screens = new[] { new ScreenBounds(0, 0, 0, 1920, 1080), new ScreenBounds(1, 1920, 0, 1920, 1080) }
        };

        var secilen = RecorderArguments.ChooseCapture(istek, ddagrabWorks: true, out var sebep);

        Assert.Equal(RecorderCapture.Gdigrab, secilen.Capture);
        Assert.Equal(DdagrabFallback.None, sebep);
        Assert.Null(RecorderArguments.CaptureArea(istek));
        Assert.False(RecorderArguments.HdrOnGdigrab(secilen));
        Assert.Empty(RecorderArguments.MaskBoxes(istek with { PrivacyMasks = new[] { new RecorderRegion(0, 0, 64, 64) } }));
        Assert.Equal(RecorderCapture.Ddagrab,
            RecorderArguments.ChooseCapture(istek with { Target = RecorderTargetKind.Screen, Device = null }, ddagrabWorks: true, out _).Capture);
    }

    [Fact]
    public void AnlikKareAyniAygittanTekKareAlir()
    {
        var args = RecorderArguments.BuildSnapshot(Istek(), "kare.png");

        Assert.Equal(new[] { "video=Kart A" }, Girdiler(args));
        Assert.Contains("dshow", args);
        Assert.Equal("1", Deger(args, "-frames:v"));
        Assert.Equal("kare.png", args[^1]);
    }

    [Fact]
    public void ArayuzAygitHedefiniYalnizWindowsdaSunar()
    {
        Assert.Equal(RecorderTargetKind.Device, RecorderView.TargetKinds(RecorderPlatform.Windows)[(int)RecorderTargetKind.Device]);
        Assert.DoesNotContain(RecorderTargetKind.Device, RecorderView.TargetKinds(RecorderPlatform.MacOs));
        Assert.DoesNotContain(RecorderTargetKind.Device, RecorderView.TargetKinds(RecorderPlatform.Linux));
        Assert.All(RecorderView.TargetKinds(RecorderPlatform.Windows).Select((k, i) => (k, i)), x => Assert.Equal((int)x.k, x.i));
    }

    private static string? Dosyada(string dosya, string anahtar)
        => File.Exists(dosya) ? JsonNode.Parse(File.ReadAllText(dosya))?[anahtar]?.ToJsonString() : null;

    [Fact]
    public void AygitSecimiIstegeVeDosyayaGecerSonrakiGorunumdeKalir()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            File.WriteAllText(ayarYolu, "{\"privacyMasks\":[{\"x\":0,\"y\":0,\"width\":64,\"height\":64}]}");
            IReadOnlyList<string> iki = new[] { "Kam A", "Kart B" };
            var once = new RecorderView(ayarYolu) { SkipAutoMeasure = true, CameraSource = () => iki };
            Elle(once);
            var hedef = Bul<ComboBox>(once, "CmbTarget");
            var ogeSayisi = hedef.ItemsSource!.Cast<object>().Count();
            var satirOnce = Bul<StackPanel>(once, "RowDevice").IsVisible;
            hedef.SelectedIndex = (int)RecorderTargetKind.Device;
            var satirSonra = Bul<StackPanel>(once, "RowDevice").IsVisible;
            var ekranSatiri = Bul<StackPanel>(once, "RowScreen").IsVisible;
            var aygitlar = Bul<ComboBox>(once, "CmbDevice").ItemsSource!.Cast<object>().Select(o => o.ToString()).ToArray();
            Bul<ComboBox>(once, "CmbDevice").SelectedIndex = 1;
            var (istek, yol) = once.PrepareRecording()!.Value;
            var dosya = (Dosyada(ayarYolu, "target"), Dosyada(ayarYolu, "deviceName"));

            var sonra = new RecorderView(ayarYolu) { SkipAutoMeasure = true, CameraSource = () => iki };
            var sonrakiIstek = sonra.PrepareRecording()!.Value.Request;
            Bul<ComboBox>(sonra, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Screen;
            var ekranMaskeleri = sonra.PrepareRecording()!.Value.Request.PrivacyMasks.Count;
            return (ogeSayisi, satirOnce, satirSonra, ekranSatiri, aygitlar, istek, yol, dosya, sonrakiIstek, ekranMaskeleri);
        }));

        Assert.Equal(5, olcu.ogeSayisi);
        Assert.False(olcu.satirOnce);
        Assert.True(olcu.satirSonra);
        Assert.False(olcu.ekranSatiri);
        Assert.Equal(new[] { "Kam A", "Kart B" }, olcu.aygitlar);
        Assert.Equal(RecorderTargetKind.Device, olcu.istek.Target);
        Assert.Equal("Kart B", olcu.istek.Device);
        Assert.Empty(olcu.istek.PrivacyMasks);
        Assert.Empty(RecorderArguments.Validate(olcu.istek with { PreviewPath = null }, olcu.yol));
        Assert.Equal(("\"Device\"", "\"Kart B\""), olcu.dosya);
        Assert.Equal(RecorderTargetKind.Device, olcu.sonrakiIstek.Target);
        Assert.Equal("Kart B", olcu.sonrakiIstek.Device);
        Assert.Empty(olcu.sonrakiIstek.PrivacyMasks);
        Assert.Equal(1, olcu.ekranMaskeleri);
    }

    [Fact]
    public void KomutSatiriKayitliAygitHedefiniKurar()
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "aygit-hedef-cli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var dosya = Path.Combine(klasor, CliRecordSettings.FileName);
            var ayar = new RecorderSettings { OutputFolder = klasor, Target = RecorderTargetKind.Device, DeviceName = "Kart B", ManualMode = true };
            File.WriteAllBytes(dosya, ayar.ToJson());
            var okunan = CliRecordSettings.Load(dosya);
            var cli = CliParser.Parse(new[] { "kaydet", "--sure", "5", "--cikti", Path.Combine(klasor, "a.mkv") }).Request!;
            var makine = new CliRecordMachine(RecorderPlatform.Windows, Array.Empty<AudioCaptureDevice>(), new[] { "Kart B" }, Array.Empty<string>(), 8);

            var plan = CliRecord.Build(cli, okunan, makine, new DateTime(2026, 10, 18, 12, 0, 0), out var hata, out var arguman);

            Assert.True(plan is not null, hata + " " + arguman);
            Assert.Equal("Kart B", okunan.DeviceName);
            Assert.Equal(RecorderTargetKind.Device, plan!.Request.Target);
            Assert.Equal("Kart B", plan.Request.Device);
            Assert.Contains("video=Kart B", RecorderArguments.Build(plan.Request with { PreviewPath = null }, plan.OutputPath));
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }
}
