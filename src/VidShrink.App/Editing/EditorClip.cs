using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal sealed class EditorClip : Border
{
    private const string ReverseMark = "◀";
    private const string TimesMark = "×";

    private readonly Border _badge;
    private readonly TextBlock _label;

    public EditorClip()
    {
        _label = new TextBlock();
        _badge = new Border
        {
            Child = _label,
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

    internal void ApplyThemes(ControlTheme? clip, ControlTheme? badge, ControlTheme? text)
    {
        if (clip is not null) Theme = clip;
        Padding = new Avalonia.Thickness(EditorTokens.Size(this, "EditorClipPadding"));
        if (badge is not null) _badge.Theme = badge;
        if (text is not null) _label.Theme = text;
    }

    internal void Show(int index, EditClip clip, bool selected, bool compact)
    {
        Index = index;
        Data = clip;
        Classes.Set("selected", selected);
        var mark = Mark(clip, compact);
        _badge.IsVisible = mark.Length > 0;
        _label.SetCurrentValue(TextBlock.TextProperty, mark);
    }

    internal static string Mark(EditClip clip, bool compact)
    {
        if (compact) return clip.Reversed ? ReverseMark : string.Empty;
        if (clip.Speed == 1m && !clip.Reversed) return string.Empty;
        var speed = Bicim.Kat((double)clip.Speed, Strings.Culture) + TimesMark;
        return clip.Reversed ? ReverseMark + " " + speed : speed;
    }
}
