using System.Globalization;
using System.Text;

namespace VidShrink.Core.Editing;

public sealed record EdlPart(int Index, EditClip Clip, long TimelineStart, long EdlStart)
{
    public long Head { get; init; }

    public long Tail { get; init; }

    public string? Blend { get; init; }

    public bool IsBlend => Blend is not null;

    public int Ratio { get; init; } = 2;

    public long HeadConsumed => Consumed(Head);

    public long TailConsumed => Tail == 0 ? Clip.SourceLength : Consumed(Clip.TimelineLength - Tail);

    public long EdlLength => IsBlend ? Clip.SourceLength : TailConsumed - HeadConsumed;

    public long EdlEnd => EdlStart + EdlLength;

    public long TimelineLength => IsBlend ? Ratio * Clip.TimelineLength : Clip.TimelineLength - Head - Tail;

    public long TimelineEnd => TimelineStart + TimelineLength;

    public long SourceFrom => IsBlend ? 0 : Clip.Reversed ? Clip.SourceEnd - TailConsumed : Clip.SourceStart + HeadConsumed;

    public bool Contains(long edlTime) => edlTime >= EdlStart && edlTime < EdlEnd;

    private long Consumed(long offset) => offset * (long)(Clip.Speed * 100) / 100;
}

public sealed record EdlPatch(int Clip, long From, long To, string Path);

public sealed class EdlPreview
{
    public const string Scheme = "edl://";

    public const string Header = "# mpv EDL v0";

    public EdlPreview(string sourcePath, EditTimeline timeline)
        : this(new[] { sourcePath }, timeline)
    {
    }

    /// <summary>Cok kaynakli onizleme: her parca kendi kaynaginin yolundan okunur; yollar kaynak sirasiyla verilir.</summary>
    public EdlPreview(IReadOnlyList<string> sourcePaths, EditTimeline timeline, IReadOnlyDictionary<int, string>? blends = null,
        IReadOnlyList<EdlPatch>? patches = null)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(timeline);
        if (sourcePaths.Count == 0 || sourcePaths.Any(string.IsNullOrEmpty))
            throw new ArgumentException("Kaynak yolu bos", nameof(sourcePaths));
        if (timeline.Clips.Count == 0)
            throw new ArgumentException("Cizelgede parca yok", nameof(timeline));
        if (timeline.Clips.Any(c => c.Source >= sourcePaths.Count))
            throw new ArgumentException("Parcanin kaynagi yol listesinde yok", nameof(timeline));

        SourcePaths = sourcePaths.ToArray();
        Frame = timeline.Frame;
        var parts = new List<EdlPart>(timeline.Clips.Count);
        long timelineStart = 0;
        long edlStart = 0;
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var clip = timeline.Clips[i];
            var head = BlendLength(timeline, blends, i);
            var tail = i + 1 < timeline.Clips.Count ? BlendLength(timeline, blends, i + 1) : 0;
            if (head > 0)
            {
                var blend = new EdlPart(parts.Count, new EditClip(0, head), timelineStart - head, edlStart) { Blend = blends![i] };
                parts.Add(blend);
                edlStart += blend.EdlLength;
            }

            var length = clip.TimelineLength;
            var cursor = head;
            foreach (var patch in PatchesOf(patches, i, head, length - tail))
            {
                Plain(cursor, patch.From);
                var piece = new EdlPart(parts.Count, new EditClip(0, patch.To - patch.From), timelineStart + patch.From, edlStart) { Blend = patch.Path, Ratio = 1 };
                parts.Add(piece);
                edlStart += piece.EdlLength;
                cursor = patch.To;
            }

            Plain(cursor, length - tail);

            void Plain(long from, long to)
            {
                var part = new EdlPart(parts.Count, clip, timelineStart + from, edlStart) { Head = from, Tail = length - to };
                if (to <= from || part.EdlLength <= 0 || part.TimelineLength <= 0) return;
                parts.Add(part);
                edlStart += part.EdlLength;
            }

