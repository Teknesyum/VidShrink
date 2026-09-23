using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// E1'in açılan tek parçası: <c>-tune</c> (<c>docs/netlestirme/024-ince-ayar-yuzeyi.md</c>).
/// Merdiven küçültme kolunun kendisi — kaydedicinin <c>zerolatency</c>/<c>ull</c> değerleri
/// buraya girmez. Kodeğe uymayan ad ön ayardaki gibi düşer, koşumu düşürmez.
/// </summary>
public sealed class TuneYuzeyiTests
{
    private static MediaInfo Info() => new()
    {
        FilePath = "girdi.mp4",
        FileSizeBytes = 400_000_000L,
        DurationSeconds = 600,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000
    };

    private static PlanOptions Secenek(string kodek, string? tune) => new()
    {
        TargetMb = 25,
        Codec = CodecPreference.Compatible,
        LockedCodec = kodek,
        LockedTune = tune
    };

    /// <summary>Merdiven kodeğe göre: yazılım x264/x265 üç değer, SVT-AV1 numara, geri kalanı boş.</summary>
    [Theory]
    [InlineData("libx264", "film", true)]
    [InlineData("libx265", "grain", true)]
    [InlineData("libx265", "film", false)]
    [InlineData("libsvtav1", "2", true)]
    [InlineData("libsvtav1", "3", false)]
    [InlineData("libx264", "zerolatency", false)]
    [InlineData("libx265", "psnr", false)]
    [InlineData("libsvtav1", "film", false)]
    [InlineData("h264_nvenc", "hq", false)]
    public void MerdivenKodegeGore(string kodek, string tune, bool gecerli)
        => Assert.Equal(gecerli, FfmpegArguments.IsValidTune(kodek, tune));

    /// <summary>Kaydedicinin merdiveni küçültmeye taşınmadı — iki küme ayrı.</summary>
    [Fact]
    public void KaydediciMerdiveniKucultmeyeTasinmadi()
    {
        Assert.Contains("zerolatency", RecorderArguments.TunesFor("libx264"));
        Assert.DoesNotContain("zerolatency", FfmpegArguments.TunesFor("libx264"));
        Assert.Empty(FfmpegArguments.TunesFor("h264_nvenc"));
    }

    /// <summary>Kodek seçilmeden yapılan tek denetim: ad herhangi bir merdivende mi.</summary>
    [Theory]
    [InlineData("film", true)]
    [InlineData("2", true)]
    [InlineData("zerolatency", false)]
    [InlineData("3", false)]
    [InlineData("4", false)]
    public void TaninanTuneMerdivenlerdenGeliyor(string ad, bool taniniyor)
        => Assert.Equal(taniniyor, FfmpegArguments.IsKnownTune(ad));

    /// <summary>Kilit plana geçiyor; kilitsiz planda alan boş kalıyor (olumsuz kontrol).</summary>
    [Fact]
    public void KilitPlanaGeciyor()
    {
        Assert.Equal("film", PlanCalculator.Build(Info(), Secenek("libx264", "film")).Tune);
        Assert.Null(PlanCalculator.Build(Info(), Secenek("libx264", null)).Tune);
    }

    /// <summary>Uymayan ad düşüyor: plan tune'suz çıkıyor ve gerekçeye satır düşüyor.</summary>
    [Fact]
    public void UymayanTuneDusuyor()
    {
        var plan = PlanCalculator.Build(Info(), Secenek("libx264", "zerolatency"));

        Assert.Null(plan.Tune);
        Assert.Contains("merdiveninde yok", plan.Reason, StringComparison.Ordinal);
    }

    /// <summary>x264/x265'te değer ayrı bir bayrak olarak yazılıyor; kilitsiz koşumda yazılmıyor.</summary>
    [Fact]
    public void YazilimKodeginde_TuneBayragiYaziliyor()
    {
        var kilitli = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(
            Info(), PlanCalculator.Build(Info(), Secenek("libx264", "animation")), "cikti.mp4", 0, null));
        var kilitsiz = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(
            Info(), PlanCalculator.Build(Info(), Secenek("libx264", null)), "cikti.mp4", 0, null));

        Assert.Contains("-tune animation", kilitli);
        Assert.DoesNotContain("-tune", kilitsiz);
    }

    /// <summary>
    /// SVT-AV1'de <c>-tune</c> ayrı bayrak değil: değer <c>-svtav1-params</c> içine giriyor
    /// ve psy/AQ kümesinin kendi <c>tune=</c>'u ikinci kez yazılmıyor.
    /// </summary>
    [Fact]
    public void SvtAv1de_TuneParametreIcineGiriyor()
    {
        var plan = PlanCalculator.Build(Info(), Secenek("libsvtav1", "2"));
        var args = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(Info(), plan, "cikti.mkv", 0, null, new HepsiVar()));

        Assert.DoesNotContain("-tune 2", args);
        Assert.Contains("tune=2", args);
        Assert.DoesNotContain("tune=1", args);
        Assert.Equal(1, args.Split("-svtav1-params").Length - 1);
    }

    private sealed class HepsiVar : IEncoderAvailability, IEncoderOptionAvailability
    {
        public bool HasEncoder(string name) => true;
        public bool WorksAsEncoder(string codec) => true;
        public bool SupportsEncoderOption(string codec, string option, string value) => true;
    }
}
