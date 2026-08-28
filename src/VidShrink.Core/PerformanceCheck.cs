namespace VidShrink.Core;

/// <summary>
/// Tek bir kodlayıcının ölçülen maliyeti. Ölçüm <b>tek iş parçacığına</b> kısılmış bir
/// geçişten gelir: o zaman duvar saati doğrudan çekirdek-saniyeye eşittir, çünkü aynı
/// anda tek bir çekirdek çalışıyordur.
///
/// <paramref name="CpuMs"/> işletim sisteminin sürece yazdığı işlemci zamanı
/// (<c>TotalProcessorTime</c>). Kayıt olarak duruyor ama karar buna bağlı değil:
/// bu sayı her makinede güvenilir değil, ölçüldü ve bu makinede gerçeğin kabaca
/// altıda birini gösteriyor. <see cref="PerformanceCheckResult.CpuAccountingFactor"/>
/// o sapmayı taşır.
/// </summary>
public sealed record EncoderCost(
    string Codec,
    bool Succeeded,
    double CpuMs,
    long WallMs,
    double VideoMs)
{
    public static EncoderCost Missing(string codec) => new(codec, false, 0, 0, 0);

    public bool IsHardware => CodecModel.IsHardware(Codec);

    /// <summary>
    /// Bu kodlayıcının görüntüyü gerçek zamanlı takip edebilmek için isteyeceği
    /// çekirdek sayısı: tek çekirdekle bir saniyelik görüntüyü işlemek kaç saniye
    /// sürüyor. Kayıt aracı da oyunla aynı anda, gerçek zamanlı kodlar; oyundan
    /// çalınan şey tam olarak budur.
    ///
    /// Bir üst sınırdır: kodlayıcı birden çok çekirdeğe yayıldığında toplam iş aynı
    /// kalır, yalnız daha kısa sürede biter.
    /// </summary>
    public double RealtimeCores => VideoMs <= 0 ? 0 : WallMs / VideoMs;

    /// <summary>
    /// İşletim sisteminin yazdığı işlemci zamanının duvar saatine oranı. Gözlem
    /// olarak duruyor; tek iş parçacıklı geçişte 1'e yakın çıkması beklenir, uzaksa
    /// makinenin işlemci zamanı sayacı güvenilmezdir.
    /// </summary>
    public double ReportedCpuParallelism => WallMs <= 0 ? 0 : CpuMs / WallMs;
}

/// <summary>Kullanıcıya gösterilecek her cümlenin kodu. Metin arayüz katmanında.</summary>
public enum PerformanceFindingCode
{
    /// <summary>Ölçüm koşmadı ya da hiçbir kodlayıcı sonuç vermedi.</summary>
    NotMeasured,

    /// <summary>Bu makinede hiç donanım kodlayıcısı yok.</summary>
    NoHardwareEncoder,

    /// <summary>Donanım kodlayıcısı listede var ama kodlaması başarısız oldu.</summary>
    HardwareEncoderFailed,

    /// <summary>Donanım kodlayıcısı çalışıyor; kodlama ekran kartındaki ayrı birime düşüyor.</summary>
    HardwarePathWorks,

    /// <summary>Donanım yolunun gerçek zamanlı çekirdek talebi.</summary>
    HardwareRealtimeCost,

    /// <summary>Yazılım yolunun gerçek zamanlı çekirdek talebi.</summary>
    SoftwareRealtimeCost,

    /// <summary>İki yol da ölçüldü; donanım yolu işlemciden şu kadar kat az yiyor.</summary>
    HardwareSavesCpu,

    /// <summary>Yazılım yolu makinenin çekirdeklerinin kayda değer bir kısmını istiyor.</summary>
    SoftwareCostsCores,

    /// <summary>Yazılım yolu ucuz kaldı; kare hızı düşüşünün sebebi kodlama değil.</summary>
    SoftwareCostIsSmall,

    /// <summary>
    /// Makinenin işlemci zamanı sayacı güvenilmez. Karar bu sayaca dayanmıyor, ama
    /// gösterilen işlemci zamanları bu kadar kat eksik okunuyor.
    /// </summary>
    CpuAccountingUnreliable,

    /// <summary>Bütçe dolduğu için ölçümün bir bacağı alınamadı.</summary>
    BudgetExhausted
}

