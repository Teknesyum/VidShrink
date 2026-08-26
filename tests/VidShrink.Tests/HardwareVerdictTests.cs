using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public class HardwareVerdictTests
{
    private readonly ITestOutputHelper _output;

    public HardwareVerdictTests(ITestOutputHelper output) => _output = output;

    private static EncoderProbeResult Passed(string codec, long ms = 200) => new(codec, true, ms);

    [Fact]
    public void SoftwareEncoderMeansNoHardwarePath()
    {
        var verdict = HardwareVerdict.Decide(Passed("libx264"), 1074, 882, 496, 20);

        Assert.False(verdict.EnableFastMode);
        Assert.Equal(HardwareVerdictReason.NoHardwareEncoder, verdict.Reason);
    }

    [Fact]
    public void FailedProbeLeavesTheSettingOff()
    {
        var verdict = HardwareVerdict.Decide(new EncoderProbeResult("av1_nvenc", false, 4000), 1074, 882, 496, 20);

        Assert.False(verdict.EnableFastMode);
        Assert.Equal(HardwareVerdictReason.ProbeFailed, verdict.Reason);
    }

    [Fact]
    public void MissingEncoderLeavesTheSettingOff()
    {
        var verdict = HardwareVerdict.Decide(EncoderProbeResult.Missing("av1_nvenc"), 1074, 882, 496, 20);

        Assert.False(verdict.EnableFastMode);
        Assert.Equal(HardwareVerdictReason.ProbeFailed, verdict.Reason);
    }

    [Fact]
    public void ProbePastTheBudgetLeavesTheSettingOff()
    {
        var verdict = HardwareVerdict.Decide(Passed("av1_nvenc", HardwareVerdict.ProbeBudgetMs + 1), 1074, 882, 496, 20);

        Assert.False(verdict.EnableFastMode);
        Assert.Equal(HardwareVerdictReason.ProbeSlow, verdict.Reason);
    }

    [Fact]
    public void ProbeInsideTheBudgetTurnsTheSettingOn()
    {
        var verdict = HardwareVerdict.Decide(Passed("av1_nvenc", HardwareVerdict.ProbeBudgetMs), 1074, 882, 496, 20);

        Assert.True(verdict.EnableFastMode);
        Assert.Equal(HardwareVerdictReason.Usable, verdict.Reason);
    }

    [Fact]
    public void BitrateBelowTheEncoderFloorLeavesTheSettingOff()
    {
        var usable = CodecModel.UsableBitrateK("av1_nvenc", 1920, 1080, 30);
        var verdict = HardwareVerdict.Decide(Passed("av1_nvenc"), usable - 1, 1920, 1080, 30);

        Assert.False(verdict.EnableFastMode);
        Assert.Equal(HardwareVerdictReason.BitrateFloorTooHigh, verdict.Reason);
        Assert.Equal(usable, verdict.UsableBitrateK);
    }

    [Fact]
    public void BitrateOnTheEncoderFloorIsEnough()
    {
        var usable = CodecModel.UsableBitrateK("av1_nvenc", 1920, 1080, 30);
        var verdict = HardwareVerdict.Decide(Passed("av1_nvenc"), usable, 1920, 1080, 30);

        Assert.True(verdict.EnableFastMode);
        Assert.Equal(1.0, verdict.HeadroomRatio, 3);
    }

    [Fact]
    public void MissingBitrateIsNotAVerdict()
    {
        var verdict = HardwareVerdict.Decide(Passed("av1_nvenc"), 0, 882, 496, 20);

        Assert.False(verdict.EnableFastMode);
        Assert.Equal(HardwareVerdictReason.BitrateFloorTooHigh, verdict.Reason);
    }

    [Fact]
    public void FirstRunWritesTheDecision()
    {
        var settings = new UpdateSettings();
        var verdict = HardwareVerdict.Decide(Passed("av1_nvenc"), 1074, 882, 496, 20);

        Assert.True(verdict.ApplyTo(settings));
        Assert.True(settings.FastGpu);
    }

    [Fact]
    public void FirstRunWritesAnOffDecisionToo()
    {
        var settings = new UpdateSettings();
        var verdict = HardwareVerdict.Decide(Passed("libx264"), 1074, 882, 496, 20);

        Assert.True(verdict.ApplyTo(settings));
        Assert.False(settings.FastGpu);
    }

    [Fact]
    public void AnUnprobedVerdictWritesNothing()
    {
        var settings = new UpdateSettings();

        Assert.False(HardwareVerdict.NotProbed.ApplyTo(settings));
        Assert.Null(settings.FastGpu);
    }

    [Fact]
    public void TheSettingTheUserTurnedOffIsNotOverwritten()
    {
        var settings = new UpdateSettings { FastGpu = false };
        var verdict = HardwareVerdict.Decide(Passed("av1_nvenc"), 1074, 882, 496, 20);

        Assert.True(verdict.EnableFastMode);
        Assert.False(verdict.ApplyTo(settings));
        Assert.False(settings.FastGpu);
    }

    [Fact]
    public void TheSettingTheUserTurnedOnIsNotOverwritten()
    {
        var settings = new UpdateSettings { FastGpu = true };
        var verdict = HardwareVerdict.Decide(Passed("libx264"), 1074, 882, 496, 20);

        Assert.False(verdict.EnableFastMode);
        Assert.False(verdict.ApplyTo(settings));
        Assert.True(settings.FastGpu);
    }

    [Fact]
    public void TheDecisionSurvivesTheSettingsFile()
    {
        var file = Path.Combine(Path.GetTempPath(), $"vidshrink-fastgpu-{Guid.NewGuid():N}.json");
        try
        {
            new UpdateSettings { AutoUpdate = false, FastGpu = true }.Save(file);
            var reloaded = UpdateSettings.Load(file);

            Assert.False(reloaded.AutoUpdate);
            Assert.True(reloaded.FastGpu);

            var second = HardwareVerdict.Decide(Passed("libx264"), 1074, 882, 496, 20);
            Assert.False(second.ApplyTo(reloaded));
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void AnOldSettingsFileHasNoDecisionYet()
    {
        var file = Path.Combine(Path.GetTempPath(), $"vidshrink-fastgpu-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(file, "{\"autoUpdate\": true}");

            Assert.Null(UpdateSettings.Load(file).FastGpu);
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void TheSettingsPathOverrideKeepsTheTestOutOfAppData()
    {
        var file = Path.Combine(Path.GetTempPath(), $"vidshrink-fastgpu-{Guid.NewGuid():N}.json");
        var previous = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
        try
        {
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", file);
            Assert.Equal(file, UpdateSettings.DefaultPath);

            new UpdateSettings { FastGpu = true }.Save();
            Assert.True(UpdateSettings.Load().FastGpu);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", previous);
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void ReprobeIsNeverAutomatic()
    {
        var previous = Environment.GetEnvironmentVariable("VIDSHRINK_REPROBE_HARDWARE");
        try
        {
            Environment.SetEnvironmentVariable("VIDSHRINK_REPROBE_HARDWARE", null);
            Assert.False(HardwareVerdict.ReprobeRequested());

            Environment.SetEnvironmentVariable("VIDSHRINK_REPROBE_HARDWARE", "0");
            Assert.False(HardwareVerdict.ReprobeRequested());

            Environment.SetEnvironmentVariable("VIDSHRINK_REPROBE_HARDWARE", "1");
            Assert.True(HardwareVerdict.ReprobeRequested());
        }
        finally
        {
            Environment.SetEnvironmentVariable("VIDSHRINK_REPROBE_HARDWARE", previous);
        }
    }

    /// <summary>
    /// K5: yoklama açılışı bekletmemeli. Pencere test projesinde açılamadığı için kural
    /// kaynaktan ölçülüyor — yoklama gövdesi <c>Task.Run</c> içinde kalmalı.
    /// </summary>
    [Fact]
    public void TheProbeStaysOffTheUiThread()
    {
        var code = File.ReadAllText(TipSources.WindowCodePath);
        var start = code.IndexOf("private async Task ProbeHardwareEncodersAsync()", StringComparison.Ordinal);
        Assert.True(start >= 0, "ProbeHardwareEncodersAsync MainWindow.axaml.cs içinde yok.");

        var body = code[start..code.IndexOf("private bool ResolveFastGpuSetting", start, StringComparison.Ordinal)];

        Assert.Contains("await Task.Run(", body, StringComparison.Ordinal);
        Assert.Contains("capabilities.Probe(plan.Codec)", body, StringComparison.Ordinal);
        Assert.True(
            body.IndexOf("await Task.Run(", StringComparison.Ordinal) < body.IndexOf("capabilities.Probe(plan.Codec)", StringComparison.Ordinal),
            "Yoklama Task.Run dışına çıkmış.");
    }

    /// <summary>
    /// Bu makinenin gerçek kararı. VIDSHRINK_LIVE_PROBE verilmeden koşmaz; ffmpeg'i
    /// gerçekten çağırdığı için normal takımda sessizce döner.
    /// </summary>
    [Fact]
    public void LiveProbeDecidesOnThisMachine()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_PROBE"))) return;

        var source = new MediaInfo
        {
            FilePath = "hardware-probe.mp4",
            FileSizeBytes = 200L * 1024 * 1024,
            DurationSeconds = 120,
            Width = 1920,
            Height = 1080,
            Fps = 30,
            VideoCodec = "h264",
            TotalBitrateBps = 14_000_000
        };

        var capabilities = EncoderCapabilities.Instance;
        var plan = PlanCalculator.Build(
            source,
            new PlanOptions { TargetMb = 16, Codec = CodecPreference.Auto, SpeedMode = SpeedMode.Fast },
            capabilities);
        var probe = capabilities.Probe(plan.Codec);
        var verdict = HardwareVerdict.Decide(probe, plan.VideoBitrateK, plan.Width, plan.Height, plan.Fps);

        _output.WriteLine($"codec={probe.Codec} succeeded={probe.Succeeded} elapsed={probe.ElapsedMs}ms");
        _output.WriteLine($"layout={plan.Width}x{plan.Height}@{plan.Fps} requested={verdict.RequestedBitrateK}k usable={verdict.UsableBitrateK}k headroom={verdict.HeadroomRatio:0.00}x");
        _output.WriteLine($"verdict={verdict.Reason} enableFastMode={verdict.EnableFastMode}");

        // Bu makinede AMF sürücüsü düşüyor: ffmpeg av1_amf'i listeliyor ama yoklama
        // kodlaması geçmiyor. Başarısız yoklamanın kararı gerçek donanımla ölçülüyor.
        var amf = capabilities.Probe("av1_amf");
        var amfVerdict = HardwareVerdict.Decide(amf, plan.VideoBitrateK, plan.Width, plan.Height, plan.Fps);
        _output.WriteLine($"av1_amf listed={capabilities.HasEncoder("av1_amf")} succeeded={amf.Succeeded} elapsed={amf.ElapsedMs}ms verdict={amfVerdict.Reason} enableFastMode={amfVerdict.EnableFastMode}");

        // Yoklamanın kendisi Task.Run içinde koşuyor; açılışın arayüz iş parçacığında
        // ödediği tek yeni maliyet kararın ayar dosyasıyla buluşması.
        var file = Path.Combine(Path.GetTempPath(), $"vidshrink-fastgpu-{Guid.NewGuid():N}.json");
        try
        {
            var first = System.Diagnostics.Stopwatch.StartNew();
            var settings = UpdateSettings.Load(file);
            if (verdict.ApplyTo(settings)) settings.Save(file);
            first.Stop();

            var second = System.Diagnostics.Stopwatch.StartNew();
            var again = UpdateSettings.Load(file);
            var wrote = verdict.ApplyTo(again);
            second.Stop();

            _output.WriteLine($"settings first launch={first.Elapsed.TotalMilliseconds:0.00}ms, later launch={second.Elapsed.TotalMilliseconds:0.00}ms, rewrote={wrote}");
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }
}
