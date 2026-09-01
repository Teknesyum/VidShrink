using System.Globalization;

namespace VidShrink.Core;

public enum FillPolicy { FillTarget, QualityCeiling }

public sealed class PlanOptions
{
    public double TargetMb { get; set; } = 25;
    public Intent Intent { get; set; } = Intent.Sharing;
    public CodecPreference Codec { get; set; } = CodecPreference.Compatible;
    public bool AllowResolutionDrop { get; set; } = true;
    public bool AllowFpsDrop { get; set; } = true;
    public HdrPolicy HdrPolicy { get; set; } = HdrPolicy.Preserve;
    public FillPolicy FillPolicy { get; set; } = FillPolicy.FillTarget;
    public SpeedMode SpeedMode { get; set; } = SpeedMode.Quality;
}

public readonly record struct FillBand(double LowerMb, double HardFloorMb, double UpperMb)
{
    public double CenterMb => (LowerMb + UpperMb) / 2.0;

    public double RelativeWidth => UpperMb <= 0 ? 0 : (UpperMb - LowerMb) / UpperMb;

    public static FillBand For(double targetMb)
    {
        double lowerFactor, floorFactor;
        if (targetMb >= 50) { lowerFactor = 0.972; floorFactor = 0.944; }
        else if (targetMb >= 10) { lowerFactor = 0.95; floorFactor = 0.90; }
        else { lowerFactor = 0.92; floorFactor = 0.85; }
        return new FillBand(targetMb * lowerFactor, targetMb * floorFactor, targetMb);
    }
}

public sealed record PlanResult(EncodePlan Plan, SizeEstimate Estimate, double PredictedQuality, ComplexityProfile Profile, StrategyAdvice Advice);

public enum QualityTargetBound { Matched, AboveSourceCeiling, BelowFloor }

public sealed record QualityTargetResult(
    double TargetMb,
    double PredictedQuality,
    double RequestedQuality,
    QualityTargetBound Bound,
    int Evaluations,
    PlanResult Plan)
{
    public double QualityError => PredictedQuality - RequestedQuality;
}

public static class PlanCalculator
{
    private const double ContainerOverhead = 0.995;
    private const double KbitPerMib = 8388.608;
    public const double TwoPassUncertainty = 0.012;
    public const double SourceSizeCap = 0.95;
    private const double ScaleStep = 0.02;
    private const double LowFpsSurcharge = 12.0;
    private const double LowFpsThreshold = 20.0;
    private const double MotionCutCheapSavingShare = 0.20;
    private const double MotionCutExpensiveSavingShare = 0.10;
    private static readonly double MotionCutIsCheapBelow = Math.Log2(2 * (1 - MotionCutCheapSavingShare));
    private static readonly double MotionCutIsExpensiveAbove = Math.Log2(2 * (1 - MotionCutExpensiveSavingShare));
    private const int MinVideoBitrateK = 48;
    private const double SourceQualityScore = 100.0;

    // ContainerOverhead is a percentage, but what a delivered file costs over the plan is close to
    // a fixed rate. Four live av1_nvenc runs on a 400 s 1080p60 clip, measured on the delivered
    // files (ffprobe per stream, kbit/s):
    //   target  request  video   audio   mux   over plan
    //   100 MB     1923   1930   125,6   9,0        16,0
    //    50 MB      895    895   125,6   9,0         9,0
    //    25 MB      410    420    91,0   9,0        19,0
    //     8 MB      113    119    29,0   9,0        15,0
    // "Over plan" is video plus mux less the request; the columns are rounded to whole kbit/s, so
    // reading it off them is good to about a kbit/s.
    // The container cost is 9,0 kbit/s at every target, and the encoder spends a few kbit/s past
    // the request on top. Together that is 9 to 19 kbit/s no matter how big the target is - 0,7%
    // of a 100 MB budget and 9% of an 8 MB one, which is why only the small targets overshot.
    // Eleven is held back for it. Fifteen was tried first and cost the 50 MB target its single
    // attempt: it delivered 48,50 MB against a 48,60 MB lower edge. That band is only 2,8% wide
    // and it is the binding one, so the reserve sits just above the smallest excess of the four
    // rather than near their mean, and covers the container's fixed 9,0 kbit/s.
    public const int HardwareDeliveryReserveK = 11;

    // Two layouts can score within a hair of each other and each win once the profile is
    // calibrated at the other, so the calibration loop flips between them forever. The shape the
    // profile was actually measured at gets a small bonus to break the tie in its own favour.
    private const double CalibratedShapeHysteresis = 0.25;

    // Only the hardware path was measured, so only the hardware path reserves it; the processor
    // path keeps the bitrates it has today.
    private static int DeliveryReserveK(string codec) => CodecModel.IsHardware(codec) ? HardwareDeliveryReserveK : 0;

    private static readonly string[] FastHardwareOrder =
    {
        "av1_nvenc", "hevc_nvenc", "av1_qsv", "hevc_qsv", "av1_amf", "hevc_amf", "h264_nvenc"
    };

    public static EncodePlan Build(MediaInfo info, PlanOptions options, IEncoderAvailability? availability = null)
        => BuildDetailed(info, options, null, availability).Plan;

