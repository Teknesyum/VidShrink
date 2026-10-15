using System.Globalization;

namespace VidShrink.Core.Editing;

/// <summary>Kesim listesinin dis bicimi: uzantidan secilir (<see cref="CutList.FormatOf"/>).</summary>
public enum CutListFormat
{
    Csv,
    Edl,
    YouTube,
    FcpXml,
    Cue
}

/// <summary>Kesim listesinin tek satiri: kaynakta [Start, End) araligi tick olarak, etiket, hiz, yon ve kaynak sirasi.</summary>
public sealed record CutListEntry(long Start, long End, string? Label = null, decimal Speed = 1m, bool Reversed = false, int Source = 0);

/// <summary>
/// Yazimin cizelgede olmayan bilgisi: kaynak yollari (kaynak sirasiyla), kare hizi, kare boyu, ses var mi, adsiz
/// bolumun yedek adi (<c>{0}</c> sirasi) ve kurgunun adi.
/// </summary>
public sealed record CutListContext(IReadOnlyList<string> SourcePaths, double Fps)
{
    public int Width { get; init; }

    public int Height { get; init; }

    public bool HasAudio { get; init; } = true;

    public string ChapterTitle { get; init; } = EditChapters.DefaultTitle;

    public string? Title { get; init; }
}

/// <summary>
/// Cizelgeyi dis kesim listelerine ceviren tek okuyucu ve yazici. Disa CSV (LosslessCut'in <c>bas,son,ad</c> satiri,
/// saniye), EDL (CMX3600; hiz <c>M2</c>, etiket <c>* COMMENT</c> satirinda), YouTube bolum metni ve Premiere Pro ile
/// DaVinci Resolve'un actigi Final Cut XML'i (<see cref="FcpXml"/>); ice CSV, EDL ve CUE. Okunan satirlar ilk kaynaga
/// aittir, kaynak suresine kirpilir; hic gecerli satir kalmazsa ya da bicim bozuksa <c>null</c>. Gecisler EDL'e ve XML'e
/// kesme olarak yazilir; YouTube bolumlerinde teslim gibi bindirilir. XML'in hedef programda acildigi olculmedi.
/// </summary>
public static class CutList
{
    public static CutListFormat? FormatOf(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".csv" => CutListFormat.Csv,
            ".edl" => CutListFormat.Edl,
            ".txt" => CutListFormat.YouTube,
            ".xml" => CutListFormat.FcpXml,
            ".cue" => CutListFormat.Cue,
            _ => null
        };

    public static bool CanWrite(CutListFormat format) => format != CutListFormat.Cue;

    public static bool CanRead(CutListFormat format) => format is CutListFormat.Csv or CutListFormat.Edl or CutListFormat.Cue;

    public static IReadOnlyList<CutListEntry> Entries(EditTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        return timeline.Clips.Select(c => new CutListEntry(c.SourceStart, c.SourceEnd, c.Label, c.Speed, c.Reversed, c.Source)).ToArray();
    }

    public static string Write(CutListFormat format, EditTimeline timeline, CutListContext context)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(context);
        return format switch
        {
            CutListFormat.Csv => CutListText.WriteCsv(Entries(timeline)),
            CutListFormat.Edl => CutListEdl.Write(timeline, context),
            CutListFormat.YouTube => CutListText.WriteYouTube(timeline, context.ChapterTitle),
            CutListFormat.FcpXml => FcpXml.Write(timeline, context),
            _ => throw new ArgumentException("Bu bicim yazilmaz", nameof(format))
        };
    }

    public static IReadOnlyList<CutListEntry>? Read(CutListFormat format, string text, double fps, long sourceDuration)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (sourceDuration <= 0) return null;
        var raw = format switch
        {
            CutListFormat.Csv => CutListText.ReadCsv(text),
            CutListFormat.Edl => CutListEdl.Read(text, fps),
            CutListFormat.Cue => CutListText.ReadCue(text),
            _ => null
        };
        return raw is null ? null : Fit(raw, sourceDuration);
    }

    public static IReadOnlyList<EditClip> ToClips(IReadOnlyList<CutListEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries.Select(e => new EditClip(e.Start, e.End, e.Speed, e.Reversed) { Label = e.Label, Source = e.Source }).ToArray();
    }

    internal static string FileName(string path) => path[(path.LastIndexOfAny(new[] { '/', '\\' }) + 1)..];

    internal static string Seconds(long ticks)
        => ((decimal)ticks / EditTime.TicksPerSecond).ToString("0.######", CultureInfo.InvariantCulture);

    internal static decimal Speed(decimal speed)
        => Math.Clamp(decimal.Round(speed, 2, MidpointRounding.AwayFromZero), EditClip.MinSpeed, EditClip.MaxSpeed);

    private static IReadOnlyList<CutListEntry>? Fit(IReadOnlyList<CutListEntry> raw, long sourceDuration)
    {
        var list = new List<CutListEntry>(raw.Count);
        foreach (var entry in raw)
        {
            var start = Math.Max(0, entry.Start);
            var end = Math.Min(entry.End, sourceDuration);
            if (end <= start) continue;
            list.Add(entry with { Start = start, End = end, Label = EditClip.NormalizeLabel(entry.Label), Speed = Speed(entry.Speed), Source = 0 });
        }

        return list.Count == 0 ? null : list;
    }
}
