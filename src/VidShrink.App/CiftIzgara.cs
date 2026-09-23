using Avalonia;
using Avalonia.Controls;

namespace VidShrink.App;

/// <summary>
/// Çocukları etiket-değer çiftleri olarak dizen panel. Yan yana düzende etiket sütunu en
/// geniş etiket kadardır; değerlerin en genişi kalan yere tek satırda sığmıyorsa bütün
/// çiftler alt alta iner (etiket üstte, değer altında, tam genişlikte). Auto,* ızgarada dar
/// pencerede değer sütunu 111 px'e iniyor, "Dvojprechodový" gibi tek sözcük ortasından
/// bölünüyordu.
/// </summary>
public sealed class CiftIzgara : Panel
{
    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<CiftIzgara, double>(nameof(ColumnSpacing));

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<CiftIzgara, double>(nameof(RowSpacing));

    static CiftIzgara()
    {
        AffectsMeasure<CiftIzgara>(ColumnSpacingProperty, RowSpacingProperty);
    }

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>Son ölçümde çiftler alt alta mı dizildi; testler okur.</summary>
    public bool Stacked { get; private set; }

    private double _etiketGenisligi;

    private List<(Control Etiket, Control? Deger)> Ciftler()
    {
        var gorunen = Children.Where(c => c.IsVisible).ToList();
        var ciftler = new List<(Control, Control?)>();
        for (var i = 0; i < gorunen.Count; i += 2)
            ciftler.Add((gorunen[i], i + 1 < gorunen.Count ? gorunen[i + 1] : null));
        return ciftler;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var ciftler = Ciftler();
        if (ciftler.Count == 0) return default;

        double etiket = 0, deger = 0;
        foreach (var (e, d) in ciftler)
        {
            e.Measure(Size.Infinity);
            etiket = Math.Max(etiket, e.DesiredSize.Width);
            if (d is null) continue;
            d.Measure(Size.Infinity);
            deger = Math.Max(deger, d.DesiredSize.Width);
        }

        var yer = availableSize.Width;
        Stacked = !double.IsInfinity(yer) && etiket + ColumnSpacing + deger > yer;
        _etiketGenisligi = Stacked ? yer : etiket;
        var degerYeri = Stacked ? yer : double.IsInfinity(yer) ? double.PositiveInfinity : Math.Max(0, yer - etiket - ColumnSpacing);

        var yukseklik = 0.0;
        foreach (var (e, d) in ciftler)
        {
            e.Measure(new Size(_etiketGenisligi, double.PositiveInfinity));
            d?.Measure(new Size(degerYeri, double.PositiveInfinity));
            var dh = d?.DesiredSize.Height ?? 0;
            yukseklik += Stacked ? e.DesiredSize.Height + dh : Math.Max(e.DesiredSize.Height, dh);
        }
        yukseklik += (ciftler.Count - 1) * RowSpacing;

        var genislik = double.IsInfinity(yer) ? etiket + ColumnSpacing + deger : yer;
        return new Size(genislik, yukseklik);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        foreach (var (e, d) in Ciftler())
        {
            var eh = e.DesiredSize.Height;
            var dh = d?.DesiredSize.Height ?? 0;
            if (Stacked)
            {
                e.Arrange(new Rect(0, y, finalSize.Width, eh));
                d?.Arrange(new Rect(0, y + eh, finalSize.Width, dh));
                y += eh + dh + RowSpacing;
                continue;
            }

            var satir = Math.Max(eh, dh);
            var x = _etiketGenisligi + ColumnSpacing;
            e.Arrange(new Rect(0, y, _etiketGenisligi, satir));
            d?.Arrange(new Rect(x, y, Math.Max(0, finalSize.Width - x), satir));
            y += satir + RowSpacing;
        }
        return finalSize;
    }
}
