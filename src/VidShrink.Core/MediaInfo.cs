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
