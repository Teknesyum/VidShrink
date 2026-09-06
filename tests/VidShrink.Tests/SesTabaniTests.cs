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
    public void K11_UzunKaynaktaKucukHedefVideoBitrateFloorunaCakilmiyor()
    {
        var info = Info(1800);
        var options = new PlanOptions { TargetMb = 10, Intent = Intent.Sharing, Codec = CodecPreference.Compatible };

        var result = PlanCalculator.BuildDetailed(info, options, null);

        Assert.True(result.Estimate.ExpectedMb <= 10.5, $"tahminiMB={result.Estimate.ExpectedMb:0.###} hedefi asti (30dk/10MB) - MinVideoBitrateK florou 505/511/520/528'e geri donduyse videoK 48'e cakilir ve tahmini ~15,5 MB'a cikar");
    }

    public static IEnumerable<object[]> VideoKSifirBandi => new[]
    {
        new object[] { 300.0, 0.5 },
        new object[] { 600.0, 1.5 },
        new object[] { 1200.0, 3.0 },
        new object[] { 1800.0, 5.0 },
        new object[] { 3600.0, 10.0 },
        new object[] { 5400.0, 15.0 },
        new object[] { 7200.0, 20.0 }
    };

    [Theory]
    [MemberData(nameof(VideoKSifirBandi))]
    public void K17_SesTabaniButceyiYediginde_VideoBitrateKodlayiciTabaninaYukselir(double durationSeconds, double targetMb)
    {
        var info = Info(durationSeconds);
        var options = new PlanOptions { TargetMb = targetMb, Intent = Intent.Sharing, Codec = CodecPreference.Compatible };

        var result = PlanCalculator.BuildDetailed(info, options, null);
        var plan = result.Plan;
        var runnableK = PlanCalculator.RunnableVideoBitrateK(plan.Width, plan.Height, plan.Fps);

        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);
        Assert.True(plan.VideoBitrateK >= runnableK,
            $"sure={durationSeconds}s target={targetMb}MB: videoK={plan.VideoBitrateK}, kodlayicinin actigi taban {runnableK}k ({plan.Width}x{plan.Height}@{plan.Fps:0.##}). videoK=0 iki geciste 'CRF/CQP is incompatible with 2pass' verip sifir baytlik dosya birakiyor.");
        Assert.Contains(AdviceCode.TargetBelowCodecFloor, result.Advice.Notes);
    }

    [Fact]
    public void K17_YenidenDenemeSifirdaCakilmiyor()
    {
        var info = Info(3600);
        var options = new PlanOptions { TargetMb = 10, Intent = Intent.Sharing, Codec = CodecPreference.Compatible };
        var plan = PlanCalculator.BuildDetailed(info, options, null).Plan;
        var runnableK = PlanCalculator.RunnableVideoBitrateK(plan.Width, plan.Height, plan.Fps);

        var once = PlanCalculator.Correct(plan, 30.0, 10, info.DurationSeconds);
        var sonra = PlanCalculator.Correct(once, 30.0, 10, info.DurationSeconds);

        Assert.True(once.VideoBitrateK >= runnableK, $"1. duzeltme videoK={once.VideoBitrateK}, taban {runnableK}k");
        Assert.True(sonra.VideoBitrateK >= runnableK, $"2. duzeltme videoK={sonra.VideoBitrateK}, taban {runnableK}k - Correct() 0 sabit noktasina duserse ffmpeg hic acilmaz");
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
