using System;
using System.Collections.Generic;
using System.Linq;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App;

public partial class MainWindow
{
    public static bool ShowsMeasuredQualityStop(EncodePlan plan, ReasonNote note, FillPolicy fillPolicy) =>
        plan.StopsShortOfBandOnPurpose
        && fillPolicy == FillPolicy.FillTarget
        && note.Mb >= FillBand.For(note.TargetMb).HardFloorMb;

    /// <summary>
    /// Dusme cumlesinin yerellestirme anahtari. Core uc ayri sebep uretiyor
    /// (<see cref="EncoderFallbackCause"/>) ve ucu de kullaniciya ayri cumleyle gidiyor:
    /// olculmemis ya da derlemede hic olmayan bir aday icin "bu makinede kullanilamadi"
    /// demek, yapilmamis bir olcumun sonucunu bildirmektir.
    /// </summary>
    internal static string EncoderFallbackReasonKey(ReasonNote note) => note.FallbackCause switch
    {
        EncoderFallbackCause.NotInBuild => "main.reason.encoder-fallback-not-in-build",
        EncoderFallbackCause.NotMeasured => "main.reason.encoder-fallback-not-measured",
        EncoderFallbackCause.NotWorking => "main.reason.encoder-fallback-not-working",
        _ => "main.reason.encoder-fallback-not-working"
    };

    /// <summary>Ayni ayrimin tavsiye satirindaki karsiligi; GPU kolu kendi anahtarini korur.</summary>
    internal static string EncoderFallbackAdviceKey(EncoderFallbackCause cause) => cause switch
    {
        EncoderFallbackCause.NotInBuild => "main.advice.encoder-fallback-not-in-build",
        EncoderFallbackCause.NotMeasured => "main.advice.encoder-fallback-not-measured",
        EncoderFallbackCause.NotWorking => "main.advice.encoder-fallback-not-working",
        _ => "main.advice.encoder-fallback-not-working"
    };

    internal static EncoderFallbackCause EncoderFallbackCauseOf(EncodePlan? plan) =>
        plan?.ReasonCodes.FirstOrDefault(note => note.Code == ReasonCode.EncoderFallback)?.FallbackCause
        ?? EncoderFallbackCause.NotWorking;

