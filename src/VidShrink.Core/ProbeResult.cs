namespace VidShrink.Core;

public sealed record ProbeResult(
    ComplexityProfile Profile,
    IReadOnlyList<WindowQualityMeasurement> QualityMeasurements)
{
    public bool HasQuality => QualityMeasurements.Count > 0;
}