    public static PlanResult BuildDetailed(MediaInfo info, PlanOptions options, ComplexityProfile? profile, IEncoderAvailability? availability = null)
    {
        var complexity = (profile ?? ComplexityProfile.FromSourceBitrate(info)).WithoutSampleContainerBias(info.Width, info.Height);
        var regime = CompressionStrategy.RegimeFor(info.FileSizeMb, options.TargetMb);
        var ratio = CompressionStrategy.Ratio(info.FileSizeMb, options.TargetMb);
        var notes = new List<AdviceCode>();
        var reason = new List<string>();
        var reasonCodes = new List<ReasonNote>();

        var preference = options.Codec == CodecPreference.Auto
            ? CompressionStrategy.AutoPreference(regime)
            : options.Codec;
        var fast = options.SpeedMode == SpeedMode.Fast;
        var codec = fast ? PickFastCodec(preference, availability) : PickCodec(preference, availability);
        var suggestedPreference = CompressionStrategy.AutoPreference(regime);

        var hdr = HdrResolver.Resolve(info, options.HdrPolicy, codec, availability);

        if (CanPassThrough(info, options, codec, hdr))
            return PassThroughResult(info, options, complexity, regime, ratio, suggestedPreference, availability, notes);

        var effectiveTargetMb = EffectiveTargetMb(options.TargetMb, info.FileSizeMb);
        if (effectiveTargetMb < options.TargetMb - 1e-9)
        {
            reason.Add($"the target was capped to {effectiveTargetMb:0.##} MB, {SourceSizeCap * 100:0.#}% of the {info.FileSizeMb:0.0} MB source, so the output is never larger than what it was made from");
            reasonCodes.Add(new ReasonNote(ReasonCode.TargetCappedToSource, Mb: effectiveTargetMb, TargetMb: options.TargetMb));
        }

        var band = FillBand.For(effectiveTargetMb);
        var aimMb = RetryAimMb(effectiveTargetMb, null);

        if (hdr.PolicyChanged)
        {
            notes.Add(AdviceCode.HdrTonemapped);
            reason.Add("the source is HDR but the selected encoder cannot preserve 10-bit, so it was tone-mapped to SDR BT.709");
            reasonCodes.Add(new ReasonNote(ReasonCode.HdrTonemapped));
        }

        var preferredCodec = fast ? FastHardwareOrder[0] : PreferredCodecFor(preference);
        if (codec != preferredCodec)
        {
            notes.Add(AdviceCode.EncoderFallback);
            reason.Add($"the {preferredCodec} encoder is not available on this ffmpeg build, so encoding falls back to {codec}");
            reasonCodes.Add(new ReasonNote(ReasonCode.EncoderFallback, RequestedCodec: preferredCodec, FallbackCodec: codec));
        }

        if (options.Codec != CodecPreference.Auto && suggestedPreference == CodecPreference.MaxCompression && preference == CodecPreference.Compatible)
            notes.Add(AdviceCode.CodecUpgradeRecommended);
        if (CodecModel.CostsQualityInHardware(codec) && regime is CompressionRegime.Aggressive or CompressionRegime.Extreme)
            notes.Add(AdviceCode.HardwareCodecCostsQuality);
        if (regime == CompressionRegime.Extreme)
            notes.Add(AdviceCode.ExtremeRatioWarning);

        var totalK = aimMb * KbitPerMib / Math.Max(info.DurationSeconds, 0.1);
        var (audioK, audioChannels) = PickAudio(info, options, regime, totalK, notes);
        var videoK = Math.Max(MinVideoBitrateK, totalK * ContainerOverhead - audioK - DeliveryReserveK(codec));

        var effective = new PlanOptions
        {
            TargetMb = effectiveTargetMb,
            Intent = options.Intent,
            Codec = preference,
            AllowResolutionDrop = options.AllowResolutionDrop && CompressionStrategy.AllowsResolutionDrop(regime),
            AllowFpsDrop = options.AllowFpsDrop && CompressionStrategy.AllowsFpsDrop(regime),
            SpeedMode = options.SpeedMode
        };

        var (best, sourceFpsViable) = SearchLayout(info, effective, complexity, codec, videoK, regime);

        if (complexity.Measured)
        {
            if (effective.AllowResolutionDrop)
            {
                if (complexity.DetailExponent >= 0.6) notes.Add(AdviceCode.ScaleSavesMuch);
                else if (complexity.DetailExponent <= 0.2) notes.Add(AdviceCode.ScaleSavesLittle);
            }
            if (complexity.ReferenceBppf <= 0.02) notes.Add(AdviceCode.ContentIsSimple);
            else if (complexity.ReferenceBppf >= 0.25) notes.Add(AdviceCode.ContentIsComplex);
        }

        if (complexity.MotionMeasured && effective.AllowFpsDrop)
        {
            var halvingSaving = (1 - Math.Pow(2, complexity.MotionExponent - 1)) * 100;
            if (complexity.MotionExponent <= MotionCutIsCheapBelow)
            {
                notes.Add(AdviceCode.MotionCutIsCheap);
                reason.Add($"the motion measurement puts this title's frame rate exponent at {complexity.MotionExponent:0.00}, so halving the frame rate saves {halvingSaving:0.#}% of the bits and dropping frames is cheap here");
            }
            else if (complexity.MotionExponent >= MotionCutIsExpensiveAbove)
            {
                notes.Add(AdviceCode.MotionCutIsExpensive);
                reason.Add($"the motion measurement puts this title's frame rate exponent at {complexity.MotionExponent:0.00}, so halving the frame rate saves only {halvingSaving:0.#}% of the bits and resolution is cut before frames are");
            }
        }

        if (!best.MeetsFloor)
        {
            // Two different walls end up here and the user is owed the one that was actually hit:
            // either no layout carries enough bits per pixel for a meaningful picture, or the
            // encoder itself will not deliver a bitrate this low whatever the layout.
            notes.Add(AdviceCode.TargetBelowCodecFloor);
            reason.Add(best.Deliverable
                ? $"no layout reaches the {complexity.FloorBppf(codec, best.Fps, info.Fps):0.0000} bits per pixel per frame that {codec} needs for a meaningful picture; the densest layout ({best.Bppf:0.0000}) was taken, but this target is genuinely too small for this source"
                : $"the budget leaves {videoK:0}k for video, below the {CodecModel.UsableBitrateK(codec, best.Width, best.Height, best.Fps)}k the {codec} hardware encoder still follows at {best.Width}x{best.Height}@{best.Fps:0.##}, and no smaller layout gets under its floor either; the densest layout was taken, but this encoder cannot deliver a file this small from this source");
        }
        else if (best.Fps < info.Fps - 0.01 && !sourceFpsViable)
        {
            notes.Add(AdviceCode.FrameRateCutForFloor);
            reason.Add($"at {info.Fps:0.##} fps every frame would fall below the {complexity.FloorBppf(codec, info.Fps, info.Fps):0.0000} bits per pixel per frame that {codec} needs, so the frame rate was cut to {best.Fps:0.##} and the freed bits went to the frames that remain");
        }

        if (best.Width != info.Width || best.Height != info.Height)
        {
            notes.Add(AdviceCode.ResolutionReduced);
            reason.Add($"scaled to {best.Width}x{best.Height} ({best.Scale * 100:0.#}% of source); at this title's measured detail falloff that frees enough bits to raise predicted quality");
            reasonCodes.Add(new ReasonNote(ReasonCode.ResolutionScaled, Width: best.Width, Height: best.Height, ScalePercent: best.Scale * 100));
        }
        if (best.Fps < info.Fps - 0.01)
        {
            notes.Add(AdviceCode.FrameRateReduced);
            reason.Add($"frame rate reduced to {best.Fps:0.##} to keep per-frame detail");
            reasonCodes.Add(new ReasonNote(ReasonCode.FrameRateReduced, Fps: best.Fps));
        }

        var budgetBppf = BitsPerPixel(videoK, best.Width, best.Height, best.Fps);
        var budgetCrf = complexity.CrfForBppf(codec, budgetBppf, best.Scale, best.Fps, info.Fps);
        var ceilingCrf = TransparencyCrf(codec, options.Intent);

        var qualityStopCrf = MeasuredQualityStopCrf(complexity, codec, best, info);
        var qualityStopBinding = qualityStopCrf is double stopCrf && stopCrf > ceilingCrf + 1e-9;
        if (qualityStopBinding)
        {
            ceilingCrf = qualityStopCrf!.Value;
            reason.Add($"the measured quality stop binds before the transparency ceiling: the probe measured VMAF-NEG {complexity.QualityAnchor!.VmafNeg:0.##} on this source, and CRF {ceilingCrf:0.#} already reaches it, so the plan stops there instead of spending the rest of the budget on an extrapolation no measurement covers");
        }

        EncodePlan plan;
        var ceilingBppf = complexity.BppfAtCrf(codec, ceilingCrf, best.Scale, best.Fps, info.Fps);
        var ceilingVideoK = VideoBitrateK(ceilingBppf, best.Width, best.Height, best.Fps);
        var ceilingSizeMb = SizeMb(ceilingVideoK, audioK, info.DurationSeconds);

        if (budgetCrf <= ceilingCrf && ceilingSizeMb < band.LowerMb)
        {
            var recovered = RecoverLayoutAtCeiling(info, effective, complexity, codec, best, ceilingCrf, audioK, band.LowerMb, videoK, regime);
            if (recovered.Scale > best.Scale + 1e-6 || recovered.Fps > best.Fps + 0.01)
            {
                reason.Add($"the ceiling left budget unused, so resolution was restored to {recovered.Width}x{recovered.Height}@{recovered.Fps:0.##} — the largest layout that still fits the target at CRF {ceilingCrf:0}");
                reasonCodes.Add(new ReasonNote(ReasonCode.ResolutionRestoredAtCeiling, Width: recovered.Width, Height: recovered.Height, Fps: recovered.Fps, Crf: ceilingCrf));
                best = recovered;
                notes.Remove(AdviceCode.ResolutionReduced);
                notes.Remove(AdviceCode.FrameRateReduced);
                reasonCodes.RemoveAll(n => n.Code is ReasonCode.ResolutionScaled or ReasonCode.FrameRateReduced);
                notes.Remove(AdviceCode.FrameRateCutForFloor);
                if (best.Width != info.Width || best.Height != info.Height) notes.Add(AdviceCode.ResolutionReduced);
                if (best.Fps < info.Fps - 0.01) notes.Add(AdviceCode.FrameRateReduced);
                ceilingBppf = complexity.BppfAtCrf(codec, ceilingCrf, best.Scale, best.Fps, info.Fps);
                ceilingVideoK = VideoBitrateK(ceilingBppf, best.Width, best.Height, best.Fps);
                ceilingSizeMb = SizeMb(ceilingVideoK, audioK, info.DurationSeconds);
            }

            notes.Add(AdviceCode.BudgetIsGenerous);
            notes.Add(AdviceCode.QualityCeilingReached);
            reason.Add($"the budget affords CRF {budgetCrf:0.#}, better than the CRF {ceilingCrf:0} transparency ceiling for this intent, so the encoder stops at the ceiling and delivers about {ceilingSizeMb:0.0} MB instead of padding the file to {effectiveTargetMb:0.##} MB");
            reasonCodes.Add(new ReasonNote(ReasonCode.BudgetExceedsCeiling, BudgetCrf: budgetCrf, Crf: ceilingCrf, Mb: ceilingSizeMb, TargetMb: effectiveTargetMb));
            plan = NewPlan(codec, effective, info, best, audioK, audioChannels, hdr);
            plan.Mode = "crf";
            plan.Crf = (int)Math.Round(ceilingCrf);
            plan.VideoBitrateK = (int)Math.Round(Math.Max(ceilingVideoK, MinVideoBitrateK));

            if (options.FillPolicy == FillPolicy.FillTarget && !qualityStopBinding)
            {
                var (minCrf, _) = CodecModel.CrfRange(codec);
                var totalBudgetK = aimMb * KbitPerMib * ContainerOverhead / Math.Max(info.DurationSeconds, 0.1);
                var desiredVideoK = Math.Max(MinVideoBitrateK, totalBudgetK - audioK - DeliveryReserveK(codec));
                var desiredBppf = BitsPerPixel(desiredVideoK, best.Width, best.Height, best.Fps);
                var fillCrf = complexity.CrfForBppf(codec, desiredBppf, best.Scale, best.Fps, info.Fps);
                var crfStep = complexity.CrfStepSizeEffect(codec, best.Scale, best.Fps);
                var gridIsCoarserThanBand = crfStep > band.RelativeWidth;

                if (fillCrf >= minCrf && !gridIsCoarserThanBand)
                {
                    plan.Crf = (int)Math.Round(fillCrf);
                    plan.VideoBitrateK = (int)Math.Round(Math.Max(desiredVideoK, MinVideoBitrateK));
                    reason.Add($"the fill target policy lowered CRF to {fillCrf:0.#} instead of stopping at the transparency ceiling, landing near {aimMb:0.0} MB inside the {band.LowerMb:0.0}-{band.UpperMb:0.0} MB band");
                    reasonCodes.Add(new ReasonNote(ReasonCode.FillCrfLowered, Crf: fillCrf, Mb: aimMb, TargetMb: band.UpperMb, BandLowerMb: band.LowerMb));
                }
                else
                {
                    plan.Mode = "2pass";
                    plan.Crf = null;
                    plan.VideoBitrateK = (int)Math.Round(Math.Max(desiredVideoK, MinVideoBitrateK));
                    reason.Add(gridIsCoarserThanBand
                        ? $"one CRF step moves the file by {crfStep * 100:0.#}%, wider than the {band.RelativeWidth * 100:0.#}% fill band, so single-pass CRF cannot land inside it and two-pass VBR targets {aimMb:0.0} MB directly"
                        : $"CRF floor {minCrf} was reached before the fill band, so two-pass VBR targets the {aimMb:0.0} MB band center directly");
                    reasonCodes.Add(new ReasonNote(gridIsCoarserThanBand ? ReasonCode.FillTwoPassBandTooNarrowForCrf : ReasonCode.FillTwoPassBandCenter,
                        Crf: minCrf, Mb: aimMb, TargetMb: band.UpperMb, BandLowerMb: band.LowerMb, Factor: crfStep));
                    AddHardwareYieldNote(codec, complexity, best, reason, reasonCodes);
                }
            }
        }
        else
        {
            notes.Add(AdviceCode.TargetEnforcedTwoPass);
            reason.Add($"the budget lands near CRF {budgetCrf:0.#}, short of the CRF {ceilingCrf:0} ceiling, so two-pass VBR spends {aimMb:0.##} MB, the band center of the {effectiveTargetMb:0.##} MB target");
            reasonCodes.Add(new ReasonNote(ReasonCode.BudgetBelowCeilingTwoPass, BudgetCrf: budgetCrf, Crf: ceilingCrf, TargetMb: effectiveTargetMb));
            plan = NewPlan(codec, effective, info, best, audioK, audioChannels, hdr);
            plan.Mode = "2pass";
            plan.VideoBitrateK = (int)Math.Round(Math.Max(videoK, MinVideoBitrateK));
            AddHardwareYieldNote(codec, complexity, best, reason, reasonCodes);
        }

        reason.Add($"predicted quality {best.Score:0.#}/100{(complexity.Measured ? $" from a measured sample (bppf {complexity.ReferenceBppf:0.0000}, detail falloff {complexity.DetailExponent:0.00})" : " estimated from the source bitrate")}");
        reasonCodes.Add(complexity.Measured
            ? new ReasonNote(ReasonCode.PredictedQualityMeasured, Score: best.Score, Bppf: complexity.ReferenceBppf, DetailExponent: complexity.DetailExponent)
            : new ReasonNote(ReasonCode.PredictedQualityEstimated, Score: best.Score));
        plan.Reason = string.Join("; ", reason);
        plan.ReasonCodes = reasonCodes;
        plan.EffectiveTargetMb = effectiveTargetMb;

        var estimate = Estimate(plan, info, complexity);
        var advice = new StrategyAdvice(regime, ratio, PickCodec(suggestedPreference, availability), suggestedPreference, best.Score, notes);
        return new PlanResult(plan, estimate, best.Score, complexity, advice);
    }

