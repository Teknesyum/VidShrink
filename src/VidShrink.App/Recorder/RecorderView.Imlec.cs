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

    private static readonly KeyCaptionPlace[] CaptionPlaces = Enum.GetValues<KeyCaptionPlace>();

    private static readonly KeyCaptionSize[] CaptionSizes = Enum.GetValues<KeyCaptionSize>();

    private static readonly KeyCaptionHold[] CaptionHolds = Enum.GetValues<KeyCaptionHold>();

    private void InitImlec()
    {
        ChkCursorHighlight.IsChecked = _settings.CursorHighlight;
        ChkSpotlight.IsChecked = _settings.Spotlight;
        ChkKeysShortcutsOnly.IsChecked = _settings.KeysShortcutsOnly;
        RefreshPointerLabels();
        ChkKeysShortcutsOnly.IsCheckedChanged += (_, _) => PersistChoices();
        foreach (var box in new[] { CmbKeyCaptionPlace, CmbKeyCaptionSize, CmbKeyCaptionHold })
            box.SelectionChanged += (_, _) => PersistChoices();
        ChkShowKeys.IsCheckedChanged += (_, _) => RefreshKeyChoices();
        ChkCursorHighlight.IsCheckedChanged += (_, _) => PersistChoices();
        ChkSpotlight.IsCheckedChanged += (_, _) => PersistChoices();
        foreach (var box in new[] { CmbPointerSize, CmbHighlightColor, CmbLeftClickColor, CmbRightClickColor })
            box.SelectionChanged += (_, _) => PersistChoices();
        foreach (var check in new[] { ChkCursorHighlight, ChkSpotlight, ChkShowClicks })
            check.IsCheckedChanged += (_, _) => RefreshPointerChoices();
        RefreshInputBoxes();
    }

    private void RefreshPointerChoices()
    {
        PointerChoices.IsVisible = ChkCursorHighlight.IsChecked == true || ChkSpotlight.IsChecked == true || (HooksAvailable && ChkShowClicks.IsChecked == true);
        RefreshKeyChoices();
    }

    private void RefreshKeyChoices() => KeyChoices.IsVisible = HooksAvailable && ChkShowKeys.IsChecked == true;

    private void RefreshPointerLabels()
    {
        var size = CmbPointerSize.SelectedIndex >= 0 ? PointerSizes[CmbPointerSize.SelectedIndex] : _settings.PointerSize;
        CmbPointerSize.ItemsSource = PointerSizes.Select(s => Say(PointerSizeKey(s))).ToList();
        CmbPointerSize.SelectedIndex = Array.IndexOf(PointerSizes, size);

        Fill(CmbKeyCaptionPlace, CaptionPlaces, _settings.KeyCaption.Place, p => p == KeyCaptionPlace.Top ? "recorder.keys.place.top" : "recorder.keys.place.bottom");
        Fill(CmbKeyCaptionSize, CaptionSizes, _settings.KeyCaption.Size, s => s switch
        {
            KeyCaptionSize.Small => "recorder.pointer.size.small",
            KeyCaptionSize.Large => "recorder.pointer.size.large",
            _ => "recorder.pointer.size.medium"
        });
        Fill(CmbKeyCaptionHold, CaptionHolds, _settings.KeyCaption.Hold, h => h switch
        {
            KeyCaptionHold.Short => "recorder.keys.hold.short",
            KeyCaptionHold.Long => "recorder.keys.hold.long",
            _ => "recorder.keys.hold.normal"
        });

        FillColors(CmbHighlightColor, _settings.HighlightColor);
        FillColors(CmbLeftClickColor, _settings.LeftClickColor);
        FillColors(CmbRightClickColor, _settings.RightClickColor);
    }

    private void Fill<T>(ComboBox box, T[] values, T saved, Func<T, string> key)
    {
        var value = box.SelectedIndex >= 0 ? values[box.SelectedIndex] : saved;
        box.ItemsSource = values.Select(v => Say(key(v))).ToList();
        box.SelectedIndex = Array.IndexOf(values, value);
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
        _settings.KeysShortcutsOnly = ChkKeysShortcutsOnly.IsChecked ?? true;
        _settings.KeyCaption = new KeyCaptionStyle(
            CmbKeyCaptionPlace.SelectedIndex >= 0 ? CaptionPlaces[CmbKeyCaptionPlace.SelectedIndex] : _settings.KeyCaption.Place,
            CmbKeyCaptionSize.SelectedIndex >= 0 ? CaptionSizes[CmbKeyCaptionSize.SelectedIndex] : _settings.KeyCaption.Size,
            CmbKeyCaptionHold.SelectedIndex >= 0 ? CaptionHolds[CmbKeyCaptionHold.SelectedIndex] : _settings.KeyCaption.Hold);
    }
}
