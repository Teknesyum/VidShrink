namespace VidShrink.Core.Editing;

/// <summary>
/// A2 muzik izi: tek ses dosyasi, cizelgede <see cref="Start"/> aninda baslar ve teslimin sonuna kadar calar (dosya kisaysa sessizlik
/// gelir). Duzeyi dB olarak <see cref="MinLevelDb"/>-<see cref="MaxLevelDb"/> araliginda; teslimde kaynak sesiyle <c>amix</c> ile
/// karisir ve yalniz Tam kipte kodlanir. Onizlemede calmaz.
/// </summary>
public sealed record EditMusic
{
    public const double MinLevelDb = -30;
    public const double MaxLevelDb = 6;

    public EditMusic(string path, long duration, long start = 0, double levelDb = 0)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Muzik dosyasinin yolu bos", nameof(path));
        if (duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration), duration, "Muzik suresi pozitif olmalidir");
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), start, "Muzik cizelgenin basindan once baslayamaz");
        if (!double.IsFinite(levelDb) || levelDb < MinLevelDb || levelDb > MaxLevelDb)
            throw new ArgumentOutOfRangeException(nameof(levelDb), levelDb, "Muzik duzeyi -30 ile +6 dB arasinda olmalidir");
        Path = path;
        Duration = duration;
        Start = start;
        LevelDb = levelDb;
    }

    public string Path { get; }

    public long Duration { get; }

    public long Start { get; }

    public double LevelDb { get; }

    /// <summary>Duzeyi araliga kirpip yazar.</summary>
    public static double ClampLevel(double levelDb) => double.IsFinite(levelDb) ? Math.Clamp(Math.Round(levelDb, 1), MinLevelDb, MaxLevelDb) : 0;
}
