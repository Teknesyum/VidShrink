namespace VidShrink.Core;

public enum ConversionQualityMode { Crf, Bitrate }

public enum ConversionNote { WebpBitrateIgnored, GifFitsTarget, FiltersReencode }

public sealed record ConversionPlan
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

    /// <summary>Ses yüksekliği <c>loudnorm</c> ile eşitlenir. Seçilince ses kopyası kapanır.</summary>
    public bool AudioLoudnorm { get; init; }

    /// <summary>Çıktı ses kanal sayısı (<c>-ac</c>); <c>null</c> kaynağınki. Seçilince ses kopyası kapanır.</summary>
    public int? AudioChannels { get; init; }

    /// <summary>Çıktı örnekleme hızı, Hz (<c>-ar</c>); <c>null</c> kaynağınki. Seçilince ses kopyası kapanır.</summary>
    public int? AudioSampleRate { get; init; }

    /// <summary>Ses yüksekliği, kanal ya da örnekleme hızından biri seçili mi.</summary>
    public bool ProcessesAudio => AudioLoudnorm || AudioChannels is not null || AudioSampleRate is not null;
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

    /// <summary>
    /// Ana girdiden sonraki ilk <c>-i</c> olarak baglanan ses dosyasi, <c>1:a:0</c> ile eslenir.
    /// <see cref="ExternalAudio.Replace"/> kaynagin sesini birakir, degilse iz kaynagin seslerinin ardina eklenir.
    /// </summary>
    public ExternalAudio? ExternalAudio { get; init; }

    /// <summary>Dis ses varken cikti kisa olan akista biter (<c>-shortest</c>). Dis ses yokken okunmaz.</summary>
    public bool EndAtShortest { get; init; }

    /// <summary>Ek <c>-i</c> girdisi olarak baglanan .srt/.ass dosyalari.</summary>
    public IReadOnlyList<ExternalSubtitle> ExternalSubtitles { get; init; } = Array.Empty<ExternalSubtitle>();

    /// <summary>
    /// Goruntuye yakilan kaynak altyazi izi (0 tabanli altyazi sirasi); yakilan iz yumusak altyazi
    /// olarak cikmaz. Metin izi <c>subtitles</c> suzgeciyle, resim izi bindirme grafigiyle yazilir.
    /// </summary>
    public int? BurnSubtitle { get; init; }

    /// <summary>Yakilan metin izin yazi tipi; resim izde okunmaz.</summary>
    public string? BurnFont { get; init; }

    /// <summary>Yakilan metin izin boyutu (libass olcegi); resim izde okunmaz.</summary>
    public int? BurnFontSize { get; init; }

    /// <summary>Yakilan metin izin rengi, <c>RRGGBB</c>; resim izde okunmaz.</summary>
    public string? BurnColor { get; init; }

    /// <summary>Yakilan metin izin arkasina opak kutu; resim izde okunmaz.</summary>
    public bool BurnBox { get; init; }

    /// <summary>Dönüştür'ün süzgeç varsayılanı: Küçült'ünkiyle aynı, yalnız tarama çözme kapalı başlar.</summary>
    public static readonly VideoFilterOptions DefaultFilters = VideoFilterOptions.Default with { Deinterlace = DeinterlaceMode.Off };

    /// <summary>Görüntü süzgeçleri Küçült'ün seçenekleriyle ve aynı zincirden; yakma alanları burada okunmaz.</summary>
    public VideoFilterOptions Filters { get; init; } = DefaultFilters;

    /// <summary>Genişlik ve yükseklik birlikte verilmişse görüntü en-boyu korunarak sığdırılır, kalan siyah bantla doldurulur.</summary>
    public bool Letterbox { get; init; }

    /// <summary>Kare hızı kipi: sabit <c>cfr</c>, değişken tavanlı <c>vfr</c>; otomatik bugünkü komut.</summary>
    public FrameRateMode FrameRate { get; init; } = FrameRateMode.Auto;

    /// <summary>Kare hızı değişiminde kareleri çoğaltıp atmak yerine hareketten ara kare üretir (<c>minterpolate</c>).</summary>
    public bool Interpolate { get; init; }

    /// <summary>Dolgu yalnız iki boyut birlikte verilince okunur.</summary>
    public bool PadsToFrame => Letterbox && Width is not null && Height is not null;

    /// <summary>Kucultmenin suzgec secenekleri olarak yakma; iz secilmemisse bicem de bos.</summary>
    public VideoFilterOptions BurnOptions => BurnSubtitle is null
        ? Filters with { BurnSubtitle = null, BurnFile = null }
        : Filters with { BurnSubtitle = BurnSubtitle, BurnFile = null, BurnFont = BurnFont, BurnFontSize = BurnFontSize, BurnColor = BurnColor, BurnBox = BurnBox };
    public bool AudioOnly => Container is "mp3" or "m4a" or "wav" or "flac" or "opus" or "ogg" or "aiff" or "ac3";
    public bool Gif => Container == "gif";
    public bool AnimatedImage => Container is "webp" or "avif" or "apng";

    /// <summary>PNG ya da JPEG dizisi: kareler bir klasöre numaralı yazılır.</summary>
    public bool ImageSequenceOutput => Container is "png" or "jpg";

    /// <summary>Resim dizisi çıktısında yalnız ilk kare tek dosyaya yazılır. Öteki kaplarda okunmaz.</summary>
    public bool SingleFrame { get; init; }

    /// <summary>Çıktı ses akışı taşıyor mu. GIF, hareketli görsel ve resim dizisi taşımaz; ses seçimi orada okunmaz.</summary>
    public bool CarriesAudio => !Gif && !AnimatedImage && !ImageSequenceOutput;

    /// <summary>GIF kaynaktan GIF çıktıda hedef boyut; <see cref="GifPalette.FitAsync"/> bunun altına iner. Öteki planlarda okunmaz.</summary>
    public long? TargetBytes { get; init; }

    /// <summary>GIF paletinin renk sayısı (<c>palettegen=max_colors</c>); <c>null</c> 256.</summary>
    public int? GifColors { get; init; }

    /// <summary>GIF döngü sayısı (<c>-loop</c>): -1 bir kez oynar, 0 sonsuz, N kez tekrar; <c>null</c> yazılmaz (sonsuz).</summary>
    public int? GifLoop { get; init; }

    /// <summary>
    /// Kullanıcının ek ffmpeg argümanları (<see cref="CustomArguments"/>); komutta çıktı yolundan hemen önce yer alır.
    /// GIF'in palet geçişine girmez.
    /// </summary>
    public IReadOnlyList<string> ExtraArgs { get; init; } = Array.Empty<string>();

    /// <summary>GIF titreme yöntemi (<c>paletteuse=dither</c>); <c>null</c> <c>sierra2_4a</c>.</summary>
    public string? GifDither { get; init; }
}
