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

    private readonly string? _label;

    /// <summary>Parcanin etiketi; kirpilir, bos etiket <c>null</c> sayilir. Bolmede iki yana da gecer.</summary>
    public string? Label
    {
        get => _label;
        init => _label = NormalizeLabel(value);
    }

    /// <summary>Parcanin grup kimligi; ayni kimligi tasiyan parcalar birlikte tasinir ve silinir. Gruplanmamis parca <c>null</c>.</summary>
    public int? Group { get; init; }

    /// <summary>Onceki klipten bu klibe gecis; ilk klipte ya da <c>null</c> iken klipler kesme ile birlesir. Bolmede bas tarafta kalir.</summary>
    public ClipTransition? Transition { get; init; }

    /// <summary>Akici agir cekim: hiz 1'in altindayken ara kareler <c>minterpolate</c> ile uretilir (<see cref="SpeedRamp.Interpolates"/>). Bolmede iki yana da gecer.</summary>
    public bool Smooth { get; init; }

    public static string? NormalizeLabel(string? label) => string.IsNullOrWhiteSpace(label) ? null : label.Trim();

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

    internal EditClip WithMotion(decimal speed, bool reversed) => new(SourceStart, SourceEnd, speed, reversed) { Effects = Effects.ScaledKeys((double)Speed / (double)speed), Source = Source, Label = Label, Group = Group, Transition = Transition, Smooth = Smooth };

    internal EditClip WithRange(long sourceStart, long sourceEnd)
    {
        var clip = new EditClip(sourceStart, sourceEnd, Speed, Reversed) { Effects = Effects, Source = Source, Label = Label, Group = Group, Transition = Transition, Smooth = Smooth };
        if (!Effects.HasKeyframes) return clip;
        var from = (Reversed ? SourceEnd - sourceEnd : sourceStart - SourceStart) * 100 / Hundredths;
        return clip with { Effects = Effects.SlicedKeys(from, from + clip.TimelineLength) };
    }

    internal (EditClip First, EditClip Second)? SplitAt(long offset)
    {
        var consumed = Consumed(offset);
        if (consumed <= 0 || consumed >= SourceLength) return null;

        var cut = CeilDiv(consumed * 100, Hundredths);
        var head = (Effects with { FadeOut = 0 }).SlicedKeys(0, cut);
        var tail = (Effects with { FadeIn = 0 }).SlicedKeys(cut, TimelineLength);
        return Reversed
            ? (new EditClip(SourceEnd - consumed, SourceEnd, Speed, true) { Effects = head, Source = Source, Label = Label, Group = Group, Transition = Transition, Smooth = Smooth }, new EditClip(SourceStart, SourceEnd - consumed, Speed, true) { Effects = tail, Source = Source, Label = Label, Group = Group, Smooth = Smooth })
            : (new EditClip(SourceStart, SourceStart + consumed, Speed, false) { Effects = head, Source = Source, Label = Label, Group = Group, Transition = Transition, Smooth = Smooth }, new EditClip(SourceStart + consumed, SourceEnd, Speed, false) { Effects = tail, Source = Source, Label = Label, Group = Group, Smooth = Smooth });
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
        => Reshape(ranges, Array.Empty<(long Start, long End)>(), 1m);

    /// <summary>
    /// <paramref name="cuts"/> cikarilmis, <paramref name="faster"/> ayri parcaya bolunup hizi
    /// <paramref name="factor"/> ile carpilmis (en cok <see cref="MaxSpeed"/>) parcalar, oynatma
    /// sirasiyla. Kesim hizlandirmanin onune gecer; hicbir sey degismezse parcanin kendisi doner.
    /// </summary>
    internal IReadOnlyList<EditClip> Reshape(IReadOnlyList<(long Start, long End)> cuts, IReadOnlyList<(long Start, long End)> faster, decimal factor)
    {
        var fastSpeed = Math.Min(MaxSpeed, decimal.Round(Speed * factor, 2));
        var points = new SortedSet<long> { SourceStart, SourceEnd };
        foreach (var (start, end) in cuts.Concat(faster))
        {
            if (start > SourceStart && start < SourceEnd) points.Add(start);
            if (end > SourceStart && end < SourceEnd) points.Add(end);
        }

        var kept = new List<(long Start, long End, bool Fast)>();
        long? previous = null;
        foreach (var point in points)
        {
            if (previous is { } from && !Covers(cuts, from, point))
            {
                var fast = fastSpeed != Speed && Covers(faster, from, point);
                if (kept.Count > 0 && kept[^1].End == from && kept[^1].Fast == fast)
                    kept[^1] = kept[^1] with { End = point };
                else
                    kept.Add((from, point, fast));
            }

            previous = point;
        }

        if (kept.Count == 1 && kept[0] == (SourceStart, SourceEnd, false)) return new[] { this };
        if (Reversed) kept.Reverse();

        var pieces = new EditClip[kept.Count];
        for (var i = 0; i < kept.Count; i++)
        {
            var effects = Effects;
            if (i > 0) effects = effects with { FadeIn = 0 };
            if (i < kept.Count - 1) effects = effects with { FadeOut = 0 };
            if (effects.HasKeyframes)
            {
                var from = OffsetOfConsumed(Reversed ? SourceEnd - kept[i].End : kept[i].Start - SourceStart);
                var to = OffsetOfConsumed(Reversed ? SourceEnd - kept[i].Start : kept[i].End - SourceStart);
                effects = effects.SlicedKeys(from, to);
                if (kept[i].Fast) effects = effects.ScaledKeys((double)Speed / (double)fastSpeed);
            }
            pieces[i] = new EditClip(kept[i].Start, kept[i].End, kept[i].Fast ? fastSpeed : Speed, Reversed) { Effects = effects, Source = Source, Label = Label, Group = Group, Transition = i == 0 ? Transition : null, Smooth = Smooth };
        }

        return pieces;
    }

    private static bool Covers(IReadOnlyList<(long Start, long End)> ranges, long from, long to)
        => ranges.Any(r => r.Start <= from && r.End >= to);

    private long OffsetOfConsumed(long consumed) => Math.Min(CeilDiv(consumed * 100, Hundredths), TimelineLength);

    private long Consumed(long offset) => offset * Hundredths / 100;

    private static long CeilDiv(long value, long divisor) => (value + divisor - 1) / divisor;
}
