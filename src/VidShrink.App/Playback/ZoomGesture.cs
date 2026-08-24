namespace VidShrink.App.Playback;

/// <summary>
/// K3: tek jest parametresi. Tekerlek <see cref="T"/> değerini oynatır; panelin boyu da,
/// görüntünün yakınlaştırması da o tek sayıdan türer. İki ayrı sayaç yok.
///
/// Sınıf bilerek saf: hiçbir Avalonia türü kullanmıyor, bu yüzden test etmek pencere
/// açmayı gerektirmiyor. Bütün ölçüler kullanıcı arayüzü birimidir (DIP) ve dışarıdan
/// verilir; sınıf kendi başına hiçbir ölçü uydurmaz.
/// </summary>
internal sealed class ZoomGesture
{
    /// <summary>Terfi eşiği. Plan §2, kapanmış tartışma.</summary>
    internal const double PromoteAt = 1.00;

    /// <summary>İniş eşiği. Histerezis bandı burada başlar.</summary>
    internal const double DemoteAt = 0.92;

    internal const double Floor = 0.00;

    /// <summary>Tavan. Buraya varınca tekerlek durur, panel daha büyümez.</summary>
    internal const double Ceiling = PromoteAt;

    /// <summary>
    /// Bir tekerlek çentiğinin adımı. Uydurulmuş bir sayı değil: histerezis bandının
    /// genişliği (1.00 - 0.92). Böylece tavandaki tek çentik geri iniş eşiğine değer,
    /// bir fazlası inişi tetikler; band tam bir çentiktir.
    /// </summary>
    internal const double NotchStep = Ceiling - DemoteAt;

    private const double Epsilon = 1e-9;

    private readonly double _maxContentZoom;

    internal ZoomGesture(double maxContentZoom = 4.0)
    {
        if (maxContentZoom < 1.0) throw new ArgumentOutOfRangeException(nameof(maxContentZoom));
        _maxContentZoom = maxContentZoom;
    }

    internal double T { get; private set; }

    /// <summary>Terfi hâli. Histerezisli: eşikler farklıdır, bu yüzden titrek tekerlek çırpınmaz.</summary>
    internal bool Promoted { get; private set; }

    internal double ViewportWidth { get; private set; }
    internal double ViewportHeight { get; private set; }
    internal double SourceWidth { get; private set; }
    internal double SourceHeight { get; private set; }

    /// <summary>Kaynağın sol üst köşesinin pano içindeki yeri.</summary>
    internal double OffsetX { get; private set; }

    internal double OffsetY { get; private set; }

    /// <summary>t=0'da 1, t=1'de tavan. Görüntü yakınlaştırması.</summary>
    internal double ContentZoom => 1.0 + T * (_maxContentZoom - 1.0);

    /// <summary>Kaynağı panoya sığdıran ölçek. Yakınlaştırma bunun üstüne biner.</summary>
    internal double FitScale
    {
        get
        {
            if (SourceWidth <= 0 || SourceHeight <= 0) return 1.0;
            if (ViewportWidth <= 0 || ViewportHeight <= 0) return 1.0;
            return Math.Min(ViewportWidth / SourceWidth, ViewportHeight / SourceHeight);
        }
    }

    internal double Scale => FitScale * ContentZoom;

    internal double ContentWidth => SourceWidth * Scale;

    internal double ContentHeight => SourceHeight * Scale;

    internal bool AtCeiling => T >= Ceiling - Epsilon;

    internal bool AtFloor => T <= Floor + Epsilon;

    internal void SetViewport(double width, double height)
    {
        if (Same(width, ViewportWidth) && Same(height, ViewportHeight)) return;
        ViewportWidth = Math.Max(0, width);
        ViewportHeight = Math.Max(0, height);
        ClampOffset();
    }

    internal void SetSource(double width, double height)
    {
        if (Same(width, SourceWidth) && Same(height, SourceHeight)) return;
        SourceWidth = Math.Max(0, width);
        SourceHeight = Math.Max(0, height);
        ClampOffset();
    }

    /// <summary>
    /// Tekerlek. <paramref name="notches"/> pozitifse yakınlaşır. İmlecin altındaki kaynak
    /// noktası sabit kalır: panonun ortasına değil, farenin gösterdiği yere yakınlaşır.
    /// Tavanda ya da tabanda hiçbir şey değişmiyorsa <c>false</c> döner — tekerlek durur.
    /// </summary>
    internal bool Wheel(double notches, double anchorX, double anchorY)
    {
        var target = Clamp(T + notches * NotchStep, Floor, Ceiling);
        if (Same(target, T)) return false;

        var before = Scale;
        var sourceX = before > 0 ? (anchorX - OffsetX) / before : 0;
        var sourceY = before > 0 ? (anchorY - OffsetY) / before : 0;

        T = target;

        var after = Scale;
        OffsetX = anchorX - sourceX * after;
        OffsetY = anchorY - sourceY * after;

        ClampOffset();
        UpdateShelter();
        return true;
    }

    /// <summary>Pano sürükleme. Kaynağın dışına çıkılamaz; clamp bunu kesin yapar.</summary>
    internal bool Drag(double deltaX, double deltaY)
    {
        var beforeX = OffsetX;
        var beforeY = OffsetY;
        OffsetX += deltaX;
        OffsetY += deltaY;
        ClampOffset();
        return !Same(beforeX, OffsetX) || !Same(beforeY, OffsetY);
    }

    /// <summary>Görüntü panoya sığmıyorsa sürüklenebilir; sığıyorsa sürüklenecek yer yoktur.</summary>
    internal bool CanPan => ContentWidth > ViewportWidth + Epsilon || ContentHeight > ViewportHeight + Epsilon;

    /// <summary>Düğmeyle terfi: parametreyi tavana taşır.</summary>
    internal void Promote()
    {
        T = Ceiling;
        ClampOffset();
        UpdateShelter();
    }

    /// <summary>
    /// <c>Esc</c> ile iniş. Parametre tabana döner, pano ortalanır — inen panel
    /// yakınlaştırılmış bir köşede kalmaz.
    /// </summary>
    internal void Demote()
    {
        T = Floor;
        OffsetX = 0;
        OffsetY = 0;
        ClampOffset();
        UpdateShelter();
    }

    internal void Reset() => Demote();

    private void UpdateShelter()
    {
        if (!Promoted)
        {
            if (T >= PromoteAt - Epsilon) Promoted = true;
        }
        else if (T < DemoteAt - Epsilon)
        {
            Promoted = false;
        }
    }

    private void ClampOffset()
    {
        OffsetX = ClampAxis(OffsetX, ContentWidth, ViewportWidth);
        OffsetY = ClampAxis(OffsetY, ContentHeight, ViewportHeight);
    }

    private static double ClampAxis(double offset, double content, double viewport)
    {
        if (content <= viewport) return (viewport - content) / 2.0;
        return Clamp(offset, viewport - content, 0);
    }

    private static double Clamp(double value, double low, double high)
        => value < low ? low : value > high ? high : value;

    private static bool Same(double a, double b) => Math.Abs(a - b) < Epsilon;
}
