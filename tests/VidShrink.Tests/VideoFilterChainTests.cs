using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class VideoFilterChainTests
{
    private static MediaInfo Kaynak(int w = 1920, int h = 1080, double fps = 25, string codec = "h264",
        bool taramali = false, string? alan = null, string? renk = null) => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 400L * 1024 * 1024,
        DurationSeconds = 120,
        Width = w,
        Height = h,
        Fps = fps,
        VideoCodec = codec,
        TotalBitrateBps = 20_000_000,
        IsInterlaced = taramali,
        FieldOrder = alan,
        ColorSpace = renk
    };

    private static EncodePlan Plan(VideoFilterOptions filtre, int w = 1920, int h = 1080, double fps = 25) => new()
    {
        Codec = "libx264",
        Mode = "crf",
        Crf = 23,
        VideoBitrateK = 3000,
        Width = w,
        Height = h,
        Fps = fps,
        Preset = "medium",
        Filters = filtre
    };

    private static IReadOnlyList<string> Zincir(VideoFilterOptions filtre, MediaInfo? kaynak = null, int w = 1920, int h = 1080, double fps = 25)
        => VideoFilterChain.Filters(kaynak ?? Kaynak(), Plan(filtre, w, h, fps));

    [Fact]
    public void VarsayilanZincirBosVeResmiDegistirmez()
    {
        Assert.Empty(Zincir(VideoFilterOptions.Default));
        Assert.False(VideoFilterOptions.Default.ChangesPicture);
        Assert.Equal(DeinterlaceMode.Auto, VideoFilterOptions.Default.Deinterlace);
        Assert.Equal(DenoiseFilter.Off, VideoFilterOptions.Default.Denoise);
        Assert.Null(VideoFilterOptions.Default.Crop);
    }

    [Fact]
    public void TaramaliKaynakOtomatikteIdetBwdifAlirProgressiveAlmaz()
    {
        Assert.Equal(new[] { "idet,bwdif=mode=send_frame:parity=auto:deint=interlaced" },
            Zincir(VideoFilterOptions.Default, Kaynak(taramali: true, alan: "tt")));
        Assert.Empty(Zincir(VideoFilterOptions.Default, Kaynak(alan: "progressive")));
        Assert.Empty(Zincir(new VideoFilterOptions { Deinterlace = DeinterlaceMode.Off }, Kaynak(taramali: true, alan: "tt")));
        Assert.Contains(VideoFilterChain.DeinterlaceChain, Zincir(new VideoFilterOptions { Deinterlace = DeinterlaceMode.On }));
    }

    [Fact]
    public void IdetYoklamasiYalnizBelirsizAlanVeTaramaliKodeklerdeIstenir()
    {
        var auto = VideoFilterOptions.Default;
        Assert.True(VideoFilterChain.NeedsInterlaceProbe(Kaynak(alan: null), auto));
        Assert.True(VideoFilterChain.NeedsInterlaceProbe(Kaynak(codec: "mpeg2video", alan: "unknown"), auto));
        Assert.True(VideoFilterChain.NeedsInterlaceProbe(Kaynak(codec: "dvvideo"), auto));
        Assert.False(VideoFilterChain.NeedsInterlaceProbe(Kaynak(alan: "progressive"), auto));
        Assert.False(VideoFilterChain.NeedsInterlaceProbe(Kaynak(codec: "hevc"), auto));
        Assert.False(VideoFilterChain.NeedsInterlaceProbe(Kaynak(taramali: true, alan: "tt"), auto));
        Assert.False(VideoFilterChain.NeedsInterlaceProbe(Kaynak(), auto with { Deinterlace = DeinterlaceMode.Off }));
        Assert.False(VideoFilterChain.NeedsInterlaceProbe(Kaynak(), auto with { Detelecine = true }));
    }

    [Fact]
    public void IdetKarariParitesiBaskinTaramaliyiSecerKarisikParityiReddeder()
    {
        Assert.True(VideoFilterChain.IdetSaysInterlaced(new IdetCounts(75, 0, 0, 1)));
        Assert.True(VideoFilterChain.IdetSaysInterlaced(new IdetCounts(0, 75, 0, 1)));
        Assert.True(VideoFilterChain.IdetSaysInterlaced(new IdetCounts(30, 3, 67, 0)));
        Assert.False(VideoFilterChain.IdetSaysInterlaced(new IdetCounts(41, 25, 9, 1)));
        Assert.False(VideoFilterChain.IdetSaysInterlaced(new IdetCounts(31, 23, 21, 1)));
        Assert.False(VideoFilterChain.IdetSaysInterlaced(new IdetCounts(20, 0, 80, 0)));
        Assert.False(VideoFilterChain.IdetSaysInterlaced(new IdetCounts(0, 0, 76, 0)));
        Assert.False(VideoFilterChain.IdetSaysInterlaced(new IdetCounts(0, 0, 0, 0)));
    }

    [Fact]
    public void IdetCiktisindanSonDoluCokluKareSatiriOkunur()
    {
        const string stderr = """
            [Parsed_idet_0 @ 0] Repeated Fields: Neither:    76 Top:     0 Bottom:     0
            [Parsed_idet_0 @ 0] Single frame detection: TFF:    60 BFF:     0 Progressive:    10 Undetermined:     6
            [Parsed_idet_0 @ 0] Multi frame detection: TFF:    75 BFF:     0 Progressive:     0 Undetermined:     1
            [Parsed_idet_1 @ 0] Multi frame detection: TFF:     0 BFF:     0 Progressive:     0 Undetermined:     0
            """;
        Assert.Equal(new IdetCounts(75, 0, 0, 1), IdetCounts.Parse(stderr));
        Assert.Null(IdetCounts.Parse("frame=  76 fps=0.0"));
    }

    [Fact]
    public void YoklamaSonucuOtomatigiAcikYaDaKapaliyaCevirirElleSecimeDokunmaz()
    {
        var auto = VideoFilterOptions.Default;
        var info = Kaynak();
        Assert.Equal(DeinterlaceMode.On, VideoFilterChain.ResolveInterlace(auto, info, new IdetCounts(75, 0, 0, 1)).Deinterlace);
        Assert.Equal(DeinterlaceMode.Off, VideoFilterChain.ResolveInterlace(auto, info, new IdetCounts(0, 0, 76, 0)).Deinterlace);
        Assert.Equal(DeinterlaceMode.Auto, VideoFilterChain.ResolveInterlace(auto, info, null).Deinterlace);
        Assert.Equal(DeinterlaceMode.On, VideoFilterChain.ResolveInterlace(auto, Kaynak(taramali: true), null).Deinterlace);
        var kapali = auto with { Deinterlace = DeinterlaceMode.Off };
        Assert.Same(kapali, VideoFilterChain.ResolveInterlace(kapali, info, new IdetCounts(75, 0, 0, 1)));
    }

    [Fact]
    public void DetelecineZinciriFpsyiBesteDordeIndirirVeDeinterlaceYerineGecer()
    {
        var filtre = new VideoFilterOptions { Detelecine = true, Deinterlace = DeinterlaceMode.On };
        var info = Kaynak(fps: 29.97, taramali: true);
        var zincir = VideoFilterChain.Filters(info, Plan(filtre, fps: 29.97 * 0.8));
        Assert.Equal(new[] { "fieldmatch,yadif=deint=interlaced,decimate" }, zincir);
        Assert.Equal(23.976, VideoFilterChain.PlannedSource(info, filtre).Fps, 3);
        Assert.DoesNotContain(zincir, f => f.Contains("fps="));
    }

    [Theory]
    [InlineData(DenoiseFilter.NlMeans, FilterStrength.Light, "nlmeans=s=1.0:p=7:r=9")]
    [InlineData(DenoiseFilter.NlMeans, FilterStrength.Medium, "nlmeans=s=2.0:p=7:r=9")]
    [InlineData(DenoiseFilter.NlMeans, FilterStrength.Strong, "nlmeans=s=4.0:p=7:r=15")]
    [InlineData(DenoiseFilter.Hqdn3d, FilterStrength.Light, "hqdn3d=2:1:2:3")]
    [InlineData(DenoiseFilter.Hqdn3d, FilterStrength.Medium, "hqdn3d=3:2:2:3")]
    [InlineData(DenoiseFilter.Hqdn3d, FilterStrength.Strong, "hqdn3d=7:7:5:5")]
    public void GurultuAzaltmaSecilenFiltreVeGucuYazar(DenoiseFilter tur, FilterStrength guc, string beklenen)
    {
        Assert.Equal(new[] { beklenen }, Zincir(new VideoFilterOptions { Denoise = tur, DenoiseStrength = guc }));
        Assert.Empty(Zincir(new VideoFilterOptions { Denoise = DenoiseFilter.Off, DenoiseStrength = guc }));
    }

    [Theory]
    [InlineData(SharpenMode.Light, "unsharp=5:5:0.5:3:3:0.0")]
    [InlineData(SharpenMode.Medium, "unsharp=5:5:1.0:3:3:0.0")]
    [InlineData(SharpenMode.Strong, "unsharp=5:5:1.5:3:3:0.0")]
    public void KeskinlestirmeOlceklemedenSonraGelir(SharpenMode kip, string beklenen)
    {
        var zincir = Zincir(new VideoFilterOptions { Sharpen = kip }, w: 1280, h: 720);
        Assert.Equal(new[] { "scale=1280:720:flags=lanczos", beklenen }, zincir);
    }

    [Fact]
    public void DeblockVeDebandTekFiltreYazarKapalidaYazmaz()
    {
        Assert.Equal(new[] { "deblock=filter=strong:block=8" }, Zincir(new VideoFilterOptions { Deblock = true }));
        Assert.Equal(new[] { "deband" }, Zincir(new VideoFilterOptions { Deband = true }));
        Assert.DoesNotContain(Zincir(new VideoFilterOptions { Deband = false, Deblock = false }), f => f.StartsWith("deb"));
    }

    [Theory]
    [InlineData(TransposeMode.Clockwise, new[] { "transpose=clock" })]
    [InlineData(TransposeMode.CounterClockwise, new[] { "transpose=cclock" })]
    [InlineData(TransposeMode.UpsideDown, new[] { "hflip", "vflip" })]
    [InlineData(TransposeMode.FlipHorizontal, new[] { "hflip" })]
    [InlineData(TransposeMode.FlipVertical, new[] { "vflip" })]
    public void DondurmeFiltresiVeDoksanDerecedeBoyutTakasi(TransposeMode kip, string[] beklenen)
    {
        var filtre = new VideoFilterOptions { Transpose = kip };
        var kaynak = VideoFilterChain.PlannedSource(Kaynak(), filtre);
        var dik = kip is TransposeMode.Clockwise or TransposeMode.CounterClockwise;
        Assert.Equal(dik ? 1080 : 1920, kaynak.Width);
        Assert.Equal(dik ? 1920 : 1080, kaynak.Height);
        Assert.Equal(beklenen, Zincir(filtre, w: kaynak.Width, h: kaynak.Height));
    }

    [Fact]
    public void KirpmaDikdortgeniYazilirVeOlceklemeKirpikBoyuttanHesaplanir()
    {
        var filtre = new VideoFilterOptions { Crop = new CropRect(1920, 800, 0, 140) };
        Assert.Equal(new[] { "crop=1920:800:0:140" }, Zincir(filtre, w: 1920, h: 800));
        Assert.Equal(new[] { "crop=1920:800:0:140", "scale=1280:534:flags=lanczos" }, Zincir(filtre, w: 1280, h: 534));
        Assert.Equal(new[] { "scale=1920:800:flags=lanczos" }, Zincir(VideoFilterOptions.Default, w: 1920, h: 800));
    }

    [Fact]
    public void PadVeGriSonaYazilir()
    {
        var filtre = new VideoFilterOptions { Grayscale = true, Pad = new PadBorders(10, 20, 4, 6) };
        Assert.Equal(new[] { "hue=s=0", "pad=w=iw+10:h=ih+30:x=4:y=10:color=black" }, Zincir(filtre));
        Assert.Empty(Zincir(new VideoFilterOptions { Grayscale = false, Pad = null }));
    }

    [Fact]
    public void RenkUzayiDonusumuYalnizKaynakFarkliysaZscaleVeEtiketYazar()
    {
        var bt709 = new VideoFilterOptions { ColorMatrix = ColorMatrixTarget.Bt709 };
        var sd = Kaynak(720, 576, renk: "bt470bg");
        Assert.Equal(new[] { "zscale=min=470bg:tin=601:pin=470bg:m=709:t=709:p=709" }, Zincir(bt709, sd, 720, 576));
        Assert.Empty(Zincir(bt709, Kaynak(renk: "bt709")));
        Assert.Empty(Zincir(bt709, Kaynak()));
        Assert.Equal(new[] { "zscale=min=709:tin=709:pin=709:m=170m:t=601:p=170m" },
            Zincir(new VideoFilterOptions { ColorMatrix = ColorMatrixTarget.Bt601 }));
        Assert.Equal(new[] { "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709" },
            VideoFilterChain.ColorArgs(Plan(bt709)));
        Assert.Empty(VideoFilterChain.ColorArgs(Plan(VideoFilterOptions.Default)));
    }

    [Fact]
    public void HdrFiltresiVarkenRenkDonusumuVeEtiketiYazilmaz()
    {
        var plan = Plan(new VideoFilterOptions { ColorMatrix = ColorMatrixTarget.Bt601 });
        plan.HdrVideoFilter = "zscale=t=linear,tonemap=hable";
        var zincir = VideoFilterChain.Filters(Kaynak(), plan);
        Assert.Equal(new[] { "zscale=t=linear,tonemap=hable" }, zincir);
        Assert.Empty(VideoFilterChain.ColorArgs(plan));
    }

    [Fact]
    public void TumFiltrelerSabitSiradaDizilirFpsEnSonda()
    {
        var filtre = new VideoFilterOptions
        {
            Deinterlace = DeinterlaceMode.On,
            Crop = new CropRect(1920, 800, 0, 140),
            Deblock = true,
            Denoise = DenoiseFilter.Hqdn3d,
            Deband = true,
            Transpose = TransposeMode.FlipHorizontal,
            Sharpen = SharpenMode.Light,
            ColorMatrix = ColorMatrixTarget.Bt601,
            Grayscale = true,
            Pad = new PadBorders(2, 2, 0, 0)
        };
        Assert.Equal(new[]
        {
            VideoFilterChain.DeinterlaceChain, "crop=1920:800:0:140", "deblock=filter=strong:block=8", "hqdn3d=3:2:2:3",
            "deband", "hflip", "scale=1280:534:flags=lanczos", "unsharp=5:5:0.5:3:3:0.0",
            "zscale=min=709:tin=709:pin=709:m=170m:t=601:p=170m", "hue=s=0", "pad=w=iw+0:h=ih+4:x=0:y=2:color=black", "fps=24"
        }, Zincir(filtre, w: 1280, h: 534, fps: 24));
    }

    [Fact]
    public void FfmpegArgumanlariZinciriVfIcindeVeRenkEtiketiniTasir()
    {
        var plan = Plan(new VideoFilterOptions { Grayscale = true, ColorMatrix = ColorMatrixTarget.Bt709 }, 1280, 720);
        var args = FfmpegArguments.Build(Kaynak(720, 480, renk: "smpte170m"), plan, "out.mp4", 0, null).ToList();
        var vf = args.IndexOf("-vf");
        Assert.True(vf >= 0, string.Join(' ', args));
        Assert.Equal("scale=1280:720:flags=lanczos,zscale=min=170m:tin=601:pin=170m:m=709:t=709:p=709,hue=s=0", args[vf + 1]);
        Assert.Equal("bt709", args[args.IndexOf("-colorspace") + 1]);

        var sade = FfmpegArguments.Build(Kaynak(), Plan(VideoFilterOptions.Default), "out.mp4", 0, null).ToList();
        Assert.DoesNotContain("-vf", sade);
        Assert.DoesNotContain("-colorspace", sade);
    }

    [Fact]
    public void AcikFiltrePassthroughuEngeller()
    {
        var info = Kaynak(1280, 720) with { FileSizeBytes = 5L * 1024 * 1024, DurationSeconds = 10, TotalBitrateBps = 4_000_000 };
        var sade = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Auto });
        Assert.Equal(EncodeMode.PassThrough, sade.ModeEnum);

        var gri = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Auto, Filters = new VideoFilterOptions { Grayscale = true } });
        Assert.NotEqual(EncodeMode.PassThrough, gri.ModeEnum);
        Assert.True(gri.Filters.Grayscale);
    }

    [Fact]
    public void TaramaliKaynakOtomatiktePassthroughaDusmezProgressiveDuser()
    {
        var duz = Kaynak(1280, 720, alan: "progressive") with { FileSizeBytes = 5L * 1024 * 1024, DurationSeconds = 10, TotalBitrateBps = 4_000_000 };
        var duzPlan = PlanCalculator.Build(duz, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Auto });
        Assert.Equal(EncodeMode.PassThrough, duzPlan.ModeEnum);

        var taramali = duz with { IsInterlaced = true, FieldOrder = "tt" };
        var taramaliPlan = PlanCalculator.Build(taramali, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Auto });
        Assert.NotEqual(EncodeMode.PassThrough, taramaliPlan.ModeEnum);
        Assert.Contains("idet,bwdif=mode=send_frame:parity=auto:deint=interlaced", VideoFilterChain.Filters(taramali, taramaliPlan));

        var elleKapali = PlanCalculator.Build(taramali, new PlanOptions
        {
            TargetMb = 25,
            Codec = CodecPreference.Auto,
            Filters = VideoFilterOptions.Default with { Deinterlace = DeinterlaceMode.Off }
        });
        Assert.Equal(EncodeMode.PassThrough, elleKapali.ModeEnum);
    }

    [Fact]
    public void PlanKirpmaOnerisiniTasirAmaKirpmayiUygulamaz()
    {
        var info = Kaynak();
        var oneri = new CropRect(1920, 800, 0, 140);
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, DetectedCrop = oneri });
        Assert.Equal(oneri, plan.SuggestedCrop);
        Assert.Null(plan.Filters.Crop);
        Assert.DoesNotContain(VideoFilterChain.Filters(info, plan), f => f.StartsWith("crop="));

        var kirp = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, DetectedCrop = oneri, Filters = VideoFilterOptions.Default.WithCrop(oneri) });
        Assert.Null(kirp.SuggestedCrop);
        Assert.Contains("crop=1920:800:0:140", VideoFilterChain.Filters(info, kirp));
        Assert.True(kirp.Height <= 800, $"{kirp.Width}x{kirp.Height}");
    }

    [Fact]
    public void GecersizKirpmaVePadBildirilir()
    {
        var info = Kaynak(640, 360);
        Assert.Empty(VideoFilterChain.Validate(info, new VideoFilterOptions { Crop = new CropRect(640, 300, 0, 30), Pad = new PadBorders(2, 2, 1, 1) }));
        Assert.Contains("crop: size must be even", VideoFilterChain.Validate(info, new VideoFilterOptions { Crop = new CropRect(639, 300, 0, 0) }));
        Assert.Contains("crop: rectangle leaves the source frame", VideoFilterChain.Validate(info, new VideoFilterOptions { Crop = new CropRect(640, 300, 0, 62) }));
        Assert.Contains("crop: size must be positive", VideoFilterChain.Validate(info, new VideoFilterOptions { Crop = new CropRect(0, 300, 0, 0) }));
        Assert.Contains("pad: borders must not be negative", VideoFilterChain.Validate(info, new VideoFilterOptions { Pad = new PadBorders(-2, 0, 0, 0) }));
        Assert.Contains("pad: added size must be even", VideoFilterChain.Validate(info, new VideoFilterOptions { Pad = new PadBorders(1, 0, 0, 0) }));
    }

    [Fact]
    public void MetinSecenegiHerFiltreyiOkurBilinmeyeniReddeder()
    {
        var o = VideoFilterChain.Parse("deinterlace=on, denoise=hqdn3d:strong,sharpen=light,deblock,deband,gray,rotate=cclock,pad=2:2:0:0,colorspace=bt601,crop=640:300:0:30");
        Assert.Equal(new VideoFilterOptions
        {
            Deinterlace = DeinterlaceMode.On,
            Denoise = DenoiseFilter.Hqdn3d,
            DenoiseStrength = FilterStrength.Strong,
            Sharpen = SharpenMode.Light,
            Deblock = true,
            Deband = true,
            Grayscale = true,
            Transpose = TransposeMode.CounterClockwise,
            Pad = new PadBorders(2, 2, 0, 0),
            ColorMatrix = ColorMatrixTarget.Bt601,
            Crop = new CropRect(640, 300, 0, 30)
        }, o);
        Assert.Equal(VideoFilterOptions.Default, VideoFilterChain.Parse(""));
        Assert.True(VideoFilterChain.Parse("detelecine").Detelecine);
        Assert.Equal(DenoiseFilter.NlMeans, VideoFilterChain.Parse("denoise=nlmeans").Denoise);
        Assert.Equal(DeinterlaceMode.Off, VideoFilterChain.Parse("deinterlace=off").Deinterlace);
        foreach (var kotu in new[] { "blur", "deinterlace=maybe", "denoise=median", "rotate=45", "crop=1:2:3", "pad=a:b:c:d", "colorspace=bt2020" })
            Assert.StartsWith("unknown filter option:", Assert.Throws<ArgumentException>(() => VideoFilterChain.Parse(kotu)).Message);
    }

    [Fact]
    public void KirpmaKarariOrneklerinModunuAlirBirlesimiDegil()
    {
        var bant = new CropRect(1920, 800, 0, 140);
        var ornek = new[] { bant, bant, new CropRect(1920, 1080, 0, 0), bant, new CropRect(1920, 1040, 0, 20), bant };
        Assert.Equal(bant, CropProbe.Decide(ornek, 1920, 1080));
        Assert.Null(CropProbe.Decide(new[] { new CropRect(1920, 1080, 0, 0), new CropRect(1920, 1080, 0, 0), bant }, 1920, 1080));
        Assert.Null(CropProbe.Decide(Array.Empty<CropRect>(), 1920, 1080));
        Assert.Equal(new CropRect(1920, 1040, 0, 20), CropProbe.Decide(new[] { bant, new CropRect(1920, 1040, 0, 20) }, 1920, 1080));
    }

    [Fact]
    public void KirpmaYoklamasiSinirYirmiDortVeOnNoktayaYayilir()
    {
        Assert.Equal("cropdetect=limit=24:round=2:skip=0:reset=0", CropProbe.Filter);
        Assert.Equal(new[] { 5, 15, 25, 35, 45, 55, 65, 75, 85, 95 }, CropProbe.SampleTimes(100).Select(t => (int)t));
        Assert.Equal(new CropRect(640, 360, 0, 60),
            CropProbe.ParseLast("[Parsed_cropdetect_0 @ 0] x1:0 x2:639 y1:58 y2:421 w:640 h:364 x:0 y:58 pts:0 t:0 crop=640:364:0:58\n[Parsed_cropdetect_0 @ 0] crop=640:360:0:60"));
        Assert.Null(CropProbe.ParseLast("frame=2"));
        var args = InterlaceProbe.Arguments(Kaynak() with { DurationSeconds = 100 });
        Assert.Equal("10", args[args.ToList().IndexOf("-ss") + 1]);
        Assert.DoesNotContain("-ss", InterlaceProbe.Arguments(Kaynak() with { DurationSeconds = 5 }));
    }
}
