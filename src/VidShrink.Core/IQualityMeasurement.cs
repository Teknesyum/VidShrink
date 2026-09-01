namespace VidShrink.Core;

public sealed record WindowQualityMeasurement(
    double StartSeconds,
    double? VmafNegMean,
    double? VmafNegHarmonic,
    double? VmafNegP10,
    bool Comparable,
    long ElapsedMilliseconds,
    string? Message = null);

public interface IQualityMeasurement
{
    bool IsAvailable { get; }
    Task<WindowQualityMeasurement?> MeasureWindowAsync(
        string referencePath, string samplePath, double referenceStartSeconds,
        double durationSeconds, CancellationToken ct);
}