/// <summary>
/// Bir bulgu ve ona bağlı sayılar. <see cref="ReasonNote"/> kalıbı: metin yok,
/// kod ve ölçülen sayı var.
/// </summary>
public sealed record PerformanceFinding(
    PerformanceFindingCode Code,
    string Codec = "",
    double RealtimeCores = 0,
    double SoftwareRealtimeCores = 0,
    double HardwareRealtimeCores = 0,
    double Factor = 0,
    double CpuMs = 0,
    long WallMs = 0,
    int LogicalCores = 0,
    long BudgetMs = 0);

/// <summary>Kayıt sırasında kodlamanın oyuna ne yaptığı — tek kelimelik karar.</summary>
public enum RecordingImpact
{
    /// <summary>Ölçüm yok, karar yok.</summary>
    Unknown,

    /// <summary>Donanım kodlayıcısı çalışıyor: kodlama işlemcinin dışında.</summary>
    HardwareOffload,

    /// <summary>Donanım yok, ama yazılım kodlamasının maliyeti küçük.</summary>
    SoftwareLightLoad,

    /// <summary>Donanım yok ve yazılım kodlaması çekirdeklerin kayda değer kısmını yiyor.</summary>
    SoftwareHeavyLoad
}

public sealed record PerformanceCheckResult(
    RecordingImpact Impact,
    string HardwareCodec,
    string SoftwareCodec,
    double HardwareRealtimeCores,
    double SoftwareRealtimeCores,
    int LogicalCores,
    long ElapsedMs,
    double CpuAccountingFactor,
    IReadOnlyList<PerformanceFinding> Findings)
{
    public static PerformanceCheckResult NotMeasured { get; } = new(
        RecordingImpact.Unknown, string.Empty, string.Empty, 0, 0, 0, 0, 1,
        new[] { new PerformanceFinding(PerformanceFindingCode.NotMeasured) });

    /// <summary>Donanım yolu yazılım yolundan kaç kat az çekirdek istiyor. İkisi de yoksa 0.</summary>
    public double CpuSavingFactor => HardwareRealtimeCores <= 0 || SoftwareRealtimeCores <= 0
        ? 0
        : SoftwareRealtimeCores / HardwareRealtimeCores;
}

/// <summary>
/// Ölçülen kodlayıcı maliyetlerinden "bu makinede kayıt sırasında kodlama nereye
/// düşüyor ve oyuna ne kalıyor" kararını üretir. Saf: süreç çalıştırmaz, ölçmez,
/// yalnız verilen sayıları okur.
///
/// VidShrink kayıt yapmaz; bu yüzden ölçülen şey kayıt aracı değil, bu makinede
/// kodlamanın nereye düştüğüdür. Kayıt aracı da aynı kodlayıcıları kullandığı için
/// cevap oradan çıkar.
/// </summary>
public static class PerformanceCheck
{
    /// <summary>
    /// Yazılım kodlamasının "ağır" sayıldığı sınır: makinenin mantıksal çekirdek
    /// sayısının dörtte biri. Oyunun kendisi zaten birden çok çekirdek kullanıyor;
    /// çekirdeklerin dörtte birini sürekli meşgul tutan bir kodlayıcı, oyunun
    /// kare üreten iş parçacığıyla aynı çekirdeğe düşmeye başlar. Altında kalan
    /// maliyet, oyunla kodlayıcının aynı anda yer bulabildiği bölge.
    /// </summary>
    public const double HeavyLoadCoreFraction = 0.25;

    /// <summary>
    /// İşlemci zamanı sayacının güvenilir sayıldığı bant. Tek iş parçacıklı bir
    /// yükte sayaç duvar saatiyle örtüşmeli; bu kadar kat uzaklaşıyorsa makinenin
    /// sayacı bozuktur ve okunan işlemci zamanları kullanıcıya öyle bildirilir.
    /// </summary>
    public const double CpuAccountingTolerance = 1.5;

