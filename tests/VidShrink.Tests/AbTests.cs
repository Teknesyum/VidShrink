using System.Globalization;
using VidShrink.Ab;

namespace VidShrink.Tests;

public sealed class ColorGateAbTests
{
    private static readonly ColorSignature HdrReference = new("bt2020", "smpte2084", "bt2020nc", "yuv420p10le", true);
    private static readonly ColorSignature Bt709Sdr = new("bt709", "bt709", "bt709", "yuv420p", false);

    [Fact]
    public void SameHdrSpaceIsComparedDirectly()
    {
        var decision = ColorGate.Decide(HdrReference, HdrReference with { PixelFormat = "yuv420p10le" });

        Assert.Equal(ColorGateKind.Direct, decision.Kind);
        Assert.True(decision.Measurable);
    }

    [Fact]
    public void SdrOutputAgainstHdrReferenceMovesTheReferenceAndSaysSo()
    {
        var decision = ColorGate.Decide(HdrReference, Bt709Sdr);

        Assert.Equal(ColorGateKind.ReferenceTransformed, decision.Kind);
        Assert.Equal("SDR uzayında karşılaştırma — HDR kaybı hariç", decision.Label);
    }

    [Fact]
    public void UntaggedOutputIsRefusedInsteadOfAssumed()
    {
        var untagged = new ColorSignature(null, null, null, "yuv420p", false);

        var decision = ColorGate.Decide(HdrReference, untagged);

        Assert.Equal(ColorGateKind.Rejected, decision.Kind);
        Assert.False(decision.Measurable);
        Assert.Contains("etiket", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UntaggedReferenceIsRefusedInsteadOfAssumed()
    {
        var untaggedReference = new ColorSignature(null, null, null, "yuv420p", false);

        var decision = ColorGate.Decide(untaggedReference, Bt709Sdr);

        Assert.Equal(ColorGateKind.Rejected, decision.Kind);
    }

    [Fact]
    public void TwoDifferentHdrTransfersAreRefused()
    {
        var hlg = HdrReference with { Transfer = "arib-std-b67" };

        var decision = ColorGate.Decide(HdrReference, hlg);

        Assert.Equal(ColorGateKind.Rejected, decision.Kind);
    }

    [Fact]
    public void SdrReferenceCannotBeRaisedToAnHdrOutput()
    {
        var decision = ColorGate.Decide(Bt709Sdr, HdrReference);

        Assert.Equal(ColorGateKind.Rejected, decision.Kind);
    }

    [Fact]
    public void TwoSdrSpacesThatDisagreeAreRefused()
    {
        var smpte170m = new ColorSignature("smpte170m", "bt709", "smpte170m", "yuv420p", false);

        var decision = ColorGate.Decide(Bt709Sdr, smpte170m);

        Assert.Equal(ColorGateKind.Rejected, decision.Kind);
    }

    [Fact]
    public void HdrReferenceOnlyDropsToBt709Sdr()
    {
        var wideSdr = new ColorSignature("bt2020", "bt709", "bt2020nc", "yuv420p", false);

        var decision = ColorGate.Decide(HdrReference, wideSdr);

        Assert.Equal(ColorGateKind.Rejected, decision.Kind);
    }

    [Fact]
    public void MatchingFrameRatesAreComparableAndMismatchedOnesAreNot()
    {
        Assert.True(RateGate.Check(60, 60).Comparable);
        Assert.False(RateGate.Check(60, 30).Comparable);
    }
}

public sealed class SizeParityAbTests
{
    [Fact]
    public void DeltaIsMeasuredAgainstTheBaseline()
    {
        var parity = SizeParityCheck.Evaluate(100_000_000, 97_000_000, 2.0);

        Assert.Equal(-3.0, parity.DeltaPercent, 9);
        Assert.False(parity.Equal);
        Assert.Equal("eş boyut değil", parity.Stamp);
    }

    [Fact]
    public void ExactlyOnToleranceStillCountsAsEqual()
    {
        var parity = SizeParityCheck.Evaluate(100_000_000, 102_000_000, 2.0);

        Assert.Equal(2.0, parity.DeltaPercent, 9);
        Assert.True(parity.Equal);
        Assert.Equal("", parity.Stamp);
    }

    [Fact]
    public void OneByteOverToleranceIsStamped()
    {
        var parity = SizeParityCheck.Evaluate(100_000_000, 102_000_001, 2.0);

        Assert.False(parity.Equal);
        Assert.Equal("eş boyut değil", parity.Stamp);
    }

    [Fact]
    public void UndershootOutsideToleranceIsStampedToo()
    {
        var parity = SizeParityCheck.Evaluate(100_000_000, 97_500_000, 2.0);

        Assert.False(parity.Equal);
    }

    [Fact]
    public void ZeroBaselineIsRefused()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SizeParityCheck.Evaluate(0, 10, 2.0));
}

public sealed class ChunkAggregateAbTests
{
    private static ChunkQuality Part(string name, int weight, double value, double p10, double min)
        => new(name, weight, value, value, p10, min, value, value / 100.0);

