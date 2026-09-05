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

    /// <summary>
    /// Kullanicinin acikca sectigi kodlayici. <c>null</c> "secim yok" demektir ve motor
    /// <see cref="Codec"/>/<see cref="CompressionStrategy.AutoPreference"/> yolundan gectigi
    /// gibi gecmeye devam eder. Dolu oldugunda kodek kilitlenir; bitrate, preset, cozunurluk,
    /// fps ve ses karari yine otomatik hesaplanir, degisen tek sey hangi kodlayicinin
    /// kullanildigidir.
    ///
    /// Aile degil kodlayici adi tutuluyor ("libsvtav1", "av1_nvenc"): motorun yedege dusme
    /// mekanizmasi (<see cref="EncoderFallbackCause"/>) zaten kodlayici adi uzerinden calisiyor,
    /// bir aile secilseydi "bu ailede hangi kodlayici" sorusunu ikinci kez cozmek gerekirdi.
    /// Adlar <see cref="PlanParser.Parse"/>'in kabul ettigi kume ile ayni tutuluyor.
    /// </summary>
    public string? LockedCodec { get; set; } = null;

    /// <summary>
    /// Kullanicinin acikca sectigi kodlama kipi. <c>null</c> "secim yok" demektir; motor
    /// bugunku gibi butce/tavan karsilastirmasiyla crf/2pass arasinda kendi secer. Doluysa
    /// nihai kip zorlanir, kalan her sey (cozunurluk, fps, ses, bitrate tahmini) yine
    /// otomatik hesaplanir.
    /// </summary>
    public EncodeMode? LockedMode { get; set; } = null;

    /// <summary>
    /// Kullanicinin acikca verdigi CRF degeri. Doluysa hedef boyut artik zorlanmaz: CRF
    /// kazanir, hedef boyut bir tahmine doner (<see cref="EncodePlan.EffectiveTargetMb"/>
    /// degismez ama uretilen boyut artik bir butce degil bir kestirimdir). <see cref="LockedMode"/>
    /// doluysa bile bu alan onceliklidir, cunku acik bir sayi acik bir kipten daha az belirsiz.
    /// </summary>
    public double? LockedCrf { get; set; } = null;

    /// <summary>
    /// Kullanicinin acikca sectigi kodlayici on ayari (ör. "veryslow", "p2", "7").
    /// <see cref="FfmpegArguments.IsValidPreset"/>'in kabul ettigi kumeden olmali; degilse
    /// acik bir <see cref="ArgumentException"/> firlar, sessizce yakina yuvarlanmaz.
    /// </summary>
    public string? LockedPreset { get; set; } = null;

    /// <summary>Kullanicinin acikca verdigi ses hedefi (kbps). Doluysa <see cref="PickAudio"/>'nun hesabi yerine gecer.</summary>
    public int? LockedAudioKbps { get; set; } = null;

    /// <summary>Kullanicinin acikca sectigi ses kanal politikasi. <see cref="AudioChannelOverride.Auto"/> "secim yok" demektir.</summary>
    public AudioChannelOverride AudioChannels { get; set; } = AudioChannelOverride.Auto;

    /// <summary>
    /// Kullanicinin "en az bu cozunurluk" olarak verdigi taban (piksel yukseklik). Rejimin
    /// kendi tabanindan (<see cref="RegimeFloors.MinHeight"/>) dusukse yok sayilir — taban
    /// yalniz yukselebilir, motorun olculmus tabanini asagi cekemez.
    /// </summary>
    public int? MinResolutionHeight { get; set; } = null;

    /// <summary>
    /// Kullanicinin "en az bu kare hizi" olarak verdigi taban. <see cref="MinResolutionHeight"/>
    /// ile ayni kural: rejimin tabanini yalniz yukseltebilir.
    /// </summary>
    public double? MinFps { get; set; } = null;

    /// <summary>
    /// Kullanicinin kodlayici yolunu zorlamasi: yazilim ya da donanim. <see cref="LockedCodec"/>
    /// tek bir kodlayici adi seciyor, bu ise aile secmeden "yalniz yazilim" / "yalniz donanim"
    /// diyor — <see cref="LockedCodec"/> doluyken bu alan etkisizdir, cunku ad zaten yolu belirler.
    /// </summary>
    public EncoderPathOverride EncoderPath { get; set; } = EncoderPathOverride.Auto;
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

public sealed record PlanResult(EncodePlan Plan, SizeEstimate Estimate, double PredictedQuality, ComplexityProfile Profile, StrategyAdvice Advice)
{
    /// <summary>
    /// Donanim yoklamasi henuz bitmedi; plandaki kodlayici ve HDR karari gecici. Cagiran
    /// bu plana bakip "donanim yok" dememeli, olcum gelince yeniden hesap yapmali.
    /// </summary>
    public bool HardwareNotMeasured { get; init; }
}

/// <summary>
/// Yoklamanin ucuncu durumu: "henuz olculmedi". <see cref="IEncoderAvailability"/> yalniz
/// evet/hayir soyleyebiliyor ve olculmemis bir kodlayici orada "hayir" gorunuyor; bu, henuz
/// sorulmamis bir donanimi yokmus gibi gostermek demek. Bu arayuz o ayrimi tasiyor.
///
/// Gecici: T129 ayni ayrimi <c>EncoderProbeResult</c> uzerinde aciyor. O is birlestiginde
/// buradaki temsil oraya devredilir ve bu arayuz kalkar.
/// </summary>
public interface IEncoderMeasurementState
{
    /// <summary>Kodlayicinin calisip calismadigi olculdu mu.</summary>
    bool IsMeasured(string codec);

    /// <summary>Kodlayicinin HDR10 piksel bicimi olculdu mu.</summary>
    bool IsHdr10Measured(string codec);
}

