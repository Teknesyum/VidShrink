namespace VidShrink.Core;

/// <summary>
/// Tek bir kodlayıcı geçişinin ölçülen maliyeti.
///
/// <paramref name="WallMs"/> geçişin duvar saati, <paramref name="VideoMs"/> kodlanan
/// görüntünün kendi süresi. Yazılım yolunda geçiş tek iş parçacığına kısılır; orada
/// duvar saati doğrudan çekirdek-saniyedir, çünkü aynı anda tek çekirdek çalışır ve
/// kodlayıcı o çekirdeği doldurur.
///
/// <paramref name="CpuMs"/> işletim sisteminin sürece yazdığı işlemci zamanı
/// (<c>TotalProcessorTime</c>). Kayıt olarak duruyor, karar buna bağlı değil: bu sayaç
/// her makinede doğru okumuyor. Sapma her koşuda yeniden ölçülür ve
/// <see cref="PerformanceCheckResult.CpuAccountingFactor"/> ile taşınır. Ölçülen
/// değerler burada değil, o koşumun ham günlüğünde durur.
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
    /// Görüntüyü gerçek zamanlı takip edebilmek için istenen çekirdek sayısı: tek
    /// çekirdekle bir saniyelik görüntüyü işlemek kaç saniye sürüyor. Kayıt aracı da
    /// oyunla aynı anda, gerçek zamanlı kodlar; oyundan çalınan şey budur.
    ///
    /// Yalnız işlemciye bağlı (CPU-bound) bir geçiş için anlamlıdır. Donanım
    /// kodlayıcısında iş ekran kartında koştuğu için duvar saati çekirdek maliyeti
    /// değildir; orada bu sayı kullanılmaz.
    /// </summary>
    public double RealtimeCores => VideoMs <= 0 ? 0 : WallMs / VideoMs;

    /// <summary>Geçişin gerçek zamanın kaç katı hızda tamamlandığı.</summary>
    public double RealtimeFactor => WallMs <= 0 ? 0 : VideoMs / WallMs;

    /// <summary>
    /// İşletim sisteminin yazdığı işlemci zamanının duvar saatine oranı. Gözlem
    /// olarak duruyor; tek iş parçacıklı, işlemciye bağlı bir geçişte 1'e yakın
    /// çıkması beklenir, uzaksa makinenin işlemci zamanı sayacı güvenilmezdir.
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

    /// <summary>Donanım kodlayıcısı çalışıyor.</summary>
    HardwarePathWorks,

    /// <summary>
    /// Donanım yolunun süresi iş parçacığı sayısından etkilenmiyor: iş işlemcide
    /// değil. Ölçüldü — kodlayıcıya bir iş parçacığı da verilse, serbest de bırakılsa
    /// geçiş aynı sürüyor.
    /// </summary>
    HardwareNotCpuBound,

    /// <summary>
    /// Donanım yolunun işlemci maliyeti <b>ölçülemedi</b>. Duvar saati işlemciye
    /// bağlı olmadığı için çekirdek maliyeti sayılamaz, süreç işlemci zamanı sayacı
    /// da bu makinede güvenilir değil. Ölçülmeyen bir sayı uydurulmaz.
    /// </summary>
    HardwareCpuCostNotMeasured,

    /// <summary>Donanım yolunun gerçek zamana göre hızı — kaç kat gerçek zaman.</summary>
    HardwarePipelineHeadroom,

    /// <summary>Yazılım yolunun gerçek zamanlı çekirdek talebi.</summary>
    SoftwareRealtimeCost,

    /// <summary>Yazılım yolu bir çekirdeği tam istiyor; kayıt sırasında oyundan çekirdek alır.</summary>
    SoftwareCostsCores,

    /// <summary>Yazılım yolu bir çekirdeğin altında kaldı.</summary>
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
    double RealtimeFactor = 0,
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

    /// <summary>Donanım kodlayıcısı çalışıyor ve işlemciye bağlı değil: kodlama işlemcinin dışında.</summary>
    HardwareOffload,

    /// <summary>Donanım yok, ama yazılım kodlaması bir çekirdeğin altında kalıyor.</summary>
    SoftwareLightLoad,

    /// <summary>Donanım yok ve yazılım kodlaması en az bir çekirdeği tam istiyor.</summary>
    SoftwareHeavyLoad
}

