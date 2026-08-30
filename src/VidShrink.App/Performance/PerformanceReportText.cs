using System.Globalization;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App.Performance;

/// <summary>Ekrandaki tek bir ölçüm satırı: solda ad, sağda ölçülen değer.</summary>
internal readonly record struct PerformanceFact(string Label, string Value);

/// <summary>
/// Ölçümün cümlesini kuran taraf. <see cref="PerformanceCheck"/> yalnız kod ve sayı
/// döndürür; okunacak satır burada anahtarla seçilir.
///
/// Ayrım kasıtlı: cümle ölçümün yanında dursaydı biri değişince öteki sessizce eskirdi.
/// Buradaki her satırın karşılığı bir <see cref="PerformanceFindingCode"/> değeridir ve
/// eşlemede serbest geçiş (<c>default</c>) yoktur — enum'a yeni bir değer eklendiğinde
/// derleyici değil, ölçüm konuşur.
///
/// <para>Cümlenin kendisi burada yazılı değil: <c>Locales/&lt;dil&gt;/performance.json</c>
/// içindedir ve bu sınıf yalnız hangi anahtarın hangi koda düştüğüne karar verir.</para>
///
/// <para>Manşet <see cref="PerformanceCheckResult.Impact"/> üzerinden kurulmaz.
/// Ölçüldü: makine meşgulken <c>Impact</c>, donanım kodlayıcısı çalışırken bile yazılım
/// dalına kayabiliyor. Aynı sonuç nesnesi o durumda da
/// <see cref="PerformanceFindingCode.HardwarePathWorks"/> taşımaya devam ediyor, yani
/// doğru bilgi bulgularda. Manşet bu yüzden bulgulardan okunur.</para>
/// </summary>
internal static class PerformanceReportText
{
    /// <summary>
    /// Panelin kalıcı sınır cümlesi. Her sonucun metninde son satır olarak durur, ölçüm
    /// hiç koşmamışken bile. VidShrink kayıt yapmaz; ölçtüğü şey bu makinede kodlamanın
    /// maliyetidir, kayıt aracının davranışı değil.
    /// </summary>
    internal static string Boundary => Strings.Get("performance.boundary");

    internal static IReadOnlyList<string> Describe(PerformanceCheckResult result)
    {
        var lines = new List<string> { Headline(result) };
        foreach (var finding in result.Findings) lines.Add(Line(finding));
        lines.Add(Boundary);
        return lines;
    }

    /// <summary>
    /// Manşet. Donanımın varlığı <see cref="PerformanceFindingCode.HardwarePathWorks"/>,
    /// işlemciye bağlı olmadığı <see cref="PerformanceFindingCode.HardwareNotCpuBound"/>,
    /// yazılımın maliyeti <see cref="PerformanceFindingCode.SoftwareCostsCores"/> ile
    /// <see cref="PerformanceFindingCode.SoftwareCostIsSmall"/> üzerinden okunur.
    /// </summary>
    internal static string Headline(PerformanceCheckResult result)
    {
        var hardware = Has(result, PerformanceFindingCode.HardwarePathWorks);
        var offloaded = Has(result, PerformanceFindingCode.HardwareNotCpuBound);
        var heavy = Has(result, PerformanceFindingCode.SoftwareCostsCores);
        var light = Has(result, PerformanceFindingCode.SoftwareCostIsSmall);

        if (hardware && offloaded) return Strings.Get("performance.headline.hardware-offloaded");
        if (hardware) return Strings.Get("performance.headline.hardware");
        if (heavy) return Strings.Get("performance.headline.software-heavy");
        if (light) return Strings.Get("performance.headline.software-light");

        return Strings.Get("performance.headline.not-enough");
    }

