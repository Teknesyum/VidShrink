using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal sealed class EditorClip : Border
{
    internal const string ReverseIcon = "IconRewind";
    private const string TimesMark = "×";

    private readonly Border _badge;
    private readonly TextBlock _label;
    private readonly Path _reverse;

    public EditorClip()
    {
        _label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _reverse = new Path { Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
        _reverse.Bind(Shape.FillProperty, _label.GetObservable(TextBlock.ForegroundProperty));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Children = { _reverse, _label } };
        _badge = new Border
        {
            Child = row,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsVisible = false
        };
        Child = _badge;
    }

    internal int Index { get; private set; } = -1;

    internal EditClip? Data { get; private set; }

    internal Border Badge => _badge;

    internal string BadgeText => _label.Text ?? string.Empty;

    internal Path ReverseMark => _reverse;

    internal void ApplyThemes(ControlTheme? clip, ControlTheme? badge, ControlTheme? text)
    {
        if (clip is not null) Theme = clip;
        Padding = new Avalonia.Thickness(EditorTokens.Size(this, "EditorClipPadding"));
        if (badge is not null) _badge.Theme = badge;
        if (text is not null) _label.Theme = text;
        _reverse.Data = EditorTokens.Find(this, ReverseIcon) as Geometry;
        var icon = EditorTokens.Size(this, "IconSizeSm");
        _reverse.Width = icon;
        _reverse.Height = icon;
        if (_badge.Child is StackPanel row) row.Spacing = EditorTokens.Size(this, "SpaceXs");
    }

    internal void Show(int index, EditClip clip, bool selected, bool compact)
    {
        Index = index;
        Data = clip;
        Classes.Set("selected", selected);
        var mark = Mark(clip, compact);
        _reverse.IsVisible = clip.Reversed;
        _label.IsVisible = mark.Length > 0;
        _badge.IsVisible = clip.Reversed || mark.Length > 0;
        _label.SetCurrentValue(TextBlock.TextProperty, mark);
    }

    internal static string Mark(EditClip clip, bool compact)
    {
        if (compact || clip.Speed == 1m) return string.Empty;
        return Bicim.Kat((double)clip.Speed, Strings.Culture) + TimesMark;
    }
}