    private static EncodePlan NewPlan(string codec, PlanOptions options, MediaInfo info, Layout best, int audioK, int? audioChannels, HdrResolution hdr) => new()
    {
        Codec = codec,
        AudioCodec = info.HasAudio && audioK > 0 ? PickAudioCodec() : null,
        AudioBitrateK = audioK,
        AudioChannels = audioChannels,
        Width = best.Width,
        Height = best.Height,
        Fps = best.Fps,
        Preset = PickPreset(codec, options.Codec, options.SpeedMode),
        PixelFormat = hdr.PixelFormat,
        HdrVideoFilter = hdr.VideoFilter,
        HdrColorArgs = new List<string>(hdr.ColorArgs)
    };

    private static bool CanPassThrough(MediaInfo info, PlanOptions options, string codec, HdrResolution hdr)
    {
        if (info.FileSizeMb <= 0 || info.FileSizeMb > options.TargetMb) return false;
        if (hdr.PolicyChanged) return false;
        if (options.Codec == CodecPreference.Auto) return true;
        return string.Equals(CodecModel.SourceFamily(info.VideoCodec), CodecModel.SourceFamily(codec), StringComparison.OrdinalIgnoreCase);
    }

    private static PlanResult PassThroughResult(MediaInfo info, PlanOptions options, ComplexityProfile complexity,
        CompressionRegime regime, double ratio, CodecPreference suggestedPreference, IEncoderAvailability? availability, List<AdviceCode> notes)
    {
        var videoBps = Math.Max(0, info.TotalBitrateBps - info.AudioBitrateBps);
        var plan = new EncodePlan
        {
            Codec = info.VideoCodec,
            Mode = "passthrough",
            VideoBitrateK = (int)Math.Round(videoBps / 1000.0),
            Crf = null,
            AudioCodec = info.AudioCodec,
            AudioBitrateK = (int)Math.Round(info.AudioBitrateBps / 1000.0),
            Width = info.Width,
            Height = info.Height,
            Fps = info.Fps,
            Preset = "copy",
            PixelFormat = info.PixelFormat ?? "yuv420p",
            Reason = $"the source is already {info.FileSizeMb:0.0} MB, under the {options.TargetMb:0.##} MB target, so it is copied as it is instead of being re-encoded",
            ReasonCodes = new List<ReasonNote> { new(ReasonCode.SourceAlreadyUnderTarget, Mb: info.FileSizeMb, TargetMb: options.TargetMb) }
        };

        notes.Add(AdviceCode.BudgetIsGenerous);
        var estimate = new SizeEstimate(info.FileSizeMb, info.FileSizeMb, info.FileSizeMb, complexity.Measured, true);
        var advice = new StrategyAdvice(regime, ratio, PickCodec(suggestedPreference, availability), suggestedPreference, SourceQualityScore, notes);
        return new PlanResult(plan, estimate, SourceQualityScore, complexity, advice);
    }

