using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class EncoderAvailabilityTests
{
    private sealed class Makine : IEncoderAvailability
    {
        private readonly HashSet<string> _built;
        private readonly HashSet<string> _works;
        private readonly Dictionary<string, int> _yoklama = new(StringComparer.OrdinalIgnoreCase);

        public Makine(string[] built, string[] works)
        {
            _built = new HashSet<string>(built, StringComparer.OrdinalIgnoreCase);
            _works = new HashSet<string>(works, StringComparer.OrdinalIgnoreCase);
        }

        public bool HasEncoder(string name) => _built.Contains(name);

        public bool WorksAsEncoder(string codec)
        {
            _yoklama[codec] = YoklamaSayisi(codec) + 1;
            return _works.Contains(codec);
        }

        public EncoderProbeState EncoderState(string codec) =>
            WorksAsEncoder(codec) ? EncoderProbeState.Working : EncoderProbeState.NotWorking;

        public int YoklamaSayisi(string codec) => _yoklama.TryGetValue(codec, out var n) ? n : 0;
    }

    private static readonly string[] TumAdaylar =
    {
        "h264_nvenc", "h264_qsv", "h264_amf",
        "hevc_nvenc", "hevc_qsv", "hevc_amf",
        "av1_nvenc"
    };

    private static string? Sec(Makine makine) => PerformanceProbe.SelectHardwareCodec(makine, Stopwatch.StartNew(), 0);

    private static MediaInfo OrnekKaynak() => new()
    {
        FilePath = "ornek.mp4",
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

    [Fact]
    public void OlcumCalisanAdayiSeciyorListedekiIlkiniDegil()
    {
        var makine = new Makine(built: TumAdaylar, works: new[] { "av1_nvenc" });

        Assert.Equal("av1_nvenc", Sec(makine));
    }

    [Fact]
    public void OlcumSirayiCalisanIlkAdayaKadarYuruyor()
    {
        var makine = new Makine(built: TumAdaylar, works: new[] { "hevc_nvenc", "av1_nvenc" });

        Assert.Equal("hevc_nvenc", Sec(makine));
    }

    [Fact]
    public void CalisanAdayBulununcaSonrakiAdaylarYoklanmiyor()
    {
        var makine = new Makine(built: TumAdaylar, works: new[] { "h264_qsv", "av1_nvenc" });

        Assert.Equal("h264_qsv", Sec(makine));
        Assert.Equal(1, makine.YoklamaSayisi("h264_nvenc"));
        Assert.Equal(1, makine.YoklamaSayisi("h264_qsv"));
        Assert.Equal(0, makine.YoklamaSayisi("h264_amf"));
        Assert.Equal(0, makine.YoklamaSayisi("av1_nvenc"));
    }

    [Fact]
    public void DerlemeListesindeOlmayanAdayHicYoklanmiyor()
    {
        var makine = new Makine(built: new[] { "av1_nvenc" }, works: new[] { "av1_nvenc" });

        Assert.Equal("av1_nvenc", Sec(makine));
        Assert.Equal(0, makine.YoklamaSayisi("h264_nvenc"));
        Assert.Equal(0, makine.YoklamaSayisi("h264_qsv"));
    }

    [Fact]
    public void HicbiriCalismayincaListedekiIlkAdayOlculuyor()
    {
        var makine = new Makine(built: TumAdaylar, works: new[] { "libx264" });

        Assert.Equal("h264_nvenc", Sec(makine));
    }

    [Fact]
    public void DerlemeListesindeDonanimYokkenAdayDaYok()
    {
        var makine = new Makine(built: new[] { "libx264", "libx265" }, works: new[] { "libx264", "libx265" });

        Assert.Null(Sec(makine));
    }

    [Fact]
    public void ButceBitmisseKalanAdaylarYoklanmiyor()
    {
        var makine = new Makine(built: TumAdaylar, works: new[] { "av1_nvenc" });
        var clock = Stopwatch.StartNew();
        Thread.Sleep(20);

        var secilen = PerformanceProbe.SelectHardwareCodec(makine, clock, 1);

        Assert.Equal("h264_nvenc", secilen);
        Assert.Equal(0, makine.YoklamaSayisi("h264_nvenc"));
        Assert.Equal(0, makine.YoklamaSayisi("av1_nvenc"));
    }

    [Fact]
    public void SurucusuzMakinedeHizliKipYazilimaDusuyor()
    {
        var makine = new Makine(
            built: new[] { "libx264", "libx265", "libsvtav1", "h264_nvenc", "hevc_nvenc", "av1_nvenc" },
            works: new[] { "libx264", "libx265", "libsvtav1" });

        var result = PlanCalculator.BuildDetailed(
            OrnekKaynak(),
            new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, SpeedMode = SpeedMode.Fast },
            null,
            makine);

        Assert.False(CodecModel.IsHardware(result.Plan.Codec));
        Assert.Contains(AdviceCode.EncoderFallback, result.Advice.Notes);
        Assert.Equal(1, makine.YoklamaSayisi("av1_nvenc"));
    }

    [Fact]
    public void PickCodecArtikDerlemeListesineDegilYoklamayaBakiyor()
    {
        var makine = new Makine(
            built: new[] { "libx264", "libx265", "libsvtav1", "h264_nvenc", "hevc_nvenc", "av1_nvenc" },
            works: new[] { "libx264", "libx265", "libsvtav1" });

        var result = PlanCalculator.BuildDetailed(
            OrnekKaynak(),
            new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Fast, SpeedMode = SpeedMode.Quality },
            null,
            makine);

        Assert.Equal("libx264", result.Plan.Codec);
        Assert.Equal(1, makine.YoklamaSayisi("h264_nvenc"));
        Assert.Contains(AdviceCode.EncoderFallback, result.Advice.Notes);
    }

    private const string TekKodlayiciListesi = """
        Encoders:
         V..... = Video
        -------
         V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codecs: h264)
        """;

    private static EncoderCapabilities TekKodlayicili()
        => EncoderCapabilities.Parse(TekKodlayiciListesi, "", "ffmpeg version test\n");

    [Fact]
    public void WorksAsEncoderOlcemediyiCalismiyordanAyiriyor()
    {
        var olcemeyen = TekKodlayicili();
        olcemeyen.EncoderProbeHook = _ => EncoderCapabilities.ProbeOutcome.Unmeasured;

        var calismayan = TekKodlayicili();
        calismayan.EncoderProbeHook = _ => EncoderCapabilities.ProbeOutcome.Rejected;

        Assert.True(olcemeyen.WorksAsEncoder("h264_nvenc"), "olcemeyen yoklama 'bu kodlayici yok' sayiliyor");
        Assert.False(calismayan.WorksAsEncoder("h264_nvenc"));
        Assert.NotEqual(
            olcemeyen.WorksAsEncoder("h264_nvenc"),
            calismayan.WorksAsEncoder("h264_nvenc"));
    }

    [Fact]
    public void OlcemeyenYoklamaListedeOlmayanKodlayiciyiVarSaymiyor()
    {
        var caps = TekKodlayicili();
        caps.EncoderProbeHook = _ => EncoderCapabilities.ProbeOutcome.Unmeasured;

        Assert.False(caps.WorksAsEncoder("av1_nvenc"));
        Assert.Equal(EncoderProbeState.NotWorking, caps.WorksAsEncoderState("av1_nvenc"));
    }

    [Fact]
    public void WorksAsEncoderStateOlcemediyleCalismiyoriAyirtEdiyor()
    {
        var olcemeyen = TekKodlayicili();
        olcemeyen.EncoderProbeHook = _ => EncoderCapabilities.ProbeOutcome.Unmeasured;

        var calismayan = TekKodlayicili();
        calismayan.EncoderProbeHook = _ => EncoderCapabilities.ProbeOutcome.Rejected;

        Assert.Equal(EncoderProbeState.Unmeasured, olcemeyen.WorksAsEncoderState("h264_nvenc"));
        Assert.Equal(EncoderProbeState.NotWorking, calismayan.WorksAsEncoderState("h264_nvenc"));
        Assert.NotEqual(
            olcemeyen.WorksAsEncoderState("h264_nvenc"),
            calismayan.WorksAsEncoderState("h264_nvenc"));
    }
}
