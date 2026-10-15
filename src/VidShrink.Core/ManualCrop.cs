namespace VidShrink.Core;

/// <summary>Kırpma dikdörtgeninin kareye oranı: her kenar 0..1 arasında, sol üst köşe 0,0.</summary>
public readonly record struct CropFraction(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;
    public double Height => Bottom - Top;
}

/// <summary>
/// SIK-P8: önizlemede elle çizilen kırpma. Ekran noktası önizlemenin içerik dikdörtgenine
/// (yakınlaştırma ve kaydırma dahil) göre orana, oran kaynak pikseline çevrilir. Oran tutulur
/// çünkü önizleme karesi kaynaktan küçük olabilir; piksel ancak kaynak boyutu bilinince çıkar.
/// </summary>
public static class ManualCrop
{
    public static CropFraction Full { get; } = new(0, 0, 1, 1);

    public static (double X, double Y) ToFraction(double x, double y, double contentX, double contentY, double contentWidth, double contentHeight)
        => contentWidth <= 0 || contentHeight <= 0
            ? (0, 0)
            : (Math.Clamp((x - contentX) / contentWidth, 0, 1), Math.Clamp((y - contentY) / contentHeight, 0, 1));

    public static CropFraction FromPoints(double ax, double ay, double bx, double by)
        => new(Math.Clamp(Math.Min(ax, bx), 0, 1), Math.Clamp(Math.Min(ay, by), 0, 1),
            Math.Clamp(Math.Max(ax, bx), 0, 1), Math.Clamp(Math.Max(ay, by), 0, 1));

    public static CropFraction FromScreen(double x1, double y1, double x2, double y2,
        double contentX, double contentY, double contentWidth, double contentHeight)
    {
        var a = ToFraction(x1, y1, contentX, contentY, contentWidth, contentHeight);
        var b = ToFraction(x2, y2, contentX, contentY, contentWidth, contentHeight);
        return FromPoints(a.X, a.Y, b.X, b.Y);
    }

    /// <summary>Dikdörtgeni boyutunu koruyarak kaydırır; kareden taşan kısım kenara yaslanır.</summary>
    public static CropFraction Move(CropFraction f, double dx, double dy)
    {
        var x = Math.Clamp(f.Left + dx, 0, 1 - f.Width);
        var y = Math.Clamp(f.Top + dy, 0, 1 - f.Height);
        return new CropFraction(x, y, x + f.Width, y + f.Height);
    }

    /// <summary>
    /// Oranı kaynak pikseline çevirir: sol üst köşe ve boyut çift sayıya iner, dikdörtgen kareden
    /// taşmaz, en küçük boyut 2 pikseldir. Tam kare seçimi kırpma değildir, <c>null</c> döner.
    /// </summary>
    public static CropRect? ToSource(CropFraction f, int width, int height)
    {
        if (width < 2 || height < 2 || f == Full) return null;
        var (x, w) = Axis(f.Left, f.Right, width);
        var (y, h) = Axis(f.Top, f.Bottom, height);
        return w == width && h == height ? null : new CropRect(w, h, x, y);
    }

    private static (int Start, int Size) Axis(double start, double end, int length)
    {
        var a = Even((int)Math.Round(Math.Clamp(start, 0, 1) * length));
        var b = (int)Math.Round(Math.Clamp(end, 0, 1) * length);
        var size = Math.Clamp(Even(b - a), 2, Even(length));
        if (a + size > length) a = Even(length - size);
        return (a, size);
    }

    private static int Even(int n) => n - (n & 1);
}