    public static SizeEstimate Estimate(EncodePlan plan, MediaInfo info, ComplexityProfile? profile)
    {
        var complexity = (profile ?? ComplexityProfile.FromSourceBitrate(info)).WithoutSampleContainerBias(info.Width, info.Height);

        if (plan.ModeEnum == EncodeMode.PassThrough)
            return new SizeEstimate(info.FileSizeMb, info.FileSizeMb, info.FileSizeMb, complexity.Measured, true);

        var scale = info.Height <= 0 ? 1.0 : (double)plan.Height / info.Height;

        if (plan.ModeEnum == EncodeMode.TwoPass)
        {
            var expected = SizeMb(plan.VideoBitrateK, plan.AudioBitrateK, info.DurationSeconds);
            var yield = HardwareBitrateYield(plan.Codec, complexity, scale, plan.Fps);
            var low = Math.Min(expected * (1 - TwoPassUncertainty), expected * yield);
            return new SizeEstimate(expected, low, expected * (1 + TwoPassUncertainty), complexity.Measured, true);
        }

        var bppf = complexity.BppfAtCrf(plan.Codec, plan.Crf ?? CodecModel.ReferenceCrf(plan.Codec), scale, plan.Fps, info.Fps);
        var videoK = VideoBitrateK(bppf, plan.Width, plan.Height, plan.Fps);
        var expectedMb = SizeMb(videoK, plan.AudioBitrateK, info.DurationSeconds);
        var band = complexity.EstimateBandFor(plan.Codec, scale, plan.Fps);
        return new SizeEstimate(expectedMb, expectedMb * (1 - band), expectedMb * (1 + band), complexity.Measured, false);
    }

