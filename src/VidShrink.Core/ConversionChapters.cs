namespace VidShrink.Core;

/// <summary>Dönüştür'de düzenlenen tek bölüm: başlangıcı kaynağın başından sayılır.</summary>
public sealed record ChapterEntry(TimeSpan Start, string Title);

/// <summary>
/// later.md #44: Dönüştür'ün bölüm listesinden çıktının ffmetadata dosyasına giden saf hesap. Liste kaynak
/// zamanındadır; kırpma başı kadar geri çekilir, kırpma başını içeren bölüm 0'dan başlar, çıktının sonuna
/// sığmayan ve öncekine <see cref="RecorderChapters.MinGapSeconds"/>'tan yakın düşen bölüm atılır.
/// <para>
/// <see cref="ConversionPlan.Chapters"/> <c>null</c> ise ffmpeg kaynağın bölümlerini kendisi taşır; boş liste
/// bölümleri siler (<c>-map_chapters -1</c>).
/// </para>
/// </summary>
public static class ConversionChapters
{
    /// <summary>Bölüm taşıyan kaplar; ölçüsü <see cref="RecorderChapters.Carries"/>'ta.</summary>
    public static bool Carries(string container) => container is "mp4" or "mkv" or "mov";

    /// <summary>Çıktının yanında koşum süresince duran ffmetadata dosyası.</summary>
    public static string MetadataPath(string outputPath) => outputPath + ".bolumler.ffmeta";

    /// <summary>Çıktıya yazılacak bölümler; <paramref name="duration"/> kaynağın süresi.</summary>
    public static IReadOnlyList<RecorderChapter> ForOutput(IReadOnlyList<ChapterEntry> entries, TimeSpan duration, TimeSpan? start, TimeSpan? end)
    {
        var from = start ?? TimeSpan.Zero;
        var length = (end is { } e && e < duration ? e : duration) - from;
        if (length <= TimeSpan.Zero) return Array.Empty<RecorderChapter>();
        var gap = TimeSpan.FromSeconds(RecorderChapters.MinGapSeconds);
        var ordered = entries.OrderBy(c => c.Start).ToArray();
        var starts = new List<(TimeSpan At, string Title)>();
        for (var i = 0; i < ordered.Length; i++)
        {
            var next = i + 1 < ordered.Length ? ordered[i + 1].Start : TimeSpan.MaxValue;
            if (next <= from) continue;
            var at = ordered[i].Start - from;
            if (at < TimeSpan.Zero) at = TimeSpan.Zero;
            if (at > length - gap) break;
            if (starts.Count > 0 && at - starts[^1].At < gap) continue;
            starts.Add((at, ordered[i].Title.Trim()));
        }

        var chapters = new RecorderChapter[starts.Count];
        for (var i = 0; i < chapters.Length; i++)
            chapters[i] = new RecorderChapter(starts[i].At, i + 1 < chapters.Length ? starts[i + 1].At : length, starts[i].Title);
        return chapters;
    }

    /// <summary>
    /// Kaynağın başlangıç zamanı bu kadardan büyükse liste el değmemiş olsa da ffmetadata'dan yazılır: ffmpeg
    /// kendi taşırken bölümleri kaynağın başlangıcı kadar geri çekiyor, 10 sn'lik kaynakta altı bölümün altısı da düştü.
    /// </summary>
    public const double ShiftedStartSeconds = 0.001;

    /// <summary>Bölümler ffmpeg'e bırakılmaz, uygulamanın yazdığı dosyadan eşlenir.</summary>
    public static bool Writes(MediaInfo info, ConversionPlan plan)
        => Carries(plan.Container) && Entries(info, plan) is not null;

    private static IReadOnlyList<ChapterEntry>? Entries(MediaInfo info, ConversionPlan plan)
        => plan.Chapters
           ?? (Math.Abs(info.StartSeconds) > ShiftedStartSeconds && info.Chapters.Count > 0
               ? info.Chapters.Select(c => new ChapterEntry(TimeSpan.FromSeconds(c.StartSeconds), c.Title ?? "")).ToArray()
               : null);

    /// <summary>Plana göre ffmetadata metni; bölüm yazılmayacaksa <c>null</c>.</summary>
    public static string? Metadata(MediaInfo info, ConversionPlan plan)
    {
        if (!Carries(plan.Container) || Entries(info, plan) is not { } entries) return null;
        var chapters = ForOutput(entries, TimeSpan.FromSeconds(info.DurationSeconds), plan.Start, plan.End);
        return chapters.Count == 0 ? null : RecorderChapters.Metadata(chapters);
    }
}
