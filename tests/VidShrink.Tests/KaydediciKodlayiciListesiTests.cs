using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// later 65: kaydedicinin kodlayıcı listesi dönüştürme yolunun (K12) kodlayıcılarına yetişir ve
/// kare hızı kipi seçilebilir.
///
/// <para>VideoToolbox yalnız macOS'ta, VAAPI yalnız Linux'ta kabul edilir; argümanları
/// <see cref="CodecModel"/>'den gelir (VAAPI aygıtı girdiden önce, zincirin sonunda
/// <c>format=nv12,hwupload</c>, <c>-pix_fmt</c> yok). <c>av1_qsv</c> ve <c>av1_amf</c> Windows
/// kümesine girer. <c>-fps_mode</c> yalnız Sabit ya da Değişken seçilince yazılır.</para>
///
/// <para>Yalnız argüman ölçüsü: VideoToolbox, VAAPI ve AV1 donanımı bu makinede koşturulmadı.</para>
/// </summary>
public sealed class KaydediciKodlayiciListesiTests
{
    private static RecorderRequest Istek(RecorderPlatform platform, string kodek) => new()
    {
        Platform = platform,
        Target = RecorderTargetKind.Screen,
        Container = platform == RecorderPlatform.MacOs ? RecorderContainer.Mov : RecorderContainer.Mkv,
        VideoCodec = kodek,
        RateControl = RecorderRateControl.Bitrate,
        BitrateKbps = 6000
    };

    private static string Yol(RecorderPlatform platform) => platform == RecorderPlatform.MacOs ? "/tmp/k.mov" : "k.mkv";

    [Theory]
    [InlineData("h264_videotoolbox", RecorderPlatform.MacOs)]
    [InlineData("hevc_videotoolbox", RecorderPlatform.MacOs)]
    [InlineData("h264_vaapi", RecorderPlatform.Linux)]
    [InlineData("hevc_vaapi", RecorderPlatform.Linux)]
    [InlineData("av1_qsv", RecorderPlatform.Windows)]
    [InlineData("av1_amf", RecorderPlatform.Windows)]
    public void YeniKodlayiciKendiPlatformundaGecer(string kodek, RecorderPlatform platform)
    {
        Assert.Empty(RecorderArguments.Validate(Istek(platform, kodek), Yol(platform)));
        Assert.Equal(kodek, Deger(RecorderArguments.Build(Istek(platform, kodek), Yol(platform)), "-c:v"));
    }

    [Theory]
    [InlineData("h264_videotoolbox", RecorderPlatform.Windows)]
    [InlineData("hevc_videotoolbox", RecorderPlatform.Linux)]
    [InlineData("h264_vaapi", RecorderPlatform.Windows)]
    [InlineData("hevc_vaapi", RecorderPlatform.MacOs)]
    public void YanlisPlatformdaReddedilir(string kodek, RecorderPlatform platform)
        => Assert.Contains(RecorderArguments.Validate(Istek(platform, kodek), Yol(platform)), e => e.Contains(kodek, StringComparison.Ordinal));

    [Fact]
    public void VaapiAygitiGirdidenOnceYuklemeZincirinSonunda()
    {
        var args = RecorderArguments.Build(Istek(RecorderPlatform.Linux, "h264_vaapi"), "k.mkv");

        var aygit = Array.IndexOf(args.ToArray(), "-vaapi_device");
        Assert.True(aygit >= 0);
        Assert.Equal(CodecModel.VaapiDevice, args[aygit + 1]);
        Assert.True(aygit < Array.IndexOf(args.ToArray(), "-i"));
        Assert.EndsWith("format=nv12,hwupload", Deger(args, "-vf"));
        Assert.DoesNotContain("-pix_fmt", args);
        Assert.DoesNotContain("-preset", args);
        Assert.Equal("VBR", Deger(args, "-rc_mode"));
    }

    [Fact]
    public void OlcekliVaapiOlcektenSonraYukler()
    {
        var istek = Istek(RecorderPlatform.Linux, "hevc_vaapi") with
        {
            Target = RecorderTargetKind.Region,
            Region = new RecorderRegion(100, 50, 640, 480),
            Scale = new RecorderScale(320, 240)
        };
        var args = RecorderArguments.Build(istek, "k.mkv");

        Assert.Equal("scale=320:240,format=nv12,hwupload", Deger(args, "-vf"));
        Assert.Equal("640x480", Deger(args, "-video_size"));
    }

    [Fact]
    public void VideoToolboxOnAyarYazmazHevcOnBitMain10Yazar()
    {
        var h264 = RecorderArguments.Build(Istek(RecorderPlatform.MacOs, "h264_videotoolbox"), "/tmp/k.mov");
        Assert.DoesNotContain("-preset", h264);
        Assert.DoesNotContain("-vaapi_device", h264);
        Assert.Equal("yuv420p", Deger(h264, "-pix_fmt"));
        Assert.Null(Deger(h264, "-profile:v"));

        var hevc = RecorderArguments.Build(Istek(RecorderPlatform.MacOs, "hevc_videotoolbox"), "/tmp/k.mov");
        Assert.Equal("p010le", Deger(hevc, "-pix_fmt"));
        Assert.Equal("main10", Deger(hevc, "-profile:v"));
    }

