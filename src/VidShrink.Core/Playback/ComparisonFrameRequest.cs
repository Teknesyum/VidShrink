namespace VidShrink.Core.Playback;

/// <summary>
/// Karsilastirma akisinin tarifi: hangi iki dosya, hangi panel olcusu, kac fps.
/// </summary>
/// <remarks>
/// Panel olcusu grafigi belirler, kaynagin cozunurlugu degil — birlestirme boruya girmeden
/// once ve panel olcusunde yapilir. Duvara carpilirsa <see cref="Fps"/> duser,
/// <see cref="PanelWidth"/> dusmez (PLAN §4/§8, T33/K3 ile desteklendi).
/// </remarks>
public sealed record ComparisonFrameRequest
{
    /// <summary>Sol panelin dosyasi — orijinal.</summary>
    public required string LeftPath { get; init; }

    /// <summary>Sag panelin dosyasi — islenmis.</summary>
    public required string RightPath { get; init; }

    /// <summary><b>Tek</b> panelin genisligi. Birlesik kare bunun iki katidir.</summary>
    public required int PanelWidth { get; init; }

    public required int PanelHeight { get; init; }

    /// <summary>Boruya verilecek kare hizi.</summary>
    public int Fps { get; init; } = 60;

    /// <summary>Akisin baslayacagi konum.</summary>
    public TimeSpan Position { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gercek zamanli tempo (<c>-re</c>). Kapaliyken ffmpeg elinden geldigince hizli besler;
    /// olcum icin kullanilir, oynatmada acik olur.
    /// </summary>
    public bool Realtime { get; init; } = true;

    /// <summary>Kaynak bitince basa sar. Olcum icin.</summary>
    public bool Loop { get; init; }

    /// <summary>Birlesik karenin genisligi.</summary>
    public int FrameWidth => PanelWidth * 2;

    /// <summary>Birlesik karenin BGRA bayt boyu.</summary>
    public int FrameBytes => FrameWidth * PanelHeight * 4;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LeftPath)) throw new ArgumentException("Sol dosya yolu bos.", nameof(LeftPath));
        if (string.IsNullOrWhiteSpace(RightPath)) throw new ArgumentException("Sag dosya yolu bos.", nameof(RightPath));
        if (PanelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(PanelWidth));
        if (PanelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(PanelHeight));
        if (Fps <= 0) throw new ArgumentOutOfRangeException(nameof(Fps));
        if (Position < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(Position));
    }
}
