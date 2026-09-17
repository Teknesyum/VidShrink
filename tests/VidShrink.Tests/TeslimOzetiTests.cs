using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class TeslimOzetiTests
{
    private static EncodePlan Planned() => new()
    {
        Codec = "libsvtav1",
        Mode = "crf",
        Crf = 38,
        VideoBitrateK = 900,
        Width = 1920,
        Height = 1080,
        Fps = 30
    };

    [Fact]
    public void Ozet_teslim_edilen_denemenin_kipini_yazar()
    {
        var planned = Planned();
        var delivered = planned.Clone();
        delivered.Mode = "2pass";
        delivered.Crf = null;
        delivered.VideoBitrateK = 612;
        delivered.Width = 1280;
        delivered.Height = 720;

        var ozet = TeslimOzeti.Of(new EncodeResult(true, "out.mp4", 1.0, delivered, 2, null));

        Assert.Equal("2pass", ozet.Mode);
        Assert.Equal("612k", ozet.CrfOrBitrate);
        Assert.Equal((1280, 720), (ozet.Width, ozet.Height));
    }

    [Fact]
    public void Ilk_denemede_teslimde_ozet_planla_ayni_negatif_kontrol()
    {
        var planned = Planned();

        var ozet = TeslimOzeti.Of(new EncodeResult(true, "out.mp4", 1.0, planned, 1, null));

        Assert.Equal("crf", ozet.Mode);
        Assert.Equal("crf 38", ozet.CrfOrBitrate);
    }
}
