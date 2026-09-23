using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace VidShrink.App;

/// <summary>
/// Küçült sayfasının üç sütunu. Eşit başlar; <see cref="Olcut"/> ızgarası dört sütunu kesmeden
/// taşıyamıyorsa sol sütun tam o kadar genişler, ama yalnız öbür iki sütun pencerenin en dar
/// hâlindeki eşit paydan aşağı inmiyorsa: o pay yerleşim denetiminin her dilde sınadığı en dar
/// sütun. 1560 px pencerede bilgi ızgarası iki sütuna düşüp dolu sayfayı uzatıyordu. Karar
/// ölçüm geçişinin içinde verilir; boyut olayından verilince tek geçişli yerleşim onu görmüyordu.
/// </summary>
public sealed class KucultSutunlari : Grid
{
    private double _solKabuk = double.NaN;
    private double _disKabuk = double.NaN;
    private bool _yenidenIstendi;

    protected override Type StyleKeyOverride => typeof(Grid);

    public SutunIzgara? Olcut { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (TopLevel.GetTopLevel(this) is Window { Content: Layoutable { Bounds.Width: > 0 } govde } && Bounds.Width > 0)
            _disKabuk = govde.Bounds.Width - Bounds.Width;
        if (ColumnDefinitions.Count == 3)
        {
            var istenen = Olcut is { } olcut && TopLevel.GetTopLevel(this) is Window pencere
                ? SolGenislik(availableSize.Width, 2 * ColumnSpacing, _solKabuk, olcut.WidthFor(olcut.MaxColumns), pencere.MinWidth - _disKabuk)
                : null;
            var uzunluk = istenen is { } en ? new GridLength(en) : new GridLength(1, GridUnitType.Star);
            if (ColumnDefinitions[0].Width != uzunluk) ColumnDefinitions[0].Width = uzunluk;
        }

        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var boyut = base.ArrangeOverride(finalSize);
        if (Olcut is { Bounds.Width: > 0 } olcut && ColumnDefinitions.Count == 3)
            _solKabuk = ColumnDefinitions[0].ActualWidth - olcut.Bounds.Width;
        if (!_yenidenIstendi && !double.IsNaN(_solKabuk))
        {
            _yenidenIstendi = true;
            Dispatcher.UIThread.Post(InvalidateMeasure);
        }
        return boyut;
    }

    /// <summary>
    /// Sol sütunun piksel genişliği, ya da eşit bölüşüm için <c>null</c>. <paramref name="enDarIzgara"/>
    /// pencere en darken ızgaranın alacağı genişlik; bilinmiyorsa genişletilmez.
    /// </summary>
    internal static double? SolGenislik(double yer, double bosluk, double solKabuk, double izgara, double enDarIzgara)
    {
        if (double.IsInfinity(yer) || double.IsNaN(solKabuk) || double.IsNaN(enDarIzgara) || izgara <= 0) return null;
        var esit = (yer - bosluk) / 3;
        var gereken = Math.Ceiling(izgara + solKabuk);
        if (gereken <= esit) return null;
        var enDar = (enDarIzgara - bosluk) / 3;
        return (yer - bosluk - gereken) / 2 >= enDar ? gereken : null;
    }
}
