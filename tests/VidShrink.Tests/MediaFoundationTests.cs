using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// ffmpeg **ve** bu makinede donanim MFT'siyle gercekten acilan <c>h264_mf</c> gerektiren
/// olculer. <see cref="HardwareEncoderFactAttribute"/> ile ayni duzen: adin derlemede
/// gecmesi yetmez, yoklama urunun kendi argumanlariyla (<c>-hw_encoding 1 -pix_fmt nv12</c>)
/// kosar. MFT yoklugu kod hatasi degil ortam bulgusudur; sebebini yazarak atlanir. Windows
/// disinda MF hic onerilmez, orada da atlanir.
/// </summary>
public sealed class MediaFoundationFactAttribute : FactAttribute
{
    public const string Codec = "h264_mf";

    public MediaFoundationFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Media Foundation yalniz Windows'ta, canli kol kosturulmadi.";
            return;
        }

        if (!ToolLocator.IsAvailable(out var missing))
        {
            Skip = $"{missing} bulunamadi, Media Foundation olculeri kosturulmadi.";
            return;
        }

        if (!EncoderCapabilities.Instance.HasEncoder(Codec))
        {
            Skip = $"{Codec} bu ffmpeg derlemesinde yok, Media Foundation olculeri kosturulmadi.";
            return;
        }

        var probe = EncoderCapabilities.Instance.Probe(Codec);
        if (!probe.Succeeded)
            Skip = $"{Codec} derlemede var ama bu makinede donanim MFT'si acilmadi ({probe.State}, {probe.ElapsedMs}ms), " +
                   "Media Foundation olculeri kosturulmadi.";
    }
}

public sealed class MediaFoundationTests
{
    private readonly ITestOutputHelper _cikti;

    public MediaFoundationTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static readonly string[] MfKodlayicilar = { "h264_mf", "hevc_mf", "av1_mf" };

    private sealed class Liste : IEncoderAvailability
    {
        private readonly HashSet<string> _calisan;
        public Liste(params string[] calisan) => _calisan = new HashSet<string>(calisan, StringComparer.OrdinalIgnoreCase);
        public bool HasEncoder(string name) => true;
        public bool WorksAsEncoder(string codec) => _calisan.Contains(codec);
        public EncoderProbeState EncoderState(string codec)
            => _calisan.Contains(codec) ? EncoderProbeState.Working : EncoderProbeState.NotWorking;
    }

    private static MediaInfo Kaynak() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 900_000_000L,
        DurationSeconds = 300,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 24_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    private static EncodePlan BitHiziPlani(string codec) => new()
    {
        Codec = codec,
        Mode = "2pass",
        VideoBitrateK = 2000,
        AudioCodec = "aac",
        AudioBitrateK = 128,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        Preset = FfmpegArguments.DefaultPreset(codec),
        PixelFormat = CodecModel.OutputPixelFormat(codec, "yuv420p")
    };

    private static string? Sonraki(IReadOnlyList<string> args, string bayrak)
    {
        var i = args.ToList().IndexOf(bayrak);
        return i >= 0 && i + 1 < args.Count ? args[i + 1] : null;
    }

    [Theory]
    [InlineData("h264_mf")]
    [InlineData("hevc_mf")]
    [InlineData("av1_mf")]
    public void MfDonanimAilesindeTekGecisVeOnAyarsiz(string codec)
    {
        Assert.Equal(EncoderVendor.MediaFoundation, CodecModel.Vendor(codec));
        Assert.True(CodecModel.IsHardware(codec));
        Assert.True(CodecModel.SinglePassRateControl(codec));
        Assert.False(FfmpegArguments.NeedsTwoPasses(codec));
        Assert.False(CodecModel.TakesPreset(codec));
        Assert.False(CodecModel.HasQualityScale(codec));
        Assert.True(FfmpegArguments.IsValidPreset(codec, FfmpegArguments.DefaultPreset(codec)));
        Assert.Equal("nv12", CodecModel.OutputPixelFormat(codec, "yuv420p"));
        Assert.Equal("nv12", CodecModel.OutputPixelFormat(codec, "p010le"));
    }

    [Fact]
    public void MfOlmayanAdlarMfSayilmaz()
    {
        foreach (var codec in new[] { "libx264", "h264_nvenc", "hevc_amf", "av1_qsv", "h264_videotoolbox", "mf" })
            Assert.NotEqual(EncoderVendor.MediaFoundation, CodecModel.Vendor(codec));
        Assert.Equal("yuv420p", CodecModel.OutputPixelFormat("h264_nvenc", "yuv420p"));
    }

