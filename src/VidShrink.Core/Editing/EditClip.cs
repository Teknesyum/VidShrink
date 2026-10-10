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

    private readonly int _source;

    /// <summary>Parcanin geldigi kaynagin sirasi; ilk kaynak 0, sonradan eklenenler 1, 2...</summary>
    public int Source
    {
        get => _source;
        init => _source = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Kaynak sirasi negatif olamaz");
    }

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

    internal EditClip WithMotion(decimal speed, bool reversed) => new(SourceStart, SourceEnd, speed, reversed) { Effects = Effects, Source = Source };

    internal EditClip WithRange(long sourceStart, long sourceEnd) => new(sourceStart, sourceEnd, Speed, Reversed) { Effects = Effects, Source = Source };

    internal (EditClip First, EditClip Second)? SplitAt(long offset)
    {
        var consumed = Consumed(offset);
        if (consumed <= 0 || consumed >= SourceLength) return null;

        var head = Effects with { FadeOut = 0 };
        var tail = Effects with { FadeIn = 0 };
        return Reversed
            ? (new EditClip(SourceEnd - consumed, SourceEnd, Speed, true) { Effects = head, Source = Source }, new EditClip(SourceStart, SourceEnd - consumed, Speed, true) { Effects = tail, Source = Source })
            : (new EditClip(SourceStart, SourceStart + consumed, Speed, false) { Effects = head, Source = Source }, new EditClip(SourceStart + consumed, SourceEnd, Speed, false) { Effects = tail, Source = Source });
    }

    /// <summary>
    /// Kaynak [<paramref name="start"/>, <paramref name="end"/>) araliginin parcadaki cizelge
    /// araligi (parca basina gore). Aralik parcayla kesismiyorsa <c>null</c>.
    /// </summary>
    public (long From, long To)? OffsetsOf(long start, long end)
    {
        var a = Math.Max(start, SourceStart);
        var b = Math.Min(end, SourceEnd);
        if (b <= a) return null;
        return Reversed
            ? (OffsetOfConsumed(SourceEnd - b), a <= SourceStart ? TimelineLength : OffsetOfConsumed(SourceEnd - a))
            : (OffsetOfConsumed(a - SourceStart), b >= SourceEnd ? TimelineLength : OffsetOfConsumed(b - SourceStart));
    }

    /// <summary>
    /// Kaynak araliklari cikarildiktan sonra kalan parcalar, oynatma sirasiyla. Solma yalniz
    /// parcanin kendi kenarinda kalir; hiz, yon ve oteki ayarlar tasinir.
    /// </summary>
    internal IReadOnlyList<EditClip> Without(IReadOnlyList<(long Start, long End)> ranges)
    {
        var kept = new List<(long Start, long End)>();
        var cursor = SourceStart;
        foreach (var (start, end) in ranges.Where(r => r.End > SourceStart && r.Start < SourceEnd).OrderBy(r => r.Start))
        {
            if (start > cursor) kept.Add((cursor, Math.Min(start, SourceEnd)));
            cursor = Math.Max(cursor, end);
            if (cursor >= SourceEnd) break;
        }

        if (cursor < SourceEnd) kept.Add((cursor, SourceEnd));
        if (kept.Count == 1 && kept[0] == (SourceStart, SourceEnd)) return new[] { this };
        if (Reversed) kept.Reverse();

        var pieces = new EditClip[kept.Count];
        for (var i = 0; i < kept.Count; i++)
        {
            var effects = Effects;
            if (i > 0) effects = effects with { FadeIn = 0 };
            if (i < kept.Count - 1) effects = effects with { FadeOut = 0 };
            pieces[i] = new EditClip(kept[i].Start, kept[i].End, Speed, Reversed) { Effects = effects, Source = Source };
        }

        return pieces;
    }

    private long OffsetOfConsumed(long consumed) => Math.Min(CeilDiv(consumed * 100, Hundredths), TimelineLength);

    private long Consumed(long offset) => offset * Hundredths / 100;

    private static long CeilDiv(long value, long divisor) => (value + divisor - 1) / divisor;
}
