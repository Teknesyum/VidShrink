namespace VidShrink.Core;

/// <summary>
/// Tek bir kodlayıcı yoklamasının ölçülen sonucu: gerçekten kodladı mı ve ne kadar sürdü.
/// </summary>
public sealed record EncoderProbeResult(string Codec, bool Succeeded, long ElapsedMs)
{
    public static EncoderProbeResult Missing(string codec) => new(codec, false, 0);
}

/// <summary>Hızlı modun kendiliğinden açılıp açılmama sebebi.</summary>
public enum HardwareVerdictReason
{
    /// <summary>Yoklama koşmadı; karar verilemez.</summary>
    NotProbed,

    /// <summary>Kodlayıcı bulundu ama yoklama kodlaması başarısız oldu ya da zaman aşımına uğradı.</summary>
    ProbeFailed,

    /// <summary>Yoklama geçti ama bütçenin üstünde sürdü; yol sağlıklı değil.</summary>
    ProbeSlow,

    /// <summary>Plan donanım kodlayıcı seçmedi; bu makinede kullanılabilir donanım yolu yok.</summary>
    NoHardwareEncoder,

    /// <summary>Kodlayıcının teslim edebildiği alt bit hızı planın istediğinin üstünde.</summary>
    BitrateFloorTooHigh,

    /// <summary>Ölçümlerin hepsi tuttu.</summary>
    Usable
}

/// <summary>
/// Yoklamanın ölçtüğü sayılardan "hızlı mod varsayılan açık olsun mu" kararını üretir.
/// Karar model adına değil ölçüye dayanır: yoklama kodlaması geçti mi, ne kadar sürdü,
/// seçilen kodlayıcı donanım mı ve planın istediği bit hızı o kodlayıcının gerçekten
/// takip edebildiği alt sınırın üstünde mi.
/// </summary>
public sealed record HardwareVerdict(
    bool EnableFastMode,
    HardwareVerdictReason Reason,
    string Codec,
    long ElapsedMs,
    int RequestedBitrateK,
    int UsableBitrateK)
{
    /// <summary>
    /// Yoklamanın sağlıklı sayıldığı üst süre. Yoklamanın kendi zaman aşımı 4000 ms; bu
    /// makinede av1_nvenc yoklaması 195-236 ms sürüyor. Bütçe ikisinin arasına, ölçülen
    /// sürenin kabaca altı katına konuldu: sürücü içinde geri düşe düşe zar zor tamamlanan
    /// bir yol "çalışıyor" sayılmasın, ölçüm gürültüsü de kararı çevirmesin.
    /// </summary>
    public const long ProbeBudgetMs = 1500;

    /// <summary>Planın istediği bit hızının kodlayıcının takip edebildiği alt sınıra oranı.</summary>
    public double HeadroomRatio => UsableBitrateK <= 0 ? 0 : (double)RequestedBitrateK / UsableBitrateK;

    public static HardwareVerdict NotProbed { get; } =
        new(false, HardwareVerdictReason.NotProbed, string.Empty, 0, 0, 0);

    /// <summary>
    /// <paramref name="probe"/> plan tarafından seçilen kodlayıcının yoklaması,
    /// <paramref name="requestedBitrateK"/> planın o kodlayıcıdan istediği video bit hızı,
    /// kalan üçlü de planın çıkış düzeni.
    /// </summary>
    public static HardwareVerdict Decide(
        EncoderProbeResult probe,
        int requestedBitrateK,
        int width,
        int height,
        double fps)
    {
        var codec = probe.Codec;

        if (!CodecModel.IsHardware(codec))
            return new HardwareVerdict(false, HardwareVerdictReason.NoHardwareEncoder, codec, probe.ElapsedMs, requestedBitrateK, 0);

        if (!probe.Succeeded)
            return new HardwareVerdict(false, HardwareVerdictReason.ProbeFailed, codec, probe.ElapsedMs, requestedBitrateK, 0);

        var usable = CodecModel.UsableBitrateK(codec, width, height, fps);

        if (probe.ElapsedMs > ProbeBudgetMs)
            return new HardwareVerdict(false, HardwareVerdictReason.ProbeSlow, codec, probe.ElapsedMs, requestedBitrateK, usable);

        if (requestedBitrateK <= 0 || requestedBitrateK < usable)
            return new HardwareVerdict(false, HardwareVerdictReason.BitrateFloorTooHigh, codec, probe.ElapsedMs, requestedBitrateK, usable);

        return new HardwareVerdict(true, HardwareVerdictReason.Usable, codec, probe.ElapsedMs, requestedBitrateK, usable);
    }

    /// <summary>
    /// Kararı ayara yazar. Ayarda zaten bir değer varsa dokunmaz: karar bir kez verilir ve
    /// kullanıcı elle değiştirdiyse o karar kalıcıdır. Ayarın değiştiği hâlde true döner.
    /// </summary>
    public bool ApplyTo(UpdateSettings settings)
    {
        if (Reason == HardwareVerdictReason.NotProbed) return false;
        if (settings.FastGpu.HasValue) return false;
        settings.FastGpu = EnableFastMode;
        return true;
    }

    /// <summary>
    /// Donanım değiştiğinde yeniden yoklamanın yolu. Kendiliğinden olmaz: ya
    /// VIDSHRINK_REPROBE_HARDWARE ortam değişkeni verilir ya da ayar dosyasındaki
    /// fastGpu alanı silinir.
    /// </summary>
    public static bool ReprobeRequested()
    {
        var value = Environment.GetEnvironmentVariable("VIDSHRINK_REPROBE_HARDWARE");
        return !string.IsNullOrWhiteSpace(value) && value != "0";
    }
}
