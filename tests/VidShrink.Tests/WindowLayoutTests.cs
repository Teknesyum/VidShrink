using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using VidShrink.App;

namespace VidShrink.Tests;

/// <summary>
/// T14 K1/K5 ve T23 K7: açılışta ve en küçük pencerede sayfa kaymayacak, metin kırpılmayacak.
///
/// Kanıt bugüne kadar ekran görüntüsüyle isteniyordu ve dört sözleşmede birden alınamadan
/// bekledi. Kaydırma çubuğunun görünüp görünmediği ölçülebilir bir şey: bir
/// <see cref="ScrollViewer"/> yalnız içeriği görüş alanından büyükse kayar. Pencere
/// gösterilmeden ölçülüp yerleştirilirse aynı yerleşim motoru koşar, masaüstünde pencere
/// açılmaz ve ölçüm başsız kalır.
/// </summary>
public sealed class WindowLayoutTests
{
    /// <summary>Bir taşıyıcının taştığı miktar; sıfırın üstündeyse çubuk görünür.</summary>
    private readonly record struct Overflow(string Name, double Vertical, double Horizontal)
    {
        public override string ToString() =>
            $"{Name}: dikey +{Vertical:0.#}, yatay +{Horizontal:0.#}";
    }

    private static IReadOnlyList<Overflow> LayOut(double width, double height) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();

            // Pencerenin kendi Width/Height degerleri ArrangeCore icindeki
            // ApplyLayoutConstraints tarafindan olcum argumanina uygulanir ve argumani
            // yutar; temizlenmezse her boyut ayni yerlesimi olcer.
            window.Width = double.NaN;
            window.Height = double.NaN;

            window.Measure(new Size(width, height));
            window.Arrange(new Rect(0, 0, width, height));
            window.UpdateLayout();

            return window.GetVisualDescendants()
                .OfType<ScrollViewer>()
                .Where(viewer => viewer.IsEffectivelyVisible)
                .Select(viewer => new Overflow(
                    string.IsNullOrEmpty(viewer.Name) ? viewer.GetType().Name : viewer.Name!,
                    viewer.Extent.Height - viewer.Viewport.Height,
                    viewer.Extent.Width - viewer.Viewport.Width))
                .Where(entry => entry.Vertical > 0.5 || entry.Horizontal > 0.5)
                .ToList();
        });

    /// <summary>
    /// Pencere <c>WindowState="Maximized"</c> ile aciliyor, yani gercek acilis boyutu
    /// bildirilen 1560x1060 degil ekranin calisma alani. Iki boyut da olculuyor.
    /// </summary>
    [Theory]
    [InlineData(1560, 1060)]
    [InlineData(1920, 1032)]
    public void TheEmptyWindowDoesNotScrollAtItsStartingSize(double width, double height)
    {
        var overflowing = LayOut(width, height);

        Assert.True(
            overflowing.Count == 0,
            $"{width}x{height} boyutunda tasan tasiyici var:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, overflowing));
    }

    /// <summary>
    /// T23 K7: en kucuk boyutta kaydirma <b>beklenen</b> davranis — icerik gercekten
    /// sigmiyor. Olcum bunu kaydediyor ki bir gun sigdigi zaman kimse fark etmeden
    /// gecmesin; taban degistiginde test kirmiziya duser ve sayi yeniden konusulur.
    /// </summary>
    [Fact]
    public void TheSmallestSizeStillScrolls()
    {
        var overflowing = LayOut(1040, 720);

        Assert.Single(overflowing);
        Assert.InRange(overflowing[0].Vertical, 100, 150);
    }

    [Fact]
    public void NoTextIsClippedAtTheSmallestSize()
    {
        var clipped = AppHost.Run(() =>
        {
            var window = new MainWindow();
            var size = new Size(window.MinWidth, window.MinHeight);

            window.Width = double.NaN;
            window.Height = double.NaN;
            window.Measure(size);
            window.Arrange(new Rect(size));
            window.UpdateLayout();

            return window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible
                             && block.Bounds.Width > 0
                             && block.DesiredSize.Width > block.Bounds.Width + 0.5)
                .Select(block => (block.Text ?? "(boş)").Split('\n')[0])
                .ToList();
        });

        Assert.True(
            clipped.Count == 0,
            "En küçük boyutta kırpılan metin var:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, clipped));
    }
}
