using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class NvencAsagiTests
{
    private const double Sure = 10;
    private const double Hedef = 2000 * Sure / 8 / 1024;

    private readonly ITestOutputHelper _output;

    public NvencAsagiTests(ITestOutputHelper output) => _output = output;

    private sealed class Hazir : IEncoderAvailability
    {
        public bool HasEncoder(string name) => true;
        public bool WorksAsEncoder(string codec) => true;
        public EncoderProbeState EncoderState(string codec) => EncoderProbeState.Working;
    }

    private static MediaInfo Kaynak() => new()
    {
        FilePath = "kesit.mkv",
        FileSizeBytes = 100L * 1024 * 1024,
        DurationSeconds = Sure,
        Width = 1920,
        Height = 818,
        Fps = 24,
        VideoCodec = "ffv1",
        TotalBitrateBps = 80_000_000
    };

    private static EncodePlan Planla(string codec)
        => PlanCalculator.BuildDetailed(Kaynak(), new PlanOptions
        {
            TargetMb = Hedef,
            Intent = Intent.Sharing,
            Codec = CodecPreference.Auto,
            LockedCodec = codec,
            FillPolicy = FillPolicy.FillTarget
        }, null, new Hazir()).Plan;

    [Theory]
    [InlineData("h264_nvenc", 1.2207)]
    [InlineData("hevc_nvenc", 4.2725)]
    [InlineData("av1_nvenc", 25.0)]
    public void NvencIlkNisaniHedefinYuzdeDoksanSekizBucugu(string codec, double hedefMb)
    {
        Assert.Equal(BudgetFill.Aim * hedefMb, PlanCalculator.FirstAimMb(hedefMb, codec), 9);
        Assert.True(PlanCalculator.FirstAimMb(hedefMb, codec) > PlanCalculator.RetryAimMb(hedefMb, null));
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libsvtav1")]
    [InlineData("h264_qsv")]
    [InlineData("hevc_amf")]
    [InlineData("hevc_videotoolbox")]
    public void NegatifKontrolNvencDisindaIlkNisanBantMerkezindeKalir(string codec)
    {
        foreach (var hedefMb in new[] { 1.2207, 4.2725, 25.0, 180.0 })
            Assert.Equal(PlanCalculator.RetryAimMb(hedefMb, null), PlanCalculator.FirstAimMb(hedefMb, codec), 9);
    }

    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    public void NvencPlaniBantMerkezininUstundeHarcar(string codec)
    {
        var plan = Planla(codec);
        var tahmin = PlanCalculator.EstimatedMb(plan, Sure)!.Value;
        _output.WriteLine($"{codec}: {plan.Mode} {plan.VideoBitrateK}k {plan.Width}x{plan.Height} tahmin {tahmin:0.####} / hedef {Hedef:0.####}");

        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);
        Assert.True(tahmin > PlanCalculator.RetryAimMb(Hedef, null), $"{tahmin:0.####} MB");
        Assert.True(tahmin <= BudgetFill.Aim * Hedef, $"{tahmin:0.####} MB");
    }

    [Fact]
    public void NegatifKontrolYazilimPlaniBantMerkezindeKalir()
    {
        var plan = Planla("libx264");
        var tahmin = PlanCalculator.EstimatedMb(plan, Sure)!.Value;
        _output.WriteLine($"libx264: {plan.Mode} {plan.VideoBitrateK}k tahmin {tahmin:0.####}");

        Assert.True(tahmin <= PlanCalculator.RetryAimMb(Hedef, null) + 1e-9, $"{tahmin:0.####} MB");
    }

    [Theory]
    [InlineData("h264_nvenc", 1.03)]
    [InlineData("hevc_nvenc", 1.10)]
    [InlineData("av1_nvenc", 1.30)]
    public void TavaniAsanNvencDenemesiBirAsagiDenemeyeGider(string codec, double asim)
    {
        var plan = Planla(codec);
        var cikanMb = asim * Hedef;

        var asagi = PlanCalculator.Correct(plan, cikanMb, Hedef, Sure);
        var beklenenMb = cikanMb * asagi.VideoBitrateK / plan.VideoBitrateK;
        _output.WriteLine($"{codec} x{asim}: {plan.VideoBitrateK}k -> {asagi.VideoBitrateK}k, beklenen {beklenenMb:0.####} MB");

        Assert.True(asagi.VideoBitrateK < plan.VideoBitrateK);
        Assert.True(beklenenMb < BudgetFill.Floor * Hedef, $"{beklenenMb:0.####} MB");
        Assert.True(beklenenMb >= FillBand.For(Hedef).LowerMb, $"{beklenenMb:0.####} MB");
    }

    [Fact]
    public void YazilimYukariDenemesiDegismedi()
    {
        var teslim = Planla("libx264");
        var teslimMb = 0.92 * Hedef;

        var yukari = BudgetFill.Plan(teslim, teslimMb, Array.Empty<SizeSample>(), Hedef, Sure);

        Assert.NotNull(yukari);
        Assert.True(yukari!.VideoBitrateK > teslim.VideoBitrateK);
        Assert.Null(BudgetFill.Plan(Planla("hevc_nvenc"), teslimMb, Array.Empty<SizeSample>(), Hedef, Sure));
    }
}
