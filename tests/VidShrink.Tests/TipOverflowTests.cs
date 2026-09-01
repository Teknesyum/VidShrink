using System.Globalization;
using System.Text;

namespace VidShrink.Tests;

/// <summary>
/// T27: bir madde satırı balonun tavanını **tek kelimeyle** aşınca balon boşuna bir satır
/// uzuyor. Bu ölçüm o deseni gözle değil piksel ölçerek bulur, iki dilde birden.
///
/// Taşmanın kendisi hata değil — uzun bir madde meşru olarak iki satır sürebilir. Hata
/// olan, alt satırda tek kelime kalması: metni bir tık kısaltmak o satırı tümüyle
/// kaldırır. Ölçüm bu yüzden yalnız tek kelimelik taşmayı kırar.
/// </summary>
public sealed class TipOverflowTests
{
    /// <summary>
    /// Kısaltılmayan maddeler. Bilgi kaybetmeden kısalmayan bir madde buraya yazılır,
    /// nedeni de yanına. Boş kalması iyidir; buraya bir satır eklemek metni kısaltmayı
    /// denedikten sonra yapılacak son adımdır.
    /// </summary>
    private static readonly IReadOnlyCollection<string> Accepted = Array.Empty<string>();

    /// <summary>Ölçüm tablosu buraya yazılır ki metin değişince yeniden bakılabilsin.</summary>
    private static readonly string ReportPath = Path.Combine(
        TipSources.Root, "docs", "olcumler", "t27-ipucu-satir-genislikleri.md");

    [Fact]
    public void NoTipLineOverflowsTheBalloonByASingleWord()
    {
        var measurements = TipLineMetrics.MeasureAll();
        Assert.NotEmpty(measurements);

        var offenders = measurements
            .Where(line => line.OverflowsByASingleWord)
            .Where(line => !Accepted.Contains(line.Text))
            .Select(line =>
                $"[{line.Language}] {line.Source} · satır {line.LineIndex}: "
                + $"{line.Width:F0} px, tavan {TipLineMetrics.Ceiling:F0} px, "
                + $"taşma {line.Overflow:F0} px, alt satırda tek kelime: "
                + $"\"{line.LastVisualLine.Trim()}\"")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"{offenders.Count} madde satırı tavanı tek kelimeyle aşıyor:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// K4: ölçüm bir kereye mahsus betik olmayacak. Bu, tabloyu her koşuda yeniden yazar;
    /// metin değiştiğinde tablo da değişir ve fark diff'te görünür.
    /// </summary>
    [Fact]
    public void TipLineWidthTableIsWritten()
    {
        var measurements = TipLineMetrics.MeasureAll();
        var ceiling = TipLineMetrics.Ceiling;

        var report = new StringBuilder();
        report.AppendLine("# İpucu satır genişlikleri");
        report.AppendLine();
        report.AppendLine(
            "Bu dosyayı `TipOverflowTests` üretir, elle yazılmaz. Yeniden üretmek için:");
        report.AppendLine();
        report.AppendLine("```");
        report.AppendLine("dotnet test VidShrink.sln -c Release --filter TipOverflowTests");
        report.AppendLine("```");
        report.AppendLine();
        report.AppendLine(CultureInfo.InvariantCulture, $"""
            Ölçüm uygulamanın kendi yazı tipiyle yapılır (Atkinson Hyperlegible Next,
            {TipLineMetrics.FontSize:F0} px). Tavan `Themes/Theme.axaml` belirteçlerinden
            hesaplanır: `TooltipMaxWidth` eksi iki yanın dolgusu ve kenarlığı = **{ceiling:F0} px**.
            """);
        report.AppendLine();
        report.AppendLine(CultureInfo.InvariantCulture, $"""
            Ölçülen satır: **{measurements.Count}** · tavanı aşan: **{measurements.Count(m => m.Overflow > 0)}** ·
            tek kelimeyle aşan: **{measurements.Count(m => m.OverflowsByASingleWord)}**
            """);
        report.AppendLine();
        report.AppendLine(
            "| Dil | İpucu | Satır | Genişlik | Taşma | Görsel satır | Alt satır | Tek kelime |");
        report.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | --- | :-: |");

        foreach (var line in measurements.Where(m => m.Overflow > 0))
        {
            report.AppendLine(CultureInfo.InvariantCulture,
                $"| {line.Language} | {Escape(line.Source)} | {line.LineIndex} "
                + $"| {line.Width:F0} | {line.Overflow:F0} | {line.VisualLines} "
                + $"| {Escape(line.LastVisualLine.Trim())} "
                + $"| {(line.OverflowsByASingleWord ? "evet" : "")} |");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
        File.WriteAllText(ReportPath, report.ToString());

        Assert.True(File.Exists(ReportPath));
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
