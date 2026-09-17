using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class KaranlikGecisTests
{
    private const double Karanlik = 28.74;
    private const double Ekran = 66.9;

    private sealed class FakeAvailability : IEncoderAvailability
    {
        private readonly Dictionary<string, EncoderProbeState> _states;

        public FakeAvailability(params (string Codec, EncoderProbeState State)[] states)
            => _states = states.ToDictionary(s => s.Codec, s => s.State, StringComparer.OrdinalIgnoreCase);

        public bool HasEncoder(string name) => _states.ContainsKey(name);
        public bool WorksAsEncoder(string codec) => _states.TryGetValue(codec, out var s) && s == EncoderProbeState.Working;
        public EncoderProbeState EncoderState(string codec) => _states.TryGetValue(codec, out var s) ? s : EncoderProbeState.NotWorking;
    }

    private static FakeAvailability Available(EncoderProbeState x265 = EncoderProbeState.Working) => new(
        ("libx264", EncoderProbeState.Working),
        ("libx265", x265),
        ("libsvtav1", EncoderProbeState.Working),
        ("h264_nvenc", EncoderProbeState.Working),
        ("hevc_nvenc", EncoderProbeState.Working),
        ("av1_nvenc", EncoderProbeState.Working));

    private static MediaInfo Info() => new()
    {
        FilePath = "sample.mp4",
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

    private static EncodePlan Plan(double targetMb, double? luma, CodecPreference codec = CodecPreference.Auto, string? locked = null, SpeedMode speed = SpeedMode.Quality, FakeAvailability? availability = null)
    {
        var info = Info();
        var profile = ComplexityProfile.FromSourceBitrate(info) with { MeanLuma = luma };
        var options = new PlanOptions { TargetMb = targetMb, Codec = codec, LockedCodec = locked, SpeedMode = speed };
        return PlanCalculator.BuildDetailed(info, options, profile, availability ?? Available()).Plan;
    }

    [Theory]
    [InlineData(40.0)]
    [InlineData(10.0)]
    public void AutoSikiRejimdeKaranlikKaynakX265TurboyaGecer(double targetMb)
    {
        var plan = Plan(targetMb, Karanlik);

        Assert.Equal("libx265", plan.Codec);
        Assert.True(plan.TurboFirstPass);
        var note = Assert.Single(plan.ReasonCodes, n => n.Code == ReasonCode.DarkContentHevc);
        Assert.Equal("libsvtav1", note.RequestedCodec);
        Assert.Equal(Karanlik, note.Score, 3);
        Assert.DoesNotContain(plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback);
    }

    [Fact]
    public void AutoDengeliRejimdeKaranlikKaynakX264Kalir()
    {
        var plan = Plan(150, Karanlik);

        Assert.Equal("libx264", plan.Codec);
        Assert.DoesNotContain(plan.ReasonCodes, n => n.Code == ReasonCode.DarkContentHevc);
    }

    [Fact]
    public void AcikMaxCompressionKaranlikKaynaktaSvtKalir()
    {
        var plan = Plan(40, Karanlik, CodecPreference.MaxCompression);

        Assert.Equal("libsvtav1", plan.Codec);
        Assert.False(plan.TurboFirstPass);
    }

    [Theory]
    [InlineData("libsvtav1")]
    [InlineData("libx264")]
    public void KodekKilidiKaranlikKaynaktaDegismez(string locked)
    {
        var plan = Plan(40, Karanlik, locked: locked);

        Assert.Equal(locked, plan.Codec);
        Assert.DoesNotContain(plan.ReasonCodes, n => n.Code == ReasonCode.DarkContentHevc);
    }

    [Theory]
    [InlineData(Ekran)]
    [InlineData(137.66)]
    [InlineData(197.04)]
    [InlineData(null)]
    public void KaranlikOlmayanKaynakSvtKalir(double? luma)
    {
        var plan = Plan(40, luma);

        Assert.Equal("libsvtav1", plan.Codec);
        Assert.False(plan.TurboFirstPass);
        Assert.DoesNotContain(plan.ReasonCodes, n => n.Code == ReasonCode.DarkContentHevc);
    }

    [Fact]
    public void HizliKipteDonanimSecimineDokunulmaz()
    {
        var plan = Plan(40, Karanlik, speed: SpeedMode.Fast);

        Assert.Equal("av1_nvenc", plan.Codec);
        Assert.DoesNotContain(plan.ReasonCodes, n => n.Code == ReasonCode.DarkContentHevc);
    }

    [Fact]
    public void CalismayanX265SvtdeBirakir()
    {
        var plan = Plan(40, Karanlik, availability: Available(EncoderProbeState.NotWorking));

        Assert.Equal("libsvtav1", plan.Codec);
        Assert.DoesNotContain(plan.ReasonCodes, n => n.Code == ReasonCode.DarkContentHevc);
    }

    [Fact]
    public void EsikOlculenIkiKesitinGeometrikOrtasinda()
    {
        Assert.True(DarkContentSwitch.IsDark(43.9));
        Assert.False(DarkContentSwitch.IsDark(44.0));
        Assert.False(DarkContentSwitch.IsDark(0));
        Assert.False(DarkContentSwitch.IsDark(double.NaN));
        Assert.InRange(Math.Sqrt(Karanlik * Ekran), 43.5, 44.5);
        Assert.True(Ekran / Karanlik >= 2.0);
    }

    [Fact]
    public void AyristiriciSabitSignalstatsSatirlariniOrtalar()
    {
        const string stderr = """
            [Parsed_metadata_4 @ 000001d2c3a0] frame:0    pts:0       pts_time:0
            [Parsed_metadata_4 @ 000001d2c3a0] lavfi.signalstats.YAVG=27.85
            [Parsed_metadata_4 @ 000001d2c3a0] frame:1    pts:1       pts_time:0.25
            [Parsed_metadata_4 @ 000001d2c3a0] lavfi.signalstats.YAVG=29.65
            frame=    8 fps=0.0 q=-0.0 Lsize=N/A time=00:00:02.00 bitrate=N/A speed=12.1x
            """;

        Assert.Equal(28.75, ComplexityProbe.ParseMeanLuma(stderr)!.Value, 6);
        Assert.Null(ComplexityProbe.ParseMeanLuma("frame=    8 fps=0.0 q=-0.0 Lsize=N/A"));
        Assert.Null(ComplexityProbe.ParseMeanLuma(""));
    }

    [Fact]
    public void PencereOrtalamasiOkunamayanPencereyiAtlar()
    {
        Assert.Equal(30.0, ComplexityProbe.MeanOf(new double?[] { 20.0, null, 40.0 })!.Value, 6);
        Assert.Null(ComplexityProbe.MeanOf(new double?[] { null, null }));
    }

    [Fact]
    public void SondaKomutuOlcumdekiFiltreyiKullanir()
    {
        var args = ComplexityProbe.LumaArgs("in.mkv", 2, 2);

        Assert.Equal(
            new[] { "-hide_banner", "-nostdin", "-ss", "2", "-t", "2", "-i", "in.mkv", "-an", "-sn", "-dn", "-vf", "fps=4,scale=160:-2,format=yuv420p,signalstats,metadata=print:key=lavfi.signalstats.YAVG", "-f", "null", "-" },
            args);
    }

    [Fact]
    public void SafKararHerKoluAyriTutar()
    {
        Assert.True(DarkContentSwitch.Applies(CodecPreference.Auto, null, CompressionRegime.Aggressive, "libsvtav1", Karanlik));
        Assert.True(DarkContentSwitch.Applies(CodecPreference.Auto, null, CompressionRegime.Extreme, "libsvtav1", Karanlik));
        Assert.False(DarkContentSwitch.Applies(CodecPreference.MaxCompression, null, CompressionRegime.Aggressive, "libsvtav1", Karanlik));
        Assert.False(DarkContentSwitch.Applies(CodecPreference.Auto, "libsvtav1", CompressionRegime.Aggressive, "libsvtav1", Karanlik));
        Assert.False(DarkContentSwitch.Applies(CodecPreference.Auto, null, CompressionRegime.Balanced, "libsvtav1", Karanlik));
        Assert.False(DarkContentSwitch.Applies(CodecPreference.Auto, null, CompressionRegime.Light, "libsvtav1", Karanlik));
        Assert.False(DarkContentSwitch.Applies(CodecPreference.Auto, null, CompressionRegime.Aggressive, "av1_nvenc", Karanlik));
        Assert.False(DarkContentSwitch.Applies(CodecPreference.Auto, null, CompressionRegime.Aggressive, "libsvtav1", Ekran));
    }

    [FfmpegTheory]
    [InlineData("color=c=0x101010:size=320x240:rate=12:duration=8", true)]
    [InlineData("color=c=0x808080:size=320x240:rate=12:duration=8", false)]
    public async Task SondaKaranlikKlibiEsigeGoreAyirir(string source, bool dark)
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "karanlik-gecis", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var clip = Path.Combine(dir, "clip.mp4");
            using (var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, new[] { "-y", "-f", "lavfi", "-i", source, "-c:v", "libx264", "-pix_fmt", "yuv420p", clip }) })
            {
                process.Start();
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                await Task.WhenAll(stdout, stderr);
                Assert.Equal(0, process.ExitCode);
            }

            var info = await FfprobeClient.ProbeAsync(clip);
            var profile = (await ComplexityProbe.RunDetailedAsync(info, SpeedMode.Fast)).Profile;

            Assert.NotNull(profile.MeanLuma);
            Assert.Equal(dark, DarkContentSwitch.IsDark(profile.MeanLuma));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
