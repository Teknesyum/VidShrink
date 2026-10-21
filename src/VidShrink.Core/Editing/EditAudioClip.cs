namespace VidShrink.Core.Editing;

public enum EditAudioKind
{
    Detached,
    Narration
}

public sealed record EditAudioClip
{
    public EditAudioClip(string path, long duration, long @in, long @out, long start, double levelDb = 0, EditAudioKind kind = EditAudioKind.Detached)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Ses dosyasinin yolu bos", nameof(path));
        if (duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration), duration, "Ses suresi pozitif olmalidir");
        if (@in < 0 || @out <= @in || @out > duration)
            throw new ArgumentOutOfRangeException(nameof(@out), @out, "Ses araligi dosyanin icinde ve bos olmamalidir");
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), start, "Ses cizelgenin basindan once baslayamaz");
        if (!double.IsFinite(levelDb) || levelDb < ClipEffects.MinVolumeDb || levelDb > ClipEffects.MaxVolumeDb)
            throw new ArgumentOutOfRangeException(nameof(levelDb), levelDb, "Ses duzeyi -60 ile +20 dB arasinda olmalidir");
        Path = path;
        Duration = duration;
        In = @in;
        Out = @out;
        Start = start;
        LevelDb = levelDb;
        Kind = kind;
    }

    public string Path { get; }

    public long Duration { get; }

    public long In { get; }

    public long Out { get; }

    public long Start { get; }

    public double LevelDb { get; }

    public EditAudioKind Kind { get; }

    public long Length => Out - In;

    public long End => Start + Length;

    public EditAudioClip MovedTo(long start) => new(Path, Duration, In, Out, start, LevelDb, Kind);

    public EditAudioClip WithLevel(double levelDb) => new(Path, Duration, In, Out, Start, ClampLevel(levelDb), Kind);

    public EditAudioClip TrimmedTo(bool head, long edge)
    {
        if (head)
        {
            var shift = Math.Clamp(edge - Start, -Math.Min(In, Start), Length - 1);
            return new(Path, Duration, In + shift, Out, Start + shift, LevelDb, Kind);
        }

        var length = Math.Clamp(edge - Start, 1, Duration - In);
        return new(Path, Duration, In, In + length, Start, LevelDb, Kind);
    }

    public static double ClampLevel(double levelDb)
        => double.IsFinite(levelDb) ? Math.Clamp(Math.Round(levelDb, 1), ClipEffects.MinVolumeDb, ClipEffects.MaxVolumeDb) : 0;
}
