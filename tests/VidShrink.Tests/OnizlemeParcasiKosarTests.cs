using System.Text;
using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Onizleme parcasinin <b>kosan</b> olcusu. <see cref="FfmpegArguments.BuildSegment"/>
/// yedi cagri yerinde yalnizca dizgi olarak pimliydi; uretilen arguman dizisi hicbir
/// olcude gercek ffmpeg'e verilmiyordu. Kullanicinin ekraninda 19 Eylul 2026'da
/// "Onizleme Ornegi Kodlanamadi" ciktı: filtre grafigi <c>-22 Invalid argument</c>
/// verdi, libx264 hic acilmadi, cikti 0 bayt kaldi. Bu olcu argumani kosturur ve
/// ciktinin dolu oldugunu okur.
/// </summary>
public sealed class OnizlemeParcasiKosarTests : IClassFixture<SegmentClips>
{
    private readonly SegmentClips _clips;
    private readonly ITestOutputHelper _cikti;

    public OnizlemeParcasiKosarTests(SegmentClips clips, ITestOutputHelper cikti)
    {
        _clips = clips;
        _cikti = cikti;
    }

    private string Temp()
    {
        var dir = Path.Combine(_clips.Directory, "onizleme-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static MediaInfo Kaynak(string path, int width, int height) => new()
    {
        FilePath = path,
        FileSizeBytes = new FileInfo(path).Length,
        DurationSeconds = 12,
        Width = width,
        Height = height,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 2_000_000,
    };

    private static EncodePlan Plan(int genislik, int yukseklik, VideoFilterOptions filtreler) => new()
    {
        Codec = "libx264",
        Width = genislik,
        Height = yukseklik,
        Fps = 30,
        Preset = "veryfast",
        PixelFormat = "yuv420p",
        AudioCodec = null,
        Filters = filtreler,
    };

    [FfmpegTheory]
    [InlineData(false, 640, 360, 4.0)]
    [InlineData(true, 1920, 1080, 4.0)]
    [InlineData(true, 1280, 720, 0.0)]
    [InlineData(true, 854, 480, 9.5)]
    public void ParcaArgumaniGercekFfmpegdenGeciyor(bool buyuk, int genislik, int yukseklik, double baslangic)
    {
        Assert.True(_clips.Ready);

        var info = Kaynak(buyuk ? _clips.Buyuk : _clips.Kaynak, buyuk ? 1920 : 640, buyuk ? 1080 : 360);
        var dizin = Temp();
        var cikti = Path.Combine(dizin, "parca.mp4");
        var parca = PreviewSegment.For(info, Plan(genislik, yukseklik, VideoFilterOptions.Default), baslangic, cikti);

        var kod = SegmentClips.Ffmpeg(parca.Arguments);

        Assert.True(File.Exists(cikti), $"Parca dosyasi yok. Arguman: {string.Join(' ', parca.Arguments)}");
        Assert.True(new FileInfo(cikti).Length > 0, $"Parca 0 bayt. Arguman: {string.Join(' ', parca.Arguments)}");
        Assert.Equal(0, kod);
    }

    /// <summary>
    /// Kullanicinin 19 Eylul 2026'da gordugu kusurun pimi. Plan passthrough oldugunda
    /// <see cref="EncodePlan.Codec"/> kaynagin cozucu adini, <see cref="EncodePlan.Preset"/>
    /// ise <c>copy</c> tasir; kodlama argumanina cevrilince ffmpeg
    /// <c>invalid preset 'copy'</c> deyip kodlayiciyi hic acmiyor ve cikti 0 bayt kaliyordu.
    /// Parca artik teslimin kendisi gibi kopyalaniyor.
    /// </summary>
    [FfmpegFact]
    public void PassthroughParcasiKodlanmaz()
    {
        Assert.True(_clips.Ready);

        var info = Kaynak(_clips.Buyuk, 1920, 1080);
        var plan = new EncodePlan
        {
            Codec = info.VideoCodec,
            Mode = "passthrough",
            Preset = "copy",
            Width = info.Width,
            Height = info.Height,
            Fps = info.Fps,
            PixelFormat = "yuv420p",
            VideoBitrateK = 2000,
            AudioCodec = null,
        };

        var cikti = Path.Combine(Temp(), "kopya.mp4");
        var parca = PreviewSegment.For(info, plan, 4.0, cikti, durationSeconds: 1.0);

        Assert.DoesNotContain("-preset", parca.Arguments);
        Assert.Equal("copy", parca.Arguments[parca.Arguments.ToList().IndexOf("-c:v") + 1]);
        Assert.Equal(PreviewQuality.Kopya, parca.Quality.Kind);
        Assert.False(parca.IsApproximate);

        var kod = SegmentClips.Ffmpeg(parca.Arguments);

        Assert.Equal(0, kod);
        Assert.True(File.Exists(cikti), "Parca dosyasi yok.");
        Assert.True(new FileInfo(cikti).Length > 0, "Parca 0 bayt.");
    }

    /// <summary>
    /// Filtre uzayinin taranmasi. Kullanicinin gordugu <c>-22</c> filtre grafiginden geldi;
    /// hangi kolun urettigi tahminle degil kosumla bulunur. Her kol ayri bir ffmpeg
    /// surecidir ve ciktinin dolu olmasi beklenir.
    /// </summary>
    [FfmpegFact]
    public void HerFiltreKoluParcadaKosuyor()
    {
        Assert.True(_clips.Ready);

        var kollar = new (string Ad, VideoFilterOptions Filtre, int Genislik, int Yukseklik)[]
        {
            ("filtresiz", VideoFilterOptions.Default, 1280, 720),
            ("kirpma", VideoFilterOptions.Default with { Crop = new CropRect(1920, 800, 0, 140) }, 1280, 534),
            ("kirpma+olcek", VideoFilterOptions.Default with { Crop = new CropRect(1280, 720, 320, 180) }, 640, 360),
            ("cerceve", VideoFilterOptions.Default with { Pad = new PadBorders(20, 20, 40, 40) }, 1920, 1080),
            ("dondurme", VideoFilterOptions.Default with { Transpose = TransposeMode.Clockwise }, 608, 1080),
            ("aynalama", VideoFilterOptions.Default with { Transpose = TransposeMode.FlipHorizontal }, 1280, 720),
            ("gurultu-hqdn3d", VideoFilterOptions.Default with { Denoise = DenoiseFilter.Hqdn3d }, 1280, 720),
            ("keskinlestirme", VideoFilterOptions.Default with { Sharpen = SharpenMode.Medium }, 1280, 720),
            ("bantlama", VideoFilterOptions.Default with { Deband = true }, 1280, 720),
            ("blok", VideoFilterOptions.Default with { Deblock = true }, 1280, 720),
            ("gri", VideoFilterOptions.Default with { Grayscale = true }, 1280, 720),
            ("renk-bt709", VideoFilterOptions.Default with { ColorMatrix = ColorMatrixTarget.Bt709 }, 1280, 720),
            ("tarama-acma", VideoFilterOptions.Default with { Deinterlace = DeinterlaceMode.On }, 1280, 720),
            ("telesine", VideoFilterOptions.Default with { Detelecine = true }, 1280, 720),
        };

        var info = Kaynak(_clips.Buyuk, 1920, 1080);
        var dizin = Temp();
        var dusuk = new StringBuilder();

        foreach (var kol in kollar)
        {
            var cikti = Path.Combine(dizin, kol.Ad + ".mp4");
            var parca = PreviewSegment.For(
                info, Plan(kol.Genislik, kol.Yukseklik, kol.Filtre), 4.0, cikti, durationSeconds: 1.0);

            var kod = SegmentClips.Ffmpeg(parca.Arguments);
            var boyut = File.Exists(cikti) ? new FileInfo(cikti).Length : 0;
            _cikti.WriteLine($"{kol.Ad}: kod={kod} boyut={boyut}");
            _cikti.WriteLine("  " + FfmpegArguments.ToCommandLine(parca.Arguments));

            if (kod != 0 || boyut <= 0)
                dusuk.AppendLine($"{kol.Ad}: kod={kod} boyut={boyut} :: {FfmpegArguments.ToCommandLine(parca.Arguments)}");
        }

        Assert.True(dusuk.Length == 0, dusuk.ToString());
    }
}
