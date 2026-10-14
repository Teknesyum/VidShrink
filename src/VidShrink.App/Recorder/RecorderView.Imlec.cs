using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private static readonly PointerRingSize[] PointerSizes = Enum.GetValues<PointerRingSize>();

    private static readonly PointerColor[] PointerColors = Enum.GetValues<PointerColor>();

    private void InitImlec()
    {
        ChkCursorHighlight.IsChecked = _settings.CursorHighlight;
        ChkSpotlight.IsChecked = _settings.Spotlight;
        RefreshPointerLabels();
        ChkCursorHighlight.IsCheckedChanged += (_, _) => PersistChoices();
        ChkSpotlight.IsCheckedChanged += (_, _) => PersistChoices();
        foreach (var box in new[] { CmbPointerSize, CmbHighlightColor, CmbLeftClickColor, CmbRightClickColor })
            box.SelectionChanged += (_, _) => PersistChoices();
        foreach (var check in new[] { ChkCursorHighlight, ChkSpotlight, ChkShowClicks })
            check.IsCheckedChanged += (_, _) => RefreshPointerChoices();
        RefreshInputBoxes();
    }

    private void RefreshPointerChoices() =>
        PointerChoices.IsVisible = ChkCursorHighlight.IsChecked == true || ChkSpotlight.IsChecked == true || (HooksAvailable && ChkShowClicks.IsChecked == true);

    private void RefreshPointerLabels()
    {
        var size = CmbPointerSize.SelectedIndex >= 0 ? PointerSizes[CmbPointerSize.SelectedIndex] : _settings.PointerSize;
        CmbPointerSize.ItemsSource = PointerSizes.Select(s => Say(PointerSizeKey(s))).ToList();
        CmbPointerSize.SelectedIndex = Array.IndexOf(PointerSizes, size);

        FillColors(CmbHighlightColor, _settings.HighlightColor);
        FillColors(CmbLeftClickColor, _settings.LeftClickColor);
        FillColors(CmbRightClickColor, _settings.RightClickColor);
    }

    private void FillColors(ComboBox box, PointerColor saved)
    {
        var color = box.SelectedIndex >= 0 ? PointerColors[box.SelectedIndex] : saved;
        box.ItemsSource = PointerColors.Select((c, i) => ColorItem(c, i + 1)).ToList();
        box.SelectedIndex = Array.IndexOf(PointerColors, color);
    }

    private Control ColorItem(PointerColor color, int number)
    {
        var swatch = new Ellipse { VerticalAlignment = VerticalAlignment.Center };
        if (this.TryFindResource("IconSizeSm", out var size) && size is double side)
        {
            swatch.Width = side;
            swatch.Height = side;
        }

        if (this.TryFindResource(PointerStyle.BrushKey(color), out var brush) && brush is IBrush fill) swatch.Fill = fill;
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        if (this.TryFindResource("SpaceSm", out var gap) && gap is double spacing) panel.Spacing = spacing;
        panel.Children.Add(swatch);
        panel.Children.Add(new TextBlock { Text = Say("recorder.pointer.color", number), VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    private static string PointerSizeKey(PointerRingSize size) => size switch
    {
        PointerRingSize.Small => "recorder.pointer.size.small",
        PointerRingSize.Large => "recorder.pointer.size.large",
        _ => "recorder.pointer.size.medium"
    };

    private void CollectPointer()
    {
        _settings.CursorHighlight = ChkCursorHighlight.IsChecked ?? false;
        _settings.Spotlight = ChkSpotlight.IsChecked ?? false;
        if (CmbPointerSize.SelectedIndex >= 0) _settings.PointerSize = PointerSizes[CmbPointerSize.SelectedIndex];
        if (CmbHighlightColor.SelectedIndex >= 0) _settings.HighlightColor = PointerColors[CmbHighlightColor.SelectedIndex];
        if (CmbLeftClickColor.SelectedIndex >= 0) _settings.LeftClickColor = PointerColors[CmbLeftClickColor.SelectedIndex];
        if (CmbRightClickColor.SelectedIndex >= 0) _settings.RightClickColor = PointerColors[CmbRightClickColor.SelectedIndex];
    }
}
