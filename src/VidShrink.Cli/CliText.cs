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

    /// <summary>
    /// Dil bayrağının okunması. <c>--dil</c> / <c>--lang</c> ayrı sözcükle de
    /// (<c>--dil tr</c>) eşittir işaretiyle de (<c>--dil=tr</c>) yazılabiliyor; bayrak
    /// listeden çıkarılıyor, çünkü komut çözümleyicisi tanımadığı seçeneği hata sayıyor.
    /// Tanınmayan değer sessizce yutulmuyor, <see cref="LanguageSelection.Invalid"/> ile
    /// geri dönüyor.
    /// </summary>
    public static LanguageSelection SplitLanguage(IReadOnlyList<string> args)
    {
        var rest = new List<string>(args.Count);
        string? language = null;
        string? invalid = null;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            string? value;

            if (IsLanguageFlag(arg))
            {
                if (i + 1 >= args.Count)
                {
                    invalid ??= arg;
                    continue;
                }

                value = args[++i];
            }
            else if (arg.IndexOf('=') is var eq && eq > 0 && IsLanguageFlag(arg[..eq]))
            {
                value = arg[(eq + 1)..];
            }
            else
            {
                rest.Add(arg);
                continue;
            }

            if (TryNormalizeLanguage(value, out var normalized)) language = normalized;
            else invalid ??= value;
        }

        return new LanguageSelection(rest, language, invalid);
    }

    public sealed record LanguageSelection(IReadOnlyList<string> Rest, string? Language, string? Invalid);

    private static bool IsLanguageFlag(string arg)
        => arg is "--dil" or "--lang";

    /// <summary>
    /// <c>tr</c>, <c>TR</c>, <c>tr-TR</c> ve <c>tr_TR</c> aynı dile çıkıyor; desteklenmeyen
    /// kod kabul edilmiyor, yoksa kullanıcı yazdığı dili aldığını sanıp İngilizce okurdu.
    /// </summary>
    public static bool TryNormalizeLanguage(string? value, out string language)
    {
        language = "en";
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();
        var cut = trimmed.IndexOfAny(new[] { '-', '_' });
        if (cut > 0) trimmed = trimmed[..cut];
        trimmed = trimmed.ToLowerInvariant();

        foreach (var known in Languages)
            if (known == trimmed)
            {
                language = known;
                return true;
            }

        return false;
    }

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
        return JsonSerializer.Deserialize(stream, CliJson.Default.DictionaryStringString)
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
