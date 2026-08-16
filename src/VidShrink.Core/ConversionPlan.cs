namespace VidShrink.Core;

public enum ConversionQualityMode { Crf, Bitrate }

public sealed class ConversionPlan
{
    public string Container { get; init; } = "mp4";
    public string VideoCodec { get; init; } = "libx264";
    public ConversionQualityMode QualityMode { get; init; } = ConversionQualityMode.Crf;
    public int Crf { get; init; } = 23;
    public int VideoBitrateK { get; init; } = 2500;
    public int? Height { get; init; }
    public int? Width { get; init; }
    public double? Fps { get; init; }
    public string? AudioCodec { get; init; } = "aac";
    public int AudioBitrateK { get; init; } = 128;
    public TimeSpan? Start { get; init; }
    public TimeSpan? End { get; init; }
    public bool AudioOnly => Container is "mp3" or "m4a" or "wav";
    public bool Gif => Container == "gif";
}
