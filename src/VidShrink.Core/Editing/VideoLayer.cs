namespace VidShrink.Core.Editing;

public sealed record VideoLayer
{
    public const double MinScale = 0.05;
    public const double MaxScale = 1;
    public const double DefaultScale = 0.33;
    public const double DefaultX = 0.95;
    public const double DefaultY = 0.95;

    public VideoLayer(string path, long duration, long start = 0)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Katman dosyasinin yolu bos", nameof(path));
        if (duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration), duration, "Katman suresi pozitif olmalidir");
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), start, "Katman cizelgenin basindan once baslayamaz");
        Path = path;
        Duration = duration;
        Start = start;
        SourceEnd = duration;
    }

    public string Path { get; }

    public long Duration { get; }

    public long Start { get; init; }

    public long SourceStart { get; init; }

    public long SourceEnd { get; init; }

    public double Scale { get; init; } = DefaultScale;

    public double X { get; init; } = DefaultX;

    public double Y { get; init; } = DefaultY;

    public bool ChromaKey { get; init; }

    public long Length => SourceEnd - SourceStart;

    public long End => Start + Length;

    public void Validate()
    {
        if (Start < 0) throw new ArgumentOutOfRangeException(nameof(Start), Start, "Katman cizelgenin basindan once baslayamaz");
        if (SourceStart < 0 || SourceEnd <= SourceStart || SourceEnd > Duration)
            throw new ArgumentOutOfRangeException(nameof(SourceEnd), SourceEnd, "Katmanin araligi dosyanin disinda ya da ters");
        if (!double.IsFinite(Scale) || Scale < MinScale || Scale > MaxScale)
            throw new ArgumentOutOfRangeException(nameof(Scale), Scale, "Katman boyu 0,05 ile 1 arasinda olmalidir");
        if (!double.IsFinite(X) || X < 0 || X > 1) throw new ArgumentOutOfRangeException(nameof(X), X, "Yatay yer 0 ile 1 arasinda olmalidir");
        if (!double.IsFinite(Y) || Y < 0 || Y > 1) throw new ArgumentOutOfRangeException(nameof(Y), Y, "Dikey yer 0 ile 1 arasinda olmalidir");
    }
}