    public static double? MeasuredEncoderEfficiency(EncodePlan plan, double actualMb, double durationSeconds)
    {
        if (plan.ModeEnum != EncodeMode.TwoPass || plan.VideoBitrateK <= 0) return null;
        var requestedVideoMb = SizeMb(plan.VideoBitrateK, 0, durationSeconds);
        var deliveredVideoMb = actualMb - SizeMb(0, plan.AudioBitrateK, durationSeconds);
        if (requestedVideoMb <= 0.01 || deliveredVideoMb <= 0.01) return null;
        var efficiency = deliveredVideoMb / requestedVideoMb;
        return efficiency is > 0.5 and < 2.0 ? efficiency : null;
    }

    public static double EffectiveTargetMb(double targetMb, double sourceMb)
        => sourceMb > 0 ? Math.Min(targetMb, sourceMb * SourceSizeCap) : targetMb;

    // The quality scale is the one BuildDetailed already returns; the search below only runs it
    // backwards. It cannot assume the relation is monotone - it is not, and that was measured
    // rather than reasoned about. Sweeping the target across its whole range on three synthetic
    // sources at two intents (1206 samples) the predicted quality falls back 42 times and the
    // largest fall is 16,8 points; on two ffmpeg-made clips with a measured complexity profile
    // (242 samples) it falls back 12 times, worst 7,9. The cause is that the layout search, the
    // regime thresholds and the audio budget all step: a bigger target can buy a layout that
    // scores worse, so the reachable qualities form a chain of peaks rather than one rising line.
    //
    // The inverse is therefore defined as the smallest target that reaches the requested quality,
    // and finding it means not stepping over a peak. A coarse grid does exactly that: measured
    // against a 0,15%-grid ground truth, a grid of 31% steps answers up to 3,83x too large, 10%
    // and 5% up to 1,245x, 2% up to 1,112x. The scan therefore walks 0,5% steps from the floor
    // and stops at the first crossing - 1,000x against the same ground truth, at 1125 calls
    // worst case. 1% steps would cost half that and answer 1,002x; the cheaper grid was not
    // taken because K5 asks for expensive and right over cheap and wrong.
    // ScanResolutionIsChosenByConvergence in QualityTargetTests is that measurement.
    private const double QualityScanStep = 1.005;
    private const int QualityBisectionMaxSteps = 4;
    private const int QualityBracketEvaluations = 2;
    public const int QualitySearchMaxEvaluations = 1400;
    private const double QualityBisectionResolution = 0.20;

    public static double QualityFloorTargetMb(MediaInfo info)
        => Math.Max(0.02, SizeMb(MinVideoBitrateK, 0, Math.Max(info.DurationSeconds, 0.1)));

    public static double QualityCeilingTargetMb(MediaInfo info)
    {
        var cap = info.FileSizeMb > 0 ? info.FileSizeMb * SourceSizeCap : QualityFloorTargetMb(info) * 4096;
        return Math.Max(cap, QualityFloorTargetMb(info) * 1.01);
    }

