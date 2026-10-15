using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// HDR ekranda kayit (EKA-P15): HDR cikista <c>ddagrab</c> 10 bit ister, kare BT.2020 matrisiyle
/// YUV'a iner, kodlayici 10 bit bicim ve HDR10 etiketleri yazar; SDR'ye cevirme secenegi ve HDR
/// olmayan cikis bugunku <c>bgra</c> yolunda kalir. Gercek HDR ekran bu makinede yok
/// (<c>docs/olcumler/kaydedici-hdr.md</c>); canli kol <c>ddagrab</c> yerine ayni zinciri
/// <c>testsrc2</c>'den gecirip <c>ffprobe</c> ile etiketleri okur.
/// </summary>
public sealed class KaydediciHdrTests
{
    private static readonly DdaOutput HdrCikis = new(0, 0, 0, 2560, 1440, Hdr: true);
    private static readonly DdaOutput SdrCikis = new(0, 0, 0, 2560, 1440);

    private static RecorderRequest Istek(DdaOutput cikis) => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Region = new RecorderRegion(0, 0, 640, 480),
        DdaOutputs = new[] { cikis },
        Capture = RecorderCapture.Ddagrab,
        Container = RecorderContainer.Mkv,
        Fps = 30
    };

    private static IReadOnlyList<string> Kur(RecorderRequest istek) => RecorderArguments.Build(istek, @"C:\kayit\a.mkv");

    private static string Kaynak(IReadOnlyList<string> args) => args[args.ToList().IndexOf("lavfi") + 2];

    private static string? Deger(IReadOnlyList<string> args, string anahtar)
    {
        var i = args.ToList().IndexOf(anahtar);
        return i >= 0 && i + 1 < args.Count ? args[i + 1] : null;
    }

    [Fact]
    public void HdrCikistaKayitOnBitHdr10Kalir()
    {
        var args = Kur(Istek(HdrCikis));

        Assert.Equal(
            "ddagrab=output_idx=0:framerate=30:draw_mouse=1:offset_x=0:offset_y=0:video_size=640x480:output_fmt=10bit,hwdownload,format=x2bgr10,"
            + "scale=out_color_matrix=bt2020:out_range=tv,format=yuv444p10le,"
            + "setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc:range=tv",
            Kaynak(args));
        Assert.Equal("yuv420p10le", Deger(args, "-pix_fmt"));
        Assert.Equal("bt2020", Deger(args, "-color_primaries"));
        Assert.Equal("smpte2084", Deger(args, "-color_trc"));
        Assert.Equal("bt2020nc", Deger(args, "-colorspace"));
        Assert.Equal("tv", Deger(args, "-color_range"));
        Assert.True(RecorderArguments.HdrCapture(Istek(HdrCikis)));
        Assert.False(RecorderArguments.HdrLost(Istek(HdrCikis)));
        Assert.Empty(RecorderArguments.Validate(Istek(HdrCikis), @"C:\kayit\a.mkv"));
    }

    [Fact]
    public void HdrOlmayanCikistaDavranisDegismez()
    {
        var args = Kur(Istek(SdrCikis));

        Assert.EndsWith(",hwdownload,format=bgra", Kaynak(args));
        Assert.DoesNotContain("output_fmt", Kaynak(args));
        Assert.Equal("yuv420p", Deger(args, "-pix_fmt"));
        Assert.DoesNotContain("-color_trc", args);
        Assert.DoesNotContain("-color_primaries", args);
        Assert.False(RecorderArguments.HdrCapture(Istek(SdrCikis)));
        Assert.False(RecorderArguments.HdrLost(Istek(SdrCikis)));
    }

    [Fact]
    public void SdryeCevirmeSecenegiHdrCikistaBugunkuYolu()
    {
        var istek = Istek(HdrCikis) with { HdrToSdr = true };
        var args = Kur(istek);

        Assert.EndsWith(",hwdownload,format=bgra", Kaynak(args));
        Assert.Equal("yuv420p", Deger(args, "-pix_fmt"));
        Assert.DoesNotContain("-color_trc", args);
        Assert.False(RecorderArguments.HdrLost(istek));
    }

    [Fact]
    public void GdigrabKolundaHdrArgumaniYazilmaz()
    {
        var istek = Istek(HdrCikis) with { Capture = RecorderCapture.Gdigrab };

        Assert.False(RecorderArguments.HdrCapture(istek));
        Assert.DoesNotContain("-color_trc", Kur(istek));
    }

    [Theory]
    [InlineData("libx264", "high", "yuv420p", "high10", "yuv420p10le")]
    [InlineData("libx264", "high", "yuv444p", "high444", "yuv444p10le")]
    [InlineData("libx265", "main", "yuv420p", "main10", "yuv420p10le")]
    [InlineData("libvpx-vp9", "0", "yuv420p", "2", "yuv420p10le")]
    [InlineData("hevc_nvenc", null, "yuv420p", null, "p010le")]
    [InlineData("h264_nvenc", null, "yuv444p", null, "p010le")]
    [InlineData("libsvtav1", null, "yuv420p", null, "yuv420p10le")]
    public void ProfilVeBicimOnBitKarsiliginaCekilir(string kodek, string? profil, string bicim, string? beklenenProfil, string beklenenBicim)
    {
        var istek = Istek(HdrCikis) with { VideoCodec = kodek, Profile = profil, PixelFormat = bicim };
        var args = Kur(istek);

        Assert.Equal(beklenenBicim, Deger(args, "-pix_fmt"));
        Assert.Equal(beklenenProfil, Deger(args, "-profile:v"));
        Assert.Equal("smpte2084", Deger(args, "-color_trc"));
    }

    [Fact]
    public void OnBitAlmayanKodlayicidaHdrKaybiSoylenir()
    {
        var istek = Istek(HdrCikis) with { VideoCodec = "h264_qsv" };

        Assert.False(RecorderArguments.HdrCapture(istek));
        Assert.True(RecorderArguments.HdrLost(istek));
        Assert.EndsWith(",hwdownload,format=bgra", Kaynak(Kur(istek)));
    }

    [Fact]
    public void GifKabindaHdrKaybiSoylenir()
    {
        var istek = Istek(HdrCikis) with { Container = RecorderContainer.Gif };

        Assert.False(RecorderArguments.HdrCapture(istek));
        Assert.True(RecorderArguments.HdrLost(istek));
    }

    [Fact]
    public void DxgiRenkUzayi12HdrDigerleriSdr()
    {
        Assert.True(DdaOutputs.IsHdrColorSpace(12));
        Assert.False(DdaOutputs.IsHdrColorSpace(0));
        Assert.False(DdaOutputs.IsHdrColorSpace(14));
    }

    [Fact]
    public void AyarDiskeGidipDoner()
    {
        var ayar = new RecorderSettings { HdrToSdr = true };
        var klasor = Path.Combine(AppContext.BaseDirectory, "hdr-ayar-" + Guid.NewGuid().ToString("N"));
        var dosya = Path.Combine(klasor, RecorderSettings.FileName);
        try
        {
            ayar.Save(dosya);
            Assert.True(RecorderSettings.Load(dosya).HdrToSdr);
            Assert.False(new RecorderSettings().HdrToSdr);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public void ZincirDosyayaHdr10EtiketiYazar()
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "hdr-zincir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "hdr.mkv");
        try
        {
            var args = Kur(Istek(HdrCikis) with { Preset = "ultrafast", MaxDuration = TimeSpan.FromSeconds(1) }).ToList();
            var kaynak = Kaynak(args);
            var zincir = kaynak[(kaynak.IndexOf(",hwdownload,format=x2bgr10,", StringComparison.Ordinal) + ",hwdownload,".Length)..];
            args[args.IndexOf(kaynak)] = "testsrc2=s=640x480:r=30:d=1," + zincir;
            args[^1] = dosya;
            args.InsertRange(args.Count - 1, new[] { "-threads", "2" });

            Calistir(ToolLocator.Ffmpeg, args.ToArray());
            var satirlar = Calistir(ToolLocator.Ffprobe, "-v", "error", "-select_streams", "v:0",
                "-show_entries", "stream=pix_fmt,color_space,color_transfer,color_primaries,color_range",
                "-of", "default=nw=1", dosya);

            Assert.Contains("pix_fmt=yuv420p10le", satirlar);
            Assert.Contains("color_transfer=smpte2084", satirlar);
            Assert.Contains("color_primaries=bt2020", satirlar);
            Assert.Contains("color_space=bt2020nc", satirlar);
            Assert.Contains("color_range=tv", satirlar);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    private static string Calistir(string arac, params string[] args)
    {
        var psi = new ProcessStartInfo(arac) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var cikti = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(60000))
        {
            surec.Kill(true);
            Assert.Fail(arac + " 60 sn icinde bitmedi.");
        }

        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
        return cikti.GetAwaiter().GetResult();
    }
}
