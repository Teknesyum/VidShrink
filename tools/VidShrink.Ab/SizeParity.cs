namespace VidShrink.Ab;

public sealed record SizeParity(
    long BaselineBytes,
    long CandidateBytes,
    double DeltaPercent,
    double TolerancePercent,
    bool Equal)
{
    public string Stamp => Equal ? "" : SizeParityCheck.NotEqualStamp;
}

public static class SizeParityCheck
{
    public const string NotEqualStamp = "eş boyut değil";
    public const double DefaultTolerancePercent = 2.0;

    public static SizeParity Evaluate(long baselineBytes, long candidateBytes, double tolerancePercent = DefaultTolerancePercent)
    {
        if (baselineBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(baselineBytes), "Karşılaştırma tabanı sıfır ya da eksi olamaz.");
        if (tolerancePercent < 0)
            throw new ArgumentOutOfRangeException(nameof(tolerancePercent), "Tolerans eksi olamaz.");

        var delta = (candidateBytes - baselineBytes) / (double)baselineBytes * 100.0;
        return new SizeParity(baselineBytes, candidateBytes, delta, tolerancePercent, Math.Abs(delta) <= tolerancePercent);
    }
}
