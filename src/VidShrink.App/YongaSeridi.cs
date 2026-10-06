using Avalonia;
using Avalonia.Controls;

namespace VidShrink.App;

/// <summary>
/// Hızlı hedef yongalarının şeridi. Düz <see cref="WrapPanel"/> satırı yonga yazısının genişliğine
/// göre kırıyordu: 1600x1000'de 42 dilin 37'si 5+4, beşi 6+3; 1920x1040'ta 24'ü 7+2, 18'i 6+3.
/// <see cref="SatirBasiProperty"/> taşıyan çocuk, kendinden öncekiler tek satıra sığdıysa yeni
/// satırı başlatır; kırılım dile bağlı kalmaz. Öncekiler sığmayıp şerit zaten sarıldıysa bildirim
/// yok sayılır, yoksa dar pencerede fazladan bir satır açılırdı. Sonraki çocuklar sığdıkça dizilir,
/// sığmayan alt satıra iner.
/// </summary>
public sealed class YongaSeridi : WrapPanel
{
    public static readonly AttachedProperty<bool> SatirBasiProperty =
        AvaloniaProperty.RegisterAttached<YongaSeridi, Control, bool>("SatirBasi");

    public static bool GetSatirBasi(Control yonga) => yonga.GetValue(SatirBasiProperty);

    public static void SetSatirBasi(Control yonga, bool deger) => yonga.SetValue(SatirBasiProperty, deger);

    static YongaSeridi()
    {
        AffectsParentMeasure<YongaSeridi>(SatirBasiProperty);
    }

    /// <summary>Son yerleşimde satır başına görünür çocuk sayısı; testler okur.</summary>
    public IReadOnlyList<int> Satirlar { get; private set; } = [];

    private List<List<Control>> Diz(double genislik)
    {
        var satirlar = new List<List<Control>>();
        var satir = new List<Control>();
        var x = 0.0;
        var sarildi = false;
        foreach (var yonga in Children)
        {
            if (!yonga.IsVisible) continue;
            var en = yonga.DesiredSize.Width;
            var bildirilen = GetSatirBasi(yonga) && !sarildi;
            var sigmiyor = x + en > genislik + 0.01;
            if (satir.Count > 0 && (bildirilen || sigmiyor))
            {
                if (!bildirilen) sarildi = true;
                satirlar.Add(satir);
                satir = new List<Control>();
                x = 0;
            }
            satir.Add(yonga);
            x += en;
        }
        if (satir.Count > 0) satirlar.Add(satir);
        return satirlar;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var yonga in Children) yonga.Measure(availableSize);
        var en = 0.0;
        var boy = 0.0;
        foreach (var satir in Diz(availableSize.Width))
        {
            en = Math.Max(en, satir.Sum(y => y.DesiredSize.Width));
            boy += satir.Max(y => y.DesiredSize.Height);
        }
        return new Size(en, boy);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var satirlar = Diz(finalSize.Width);
        Satirlar = satirlar.Select(s => s.Count).ToList();
        var ust = 0.0;
        foreach (var satir in satirlar)
        {
            var boy = satir.Max(y => y.DesiredSize.Height);
            var x = 0.0;
            foreach (var yonga in satir)
            {
                yonga.Arrange(new Rect(x, ust, yonga.DesiredSize.Width, boy));
                x += yonga.DesiredSize.Width;
            }
            ust += boy;
        }
        return finalSize;
    }
}
