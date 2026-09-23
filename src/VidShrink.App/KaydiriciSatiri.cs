using System;
using Avalonia;
using Avalonia.Controls;

namespace VidShrink.App;

/// <summary>
/// Kaydırıcı ile ardındaki sayı kutusu ve birimi taşıyan satır. İlk çocuk kaydırıcıdır, öbürleri
/// sırayla sonraki sütunlara oturur. Kaydırıcıya <see cref="EnDarKaydirici"/> kadar yer
/// kalmıyorsa kaydırıcı bütün sütunlara yayılır, kutu ve birim bir alt satıra iner: 1136 px
/// pencerede hedef kaydırıcısı 110 px'e düşüp 1-500 MB aralığında kullanılamıyordu.
/// </summary>
public sealed class KaydiriciSatiri : Grid
{
    public static readonly StyledProperty<double> EnDarKaydiriciProperty =
        AvaloniaProperty.Register<KaydiriciSatiri, double>(nameof(EnDarKaydirici));

    protected override Type StyleKeyOverride => typeof(Grid);

    public double EnDarKaydirici
    {
        get => GetValue(EnDarKaydiriciProperty);
        set => SetValue(EnDarKaydiriciProperty, value);
    }

    /// <summary>Kutu ve birim kaydırıcının altında mı.</summary>
    public bool Dar { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count > 1)
        {
            var kuyruk = 0.0;
            for (var i = 1; i < Children.Count; i++)
            {
                var cocuk = Children[i];
                if (!cocuk.IsVisible) continue;
                cocuk.Measure(Size.Infinity);
                kuyruk += cocuk.DesiredSize.Width + ColumnSpacing;
            }
            Uygula(DarGerekir(availableSize.Width, kuyruk, EnDarKaydirici));
        }
        return base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// <paramref name="yer"/> genişliğinden kutu, birim ve aralıkları (<paramref name="kuyruk"/>)
    /// düşünce kaydırıcıya <paramref name="enDar"/> kalmıyorsa dar düzen.
    /// </summary>
    internal static bool DarGerekir(double yer, double kuyruk, double enDar)
        => !double.IsInfinity(yer) && !double.IsNaN(yer) && yer - kuyruk < enDar;

    private void Uygula(bool dar)
    {
        Dar = dar;
        var satirSayisi = dar ? 2 : 1;
        if (RowDefinitions.Count != satirSayisi)
        {
            RowDefinitions.Clear();
            for (var i = 0; i < satirSayisi; i++) RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        var yayilim = dar ? Math.Max(1, ColumnDefinitions.Count) : 1;
        if (GetColumnSpan(Children[0]) != yayilim) SetColumnSpan(Children[0], yayilim);
        if (GetRow(Children[0]) != 0) SetRow(Children[0], 0);
        for (var i = 1; i < Children.Count; i++)
        {
            var satir = dar ? 1 : 0;
            if (GetRow(Children[i]) != satir) SetRow(Children[i], satir);
            if (GetColumn(Children[i]) != i) SetColumn(Children[i], i);
        }
    }
}
