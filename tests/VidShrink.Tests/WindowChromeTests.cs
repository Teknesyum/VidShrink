using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App;

namespace VidShrink.Tests;

/// <summary>
/// T12 K2: özel pencere çerçevesinin düğmeleri gerçekten bağlı.
///
/// Kabuk yok, işletim sisteminin başlık çubuğu yok; küçült, büyüt/geri al ve kapat
/// uygulamanın kendi düğmeleri. Üçü de birer <see cref="WindowState"/> ataması ve bir
/// <see cref="Window.Close"/> çağrısı, yani pencere gösterilmeden ölçülebilir: düğmenin
/// <see cref="Button.ClickEvent"/> olayı yükseltilir ve pencerenin durumuna bakılır.
///
/// Başlık çubuğundan sürükleme (<c>BeginMoveDrag</c>) bu ölçümün dışında; işletim
/// sisteminden gerçek fare girdisi istiyor ve başsız koşuda karşılığı yok.
/// </summary>
public sealed class WindowChromeTests
{
    private static Button Control(MainWindow window, string name)
    {
        var button = window.FindControl<Button>(name);
        Assert.True(button is not null, $"{name} düğmesi biçimlemede yok.");
        return button!;
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public void Minimize_button_minimizes_the_window()
    {
        var state = AppHost.Run(() =>
        {
            var window = new MainWindow();
            Click(Control(window, "BtnMinimize"));
            return window.WindowState;
        });

        Assert.Equal(WindowState.Minimized, state);
    }

    private static Avalonia.Media.Geometry? Glyph(MainWindow window)
    {
        var path = window.FindControl<Avalonia.Controls.Shapes.Path>("MaximizeGlyph");
        Assert.True(path is not null, "MaximizeGlyph biçimlemede yok.");
        return path!.Data;
    }

    [Fact]
    public void Maximize_button_toggles_between_maximized_and_normal()
    {
        var (first, firstGlyph, second, secondGlyph) = AppHost.Run(() =>
        {
            var window = new MainWindow();
            var button = Control(window, "BtnMaximize");

            window.WindowState = WindowState.Normal;
            Click(button);
            var up = (window.WindowState, Glyph(window));

            Click(button);
            var down = (window.WindowState, Glyph(window));

            return (up.WindowState, up.Item2, down.WindowState, down.Item2);
        });

        Assert.Equal(WindowState.Maximized, first);
        Assert.Equal(WindowState.Normal, second);
        Assert.NotNull(firstGlyph);
        Assert.NotNull(secondGlyph);
        Assert.NotSame(firstGlyph, secondGlyph);
    }

    [Fact]
    public void Close_button_closes_the_window()
    {
        var closed = AppHost.Run(() =>
        {
            var window = new MainWindow();
            var seen = false;
            window.Closed += (_, _) => seen = true;

            Click(Control(window, "BtnClose"));
            return seen;
        });

        Assert.True(closed, "Kapat düğmesi pencereyi kapatmadı.");
    }
}
