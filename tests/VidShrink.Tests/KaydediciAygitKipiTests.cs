using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// Later 69 kalanı: ana kaynak olan kameranın çözünürlük/kare hızı listesi ve kayıt sürerken anlık kare.
/// Liste ffmpeg 9'un bu makinede okunmuş <c>-list_options</c> çıktısıyla ölçülür; gerçek kamerayla
/// kayıt yalnız <see cref="KameraFactAttribute"/> altında, OBS Virtual Camera varken koşar.
/// </summary>
public sealed class KaydediciAygitKipiTests
{
    private const string Ffmpeg9 = """
        I2026-10-20 11:10:21.110811 (22600) [INFO] [VCAMDS] ffmpeg.exe
        [in#0 @ 0000026d5ff42680] DirectShow video device options (from video devices)
        [in#0 @ 0000026d5ff42680]  Pin "Camera (NVIDIA Broadcast)" (alternative pin name "1")
        I2026-10-20 11:10:21.113262 (22600) [INFO] [VCAMDS] Get number of capabilities
        [in#0 @ 0000026d5ff42680]   pixel_format=bgr0  min s=1280x720 fps=0.2 max s=1280x720 fps=60.0002
        [in#0 @ 0000026d5ff42680]   pixel_format=bgr24  min s=1280x720 fps=0.2 max s=1280x720 fps=60.0002
        [in#0 @ 0000026d5ff42680]   pixel_format=bgr0  min s=3840x2160 fps=0.2 max s=3840x2160 fps=60.0002
        [in#0 @ 0000026d5ff42680]   pixel_format=nv12  min s=1920x1080 fps=48.0001 max s=1920x1080 fps=48.0001
        [in#0 @ 0000026d5ff42680]   pixel_format=bgr0  min s=1440x1080 fps=0.2 max s=1440x1080 fps=60.0002
        [in#0 @ 0000026d5ff42680] DirectShow audio device options (from audio devices)
        [in#0 @ 0000026d5ff42680]   ch= 2, bits=16, rate= 44100
        [in#0 @ 0000026d5ff42680]   pixel_format=bgr0  min s=640x480 fps=5 max s=640x480 fps=15
        Error opening input file video=Camera (NVIDIA Broadcast).
        """;

    private const string EskiDshow = """
        [dshow @ 000001] DirectShow video device options (from video devices)
        [dshow @ 000001]  Pin "Capture" (alternative pin name "0")
        [dshow @ 000001]   vcodec=mjpeg  min s=1280x720 fps=5 max s=1280x720 fps=29.97
        [dshow @ 000001]   vcodec=mjpeg  min s=1280x720 fps=5 max s=1280x720 fps=30
        [dshow @ 000001]   pixel_format=yuyv422  min s=640x480 fps=30 max s=640x480 fps=30
        """;

