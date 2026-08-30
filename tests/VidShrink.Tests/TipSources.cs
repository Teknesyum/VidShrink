using System.Text;
using System.Text.RegularExpressions;

namespace VidShrink.Tests;

/// <summary>
/// İpucu metinlerinin tek okuma yeri. T83'ten önce metin üç ayrı kaynaktan geliyordu
/// (biçimleme, tema sabitleri, arka koddaki <c>*TipEnglish</c> sabitleri) ve yalnız birine
/// bakan bir ölçüm sessizce yeşil veriyordu. Bugün tek kaynak var: <c>Locales</c>
/// altındaki dil dosyaları. Okuyucu oraya taşındı — biçimlemeye bakmaya devam etseydi
/// hiçbir ipucu bulamaz ve ölçümler boş kümede yeşil kalırdı.
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

    internal static readonly Regex Bullet = new("""^\s*•""", RegexOptions.Compiled);

    /// <summary>
    /// Balonun gövdesine bağlanan anahtar. <c>TipText</c> teması şart: aynı madde boyacısına
    /// bağlanan <c>BulletText</c> blokları sekme gövdesinde tam genişlikte duruyor, balon
    /// tavanıyla ölçülmeleri yanlış olur.
    /// </summary>
    private static readonly Regex TipBinding = new(
        "Theme=\"\\{StaticResource TipText\\}\"[^>]*?loc:Bullets\\.Text=\"\\{loc:Text ([\\w.\\-]+)\\}\"",
        RegexOptions.Compiled);

    /// <summary>
    /// Biçimlemede bağlı olmayan, arka koddan yazılan balon metinleri. Adlarıyla sayılırlar;
    /// düzen "madde taşıyan her anahtar" olsaydı bilgi kutusunun uzun paragrafları da
    /// balon sanılır ve tavan ölçümü anlamını yitirirdi.
    /// </summary>
    private static readonly IReadOnlyList<string> WrittenFromCode = new[]
    {
        "main.fast-gpu.tip",
        "main.fast-gpu.tip-missing",
        "settings.update.auto-effect",
        "settings.update.no-self-effect"
    };

    /// <summary>
    /// İngilizce metinden Türkçesine. Eşleşme anahtar üzerinden kuruluyor: iki dil dosyası
    /// aynı anahtarı taşıdığı için karşılık aramak artık metin karşılaştırması değil.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> ReadCatalogue()
    {
        var english = Locales.Values("en");
        var turkish = Locales.Values("tr");
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in english)
            if (turkish.TryGetValue(key, out var written))
                pairs[value] = written;

        return pairs;
    }

    /// <summary>
    /// İpucu balonuna giren metinler. Anahtarlar biçimlemedeki bağlamadan okunur, metinler
    /// dil dosyasından: balonun tavanı ölçülürken ekranda balonda duran metin ölçülsün diye.
    /// </summary>
    internal static IReadOnlyList<Tip> ReadTips()
    {
        var english = Locales.Values("en");
        var xaml = File.ReadAllText(WindowXamlPath);
        var keys = TipBinding.Matches(xaml).Select(match => match.Groups[1].Value)
            .Concat(WrittenFromCode)
            .Distinct(StringComparer.Ordinal);

        var tips = new List<Tip>();
        foreach (var key in keys)
            if (english.TryGetValue(key, out var value))
                tips.Add(new Tip(key, value));

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
