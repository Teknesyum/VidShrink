global using VidShrink.App.Yazi;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Metadata;

[assembly: XmlnsDefinition("https://github.com/avaloniaui", "VidShrink.App.Yazi")]

namespace VidShrink.App.Yazi;

/// <summary>
/// Doğal genişliğinde yerleşince son sözcüğünü alt satıra atmayan <see cref="TextBlock"/>.
/// Avalonia metni ölçtüğü genişlikte yeniden kurarken sarabiliyor: ölçek 1 değilken yerleşim
/// yuvarlaması ölçünün kayan nokta artığını siliyor (588,0000000000003 → 588), ölçek 1'de tam
/// sayı genişlikte satır kırıcı kendi toplamıyla sınırı aşabiliyor. Kutu bir satırlık
/// yüksekliğe göre kurulduğu için alt satıra düşen sözcük yerleşimden düşüyor, görünmez oluyordu.
/// <para>Avalonia kaydı: https://github.com/AvaloniaUI/Avalonia/issues/22493 (12.1.4 de kusurlu).</para>
/// <para>Düzeltme yalnız yerleşimde: metin ölçtüğü genişlikte yerleşir ve taşıdığı karakter azalırsa iç yerleşim
/// <see cref="Pay"/> kadar geniş kurulur. İstenen boyut değişmez; düzen pimleri kıpırdamaz,
/// ortalanmış metin yalnız kusurlu durumda yarım piksel kayar.</para>
/// </summary>
public class SarilanMetin : TextBlock
{
    internal const double Pay = 1;

    private int _olcuKapsami;
    private double _olcuGenisligi;

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override Size MeasureOverride(Size availableSize)
    {
        var boyut = base.MeasureOverride(availableSize);
        _olcuGenisligi = boyut.Width;
        _olcuKapsami = TextWrapping == TextWrapping.NoWrap ? 0 : Kapsam(TextLayout);
        return boyut;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var sonuc = base.ArrangeOverride(finalSize);
        if (_olcuKapsami == 0 || finalSize.Width + 0.5 < _olcuGenisligi || Kapsam(TextLayout) >= _olcuKapsami) return sonuc;
        base.ArrangeOverride(finalSize.WithWidth(finalSize.Width + Pay));
        return sonuc;
    }

    private static int Kapsam(TextLayout yerlesim) => yerlesim.TextLines.Sum(satir => satir.Length);
}
