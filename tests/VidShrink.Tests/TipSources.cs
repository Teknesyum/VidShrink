using System.Text;
using System.Text.RegularExpressions;

namespace VidShrink.Tests;

/// <summary>
/// İpucu metinlerinin tek okuma yeri. Metin üç ayrı kaynaktan geliyor
/// (<c>MainWindow.axaml</c>, <c>Themes/Theme.axaml</c> sabitleri ve
/// <c>MainWindow.axaml.cs</c> içindeki <c>*TipEnglish</c>/<c>*EffectEnglish</c> sabitleri);
/// yalnız birine bakan bir ölçüm sessizce yeşil verir. T26 bunu bir kez yaşadı.
///
/// Kaynak dosyalar metin olarak okunur, çünkü test projesi <c>VidShrink.App</c>'e
/// başvurmuyor.
/// </summary>
internal static class TipSources
{
    internal readonly record struct Tip(string Source, string Text);

    internal static readonly string Root = FindRoot();

    internal static readonly string CatalogPath =
        Path.Combine(Root, "src", "VidShrink.App", "LanguageCatalog.cs");

    internal static readonly string WindowXamlPath =
        Path.Combine(Root, "src", "VidShrink.App", "MainWindow.axaml");

    internal static readonly string WindowCodePath =
        Path.Combine(Root, "src", "VidShrink.App", "MainWindow.axaml.cs");

    internal static readonly string ThemePath =
        Path.Combine(Root, "src", "VidShrink.App", "Themes", "Theme.axaml");

    /// <summary>Sözlükteki her çift: anahtar İngilizce, değer Türkçe.</summary>
    private static readonly Regex CatalogPair = new(
        """^\s*\["((?:[^"\\]|\\.)*)"\]\s*=\s*"((?:[^"\\]|\\.)*)"\s*[,;]?\s*$""",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>XAML'de ipucu balonunun gövdesi.</summary>
    private static readonly Regex TipInXaml = new(
        """"Theme="\{StaticResource TipText\}"\s+Text="([^"]*)"""",
        RegexOptions.Compiled);

    /// <summary>Tema içinde paylaşılan metin sabitleri.</summary>
    private static readonly Regex ThemeString = new(
        """<x:String x:Key="([^"]+)">([^<]*)</x:String>""",
        RegexOptions.Compiled);

    /// <summary>Arka koddan yazılan ipucu metinleri.</summary>
    private static readonly Regex TipConstant = new(
        """private const string (\w+(?:Tip|Effect)English) = "((?:[^"\\]|\\.)*)";""",
        RegexOptions.Compiled);

    internal static readonly Regex Bullet = new("""^\s*•""", RegexOptions.Compiled);

    internal static IReadOnlyDictionary<string, string> ReadCatalogue()
    {
        var source = File.ReadAllText(CatalogPath);
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in CatalogPair.Matches(source))
            pairs[Unescape(match.Groups[1].Value)] = Unescape(match.Groups[2].Value);

        return pairs;
    }

    internal static IReadOnlyList<Tip> ReadTips()
    {
        var themeStrings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in ThemeString.Matches(File.ReadAllText(ThemePath)))
            themeStrings[match.Groups[1].Value] = DecodeXml(match.Groups[2].Value);

        var tips = new List<Tip>();

        foreach (Match match in TipInXaml.Matches(File.ReadAllText(WindowXamlPath)))
        {
            var raw = match.Groups[1].Value;
            var reference = Regex.Match(raw, """^\{StaticResource\s+(\w+)\}$""");
            if (reference.Success)
            {
                var key = reference.Groups[1].Value;
                if (!themeStrings.TryGetValue(key, out var shared))
                    throw new InvalidOperationException($"Theme.axaml içinde {key} yok.");
                tips.Add(new Tip($"Theme.axaml/{key}", shared));
            }
            else
            {
                tips.Add(new Tip("MainWindow.axaml", DecodeXml(raw)));
            }
        }

        foreach (Match match in TipConstant.Matches(File.ReadAllText(WindowCodePath)))
            tips.Add(new Tip(
                $"MainWindow.axaml.cs/{match.Groups[1].Value}",
                Unescape(match.Groups[2].Value)));

        return tips;
    }

    internal static string FirstLine(string text)
    {
        var end = text.IndexOf('\n');
        return end < 0 ? text : text[..end];
    }

    internal static string DecodeXml(string value) => value
        .Replace("&#10;", "\n", StringComparison.Ordinal)
        .Replace("&lt;", "<", StringComparison.Ordinal)
        .Replace("&gt;", ">", StringComparison.Ordinal)
        .Replace("&quot;", "\"", StringComparison.Ordinal)
        .Replace("&apos;", "'", StringComparison.Ordinal)
        .Replace("&amp;", "&", StringComparison.Ordinal);

    /// <summary>C# kaynak metnindeki kaçışları çözer.</summary>
    internal static string Unescape(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '\\' || index + 1 >= value.Length)
            {
                builder.Append(value[index]);
                continue;
            }

            index++;
            builder.Append(value[index] switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '0' => '\0',
                _ => value[index]
            });
        }

        return builder.ToString();
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VidShrink.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
