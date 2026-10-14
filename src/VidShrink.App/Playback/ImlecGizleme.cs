using System;

namespace VidShrink.App.Playback;

/// <summary>
/// OYA-08: fare durunca görüntünün üstündeki imleci saklar. Her hareket imleci geri getirir ve saati
/// yeniden kurar; saat dolduğunda oynatma sürüyorsa ve <c>izin</c> veriyorsa (fare şeritte değil, menü
/// kapalı) imleç gizlenir. Duraklayınca imleç döner ve saat durur.
/// </summary>
internal sealed class ImlecGizleme
{
    private readonly Func<TimeSpan> _gecikme;
    private readonly Func<bool> _izin;
    private readonly Action<bool> _uygula;
    private bool _oynuyor;

    internal ImlecGizleme(Func<TimeSpan> gecikme, Func<bool> izin, Action<bool> uygula)
    {
        _gecikme = gecikme;
        _izin = izin;
        _uygula = uygula;
    }

    internal IHoverClock Saat { get; set; } = new DispatcherHoverClock();

    internal bool Gizli { get; private set; }

    internal void Hareket()
    {
        Goster();
        if (_oynuyor) Saat.Start(_gecikme(), Doldu);
    }

    internal void Oynatma(bool oynuyor)
    {
        if (_oynuyor == oynuyor) return;
        _oynuyor = oynuyor;
        if (oynuyor)
        {
            Saat.Start(_gecikme(), Doldu);
            return;
        }

        Saat.Stop();
        Goster();
    }

    private void Doldu()
    {
        if (!_oynuyor || Gizli || !_izin()) return;
        Gizli = true;
        _uygula(true);
    }

    private void Goster()
    {
        if (!Gizli) return;
        Gizli = false;
        _uygula(false);
    }
}
