using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class PlanCalculatorTests
{
    private sealed class FakeAvailability : IEncoderAvailability
    {
        private readonly HashSet<string> _encoders;
        public FakeAvailability(params string[] encoders) => _encoders = new HashSet<string>(encoders, StringComparer.OrdinalIgnoreCase);
        public bool HasEncoder(string name) => _encoders.Contains(name);
        public bool WorksAsEncoder(string codec) => _encoders.Contains(codec);
    }

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

    [Fact]
    public void MissingLibsvtav1FallsBackToLibx265AndExplainsWhy()
    {
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.MaxCompression };
        var availability = new FakeAvailability("libx264", "libx265", "h264_nvenc");

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, availability);

        Assert.Equal("libx265", result.Plan.Codec);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback && n.RequestedCodec == "libsvtav1" && n.FallbackCodec == "libx265");
        Assert.Contains("libx265", result.Plan.Reason);
    }

    [Fact]
    public void Libsvtav1AvailablePicksItForMaxCompression()
    {
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.MaxCompression };
        var availability = new FakeAvailability("libx264", "libx265", "libsvtav1");

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, availability);

        Assert.Equal("libsvtav1", result.Plan.Codec);
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback);
    }

    [Fact]
    public void CorrectionReservesFixedAudioBeforeScalingVideo()
    {
        var plan = new EncodePlan
        {
            Mode = "crf",
            Crf = 23,
            VideoBitrateK = 1800,
            AudioBitrateK = 128,
            AudioCodec = "aac",
            Width = 1920,
            Height = 1080,
            Fps = 30,
            Codec = "libx264",
            Preset = "slow"
        };

        var corrected = PlanCalculator.Correct(plan, actualMb: 30, targetMb: 20, durationSeconds: 120);

        Assert.Equal(EncodeMode.TwoPass, corrected.ModeEnum);
        Assert.Null(corrected.Crf);
        Assert.True(corrected.VideoBitrateK < 1200, $"Expected the correction to reserve audio before scaling video, staying below the audio-blind whole-file proportional result of 1200k, got {corrected.VideoBitrateK}k.");
        Assert.True(PlanCalculator.EstimatedMb(corrected, 120) <= 20 * 0.94 + 0.05);
    }

    [Fact]
    public void BuildDetailedProducesReasonCodesAlongsideReasonText()
    {
        var info = new MediaInfo
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

        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing };
        var result = PlanCalculator.BuildDetailed(info, options, null);

        Assert.NotEmpty(result.Plan.ReasonCodes);
        Assert.False(string.IsNullOrWhiteSpace(result.Plan.Reason));
    }

    private static ComplexityProfile MeasuredComplexity(double referenceBppf)
        => ComplexityProfile.FromProbe(referenceBppf, referenceBppf * 0.6, 6, 288);

    [Fact]
    public void MeasuredQualityPointsMoveThePredictionOneForOne()
    {
        var info = SampleInfo();
        var options = new PlanOptions { TargetMb = 8, Intent = Intent.Sharing };
        var complexity = MeasuredComplexity(0.09);

        var prior = PlanCalculator.BuildDetailed(info, options, complexity).PredictedQuality;
        var low = PlanCalculator.BuildDetailed(info, options, complexity.WithProbeQuality(new[] { 70.0 })).PredictedQuality;
        var high = PlanCalculator.BuildDetailed(info, options, complexity.WithProbeQuality(new[] { 85.0 })).PredictedQuality;

        Assert.Equal(15.0, high - low, 6);
        Assert.True(Math.Abs(prior - low) > 1.0, $"Olculen 70 noktasi tahmini oynatmadi: prior {prior:0.###}, olculen {low:0.###}.");
        Assert.True(Math.Abs(prior - high) > 1.0, $"Olculen 85 noktasi tahmini oynatmadi: prior {prior:0.###}, olculen {high:0.###}.");
    }

    [Fact]
    public void WithoutMeasuredPointsThePredictionFallsBackToThePrior()
    {
        var info = SampleInfo();
        var options = new PlanOptions { TargetMb = 8, Intent = Intent.Sharing };
        var complexity = MeasuredComplexity(0.09);

        var prior = PlanCalculator.BuildDetailed(info, options, complexity).PredictedQuality;
        var dropped = complexity.WithProbeQuality(new[] { 70.0 }).WithMeasuredQuality(Array.Empty<QualitySample>());

        Assert.False(dropped.QualityMeasured);
        Assert.Equal(prior, PlanCalculator.BuildDetailed(info, options, dropped).PredictedQuality, 9);
    }

    [Fact]
    public void TwoSeparatedQualityPointsMeasureTheSlopeInsteadOfAssumingIt()
    {
        var complexity = MeasuredComplexity(0.09);
        var flat = complexity.WithMeasuredQuality(new[] { new QualitySample(0.045, 88.0), new QualitySample(0.18, 96.0) });

        Assert.True(flat.Level.SlopeMeasured);
        Assert.Equal(4.0, flat.Level.PerHalving, 6);
        Assert.False(complexity.WithProbeQuality(new[] { 88.0, 96.0 }).Level.SlopeMeasured);
    }

    [Fact]
    public void MeasuredQualityStopLeavesTheRestOfTheBudgetUnspent()
    {
        var info = SampleInfo();
        var options = new PlanOptions { TargetMb = 60, Intent = Intent.Sharing };
        var complexity = MeasuredComplexity(0.02);

        var unmeasured = PlanCalculator.BuildDetailed(info, options, complexity);
        var measured = PlanCalculator.BuildDetailed(info, options, complexity.WithProbeQuality(new[] { 96.0 }));

        Assert.True(unmeasured.Estimate.ExpectedMb >= 55.0,
            $"Olcumsuz plan hedefi doldurmadi: {unmeasured.Estimate.ExpectedMb:0.00} MB.");
        Assert.True(measured.Estimate.ExpectedMb <= 30.0,
            $"Olculen kalite durdurma kisiti butceyi harcamayi surdurdu: {measured.Estimate.ExpectedMb:0.00} MB.");
        Assert.True(measured.Plan.VideoBitrateK < unmeasured.Plan.VideoBitrateK,
            $"Durdurma kisiti harcamayi dusurmedi: olculen {measured.Plan.VideoBitrateK}k, olcumsuz {unmeasured.Plan.VideoBitrateK}k.");
        Assert.True(measured.Estimate.ExpectedMb <= options.TargetMb,
            $"Hedef boyut garantisi kirildi: {measured.Estimate.ExpectedMb:0.00} MB > {options.TargetMb} MB.");
    }

    [Fact]
    public void TheStopSitsAtTheOperatingPointTheMeasurementWasTakenAt()
    {
        var info = SampleInfo();
        var options = new PlanOptions { TargetMb = 60, Intent = Intent.Sharing, AllowResolutionDrop = false, AllowFpsDrop = false };

        PlanResult At(double bias)
        {
            var complexity = ComplexityProfile.FromProbe(0.02 * bias, 0.012 * bias, 6, 288, bias);
            Assert.Equal(0.02, complexity.ReferenceBppf, 9);
            return PlanCalculator.BuildDetailed(info, options, complexity.WithProbeQuality(new[] { 90.0 }));
        }

        var atProbe = At(1.0);
        var atHalfTheDensity = At(0.5);

        var ratio = (double)atProbe.Plan.VideoBitrateK / atHalfTheDensity.Plan.VideoBitrateK;
        Assert.True(Math.Abs(ratio - 2.0) < 0.01,
            $"Durdurma noktasi olcumun alindigi isletme noktasini takip etmiyor: {atProbe.Plan.VideoBitrateK}k / {atHalfTheDensity.Plan.VideoBitrateK}k = {ratio:0.###}, beklenen 2.");
        Assert.True(atProbe.Estimate.ExpectedMb <= options.TargetMb && atHalfTheDensity.Estimate.ExpectedMb <= options.TargetMb,
            $"Hedef boyut garantisi kirildi: {atProbe.Estimate.ExpectedMb:0.00} MB / {atHalfTheDensity.Estimate.ExpectedMb:0.00} MB.");
    }

    [Fact]
    public void QualitySearchStaysInsideItsEvaluationBudget()
    {
        var info = new MediaInfo
        {
            FilePath = "huge.mkv",
            FileSizeBytes = 4L * 1024 * 1024 * 1024 * 1024,
            DurationSeconds = 28800,
            Width = 3840,
            Height = 2160,
            Fps = 60,
            VideoCodec = "h264",
            TotalBitrateBps = 1_200_000_000,
            AudioCodec = "aac",
            AudioBitrateBps = 128_000,
            AudioChannels = 2
        };

        var ceilingMb = PlanCalculator.QualityCeilingTargetMb(info);
        var span = ceilingMb / PlanCalculator.QualityFloorTargetMb(info);
        var unbounded = (int)Math.Ceiling(Math.Log(span) / Math.Log(1.005));
        Assert.True(unbounded > 1400, $"Bu kaynak siniri zorlamiyor: sinirsiz tarama {unbounded} adim.");

        var options = new PlanOptions { Intent = Intent.Sharing };
        var atCeiling = PlanCalculator.BuildDetailed(info, new PlanOptions { Intent = Intent.Sharing, TargetMb = ceilingMb }, null);
        var result = PlanCalculator.TargetMbForQuality(info, options, atCeiling.PredictedQuality - 0.01);

        Assert.Equal(QualityTargetBound.Matched, result.Bound);
        Assert.True(result.Evaluations <= 1400,
            $"Arama {result.Evaluations} BuildDetailed cagrisi surdu, olculen 1400 butcesinin ustunde.");
    }

    [Fact]
    public void MotionCutIsCalledCheapOnlyWhenHalvingTheFrameRateReallySavesBits()
    {
        var info = SampleInfo();
        var options = new PlanOptions { TargetMb = 20, Intent = Intent.Sharing };

        PlanResult WithMotion(double exponent)
        {
            var full = 0.08;
            var profile = ComplexityProfile.FromProbe(full, full * 0.6, 6, 288, 1.0, WindowBiasSource.Scan, full * Math.Pow(2, exponent));
            Assert.True(profile.MotionMeasured);
            return PlanCalculator.BuildDetailed(info, options, profile);
        }

        var quarter = WithMotion(0.6408);
        var median = WithMotion(0.8706);

        Assert.Contains(AdviceCode.MotionCutIsCheap, quarter.Advice.Notes);
        Assert.DoesNotContain(AdviceCode.MotionCutIsCheap, median.Advice.Notes);
    }

    [Fact]
    public void ThePlanReadsTheProfileWithTheContainerCostAlreadyTakenOut()
    {
        var info = new MediaInfo
        {
            FilePath = "dusuk.mp4",
            FileSizeBytes = 40L * 1024 * 1024,
            DurationSeconds = 300,
            Width = 320,
            Height = 180,
            Fps = 48,
            VideoCodec = "h264",
            TotalBitrateBps = 1_100_000,
            AudioCodec = "aac",
            AudioBitrateBps = 128_000,
            AudioChannels = 2
        };
        var options = new PlanOptions { TargetMb = 12, Intent = Intent.Sharing, FillPolicy = FillPolicy.QualityCeiling };

        var pixels = (double)info.Width * info.Height;
        var measuredBppf = 290.0 * 8.0 / pixels;
        var contaminated = ComplexityProfile.FromProbe(measuredBppf, measuredBppf * 0.6, 6, 288, 1.0);
        var cleaned = contaminated.WithoutSampleContainerBias(info.Width, info.Height);
        var frozen = contaminated with { SampleContainerBiasRemoved = true };

        Assert.True(contaminated.ReferenceBppf / cleaned.ReferenceBppf > 1.04,
            $"Kirlenme olcusuz kaldi: {contaminated.ReferenceBppf:0.000000} / {cleaned.ReferenceBppf:0.000000}.");

        var fromContaminated = PlanCalculator.BuildDetailed(info, options, contaminated);
        var fromCleaned = PlanCalculator.BuildDetailed(info, options, cleaned);
        var fromFrozen = PlanCalculator.BuildDetailed(info, options, frozen);

        Assert.Equal(fromCleaned.Plan.VideoBitrateK, fromContaminated.Plan.VideoBitrateK);
        Assert.True(fromFrozen.Plan.VideoBitrateK > fromContaminated.Plan.VideoBitrateK * 1.04,
            $"Kirli profil planda ayni sonucu verdi: {fromFrozen.Plan.VideoBitrateK}k / {fromContaminated.Plan.VideoBitrateK}k.");
    }

    private static MediaInfo LongHdrCapture() => new()
    {
        FilePath = "uzun-hdr.mp4",
        FileSizeBytes = 1_729_085_563L,
        DurationSeconds = 1036.17,
        Width = 1920,
        Height = 1080,
        Fps = 60,
        VideoCodec = "hevc",
        TotalBitrateBps = 13_350_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    private static ComplexityProfile MeasuredProfile() => new()
    {
        ReferenceBppf = 0.1264,
        Measured = true,
        MotionExponent = 0.871,
        MotionMeasured = true,
        DetailExponent = 0.55,
        SampledSeconds = 6,
        SampledFrames = 360
    };

    [Fact]
    public void NoTargetEverLandsUnderTheFloorWithoutSayingSo()
    {
        var info = LongHdrCapture();
        var profile = MeasuredProfile();
        var availability = new FakeAvailability("libx264", "libx265", "libsvtav1", "av1_nvenc");

        foreach (var targetMb in new[] { 5.0, 12.0, 25.0, 50.0, 80.0, 117.0, 250.0, 600.0 })
        {
            var result = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = targetMb }, profile, availability);
            var plan = result.Plan;
            var pixelRate = (double)plan.Width * plan.Height * plan.Fps;
            var bppf = plan.VideoBitrateK * 1000.0 / pixelRate;
            var floor = result.Profile.FloorBppf(plan.Codec, plan.Fps, info.Fps);

            Assert.True(bppf + 1000.0 / pixelRate >= floor || result.Advice.Notes.Contains(AdviceCode.TargetBelowCodecFloor),
                $"{targetMb:0.#} MB -> {plan.Codec} {plan.Width}x{plan.Height}@{plan.Fps:0.##} at {bppf:0.0000} bppf, under the {floor:0.0000} floor, and the plan says nothing.");
        }
    }

    [Fact]
    public void TheHardwareFloorRejectsALayoutTheSoftwareFloorAccepts()
    {
        var profile = MeasuredProfile();
        const int width = 1280;
        const int height = 720;
        const double fps = 60.0;

        var softFloor = profile.FloorBppf("libsvtav1", fps, fps);
        var hardFloor = profile.FloorBppf("av1_nvenc", fps, fps);
        var inTheGap = Math.Sqrt(softFloor * hardFloor);
        var justOverTheHardFloor = hardFloor * 1.02;

        var gapK = inTheGap * width * height * fps / 1000.0;
        var overK = justOverTheHardFloor * width * height * fps / 1000.0;

        Assert.True(PlanCalculator.LayoutClearsFloor(profile, "libsvtav1", gapK, width, height, fps, fps),
            $"The software floor rejected {inTheGap:0.00000} bppf, which sits above its own {softFloor:0.00000} floor.");
        Assert.False(PlanCalculator.LayoutClearsFloor(profile, "av1_nvenc", gapK, width, height, fps, fps),
            $"The hardware floor accepted {inTheGap:0.00000} bppf, under its {hardFloor:0.00000} floor: the hardware surcharge does nothing.");
        Assert.True(PlanCalculator.LayoutClearsFloor(profile, "av1_nvenc", overK, width, height, fps, fps),
            $"The hardware encoder rejected {justOverTheHardFloor:0.00000} bppf, over its {hardFloor:0.00000} floor: something other than the floor is refusing this layout.");
    }
}
