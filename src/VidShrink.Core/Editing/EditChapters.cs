using System.Globalization;

namespace VidShrink.Core.Editing;

/// <summary>
/// Cizelgeden teslim dosyasinin bolum listesine giden saf hesap. Surec calistirmaz.
/// <para>
/// Iki kaynak vardir: kaynagin kendi bolumleri (<see cref="Shifted"/>) ve parca sinirlari
/// (<see cref="FromClips"/>). Kaynak bolumu kesimle kayar: silinen aralik bolumden duser, hiz bolumu
/// kisaltir, ters parcada bolum sirasi da ters doner, gecis sonraki parcayi oncekinin ustune bindirir.
/// Hizli kipte parca anahtar kareden erken basladigi icin (<see cref="ExportPlan.KeyframeLeads"/>)
/// parcadaki bolum o kadar gec duser. Liste <see cref="RecorderChapters.Metadata"/> ile ffmetadata
/// metnine cevrilir; Kaydedici'nin yolu budur.
/// </para>
/// </summary>
public static class EditChapters
{
    /// <summary>Adi olmayan bolumun yedek adi; <c>{0}</c> bolumun numarasidir.</summary>
    public const string DefaultTitle = "Chapter {0}";

    private static readonly long JoinTolerance = EditTime.FromSeconds(0.001);

    /// <summary>
    /// Dosyanin mutlak saniyesindeki bolumler kaynagin baslangicina gore: <paramref name="startTime"/>
    /// kadar geri cekilir, sifirin altina inen kisim kirpilir, tamamen once kalan bolum duser.
    /// </summary>
    public static IReadOnlyList<ChapterMark> Relative(IReadOnlyList<ChapterMark> chapters, double startTime)
    {
        if (startTime == 0) return chapters;
        var list = new List<ChapterMark>(chapters.Count);
        foreach (var chapter in chapters)
        {
            var end = chapter.EndSeconds - startTime;
            if (end <= 0) continue;
            list.Add(chapter with { StartSeconds = Math.Max(0, chapter.StartSeconds - startTime), EndSeconds = end });
        }

        return list;
    }

    /// <summary>
    /// Kaynak bolumlerinin teslimdeki yeri. <paramref name="sourceChapters"/> kaynak sirasina gore,
    /// kaynagin baslangicina gore saniyedir (<see cref="Relative"/>). Ayni bolumun bitisik parcalari tek
    /// bolum olur; ortusen bolumde oncekinin sonu sonrakinin basina cekilir;
    /// <see cref="RecorderChapters.MinGapSeconds"/>'tan kisa kalan parca atilir. Adsiz bolum
    /// <paramref name="titleFormat"/> ile kendi numarasini alir.
    /// </summary>
    public static IReadOnlyList<RecorderChapter> Shifted(
        EditTimeline timeline, IReadOnlyDictionary<int, IReadOnlyList<ChapterMark>> sourceChapters, string titleFormat,
        IReadOnlyList<double>? leads = null)
    {
        var pieces = new List<(long Start, long End, int Source, int Number, string Title)>();
        long at = 0;
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var clip = timeline.Clips[i];
            at -= timeline.TransitionLength(i);
            var lead = leads is not null && i < leads.Count ? EditTime.FromSeconds(leads[i]) : 0;
            if (sourceChapters.TryGetValue(clip.Source, out var chapters))
            {
                foreach (var chapter in chapters)
                {
                    if (clip.OffsetsOf(EditTime.FromSeconds(chapter.StartSeconds), EditTime.FromSeconds(chapter.EndSeconds)) is not { } span) continue;
                    pieces.Add((at + lead + span.From, at + lead + span.To, clip.Source, chapter.Number,
                        string.IsNullOrWhiteSpace(chapter.Title) ? Format(titleFormat, chapter.Number) : chapter.Title!));
                }
            }

            at += clip.TimelineLength + lead;
        }

        pieces.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));
        var joined = new List<(long Start, long End, int Source, int Number, string Title)>(pieces.Count);
        foreach (var piece in pieces)
        {
            if (joined.Count > 0)
            {
                var last = joined[^1];
                if (last.Source == piece.Source && last.Number == piece.Number && Math.Abs(piece.Start - last.End) <= JoinTolerance)
                {
                    joined[^1] = last with { End = Math.Max(last.End, piece.End) };
                    continue;
                }

                if (last.End > piece.Start) joined[^1] = last with { End = piece.Start };
            }

            joined.Add(piece);
        }

        var min = EditTime.FromSeconds(RecorderChapters.MinGapSeconds);
        return joined
            .Where(p => p.End - p.Start >= min)
            .Select(p => new RecorderChapter(Span(p.Start), Span(p.End), p.Title))
            .ToArray();
    }

    /// <summary>
    /// Parca sinirlarindan bolumler: her parca bir bolum, adi parcanin etiketi, etiket yoksa
    /// <paramref name="titleFormat"/> ile sirasi. Gecis ve Hizli kipin anahtar kare kaymasi sinira girer.
    /// Ikiden az parcada liste bostur.
    /// </summary>
    public static IReadOnlyList<RecorderChapter> FromClips(EditTimeline timeline, string titleFormat, IReadOnlyList<double>? leads = null)
    {
        if (timeline.Clips.Count < 2) return Array.Empty<RecorderChapter>();
        var starts = new long[timeline.Clips.Count + 1];
        long at = 0;
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            at -= timeline.TransitionLength(i);
            starts[i] = at;
            at += timeline.Clips[i].TimelineLength + (leads is not null && i < leads.Count ? EditTime.FromSeconds(leads[i]) : 0);
        }

        starts[^1] = at;
        var chapters = new RecorderChapter[timeline.Clips.Count];
        for (var i = 0; i < chapters.Length; i++)
            chapters[i] = new RecorderChapter(Span(starts[i]), Span(starts[i + 1]), timeline.Clips[i].Label ?? Format(titleFormat, i + 1));
        return chapters;
    }

    /// <summary>
    /// Acilista onerilen isaretciler: kaynagin her bolumunun basi, kaynagin baslangicina gore, bolumun
    /// adiyla. Isaretci cizelge anidir; kaynak acilirken cizelge kaynakla birebir oldugu icin ayni andir.
    /// </summary>
    public static IReadOnlyList<EditMarker> Markers(IReadOnlyList<ChapterMark> chapters, double startTime)
        => chapters.Select(c => new EditMarker(Math.Max(0, EditTime.FromSeconds(c.StartSeconds - startTime)), c.Title)).ToArray();

    private static TimeSpan Span(long ticks) => TimeSpan.FromSeconds(EditTime.ToSeconds(ticks));

    private static string Format(string format, int number) => string.Format(CultureInfo.InvariantCulture, format, number);
}
