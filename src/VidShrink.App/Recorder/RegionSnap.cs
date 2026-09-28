using System;
using System.Collections.Generic;
using Avalonia;

namespace VidShrink.App.Recorder;

internal readonly record struct SnapGuide(bool Vertical, int At);

internal readonly record struct SnapResult(PixelVector Delta, SnapGuide? X, SnapGuide? Y);

internal readonly record struct PointSnap(PixelPoint Point, SnapGuide? X, SnapGuide? Y);

internal static class RegionSnap
{
    internal static int? Nearest(int value, int from, int to, bool vertical, IReadOnlyList<PixelRect> targets, int threshold)
    {
        if (threshold <= 0) return null;
        int? best = null;
        var bestDistance = int.MaxValue;
        foreach (var target in targets)
        {
            if (target.Width <= 0 || target.Height <= 0) continue;
            var spanFrom = vertical ? target.Y : target.X;
            var spanTo = vertical ? target.Bottom : target.Right;
            if (to + threshold < spanFrom || from - threshold > spanTo) continue;
            var first = vertical ? target.X : target.Y;
            var second = vertical ? target.Right : target.Bottom;
            foreach (var line in new[] { first, second })
            {
                var distance = Math.Abs(line - value);
                if (distance > threshold || distance >= bestDistance) continue;
                bestDistance = distance;
                best = line;
            }
        }

        return best;
    }

    private static (bool Left, bool Top, bool Right, bool Bottom) Edges(RegionGrip grip) => grip switch
    {
        RegionGrip.Move => (true, true, true, true),
        RegionGrip.Left => (true, false, false, false),
        RegionGrip.Top => (false, true, false, false),
        RegionGrip.Right => (false, false, true, false),
        RegionGrip.Bottom => (false, false, false, true),
        RegionGrip.TopLeft => (true, true, false, false),
        RegionGrip.TopRight => (false, true, true, false),
        RegionGrip.BottomLeft => (true, false, false, true),
        RegionGrip.BottomRight => (false, false, true, true),
        _ => (false, false, false, false)
    };

    private static (int Shift, SnapGuide? Guide) Axis(
        bool vertical, bool movesLow, bool movesHigh, int low, int high, int from, int to,
        IReadOnlyList<PixelRect> targets, int threshold)
    {
        (int Shift, SnapGuide? Guide) best = (0, null);
        var bestDistance = int.MaxValue;
        if (movesLow && Nearest(low, from, to, vertical, targets, threshold) is { } lowLine)
        {
            bestDistance = Math.Abs(lowLine - low);
            best = (lowLine - low, new SnapGuide(vertical, lowLine));
        }

        if (movesHigh && Nearest(high, from, to, vertical, targets, threshold) is { } highLine
            && Math.Abs(highLine - high) < bestDistance)
            best = (highLine - high, new SnapGuide(vertical, highLine));

        return best;
    }

    internal static SnapResult Drag(PixelRect start, RegionGrip grip, PixelVector delta, IReadOnlyList<PixelRect> targets, int threshold, bool off)
    {
        if (off || grip == RegionGrip.None || threshold <= 0) return new SnapResult(delta, null, null);
        var (movesLeft, movesTop, movesRight, movesBottom) = Edges(grip);
        var left = start.X + (movesLeft ? delta.X : 0);
        var right = start.Right + (movesRight ? delta.X : 0);
        var top = start.Y + (movesTop ? delta.Y : 0);
        var bottom = start.Bottom + (movesBottom ? delta.Y : 0);

        var x = Axis(true, movesLeft, movesRight, left, right, Math.Min(top, bottom), Math.Max(top, bottom), targets, threshold);
        var y = Axis(false, movesTop, movesBottom, top, bottom, Math.Min(left, right), Math.Max(left, right), targets, threshold);
        return new SnapResult(new PixelVector(delta.X + x.Shift, delta.Y + y.Shift), x.Guide, y.Guide);
    }

    internal static PointSnap Point(PixelPoint point, IReadOnlyList<PixelRect> targets, int threshold, bool off)
    {
        if (off || threshold <= 0) return new PointSnap(point, null, null);
        var x = Nearest(point.X, point.Y, point.Y, true, targets, threshold);
        var y = Nearest(point.Y, point.X, point.X, false, targets, threshold);
        return new PointSnap(
            new PixelPoint(x ?? point.X, y ?? point.Y),
            x is { } gx ? new SnapGuide(true, gx) : null,
            y is { } gy ? new SnapGuide(false, gy) : null);
    }

    internal static IReadOnlyList<PixelRect> Lines(PixelRect region, SnapGuide? x, SnapGuide? y, int thickness)
    {
        var lines = new List<PixelRect>();
        var size = Math.Max(1, thickness);
        if (x is { } gx && Edge(region.X, region.Right, gx.At) is { } atX)
            lines.Add(new PixelRect(atX - size / 2, region.Y, size, region.Height));
        if (y is { } gy && Edge(region.Y, region.Bottom, gy.At) is { } atY)
            lines.Add(new PixelRect(region.X, atY - size / 2, region.Width, size));
        return lines;
    }

    private static int? Edge(int low, int high, int at)
        => Math.Abs(low - at) <= 1 ? low : Math.Abs(high - at) <= 1 ? high : null;

    internal static IReadOnlyList<PixelRect> Targets(IEnumerable<PixelRect> screens, IEnumerable<PixelRect> windows)
    {
        var all = new List<PixelRect>();
        foreach (var rect in screens)
            if (rect.Width > 0 && rect.Height > 0) all.Add(rect);
        foreach (var rect in windows)
            if (rect.Width > 0 && rect.Height > 0) all.Add(rect);
        return all;
    }
}
