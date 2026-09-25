using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace VidShrink.App.Recorder;

internal enum RegionGrip
{
    None,
    Move,
    Left,
    Top,
    Right,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

/// <summary>
/// Bölge düzenleyicisinin saf hesapları. Pencere yalnız çiziyor; tutamak sürükleme, oran
/// kilidi, masaüstü sınırı, araç panelinin yeri ve Windows pencere biçiminin dikdörtgenleri
/// burada, ekran açmadan ölçülebilsin diye.
/// </summary>
internal static class RegionEdit
{
    internal const int MinSide = 32;

    internal static readonly RegionGrip[] Handles =
    {
        RegionGrip.TopLeft, RegionGrip.Top, RegionGrip.TopRight, RegionGrip.Right,
        RegionGrip.BottomRight, RegionGrip.Bottom, RegionGrip.BottomLeft, RegionGrip.Left
    };

    private static (bool Left, bool Top, bool Right, bool Bottom) Edges(RegionGrip grip) => grip switch
    {
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

    private static int Between(int value, int low, int high) => high < low ? low : Math.Clamp(value, low, high);

    private static int Even(int value) => value - value % 2;

    /// <summary>
    /// Sürüklemenin sonucu. Taşıma boyu korur ve bölgeyi sınırın içine iter; tutamak karşı
    /// kenarı sabit tutar. Oran kilidi köşede baskın kenarı izler, kenar tutamağında öteki
    /// boyu ortadan büyütür. Genişlik ve yükseklik çift sayıya iner.
    /// </summary>
    internal static PixelRect Drag(PixelRect start, RegionGrip grip, PixelVector delta, double? ratio, PixelRect bounds)
    {
        if (grip == RegionGrip.None) return start;
        if (grip == RegionGrip.Move)
            return Fit(new PixelRect(start.X + delta.X, start.Y + delta.Y, start.Width, start.Height), bounds);

        var (movesLeft, movesTop, movesRight, movesBottom) = Edges(grip);
        var left = start.X;
        var top = start.Y;
        var right = start.Right;
        var bottom = start.Bottom;

        if (movesLeft) left = Between(start.X + delta.X, bounds.X, right - MinSide);
        if (movesRight) right = Between(start.Right + delta.X, left + MinSide, bounds.Right);
        if (movesTop) top = Between(start.Y + delta.Y, bounds.Y, bottom - MinSide);
        if (movesBottom) bottom = Between(start.Bottom + delta.Y, top + MinSide, bounds.Bottom);

        var horizontal = movesLeft || movesRight;
        var vertical = movesTop || movesBottom;
        var width = right - left;
        var height = bottom - top;

        if (ratio is { } r && r > 0)
        {
            double w = width;
            double h = height;
            if (horizontal && vertical)
            {
                if (h == 0 || w / h > r) h = w / r;
                else w = h * r;
            }
            else if (horizontal) h = w / r;
            else w = h * r;

            double roomX = horizontal ? (movesLeft ? right - bounds.X : bounds.Right - left) : bounds.Width;
            double roomY = vertical ? (movesTop ? bottom - bounds.Y : bounds.Bottom - top) : bounds.Height;
            if (h > roomY) { h = roomY; w = h * r; }
            if (w > roomX) { w = roomX; h = w / r; }
            width = (int)Math.Round(w);
            height = (int)Math.Round(h);
        }

        width = Even(width);
        height = Even(height);

        var x = movesLeft ? right - width
            : movesRight ? left
            : width == start.Width ? start.X
            : Between((int)Math.Round(start.X + start.Width / 2.0 - width / 2.0), bounds.X, bounds.Right - width);
        var y = movesTop ? bottom - height
            : movesBottom ? top
            : height == start.Height ? start.Y
            : Between((int)Math.Round(start.Y + start.Height / 2.0 - height / 2.0), bounds.Y, bounds.Bottom - height);

        return Fit(new PixelRect(x, y, width, height), bounds);
    }

    /// <summary>Bölgeyi sınırın içine iter; sınırdan büyükse sınıra küçültür.</summary>
    internal static PixelRect Fit(PixelRect region, PixelRect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return region;
        var width = Math.Min(region.Width, Even(bounds.Width));
        var height = Math.Min(region.Height, Even(bounds.Height));
        var x = Between(region.X, bounds.X, bounds.Right - width);
        var y = Between(region.Y, bounds.Y, bounds.Bottom - height);
        return new PixelRect(x, y, width, height);
    }

    private static bool Inside(PixelRect rect, PixelPoint point)
        => point.X >= rect.X && point.X < rect.Right && point.Y >= rect.Y && point.Y < rect.Bottom;

    private static PixelRect Inflate(PixelRect rect, int by)
        => new(rect.X - by, rect.Y - by, Math.Max(0, rect.Width + 2 * by), Math.Max(0, rect.Height + 2 * by));

    /// <summary>Tutamağın merkezi: köşeler ve kenar ortaları.</summary>
    internal static PixelPoint HandleCenter(PixelRect region, RegionGrip grip)
    {
        var midX = region.X + region.Width / 2;
        var midY = region.Y + region.Height / 2;
        return grip switch
        {
            RegionGrip.TopLeft => new PixelPoint(region.X, region.Y),
            RegionGrip.Top => new PixelPoint(midX, region.Y),
            RegionGrip.TopRight => new PixelPoint(region.Right, region.Y),
            RegionGrip.Right => new PixelPoint(region.Right, midY),
            RegionGrip.BottomRight => new PixelPoint(region.Right, region.Bottom),
            RegionGrip.Bottom => new PixelPoint(midX, region.Bottom),
            RegionGrip.BottomLeft => new PixelPoint(region.X, region.Bottom),
            RegionGrip.Left => new PixelPoint(region.X, midY),
            _ => new PixelPoint(midX, midY)
        };
    }

    internal static PixelRect HandleRect(PixelRect region, RegionGrip grip, int size)
    {
        var center = HandleCenter(region, grip);
        return new PixelRect(center.X - size / 2, center.Y - size / 2, size, size);
    }

    /// <summary>
    /// İmlecin altındaki tutamak. Önce sekiz tutamak, sonra çerçevenin iki yanındaki
    /// <paramref name="band"/> genişliğinde şerit (taşıma); iç alan ve dışarısı boş.
    /// </summary>
    internal static RegionGrip Hit(PixelPoint point, PixelRect region, int band, int handle)
    {
        var size = Math.Max(handle, 2 * band);
        foreach (var grip in Handles)
            if (Inside(HandleRect(region, grip, size), point)) return grip;

        if (!Inside(Inflate(region, band), point)) return RegionGrip.None;
        var inner = Inflate(region, -band);
        return inner.Width > 0 && inner.Height > 0 && Inside(inner, point) ? RegionGrip.None : RegionGrip.Move;
    }

    /// <summary>Çerçevenin tıklama alan halkası: dört şerit, iç alanı boş bırakıyor.</summary>
    internal static IReadOnlyList<PixelRect> Ring(PixelRect region, int band)
    {
        var side = Math.Max(0, region.Height - 2 * band);
        var list = new List<PixelRect>
        {
            new(region.X - band, region.Y - band, region.Width + 2 * band, 2 * band),
            new(region.X - band, region.Bottom - band, region.Width + 2 * band, 2 * band)
        };
        if (side > 0)
        {
            list.Add(new PixelRect(region.X - band, region.Y + band, 2 * band, side));
            list.Add(new PixelRect(region.Right - band, region.Y + band, 2 * band, side));
        }

        return list;
    }

    /// <summary>
    /// Araç panelinin sol üst köşesi. Bölgenin üstüne <paramref name="gap"/> payla konur;
    /// ekranda üstte yer yoksa altına, orada da yoksa bölgenin içine üstten. Yatayda bölgenin
    /// solundan hizalanır ve ekranın içinde tutulur.
    /// </summary>
    internal static PixelPoint Toolbar(PixelRect region, PixelSize bar, int gap, PixelRect screen)
    {
        var above = region.Y - gap - bar.Height;
        var below = region.Bottom + gap;
        var y = above >= screen.Y ? above
            : below + bar.Height <= screen.Bottom ? below
            : region.Y + gap;
        var x = Between(region.X, screen.X, screen.Right - bar.Width);
        return new PixelPoint(x, y);
    }

    /// <summary>Bölgenin ortasını taşıyan ekran; hiçbiri taşımıyorsa en çok kesişen, o da yoksa masaüstü.</summary>
    internal static PixelRect ScreenOf(PixelRect region, IEnumerable<PixelRect> screens, PixelRect desktop)
    {
        var all = screens.ToList();
        var center = new PixelPoint(region.X + region.Width / 2, region.Y + region.Height / 2);
        foreach (var screen in all)
            if (Inside(screen, center)) return screen;

        var best = all
            .Select(s => (Screen: s, Area: (long)s.Intersect(region).Width * s.Intersect(region).Height))
            .Where(p => p.Area > 0)
            .OrderByDescending(p => p.Area)
            .FirstOrDefault();
        return best.Area > 0 ? best.Screen : desktop;
    }

    /// <summary>
    /// Windows pencere biçimi: çerçeve halkası, sekiz tutamak ve araç paneli. Dikdörtgenler
    /// pencerenin sol üst köşesine (<paramref name="origin"/>) göre; bu biçimin dışı
    /// tıklamayı alttaki uygulamaya bırakır.
    /// </summary>
    internal static IReadOnlyList<PixelRect> Shape(PixelRect region, int band, int handle, PixelRect toolbar, PixelPoint origin)
    {
        var parts = new List<PixelRect>(Ring(region, band));
        parts.AddRange(Handles.Select(g => HandleRect(region, g, Math.Max(handle, 2 * band))));
        parts.Add(toolbar);
        return parts
            .Where(p => p.Width > 0 && p.Height > 0)
            .Select(p => new PixelRect(p.X - origin.X, p.Y - origin.Y, p.Width, p.Height))
            .ToList();
    }
}
