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

public static class PlanCalculator
{
    private const double ContainerOverhead = 0.995;
    private const double CrfFitMargin = 0.94;
    private const double TwoPassUncertainty = 0.04;
    public const double CalibratedRetrySpread = 0.016;
    private const double MinScale = 0.25;
    private const double ScaleStep = 0.02;
    private const int MinHeight = 240;
    private const double MinFps = 12.0;
    private const int MinVideoBitrateK = 48;
    private const double HardwareUncalibratedBias = 1.06;

    private static readonly string[] FastHardwareOrder =
    {
        "av1_nvenc", "hevc_nvenc", "av1_qsv", "hevc_qsv", "av1_amf", "hevc_amf", "h264_nvenc"
    };

    public static EncodePlan Build(MediaInfo info, PlanOptions options, IEncoderAvailability? availability = null)
        => BuildDetailed(info, options, null, availability).Plan;

    public static PlanResult BuildDetailed(MediaInfo info, PlanOptions options, ComplexityProfile? profile, IEncoderAvailability? availability = null)
    {
        var complexity = profile ?? ComplexityProfile.FromSourceBitrate(info);
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
        if (CostsQualityInHardware(codec) && regime is CompressionRegime.Aggressive or CompressionRegime.Extreme)
            notes.Add(AdviceCode.HardwareCodecCostsQuality);
        if (regime == CompressionRegime.Extreme)
            notes.Add(AdviceCode.ExtremeRatioWarning);

        var totalK = options.TargetMb * 8192.0 / Math.Max(info.DurationSeconds, 0.1);
        var (audioK, audioChannels) = PickAudio(info, options, regime, totalK, notes);
        var videoK = Math.Max(MinVideoBitrateK, totalK * ContainerOverhead - audioK);

        var effective = new PlanOptions
        {
            TargetMb = options.TargetMb,
            Intent = options.Intent,
            Codec = preference,
            AllowResolutionDrop = options.AllowResolutionDrop && CompressionStrategy.AllowsResolutionDrop(regime),
            AllowFpsDrop = options.AllowFpsDrop && CompressionStrategy.AllowsFpsDrop(regime),
            SpeedMode = options.SpeedMode
        };

        var best = SearchLayout(info, effective, complexity, codec, videoK);

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

        var budgetBppf = videoK * 1000.0 / ((double)best.Width * best.Height * best.Fps);
        var budgetCrf = complexity.CrfForBppf(codec, budgetBppf, best.Scale, best.Fps, info.Fps);
        var ceilingCrf = TransparencyCrf(codec, options.Intent);

        EncodePlan plan;
        var ceilingBppf = complexity.BppfAtCrf(codec, ceilingCrf, best.Scale, best.Fps, info.Fps);
        var ceilingVideoK = ceilingBppf * best.Width * best.Height * best.Fps / 1000.0;
        var ceilingSizeMb = SizeMb(ceilingVideoK, audioK, info.DurationSeconds);

        if (budgetCrf <= ceilingCrf && ceilingSizeMb <= options.TargetMb * CrfFitMargin)
        {
            var recovered = RecoverLayoutAtCeiling(info, effective, complexity, codec, best, ceilingCrf, audioK, options.TargetMb);
            if (recovered.Scale > best.Scale + 1e-6 || recovered.Fps > best.Fps + 0.01)
            {
                reason.Add($"the ceiling left budget unused, so resolution was restored to {recovered.Width}x{recovered.Height}@{recovered.Fps:0.##} — the largest layout that still fits the target at CRF {ceilingCrf:0}");
                reasonCodes.Add(new ReasonNote(ReasonCode.ResolutionRestoredAtCeiling, Width: recovered.Width, Height: recovered.Height, Fps: recovered.Fps, Crf: ceilingCrf));
                best = recovered;
                notes.Remove(AdviceCode.ResolutionReduced);
                notes.Remove(AdviceCode.FrameRateReduced);
                reasonCodes.RemoveAll(n => n.Code is ReasonCode.ResolutionScaled or ReasonCode.FrameRateReduced);
                if (best.Width != info.Width || best.Height != info.Height) notes.Add(AdviceCode.ResolutionReduced);
                if (best.Fps < info.Fps - 0.01) notes.Add(AdviceCode.FrameRateReduced);
                ceilingBppf = complexity.BppfAtCrf(codec, ceilingCrf, best.Scale, best.Fps, info.Fps);
                ceilingVideoK = ceilingBppf * best.Width * best.Height * best.Fps / 1000.0;
                ceilingSizeMb = SizeMb(ceilingVideoK, audioK, info.DurationSeconds);
            }

            notes.Add(AdviceCode.BudgetIsGenerous);
            notes.Add(AdviceCode.QualityCeilingReached);
            reason.Add($"the budget affords CRF {budgetCrf:0.#}, better than the CRF {ceilingCrf:0} transparency ceiling for this intent, so the encoder stops at the ceiling and delivers about {ceilingSizeMb:0.0} MB instead of padding the file to {options.TargetMb:0.##} MB");
            reasonCodes.Add(new ReasonNote(ReasonCode.BudgetExceedsCeiling, BudgetCrf: budgetCrf, Crf: ceilingCrf, Mb: ceilingSizeMb, TargetMb: options.TargetMb));
            plan = NewPlan(codec, effective, info, best, audioK, audioChannels, hdr);
            plan.Mode = "crf";
            plan.Crf = (int)Math.Round(ceilingCrf);
            plan.VideoBitrateK = (int)Math.Round(Math.Max(ceilingVideoK, MinVideoBitrateK));

            if (options.FillPolicy == FillPolicy.FillTarget)
            {
                var band = FillBand.For(options.TargetMb);
                if (ceilingSizeMb < band.LowerMb)
                {
                    var (minCrf, _) = CodecModel.CrfRange(codec);
                    var bandCenterMb = (band.LowerMb + band.UpperMb) / 2.0;
                    var totalBudgetK = bandCenterMb * 8192.0 * ContainerOverhead / Math.Max(info.DurationSeconds, 0.1);
                    var desiredVideoK = Math.Max(MinVideoBitrateK, totalBudgetK - audioK);
                    var desiredBppf = desiredVideoK * 1000.0 / ((double)best.Width * best.Height * best.Fps);
                    var fillCrf = complexity.CrfForBppf(codec, desiredBppf, best.Scale, best.Fps, info.Fps);

                    if (fillCrf >= minCrf)
                    {
                        plan.Crf = (int)Math.Round(fillCrf);
                        plan.VideoBitrateK = (int)Math.Round(Math.Max(desiredVideoK, MinVideoBitrateK));
                        reason.Add($"the fill target policy lowered CRF to {fillCrf:0.#} instead of stopping at the transparency ceiling, landing near {bandCenterMb:0.0} MB inside the {band.LowerMb:0.0}-{band.UpperMb:0.0} MB band");
                        reasonCodes.Add(new ReasonNote(ReasonCode.FillCrfLowered, Crf: fillCrf, Mb: bandCenterMb, TargetMb: band.UpperMb, BandLowerMb: band.LowerMb));
                    }
                    else
                    {
                        plan.Mode = "2pass";
                        plan.Crf = null;
                        var fillBias = HardwareBitrateBias(codec, complexity, best.Scale, best.Fps);
                        plan.VideoBitrateK = (int)Math.Round(Math.Max(desiredVideoK * fillBias, MinVideoBitrateK));
                        reason.Add($"CRF floor {minCrf} was reached before the fill band, so two-pass VBR targets the {bandCenterMb:0.0} MB band center directly");
                        reasonCodes.Add(new ReasonNote(ReasonCode.FillTwoPassBandCenter, Crf: minCrf, Mb: bandCenterMb, TargetMb: band.UpperMb, BandLowerMb: band.LowerMb));
                        AddHardwareBiasNote(codec, fillBias, reason, reasonCodes);
                    }
                }
            }
        }
        else
        {
            notes.Add(AdviceCode.TargetEnforcedTwoPass);
            reason.Add($"the budget lands near CRF {budgetCrf:0.#}, short of the CRF {ceilingCrf:0} ceiling, so two-pass VBR spends the whole {options.TargetMb:0.##} MB");
            reasonCodes.Add(new ReasonNote(ReasonCode.BudgetBelowCeilingTwoPass, BudgetCrf: budgetCrf, Crf: ceilingCrf, TargetMb: options.TargetMb));
            plan = NewPlan(codec, effective, info, best, audioK, audioChannels, hdr);
            plan.Mode = "2pass";
            var enforcedBias = HardwareBitrateBias(codec, complexity, best.Scale, best.Fps);
            plan.VideoBitrateK = (int)Math.Round(videoK * enforcedBias);
            AddHardwareBiasNote(codec, enforcedBias, reason, reasonCodes);
        }

        reason.Add($"predicted quality {best.Score:0.#}/100{(complexity.Measured ? $" from a measured sample (bppf {complexity.ReferenceBppf:0.0000}, detail falloff {complexity.DetailExponent:0.00})" : " estimated from the source bitrate")}");
        reasonCodes.Add(complexity.Measured
            ? new ReasonNote(ReasonCode.PredictedQualityMeasured, Score: best.Score, Bppf: complexity.ReferenceBppf, DetailExponent: complexity.DetailExponent)
            : new ReasonNote(ReasonCode.PredictedQualityEstimated, Score: best.Score));
        plan.Reason = string.Join("; ", reason);
        plan.ReasonCodes = reasonCodes;

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

    public static SizeEstimate Estimate(EncodePlan plan, MediaInfo info, ComplexityProfile? profile)
    {
        var complexity = profile ?? ComplexityProfile.FromSourceBitrate(info);

        if (plan.ModeEnum == EncodeMode.TwoPass)
        {
            var expected = SizeMb(plan.VideoBitrateK, plan.AudioBitrateK, info.DurationSeconds);
            return new SizeEstimate(expected, expected * (1 - TwoPassUncertainty), expected * (1 + TwoPassUncertainty), complexity.Measured, true);
        }

        var scale = info.Height <= 0 ? 1.0 : (double)plan.Height / info.Height;
        var bppf = complexity.BppfAtCrf(plan.Codec, plan.Crf ?? CodecModel.ReferenceCrf(plan.Codec), scale, plan.Fps, info.Fps);
        var videoK = bppf * plan.Width * plan.Height * plan.Fps / 1000.0;
        var expectedMb = SizeMb(videoK, plan.AudioBitrateK, info.DurationSeconds);
        var band = complexity.EstimateBandFor(plan.Codec, scale, plan.Fps);
        return new SizeEstimate(expectedMb, expectedMb * (1 - band), expectedMb * (1 + band), complexity.Measured, false);
    }

    public static double RetryAimMb(double targetMb, ComplexityProfile? profile, EncodePlan plan, double sourceHeight, out bool calibrated)
    {
        var scale = sourceHeight > 0 ? plan.Height / sourceHeight : 1.0;
        calibrated = profile is not null && profile.AppliesTo(plan.Codec, scale, plan.Fps);
        if (!calibrated) return targetMb * CrfFitMargin;

        var band = FillBand.For(targetMb);
        var ceilingAim = targetMb / (1 + CalibratedRetrySpread);
        var bandAim = band.LowerMb / (1 - CalibratedRetrySpread);
        var bandCenterMb = (band.LowerMb + band.UpperMb) / 2.0;
        return Math.Max(band.HardFloorMb, Math.Min(ceilingAim, Math.Max(bandAim, bandCenterMb)));
    }

    public static EncodePlan Correct(EncodePlan plan, double actualMb, double targetMb, double durationSeconds, bool fillUnderBand = false, ComplexityProfile? profile = null, double sourceHeight = 0)
    {
        var corrected = plan.Clone();
        var audioMb = SizeMb(0, plan.AudioBitrateK, durationSeconds);
        var previousVideoK = plan.VideoBitrateK;
        var aimMb = RetryAimMb(targetMb, profile, plan, sourceHeight, out var calibrated);
        var aimSource = calibrated
            ? $"aimed at {aimMb:0.0} MB from the calibrated ±{CalibratedRetrySpread * 100:0.#}% retry error"
            : $"aimed at {aimMb:0.0} MB from the uncalibrated {CrfFitMargin * 100:0.#}% margin";
        corrected.Mode = "2pass";
        corrected.Crf = null;

        if (fillUnderBand)
        {
            var band = FillBand.For(targetMb);
            var factorUp = aimMb / Math.Max(actualMb, 0.01);
            var totalBudgetKUp = aimMb * 8192.0 * ContainerOverhead / Math.Max(durationSeconds, 0.1);
            var videoBudgetKUp = Math.Max(1, totalBudgetKUp - plan.AudioBitrateK);
            corrected.VideoBitrateK = Math.Max(1, (int)Math.Round(Math.Min(previousVideoK * factorUp, videoBudgetKUp)));
            corrected.Reason = $"retry: the previous attempt produced {actualMb:0.0} MB, below the {band.HardFloorMb:0.0} MB floor for a {targetMb:0.##} MB target; video bitrate was scaled by {factorUp:0.###} and {aimSource}, which keeps the whole predicted spread under the hard ceiling";
            corrected.ReasonCodes = new List<ReasonNote> { new(ReasonCode.RetryScaled, Mb: actualMb, TargetMb: targetMb, AudioMb: audioMb, Factor: factorUp, BandLowerMb: band.LowerMb) };
            return corrected;
        }

        var desiredMb = aimMb;
        var actualVideoMb = Math.Max(actualMb - audioMb, 0.01);
        var desiredVideoMb = Math.Max(desiredMb - audioMb, 0.01);
        var factor = desiredVideoMb / actualVideoMb;
        var totalBudgetK = desiredMb * 8192.0 * ContainerOverhead / Math.Max(durationSeconds, 0.1);
        var videoBudgetK = Math.Max(1, totalBudgetK - plan.AudioBitrateK);
        corrected.VideoBitrateK = Math.Max(1, (int)Math.Round(Math.Min(previousVideoK * factor, videoBudgetK)));
        corrected.Reason = $"retry: the previous attempt produced {actualMb:0.0} MB against a {targetMb:0.##} MB target; after reserving {audioMb:0.00} MB for audio, video bitrate was scaled by {factor:0.###}, {aimSource}";
        corrected.ReasonCodes = new List<ReasonNote> { new(ReasonCode.RetryScaled, Mb: actualMb, TargetMb: targetMb, AudioMb: audioMb, Factor: factor, BandLowerMb: FillBand.For(targetMb).LowerMb) };
        return corrected;
    }

    public static double? EstimatedMb(EncodePlan plan, double durationSeconds)
        => plan.ModeEnum == EncodeMode.Crf ? null : SizeMb(plan.VideoBitrateK, plan.AudioBitrateK, durationSeconds);

    public static double BitsPerPixel(double videoK, int width, int height, double fps)
        => videoK * 1000.0 / Math.Max(1.0, (double)width * height * fps);

    private static double SizeMb(double videoK, double audioK, double durationSeconds)
        => (videoK + audioK) * durationSeconds / 8192.0 / ContainerOverhead;

    private sealed record Layout(int Width, int Height, double Fps, double Scale, double Score);

    private static Layout RecoverLayoutAtCeiling(MediaInfo info, PlanOptions options, ComplexityProfile complexity, string codec, Layout fallback, double ceilingCrf, int audioK, double targetMb)
    {
        Layout? best = null;

        foreach (var fps in FpsCandidates(info, options))
        foreach (var scale in ScaleCandidates(options))
        {
            if (scale < fallback.Scale - 1e-6) continue;

            var (width, height) = Dimensions(info, scale);
            if (height < MinHeight && height < info.Height) continue;
            if (width < 2 || height < 2) continue;

            var effectiveScale = (double)height / Math.Max(1, info.Height);
            var bppf = complexity.BppfAtCrf(codec, ceilingCrf, effectiveScale, fps, info.Fps);
            var videoK = bppf * width * height * fps / 1000.0;
            if (SizeMb(videoK, audioK, info.DurationSeconds) > targetMb * CrfFitMargin) continue;

            var score = -ScalePenalty(effectiveScale) - FpsPenalty(fps, info.Fps);
            if (best is null || score > best.Score)
                best = new Layout(width, height, fps, effectiveScale, score);
        }

        return best is null ? fallback : best with { Score = fallback.Score };
    }

    private static Layout SearchLayout(MediaInfo info, PlanOptions options, ComplexityProfile complexity, string codec, double videoK)
    {
        Layout? best = null;

        foreach (var fps in FpsCandidates(info, options))
        foreach (var scale in ScaleCandidates(options))
        {
            var (width, height) = Dimensions(info, scale);
            if (height < MinHeight && height < info.Height) continue;
            if (width < 2 || height < 2) continue;

            var effectiveScale = (double)height / Math.Max(1, info.Height);
            var required = complexity.RequiredBppf(codec, effectiveScale, fps, info.Fps);
            var provided = videoK * 1000.0 / ((double)width * height * fps);

            var rate = CodecModel.QualityAtReference - CodecModel.QualityPerHalving * Math.Log2(Math.Max(required, 1e-9) / Math.Max(provided, 1e-9));
            rate = Math.Min(rate, CodecModel.QualityLimit(codec));

            var score = rate - ScalePenalty(effectiveScale) - FpsPenalty(fps, info.Fps);

            if (best is null || score > best.Score)
                best = new Layout(width, height, fps, effectiveScale, score);
        }

        if (best is not null) return best;

        var (fallbackWidth, fallbackHeight) = Dimensions(info, 1.0);
        return new Layout(fallbackWidth, fallbackHeight, info.Fps, 1.0, 0);
    }

    private static IEnumerable<double> ScaleCandidates(PlanOptions options)
    {
        if (!options.AllowResolutionDrop)
        {
            yield return 1.0;
            yield break;
        }
        for (var scale = 1.0; scale >= MinScale - 1e-9; scale -= ScaleStep)
            yield return Math.Round(scale, 4);
    }

    private static IEnumerable<double> FpsCandidates(MediaInfo info, PlanOptions options)
    {
        var source = info.Fps <= 0 ? 30 : info.Fps;
        yield return source;
        if (!options.AllowFpsDrop) yield break;

        var seen = new HashSet<double> { Math.Round(source, 3) };
        foreach (var candidate in new[] { source / 2.0, source / 2.5, source / 3.0, 30.0, 25.0, 24.0 })
        {
            if (candidate >= source - 0.01 || candidate < MinFps) continue;
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

    private static double ScalePenalty(double scale)
    {
        if (scale >= 0.999) return 0;
        return CodecModel.ScalePenaltyScale * Math.Pow(1.0 / Math.Max(scale, 0.05) - 1.0, CodecModel.ScalePenaltyExponent);
    }

    private static double FpsPenalty(double fps, double sourceFps)
    {
        if (fps >= sourceFps - 0.01) return 0;
        var penalty = CodecModel.FpsPenaltyPerHalving * Math.Log2(sourceFps / Math.Max(fps, 1.0));
        if (fps < 20) penalty += 12.0 * (20.0 - fps) / 8.0;
        return penalty;
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

    private static bool CostsQualityInHardware(string codec)
        => CodecModel.IsHardware(codec)
           && !codec.Equals("av1_nvenc", StringComparison.OrdinalIgnoreCase)
           && !codec.Equals("av1_qsv", StringComparison.OrdinalIgnoreCase);

    private static double HardwareBitrateBias(string codec, ComplexityProfile complexity, double scale, double fps)
        => CodecModel.IsHardware(codec) && !complexity.AppliesTo(codec, scale, fps)
            ? HardwareUncalibratedBias
            : 1.0;

    private static void AddHardwareBiasNote(string codec, double bias, List<string> reason, List<ReasonNote> reasonCodes)
    {
        if (bias <= 1.0) return;
        reason.Add($"{codec} was not calibrated on this source, and hardware encoders land below a requested bitrate, so the two-pass target was raised by {(bias - 1) * 100:0.#}%");
        reasonCodes.Add(new ReasonNote(ReasonCode.HardwareBitrateBias, Factor: bias, FallbackCodec: codec));
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
