using Avalonia;
using Avalonia.Controls;

namespace VidShrink.App;

/// <summary>
/// Kartları sütunlara dizen duvar. Sütun sayısı genişlikten seçilir: her sütun en az
/// <see cref="MinColumnWidth"/>, en çok <see cref="MaxColumns"/> sütun. Kartlar sırayla en kısa
/// sütunun altına girer, genişlikleri sütunun tamamı. Ayarlar tek sütunlu yığındayken 1850 px
/// genişlikte 4926 px boyundaydı, görünen alan 988 px; kartların çoğu satırın üçte birini
/// dolduruyor, kalanı boş kalıyordu.
/// <para>Tam sütun sayısında kart <see cref="SutunProperty"/> ile kendi sütununu bildirebilir:
/// açgözlü dizilişte bir kartın yeri kendinden önceki kartların boyuna bağlı, Hakkında açılınca
/// ondan sonraki kartlar başka sütunlara atlıyordu. Bildirilen sütunda açılan kart yalnız kendi
/// sütununu uzatır. Daha az sütunda bildirim yok sayılır, kartlar sırayla en kısa sütuna.</para>
/// </summary>
public sealed class KartDuvari : Panel
{
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<KartDuvari, double>(nameof(MinColumnWidth), 420);

    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<KartDuvari, int>(nameof(MaxColumns), 4);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<KartDuvari, double>(nameof(ColumnSpacing));

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<KartDuvari, double>(nameof(RowSpacing));

    public static readonly AttachedProperty<int> SutunProperty =
        AvaloniaProperty.RegisterAttached<KartDuvari, Control, int>("Sutun", -1);

    public static int GetSutun(Control kart) => kart.GetValue(SutunProperty);

    public static void SetSutun(Control kart, int sutun) => kart.SetValue(SutunProperty, sutun);

    static KartDuvari()
    {
        AffectsMeasure<KartDuvari>(MinColumnWidthProperty, MaxColumnsProperty, ColumnSpacingProperty, RowSpacingProperty);
        AffectsParentMeasure<KartDuvari>(SutunProperty);
    }

    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
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

    private readonly Dictionary<Control, (int Sutun, double Ust)> _yerler = new();

    private double _sutunGenisligi;

    /// <summary><paramref name="genislik"/> için sütun sayısı: en az bir, en çok <see cref="MaxColumns"/>.</summary>
    public int ColumnsFor(double genislik)
    {
        var sigan = (int)Math.Floor((genislik + ColumnSpacing) / (Math.Max(1, MinColumnWidth) + ColumnSpacing));
        return Math.Clamp(sigan, 1, Math.Max(1, MaxColumns));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var gorunen = Children.Where(c => c.IsVisible).ToList();
        var genislik = double.IsInfinity(availableSize.Width)
            ? Math.Max(1, MaxColumns) * MinColumnWidth + (Math.Max(1, MaxColumns) - 1) * ColumnSpacing
            : availableSize.Width;
        Columns = Math.Min(ColumnsFor(genislik), Math.Max(1, gorunen.Count));
        _sutunGenisligi = Math.Max(0, (genislik - (Columns - 1) * ColumnSpacing) / Columns);

        _yerler.Clear();
        var boylar = new double[Columns];
        var dolu = new bool[Columns];
        foreach (var kart in gorunen)
        {
            kart.Measure(new Size(_sutunGenisligi, double.PositiveInfinity));
            var sutun = Columns == MaxColumns ? GetSutun(kart) : -1;
            if (sutun < 0 || sutun >= Columns)
            {
                sutun = 0;
                for (var i = 1; i < Columns; i++)
                    if (boylar[i] < boylar[sutun] - 0.5) sutun = i;
            }
            var ust = boylar[sutun] + (dolu[sutun] ? RowSpacing : 0);
            _yerler[kart] = (sutun, ust);
            boylar[sutun] = ust + kart.DesiredSize.Height;
            dolu[sutun] = true;
        }

        foreach (var gizli in Children.Where(c => !c.IsVisible)) gizli.Measure(default);
        return new Size(genislik, boylar.Length == 0 ? 0 : boylar.Max());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var genislik = Math.Max(0, (finalSize.Width - (Columns - 1) * ColumnSpacing) / Columns);
        foreach (var kart in Children)
        {
            if (!kart.IsVisible || !_yerler.TryGetValue(kart, out var yer)) continue;
            kart.Arrange(new Rect(yer.Sutun * (genislik + ColumnSpacing), yer.Ust, genislik, kart.DesiredSize.Height));
        }
        return finalSize;
    }
}
