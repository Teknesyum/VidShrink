using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Kullanıcı 09-04: dinamik çözünürlük varsayılan; kutu kaldırılınca "1080p istiyorum" denebilir.
/// <see cref="PlanOptions.FixedResolution"/> kısa kenarı sabitler, rejimin tabanı ve ölçek
/// merdiveni ona dokunmaz, kaynaktan büyük istek yukarı ölçeklemez.
/// </summary>
public sealed class SabitCozunurlukTests
{
    private sealed class AllWorking : IEncoderAvailability
    {
        public bool HasEncoder(string name) => true;
        public bool WorksAsEncoder(string codec) => true;
        public EncoderProbeState EncoderState(string codec) => EncoderProbeState.Working;
    }

    private static MediaInfo Info(int width, int height) => new()
    {
        FilePath = "sample.mp4",
        FileSizeBytes = 500L * 1024 * 1024,
        DurationSeconds = 120,
        Width = width,
        Height = height,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    private static (int Width, int Height) Plan(MediaInfo info, double targetMb, int? fixedResolution)
    {
        var options = new PlanOptions { TargetMb = targetMb, Codec = CodecPreference.Auto, FixedResolution = fixedResolution };
        var plan = PlanCalculator.BuildDetailed(info, options, null, new AllWorking()).Plan;
        return (plan.Width, plan.Height);
    }

    [Fact]
    public void KucukHedefteDinamikDuserSabit1080Kalir()
    {
        var info = Info(1920, 1080);

        var dynamic = Plan(info, 3, null);
        var fixedPlan = Plan(info, 3, 1080);

        Assert.True(dynamic.Height < 1080, $"dinamik plan zaten 1080 kaliyorsa olcu bir sey kanitlamaz ({dynamic.Width}x{dynamic.Height})");
        Assert.Equal((1920, 1080), fixedPlan);
    }

    [Fact]
    public void BuyukHedefteDinamikKaynakKalirSabit720Iner()
    {
        var info = Info(1920, 1080);

        var dynamic = Plan(info, 400, null);
        var fixedPlan = Plan(info, 400, 720);

        Assert.Equal((1920, 1080), dynamic);
        Assert.Equal((1280, 720), fixedPlan);
    }

    [Fact]
    public void DikeyVideodaKisaKenarSabitlenir()
    {
        Assert.Equal((720, 1280), Plan(Info(1080, 1920), 400, 720));
    }

    [Fact]
    public void TabanIstegiSabitBoyuYukariCekmez()
    {
        var info = Info(1920, 1080);

        var withFloor = new PlanOptions { TargetMb = 400, Codec = CodecPreference.Auto, MinResolutionHeight = 720, FixedResolution = 480 };
        var plan = PlanCalculator.BuildDetailed(info, withFloor, null, new AllWorking()).Plan;

        Assert.Equal((1920, 1080), Plan(info, 400, null));
        Assert.Equal(480, plan.Height);
    }

    [Fact]
    public void KaynaktanBuyukIstekYukariOlceklemez()
    {
        Assert.Equal((1920, 1080), Plan(Info(1920, 1080), 3, 2160));
    }
}
