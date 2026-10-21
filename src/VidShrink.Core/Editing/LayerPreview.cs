using System.Text;

namespace VidShrink.Core.Editing;

public sealed record LayerSpan(int Clip, long From, long To)
{
    public long Length => To - From;
}

public static class LayerPreview
{
    public static readonly long MinSpan = EditTime.TicksPerSecond / 10;

    public static IReadOnlyList<LayerSpan> Pending(EditTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        var spans = new List<LayerSpan>();
        if (!timeline.HasLayers) return spans;
        var covered = Merged(timeline.Layers.Select(l => (l.Start, l.End)));
        long start = 0;
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var length = timeline.Clips[i].TimelineLength;
            var from = start + timeline.TransitionLength(i);
            var to = start + length - (i + 1 < timeline.Clips.Count ? timeline.TransitionLength(i + 1) : 0);
            foreach (var (a, b) in covered)
            {
                var lo = Math.Max(a, from);
                var hi = Math.Min(b, to);
                if (hi - lo >= MinSpan) spans.Add(new LayerSpan(i, lo - start, hi - start));
            }

            start += length;
        }

        return spans;
    }

    public static IReadOnlyList<string> Arguments(
        EditTimeline timeline, LayerSpan span, IReadOnlyList<string> sourcePaths, IReadOnlyList<MediaInfo> sources,
        (int Width, int Height) canvas, double fps, bool audio, int track, string output)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentException.ThrowIfNullOrEmpty(output);
        if (span.Clip < 0 || span.Clip >= timeline.Clips.Count)
            throw new ArgumentOutOfRangeException(nameof(span), span.Clip, "Klip cizelgede yok");
        var clip = timeline.Clips[span.Clip];
        if (span.From < 0 || span.To > clip.TimelineLength || span.To <= span.From)
            throw new ArgumentOutOfRangeException(nameof(span), span.To, "Aralik klibin disinda ya da ters");

        var rate = TransitionPreview.Number(fps > 0 ? fps : 30);
        var length = TransitionPreview.Number(EditTime.ToSeconds(span.Length));
        var part = new EdlPart(0, clip, 0, 0) { Head = span.From, Tail = clip.TimelineLength - span.To };
        var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-threads", "2", "-filter_complex_threads", "2" };
        var graph = new StringBuilder();
        TransitionPreview.AppendSide(args, graph, clip, part, span.From, 0, sources[clip.Source], sourcePaths[clip.Source], canvas, rate, length, null, audio, track);
        graph.Length--;

        var at = timeline.ClipStart(span.Clip) + span.From;
        var end = at + span.Length;
        var layers = new List<VideoLayer>();
        foreach (var layer in timeline.Layers)
        {
            var lo = Math.Max(layer.Start, at);
            var hi = Math.Min(layer.End, end);
            if (hi <= lo) continue;
            args.AddRange(new[] { "-threads", "2" });
            args.AddRange(EditExport.LayerInput(layer, layer.SourceStart + (lo - layer.Start), hi - lo, EditTime.ToSeconds(lo - at)));
            layers.Add(layer);
        }

        graph.Append(layers.Count > 0 ? EditExport.LayerGraph(layers, 1, canvas.Width, "[vout]", "[v0]") : ";[v0]null[vout]");
        if (audio) graph.Append(";[a0]anull[aout]");
        args.AddRange(new[] { "-filter_complex", graph.ToString(), "-map", "[vout]" });
        args.AddRange(TransitionPreview.OutputArguments(rate, audio, output));
        return args;
    }

    private static IReadOnlyList<(long Start, long End)> Merged(IEnumerable<(long Start, long End)> ranges)
    {
        var merged = new List<(long Start, long End)>();
        foreach (var range in ranges.Where(r => r.End > r.Start).OrderBy(r => r.Start))
        {
            if (merged.Count > 0 && range.Start <= merged[^1].End)
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, range.End));
            else
                merged.Add(range);
        }

        return merged;
    }
}
