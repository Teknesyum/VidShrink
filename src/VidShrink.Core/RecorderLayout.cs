using System;
using System.Collections.Generic;

namespace VidShrink.Core;

/// <summary>
/// Bir monitorun masaustundeki yeri ve o monitorun olcek carpani. Sinir <b>fiziksel
/// piksel</b>, olcek ise pencere sisteminin o monitorde kullandigi carpan (Windows'ta
/// %125 icin 1,25). Ikisi ayri tutuluyor cunku yakalama fiziksel pikselle, pencere
/// yerlesimi olcege bolunmus noktayla konusuyor.
/// </summary>
public sealed record ScreenPlacement(ScreenBounds Bounds, double Scale);

/// <summary>
/// Bolge cizim ortusunun masaustunu kaplamak icin alacagi yer. <see cref="X"/> ve
/// <see cref="Y"/> fiziksel piksel, <see cref="Width"/> ve <see cref="Height"/> ise
/// <see cref="Scale"/> carpanina bolunmus nokta olcusu — pencere sistemi pencere boyunu
/// noktayla aliyor.
/// </summary>
public sealed record OverlayCover(int X, int Y, double Width, double Height, double Scale)
{
    /// <summary>
    /// Ortunun <paramref name="actualScale"/> carpanli bir monitorde acildiginda kapladigi
    /// fiziksel piksel dikdortgeni. Pencerenin boyu noktayla verildigi icin gercek kaplama
    /// <b>pencerenin dustugu</b> monitorun carpanina gore olusuyor; hesapta kullanilan
    /// carpan baska bir monitorunse ortu masaustunu eksik ya da fazla kapliyor.
    /// </summary>
    public RecorderRegion Physical(double actualScale) => new(
        X,
        Y,
        (int)Math.Round(Width * actualScale),
        (int)Math.Round(Height * actualScale));
}

/// <summary>
/// Cok monitorlu masaustunun saf geometrisi: birlesim dikdortgeni, bir noktayi ya da
/// dikdortgeni tasiyan monitor, ortunun yeri. Pencere sistemine hic dokunmuyor — monitor
/// listesi disaridan veriliyor, boylece tek ekranli bir makinede de olculebiliyor.
/// </summary>
public static class RecorderLayout
{
    /// <summary>
    /// Monitorlerin kapsayan dikdortgeni, fiziksel piksel. Bos listede <c>null</c>.
    /// Negatif X'teki bir monitor birlesimi sola tasiyor: kose (0,0) olmak zorunda degil.
    /// </summary>
    public static RecorderRegion? Union(IReadOnlyList<ScreenBounds> screens)
    {
        if (screens is null || screens.Count == 0) return null;

        var left = int.MaxValue;
        var top = int.MaxValue;
        var right = int.MinValue;
        var bottom = int.MinValue;
        foreach (var s in screens)
        {
            if (s.Width <= 0 || s.Height <= 0) continue;
            left = Math.Min(left, s.X);
            top = Math.Min(top, s.Y);
            right = Math.Max(right, s.X + s.Width);
            bottom = Math.Max(bottom, s.Y + s.Height);
        }

        return right <= left || bottom <= top ? null : new RecorderRegion(left, top, right - left, bottom - top);
    }

    /// <summary>Verilen fiziksel piksel noktasini tasiyan monitor; hicbiri tasimiyorsa <c>null</c>.</summary>
    public static ScreenBounds? Containing(IReadOnlyList<ScreenBounds> screens, int x, int y)
    {
        if (screens is null) return null;
        foreach (var s in screens)
            if (x >= s.X && x < s.X + s.Width && y >= s.Y && y < s.Y + s.Height)
                return s;
        return null;
    }

