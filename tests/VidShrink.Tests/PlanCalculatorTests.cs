using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class PlanCalculatorTests
{
    [Fact]
    public void CorrectionReservesFixedAudioBeforeScalingVideo()
    {
        var plan = new EncodePlan
        {
            Mode = "crf",
            Crf = 23,
            VideoBitrateK = 1800,
            AudioBitrateK = 128,
            AudioCodec = "aac",
            Width = 1920,
            Height = 1080,
            Fps = 30,
            Codec = "libx264",
            Preset = "slow"
        };

        var corrected = PlanCalculator.Correct(plan, actualMb: 30, targetMb: 20, durationSeconds: 120);

        Assert.Equal(EncodeMode.TwoPass, corrected.ModeEnum);
        Assert.Null(corrected.Crf);
        Assert.True(corrected.VideoBitrateK < 1128, $"Expected audio-aware correction below the old whole-file proportional result, got {corrected.VideoBitrateK}k.");
        Assert.True(PlanCalculator.EstimatedMb(corrected, 120) <= 20 * 0.94 + 0.05);
    }
}