    [Theory]
    [InlineData("h264_mf")]
    [InlineData("hevc_mf")]
    [InlineData("av1_mf")]
    public void BitHiziKoluDonanimMftVeTepeSinirliPcVbrYazar(string codec)
    {
        var plan = BitHiziPlani(codec);
        var args = FfmpegArguments.Build(Kaynak(), plan, "cikti.mp4", 0, null);

        Assert.Equal(codec, Sonraki(args, "-c:v"));
        Assert.Equal("1", Sonraki(args, "-hw_encoding"));
        Assert.Equal("pc_vbr", Sonraki(args, "-rate_control"));
        Assert.Equal("2000k", Sonraki(args, "-b:v"));
        Assert.Equal("nv12", Sonraki(args, "-pix_fmt"));

        var tepe = int.Parse(Sonraki(args, "-maxrate")!.TrimEnd('k'), CultureInfo.InvariantCulture);
        Assert.InRange(tepe, plan.VideoBitrateK, (int)(plan.VideoBitrateK * FfmpegArguments.HardwarePeakCeiling));

        foreach (var yok in new[] { "-preset", "-pass", "-passlogfile", "-crf", "-quality", "-global_quality", "-rc", "-keyint_min" })
            Assert.DoesNotContain(yok, args);
    }

    [Fact]
    public void KaliteOlcegiYokCrfArgumaniPatlar()
    {
        foreach (var codec in MfKodlayicilar)
            Assert.Throws<NotSupportedException>(() => CodecModel.QualityArgs(codec, 24));
    }

    [Fact]
    public void YoklamaUretimdekiAnahtarlarlaKosar()
    {
        foreach (var codec in MfKodlayicilar)
        {
            var args = EncoderCapabilities.ProbeArguments(codec);
            Assert.Equal("1", Sonraki(args, "-hw_encoding"));
            Assert.Equal("nv12", Sonraki(args, "-pix_fmt"));
            Assert.Equal(codec, Sonraki(args, "-c:v"));
        }

        var nvenc = EncoderCapabilities.ProbeArguments("h264_nvenc");
        Assert.DoesNotContain("-hw_encoding", nvenc);
        Assert.DoesNotContain("-pix_fmt", nvenc);
    }

    /// <summary>
    /// Kalite kipi istenen plan donanimda CRF'le kosar (NVENC olumsuz kontrol); MF'nin o olcegi
    /// yok. Plan bit hizi kipine doner ve gerekce kodu bunu soyler.
    /// </summary>
    [Theory]
    [InlineData("h264_mf")]
    [InlineData("hevc_mf")]
    [InlineData("av1_mf")]
    public void KaliteKipiIstenenMfPlaniBitHizinaDoner(string codec)
    {
        var options = new PlanOptions { TargetMb = 50, LockedCodec = codec, LockedMode = EncodeMode.Crf };
        var nvenc = new PlanOptions { TargetMb = 50, LockedCodec = "h264_nvenc", LockedMode = EncodeMode.Crf };

        var plan = PlanCalculator.Build(Kaynak(), options, new Liste(codec, "h264_nvenc"));
        var kiyas = PlanCalculator.Build(Kaynak(), nvenc, new Liste(codec, "h264_nvenc"));

        Assert.Equal(EncodeMode.Crf, kiyas.ModeEnum);
        Assert.DoesNotContain(kiyas.ReasonCodes, n => n.Code == ReasonCode.NoQualityScaleBitrate);
        Assert.Equal(codec, plan.Codec);
        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);
        Assert.Null(plan.Crf);
        Assert.True(plan.VideoBitrateK > 0);
        Assert.Equal("nv12", plan.PixelFormat);
        Assert.Contains(plan.ReasonCodes, n => n.Code == ReasonCode.NoQualityScaleBitrate && n.RequestedCodec == codec);