    [Fact]
    public void HarmonicMeanIsHarmonicNotArithmetic()
    {
        var combined = ChunkAggregate.Combine(new[]
        {
            Part("a", 100, 40, 30, 10),
            Part("b", 100, 60, 20, 25)
        });

        Assert.NotNull(combined.Harmonic);
        Assert.Equal(48.0, combined.Harmonic!.Value, 9);
    }

    [Fact]
    public void HarmonicMeanFollowsFrameWeights()
    {
        var combined = ChunkAggregate.Combine(new[]
        {
            Part("a", 300, 40, 30, 10),
            Part("b", 100, 60, 20, 25)
        });

        Assert.Equal(400.0 / (300.0 / 40.0 + 100.0 / 60.0), combined.Harmonic!.Value, 9);
    }

    [Fact]
    public void CombinedP10IsTheWorstChunkNotTheAverage()
    {
        var combined = ChunkAggregate.Combine(new[]
        {
            Part("a", 100, 40, 30, 10),
            Part("b", 100, 60, 20, 25)
        });

        Assert.Equal(20.0, combined.WorstP10!.Value, 9);
    }

    [Fact]
    public void CombinedMinimumIsTheLowestFrameNotTheLowestP10()
    {
        var combined = ChunkAggregate.Combine(new[]
        {
            Part("a", 100, 40, 30, 10),
            Part("b", 100, 60, 20, 25)
        });

        Assert.Equal(10.0, combined.Min!.Value, 9);
    }

    [Fact]
    public void ArithmeticMeanFollowsFrameWeights()
    {
        var combined = ChunkAggregate.Combine(new[]
        {
            Part("a", 300, 40, 30, 10),
            Part("b", 100, 60, 20, 25)
        });

        Assert.Equal(45.0, combined.Mean!.Value, 9);
        Assert.Equal(400, combined.TotalWeight);
    }

    [Fact]
    public void AMissingChunkScoreMakesTheCombinationMissing()
    {
        var combined = ChunkAggregate.Combine(new[]
        {
            Part("a", 100, 40, 30, 10),
            new ChunkQuality("b", 100, 60, null, 20, 25, 60, 0.6)
        });

        Assert.Null(combined.Harmonic);
        Assert.NotNull(combined.Mean);
    }

    [Fact]
    public void ZeroWeightIsRefused()
        => Assert.Throws<ArgumentException>(() => ChunkAggregate.Combine(new[] { Part("a", 0, 40, 30, 10) }));

    [Fact]
    public void EmptyInputIsRefused()
        => Assert.Throws<ArgumentException>(() => ChunkAggregate.Combine(Array.Empty<ChunkQuality>()));
}

public sealed class SensitivityAbTests
{
    [Fact]
    public void AClearGapCountsAsSensitive()
    {
        var verdict = SensitivityCheck.Evaluate("vidshrink", 60, 40.0, 600, 41.5);

        Assert.True(verdict.Sensitive);
        Assert.Equal(1.5, verdict.Separation!.Value, 9);
    }

    [Fact]
    public void ExactlyOnTheThresholdCountsAsSensitive()
    {
        var verdict = SensitivityCheck.Evaluate("vidshrink", 60, 40.0, 600, 41.0);

        Assert.True(verdict.Sensitive);
    }

    [Fact]
    public void AFlatMeterIsCalledOutInsteadOfReported()
    {
        var verdict = SensitivityCheck.Evaluate("vidshrink", 60, 14.86, 600, 14.67);

        Assert.False(verdict.Sensitive);
        Assert.Contains("duyarsız", verdict.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MovingTheWrongWayIsNotSensitivity()
    {
        var verdict = SensitivityCheck.Evaluate("vidshrink", 60, 45.0, 600, 40.0);

        Assert.False(verdict.Sensitive);
    }

    [Fact]
    public void AMissingScoreIsNotProof()
    {
        var verdict = SensitivityCheck.Evaluate("vidshrink", 60, null, 600, 41.5);

        Assert.False(verdict.Sensitive);
        Assert.Null(verdict.Separation);
    }

    [Fact]
    public void TargetsMustActuallyDiffer()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SensitivityCheck.Evaluate("vidshrink", 600, 40.0, 600, 41.5));
}

public sealed class HandBrakeArgumentsAbTests
{
    [Fact]
    public void TheBitrateFillsTheRequestedMebibytes()
    {
        const double targetMb = 60;
        const double duration = 1036.17;

        var kbps = HandBrakeCompetitor.VideoBitrateKbps(targetMb, duration);
        var producedMb = kbps * 1000.0 * duration / 8.0 / 1024.0 / 1024.0;

        Assert.InRange(producedMb, targetMb * 0.98, targetMb);
    }

    [Fact]
    public void TheBitrateScalesWithDuration()
    {
        var full = HandBrakeCompetitor.VideoBitrateKbps(60, 1000);
        var half = HandBrakeCompetitor.VideoBitrateKbps(30, 1000);

        Assert.Equal(full / 2.0, half, 0);
    }