    public static QualityTargetResult TargetMbForQuality(MediaInfo info, PlanOptions options, double requestedQuality,
        ComplexityProfile? profile = null, IEncoderAvailability? availability = null)
    {
        var evaluations = 0;
        PlanResult At(double mb)
        {
            evaluations++;
            return BuildDetailed(info, WithTarget(options, mb), profile, availability);
        }

        var floorMb = QualityFloorTargetMb(info);
        var ceilingMb = QualityCeilingTargetMb(info);

        var low = At(floorMb);
        if (low.PredictedQuality >= requestedQuality)
            return new QualityTargetResult(floorMb, low.PredictedQuality, requestedQuality,
                low.PredictedQuality > requestedQuality + QualityBisectionResolution ? QualityTargetBound.BelowFloor : QualityTargetBound.Matched,
                evaluations, low);

        var ceiling = At(ceilingMb);
        if (ceiling.PredictedQuality < requestedQuality)
            return new QualityTargetResult(ceilingMb, ceiling.PredictedQuality, requestedQuality,
                QualityTargetBound.AboveSourceCeiling, evaluations, ceiling);

        var span = ceilingMb / floorMb;
        var scanBudget = QualitySearchMaxEvaluations - QualityBisectionMaxSteps - QualityBracketEvaluations;
        var steps = Math.Clamp((int)Math.Ceiling(Math.Log(span) / Math.Log(QualityScanStep)), 1, scanBudget);
        var lowMb = floorMb;
        var bestMb = ceilingMb;
        var best = ceiling;
        for (var i = 1; i < steps; i++)
        {
            var mb = floorMb * Math.Pow(span, (double)i / steps);
            var probe = At(mb);
            if (probe.PredictedQuality >= requestedQuality)
            {
                bestMb = mb;
                best = probe;
                break;
            }
            lowMb = mb;
        }

        for (var step = 0; step < QualityBisectionMaxSteps; step++)
        {
            if (evaluations >= QualitySearchMaxEvaluations) break;
            var midMb = Math.Sqrt(lowMb * bestMb);
            if (midMb <= lowMb * 1.000001 || midMb >= bestMb * 0.999999) break;
            var mid = At(midMb);
            if (mid.PredictedQuality >= requestedQuality)
            {
                best = mid;
                bestMb = midMb;
            }
            else lowMb = midMb;
        }

        return new QualityTargetResult(bestMb, best.PredictedQuality, requestedQuality, QualityTargetBound.Matched, evaluations, best);
    }

    private static PlanOptions WithTarget(PlanOptions options, double targetMb) => new()
    {
        TargetMb = targetMb,
        Intent = options.Intent,
        Codec = options.Codec,
        AllowResolutionDrop = options.AllowResolutionDrop,
        AllowFpsDrop = options.AllowFpsDrop,
        HdrPolicy = options.HdrPolicy,
        FillPolicy = options.FillPolicy,
        SpeedMode = options.SpeedMode
    };

    public static double RetryAimMb(double targetMb, double? measuredEfficiency)
    {
        var band = FillBand.For(targetMb);
        if (measuredEfficiency is not null) return band.CenterMb;

        var ceilingAim = targetMb / (1 + TwoPassUncertainty);
        return Math.Max(band.LowerMb, Math.Min(band.CenterMb, ceilingAim));
    }

    public static EncodePlan Correct(EncodePlan plan, double actualMb, double targetMb, double durationSeconds, bool fillUnderBand = false)
    {
        var corrected = plan.Clone();
        var band = FillBand.For(targetMb);
        var audioMb = SizeMb(0, plan.AudioBitrateK, durationSeconds);
        var previousVideoK = plan.VideoBitrateK;
        var efficiency = MeasuredEncoderEfficiency(plan, actualMb, durationSeconds);
        var aimMb = RetryAimMb(targetMb, efficiency);
        var deliveredVideoMb = Math.Max(actualMb - audioMb, 0.01);
        var aimedVideoMb = Math.Max(aimMb - audioMb, 0.01);
        var factor = aimedVideoMb / deliveredVideoMb;
        var requestedVideoMb = aimedVideoMb / (efficiency ?? 1.0);
        var videoBudgetK = Math.Max(MinVideoBitrateK, requestedVideoMb * KbitPerMib * ContainerOverhead / Math.Max(durationSeconds, 0.1));
        var aimSource = efficiency is double e
            ? $"aimed at the {aimMb:0.0} MB band center and divided by the {e:0.###} encoder yield measured on the previous attempt"
            : $"aimed at {aimMb:0.0} MB, the band center held back by the +{TwoPassUncertainty * 100:0.#}% two-pass spread because no encoder yield was measured yet";

        corrected.Mode = "2pass";
        corrected.Crf = null;
        corrected.BitrateBias = HardwareDeliveryBias(efficiency);
        corrected.VideoBitrateK = Math.Max(MinVideoBitrateK, (int)Math.Round(Math.Min(previousVideoK * factor, videoBudgetK)));

        corrected.Reason = fillUnderBand
            ? $"retry: the previous attempt produced {actualMb:0.0} MB, below the {band.LowerMb:0.0} MB lower edge of the fill band for a {targetMb:0.##} MB target; video bitrate was scaled by {factor:0.###} and {aimSource}"
            : $"retry: the previous attempt produced {actualMb:0.0} MB against a {targetMb:0.##} MB target; after reserving {audioMb:0.00} MB for audio, video bitrate was scaled by {factor:0.###}, {aimSource}";
        corrected.ReasonCodes = new List<ReasonNote> { new(ReasonCode.RetryScaled, Mb: actualMb, TargetMb: targetMb, AudioMb: audioMb, Factor: factor, BandLowerMb: band.LowerMb) };
        return corrected;
    }

    public static double? EstimatedMb(EncodePlan plan, double durationSeconds)
        => plan.ModeEnum == EncodeMode.Crf ? null : SizeMb(plan.VideoBitrateK, plan.AudioBitrateK, durationSeconds);

    public static double BitsPerPixel(double videoK, int width, int height, double fps)
        => videoK * 1000.0 / Math.Max(1.0, (double)width * height * fps);

    private static double VideoBitrateK(double bppf, int width, int height, double fps)
        => bppf * Math.Max(1.0, (double)width * height * fps) / 1000.0;

    private static double SizeMb(double videoK, double audioK, double durationSeconds)
        => (videoK + audioK) * durationSeconds / KbitPerMib / ContainerOverhead;

    private sealed record Layout(int Width, int Height, double Fps, double Scale, double Score, double Bppf = 0, bool MeetsFloor = true, bool Deliverable = true);