public sealed record PerformanceCheckResult(
    RecordingImpact Impact,
    string HardwareCodec,
    string SoftwareCodec,
    double HardwarePipelineRealtimeFactor,
    double SoftwareRealtimeCores,
    int LogicalCores,
    long ElapsedMs,
    double CpuAccountingFactor,
    IReadOnlyList<PerformanceFinding> Findings)
{
    public static PerformanceCheckResult NotMeasured { get; } = new(
        RecordingImpact.Unknown, string.Empty, string.Empty, 0, 0, 0, 0, 0,
        new[] { new PerformanceFinding(PerformanceFindingCode.NotMeasured) });

    /// <summary>Makinenin işlemci zamanı sayacı karar verilebilecek kadar sağlam mı.</summary>
    public bool CpuAccountingTrustworthy =>
        CpuAccountingFactor > 0 && CpuAccountingFactor <= PerformanceCheck.CpuAccountingTolerance;
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
    /// Yazılım kodlamasının "ağır" sayıldığı sınır: bir tam çekirdek.
    ///
    /// Sınır mutlak, makinenin çekirdek sayısının oranı değil. Oyunun kare üreten
    /// yolu birkaç iş parçacığından ibarettir ve makine büyüdükçe genişlemez; kayıt
    /// kodlayıcısı da oyunun tamamıyla değil o yolla yarışır. Bir tam çekirdeği sürekli
    /// isteyen kodlayıcı, kaydın süresi boyunca bir çekirdeği <b>sahiplenmek</b>
    /// zorundadır ve o çekirdek oyunun elinden çıkar; altında kalan maliyet oyunun
    /// boşluklarına serpiştirilebilir.
    ///
    /// Çekirdek sayısının oranı olarak kurulmuş bir eşik bunun tersini yapıyordu:
    /// makine büyüdükçe eşik gevşiyor, 16 çekirdekli bir makinede dört çekirdek yiyen
    /// bir kodlayıcı "hafif" sayılıyordu.
    /// </summary>
    public const double HeavyLoadCores = 1.0;

    /// <summary>
    /// İşlemci zamanı sayacının güvenilir sayıldığı üst sınır. Tek iş parçacıklı,
    /// işlemciye bağlı bir yükte sayaç duvar saatiyle örtüşmeli; bu kadar kat
    /// uzaklaşıyorsa sayaç bozuktur ve okunan işlemci zamanları öyle bildirilir.
    /// </summary>
    public const double CpuAccountingTolerance = 1.5;

    /// <summary>
    /// Donanım geçişinin iş parçacığı sayısından etkilenmediğine karar verilen bant.
    /// Tek iş parçacıklı geçişle serbest geçiş bu oranın içinde kalıyorsa iş
    /// işlemcide değildir.
    /// </summary>
    public const double NotCpuBoundTolerance = 1.25;

    /// <summary>
    /// <paramref name="software"/> tek iş parçacığına kısılmış yazılım kodlaması,
    /// <paramref name="hardwareSingleThread"/> ve <paramref name="hardwareFreeThreads"/>
    /// donanım kodlamasının bir iş parçacıklı ve serbest geçişleri — ikisinin farkı
    /// işin işlemcide olup olmadığını söyler.
    /// <paramref name="cpuAccountingFactor"/> makinenin işlemci zamanı sayacının kaç kat
    /// eksik okuduğu (1 = sağlam, 0 = ölçülemedi).
    /// <paramref name="hardwareEncoderPresent"/> ffmpeg listesinde donanım kodlayıcısı
    /// olup olmadığı — listede varken kodlaması başarısız olmak, hiç olmamaktan
    /// başka bir cevaptır.
    /// </summary>
    public static PerformanceCheckResult Evaluate(
        EncoderCost? software,
        EncoderCost? hardwareSingleThread,
        EncoderCost? hardwareFreeThreads,
        int logicalCores,
        long elapsedMs,
        long budgetMs,
        bool hardwareEncoderPresent,
        double cpuAccountingFactor = 0)
    {
        var sw = Usable(software);
        var hwSingle = Usable(hardwareSingleThread);
        var hwFree = Usable(hardwareFreeThreads);

        var cores = Math.Max(1, logicalCores);
        var budgetSpent = budgetMs > 0 && elapsedMs > budgetMs;

        if (sw is null && hwSingle is null)
        {
            var bos = new List<PerformanceFinding> { new(PerformanceFindingCode.NotMeasured) };
            if (budgetSpent)
                bos.Add(new PerformanceFinding(
                    PerformanceFindingCode.BudgetExhausted, WallMs: elapsedMs, BudgetMs: budgetMs));
            return new PerformanceCheckResult(
                RecordingImpact.Unknown, string.Empty, string.Empty, 0, 0, cores, elapsedMs,
                cpuAccountingFactor, bos);
        }

        var findings = new List<PerformanceFinding>();

        var notCpuBound = false;
        if (hwSingle is not null)
        {
            findings.Add(new PerformanceFinding(
                PerformanceFindingCode.HardwarePathWorks, Codec: hwSingle.Codec));

            if (hwFree is not null && hwFree.WallMs > 0)
            {
                var spread = (double)hwSingle.WallMs / hwFree.WallMs;
                notCpuBound = spread <= NotCpuBoundTolerance;
                if (notCpuBound)
                    findings.Add(new PerformanceFinding(
                        PerformanceFindingCode.HardwareNotCpuBound,
                        Codec: hwSingle.Codec,
                        Factor: spread));
            }

            findings.Add(new PerformanceFinding(
                PerformanceFindingCode.HardwarePipelineHeadroom,
                Codec: hwSingle.Codec,
                RealtimeFactor: hwSingle.RealtimeFactor,
                WallMs: hwSingle.WallMs));

            if (notCpuBound)
                findings.Add(new PerformanceFinding(
                    PerformanceFindingCode.HardwareCpuCostNotMeasured,
                    Codec: hwSingle.Codec,
                    Factor: cpuAccountingFactor));
        }
        else if (hardwareEncoderPresent)
        {
            findings.Add(new PerformanceFinding(
                PerformanceFindingCode.HardwareEncoderFailed,
                Codec: hardwareSingleThread?.Codec ?? string.Empty));
        }
        else
        {
            findings.Add(new PerformanceFinding(PerformanceFindingCode.NoHardwareEncoder));
        }

        if (sw is not null)
            findings.Add(new PerformanceFinding(
                PerformanceFindingCode.SoftwareRealtimeCost,
                Codec: sw.Codec,
                RealtimeCores: sw.RealtimeCores,
                CpuMs: sw.CpuMs,
                WallMs: sw.WallMs,
                LogicalCores: cores));

        var impact = RecordingImpact.Unknown;

        if (hwSingle is not null && notCpuBound)
        {
            impact = RecordingImpact.HardwareOffload;
        }
        else if (sw is not null)
        {
            var heavy = sw.RealtimeCores >= HeavyLoadCores;
            impact = heavy ? RecordingImpact.SoftwareHeavyLoad : RecordingImpact.SoftwareLightLoad;
            findings.Add(new PerformanceFinding(
                heavy ? PerformanceFindingCode.SoftwareCostsCores : PerformanceFindingCode.SoftwareCostIsSmall,
                Codec: sw.Codec,
                RealtimeCores: sw.RealtimeCores,
                LogicalCores: cores));
        }

        if (cpuAccountingFactor <= 0 || cpuAccountingFactor > CpuAccountingTolerance)
            findings.Add(new PerformanceFinding(
                PerformanceFindingCode.CpuAccountingUnreliable,
                Factor: cpuAccountingFactor));

        if (budgetSpent)
            findings.Add(new PerformanceFinding(
                PerformanceFindingCode.BudgetExhausted,
                WallMs: elapsedMs,
                BudgetMs: budgetMs));

        if (impact == RecordingImpact.Unknown)
            findings.Insert(0, new PerformanceFinding(PerformanceFindingCode.NotMeasured));

        return new PerformanceCheckResult(
            impact,
            hwSingle?.Codec ?? string.Empty,
            sw?.Codec ?? string.Empty,
            hwSingle?.RealtimeFactor ?? 0,
            sw?.RealtimeCores ?? 0,
            cores,
            elapsedMs,
            cpuAccountingFactor,
            findings);
    }

    private static EncoderCost? Usable(EncoderCost? cost)
        => cost is { Succeeded: true, VideoMs: > 0, WallMs: > 0 } ? cost : null;
}
