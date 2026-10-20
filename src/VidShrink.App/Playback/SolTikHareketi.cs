using System;

namespace VidShrink.App.Playback;

internal enum SolTikSonucu
{
    Yok,
    Tik,
    CiftTik,
    Surukleme,
    TutmaBitti
}

internal sealed class SolTikHareketi
{
    private readonly ClickArbiter _arbiter;
    private readonly double _tutmaSn;
    private readonly double _ciftSn;
    private double _basisAni;
    private double _basisX;
    private double _basisY;
    private double? _sonTikAni;
    private double _sonTikX;
    private double _sonTikY;
    private bool _ikinci;

    internal SolTikHareketi(ClickArbiter arbiter, double tutmaSn, double ciftSn)
    {
        if (!(tutmaSn > 0)) throw new ArgumentOutOfRangeException(nameof(tutmaSn));
        if (!(ciftSn >= 0)) throw new ArgumentOutOfRangeException(nameof(ciftSn));
        _arbiter = arbiter;
        _tutmaSn = tutmaSn;
        _ciftSn = ciftSn;
    }

    internal bool Tutuyor { get; private set; }

    internal double TutmaSn => _tutmaSn;

    internal double CiftSn => _ciftSn;

    internal void Bas(double x, double y, double an)
    {
        _ikinci = _sonTikAni is { } once
                  && an - once <= _ciftSn
                  && Math.Abs(x - _sonTikX) < ClickArbiter.DragThresholdDip
                  && Math.Abs(y - _sonTikY) < ClickArbiter.DragThresholdDip;
        Tutuyor = false;
        _basisAni = an;
        _basisX = x;
        _basisY = y;
        _arbiter.Press(x, y);
    }

    internal bool Oyna(double x, double y)
    {
        if (Tutuyor) return false;
        return _arbiter.Move(x, y);
    }

    internal double Kalan(double an) => Math.Max(0, _tutmaSn - (an - _basisAni));

    internal bool Zaman(double an)
    {
        if (Tutuyor || !_arbiter.Down || _arbiter.Dragging) return false;
        if (an - _basisAni < _tutmaSn) return false;
        Tutuyor = true;
        _ikinci = false;
        _sonTikAni = null;
        return true;
    }

    internal SolTikSonucu Birak(double an)
    {
        if (Tutuyor)
        {
            Tutuyor = false;
            _arbiter.Cancel();
            return SolTikSonucu.TutmaBitti;
        }

        var gecikti = _arbiter.Down && !_arbiter.Dragging && an - _basisAni >= _tutmaSn;
        var sonuc = _arbiter.Release();
        if (sonuc == ReleaseOutcome.None) return SolTikSonucu.Yok;
        if (sonuc == ReleaseOutcome.Drag || gecikti)
        {
            _ikinci = false;
            _sonTikAni = null;
            return sonuc == ReleaseOutcome.Drag ? SolTikSonucu.Surukleme : SolTikSonucu.TutmaBitti;
        }

        if (_ikinci)
        {
            _ikinci = false;
            _sonTikAni = null;
            return SolTikSonucu.CiftTik;
        }

        _sonTikAni = _basisAni;
        _sonTikX = _basisX;
        _sonTikY = _basisY;
        return SolTikSonucu.Tik;
    }

    internal bool Iptal()
    {
        var tutuyordu = Tutuyor;
        Tutuyor = false;
        _ikinci = false;
        _sonTikAni = null;
        _arbiter.Cancel();
        return tutuyordu;
    }
}
