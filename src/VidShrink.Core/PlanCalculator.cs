namespace VidShrink.Core;

public sealed class PlanOptions
{
    public double TargetMb { get; set; } = 25;
    public Intent Intent { get; set; } = Intent.Sharing;
    public CodecPreference Codec { get; set; } = CodecPreference.Compatible;
    public bool AllowResolutionDrop { get; set; } = true;
    public bool AllowFpsDrop { get; set; } = true;
}

public sealed record PlanResult(EncodePlan Plan, SizeEstimate Estimate, double PredictedQuality, ComplexityProfile Profile, StrategyAdvice Advice);

public static class PlanCalculator
{
    private const double ContainerOverhead = 0.995;
    private const double CrfFitMargin = 0.94;
    private const double MinScale = 0.25;
    private const double ScaleStep = 0.02;
    private const int MinHeight = 240;
    private const double MinFps = 12.0;
    private const int MinVideoBitrateK = 48;

    public static EncodePlan Build(MediaInfo info, PlanOptions options)
        => BuildDetailed(info, options, null).Plan;

    public static PlanResult BuildDetailed(MediaInfo info, PlanOptions options, ComplexityProfile? profile)
    {
        var complexity = profile ?? ComplexityProfile.FromSourceBitrate(info);
        var regime = CompressionStrategy.RegimeFor(info.FileSizeMb, options.TargetMb);
        var ratio = CompressionStrategy.Ratio(info.FileSizeMb, options.TargetMb);
        var notes = new List<AdviceCode>();

        var preference = options.Codec == CodecPreference.Auto
            ? CompressionStrategy.AutoPreference(regime)
            : options.Codec;
        var codec = PickCodec(preference);
        var suggestedPreference = CompressionStrategy.AutoPreference(regime);

        if (options.Codec != CodecPreference.Auto && suggestedPreference == CodecPreference.MaxCompression && preference == CodecPreference.Compatible)
            notes.Add(AdviceCode.CodecUpgradeRecommended);
        if (preference == CodecPreference.Fast && regime is CompressionRegime.Aggressive or CompressionRegime.Extreme)
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
            AllowFpsDrop = options.AllowFpsDrop && CompressionStrategy.AllowsFpsDrop(regime)
        };

