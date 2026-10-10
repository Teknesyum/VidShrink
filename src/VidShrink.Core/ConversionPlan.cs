namespace VidShrink.Core;

public enum ConversionQualityMode { Crf, Bitrate }

public enum ConversionNote { WebpBitrateIgnored }

public sealed class ConversionPlan
{
    public string Container { get; init; } = "mp4";
    public string VideoCodec { get; init; } = "libx264";

    /// <summary>
    /// Ara kodegin profili (<see cref="IntermediateCodecs"/>). Ara kodekte zorunludur ve
    /// <see cref="QualityMode"/>, <see cref="Crf"/>, <see cref="VideoBitrateK"/> okunmaz: bit
    /// hizini profil belirler. Oteki kodeklerde okunmaz.
    /// </summary>
    public string? VideoProfile { get; init; }
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
    public HdrPolicy HdrPolicy { get; init; } = HdrPolicy.Preserve;

    /// <summary>Kap ve akış etiketleri silinir, iz dili açıkça geri yazılır. GIF'te okunmaz.</summary>
    public bool DropMetadata { get; init; }
    public bool AudioOnly => Container is "mp3" or "m4a" or "wav" or "flac";
    public bool Gif => Container == "gif";
    public bool AnimatedImage => Container is "webp" or "avif";

    /// <summary>Çıktı ses akışı taşıyor mu. GIF ve hareketli görsel taşımaz; ses seçimi orada okunmaz.</summary>
    public bool CarriesAudio => !Gif && !AnimatedImage;
}