    /// <summary>
    /// Bulgu kodunun ekrandaki karşılığı. Her kod ayrı ayrı karşılanır; serbest geçiş
    /// yok, çünkü karşılıksız kalan bir kod ekranda sessizce kaybolurdu.
    /// </summary>
    internal static string Line(PerformanceFinding finding)
    {
        switch (finding.Code)
        {
            case PerformanceFindingCode.NotMeasured:
                return Strings.Get("performance.line.not-measured");

            case PerformanceFindingCode.NoHardwareEncoder:
                return Strings.Get("performance.line.no-hardware");

            case PerformanceFindingCode.HardwareEncoderFailed:
                return Strings.Get("performance.line.hardware-failed");

            case PerformanceFindingCode.HardwarePathWorks:
                return Strings.Get("performance.line.hardware-works");

            case PerformanceFindingCode.HardwareNotCpuBound:
                return Strings.Get("performance.line.hardware-not-cpu-bound", Number(finding.Factor, "0.00"));

            case PerformanceFindingCode.HardwareCpuCostNotMeasured:
                return Strings.Get("performance.line.hardware-cost-unknown");

            case PerformanceFindingCode.HardwarePipelineHeadroom:
                return Strings.Get("performance.line.hardware-headroom", Number(finding.RealtimeFactor, "0.#"));

            case PerformanceFindingCode.SoftwareRealtimeCost:
                return Strings.Get(
                    "performance.line.software-realtime",
                    Number(finding.RealtimeCores, "0.00"),
                    finding.LogicalCores.ToString(CultureInfo.InvariantCulture));

            case PerformanceFindingCode.SoftwareCostsCores:
                return Strings.Get("performance.line.software-costs-cores", Number(finding.RealtimeCores, "0.00"));

            case PerformanceFindingCode.SoftwareCostIsSmall:
                return Strings.Get("performance.line.software-small", Number(finding.RealtimeCores, "0.00"));

            case PerformanceFindingCode.CpuAccountingUnreliable:
                return Strings.Get("performance.line.cpu-unreliable", Number(finding.Factor, "0.00"));

            case PerformanceFindingCode.BudgetExhausted:
                return Strings.Get(
                    "performance.line.budget-exhausted",
                    finding.WallMs.ToString(CultureInfo.InvariantCulture),
                    finding.BudgetMs.ToString(CultureInfo.InvariantCulture));
        }

        throw new ArgumentOutOfRangeException(nameof(finding), finding.Code, "Bulgu kodunun ekranda karşılığı yok.");
    }

    /// <summary>
    /// Sonucun sayıları, ölçüldükleri birimle. Hız oranı çekirdek diye etiketlenmez:
    /// <see cref="PerformanceCheckResult.SoftwareRealtimeCores"/> çekirdek,
    /// <see cref="PerformanceCheckResult.HardwarePipelineRealtimeFactor"/> gerçek zamanın
    /// katıdır. Ölçülmemiş bir alan satır açmaz.
    /// </summary>
    internal static IReadOnlyList<PerformanceFact> Facts(PerformanceCheckResult result)
    {
        var facts = new List<PerformanceFact>();

        if (!string.IsNullOrEmpty(result.HardwareCodec))
            facts.Add(new PerformanceFact(Strings.Get("performance.fact.hardware-encoder"), result.HardwareCodec));

        if (result.HardwarePipelineRealtimeFactor > 0)
            facts.Add(new PerformanceFact(
                Strings.Get("performance.fact.hardware-pipeline"),
                Strings.Get("performance.value.realtime", Number(result.HardwarePipelineRealtimeFactor, "0.#"))));

        if (!string.IsNullOrEmpty(result.SoftwareCodec))
            facts.Add(new PerformanceFact(Strings.Get("performance.fact.software-encoder"), result.SoftwareCodec));

        if (result.SoftwareRealtimeCores > 0)
            facts.Add(new PerformanceFact(
                Strings.Get("performance.fact.software-cost"),
                Strings.Get("performance.value.cores", Number(result.SoftwareRealtimeCores, "0.00"))));

        if (result.LogicalCores > 0)
            facts.Add(new PerformanceFact(
                Strings.Get("performance.fact.logical-cores"),
                result.LogicalCores.ToString(CultureInfo.InvariantCulture)));

        if (result.ElapsedMs > 0)
            facts.Add(new PerformanceFact(
                Strings.Get("performance.fact.measured-in"),
                Strings.Get("performance.value.ms", result.ElapsedMs.ToString(CultureInfo.InvariantCulture))));

        return facts;
    }

    private static bool Has(PerformanceCheckResult result, PerformanceFindingCode code)
        => result.Findings.Any(finding => finding.Code == code);

    private static string Number(double value, string format)
        => value.ToString(format, CultureInfo.InvariantCulture);
}