        var best = SearchLayout(info, effective, complexity, codec, videoK);
        var reason = new List<string>();

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
        }
        if (best.Fps < info.Fps - 0.01)
        {
            notes.Add(AdviceCode.FrameRateReduced);
            reason.Add($"frame rate reduced to {best.Fps:0.##} to keep per-frame detail");
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
                best = recovered;
                notes.Remove(AdviceCode.ResolutionReduced);
                notes.Remove(AdviceCode.FrameRateReduced);
                if (best.Width != info.Width || best.Height != info.Height) notes.Add(AdviceCode.ResolutionReduced);
                if (best.Fps < info.Fps - 0.01) notes.Add(AdviceCode.FrameRateReduced);
                ceilingBppf = complexity.BppfAtCrf(codec, ceilingCrf, best.Scale, best.Fps, info.Fps);
                ceilingVideoK = ceilingBppf * best.Width * best.Height * best.Fps / 1000.0;
                ceilingSizeMb = SizeMb(ceilingVideoK, audioK, info.DurationSeconds);
            }

            notes.Add(AdviceCode.BudgetIsGenerous);
            notes.Add(AdviceCode.QualityCeilingReached);
            reason.Add($"the budget affords CRF {budgetCrf:0.#}, better than the CRF {ceilingCrf:0} transparency ceiling for this intent, so the encoder stops at the ceiling and delivers about {ceilingSizeMb:0.0} MB instead of padding the file to {options.TargetMb:0.##} MB");
            plan = NewPlan(codec, effective, info, best, audioK, audioChannels);
            plan.Mode = "crf";
            plan.Crf = (int)Math.Round(ceilingCrf);
            plan.VideoBitrateK = (int)Math.Round(Math.Max(ceilingVideoK, MinVideoBitrateK));
        }
        else
        {
            notes.Add(AdviceCode.TargetEnforcedTwoPass);
            reason.Add($"the budget lands near CRF {budgetCrf:0.#}, short of the CRF {ceilingCrf:0} ceiling, so two-pass VBR spends the whole {options.TargetMb:0.##} MB");
            plan = NewPlan(codec, effective, info, best, audioK, audioChannels);
            plan.Mode = "2pass";
            plan.VideoBitrateK = (int)Math.Round(videoK);
        }

        reason.Add($"predicted quality {best.Score:0.#}/100{(complexity.Measured ? $" from a measured sample (bppf {complexity.ReferenceBppf:0.0000}, detail falloff {complexity.DetailExponent:0.00})" : " estimated from the source bitrate")}");
        plan.Reason = string.Join("; ", reason);

        var estimate = Estimate(plan, info, complexity);
        var advice = new StrategyAdvice(regime, ratio, PickCodec(suggestedPreference), suggestedPreference, best.Score, notes);
        return new PlanResult(plan, estimate, best.Score, complexity, advice);
    }

    private static EncodePlan NewPlan(string codec, PlanOptions options, MediaInfo info, Layout best, int audioK, int? audioChannels) => new()
    {
        Codec = codec,
        AudioCodec = info.HasAudio && audioK > 0 ? PickAudioCodec() : null,
        AudioBitrateK = audioK,
        AudioChannels = audioChannels,
        Width = best.Width,
        Height = best.Height,
        Fps = best.Fps,
        Preset = PickPreset(codec, options.Codec)
    };

    public static SizeEstimate Estimate(EncodePlan plan, MediaInfo info, ComplexityProfile? profile)
    {
        var complexity = profile ?? ComplexityProfile.FromSourceBitrate(info);

        if (plan.ModeEnum == EncodeMode.TwoPass)
        {
            var expected = SizeMb(plan.VideoBitrateK, plan.AudioBitrateK, info.DurationSeconds);
            return new SizeEstimate(expected, expected * 0.96, expected * 1.04, complexity.Measured, true);
        }

        var scale = info.Height <= 0 ? 1.0 : (double)plan.Height / info.Height;
        var bppf = complexity.BppfAtCrf(plan.Codec, plan.Crf ?? CodecModel.ReferenceCrf(plan.Codec), scale, plan.Fps, info.Fps);
        var videoK = bppf * plan.Width * plan.Height * plan.Fps / 1000.0;
        var expectedMb = SizeMb(videoK, plan.AudioBitrateK, info.DurationSeconds);
        var band = complexity.Measured ? 0.14 : 0.32;
        return new SizeEstimate(expectedMb, expectedMb * (1 - band), expectedMb * (1 + band), complexity.Measured, false);
    }

    public static EncodePlan Correct(EncodePlan plan, double actualMb, double targetMb, double durationSeconds)
    {
        var corrected = plan.Clone();
        var desiredMb = targetMb * CrfFitMargin;
        var audioMb = SizeMb(0, plan.AudioBitrateK, durationSeconds);
        var actualVideoMb = Math.Max(actualMb - audioMb, 0.01);
        var desiredVideoMb = Math.Max(desiredMb - audioMb, 0.01);
        var factor = desiredVideoMb / actualVideoMb;
        var totalBudgetK = desiredMb * 8192.0 * ContainerOverhead / Math.Max(durationSeconds, 0.1);
        var videoBudgetK = Math.Max(1, totalBudgetK - plan.AudioBitrateK);
        var previousVideoK = plan.VideoBitrateK;
        corrected.Mode = "2pass";
        corrected.Crf = null;
        corrected.VideoBitrateK = Math.Max(1, (int)Math.Round(Math.Min(previousVideoK * factor, videoBudgetK)));
        corrected.Reason = $"retry: the previous attempt produced {actualMb:0.0} MB against a {targetMb:0.##} MB target; after reserving {audioMb:0.00} MB for audio, video bitrate was scaled by {factor:0.###} and capped to the remaining budget";
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

    private static string PickCodec(CodecPreference pref) => pref switch
    {
        CodecPreference.MaxCompression => "libx265",
        CodecPreference.Fast => "h264_nvenc",
        _ => "libx264"
    };

    private static string PickAudioCodec() => "aac";

    private static string PickPreset(string codec, CodecPreference pref)
    {
        if (CodecModel.UsesCq(codec)) return "p5";
        if (codec == "libsvtav1") return "6";
        return pref == CodecPreference.Fast ? "medium" : "slow";
    }
}
