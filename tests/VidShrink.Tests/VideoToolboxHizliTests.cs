using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class VideoToolboxHizliTests
{
    private sealed class Makine : IEncoderAvailability
    {
        private readonly Dictionary<string, EncoderProbeState> _durum;

        public Makine(params (string Kodek, EncoderProbeState Durum)[] durum)
            => _durum = durum.ToDictionary(d => d.Kodek, d => d.Durum, StringComparer.OrdinalIgnoreCase);

        public bool HasEncoder(string name) => _durum.ContainsKey(name);
        public bool WorksAsEncoder(string codec) => _durum.TryGetValue(codec, out var d) && d == EncoderProbeState.Working;
        public EncoderProbeState EncoderState(string codec) => _durum.TryGetValue(codec, out var d) ? d : EncoderProbeState.NotWorking;
    }

    private static Makine MacVt(EncoderProbeState vt = EncoderProbeState.Working) => new(
        ("libx264", EncoderProbeState.Working),
        ("libx265", EncoderProbeState.Working),
        ("libsvtav1", EncoderProbeState.Working),
        ("h264_videotoolbox", EncoderProbeState.Working),
        ("hevc_videotoolbox", vt));

    private static MediaInfo Kaynak() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 500L * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    private static EncodePlan Plan(PlanOptions secenek, IEncoderAvailability makine, bool macOS)
        => PlanCalculator.BuildDetailed(Kaynak(), secenek, null, makine, macOS).Plan;

    private static PlanOptions Hizli(double mb = 60) => new() { TargetMb = mb, SpeedMode = SpeedMode.Fast };

    [Theory]
    [InlineData(CodecPreference.Auto)]
    [InlineData(CodecPreference.Compatible)]
    [InlineData(CodecPreference.MaxCompression)]
    public void MacHizliKipVtVarkenHevcVideoToolboxSecer(CodecPreference tercih)
    {
        var secenek = Hizli();
        secenek.Codec = tercih;

        var plan = Plan(secenek, MacVt(), macOS: true);

        Assert.Equal("hevc_videotoolbox", plan.Codec);
        Assert.NotEqual(EncodeMode.Crf, plan.ModeEnum);
        Assert.DoesNotContain(plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback);
    }

    [Fact]
    public void WindowsVeLinuxtaAyniMakineVtSecmez()
    {
        var plan = Plan(Hizli(), MacVt(), macOS: false);

        Assert.DoesNotContain("videotoolbox", plan.Codec, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("libx264", plan.Codec);
    }

    [Theory]
    [InlineData(CodecPreference.Auto)]
    [InlineData(CodecPreference.Compatible)]
    [InlineData(CodecPreference.MaxCompression)]
    public void KaliteKipiMacteYazilimdaKalir(CodecPreference tercih)
    {
        var secenek = new PlanOptions { TargetMb = 60, SpeedMode = SpeedMode.Quality, Codec = tercih };

        var plan = Plan(secenek, MacVt(), macOS: true);

        Assert.StartsWith("lib", plan.Codec);
    }

    [Theory]
    [InlineData(EncoderProbeState.NotWorking)]
    public void VtCalismiyorsaYazilimaDuser(EncoderProbeState vt)
    {
        var plan = Plan(Hizli(), MacVt(vt), macOS: true);

        Assert.Equal("libx264", plan.Codec);
    }

    [Fact]
    public void KodekKilidiLibx264MacHizliKipteKalir()
    {
        var secenek = Hizli();
        secenek.LockedCodec = "libx264";

        var plan = Plan(secenek, MacVt(), macOS: true);

        Assert.Equal("libx264", plan.Codec);
    }

    [Fact]
    public void YazilimYoluSabitlenmisseVtSecilmez()
    {
        var secenek = Hizli();
        secenek.EncoderPath = EncoderPathOverride.Software;

        var plan = Plan(secenek, MacVt(), macOS: true);

        Assert.Equal("libx265", plan.Codec);
        Assert.Contains(plan.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathOverride);
    }

    [Fact]
    public void DonanimYoluSabitlenmisseVtIstegiKarsilar()
    {
        var secenek = new PlanOptions { TargetMb = 60, SpeedMode = SpeedMode.Quality, EncoderPath = EncoderPathOverride.Hardware };

        var plan = Plan(secenek, MacVt(), macOS: true);

        Assert.Equal("hevc_videotoolbox", plan.Codec);
        Assert.DoesNotContain(plan.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathUnmet);
    }

    [Fact]
    public void CrfKilidiVtdeTekGecisBitHiziOlur()
    {
        var secenek = new PlanOptions { TargetMb = 60, SpeedMode = SpeedMode.Fast, LockedCrf = 24 };
        var yazilim = Plan(secenek, MacVt(), macOS: false);
        Assert.Equal(EncodeMode.Crf, yazilim.ModeEnum);

        var plan = Plan(secenek, MacVt(), macOS: true);

        Assert.Equal("hevc_videotoolbox", plan.Codec);
        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);
        Assert.Null(plan.Crf);
        var arg = FfmpegArguments.Build(Kaynak(), plan, "cikti.mp4", 0, null);
        Assert.Contains("-b:v", arg);
        Assert.DoesNotContain("-crf", arg);
    }

    [Fact]
    public void H264VideoToolboxAdayDegil()
    {
        var makine = new Makine(("libx264", EncoderProbeState.Working), ("h264_videotoolbox", EncoderProbeState.Working));

        var plan = Plan(Hizli(), makine, macOS: true);

        Assert.Equal("libx264", plan.Codec);
    }

    [Fact]
    public void DonanimKarariVtYoklamasiGecinceKutuyuAcar()
    {
        var plan = Plan(Hizli(), MacVt(), macOS: true);
        var yoklama = new EncoderProbeResult("hevc_videotoolbox", true, 120);

        var karar = HardwareVerdict.Decide(yoklama, plan.VideoBitrateK, plan.Width, plan.Height, plan.Fps);

        Assert.True(karar.EnableFastMode);
        Assert.Equal(HardwareVerdictReason.Usable, karar.Reason);
        Assert.True(MainWindow.HardwareAvailableFrom(plan, yoklama));
        Assert.False(HardwareVerdict.Decide(new EncoderProbeResult("h264_videotoolbox", true, 120), plan.VideoBitrateK, plan.Width, plan.Height, plan.Fps).EnableFastMode);
    }
}
