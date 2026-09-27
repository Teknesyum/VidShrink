using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace VidShrink.App.Editing;

internal static class EditorTokens
{
    internal static object? Find(StyledElement host, string key)
    {
        if (host.TryFindResource(key, host.ActualThemeVariant, out var value)) return value;
        if (Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out value)) return value;
        return null;
    }

    internal static double Size(StyledElement host, string key) => Find(host, key) switch
    {
        double d => d,
        Thickness t => t.Left,
        CornerRadius r => r.TopLeft,
        _ => 0
    };

    internal static IBrush? Brush(StyledElement host, string key) => Find(host, key) as IBrush;

    internal static BoxShadows Shadow(StyledElement host, string key) => Find(host, key) is BoxShadows s ? s : default;

    internal static ControlTheme? Theme(StyledElement host, string key) => Find(host, key) as ControlTheme;

    internal static FontFamily Font(StyledElement host, string key) => Find(host, key) as FontFamily ?? FontFamily.Default;
}
