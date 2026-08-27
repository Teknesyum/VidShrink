namespace VidShrink.App.Playback;

/// <summary>
/// Panelin boy kademesi. Üç durum var ve üçü de tek jest parametresinden
/// (<see cref="ZoomGesture.T"/>) türer; ikinci bir sayaç yoktur.
/// </summary>
internal enum ShelterStage
{
    /// <summary>Taban boyunda: ortadaki sütunun içinde, düzenin bir parçası.</summary>
    Band = 0,

    /// <summary>Taban boyundan büyük: kök katmanda, yan sütunların üstüne taşar ama pencereyi kaplamaz.</summary>
    Mid = 1,

    /// <summary>Tam pencere. Panel ölçeğinin tavanı.</summary>
    Full = 2
}

/// <summary>
/// K3: tek jest parametresi. Tekerlek <see cref="T"/> değerini oynatır; panelin kademesi de
/// boy ölçeği de o tek sayıdan türer. İki ayrı sayaç yok.
///
/// T52: yakınlaştırmanın anlamı değişti. Ölçeklenen şey <b>panelin boyu</b>; görüntü panelin
/// içinde kendi en-boy oranını koruyarak sığar ve ayrıca yakınlaştırılmaz. Ekranda okunan
/// yüzde <see cref="PanelScale"/>'dir: %100 panelin taban boyu, %200 iki katı.
///
/// Kademe eşikleri ölçek cinsinden okunur ve hiçbiri uydurulmaz:
///   Band  — ölçek 1x, panel bandında.
///   Mid   — ölçek <see cref="PromoteScale"/> (varsayılan 2x, <c>PlaybackHoverZoom</c>) ile
///           tavanın arası. Panel bandına sığmaz, kök katmana çıkar.
///   Full  — ölçek tavanda (<c>PlaybackMaxPanelScale</c>), panel pencereyi kaplar.
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

    private const double Epsilon = 1e-9;

    private readonly double _maxPanelScale;

    /// <param name="maxPanelScale">
    /// Panel boyunun tavanı, taban boyun katı olarak. <c>PlaybackMaxPanelScale</c>.
    /// </param>
    /// <param name="promoteScale">
    /// Panelin bandından çıktığı ölçek. <c>PlaybackHoverZoom</c> ile aynı sayı: kullanıcı
    /// "fare üstüne gelince iki kat" dedi, iki kat panel bandına sığmayan ilk boydur.
    /// </param>
    internal ZoomGesture(double maxPanelScale = 4.0, double promoteScale = 2.0)
    {
        if (maxPanelScale < 1.0) throw new ArgumentOutOfRangeException(nameof(maxPanelScale));
        if (promoteScale < 1.0 || promoteScale > maxPanelScale) throw new ArgumentOutOfRangeException(nameof(promoteScale));

        _maxPanelScale = maxPanelScale;
        PromoteScale = promoteScale;
        MidAt = ParameterFor(promoteScale);

        // Histerezis bandı bir tekerlek çentiği genişliğinde; çıkış eşiği bir çentikten
        // alçaksa band eşiğin yarısı kadar olur, yoksa iniş eşiği tabana yapışır ve panel
        // bir daha bandına inemezdi.
        MidDropAt = MidAt - Math.Min(NotchStep, MidAt / 2.0);
    }

    /// <summary>Panelin bandından çıktığı boy ölçeği. Yapıcıdan gelir, burada üretilmez.</summary>
    internal double PromoteScale { get; }

    /// <summary>
    /// Orta kademeye çıkış eşiği. Uydurulmuş bir sayı değil: <see cref="PromoteScale"/>
    /// ölçeğinin parametre karşılığı. Tavan değişirse hesap kendiliğinden düzelir.
    /// </summary>
    internal double MidAt { get; }

    /// <summary>Orta kademeden banda iniş eşiği. Histerezis bandı bir tekerlek çentiği.</summary>
    internal double MidDropAt { get; }

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

    /// <summary>
    /// T52: panelin boy ölçeği. Taban boyda 1, tavanda <c>PlaybackMaxPanelScale</c>.
    /// Ekranda okunan yüzde budur; görüntünün kendi ölçeği değil.
    /// </summary>
    internal double PanelScale => 1.0 + T * (_maxPanelScale - 1.0);

    /// <summary>Kaynağı panoya sığdıran ölçek. T52'den beri görüntünün tek ölçeği bu.</summary>
    internal double FitScale
    {
        get
        {
            if (SourceWidth <= 0 || SourceHeight <= 0) return 1.0;
            if (ViewportWidth <= 0 || ViewportHeight <= 0) return 1.0;
            return Math.Min(ViewportWidth / SourceWidth, ViewportHeight / SourceHeight);
        }
    }

    /// <summary>
    /// Görüntünün çizim ölçeği. Yakınlaştırma paneli büyüttüğü için görüntü hep sığdırma
    /// ölçeğinde durur: panel büyüyünce pano da büyür, görüntü onunla birlikte büyür.
    /// </summary>
    internal double Scale => FitScale;

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
    /// Tekerlek. <paramref name="notches"/> pozitifse panel büyür. Tavanda ya da tabanda
    /// hiçbir şey değişmiyorsa <c>false</c> döner — tekerlek durur.
    /// </summary>
    internal bool Wheel(double notches, double anchorX, double anchorY)
        => Apply(T + notches * NotchStep, anchorX, anchorY);

    /// <summary>
    /// T52: doğrudan bir panel ölçeğine gider. Hedef parametre <see cref="PanelScale"/>'in
    /// tersinden türer, yani tavan değişirse hesap kendiliğinden düzelir; çağıran taraf
    /// hiçbir sayı uydurmaz.
    /// </summary>
    internal bool ScaleTo(double panelScale, double anchorX, double anchorY)
    {
        if (_maxPanelScale <= 1.0 + Epsilon) return false;
        return Apply(ParameterFor(panelScale), anchorX, anchorY);
    }

    private double ParameterFor(double panelScale)
        => _maxPanelScale <= 1.0 + Epsilon ? Floor : (panelScale - 1.0) / (_maxPanelScale - 1.0);

    /// <summary>
    /// Parametreyi taşıyan tek yol. Tekerlek de, doğrudan ölçek de buradan geçer;
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
