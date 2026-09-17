using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class SaturationTests
{
    private static EncodePlan Plan(int width = 1920, int height = 818, int videoK = 98, int audioK = 64, int? minHeight = 240) => new()
    {
        Codec = "libsvtav1",
        Mode = "2pass",
        VideoBitrateK = videoK,
        AudioCodec = "aac",
        AudioBitrateK = audioK,
        Width = width,
        Height = height,
        Fps = 24,
        LayoutStepMinHeight = minHeight
    };

    [Fact]
    public void BytesThatHoldWhileTheRequestFallsAreTheEncoderFloor()
    {
        Assert.True(Saturation.AtEncoderFloor(new SizeSample(98, 0.392, true), new SizeSample(29, 0.392, true)));
        Assert.True(Saturation.AtEncoderFloor(new SizeSample(100, 0.137, true), new SizeSample(80, 0.131, true)));
    }

    [Fact]
    public void BytesThatFollowTheRequestAreNotTheFloor()
    {
        Assert.False(Saturation.AtEncoderFloor(new SizeSample(100, 0.137, true), new SizeSample(60, 0.120, true)));
        Assert.False(Saturation.AtEncoderFloor(new SizeSample(100, 0.137, true), new SizeSample(81, 0.137, true)));
        Assert.False(Saturation.AtEncoderFloor(new SizeSample(100, 0.137, false), new SizeSample(60, 0.137, true)));
        Assert.False(Saturation.AtEncoderFloor(new SizeSample(100, 0.137, true), new SizeSample(60, 0.137, false)));
    }

    [Fact]
    public void FloorIsFoundAcrossSmallCorrectStepsFromTheEarliestSample()
    {
        var first = new SizeSample(98, 0.137, true);
        var second = new SizeSample(84, 0.134, true);
        var third = new SizeSample(74, 0.134, true);

        Assert.False(Saturation.AtEncoderFloor(second, third));
        Assert.Null(Saturation.FloorReference(new[] { first }, second));
        Assert.Same(first, Saturation.FloorReference(new[] { first, second }, third));
    }

    [Fact]
    public void BytesThatFollowTheRequestAcrossTheRunHaveNoFloorReference()
    {
        var run = new[] { new SizeSample(98, 0.137, true), new SizeSample(84, 0.118, true) };
        Assert.Null(Saturation.FloorReference(run, new SizeSample(74, 0.104, true)));
        Assert.Null(Saturation.FloorReference(Array.Empty<SizeSample>(), new SizeSample(74, 0.134, true)));
    }

    [Fact]
    public void FloorStepScalesTheFrameBySquareRootOfTheMiss()
    {
        var plan = Plan();
        var stepped = Saturation.StepLayoutDown(plan, new SizeSample(98, 0.137, true), new SizeSample(29, 0.134, true), 0.117);

        Assert.NotNull(stepped);
        var scale = Math.Sqrt(0.9 * 0.117 / 0.134);
        Assert.Equal((int)Math.Round(818 * scale / 2) * 2, stepped!.Height);
        Assert.True(stepped.Width < plan.Width);
        Assert.Equal(0, stepped.Width % 2);
        Assert.Equal(plan.AudioBitrateK, stepped.AudioBitrateK);
        Assert.Contains(stepped.ReasonCodes, n => n.Code == ReasonCode.RetryScaled);
    }

    [Fact]
    public void FloorStepStopsAtTheMinimumHeightThenTakesTheAudio()
    {
        var clamped = Saturation.StepLayoutDown(Plan(height: 300, width: 704, minHeight: 280), new SizeSample(98, 1.0, true), new SizeSample(29, 1.0, true), 0.1);
        Assert.Equal(280, clamped!.Height);

        var atMin = Saturation.StepLayoutDown(Plan(height: 280, width: 656, minHeight: 280), new SizeSample(98, 1.0, true), new SizeSample(29, 1.0, true), 0.1);
        Assert.Equal(280, atMin!.Height);
        Assert.Equal(32, atMin.AudioBitrateK);

        var noLayoutStep = Saturation.StepLayoutDown(Plan(minHeight: null), new SizeSample(98, 1.0, true), new SizeSample(29, 1.0, true), 0.1);
        Assert.Equal(818, noLayoutStep!.Height);
        Assert.Equal(32, noLayoutStep.AudioBitrateK);
    }

    [Fact]
    public void AudioStepReachesThePlannedStreamsThatFfmpegReads()
    {
        var plan = Plan(minHeight: null, audioK: 128);
        var encoded = new AudioTrack("0:1", TrackAction.Encode, "aac", 128, 2, "eng");
        var copied = new AudioTrack("0:2", TrackAction.Copy, "ac3", 192, null, "tur");
        plan.Streams = new StreamPlan(OutputContainer.Mkv, "0:0", new[] { encoded, copied }, Array.Empty<SubtitleTrack>(), Array.Empty<string>(), Array.Empty<StreamNote>(), 320, new StreamRequest(KeepAllTracks: true));

        var stepped = Saturation.StepLayoutDown(plan, new SizeSample(98, 1.0, true), new SizeSample(29, 1.0, true), 0.1);

        Assert.Equal(64, stepped!.Streams!.Audio[0].BitrateK);
        Assert.Same(copied, stepped.Streams.Audio[1]);
        Assert.Equal(256, stepped.NonVideoK);
        Assert.Contains("64k", stepped.Streams.OutputArguments());
        Assert.DoesNotContain("128k", stepped.Streams.OutputArguments());
        Assert.Equal(128, plan.Streams.Audio[0].BitrateK);
        Assert.Contains("128k", plan.Streams.OutputArguments());
    }

    [Fact]
    public void DeadYieldBudgetLeavesRoomForEveryPlannedSideStream()
    {
        var plan = Plan(videoK: 116, audioK: 0);
        plan.AudioCodec = null;
        plan.Streams = new StreamPlan(OutputContainer.Mkv, "0:0", Array.Empty<AudioTrack>(), Array.Empty<SubtitleTrack>(), Array.Empty<string>(), Array.Empty<StreamNote>(), 200, StreamRequest.Default);
        plan.VideoBitrateK = PlanCalculator.VideoBudgetK(0.5, 200, 10);
        Assert.Null(Saturation.StepDeadYield(plan, 0.012, 0.5, 10, new[] { new SizeSample(plan.VideoBitrateK, 0.012, false) }));

        plan.Streams = null;
        Assert.NotNull(Saturation.StepDeadYield(plan, 0.012, 0.5, 10, new[] { new SizeSample(plan.VideoBitrateK, 0.012, false) }));
    }

    [Fact]
    public void FloorStepWithNothingLeftToDropReturnsNull()
    {
        var plan = Plan(minHeight: null, audioK: 24);
        Assert.Null(Saturation.StepLayoutDown(plan, new SizeSample(98, 1.0, true), new SizeSample(29, 1.0, true), 0.1));

        plan.AudioCodec = null;
        plan.AudioBitrateK = 128;
        Assert.Null(Saturation.StepLayoutDown(plan, new SizeSample(98, 1.0, true), new SizeSample(29, 1.0, true), 0.1));
    }

    [Fact]
    public void YieldUnderHalfIsDeadAndTheMeasuredYieldIgnoresIt()
    {
        Assert.True(Saturation.YieldIsDead(0.03));
        Assert.False(Saturation.YieldIsDead(0.78));
        Assert.False(Saturation.YieldIsDead(0.5));
        Assert.False(Saturation.YieldIsDead(null));

        var plan = Plan(videoK: 1000, audioK: 0);
        plan.AudioCodec = null;
        var requestedMb = 1.0 / PlanCalculator.RawEncoderYield(plan, 1.0, 10)!.Value;
        Assert.Equal(0.1, PlanCalculator.RawEncoderYield(plan, requestedMb * 0.1, 10)!.Value, 3);
        Assert.Null(PlanCalculator.MeasuredEncoderEfficiency(plan, requestedMb * 0.1, 10));
        Assert.Equal(0.78, PlanCalculator.MeasuredEncoderEfficiency(plan, requestedMb * 0.78, 10)!.Value, 3);
    }

    [Fact]
    public void UnderBandStepBisectsTheCornerOrDoublesUnderTheBudget()
    {
        Assert.Equal(187, Saturation.UnderBandBitrateK(116, 300, 5000));
        Assert.Equal(232, Saturation.UnderBandBitrateK(116, null, 5000));
        Assert.Equal(150, Saturation.UnderBandBitrateK(116, null, 150));
        Assert.Equal(232, Saturation.UnderBandBitrateK(116, 100, 5000));
        Assert.Equal(300, Saturation.UnderBandBitrateK(300, null, 250));
    }

    [Fact]
    public void DeadYieldStepUsesOnlyAnOverCeilingSampleAboveTheCurrentRequest()
    {
        var plan = Plan(videoK: 116, audioK: 0);
        plan.AudioCodec = null;
        var samples = new[] { new SizeSample(300, 0.589, true), new SizeSample(116, 0.012, false) };

        var bisected = Saturation.StepDeadYield(plan, 0.012, 0.5, 10, samples);
        Assert.Equal(187, bisected!.VideoBitrateK);

        var doubled = Saturation.StepDeadYield(plan, 0.012, 0.5, 10, new[] { new SizeSample(300, 0.589, false), new SizeSample(116, 0.012, false) });
        Assert.Equal(232, doubled!.VideoBitrateK);

        plan.VideoBitrateK = PlanCalculator.VideoBudgetK(0.5, 0, 10);
        Assert.Null(Saturation.StepDeadYield(plan, 0.012, 0.5, 10, new[] { new SizeSample(plan.VideoBitrateK, 0.012, false) }));
    }

    [Fact]
    public void FillUnderHalfOfTheTargetIsSaturated()
    {
        Assert.True(Saturation.FillIsSaturated(0.033, 1.0));
        Assert.False(Saturation.FillIsSaturated(0.76, 1.0));
        Assert.False(Saturation.FillIsSaturated(0.5, 1.0));
    }
}
