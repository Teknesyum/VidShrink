namespace VidShrink.Core.Editing;

/// <summary>
/// Metnin bir andaki konumu. <paramref name="Offset"/> katmanin basina gore tick'tir,
/// <paramref name="X"/> ve <paramref name="Y"/> metin merkezinin karedeki kesri (0..1).
/// </summary>
public sealed record TextKeyframe(long Offset, double X, double Y);

/// <summary>
/// Hazir giris ya da cikis hareketi. Kenar degerleri metnin o kenarin disindan kayarak
/// gelmesi ya da o kenara kayarak gitmesidir; <see cref="Typewriter"/> harf harf yazar ve siler.
/// </summary>
public enum TextMotion
{
    None,
    Left,
    Right,
    Bottom,
    Top,
    Typewriter
}

/// <summary>
/// T1 izindeki tek metin klibi. <see cref="Start"/> ve <see cref="End"/> cizelge tick'idir,
/// yani cikti zamani; kliplerin kaynagina bagli degildir. Boyut kare yuksekliginin yuzdesi,
/// renk kullanicinin sectigi <c>0xRRGGBB</c> degeridir. Solmalar tick'tir ve toplamlari
/// katman suresini asmaz.
/// </summary>
public sealed record TextLayer
{
    public const string DefaultFont = "Arial";

    public const double DefaultSize = 8;

    public const double MinSize = 1;

    public const double MaxSize = 50;

    public const uint DefaultColor = 0xFFFFFF;

    public const double MaxOutline = 50;

    public const double MaxShadow = 50;

