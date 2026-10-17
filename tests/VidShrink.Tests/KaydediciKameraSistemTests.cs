using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VidShrink.Cli;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Later 70: kamera katmanı ve kamera ana kaynağı (later 69) Windows dışında. macOS'ta
/// avfoundation, Linux'ta v4l2 aynı kurucudan (<see cref="RecorderArguments.CameraInput"/>) kurulur.
/// Gerçek aygıt açılmaz: argüman istekten, Linux listesi geçici klasördeki sahte düğümlerden
/// ölçülür; takılı kamerayla deneme o sistemlerde elle yapılır.
/// </summary>
public sealed class KaydediciKameraSistemTests
{
    private static RecorderRequest Istek(RecorderPlatform platform) => new()
    {
        Platform = platform,
        Target = RecorderTargetKind.Screen,
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

    private static string[] Parca(IReadOnlyList<string> args, string girdi)
    {
        var son = args.ToList().IndexOf(girdi);
        var bas = args.Take(son).ToList().LastIndexOf("-f");
        return args.Skip(bas).Take(son - bas + 1).ToArray();
    }

    [Fact]
    public void HerSistemKendiKameraGirdisiniKurar()
    {
        Assert.Equal(RecorderArguments.DirectShowVideoInput("Kam A"), RecorderArguments.CameraInput(RecorderPlatform.Windows, "Kam A"));
        Assert.Equal(new[] { "-f", "avfoundation", "-framerate", "30", "-i", "FaceTime HD Camera:none" },
            RecorderArguments.CameraInput(RecorderPlatform.MacOs, "FaceTime HD Camera"));
        Assert.Equal(new[] { "-f", "v4l2", "-i", "/dev/video0" }, RecorderArguments.CameraInput(RecorderPlatform.Linux, "/dev/video0"));
        Assert.Throws<InvalidOperationException>(() => RecorderArguments.CameraInput((RecorderPlatform)9, "x"));
    }

    [Fact]
    public void MacKameraKatmaniEkranGirdisininArdindanAvfoundationIleGelir()
    {
        var istek = Istek(RecorderPlatform.MacOs) with { Webcam = new RecorderWebcam("FaceTime HD Camera", 240) };
        var args = RecorderArguments.Build(istek, "a.mkv");

        Assert.Empty(Hata(istek));
        Assert.Equal(new[] { "Capture screen 0:none", "FaceTime HD Camera:none" }, Girdiler(args));
        Assert.Equal(RecorderArguments.CameraInput(RecorderPlatform.MacOs, "FaceTime HD Camera"), Parca(args, "FaceTime HD Camera:none"));
        Assert.DoesNotContain("dshow", args);
        Assert.StartsWith("[0:v]null[base];[1:v]scale=240:-2[cam];", Deger(args, "-filter_complex"));
        Assert.Equal("[vout]", Deger(args, "-map"));
    }

    [Fact]
    public void LinuxKameraKatmaniX11grabArdindanV4l2IleGelir()
    {
        var istek = Istek(RecorderPlatform.Linux) with { Webcam = new RecorderWebcam("/dev/video0", 240) };
        var args = RecorderArguments.Build(istek, "a.mkv");

        Assert.Empty(Hata(istek));
        Assert.Equal(new[] { RecorderArguments.DefaultDisplay, "/dev/video0" }, Girdiler(args));
        Assert.Equal(new[] { "-f", "v4l2", "-i", "/dev/video0" }, Parca(args, "/dev/video0"));
        Assert.True(args.ToList().IndexOf("x11grab") < args.ToList().IndexOf("v4l2"));
        Assert.DoesNotContain("dshow", args);
        Assert.StartsWith("[0:v]null[base];[1:v]scale=240:-2[cam];", Deger(args, "-filter_complex"));
    }

    [Theory]
    [InlineData(RecorderPlatform.MacOs, "Elgato HD60 X", "Elgato HD60 X:none", "avfoundation")]
    [InlineData(RecorderPlatform.Linux, "/dev/v4l/by-id/usb-Elgato-video-index0", "/dev/v4l/by-id/usb-Elgato-video-index0", "v4l2")]
    public void AygitHedefiEkranGirdisininYerineKameraGirdisiKurar(RecorderPlatform platform, string aygit, string girdi, string demuxer)
    {
        var istek = Istek(platform) with { Target = RecorderTargetKind.Device, Device = aygit, ShowClicks = true };
        var args = RecorderArguments.Build(istek, "a.mkv");

        Assert.Empty(Hata(istek));
        Assert.Equal(new[] { girdi }, Girdiler(args));
        Assert.Equal(RecorderArguments.CameraInput(platform, aygit), args.Skip(2).Take(RecorderArguments.CameraInput(platform, aygit).Count).ToArray());
        Assert.Equal(demuxer, Deger(args, "-f"));
        Assert.DoesNotContain("x11grab", args);
        Assert.DoesNotContain("-capture_cursor", args);
        Assert.DoesNotContain("-capture_mouse_clicks", args);
        Assert.DoesNotContain(args, a => a.StartsWith("Capture screen", StringComparison.Ordinal));
        Assert.Equal("a.mkv", args[^1]);
    }

    [Fact]
    public void MacAygitHedefindeSesVeKameraKatmaniSiralanir()
    {
        var mikrofon = new AudioCaptureDevice("Built-in Microphone", CaptureBackend.AvFoundation, AudioSourceRole.Microphone, "0");
        var ses = AudioCaptureArguments.Build(new AudioCaptureSelection(mikrofon), new[] { mikrofon },
            RecorderArguments.AudioFirstInputIndexFor(RecorderTargetKind.Device));
        var istek = Istek(RecorderPlatform.MacOs) with
        {
            Target = RecorderTargetKind.Device,
            Device = "Elgato HD60 X",
            Audio = ses,
            Webcam = new RecorderWebcam("FaceTime HD Camera", 240)
        };
        var args = RecorderArguments.Build(istek, "a.mkv");

        Assert.Empty(Hata(istek));
        Assert.Equal(new[] { "Elgato HD60 X:none", ":0", "FaceTime HD Camera:none" }, Girdiler(args));
        Assert.Contains("[2:v]scale=240:-2[cam]", Deger(args, "-filter_complex"));
        Assert.Contains("1:a", args);
    }

    [Fact]
    public void LinuxAnlikKareAyniV4l2AygitindanAlinir()
    {
        var args = RecorderArguments.BuildSnapshot(Istek(RecorderPlatform.Linux) with { Target = RecorderTargetKind.Device, Device = "/dev/video2" }, "kare.png");

        Assert.Equal(new[] { "/dev/video2" }, Girdiler(args));
        Assert.Equal("v4l2", Deger(args, "-f"));
        Assert.Equal("1", Deger(args, "-frames:v"));
    }

    [Fact]
    public void SisteminYazamayacagiAygitAdiReddedilir()
    {
        var mac = Istek(RecorderPlatform.MacOs);
        var linux = Istek(RecorderPlatform.Linux);
        var windows = Istek(RecorderPlatform.Windows);

        Assert.Contains(Hata(mac with { Webcam = new RecorderWebcam("Kam:1", 240) }), h => h.Contains("colon", StringComparison.Ordinal));
        Assert.Contains(Hata(mac with { Target = RecorderTargetKind.Device, Device = "Kart:1" }), h => h.Contains("colon", StringComparison.Ordinal));
        Assert.Contains(Hata(linux with { Webcam = new RecorderWebcam("Integrated Camera", 240) }), h => h.Contains("device file", StringComparison.Ordinal));
        Assert.Contains(Hata(linux with { Target = RecorderTargetKind.Device, Device = "video0" }), h => h.Contains("device file", StringComparison.Ordinal));
        Assert.Contains(Hata(windows with { Target = RecorderTargetKind.Device, Device = "Kart\" -y" }), h => h.Contains("quotation", StringComparison.Ordinal));
        Assert.Empty(Hata(mac with { Webcam = new RecorderWebcam("Kam \"A\"", 240) }));
        Assert.Empty(Hata(windows with { Webcam = new RecorderWebcam("Kam:1", 240) }));
        Assert.Contains(Hata(linux with { Target = RecorderTargetKind.Device, Device = "/dev/video0", Webcam = new RecorderWebcam("/dev/video0", 240) }),
            h => h.Contains("already the main picture", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => RecorderArguments.Build(linux with { Webcam = new RecorderWebcam("Cam", 240) }, "a.mkv"));
    }

    [Fact]
    public void AvfoundationListesiEkranlariKameraSaymaz()
    {
        const string cikti = "[AVFoundation indev @ 0x1] AVFoundation video devices:\n"
                             + "[AVFoundation indev @ 0x1] [0] FaceTime HD Camera\n"
                             + "[AVFoundation indev @ 0x1] [1] Elgato HD60 X\n"
                             + "[AVFoundation indev @ 0x1] [2] Capture screen 0\n"
                             + "[AVFoundation indev @ 0x1] [3] Capture screen 1\n"
                             + "[AVFoundation indev @ 0x1] AVFoundation audio devices:\n"
                             + "[AVFoundation indev @ 0x1] [0] Built-in Microphone\n";

        var liste = CaptureDevices.ParseAvFoundation(cikti);

        Assert.Equal(new[] { "FaceTime HD Camera", "Elgato HD60 X" }, liste.Video);
        Assert.Single(liste.Audio);
        Assert.True(CaptureDevices.IsAvFoundationScreen("Capture screen 12"));
        Assert.False(CaptureDevices.IsAvFoundationScreen("Capture screen"));
    }

    private static string GeciciKlasor()
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "kamera-diger-sistem", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static void Dugum(string sys, string ad, string? index)
    {
        var klasor = Path.Combine(sys, ad);
        Directory.CreateDirectory(klasor);
        if (index is not null) File.WriteAllText(Path.Combine(klasor, "index"), index + "\n");
    }

    [Fact]
    public void V4l2ListesiByIdAdlariniYalnizGoruntuDugumuyleVerir()
    {
        var kok = GeciciKlasor();
        try
        {
            var byId = Path.Combine(kok, "by-id");
            Directory.CreateDirectory(byId);
            foreach (var ad in new[] { "usb-Logitech_C920-video-index1", "usb-Logitech_C920-video-index0", "usb-Elgato_HD60-video-index0", "README" })
                File.WriteAllText(Path.Combine(byId, ad), string.Empty);

            var liste = CaptureDevices.ListV4l2(byId, Path.Combine(kok, "yok"));

            Assert.Equal(new[]
            {
                CaptureDevices.V4l2ByIdFolder + "/usb-Elgato_HD60-video-index0",
                CaptureDevices.V4l2ByIdFolder + "/usb-Logitech_C920-video-index0"
            }, liste);
            Assert.Empty(Hata(Istek(RecorderPlatform.Linux) with { Webcam = new RecorderWebcam(liste[0], 240) }));
        }
        finally
        {
            Directory.Delete(kok, recursive: true);
        }
    }

    [Fact]
    public void V4l2ListesiByIdYoksaSysDugumlerineDuser()
    {
        var kok = GeciciKlasor();
        try
        {
            var bosById = Path.Combine(kok, "by-id");
            Directory.CreateDirectory(bosById);
            var sys = Path.Combine(kok, "video4linux");
            Dugum(sys, "video10", "0");
            Dugum(sys, "video0", "0");
            Dugum(sys, "video1", "1");
            Dugum(sys, "video2", null);
            Dugum(sys, "video3", "bozuk");
            Dugum(sys, "videoX", "0");

            Assert.Equal(new[] { "/dev/video0", "/dev/video2", "/dev/video10" }, CaptureDevices.ListV4l2(bosById, sys));
            Assert.Equal(new[] { "/dev/video0", "/dev/video2", "/dev/video10" }, CaptureDevices.ListV4l2(Path.Combine(kok, "yok"), sys));
            Assert.Empty(CaptureDevices.ListV4l2(Path.Combine(kok, "yok"), Path.Combine(kok, "yok2")));
        }
        finally
        {
            Directory.Delete(kok, recursive: true);
        }
    }

    [Fact]
    public void KomutSatiriMacteKayitliAygitHedefiniAvfoundationlaKurar()
    {
        var klasor = GeciciKlasor();
        try
        {
            var dosya = Path.Combine(klasor, CliRecordSettings.FileName);
            var ayar = new RecorderSettings { OutputFolder = klasor, Target = RecorderTargetKind.Device, DeviceName = "Elgato HD60 X", ManualMode = true };
            File.WriteAllBytes(dosya, ayar.ToJson());
            var okunan = CliRecordSettings.Load(dosya);
            var cli = CliParser.Parse(new[] { "kaydet", "--sure", "5", "--cikti", Path.Combine(klasor, "a.mkv") }).Request!;
            var makine = new CliRecordMachine(RecorderPlatform.MacOs, Array.Empty<AudioCaptureDevice>(), new[] { "Elgato HD60 X" }, Array.Empty<string>(), 8);

            var plan = CliRecord.Build(cli, okunan, makine, new DateTime(2026, 10, 18, 12, 0, 0), out var hata, out var arguman);

            Assert.True(plan is not null, hata + " " + arguman);
            Assert.Equal("Elgato HD60 X", plan!.Request.Device);
            var args = RecorderArguments.Build(plan.Request with { PreviewPath = null }, plan.OutputPath);
            Assert.Equal("Elgato HD60 X:none", Girdiler(args)[0]);
            Assert.Equal("avfoundation", Deger(args, "-f"));
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }
}
