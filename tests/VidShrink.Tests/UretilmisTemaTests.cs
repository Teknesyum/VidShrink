using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace VidShrink.Tests;

/// <summary>
/// Standardın üretilmiş teması (<c>teknesyum-ui/avalonia/Theme.axaml</c>) projeye yerinden
/// bağlanır; belirteçleri kullanılır, küresel stillerinden palet izlemeyenler
/// <c>Controls.axaml</c>'ın sonunda projenin kendi değeriyle geri alınır.
/// </summary>
public sealed class UretilmisTemaTests
{
    private sealed record Olcu(double Genislik, bool ProjeSablonu, object? PencereZemini, CornerRadius PencereKosesi, bool OdakSusuYok);

    private static Olcu Olc() => AppHost.Run(() =>
    {
        var dugme = new Button { Content = "x" };
        var icerik = new StackPanel { Height = 4000 };
        icerik.Children.Add(dugme);
        var kaydirici = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            AllowAutoHide = false,
            Content = icerik
        };
        var pencere = new Window { Width = 400, Height = 300, Content = kaydirici };
        pencere.Show();
        try
        {
            pencere.UpdateLayout();
            var cubuk = kaydirici.GetVisualDescendants().OfType<ScrollBar>()
                .First(b => b.Orientation == Orientation.Vertical);
            var sablon = Application.Current!.TryFindResource("ScrollBarVerticalTemplate", out var s) ? s : null;
            var sunucu = pencere.GetVisualDescendants().OfType<ContentPresenter>()
                .First(p => p.Name == "PART_ContentPresenter");
            return new Olcu(cubuk.Bounds.Width, ReferenceEquals(cubuk.Template, sablon), sunucu.Background,
                sunucu.CornerRadius, dugme.FocusAdorner is null);
        }
        finally { pencere.Close(); }
    });

    [Fact]
    public void KaydirmaCubuguProjeninSablonuVeKalinligiylaKalir()
    {
        var olcu = Olc();
        Assert.True(olcu.ProjeSablonu, "dikey kaydırma çubuğu üretilmiş temanın şablonuna geçti");
        Assert.Equal(double.Parse(ThemeSources.Token("ScrollBarThickness"), System.Globalization.CultureInfo.InvariantCulture), olcu.Genislik);
    }

    [Fact]
    public void PencereKabuguUretilmisZeminiBoyamaz()
    {
        var olcu = Olc();
        Assert.Null(olcu.PencereZemini);
        Assert.Equal(default, olcu.PencereKosesi);
    }

    /// <summary>
    /// Üretilmiş temanın odak halkası <c>Selector="Control"</c> ile yazılı; Avalonia'da bu
    /// tam tür eşleşmesidir, türeyen denetime ulaşmaz. Proje kendi halkasını çizer.
    /// </summary>
    [Fact]
    public void UretilmisOdakHalkasiTureyenDenetimeUlasmaz()
        => Assert.True(Olc().OdakSusuYok);
}