    [Fact]
    public void AudioIsOffAndTheFrameRateIsPinnedToTheSource()
    {
        var args = HandBrakeCompetitor.BuildArguments("in.mkv", "out.mkv", 4321, 60);

        Assert.Equal("none", ValueAfter(args, "-a"));
        Assert.Equal("60", ValueAfter(args, "-r"));
        Assert.Contains("--cfr", args);
        Assert.Equal("4321", ValueAfter(args, "-b"));
        Assert.Equal("x265", ValueAfter(args, "-e"));
    }

    [Fact]
    public void AutoCropIsDisabledSoTheGeometryStaysComparable()
    {
        var args = HandBrakeCompetitor.BuildArguments("in.mkv", "out.mkv", 4321, 60);

        Assert.Equal("0:0:0:0", ValueAfter(args, "--crop"));
        Assert.Contains("--non-anamorphic", args);
    }

    private static string ValueAfter(IReadOnlyList<string> args, string flag)
    {
        var index = args.ToList().IndexOf(flag);
        Assert.True(index >= 0 && index + 1 < args.Count, $"{flag} yok");
        return args[index + 1];
    }
}

public sealed class GeometryGateAbTests
{
    [Fact]
    public void SameShapeAtLowerResolutionStaysComparable()
    {
        var decision = GeometryGate.Check(1920, 1080, 1152, 648);

        Assert.True(decision.Comparable);
    }

    [Fact]
    public void EightRowsCroppedOffTheHeightIsRefused()
    {
        var decision = GeometryGate.Check(1920, 1080, 1920, 1072);

        Assert.False(decision.Comparable);
        Assert.Contains("1920x1072", decision.Reason);
    }

    [Fact]
    public void ThePaddedDirectionIsRefusedToo()
    {
        var decision = GeometryGate.Check(1920, 1080, 1920, 1440);

        Assert.False(decision.Comparable);
    }

    [Fact]
    public void DriftInsideToleranceIsAccepted()
    {
        var accepted = GeometryGate.Check(1000, 1000, 1002, 1000);
        var refused = GeometryGate.Check(1000, 1000, 1010, 1000);

        Assert.True(accepted.Comparable);
        Assert.False(refused.Comparable);
    }

    [Fact]
    public void ZeroSizedInputIsAProgrammingErrorNotAVerdict()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GeometryGate.Check(1920, 0, 1920, 1080));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeometryGate.Check(1920, 1080, 0, 1080));
    }
}

public sealed class DeviationAbTests
{
    [Fact]
    public void DeviationIsTheChunkEstimateMinusTheFullRun()
    {
        var full = Report("tam", 46.0);
        var chunked = Report("parca", 44.5);

        var rows = Reporting.Deviation(full, chunked);

        var row = Assert.Single(rows);
        Assert.Equal(-1.5, row.Deviation!.Value, 9);
        Assert.Contains("-1.50", Reporting.DeviationTable(rows));
    }

    private static AbReport Report(string mode, double harmonic)
        => new(
            "kaynak.mp4", mode, 2.0, "test", "2026-09-02 00:00:00",
            new[] { 60.0 },
            Array.Empty<Measurement>(),
            new[]
            {
                new CompetitorSummary("vidshrink", 60.0, 1000, true, true, "aynı", harmonic, harmonic, harmonic, harmonic, 30.0, 0.9,
                    Array.Empty<string>())
            });
}

public sealed class AbSettingsAbTests
{
    [Fact]
    public void ChunkModeAndTargetsAreRead()
    {
        var settings = AbSettings.Parse(
            new[] { "--kaynak", "kaynak.mp4", "--hedef-mb", "60,600", "--parca", "--tolerans", "1.5" },
            Path.GetTempPath());

        Assert.True(settings.ChunkMode);
        Assert.Equal(new[] { 60.0, 600.0 }, settings.TargetsMb);
        Assert.Equal(1.5, settings.TolerancePercent);
        Assert.Equal(AbSettings.DefaultCompetitors, settings.Competitors);
    }

    [Fact]
    public void TheSdrCompetitorIsKnownButNotRunByDefault()
    {
        Assert.Contains("vidshrink-sdr", AbSettings.KnownCompetitors);
        Assert.DoesNotContain("vidshrink-sdr", AbSettings.DefaultCompetitors);
    }

    [Fact]
    public void AnUnknownCompetitorIsRefused()
        => Assert.Throws<ArgumentException>(() => AbSettings.Parse(
            new[] { "--kaynak", "k.mp4", "--hedef-mb", "60", "--yarismaci", "ffmpeg" },
            Path.GetTempPath()));

    [Fact]
    public void TargetsAreParsedInvariantOfTheCurrentCulture()
    {
        var settings = AbSettings.Parse(
            new[] { "--kaynak", "k.mp4", "--hedef-mb", "60.5" },
            Path.GetTempPath());

        Assert.Equal(60.5, settings.TargetsMb[0]);
        Assert.Equal("60.5", settings.TargetsMb[0].ToString(CultureInfo.InvariantCulture));
    }
}
