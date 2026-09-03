using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class KestirimPlanTests
{
    private static MediaInfo SampleInfo() => new()
    {
        FilePath = "parca.mkv",
        FileSizeBytes = 100L * 1024 * 1024,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 60,
        VideoCodec = "hevc",
        TotalBitrateBps = 14_000_000,
        AudioCodec = "eac3",
        AudioBitrateBps = 192_000,
        AudioChannels = 6
    };

    private static ComplexityProfile Profile(double referenceBppf) => new()
    {
        ReferenceBppf = referenceBppf,
        Measured = true
    };

    private static PlanOptions Options() => new()
    {
        TargetMb = 20,
        Intent = Intent.Sharing,
        Codec = CodecPreference.Compatible,
        AllowResolutionDrop = true,
        AllowFpsDrop = true,
        HdrPolicy = HdrPolicy.Preserve,
        FillPolicy = FillPolicy.FillTarget,
        SpeedMode = SpeedMode.Quality
    };

    [Fact]
    public void RequiredBppfIsDirectlyProportionalToReferenceBppf()
    {
        const string codec = "libx264";
        const double scale = 1.0, fps = 60, sourceFps = 60;

        var kucuk = Profile(0.02).RequiredBppf(codec, scale, fps, sourceFps);
        var buyuk = Profile(0.04).RequiredBppf(codec, scale, fps, sourceFps);

        Assert.True(kucuk > 0);
        Assert.Equal(2.0, buyuk / kucuk, precision: 9);
    }

    [Fact]
    public void HigherReferenceBppfNeverPicksALargerResolutionThanLower()
    {
        var info = SampleInfo();
        var options = Options();
        const double taban = 0.05;

        var tabanPlan = PlanCalculator.BuildDetailed(info, options, Profile(taban), null).Plan;
        var buyutulmusPlan = PlanCalculator.BuildDetailed(info, options, Profile(taban * 2.0), null).Plan;
        var kucultulmusPlan = PlanCalculator.BuildDetailed(info, options, Profile(taban * 0.5), null).Plan;

        Assert.True(buyutulmusPlan.Height <= tabanPlan.Height,
            $"x2,00 ({buyutulmusPlan.Height}p) taban ({tabanPlan.Height}p)'dan yuksek cozunurluk secti.");
        Assert.True(tabanPlan.Height <= kucultulmusPlan.Height,
            $"taban ({tabanPlan.Height}p) x0,50 ({kucultulmusPlan.Height}p)'den yuksek cozunurluk secti.");

        Assert.True(buyutulmusPlan.Height < tabanPlan.Height || tabanPlan.Height < kucultulmusPlan.Height,
            "hicbir komsu carpan cozunurlugu degistirmedi; kesit karar noktasina duyarsiz.");
    }

    [Fact]
    public void SameProfileAlwaysProducesTheIdenticalPlan()
    {
        var info = SampleInfo();
        var options = Options();
        var profile = Profile(0.049847);

        var birinci = PlanCalculator.BuildDetailed(info, options, profile, null).Plan;
        var ikinci = PlanCalculator.BuildDetailed(info, options, profile, null).Plan;

        Assert.Empty(birinci.DescribeDifferences(ikinci));
    }
}
