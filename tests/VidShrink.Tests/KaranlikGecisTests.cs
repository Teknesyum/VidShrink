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
        => Result(targetMb, luma, codec, locked, speed, availability).Plan;

    private static PlanResult Result(double targetMb, double? luma, CodecPreference codec = CodecPreference.Auto, string? locked = null, SpeedMode speed = SpeedMode.Quality, FakeAvailability? availability = null, MediaInfo? source = null)
    {
        var info = source ?? Info();
        var profile = ComplexityProfile.FromSourceBitrate(info) with { MeanLuma = luma };
        var options = new PlanOptions { TargetMb = targetMb, Codec = codec, LockedCodec = locked, SpeedMode = speed };
        return PlanCalculator.BuildDetailed(info, options, profile, availability ?? Available());
    }

    private static MediaInfo HdrInfo(string? transfer) => Info() with
    {
        IsHdr = transfer is "smpte2084" or "arib-std-b67",
        ColorTransfer = transfer,
        ColorPrimaries = transfer is null ? null : "bt2020",
        PixelFormat = "yuv420p10le",
        BitDepth = 10
    };

    [Theory]
    [InlineData(Karanlik, "libx265")]
    [InlineData(Ekran, "libsvtav1")]
    [InlineData(null, "libsvtav1")]
    public void StratejiOnerisiKodlananKodegiSoyler(double? luma, string expected)
    {
        var result = Result(40, luma);

        Assert.Equal(expected, result.Plan.Codec);
        Assert.Equal(result.Plan.Codec, result.Advice.SuggestedCodec);
    }

    [Fact]
    public void StratejiOnerisiElleSecilenUyumlulukteOtomatiginKaranlikKodeginiSoyler()
    {
        var result = Result(40, Karanlik, CodecPreference.Compatible);

        Assert.Equal("libx264", result.Plan.Codec);
        Assert.Equal(CodecPreference.MaxCompression, result.Advice.SuggestedPreference);
        Assert.Equal("libx265", result.Advice.SuggestedCodec);
        Assert.Equal("libsvtav1", Result(40, Ekran, CodecPreference.Compatible).Advice.SuggestedCodec);
    }

    [Theory]
    [InlineData("smpte2084")]
    [InlineData("arib-std-b67")]
    public void HdrKaynaktaKaranlikGecisiKosmaz(string transfer)
    {
        var result = Result(40, Karanlik, source: HdrInfo(transfer));

        Assert.Equal("libsvtav1", result.Plan.Codec);
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.DarkContentHevc);
        Assert.Equal("libsvtav1", result.Advice.SuggestedCodec);
    }

    [Theory]
    [InlineData("bt709")]
    [InlineData(null)]
    public void SdrAktarimliOnBitKaranlikKaynakGecer(string? transfer)
    {
        var result = Result(40, Karanlik, source: HdrInfo(transfer));

        Assert.Equal("libx265", result.Plan.Codec);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.DarkContentHevc);
    }

    [Fact]
    public void HdrBayragiAktarimAdiOlmadanDaGecisiDurdurur()
    {
        var info = Info() with { IsHdr = true };

        Assert.True(DarkContentSwitch.IsHdrSource(info));
        Assert.False(DarkContentSwitch.IsHdrSource(Info()));
        Assert.True(DarkContentSwitch.IsHdrSource(Info() with { ColorTransfer = "SMPTE2084" }));
        Assert.Equal("libsvtav1", Result(40, Karanlik, source: info).Plan.Codec);
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
        Assert.True(DarkContentSwitch.Applies(CodecPreference.Auto, null, CompressionRegime.Aggressive, "libsvtav1", Karanlik, false));
        Assert.True(DarkContentSwitch.Applies(CodecPreference.Auto, null, CompressionRegime.Extreme, "libsvtav1", Karanlik, false));
        Assert.False(DarkContentSwitch.Applies(CodecPreference.MaxCompression, null, CompressionRegime.Aggressive, "libsvtav1", Karanlik, false));
        Assert.False(DarkContentSwitch.Applies(CodecPreference.Auto, "libsvtav1", CompressionRegime.Aggressive, "libsvtav1", Karanlik, false));
        Assert.False(DarkContentSwitch.Applies(CodecPreference.Auto, null, CompressionRegime.Balanced, "libsvtav1", Karanlik, false));
        Assert.False(DarkContentSwitch.Applies(CodecPreference.Auto, null, CompressionRegime.Light, "libsvtav1", Karanlik, false));
        Assert.False(DarkContentSwitch.Applies(CodecPreference.Auto, null, CompressionRegime.Aggressive, "av1_nvenc", Karanlik, false));
        Assert.False(DarkContentSwitch.Applies(CodecPreference.Auto, null, CompressionRegime.Aggressive, "libsvtav1", Ekran, false));
        Assert.False(DarkContentSwitch.Applies(CodecPreference.Auto, null, CompressionRegime.Aggressive, "libsvtav1", Karanlik, true));
    }

    [Fact]
    public void GerekceNotuPencereninSatirinaVeKirkIkiDileCevrilir()
    {
        var plan = Plan(40, Karanlik);
        Assert.Contains(plan.ReasonCodes, n => n.Code == ReasonCode.DarkContentHevc);

        var lines = AppHost.Run(() =>
        {
            var window = new VidShrink.App.MainWindow();
            try
            {
                window.UseTurkish();
                return window.ReasonLinesForTest(plan);
            }
            finally { window.Close(); }
        });

        Assert.Contains(lines, line => line.StartsWith("kaynak karanlık", StringComparison.OrdinalIgnoreCase)
                                       && line.Contains("libsvtav1") && line.Contains("libx265") && line.Contains("28"));

        const string key = "main.reason.dark-content-hevc";
        var english = Locales.Domain("en", "main")[key];
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var value = Locales.Domain(language, "main")[key];
            Assert.Contains("{0}", value);
            Assert.Contains("{1}", value);
            Assert.Contains("{2}", value);
            if (language != "en") Assert.NotEqual(english, value);
        }
    }

    [Fact]
    public void BolunmusSondaLumaKolunuAyriSondaninFiltresiyleTasir()
    {
        var args = ComplexityProbe.SplitArgs("in.mkv", 2, (320, 180), "veryfast", SpeedMode.Quality, "full.mkv", "half.mkv");

        var graph = args[Array.IndexOf(args, "-filter_complex") + 1];
        Assert.Equal($"[0:v]split=3[full][raw][lraw];[raw]scale=320:180[small];[lraw]{ComplexityProbe.LumaFilter}[luma]", graph);
        Assert.Equal(new[] { "-map", "[luma]", "-f", "null", "-" }, args[^5..]);
        Assert.Equal("[full]", args[Array.IndexOf(args, "-map") + 1]);

        var luma = ComplexityProbe.LumaArgs("in.mkv", 2, 2);
        Assert.Equal(luma[..8], new[] { "-hide_banner", "-nostdin" }.Concat(args[3..9]).ToArray());
    }

    [FfmpegFact]
    public async Task BirlesikSondaAyriSondaylaAyniLumayiVeKareyiOkur()
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "karanlik-gecis", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var clip = Path.Combine(dir, "clip.mp4");
            using (var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, new[] { "-y", "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=24:duration=8,eq=brightness=-0.3", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", clip }) })
            {
                process.Start();
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                await Task.WhenAll(stdout, stderr);
                Assert.Equal(0, process.ExitCode);
            }

            foreach (var start in new[] { 1.0, 5.0 })
            {
                var ayri = await ComplexityProbe.LumaSampleAsync(clip, start, 2, default);
                var pencere = await ComplexityProbe.SampleWindowAsync(clip, start, (320, 180), "veryfast", SpeedMode.Quality, null, default);
                var (_, tekKare) = await ComplexityProbe.SampleAsync(clip, start, 2, null, "veryfast", SpeedMode.Quality, default);

                Assert.NotNull(ayri);
                Assert.Equal(ayri, pencere.MeanLuma);
                Assert.Equal(tekKare, pencere.FullFrames);
                Assert.Equal(tekKare, pencere.HalfFrames);
            }
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static async Task<string> KlipAsync(string dir, string source)
    {
        var clip = Path.Combine(dir, "clip.mp4");
        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, new[] { "-y", "-f", "lavfi", "-i", source, "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", clip }) };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        Assert.Equal(0, process.ExitCode);
        return clip;
    }

    [FfmpegFact]
    public async Task YarimBoyuOlmayanKucukKaynaktaAyriSondaLumayiOlcer()
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "karanlik-gecis", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var clip = await KlipAsync(dir, "color=c=0x101010:size=100x100:rate=12:duration=8");
            var info = await FfprobeClient.ProbeAsync(clip);

            var pencere = await ComplexityProbe.SampleWindowAsync(clip, 1, null, "veryfast", SpeedMode.Quality, null, default);
            Assert.True(pencere.FullFrames > 0);
            Assert.Null(pencere.MeanLuma);

            var profile = (await ComplexityProbe.RunDetailedAsync(info, SpeedMode.Quality)).Profile;

            Assert.True(profile.Measured);
            Assert.NotNull(profile.MeanLuma);
            Assert.True(DarkContentSwitch.IsDark(profile.MeanLuma));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [FfmpegFact]
    public async Task BirlesikSondaLumasizDonerseAyriSondaDoldurur()
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "karanlik-gecis", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var clip = await KlipAsync(dir, "color=c=0x101010:size=320x240:rate=12:duration=8");
            var windows = new[] { 1.0, 5.0 };
            var samples = new[]
            {
                new ComplexityProbe.WindowSample(1000, 24, 300, 24),
                new ComplexityProbe.WindowSample(1000, 24, 300, 24, MeanLuma: 99.0)
            };

            var lumas = await ComplexityProbe.WindowLumasAsync(clip, windows, samples, default);

            Assert.Equal(await ComplexityProbe.LumaSampleAsync(clip, 1.0, 2, default), lumas[0]);
            Assert.NotNull(lumas[0]);
            Assert.True(DarkContentSwitch.IsDark(lumas[0]));
            Assert.Equal(99.0, lumas[1]);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
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
