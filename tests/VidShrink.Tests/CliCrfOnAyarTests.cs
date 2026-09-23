using VidShrink.Core;
using VidShrink.Cli;

namespace VidShrink.Tests;

/// <summary>
/// E1/E5 kararının üçüncü parçası: arayüz ile CLI arasındaki asimetri yalnız
/// <c>--crf</c> ve <c>--on-ayar</c> ile kapatılıyor
/// (<c>docs/netlestirme/024-ince-ayar-yuzeyi.md</c>). Ad kodeğe uymazsa plan onu düşürür,
/// çakmaz — CLI'da kodlayıcı ancak plan kurulurken seçildiği için tek güvenli davranış budur.
/// </summary>
public sealed class CliCrfOnAyarTests
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

    /// <summary>Uzun ve kısa yazım aynı isteği kuruyor, değerler plan seçeneklerine iniyor.</summary>
    [Theory]
    [InlineData("--on-ayar")]
    [InlineData("--preset")]
    public void CrfVeOnAyarPlanSecenegineIniyor(string onAyarYazimi)
    {
        var parsed = CliParser.Parse(new[] { "plan", "a.mp4", "--hedef", "25", "--crf", "26", onAyarYazimi, "slow" });

        Assert.True(parsed.Ok, parsed.ErrorKey);
        Assert.Equal(26d, parsed.Request!.Crf);
        Assert.Equal("slow", parsed.Request.Preset);

        var options = parsed.Request.ToPlanOptions(10);
        Assert.Equal(26d, options.LockedCrf);
        Assert.Equal("slow", options.LockedPreset);
    }

    /// <summary>Verilmeyen bayrak kilit bırakmıyor — kolun her koşumda açık olmadığı pimli.</summary>
    [Fact]
    public void BayraksizIstekteKilitYok()
    {
        var options = CliParser.Parse(new[] { "plan", "a.mp4", "--hedef", "25" }).Request!.ToPlanOptions(10);

        Assert.Null(options.LockedCrf);
        Assert.Null(options.LockedPreset);
    }

    /// <summary>Sınır dışı CRF ve tanınmayan ön ayar adıyla reddediliyor.</summary>
    [Theory]
    [InlineData("error.bad-crf", "--crf", "-1")]
    [InlineData("error.bad-crf", "--crf", "64")]
    [InlineData("error.bad-crf", "--crf", "orta")]
    [InlineData("error.bad-preset", "--on-ayar", "uydurma")]
    [InlineData("error.bad-preset", "--preset", "p9")]
    public void BozukDegerAdiylaReddediliyor(string key, string bayrak, string deger)
    {
        var parsed = CliParser.Parse(new[] { "plan", "a.mp4", "--hedef", "25", bayrak, deger });

        Assert.False(parsed.Ok);
        Assert.Equal(key, parsed.ErrorKey);
    }

    /// <summary>Sınırın kendisi geçerli: 0 ve 63 kabul ediliyor.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("63")]
    public void SinirDegerleriKabulEdiliyor(string deger)
        => Assert.True(CliParser.Parse(new[] { "plan", "a.mp4", "--hedef", "25", "--crf", deger }).Ok);

    /// <summary>
    /// <c>izle</c> kolunda iki bayrak da yok: orada tek girdi yok, kilit tüm kuyruğa yayılırdı.
    /// </summary>
    [Theory]
    [InlineData("--crf", "26")]
    [InlineData("--on-ayar", "slow")]
    public void IzleKolundaBayrakYok(string bayrak, string deger)
    {
        var parsed = CliParser.Parse(new[] { "izle", "klasor", "--cikti", "c", "--hedef", "25", bayrak, deger });

        Assert.False(parsed.Ok);
        Assert.Equal("error.unknown-option", parsed.ErrorKey);
    }

    /// <summary>Ayrıştırıcının tanıdığı ad, motorun merdivenlerinden geliyor.</summary>
    [Theory]
    [InlineData("veryslow", true)]
    [InlineData("p5", true)]
    [InlineData("8", true)]
    [InlineData("balanced", true)]
    [InlineData("p9", false)]
    [InlineData("hizli", false)]
    public void TaninanOnAyarMerdivenlerdenGeliyor(string ad, bool taniniyor)
        => Assert.Equal(taniniyor, FfmpegArguments.IsKnownPreset(ad));

    /// <summary>Kilitlenen CRF plana geçiyor: kip crf oluyor ve değer plana yazılıyor.</summary>
    [Fact]
    public void KilitliCrfPlanaGeciyor()
    {
        var plan = PlanCalculator.Build(Info(), new PlanOptions { TargetMb = 25, LockedCrf = 26 });

        Assert.Equal("crf", plan.Mode);
        Assert.Equal(26, plan.Crf);
    }

    /// <summary>
    /// Kodeğe uymayan ad plan kurulurken düşüyor: koşum çakmıyor, motorun ön ayarı kalıyor
    /// ve gerekçeye tek satır giriyor.
    /// </summary>
    [Fact]
    public void UymayanOnAyarDusuyorCakmiyor()
    {
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, LockedPreset = "p5" };
        var motorunku = PlanCalculator.Build(Info(), new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible }).Preset;

        var sonuc = PlanCalculator.BuildDetailed(Info(), options, null, null);

        Assert.Equal("libx264", sonuc.Plan.Codec);
        Assert.Equal(motorunku, sonuc.Plan.Preset);
        Assert.Contains("p5", sonuc.Plan.Reason, StringComparison.Ordinal);
        Assert.Contains("merdiveninde yok", sonuc.Plan.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// Uyan ad düşmüyor — düşme kolunun her ada çalmadığı olumsuz kontrolle pimli.
    /// </summary>
    [Fact]
    public void UyanOnAyarDusmuyor()
    {
        var sonuc = PlanCalculator.BuildDetailed(
            Info(), new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, LockedPreset = "slow" }, null, null);

        Assert.Equal("slow", sonuc.Plan.Preset);
        Assert.DoesNotContain("merdiveninde yok", sonuc.Plan.Reason, StringComparison.Ordinal);
    }
}
