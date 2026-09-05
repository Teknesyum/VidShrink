using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Playback;

namespace VidShrink.Tests;

public sealed class OnizlemeBarinakTests
{
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

        Assert.False(shell.Contains(new Point(-5, -5)));
        Assert.Equal(ShelterStage.Mid, before);
        Assert.Equal(ShelterStage.Band, after);
    }

    [Fact]
    public void K2_Band_kademede_disari_tiklama_hicbir_sey_yapmaz()
    {
        var (before, after, promoted) = Read((window, panel) =>
        {
            var beforeStage = panel.Shelter;

            Tap(window, new Point(0, 0));

            return (beforeStage, panel.Shelter, panel.IsPromoted);
        });

        Assert.Equal(ShelterStage.Band, before);
        Assert.Equal(ShelterStage.Band, after);
        Assert.False(promoted);
    }

    [Fact]
    public void K3_Panel_icine_tiklama_asamayi_degistirmez()
    {
        var results = Read((window, panel) =>
        {
            WheelTo(window, panel, ShelterStage.Mid);
            var rect = ShellOnWindow(window, panel);

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

        Assert.Equal(ShelterStage.Band, afterFirstEscape);
        Assert.Equal(ShelterStage.Band, afterSecondEscape);
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

        Assert.True(focusedOnPromote);
        Assert.True(focusReleasedAfterOutsideClick);
    }
}
