using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace VidShrink.App.Playback;

/// <summary>
/// Panelin alt bölgesindeki fare durumunu ve şeridin görünürlüğünü tutar.
///
/// Bölge yüksekliği piksel değil oran: <see cref="Share"/> panelin yüksekliğiyle çarpılır,
/// böylece panel terfi edip program boyuna çıktığında bölge de büyür.
///
/// Kaybolma gecikmesi bir kuşak sayacıyla korunuyor. Zamanlayıcı tik attığında kendi
/// kuşağını taşıyıp taşımadığına bakıyor; araya yeni bir gösterme girdiyse eski tik
/// kararını uygulamıyor. Opaklık okuyup karar veren kalıp bu yarışı kaybediyordu.
/// </summary>
internal sealed class HoverZone
{
    private const uint ClientAreaAnimationQuery = 0x1042;

    private static bool? _motionReduced;

    private readonly Func<TimeSpan> _hideDelay;
    private readonly Action<bool> _apply;
    private readonly double _share;

    private DispatcherTimer? _timer;
    private int _generation;
    private bool _pointerInside;
    private bool _held;
    private bool _visible;

    internal HoverZone(double share, Func<TimeSpan> hideDelay, Action<bool> apply)
    {
        _share = Math.Clamp(share, 0, 1);
        _hideDelay = hideDelay;
        _apply = apply;
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
        if (_pointerInside || _held) Show();
        else ScheduleHide();
    }

    private void Show()
    {
        _generation++;
        _timer?.Stop();
        _timer = null;
        if (_visible) return;
        _visible = true;
        _apply(true);
    }

    private void ScheduleHide()
    {
        if (!_visible) return;

        var mine = ++_generation;
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = _hideDelay() };
        _timer.Tick += (_, _) =>
        {
            _timer?.Stop();
            _timer = null;
            if (mine != _generation) return;
            if (_pointerInside || _held) return;
            _visible = false;
            _apply(false);
        };
        _timer.Start();
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
