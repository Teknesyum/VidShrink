using System.Text.Json.Serialization;

namespace VidShrink.Core;

public enum EncodeMode { Crf, TwoPass, PassThrough }

public enum Intent { Archive, Sharing, SocialMedia }

public enum CodecPreference { Compatible, MaxCompression, Fast, Auto }

public enum HdrPolicy { Preserve, TonemapToSdr }

public enum AudioChannelOverride { Auto, Stereo, Mono, None }

public enum EncoderPathOverride { Auto, Software, Hardware }

/// <summary>
/// Tercih edilen kodlayicidan yedege dusmenin sebebi. Ucu ayri tutuluyor cunku ucu
/// kullaniciya ayri sey soyler: <see cref="NotInBuild"/> ve <see cref="NotMeasured"/>
/// hicbir olcume dayanmaz, yalniz <see cref="NotWorking"/> "denendi ve olmadi" der.
/// </summary>
public enum EncoderFallbackCause
{
    /// <summary>Aday bu ffmpeg derlemesinde hic yok; yoklama sorusu sorulmadi bile.</summary>
    NotInBuild,

    /// <summary>Aday derlemede var ama yoklamasi sonuca varmadi; makine hakkinda bir sey bilinmiyor.</summary>
    NotMeasured,

    /// <summary>Yoklama kostu ve aday kodlayamadi: olculmus bir olumsuzluk.</summary>
    NotWorking
}

public enum ReasonCode
{
    ResolutionScaled,
    FrameRateReduced,
    ResolutionRestoredAtCeiling,
    BudgetExceedsCeiling,
    BudgetBelowCeilingTwoPass,
    PredictedQualityMeasured,
    PredictedQualityEstimated,
    RetryScaled,
    EncoderFallback,
    HdrTonemapped,
    FillCrfLowered,
    FillTwoPassBandCenter,
    FillTwoPassBandTooNarrowForCrf,
    HardwareBitrateBias,
    SourceAlreadyUnderTarget,
    TargetCappedToSource,
    ManualModeOverride,
    ManualCrfOverride,
    ManualPresetOverride,
    ManualAudioBitrateOverride,
    ManualAudioChannelsOverride,
    ManualMinResolutionOverride,
    ManualMinFpsOverride,
    ManualEncoderPathOverride
}

public sealed record ReasonNote(
    ReasonCode Code,
    int Width = 0,
    int Height = 0,
    double Fps = 0,
    double ScalePercent = 0,
    double Crf = 0,
    double BudgetCrf = 0,
    double Mb = 0,
    double TargetMb = 0,
    double AudioMb = 0,
    double Factor = 0,
    double Score = 0,
    double Bppf = 0,
    double DetailExponent = 0,
    string? RequestedCodec = null,
    string? FallbackCodec = null,
    double BandLowerMb = 0,
    EncoderFallbackCause FallbackCause = EncoderFallbackCause.NotWorking,
    string? ManualOverrideValue = null,
    string? EngineWouldHaveChosen = null);

public sealed class EncodePlan
{
    [JsonPropertyName("codec")] public string Codec { get; set; } = "libx264";
    [JsonPropertyName("mode")] public string Mode { get; set; } = "2pass";
    [JsonPropertyName("videoBitrateK")] public int VideoBitrateK { get; set; }
    [JsonPropertyName("crf")] public int? Crf { get; set; }
    [JsonPropertyName("audioCodec")] public string? AudioCodec { get; set; } = "aac";
    [JsonPropertyName("audioBitrateK")] public int AudioBitrateK { get; set; } = 128;
    [JsonPropertyName("audioChannels")] public int? AudioChannels { get; set; }
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("fps")] public double Fps { get; set; }
    [JsonPropertyName("preset")] public string Preset { get; set; } = "slow";
    [JsonPropertyName("pixelFormat")] public string PixelFormat { get; set; } = "yuv420p";
    [JsonIgnore] public string? HdrVideoFilter { get; set; }
    [JsonIgnore] public List<string> HdrColorArgs { get; set; } = new();
    [JsonPropertyName("extraArgs")] public List<string> ExtraArgs { get; set; } = new();
    [JsonPropertyName("reason")] public string Reason { get; set; } = "";
    [JsonIgnore] public List<ReasonNote> ReasonCodes { get; set; } = new();
    [JsonIgnore] public bool TurboFirstPass { get; set; }

    /// <summary>
    /// Plandaki kodlayıcı <b>ölçülmemiş</b> bir adaydan geldi: yoklama henüz cevap
    /// vermediği için aday elenmeden geçirildi ve seçim geçicidir. Bu plana bakıp
    /// "bu makinede donanım kodlayıcı var" denemez; ölçüm gelince hesap yenilenir.
    /// </summary>
    [JsonIgnore] public bool CodecNotMeasured { get; set; }
    [JsonIgnore] public double BitrateBias { get; set; } = 1.0;
    [JsonIgnore] public double? EffectiveTargetMb { get; set; }

    private static readonly ReasonCode[] FillNotes =
    {
        ReasonCode.FillCrfLowered,
        ReasonCode.FillTwoPassBandCenter,
        ReasonCode.FillTwoPassBandTooNarrowForCrf
    };

    [JsonIgnore] public bool StopsShortOfBandOnPurpose =>
        ModeEnum == EncodeMode.Crf
        && ReasonCodes.Count > 0
        && !ReasonCodes.Any(note => FillNotes.Contains(note.Code));

    [JsonIgnore] public EncodeMode ModeEnum => Mode.ToLowerInvariant() switch
    {
        "crf" => EncodeMode.Crf,
        "passthrough" => EncodeMode.PassThrough,
        _ => EncodeMode.TwoPass
    };

    public EncodePlan Clone()
    {
        var clone = (EncodePlan)MemberwiseClone();
        clone.ExtraArgs = new List<string>(ExtraArgs);
        clone.HdrColorArgs = new List<string>(HdrColorArgs);
        clone.ReasonCodes = new List<ReasonNote>(ReasonCodes);
        return clone;
    }

    public IEnumerable<string> DescribeDifferences(EncodePlan other)
    {
        if (Codec != other.Codec) yield return $"codec: {Codec} → {other.Codec}";
        if (Mode != other.Mode) yield return $"mode: {Mode} → {other.Mode}";
        if (VideoBitrateK != other.VideoBitrateK) yield return $"video bitrate: {VideoBitrateK}k → {other.VideoBitrateK}k";
        if (Crf != other.Crf) yield return $"crf: {Crf?.ToString() ?? "-"} → {other.Crf?.ToString() ?? "-"}";
        if (AudioCodec != other.AudioCodec) yield return $"audio codec: {AudioCodec} → {other.AudioCodec}";
        if (AudioBitrateK != other.AudioBitrateK) yield return $"audio bitrate: {AudioBitrateK}k → {other.AudioBitrateK}k";
        if (AudioChannels != other.AudioChannels) yield return $"audio channels: {AudioChannels?.ToString() ?? "source"} → {other.AudioChannels?.ToString() ?? "source"}";
        if (Width != other.Width || Height != other.Height) yield return $"resolution: {Width}x{Height} → {other.Width}x{other.Height}";
        if (Math.Abs(Fps - other.Fps) > 0.01) yield return $"fps: {Fps:0.##} → {other.Fps:0.##}";
        if (Preset != other.Preset) yield return $"preset: {Preset} → {other.Preset}";
    }
}
