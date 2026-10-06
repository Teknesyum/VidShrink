namespace VidShrink.Core.Editing;

/// <summary>
/// Sahne sinirlarindan bolme hesabi: kaynak tick'inde sinir listesi ve mevcut parcalar girer,
/// yeni parcalar cikar. Hicbir parca silinmez. Parcanin disindaki sinir yok sayilir; parcanin
/// kenarina ya da bir onceki sinira <see cref="MinPieceSeconds"/> kadar yakin olan atilir.
/// Sure kaynakta olculur, hiz ve yon sonucu degistirmez.
/// </summary>
public static class SceneSplit
{
    public const double MinPieceSeconds = SilenceCutOptions.DefaultMinSeconds;

    public static long MinPieceTicks => EditTime.FromSeconds(MinPieceSeconds);

    /// <summary>Saniye listesini sirali, tekrarsiz kaynak tick'ine cevirir; sonlu ve arti olmayan atilir.</summary>
    public static IReadOnlyList<long> ToTicks(IEnumerable<double> seconds)
    {
        ArgumentNullException.ThrowIfNull(seconds);
        return seconds
            .Where(s => double.IsFinite(s) && s > 0)
            .Select(EditTime.FromSeconds)
            .Distinct()
            .OrderBy(t => t)
            .ToArray();
    }

    /// <summary>Parcanin icinde kalan ve iki yanda en az <paramref name="minPiece"/> birakan sinirlar, artan sirada.</summary>
    public static IReadOnlyList<long> Accepted(EditClip clip, IEnumerable<long> boundaries, long minPiece)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(boundaries);
        if (minPiece < 1) throw new ArgumentOutOfRangeException(nameof(minPiece), minPiece, "En kisa parca arti olmalidir");

        var accepted = new List<long>();
        var last = clip.SourceStart;
        foreach (var boundary in boundaries.Where(b => b > clip.SourceStart && b < clip.SourceEnd).Distinct().OrderBy(b => b))
        {
            if (boundary - last < minPiece) continue;
            if (clip.SourceEnd - boundary < minPiece) continue;
            accepted.Add(boundary);
            last = boundary;
        }

        return accepted;
    }

    /// <summary>
    /// Tek parcanin bolunmus hali, oynatma sirasiyla. Solma yalniz parcanin kendi kenarinda
    /// kalir; hiz, yon ve oteki ayarlar tasinir. Kabul edilen sinir yoksa ayni ornek doner.
    /// </summary>
    public static IReadOnlyList<EditClip> Pieces(EditClip clip, IEnumerable<long> boundaries, long minPiece)
    {
        var accepted = Accepted(clip, boundaries, minPiece);
        if (accepted.Count == 0) return new[] { clip };

        var ranges = new List<(long Start, long End)>(accepted.Count + 1);
        var cursor = clip.SourceStart;
        foreach (var boundary in accepted)
        {
            ranges.Add((cursor, boundary));
            cursor = boundary;
        }

        ranges.Add((cursor, clip.SourceEnd));
        if (clip.Reversed) ranges.Reverse();

        var pieces = new EditClip[ranges.Count];
        for (var i = 0; i < ranges.Count; i++)
        {
            var effects = clip.Effects;
            if (i > 0) effects = effects with { FadeIn = 0 };
            if (i < ranges.Count - 1) effects = effects with { FadeOut = 0 };
            pieces[i] = new EditClip(ranges[i].Start, ranges[i].End, clip.Speed, clip.Reversed) { Effects = effects };
        }

        return pieces;
    }

    /// <summary>Butun cizelgenin bolunmus hali; sinir dusmeyen parca oldugu gibi kalir.</summary>
    public static IReadOnlyList<EditClip> Plan(IEnumerable<EditClip> clips, IReadOnlyList<long> boundaries, long minPiece)
    {
        ArgumentNullException.ThrowIfNull(clips);
        ArgumentNullException.ThrowIfNull(boundaries);
        return clips.SelectMany(clip => Pieces(clip, boundaries, minPiece)).ToArray();
    }
}
