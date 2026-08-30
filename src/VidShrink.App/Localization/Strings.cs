using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace VidShrink.App.Localization;

public static class Strings
{
    public const string FallbackLanguage = "en";

    private const string ResourcePrefix = "VidShrink.App.Locales.";
    private const string ResourceSuffix = ".json";

    private static readonly object Gate = new();

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Loaded =
        new(StringComparer.OrdinalIgnoreCase);

    private static string root = Path.Combine(AppContext.BaseDirectory, "Locales");
    private static string current = FallbackLanguage;
    private static CultureInfo culture = CultureInfo.InvariantCulture;

    public static event EventHandler? Changed;

    internal static bool AssertOnMissingKey { get; set; } = true;

    public static string Language
    {
        get
        {
            lock (Gate)
            {
                return current;
            }
        }
    }

    public static CultureInfo Culture
    {
        get
        {
            lock (Gate)
            {
                return culture;
            }
        }
    }

    public static string Root
    {
        get
        {
            lock (Gate)
            {
                return root;
            }
        }
    }

    public static IReadOnlyList<string> Languages
    {
        get
        {
            var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in EmbeddedNames())
            {
                var language = SplitResource(name).Language;
                if (language.Length > 0)
                {
                    found.Add(language);
                }
            }

            var folder = Root;
            if (Directory.Exists(folder))
            {
                foreach (var directory in Directory.EnumerateDirectories(folder))
                {
                    var language = Path.GetFileName(directory);
                    if (language.Length > 0 &&
                        Directory.EnumerateFiles(directory, "*" + ResourceSuffix).Any())
                    {
                        found.Add(language);
                    }
                }
            }

            return found.ToArray();
        }
    }

    public static void Use(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language must not be empty.", nameof(language));
        }

        var wanted = language.Trim();
        bool moved;

        lock (Gate)
        {
            moved = !string.Equals(current, wanted, StringComparison.OrdinalIgnoreCase);
            current = wanted;
            culture = CultureOf(wanted);
        }

        if (moved)
        {
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static string Get(string key) => GetIn(Language, key);

    public static string GetIn(string language, string key)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(key);

        if (Catalog(language).TryGetValue(key, out var text))
        {
            return text;
        }

        if (!string.Equals(language, FallbackLanguage, StringComparison.OrdinalIgnoreCase) &&
            Catalog(FallbackLanguage).TryGetValue(key, out text))
        {
            return text;
        }

        if (AssertOnMissingKey)
        {
            Debug.Fail($"Localization key '{key}' is missing in '{language}' and in '{FallbackLanguage}'.");
        }

        return key;
    }

    public static string Get(string key, params object?[] args) => GetIn(Language, key, args);

    public static string GetIn(string language, string key, params object?[] args)
    {
        var text = GetIn(language, key);
        if (args is null || args.Length == 0)
        {
            return text;
        }

        var format = CultureOf(language);

        try
        {
            return string.Format(format, text, args);
        }
        catch (FormatException)
        {
            return text;
        }
    }

    public static IReadOnlyCollection<string> KeysOf(string language)
        => Catalog(language).Keys.ToArray();

    internal static void UseRoot(string? folder)
    {
        lock (Gate)
        {
            root = folder ?? Path.Combine(AppContext.BaseDirectory, "Locales");
            Loaded.Clear();
        }
    }

    internal static void Reset()
    {
        lock (Gate)
        {
            root = Path.Combine(AppContext.BaseDirectory, "Locales");
            current = FallbackLanguage;
            culture = CultureOf(FallbackLanguage);
            Loaded.Clear();
        }

        AssertOnMissingKey = true;
    }

    private static IReadOnlyDictionary<string, string> Catalog(string language)
    {
        lock (Gate)
        {
            if (Loaded.TryGetValue(language, out var known))
            {
                return known;
            }

            var built = Build(language, root);
            Loaded[language] = built;
            return built;
        }
    }

    private static IReadOnlyDictionary<string, string> Build(string language, string folder)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var name in EmbeddedNames().OrderBy(n => n, StringComparer.Ordinal))
        {
            var parts = SplitResource(name);
            if (!string.Equals(parts.Language, language, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var stream = typeof(Strings).Assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            Merge(texts, Read(stream));
        }

        var directory = Path.Combine(folder, language);
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory
                         .EnumerateFiles(directory, "*" + ResourceSuffix)
                         .OrderBy(f => f, StringComparer.Ordinal))
            {
                using var stream = File.OpenRead(file);
                Merge(texts, Read(stream));
            }
        }

        return texts;
    }

    private static void Merge(Dictionary<string, string> into, Dictionary<string, string> from)
    {
        foreach (var pair in from)
        {
            into[pair.Key] = pair.Value;
        }
    }

    private static Dictionary<string, string> Read(Stream stream)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static IEnumerable<string> EmbeddedNames()
        => typeof(Strings).Assembly
            .GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                        n.EndsWith(ResourceSuffix, StringComparison.Ordinal));

    private static (string Language, string Domain) SplitResource(string name)
    {
        var trimmed = name.Substring(
            ResourcePrefix.Length,
            name.Length - ResourcePrefix.Length - ResourceSuffix.Length);

        var cut = trimmed.IndexOf('.');
        return cut <= 0
            ? (string.Empty, string.Empty)
            : (trimmed[..cut], trimmed[(cut + 1)..]);
    }

    private static CultureInfo CultureOf(string language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