    private List<string> ReasonLines(EncodePlan plan)
    {
        var parts = new List<string>();
        foreach (var note in plan.ReasonCodes)
        {
            var text = note.Code switch
            {
                ReasonCode.ResolutionScaled => Say("main.reason.resolution-scaled",
                    note.Width, note.Height, Bicim.Yuzde.Hazir(note.ScalePercent, Strings.Culture)),
                ReasonCode.FrameRateReduced => Say("main.reason.frame-rate-reduced", Bicim.Kare(note.Fps, Strings.Culture)),
                ReasonCode.ResolutionRestoredAtCeiling => Say("main.reason.resolution-restored",
                    note.Width, note.Height, Bicim.Kare(note.Fps, Strings.Culture), Num(note.Crf, "0")),
                ReasonCode.BudgetExceedsCeiling => ShowsMeasuredQualityStop(plan, note, CurrentOptions().FillPolicy)
                    ? Say("main.reason.measured-quality-stop",
                        Num(note.Crf, "0"), Num(note.Mb, "0.0"), Num(note.TargetMb, "0.##"))
                    : Say("main.reason.budget-exceeds-ceiling",
                        Num(note.BudgetCrf, "0.#"), Num(note.Crf, "0"), Num(note.Mb, "0.0"), Num(note.TargetMb, "0.##")),
                ReasonCode.BudgetBelowCeilingTwoPass => Say("main.reason.budget-below-ceiling",
                    Num(note.BudgetCrf, "0.#"), Num(note.Crf, "0"), Num(note.TargetMb, "0.##")),
                ReasonCode.PredictedQualityMeasured => Say("main.reason.quality-measured",
                    Num(note.Score, "0.#"), Num(note.Bppf, "0.0000"), Num(note.DetailExponent, "0.00")),
                ReasonCode.PredictedQualityEstimated => Say("main.reason.quality-estimated", Num(note.Score, "0.#")),
                ReasonCode.RetryScaled => Say("main.reason.retry-scaled",
                    Num(note.Mb, "0.0"), Num(note.TargetMb, "0.##"), Num(note.AudioMb, "0.00"), Num(note.Factor, "0.###")),
                ReasonCode.EncoderFallback => Say(EncoderFallbackReasonKey(note), note.RequestedCodec, note.FallbackCodec),
                ReasonCode.HdrTonemapped => Say("main.reason.hdr-tonemapped"),
                ReasonCode.FillCrfLowered => Say("main.reason.fill-crf-lowered",
                    Num(note.Crf, "0.#"), Num(note.Mb, "0.0"), Num(note.BandLowerMb, "0.0"), Num(note.TargetMb, "0.0")),
                ReasonCode.FillTwoPassBandCenter => Say("main.reason.fill-band-center",
                    Num(note.Crf, "0"), Num(note.Mb, "0.0")),
                ReasonCode.FillTwoPassBandTooNarrowForCrf => Say("main.reason.fill-band-narrow",
                    Bicim.Yuzde.Orandan(note.Factor, Strings.Culture),
                    Bicim.Yuzde.Orandan((note.TargetMb - note.BandLowerMb) / Math.Max(note.TargetMb, 0.01), Strings.Culture),
                    Num(note.Mb, "0.0")),
                ReasonCode.HardwareBitrateBias => Say("main.reason.hardware-bitrate-bias",
                    note.FallbackCodec, Bicim.Yuzde.Orandan(1 - note.Factor, Strings.Culture)),
                ReasonCode.SourceAlreadyUnderTarget => Say("main.reason.source-under-target",
                    Num(note.Mb, "0.0"), Num(note.TargetMb, "0.##")),
                ReasonCode.TargetCappedToSource => Say("main.reason.target-capped",
                    Num(note.Mb, "0.##"), Num(note.TargetMb, "0.##")),
                ReasonCode.ManualEncoderPathSupersededByCodec => Say("main.reason.manual-encoder-path-superseded",
                    note.ManualOverrideValue, note.FallbackCodec),
                ReasonCode.ManualEncoderPathUnmet => Say("main.reason.manual-encoder-path-unmet",
                    note.ManualOverrideValue, note.FallbackCodec),
                ReasonCode.ManualEncoderPathOverride => Say("main.reason.manual-encoder-path-override",
                    note.ManualOverrideValue, note.EngineWouldHaveChosen, note.FallbackCodec),
                ReasonCode.ManualAudioBitrateUnmet => Say("main.reason.manual-audio-bitrate-unmet",
                    Strings.BitHizi(note.ManualOverrideValue)),
                ReasonCode.ManualAudioBitrateSupersededByChannels => Say("main.reason.manual-audio-bitrate-superseded",
                    Strings.BitHizi(note.ManualOverrideValue)),
                ReasonCode.ManualAudioBitrateOverride => Say("main.reason.manual-audio-bitrate-override",
                    Strings.BitHizi(note.ManualOverrideValue), Strings.BitHizi(note.EngineWouldHaveChosen)),
                ReasonCode.ManualAudioChannelsUnmet => Say("main.reason.manual-audio-channels-unmet",
                    note.ManualOverrideValue),
                ReasonCode.ManualAudioChannelsOverride => Say("main.reason.manual-audio-channels-override",
                    note.ManualOverrideValue, note.EngineWouldHaveChosen),
                ReasonCode.ManualMinResolutionUnmet => Say("main.reason.manual-min-resolution-unmet",
                    note.ManualOverrideValue, note.Width, note.Height),
                ReasonCode.ManualMinResolutionOverride => Say("main.reason.manual-min-resolution-override",
                    note.ManualOverrideValue, note.EngineWouldHaveChosen, note.Width, note.Height),
                ReasonCode.ManualMinFpsUnmet => Say("main.reason.manual-min-fps-unmet",
                    note.ManualOverrideValue, Bicim.Kare(note.Fps, Strings.Culture)),
                ReasonCode.ManualMinFpsOverride => Say("main.reason.manual-min-fps-override",
                    note.ManualOverrideValue, note.EngineWouldHaveChosen, Bicim.Kare(note.Fps, Strings.Culture)),
                ReasonCode.ManualCrfClamped => Say("main.reason.manual-crf-clamped",
                    note.ManualOverrideValue, note.EngineWouldHaveChosen, Num(note.Crf, "0")),
                ReasonCode.ManualCrfOverride => Say("main.reason.manual-crf-override",
                    note.ManualOverrideValue, note.EngineWouldHaveChosen, Num(note.Crf, "0"), Num(note.Mb, "0.0")),
                ReasonCode.ManualModeSupersededByCrf => Say("main.reason.manual-mode-superseded-by-crf",
                    note.ManualOverrideValue),
                ReasonCode.ManualModeOverride => Say("main.reason.manual-mode-override",
                    note.ManualOverrideValue, note.EngineWouldHaveChosen),
                ReasonCode.ManualPresetOverride => Say("main.reason.manual-preset-override",
                    note.ManualOverrideValue, note.EngineWouldHaveChosen),
                ReasonCode.ManualTuneOverride => Say("main.reason.manual-tune-override",
                    note.ManualOverrideValue),
                ReasonCode.ManualPresetFirstPassRelaxed => Say("main.reason.manual-preset-first-pass-relaxed",
                    note.ManualOverrideValue, note.EngineWouldHaveChosen),
                ReasonCode.ManualOverrideDroppedOnPassThrough => Say("main.reason.manual-override-dropped-on-pass-through",
                    note.ManualOverrideValue, note.EngineWouldHaveChosen),
                ReasonCode.DarkContentHevc => Say("main.reason.dark-content-hevc",
                    Num(note.Score, "0.#"), note.RequestedCodec, note.FallbackCodec),
                _ => null
            };
            if (text is not null) parts.Add(text);
        }

        if (plan.Streams is { } streams)
            foreach (var note in streams.Notes) parts.Add(Say(StreamNoteKey(note)));

        return parts;
    }

