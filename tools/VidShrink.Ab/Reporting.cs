using System.Globalization;
using System.Text;
using System.Text.Json;

namespace VidShrink.Ab;

public sealed record DeviationRow(
    string Competitor,
    double TargetMb,
    double? FullScore,
    double? ChunkScore,
    double? Deviation);

public static class Reporting
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string ToJson(AbReport report) => JsonSerializer.Serialize(report, Json);

    public static AbReport FromJson(string json)
        => JsonSerializer.Deserialize<AbReport>(json, Json)
           ?? throw new InvalidOperationException("Sonuç dosyası okunamadı.");

    public static string Table(AbReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"kaynak      : {report.Source}");
        sb.AppendLine($"kip         : {report.Mode}");
        sb.AppendLine($"tolerans    : ±%{report.TolerancePercent.ToString("0.##", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"ffmpeg      : {report.FfmpegVersion}");
        sb.AppendLine($"başlangıç   : {report.StartedUtc} UTC");
        sb.AppendLine();

        sb.AppendLine("Ölçüm satırları");
        sb.AppendLine("| girdi | yarışmacı | hedef MB | bayt | fark % | eş boyut | renk kapısı | harm | p10 | min | ort | XPSNR | SSIM |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var m in report.Measurements)
        {
            var parity = m.SizeEqual ? "evet" : SizeParityCheck.NotEqualStamp;
            var color = m.Measured ? m.ColorLabel : "ÖLÇÜLMEDİ — " + m.Error;
            sb.AppendLine($"| {m.Input} | {m.Competitor} | {m.TargetMb.ToString("0.###", CultureInfo.InvariantCulture)} | {m.Bytes} | {m.SizeDeltaPercent.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)} | {parity} | {color} | {AbRunner.Fmt(m.VmafNegHarmonic)} | {AbRunner.Fmt(m.VmafNegP10)} | {AbRunner.Fmt(m.VmafNegMin)} | {AbRunner.Fmt(m.VmafNegMean)} | {AbRunner.Fmt(m.Xpsnr)} | {Fmt4(m.Ssim)} |");
        }
        sb.AppendLine();

        sb.AppendLine("Özet");
        sb.AppendLine("| yarışmacı | hedef MB | toplam bayt | eş boyut | renk kapısı | harm | en kötü p10 | kare min | ort | XPSNR | SSIM |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var s in report.Summaries)
        {
            var color = s.AllMeasured ? s.ColorLabel : "ÖLÇÜLMEDİ";
            sb.AppendLine($"| {s.Competitor} | {s.TargetMb.ToString("0.###", CultureInfo.InvariantCulture)} | {s.TotalBytes} | {(s.AllSizesEqual ? "evet" : SizeParityCheck.NotEqualStamp)} | {color} | {AbRunner.Fmt(s.VmafNegHarmonic)} | {AbRunner.Fmt(s.VmafNegWorstP10)} | {AbRunner.Fmt(s.VmafNegMin)} | {AbRunner.Fmt(s.VmafNegMean)} | {AbRunner.Fmt(s.Xpsnr)} | {Fmt4(s.Ssim)} |");
        }

        var sensitivity = Sensitivity(report).ToList();
        if (sensitivity.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Duyarlılık");
            foreach (var verdict in sensitivity)
                sb.AppendLine($"- {verdict.Competitor}: {(verdict.Sensitive ? "AYRIŞIYOR" : "AYRIŞMIYOR")} — {verdict.Reason}");
        }

        sb.AppendLine();
        sb.AppendLine("Komut satırları");
        foreach (var m in report.Measurements.GroupBy(x => x.Competitor + "|" + x.Settings).Select(g => g.First()))
        {
            sb.AppendLine($"- {m.Competitor} ({m.Settings})");
            sb.AppendLine($"  {m.CommandLine}");
        }

        return sb.ToString();
    }

    public static IEnumerable<SensitivityVerdict> Sensitivity(AbReport report)
    {
        foreach (var group in report.Summaries.GroupBy(s => s.Competitor))
        {
            var ordered = group.OrderBy(s => s.TargetMb).ToList();
            if (ordered.Count < 2) continue;
            var low = ordered[0];
            var high = ordered[^1];
            yield return SensitivityCheck.Evaluate(
                group.Key, low.TargetMb, low.VmafNegHarmonic, high.TargetMb, high.VmafNegHarmonic);
        }
    }

    public static IReadOnlyList<DeviationRow> Deviation(AbReport full, AbReport chunked)
    {
        var rows = new List<DeviationRow>();
        foreach (var fullSummary in full.Summaries)
        {
            var match = chunked.Summaries.FirstOrDefault(s =>
                s.Competitor == fullSummary.Competitor && Math.Abs(s.TargetMb - fullSummary.TargetMb) < 0.0005);
            if (match is null) continue;
            var deviation = fullSummary.VmafNegHarmonic is { } f && match.VmafNegHarmonic is { } c
                ? c - f
                : (double?)null;
            rows.Add(new DeviationRow(fullSummary.Competitor, fullSummary.TargetMb, fullSummary.VmafNegHarmonic, match.VmafNegHarmonic, deviation));
        }
        return rows;
    }

    public static string DeviationTable(IReadOnlyList<DeviationRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| yarışmacı | hedef MB | tam koşum harm | parça tahmini harm | sapma |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var row in rows)
            sb.AppendLine($"| {row.Competitor} | {row.TargetMb.ToString("0.###", CultureInfo.InvariantCulture)} | {AbRunner.Fmt(row.FullScore)} | {AbRunner.Fmt(row.ChunkScore)} | {(row.Deviation is { } d ? d.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) : "yok")} |");
        return sb.ToString();
    }

    private static string Fmt4(double? value)
        => value is { } v && !double.IsInfinity(v) ? v.ToString("0.0000", CultureInfo.InvariantCulture) : "yok";
}
