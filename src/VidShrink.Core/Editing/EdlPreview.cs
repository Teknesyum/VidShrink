using System.Globalization;
using System.Text;

namespace VidShrink.Core.Editing;

public sealed record EdlPart(int Index, EditClip Clip, long TimelineStart, long EdlStart)
{
    public long EdlLength => Clip.SourceLength;

    public long EdlEnd => EdlStart + EdlLength;

    public long TimelineEnd => TimelineStart + Clip.TimelineLength;

    public bool Contains(long edlTime) => edlTime >= EdlStart && edlTime < EdlEnd;
}

public sealed class EdlPreview
{
    public const string Scheme = "edl://";

    public const string Header = "# mpv EDL v0";

    public EdlPreview(string sourcePath, EditTimeline timeline)
        : this(new[] { sourcePath }, timeline)
    {
    }

    /// <summary>Cok kaynakli onizleme: her parca kendi kaynaginin yolundan okunur; yollar kaynak sirasiyla verilir.</summary>
    public EdlPreview(IReadOnlyList<string> sourcePaths, EditTimeline timeline)
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
        var parts = new List<EdlPart>(timeline.Clips.Count);
        long timelineStart = 0;
        long edlStart = 0;
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var clip = timeline.Clips[i];
            parts.Add(new EdlPart(i, clip, timelineStart, edlStart));
            timelineStart += clip.TimelineLength;
            edlStart += clip.SourceLength;
        }

        Parts = parts.AsReadOnly();
        TimelineDuration = timelineStart;
        EdlDuration = edlStart;
    }

    public IReadOnlyList<string> SourcePaths { get; }

    public IReadOnlyList<EdlPart> Parts { get; }

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
        var source = part.Clip.ToSource(time - part.TimelineStart);
        return part.EdlStart + (source - part.Clip.SourceStart);
    }

    public long ToTimeline(long edlTime)
    {
        if (edlTime == EdlDuration && edlTime >= 0) return TimelineDuration;

        var part = PartAtEdl(edlTime);
        var source = part.Clip.SourceStart + (edlTime - part.EdlStart);
        return part.TimelineStart + part.Clip.ToOffset(source);
    }

    private string Entry(EdlPart part)
        => Escape(SourcePaths[part.Clip.Source]) + "," + Seconds(part.Clip.SourceStart) + "," + Seconds(part.EdlLength);
}
