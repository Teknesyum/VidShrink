using System;
using System.Collections.Generic;

namespace VidShrink.App.Playback;

internal enum DokunmaKipi
{
    Yok,
    Bekliyor,
    Atlama,
    Ses,
    Kistirma,
    Bitti
}

internal readonly record struct DokunmaAdimi(DokunmaKipi Kip, double Miktar);

/// <summary>
/// Dokunmatik parmaklarin hareketini ayirir: tek parmak esik altinda kalirsa dokunus, yatay
/// kayarsa atlama, dikey kayarsa ses; iki parmak kistirma orani verir. Eksen esik gecilince kilitlenir.
/// </summary>
internal sealed class DokunmaHareketi
{
    private readonly double _esik;
    private readonly Dictionary<long, (double X, double Y)> _parmaklar = new();
    private DokunmaKipi _kip;
    private double _basX;
    private double _basY;
    private double _sonX;
    private double _sonY;
    private double _ilkAralik;
    private bool _cokParmak;

    internal DokunmaHareketi(double esik) => _esik = esik;

    internal DokunmaKipi Kip => _kip;

    internal double OrtaX { get; private set; }

    internal double OrtaY { get; private set; }

    internal void Bas(long id, double x, double y)
    {
        _parmaklar[id] = (x, y);
        if (_parmaklar.Count == 1)
        {
            _kip = DokunmaKipi.Bekliyor;
            _cokParmak = false;
            _basX = _sonX = x;
            _basY = _sonY = y;
            return;
        }

        _cokParmak = true;
        if (_parmaklar.Count == 2)
        {
            _kip = DokunmaKipi.Kistirma;
            _ilkAralik = Aralik();
            Ortala();
        }
    }

    internal DokunmaAdimi Oyna(long id, double x, double y)
    {
        if (!_parmaklar.ContainsKey(id)) return default;
        _parmaklar[id] = (x, y);

        switch (_kip)
        {
            case DokunmaKipi.Kistirma:
                if (_parmaklar.Count < 2 || _ilkAralik <= 0) return default;
                Ortala();
                return new DokunmaAdimi(DokunmaKipi.Kistirma, Aralik() / _ilkAralik);
            case DokunmaKipi.Bekliyor:
                var dx = x - _basX;
                var dy = y - _basY;
                if (Math.Abs(dx) < _esik && Math.Abs(dy) < _esik) return default;
                _kip = Math.Abs(dx) >= Math.Abs(dy) ? DokunmaKipi.Atlama : DokunmaKipi.Ses;
                _sonX = _basX;
                _sonY = _basY;
                return Adim(x, y);
            case DokunmaKipi.Atlama:
            case DokunmaKipi.Ses:
                return Adim(x, y);
            default:
                return default;
        }
    }

    internal bool Birak(long id)
    {
        if (!_parmaklar.Remove(id)) return false;
        var dokunus = _kip == DokunmaKipi.Bekliyor && !_cokParmak && _parmaklar.Count == 0;
        if (_parmaklar.Count == 0) _kip = DokunmaKipi.Yok;
        else if (_kip == DokunmaKipi.Kistirma) _kip = DokunmaKipi.Bitti;
        return dokunus;
    }

    private DokunmaAdimi Adim(double x, double y)
    {
        var adim = _kip == DokunmaKipi.Atlama
            ? new DokunmaAdimi(DokunmaKipi.Atlama, x - _sonX)
            : new DokunmaAdimi(DokunmaKipi.Ses, _sonY - y);
        _sonX = x;
        _sonY = y;
        return adim;
    }

    private double Aralik()
    {
        var e = _parmaklar.Values.GetEnumerator();
        e.MoveNext();
        var a = e.Current;
        e.MoveNext();
        var b = e.Current;
        return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }

    private void Ortala()
    {
        double x = 0, y = 0;
        var n = 0;
        foreach (var p in _parmaklar.Values)
        {
            if (n == 2) break;
            x += p.X;
            y += p.Y;
            n++;
        }

        OrtaX = x / n;
        OrtaY = y / n;
    }
}
