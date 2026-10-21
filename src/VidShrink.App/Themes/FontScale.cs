using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace VidShrink.App.Themes;

internal static class FontScale
{
    internal static readonly IReadOnlyList<string> Keys = new[]
    {
        "FontSize1", "FontSize2", "FontSize3", "FontSize4", "FontSize5",
        "FontSizeSm", "FontSizeMd", "FontSizeLg", "FontSizeHero", "LineHeightBody", "TipMaxWidth"
    };

    private static readonly Dictionary<string, double> BaseResources = new(StringComparer.Ordinal);
    private static readonly Dictionary<Setter, double> BaseSetters = new(ReferenceEqualityComparer.Instance);

    public static int Current { get; private set; } = 100;

    public static void Apply(int percent)
    {
        if (Application.Current is not { } app) return;
        var factor = percent / 100.0;
        Capture(app);

        foreach (var (key, value) in BaseResources)
        {
            if (percent == 100) app.Resources.Remove(key);
            else app.Resources[key] = value * factor;
        }

        foreach (var (setter, value) in BaseSetters) setter.Value = value * factor;
        Current = percent;
    }

    private static void Capture(Application app)
    {
        if (BaseResources.Count == 0)
        {
            foreach (var key in Keys)
                if (app.TryGetResource(key, null, out var value) && value is double size)
                    BaseResources[key] = size;
        }

        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);

        void FromDictionary(IResourceDictionary? dictionary)
        {
            if (dictionary is null || !seen.Add(dictionary)) return;
            foreach (var key in dictionary.Keys)
                if (dictionary.TryGetValue(key, out var value)) FromValue(value);
            foreach (var merged in dictionary.MergedDictionaries)
            {
                switch (merged)
                {
                    case ResourceInclude include: FromDictionary(include.Loaded); break;
                    case IResourceDictionary inner: FromDictionary(inner); break;
                }
            }
        }

        void FromSetters(IEnumerable<SetterBase> setters)
        {
            foreach (var setter in setters)
            {
                if (setter is not Setter typed || BaseSetters.ContainsKey(typed)) continue;
                if (typed.Property?.Name is not ("FontSize" or "LineHeight")) continue;
                if (typed.Value is double size && double.IsFinite(size)) BaseSetters[typed] = size;
            }
        }

        void FromValue(object? value)
        {
            if (value is null || !seen.Add(value)) return;
            switch (value)
            {
                case StyleInclude include: FromValue(include.Loaded); break;
                case Styles styles:
                    FromDictionary(styles.Resources);
                    foreach (var child in styles) FromValue(child);
                    break;
                case ControlTheme theme:
                    FromDictionary(theme.Resources);
                    FromSetters(theme.Setters);
                    foreach (var child in theme.Children) FromValue(child);
                    FromValue(theme.BasedOn);
                    break;
                case Style style:
                    FromDictionary(style.Resources);
                    FromSetters(style.Setters);
                    foreach (var child in style.Children) FromValue(child);
                    break;
            }
        }

        FromDictionary(app.Resources);
        FromValue(app.Styles);
    }
}
