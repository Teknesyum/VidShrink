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

    /// <summary>
    /// Ciktiya giden kaynak altyazi izleri (0 tabanli altyazi sirasi). <c>null</c> hepsi, bos liste
    /// hicbiri. Kabin tasiyamadigi iz kuralla cevrilir ya da duser (<see cref="StreamMapping.SubtitleTracks"/>).
    /// </summary>
    public IReadOnlyList<int>? SubtitleTracks { get; init; }

    /// <summary>
    /// Ciktiya giden kaynak ses izleri (0 tabanli ses sirasi), kaynak sirasiyla eslenir. <c>null</c>
    /// bugunku davranis: tek ses, ffmpeg'in <c>-map</c>'siz sececegi iz (<see cref="ConversionArguments.AutoAudio"/>).
    /// Bos liste ses yazmaz.
    /// </summary>
    public IReadOnlyList<int>? AudioTracks { get; init; }

    /// <summary>Ek <c>-i</c> girdisi olarak baglanan .srt/.ass dosyalari.</summary>
    public IReadOnlyList<ExternalSubtitle> ExternalSubtitles { get; init; } = Array.Empty<ExternalSubtitle>();
    public bool AudioOnly => Container is "mp3" or "m4a" or "wav" or "flac";
    public bool Gif => Container == "gif";
    public bool AnimatedImage => Container is "webp" or "avif";

    /// <summary>PNG ya da JPEG dizisi: kareler bir klasöre numaralı yazılır.</summary>
    public bool ImageSequenceOutput => Container is "png" or "jpg";

    /// <summary>Resim dizisi çıktısında yalnız ilk kare tek dosyaya yazılır. Öteki kaplarda okunmaz.</summary>
    public bool SingleFrame { get; init; }

    /// <summary>Çıktı ses akışı taşıyor mu. GIF, hareketli görsel ve resim dizisi taşımaz; ses seçimi orada okunmaz.</summary>
    public bool CarriesAudio => !Gif && !AnimatedImage && !ImageSequenceOutput;
}
