using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class KomutSatiriTests
{
    private sealed class HepsiVar : IEncoderAvailability
    {
        public bool HasEncoder(string name) => true;
        public bool WorksAsEncoder(string codec) => true;
        public EncoderProbeState EncoderState(string codec) => EncoderProbeState.Working;
    }

    private static MediaInfo Kaynak() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 40_000_000,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 5_700_000
    };

    private static EncodePlan Plan(string codec, string mode = "2pass") => new()
    {
        Codec = codec,
        Mode = mode,
        VideoBitrateK = 2000,
        Crf = mode == "crf" ? 24 : null,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        Preset = CodecModel.TakesPreset(codec) ? "slow" : "",
        PixelFormat = CodecModel.OutputPixelFormat(codec, "yuv420p"),
        AudioCodec = "aac",
        AudioBitrateK = 128
    };

    private static IReadOnlyList<string> KosucununArgumanlari(
        MediaInfo info, EncodePlan plan, string cikti, string gunlukDizin, IEncoderAvailability kabiliyet)
    {
        var ikiGecis = plan.ModeEnum == EncodeMode.TwoPass && FfmpegArguments.NeedsTwoPasses(plan.Codec);
        return ikiGecis
            ? EncodeRunner.EncodeArguments(info, plan, cikti, 2, Path.Combine(gunlukDizin, "pass"), kabiliyet)
            : EncodeRunner.EncodeArguments(info, plan, cikti, 0, null, kabiliyet);
    }

    [Theory]
    [InlineData("libx265", "2pass", 2)]
    [InlineData("libsvtav1", "2pass", 2)]
    [InlineData("libx265", "crf", 0)]
    [InlineData("hevc_nvenc", "2pass", 0)]
    [InlineData("hevc_videotoolbox", "2pass", 0)]
    public void Benchin_yazdigi_komut_kosucunun_gercek_argumanlariyla_ayni(string codec, string mode, int gecis)
    {
        var kabiliyet = new HepsiVar();
        var info = Kaynak();
        var plan = Plan(codec, mode);

        var beklenen = KosucununArgumanlari(info, plan, "cikis.mp4", "dizin", kabiliyet);
        var yazilan = KomutSatiri.Argumanlar(info, plan, "cikis.mp4", "dizin", kabiliyet);

        Assert.Equal(gecis, KomutSatiri.GecisNo(plan));
        Assert.Equal(beklenen, yazilan);
        Assert.Equal(
            "komut: " + FfmpegArguments.ToCommandLine(beklenen),
            KomutSatiri.Yaz(info, plan, "cikis.mp4", "dizin", kabiliyet));
    }

    [Fact]
    public void Videotoolbox_komutunda_pass_ve_passlogfile_yok()
    {
        var kabiliyet = new HepsiVar();
        var vt = KomutSatiri.Argumanlar(Kaynak(), Plan("hevc_videotoolbox"), "cikis.mp4", "dizin", kabiliyet);
        var yazilim = KomutSatiri.Argumanlar(Kaynak(), Plan("libx265"), "cikis.mp4", "dizin", kabiliyet);

        Assert.DoesNotContain("-pass", vt);
        Assert.DoesNotContain("-passlogfile", vt);
        Assert.Contains("-pass", yazilim);
        Assert.Contains("-passlogfile", yazilim);
    }
}
