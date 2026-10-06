using System.Globalization;
using System.Text;

namespace VidShrink.Core;

/// <summary>
/// Kayit surerken konan bir isaret. <paramref name="Part"/> isaretin dustugu teslim dosyasinin
/// sirasi, <paramref name="At"/> o dosyanin kendi baslangicindan sayilan cikti zamani.
/// </summary>
public readonly record struct RecorderMark(int Part, TimeSpan At);

/// <summary>Dosyaya yazilacak tek bolum.</summary>
public sealed record RecorderChapter(TimeSpan Start, TimeSpan End, string Title);

/// <summary>
/// Kayit isaretlerinden bolum listesine ve ffmetadata metnine giden saf hesap. Surec calistirmaz.
/// <para>
/// Isaretin zamani <b>cikti zaman cizgisindedir</b>: duraklatma o anki parcayi kapatir, devam yeni
/// parca acar ve parcalar ard arda birlestirilir, yani duraklatilan aralik dosyada hic durmaz.
/// Birlestirilen kayitta isaret, onceki parcalarin toplam suresine o parcadaki cikti zamani
/// eklenerek bulunur. Kendiliginden bolunen kayitta parcalar birlestirilmez; isaret dustugu
/// parcanin kendi baslangicina gore yazilir.
/// </para>
/// </summary>
public static class RecorderChapters
{
    /// <summary>
    /// Iki isaret arasindaki en kisa aralik. Bundan yakin ikinci basis ayri isaret sayilmaz;
    /// dosyanin basina bundan yakin basis da ilk bolume karisir.
    /// </summary>
    public const double MinGapSeconds = 1.0;

    /// <summary>
    /// Kabin bolum tasiyip tasimadigi. Olculdu (<c>-map_chapters</c> + <c>-c copy</c>,
    /// ffprobe <c>-show_chapters</c>): mp4, mkv ve mov iki bolumu geri verir, gif sifir.
    /// </summary>
    public static bool Carries(RecorderContainer? container)
        => container is RecorderContainer.Mp4 or RecorderContainer.Mkv or RecorderContainer.Mov;

    /// <summary>Basis aninin isareti: bolunen kayitta parcaya gore, birlestirilende bastan beri.</summary>
    public static RecorderMark Position(bool split, int segment, TimeSpan capturedBefore, TimeSpan capturedNow)
        => split ? new RecorderMark(segment, capturedNow) : new RecorderMark(0, capturedBefore + capturedNow);

    /// <summary>Yeni isaret ayri bir bolum acar mi: dosyanin basindan ve ayni dosyadaki son isaretten yeterince uzak olmali.</summary>
    public static bool Accepts(IReadOnlyList<RecorderMark> marks, RecorderMark mark)
    {
        if (mark.At.TotalSeconds < MinGapSeconds) return false;
        for (var i = marks.Count - 1; i >= 0; i--)
        {
            if (marks[i].Part != mark.Part) continue;
            return (mark.At - marks[i].At).TotalSeconds >= MinGapSeconds;
        }

        return true;
    }

    /// <summary>Teslim dosyalarinin sureleri: bolunen kayitta parca basina, birlestirilende tek toplam.</summary>
    public static IReadOnlyList<TimeSpan> PartDurations(bool split, IReadOnlyList<TimeSpan> segments)
        => split ? segments : new[] { segments.Aggregate(TimeSpan.Zero, (sum, next) => sum + next) };

    /// <summary>
    /// Bir dosyanin bolumleri. Ilk bolum 0'dan baslar, sonuncusu dosyanin sonunda biter. Dosyanin
    /// suresine sigmayan ve birbirine <see cref="MinGapSeconds"/>'tan yakin isaretler atilir;
    /// isaret kalmazsa liste bostur ve cagiran dosyaya dokunmaz.
    /// </summary>
    public static IReadOnlyList<RecorderChapter> ForPart(
        IReadOnlyList<RecorderMark> marks, int part, TimeSpan duration, string titleFormat)
    {
        var starts = new List<TimeSpan> { TimeSpan.Zero };
        foreach (var at in marks.Where(m => m.Part == part).Select(m => m.At).Order())
        {
            if (at >= duration || (at - starts[^1]).TotalSeconds < MinGapSeconds) continue;
            starts.Add(at);
        }

        if (starts.Count == 1) return Array.Empty<RecorderChapter>();

        var chapters = new List<RecorderChapter>(starts.Count);
        for (var i = 0; i < starts.Count; i++)
        {
            chapters.Add(new RecorderChapter(
                starts[i],
                i + 1 < starts.Count ? starts[i + 1] : duration,
                string.Format(CultureInfo.InvariantCulture, titleFormat, i + 1)));
        }

        return chapters;
    }

    /// <summary>Bolumlerin ffmetadata metni; zaman tabani milisaniye.</summary>
    public static string Metadata(IReadOnlyList<RecorderChapter> chapters)
    {
        var text = new StringBuilder(";FFMETADATA1\n");
        foreach (var chapter in chapters)
        {
            text.Append("[CHAPTER]\nTIMEBASE=1/1000\n");
            text.Append("START=").Append(Millis(chapter.Start)).Append('\n');
            text.Append("END=").Append(Millis(chapter.End)).Append('\n');
            text.Append("title=").Append(Escape(chapter.Title)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Bolumleri dosyaya yazan yeniden paketleme: akislar kopyalanir, yeniden kodlama yoktur.
    /// Bolumler ikinci girdiden (<c>-f ffmetadata</c>), dosyanin kendi etiketleri ilkinden gelir.
    /// </summary>
    public static IReadOnlyList<string> BuildRemux(string source, string metadataPath, string target)
    {
        var args = new List<string>
        {
            "-hide_banner", "-y", "-nostdin",
            "-i", source,
            "-f", "ffmetadata", "-i", metadataPath,
            "-map", "0", "-map_metadata", "0", "-map_chapters", "1",
            "-c", "copy"
        };
        if (RecorderArguments.ContainerOf(target) is RecorderContainer.Mp4 or RecorderContainer.Mov)
            args.AddRange(new[] { "-movflags", "+faststart" });
        args.Add(target);
        return args;
    }

    private static string Millis(TimeSpan at)
        => ((long)Math.Round(at.TotalMilliseconds, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);

    private static string Escape(string title)
    {
        var text = new StringBuilder(title.Length);
        foreach (var c in title)
        {
            if (c is '=' or ';' or '#' or '\\' or '\n') text.Append('\\');
            if (c != '\r') text.Append(c);
        }

        return text.ToString();
    }
}
