using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace VidShrink.App.Recorder;

internal static class RegionDraw
{
    internal const string Free = "free";

    internal static readonly string[] Aspects = { Free, "16:9", "4:3", "1:1", "9:16" };

    internal static readonly (int Width, int Height)[] Sizes =
    {
        (1920, 1080), (1280, 720), (854, 480), (1080, 1080), (1080, 1920)
    };

    internal static double? Ratio(string? aspect)
    {
        if (string.IsNullOrEmpty(aspect) || aspect == Free) return null;
        var parts = aspect.Split(':');
        return parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h) && w > 0 && h > 0
            ? (double)w / h
            : null;
    }

    internal static PixelRect Desktop(IEnumerable<PixelRect> screens)
    {
        var all = screens.ToList();
        if (all.Count == 0) return default;
        var left = all.Min(s => s.X);
        var top = all.Min(s => s.Y);
        return new PixelRect(left, top, all.Max(s => s.Right) - left, all.Max(s => s.Bottom) - top);
    }

    internal static PixelRect FromDrag(PixelPoint start, PixelPoint end, double? ratio, PixelRect bounds)
    {
        var x0 = Math.Clamp(start.X, bounds.X, bounds.Right);
        var y0 = Math.Clamp(start.Y, bounds.Y, bounds.Bottom);
        var x1 = Math.Clamp(end.X, bounds.X, bounds.Right);
        var y1 = Math.Clamp(end.Y, bounds.Y, bounds.Bottom);

        var width = Math.Abs(x1 - x0);
        var height = Math.Abs(y1 - y0);

        if (ratio is { } r)
        {
            var roomX = x1 >= x0 ? bounds.Right - x0 : x0 - bounds.X;
            var roomY = y1 >= y0 ? bounds.Bottom - y0 : y0 - bounds.Y;
            if (height == 0 || (double)width / height > r) height = (int)Math.Round(width / r);
            else width = (int)Math.Round(height * r);
            if (height > roomY) { height = roomY; width = (int)Math.Round(height * r); }
            if (width > roomX) { width = roomX; height = (int)Math.Round(width / r); }
        }

        width -= width % 2;
        height -= height % 2;
        var left = x1 >= x0 ? x0 : x0 - width;
        var top = y1 >= y0 ? y0 : y0 - height;
        return new PixelRect(left, top, width, height);
    }

    internal static bool Usable(PixelRect rect) => rect.Width >= 2 && rect.Height >= 2;
}
