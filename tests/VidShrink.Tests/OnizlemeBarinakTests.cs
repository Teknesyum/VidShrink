using Avalonia;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Playback;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class OnizlemeBarinakTests
{
    private readonly ITestOutputHelper _output;

    public OnizlemeBarinakTests(ITestOutputHelper output) => _output = output;

    private static readonly Size WindowSize = new(1560, 1060);

    private static void LayOutAt(MainWindow window, Size size)
    {
        window.Width = double.NaN;
        window.Height = double.NaN;

        window.Measure(size);
        window.Arrange(new Rect(size));
        window.UpdateLayout();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.Measure(size);
        root.Arrange(new Rect(size));
    }

    private static T Read<T>(Func<MainWindow, ComparisonPanel, T> read) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            LayOutAt(window, WindowSize);

            var panel = window.GetVisualDescendants().OfType<ComparisonPanel>().Single();
            return read(window, panel);
        });

    private static void Settle(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void WheelTo(MainWindow window, ComparisonPanel panel, ShelterStage stage)
    {
        for (var i = 0; i < 100 && panel.Shelter != stage; i++)
        {
            panel.Zoom(1, new Point(0, 0));
            Settle(window);
        }

        Assert.Equal(stage, panel.Shelter);
    }

    private static PointerPressedEventArgs Click(MainWindow window, Point atWindow) =>
        new(window, new Pointer(0, PointerType.Mouse, true), window, atWindow, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None, 1);

    private static KeyEventArgs Escape(MainWindow window) =>
        new() { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = window };

    private static Rect ShellOnWindow(MainWindow window, ComparisonPanel panel)
    {
        var origin = panel.Shell.TranslatePoint(new Point(0, 0), window) ?? new Point(0, 0);
        return new Rect(origin, panel.Shell.Bounds.Size);
    }

    private static Rect IndependentShellRect(MainWindow window, ComparisonPanel panel)
    {
        var size = panel.Shell.Bounds.Size;
        var matrix = panel.Shell.TransformToVisual(window);
        Point origin;
        if (matrix is { } m)
        {
            var corners = new[]
            {
                new Point(0, 0).Transform(m),
                new Point(size.Width, 0).Transform(m),
                new Point(0, size.Height).Transform(m),
                new Point(size.Width, size.Height).Transform(m),
            };
            origin = new Point(corners.Min(p => p.X), corners.Min(p => p.Y));
        }
        else
        {
            origin = new Point(0, 0);
        }

        var fromMatrix = new Rect(origin, size);
        var fromShell = ShellOnWindow(window, panel);

        Assert.Equal(fromMatrix.X, fromShell.X, 3);
        Assert.Equal(fromMatrix.Y, fromShell.Y, 3);
        Assert.Equal(fromMatrix.Width, fromShell.Width, 3);
        Assert.Equal(fromMatrix.Height, fromShell.Height, 3);

        return fromShell;
    }

    private static void Tap(MainWindow window, Point atWindow)
    {
        window.RaiseEvent(Click(window, atWindow));
        Settle(window);
    }

    [Fact]
    public void K2_Full_kademede_disari_tiklama_bandin_iner()
    {
        var (before, after) = Read((window, panel) =>
        {
            WheelTo(window, panel, ShelterStage.Full);
            var beforeStage = panel.Shelter;

            Tap(window, new Point(-5, -5));

            return (beforeStage, panel.Shelter);
        });

        _output.WriteLine($"K2FULL before={before} after={after}");

        Assert.Equal(ShelterStage.Full, before);
        Assert.Equal(ShelterStage.Band, after);
    }

    [Fact]
    public void K2_Mid_kademede_disari_tiklama_bandin_iner()
    {
        var (before, after, shell) = Read((window, panel) =>
        {
            WheelTo(window, panel, ShelterStage.Mid);
            var beforeStage = panel.Shelter;
            var rect = ShellOnWindow(window, panel);

            Tap(window, new Point(-5, -5));

            return (beforeStage, panel.Shelter, rect);
        });

        _output.WriteLine($"K2MID before={before} after={after} shellRect={shell}");

        Assert.False(shell.Contains(new Point(-5, -5)));
        Assert.Equal(ShelterStage.Mid, before);
        Assert.Equal(ShelterStage.Band, after);
    }

    [Fact]
    public void K2_Mid_kademede_pencere_ici_panel_disi_noktayla_disari_tiklama_bandin_iner()
    {
        var outside = new Point(1000, 500);

        var (before, after, shell) = Read((window, panel) =>
        {
            WheelTo(window, panel, ShelterStage.Mid);
            var beforeStage = panel.Shelter;
            var rect = ShellOnWindow(window, panel);

            Tap(window, outside);

            return (beforeStage, panel.Shelter, rect);
        });

        _output.WriteLine($"K2MID_ICERI before={before} after={after} shellRect={shell} nokta={outside}");

        Assert.True(outside.X >= 0 && outside.X <= WindowSize.Width);
        Assert.True(outside.Y >= 0 && outside.Y <= WindowSize.Height);
        Assert.False(shell.Contains(outside));
        Assert.Equal(ShelterStage.Mid, before);
        Assert.Equal(ShelterStage.Band, after);
    }

    [Fact]
    public void K2_Band_kademede_disari_tiklama_hicbir_sey_yapmaz()
    {
        var far = new Point(5000, 5000);

        var (before, dismissed, after, promoted) = Read((window, panel) =>
        {
            var beforeStage = panel.Shelter;
            var handled = panel.TryDismissOnOutsideClick(far);

            Tap(window, new Point(0, 0));

            return (beforeStage, handled, panel.Shelter, panel.IsPromoted);
        });

        _output.WriteLine($"K2BAND before={before} dismissed={dismissed} after={after} promoted={promoted}");

        Assert.Equal(ShelterStage.Band, before);
        Assert.False(dismissed);
        Assert.Equal(ShelterStage.Band, after);
        Assert.False(promoted);
    }

    [Fact]
    public void K3_Panel_icine_tiklama_asamayi_degistirmez()
    {
        var results = Read((window, panel) =>
        {
            WheelTo(window, panel, ShelterStage.Mid);
            var rect = IndependentShellRect(window, panel);

            var points = new[]
            {
                new Point(rect.Left + 1, rect.Top + 1),
                new Point(rect.Right - 1, rect.Top + 1),
                new Point(rect.Left + 1, rect.Bottom - 1),
                new Point(rect.Right - 1, rect.Bottom - 1),
                rect.Center
            };

            var stages = new List<(Point Point, ShelterStage Stage)>();
            foreach (var point in points)
            {
                Tap(window, point);
                stages.Add((point, panel.Shelter));
            }

            return stages;
        });

        foreach (var (point, stage) in results) _output.WriteLine($"K3PT point=({point.X:0.##},{point.Y:0.##}) stage={stage}");

        Assert.Equal(5, results.Count);
        Assert.All(results, r => Assert.Equal(ShelterStage.Mid, r.Stage));
    }

    [Fact]
    public void K4_Esc_bir_kademe_kucultur_bandde_hicbir_sey_yapmaz()
    {
        var (afterFirstEscape, afterSecondEscape) = Read((window, panel) =>
        {
            WheelTo(window, panel, ShelterStage.Mid);

            window.RaiseEvent(Escape(window));
            Settle(window);
            var first = panel.Shelter;

            window.RaiseEvent(Escape(window));
            Settle(window);
            var second = panel.Shelter;

            return (first, second);
        });

        _output.WriteLine($"K4ESC first={afterFirstEscape} second={afterSecondEscape}");

        Assert.Equal(ShelterStage.Band, afterFirstEscape);
        Assert.Equal(ShelterStage.Band, afterSecondEscape);
    }

    [Fact]
    public void K4_Esc_Full_kademede_tekerlekle_ulasilmissa_bandin_iner()
    {
        var (before, after, enlarged) = Read((window, panel) =>
        {
            WheelTo(window, panel, ShelterStage.Full);
            var beforeEnlarged = panel.IsEnlarged;
            var beforeStage = panel.Shelter;

            window.RaiseEvent(Escape(window));
            Settle(window);

            return (beforeStage, panel.Shelter, beforeEnlarged);
        });

        _output.WriteLine($"K4ESCFULL_WHEEL before={before} after={after} enlarged={enlarged}");

        Assert.False(enlarged);
        Assert.Equal(ShelterStage.Full, before);
        Assert.Equal(ShelterStage.Band, after);
    }

    [Fact]
    public void K4_Esc_dugmeyle_buyutulmus_Full_kademede_saklanan_boya_doner()
    {
        var (viaButton, afterEscape, enlargedBeforeEscape) = Read((window, panel) =>
        {
            WheelTo(window, panel, ShelterStage.Mid);

            var fullButton = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "BtnPanelFullScreen");
            fullButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Settle(window);
            var stageViaButton = panel.Shelter;
            var enlarged = panel.IsEnlarged;

            window.RaiseEvent(Escape(window));
            Settle(window);

            return (stageViaButton, panel.Shelter, enlarged);
        });

        _output.WriteLine($"K4ESCFULL_BUTTON viaButton={viaButton} enlarged={enlargedBeforeEscape} afterEscape={afterEscape}");

        Assert.True(enlargedBeforeEscape);
        Assert.Equal(ShelterStage.Full, viaButton);
        Assert.Equal(ShelterStage.Mid, afterEscape);
    }

    [Fact]
    public void K2_dugmeyle_buyutulmus_Full_kademede_disari_tiklama_da_bandin_iner()
    {
        var (viaButton, afterOutsideClick, enlargedBeforeClick) = Read((window, panel) =>
        {
            WheelTo(window, panel, ShelterStage.Mid);

            var fullButton = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "BtnPanelFullScreen");
            fullButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Settle(window);
            var stageViaButton = panel.Shelter;
            var enlarged = panel.IsEnlarged;

            Tap(window, new Point(-5, -5));

            return (stageViaButton, panel.Shelter, enlarged);
        });

        _output.WriteLine($"K2FULL_BUTTON viaButton={viaButton} enlarged={enlargedBeforeClick} afterOutsideClick={afterOutsideClick}");

        Assert.True(enlargedBeforeClick);
        Assert.Equal(ShelterStage.Full, viaButton);
        Assert.Equal(ShelterStage.Band, afterOutsideClick);
    }

    [Fact]
    public void K4_Terfi_odagi_tutar_disari_tiklama_odagi_birakir()
    {
        var (focusedOnPromote, focusReleasedAfterOutsideClick) = Read((window, panel) =>
        {
            WheelTo(window, panel, ShelterStage.Mid);
            var onPromote = ReferenceEquals(window.FocusManager?.GetFocusedElement(), panel.Shell);

            Tap(window, new Point(-5, -5));

            var afterOutside = window.FocusManager?.GetFocusedElement();
            var released = !ReferenceEquals(afterOutside, panel.Shell);

            return (onPromote, released);
        });

        _output.WriteLine($"K4FOCUS onPromote={focusedOnPromote} released={focusReleasedAfterOutsideClick}");

        Assert.True(focusedOnPromote);
        Assert.True(focusReleasedAfterOutsideClick);
    }
}