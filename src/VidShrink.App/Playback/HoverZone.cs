using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace VidShrink.App.Playback;

/// <summary>
/// Gecikmeyi işleten saat. Tek uygulaması <see cref="DispatcherHoverClock"/>; ölçüm
/// yerine sahtesini takıp bekleme süresini duvar saati olmadan sürer (T70/K7).
/// </summary>
internal interface IHoverClock
{
    void Start(TimeSpan delay, Action fire);

    void Stop();
}

/// <summary>Gerçek saat: arayüz iş parçacığının zamanlayıcısı.</summary>
internal sealed class DispatcherHoverClock : IHoverClock
{
    private DispatcherTimer? _timer;

    public void Start(TimeSpan delay, Action fire)
    {
        Stop();
        var timer = new DispatcherTimer { Interval = delay };
        _timer = timer;
        timer.Tick += (_, _) =>
        {
            Stop();
            fire();
        };
        timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }
}

/// <summary>
/// Panelin alt bölgesindeki fare durumunu ve şeridin görünürlüğünü tutar.
///
/// Bölge yüksekliği piksel değil oran: <see cref="Share"/> panelin yüksekliğiyle çarpılır,
/// böylece panel terfi edip program boyuna çıktığında bölge de büyür.
///
/// Kaybolma gecikmesi bir kuşak sayacıyla korunuyor. Zamanlayıcı tik attığında kendi
/// kuşağını taşıyıp taşımadığına bakıyor; araya yeni bir gösterme girdiyse eski tik
/// kararını uygulamıyor. Opaklık okuyup karar veren kalıp bu yarışı kaybediyordu.
///
/// T44: aynı kuşak koruması karşılaştırma panelinin gecikmeli inişini de sürüyor. Orada
/// bölge oranı değil, hedef kademenin sınırı karar veriyor; sınır dışarıdan
/// <see cref="PointerWithin"/> ile bildiriliyor.
///
/// T70: iki yön iki ayrı gecikme taşıyor. Gösterme gecikmesi farenin üstünde <i>durduğu</i>
/// süreyi ölçer — süre dolmadan fare çıkarsa kuşak ilerler ve bekleyen tik kararını
/// uygulamaz. Gecikmesi sıfır olan yön beklemeden uygulanır; zamanlayıcı hiç kurulmaz.
/// </summary>
internal sealed class HoverZone
{
    private const uint ClientAreaAnimationQuery = 0x1042;

    private static bool? _motionReduced;

    private readonly Func<TimeSpan> _showDelay;
    private readonly Func<TimeSpan> _hideDelay;
    private readonly Action<bool> _apply;
    private readonly double _share;

    private IHoverClock _clock = new DispatcherHoverClock();
    private int _generation;
    private bool _pointerInside;
    private bool _held;
    private bool _visible;

    internal HoverZone(double share, Func<TimeSpan> showDelay, Func<TimeSpan> hideDelay, Action<bool> apply)
    {
        _share = Math.Clamp(share, 0, 1);
        _showDelay = showDelay;
        _hideDelay = hideDelay;
        _apply = apply;
    }

    /// <summary>
    /// T73: gösterme beklemesinin dışarıya açılan yüzü. Sayaç kurulduğunda kurulan süreyle,
    /// bittiğinde ya da iptal edildiğinde <c>null</c> ile çağrılır.
    ///
    /// Süre burada verilmese gösterge belirteci ikinci kez okumak zorunda kalırdı; o zaman
    /// belirteç değiştiğinde gösterge ile sayaç ayrı sürelere bakar ve gösterge yalan söyler.
    /// Verilen değer <see cref="_showDelay"/>'in bu turda döndürdüğü sayının kendisidir.
    /// </summary>
    internal Action<TimeSpan?>? ShowCountdown { get; set; }

    /// <summary>Gecikmeyi işleten saat. Ölçüm sahtesini takar; bekleyen iş devralınmaz.</summary>
    internal IHoverClock Clock
    {
        get => _clock;
        set
        {
            _clock.Stop();
            _clock = value;
        }
    }

    /// <summary>
    /// K5: azaltılmış hareket ayarını okuyan tek yer. Karşılaştırma paneli de buraya bağlı;
    /// ikinci kopya yok. Ayar oturum boyunca bir kez okunur.
    /// </summary>
    internal static bool MotionReduced => _motionReduced ??= !AnimationsAllowed();

    /// <summary>Bölgenin panel yüksekliğine oranı.</summary>
    internal double Share => _share;

    internal bool IsVisible => _visible;