    public TextLayer(string text, long start, long end)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (start < 0 || end <= start)
            throw new ArgumentOutOfRangeException(nameof(end), end, "Metin araligi bos ya da ters");
        Text = text;
        Start = start;
        End = end;
    }

    public string Text { get; init; }

    public long Start { get; init; }

    public long End { get; init; }

    public string FontName { get; init; } = DefaultFont;

    public double Size { get; init; } = DefaultSize;

    public uint Color { get; init; } = DefaultColor;

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public long FadeIn { get; init; }

    public long FadeOut { get; init; }

    /// <summary>Kenarlik kalinligi, kaynak karenin pikseli; <c>null</c> boyuttan hesaplanan kendiliginden kalinliktir.</summary>
    public double? Outline { get; init; }

    /// <summary>Golge uzakligi, kaynak karenin pikseli.</summary>
    public double Shadow { get; init; }

    /// <summary>Metnin arkasina yari saydam kutu; kenarlik o zaman kutunun ic boslugudur.</summary>
    public bool Box { get; init; }

    public IReadOnlyList<TextKeyframe> Keyframes { get; init; } = new[] { new TextKeyframe(0, 0.5, 0.5) };

    /// <summary>Kayan metnin kenar disinda bekledigi uzaklik, kare boyunun kesri.</summary>
    public const double OffScreen = 0.5;

    public TextMotion MotionIn { get; init; }

    public TextMotion MotionOut { get; init; }

    /// <summary>Giris hareketinin suresi, tick; hareket <see cref="TextMotion.None"/> ise yok sayilir.</summary>
    public long MotionInLength { get; init; }

    public long MotionOutLength { get; init; }

    public long Length => End - Start;

    public bool Covers(long time) => time >= Start && time < End;

    /// <summary>Katmanin kurallara uyup uymadigi; komutlar yalniz gecerli katmani kabul eder.</summary>
    public void Validate()
    {
        if (Start < 0 || End <= Start)
            throw new ArgumentOutOfRangeException(nameof(End), End, "Metin araligi bos ya da ters");
        if (string.IsNullOrWhiteSpace(FontName))
            throw new ArgumentException("Yazi tipi adi bos", nameof(FontName));
        if (!double.IsFinite(Size) || Size < MinSize || Size > MaxSize)
            throw new ArgumentOutOfRangeException(nameof(Size), Size, "Boyut kare yuksekliginin %1-%50'si olmalidir");
        if (Color > 0xFFFFFF)
            throw new ArgumentOutOfRangeException(nameof(Color), Color, "Renk 0xRRGGBB olmalidir");
        if (FadeIn < 0 || FadeOut < 0 || FadeIn + FadeOut > Length)
            throw new ArgumentOutOfRangeException(nameof(FadeIn), FadeIn, "Solmalar negatif ya da katmandan uzun");
        if (Outline is { } outline && (!double.IsFinite(outline) || outline < 0 || outline > MaxOutline))
            throw new ArgumentOutOfRangeException(nameof(Outline), Outline, "Kenarlik 0-50 piksel olmalidir");
        if (!double.IsFinite(Shadow) || Shadow < 0 || Shadow > MaxShadow)
            throw new ArgumentOutOfRangeException(nameof(Shadow), Shadow, "Golge 0-50 piksel olmalidir");
        if (!Enum.IsDefined(MotionIn) || !Enum.IsDefined(MotionOut))
            throw new ArgumentOutOfRangeException(nameof(MotionIn), MotionIn, "Bilinmeyen metin hareketi");
        if (MotionInLength < 0 || MotionOutLength < 0 || MotionInLength + MotionOutLength > Length)
            throw new ArgumentOutOfRangeException(nameof(MotionInLength), MotionInLength, "Hareket sureleri negatif ya da katmandan uzun");
        if (Keyframes is null || Keyframes.Count == 0)
            throw new ArgumentException("En az bir anahtar kare gerekir", nameof(Keyframes));
        foreach (var k in Keyframes)
        {
            if (k.Offset < 0 || k.Offset > Length)
                throw new ArgumentOutOfRangeException(nameof(Keyframes), k.Offset, "Anahtar kare katmanin disinda");
            if (!double.IsFinite(k.X) || !double.IsFinite(k.Y))
                throw new ArgumentOutOfRangeException(nameof(Keyframes), k, "Anahtar kare konumu sayi degil");
        }

        for (var i = 1; i < Keyframes.Count; i++)
            if (Keyframes[i].Offset <= Keyframes[i - 1].Offset)
                throw new ArgumentException("Anahtar kareler artan sirada ve tekil olmalidir", nameof(Keyframes));
    }

    /// <summary>
    /// Katman basina gore <paramref name="offset"/> anindaki ekrandaki konum: anahtar kare
    /// konumu, giris ve cikis kaymasi icindeyse kenar disindaki noktayla dogrusal karisir.
    /// </summary>
    public (double X, double Y) PositionAt(long offset)
    {
        var (x, y) = KeyframePositionAt(offset);
        if (Slides(MotionIn) && MotionInLength > 0 && offset < MotionInLength)
        {
            var (ox, oy) = OffScreenFrom(MotionIn, x, y);
            var t = Math.Max(0, (double)offset / MotionInLength);
            return (ox + (x - ox) * t, oy + (y - oy) * t);
        }

        var outStart = Length - MotionOutLength;
        if (Slides(MotionOut) && MotionOutLength > 0 && offset > outStart)
        {
            var (ox, oy) = OffScreenFrom(MotionOut, x, y);
            var t = Math.Min(1, (double)(offset - outStart) / MotionOutLength);
            return (x + (ox - x) * t, y + (oy - y) * t);
        }

        return (x, y);
    }

    /// <summary>Daktilo hareketinde <paramref name="offset"/> aninda gorunen metin ogesi sayisi; hareketsiz metinde hepsi.</summary>
    public int RevealedAt(long offset)
    {
        var count = TextElementCount;
        var shown = count;
        if (MotionIn == TextMotion.Typewriter && MotionInLength > 0 && offset < MotionInLength)
            shown = Math.Min(shown, (int)Math.Min(count, Math.Max(0, offset) * count / MotionInLength + 1));
        var outStart = Length - MotionOutLength;
        if (MotionOut == TextMotion.Typewriter && MotionOutLength > 0 && offset > outStart)
            shown = Math.Min(shown, (int)Math.Max(1, count - Math.Min(MotionOutLength, offset - outStart) * count / MotionOutLength));
        return shown;
    }

    public int TextElementCount => new System.Globalization.StringInfo(Text).LengthInTextElements;

    /// <summary>Hareketin bolunmesi gereken anlar: kayma sinirlari ve daktilonun her harfi.</summary>
    public IEnumerable<long> MotionCuts()
    {
        var count = TextElementCount;
        if (MotionIn != TextMotion.None && MotionInLength > 0)
        {
            if (MotionIn == TextMotion.Typewriter)
                for (var i = 1; i < count; i++) yield return MotionInLength * i / count;
            yield return MotionInLength;
        }

        if (MotionOut != TextMotion.None && MotionOutLength > 0)
        {
            var outStart = Length - MotionOutLength;
            yield return outStart;
            if (MotionOut == TextMotion.Typewriter)
                for (var i = 1; i < count; i++) yield return outStart + MotionOutLength * i / count;
        }
    }

    /// <summary>
    /// Konumu <paramref name="offset"/> anina yazar. Tek anahtar kareli (duran) metin butunuyle
    /// tasinir; birden cok anahtar kareli metinde o andaki kare yazilir, yoksa eklenir.
    /// </summary>
    public TextLayer WithPositionAt(long offset, double x, double y)
    {
        if (Keyframes.Count <= 1) return this with { Keyframes = new[] { new TextKeyframe(0, x, y) } };
        var at = Math.Clamp(offset, 0, Length);
        var points = Keyframes.Where(k => k.Offset != at).Append(new TextKeyframe(at, x, y)).OrderBy(k => k.Offset).ToArray();
        return this with { Keyframes = points };
    }

    /// <summary>Hareketli metinde <paramref name="offset"/> anina <paramref name="tolerance"/> kadar yakin anahtar kare var mi.</summary>
    public bool HasKeyframeNear(long offset, long tolerance)
        => Keyframes.Count > 1 && Keyframes.Any(k => Math.Abs(k.Offset - offset) <= tolerance);

    /// <summary>
    /// O anda bugunku konumla bir anahtar kare acar. Duran metin basta kare acilinca sona da bir
    /// kare alir ki hareketli sayilsin.
    /// </summary>
    public TextLayer WithKeyframeAt(long offset)
    {
        var at = Math.Clamp(offset, 0, Length);
        if (Keyframes.Any(k => k.Offset == at) && Keyframes.Count > 1) return this;
        var (x, y) = KeyframePositionAt(at);
        var points = Keyframes.Where(k => k.Offset != at).Append(new TextKeyframe(at, x, y)).ToList();
        if (points.Count == 1)
        {
            var (ex, ey) = KeyframePositionAt(Length);
            points.Add(new TextKeyframe(Length, ex, ey));
        }

        return this with { Keyframes = points.OrderBy(k => k.Offset).ToArray() };
    }

    /// <summary>O ana yakin anahtar kareyi siler; son kalan kare silinmez.</summary>
    public TextLayer WithoutKeyframeNear(long offset, long tolerance)
    {
        if (Keyframes.Count <= 1) return this;
        var near = Keyframes.Where(k => Math.Abs(k.Offset - offset) <= tolerance).OrderBy(k => Math.Abs(k.Offset - offset)).FirstOrDefault();
        if (near is null) return this;
        var rest = Keyframes.Where(k => !ReferenceEquals(k, near)).ToArray();
        return this with { Keyframes = rest };
    }

    private static bool Slides(TextMotion motion) => motion is TextMotion.Left or TextMotion.Right or TextMotion.Bottom or TextMotion.Top;

    private static (double X, double Y) OffScreenFrom(TextMotion motion, double x, double y) => motion switch
    {
        TextMotion.Left => (-OffScreen, y),
        TextMotion.Right => (1 + OffScreen, y),
        TextMotion.Top => (x, -OffScreen),
        TextMotion.Bottom => (x, 1 + OffScreen),
        _ => (x, y)
    };

    /// <summary>Katman basina gore <paramref name="offset"/> anindaki anahtar kare konumu; kareler arasi dogrusal.</summary>
    public (double X, double Y) KeyframePositionAt(long offset)
    {
        var first = Keyframes[0];
        if (offset <= first.Offset) return (first.X, first.Y);
        for (var i = 1; i < Keyframes.Count; i++)
        {
            var b = Keyframes[i];
            if (offset > b.Offset) continue;
            var a = Keyframes[i - 1];
            var t = (double)(offset - a.Offset) / (b.Offset - a.Offset);
            return (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        var last = Keyframes[^1];
        return (last.X, last.Y);
    }

    /// <summary>Katmanin ayni metinle baska bir araliga tasinmis hali; anahtar kareler katmanla gider.</summary>
    public TextLayer MovedTo(long start) => this with { Start = start, End = start + Length };

    /// <summary>
    /// Kenar kirpilmis hali. Bastan kirpmada anahtar kareler yeni basa gore kayar; disarida
    /// kalan kareler sinirdaki konumla degistirilir. Solmalar yeni sureye sigacak kadar kisalir.
    /// </summary>
    public TextLayer TrimmedTo(long start, long end)
    {
        if (start < 0 || end <= start)
            throw new ArgumentOutOfRangeException(nameof(end), end, "Metin araligi bos ya da ters");

        var shift = start - Start;
        var length = end - start;
        var points = new List<TextKeyframe>();
        foreach (var k in Keyframes)
        {
            var offset = k.Offset - shift;
            if (offset > 0 && offset < length) points.Add(k with { Offset = offset });
        }

        var (hx, hy) = KeyframePositionAt(shift);
        var (tx, ty) = KeyframePositionAt(shift + length);
        points.Insert(0, new TextKeyframe(0, hx, hy));
        if (Keyframes.Count > 1 && Keyframes[^1].Offset - shift >= length) points.Add(new TextKeyframe(length, tx, ty));
        points = Simplify(points);

        var fadeIn = Math.Min(FadeIn, length);
        var fadeOut = Math.Min(FadeOut, length - fadeIn);
        var motionIn = Math.Min(MotionInLength, length);
        var motionOut = Math.Min(MotionOutLength, length - motionIn);
        return this with
        {
            Start = start, End = end, Keyframes = points, FadeIn = fadeIn, FadeOut = fadeOut,
            MotionInLength = motionIn, MotionOutLength = motionOut
        };
    }

    public bool Equals(TextLayer? other)
        => other is not null
           && Text == other.Text && Start == other.Start && End == other.End
           && FontName == other.FontName && Size.Equals(other.Size) && Color == other.Color
           && Bold == other.Bold && Italic == other.Italic
           && FadeIn == other.FadeIn && FadeOut == other.FadeOut
           && Nullable.Equals(Outline, other.Outline) && Shadow.Equals(other.Shadow) && Box == other.Box
           && MotionIn == other.MotionIn && MotionOut == other.MotionOut
           && MotionInLength == other.MotionInLength && MotionOutLength == other.MotionOutLength
           && Keyframes.SequenceEqual(other.Keyframes);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Text);
        hash.Add(Start);
        hash.Add(End);
        hash.Add(FontName);
        hash.Add(Size);
        hash.Add(Color);
        hash.Add(Bold);
        hash.Add(Italic);
        hash.Add(FadeIn);
        hash.Add(FadeOut);
        hash.Add(Outline);
        hash.Add(Shadow);
        hash.Add(Box);
        hash.Add(MotionIn);
        hash.Add(MotionOut);
        hash.Add(MotionInLength);
        hash.Add(MotionOutLength);
        foreach (var k in Keyframes) hash.Add(k);
        return hash.ToHashCode();
    }

    private static List<TextKeyframe> Simplify(List<TextKeyframe> points)
    {
        var result = new List<TextKeyframe>(points.Count);
        foreach (var p in points)
            if (result.Count == 0 || result[^1].Offset < p.Offset) result.Add(p);
        if (result.Count == 2 && result[0].X.Equals(result[1].X) && result[0].Y.Equals(result[1].Y)) result.RemoveAt(1);
        return result;
    }
}