    private static Layout RecoverLayoutAtCeiling(MediaInfo info, PlanOptions options, ComplexityProfile complexity, string codec, Layout fallback, double ceilingCrf, int audioK, double capMb, double budgetVideoK, CompressionRegime regime)
    {
        Layout? best = null;
        var floors = CompressionStrategy.FloorsFor(regime);

        foreach (var fps in FpsCandidates(info, options, regime))
        foreach (var scale in ScaleCandidates(options, regime))
        {
            if (scale < fallback.Scale - 1e-6) continue;

            var (width, height) = Dimensions(info, scale);
            if (height < floors.MinHeight && height < info.Height) continue;
            if (width < 2 || height < 2) continue;

            var effectiveScale = (double)height / Math.Max(1, info.Height);
            var bppf = complexity.BppfAtCrf(codec, ceilingCrf, effectiveScale, fps, info.Fps);
            var videoK = VideoBitrateK(bppf, width, height, fps);
            if (SizeMb(videoK, audioK, info.DurationSeconds) >= capMb) continue;
            if (budgetVideoK < CodecModel.UsableBitrateK(codec, width, height, fps)) continue;

            var required = complexity.RequiredBppf(codec, effectiveScale, fps, info.Fps);
            var score = LayoutScore(complexity, codec, required, bppf, effectiveScale, fps, info.Fps, regime);
            if (best is null || score > best.Score)
                best = new Layout(width, height, fps, effectiveScale, score, bppf);
        }

        return best ?? fallback;
    }

    private static double? MeasuredQualityStopCrf(ComplexityProfile complexity, string codec, Layout best, MediaInfo info)
    {
        if (complexity.QualityAnchor is not { } anchor) return null;
        var level = complexity.Level;
        if (level.PerHalving <= 0) return null;

        var required = complexity.RequiredBppf(codec, best.Scale, best.Fps, info.Fps);
        var stopBppf = required * Math.Pow(2, (anchor.VmafNeg - level.AtReference) / level.PerHalving);
        if (!double.IsFinite(stopBppf) || stopBppf <= 0) return null;

        var crf = complexity.CrfForBppf(codec, stopBppf, best.Scale, best.Fps, info.Fps);
        if (!double.IsFinite(crf)) return null;
        var (min, max) = CodecModel.CrfRange(codec);
        return Math.Clamp(crf, min, max);
    }

    private static double LayoutScore(ComplexityProfile complexity, string codec, double required, double provided, double scale, double fps, double sourceFps, CompressionRegime regime)
    {
        var weights = CompressionStrategy.PenaltyWeights(regime);
        var level = complexity.Level;
        var rate = level.AtReference - level.PerHalving * Math.Log2(Math.Max(required, 1e-9) / Math.Max(provided, 1e-9));
        rate = Math.Min(rate, CodecModel.QualityLimit(codec));
        return rate - ScalePenalty(scale, weights) - FpsPenalty(fps, sourceFps, weights);
    }

    private static (Layout Best, bool SourceFpsViable) SearchLayout(MediaInfo info, PlanOptions options, ComplexityProfile complexity, string codec, double videoK, CompressionRegime regime)
    {
        Layout? best = null;
        Layout? densest = null;
        var sourceFpsViable = false;
        var floors = CompressionStrategy.FloorsFor(regime);

        foreach (var fps in FpsCandidates(info, options, regime))
        foreach (var scale in ScaleCandidates(options, regime))
        {
            var (width, height) = Dimensions(info, scale);
            if (height < floors.MinHeight && height < info.Height) continue;
            if (width < 2 || height < 2) continue;

            var effectiveScale = (double)height / Math.Max(1, info.Height);
            var required = complexity.RequiredBppf(codec, effectiveScale, fps, info.Fps);
            var provided = BitsPerPixel(videoK, width, height, fps);
            var score = LayoutScore(complexity, codec, required, provided, effectiveScale, fps, info.Fps, regime);
            if (complexity.AppliesTo(codec, effectiveScale, fps)) score += CalibratedShapeHysteresis;
            var deliverable = videoK >= CodecModel.UsableBitrateK(codec, width, height, fps);

            if (deliverable && provided >= complexity.FloorBppf(codec, fps, info.Fps))
            {
                if (fps >= info.Fps - 0.01) sourceFpsViable = true;
                if (best is null || score > best.Score)
                    best = new Layout(width, height, fps, effectiveScale, score, provided);
            }
            else if (densest is null || provided > densest.Bppf)
                densest = new Layout(width, height, fps, effectiveScale, score, provided, false, deliverable);
        }

        if (best is not null) return (best, sourceFpsViable);
        if (densest is not null) return (densest, false);

        var (fallbackWidth, fallbackHeight) = Dimensions(info, 1.0);
        return (new Layout(fallbackWidth, fallbackHeight, info.Fps, 1.0, 0), true);
    }

    public static IEnumerable<double> ScaleCandidates(PlanOptions options, CompressionRegime regime)
    {
        if (!options.AllowResolutionDrop)
        {
            yield return 1.0;
            yield break;
        }
        var floors = CompressionStrategy.FloorsFor(regime);
        for (var scale = 1.0; scale >= floors.MinScale - 1e-9; scale -= ScaleStep)
            yield return Math.Round(scale, 4);
    }

    public static IEnumerable<double> FpsCandidates(MediaInfo info, PlanOptions options, CompressionRegime regime)
    {
        var floors = CompressionStrategy.FloorsFor(regime);
        var source = info.Fps <= 0 ? 30 : info.Fps;
        yield return source;
        if (!options.AllowFpsDrop) yield break;

        var seen = new HashSet<double> { Math.Round(source, 3) };
        var candidates = new[]
        {
            source / 1.5, source / 2.0, source / 2.5, source / 3.0, source / 4.0, source / 5.0, source / 6.0,
            30.0, 25.0, 24.0, 20.0, 15.0, 12.0, 10.0, 8.0, 6.0
        };
        foreach (var candidate in candidates)
        {
            if (candidate >= source - 0.01 || candidate < floors.MinFps) continue;
            if (!seen.Add(Math.Round(candidate, 3))) continue;
            yield return candidate;
        }
    }

    private static (int Width, int Height) Dimensions(MediaInfo info, double scale)
    {
        if (scale >= 0.985) return (EvenDown(info.Width), EvenDown(info.Height));
        return (EvenDown((int)Math.Round(info.Width * scale)), EvenDown((int)Math.Round(info.Height * scale)));
    }

    private static int EvenDown(int value) => value % 2 == 0 ? value : value - 1;

