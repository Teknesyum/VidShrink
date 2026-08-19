using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class WindowSamplingTests
{
    private const string Codec = "libx264";
    private const double SourceFps = 48.0;
    private const double Duration = 52.0;

    private static IReadOnlyList<PacketSample> Profile(params double[] bytesPerSecond)
    {
        var packets = new List<PacketSample>();
        for (var i = 0; i < bytesPerSecond.Length; i++)
            packets.Add(new PacketSample(i + 0.25, (long)bytesPerSecond[i]));
        return packets;
    }

    private static double[] Flat(double value, int seconds)
    {
        var values = new double[seconds];
        Array.Fill(values, value);
        return values;
    }

    private static ComplexityProfile Calibrated(ComplexityProfile profile)
    {
        var signature = new CalibrationSignature
        {
            Codec = Codec,
            Width = 1920,
            Height = 1080,
            Fps = SourceFps,
            Scale = 1.0
        };
        return profile.Calibrate(signature, 23, 0.06, 27, 0.03, SourceFps);
    }

    [Fact]
    public void ComputeWindowBias_FlatProfile_IsOne()
    {
        var bias = ComplexityProbe.ComputeWindowBias(Profile(Flat(1000, 52)), Duration);
        Assert.Equal(1.0, bias, 9);
    }

    [Fact]
    public void ComputeWindowBias_HeavyWindows_MatchesRatio()
    {
        var values = Flat(1000, 52);
        var selected = new HashSet<int>();
        foreach (var start in ComplexityProbe.Windows(Duration))
        {
            var first = (int)Math.Floor(start);
            selected.Add(first);
            selected.Add(first + 1);
        }
        foreach (var index in selected) values[index] = 2000;

        var bias = ComplexityProbe.ComputeWindowBias(Profile(values), Duration);

        var expected = 2000.0 / (values.Sum() / values.Length);
        Assert.Equal(expected, bias, 9);
        Assert.True(bias > 1.0);
    }

    [Fact]
    public void FromProbe_AppliesBiasToReferenceBppf()
    {
        var plain = ComplexityProfile.FromProbe(0.08, 0.05, 6, 288);
        var corrected = ComplexityProfile.FromProbe(0.08, 0.05, 6, 288, 1.25);

        Assert.Equal(0.08 / 1.25, corrected.ReferenceBppf, 9);
        Assert.Equal(1.25, corrected.WindowBias, 9);
        Assert.Equal(0.08, plain.ReferenceBppf, 9);
    }

    [Fact]
    public void FromProbe_BiasOutsideClampRange_IsNotApplied()
    {
        var tooLow = ComplexityProfile.FromProbe(0.08, 0.05, 6, 288, 0.4);
        var tooHigh = ComplexityProfile.FromProbe(0.08, 0.05, 6, 288, 2.5);

        Assert.Equal(0.08, tooLow.ReferenceBppf, 9);
        Assert.Equal(0.08, tooHigh.ReferenceBppf, 9);
        Assert.False(tooLow.WindowBiasKnown);
        Assert.False(tooHigh.WindowBiasKnown);
    }

    [Fact]
    public void ComputeWindowBias_WithoutPacketData_FallsBackToTodaysProfile()
    {
        var bias = ComplexityProbe.ComputeWindowBias(Array.Empty<PacketSample>(), Duration);
        var profile = ComplexityProfile.FromProbe(0.08, 0.05, 6, 288, bias);

        Assert.Equal(0.0, bias, 9);
        var today = ComplexityProfile.FromProbe(0.08, 0.05, 6, 288);
        Assert.Equal(today.ReferenceBppf, profile.ReferenceBppf, 9);
        Assert.Equal(today.DetailExponent, profile.DetailExponent, 9);
        Assert.True(profile.Measured);
        Assert.False(profile.WindowBiasKnown);
    }

    [Fact]
    public void ComputeWindowBias_ShortSource_IsNotMeasured()
    {
        Assert.Equal(0.0, ComplexityProbe.ComputeWindowBias(Profile(Flat(1000, 3)), 3.0), 9);
    }

    [Fact]
    public void ParsePackets_ReadsPtsAndSize()
    {
        var packets = ComplexityProbe.ParsePackets("0.000000,420010\n0.062500,131348\nbroken\n\n0.020833,20934\n");

        Assert.Equal(3, packets.Count);
        Assert.Equal(0.0625, packets[1].PtsSeconds, 6);
        Assert.Equal(20934, packets[2].Size);
    }

    [Fact]
    public void EstimateBand_WithoutBias_DoesNotNarrowToCalibratedBand()
    {
        var withoutBias = Calibrated(ComplexityProfile.FromProbe(0.08, 0.05, 6, 288));
        var withBias = Calibrated(ComplexityProfile.FromProbe(0.08, 0.05, 6, 288, 1.1));

        Assert.True(withoutBias.Calibrated);
        Assert.Equal(0.14, withoutBias.EstimateBand, 9);
        Assert.Equal(0.14, withoutBias.EstimateBandFor(Codec, 1.0, SourceFps), 9);
        Assert.Equal(0.05, withBias.EstimateBand, 9);
        Assert.Equal(0.05, withBias.EstimateBandFor(Codec, 1.0, SourceFps), 9);
    }

    [Fact]
    public void Build_SkipsRateLimitsForSvtAv1()
    {
        var info = new MediaInfo
        {
            FilePath = "in.mp4",
            FileSizeBytes = 830L * 1024 * 1024,
            Width = 1920,
            Height = 1080,
            Fps = SourceFps,
            DurationSeconds = Duration,
            TotalBitrateBps = 20_000_000,
            AudioBitrateBps = 128_000,
            VideoCodec = "h264",
            AudioCodec = "aac",
            AudioChannels = 2
        };

        var av1 = Plan(info, "libsvtav1");
        var x264 = Plan(info, Codec);

        Assert.DoesNotContain("-maxrate", FfmpegArguments.Build(info, av1, "out.mp4", 2, null));
        Assert.DoesNotContain("-bufsize", FfmpegArguments.Build(info, av1, "out.mp4", 1, "log"));
        Assert.Contains("-maxrate", FfmpegArguments.Build(info, x264, "out.mp4", 2, null));
        Assert.False(FfmpegArguments.SupportsRateLimits("libsvtav1"));
        Assert.True(FfmpegArguments.SupportsRateLimits(Codec));
    }

    private static EncodePlan Plan(MediaInfo info, string codec) => new()
    {
        Codec = codec,
        Preset = FfmpegArguments.DefaultPreset(codec),
        Mode = "bitrate",
        VideoBitrateK = 1200,
        AudioCodec = "aac",
        AudioBitrateK = 96,
        AudioChannels = 2,
        Width = info.Width,
        Height = info.Height,
        Fps = info.Fps,
        PixelFormat = "yuv420p"
    };
}
