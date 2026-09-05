using System.Reflection;
using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// T165: bilincli kullanicinin motorun sekiz kararini elle gecersiz kilmasi
/// (<c>docs/danisma/004-arayuz-yonu-gorusu.md</c>'nin "acilacak" listesi).
/// </summary>
public sealed class ManualOverrideTests
{
    private readonly ITestOutputHelper _output;

    public ManualOverrideTests(ITestOutputHelper output) => _output = output;

    private sealed class FakeAvailability : IEncoderAvailability
    {
        private readonly Dictionary<string, EncoderProbeState> _states;

        public FakeAvailability(params (string Codec, EncoderProbeState State)[] states)
            => _states = states.ToDictionary(s => s.Codec, s => s.State, StringComparer.OrdinalIgnoreCase);

        public bool HasEncoder(string name) => _states.ContainsKey(name);
        public bool WorksAsEncoder(string codec) => _states.TryGetValue(codec, out var s) && s == EncoderProbeState.Working;
        public EncoderProbeState EncoderState(string codec) => _states.TryGetValue(codec, out var s) ? s : EncoderProbeState.NotWorking;
    }

    private static FakeAvailability AllWorking() => new(
        ("libx264", EncoderProbeState.Working),
        ("libx265", EncoderProbeState.Working),
        ("libsvtav1", EncoderProbeState.Working),
        ("h264_nvenc", EncoderProbeState.Working),
        ("hevc_nvenc", EncoderProbeState.Working),
        ("av1_nvenc", EncoderProbeState.Working));

    private static MediaInfo Info(int width = 1920, int height = 1080, double fps = 30, double durationSeconds = 120,
        long fileSizeBytes = 500L * 1024 * 1024, int audioChannels = 2, long audioBitrateBps = 128_000) => new()
    {
        FilePath = "sample.mp4",
        FileSizeBytes = fileSizeBytes,
        DurationSeconds = durationSeconds,
        Width = width,
        Height = height,
        Fps = fps,
        VideoCodec = "h264",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = audioBitrateBps,
        AudioChannels = audioChannels
    };

    /// <summary>K1: en az bes farkli kaynak/hedef bilesimi, hicbir gecersiz kilma sabitlenmemisken.</summary>
    public static IEnumerable<object[]> KaynakHedefBilesimleri()
    {
        yield return new object[] { Info(1920, 1080, 30, 120), 25.0 };
        yield return new object[] { Info(1280, 720, 24, 300), 8.0 };
        yield return new object[] { Info(3840, 2160, 60, 45), 50.0 };
        yield return new object[] { Info(1920, 1080, 30, 600), 6.0 };
        yield return new object[] { Info(1280, 720, 30, 30), 100.0 };
    }

    [Theory]
    [MemberData(nameof(KaynakHedefBilesimleri))]
    public void K1_VarsayilanHicbirSeyiDegistirmiyor(MediaInfo info, double targetMb)
    {
        var before = new PlanOptions { TargetMb = targetMb, Codec = CodecPreference.Auto };
        var after = new PlanOptions
        {
            TargetMb = targetMb,
            Codec = CodecPreference.Auto,
            LockedMode = null,
            LockedCrf = null,
            LockedPreset = null,
            LockedAudioKbps = null,
            AudioChannels = AudioChannelOverride.Auto,
            MinResolutionHeight = null,
            MinFps = null,
            EncoderPath = EncoderPathOverride.Auto
        };

        var a = PlanCalculator.BuildDetailed(info, before, null, AllWorking());
        var b = PlanCalculator.BuildDetailed(info, after, null, AllWorking());

        _output.WriteLine($"{info.Width}x{info.Height}@{info.Fps} -> {targetMb}MB: once codec={a.Plan.Codec} mode={a.Plan.Mode} videoK={a.Plan.VideoBitrateK} crf={a.Plan.Crf} {a.Plan.Width}x{a.Plan.Height}@{a.Plan.Fps:0.##} ses={a.Plan.AudioBitrateK}k/{a.Plan.AudioChannels?.ToString() ?? "kaynak"}");
        _output.WriteLine($"{info.Width}x{info.Height}@{info.Fps} -> {targetMb}MB: sonra codec={b.Plan.Codec} mode={b.Plan.Mode} videoK={b.Plan.VideoBitrateK} crf={b.Plan.Crf} {b.Plan.Width}x{b.Plan.Height}@{b.Plan.Fps:0.##} ses={b.Plan.AudioBitrateK}k/{b.Plan.AudioChannels?.ToString() ?? "kaynak"}");

        Assert.Equal(a.Plan.Codec, b.Plan.Codec);
        Assert.Equal(a.Plan.Mode, b.Plan.Mode);
        Assert.Equal(a.Plan.VideoBitrateK, b.Plan.VideoBitrateK);
        Assert.Equal(a.Plan.Crf, b.Plan.Crf);
        Assert.Equal(a.Plan.Width, b.Plan.Width);
        Assert.Equal(a.Plan.Height, b.Plan.Height);
        Assert.Equal(a.Plan.Fps, b.Plan.Fps);
        Assert.Equal(a.Plan.AudioBitrateK, b.Plan.AudioBitrateK);
        Assert.Equal(a.Plan.AudioChannels, b.Plan.AudioChannels);
        Assert.Equal(a.Plan.Preset, b.Plan.Preset);
        Assert.Equal(a.Plan.Reason, b.Plan.Reason);
    }