    private static double ScalePenalty(double scale, PenaltyWeights weights)
    {
        if (scale >= 0.999) return 0;
        return CodecModel.ScalePenaltyScale * Math.Pow(1.0 / Math.Max(scale, 0.05) - 1.0, CodecModel.ScalePenaltyExponent) * weights.Scale;
    }

    private static double FpsPenalty(double fps, double sourceFps, PenaltyWeights weights)
    {
        if (fps >= sourceFps - 0.01) return 0;
        var penalty = CodecModel.FpsPenaltyPerHalving * Math.Log2(sourceFps / Math.Max(fps, 1.0));
        if (weights.LowFpsSurcharge && fps < LowFpsThreshold)
            penalty += LowFpsSurcharge * (LowFpsThreshold - fps) / 8.0;
        return penalty * weights.Fps;
    }

    private static double TransparencyCrf(string codec, Intent intent)
    {
        var reference = CodecModel.ReferenceCrf(codec);
        var scaled = CompressionStrategy.TransparencyOffset(intent) * CodecModel.CrfHalvingStep(codec) / 6.0;
        var (min, max) = CodecModel.CrfRange(codec);
        return Math.Clamp(reference + scaled, min, max);
    }

    private static (int BitrateK, int? Channels) PickAudio(MediaInfo info, PlanOptions options, CompressionRegime regime, double totalK, List<AdviceCode> notes)
    {
        if (!info.HasAudio) return (0, null);

        var stereo = info.AudioChannels >= 2;
        var baseK = stereo ? 128 : 96;
        if (options.Intent == Intent.Archive) baseK = stereo ? 160 : 112;
        if (info.AudioBitrateBps > 0)
            baseK = Math.Min(baseK, (int)Math.Round(info.AudioBitrateBps / 1000.0));

        var cap = totalK * CompressionStrategy.AudioBudgetShare(regime);
        var audioK = baseK;
        if (audioK > cap) audioK = (int)Math.Round(cap);

        if (audioK < 16 && totalK < 96)
        {
            notes.Add(AdviceCode.AudioDropped);
            return (0, null);
        }

        audioK = Math.Max(24, audioK);
        if (audioK < baseK) notes.Add(AdviceCode.AudioReduced);

        int? channels = null;
        if (stereo && audioK <= 56)
        {
            channels = 1;
            notes.Add(AdviceCode.AudioMono);
        }

        return (audioK, channels);
    }

    private static string PreferredCodecFor(CodecPreference pref) => pref switch
    {
        CodecPreference.MaxCompression => "libsvtav1",
        CodecPreference.Fast => "h264_nvenc",
        _ => "libx264"
    };

    private static string FallbackCodecFor(CodecPreference pref) => pref switch
    {
        CodecPreference.MaxCompression => "libx265",
        CodecPreference.Fast => "libx264",
        _ => "libx264"
    };

    private static string PickCodec(CodecPreference pref, IEncoderAvailability? availability)
    {
        var preferred = PreferredCodecFor(pref);
        if (pref is not (CodecPreference.MaxCompression or CodecPreference.Fast)) return preferred;
        if (availability is null || availability.HasEncoder(preferred)) return preferred;
        return FallbackCodecFor(pref);
    }

    private static string PickFastCodec(CodecPreference pref, IEncoderAvailability? availability)
    {
        if (availability is null) return FastHardwareOrder[0];
        foreach (var candidate in FastHardwareOrder)
            if (availability.WorksAsEncoder(candidate)) return candidate;
        return FallbackCodecFor(pref);
    }

    public static double HardwareBitrateYield(string codec, ComplexityProfile complexity, double scale, double fps)
        => CodecModel.IsHardware(codec) && !complexity.AppliesTo(codec, scale, fps)
            ? CodecModel.HardwareBitrateYield
            : 1.0;

    // The bias points both ways. Above 1 it aims high for an encoder that lands short; below 1 it
    // trims the request for one that overspends. A measured yield above 1 means the attempt spent
    // past the request, so the next request is divided by it. The trim is bounded by the same
    // CodecModel.HardwareBitrateYield the upward direction uses, so both carry equal weight.
    public static double HardwareDeliveryBias(double? measuredEfficiency)
    {
        if (measuredEfficiency is not double yield || !double.IsFinite(yield) || yield <= 0) return 1.0;
        return Math.Clamp(1.0 / yield, CodecModel.HardwareBitrateYield, 1.0 / CodecModel.HardwareBitrateYield);
    }

    private static void AddHardwareYieldNote(string codec, ComplexityProfile complexity, Layout best, List<string> reason, List<ReasonNote> reasonCodes)
    {
        var yield = HardwareBitrateYield(codec, complexity, best.Scale, best.Fps);
        if (yield >= 1.0) return;
        reason.Add($"{codec} was not calibrated on this source, and a hardware encoder can land up to {(1 - yield) * 100:0.#}% below the bitrate it is given, so the estimate carries that much room underneath while the requested bitrate stays on the target");
        reasonCodes.Add(new ReasonNote(ReasonCode.HardwareBitrateBias, Factor: yield, FallbackCodec: codec));
    }

    private static string PickAudioCodec() => "aac";

    private static string PickPreset(string codec, CodecPreference pref, SpeedMode speed)
    {
        if (CodecModel.IsHardware(codec))
        {
            var preset = FfmpegArguments.DefaultPreset(codec);
            if (speed == SpeedMode.Fast) preset = OneStepFaster(codec, preset);
            return FfmpegArguments.IsValidPreset(codec, preset) ? preset : FfmpegArguments.DefaultPreset(codec);
        }
        if (codec == "libsvtav1") return "6";
        return pref == CodecPreference.Fast ? "medium" : "slow";
    }

    private static string OneStepFaster(string codec, string preset)
    {
        if (preset.Length < 2 || preset[0] != 'p') return preset;
        if (!int.TryParse(preset[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level)) return preset;
        var faster = "p" + Math.Max(1, level - 1).ToString(CultureInfo.InvariantCulture);
        return FfmpegArguments.IsValidPreset(codec, faster) ? faster : preset;
    }
}
