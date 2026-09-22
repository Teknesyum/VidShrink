using Avalonia;
using Avalonia.Controls;

namespace VidShrink.App;

/// <summary>
/// Hücreleri eşit genişlikte sütunlara dizen, sütun sayısını içerikten seçen ızgara.
/// Bilgi ızgarası sabit dört sütundayken değerler her dilde üç noktayla kesiliyordu
/// (dar pencerede sütun 66 px, "00:03:07" 70 px; varsayılan boyda sütun 102 px,
/// "aac 192 kbit/sn" 132 px). Burada en geniş hücrenin doğal genişliği sığana dek sütun
/// sayısı <see cref="MaxColumns"/>'tan aşağı iner; yalnız hücre sayısını tam bölen
/// sayılar denenir ki son satır yarım kalmasın. Sayı yazılmıyor, ölçü içerikten geliyor.
/// </summary>
public sealed class EsitSutunIzgara : Panel
{
    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<EsitSutunIzgara, int>(nameof(MaxColumns), 4);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<EsitSutunIzgara, double>(nameof(ColumnSpacing));

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<EsitSutunIzgara, double>(nameof(RowSpacing));

    static EsitSutunIzgara()
    {
        AffectsMeasure<EsitSutunIzgara>(MaxColumnsProperty, ColumnSpacingProperty, RowSpacingProperty);
    }

    public int MaxColumns
    {
        get => GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
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

    /// <summary>Son ölçümde seçilen sütun sayısı; testler okur.</summary>
    public int Columns { get; private set; } = 1;

    private List<Control> Gorunen() => Children.Where(c => c.IsVisible).ToList();

    private int SutunSec(int adet, double genislik, double gereken)
    {
        var ust = Math.Max(1, Math.Min(MaxColumns, adet));
        if (double.IsInfinity(genislik)) return ust;
        for (var c = ust; c > 1; c--)
        {
            if (adet % c != 0) continue;
            if (HucreGenisligi(genislik, c) >= gereken) return c;
        }
        return 1;
    }

    private double HucreGenisligi(double genislik, int sutun) =>
        Math.Max(0, (genislik - (sutun - 1) * ColumnSpacing) / sutun);

    protected override Size MeasureOverride(Size availableSize)
    {
        var hucreler = Gorunen();
        if (hucreler.Count == 0) return default;

        var gereken = 0.0;
        foreach (var hucre in hucreler)
        {
            hucre.Measure(Size.Infinity);
            gereken = Math.Max(gereken, hucre.DesiredSize.Width);
        }

        Columns = SutunSec(hucreler.Count, availableSize.Width, gereken);
        var hucreGenisligi = double.IsInfinity(availableSize.Width)
            ? gereken
            : HucreGenisligi(availableSize.Width, Columns);

        var yukseklik = 0.0;
        var satirlar = (hucreler.Count + Columns - 1) / Columns;
        for (var satir = 0; satir < satirlar; satir++)
        {
            var satirYuksekligi = 0.0;
            for (var sutun = 0; sutun < Columns && satir * Columns + sutun < hucreler.Count; sutun++)
            {
                var hucre = hucreler[satir * Columns + sutun];
                hucre.Measure(new Size(hucreGenisligi, double.PositiveInfinity));
                satirYuksekligi = Math.Max(satirYuksekligi, hucre.DesiredSize.Height);
            }
            yukseklik += satirYuksekligi;
        }
        yukseklik += (satirlar - 1) * RowSpacing;

        var genislik = double.IsInfinity(availableSize.Width)
            ? Columns * gereken + (Columns - 1) * ColumnSpacing
            : availableSize.Width;
        return new Size(genislik, yukseklik);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var hucreler = Gorunen();
        if (hucreler.Count == 0) return finalSize;

        var hucreGenisligi = HucreGenisligi(finalSize.Width, Columns);
        var y = 0.0;
        for (var ilk = 0; ilk < hucreler.Count; ilk += Columns)
        {
            var satir = hucreler.Skip(ilk).Take(Columns).ToList();
            var satirYuksekligi = satir.Max(h => h.DesiredSize.Height);
            for (var sutun = 0; sutun < satir.Count; sutun++)
            {
                var x = sutun * (hucreGenisligi + ColumnSpacing);
                satir[sutun].Arrange(new Rect(x, y, hucreGenisligi, satirYuksekligi));
            }
            y += satirYuksekligi + RowSpacing;
        }
        return finalSize;
    }
}
