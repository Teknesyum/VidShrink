using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class SosyalOnAyarTests
{
    private sealed class HepsiCalisiyor : IEncoderAvailability
    {
        public bool HasEncoder(string name) => true;
        public bool WorksAsEncoder(string codec) => true;
        public EncoderProbeState EncoderState(string codec) => EncoderProbeState.Working;
    }

    private static MediaInfo Kaynak() => new()
    {
        FilePath = "social.mp4",
        FileSizeBytes = 300L * 1024 * 1024,
        DurationSeconds = 30,
        Width = 1920,
        Height = 1080,
        Fps = 60,
        VideoCodec = "h264",
        TotalBitrateBps = 80_000_000
    };

    private static EncodePlan Plan(string codec, Intent intent, SpeedMode speed)
        => PlanCalculator.BuildDetailed(Kaynak(), new PlanOptions
        {
            TargetMb = 25,
            Intent = intent,
            SpeedMode = speed,
            LockedCodec = codec
        }, null, new HepsiCalisiyor()).Plan;

    [Theory]
    [InlineData("libsvtav1", Intent.SocialMedia, SpeedMode.Quality, "4")]
    [InlineData("libsvtav1", Intent.Sharing, SpeedMode.Quality, "6")]
    [InlineData("libsvtav1", Intent.Archive, SpeedMode.Quality, "6")]
    [InlineData("libsvtav1", Intent.SocialMedia, SpeedMode.Fast, "6")]
    [InlineData("libx265", Intent.SocialMedia, SpeedMode.Quality, "slow")]
    [InlineData("libx264", Intent.SocialMedia, SpeedMode.Quality, "slow")]
    public void SvtPreset4YalnizSosyalMedyaKalitesinde(string codec, Intent intent, SpeedMode speed, string beklenen)
    {
        var plan = Plan(codec, intent, speed);

        Assert.Equal(codec, plan.Codec);
        Assert.Equal(beklenen, plan.Preset);

        var args = FfmpegArguments.Build(Kaynak(), plan, "out.mp4", 2, null);
        var i = args.IndexOf("-preset");
        Assert.True(i >= 0);
        Assert.Equal(beklenen, args[i + 1]);
    }
}
