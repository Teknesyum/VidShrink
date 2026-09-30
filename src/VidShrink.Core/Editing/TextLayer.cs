namespace VidShrink.Core.Editing;

/// <summary>
/// Metnin bir andaki konumu. <paramref name="Offset"/> katmanin basina gore tick'tir,
/// <paramref name="X"/> ve <paramref name="Y"/> metin merkezinin karedeki kesri (0..1).
/// </summary>
public sealed record TextKeyframe(long Offset, double X, double Y);

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

    public IReadOnlyList<TextKeyframe> Keyframes { get; init; } = new[] { new TextKeyframe(0, 0.5, 0.5) };

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

    /// <summary>Katman basina gore <paramref name="offset"/> anindaki konum; kareler arasi dogrusal.</summary>
    public (double X, double Y) PositionAt(long offset)
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

        var (hx, hy) = PositionAt(shift);
        var (tx, ty) = PositionAt(shift + length);
        points.Insert(0, new TextKeyframe(0, hx, hy));
        if (Keyframes.Count > 1 && Keyframes[^1].Offset - shift >= length) points.Add(new TextKeyframe(length, tx, ty));
        points = Simplify(points);

        var fadeIn = Math.Min(FadeIn, length);
        var fadeOut = Math.Min(FadeOut, length - fadeIn);
        return this with { Start = start, End = end, Keyframes = points, FadeIn = fadeIn, FadeOut = fadeOut };
    }

    public bool Equals(TextLayer? other)
        => other is not null
           && Text == other.Text && Start == other.Start && End == other.End
           && FontName == other.FontName && Size.Equals(other.Size) && Color == other.Color
           && Bold == other.Bold && Italic == other.Italic
           && FadeIn == other.FadeIn && FadeOut == other.FadeOut
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