    /// <summary>
    /// <paramref name="costs"/> ölçülen kodlayıcılar, <paramref name="logicalCores"/>
    /// makinenin mantıksal çekirdek sayısı, <paramref name="elapsedMs"/> ölçümün
    /// toplam duvar saati, <paramref name="budgetMs"/> bütçe,
    /// <paramref name="cpuAccountingFactor"/> makinenin işlemci zamanı sayacının kaç
    /// kat eksik okuduğu (1 = sağlam).
    /// <paramref name="hardwareEncoderPresent"/> ffmpeg listesinde donanım kodlayıcısı
    /// olup olmadığı — listede varken kodlaması başarısız olmak, hiç olmamaktan
    /// başka bir cevaptır.
    /// </summary>
    public static PerformanceCheckResult Evaluate(
        IReadOnlyList<EncoderCost> costs,
        int logicalCores,
        long elapsedMs,
        long budgetMs,
        bool hardwareEncoderPresent,
        double cpuAccountingFactor = 1)
    {
        if (costs.Count == 0)
            return PerformanceCheckResult.NotMeasured;

        var hardware = costs.FirstOrDefault(c => c.IsHardware && c.Succeeded && c.VideoMs > 0);
        var software = costs.FirstOrDefault(c => !c.IsHardware && c.Succeeded && c.VideoMs > 0);
        var cores = Math.Max(1, logicalCores);

        var findings = new List<PerformanceFinding>();

        if (hardware is not null)
        {
            findings.Add(new PerformanceFinding(
                PerformanceFindingCode.HardwarePathWorks,
                Codec: hardware.Codec));
            findings.Add(new PerformanceFinding(
                PerformanceFindingCode.HardwareRealtimeCost,
                Codec: hardware.Codec,
                RealtimeCores: hardware.RealtimeCores,
                CpuMs: hardware.CpuMs,
                WallMs: hardware.WallMs,
                LogicalCores: cores));
        }
        else if (hardwareEncoderPresent)
        {
            findings.Add(new PerformanceFinding(
                PerformanceFindingCode.HardwareEncoderFailed,
                Codec: costs.FirstOrDefault(c => c.IsHardware)?.Codec ?? string.Empty));
        }
        else
        {
            findings.Add(new PerformanceFinding(PerformanceFindingCode.NoHardwareEncoder));
        }

        if (software is not null)
        {
            findings.Add(new PerformanceFinding(
                PerformanceFindingCode.SoftwareRealtimeCost,
                Codec: software.Codec,
                RealtimeCores: software.RealtimeCores,
                CpuMs: software.CpuMs,
                WallMs: software.WallMs,
                LogicalCores: cores));
        }

        if (hardware is not null && software is not null && hardware.RealtimeCores > 0)
        {
            findings.Add(new PerformanceFinding(
                PerformanceFindingCode.HardwareSavesCpu,
                SoftwareRealtimeCores: software.RealtimeCores,
                HardwareRealtimeCores: hardware.RealtimeCores,
                Factor: software.RealtimeCores / hardware.RealtimeCores,
                LogicalCores: cores));
        }

        var impact = RecordingImpact.Unknown;

        if (hardware is not null)
        {
            impact = RecordingImpact.HardwareOffload;
        }
        else if (software is not null)
        {
            var heavy = software.RealtimeCores >= cores * HeavyLoadCoreFraction;
            impact = heavy ? RecordingImpact.SoftwareHeavyLoad : RecordingImpact.SoftwareLightLoad;
            findings.Add(new PerformanceFinding(
                heavy ? PerformanceFindingCode.SoftwareCostsCores : PerformanceFindingCode.SoftwareCostIsSmall,
                Codec: software.Codec,
                RealtimeCores: software.RealtimeCores,
                LogicalCores: cores));
        }

        if (cpuAccountingFactor > CpuAccountingTolerance)
        {
            findings.Add(new PerformanceFinding(
                PerformanceFindingCode.CpuAccountingUnreliable,
                Factor: cpuAccountingFactor));
        }

        if (budgetMs > 0 && elapsedMs > budgetMs)
        {
            findings.Add(new PerformanceFinding(
                PerformanceFindingCode.BudgetExhausted,
                WallMs: elapsedMs,
                BudgetMs: budgetMs));
        }

        if (impact == RecordingImpact.Unknown)
            findings.Insert(0, new PerformanceFinding(PerformanceFindingCode.NotMeasured));

        return new PerformanceCheckResult(
            impact,
            hardware?.Codec ?? string.Empty,
            software?.Codec ?? string.Empty,
            hardware?.RealtimeCores ?? 0,
            software?.RealtimeCores ?? 0,
            cores,
            elapsedMs,
            cpuAccountingFactor,
            findings);
    }
}
