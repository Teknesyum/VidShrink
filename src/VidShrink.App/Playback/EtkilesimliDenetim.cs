using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace VidShrink.App.Playback;

internal static class EtkilesimliDenetim
{
    internal static bool Icinde(Visual? source, Visual? stop)
    {
        for (var v = source; v is not null && !ReferenceEquals(v, stop); v = v.GetVisualParent())
        {
            if (Mi(v)) return true;
        }

        return false;
    }

    internal static bool Mi(Visual v)
        => v is Button or TextBox or SelectableTextBlock or RangeBase or Thumb
            or ScrollBar or ComboBox or ListBoxItem or TreeViewItem or ToggleSwitch
            or NumericUpDown or DatePicker or TimePicker or CalendarDatePicker or AutoCompleteBox
            or NativeControlHost or GridSplitter or MenuItem or TabItem;
}
