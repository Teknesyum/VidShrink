using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;
using VidShrink.Core;

namespace VidShrink.App.Playback;

/// <summary>
/// SIK-P8: önizleme karesinin üstünde elle kırpma dikdörtgeni. Dikdörtgen kareye oran olarak
/// tutulur (<see cref="CropFraction"/>), ekranda yakınlaştırma jestinin içerik dikdörtgenine
/// eşlenir. Köşe tutamacı karşı köşeyi çıpa yapar, gövde taşır, dışarıda basmak yeni dikdörtgen
/// başlatır. Renk ve ölçü temadan okunur; bırakınca <see cref="Degisti"/> bir kez çalar.
/// </summary>
internal sealed class KirpmaKatmani : Control, ICustomHitTest
{
    private enum Kip { Yok, Boyut, Tasi }

    private Kip _kip;
    private (double X, double Y) _cipa;
    private (double X, double Y) _tasimaBasi;
    private CropFraction _tasimaOrani;
    private CropFraction _basinca;

    internal ZoomGesture? Gesture { get; set; }

    internal CropFraction Oran { get; private set; } = ManualCrop.Full;

    internal event Action? Degisti;

    internal void Ayarla(CropFraction oran)
    {
        Oran = oran;
        InvalidateVisual();
    }

    public bool HitTest(Point point) => IsVisible && IcerikVar(out _);

    private bool IcerikVar(out Rect icerik)
    {
        icerik = Gesture is { ContentWidth: > 0, ContentHeight: > 0 } g
            ? new Rect(g.OffsetX, g.OffsetY, g.ContentWidth, g.ContentHeight)
            : default;
        return icerik.Width > 0 && icerik.Height > 0;
    }

    private static Rect Ekranda(Rect icerik, CropFraction f)
        => new(icerik.X + f.Left * icerik.Width, icerik.Y + f.Top * icerik.Height,
            f.Width * icerik.Width, f.Height * icerik.Height);

    public override void Render(DrawingContext context)
    {
        if (!IcerikVar(out var icerik)) return;
        var kutu = Ekranda(icerik, Oran);
        var gorunen = icerik.Intersect(new Rect(Bounds.Size));
        if (Kaynak("PlaybackScrim") is IBrush perde)
        {
            context.FillRectangle(perde, new Rect(gorunen.Left, gorunen.Top, gorunen.Width, Math.Max(0, kutu.Top - gorunen.Top)));
            context.FillRectangle(perde, new Rect(gorunen.Left, kutu.Bottom, gorunen.Width, Math.Max(0, gorunen.Bottom - kutu.Bottom)));
            context.FillRectangle(perde, new Rect(gorunen.Left, kutu.Top, Math.Max(0, kutu.Left - gorunen.Left), kutu.Height));
            context.FillRectangle(perde, new Rect(kutu.Right, kutu.Top, Math.Max(0, gorunen.Right - kutu.Right), kutu.Height));
        }
        if (Kaynak("NeonBlue") is not IBrush kenar) return;
        context.DrawRectangle(null, new Pen(kenar, Olcu("BorderThinScalar")), kutu);
        var yan = Olcu("PlaybackButtonIconSize");
        foreach (var kose in Koseler(kutu))
            context.FillRectangle(kenar, new Rect(kose.X - yan / 2, kose.Y - yan / 2, yan, yan));
    }

    private static Point[] Koseler(Rect r) => new[] { r.TopLeft, r.TopRight, r.BottomLeft, r.BottomRight };

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || !IcerikVar(out var icerik)) return;
        var nokta = e.GetPosition(this);
        var kutu = Ekranda(icerik, Oran);
        var yaricap = Olcu("PlaybackHandleWidth") / 2;
        var koseler = Koseler(kutu);
        var karsi = new[] { kutu.BottomRight, kutu.BottomLeft, kutu.TopRight, kutu.TopLeft };
        _basinca = Oran;
        _kip = Kip.Boyut;
        _cipa = OranaCevir(nokta, icerik);
        for (var i = 0; i < koseler.Length; i++)
        {
            if (Math.Abs(nokta.X - koseler[i].X) > yaricap || Math.Abs(nokta.Y - koseler[i].Y) > yaricap) continue;
            _cipa = OranaCevir(karsi[i], icerik);
            break;
        }
        if (_cipa == OranaCevir(nokta, icerik) && kutu.Contains(nokta))
        {
            _kip = Kip.Tasi;
            _tasimaBasi = _cipa;
            _tasimaOrani = Oran;
        }
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_kip == Kip.Yok || !IcerikVar(out var icerik)) return;
        var simdi = OranaCevir(e.GetPosition(this), icerik);
        Oran = _kip == Kip.Tasi
            ? ManualCrop.Move(_tasimaOrani, simdi.X - _tasimaBasi.X, simdi.Y - _tasimaBasi.Y)
            : ManualCrop.FromPoints(_cipa.X, _cipa.Y, simdi.X, simdi.Y);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_kip == Kip.Yok) return;
        _kip = Kip.Yok;
        e.Pointer.Capture(null);
        e.Handled = true;
        if (Oran.Width <= 0 || Oran.Height <= 0) Oran = _basinca;
        InvalidateVisual();
        if (Oran != _basinca) Degisti?.Invoke();
    }

    private static (double X, double Y) OranaCevir(Point p, Rect icerik)
        => ManualCrop.ToFraction(p.X, p.Y, icerik.X, icerik.Y, icerik.Width, icerik.Height);

    private object? Kaynak(string anahtar) => this.TryFindResource(anahtar, out var deger) ? deger : null;

    private double Olcu(string anahtar) => Kaynak(anahtar) is double d ? d : 0;
}
