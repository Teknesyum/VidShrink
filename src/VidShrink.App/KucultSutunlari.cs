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
/// <para>Orta ve sağ sütun sayfa kaydırıcısında yapışkandır ve satırın boyuna gerilmez: sol
/// sütun bölümler açılınca uzadığında önizleme görünüm yüksekliğinde kalır, yer tutucusu
/// ilk ekranda görünür, sağ sütun kendi boyunda biter.</para>
/// </summary>
public sealed class KucultSutunlari : Grid
{
    private double _solKabuk = double.NaN;
    private double _disKabuk = double.NaN;
    private bool _yenidenIstendi;
    private ScrollViewer? _kaydirici;

    protected override Type StyleKeyOverride => typeof(Grid);

    public SutunIzgara? Olcut { get; set; }

    /// <summary>Sütunların alt alta dizili olup olmadığı (<c>CompactBreakpointWidth</c> altında).</summary>
    internal bool Yigili => _sutunlar is not null;

    private int[]? _sutunlar;
    private double _ortaMin;

    /// <summary>
    /// Küçük kipte (çalışma alanının üçte biri) üç sütun 200 px'in altına iniyor, başlıklar sözcük
    /// ortasından bölünüyordu. Yer <c>CompactBreakpointWidth</c>'in altındaysa sütunlar sırasıyla
    /// alt alta dizilir; orta sütun (önizleme ve plan) görünür yüksekliği en az boy olarak alır.
    /// </summary>
    private void Diz(double yer)
    {
        var esik = this.TryFindResource("CompactBreakpointWidth", out var deger) && deger is double d ? d : 0;
        var dar = !double.IsInfinity(yer) && yer < esik;
        if (dar == Yigili) return;

        if (dar)
        {
            _sutunlar = Children.Select(GetColumn).ToArray();
            ColumnDefinitions = new ColumnDefinitions("*");
            RowDefinitions = new RowDefinitions(string.Join(",", Children.Select(_ => "Auto")));
            RowSpacing = ColumnSpacing;
            for (var i = 0; i < Children.Count; i++)
            {
                SetRow(Children[i], _sutunlar[i]);
                SetColumn(Children[i], 0);
            }
            if (Orta is { } orta) _ortaMin = orta.MinHeight;
            return;
        }

        var eski = _sutunlar!;
        _sutunlar = null;
        RowDefinitions = new RowDefinitions();
        ColumnDefinitions = new ColumnDefinitions("*,*,*");
        for (var i = 0; i < Children.Count && i < eski.Length; i++)
        {
            SetRow(Children[i], 0);
            SetColumn(Children[i], eski[i]);
        }
        if (Orta is { } ortaSutun) ortaSutun.MinHeight = _ortaMin;
    }

    private Control? Orta => _sutunlar is { } s ? Children.Where((_, i) => i < s.Length && s[i] == 1).FirstOrDefault()
        : Children.FirstOrDefault(c => GetColumn(c) == 1);

    protected override Size MeasureOverride(Size availableSize)
    {
        Diz(availableSize.Width);
        if (Yigili && Orta is { } orta && _kaydirici is { Viewport.Height: > 0 } kaydirici)
            orta.MinHeight = Math.Max(_ortaMin, kaydirici.Viewport.Height - Margin.Top - Margin.Bottom);
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _kaydirici = this.FindAncestorOfType<ScrollViewer>();
        if (_kaydirici is not null) _kaydirici.PropertyChanged += KaydiriciDegisti;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_kaydirici is not null) _kaydirici.PropertyChanged -= KaydiriciDegisti;
        _kaydirici = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void KaydiriciDegisti(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.OffsetProperty || e.Property == ScrollViewer.ViewportProperty)
            InvalidateArrange();
        if (Yigili && e.Property == ScrollViewer.ViewportProperty) InvalidateMeasure();
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
        if (ColumnDefinitions.Count == 3 && _kaydirici is { Viewport.Height: > 0 } kaydirici)
        {
            var gorunen = kaydirici.Viewport.Height - Margin.Top - Margin.Bottom;
            var kayma = kaydirici.Offset.Y;
            foreach (var cocuk in Children)
            {
                var sutun = GetColumn(cocuk);
                if (sutun == 0 || sutun > 2) continue;
                var kenar = cocuk.Margin;
                var yer = cocuk.Bounds;
                var istenen = cocuk.DesiredSize.Height;
                var boy = sutun == 1 ? Math.Min(finalSize.Height, Math.Max(istenen, gorunen)) : Math.Min(finalSize.Height, istenen);
                var y = YapiskanUst(boy, finalSize.Height, gorunen, kayma);
                cocuk.Arrange(new Rect(yer.X - kenar.Left, y, yer.Width + kenar.Left + kenar.Right, boy));
            }
        }
        return boyut;
    }

    /// <summary>
    /// Yapışkan sütunun üst kenarı. Görünüme sığan sütun kaydırılan sayfanın tepesinde durur;
    /// sığmayan sütun önce kendi tepesini, sonra dibini gösterir. İki durumda da sütun satırın
    /// dışına çıkmaz.
    /// </summary>
    internal static double YapiskanUst(double boy, double satir, double gorunen, double kayma)
    {
        var enAlt = Math.Max(0, satir - boy);
        var istenen = boy <= gorunen ? kayma : kayma + gorunen - boy;
        return Math.Clamp(istenen, 0, enAlt);
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
