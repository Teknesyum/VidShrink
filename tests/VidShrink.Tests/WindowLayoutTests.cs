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

    [Fact]
    public void TheEmptyWindowDoesNotScrollAtItsStartingSize()
    {
        var overflowing = LayOut(1560, 1060);

        Assert.True(
            overflowing.Count == 0,
            "Açılış boyutunda taşan taşıyıcı var:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, overflowing));
    }

    [Fact]
    public void NoTextIsClippedAtTheSmallestSize()
    {
        var clipped = AppHost.Run(() =>
        {
            var window = new MainWindow();

            window.Measure(new Size(window.MinWidth, window.MinHeight));
            window.Arrange(new Rect(0, 0, window.MinWidth, window.MinHeight));
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