    /// <summary>
    /// Dikdortgenin <b>tamamen</b> tek bir monitorun icinde kalip kalmadigi. Iki monitore
    /// yayilan ya da aradaki bosluga dusen dikdortgen <c>false</c> donuyor.
    /// </summary>
    public static bool WithinOneScreen(IReadOnlyList<ScreenBounds> screens, RecorderRegion region)
    {
        if (screens is null || region is null || region.Width <= 0 || region.Height <= 0) return false;
        foreach (var s in screens)
            if (region.X >= s.X && region.Y >= s.Y
                && region.X + region.Width <= s.X + s.Width
                && region.Y + region.Height <= s.Y + s.Height)
                return true;
        return false;
    }

    /// <summary>
    /// Dikdortgenin her pikselinin bir monitor tarafindan kapsanip kapsanmadigi. Iki
    /// monitore yayilan dikdortgen kapsanmis sayiliyor; monitorler arasindaki bosluga
    /// tasan dikdortgen sayilmiyor — <c>gdigrab</c> orayi siyah veriyor.
    /// </summary>
    public static bool Covered(IReadOnlyList<ScreenBounds> screens, RecorderRegion region)
    {
        if (screens is null || region is null || region.Width <= 0 || region.Height <= 0) return false;

        var xs = new SortedSet<int> { region.X, region.X + region.Width };
        var ys = new SortedSet<int> { region.Y, region.Y + region.Height };
        foreach (var s in screens)
        {
            if (s.X > region.X && s.X < region.X + region.Width) xs.Add(s.X);
            if (s.X + s.Width > region.X && s.X + s.Width < region.X + region.Width) xs.Add(s.X + s.Width);
            if (s.Y > region.Y && s.Y < region.Y + region.Height) ys.Add(s.Y);
            if (s.Y + s.Height > region.Y && s.Y + s.Height < region.Y + region.Height) ys.Add(s.Y + s.Height);
        }

        var xList = new List<int>(xs);
        var yList = new List<int>(ys);
        for (var i = 0; i + 1 < xList.Count; i++)
            for (var j = 0; j + 1 < yList.Count; j++)
            {
                var cx = xList[i] + ((xList[i + 1] - xList[i]) / 2);
                var cy = yList[j] + ((yList[j + 1] - yList[j]) / 2);
                if (Containing(screens, cx, cy) is null) return false;
            }

        return true;
    }

    /// <summary>
    /// Bolge cizim ortusunun yeri. Konum birlesimin sol ust kosesi (fiziksel piksel), boy
    /// ise <b>o koseyi tasiyan</b> monitorun carpanina bolunmus nokta olcusu: pencere
    /// acildiginda o monitorde dogduğu icin carpani da oradan geliyor. Kose hicbir
    /// monitorde degilse birlesimle en cok ortusen monitorun carpani aliniyor.
    /// </summary>
    public static OverlayCover? Cover(IReadOnlyList<ScreenPlacement> screens)
    {
        if (screens is null || screens.Count == 0) return null;

        var bounds = new List<ScreenBounds>(screens.Count);
        foreach (var s in screens) bounds.Add(s.Bounds);
        if (Union(bounds) is not { } union) return null;

        var scale = ScaleAt(screens, union.X, union.Y) ?? Widest(screens);
        if (!(scale > 0)) return null;

        return new OverlayCover(union.X, union.Y, union.Width / scale, union.Height / scale, scale);
    }

    /// <summary>Noktayi tasiyan monitorun carpani; hicbiri tasimiyorsa <c>null</c>.</summary>
    public static double? ScaleAt(IReadOnlyList<ScreenPlacement> screens, int x, int y)
    {
        if (screens is null) return null;
        foreach (var s in screens)
        {
            var b = s.Bounds;
            if (x >= b.X && x < b.X + b.Width && y >= b.Y && y < b.Y + b.Height) return s.Scale;
        }

        return null;
    }

    private static double Widest(IReadOnlyList<ScreenPlacement> screens)
    {
        var best = 0d;
        var area = 0L;
        foreach (var s in screens)
        {
            var size = (long)s.Bounds.Width * s.Bounds.Height;
            if (size <= area || !(s.Scale > 0)) continue;
            area = size;
            best = s.Scale;
        }

        return best;
    }
}
