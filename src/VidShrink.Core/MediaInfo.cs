namespace VidShrink.Core;

public sealed record MediaInfo
{
    public required string FilePath { get; init; }
    public required long FileSizeBytes { get; init; }
    public required double DurationSeconds { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required double Fps { get; init; }
    public required string VideoCodec { get; init; }
    public required long TotalBitrateBps { get; init; }
    public string? AudioCodec { get; init; }
    public long AudioBitrateBps { get; init; }
    public int AudioChannels { get; init; }
    public bool HasAudio => AudioCodec is not null;
    public bool IsHdr { get; init; }
    public string? PixelFormat { get; init; }
    public string? ColorPrimaries { get; init; }
    public string? ColorTransfer { get; init; }
    public string? ColorSpace { get; init; }
    public string? ColorRange { get; init; }
    public int BitDepth { get; init; } = 8;
    public string? MasteringDisplayMetadata { get; init; }
    public string? ContentLightLevel { get; init; }

    /// <summary>
    /// Kaynagin Dolby Vision profili, akisin <c>DOVI configuration record</c> kaydindan.
    /// DV tasimayan kaynakta <c>null</c>.
    /// </summary>
    public int? DolbyVisionProfile { get; init; }

    /// <summary>DV taban katmaninin uyumluluk kimligi: profil 8.1'de 1 (HDR10), 8.4'te 4 (HLG).</summary>
    public int? DolbyVisionCompatibilityId { get; init; }

    public bool HasDolbyVision => DolbyVisionProfile is not null;

    /// <summary>Ilk karede HDR10+ (SMPTE 2094-40) dinamik meta verisi var. Yalniz HDR kaynakta yoklanir.</summary>
    public bool HasHdr10Plus { get; init; }

    public bool IsInterlaced { get; init; }
    public string? FieldOrder { get; init; }
    public IReadOnlyList<SourceStream> Streams { get; init; } = Array.Empty<SourceStream>();

    /// <summary>
    /// Kaynagin bolum isaretleri, dosyadaki sirayla. Bos liste "bolum yok" demektir;
    /// <see cref="ChapterCount"/> bu listeden turer, ayri beslenmez.
    /// </summary>
    public IReadOnlyList<ChapterMark> Chapters { get; init; } = Array.Empty<ChapterMark>();

    /// <summary>
    /// Kaynaktaki basliklar, dosyadaki sirayla. Duz dosyada tek ogeli; cok programli bir
    /// yayinda her program bir baslik. Bos kalmaz — yoklama en az bir baslik doldurur,
    /// boylece "baslik var mi" sorusu <see cref="HasMultipleTitles"/> ile tek yerde sorulur.
    /// </summary>
    public IReadOnlyList<SourceTitle> Titles { get; init; } = Array.Empty<SourceTitle>();

    /// <summary>Baslik secicisinin gorunme sarti: birden cok baslik.</summary>
    public bool HasMultipleTitles => Titles.Count > 1;

    public int ChapterCount => Chapters.Count;

    /// <summary>
    /// Piksel en-boy oraninin payi. ffprobe'un <c>sample_aspect_ratio</c> alani; yoklugu,
    /// <c>"0:1"</c> ve <c>"N/A"</c> 1:1'e duser. Kare piksel kaynakta pay ve payda esittir.
    /// </summary>
    public int ParNum { get; init; } = 1;

    /// <summary>Piksel en-boy oraninin paydasi. Sifir olmaz; yoklama 1'e cevirir.</summary>
    public int ParDen { get; init; } = 1;

    /// <summary>
    /// Kaynak anamorfik mi: depolanan kare kare degil. DVD disinda neredeyse hep
    /// <c>false</c>.
    /// </summary>
    public bool IsAnamorphic => ParNum != ParDen && ParNum > 0 && ParDen > 0;

    /// <summary>
    /// Kaynagin ekranda gorundugu genislik. Yukseklik korunur, genislik PAR ile carpilir —
    /// HandBrake'in <c>--non-anamorphic</c> davranisinin aynisi. Olcek merdiveni bu sayidan
    /// iner; <see cref="Width"/> depolanan genislik olarak kalir.
    /// </summary>
    public int DisplayWidth => IsAnamorphic
        ? Math.Max(1, (int)Math.Round(Width * (double)ParNum / ParDen))
        : Width;

    public double FileSizeMb => FileSizeBytes / 1024.0 / 1024.0;
    public long Pixels => (long)Width * Height;
}

/// <summary>
/// Tek bir bolum isareti. <paramref name="Number"/> kullanicinin yazdigi numaradir:
/// HandBrake gibi 1'den baslar, dosyadaki dizinden bir fazladir.
/// </summary>
public sealed record ChapterMark(int Number, double StartSeconds, double EndSeconds, string? Title)
{
    public double DurationSeconds => Math.Max(0, EndSeconds - StartSeconds);
}