    [Fact]
    public void YazilimKodlayicisiOnAyariKorur()
    {
        var args = RecorderArguments.Build(Istek(RecorderPlatform.Windows, "libx264"), "k.mkv");
        Assert.Equal(RecorderArguments.DefaultPreset, Deger(args, "-preset"));
        Assert.DoesNotContain("-vaapi_device", args);
    }

    [Fact]
    public void Av1QsvVeAmfPaketliBicimYazar()
    {
        Assert.Equal("nv12", Deger(RecorderArguments.Build(Istek(RecorderPlatform.Windows, "av1_qsv"), "k.mkv"), "-pix_fmt"));
        var amf10 = Istek(RecorderPlatform.Windows, "av1_amf") with { PixelFormat = "yuv420p10le" };
        Assert.Equal("p010le", Deger(RecorderArguments.Build(amf10, "k.mkv"), "-pix_fmt"));
        Assert.Equal("yuv420p", Deger(RecorderArguments.Build(Istek(RecorderPlatform.Windows, "av1_amf"), "k.mkv"), "-pix_fmt"));
        Assert.Equal(new[] { "yuv420p", "yuv420p10le" }, RecorderArguments.PixelFormatsFor("av1_qsv"));
        Assert.Equal(new[] { "yuv420p" }, RecorderArguments.PixelFormatsFor("h264_vaapi"));
    }

    [Theory]
    [InlineData("h264_videotoolbox", RecorderPlatform.MacOs)]
    [InlineData("hevc_vaapi", RecorderPlatform.Linux)]
    public void OlceksizKodlayicidaKaliteKoluReddedilir(string kodek, RecorderPlatform platform)
    {
        var kalite = Istek(platform, kodek) with { RateControl = RecorderRateControl.Quality, BitrateKbps = null };
        Assert.NotEmpty(RecorderArguments.Validate(kalite, Yol(platform)));

        var x264 = Istek(platform, "libx264") with { RateControl = RecorderRateControl.Quality, BitrateKbps = null };
        Assert.Empty(RecorderArguments.Validate(x264, Yol(platform)));
    }

    [Theory]
    [InlineData(RecorderFrameRateMode.Constant, "cfr")]
    [InlineData(RecorderFrameRateMode.Variable, "vfr")]
    public void KareHiziKipiCiktiYolundanOnceYazilir(RecorderFrameRateMode kip, string beklenen)
    {
        var args = RecorderArguments.Build(Istek(RecorderPlatform.Windows, "libx264") with { FrameRateMode = kip }, "k.mkv").ToArray();

        Assert.Equal(beklenen, Deger(args, "-fps_mode"));
        Assert.True(Array.IndexOf(args, "-fps_mode") < Array.IndexOf(args, "k.mkv"));
    }

    [Fact]
    public void OtomatikKareHiziKipiYazilmaz()
    {
        Assert.Equal(RecorderFrameRateMode.Auto, new RecorderRequest { Platform = RecorderPlatform.Windows, Target = RecorderTargetKind.Screen }.FrameRateMode);
        Assert.DoesNotContain("-fps_mode", RecorderArguments.Build(Istek(RecorderPlatform.Windows, "libx264"), "k.mkv"));
    }

    [Fact]
    public void KareHiziKutusuIstegeVeAyaraGider()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var once = new RecorderView(ayarYolu) { SkipAutoMeasure = true, CapturePlatform = RecorderPlatform.Windows };
            Elle(once);
            var kutu = once.FindControl<ComboBox>("CmbFrameRateMode")!;
            var ilk = (kutu.SelectedIndex, kutu.ItemCount);
            kutu.SelectedIndex = (int)RecorderFrameRateMode.Variable;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var istek = once.BuildRequest(applyAuto: false)!.FrameRateMode;
            var dosyada = DosyadakiDeger(ayarYolu, "frameRateMode");

            var sonra = new RecorderView(ayarYolu) { SkipAutoMeasure = true, CapturePlatform = RecorderPlatform.Windows };
            Elle(sonra);
            var geri = sonra.FindControl<ComboBox>("CmbFrameRateMode")!.SelectedIndex;
            var args = sonra.PrepareRecording() is { } hazir ? RecorderArguments.Build(hazir.Request, hazir.Path) : (IReadOnlyList<string>)Array.Empty<string>();
            return (ilk, istek, dosyada, geri, fps: Deger(args, "-fps_mode"));
        }));

        Assert.Equal((0, 3), olcu.ilk);
        Assert.Equal(RecorderFrameRateMode.Variable, olcu.istek);
        Assert.Equal("\"Variable\"", olcu.dosyada);
        Assert.Equal((int)RecorderFrameRateMode.Variable, olcu.geri);
        Assert.Equal("vfr", olcu.fps);
    }

    [Theory]
    [InlineData(RecorderPlatform.Windows, "av1_qsv", "h264_vaapi")]
    [InlineData(RecorderPlatform.MacOs, "h264_videotoolbox", "av1_amf")]
    [InlineData(RecorderPlatform.Linux, "hevc_vaapi", "hevc_videotoolbox")]
    public void ArayuzListesiPlatformaGoreSuzulur(RecorderPlatform platform, string var, string yok)
    {
        var liste = RecorderView.CodecsFor(platform);
        Assert.Contains(var, liste);
        Assert.DoesNotContain(yok, liste);
        Assert.Contains("libx264", liste);
    }
}
