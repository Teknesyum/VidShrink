using System.Globalization;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class SpeedModeTests
{
    private sealed class FakeAvailability : IEncoderAvailability
    {
        private readonly HashSet<string> _encoders;
        public FakeAvailability(params string[] encoders) => _encoders = new HashSet<string>(encoders, StringComparer.OrdinalIgnoreCase);
        public bool HasEncoder(string name) => _encoders.Contains(name);
        public bool WorksAsEncoder(string codec) => _encoders.Contains(codec);
    }

    private static readonly string[] FastHardwareCodecs =
    {
        "av1_nvenc", "hevc_nvenc", "av1_qsv", "hevc_qsv", "av1_amf", "hevc_amf", "h264_nvenc"
    };

    private static MediaInfo SampleInfo() => new()
    {
        FilePath = "sample.mp4",
        FileSizeBytes = 500L * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    private static PlanOptions FastOptions(double targetMb) => new()
    {
        TargetMb = targetMb,
        Intent = Intent.Sharing,
        SpeedMode = SpeedMode.Fast
    };

    [Fact]
    public void FastModePicksAv1NvencWhenItWorks()
    {
        var availability = new FakeAvailability("libx264", "libx265", "h264_nvenc", "hevc_nvenc", "av1_nvenc");

        var result = PlanCalculator.BuildDetailed(SampleInfo(), FastOptions(25), null, availability);

        Assert.Equal("av1_nvenc", result.Plan.Codec);
        Assert.DoesNotContain(AdviceCode.EncoderFallback, result.Advice.Notes);
    }

    [Fact]
    public void FastModeWalksTheMeasuredOrderWhenAv1NvencIsMissing()
    {
        var availability = new FakeAvailability("libx264", "hevc_nvenc", "h264_nvenc");

        var result = PlanCalculator.BuildDetailed(SampleInfo(), FastOptions(25), null, availability);

        Assert.Equal("hevc_nvenc", result.Plan.Codec);
    }

    [Fact]
    public void FastModeKeepsH264NvencLastInTheOrder()
    {
        var availability = new FakeAvailability("libx264", "h264_nvenc", "hevc_amf");

        var result = PlanCalculator.BuildDetailed(SampleInfo(), FastOptions(25), null, availability);

        Assert.Equal("hevc_amf", result.Plan.Codec);
    }

    [Fact]
    public void FastModeWithoutAnyHardwareEncoderFallsBackToSoftwareAndSaysSo()
    {
        var availability = new FakeAvailability("libx264", "libx265");

        var result = PlanCalculator.BuildDetailed(SampleInfo(), FastOptions(25), null, availability);

        Assert.False(CodecModel.IsHardware(result.Plan.Codec));
        Assert.Contains(AdviceCode.EncoderFallback, result.Advice.Notes);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback && n.RequestedCodec == "av1_nvenc");
    }

    [Fact]
    public void Av1NvencDoesNotWarnAboutHardwareQualityOnATightTarget()
    {
        var availability = new FakeAvailability("libx264", "av1_nvenc");

        var result = PlanCalculator.BuildDetailed(SampleInfo(), FastOptions(8), null, availability);

        Assert.Equal("av1_nvenc", result.Plan.Codec);
        Assert.DoesNotContain(AdviceCode.HardwareCodecCostsQuality, result.Advice.Notes);
    }

    [Fact]
    public void Av1QsvDoesNotWarnAboutHardwareQualityOnATightTarget()
    {
        var availability = new FakeAvailability("libx264", "av1_qsv");

        var result = PlanCalculator.BuildDetailed(SampleInfo(), FastOptions(8), null, availability);

        Assert.Equal("av1_qsv", result.Plan.Codec);
        Assert.DoesNotContain(AdviceCode.HardwareCodecCostsQuality, result.Advice.Notes);
    }

    [Fact]
    public void H264NvencStillWarnsAboutHardwareQualityOnATightTarget()
    {
        var availability = new FakeAvailability("libx264", "h264_nvenc");

        var result = PlanCalculator.BuildDetailed(SampleInfo(), FastOptions(8), null, availability);

        Assert.Equal("h264_nvenc", result.Plan.Codec);
        Assert.Contains(AdviceCode.HardwareCodecCostsQuality, result.Advice.Notes);
    }

    [Fact]
    public void HevcNvencStillWarnsAboutHardwareQualityOnATightTarget()
    {
        var availability = new FakeAvailability("libx264", "hevc_nvenc");

        var result = PlanCalculator.BuildDetailed(SampleInfo(), FastOptions(8), null, availability);

        Assert.Equal("hevc_nvenc", result.Plan.Codec);
        Assert.Contains(AdviceCode.HardwareCodecCostsQuality, result.Advice.Notes);
    }

    [Fact]
    public void EveryHardwareEncoderGetsAPresetFfmpegAccepts()
    {
        foreach (var codec in FastHardwareCodecs)
        foreach (var target in new[] { 180.0, 25.0, 8.0 })
        {
            var options = new PlanOptions { TargetMb = target, Intent = Intent.Sharing, SpeedMode = SpeedMode.Fast };
            var availability = new FakeAvailability("libx264", codec);
            var plan = PlanCalculator.BuildDetailed(SampleInfo(), options, null, availability).Plan;

            Assert.Equal(codec, plan.Codec);
            Assert.True(FfmpegArguments.IsValidPreset(codec, plan.Preset), $"{codec} got preset {plan.Preset}, which ffmpeg does not accept.");
        }
    }

    [Fact]
    public void FastModeTakesNvencOneStepFasterThanQualityMode()
    {
        var availability = new FakeAvailability("libx264", "av1_nvenc");
        var info = SampleInfo();

        var fast = PlanCalculator.BuildDetailed(info, FastOptions(25), null, availability).Plan;
        var quality = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Fast }, null, availability).Plan;

        Assert.Equal("p5", fast.Preset);
        Assert.Equal("p6", FfmpegArguments.DefaultPreset("av1_nvenc"));
        Assert.True(FfmpegArguments.IsValidPreset("av1_nvenc", fast.Preset));
        Assert.NotEqual(fast.Preset, quality.Preset);
    }

    [Fact]
    public void UncalibratedHardwarePlanRaisesTheTwoPassTargetAndExplainsIt()
    {
        var availability = new FakeAvailability("libx264", "av1_nvenc");
        var info = SampleInfo();
        var options = new PlanOptions
        {
            TargetMb = 25,
            SpeedMode = SpeedMode.Fast,
            AllowResolutionDrop = false,
            AllowFpsDrop = false
        };

        var hardware = PlanCalculator.BuildDetailed(info, options, null, availability).Plan;
        var software = PlanCalculator.BuildDetailed(info, new PlanOptions
        {
            TargetMb = 25,
            AllowResolutionDrop = false,
            AllowFpsDrop = false
        }, null, availability).Plan;

        Assert.Equal(EncodeMode.TwoPass, hardware.ModeEnum);
        Assert.True(hardware.VideoBitrateK <= software.VideoBitrateK);
        Assert.Contains(hardware.ReasonCodes, n => n.Code == ReasonCode.HardwareBitrateBias && Math.Abs(n.Factor - CodecModel.HardwareBitrateYield) < 1e-9);
    }

    [Fact]
    public void CalibrationCarriesTheHardwareDeviationIntoTheProfile()
    {
        var info = SampleInfo();
        var profile = ComplexityProfile.FromProbe(0.12, 0.09, 6.0, 180);
        var signature = new CalibrationSignature { Codec = "av1_nvenc", Width = 1920, Height = 1080, Fps = 30, Scale = 1.0 };

        var lowCrf = CodecModel.ReferenceCrf("av1_nvenc");
        var highCrf = lowCrf + 4.0;
        var modelled = profile.RequiredBppf("av1_nvenc", 1.0, 30, 30);
        var lowBppf = modelled * 0.88;
        var highBppf = lowBppf / Math.Pow(2, 4.0 / CodecModel.CrfHalvingStep("av1_nvenc"));

        var calibrated = profile.Calibrate(signature, lowCrf, lowBppf, highCrf, highBppf, 30);

        Assert.True(calibrated.Calibrated);
        Assert.True(calibrated.AppliesTo("av1_nvenc", 1.0, 30));
        Assert.Equal(0.88, calibrated.LevelFactor, 3);
        Assert.Equal(CodecModel.CrfHalvingStep("av1_nvenc"), calibrated.HalvingStep, 3);
        Assert.Equal(lowBppf, calibrated.BppfAtCrf("av1_nvenc", lowCrf, 1.0, 30, 30), 6);
    }

    [Fact]
    public void CalibratedHardwarePlanDropsTheBlindBiasAndUsesTheMeasurement()
    {
        var info = SampleInfo();
        var availability = new FakeAvailability("libx264", "av1_nvenc");
        var options = new PlanOptions
        {
            TargetMb = 25,
            SpeedMode = SpeedMode.Fast,
            AllowResolutionDrop = false,
            AllowFpsDrop = false
        };

        var profile = ComplexityProfile.FromProbe(0.12, 0.09, 6.0, 180);
        var signature = new CalibrationSignature { Codec = "av1_nvenc", Width = 1920, Height = 1080, Fps = 30, Scale = 1.0 };
        var lowCrf = CodecModel.ReferenceCrf("av1_nvenc");
        var modelled = profile.RequiredBppf("av1_nvenc", 1.0, 30, 30);
        var lowBppf = modelled * 0.88;
        var highBppf = lowBppf / Math.Pow(2, 4.0 / CodecModel.CrfHalvingStep("av1_nvenc"));
        var calibrated = profile.Calibrate(signature, lowCrf, lowBppf, lowCrf + 4.0, highBppf, 30);

        var blind = PlanCalculator.BuildDetailed(info, options, profile, availability).Plan;
        var learned = PlanCalculator.BuildDetailed(info, options, calibrated, availability).Plan;

        Assert.Contains(blind.ReasonCodes, n => n.Code == ReasonCode.HardwareBitrateBias);
        Assert.DoesNotContain(learned.ReasonCodes, n => n.Code == ReasonCode.HardwareBitrateBias);
        Assert.True(learned.VideoBitrateK <= blind.VideoBitrateK);
    }

    [Fact]
    public void HardwareTwoPassPlanIsBuiltAsASingleRun()
    {
        var info = SampleInfo();
        var plan = new EncodePlan
        {
            Codec = "av1_nvenc",
            Mode = "2pass",
            VideoBitrateK = 3000,
            AudioCodec = "aac",
            AudioBitrateK = 128,
            Width = 1920,
            Height = 1080,
            Fps = 30,
            Preset = "p6"
        };

        Assert.False(FfmpegArguments.NeedsTwoPasses(plan.Codec));

        var args = FfmpegArguments.Build(info, plan, "out.mp4", 0, null);

        Assert.DoesNotContain("-pass", args);
        Assert.DoesNotContain("-passlogfile", args);
        Assert.Contains("-multipass", args);
        Assert.Contains("-c:a", args);
        Assert.Equal("out.mp4", args[^1]);
    }

    [Fact]
    public void SoftwareTwoPassPlanStillNeedsTwoRuns()
    {
        var plan = new EncodePlan { Codec = "libx264", Mode = "2pass", VideoBitrateK = 3000 };
        Assert.True(FfmpegArguments.NeedsTwoPasses(plan.Codec));
    }

    /// <summary>
    /// Altın parmak izi: hız kipi Quality'de bugünün planları. Liste T99 turu 2'de
    /// yenilendi, on sekiz satırın <b>on ikisi</b> değişti. Değişimin tamamı tek sabitten
    /// geliyor: <c>ComplexityProfile.DefaultMotionExponent</c> 0,25'ten 0,871'e çıktı
    /// (T89'un ölçtüğü on üç noktanın hepsi 0,25'in üstündeydi). 0,25'te kare hızını
    /// yarıya indirmek bitlerin %40,5'ini kurtarıyor gibi görünüyordu; ölçülen 0,871'de
    /// kurtardığı %8,6, yani kare hızı kesmek artık yer açmıyor ve arama kesmiyor.
    /// Taban değişikliği (av1 0,020 -> 0,0095, donanım çarpanı 1,25 -> 1,52) bu listede
    /// <b>tek satır</b> bile oynatmadı; ikisi ayrı ayrı geri alınıp ölçüldü.
    ///
    /// <para>Değişen satırlar, hepsi kare hızının kaynakta (30) kalması yönünde:</para>
    /// <list type="bullet">
    /// <item>Compatible|25 (iki satır): 806x454@20 -> 806x454@30</item>
    /// <item>Compatible|8 (iki satır): 576x324@6 -> 576x324@30</item>
    /// <item>MaxCompression|25 (iki satır): 806x454@20 -> 806x454@30</item>
    /// <item>MaxCompression|8|FillTarget: 614x346@6 -> 576x324@30</item>
    /// <item>MaxCompression|8|QualityCeiling: crf 32 / 433k / 614x346@6 -> 2pass / 470k / 576x324@30</item>
    /// <item>Auto|25 (iki satır): 806x454@20 -> 806x454@30</item>
    /// <item>Auto|8|FillTarget: 614x346@6 -> 576x324@30</item>
    /// <item>Auto|8|QualityCeiling: crf 32 / 433k / 614x346@6 -> 2pass / 470k / 576x324@30</item>
    /// </list>
    ///
    /// <para>Değişmeyen altı satır 180 MB hedefinin hepsi: o bütçede zaten kare hızı
    /// kesilmiyordu.</para>
    /// </summary>
    [Fact]
    public void QualityModeLeavesTodaysPlansUntouched()
    {
        var expected = new[]
        {
            "Compatible|180|FillTarget|libx264|2pass||12217|128|1190x670@30|slow",
            "Compatible|180|QualityCeiling|libx264|crf|20|11958|128|1190x670@30|slow",
            "Compatible|25|FillTarget|libx264|2pass||1567|128|806x454@30|slow",
            "Compatible|25|QualityCeiling|libx264|2pass||1567|128|806x454@30|slow",
            "Compatible|8|FillTarget|libx264|2pass||470|64|576x324@30|slow",
            "Compatible|8|QualityCeiling|libx264|2pass||470|64|576x324@30|slow",
            "MaxCompression|180|FillTarget|libsvtav1|2pass||12217|128|1498x842@30|6",
            "MaxCompression|180|QualityCeiling|libsvtav1|crf|32|11798|128|1498x842@30|6",
            "MaxCompression|25|FillTarget|libsvtav1|2pass||1567|128|806x454@30|6",
            "MaxCompression|25|QualityCeiling|libsvtav1|2pass||1567|128|806x454@30|6",
            "MaxCompression|8|FillTarget|libsvtav1|2pass||470|64|576x324@30|6",
            "MaxCompression|8|QualityCeiling|libsvtav1|2pass||470|64|576x324@30|6",
            "Auto|180|FillTarget|libx264|2pass||12217|128|1190x670@30|slow",
            "Auto|180|QualityCeiling|libx264|crf|20|11958|128|1190x670@30|slow",
            "Auto|25|FillTarget|libsvtav1|2pass||1567|128|806x454@30|6",
            "Auto|25|QualityCeiling|libsvtav1|2pass||1567|128|806x454@30|6",
            "Auto|8|FillTarget|libsvtav1|2pass||470|64|576x324@30|6",
            "Auto|8|QualityCeiling|libsvtav1|2pass||470|64|576x324@30|6"
        };

        var actual = new List<string>();
        foreach (var preference in new[] { CodecPreference.Compatible, CodecPreference.MaxCompression, CodecPreference.Auto })
        foreach (var target in new[] { 180.0, 25.0, 8.0 })
        foreach (var fill in new[] { FillPolicy.FillTarget, FillPolicy.QualityCeiling })
        {
            var options = new PlanOptions { TargetMb = target, Codec = preference, FillPolicy = fill };
            Assert.Equal(SpeedMode.Quality, options.SpeedMode);

            var plan = PlanCalculator.BuildDetailed(SampleInfo(), options, null).Plan;
            actual.Add(string.Join("|", new[]
            {
                preference.ToString(),
                target.ToString("0", CultureInfo.InvariantCulture),
                fill.ToString(),
                plan.Codec,
                plan.Mode,
                plan.Crf?.ToString(CultureInfo.InvariantCulture) ?? "",
                plan.VideoBitrateK.ToString(CultureInfo.InvariantCulture),
                plan.AudioBitrateK.ToString(CultureInfo.InvariantCulture),
                $"{plan.Width}x{plan.Height}@{plan.Fps.ToString("0.##", CultureInfo.InvariantCulture)}",
                plan.Preset
            }));
        }

        Assert.Equal(expected, actual);
    }

}