        var args = FfmpegArguments.Build(Kaynak(), plan, "cikti.mp4", 0, null);
        Assert.Contains("-b:v", args);
        Assert.DoesNotContain("-crf", args);
    }

    [Fact]
    public void ElleCrfKilidiMfdeBitHizinaDoner()
    {
        var options = new PlanOptions { TargetMb = 50, LockedCodec = "hevc_mf", LockedCrf = 24 };

        var plan = PlanCalculator.Build(Kaynak(), options, new Liste("hevc_mf"));

        Assert.Equal("hevc_mf", plan.Codec);
        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);
        Assert.Contains(plan.ReasonCodes, n => n.Code == ReasonCode.NoQualityScaleBitrate);
    }

    [Fact]
    public void AcilmayanMfKilidiAilesininYazilimKodlayicisinaDuser()
    {
        Assert.Equal("libx264", PlanCalculator.Build(Kaynak(), new PlanOptions { TargetMb = 50, LockedCodec = "h264_mf" }, new Liste()).Codec);
        Assert.Equal("libx265", PlanCalculator.Build(Kaynak(), new PlanOptions { TargetMb = 50, LockedCodec = "hevc_mf" }, new Liste()).Codec);
        Assert.Equal("libsvtav1", PlanCalculator.Build(Kaynak(), new PlanOptions { TargetMb = 50, LockedCodec = "av1_mf" }, new Liste()).Codec);
    }

    /// <summary>
    /// Otomatik kipin donanim yeglemesi MF'yi hic secmez: bu makinede MF, NVIDIA MFT'sinin
    /// kendisi ve nvenc zaten ondan once; baska bir MFT olculmedi
    /// (<c>docs/olcumler/hb15-media-foundation.md</c>). Yalniz MF calisan bir makinede hizli
    /// kip yazilima duser.
    /// </summary>
    [Fact]
    public void OtomatikHizliKipMfyiSecmez()
    {
        var options = new PlanOptions { TargetMb = 50, SpeedMode = SpeedMode.Fast };

        var plan = PlanCalculator.Build(Kaynak(), options, new Liste("h264_mf", "hevc_mf", "av1_mf", "libx264", "libx265", "libsvtav1"));

        Assert.NotEqual(EncoderVendor.MediaFoundation, CodecModel.Vendor(plan.Codec));
        Assert.False(CodecModel.IsHardware(plan.Codec));
    }

    [Fact]
    public void WindowsDisindaMfHicOnerilmez()
    {
        foreach (var codec in MfKodlayicilar)
        {
            Assert.False(CodecModel.IsOfferedOn(codec, windows: false));
            Assert.True(CodecModel.IsOfferedOn(codec, windows: true));
            Assert.False(PlanCalculator.IsLockableCodecOn(codec, windows: false));
            Assert.True(PlanCalculator.IsLockableCodecOn(codec, windows: true));
        }

        var disarida = FfmpegArguments.OfferedCodecs(windows: false);
        var icerde = FfmpegArguments.OfferedCodecs(windows: true);
        Assert.DoesNotContain(disarida, c => c.EndsWith("_mf", StringComparison.Ordinal));
        Assert.Contains("libx264", disarida);
        Assert.Contains("h264_nvenc", disarida);
        foreach (var codec in MfKodlayicilar) Assert.Contains(codec, icerde);
        Assert.Equal(FfmpegArguments.KnownCodecs.Count, icerde.Count);
        Assert.Equal(FfmpegArguments.KnownCodecs.Count - MfKodlayicilar.Length, disarida.Count);
        Assert.True(PlanCalculator.IsLockableCodecOn("libx264", windows: false));
    }

    [Fact]
    public void PlanCozumleyiciMfyiYalnizWindowstaVeBitHiziyleKabulEder()
    {
        const string mf = """{"codec":"h264_mf","mode":"2pass","videoBitrateK":1500,"preset":"default","width":1920,"height":1080,"fps":30}""";
        const string crf = """{"codec":"h264_mf","mode":"crf","crf":24,"preset":"default","width":1920,"height":1080,"fps":30}""";
        var options = new PlanOptions { TargetMb = 200 };

        var windows = PlanParser.Parse(mf, Kaynak(), options, windows: true);
        var disarida = PlanParser.Parse(mf, Kaynak(), options, windows: false);
        var crfli = PlanParser.Parse(crf, Kaynak(), options, windows: true);

        Assert.True(windows.Ok, string.Join("; ", windows.Errors));
        Assert.Equal("nv12", windows.Plan!.PixelFormat);
        Assert.False(disarida.Ok);
        Assert.Contains(disarida.Errors, e => e.Contains("Windows", StringComparison.Ordinal));
        Assert.False(crfli.Ok);
    }

    /// <summary>
    /// Her Windows koşucusunda koşan canli kol: yoklama karar verir (olculemedi kalmaz) ve
    /// kilitli MF plani yoklamanin cevabini izler — acilan makinede MF, acilmayanda ailenin
    /// yazilim kodlayicisi. CI koşucusunda donanim MFT'si yok; kol orada ret kolunu olcer.
    /// </summary>
    [FfmpegFact]
    public void YoklamaKararVerirVeKilitliPlanOnuIzler()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.False(PlanCalculator.IsLockableCodec("h264_mf"));
            return;
        }

        if (!EncoderCapabilities.Instance.HasEncoder("h264_mf"))
        {
            _cikti.WriteLine("h264_mf bu ffmpeg derlemesinde yok");
            Assert.Equal("libx264", PlanCalculator.Build(Kaynak(), new PlanOptions { TargetMb = 50, LockedCodec = "h264_mf" }, EncoderCapabilities.Instance).Codec);
            return;
        }

        var yoklama = EncoderCapabilities.Instance.Probe("h264_mf");
        _cikti.WriteLine($"h264_mf yoklama: {yoklama.State} {yoklama.ElapsedMs}ms");
        Assert.NotEqual(EncoderProbeState.Unmeasured, yoklama.State);

        var plan = PlanCalculator.Build(Kaynak(), new PlanOptions { TargetMb = 50, LockedCodec = "h264_mf" }, EncoderCapabilities.Instance);
        Assert.Equal(yoklama.Succeeded ? "h264_mf" : "libx264", plan.Codec);
    }

    /// <summary>
    /// Urun yolu uctan uca: 2 sn 320x240@30 gurultulu kaynak, kilitli h264_mf plani,
    /// <see cref="EncodeRunner"/> tavani. Teslim edilen dosya hedefi asmaz ve gercekten H.264'tur.
    /// </summary>
    [MediaFoundationFact]
    public async Task KilitliMfHedefBoyutuAsmadanTeslimEder()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "media-foundation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mkv");
            var hazirlik = await EncodeRunner.RunCommandAsync(
                new[]
                {
                    "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=30,noise=alls=10:allf=t",
                    "-t", "2", "-threads", "2", "-c:v", "ffv1", "-pix_fmt", "yuv420p", kaynak
                },
                durationSeconds: 2, progress: null, stage: "hazirlik", spanFrom: 0.0, spanTo: 1.0,
                ct: CancellationToken.None);
            Assert.Equal(0, hazirlik.ExitCode);

            var info = await FfprobeClient.ProbeAsync(kaynak);
            const double hedefMb = 0.08;
            var options = new PlanOptions { TargetMb = hedefMb, LockedCodec = MediaFoundationFactAttribute.Codec, AllowResolutionDrop = false, AllowFpsDrop = false };
            var plan = PlanCalculator.Build(info, options, EncoderCapabilities.Instance);
            Assert.Equal(MediaFoundationFactAttribute.Codec, plan.Codec);

            var cikti = Path.Combine(klasor, "cikti.mp4");
            var sonuc = await new EncodeRunner().RunAsync(info, plan, cikti, hedefMb, null, CancellationToken.None, FillPolicy.FillTarget);

            _cikti.WriteLine($"plan {plan.Codec} {plan.Mode} {plan.VideoBitrateK}k {plan.Width}x{plan.Height}@{plan.Fps:0.##} {plan.PixelFormat}");
            foreach (var adim in sonuc.Trace ?? Array.Empty<EncodeAttempt>())
                _cikti.WriteLine($"  deneme {adim.Number}: {adim.Branch} {adim.VideoBitrateK}k {adim.ActualMb:0.####} MB");
            _cikti.WriteLine($"sonuc {sonuc.OutputMb:0.####} MB / hedef {hedefMb} MB, deneme {sonuc.Attempts}");

            Assert.True(sonuc.Success, sonuc.Error);
            Assert.True(sonuc.OutputMb <= hedefMb, $"{sonuc.OutputMb:0.####} MB hedefi ({hedefMb} MB) asti");
            var cikan = await FfprobeClient.ProbeAsync(cikti);
            Assert.Equal("h264", cikan.VideoCodec);
        }
        finally { try { Directory.Delete(klasor, true); } catch { } }
    }
}
