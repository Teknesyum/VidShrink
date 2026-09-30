namespace VidShrink.Core.Editing;

/// <summary>
/// Klibin kaynak ustune uygulanan ayarlari: kaynak koordinatinda kesirli kirpma, saat yonunde
/// 90 adimli dondurme, yatay ve dikey cevirme, desibel ses kazanci, sessize alma ve cizelge
/// tick'inde giris/cikis solmasi. Sirasi sabittir: kirp, dondur, cevir.
/// </summary>
public sealed record ClipEffects
{
    public const double MinVolumeDb = -60;
    public const double MaxVolumeDb = 20;

    public static ClipEffects None { get; } = new();

    public double CropLeft { get; init; }

    public double CropTop { get; init; }

    public double CropRight { get; init; }

    public double CropBottom { get; init; }

    public int Rotation { get; init; }

    public bool FlipH { get; init; }

    public bool FlipV { get; init; }

    public double VolumeDb { get; init; }

    public bool Muted { get; init; }

    public long FadeIn { get; init; }

    public long FadeOut { get; init; }

    public bool HasCrop => CropLeft > 0 || CropTop > 0 || CropRight > 0 || CropBottom > 0;

    public bool HasGeometry => HasCrop || Rotation != 0 || FlipH || FlipV;

    public bool HasAudio => Muted || VolumeDb != 0;

    public bool IsNeutral => !HasGeometry && !HasAudio && FadeIn == 0 && FadeOut == 0;

    /// <summary>Degerleri gecerli araliga ceker: kirpma kenarlari toplamda karenin %95'ini asmaz, aci 90'in katina iner.</summary>
    public ClipEffects Normalized()
    {
        static double Edge(double v) => double.IsFinite(v) ? Math.Clamp(v, 0, 0.95) : 0;
        var left = Edge(CropLeft);
        var right = Math.Min(Edge(CropRight), 0.95 - left);
        var top = Edge(CropTop);
        var bottom = Math.Min(Edge(CropBottom), 0.95 - top);
        var rotation = ((Rotation / 90 * 90) % 360 + 360) % 360;
        var volume = double.IsFinite(VolumeDb) ? Math.Round(Math.Clamp(VolumeDb, MinVolumeDb, MaxVolumeDb), 1) : 0;
        return this with
        {
            CropLeft = left,
            CropRight = right,
            CropTop = top,
            CropBottom = bottom,
            Rotation = rotation,
            VolumeDb = volume,
            FadeIn = Math.Max(0, FadeIn),
            FadeOut = Math.Max(0, FadeOut),
        };
    }

    /// <summary>Kirpma dikdortgeni, depolanan degil gorunen piksel olarak; kenarlar cift, en az 2x2.</summary>
    public (int X, int Y, int Width, int Height) CropRect(int width, int height)
    {
        var x = Even(width * CropLeft);
        var y = Even(height * CropTop);
        var w = Math.Max(2, Even(width * (1 - CropLeft - CropRight)));
        var h = Math.Max(2, Even(height * (1 - CropTop - CropBottom)));
        w = Math.Min(w, EvenFloor(width - x));
        h = Math.Min(h, EvenFloor(height - y));
        return (x, y, Math.Max(2, w), Math.Max(2, h));
    }

    /// <summary>Kirpma ve dondurme sonrasi cikti boyutu; 90 ve 270'te genislik ile yukseklik yer degistirir.</summary>
    public (int Width, int Height) OutputSize(int width, int height)
    {
        var rect = CropRect(width, height);
        return Rotation is 90 or 270 ? (rect.Height, rect.Width) : (rect.Width, rect.Height);
    }

    /// <summary>Uzunlugu <paramref name="length"/> olan klipte etkin giris ve cikis solmasi; ikisi toplamda klibi asmaz.</summary>
    public (long In, long Out) Fades(long length)
    {
        var fadeIn = Math.Clamp(FadeIn, 0, Math.Max(0, length));
        var fadeOut = Math.Clamp(FadeOut, 0, Math.Max(0, length - fadeIn));
        return (fadeIn, fadeOut);
    }

    /// <summary>Kaynak kareden <paramref name="num"/>:<paramref name="den"/> oranli, ortali kirpma; donmus goruntunun oranina gore.</summary>
    public ClipEffects WithAspect(int num, int den, int width, int height)
    {
        var cleared = this with { CropLeft = 0, CropTop = 0, CropRight = 0, CropBottom = 0 };
        if (num <= 0 || den <= 0 || width <= 0 || height <= 0) return cleared;

        var target = Rotation is 90 or 270 ? (double)den / num : (double)num / den;
        var source = (double)width / height;
        if (Math.Abs(target - source) < 1e-6) return cleared;

        if (target < source)
        {
            var cut = (1 - target / source) / 2;
            return cleared with { CropLeft = cut, CropRight = cut };
        }

        var vertical = (1 - source / target) / 2;
        return cleared with { CropTop = vertical, CropBottom = vertical };
    }

    private static int Even(double value) => Math.Max(0, (int)Math.Round(value / 2, MidpointRounding.AwayFromZero) * 2);

    private static int EvenFloor(int value) => Math.Max(0, value / 2 * 2);
}
