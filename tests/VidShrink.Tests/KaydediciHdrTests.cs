using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            + "limiter=min=64:max=940:planes=1,limiter=min=64:max=960:planes=6,"
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
        Assert.Equal("scale=out_color_matrix=bt709:out_range=tv,format=yuv420p,setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709:range=tv", Deger(args, "-vf"));
        Assert.Equal("bt709", Deger(args, "-color_primaries"));
        Assert.Equal("bt709", Deger(args, "-color_trc"));
        Assert.Equal("bt709", Deger(args, "-colorspace"));
        Assert.Equal("tv", Deger(args, "-color_range"));
        Assert.False(RecorderArguments.HdrCapture(Istek(SdrCikis)));
        Assert.False(RecorderArguments.HdrLost(Istek(SdrCikis)));
    }

    [Fact]
    public void SdryeCevirmeSecenegiHdrCikistaBugunkuYolu()
    {
        var istek = Istek(HdrCikis) with { HdrToSdr = true };
        var args = Kur(istek);

        Assert.Equal(
            "ddagrab=output_idx=0:framerate=30:draw_mouse=1:offset_x=0:offset_y=0:video_size=640x480:output_fmt=10bit,hwdownload,format=x2bgr10,"
            + "zscale=tin=smpte2084:pin=bt2020:rin=full:t=linear:p=bt709:npl=203,format=gbrpf32le,"
            + "tonemap=mobius:param=0.99:desat=0:peak=4.9261,"
            + "zscale=tin=linear:pin=bt709:rin=full:t=iec61966-2-1:p=bt709:r=full,format=gbrp,format=bgra",
            Kaynak(args));
        Assert.Equal("scale=out_color_matrix=bt709:out_range=tv,format=yuv420p,setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709:range=tv", Deger(args, "-vf"));
        Assert.Equal("yuv420p", Deger(args, "-pix_fmt"));
        Assert.Equal("bt709", Deger(args, "-color_trc"));
        Assert.Equal("bt709", Deger(args, "-color_primaries"));
        Assert.Equal("tv", Deger(args, "-color_range"));
        Assert.True(RecorderArguments.HdrTonemapCapture(istek));
        Assert.False(RecorderArguments.HdrTonemapMissing(istek));
        Assert.False(RecorderArguments.HdrCapture(istek));
        Assert.False(RecorderArguments.HdrLost(istek));
    }

    [Fact]
    public void ZscaleYoksaSdrCevirmeEskiYolaDuserVeSoylenir()
    {
        var istek = Istek(HdrCikis) with { HdrToSdr = true, CanTonemap = false };
        var args = Kur(istek);

        Assert.EndsWith(",hwdownload,format=bgra", Kaynak(args));
        Assert.DoesNotContain("zscale", Kaynak(args));
        Assert.Equal("bt709", Deger(args, "-color_trc"));
        Assert.True(RecorderArguments.HdrTonemapMissing(istek));
        Assert.False(RecorderArguments.HdrTonemapCapture(istek));
    }

    [Fact]
    public void SdrCikistaTonEslemeKoluAcilmaz()
    {
        var istek = Istek(SdrCikis) with { HdrToSdr = true };

        Assert.False(RecorderArguments.HdrTonemapCapture(istek));
        Assert.False(RecorderArguments.HdrTonemapMissing(istek));
        Assert.EndsWith(",hwdownload,format=bgra", Kaynak(Kur(istek)));
    }

    [Fact]
    public void GifYakalamasiTonEslemeAlirSdrEtiketiAlmaz()
    {
        var gif = RecorderArguments.CaptureRequest(Istek(HdrCikis) with { HdrToSdr = true, CanTonemap = true, Container = RecorderContainer.Gif });
        var args = Kur(gif);

        Assert.True(gif.GifCapture);
        Assert.True(RecorderArguments.HdrTonemapCapture(gif));
        Assert.Contains("output_fmt=10bit,hwdownload,format=x2bgr10,zscale=", Kaynak(args));
        Assert.EndsWith(",format=gbrp,format=bgra", Kaynak(args));
        Assert.DoesNotContain("-color_trc", args);
        Assert.DoesNotContain("-vf", args);
    }

    [Fact]
    public void GdigrabKolundaSdrEtiketiVeDonusumYazilir()
    {
        var istek = Istek(SdrCikis) with { Capture = RecorderCapture.Gdigrab };
        var args = Kur(istek);

        Assert.Equal("gdigrab", Deger(args, "-f"));
        Assert.Equal("scale=out_color_matrix=bt709:out_range=tv,format=yuv420p,setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709:range=tv", Deger(args, "-vf"));
        Assert.Equal("bt709", Deger(args, "-colorspace"));
        Assert.Equal("tv", Deger(args, "-color_range"));
    }

    [Theory]
    [InlineData("bt470bg", "pc", "out_color_matrix=bt470:out_range=pc,format=yuv420p,setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt470bg:range=pc", "bt470bg", "pc")]
    [InlineData("smpte170m", null, "out_color_matrix=smpte170m:out_range=tv,format=yuv420p,setparams=color_primaries=bt709:color_trc=bt709:colorspace=smpte170m:range=tv", "smpte170m", "tv")]
    [InlineData(null, "pc", "out_color_matrix=bt709:out_range=pc,format=yuv420p,setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709:range=pc", "bt709", "pc")]
    [InlineData("bt2020nc", "tv", "out_color_matrix=bt2020:out_range=tv,format=yuv420p,setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt2020nc:range=tv", "bt2020nc", "tv")]
    public void KullaniciRenkSecimiDonusumuVeEtiketiBelirler(string? uzay, string? aralik, string beklenenSuzgec, string beklenenUzay, string beklenenAralik)
    {
        var istek = Istek(SdrCikis) with { ColorSpace = uzay, ColorRange = aralik };
        var args = Kur(istek);

        Assert.Equal("scale=" + beklenenSuzgec, Deger(args, "-vf"));
        Assert.Equal(beklenenUzay, Deger(args, "-colorspace"));
        Assert.Equal(beklenenAralik, Deger(args, "-color_range"));
        Assert.Equal("bt709", Deger(args, "-color_primaries"));
        Assert.Equal(1, args.Count(a => a == "-colorspace"));
    }


    [Fact]
    public void GdigrabaDusenHdrEkranSoylenir()
    {
        var hedefHdr = Istek(HdrCikis) with { Capture = RecorderCapture.Gdigrab };
        var hedefSdr = Istek(SdrCikis) with { Capture = RecorderCapture.Gdigrab };
        var ikiCikis = Istek(SdrCikis) with
        {
            Capture = RecorderCapture.Gdigrab,
            Region = new RecorderRegion(2400, 0, 640, 480),
            DdaOutputs = new[] { SdrCikis, new DdaOutput(1, 2560, 0, 1920, 1080, Hdr: true) }
        };
        var hedefBilinmezSdr = ikiCikis with { DdaOutputs = new[] { SdrCikis, new DdaOutput(1, 2560, 0, 1920, 1080) } };

        Assert.True(RecorderArguments.HdrOnGdigrab(hedefHdr));
        Assert.False(RecorderArguments.HdrOnGdigrab(hedefSdr));
        Assert.Null(RecorderArguments.DdagrabTarget(ikiCikis));
        Assert.True(RecorderArguments.HdrOnGdigrab(ikiCikis));
        Assert.False(RecorderArguments.HdrOnGdigrab(hedefBilinmezSdr));
        Assert.False(RecorderArguments.HdrOnGdigrab(Istek(HdrCikis)));
    }

    [Theory]
    [InlineData("libx264", "hevc_nvenc,hevc_amf,hevc_qsv,libx265", "hevc_nvenc")]
    [InlineData("h264_amf", "hevc_nvenc,hevc_amf,libx265", "hevc_amf")]
    [InlineData("h264_qsv", "hevc_qsv,libx265", "hevc_qsv")]
    [InlineData("h264_nvenc", "libx265", "libx265")]
    [InlineData("libx264", "", "")]
    [InlineData("libx265", "hevc_nvenc", null)]
    [InlineData("libsvtav1", "libx265", null)]
    public void HdrKorunurkenH264YerineHevcSecilir(string kodek, string calisan, string? beklenen)
    {
        var acilan = calisan.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        Assert.Equal(beklenen, RecorderArguments.HdrHevcCodec(kodek, acilan.Contains));
    }

    [Fact]
    public void HevceGecisProfiliSilerVeHdr10Kalir()
    {
        var istek = Istek(HdrCikis) with { VideoCodec = "libx264", Profile = "high", Tune = "film" };

        var yeni = RecorderArguments.WithHdrCodec(istek, k => k == "libx265", out var sonuc);
        var args = Kur(yeni);

        Assert.Equal(HdrCodecOutcome.SwitchedToHevc, sonuc);
        Assert.Equal("libx265", Deger(args, "-c:v"));
        Assert.Null(Deger(args, "-profile:v"));
        Assert.Null(Deger(args, "-tune"));
        Assert.Equal("yuv420p10le", Deger(args, "-pix_fmt"));
        Assert.Equal("smpte2084", Deger(args, "-color_trc"));
    }

    [Fact]
    public void HevcYoksaKayitSdryeCevrilir()
    {
        var yeni = RecorderArguments.WithHdrCodec(Istek(HdrCikis), _ => false, out var sonuc);

        Assert.Equal(HdrCodecOutcome.NoHevc, sonuc);
        Assert.True(yeni.HdrToSdr);
        Assert.True(RecorderArguments.HdrTonemapCapture(yeni));
        Assert.Equal("libx264", yeni.VideoCodec);
    }

    [Fact]
    public void HdrKorunmuyorsaKodlayiciDegismez()
    {
        var sdr = RecorderArguments.WithHdrCodec(Istek(SdrCikis), _ => true, out var sonuc1);
        var cevir = RecorderArguments.WithHdrCodec(Istek(HdrCikis) with { HdrToSdr = true }, _ => true, out var sonuc2);

        Assert.Equal(HdrCodecOutcome.Unchanged, sonuc1);
        Assert.Equal("libx264", sdr.VideoCodec);
        Assert.Equal(HdrCodecOutcome.Unchanged, sonuc2);
        Assert.Equal("libx264", cevir.VideoCodec);
    }

    [Fact]
    public void GdigrabKolundaHdrArgumaniYazilmaz()
    {
        var istek = Istek(HdrCikis) with { Capture = RecorderCapture.Gdigrab };

        Assert.False(RecorderArguments.HdrCapture(istek));
        Assert.DoesNotContain("smpte2084", Kur(istek));
        Assert.Equal("bt709", Deger(Kur(istek), "-color_trc"));
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

    private static RecorderRequest Pencere(RecorderRegion pencere, params DdaOutput[] cikislar) => Istek(HdrCikis) with
    {
        Target = RecorderTargetKind.Window,
        WindowTitle = "Oyun",
        Region = null,
        WindowRegion = pencere,
        DdaOutputs = cikislar,
        HdrToSdr = true,
        CanTonemap = true
    };

    [Fact]
    public void IkiEkranaTasanPencereHdrEkranaKirpilirVeTonEslenir()
    {
        var sag = new DdaOutput(1, 2560, 0, 1920, 1080);
        var istek = Pencere(new RecorderRegion(1800, 100, 1000, 700), HdrCikis, sag);

        var hedef = RecorderArguments.DdagrabTarget(istek);

        Assert.Equal(0, hedef!.Value.Output.Index);
        Assert.Equal(new RecorderRegion(1800, 100, 760, 700), hedef.Value.Local);
        Assert.True(RecorderArguments.WindowClipped(istek));
        Assert.Equal(DdagrabFallback.None, RecorderArguments.DdagrabBlocker(istek));
        Assert.True(RecorderArguments.HdrTonemapCapture(istek));
        Assert.Contains(":offset_x=1800:offset_y=100:video_size=760x700:output_fmt=10bit", Kaynak(Kur(istek with { Capture = RecorderCapture.Ddagrab })));
    }

    [Fact]
    public void EkranDisinaTasanPencereGorunenKismaKirpilir()
    {
        var istek = Pencere(new RecorderRegion(-9, -9, 2578, 1458), SdrCikis);

        Assert.Equal(new RecorderRegion(0, 0, 2560, 1440), RecorderArguments.DdagrabTarget(istek)!.Value.Local);
        Assert.True(RecorderArguments.WindowClipped(istek));
    }

    [Fact]
    public void HdrsizIkiEkranaTasanPencereKirpilmazGdigrabdaButunKalir()
    {
        var sag = new DdaOutput(1, 2560, 0, 1920, 1080);
        var istek = Pencere(new RecorderRegion(1800, 100, 1000, 700), SdrCikis, sag);

        Assert.Null(RecorderArguments.DdagrabTarget(istek));
        Assert.False(RecorderArguments.WindowClipped(istek));
        Assert.Equal(DdagrabFallback.OutsideOneOutput, RecorderArguments.DdagrabBlocker(istek));
    }

    [Fact]
    public void IkiEkranaTasanBolgeKirpilmaz()
    {
        var sag = new DdaOutput(1, 2560, 0, 1920, 1080);
        var istek = Istek(HdrCikis) with { Region = new RecorderRegion(1800, 100, 1000, 700), DdaOutputs = new[] { HdrCikis, sag } };

        Assert.Null(RecorderArguments.DdagrabTarget(istek));
        Assert.True(RecorderArguments.HdrOnGdigrab(istek with { Capture = RecorderCapture.Gdigrab }));
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
            Assert.True(new RecorderSettings().HdrToSdr);
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

    private static readonly DdaDisplayInfo Ekran = new(0.68, 0.32, 0.265, 0.69, 0.15, 0.06, 0.3127, 0.329, 0.05, 1015.3, 600);

    private const string EkranMd = "G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10153000,500)";

    /// <summary>HDR10 kayitta ekranin DXGI bilgisi kodlayicinin kendi anahtariyla statik veri olur.</summary>
    [Theory]
    [InlineData("libx265", "-x265-params", "hdr10=1:master-display=" + EkranMd + ":max-cll=1015,600")]
    [InlineData("libx264", "-x264-params", "mastering-display=" + EkranMd + ":cll=1015,600")]
    [InlineData("libsvtav1", "-svtav1-params", "mastering-display=G(0.265,0.69)B(0.15,0.06)R(0.68,0.32)WP(0.3127,0.329)L(1015.3,0.05):content-light=1015,600")]
    public void EkranParlakligiStatikVeriOlarakYazilir(string kodlayici, string anahtar, string beklenen)
    {
        var istek = Istek(HdrCikis with { Display = Ekran }) with { VideoCodec = kodlayici };

        var args = Kur(istek);

        Assert.Equal(beklenen, Deger(args, anahtar));
        Assert.Equal(1, args.Count(a => a == anahtar));
        Assert.Equal(new HdrStaticMetadata(EkranMd, "1015,600"), RecorderArguments.HdrStatic(istek));
    }

    /// <summary>
    /// Bos renk koordinati BT.2020 D65'e, tam kare parlakligi tepe degerine duser; ekran bilgisi
    /// yoksa, tepe parlaklik bildirilmemisse, kayit SDR'ye cevriliyorsa ya da kodlayici donanimsa
    /// anahtar yazilmaz.
    /// </summary>
    [Fact]
    public void EksikEkranBilgisiStatikVeriyiDusurur()
    {
        var bos = Ekran with { RedX = 0, RedY = 0, GreenX = 0, GreenY = 0, BlueX = 0, BlueY = 0, WhiteX = 0, WhiteY = 0, MaxFullFrameNits = 0 };
        Assert.Equal(new HdrStaticMetadata("G(8500,39850)B(6550,2300)R(35400,14600)WP(15635,16450)L(10153000,500)", "1015,1015"),
            RecorderArguments.HdrStatic(Istek(HdrCikis with { Display = bos })));

        Assert.Null(RecorderArguments.HdrStatic(Istek(HdrCikis)));
        Assert.Null(RecorderArguments.HdrStatic(Istek(HdrCikis with { Display = Ekran with { MaxNits = 0 } })));
        Assert.Null(RecorderArguments.HdrStatic(Istek(HdrCikis with { Display = Ekran }) with { HdrToSdr = true }));
        Assert.DoesNotContain("-x264-params", Kur(Istek(HdrCikis with { Display = Ekran }) with { HdrToSdr = true }));
        Assert.DoesNotContain("-x264-params", Kur(Istek(HdrCikis)));
        var nvenc = Kur(Istek(HdrCikis with { Display = Ekran }) with { VideoCodec = "hevc_nvenc" });
        Assert.DoesNotContain(nvenc, a => a.EndsWith("-params", StringComparison.Ordinal));
    }

    [Fact]
    public void TaniMetniEkranParlakliginiVeStatikVeriyiSoyler()
    {
        var istek = Istek(HdrCikis with { Display = Ekran });

        var metin = KayitTanisi.Metin(istek, Kur(istek), "1.2.24", DateTimeOffset.UnixEpoch);

        Assert.Contains("  [0] 2560x1440 at 0,0 hdr=yes nits=0.05/1015.3/600", metin);
        Assert.Contains("hdr-static: " + EkranMd + " cll=1015,600", metin);
    }

    /// <summary>
    /// Canli: ayni zincir ve kodlayici argumanlariyla kodlanan dosyanin ilk karesinde ffprobe
    /// mastering ve icerik parlakligi yan verisini okur. Yan veri yoksa satirlar hic gelmez.
    /// </summary>
    [FfmpegTheory]
    [InlineData("libx265")]
    [InlineData("libx264")]
    [InlineData("libsvtav1")]
    public void StatikVeriDosyaninKaresindeGorunur(string kodlayici)
    {
        if (!EncoderCapabilities.Instance.WorksAsEncoder(kodlayici)) return;
        var klasor = Path.Combine(AppContext.BaseDirectory, "hdr-statik-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "hdr.mkv");
        try
        {
            var istek = Istek(HdrCikis with { Display = Ekran }) with
            {
                VideoCodec = kodlayici,
                Preset = kodlayici == "libsvtav1" ? "12" : "ultrafast",
                MaxDuration = TimeSpan.FromSeconds(1)
            };
            var args = Kur(istek).ToList();
            var kaynak = Kaynak(args);
            var zincir = kaynak[(kaynak.IndexOf(",hwdownload,format=x2bgr10,", StringComparison.Ordinal) + ",hwdownload,".Length)..];
            args[args.IndexOf(kaynak)] = "testsrc2=s=320x240:r=30:d=0.3," + zincir;
            args[^1] = dosya;
            args.InsertRange(args.Count - 1, new[] { "-threads", "2" });

            Calistir(ToolLocator.Ffmpeg, args.ToArray());
            var yan = Calistir(ToolLocator.Ffprobe, "-v", "error", "-select_streams", "v:0", "-read_intervals", "%+#1",
                "-show_frames", "-show_entries", "frame=side_data_list", "-of", "default=nw=1", dosya);

            Assert.Contains("side_data_type=Mastering display metadata", yan);
            Assert.Contains("max_content=1015", yan);
            Assert.Contains("max_average=600", yan);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    /// <summary>
    /// Canli: TV araliginda etiketli super beyaz (Y 1010, renk 1000 ve 30) zincirden 940, 960 ve
    /// 64 olarak cikar; ayni kare <c>limiter</c>'siz zincirde 1010 kalir, olcu farki ayirt eder.
    /// </summary>
    [FfmpegFact]
    public void HdrZinciriSuperBeyaziTvAraligindaKirpar()
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "hdr-kirp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            const string kaynak = "color=c=white:s=64x64:d=0.1,format=yuv444p10le,lutyuv=y=1010:u=1000:v=30,"
                                  + "setparams=colorspace=bt2020nc:range=tv:color_primaries=bt2020:color_trc=smpte2084,";
            ushort[] Ilk(string ad, string zincir)
            {
                var ham = Path.Combine(klasor, ad + ".raw");
                Calistir(ToolLocator.Ffmpeg, "-v", "error", "-f", "lavfi", "-i", kaynak + zincir, "-frames:v", "1",
                    "-f", "rawvideo", "-pix_fmt", "yuv444p10le", "-threads", "2", ham);
                var b = File.ReadAllBytes(ham);
                return new[] { 0, 1, 2 }.Select(k => BitConverter.ToUInt16(b, k * 64 * 64 * 2)).ToArray();
            }

            var kirpik = Ilk("kirpik", RecorderArguments.HdrFrameChain);
            var ham = Ilk("ham", RecorderArguments.HdrFrameChain.Replace("limiter=min=64:max=940:planes=1,limiter=min=64:max=960:planes=6,", "", StringComparison.Ordinal));

            Assert.Equal(new ushort[] { 940, 960, 64 }, kirpik);
            Assert.Equal(new ushort[] { 1010, 1000, 30 }, ham);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Theory]
    [InlineData("{\"hdrToSdr\": false}", true)]
    [InlineData("{}", true)]
    [InlineData("{\"hdrToSdr\": false, \"hdrToSdrDefaultOn\": true}", false)]
    [InlineData("{\"hdrToSdr\": true, \"hdrToSdrDefaultOn\": true}", true)]
    public void EskiDosyaninKapaliDegeriBirKezAcilir(string icerik, bool beklenen)
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "hdr-goc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, RecorderSettings.FileName);
        try
        {
            File.WriteAllText(dosya, icerik);
            Assert.Equal(beklenen, RecorderSettings.Load(dosya).HdrToSdr);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void GoctenSonraKullanicininKapatmasiKorunur()
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "hdr-goc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, RecorderSettings.FileName);
        try
        {
            File.WriteAllText(dosya, "{\"hdrToSdr\": false}");
            var gocmus = RecorderSettings.Load(dosya);
            Assert.True(gocmus.HdrToSdr);
            gocmus.HdrToSdr = false;
            gocmus.Save(dosya);

            Assert.False(RecorderSettings.Load(dosya).HdrToSdr);
            Assert.Contains("\"hdrToSdrDefaultOn\"", File.ReadAllText(dosya));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void TaniMetniYakalamaKararlariniVeArgumanlariTasir()
    {
        var istek = Istek(HdrCikis) with { CaptureFallback = DdagrabFallback.None };
        var args = Kur(istek);

        var metin = KayitTanisi.Metin(istek, args, "1.2.24", new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains("capture: ddagrab", metin);
        Assert.Contains("fallback: None", metin);
        Assert.Contains("  [0] 2560x1440 at 0,0 hdr=yes", metin);
        Assert.Contains("target-output: 0", metin);
        Assert.Contains("hdr-capture: yes", metin);
        Assert.Contains("hdr-to-sdr: no", metin);
        Assert.Contains("hdr-lost: no", metin);
        Assert.Contains("encoder: libx264", metin);
        Assert.Contains("output_fmt=10bit", metin);
        Assert.Contains(" a.mkv", metin);
        Assert.DoesNotContain(@"C:\kayit", metin);
        Assert.Contains("app: 1.2.24", metin);
    }

    [Fact]
    public void TaniMetniDususSebebiniVeHataySoyler()
    {
        var istek = Istek(HdrCikis) with { Capture = RecorderCapture.Gdigrab, CaptureFallback = DdagrabFallback.Unavailable };

        var metin = KayitTanisi.Metin(istek, null, "1.2.24", DateTimeOffset.UnixEpoch, @"ffmpeg acilmadi: C:\Users\ad\x.mkv");

        Assert.Contains("capture: gdigrab", metin);
        Assert.Contains("fallback: Unavailable", metin);
        Assert.Contains("hdr-on-gdigrab: yes", metin);
        Assert.Contains("args: -", metin);
        Assert.Contains("error: ffmpeg acilmadi: x.mkv", metin);
        Assert.DoesNotContain(@"C:\Users", metin);
    }

    [Fact]
    public void TaniDosyasiYalnizSonKaydiTutar()
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "hdr-tani-" + Guid.NewGuid().ToString("N"));
        try
        {
            var ilk = KayitTanisi.Yaz(klasor, "birinci");
            var ikinci = KayitTanisi.Yaz(klasor, "ikinci");

            Assert.Equal(Path.Combine(klasor, KayitTanisi.DosyaAdi), ilk);
            Assert.Equal(ilk, ikinci);
            Assert.Equal("ikinci", File.ReadAllText(ikinci!));
            Assert.Single(Directory.GetFiles(klasor));
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void GorunumTaniyiAyarDosyasininYaninaYazar()
    {
        var olcu = KaydediciAyarTests.AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
            view.WriteDiagnostic(Istek(HdrCikis), Kur(Istek(HdrCikis)), null);
            var yol = view.LastDiagnosticPath;
            return (yol, beklenen: Path.Combine(Path.GetDirectoryName(ayarYolu)!, Gunluk.KlasorAdi, KayitTanisi.DosyaAdi),
                icerik: yol is null ? string.Empty : File.ReadAllText(yol));
        }));

        Assert.Equal(olcu.beklenen, olcu.yol);
        Assert.Contains("hdr-capture: yes", olcu.icerik);
    }

    private static string Metin(string anahtar)
        => VidShrink.App.LanguageCatalog.Display(VidShrink.App.Localization.Strings.Get(anahtar));

    private static (RecorderRequest secilen, string satir) Sec(
        RecorderRequest istek, bool ddagrab, Func<string, bool>? hevc = null, bool tonemap = true)
        => KaydediciAyarTests.AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
            view.DdaOutputSource = () => istek.DdaOutputs;
            view.DdagrabCheck = _ => Task.FromResult(ddagrab);
            view.TonemapCheck = () => tonemap;
            view.HevcCheck = k => Task.FromResult(hevc?.Invoke(k) ?? false);
            var secilen = view.ChooseCaptureAsync(istek with { Capture = RecorderCapture.Gdigrab }).GetAwaiter().GetResult();
            return (secilen, view.NoticeText);
        }));

    [Fact]
    public void HdrEkrandaGdiDususuBirlesikMetinleSoylenir()
    {
        var (secilen, satir) = Sec(Istek(HdrCikis), ddagrab: false);

        Assert.Equal(RecorderCapture.Gdigrab, secilen.Capture);
        Assert.Equal(DdagrabFallback.Unavailable, secilen.CaptureFallback);
        Assert.False(string.IsNullOrWhiteSpace(Metin("recorder.capture.fallback-hdr")));
        Assert.Equal(Metin("recorder.capture.fallback-hdr"), satir);
    }

    [Fact]
    public void SdrEkrandaGdiDususuEskiMetniKorur()
    {
        var (_, satir) = Sec(Istek(SdrCikis), ddagrab: false);

        Assert.Equal(Metin("recorder.capture.fallback-unavailable"), satir);
    }

    [Fact]
    public void HdrKorunurkenH264HevceGecerVeSoylenir()
    {
        var (secilen, satir) = Sec(Istek(HdrCikis), ddagrab: true, hevc: k => k == "libx265");

        Assert.Equal("libx265", secilen.VideoCodec);
        Assert.True(RecorderArguments.HdrCapture(secilen));
        Assert.Contains("libx265", satir);
        Assert.NotEqual(Metin("recorder.capture.hdr-kept"), satir);
        Assert.StartsWith(Metin("recorder.capture.hdr-kept-hevc").Split("{0}")[0], satir);
    }

    [Fact]
    public void HevcYoksaSdrCevirmeSoylenir()
    {
        var (secilen, satir) = Sec(Istek(HdrCikis), ddagrab: true, hevc: _ => false);

        Assert.True(secilen.HdrToSdr);
        Assert.True(RecorderArguments.HdrTonemapCapture(secilen));
        Assert.Equal(Metin("recorder.capture.hdr-no-hevc"), satir);
    }

    [Fact]
    public void HevcKodlayicidaHdrKorunurVePaylasimUyarisiGelir()
    {
        var (secilen, satir) = Sec(Istek(HdrCikis) with { VideoCodec = "libx265" }, ddagrab: true);

        Assert.Equal("libx265", secilen.VideoCodec);
        Assert.Equal(Metin("recorder.capture.hdr-kept"), satir);
    }

    [Fact]
    public void ZscaleYoksaTonEslemesizCevirmeSoylenir()
    {
        var (secilen, satir) = Sec(Istek(HdrCikis) with { HdrToSdr = true }, ddagrab: true, tonemap: false);

        Assert.False(secilen.CanTonemap);
        Assert.Equal(Metin("recorder.capture.hdr-no-tonemap"), satir);
    }

    [Fact]
    public void TonEslemeliKayittaSatirYok()
    {
        var (secilen, satir) = Sec(Istek(HdrCikis with { SdrWhiteNits = 240 }) with { HdrToSdr = true }, ddagrab: true);

        Assert.True(RecorderArguments.HdrTonemapCapture(secilen));
        Assert.Equal(string.Empty, satir);
    }

    [Fact]
    public void SdrBeyaziOkunamazsaVarsayilanSoylenir()
    {
        var (secilen, satir) = Sec(Istek(HdrCikis) with { HdrToSdr = true }, ddagrab: true);

        Assert.True(RecorderArguments.SdrWhiteAssumed(secilen));
        Assert.Contains("203", satir);
        Assert.Equal(Metin("recorder.capture.sdr-white-assumed").Replace("{0}", "203", StringComparison.Ordinal), satir);
    }

    [Fact]
    public void VarsayilanBeyazYalnizCevrilenHdrKayittaSayilir()
    {
        Assert.False(RecorderArguments.SdrWhiteAssumed(Istek(HdrCikis) with { HdrToSdr = true, CanTonemap = false }));
        Assert.False(RecorderArguments.SdrWhiteAssumed(Istek(HdrCikis)));
        Assert.False(RecorderArguments.SdrWhiteAssumed(Istek(SdrCikis) with { HdrToSdr = true }));
        Assert.True(RecorderArguments.SdrWhiteAssumed(Istek(HdrCikis with { SdrWhiteNits = 40 }) with { HdrToSdr = true }));
        Assert.False(RecorderArguments.SdrWhiteAssumed(Istek(HdrCikis with { SdrWhiteNits = 80 }) with { HdrToSdr = true }));
    }

    /// <summary>
    /// SDR beyazi ve tepe parlaklik hedef cikistan gelir: <c>npl</c> Windows'un SDR beyazi, egrinin
    /// tavani ekranin tepesi bolu beyaz. Tepe beyazin 1,05 katindan asagi inmez; okunamayan beyaz 203.
    /// </summary>
    [Theory]
    [InlineData(240.0, 1015.3, "npl=240,", "peak=4.2304,")]
    [InlineData(480.0, null, "npl=480,", "peak=2.0833,")]
    [InlineData(null, 1015.3, "npl=203,", "peak=5.0015,")]
    [InlineData(400.0, 300.0, "npl=400,", "peak=1.05,")]
    [InlineData(double.NaN, double.NaN, "npl=203,", "peak=4.9261,")]
    public void BeyazVeTepeHedefCikistanGelir(double? beyaz, double? tepe, string npl, string peak)
    {
        var cikis = HdrCikis with
        {
            SdrWhiteNits = beyaz,
            Display = tepe is { } m ? Ekran with { MaxNits = m } : null
        };

        var kaynak = Kaynak(Kur(Istek(cikis) with { HdrToSdr = true }));

        Assert.Contains(npl, kaynak);
        Assert.Contains(peak, kaynak);
        Assert.Equal(1, kaynak.Split("npl=").Length - 1);
    }

    [Fact]
    public void WindowsBeyazDegeriBinde80NitOlcegindedir()
    {
        Assert.Equal(80.0, SdrWhiteLevel.ToNits(1000));
        Assert.Equal(202.96, SdrWhiteLevel.ToNits(2537), 6);
        Assert.Equal(480.0, SdrWhiteLevel.ToNits(6000));
    }

    [Fact]
    public void TaniMetniSdrBeyaziniVeKaynaginiSoyler()
    {
        var okunan = Istek(HdrCikis with { SdrWhiteNits = 240, Display = Ekran }) with { HdrToSdr = true };
        var varsayilan = Istek(HdrCikis) with { HdrToSdr = true };

        var m1 = KayitTanisi.Metin(okunan, Kur(okunan), "1.2.24", DateTimeOffset.UnixEpoch);
        var m2 = KayitTanisi.Metin(varsayilan, Kur(varsayilan), "1.2.24", DateTimeOffset.UnixEpoch);

        Assert.Contains("sdr-white=240", m1);
        Assert.Contains("sdr-white: 240 nits (Windows)", m1);
        Assert.Contains("peak: 1015.3 nits", m1);
        Assert.Contains("sdr-white: 203 nits (assumed, Windows value unreadable)", m2);
        Assert.Contains("peak: 1000 nits", m2);
        Assert.DoesNotContain("sdr-white:", KayitTanisi.Metin(Istek(HdrCikis), null, "1.2.24", DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void VarsayilanBeyazGunlugeDeYazilir()
    {
        var olcu = KaydediciAyarTests.AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
            var klasor = Path.Combine(Path.GetDirectoryName(ayarYolu)!, Gunluk.KlasorAdi);
            int Say() => Directory.GetFiles(klasor).Where(d => !d.EndsWith(KayitTanisi.DosyaAdi, StringComparison.Ordinal))
                .Sum(d => File.ReadAllText(d).Split("SDR white level unreadable").Length - 1);
            view.WriteDiagnostic(Istek(HdrCikis with { SdrWhiteNits = 240 }) with { HdrToSdr = true }, null, null);
            var once = Say();
            view.WriteDiagnostic(Istek(HdrCikis) with { HdrToSdr = true }, null, null);
            return (once, sonra: Say());
        }));

        Assert.Equal(0, olcu.once);
        Assert.Equal(1, olcu.sonra);
    }

    /// <summary>
    /// Canli: Windows'un SDR'yi HDR masaustune koyusu (sRGB dogrusal x beyaz nit, BT.2020'ye
    /// matris, PQ 10 bit) sentetik olarak uretilir ve <see cref="RecorderArguments.HdrSdrChain"/>'den
    /// gecirilir. Gri yamalar ±1 kodda, renkli yamalar <see cref="RecorderColourCard.ColourTolerance"/>
    /// Delta E icinde cizilen sRGB'ye doner.
    /// </summary>
    [FfmpegTheory]
    [InlineData(80.0)]
    [InlineData(203.0)]
    [InlineData(480.0)]
    public void SdrIcerikHdrMasaustundenAyniDegereDoner(double beyaz)
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "hdr-geri-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            const int en = 160, boy = 120;
            var kart = KaydediciOzSinamaTests.Kart(en, boy);
            var giris = Path.Combine(klasor, "pq.raw");
            File.WriteAllBytes(giris, PqKaresi(kart, en, boy, beyaz));
            var cikis = Path.Combine(klasor, "srgb.raw");
            Calistir(ToolLocator.Ffmpeg, "-v", "error", "-f", "rawvideo", "-pix_fmt", "gbrp10le", "-s", en + "x" + boy, "-i", giris,
                "-vf", "format=x2bgr10," + RecorderArguments.HdrSdrChain(beyaz, 1000), "-frames:v", "1",
                "-f", "rawvideo", "-pix_fmt", "rgb24", "-threads", "2", cikis);

            var sonuc = RecorderColourCard.Compare(RecorderColourCard.Sample(File.ReadAllBytes(cikis), en, boy));

            Assert.True(sonuc.All(RecorderColourCard.Passes), RecorderColourCard.Report("w" + beyaz, sonuc));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public void TepeParlaklikBeyazaIner()
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "hdr-tepe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var sira = 0;
            byte Cikti(double nit)
            {
                var giris = Path.Combine(klasor, "t" + sira + ".raw");
                var cikis = Path.Combine(klasor, "t" + sira++ + ".rgb");
                var kod = (ushort)Math.Round(Pq(nit / 10000) * 1023);
                var b = new byte[16 * 16 * 2 * 3];
                for (var i = 0; i < b.Length / 2; i++) BitConverter.TryWriteBytes(b.AsSpan(i * 2), kod);
                File.WriteAllBytes(giris, b);
                Calistir(ToolLocator.Ffmpeg, "-v", "error", "-f", "rawvideo", "-pix_fmt", "gbrp10le", "-s", "16x16", "-i", giris,
                    "-vf", "format=x2bgr10," + RecorderArguments.HdrSdrChain(200, 1000), "-frames:v", "1",
                    "-f", "rawvideo", "-pix_fmt", "rgb24", "-threads", "2", cikis);
                return File.ReadAllBytes(cikis)[0];
            }

            Assert.Equal(255, Cikti(1000));
            Assert.InRange(Cikti(200), 254, 255);
            Assert.InRange(Cikti(100), 185, 189);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    private static readonly double[,] Bt709den2020 =
    {
        { 0.627403895934699, 0.329283038377884, 0.043313065687417 },
        { 0.069097289358232, 0.919540395075459, 0.011362315566309 },
        { 0.016391438875150, 0.088013307877226, 0.895595253247624 }
    };

    private static double Dogrusal(byte v)
    {
        var c = v / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static double Pq(double y)
    {
        const double m1 = 0.1593017578125, m2 = 78.84375, c1 = 0.8359375, c2 = 18.8515625, c3 = 18.6875;
        var p = Math.Pow(Math.Max(0, y), m1);
        return Math.Pow((c1 + c2 * p) / (1 + c3 * p), m2);
    }

    /// <summary><c>rgb24</c> kartin Windows'un HDR masaustundeki karsiligi, <c>gbrp10le</c> duzlemleri (G, B, R).</summary>
    private static byte[] PqKaresi(byte[] rgb24, int en, int boy, double beyaz)
    {
        var n = en * boy;
        var cikti = new byte[n * 2 * 3];
        for (var i = 0; i < n; i++)
        {
            var l = new[] { Dogrusal(rgb24[i * 3]), Dogrusal(rgb24[i * 3 + 1]), Dogrusal(rgb24[i * 3 + 2]) };
            var q = new ushort[3];
            for (var k = 0; k < 3; k++)
                q[k] = (ushort)Math.Round(Pq((Bt709den2020[k, 0] * l[0] + Bt709den2020[k, 1] * l[1] + Bt709den2020[k, 2] * l[2]) * beyaz / 10000) * 1023);
            BitConverter.TryWriteBytes(cikti.AsSpan(i * 2), q[1]);
            BitConverter.TryWriteBytes(cikti.AsSpan((n + i) * 2), q[2]);
            BitConverter.TryWriteBytes(cikti.AsSpan((2 * n + i) * 2), q[0]);
        }
        return cikti;
    }

    [FfmpegFact]
    public void TonEslemeZinciriDosyayaSdrEtiketiYazar()
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "hdr-ton-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "ton.mkv");
        try
        {
            var args = Kur(Istek(HdrCikis) with { HdrToSdr = true, Preset = "ultrafast", MaxDuration = TimeSpan.FromSeconds(1) }).ToList();
            var kaynak = Kaynak(args);
            var zincir = kaynak[(kaynak.IndexOf(",hwdownload,format=x2bgr10,", StringComparison.Ordinal) + ",hwdownload,".Length)..];
            args[args.IndexOf(kaynak)] = "testsrc2=s=640x480:r=30:d=1," + zincir;
            args[^1] = dosya;
            args.InsertRange(args.Count - 1, new[] { "-threads", "2" });

            Calistir(ToolLocator.Ffmpeg, args.ToArray());
            var satirlar = Calistir(ToolLocator.Ffprobe, "-v", "error", "-select_streams", "v:0",
                "-show_entries", "stream=pix_fmt,color_space,color_transfer,color_primaries,color_range",
                "-of", "default=nw=1", dosya);

            Assert.Contains("zscale", zincir);
            Assert.Contains("pix_fmt=yuv420p", satirlar);
            Assert.Contains("color_transfer=bt709", satirlar);
            Assert.Contains("color_primaries=bt709", satirlar);
            Assert.Contains("color_space=bt709", satirlar);
            Assert.Contains("color_range=tv", satirlar);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public void SdrKayitBt709MatrisiyleCevrilirVeEtiketlenir()
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "hdr-sdr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "sdr.mkv");
        try
        {
            var args = Kur(Istek(SdrCikis) with { Preset = "ultrafast", MaxDuration = TimeSpan.FromSeconds(1) }).ToList();
            var kaynak = Kaynak(args);
            args[args.IndexOf(kaynak)] = "color=c=0x00FF00:s=640x480:r=30:d=1,format=bgra";
            args[^1] = dosya;
            args.InsertRange(args.Count - 1, new[] { "-threads", "2" });

            Calistir(ToolLocator.Ffmpeg, args.ToArray());
            var etiket = Calistir(ToolLocator.Ffprobe, "-v", "error", "-select_streams", "v:0",
                "-show_entries", "stream=color_space,color_transfer,color_primaries,color_range",
                "-of", "default=nw=1", dosya);
            var ham = Path.Combine(klasor, "y.raw");
            Calistir(ToolLocator.Ffmpeg, "-v", "error", "-i", dosya, "-frames:v", "1", "-vf", "extractplanes=y",
                "-f", "rawvideo", "-threads", "2", ham);
            var y = File.ReadAllBytes(ham).Average(b => (double)b);

            Assert.True(etiket.Contains("color_transfer=bt709"), etiket + string.Join(" ", args));
            Assert.Contains("color_space=bt709", etiket);
            Assert.Contains("color_primaries=bt709", etiket);
            Assert.Contains("color_range=tv", etiket);
            Assert.InRange(y, 170, 176);
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
