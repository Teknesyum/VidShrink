namespace VidShrink.Ab;

public sealed record SensitivityVerdict(
    string Competitor,
    double LowTargetMb,
    double HighTargetMb,
    double? LowScore,
    double? HighScore,
    double? Separation,
    double Threshold,
    bool Sensitive,
    string Reason);

public static class SensitivityCheck
{
    public const double MinimumSeparation = 1.0;

    public static SensitivityVerdict Evaluate(
        string competitor,
        double lowTargetMb,
        double? lowScore,
        double highTargetMb,
        double? highScore,
        double threshold = MinimumSeparation)
    {
        if (highTargetMb <= lowTargetMb)
            throw new ArgumentOutOfRangeException(nameof(highTargetMb), "Büyük hedef küçük hedeften büyük olmalı.");

        if (lowScore is not { } low || highScore is not { } high)
            return new SensitivityVerdict(competitor, lowTargetMb, highTargetMb, lowScore, highScore, null, threshold, false,
                "Ölçü alınamadı; duyarlılık kanıtlanmadı.");

        var separation = high - low;
        if (separation < threshold)
            return new SensitivityVerdict(competitor, lowTargetMb, highTargetMb, low, high, separation, threshold, false,
                $"Hedef boyut {lowTargetMb:0.###} MB'den {highTargetMb:0.###} MB'ye çıkarken ölçü yalnız {separation:0.00} puan ayrıştı; eşik {threshold:0.00}. Düzenek duyarsız.");

        return new SensitivityVerdict(competitor, lowTargetMb, highTargetMb, low, high, separation, threshold, true,
            $"Hedef boyut {lowTargetMb:0.###} MB'den {highTargetMb:0.###} MB'ye çıkarken ölçü {separation:0.00} puan ayrıştı; eşik {threshold:0.00}.");
    }
}
