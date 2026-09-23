using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Arayüz gerçekte koşan modu söyler. Donanım kodlayıcıları (<see cref="CodecModel.SinglePassRateControl"/>)
/// plan "2pass" dese de tek geçiş koşar; akış kopyalamada kodlama yoktur; ölçüm yoksa tahmin
/// kaynak bit hızına düşer. Üç metin de bunu söylemiyordu.
/// </summary>
public sealed class GecisEtiketiTests
{
    private static MediaInfo Kaynak() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 40L * 1024 * 1024,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000,
        Streams = new[] { new SourceStream(0, StreamKind.Video, "h264") }
    };

    [Theory]
    [InlineData("h264_nvenc", "2pass", true)]
    [InlineData("hevc_qsv", "2pass", true)]
    [InlineData("h264_amf", "2pass", true)]
    [InlineData("h264_videotoolbox", "2pass", true)]
    [InlineData("libx264", "2pass", false)]
    [InlineData("libsvtav1", "2pass", false)]
    [InlineData("h264_nvenc", "crf", false)]
    [InlineData("h264_nvenc", "passthrough", false)]
    public void TekGecisKosanKodlayiciAyriliyor(string kodek, string mod, bool beklenen)
    {
        var plan = new EncodePlan { Codec = kodek, Mode = mod };

        Assert.Equal(beklenen, MainWindow.RunsSinglePass(plan));
    }

    [Theory]
    [InlineData("tr")]
    [InlineData("en")]
    public void TavsiyeKosanGecisiSoyluyor(string dil)
    {
        var iki = MainWindow.AdviceLine(AdviceCode.TargetEnforcedTwoPass, dil, false);
        var tek = MainWindow.AdviceLine(AdviceCode.TargetEnforcedTwoPass, dil, false, singlePass: true);

        Assert.Equal(LanguageCatalog.Title(Strings.GetIn(dil, "main.advice.single-pass"), dil), tek);
        Assert.NotEqual(iki, tek);
    }

    [Theory]
    [InlineData("h264_nvenc", "2pass", true, "main.estimate.mode.enforced-single-pass")]
    [InlineData("libx264", "2pass", true, "main.estimate.mode.enforced")]
    [InlineData("libx264", "crf", false, "main.estimate.mode.ceiling")]
    [InlineData("libx264", "passthrough", true, "main.estimate.mode.copy")]
    public void TahminNotuModu(string kodek, string mod, bool zorlanan, string anahtar)
    {
        var plan = new EncodePlan { Codec = kodek, Mode = mod };

        Assert.Equal(anahtar, MainWindow.EstimateModeKey(plan, new SizeEstimate(10, 9, 11, true, zorlanan)));
    }

    /// <summary>
    /// D1: kopya yolunda <see cref="PlanCalculator.Estimate"/> boyutu kesin bildiği için
    /// <c>Enforced</c> döner; not "iki geçiş zorlar" değil kopya der.
    /// </summary>
    [Fact]
    public void KopyaTahminiIkiGecisDemiyor()
    {
        var info = Kaynak();
        var plan = new EncodePlan { Codec = "copy", Mode = "passthrough" };
        var tahmin = PlanCalculator.Estimate(plan, info, null);

        Assert.True(tahmin.Enforced);
        Assert.Equal("main.estimate.mode.copy", MainWindow.EstimateModeKey(plan, tahmin));
    }

    /// <summary>
    /// D2: ölçüm yokken tahmin kaynak bit hızından gelir (<c>basis.estimated</c> kolu gerçek);
    /// ipucu bunun olmadığını söyleyemez, geri düşüşü her dilde anlatır.
    /// </summary>
    [Fact]
    public void TahminIpucuGeriDususuSakliyor()
    {
        var info = Kaynak();
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 10 });

        Assert.False(PlanCalculator.Estimate(plan, info, null).Measured);
        Assert.DoesNotContain("does not guess from the source bitrate", Strings.GetIn("en", "main.output.estimated-output.tip"));
        Assert.DoesNotContain("tahmin yürütmez", Strings.GetIn("tr", "main.output.estimated-output.tip"));
        Assert.Contains("source bitrate", Strings.GetIn("en", "main.output.estimated-output.tip").Split('\n')[1]);
        Assert.Contains("kaynak bit hızına düşer", Strings.GetIn("tr", "main.output.estimated-output.tip").Split('\n')[1]);
    }
}
