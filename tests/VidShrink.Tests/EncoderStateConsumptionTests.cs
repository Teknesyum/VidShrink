using System.Diagnostics;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class EncoderStateConsumptionTests
{
    private sealed class LegacyAvailability(bool works) : IEncoderAvailability
    {
        public bool HasEncoder(string name) => true;
        public bool WorksAsEncoder(string codec) => works;
    }

    private sealed class StateAvailability(params (string Codec, EncoderProbeState State)[] answers) : IEncoderAvailability
    {
        private readonly Dictionary<string, EncoderProbeState> _answers = answers.ToDictionary(
            answer => answer.Codec,
            answer => answer.State,
            StringComparer.OrdinalIgnoreCase);

        public bool HasEncoder(string name) => _answers.ContainsKey(name);
        public bool WorksAsEncoder(string codec) =>
            _answers.TryGetValue(codec, out var state) && state == EncoderProbeState.Working;

        public EncoderProbeState EncoderState(string codec) =>
            _answers.TryGetValue(codec, out var state) ? state : EncoderProbeState.NotWorking;
    }

    private static MediaInfo SdrSource() => new()
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

    [Fact]
    public void ArayuzVarsayilaniOlculemeyeniCalismiyorSaymiyor()
    {
        IEncoderAvailability unavailable = new LegacyAvailability(false);
        IEncoderAvailability available = new LegacyAvailability(true);

        Assert.Equal(EncoderProbeState.Unmeasured, unavailable.EncoderState("libsvtav1"));
        Assert.Equal(EncoderProbeState.Unmeasured, available.EncoderState("libsvtav1"));
    }

    [Fact]
    public void PickCodecOlculmemisTercihiElemedenGeciriyor()
    {
        var availability = new StateAvailability(("libsvtav1", EncoderProbeState.Unmeasured));

        var result = PlanCalculator.BuildDetailed(
            SdrSource(),
            new PlanOptions { TargetMb = 25, Codec = CodecPreference.MaxCompression },
            null,
            availability);

        Assert.Equal("libsvtav1", result.Plan.Codec);
        Assert.True(result.HardwareNotMeasured);
    }

    [Fact]
    public void PickFastCodecOlculmemisIlkAdayiElemedenGeciriyor()
    {
        var availability = new StateAvailability(("av1_nvenc", EncoderProbeState.Unmeasured));

        var result = PlanCalculator.BuildDetailed(
            SdrSource(),
            new PlanOptions { TargetMb = 25, SpeedMode = SpeedMode.Fast },
            null,
            availability);

        Assert.Equal("av1_nvenc", result.Plan.Codec);
        Assert.True(result.HardwareNotMeasured);
    }

    [Fact]
    public void PerformanceProbeOlculmemisAdayiCalismiyorSaymiyor()
    {
        var availability = new StateAvailability(
            ("h264_nvenc", EncoderProbeState.NotWorking),
            ("h264_qsv", EncoderProbeState.Unmeasured),
            ("h264_amf", EncoderProbeState.Working));

        var selected = PerformanceProbe.SelectHardwareCodec(availability, Stopwatch.StartNew(), 0);

        Assert.Equal("h264_qsv", selected);
    }

    /// <summary>
    /// <c>EncoderCapabilities</c> biçimli sürücüsüz makine: ffmpeg nvenc'i listeliyor,
    /// önbellek soğuk, sürücü yok. <see cref="IEncoderAvailability.EncoderState"/> süreç
    /// doğurmadan "ölçülmedi" der; süreç doğuran <see cref="IEncoderAvailability.WorksAsEncoder"/>
    /// gerçekten yoklar ve kodlama sürücüsüz makinede geçmez.
    /// <c>IEncoderMeasurementState</c> taşımaz — gerçek <c>EncoderCapabilities</c> de taşımıyor.
    /// </summary>
    private sealed class ColdCapabilities : IEncoderAvailability
    {
        public bool HasEncoder(string name) => name is "av1_nvenc" or "hevc_nvenc" or "h264_nvenc";
        public bool WorksAsEncoder(string codec) => false;
    }

    private static EncodePlan ColdFastPlan() => PlanCalculator.Build(
        SdrSource(),
        new PlanOptions { TargetMb = 16, Codec = CodecPreference.Auto, SpeedMode = SpeedMode.Fast },
        new ColdCapabilities());

    /// <summary>Sürücüsüz makinede av1_nvenc yoklaması: ölçüldü ve kodlayamadı.</summary>
    private static EncoderProbeResult FailedProbe() => new("av1_nvenc", false, 42);

    /// <summary>Sürücüsü çalışan makinede aynı yoklama.</summary>
    private static EncoderProbeResult PassedProbe() => new("av1_nvenc", true, 42);

    [Fact]
    public void OlculmemisDonanimAdayiArayuzeDonanimVarDemiyor()
    {
        var plan = ColdFastPlan();

        Assert.Equal("av1_nvenc", plan.Codec);
        Assert.True(plan.CodecNotMeasured);
        Assert.False(MainWindow.HardwareAvailableFrom(plan, FailedProbe()));
    }

    [Fact]
    public void OlculmemisAdayiDogrulayanYoklamaGecerseDonanimVarDiyor()
    {
        var plan = ColdFastPlan();

        Assert.True(plan.CodecNotMeasured);
        Assert.True(MainWindow.HardwareAvailableFrom(plan, PassedProbe()));
        Assert.False(MainWindow.HardwareAvailableFrom(plan, EncoderProbeResult.Unmeasured("av1_nvenc", 30_000)));
    }

    [Fact]
    public void OlculmemisDonanimAdayiHizliKipKutusunuAcmiyor()
    {
        var file = Path.Combine(Path.GetTempPath(), $"vidshrink-t139-{Guid.NewGuid():N}.json");
        try
        {
            var (enabled, available) = AppHost.Run(() =>
            {
                var plan = ColdFastPlan();
                var window = new MainWindow { SettingsPathOverride = file };
                window.ApplyHardwareVerdict(
                    new ColdCapabilities(),
                    MainWindow.HardwareAvailableFrom(plan, FailedProbe()),
                    HardwareVerdict.NotProbed);
                return (window.ChkFastGpu.IsEnabled, window.HardwareEncoderAvailable);
            });

            Assert.False(enabled);
            Assert.False(available);
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }
}

public sealed class HdrResolverTests
{
    private sealed class UnmeasuredAvailability : IEncoderAvailability
    {
        public bool HasEncoder(string name) => name == "libsvtav1";
        public bool WorksAsEncoder(string codec) => false;
        public EncoderProbeState EncoderState(string codec) => EncoderProbeState.Unmeasured;
    }

    private static MediaInfo HdrSource() => new()
    {
        FilePath = "sample.mp4",
        FileSizeBytes = 500L * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "hevc",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p10le",
        BitDepth = 10,
        ColorPrimaries = "bt2020",
        ColorTransfer = "smpte2084",
        ColorSpace = "bt2020nc",
        IsHdr = true
    };

    [Fact]
    public void SoftwareHdrOlculmemisKodlayiciyiElemedenGeciriyor()
    {
        var result = HdrResolver.Resolve(
            HdrSource(),
            HdrPolicy.Preserve,
            "libsvtav1",
            new UnmeasuredAvailability());

        Assert.False(result.PolicyChanged);
        Assert.True(result.NotMeasured);
        Assert.Equal("yuv420p10le", result.PixelFormat);
    }
}
