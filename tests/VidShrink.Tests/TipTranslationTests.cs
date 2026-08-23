using System.Text;
using System.Text.RegularExpressions;

namespace VidShrink.Tests;

/// <summary>
/// T26 K2: ipucu metni değişip sözlük anahtarı eski metinde kalırsa arayüz sessizce
/// İngilizce kalıyordu — ne derleme ne test uyarıyordu. Bu ölçüm o sessizliği kaldırır.
///
/// Kaynak dosyalar metin olarak okunur, çünkü test projesi <c>VidShrink.App</c>'e
/// başvurmuyor. Karşılaştırma birebir: <c>LanguageCatalog</c> anahtarları da ekrandaki
/// metin de aynı <c>Title</c> geçidinden geçtiği için ham metinlerin eşleşmesi yeterli.
/// </summary>
public sealed class TipTranslationTests
{
    private static readonly string Root = FindRoot();

    private static readonly string CatalogPath =
        Path.Combine(Root, "src", "VidShrink.App", "LanguageCatalog.cs");

    private static readonly string WindowXamlPath =
        Path.Combine(Root, "src", "VidShrink.App", "MainWindow.axaml");

    private static readonly string WindowCodePath =
        Path.Combine(Root, "src", "VidShrink.App", "MainWindow.axaml.cs");

    private static readonly string ThemePath =
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

    /// <summary>
    /// Arka koddan yazılan ipucu metinleri. Bugünkü arıza tam buradaydı: XAML madde
    /// biçimine geçti, bu sabitler paragraf olarak kaldı ve arama tutmadı.
    /// </summary>
    private static readonly Regex TipConstant = new(
        """private const string (\w+(?:Tip|Effect)English) = "((?:[^"\\]|\\.)*)";""",
        RegexOptions.Compiled);

    public static readonly Regex Bullet = new("""^\s*•""", RegexOptions.Compiled);

    /// <summary>
    /// K2: ekranda gösterilen her ipucunun sözlükte birebir karşılığı olacak. Karşılığı
    /// olmayan çıkarsa bu ölçüm kırılır ve metni değiştiren kişi çeviriyi de günceller.
    /// </summary>
    [Fact]
    public void EveryTipOnScreenHasATurkishEntry()
    {
        var catalogue = ReadCatalogue();
        var tips = ReadTips();

        Assert.NotEmpty(tips);

        var missing = tips
            .Where(tip => !catalogue.ContainsKey(tip.Text))
            .Select(tip => $"{tip.Source}: {FirstLine(tip.Text)}")
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"{missing.Count}/{tips.Count} ipucunun LanguageCatalog içinde karşılığı yok:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, missing));
    }

    /// <summary>
    /// K1: madde biçimi iki dilde birebir aynı olacak. Türkçesi İngilizcesinden az madde
    /// taşırsa bilgi kısaltılmış demektir ve ölçüm kırılır.
    /// </summary>
    [Fact]
    public void EveryTipKeepsItsBulletShapeInBothLanguages()
    {
        var catalogue = ReadCatalogue();
        var broken = new List<string>();

        foreach (var tip in ReadTips())
        {
            if (!catalogue.TryGetValue(tip.Text, out var turkish)) continue;

            var english = tip.Text.Split('\n');
            var translated = turkish.Split('\n');
            var englishBullets = english.Count(line => Bullet.IsMatch(line));
            var translatedBullets = translated.Count(line => Bullet.IsMatch(line));

            if (english.Length != translated.Length || englishBullets != translatedBullets)
                broken.Add(
                    $"{tip.Source}: satır {english.Length}/{translated.Length}, "
                    + $"madde {englishBullets}/{translatedBullets} — {FirstLine(tip.Text)}");
        }

        Assert.True(
            broken.Count == 0,
            "Madde biçimi iki dilde eşleşmiyor:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, broken));
    }

    private static string FirstLine(string text)
    {
        var end = text.IndexOf('\n');
        return end < 0 ? text : text[..end];
    }

    private static IReadOnlyDictionary<string, string> ReadCatalogue()
    {
        var source = File.ReadAllText(CatalogPath);
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in CatalogPair.Matches(source))
            pairs[Unescape(match.Groups[1].Value)] = Unescape(match.Groups[2].Value);

        Assert.NotEmpty(pairs);
        return pairs;
    }

    private static IReadOnlyList<(string Source, string Text)> ReadTips()
    {
        var themeStrings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in ThemeString.Matches(File.ReadAllText(ThemePath)))
            themeStrings[match.Groups[1].Value] = DecodeXml(match.Groups[2].Value);

        var tips = new List<(string, string)>();

        foreach (Match match in TipInXaml.Matches(File.ReadAllText(WindowXamlPath)))
        {
            var raw = match.Groups[1].Value;
            var reference = Regex.Match(raw, """^\{StaticResource\s+(\w+)\}$""");
            if (reference.Success)
            {
                var key = reference.Groups[1].Value;
                Assert.True(themeStrings.ContainsKey(key), $"Theme.axaml içinde {key} yok.");
                tips.Add(($"Theme.axaml/{key}", themeStrings[key]));
            }
            else
            {
                tips.Add(("MainWindow.axaml", DecodeXml(raw)));
            }
        }

        foreach (Match match in TipConstant.Matches(File.ReadAllText(WindowCodePath)))
            tips.Add(($"MainWindow.axaml.cs/{match.Groups[1].Value}", Unescape(match.Groups[2].Value)));

        return tips;
    }

    private static string DecodeXml(string value) => value
        .Replace("&#10;", "\n", StringComparison.Ordinal)
        .Replace("&lt;", "<", StringComparison.Ordinal)
        .Replace("&gt;", ">", StringComparison.Ordinal)
        .Replace("&quot;", "\"", StringComparison.Ordinal)
        .Replace("&apos;", "'", StringComparison.Ordinal)
        .Replace("&amp;", "&", StringComparison.Ordinal);

    /// <summary>C# kaynak metnindeki kaçışları çözer, böylece karşılaştırma ham metin üzerinden olur.</summary>
    private static string Unescape(string value)
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
