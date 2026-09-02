using System.Linq;
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

        const double lowAnchor = 70.0;
        const double highAnchor = 85.0;

        var prior = PlanCalculator.BuildDetailed(info, options, complexity).PredictedQuality;
        var low = PlanCalculator.BuildDetailed(info, options, complexity.WithProbeQuality(new[] { lowAnchor })).PredictedQuality;
        var high = PlanCalculator.BuildDetailed(info, options, complexity.WithProbeQuality(new[] { highAnchor })).PredictedQuality;

        Assert.Equal(highAnchor - lowAnchor, high - low, 6);
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
        var lower = new QualitySample(0.045, 88.0);
        var upper = new QualitySample(0.18, 96.0);
        var flat = complexity.WithMeasuredQuality(new[] { lower, upper });
        var halvings = Math.Log2(upper.Bppf / lower.Bppf);

        Assert.True(flat.Level.SlopeMeasured);
        Assert.Equal((upper.VmafNeg - lower.VmafNeg) / halvings, flat.Level.PerHalving, 6);
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

        const double trueBppf = 0.02;

        PlanResult At(double bias)
        {
            var complexity = ComplexityProfile.FromProbe(trueBppf * bias, 0.012 * bias, 6, 288, bias);
            Assert.Equal(trueBppf, complexity.ReferenceBppf, 9);
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

            Assert.True(bppf >= floor || result.Advice.Notes.Contains(AdviceCode.TargetBelowCodecFloor),
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

    [Fact]
    public void EveryFloorReasonQuotesTheFloorThatWasActuallyApplied()
    {
        var info = LongHdrCapture();
        var profile = MeasuredProfile();
        var availability = new FakeAvailability("libx264", "libx265", "libsvtav1", "av1_nvenc");
        var quoted = 0;

        foreach (var targetMb in new[] { 3.0, 5.0, 12.0, 25.0, 50.0, 80.0, 117.0, 250.0 })
        {
            var result = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = targetMb }, profile, availability);
            var plan = result.Plan;

            if (result.Advice.Notes.Contains(AdviceCode.TargetBelowCodecFloor) && plan.VideoBitrateK >= CodecModel.UsableBitrateK(plan.Codec, plan.Width, plan.Height, plan.Fps))
            {
                var applied = result.Profile.FloorBppf(plan.Codec, plan.Fps, info.Fps);
                Assert.Contains(applied.ToString("0.0000"), plan.Reason);
                quoted++;
            }

            if (result.Advice.Notes.Contains(AdviceCode.FrameRateCutForFloor))
            {
                var atSourceFps = result.Profile.FloorBppf(plan.Codec, info.Fps, info.Fps);
                Assert.Contains(atSourceFps.ToString("0.0000"), plan.Reason);
                quoted++;
            }
        }

        Assert.True(quoted >= 2, $"The sweep never hit a floor reason ({quoted} quotes), so this measure proved nothing.");
    }

    private static ComplexityProfile ComplaintProfile() => new()
    {
        ReferenceBppf = 0.06244,
        Measured = true,
        MotionExponent = 1.163,
        MotionMeasured = true,
        DetailExponent = 0.55,
        SampledSeconds = 6,
        SampledFrames = 360
    };

    private static ComplexityProfile GridProfile(double fullScaleBppf, double halfScaleBppf)
        => ComplexityProfile.FromProbe(fullScaleBppf, halfScaleBppf, 6, 288);

    private static LayoutScoreParts ScoreAt(ComplexityProfile complexity, int width, int height, double videoK)
        => ScoreAt(complexity, "libsvtav1", width, height, videoK);

    private static LayoutScoreParts ScoreAt(ComplexityProfile complexity, string codec, int width, int height, double videoK)
        => PlanCalculator.ScoreLayout(complexity, codec, videoK, width, height, 60.0, 60.0, 1080, CompressionRegime.Aggressive);

    private static LayoutScoreParts[] Ladder(ComplexityProfile complexity, string codec, double videoK)
        => new[] { (1920, 1080), (1600, 900), (1280, 720), (960, 540) }
            .Select(l => ScoreAt(complexity, codec, l.Item1, l.Item2, videoK))
            .ToArray();

    [Theory]
    [InlineData(0.08, 0.05)]
    [InlineData(0.08, 0.11)]
    public void TheRateHalfOfTheScoreDoesNotMoveWhenOnlyTheResolutionChanges(double fullScaleBppf, double halfScaleBppf)
    {
        var complexity = GridProfile(fullScaleBppf, halfScaleBppf);
        const double videoK = 2000;

        var native = ScoreAt(complexity, 1920, 1080, videoK);
        var threeQuarters = ScoreAt(complexity, 1440, 810, videoK);
        var half = ScoreAt(complexity, 960, 540, videoK);

        Assert.NotEqual(0.0, complexity.DetailExponent, 3);
        Assert.Equal(native.Rate, threeQuarters.Rate, 6);
        Assert.Equal(native.Rate, half.Rate, 6);
    }

    [Theory]
    [InlineData(0.08, 0.05)]
    [InlineData(0.08, 0.11)]
    public void DroppingResolutionAtAFixedBitrateAlwaysCostsScore(double fullScaleBppf, double halfScaleBppf)
    {
        var complexity = GridProfile(fullScaleBppf, halfScaleBppf);
        const double videoK = 2000;

        var ladder = new[] { (1920, 1080), (1600, 900), (1280, 720), (960, 540) }
            .Select(l => ScoreAt(complexity, l.Item1, l.Item2, videoK))
            .ToArray();

        for (var i = 1; i < ladder.Length; i++)
            Assert.True(ladder[i].Score < ladder[i - 1].Score,
                $"Layout {i} scores {ladder[i].Score:0.###} against {ladder[i - 1].Score:0.###} one step above it. At a fixed bitrate the measured grid never rewarded the smaller frame: on the moving source 1920x1080@60 read 45,27 VMAF-NEG against 43,86 at 960x540@60, on the still source 81,29 against 64,30. A score that rises as the frame shrinks is the defect T107 measured.");
    }

    [Theory]
    [InlineData(0.08, 0.05)]
    [InlineData(0.08, 0.11)]
    public void TheHardwareArmKeepsTheScaleCreditTheSoftwareArmGaveUp(double fullScaleBppf, double halfScaleBppf)
    {
        var complexity = GridProfile(fullScaleBppf, halfScaleBppf);
        const double videoK = 800;

        var hardware = Ladder(complexity, "av1_nvenc", videoK);
        var software = Ladder(complexity, "libsvtav1", videoK);

        Assert.NotEqual(0.0, complexity.DetailExponent, 3);

        for (var i = 1; i < hardware.Length; i++)
            Assert.True(hardware[i].Score > hardware[i - 1].Score,
                $"Hardware layout {i} scores {hardware[i].Score:0.###} against {hardware[i - 1].Score:0.###} one step above it. On av1_nvenc the measured grid at 800k rose as the frame shrank - 1920x1080@60 read 31,842 VMAF-NEG, 1600x900 37,097, 1280x720 38,730, 960x540 40,036 - because the encoder cannot deliver 1080p60 below about 624 kbps. The scale credit is right on this arm and must survive.");

        for (var i = 1; i < software.Length; i++)
            Assert.True(software[i].Score < software[i - 1].Score,
                $"Software layout {i} scores {software[i].Score:0.###} against {software[i - 1].Score:0.###} one step above it. On libsvtav1 the same grid fell as the frame shrank (moving source 45,266 at 1920x1080@60 against 43,856 at 960x540@60; still source 81,288 against 64,300). One condition serves both arms, so a change that fixes software by breaking hardware fails here.");
    }

    [Fact]
    public void TheOnlyThingThatSeparatesTwoResolutionsIsTheScalePenalty()
    {
        var complexity = GridProfile(0.08, 0.05);
        const double videoK = 2000;

        var native = ScoreAt(complexity, 1920, 1080, videoK);
        var half = ScoreAt(complexity, 960, 540, videoK);

        Assert.Equal(0.0, native.ScalePenalty, 9);
        Assert.Equal(native.Score - half.Score, half.ScalePenalty - native.ScalePenalty, 6);
    }

    [Fact]
    public void TheFloorAdmitsTheLayoutThatWonTheMeasurementAndStillRejectsTheSaturatedOne()
    {
        var info = LongHdrCapture();
        var complaint = ComplaintProfile();
        const int width = 1280;
        const int height = 720;
        const double fps = 60.0;
        var pixelRateK = (double)width * height * fps / 1000.0;

        Assert.True(
            PlanCalculator.LayoutClearsFloor(complaint, "av1_nvenc", 790.0, width, height, fps, info.Fps),
            $"1280x720@60 at 790k won the five-layout measurement at 117 MB; a floor that rejects it throws the winner away before it is ever scored. bppf={PlanCalculator.BitsPerPixel(790.0, width, height, fps):0.00000} floor={complaint.FloorBppf("av1_nvenc", fps, info.Fps):0.00000} usable={CodecModel.UsableBitrateK("av1_nvenc", width, height, fps)}");

        Assert.False(
            PlanCalculator.LayoutClearsFloor(complaint, "libsvtav1", 0.0052 * pixelRateK, width, height, fps, info.Fps),
            "At 0,0052 bppf the software arm has already saturated - p10 stops falling, over three per cent of frames score under 5 VMAF-NEG - so the floor must still reject it.");
    }

    private sealed class SurucusuzMakine : IEncoderAvailability
    {
        private readonly HashSet<string> _built;
        private readonly HashSet<string> _works;
        private readonly Dictionary<string, int> _yoklama = new(StringComparer.OrdinalIgnoreCase);

        public SurucusuzMakine(string[] built, string[] works)
        {
            _built = new HashSet<string>(built, StringComparer.OrdinalIgnoreCase);
            _works = new HashSet<string>(works, StringComparer.OrdinalIgnoreCase);
        }

        public bool HasEncoder(string name) => _built.Contains(name);

        public bool WorksAsEncoder(string codec)
        {
            _yoklama[codec] = YoklamaSayisi(codec) + 1;
            return _works.Contains(codec);
        }

        public int YoklamaSayisi(string codec) => _yoklama.TryGetValue(codec, out var n) ? n : 0;
    }

    private sealed class OlculmemisMakine : IEncoderAvailability, IEncoderMeasurementState
    {
        private readonly HashSet<string> _built;
        private readonly HashSet<string> _olculen;
        private readonly HashSet<string> _works;
        private readonly Dictionary<string, int> _yoklama = new(StringComparer.OrdinalIgnoreCase);

        public OlculmemisMakine(string[] built, string[] olculen, string[] works)
        {
            _built = new HashSet<string>(built, StringComparer.OrdinalIgnoreCase);
            _olculen = new HashSet<string>(olculen, StringComparer.OrdinalIgnoreCase);
            _works = new HashSet<string>(works, StringComparer.OrdinalIgnoreCase);
        }

        public bool HasEncoder(string name) => _built.Contains(name);

        public bool WorksAsEncoder(string codec)
        {
            _yoklama[codec] = YoklamaSayisi(codec) + 1;
            return _works.Contains(codec);
        }

        public bool IsMeasured(string codec) => _olculen.Contains(codec);
        public bool IsHdr10Measured(string codec) => true;
        public int YoklamaSayisi(string codec) => _yoklama.TryGetValue(codec, out var n) ? n : 0;
    }

    private static readonly string[] NvencliDerleme =
    {
        "libx264", "libx265", "libsvtav1", "h264_nvenc", "hevc_nvenc", "av1_nvenc"
    };

    private static readonly string[] YalnizYazilim = { "libx264", "libx265" };

    [Fact]
    public void MaxCompressionListedeOlupCalismayanKodlayiciyiSecmiyor()
    {
        var makine = new SurucusuzMakine(built: NvencliDerleme, works: YalnizYazilim);
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.MaxCompression, SpeedMode = SpeedMode.Quality };

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, makine);

        Assert.Equal("libx265", result.Plan.Codec);
        Assert.Equal(1, makine.YoklamaSayisi("libsvtav1"));
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback && n.RequestedCodec == "libsvtav1" && n.FallbackCodec == "libx265");
    }

    [Fact]
    public void FastTercihiListedeOlupCalismayanDonanimiSecmiyor()
    {
        var makine = new SurucusuzMakine(built: NvencliDerleme, works: YalnizYazilim);
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Fast, SpeedMode = SpeedMode.Quality };

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, makine);

        Assert.Equal("libx264", result.Plan.Codec);
        Assert.Equal(1, makine.YoklamaSayisi("h264_nvenc"));
        Assert.Contains(AdviceCode.EncoderFallback, result.Advice.Notes);
    }

    [Fact]
    public void CalisanKodlayiciSecilmeyeDevamEdiyor()
    {
        var makine = new SurucusuzMakine(built: NvencliDerleme, works: NvencliDerleme);
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Fast, SpeedMode = SpeedMode.Quality };

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, makine);

        Assert.Equal("h264_nvenc", result.Plan.Codec);
        Assert.DoesNotContain(AdviceCode.EncoderFallback, result.Advice.Notes);
    }

    [Fact]
    public void DerlemeListesindeOlmayanKodlayiciHicYoklanmiyor()
    {
        var makine = new SurucusuzMakine(built: YalnizYazilim, works: YalnizYazilim);
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.MaxCompression, SpeedMode = SpeedMode.Quality };

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, makine);

        Assert.Equal("libx265", result.Plan.Codec);
        Assert.Equal(0, makine.YoklamaSayisi("libsvtav1"));
    }

    [Fact]
    public void OlculmemisKodlayiciYoklanmiyorGeciciCevapVeriliyor()
    {
        var makine = new OlculmemisMakine(built: NvencliDerleme, olculen: YalnizYazilim, works: YalnizYazilim);
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Fast, SpeedMode = SpeedMode.Quality };

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, makine);

        Assert.Equal("h264_nvenc", result.Plan.Codec);
        Assert.True(result.HardwareNotMeasured);
        Assert.Equal(0, makine.YoklamaSayisi("h264_nvenc"));
    }

    [Fact]
    public void OlculmusKodlayiciIcinGecicilikIsaretiKonmuyor()
    {
        var makine = new OlculmemisMakine(built: NvencliDerleme, olculen: NvencliDerleme, works: YalnizYazilim);
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Fast, SpeedMode = SpeedMode.Quality };

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, makine);

        Assert.Equal("libx264", result.Plan.Codec);
        Assert.False(result.HardwareNotMeasured);
        Assert.Equal(1, makine.YoklamaSayisi("h264_nvenc"));
    }

    [Fact]
    public void AvailabilityNullIkenTercihEdilenKodlayiciDonuyor()
    {
        var maxOptions = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.MaxCompression, SpeedMode = SpeedMode.Quality };
        var fastOptions = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Fast, SpeedMode = SpeedMode.Quality };
        var uyumluOptions = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Quality };

        var maxResult = PlanCalculator.BuildDetailed(SampleInfo(), maxOptions, null, null);
        var fastResult = PlanCalculator.BuildDetailed(SampleInfo(), fastOptions, null, null);
        var uyumluResult = PlanCalculator.BuildDetailed(SampleInfo(), uyumluOptions, null, null);

        Assert.Equal("libsvtav1", maxResult.Plan.Codec);
        Assert.Equal("h264_nvenc", fastResult.Plan.Codec);
        Assert.Equal("libx264", uyumluResult.Plan.Codec);
        Assert.DoesNotContain(AdviceCode.EncoderFallback, maxResult.Advice.Notes);
        Assert.DoesNotContain(AdviceCode.EncoderFallback, fastResult.Advice.Notes);
        Assert.False(maxResult.HardwareNotMeasured);
        Assert.False(fastResult.HardwareNotMeasured);
    }

    [Fact]
    public void CompatibleYoluHicYoklamiyor()
    {
        var makine = new SurucusuzMakine(built: NvencliDerleme, works: YalnizYazilim);
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Quality };

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, makine);

        Assert.Equal("libx264", result.Plan.Codec);
        foreach (var codec in NvencliDerleme)
            Assert.Equal(0, makine.YoklamaSayisi(codec));
    }

    [Fact]
    public void HizliKipDonanimYoklamasinaBagliKaliyor()
    {
        var makine = new SurucusuzMakine(built: NvencliDerleme, works: YalnizYazilim);
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, SpeedMode = SpeedMode.Fast };

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, makine);

        Assert.False(CodecModel.IsHardware(result.Plan.Codec));
        Assert.Equal(1, makine.YoklamaSayisi("av1_nvenc"));
        Assert.Contains(AdviceCode.EncoderFallback, result.Advice.Notes);
    }
}