    /// <summary>Verilen nokta panelin alt bölgesinin içinde mi.</summary>
    internal bool Covers(double y, double panelHeight)
        => panelHeight > 0 && y >= panelHeight * (1 - _share) && y <= panelHeight;

    internal void PointerAt(double y, double panelHeight) => SetPointer(Covers(y, panelHeight));

    internal void PointerGone() => SetPointer(false);

    /// <summary>
    /// Bölgesi oranla değil, dışarıdan verilen bir sınırla belirlenen kullanıcılar için.
    /// Karşılaştırma paneli bunu hedef kademenin sınırına göre çağırır.
    /// </summary>
    internal void PointerWithin(bool inside) => SetPointer(inside);

    /// <summary>
    /// Sayacı sıfırdan kurar. Bekleyen tik kuşağını kaybettiği için kararını uygulamaz;
    /// tutma sebepleri temizlenir, <paramref name="visible"/> yeni başlangıç hâlidir.
    /// <see cref="_apply"/> çağrılmaz — durum değişimi çağıranın kendi elinde.
    ///
    /// <paramref name="pointerInside"/> çağıranın bildiği fare durumudur: terfi eden panel
    /// bunu verirse fare hiç kıpırdamadan çıktığında da sayaç çıkışı görür.
    /// </summary>
    internal void Reset(bool visible, bool pointerInside = false)
    {
        _generation++;
        _clock.Stop();
        ShowCountdown?.Invoke(null);
        _pointerInside = pointerInside;
        _held = false;
        _visible = visible;
    }

    /// <summary>
    /// Bekleyen bir tikin kararını uygulayıp uygulamayacağı. Zamanlayıcı tam bunu soruyor;
    /// ölçüm de aynı soruyu sorabilsin diye ayrı bir kapı.
    /// </summary>
    internal bool ShouldHide(int generation)
        => generation == _generation && !_pointerInside && !_held;

    /// <summary>Bekleyen gösterme tikinin kararını uygulayıp uygulamayacağı.</summary>
    internal bool ShouldShow(int generation)
        => generation == _generation && (_pointerInside || _held);

    /// <summary>Bekleyen tikin taşıdığı kuşak. Ölçüm bunu okuyup eskisiyle karşılaştırır.</summary>
    internal int Generation => _generation;

    /// <summary>
    /// Şeridi açık tutan sebepler: fare şeridin üstünde, klavye odağı şeritte, oynatma
    /// duraklamış. Biri bile doğruyken gecikmeli kaybolma çalışmaz.
    /// </summary>
    internal void Hold(bool held)
    {
        if (_held == held) return;
        _held = held;
        Evaluate();
    }

    private void SetPointer(bool inside)
    {
        if (_pointerInside == inside) return;
        _pointerInside = inside;
        Evaluate();
    }

    private void Evaluate()
    {
        if (_pointerInside || _held) ScheduleShow();
        else ScheduleHide();
    }

    private void ScheduleShow()
    {
        if (_visible)
        {
            Cancel();
            return;
        }

        var delay = _showDelay();
        if (delay <= TimeSpan.Zero)
        {
            Cancel();
            Apply(true);
            return;
        }

        var mine = Cancel();
        ShowCountdown?.Invoke(delay);
        _clock.Start(delay, () =>
        {
            if (!ShouldShow(mine)) return;
            ShowCountdown?.Invoke(null);
            Apply(true);
        });
    }

    private void ScheduleHide()
    {
        if (!_visible)
        {
            Cancel();
            return;
        }

        var delay = _hideDelay();
        if (delay <= TimeSpan.Zero)
        {
            Cancel();
            Apply(false);
            return;
        }

        var mine = Cancel();
        _clock.Start(delay, () =>
        {
            if (!ShouldHide(mine)) return;
            Apply(false);
        });
    }

    /// <summary>
    /// Bekleyen tiki geçersizler ve yeni kuşağı döndürür. Bekleme kesildiği için geri sayım
    /// da biter — iptal hangi yönden gelirse gelsin gösterge burada kapanır. Süre dolarak
    /// biten geri sayım buradan geçmez; onu tikin kendisi bildirir.
    /// </summary>
    private int Cancel()
    {
        _generation++;
        _clock.Stop();
        ShowCountdown?.Invoke(null);
        return _generation;
    }

    private void Apply(bool visible)
    {
        _visible = visible;
        _apply(visible);
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool SystemParametersInfoW(uint action, uint param, ref int value, uint update);

    private static bool AnimationsAllowed()
    {
        if (!OperatingSystem.IsWindows()) return true;
        try
        {
            var enabled = 1;
            return !SystemParametersInfoW(ClientAreaAnimationQuery, 0, ref enabled, 0) || enabled != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