    private static RecorderRequest Istek() => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Device,
        Device = "Kart A",
        Fps = 30,
        Preset = "ultrafast",
        Container = RecorderContainer.Mkv
    };

    private static string[] Hata(RecorderRequest r) => RecorderArguments.Validate(r, "a.mkv").ToArray();

    private static string Klasor(string ad)
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "aygit-kipi", ad, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static long DshowAraligi(string arguman)
        => (long)Math.Floor(10_000_000 / double.Parse(arguman, CultureInfo.InvariantCulture));

    [Fact]
    public void Ffmpeg9CiktisiYalnizGoruntuBolumundenBoyutBasinaKipCikarir()
    {
        var kipler = CaptureDevices.ParseDirectShowModes(Ffmpeg9);

        Assert.Equal(new[]
        {
            new CameraMode(3840, 2160, 60.0002), new CameraMode(3840, 2160, 30),
            new CameraMode(1920, 1080, 48.0001),
            new CameraMode(1440, 1080, 60.0002), new CameraMode(1440, 1080, 30),
            new CameraMode(1280, 720, 60.0002), new CameraMode(1280, 720, 30)
        }, kipler);
        Assert.DoesNotContain(kipler, k => k.Width == 640);
    }

    [Fact]
    public void EskiDshowOnekiVeKodekSatirlariOkunurOtuzZatenTepeyseEklenmez()
    {
        var kipler = CaptureDevices.ParseDirectShowModes(EskiDshow);

        Assert.Equal(new[] { new CameraMode(1280, 720, 30), new CameraMode(640, 480, 30) }, kipler);
        Assert.Empty(CaptureDevices.ParseDirectShowModes("Error opening input file video=Yok."));
    }

    [Fact]
    public void KareHiziArgumaniDshowunAralikDenetiminiTutturur()
    {
        foreach (var (fps, aralik) in new[] { (48.0001, 208333L), (29.97, 333667L), (60.0002, 166666L), (30.0, 333333L), (0.2, 50_000_000L) })
        {
            var kip = new CameraMode(1920, 1080, fps);
            Assert.Equal(aralik, kip.FrameInterval);
            Assert.Equal(aralik, DshowAraligi(kip.FrameRateArgument));
        }
    }

    [Fact]
    public void KipYaziYuvarlamasindanSagSaglamGeriOkunur()
    {
        var kip = new CameraMode(1920, 1080, 48.0001);

        Assert.Equal("1920x1080@48.0001", kip.ToString());
        Assert.Equal(kip, CameraMode.Parse(kip.ToString()));
        Assert.Equal("48", kip.Rate);
        Assert.Null(CameraMode.Parse("1920x1080"));
        Assert.Null(CameraMode.Parse("0x1080@30"));
        Assert.Null(CameraMode.Parse("-1x1080@30"));
        Assert.Null(CameraMode.Parse("1920x1080@0"));
    }

    [Fact]
    public void SecilenKipGirdidenOnceBoyutVeHizOlarakYazilir()
    {
        var kip = new CameraMode(1280, 720, 60.0002);
        var args = RecorderArguments.Build(Istek() with { DeviceMode = kip }, "a.mkv").ToList();
        var girdi = args.IndexOf("video=Kart A");

        Assert.Empty(Hata(Istek() with { DeviceMode = kip }));
        Assert.Equal(RecorderArguments.DirectShowVideoInput("Kart A", kip), args.Skip(2).Take(10).ToArray());
        Assert.Equal("1280x720", args[args.IndexOf("-video_size") + 1]);
        Assert.Equal(kip.FrameRateArgument, args[args.IndexOf("-framerate") + 1]);
        Assert.True(args.IndexOf("-video_size") < girdi && args.IndexOf("-framerate") < girdi);
        Assert.DoesNotContain("-video_size", RecorderArguments.Build(Istek(), "a.mkv"));

        var katman = RecorderArguments.Build(Istek() with { DeviceMode = kip, Webcam = new RecorderWebcam("Kam B", 240) }, "a.mkv").ToList();
        Assert.Single(katman, a => a == "-video_size");
        Assert.True(katman.IndexOf("-video_size") < katman.IndexOf("video=Kart A"));
    }

    [Fact]
    public void KipYalnizWindowsAygitHedefindeVeGecerliyseKabulEdilir()
    {
        var kip = new CameraMode(1280, 720, 30);

        Assert.Contains(Hata(Istek() with { Target = RecorderTargetKind.Screen, Device = null, DeviceMode = kip }), h => h.Contains("main picture", StringComparison.Ordinal));
        Assert.Contains(Hata(Istek() with { Platform = RecorderPlatform.MacOs, DeviceMode = kip }), h => h.Contains("only available on Windows", StringComparison.Ordinal));
        Assert.Contains(Hata(Istek() with { DeviceMode = new CameraMode(0, 720, 30) }), h => h.Contains("positive width", StringComparison.Ordinal));
        Assert.Contains(Hata(Istek() with { DeviceMode = new CameraMode(1280, 720, double.NaN) }), h => h.Contains("positive width", StringComparison.Ordinal));
    }

    [Fact]
    public void SonKareIkinciCiktiOlarakSaniyedeBirYenilenir()
    {
        var istek = Istek() with { FramePath = "kare.png", MaxDuration = TimeSpan.FromSeconds(12.5) };
        var args = RecorderArguments.Build(istek, "a.mkv").ToList();
        var kayit = args.IndexOf("a.mkv");
        var kare = args.LastIndexOf("kare.png");

        Assert.True(RecorderArguments.KeepsDeviceFrame(Istek()));
        Assert.False(RecorderArguments.KeepsDeviceFrame(Istek() with { MaxMegabytes = 50 }));
        Assert.False(RecorderArguments.KeepsDeviceFrame(Istek() with { Target = RecorderTargetKind.Screen }));
        Assert.Empty(Hata(istek));
        Assert.True(kayit >= 0 && kare == args.Count - 1 && kayit < kare);
        Assert.Equal(new[] { "-map", "0:v", "-vf", "fps=1", "-an", "-t", "12.5", "-f", "image2", "-update", "1", "kare.png" },
            args.Skip(kayit + 1).ToArray());
        Assert.Empty(RecorderArguments.FrameArgs(Istek()));
        Assert.DoesNotContain("kare.png", RecorderArguments.BuildSnapshot(istek, "anlik.png"));
    }

    [Fact]
    public void SonKareDosyasiYalnizSinirsizAygitKaydindaPngOlarakIstenir()
    {
        Assert.Contains(Hata(Istek() with { FramePath = "kare.jpg" }), h => h.Contains(".png", StringComparison.Ordinal));
        Assert.Contains(Hata(Istek() with { Target = RecorderTargetKind.Screen, Device = null, FramePath = "kare.png" }), h => h.Contains("last frame", StringComparison.Ordinal));
        Assert.Contains(Hata(Istek() with { FramePath = "kare.png", MaxMegabytes = 50 }), h => h.Contains("size limit", StringComparison.Ordinal));
    }

    private static readonly byte[] PngBasi = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static readonly byte[] PngSonu = { 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };

    private static byte[] Png(int govde) => PngBasi.Concat(Enumerable.Range(0, govde).Select(i => (byte)i)).Concat(PngSonu).ToArray();

    [Fact]
    public async Task AnlikKareYalnizTamYazilmisPngyiKopyalar()
    {
        var klasor = Klasor("kopya");
        try
        {
            var kare = Path.Combine(klasor, "kare.png");
            var hedef = Path.Combine(klasor, "alt", "anlik.png");
            var tam = Png(300);

            File.WriteAllBytes(kare, tam[..200]);
            var yarim = await DeviceFrame.CopyAsync(kare, hedef, attempts: 2, retryMs: 10);
            var yarimdaDosya = File.Exists(hedef);

            var yok = await DeviceFrame.CopyAsync(Path.Combine(klasor, "yok.png"), hedef, attempts: 2, retryMs: 10);

            File.WriteAllBytes(kare, tam);
            bool acikken;
            using (new FileStream(kare, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                acikken = await DeviceFrame.CopyAsync(kare, hedef, attempts: 1, retryMs: 10);

            Assert.False(yarim);
            Assert.False(yarimdaDosya);
            Assert.False(yok);
            Assert.True(acikken);
            Assert.Equal(tam, File.ReadAllBytes(hedef));
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    [Fact]
    public async Task AnlikKareYazimBitinceYenidenDeneyipAlir()
    {
        var klasor = Klasor("yeniden");
        try
        {
            var kare = Path.Combine(klasor, "kare.png");
            var hedef = Path.Combine(klasor, "anlik.png");
            var tam = Png(500);
            File.WriteAllBytes(kare, tam[..100]);

            var kopya = DeviceFrame.CopyAsync(kare, hedef, attempts: 20, retryMs: 50);
            await Task.Delay(150);
            File.WriteAllBytes(kare, tam);
            var sonuc = await kopya;

            Assert.True(sonuc);
            Assert.Equal(tam, File.ReadAllBytes(hedef));
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    private static string? Dosyada(string dosya, string anahtar)
        => File.Exists(dosya) ? JsonNode.Parse(File.ReadAllText(dosya))?[anahtar]?.ToJsonString() : null;

    [Fact]
    public void KipSecimiIstegeVeDosyayaGecerSonrakiGorunumdeKalir()
    {
        var klasor = Klasor("arayuz");
        try
        {
            var kare = Path.Combine(klasor, "kare.png");
            IReadOnlyList<CameraMode> kipler = new[] { new CameraMode(1920, 1080, 48.0001), new CameraMode(1280, 720, 60.0002), new CameraMode(1280, 720, 30) };
            var sorulan = new List<string>();
            Task<IReadOnlyList<CameraMode>> Kaynak(string aygit)
            {
                sorulan.Add(aygit);
                return Task.FromResult(aygit == "Kart B" ? kipler : (IReadOnlyList<CameraMode>)Array.Empty<CameraMode>());
            }

            var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
            {
                IReadOnlyList<string> iki = new[] { "Kam A", "Kart B" };
                var once = new RecorderView(ayarYolu)
                {
                    SkipAutoMeasure = true, CapturePlatform = RecorderPlatform.Windows, DeviceModeSource = Kaynak,
                    FrameLocation = () => kare, CameraSource = () => iki
                };
                Elle(once);
                var satir = Bul<StackPanel>(once, "RowDeviceMode");
                var ekrandaGorunur = satir.IsVisible;
                Bul<ComboBox>(once, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Device;
                Bul<ComboBox>(once, "CmbDevice").SelectedIndex = 0;
                var kipsizAygittaGorunur = satir.IsVisible;
                Bul<ComboBox>(once, "CmbDevice").SelectedIndex = 1;
                var kutu = Bul<ComboBox>(once, "CmbDeviceMode");
                var ogeler = kutu.ItemsSource!.Cast<object>().Select(o => o.ToString()).ToArray();
                var otomatik = once.PrepareRecording()!.Value.Request.DeviceMode;
                kutu.SelectedIndex = 3;
                var (istek, yol) = once.PrepareRecording()!.Value;
                var dosya = Dosyada(ayarYolu, "deviceMode");

                var sonra = new RecorderView(ayarYolu)
                {
                    SkipAutoMeasure = true, CapturePlatform = RecorderPlatform.Windows, DeviceModeSource = Kaynak,
                    FrameLocation = () => kare, CameraSource = () => iki
                };
                var sonraki = sonra.PrepareRecording()!.Value.Request;
                var sonrakiSecim = Bul<ComboBox>(sonra, "CmbDeviceMode").SelectedIndex;
                Bul<ComboBox>(sonra, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Screen;
                var ekrandaSatir = Bul<StackPanel>(sonra, "RowDeviceMode").IsVisible;
                var ekranIstegi = sonra.PrepareRecording()!.Value.Request;
                return (ekrandaGorunur, kipsizAygittaGorunur, satirSonra: satir.IsVisible, ogeler, otomatik, istek, yol, dosya, sonraki, sonrakiSecim, ekrandaSatir, ekranIstegi);
            }));

            Assert.False(olcu.ekrandaGorunur);
            Assert.False(olcu.kipsizAygittaGorunur);
            Assert.True(olcu.satirSonra);
            Assert.Contains("Kart B", sorulan);
            Assert.Equal(4, olcu.ogeler.Length);
            Assert.Contains("1920", olcu.ogeler[1]);
            Assert.Contains("48", olcu.ogeler[1]);
            Assert.Contains("720", olcu.ogeler[3]);
            Assert.Contains("30", olcu.ogeler[3]);
            Assert.Null(olcu.otomatik);
            Assert.Equal(new CameraMode(1280, 720, 30), olcu.istek.DeviceMode);
            Assert.Equal(kare, olcu.istek.FramePath);
            Assert.Contains("-video_size", RecorderArguments.Build(olcu.istek with { PreviewPath = null }, olcu.yol));
            Assert.Equal("\"1280x720@30\"", olcu.dosya);
            Assert.Equal(new CameraMode(1280, 720, 30), olcu.sonraki.DeviceMode);
            Assert.Equal(3, olcu.sonrakiSecim);
            Assert.False(olcu.ekrandaSatir);
            Assert.Null(olcu.ekranIstegi.DeviceMode);
            Assert.Null(olcu.ekranIstegi.FramePath);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    [Fact]
    public void KomutSatiriKayitliKipiIstegeYazar()
    {
        var klasor = Klasor("cli");
        try
        {
            var dosya = Path.Combine(klasor, CliRecordSettings.FileName);
            var ayar = new RecorderSettings
            {
                OutputFolder = klasor, Target = RecorderTargetKind.Device, DeviceName = "Kart B", DeviceMode = "1280x720@29.97", ManualMode = true
            };
            File.WriteAllBytes(dosya, ayar.ToJson());
            var okunan = CliRecordSettings.Load(dosya);
            var cli = CliParser.Parse(new[] { "kaydet", "--sure", "5", "--cikti", Path.Combine(klasor, "a.mkv") }).Request!;
            var zaman = new DateTime(2026, 10, 20, 12, 0, 0);

            var windows = CliRecord.Build(cli, okunan, new CliRecordMachine(RecorderPlatform.Windows, Array.Empty<AudioCaptureDevice>(), new[] { "Kart B" }, Array.Empty<string>(), 8), zaman, out var hata, out var arguman);
            var mac = CliRecord.Build(cli, okunan, new CliRecordMachine(RecorderPlatform.MacOs, Array.Empty<AudioCaptureDevice>(), new[] { "Kart B" }, Array.Empty<string>(), 8), zaman, out _, out _);

            Assert.True(windows is not null, hata + " " + arguman);
            Assert.Equal(new CameraMode(1280, 720, 29.97), okunan.DeviceMode);
            Assert.Equal(new CameraMode(1280, 720, 29.97), windows!.Request.DeviceMode);
            Assert.Equal("1280x720", RecorderArguments.Build(windows.Request with { PreviewPath = null }, windows.OutputPath)
                .SkipWhile(a => a != "-video_size").Skip(1).First());
            Assert.NotNull(mac);
            Assert.Null(mac!.Request.DeviceMode);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    [KameraFact]
    public async Task GercekKameraSecilenKiptenKaydederKayitSurerkenAnlikKareVerir()
    {
        var kipler = CaptureDevices.ModesOf(KameraFactAttribute.Aygit);
        Assert.NotEmpty(kipler);
        var klasor = Klasor("canli");
        try
        {
            var kip = kipler[^1];
            var cikti = Path.Combine(klasor, "kayit.mkv");
            var kare = Path.Combine(klasor, "kare.png");
            var anlik = Path.Combine(klasor, "anlik.png");
            var istek = Istek() with { Device = KameraFactAttribute.Aygit, DeviceMode = kip, FramePath = kare };

            var oturum = await RecorderSession.StartAsync(istek, cikti);
            await Task.Delay(3000);
            var alindi = await oturum.SnapshotAsync(anlik);
            var sonuc = await oturum.StopAsync();
            var (kod, metin) = KayitKanit.Ffprobe(cikti, "aygit-kipi-canli.ffprobe.txt");

            Assert.True(sonuc.Ok, sonuc.StandardError);
            Assert.True(alindi);
            Assert.True(DeviceFrame.IsCompletePng(File.ReadAllBytes(anlik)));
            Assert.Equal(0, kod);
            Assert.Contains("width=" + kip.Width, metin);
            Assert.Contains("height=" + kip.Height, metin);
            KayitKanit.Kapat("aygit-kipi-canli.ffprobe.txt");
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }
}
