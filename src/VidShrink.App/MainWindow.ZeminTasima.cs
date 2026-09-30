using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VidShrink.App.Editing;
using VidShrink.App.Playback;

namespace VidShrink.App;

public partial class MainWindow
{
    private void TrackBackgroundDrag()
    {
        AddHandler(PointerPressedEvent, OnBackgroundPointerPressed, RoutingStrategies.Bubble);
    }

    private void OnBackgroundPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || e.ClickCount != 1) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (!ZemindenTasinir(e.Source as Visual)) return;
        e.Handled = true;
        BeginMoveDrag(e);
    }

    internal bool ZemindenTasinir(Visual? source)
    {
        if (source is null || WindowState == WindowState.FullScreen) return false;
        for (var v = source; v is not null && !ReferenceEquals(v, this); v = v.GetVisualParent())
        {
            if (ReferenceEquals(v, TitleBar)) return false;
            if (v is InputElement { Cursor: not null } imlecli && !ReferenceEquals(imlecli.Cursor, Cursor.Default)) return false;
            switch (v)
            {
                case PlayerView or ComparisonPanel or ComparisonSurface or ControlStrip:
                case EditorTimeline or EditorTrackCanvas:
                case Button or TextBox or SelectableTextBlock or RangeBase or Thumb:
                case ScrollBar or ComboBox or ListBoxItem or TreeViewItem or ToggleSwitch:
                case NumericUpDown or DatePicker or TimePicker or CalendarDatePicker or AutoCompleteBox:
                case NativeControlHost or GridSplitter or MenuItem or TabItem:
                    return false;
            }
        }
        return true;
    }
}
