using System.Text.RegularExpressions;

namespace VidShrink.Tests;

/// <summary>
/// T25 K1: bütünüyle büyük harf hiçbir yerde kullanılmayacak. Kural sözleşmede yazılıydı
/// ama hiçbir ölçüm onu tutmuyordu; karşılaştırma panelinin rozetleri iki tur boyunca
/// <c>Upper()</c> ile büyük harf kaldı ve borç olarak iki mühre birden yazıldı.
///
/// Aynı kriter kısaltmaları ve özel adları muaf tutuyor (<c>MP4</c>, <c>GPU</c>,
/// <c>H.264</c>). Ölçüm bu ikisini uzunlukla ayırıyor: bu kod tabanındaki kısaltmaların
/// en uzunu dört harf, bağırılan kelimeler ise (<c>ORİJİNAL</c>, <c>İŞLENMİŞ</c>) sekiz
/// harften uzun. Eşik beş harfte; daha uzun bir kısaltma gelirse ölçüm onu yakalar ve
/// eşiğin yeniden düşünülmesi gerekir — sessizce geçirmez.
/// </summary>
public sealed class CasingTests
{
    private const int AbbreviationCeiling = 4;

    private static readonly string AppRoot =
        Path.Combine(TipSources.Root, "src", "VidShrink.App");

    /// <summary>
    /// Metni tümüyle büyük harfe çeviren çağrı. <c>LanguageCatalog.Title</c> her kelimenin
    /// <b>ilk harfini</b> büyütür; o dilim bir bağırma değil, kuralın kendisidir.
    /// </summary>
    private static readonly Regex UpperCall = new(
        @"(?<!\[\.\.1\])\.ToUpper(Invariant)?\s*\(", RegexOptions.Compiled);

    private static readonly Regex VisibleText = new(
        "(?:Text|Content)=\"([^\"{}]*)\"", RegexOptions.Compiled);

    /// <summary>Beş harf ve üzeri, tümü büyük harf olan kelime.</summary>
    private static readonly Regex ShoutedWord = new(
        @"\p{Lu}{" + (AbbreviationCeiling + 1) + ",}", RegexOptions.Compiled);

    private static IEnumerable<string> Files(string extension) =>
        Directory.EnumerateFiles(AppRoot, "*" + extension, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact]
    public void NoCodePathShoutsItsText()
    {
        var offenders = Files(".cs")
            .SelectMany(path => File.ReadAllLines(path)
                .Select((line, index) => (path, line, number: index + 1))
                .Where(entry => UpperCall.IsMatch(entry.line)))
            .Select(entry => $"{Path.GetFileName(entry.path)}:{entry.number}: {entry.line.Trim()}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Metni tümüyle büyük harfe çeviren çağrı var:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void NoVisibleLabelIsWrittenInCapitals()
    {
        var offenders = new List<string>();

        foreach (var path in Files(".axaml"))
        {
            foreach (Match match in VisibleText.Matches(File.ReadAllText(path)))
            {
                var text = match.Groups[1].Value;
                if (!ShoutedWord.IsMatch(text)) continue;

                offenders.Add($"{Path.GetFileName(path)}: {text}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Tümü büyük harf yazılmış görünür metin var:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// Sözlükteki hiçbir çeviri bağırmayacak. Rozetler kaynağı değil sözlüğü okuyor;
    /// yalnız XAML'e bakan bir ölçüm onları göremezdi.
    /// </summary>
    [Fact]
    public void NoTranslationIsWrittenInCapitals()
    {
        var offenders = TipSources.ReadCatalogue()
            .Where(pair => ShoutedWord.IsMatch(pair.Key) || ShoutedWord.IsMatch(pair.Value))
            .Select(pair => $"{pair.Key} -> {pair.Value}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Sözlükte tümü büyük harf yazılmış metin var:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }
}