    internal static string StreamNoteKey(StreamNote note) => "main.reason.stream." + StreamNotes.Slug(note);

    private List<string> StrategyLines()
    {
        if (_advice is not { } advice) return new List<string>();

        var regime = advice.Regime switch
        {
            CompressionRegime.Light => Say("main.reason.regime.light"),
            CompressionRegime.Balanced => Say("main.reason.regime.balanced"),
            CompressionRegime.Aggressive => Say("main.reason.regime.aggressive"),
            _ => Say("main.reason.regime.extreme")
        };

        var lines = new List<string>
        {
            Say("main.reason.ratio", Num(advice.Ratio, "0.#"), regime)
        };

        foreach (var note in advice.Notes.Distinct())
        {
            var text = AdviceLine(note, Strings.Language, ChkFastGpu.IsChecked == true,
                EncoderFallbackCauseOf(ActivePlan));
            if (text is not null) lines.Add(text);
        }

        return lines;
    }

    internal static readonly AdviceCode[] AdviceCodesWithoutText = Array.Empty<AdviceCode>();

    internal static string? AdviceLine(AdviceCode note, string language, bool fastGpu,
        EncoderFallbackCause fallbackCause = EncoderFallbackCause.NotWorking)
    {

        return note switch
        {
            AdviceCode.BudgetIsGenerous => Speak(language, "main.advice.budget-generous"),
            AdviceCode.CodecUpgradeRecommended => Speak(language, "main.advice.codec-upgrade"),
            AdviceCode.HardwareCodecCostsQuality => Speak(language, "main.advice.hardware-costs-quality"),
            AdviceCode.ExtremeRatioWarning => Speak(language, "main.advice.extreme-ratio"),
            AdviceCode.TargetBelowCodecFloor => Speak(language, "main.advice.below-codec-floor"),
            AdviceCode.FrameRateCutForFloor => Speak(language, "main.advice.frame-rate-for-floor"),
            AdviceCode.MotionCutIsCheap => Speak(language, "main.advice.motion-cut-cheap"),
            AdviceCode.MotionCutIsExpensive => Speak(language, "main.advice.motion-cut-expensive"),
            AdviceCode.ContentIsSimple => Speak(language, "main.advice.content-simple"),
            AdviceCode.ContentIsComplex => Speak(language, "main.advice.content-complex"),
            AdviceCode.ScaleSavesMuch => Speak(language, "main.advice.scale-saves-much"),
            AdviceCode.ScaleSavesLittle => Speak(language, "main.advice.scale-saves-little"),
            AdviceCode.ResolutionReduced => Speak(language, "main.advice.resolution-reduced"),
            AdviceCode.FrameRateReduced => Speak(language, "main.advice.frame-rate-reduced"),
            AdviceCode.TargetEnforcedTwoPass => Speak(language, "main.advice.two-pass"),
            AdviceCode.QualityCeilingReached => Speak(language, "main.advice.quality-ceiling"),
            AdviceCode.AudioReduced => Speak(language, "main.advice.audio-reduced"),
            AdviceCode.AudioMono => Speak(language, "main.advice.audio-mono"),
            AdviceCode.EncoderFallback => fastGpu
                ? Speak(language, "main.advice.encoder-fallback-gpu")
                : Speak(language, EncoderFallbackAdviceKey(fallbackCause)),
            AdviceCode.HdrTonemapped => Speak(language, "main.advice.hdr-tonemapped"),
            _ => null
        };
    }
}
