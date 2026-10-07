namespace VidShrink.Core;

public enum ForeignAudioOutcome
{
    Flagged,
    Sparse,
    NoSubtitles,
    NotMeasured,
    NoCandidate,
    Ambiguous
}

/// <summary>
/// Yabanci ses aramasinin karari. <see cref="Number"/> 1 tabanli altyazi sirasidir
/// (<c>--yak N</c> ile ayni sayi); iz secilmediyse <c>null</c>. <see cref="Packets"/> secilen
/// izin, <see cref="FullestPackets"/> ayni dil ve turdeki en dolu izin paket sayisidir.
/// </summary>
public sealed record ForeignAudioPick(ForeignAudioOutcome Outcome, int? Number = null, int Packets = 0, int FullestPackets = 0)
{
    public string Slug => Outcome switch
    {
        ForeignAudioOutcome.Flagged => "flagged",
        ForeignAudioOutcome.Sparse => "sparse",
        ForeignAudioOutcome.NoSubtitles => "no-subtitles",
        ForeignAudioOutcome.NotMeasured => "not-measured",
        ForeignAudioOutcome.Ambiguous => "ambiguous",
        _ => "no-candidate"
    };
}

/// <summary>
/// HandBrake'in "Foreign Audio Search"unun karsiligi (<c>--altyazi-tara</c>): filmin yalniz
/// yabanci dilde konusulan yerlerini ceviren altyaziyi bulur. Once kaynagin kendi
/// <c>forced</c> bayragi okunur. Bayrak yoksa altyazi paketleri sayilir: ayni dilde ve ayni
/// turde (metin ya da resim) en az iki iz varken, paketi en dolu izin
/// <see cref="SparseShare"/> payini gecmeyen tek iz adaydir. Emin olunamayan her durumda iz
/// secilmez. Esigin dayanagi <c>docs/olcumler/yabanci-ses-arama.md</c>.
/// </summary>
public static class ForeignAudioSearch
{
    public const double SparseShare = 0.10;

    private sealed record Entry(SourceStream Stream, int Number, int Packets = 0, int Fullest = 0);

    private static List<Entry> Burnable(MediaInfo info)
        => info.Streams.Where(stream => stream.Kind == StreamKind.Subtitle)
            .Select((stream, index) => new Entry(stream, index + 1))
            .Where(entry => StreamMapping.IsTextSubtitle(entry.Stream.Codec) || StreamMapping.IsImageSubtitle(entry.Stream.Codec))
            .ToList();

    private static Entry? ByLanguage(IReadOnlyList<Entry> entries, MediaInfo info, string? preferredLanguage)
    {
        var audio = StreamMapping.PrimaryAudio(info.Streams, preferredLanguage)?.Language;
        return entries.FirstOrDefault(entry => StreamMapping.LanguageMatches(entry.Stream.Language, preferredLanguage))
            ?? entries.FirstOrDefault(entry => audio is not null && string.Equals(entry.Stream.Language, audio, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Zorunlu bayrakli iz: once tercih edilen dilde, sonra sesin dilinde, yoksa ilki. Bayrak yoksa <c>null</c>.</summary>
    public static ForeignAudioPick? Flagged(MediaInfo info, string? preferredLanguage)
    {
        var forced = Burnable(info).Where(entry => entry.Stream.IsForced).ToList();
        if (forced.Count == 0) return null;
        var pick = ByLanguage(forced, info, preferredLanguage) ?? forced[0];
        return new ForeignAudioPick(ForeignAudioOutcome.Flagged, pick.Number);
    }

    /// <summary>
    /// Paket sayimindan karar. <paramref name="packets"/> akis indeksinden paket sayisina gider;
    /// <c>null</c> ise sayim yapilamamistir, listede olmayan izin paketi yoktur. Birden cok dilde
    /// aday varsa tercih edilen dil, sonra sesin dili kazanir; ikisi de ayirmiyorsa iz secilmez.
    /// </summary>
    public static ForeignAudioPick Decide(MediaInfo info, IReadOnlyDictionary<int, int>? packets, string? preferredLanguage)
    {
        var tracks = Burnable(info);
        if (tracks.Count == 0) return new ForeignAudioPick(ForeignAudioOutcome.NoSubtitles);
        if (packets is null) return new ForeignAudioPick(ForeignAudioOutcome.NotMeasured);

        var candidates = new List<Entry>();
        var unsure = false;
        foreach (var group in tracks.GroupBy(entry => ((entry.Stream.Language ?? "").ToLowerInvariant(), StreamMapping.IsImageSubtitle(entry.Stream.Codec))))
        {
            var counted = group.Select(entry => entry with { Packets = packets.GetValueOrDefault(entry.Stream.Index) }).ToList();
            var fullest = counted.Max(entry => entry.Packets);
            var sparse = counted.Where(entry => entry.Packets > 0 && entry.Packets <= fullest * SparseShare).ToList();
            if (sparse.Count == 1) candidates.Add(sparse[0] with { Fullest = fullest });
            else if (sparse.Count > 1) unsure = true;
        }

        var pick = ByLanguage(candidates, info, preferredLanguage)
            ?? (candidates.Count == 1 && !unsure ? candidates[0] : null);
        if (pick is not null) return new ForeignAudioPick(ForeignAudioOutcome.Sparse, pick.Number, pick.Packets, pick.Fullest);
        return new ForeignAudioPick(candidates.Count > 0 || unsure ? ForeignAudioOutcome.Ambiguous : ForeignAudioOutcome.NoCandidate);
    }
}
