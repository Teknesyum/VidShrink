using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace VidShrink.Cli;

public sealed class CliText
{
    public static readonly IReadOnlyList<string> Languages = new[] { "en", "tr" };

    private readonly IReadOnlyDictionary<string, string> _strings;
    private readonly IReadOnlyDictionary<string, string> _fallback;

    private CliText(string language, IReadOnlyDictionary<string, string> strings, IReadOnlyDictionary<string, string> fallback)
    {
        Language = language;
        _strings = strings;
        _fallback = fallback;
    }

    public string Language { get; }

    public static string LanguageFor(CultureInfo culture)
        => string.Equals(culture.TwoLetterISOLanguageName, "tr", StringComparison.OrdinalIgnoreCase) ? "tr" : "en";

    public static CliText For(CultureInfo culture) => ForLanguage(LanguageFor(culture));

    public static CliText ForLanguage(string language)
    {
        var fallback = Load("en");
        return new CliText(language, language == "en" ? fallback : Load(language), fallback);
    }

    public static IReadOnlyDictionary<string, string> Load(string language)
    {
        var name = $"VidShrink.Cli.Locales.{language}.json";
        using var stream = typeof(CliText).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing locale resource {name}.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"Empty locale resource {name}.");
    }

    public string this[string key]
        => _strings.TryGetValue(key, out var value) ? value
            : _fallback.TryGetValue(key, out var english) ? english
            : key;

    public string Format(string key, params object?[] args)
        => string.Format(CultureInfo.InvariantCulture, this[key], args);

    public static string Version
        => typeof(CliText).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
           ?? typeof(CliText).Assembly.GetName().Version?.ToString()
           ?? "0";
}
