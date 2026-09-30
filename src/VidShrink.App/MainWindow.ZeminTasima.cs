using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VidShrink.App.Editing;
using VidShrink.App.Playback;

namespace VidShrink.App;

public partial class MainWindow
{
    private readonly ClickArbiter _zeminTik = new();
    private PointerPressedEventArgs? _zeminBasis;
    private readonly List<string> _zeminIz = new();

    internal IReadOnlyList<string> ZeminIz => _zeminIz.ToArray();

    internal bool ZeminBekliyor => _zeminBasis is not null;

    internal Action<PointerPressedEventArgs>? TasimaBaslatici { get; set; }

    private void TrackBackgroundDrag()
    {
        AddHandler(PointerPressedEvent, OnBackgroundPointerPressed, RoutingStrategies.Bubble);
        AddHandler(PointerMovedEvent, OnBackgroundPointerMoved, RoutingStrategies.Tunnel, true);
        AddHandler(PointerReleasedEvent, OnBackgroundPointerReleased, RoutingStrategies.Tunnel, true);
        AddHandler(PointerCaptureLostEvent, (_, _) => ZeminBirak("capturelost"), RoutingStrategies.Bubble, true);
    }

    private void OnBackgroundPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || e.ClickCount != 1) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (!ZemindenTasinir(e.Source as Visual)) return;
        e.Handled = true;
        ZeminBas(e);
    }

    private void ZeminBas(PointerPressedEventArgs e)
    {
        var at = e.GetPosition(this);
        _zeminBasis = e;
        _zeminTik.Press(at.X, at.Y);
        _zeminIz.Add("press");
    }

    private void OnBackgroundPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_zeminBasis is not { } basis) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ZeminBirak("nobutton");
            return;
        }

        var at = e.GetPosition(this);
        if (!_zeminTik.Move(at.X, at.Y)) return;
        _zeminBasis = null;
        _zeminTik.Cancel();
        _zeminIz.Add("movedrag");
        if (TasimaBaslatici is { } baslat) baslat(basis);
        else BeginMoveDrag(basis);
    }

    private void OnBackgroundPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        ZeminBirak("click");
    }

    private void ZeminBirak(string reason)
    {
        if (_zeminBasis is null) return;
        _zeminBasis = null;
        _zeminTik.Cancel();
        _zeminIz.Add(reason);
    }

    internal bool ZemindenTasinir(Visual? source)
    {
        if (source is null || WindowState == WindowState.FullScreen) return false;
        for (var v = source; v is not null && !ReferenceEquals(v, this); v = v.GetVisualParent())
        {
            if (ReferenceEquals(v, TitleBar)) return false;
            if (v is InputElement { Cursor: not null } imlecli && !ReferenceEquals(imlecli.Cursor, Cursor.Default)) return false;
            if (EtkilesimliDenetim.Mi(v)) return false;
            switch (v)
            {
                case PlayerView or ComparisonPanel or ComparisonSurface or ControlStrip:
                case EditorTimeline or EditorTrackCanvas:
                    return false;
            }
        }
        return true;
    }
}
