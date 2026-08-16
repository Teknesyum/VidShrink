using System.Text.Json.Serialization;

namespace VidShrink.Core;

public enum EncodeMode { Crf, TwoPass }

public enum Intent { Archive, Sharing, SocialMedia }

public enum CodecPreference { Compatible, MaxCompression, Fast, Auto }

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
    [JsonPropertyName("extraArgs")] public List<string> ExtraArgs { get; set; } = new();
    [JsonPropertyName("reason")] public string Reason { get; set; } = "";

    [JsonIgnore] public EncodeMode ModeEnum => Mode.Equals("crf", StringComparison.OrdinalIgnoreCase) ? EncodeMode.Crf : EncodeMode.TwoPass;

    public EncodePlan Clone()
    {
        var clone = (EncodePlan)MemberwiseClone();
        clone.ExtraArgs = new List<string>(ExtraArgs);
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