            timelineStart += clip.TimelineLength;
        }

        Parts = parts.AsReadOnly();
        TimelineDuration = timelineStart;
        EdlDuration = edlStart;
    }

    public IReadOnlyList<string> SourcePaths { get; }

    public EditFrame Frame { get; }

    public IReadOnlyList<EdlPart> Parts { get; }

    public bool HasBlends => Parts.Any(p => p.IsBlend);

    public long TimelineDuration { get; }

    public long EdlDuration { get; }

    public string Uri => Scheme + string.Join(";", Parts.Select(Entry));

    public string Document => Header + "\n" + string.Concat(Parts.Select(p => Entry(p) + "\n"));

    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return "%" + Encoding.UTF8.GetByteCount(value).ToString(CultureInfo.InvariantCulture) + "%" + value;
    }

    public static string Seconds(long ticks)
        => ((decimal)ticks / EditTime.TicksPerSecond).ToString("0.#########", CultureInfo.InvariantCulture);

    public EdlPart PartAtEdl(long edlTime)
    {
        if (edlTime < 0 || edlTime > EdlDuration)
            throw new ArgumentOutOfRangeException(nameof(edlTime), edlTime, "An edl suresinin disinda");

        foreach (var part in Parts)
            if (part.Contains(edlTime)) return part;
        return Parts[^1];
    }

    public EdlPart PartAtSeconds(double seconds)
    {
        var ticks = double.IsFinite(seconds) ? EditTime.FromSeconds(seconds) : 0;
        return PartAtEdl(Math.Clamp(ticks, 0, EdlDuration));
    }

    public EdlPart PartAtTimeline(long time)
    {
        if (time < 0 || time > TimelineDuration)
            throw new ArgumentOutOfRangeException(nameof(time), time, "An cizelgenin disinda");

        foreach (var part in Parts)
            if (time < part.TimelineEnd) return part;
        return Parts[^1];
    }

    public long ToEdl(long time)
    {
        if (time == TimelineDuration && time >= 0) return EdlDuration;

        var part = PartAtTimeline(time);
        var offset = Math.Clamp(time - part.TimelineStart, 0, part.TimelineLength - 1);
        if (part.IsBlend) return part.EdlStart + offset / part.Ratio;
        var source = part.Clip.ToSource(part.Head + offset);
        return Math.Clamp(part.EdlStart + (source - part.SourceFrom), part.EdlStart, part.EdlEnd - 1);
    }

    public long ToTimeline(long edlTime)
    {
        if (edlTime == EdlDuration && edlTime >= 0) return TimelineDuration;

        var part = PartAtEdl(edlTime);
        var within = Math.Clamp(edlTime - part.EdlStart, 0, part.EdlLength - 1);
        if (part.IsBlend) return part.TimelineStart + part.Ratio * within;
        var source = part.SourceFrom + within;
        return part.TimelineStart + Math.Clamp(part.Clip.ToOffset(source) - part.Head, 0, part.TimelineLength - 1);
    }

    private string Entry(EdlPart part)
        => Escape(part.Blend ?? SourcePaths[part.Clip.Source]) + "," + Seconds(part.SourceFrom) + "," + Seconds(part.EdlLength);

    private static IEnumerable<EdlPatch> PatchesOf(IReadOnlyList<EdlPatch>? patches, int clip, long from, long to)
    {
        if (patches is null) yield break;
        var cursor = from;
        foreach (var patch in patches.Where(p => p.Clip == clip && !string.IsNullOrEmpty(p.Path)).OrderBy(p => p.From))
        {
            if (patch.From < cursor || patch.To > to || patch.To <= patch.From) continue;
            yield return patch;
            cursor = patch.To;
        }
    }

    private static long BlendLength(EditTimeline timeline, IReadOnlyDictionary<int, string>? blends, int index)
        => blends is not null && blends.TryGetValue(index, out var path) && !string.IsNullOrEmpty(path) ? timeline.TransitionLength(index) : 0;
}
