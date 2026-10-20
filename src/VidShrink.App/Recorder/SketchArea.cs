using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace VidShrink.App.Recorder;

internal readonly record struct SketchScreen(PixelRect Bounds, double Scaling);

internal static class SketchArea
{
    internal static PixelRect Of(PixelRect? region, IReadOnlyList<SketchScreen> screens, PixelPoint? cursor)
    {
        if (screens.Count == 0) return region ?? default;
        var under = cursor is { } point ? screens.Where(s => s.Bounds.Contains(point)).Cast<SketchScreen?>().FirstOrDefault() : null;
        if (region is not { } area) return (under ?? Primary(screens)).Bounds;

        var touched = screens.Where(s => Overlap(s.Bounds, area) > 0).ToList();
        if (touched.Select(s => s.Scaling).Distinct().Count() <= 1) return area;

        var chosen = under is { } u && touched.Contains(u) ? u : touched.OrderByDescending(s => Overlap(s.Bounds, area)).First();
        return area.Intersect(chosen.Bounds);
    }

    internal static double ScalingOf(PixelRect area, IReadOnlyList<SketchScreen> screens)
        => screens.Where(s => Overlap(s.Bounds, area) > 0).OrderByDescending(s => Overlap(s.Bounds, area))
            .Select(s => s.Scaling).DefaultIfEmpty(screens.Count > 0 ? Primary(screens).Scaling : 1).First();

    private static SketchScreen Primary(IReadOnlyList<SketchScreen> screens)
        => screens.FirstOrDefault(s => s.Bounds.Contains(new PixelPoint(0, 0)), screens[0]);

    private static long Overlap(PixelRect a, PixelRect b)
    {
        var both = a.Intersect(b);
        return (long)both.Width * both.Height;
    }
}