public readonly record struct LayoutScoreParts(
    double Required,
    double Provided,
    double Rate,
    double ScalePenalty,
    double FpsPenalty,
    double Hysteresis,
    double Score);

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

    /// <summary>
    /// Karsilanamayan ses istegini anlatan iki sabit metin. Hem gerekce cumlesine hem
    /// <see cref="ReasonNote.EngineWouldHaveChosen"/> alanina ayni dizge gidiyor: not ile
    /// cumle ayri yazilirsa ikisi ayri seyler soylemeye baslar.
    /// </summary>
    private const string NoAudioStream = "kaynakta ses akisi yok";

    private const string SilencedByChannels = "ses kanali=None";
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
        var probe = new ProbeState();
        var result = BuildDetailedCore(info, options, profile, availability, probe);
        result.Plan.CodecNotMeasured = probe.CodecNotMeasured;
        return probe.NotMeasured ? result with { HardwareNotMeasured = true } : result;
    }

    /// <summary>
    /// Hesap boyunca "yoklama henuz bitmedi" isaretini tasiyan kap. Cikis noktalari cok
    /// oldugu icin isaret <c>out</c> ile degil buradan geciriliyor.
    /// </summary>
    private sealed class ProbeState
    {
        internal bool NotMeasured;

        /// <summary>
        /// İşaretin dar hâli: <b>kodlayıcı seçimi</b> ölçülmemiş bir adaydan geldi.
        /// <see cref="NotMeasured"/> HDR yolunun ölçülmemişliğini de topladığı için
        /// "seçilen kodek geçici mi" sorusunun cevabı ayrı taşınıyor.
        /// </summary>
        internal bool CodecNotMeasured;

        /// <summary>
        /// Tercih edilen kodlayicinin secim aninda okunan durumu. Yedege dusme notu bunu
        /// kullanir: not <b>olculmedi</b> ile <b>olculdu, calismiyor</b>u ayirmak zorunda
        /// ve durumu ikinci kez sormak yoklama sayisini artirirdi.
        /// </summary>
        internal EncoderProbeState PreferredCodecState = EncoderProbeState.NotWorking;

        /// <summary>
        /// Tercih edilen kodlayici bu ffmpeg derlemesinde var mi. Yoklukta durum sorusu
        /// hic sorulmaz, o yuzden <see cref="PreferredCodecState"/> ile ayri tasiniyor.
        /// </summary>
        internal bool PreferredCodecInBuild = true;
    }

    private static PlanResult BuildDetailedCore(MediaInfo info, PlanOptions options, ComplexityProfile? profile, IEncoderAvailability? availability, ProbeState probe)
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
        var lockedCodec = NormalizeLockedCodec(options.LockedCodec);
        var codec = lockedCodec is not null
            ? PickLockedCodec(lockedCodec, availability, probe)
            : fast ? PickFastCodec(preference, availability, probe) : PickCodec(preference, availability, probe);
        var suggestedPreference = CompressionStrategy.AutoPreference(regime);

        if (lockedCodec is not null && options.EncoderPath != EncoderPathOverride.Auto
            && CodecModel.IsHardware(codec) != (options.EncoderPath == EncoderPathOverride.Hardware))
        {
            reason.Add($"kullanici kodlayici yolunu {options.EncoderPath} olarak sabitledi ama ayni anda kodegi {lockedCodec} olarak kilitledi; kodek kilidi onceliklidir, yol istegi uygulanmadi ve kullanilan {codec}");
            reasonCodes.Add(new ReasonNote(ReasonCode.ManualEncoderPathSupersededByCodec,
                ManualOverrideValue: options.EncoderPath.ToString(), EngineWouldHaveChosen: codec, FallbackCodec: codec));
        }

        if (lockedCodec is null && options.EncoderPath != EncoderPathOverride.Auto)
        {
            var engineCodec = codec;
            var wantsHardware = options.EncoderPath == EncoderPathOverride.Hardware;
            if (!wantsHardware && CodecModel.IsHardware(codec))
                codec = LockedFallbackCodecFor(codec);
            else if (wantsHardware && !CodecModel.IsHardware(codec))
                codec = PickFastCodec(preference, availability, probe);

            if (CodecModel.IsHardware(codec) != wantsHardware)
            {
                reason.Add($"kullanici kodlayici yolunu {options.EncoderPath} olarak sabitledi ama bu makinede o yolda kullanilabilir kodlayici yok; istek karsilanmadi ve {codec} ile devam ediliyor");
                reasonCodes.Add(new ReasonNote(ReasonCode.ManualEncoderPathUnmet, ManualOverrideValue: options.EncoderPath.ToString(), EngineWouldHaveChosen: engineCodec, FallbackCodec: codec));
            }
            else if (codec != engineCodec)
            {
                reason.Add($"kullanici kodlayici yolunu {options.EncoderPath} olarak sabitledi; motor {engineCodec} secmisti, kullanilan {codec}");
                reasonCodes.Add(new ReasonNote(ReasonCode.ManualEncoderPathOverride, ManualOverrideValue: options.EncoderPath.ToString(), EngineWouldHaveChosen: engineCodec, FallbackCodec: codec));
            }
        }

        var hdr = HdrResolver.Resolve(info, options.HdrPolicy, codec, availability);
        if (hdr.NotMeasured) probe.NotMeasured = true;

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

        var preferredCodec = lockedCodec ?? (fast ? FastHardwareOrder[0] : PreferredCodecFor(preference));
        if (codec != preferredCodec)
        {
            var fallbackCause = EncoderFallbackCauseFor(probe);
            notes.Add(AdviceCode.EncoderFallback);
            reason.Add(EncoderFallbackReason(preferredCodec, codec, fallbackCause));
            reasonCodes.Add(new ReasonNote(
                ReasonCode.EncoderFallback,
                RequestedCodec: preferredCodec,
                FallbackCodec: codec,
                FallbackCause: fallbackCause));
        }

        if (lockedCodec is null && options.Codec != CodecPreference.Auto && suggestedPreference == CodecPreference.MaxCompression && preference == CodecPreference.Compatible)
            notes.Add(AdviceCode.CodecUpgradeRecommended);
        if (CodecModel.CostsQualityInHardware(codec) && regime is CompressionRegime.Aggressive or CompressionRegime.Extreme)
            notes.Add(AdviceCode.HardwareCodecCostsQuality);
        if (regime == CompressionRegime.Extreme)
            notes.Add(AdviceCode.ExtremeRatioWarning);

        var totalK = aimMb * KbitPerMib / Math.Max(info.DurationSeconds, 0.1);
        var (audioK, audioChannels) = PickAudio(info, options, regime, totalK, notes);

        var audioSilenced = options.AudioChannels == AudioChannelOverride.None;

        if (options.LockedAudioKbps is int manualAudioK)
        {
            if (!info.HasAudio)
            {
                reason.Add($"kullanici ses hedefini {manualAudioK}kbps olarak sabitledi ama {NoAudioStream}; istek karsilanmadi, cikti sessiz");
                reasonCodes.Add(new ReasonNote(ReasonCode.ManualAudioBitrateUnmet,
                    ManualOverrideValue: manualAudioK.ToString(CultureInfo.InvariantCulture), EngineWouldHaveChosen: NoAudioStream));
            }
            else if (audioSilenced)
            {
                reason.Add($"kullanici ses hedefini {manualAudioK}kbps olarak sabitledi ama ayni anda {SilencedByChannels} dedi; kanal istegi kazandi, cikti sessiz ve {manualAudioK}kbps uygulanmadi");
                reasonCodes.Add(new ReasonNote(ReasonCode.ManualAudioBitrateSupersededByChannels,
                    ManualOverrideValue: manualAudioK.ToString(CultureInfo.InvariantCulture), EngineWouldHaveChosen: SilencedByChannels));
            }
            else
            {
                reason.Add($"kullanici ses hedefini {manualAudioK}kbps olarak sabitledi; motor {audioK}kbps secmisti");
                reasonCodes.Add(new ReasonNote(ReasonCode.ManualAudioBitrateOverride, ManualOverrideValue: manualAudioK.ToString(CultureInfo.InvariantCulture), EngineWouldHaveChosen: audioK.ToString(CultureInfo.InvariantCulture)));
                audioK = manualAudioK;
            }
        }

        if (options.AudioChannels != AudioChannelOverride.Auto)
        {
            if (!info.HasAudio)
            {
                reason.Add($"kullanici ses kanalini {options.AudioChannels} olarak sabitledi ama {NoAudioStream}; istek karsilanmadi, cikti sessiz");
                reasonCodes.Add(new ReasonNote(ReasonCode.ManualAudioChannelsUnmet,
                    ManualOverrideValue: options.AudioChannels.ToString(), EngineWouldHaveChosen: NoAudioStream));
            }
            else
            {
                var engineChannels = audioChannels?.ToString(CultureInfo.InvariantCulture) ?? "source";
                switch (options.AudioChannels)
                {
                    case AudioChannelOverride.Stereo:
                        audioChannels = 2;
                        break;
                    case AudioChannelOverride.Mono:
                        audioChannels = 1;
                        break;
                    case AudioChannelOverride.None:
                        audioK = 0;
                        audioChannels = null;
                        break;
                }
                reason.Add($"kullanici ses kanalini {options.AudioChannels} olarak sabitledi; motor {engineChannels} secmisti");
                reasonCodes.Add(new ReasonNote(ReasonCode.ManualAudioChannelsOverride, ManualOverrideValue: options.AudioChannels.ToString(), EngineWouldHaveChosen: engineChannels));
            }
        }

        var videoK = Math.Max(MinVideoBitrateK, totalK * ContainerOverhead - audioK - DeliveryReserveK(codec));

        var effective = new PlanOptions
        {
            TargetMb = effectiveTargetMb,
            Intent = options.Intent,
            Codec = preference,
            AllowResolutionDrop = options.AllowResolutionDrop && CompressionStrategy.AllowsResolutionDrop(regime),
            AllowFpsDrop = options.AllowFpsDrop && CompressionStrategy.AllowsFpsDrop(regime),
            SpeedMode = options.SpeedMode,
            MinResolutionHeight = options.MinResolutionHeight,
            MinFps = options.MinFps
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

        if (best.MeetsFloor && plan.ModeEnum == EncodeMode.TwoPass)
        {
            var floorBppf = complexity.FloorBppf(codec, best.Fps, info.Fps);
            var floorK = (int)Math.Ceiling(VideoBitrateK(floorBppf, best.Width, best.Height, best.Fps));
            var deliverK = Math.Max(floorK, CodecModel.UsableBitrateK(codec, best.Width, best.Height, best.Fps));
            if (plan.VideoBitrateK < deliverK)
            {
                var liftedMb = SizeMb(deliverK, audioK, info.DurationSeconds);
                if (liftedMb <= effectiveTargetMb)
                {
                    reason.Add($"the search cleared {best.Width}x{best.Height}@{best.Fps:0.##} against the {floorBppf:0.0000} bits per pixel per frame floor, but the whole-kbit bitrate rounded to {plan.VideoBitrateK}k and landed under it, so it was raised to {deliverK}k, still inside the {effectiveTargetMb:0.##} MB target");
                    plan.VideoBitrateK = deliverK;
                }
                else
                {
                    notes.Add(AdviceCode.TargetBelowCodecFloor);
                    reason.Add($"the whole-kbit bitrate for {best.Width}x{best.Height}@{best.Fps:0.##} rounds to {plan.VideoBitrateK}k, under the {deliverK}k the {floorBppf:0.0000} bits per pixel per frame floor needs, and raising it to {deliverK}k would deliver {liftedMb:0.##} MB against a {effectiveTargetMb:0.##} MB target; the target wins and the plan runs under the floor");
                }
            }
        }

        reason.Add($"predicted quality {best.Score:0.#}/100{(complexity.Measured ? $" from a measured sample (bppf {complexity.ReferenceBppf:0.0000}, detail falloff {complexity.DetailExponent:0.00})" : " estimated from the source bitrate")}");
        reasonCodes.Add(complexity.Measured
            ? new ReasonNote(ReasonCode.PredictedQualityMeasured, Score: best.Score, Bppf: complexity.ReferenceBppf, DetailExponent: complexity.DetailExponent)
            : new ReasonNote(ReasonCode.PredictedQualityEstimated, Score: best.Score));

        if (options.MinResolutionHeight is not null || options.MinFps is not null)
        {
            var enginePlan = BuildDetailedCore(info, WithoutManualFloors(options), profile, availability, new ProbeState()).Plan;

            if (options.MinResolutionHeight is int requestedMinHeight)
            {
                if (plan.Height < requestedMinHeight)
                {
                    reason.Add($"kullanici cozunurluk tabanini en az {requestedMinHeight}p olarak sabitledi ama kaynak {info.Height}p ve motor yukari olcekleme yapmiyor; istek karsilanmadi, plan {plan.Width}x{plan.Height} ile cikiyor");
                    reasonCodes.Add(new ReasonNote(ReasonCode.ManualMinResolutionUnmet, Width: plan.Width, Height: plan.Height,
                        ManualOverrideValue: requestedMinHeight.ToString(CultureInfo.InvariantCulture),
                        EngineWouldHaveChosen: enginePlan.Height.ToString(CultureInfo.InvariantCulture)));
                }
                else if (plan.Height > enginePlan.Height)
                {
                    reason.Add($"kullanici cozunurluk tabanini en az {requestedMinHeight}p olarak sabitledi; motor {enginePlan.Width}x{enginePlan.Height} secmisti, plan {plan.Width}x{plan.Height} ile cikiyor");
                    reasonCodes.Add(new ReasonNote(ReasonCode.ManualMinResolutionOverride, Width: plan.Width, Height: plan.Height,
                        ManualOverrideValue: requestedMinHeight.ToString(CultureInfo.InvariantCulture),
                        EngineWouldHaveChosen: enginePlan.Height.ToString(CultureInfo.InvariantCulture)));
                }
            }

            if (options.MinFps is double requestedMinFps)
            {
                if (plan.Fps < requestedMinFps - 0.01)
                {
                    reason.Add($"kullanici kare hizi tabanini en az {requestedMinFps:0.##} olarak sabitledi ama kaynak {info.Fps:0.##} fps ve motor kaynagin ustune cikmiyor; istek karsilanmadi, plan {plan.Fps:0.##} fps ile cikiyor");
                    reasonCodes.Add(new ReasonNote(ReasonCode.ManualMinFpsUnmet, Fps: plan.Fps,
                        ManualOverrideValue: requestedMinFps.ToString("0.##", CultureInfo.InvariantCulture),
                        EngineWouldHaveChosen: enginePlan.Fps.ToString("0.##", CultureInfo.InvariantCulture)));
                }
                else if (plan.Fps > enginePlan.Fps + 0.01)
                {
                    reason.Add($"kullanici kare hizi tabanini en az {requestedMinFps:0.##} olarak sabitledi; motor {enginePlan.Fps:0.##} secmisti, plan {plan.Fps:0.##} ile cikiyor");
                    reasonCodes.Add(new ReasonNote(ReasonCode.ManualMinFpsOverride, Fps: plan.Fps,
                        ManualOverrideValue: requestedMinFps.ToString("0.##", CultureInfo.InvariantCulture),
                        EngineWouldHaveChosen: enginePlan.Fps.ToString("0.##", CultureInfo.InvariantCulture)));
                }
            }
        }

        if (options.LockedCrf is double manualCrf)
        {
            var (minCrf, maxCrf) = CodecModel.CrfRange(codec);
            var clampedCrf = Math.Clamp(manualCrf, minCrf, maxCrf);
            var engineMode = plan.Mode;
            var engineCrf = plan.Crf?.ToString(CultureInfo.InvariantCulture) ?? $"{plan.Mode}@{plan.VideoBitrateK}k";
            var bppfAtCrf = complexity.BppfAtCrf(codec, clampedCrf, best.Scale, best.Fps, info.Fps);
            plan.Mode = "crf";
            plan.Crf = (int)Math.Round(clampedCrf);
            plan.VideoBitrateK = (int)Math.Round(Math.Max(VideoBitrateK(bppfAtCrf, best.Width, best.Height, best.Fps), MinVideoBitrateK));
            var crfClamped = Math.Abs(clampedCrf - manualCrf) > 1e-9;
            if (crfClamped)
            {
                reason.Add($"istenen CRF {manualCrf.ToString("0.##", CultureInfo.InvariantCulture)} {codec} icin gecerli {minCrf}-{maxCrf} araliginin disinda; istek karsilanmadi ve aralik ucuna, CRF {clampedCrf.ToString("0.##", CultureInfo.InvariantCulture)}'e kirpildi");
                reasonCodes.Add(new ReasonNote(ReasonCode.ManualCrfClamped, Crf: clampedCrf,
                    ManualOverrideValue: manualCrf.ToString("0.##", CultureInfo.InvariantCulture),
                    EngineWouldHaveChosen: $"{minCrf}-{maxCrf}"));
            }

            reason.Add($"{(crfClamped ? $"plan kirpilmis CRF {plan.Crf} ile cikiyor" : $"kullanici CRF'i {plan.Crf} olarak sabitledi")}; hedef boyut artik zorlanmiyor, {plan.VideoBitrateK}k yalniz bir tahmin — motor {engineMode} kipinde {engineCrf} secmisti");
            reasonCodes.Add(new ReasonNote(ReasonCode.ManualCrfOverride, Crf: clampedCrf, Mb: SizeMb(plan.VideoBitrateK, audioK, info.DurationSeconds), ManualOverrideValue: plan.Crf.ToString(), EngineWouldHaveChosen: engineCrf));

            if (options.LockedMode is EncodeMode supersededMode && supersededMode != EncodeMode.Crf)
            {
                reason.Add($"kullanici kodlama kipini {supersededMode} olarak da sabitlemisti ama acik CRF sayisi onceliklidir; kip crf oldu ve {supersededMode} istegi uygulanmadi");
                reasonCodes.Add(new ReasonNote(ReasonCode.ManualModeSupersededByCrf,
                    ManualOverrideValue: supersededMode.ToString(), EngineWouldHaveChosen: "crf"));
            }
        }
        else if (options.LockedMode is EncodeMode requestedMode && plan.ModeEnum != requestedMode && plan.ModeEnum != EncodeMode.PassThrough)
        {
            var engineMode = plan.Mode;
            if (requestedMode == EncodeMode.TwoPass)
            {
                plan.Mode = "2pass";
                plan.Crf = null;
            }
            else if (requestedMode == EncodeMode.Crf)
            {
                var (minCrf, maxCrf) = CodecModel.CrfRange(codec);
                var currentBppf = BitsPerPixel(plan.VideoBitrateK, plan.Width, plan.Height, plan.Fps);
                var derivedCrf = complexity.CrfForBppf(codec, currentBppf, best.Scale, best.Fps, info.Fps);
                plan.Mode = "crf";
                plan.Crf = (int)Math.Round(Math.Clamp(derivedCrf, minCrf, maxCrf));
            }
            reason.Add($"kullanici kodlama kipini {requestedMode} olarak sabitledi; motor {engineMode} secmisti");
            reasonCodes.Add(new ReasonNote(ReasonCode.ManualModeOverride, ManualOverrideValue: requestedMode.ToString(), EngineWouldHaveChosen: engineMode));
        }

        if (options.LockedPreset is string manualPreset)
        {
            if (!FfmpegArguments.IsValidPreset(codec, manualPreset))
                throw new ArgumentException($"{codec} icin bilinmeyen on ayar: {manualPreset}", nameof(PlanOptions.LockedPreset));
            var enginePreset = plan.Preset;
            plan.Preset = manualPreset;
            reason.Add($"kullanici on ayari {manualPreset} olarak sabitledi; motor {enginePreset} secmisti");
            reasonCodes.Add(new ReasonNote(ReasonCode.ManualPresetOverride, ManualOverrideValue: manualPreset, EngineWouldHaveChosen: enginePreset));

            if (plan.TurboFirstPass && plan.ModeEnum == EncodeMode.TwoPass)
            {
                var firstPassPreset = FfmpegArguments.FirstPassPreset(codec, manualPreset, true);
                if (!firstPassPreset.Equals(manualPreset, StringComparison.OrdinalIgnoreCase))
                {
                    reason.Add($"turbo ilk gecis {codec} icin birinci gecisi {firstPassPreset} ile kosuyor; sabitlenen {manualPreset} yalniz ciktinin uretildigi ikinci geciste gecerli");
                    reasonCodes.Add(new ReasonNote(ReasonCode.ManualPresetFirstPassRelaxed,
                        ManualOverrideValue: manualPreset, EngineWouldHaveChosen: firstPassPreset));
                }
            }
        }

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
        TurboFirstPass = options.SpeedMode == SpeedMode.Fast && TurboFirstPassIsSafe(codec),
        PixelFormat = hdr.PixelFormat,
        HdrVideoFilter = hdr.VideoFilter,
        HdrColorArgs = new List<string>(hdr.ColorArgs)
    };

    /// <summary>
    /// <see cref="CodecModel.SupportsTurboFirstPass"/> hem <c>libx264</c> hem <c>libx265</c>
    /// icin tavan tanimlar, ama plan turboyu yalniz <c>libx265</c>'te aciyor. libx264'te ikinci
    /// gecis birinci gecisin <c>weightp</c> ayarina uymak zorundadir: <c>veryfast</c> weightp=1,
    /// <c>slow</c> weightp=2 kosar ve x264 ikinci gecisi
    /// <c>different weightp setting than first pass (2 vs 1)</c> diyerek hic acmaz —
    /// kullanici sifir bayt cikti alir. Olcum: <c>docs/olcumler/uretim-yolu.md</c>.
    /// <para>
    /// O uyusmazlik iki gecise ayni <c>weightp</c> yazilarak asilabiliyor ve bu kol yine de
    /// x264'u acmiyor: esitlenmis turbo uretim borusunda toplam sureyi %0,58 - %4,44
    /// kisaltip VMAF'tan 0,35 - 0,83 puan goturuyor, ayni olcumde <c>libx265</c> %29,6 - %33,5
    /// kazandirip VMAF'i dusurmuyor. Kazanc ilk gecisin on ayarindan degil, kodlayicinin
    /// toplam sure icindeki payindan geliyor; x264'te o pay kucuk.
    /// Olcum: <c>docs/olcumler/x264-turbo-acilis.md</c>.
    /// </para>
    /// </summary>
    private static bool TurboFirstPassIsSafe(string codec)
        => codec.Equals("libx265", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Yeniden kodlamadan gecerli kilinamayacak gecersiz kilmalar. Bunlardan biri bile
    /// doluysa kopyalama yolu kapanir: kullanici acikca bir kip, CRF, on ayar ya da ses
    /// istedi, kopya bunlarin hicbirini tasiyamaz.
    /// </summary>
    private static bool HasReencodeOverride(PlanOptions options)
        => options.LockedMode is not null
        || options.LockedCrf is not null
        || options.LockedPreset is not null
        || options.LockedAudioKbps is not null
        || options.AudioChannels != AudioChannelOverride.Auto;

    private static bool CanPassThrough(MediaInfo info, PlanOptions options, string codec, HdrResolution hdr)
    {
        if (HasReencodeOverride(options)) return false;
        if (info.FileSizeMb <= 0 || info.FileSizeMb > options.TargetMb) return false;
        if (hdr.PolicyChanged) return false;
        if (options.Codec == CodecPreference.Auto) return true;
        return string.Equals(CodecModel.SourceFamily(info.VideoCodec), CodecModel.SourceFamily(codec), StringComparison.OrdinalIgnoreCase);
    }

    private static PlanResult PassThroughResult(MediaInfo info, PlanOptions options, ComplexityProfile complexity,
        CompressionRegime regime, double ratio, CodecPreference suggestedPreference, IEncoderAvailability? availability, List<AdviceCode> notes)
    {
        var videoBps = Math.Max(0, info.TotalBitrateBps - info.AudioBitrateBps);
        var reason = new List<string>
        {
            $"the source is already {info.FileSizeMb:0.0} MB, under the {options.TargetMb:0.##} MB target, so it is copied as it is instead of being re-encoded"
        };
        var reasonCodes = new List<ReasonNote> { new(ReasonCode.SourceAlreadyUnderTarget, Mb: info.FileSizeMb, TargetMb: options.TargetMb) };
        AddPassThroughDropNotes(info, options, reason, reasonCodes);

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
            Reason = string.Join("; ", reason),
            ReasonCodes = reasonCodes
        };

        notes.Add(AdviceCode.BudgetIsGenerous);
        var estimate = new SizeEstimate(info.FileSizeMb, info.FileSizeMb, info.FileSizeMb, complexity.Measured, true);
        var advice = new StrategyAdvice(regime, ratio, PickCodec(suggestedPreference, availability), suggestedPreference, SourceQualityScore, notes);
        return new PlanResult(plan, estimate, SourceQualityScore, complexity, advice);
    }

    /// <summary>
    /// Kopyalama yolunda gecerli kilinamayan istekleri **sessizce dusurmez**, tek tek yazar.
    /// Buraya yalniz yeniden kodlama gerektirmeyen kalemler gelir; kip/CRF/on ayar/ses
    /// istegi <see cref="HasReencodeOverride"/> ile bu yolu zaten kapatir.
    /// </summary>
    private static void AddPassThroughDropNotes(MediaInfo info, PlanOptions options, List<string> reason, List<ReasonNote> reasonCodes)
    {
        void Drop(string item, string requested, string actual)
        {
            reason.Add($"kullanicinin sabitledigi {item} ({requested}) kopyalama yolunda uygulanamadi; gecerli olan {actual}");
            reasonCodes.Add(new ReasonNote(ReasonCode.ManualOverrideDroppedOnPassThrough,
                ManualOverrideValue: $"{item}={requested}", EngineWouldHaveChosen: actual));
        }

        if (options.EncoderPath != EncoderPathOverride.Auto)
            Drop("kodlayici yolu", options.EncoderPath.ToString(), "kopya, kodlayici hic calismiyor");
        if (options.MinResolutionHeight is int minHeight && minHeight > info.Height)
            Drop("cozunurluk tabani", $"{minHeight}p", $"kaynagin kendi {info.Height}p'si");
        if (options.MinFps is double minFps && minFps > info.Fps + 0.01)
            Drop("kare hizi tabani", minFps.ToString("0.##", CultureInfo.InvariantCulture), $"kaynagin kendi {info.Fps:0.##} fps'i");
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
    public const double QualityScanStep = 1.005;
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

    private static PlanOptions CopyOptions(PlanOptions options) => WithTarget(options, options.TargetMb);

    private static PlanOptions WithTarget(PlanOptions options, double targetMb) => new()
    {
        TargetMb = targetMb,
        Intent = options.Intent,
        Codec = options.Codec,
        AllowResolutionDrop = options.AllowResolutionDrop,
        AllowFpsDrop = options.AllowFpsDrop,
        HdrPolicy = options.HdrPolicy,
        FillPolicy = options.FillPolicy,
        SpeedMode = options.SpeedMode,
        LockedCodec = options.LockedCodec,
        LockedMode = options.LockedMode,
        LockedCrf = options.LockedCrf,
        LockedPreset = options.LockedPreset,
        LockedAudioKbps = options.LockedAudioKbps,
        AudioChannels = options.AudioChannels,
        MinResolutionHeight = options.MinResolutionHeight,
        MinFps = options.MinFps,
        EncoderPath = options.EncoderPath
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

    public static bool LayoutClearsFloor(ComplexityProfile complexity, string codec, double videoK, int width, int height, double fps, double sourceFps)
        => videoK >= CodecModel.UsableBitrateK(codec, width, height, fps)
           && BitsPerPixel(videoK, width, height, fps) >= complexity.FloorBppf(codec, fps, sourceFps);

    private static double VideoBitrateK(double bppf, int width, int height, double fps)
        => bppf * Math.Max(1.0, (double)width * height * fps) / 1000.0;

    private static double SizeMb(double videoK, double audioK, double durationSeconds)
        => (videoK + audioK) * durationSeconds / KbitPerMib / ContainerOverhead;

    private sealed record Layout(int Width, int Height, double Fps, double Scale, double Score, double Bppf = 0, bool MeetsFloor = true, bool Deliverable = true);

    /// <summary>
    /// Ayni secenekler, yalniz kullanicinin taban istekleri cikarilmis. Motorun o istek
    /// olmasaydi ne sececegini olcmek icin kullaniliyor. Uc hal ayriliyor: plan istegin
    /// altinda kaldiysa istek **karsilanmadi** ve `...Unmet` yazilir; plan motorun
    /// secimini asmissa gecersiz kilma yazilir; ikisi de degilse istek etkisizdir ve
    /// olmus gibi kaydedilmez.
    /// </summary>
    private static PlanOptions WithoutManualFloors(PlanOptions options)
    {
        var bare = CopyOptions(options);
        bare.MinResolutionHeight = null;
        bare.MinFps = null;
        return bare;
    }

    /// <summary>
    /// Rejimin kendi tabanini kullanicinin "en az" istegiyle birlestirir. Taban yalniz
    /// yukselebilir: kullanicinin istegi rejimin olculmus tabanindan dusukse yok sayilir,
    /// motorun kalibre ettigi taban asagi cekilemez.
    /// </summary>
    private static RegimeFloors EffectiveFloors(PlanOptions options, CompressionRegime regime)
    {
        var floors = CompressionStrategy.FloorsFor(regime);
        var minHeight = options.MinResolutionHeight is int h ? Math.Max(floors.MinHeight, h) : floors.MinHeight;
        var minFps = options.MinFps is double f ? Math.Max(floors.MinFps, f) : floors.MinFps;
        return floors with { MinHeight = minHeight, MinFps = minFps };
    }

    private static Layout RecoverLayoutAtCeiling(MediaInfo info, PlanOptions options, ComplexityProfile complexity, string codec, Layout fallback, double ceilingCrf, int audioK, double capMb, double budgetVideoK, CompressionRegime regime)
    {
        Layout? best = null;
        var floors = EffectiveFloors(options, regime);

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
        => Decompose(complexity, codec, required, provided, scale, fps, sourceFps, regime).Score;

    private static LayoutScoreParts Decompose(ComplexityProfile complexity, string codec, double required, double provided, double scale, double fps, double sourceFps, CompressionRegime regime)
    {
        var weights = CompressionStrategy.PenaltyWeights(regime);
        var level = complexity.Level;
        var onSourceGrid = !CodecModel.IsHardware(codec);
        var rateRequired = onSourceGrid ? required / Math.Max(complexity.ScaleFactor(scale), 1e-9) : required;
        var rateProvided = onSourceGrid ? provided * scale * scale : provided;
        var rate = level.AtReference - level.PerHalving * Math.Log2(Math.Max(rateRequired, 1e-9) / Math.Max(rateProvided, 1e-9));
        rate = Math.Min(rate, CodecModel.QualityLimit(codec));
        var scalePenalty = ScalePenalty(scale, weights);
        var fpsPenalty = FpsPenalty(fps, sourceFps, weights);
        return new LayoutScoreParts(required, provided, rate, scalePenalty, fpsPenalty, 0, rate - scalePenalty - fpsPenalty);
    }

    public static LayoutScoreParts ScoreLayout(ComplexityProfile complexity, string codec, double videoK, int width, int height, double fps, double sourceFps, int sourceHeight, CompressionRegime regime)
    {
        var scale = (double)height / Math.Max(1, sourceHeight);
        var required = complexity.RequiredBppf(codec, scale, fps, sourceFps);
        var provided = BitsPerPixel(videoK, width, height, fps);
        var parts = Decompose(complexity, codec, required, provided, scale, fps, sourceFps, regime);
        var hysteresis = complexity.AppliesTo(codec, scale, fps) ? CalibratedShapeHysteresis : 0;
        return parts with { Hysteresis = hysteresis, Score = parts.Score + hysteresis };
    }

    private static (Layout Best, bool SourceFpsViable) SearchLayout(MediaInfo info, PlanOptions options, ComplexityProfile complexity, string codec, double videoK, CompressionRegime regime)
    {
        Layout? best = null;
        Layout? densest = null;
        var sourceFpsViable = false;
        var floors = EffectiveFloors(options, regime);

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

            if (LayoutClearsFloor(complexity, codec, videoK, width, height, fps, info.Fps))
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
        var floors = EffectiveFloors(options, regime);
        for (var scale = 1.0; scale >= floors.MinScale - 1e-9; scale -= ScaleStep)
            yield return Math.Round(scale, 4);
    }

    public static IEnumerable<double> FpsCandidates(MediaInfo info, PlanOptions options, CompressionRegime regime)
    {
        var floors = EffectiveFloors(options, regime);
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

    /// <summary>
    /// Hesabin sonunda okunan yoklama isaretlerini tek bir sebebe cevirir. Sebep hem
    /// Core'un cumlesine hem <see cref="ReasonNote.FallbackCause"/> ile arayuze gidiyor;
    /// T157'ye kadar yalniz cumleye gidiyordu ve arayuz uc durumun ucune de tek
    /// yerellestirme anahtari veriyordu.
    /// </summary>
    private static EncoderFallbackCause EncoderFallbackCauseFor(ProbeState probe) =>
        !probe.PreferredCodecInBuild ? EncoderFallbackCause.NotInBuild
        : probe.PreferredCodecState == EncoderProbeState.Unmeasured ? EncoderFallbackCause.NotMeasured
        : EncoderFallbackCause.NotWorking;

    /// <summary>
    /// Yedege dusme notu uc ayri durumu anlatir ve karistirmaz: aday <b>bu derlemede
    /// yok</b>, aday <b>hic olculmedi</b>, aday <b>olculdu ve calismiyor</b>.
    ///
    /// T151'e kadar ikinci durum kullaniciya hic ulasmiyordu: tarama ilk olculmemis
    /// adayda duruyor, o aday geri donuyor ve <c>codec == preferredCodec</c> oldugu icin
    /// not cikmiyordu. Tarama sonraki adaya gecince not ilk kez cikti ve tek cumle
    /// olculmemis bir donanim icin "bu makinede kullanilamadi" dedi. Olcum yokken boyle
    /// bir iddiada bulunulamaz — bu deponun <see cref="EncoderProbeState.Unmeasured"/>
    /// ile ayirdigi sey tam olarak budur.
    /// </summary>
    private static string EncoderFallbackReason(string preferredCodec, string codec, EncoderFallbackCause cause) => cause switch
    {
        EncoderFallbackCause.NotInBuild =>
            $"the {preferredCodec} encoder is not part of this ffmpeg build, so encoding falls back to {codec}",
        EncoderFallbackCause.NotMeasured =>
            $"the {preferredCodec} encoder has not been measured on this machine, so encoding falls back to {codec}",
        _ =>
            $"the {preferredCodec} encoder could not be used on this machine, so encoding falls back to {codec}"
    };

    /// <summary>
    /// <see cref="PlanOptions.LockedCodec"/>'in kabul ettigi kume. <see cref="PlanParser"/>'in
    /// <c>AllowedCodecs</c> listesiyle ayni tutuluyor cunku ikisi de "bu motorun tanidigi
    /// kodlayici" sorusunu soruyor; <c>PlanParser.cs</c> bu sozlesmenin alani disinda kaldigi
    /// icin liste burada ayrica tutuluyor.
    /// </summary>
    private static readonly string[] KnownLockableCodecs =
    {
        "libx264", "libx265", "libsvtav1",
        "h264_nvenc", "hevc_nvenc", "av1_nvenc",
        "h264_qsv", "hevc_qsv", "av1_qsv",
        "h264_amf", "hevc_amf", "av1_amf"
    };

    /// <summary>
    /// Bos/bosluk kilidi "secim yok" sayar; dolu kilit tanidik kodlayicilardan biri degilse
    /// aciyor patlar. Uydurma bir ad sessizce libx264'e dusseydi K1'in "varsayilan hicbir sey
    /// secmemektir" iddiasi bu yolla delinirdi.
    /// </summary>
    private static string? NormalizeLockedCodec(string? locked)
    {
        if (string.IsNullOrWhiteSpace(locked)) return null;
        var trimmed = locked.Trim();
        var known = KnownLockableCodecs.FirstOrDefault(c => c.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (known is null)
            throw new ArgumentException($"bilinmeyen kilitli kodlayici: {trimmed}", nameof(PlanOptions.LockedCodec));
        return known;
    }

    /// <summary>
    /// Kilitlenen kodlayici NotWorking/NotInBuild ise dusulecek yer: ayni ailenin yazilim
    /// kodlayicisi. Yazilim kodlayicilar bu motorda hep var/calisir sayiliyor —
    /// <see cref="PickCodec"/>'in <c>Compatible</c> kolu da libx264'u hic yoklamadan donuyor.
    /// Kilitlenen zaten o yazilim kodlayicinin kendisiyse (ör. libx265 NotWorking cikarsa)
    /// bir alt basamak, libx264'e inilir; aksi halde tavsiyeyi kendine dusurup sonsuz
    /// donguye benzer bir "dusme" uretmis oluruz.
    /// </summary>
    private static string LockedFallbackCodecFor(string codec)
    {
        var fallback = CodecModel.SourceFamily(codec) switch
        {
            "av1" => "libsvtav1",
            "hevc" => "libx265",
            _ => "libx264"
        };
        return fallback.Equals(codec, StringComparison.OrdinalIgnoreCase) ? "libx264" : fallback;
    }

    /// <summary>
    /// <see cref="PickCodec"/> ile ayni yoklama sirasini kilitli, tek adayli kodlayiciya
    /// uygular: derlemede yoksa <see cref="EncoderFallbackCause.NotInBuild"/>, olculmemisse
    /// gecici olarak kendisi doner ve <paramref name="probe"/> isaretlenir, calisiyorsa
    /// kendisi doner, calismiyorsa <see cref="LockedFallbackCodecFor"/>'a duser. Yazilim
    /// kodlayicilar da <see cref="PickCodec"/>'in aksine buradan yoklaniyor: kilit acik bir
    /// secim, "secim yok" degil, o yuzden Compatible'in yoklamasiz kisayolu burada yok.
    /// </summary>
    private static string PickLockedCodec(string locked, IEncoderAvailability? availability, ProbeState probe)
    {
        if (availability is null) return locked;
        if (!availability.HasEncoder(locked))
        {
            probe.PreferredCodecInBuild = false;
            return LockedFallbackCodecFor(locked);
        }
        var state = availability.KnownState(locked);
        probe.PreferredCodecState = state;
        if (state == EncoderProbeState.Unmeasured)
        {
            probe.NotMeasured = true;
            probe.CodecNotMeasured = true;
            return locked;
        }
        if (state == EncoderProbeState.Working) return locked;
        return LockedFallbackCodecFor(locked);
    }

    private static string PickCodec(CodecPreference pref, IEncoderAvailability? availability)
        => PickCodec(pref, availability, null);

    /// <summary>
    /// Tercih edileni <b>gercekten kodluyor mu</b> diye secer, derleme listesinde
    /// gorunuyor mu diye degil — <see cref="PickFastCodec"/> ile ayni soru.
    ///
    /// Yoklama surec doguruyor, o yuzden sira onemli: listede olmayan yoklanmadan
    /// elenir, henuz olculmemis de yoklanmaz — gecici cevap olarak doner ve
    /// <paramref name="probe"/> ile isaretlenir. <paramref name="probe"/> <c>null</c>
    /// iken secim ayni, isaret yok: tavsiye kodlayicisinin olculmemisligi baslat
    /// dugmesini kapatmamali.
    /// </summary>
    private static string PickCodec(CodecPreference pref, IEncoderAvailability? availability, ProbeState? probe)
    {
        var preferred = PreferredCodecFor(pref);
        if (pref is not (CodecPreference.MaxCompression or CodecPreference.Fast)) return preferred;
        if (availability is null) return preferred;
        if (!availability.HasEncoder(preferred))
        {
            if (probe is not null) probe.PreferredCodecInBuild = false;
            return FallbackCodecFor(pref);
        }
        var state = availability.KnownState(preferred);
        if (probe is not null) probe.PreferredCodecState = state;
        if (state == EncoderProbeState.Unmeasured)
        {
            if (probe is not null)
            {
                probe.NotMeasured = true;
                probe.CodecNotMeasured = true;
            }
            return preferred;
        }
        if (state == EncoderProbeState.Working) return preferred;
        return FallbackCodecFor(pref);
    }

    /// <summary>
    /// Adaylari sirayla dener. Bir aday <b>henuz olculmemisse</b> tarama durmaz: aday
    /// hatirlanir ve siradakilere bakilir. Calisan bir aday bulunursa <b>o</b> doner ve
    /// isaret konmaz; hicbiri calismiyorsa hatirlanan olculmemis aday gecici cevap olarak
    /// doner, cunku olculmemis bir donanimi "yok" saymak yanlis olurdu.
    ///
    /// Tarama T151'e kadar ilk olculmemis adayda duruyordu. Yoklamasi hic yerlesmeyen bir
    /// aday (`Unsettled` kalici olarak <see cref="EncoderProbeState.Unmeasured"/> demektir)
    /// sirayi kalici kilitliyor ve sirada calisan donanim varken plan "olculmedi" isaretiyle
    /// donuyordu; T139'un dogrulamasi o isareti "donanim yok"a ceviriyordu. Olcum
    /// <c>docs/olcumler/ilk-olculmemis-aday.md</c>'de.
    ///
    /// Devam etmek olculmemis adayi yoklamasiz birakmiyor: <see cref="EncoderAvailabilityState.KnownState"/>
    /// sorunun kendisiyle yoklamayi sirayla koyar, yani tarama bir adayi gecerken de onu
    /// olcume yollar.
    /// </summary>
    private static string PickFastCodec(CodecPreference pref, IEncoderAvailability? availability, ProbeState probe)
    {
        if (availability is null) return FastHardwareOrder[0];
        probe.PreferredCodecInBuild = availability.HasEncoder(FastHardwareOrder[0]);
        string? unmeasured = null;
        foreach (var candidate in FastHardwareOrder)
        {
            var state = availability.KnownState(candidate);
            if (candidate == FastHardwareOrder[0]) probe.PreferredCodecState = state;
            if (state == EncoderProbeState.Unmeasured)
            {
                unmeasured ??= candidate;
                continue;
            }
            if (state == EncoderProbeState.Working) return candidate;
        }
        if (unmeasured is null) return FallbackCodecFor(pref);
        probe.NotMeasured = true;
        probe.CodecNotMeasured = true;
        return unmeasured;
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
