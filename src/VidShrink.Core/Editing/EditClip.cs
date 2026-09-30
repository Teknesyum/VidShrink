namespace VidShrink.Core.Editing;

/// <summary>
/// Kesim listesinin zaman birimi: saniyede 240 000 tick. 24/25/30/50/60 fps ve 48 kHz ses tam
/// bolunur; cizelge hesabi tam sayida kalir, saniye yalniz oynatici sinirinda kullanilir.
/// </summary>
public static class EditTime
{
    public const long TicksPerSecond = 240_000;

    public static double ToSeconds(long ticks) => (double)ticks / TicksPerSecond;

    public static long FromSeconds(double seconds)
    {
        if (!double.IsFinite(seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Sure sonlu olmalidir");
        return (long)Math.Round(seconds * TicksPerSecond, MidpointRounding.AwayFromZero);
    }
}

/// <summary>
/// Kaynaktan alinan tek parca: kaynakta yari acik [SourceStart, SourceEnd) araligi, 0,01 adimli
/// mutlak hiz (0,01-100) ve yon. Cizelge uzunlugu kaynak uzunlugunun hiza bolumunun yukari
/// yuvarlanmisidir; cizelge anindan kaynak tick'ine asagi yuvarlanarak inilir.
/// </summary>
public sealed record EditClip
{
    public const decimal MinSpeed = 0.01m;
    public const decimal MaxSpeed = 100m;

    public EditClip(long sourceStart, long sourceEnd, decimal speed = 1m, bool reversed = false)
    {
        if (sourceStart < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceStart), sourceStart, "Parca baslangici negatif olamaz");
        if (sourceEnd <= sourceStart)
            throw new ArgumentOutOfRangeException(nameof(sourceEnd), sourceEnd, "Parca sonu baslangicindan buyuk olmalidir");
        if (speed < MinSpeed || speed > MaxSpeed || decimal.Round(speed, 2) != speed)
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Hiz 0,01 ile 100 arasinda ve 0,01 adimli olmalidir");

        SourceStart = sourceStart;
        SourceEnd = sourceEnd;
        Speed = speed;
        Reversed = reversed;
    }

    public long SourceStart { get; }

    public long SourceEnd { get; }

    public decimal Speed { get; }

    public bool Reversed { get; }

    public ClipEffects Effects { get; init; } = ClipEffects.None;

    public long SourceLength => SourceEnd - SourceStart;

    public long TimelineLength => CeilDiv(SourceLength * 100, Hundredths);

    private long Hundredths => (long)(Speed * 100);

    /// <summary>Parcanin cizelgedeki <paramref name="offset"/> aninda gosterilen kaynak tick'i.</summary>
    public long ToSource(long offset)
    {
        if (offset < 0 || offset >= TimelineLength)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "An parcanin disinda");

        var consumed = Consumed(offset);
        return Reversed ? SourceEnd - 1 - consumed : SourceStart + consumed;
    }

    public bool Contains(long source) => source >= SourceStart && source < SourceEnd;

    /// <summary>
    /// Kaynak tick'inin parcada gorundugu ilk an. Hizli parcada atlanan tick bir sonraki gorunen
    /// ana, parcanin sonundaysa son ana duser.
    /// </summary>
    public long ToOffset(long source)
    {
        if (!Contains(source))
            throw new ArgumentOutOfRangeException(nameof(source), source, "Kaynak tick'i parcanin disinda");

        var consumed = Reversed ? SourceEnd - 1 - source : source - SourceStart;
        return Math.Min(CeilDiv(consumed * 100, Hundredths), TimelineLength - 1);
    }

    internal EditClip WithMotion(decimal speed, bool reversed) => new(SourceStart, SourceEnd, speed, reversed) { Effects = Effects };

    internal EditClip WithRange(long sourceStart, long sourceEnd) => new(sourceStart, sourceEnd, Speed, Reversed) { Effects = Effects };

    internal (EditClip First, EditClip Second)? SplitAt(long offset)
    {
        var consumed = Consumed(offset);
        if (consumed <= 0 || consumed >= SourceLength) return null;

        var head = Effects with { FadeOut = 0 };
        var tail = Effects with { FadeIn = 0 };
        return Reversed
            ? (new EditClip(SourceEnd - consumed, SourceEnd, Speed, true) { Effects = head }, new EditClip(SourceStart, SourceEnd - consumed, Speed, true) { Effects = tail })
            : (new EditClip(SourceStart, SourceStart + consumed, Speed, false) { Effects = head }, new EditClip(SourceStart + consumed, SourceEnd, Speed, false) { Effects = tail });
    }

    private long Consumed(long offset) => offset * Hundredths / 100;

    private static long CeilDiv(long value, long divisor) => (value + divisor - 1) / divisor;
}