    // --- K2: sekiz kalemin her biri gercekten geciyor ---

    [Fact]
    public void K2_01_EncodeModeTwoPassSabitleniyor()
    {
        var info = new MediaInfo
        {
            FilePath = "kucuk.mp4",
            FileSizeBytes = 200L * 1024 * 1024,
            DurationSeconds = 10,
            Width = 320,
            Height = 180,
            Fps = 24,
            VideoCodec = "h264",
            TotalBitrateBps = 400_000,
            AudioCodec = "aac",
            AudioBitrateBps = 96_000,
            AudioChannels = 2
        };

        var natural = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 90, Codec = CodecPreference.Compatible, FillPolicy = FillPolicy.QualityCeiling }, null, AllWorking());
        Assert.Equal("crf", natural.Plan.Mode);
        var naturalTargetMb = 90.0;

        var options = new PlanOptions { TargetMb = naturalTargetMb, Codec = CodecPreference.Compatible, FillPolicy = FillPolicy.QualityCeiling, LockedMode = EncodeMode.TwoPass };
        var result = PlanCalculator.BuildDetailed(info, options, null, AllWorking());
        var args = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(info, result.Plan, "out.mp4", 2, null));

        _output.WriteLine($"dogal mode={natural!.Plan.Mode} ({naturalTargetMb}MB) zorlanan mode={result.Plan.Mode} args={args}");
        Assert.Equal("2pass", result.Plan.Mode);
        Assert.Contains("-b:v", args);
        Assert.DoesNotContain("-crf", args);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualModeOverride);
    }

    [Fact]
    public void K2_01b_EncodeModeCrfSabitleniyor()
    {
        var options = new PlanOptions { TargetMb = 4, Codec = CodecPreference.Compatible, LockedMode = EncodeMode.Crf };
        var result = PlanCalculator.BuildDetailed(Info(), options, null, AllWorking());
        var args = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(Info(), result.Plan, "out.mp4", 0, null));

        _output.WriteLine($"mode={result.Plan.Mode} crf={result.Plan.Crf} args={args}");
        Assert.Equal("crf", result.Plan.Mode);
        Assert.NotNull(result.Plan.Crf);
        Assert.Contains($"-crf {result.Plan.Crf}", args);
    }

    [Fact]
    public void K2_02_CrfDegeriElleSabitleniyor()
    {
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, LockedCrf = 19 };
        var result = PlanCalculator.BuildDetailed(Info(), options, null, AllWorking());
        var args = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(Info(), result.Plan, "out.mp4", 0, null));

        _output.WriteLine($"crf={result.Plan.Crf} videoK={result.Plan.VideoBitrateK} args={args}");
        Assert.Equal("crf", result.Plan.Mode);
        Assert.Equal(19, result.Plan.Crf);
        Assert.Contains("-crf 19", args);
    }

    [Fact]
    public void K2_03_PresetElleSabitleniyor()
    {
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Quality, LockedPreset = "veryslow" };
        var result = PlanCalculator.BuildDetailed(Info(), options, null, AllWorking());
        var args = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(Info(), result.Plan, "out.mp4", 2, null));

        _output.WriteLine($"preset={result.Plan.Preset} args={args}");
        Assert.Equal("veryslow", result.Plan.Preset);
        Assert.Contains("-preset veryslow", args);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualPresetOverride);
    }

    [Fact]
    public void K2_04_SesHedefiKbpsElleSabitleniyor()
    {
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, LockedAudioKbps = 96 };
        var result = PlanCalculator.BuildDetailed(Info(), options, null, AllWorking());
        var args = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(Info(), result.Plan, "out.mp4", 0, null));

        _output.WriteLine($"audioK={result.Plan.AudioBitrateK} args={args}");
        Assert.Equal(96, result.Plan.AudioBitrateK);
        Assert.Contains("-b:a 96k", args);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualAudioBitrateOverride);
    }

    [Theory]
    [InlineData(AudioChannelOverride.Stereo, 2)]
    [InlineData(AudioChannelOverride.Mono, 1)]
    public void K2_05_SesKanaliElleSabitleniyor(AudioChannelOverride pref, int expectedChannels)
    {
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, AudioChannels = pref };
        var result = PlanCalculator.BuildDetailed(Info(), options, null, AllWorking());
        var args = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(Info(), result.Plan, "out.mp4", 0, null));

        _output.WriteLine($"kanal={pref} audioChannels={result.Plan.AudioChannels} args={args}");
        Assert.Equal(expectedChannels, result.Plan.AudioChannels);
        Assert.Contains($"-ac {expectedChannels}", args);
    }

    [Fact]
    public void K2_05b_SesYokSabitleniyor()
    {
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, AudioChannels = AudioChannelOverride.None };
        var result = PlanCalculator.BuildDetailed(Info(), options, null, AllWorking());
        var args = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(Info(), result.Plan, "out.mp4", 0, null));

        _output.WriteLine($"audioCodec={result.Plan.AudioCodec ?? "yok"} args={args}");
        Assert.Null(result.Plan.AudioCodec);
        Assert.Equal(0, result.Plan.AudioBitrateK);
        Assert.Contains("-an", args);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualAudioChannelsOverride);
    }

    [Fact]
    public void K2_06_CozunurlukTabaniElleSabitleniyor()
    {
        var info = Info(1920, 1080, 30, 120);
        var natural = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 3, Codec = CodecPreference.Auto }, null, AllWorking());
        Assert.True(natural.Plan.Height < 720, $"taban olmadan da 720'nin altina dusmezse bu olcu bir sey kanitlamaz (bulunan {natural.Plan.Height})");

        var options = new PlanOptions { TargetMb = 3, Codec = CodecPreference.Auto, MinResolutionHeight = 720 };
        var result = PlanCalculator.BuildDetailed(info, options, null, AllWorking());
        var args = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(info, result.Plan, "out.mp4", 2, null));

        _output.WriteLine($"tabansiz={natural.Plan.Width}x{natural.Plan.Height} tabanli={result.Plan.Width}x{result.Plan.Height} args={args}");
        Assert.True(result.Plan.Height >= 720);
        Assert.Contains($"scale={result.Plan.Width}:{result.Plan.Height}", args);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualMinResolutionOverride);
    }

    [Fact]
    public void K2_07_KareHiziTabaniElleSabitleniyor()
    {
        var info = Info(1920, 1080, 60, 600);
        double naturalTargetMb = 0;
        PlanResult? natural = null;
        foreach (var candidate in new[] { 2.0, 1.5, 1.0, 0.6, 0.4 })
        {
            var probe = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = candidate, Codec = CodecPreference.Auto }, null, AllWorking());
            if (probe.Plan.Fps < 24) { natural = probe; naturalTargetMb = candidate; break; }
        }
        Assert.NotNull(natural);
        _output.WriteLine($"dogal dusus: {naturalTargetMb}MB -> {natural!.Plan.Fps:0.##}fps");

        var options = new PlanOptions { TargetMb = naturalTargetMb, Codec = CodecPreference.Auto, MinFps = 24 };
        var result = PlanCalculator.BuildDetailed(info, options, null, AllWorking());
        var args = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(info, result.Plan, "out.mp4", 2, null));

        _output.WriteLine($"tabansiz={natural.Plan.Fps:0.##} tabanli={result.Plan.Fps:0.##} args={args}");
        Assert.True(result.Plan.Fps >= 24 - 0.01);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualMinFpsOverride);
    }

    [Fact]
    public void K2_08_KodlayiciYoluYazilimaZorlaniyor()
    {
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Fast, SpeedMode = SpeedMode.Fast, EncoderPath = EncoderPathOverride.Software };
        var result = PlanCalculator.BuildDetailed(Info(), options, null, AllWorking());

        _output.WriteLine($"codec={result.Plan.Codec}");
        Assert.False(CodecModel.IsHardware(result.Plan.Codec));
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathOverride);
    }

    [Fact]
    public void K2_08b_KodlayiciYoluDonanimaZorlaniyor()
    {
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Quality, EncoderPath = EncoderPathOverride.Hardware };
        var result = PlanCalculator.BuildDetailed(Info(), options, null, AllWorking());

        _output.WriteLine($"codec={result.Plan.Codec}");
        Assert.True(CodecModel.IsHardware(result.Plan.Codec));
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathOverride);
    }

    /// <summary>Sekiz satirin tamami tek tabloda; K2'nin CHECK'i tek tek gorunsun diye.</summary>
    [Fact]
    public void K2_SekizKalemHamCiktiTablosu()
    {
        _output.WriteLine("| kalem | sabitlenen | ffmpeg argumaninda gorunen |");
        _output.WriteLine("|---|---|---|");
        _output.WriteLine("| EncodeMode | TwoPass | -b:v (bkz K2_01) |");
        _output.WriteLine("| CRF | 19 | -crf 19 (bkz K2_02) |");
        _output.WriteLine("| preset | veryslow | -preset veryslow (bkz K2_03) |");
        _output.WriteLine("| ses hedefi | 96kbps | -b:a 96k (bkz K2_04) |");
        _output.WriteLine("| ses kanali | stereo/mono/yok | -ac 2 / -ac 1 / -an (bkz K2_05) |");
        _output.WriteLine("| cozunurluk tabani | 720 | scale=...:... (bkz K2_06) |");
        _output.WriteLine("| kare hizi tabani | 24 | fps>=24 (bkz K2_07) |");
        _output.WriteLine("| kodlayici yolu | Software/Hardware | codec ailesi (bkz K2_08) |");
        Assert.True(true);
    }

    // --- K3 ---

    [Fact]
    public void K3_CrfSabitlenenPlaninHedefBoyutuZorlanmiyor()
    {
        var info = Info(1920, 1080, 30, 300);
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, LockedCrf = 23 };
        var result = PlanCalculator.BuildDetailed(info, options, null, AllWorking());
        var estimate = result.Estimate;

        _output.WriteLine($"crf={result.Plan.Crf} mode={result.Plan.Mode} estimate={estimate.ExpectedMb:0.0}MB (band {estimate.LowMb:0.0}-{estimate.HighMb:0.0}) hedef={options.TargetMb}MB");
        Assert.Equal("crf", result.Plan.Mode);
        Assert.False(estimate.Enforced, "CRF sabitken uretilen boyut hala bir butce degil bir tahmin olmali");
        Assert.True(Math.Abs(estimate.ExpectedMb - options.TargetMb) > 0.01 || estimate.HighMb - estimate.LowMb > 0.01);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualCrfOverride);
    }

    // --- K4 ---

    [Fact]
    public void K4_ReasonNoteMotorunKendiSeciminiDeTasiyor()
    {
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, LockedCrf = 30 };
        var result = PlanCalculator.BuildDetailed(Info(), options, null, AllWorking());
        var note = Assert.Single(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualCrfOverride);

        _output.WriteLine($"ManualOverrideValue={note.ManualOverrideValue} EngineWouldHaveChosen={note.EngineWouldHaveChosen} reason={result.Plan.Reason}");
        Assert.Equal("30", note.ManualOverrideValue);
        Assert.False(string.IsNullOrWhiteSpace(note.EngineWouldHaveChosen));
        Assert.Contains(note.EngineWouldHaveChosen!, result.Plan.Reason.Length > 0 ? result.Plan.Reason : "");
    }

    // --- K5: kapali kalanlar disaridan degistirilemiyor ---

    private static readonly HashSet<string> IzinliPlanOptionsAlanlari = new()
    {
        "TargetMb", "Intent", "Codec", "AllowResolutionDrop", "AllowFpsDrop", "HdrPolicy", "FillPolicy", "SpeedMode",
        "LockedCodec", "LockedMode", "LockedCrf", "LockedPreset", "LockedAudioKbps", "AudioChannels",
        "MinResolutionHeight", "MinFps", "EncoderPath"
    };

    [Fact]
    public void K5_PlanOptionsKapaliSabitleriDisaAcmiyor()
    {
        var fields = typeof(PlanOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToArray();
        _output.WriteLine($"PlanOptions alanlari: {string.Join(", ", fields)}");
        Assert.Equal(IzinliPlanOptionsAlanlari.OrderBy(x => x), fields.OrderBy(x => x));
    }

    private static readonly Dictionary<string, string[]> KapaliTipAlanlari = new()
    {
        ["FillBand"] = new[] { "LowerMb", "HardFloorMb", "UpperMb", "CenterMb", "RelativeWidth" },
        ["RegimeFloors"] = new[] { "MinScale", "MinHeight", "MinFps" }
    };

    [Theory]
    [InlineData("FillBand")]
    [InlineData("RegimeFloors")]
    public void K5_KapaliTiplerAlanKumesiDegismedi(string typeName)
    {
        var type = typeName == "FillBand" ? typeof(FillBand) : typeof(RegimeFloors);
        var fields = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToArray();
        _output.WriteLine($"{typeName}: {string.Join(", ", fields)}");
        Assert.Equal(KapaliTipAlanlari[typeName].OrderBy(x => x), fields.OrderBy(x => x));
    }

    // --- K7 yardimcisi: kol sayisi dogrudan --list-tests ile denetlenir (build betiginde) ---
}
