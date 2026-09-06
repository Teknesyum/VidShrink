using System.Linq;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class SesTabaniTests
{
    private static MediaInfo Info(double durationSeconds, int channels = 2, long audioBps = 128_000, double sourceMb = 500, bool hasAudio = true) => new()
    {
        FilePath = "sample.mkv",
        FileSizeBytes = (long)(sourceMb * 1024 * 1024),
        DurationSeconds = durationSeconds,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = (long)(sourceMb * 8_388_608 / durationSeconds),
        AudioCodec = hasAudio ? "aac" : null,
        AudioBitrateBps = hasAudio ? audioBps : 0,
        AudioChannels = hasAudio ? channels : 0
    };

    public static IEnumerable<object[]> EskiKoluTetikleyenAralik => new[]
    {
        new object[] { 60.0, 0.5 },
        new object[] { 120.0, 1.0 },
        new object[] { 300.0, 2.0 },
        new object[] { 600.0, 5.0 },
        new object[] { 1200.0, 10.0 },
        new object[] { 1800.0, 15.0 },
        new object[] { 3600.0, 25.0 },
        new object[] { 5400.0, 35.0 },
        new object[] { 7200.0, 45.0 }
    };

    [Theory]
    [MemberData(nameof(EskiKoluTetikleyenAralik))]
    public void K2_EskiKodunSesiTamamenDusurduguAraliktaSesHalaVar(double durationSeconds, double targetMb)
    {
        var info = Info(durationSeconds);
        var options = new PlanOptions { TargetMb = targetMb, Intent = Intent.Sharing, Codec = CodecPreference.Compatible };

        var result = PlanCalculator.BuildDetailed(info, options, null);

        Assert.True(result.Plan.AudioBitrateK > 0, $"sure={durationSeconds}s target={targetMb}MB icin audioK sifir cikti");
        Assert.NotNull(result.Plan.AudioCodec);
        Assert.DoesNotContain(result.Advice.Notes, n => n.ToString() == "AudioDropped");
    }

    [Fact]
    public void K6_SesTabaniYirmiDortKbpsAltinaDusmez()
    {
        var info = Info(60, audioBps: 128_000);
        var options = new PlanOptions { TargetMb = 0.5, Intent = Intent.Sharing, Codec = CodecPreference.Compatible };

        var result = PlanCalculator.BuildDetailed(info, options, null);

        Assert.True(result.Plan.AudioBitrateK >= 24, $"audioK={result.Plan.AudioBitrateK}, taban 24 olmali");
    }

    [Fact]
    public void K4_KaynaktaSesYoksaCiktiDaSessizKalirVeUyariUretilmez()
    {
        var info = Info(60, hasAudio: false);
        var options = new PlanOptions { TargetMb = 0.4, Intent = Intent.Sharing, Codec = CodecPreference.Compatible };

        var result = PlanCalculator.BuildDetailed(info, options, null);

        Assert.Equal(0, result.Plan.AudioBitrateK);
        Assert.Null(result.Plan.AudioChannels);
        Assert.DoesNotContain(result.Advice.Notes, n => n.ToString().Contains("Audio"));
    }

    [Fact]
    public void K5_AudioDroppedArtikUretilmiyor()
    {
        var codes = Enum.GetNames(typeof(AdviceCode));
        Assert.DoesNotContain("AudioDropped", codes);
    }

    [Fact]
    public void ManuelSifirlamaHalaCiktiyiSessizBirakiyor()
    {
        var info = Info(600);
        var options = new PlanOptions
        {
            TargetMb = 5,
            Intent = Intent.Sharing,
            Codec = CodecPreference.Compatible,
            AudioChannels = AudioChannelOverride.None
        };

        var result = PlanCalculator.BuildDetailed(info, options, null);

        Assert.Equal(0, result.Plan.AudioBitrateK);
        Assert.Null(result.Plan.AudioChannels);
    }
}
