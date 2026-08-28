using System.Globalization;
using VidShrink.Core;

namespace VidShrink.App.Performance;

/// <summary>Ekrandaki tek bir ölçüm satırı: solda ad, sağda ölçülen değer.</summary>
internal readonly record struct PerformanceFact(string Label, string Value);

/// <summary>
/// Ölçümün cümlesini kuran taraf. <see cref="PerformanceCheck"/> yalnız kod ve sayı
/// döndürür; okunacak satır burada yazılır.
///
/// Ayrım kasıtlı: cümle ölçümün yanında dursaydı biri değişince öteki sessizce eskirdi.
/// Buradaki her satırın karşılığı bir <see cref="PerformanceFindingCode"/> değeridir ve
/// eşlemede serbest geçiş (<c>default</c>) yoktur — enum'a yeni bir değer eklendiğinde
/// derleyici değil, ölçüm konuşur.
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
    internal const string Boundary =
        "VidShrink does not capture video. What is measured here is what encoding costs on this machine, not what a capture tool does with it — its own encoder setting is the first place to look.";

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

        if (hardware && offloaded)
            return "This machine has a working hardware encoder and that pass does not lean on the processor.";

        if (hardware)
            return "This machine has a working hardware encoder, but whether the processor carries that pass was not measured here.";

        if (heavy)
            return "No working hardware encoder was found, and software encoding wants a whole processor core to keep up with realtime.";

        if (light)
            return "No working hardware encoder was found, but software encoding stays under one processor core at realtime.";

        return "There is not enough measured here to answer the question.";
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
                return "Nothing has been measured on this machine yet.";

            case PerformanceFindingCode.NoHardwareEncoder:
                return "No hardware video encoder is available here, so encoding runs on the processor.";

            case PerformanceFindingCode.HardwareEncoderFailed:
                return "A hardware encoder is listed on this machine, but its test encode failed.";

            case PerformanceFindingCode.HardwarePathWorks:
                return "Hardware encoding works on this machine.";

            case PerformanceFindingCode.HardwareNotCpuBound:
                return "The hardware pass does not lean on the processor: giving it one thread or leaving the threads free took the same time, within "
                       + Number(finding.Factor, "0.00") + "×.";

            case PerformanceFindingCode.HardwareCpuCostNotMeasured:
                return "The processor cost of the hardware pass was not measured, so it must not be read as free.";

            case PerformanceFindingCode.HardwarePipelineHeadroom:
                return $"The hardware pass ran at {Number(finding.RealtimeFactor, "0.#")}× realtime.";

            case PerformanceFindingCode.SoftwareRealtimeCost:
                return $"Software encoding wants {Number(finding.RealtimeCores, "0.00")} cores to keep up with realtime, on "
                       + $"{finding.LogicalCores} logical cores.";

            case PerformanceFindingCode.SoftwareCostsCores:
                return $"That is a whole core or more ({Number(finding.RealtimeCores, "0.00")} cores), so software encoding owns one core for as long as it runs.";

            case PerformanceFindingCode.SoftwareCostIsSmall:
                return $"That stays under one core ({Number(finding.RealtimeCores, "0.00")} cores), so it can be spread into the gaps left by other work.";

            case PerformanceFindingCode.CpuAccountingUnreliable:
                return $"The processor time counter on this machine is not dependable ({Number(finding.Factor, "0.00")}× against the wall clock), so processor times are shown but nothing is decided from them.";

            case PerformanceFindingCode.BudgetExhausted:
                return $"The measurement budget ran out ({finding.WallMs} ms of {finding.BudgetMs} ms), so one leg of the check is missing.";
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
            facts.Add(new PerformanceFact("Hardware encoder", result.HardwareCodec));

        if (result.HardwarePipelineRealtimeFactor > 0)
            facts.Add(new PerformanceFact(
                "Hardware pipeline",
                Number(result.HardwarePipelineRealtimeFactor, "0.#") + "× realtime"));

        if (!string.IsNullOrEmpty(result.SoftwareCodec))
            facts.Add(new PerformanceFact("Software encoder", result.SoftwareCodec));

        if (result.SoftwareRealtimeCores > 0)
            facts.Add(new PerformanceFact(
                "Software cost",
                Number(result.SoftwareRealtimeCores, "0.00") + " cores"));

        if (result.LogicalCores > 0)
            facts.Add(new PerformanceFact("Logical cores", result.LogicalCores.ToString(CultureInfo.InvariantCulture)));

        if (result.ElapsedMs > 0)
            facts.Add(new PerformanceFact("Measured in", result.ElapsedMs.ToString(CultureInfo.InvariantCulture) + " ms"));

        return facts;
    }

    private static bool Has(PerformanceCheckResult result, PerformanceFindingCode code)
        => result.Findings.Any(finding => finding.Code == code);

    private static string Number(double value, string format)
        => value.ToString(format, CultureInfo.InvariantCulture);
}
