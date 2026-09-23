using Avalonia;
using Avalonia.Controls;

namespace VidShrink.App;

/// <summary>
/// Hücreleri sütunlara dizen, sütun sayısını içerikten seçen ızgara. Bilgi ızgarası sabit
/// dört sütundayken değerler her dilde üç noktayla kesiliyordu (dar pencerede sütun 66 px,
/// "00:03:07" 70 px). Sütun sayısı <see cref="MaxColumns"/>'tan aşağı iner; yalnız hücre
/// sayısını tam bölen sayılar denenir ki son satır yarım kalmasın. Her sütun kendi en
/// geniş hücresi kadar yer ister, artan genişlik sütunlara eşit dağılır: eşit sütunda en
/// geniş tek hücre (124 px) dördüne de dayatılıyor, 1560 px pencerede 442 px'lik ızgara
/// iki sütuna inip dolu sayfayı 90 px uzatıyordu; sütun başına ölçüde dört sütun 428 px.
/// </summary>
public sealed class SutunIzgara : Panel
{
    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<SutunIzgara, int>(nameof(MaxColumns), 4);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<SutunIzgara, double>(nameof(ColumnSpacing));

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<SutunIzgara, double>(nameof(RowSpacing));

    static SutunIzgara()
    {
        AffectsMeasure<SutunIzgara>(MaxColumnsProperty, ColumnSpacingProperty, RowSpacingProperty);
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

    private double[] _genislikler = [];

    /// <summary>
    /// <paramref name="sutun"/> sütunun kesmeden sığdığı en dar genişlik: sütun başına en geniş
    /// hücre artı aralıklar. Hücreler burada ölçülür, önceki geçişi beklemez; hücre sayısını
    /// bölmeyen sayı ya da boş ızgara için 0.
    /// </summary>
    public double WidthFor(int sutun)
    {
        var dogal = DogalGenislikler(Gorunen());
        if (sutun < 1 || dogal.Count == 0 || dogal.Count % sutun != 0) return 0;
        return SutunGenislikleri(dogal, sutun).Sum() + (sutun - 1) * ColumnSpacing;
    }

    private static List<double> DogalGenislikler(IReadOnlyList<Control> hucreler)
    {
        var dogal = new List<double>(hucreler.Count);
        foreach (var hucre in hucreler)
        {
            hucre.Measure(Size.Infinity);
            dogal.Add(hucre.DesiredSize.Width);
        }

        return dogal;
    }

    private List<Control> Gorunen() => Children.Where(c => c.IsVisible).ToList();

    private static double[] SutunGenislikleri(IReadOnlyList<double> dogal, int sutun)
    {
        var genislik = new double[sutun];
        for (var i = 0; i < dogal.Count; i++) genislik[i % sutun] = Math.Max(genislik[i % sutun], dogal[i]);
        return genislik;
    }

    private (int Sutun, double[] Genislik) SutunSec(IReadOnlyList<double> dogal, double yer)
    {
        var ust = Math.Max(1, Math.Min(MaxColumns, dogal.Count));
        for (var c = ust; c > 1; c--)
        {
            if (dogal.Count % c != 0) continue;
            var genislik = SutunGenislikleri(dogal, c);
            var toplam = genislik.Sum() + (c - 1) * ColumnSpacing;
            if (double.IsInfinity(yer)) return (c, genislik);
            if (toplam > yer) continue;
            var pay = (yer - toplam) / c;
            return (c, genislik.Select(g => g + pay).ToArray());
        }
        return (1, [double.IsInfinity(yer) ? dogal.Max() : Math.Max(0, yer)]);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var hucreler = Gorunen();
        if (hucreler.Count == 0) return default;

        var dogal = DogalGenislikler(hucreler);
        (Columns, _genislikler) = SutunSec(dogal, availableSize.Width);

        var yukseklik = 0.0;
        var satirlar = (hucreler.Count + Columns - 1) / Columns;
        for (var satir = 0; satir < satirlar; satir++)
        {
            var satirYuksekligi = 0.0;
            for (var sutun = 0; sutun < Columns && satir * Columns + sutun < hucreler.Count; sutun++)
            {
                var hucre = hucreler[satir * Columns + sutun];
                hucre.Measure(new Size(_genislikler[sutun], double.PositiveInfinity));
                satirYuksekligi = Math.Max(satirYuksekligi, hucre.DesiredSize.Height);
            }
            yukseklik += satirYuksekligi;
        }
        yukseklik += (satirlar - 1) * RowSpacing;

        var genislik = double.IsInfinity(availableSize.Width)
            ? _genislikler.Sum() + (Columns - 1) * ColumnSpacing
            : availableSize.Width;
        return new Size(genislik, yukseklik);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var hucreler = Gorunen();
        if (hucreler.Count == 0) return finalSize;

        var toplam = _genislikler.Sum() + (Columns - 1) * ColumnSpacing;
        var pay = _genislikler.Length == Columns ? (finalSize.Width - toplam) / Columns : 0;
        var y = 0.0;
        for (var ilk = 0; ilk < hucreler.Count; ilk += Columns)
        {
            var satir = hucreler.Skip(ilk).Take(Columns).ToList();
            var satirYuksekligi = satir.Max(h => h.DesiredSize.Height);
            var x = 0.0;
            for (var sutun = 0; sutun < satir.Count; sutun++)
            {
                var en = Math.Max(0, _genislikler[sutun] + pay);
                satir[sutun].Arrange(new Rect(x, y, en, satirYuksekligi));
                x += en + ColumnSpacing;
            }
            y += satirYuksekligi + RowSpacing;
        }
        return finalSize;
    }
}
