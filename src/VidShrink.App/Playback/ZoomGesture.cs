namespace VidShrink.App.Playback;

/// <summary>
/// Panelin boy kademesi. Üç durum var ve üçü de tek jest parametresinden
/// (<see cref="ZoomGesture.T"/>) türer; ikinci bir sayaç yoktur.
/// </summary>
internal enum ShelterStage
{
    /// <summary>Bandında: ortadaki sütunun içinde, düzenin bir parçası.</summary>
    Band = 0,

    /// <summary>Orta boy: kök katmanda, yan sütunların üstüne taşar ama pencereyi kaplamaz.</summary>
    Mid = 1,

    /// <summary>Tam pencere.</summary>
    Full = 2
}

/// <summary>
/// K3: tek jest parametresi. Tekerlek <see cref="T"/> değerini oynatır; panelin kademesi de,
/// görüntünün yakınlaştırması da o tek sayıdan türer. İki ayrı sayaç yok.
///
/// T44: kademe ikiden üçe çıktı. Görüntü yakınlaştırması eskisi gibi sürekli
/// (<see cref="ContentZoom"/> parametreyle doğrusal); kademelenen yalnız panelin boyu.
/// Her geçişin çıkış ve iniş eşiği ayrıdır — titrek tekerlek kademeler arasında çırpınmaz.
///
/// Sınıf bilerek saf: hiçbir Avalonia türü kullanmıyor, bu yüzden test etmek pencere
/// açmayı gerektirmiyor. Bütün ölçüler kullanıcı arayüzü birimidir (DIP) ve dışarıdan
/// verilir; sınıf kendi başına hiçbir ölçü uydurmaz.
/// </summary>
internal sealed class ZoomGesture
{
    /// <summary>Tam pencereye çıkış eşiği. Plan §2, kapanmış tartışma.</summary>
    internal const double FullAt = 1.00;

    /// <summary>Tam pencereden iniş eşiği. Histerezis bandı burada başlar.</summary>
    internal const double FullDropAt = 0.92;

    internal const double Floor = 0.00;

    /// <summary>Tavan. Buraya varınca tekerlek durur, panel daha büyümez.</summary>
    internal const double Ceiling = FullAt;

    /// <summary>
    /// Bir tekerlek çentiğinin adımı. Uydurulmuş bir sayı değil: histerezis bandının
    /// genişliği (1.00 - 0.92). Böylece tavandaki tek çentik geri iniş eşiğine değer,
    /// bir fazlası inişi tetikler; band tam bir çentiktir.
    /// </summary>
    internal const double NotchStep = Ceiling - FullDropAt;

    /// <summary>
    /// Orta boya çıkış eşiği: aralığın tam ortası. Uydurulmuş bir sayı değil,
    /// <see cref="Floor"/> ile <see cref="Ceiling"/> arasının yarısı.
    /// </summary>
    internal const double MidAt = (Floor + Ceiling) / 2.0;

    /// <summary>
    /// Orta boydan banda iniş eşiği. Tam penceredeki ilişkinin aynısı: histerezis bandı
    /// tam bir tekerlek çentiği genişliğinde.
    /// </summary>
    internal const double MidDropAt = MidAt - NotchStep;

    private const double Epsilon = 1e-9;

    private readonly double _maxContentZoom;

    internal ZoomGesture(double maxContentZoom = 4.0)
    {
        if (maxContentZoom < 1.0) throw new ArgumentOutOfRangeException(nameof(maxContentZoom));
        _maxContentZoom = maxContentZoom;
    }

    internal double T { get; private set; }

    /// <summary>
    /// Boy kademesi. Histerezisli: her geçişin çıkış ve iniş eşiği farklıdır, bu yüzden
    /// titrek tekerlek çırpınmaz.
    /// </summary>
    internal ShelterStage Shelter { get; private set; } = ShelterStage.Band;

    /// <summary>Panel bandından çıkmış mı — yani kök katmanda mı çiziliyor.</summary>
    internal bool Promoted => Shelter != ShelterStage.Band;

    internal double ViewportWidth { get; private set; }
    internal double ViewportHeight { get; private set; }
    internal double SourceWidth { get; private set; }
    internal double SourceHeight { get; private set; }

    /// <summary>Kaynağın sol üst köşesinin pano içindeki yeri.</summary>
    internal double OffsetX { get; private set; }

    internal double OffsetY { get; private set; }

    /// <summary>t=0 değerinde 1, tavanda ise en büyük değer. Görüntü yakınlaştırması.</summary>
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
        => Apply(T + notches * NotchStep, anchorX, anchorY);

    /// <summary>
    /// T46/K5: doğrudan bir görüntü yakınlaştırmasına gider. Hedef parametre
    /// <see cref="ContentZoom"/>'un tersinden türer, yani tavan değişirse hesap
    /// kendiliğinden düzelir; çağıran taraf hiçbir sayı uydurmaz.
    /// </summary>
    internal bool ZoomTo(double contentZoom, double anchorX, double anchorY)
    {
        if (_maxContentZoom <= 1.0 + Epsilon) return false;
        return Apply((contentZoom - 1.0) / (_maxContentZoom - 1.0), anchorX, anchorY);
    }

    /// <summary>
    /// Parametreyi taşıyan tek yol. Tekerlek de, doğrudan yakınlaştırma da buradan geçer;
    /// iki giriş tek kaynağa yazsın diye ayrı bir gövde yok.
    /// </summary>
    private bool Apply(double value, double anchorX, double anchorY)
    {
        var target = Clamp(value, Floor, Ceiling);
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

    /// <summary>Düğmeyle terfi: parametreyi tavana taşır, yani doğrudan tam pencereye.</summary>
    internal void Promote()
    {
        T = Ceiling;
        ClampOffset();
        UpdateShelter();
    }

    /// <summary>
    /// Esc ile ve iniş sayacının zaman aşımıyla kullanılan tek yol. Parametre tabana döner,
    /// pano ortalanır, kademe banda iner. Zaman aşımı da buradan geçtiği için panel
    /// küçülürken parametre tavanda kalmaz ve sonraki tek tekerlek dokunuşu paneli tam
    /// pencereye fırlatmaz (T44/K2).
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

    /// <summary>
    /// Kademe kararı. Yükseliş eşikleri <see cref="MidAt"/> ve <see cref="FullAt"/>, iniş
    /// eşikleri <see cref="MidDropAt"/> ve <see cref="FullDropAt"/>; aradaki band bir
    /// tekerlek çentiği genişliğinde. İki yükseliş eşiği arasındaki mesafe bir çentikten
    /// büyük olduğu için hiçbir kademe tek çentikle atlanamaz.
    /// </summary>
    private void UpdateShelter() => Shelter = Shelter switch
    {
        ShelterStage.Band => T >= FullAt - Epsilon ? ShelterStage.Full
            : T >= MidAt - Epsilon ? ShelterStage.Mid
            : ShelterStage.Band,
        ShelterStage.Mid => T >= FullAt - Epsilon ? ShelterStage.Full
            : T < MidDropAt - Epsilon ? ShelterStage.Band
            : ShelterStage.Mid,
        _ => T < MidDropAt - Epsilon ? ShelterStage.Band
            : T < FullDropAt - Epsilon ? ShelterStage.Mid
            : ShelterStage.Full
    };

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
