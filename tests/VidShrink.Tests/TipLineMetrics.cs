using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace VidShrink.Tests;

/// <summary>
/// T27 K1/K4: ipucu balonundaki her madde satırının kaç piksel yer kapladığını ölçer.
///
/// Ölçüm uygulamanın kendi yazı tipiyle ve kendi metin dizgicisiyle yapılır. Yazı tipi
/// (<c>Atkinson Hyperlegible Next</c>) projeye gömülüdür, sistemde kurulu değildir; sistem
/// yazı tipiyle ölçmek yanlış sonuç verir. Bu yüzden aynı .ttf dosyaları test projesine de
/// kaynak olarak bağlanmıştır ve Avalonia'nın <see cref="TextLayout"/> sınıfı kullanılır —
/// yani ekranda satırı bölen kodun ta kendisi.
///
/// Tavan uydurulmaz, <c>Themes/Theme.axaml</c> belirteçlerinden hesaplanır:
/// <c>TooltipMaxWidth</c> eksi balonun iki yandaki dolgusu ve kenarlığı.
/// </summary>
internal static class TipLineMetrics
{
    private static readonly object Gate = new();
    private static bool _ready;

    /// <summary>Satırdaki kelimeler; noktalama kelimeye yapışık sayılır.</summary>
    private static readonly Regex Word = new(@"\S+", RegexOptions.Compiled);

    private static readonly Regex Token = new(
        """<(?:x:Double|Thickness) x:Key="(\w+)">([^<]+)</""",
        RegexOptions.Compiled);

    /// <summary>Bir madde satırının ölçümü.</summary>
    internal sealed record LineMeasurement(
        string Source,
        string Language,
        int LineIndex,
        string Text,
        double Width,
        int VisualLines,
        string LastVisualLine)
    {
        internal double Overflow => Math.Max(0, Width - Ceiling);

        internal int WordsOnLastVisualLine => Word.Matches(LastVisualLine).Count;

        /// <summary>
        /// Tavanı tek kelimeyle aşan satır: madde bölünüyor ve en alt görsel satırda tek
        /// kelime kalıyor. O kelime yüzünden balon bir satır uzuyor; metni bir tık
        /// kısaltmak satırı tümüyle kaldırır. Düzeltilmesi istenen desen tam budur.
        /// </summary>
        internal bool OverflowsByASingleWord => VisualLines >= 2 && WordsOnLastVisualLine == 1;
    }

    /// <summary>
    /// Metnin sarılabileceği genişlik. Balonun tavanı <c>TooltipMaxWidth</c>; metne kalan
    /// yer bundan iki yandaki dolgu ve kenarlık düşülerek bulunur.
    /// </summary>
    internal static double Ceiling { get; } = ReadCeiling();

    /// <summary>Yardım metni ölçüsü — <c>FontSizeMd</c>, <c>TipText</c> teması bunu kullanır.</summary>
    internal const double FontSize = 16;

    internal static double ReadCeiling()
    {
        var theme = File.ReadAllText(TipSources.ThemePath);
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Token.Matches(theme))
            tokens[match.Groups[1].Value] = match.Groups[2].Value.Trim();

        var maxWidth = double.Parse(tokens["TooltipMaxWidth"], CultureInfo.InvariantCulture);

        // "16,12" — yatay,dikey. Yatay dolgu iki yandan düşer.
        var horizontalPadding = double.Parse(
            tokens["TooltipPadding"].Split(',')[0], CultureInfo.InvariantCulture);

        var border = double.Parse(tokens["BorderThin"], CultureInfo.InvariantCulture);

        return maxWidth - (2 * horizontalPadding) - (2 * border);
    }

    /// <summary>Gömülü yazı tipini yükler. Süreç başına bir kez kurulur.</summary>
    private static Typeface Prepare()
    {
        lock (Gate)
        {
            if (!_ready)
            {
                if (Application.Current is null)
                    AppBuilder.Configure<Application>().UseSkia().UseWin32().SetupWithoutStarting();
                _ready = true;
            }
        }

        return new Typeface(new FontFamily(
            "avares://VidShrink.Tests/Fonts#Atkinson Hyperlegible Next"));
    }

    /// <summary>
    /// Bir ipucu metnindeki her mantıksal satırı ölçer. Boş satırlar atlanır.
    /// </summary>
    internal static IEnumerable<LineMeasurement> Measure(string source, string language, string text)
    {
        var typeface = Prepare();
        var lines = text.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var unwrapped = new FormattedText(
                line, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, FontSize, Brushes.Black);

            var layout = new TextLayout(
                line, typeface, FontSize, Brushes.Black,
                TextAlignment.Left, TextWrapping.Wrap, maxWidth: Ceiling);

            var visual = layout.TextLines;
            var last = visual[^1];
            var lastText = line.Substring(last.FirstTextSourceIndex, last.Length);

            yield return new LineMeasurement(
                source, language, index, line, unwrapped.Width, visual.Count, lastText);
        }
    }

    /// <summary>İngilizce ve Türkçe bütün ipucu satırları.</summary>
    internal static IReadOnlyList<LineMeasurement> MeasureAll()
    {
        var catalogue = TipSources.ReadCatalogue();
        var all = new List<LineMeasurement>();

        foreach (var tip in TipSources.ReadTips())
        {
            var label = Label(tip);
            all.AddRange(Measure(label, "EN", tip.Text));
            if (catalogue.TryGetValue(tip.Text, out var turkish))
                all.AddRange(Measure(label, "TR", turkish));
        }

        return all;
    }

    /// <summary>Raporda ipucunu tanıtan kısa etiket: kaynak dosya ve ilk maddenin başı.</summary>
    private static string Label(TipSources.Tip tip)
    {
        var first = TipSources.FirstLine(tip.Text).TrimStart('•', ' ');
        if (first.Length > 44) first = first[..44].TrimEnd() + "…";
        return $"{tip.Source} · {first}";
    }
}
