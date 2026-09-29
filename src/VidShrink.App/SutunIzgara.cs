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

    public static readonly StyledProperty<bool> UniformProperty =
        AvaloniaProperty.Register<SutunIzgara, bool>(nameof(Uniform));

    public static readonly StyledProperty<bool> FillProperty =
        AvaloniaProperty.Register<SutunIzgara, bool>(nameof(Fill), true);

    static SutunIzgara()
    {
        AffectsMeasure<SutunIzgara>(MaxColumnsProperty, ColumnSpacingProperty, RowSpacingProperty, FillProperty, UniformProperty);
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

    /// <summary>
    /// Artan genişlik sütunlara dağıtılsın mı. Kapalıyken ızgara doğal genişliğinde kalır:
    /// kaydedici şeridinde üç sayaç 1920 px pencerede bin piksele yayılıp okunmuyordu.
    /// Sütun sayısı yine sığana dek iner, dar pencerede etiket yine sarar.
    /// </summary>
    public bool Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>
    /// Bütün sütunlar en geniş hücre kadar. Aynı kartta alt alta duran iki ızgaranın
    /// sütunları hizalı kalsın diye: sütun başına ölçüde üst ızgaranın ikinci sütunu
    /// alttakinden kayıyordu.
    /// <para>Eşit ızgara form içindir, üç kuralı daha var. Sarılan yazının en dar hali en uzun
    /// sözcüğüdür: kaydedicinin kodlama alanları "Anahtar kare aralığı (sn)" etiketi yüzünden
    /// 411 px'lik kartta tek sütuna düşüp on iki satır oluyordu. Son satır yarım kalabilir:
    /// gizlenen bir alan on iki hücreyi on bire indirip ızgarayı tek sütuna çekiyordu. Hücreler
    /// satırın altına yaslanır ki iki satıra sarılan etiketin yanındaki kutu yukarı kaçmasın.</para>
    /// </summary>
    public bool Uniform
    {
        get => GetValue(UniformProperty);
        set => SetValue(UniformProperty, value);
    }

    /// <summary>Son ölçümde seçilen sütun sayısı; testler okur.</summary>
    public int Columns { get; private set; } = 1;

    private double[] _genislikler = [];

    private double _tamGenislik;

    /// <summary>
    /// <paramref name="sutun"/> sütunun kesmeden sığdığı en dar genişlik: sütun başına en geniş
    /// hücre artı aralıklar. Hücreler burada ölçülür, önceki geçişi beklemez; hücre sayısını
    /// bölmeyen sayı ya da boş ızgara için 0.
    /// </summary>
    public double WidthFor(int sutun)
    {
        var dogal = DogalGenislikler(Gorunen());
        if (sutun < 1 || dogal.Count == 0 || (!Uniform && dogal.Count % sutun != 0)) return 0;
        return SutunGenislikleri(dogal, sutun).Sum() + (sutun - 1) * ColumnSpacing;
    }

    private List<double> DogalGenislikler(IReadOnlyList<Control> hucreler)
    {
        var dogal = new List<double>(hucreler.Count);
        _tamGenislik = 0;
        foreach (var hucre in hucreler)
        {
            hucre.Measure(Size.Infinity);
            _tamGenislik = Math.Max(_tamGenislik, hucre.DesiredSize.Width);
            dogal.Add(Uniform ? EnDar(hucre) : hucre.DesiredSize.Width);
        }

        return dogal;
    }

    private static double EnDar(Control hucre)
    {
        var kenar = hucre.Margin.Left + hucre.Margin.Right;
        switch (hucre)
        {
            case TextBlock { TextWrapping: not Avalonia.Media.TextWrapping.NoWrap, Text: { Length: > 0 } metin } yazi:
                var sozcuk = 0.0;
                foreach (var parca in metin.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    var olcu = new TextBlock
                    {
                        Text = parca,
                        FontFamily = yazi.FontFamily,
                        FontSize = yazi.FontSize,
                        FontWeight = yazi.FontWeight,
                        FontStyle = yazi.FontStyle,
                        FontStretch = yazi.FontStretch,
                        LetterSpacing = yazi.LetterSpacing
                    };
                    olcu.Measure(Size.Infinity);
                    sozcuk = Math.Max(sozcuk, olcu.DesiredSize.Width);
                }
                return Math.Min(hucre.DesiredSize.Width, sozcuk + yazi.Padding.Left + yazi.Padding.Right + kenar);
            case StackPanel { Orientation: Avalonia.Layout.Orientation.Vertical } yigin:
                var en = 0.0;
                foreach (var cocuk in yigin.Children)
                    if (cocuk.IsVisible) en = Math.Max(en, EnDar(cocuk));
                return Math.Min(hucre.DesiredSize.Width, en + kenar);
            default:
                return hucre.DesiredSize.Width;
        }
    }

    private List<Control> Gorunen() => Children.Where(c => c.IsVisible).ToList();

    private double[] SutunGenislikleri(IReadOnlyList<double> dogal, int sutun)
    {
        var genislik = new double[sutun];
        if (Uniform)
        {
            Array.Fill(genislik, dogal.Max());
            return genislik;
        }
        for (var i = 0; i < dogal.Count; i++) genislik[i % sutun] = Math.Max(genislik[i % sutun], dogal[i]);
        return genislik;
    }

    private (int Sutun, double[] Genislik) SutunSec(IReadOnlyList<double> dogal, double yer)
    {
        var ust = Math.Max(1, Math.Min(MaxColumns, dogal.Count));
        for (var c = ust; c > 1; c--)
        {
            if (!Uniform && dogal.Count % c != 0) continue;
            var genislik = SutunGenislikleri(dogal, c);
            var toplam = genislik.Sum() + (c - 1) * ColumnSpacing;
            if (double.IsInfinity(yer)) return (c, Uniform ? Enumerable.Repeat(_tamGenislik, c).ToArray() : genislik);
            if (toplam > yer) continue;
            if (!Fill) return (c, genislik);
            var pay = (yer - toplam) / c;
            return (c, genislik.Select(g => g + pay).ToArray());
        }
        return (1, [double.IsInfinity(yer) ? _tamGenislik : Math.Max(0, yer)]);
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

        var genislik = double.IsInfinity(availableSize.Width) || !Fill
            ? Math.Min(_genislikler.Sum() + (Columns - 1) * ColumnSpacing, availableSize.Width)
            : availableSize.Width;
        return new Size(genislik, yukseklik);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var hucreler = Gorunen();
        if (hucreler.Count == 0) return finalSize;

        var toplam = _genislikler.Sum() + (Columns - 1) * ColumnSpacing;
        var pay = Fill && _genislikler.Length == Columns ? (finalSize.Width - toplam) / Columns : 0;
        var y = 0.0;
        for (var ilk = 0; ilk < hucreler.Count; ilk += Columns)
        {
            var satir = hucreler.Skip(ilk).Take(Columns).ToList();
            var satirYuksekligi = satir.Max(h => h.DesiredSize.Height);
            var x = 0.0;
            for (var sutun = 0; sutun < satir.Count; sutun++)
            {
                var en = Math.Max(0, _genislikler[sutun] + pay);
                var boy = Uniform ? satir[sutun].DesiredSize.Height : satirYuksekligi;
                satir[sutun].Arrange(new Rect(x, y + satirYuksekligi - boy, en, boy));
                x += en + ColumnSpacing;
            }
            y += satirYuksekligi + RowSpacing;
        }
        return finalSize;
    }
}
