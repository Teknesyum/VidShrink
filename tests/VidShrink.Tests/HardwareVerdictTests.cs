using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Gerçek ffmpeg yoklaması koşturan ölçümler. Anahtar yoksa test <b>atlanır</b>, sessizce
/// geçmez — hiçbir şey sınamayan yeşil bırakmamak için.
/// </summary>
public sealed class LiveProbeFactAttribute : FactAttribute
{
    public LiveProbeFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_PROBE")))
            Skip = "VIDSHRINK_LIVE_PROBE verilmedi, gerçek donanım yoklaması koşturulmadı.";
        else if (!ToolLocator.IsAvailable(out _))
            Skip = "ffmpeg bulunamadı, gerçek donanım yoklaması koşturulmadı.";
    }
}

public class HardwareVerdictTests
{
    private readonly ITestOutputHelper _output;

    public HardwareVerdictTests(ITestOutputHelper output) => _output = output;

    /// <summary>Uygulamanın yoklama için kullandığı temsili kaynak (MainWindow.HardwareProbeSource).</summary>
    private static MediaInfo ProbeSource() => new()
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

    /// <summary>
    /// Ölçüm kullanıcının gerçek ayar dosyasına dokunmaz. Yol pencereye doğrudan verilir;
    /// VIDSHRINK_SETTINGS_PATH süreç geneli olduğu ve başka bir ölçüm sınıfı onu paralelde
    /// sıfırladığı için buradan kullanılmıyor — o yarışta yazma isteği kullanıcının
    /// %APPDATA% altındaki dosyasına düşerdi.
    /// </summary>
    [Fact]
    public void TheSettingsPathOverrideKeepsTheTestOutOfAppData()
    {
        WithSettingsFile<object?>(file => AppHost.Run<object?>(() =>
        {
            Assert.NotEqual(file, UpdateSettings.DefaultPath);

            var window = new MainWindow { SettingsPathOverride = file };
            window.ApplyHardwareVerdict(null, true, UsableVerdict);

            Assert.True(File.Exists(file));
            Assert.True(UpdateSettings.Load(file).FastGpu);
            return null;
        }));
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

    // --- K3: kullanıcının gördüğü yol, uçtan uca ---

    /// <summary>
    /// Pencere + ayar dosyası + kutu, gerçek ffmpeg'e gitmeden. Yoklamanın sonucu
    /// <see cref="MainWindow.ApplyHardwareVerdict"/> ile besleniyor; geri kalan yol
    /// (<c>ResolveFastGpuSetting</c>, <c>_syncing</c> kapısı, <c>OnFastGpuChanged</c>)
    /// uygulamadaki hâliyle koşuyor.
    /// </summary>
    private static T WithSettingsFile<T>(Func<string, T> work)
    {
        // Ortam değişkeni süreç geneli ve başka bir ölçüm sınıfı onu paralelde sıfırlıyor;
        // pencere yolunu sınayan ölçümler dosyayı MainWindow'a doğrudan veriyor.
        var file = Path.Combine(Path.GetTempPath(), $"vidshrink-fastgpu-{Guid.NewGuid():N}.json");
        try
        {
            return work(file);
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    private static readonly HardwareVerdict UsableVerdict =
        HardwareVerdict.Decide(Passed("av1_nvenc", 193), 1074, 882, 496, 20);

    private static readonly HardwareVerdict ClosedVerdict =
        HardwareVerdict.Decide(Passed("av1_nvenc"), CodecModel.UsableBitrateK("av1_nvenc", 1920, 1080, 30) - 1, 1920, 1080, 30);

    /// <summary>Açılışı taklit eder: pencere kurulur ve yoklamanın sonucu bağlanır.</summary>
    private static bool Launch(string settingsFile, HardwareVerdict verdict)
    {
        var window = new MainWindow { SettingsPathOverride = settingsFile };
        window.ApplyHardwareVerdict(null, true, verdict);
        return window.ChkFastGpu.IsChecked == true;
    }

    /// <summary>Kullanıcının kutuyu elle değiştirmesi — programın yazdığı yoldan geçmez.</summary>
    private static void ToggleByHand(MainWindow window, bool on) => window.ChkFastGpu.IsChecked = on;

    [Fact]
    public void TheBoxTheUserTurnedOffStaysOffOnTheNextLaunch()
    {
        WithSettingsFile<object?>(file => AppHost.Run<object?>(() =>
        {
            Assert.True(UsableVerdict.EnableFastMode);

            // 1. İlk açılış: karar kutuyu kendiliğinden açar ve dosyaya yazar.
            var first = new MainWindow { SettingsPathOverride = file };
            first.ApplyHardwareVerdict(null, true, UsableVerdict);
            Assert.True(first.ChkFastGpu.IsChecked);
            Assert.True(UpdateSettings.Load(file).FastGpu);

            // 2. Kullanıcı kutuyu elle kapatır.
            ToggleByHand(first, false);
            Assert.False(UpdateSettings.Load(file).FastGpu);

            // 3. Sonraki açılış: karar yine açık öneriyor ama dosya ezilmiyor.
            Assert.False(Launch(file, UsableVerdict));
            Assert.False(UpdateSettings.Load(file).FastGpu);

            return null;
        }));
    }

    [Fact]
    public void TheBoxTheUserTurnedOnStaysOnOnTheNextLaunch()
    {
        WithSettingsFile<object?>(file => AppHost.Run<object?>(() =>
        {
            Assert.False(ClosedVerdict.EnableFastMode);

            // 1. İlk açılış: karar kapalı öneriyor, dosyaya false yazılıyor.
            var first = new MainWindow { SettingsPathOverride = file };
            first.ApplyHardwareVerdict(null, true, ClosedVerdict);
            Assert.False(first.ChkFastGpu.IsChecked);
            Assert.False(UpdateSettings.Load(file).FastGpu);

            // 2. Kullanıcı kutuyu elle açar.
            ToggleByHand(first, true);
            Assert.True(UpdateSettings.Load(file).FastGpu);

            // 3. Sonraki açılış: karar yine kapalı öneriyor ama kutu açık kalıyor.
            Assert.True(Launch(file, ClosedVerdict));
            Assert.True(UpdateSettings.Load(file).FastGpu);

            return null;
        }));
    }

    /// <summary>
    /// Elle açılan kutunun ipucu satırı da doğruyu söylemeli — K4'ün uçtan uca hâli.
    /// </summary>
    [Fact]
    public void TheTipFollowsTheBoxTheUserOpened()
    {
        WithSettingsFile<object?>(file => AppHost.Run<object?>(() =>
        {
            var window = new MainWindow { SettingsPathOverride = file };
            window.ApplyHardwareVerdict(null, true, ClosedVerdict);
            ToggleByHand(window, true);

            var line = MainWindow.FastGpuVerdictLine(ClosedVerdict, window.ChkFastGpu.IsChecked == true, "tr");

            Assert.NotNull(line);
            Assert.DoesNotContain("kapalı kaldı", line!, StringComparison.OrdinalIgnoreCase);
            return null;
        }));
    }

    // --- K4: ipucunun son satırı ---

    private static HardwareVerdict Closed(HardwareVerdictReason reason) => reason switch
    {
        HardwareVerdictReason.ProbeFailed => HardwareVerdict.Decide(new EncoderProbeResult("av1_nvenc", false, 0), 1074, 882, 496, 20),
        HardwareVerdictReason.ProbeSlow => HardwareVerdict.Decide(Passed("av1_nvenc", HardwareVerdict.ProbeBudgetMs + 1), 1074, 882, 496, 20),
        _ => HardwareVerdict.Decide(Passed("av1_nvenc"), CodecModel.UsableBitrateK("av1_nvenc", 1920, 1080, 30) - 1, 1920, 1080, 30)
    };

    [Fact]
    public void WithoutAProbeThereIsNoVerdictLine()
    {
        Assert.Null(MainWindow.FastGpuVerdictLine(HardwareVerdict.NotProbed, false, "tr"));
    }

    /// <summary>Gövde zaten donanım bulunamadığını yazıyor; ikinci kez yazılmaz.</summary>
    [Fact]
    public void WithoutHardwareTheBodyAlreadySaysIt()
    {
        var verdict = HardwareVerdict.Decide(Passed("libx264"), 1074, 882, 496, 20);

        Assert.Null(MainWindow.FastGpuVerdictLine(verdict, false, "tr"));
    }

    [Theory]
    [InlineData("tr")]
    [InlineData("en")]
    public void AnOpenedBoxIsAnnouncedWithItsMeasurement(string language)
    {
        var verdict = HardwareVerdict.Decide(Passed("av1_nvenc", 193), 1074, 882, 496, 20);
        var line = MainWindow.FastGpuVerdictLine(verdict, true, language);

        Assert.NotNull(line);
        Assert.Contains("193", line!, StringComparison.Ordinal);
        Assert.Contains("1074", line, StringComparison.Ordinal);
        Assert.Contains(verdict.UsableBitrateK.ToString(), line, StringComparison.Ordinal);
    }

    /// <summary>
    /// K4: ölçüm kapalı önerdiği hâlde kullanıcı kutuyu elle açtıysa satır "kapalı kaldı"
    /// diyemez. Üç kapalı dalın üçü de kutunun gerçek durumunu okumalı.
    /// </summary>
    [Theory]
    [InlineData(HardwareVerdictReason.ProbeFailed)]
    [InlineData(HardwareVerdictReason.ProbeSlow)]
    [InlineData(HardwareVerdictReason.BitrateFloorTooHigh)]
    public void AClosedVerdictDoesNotClaimTheBoxIsOffWhenItIsOn(HardwareVerdictReason reason)
    {
        var verdict = Closed(reason);
        Assert.Equal(reason, verdict.Reason);
        Assert.False(verdict.EnableFastMode);

        var off = MainWindow.FastGpuVerdictLine(verdict, false, "tr");
        var on = MainWindow.FastGpuVerdictLine(verdict, true, "tr");

        Assert.NotNull(off);
        Assert.NotNull(on);
        Assert.Contains("kapalı kaldı", off!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kapalı kaldı", on!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("elle açıldı", on, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HardwareVerdictReason.ProbeFailed)]
    [InlineData(HardwareVerdictReason.ProbeSlow)]
    [InlineData(HardwareVerdictReason.BitrateFloorTooHigh)]
    public void AClosedVerdictKeepsItsMeasurementInBothStates(HardwareVerdictReason reason)
    {
        var verdict = Closed(reason);

        foreach (var language in new[] { "tr", "en" })
        foreach (var on in new[] { true, false })
        {
            var line = MainWindow.FastGpuVerdictLine(verdict, on, language);

            Assert.NotNull(line);
            Assert.StartsWith("•", line!, StringComparison.Ordinal);
            Assert.Contains("nvenc", line, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Yoklaması hiç bulunamayan kodlayıcı için "bulundu ama" denmemeli.</summary>
    [Fact]
    public void AMissingEncoderIsNotDescribedAsPresent()
    {
        var verdict = HardwareVerdict.Decide(EncoderProbeResult.Missing("av1_nvenc"), 1074, 882, 496, 20);
        var line = MainWindow.FastGpuVerdictLine(verdict, false, "tr");

        Assert.NotNull(line);
        Assert.DoesNotContain("bulundu", line!, StringComparison.OrdinalIgnoreCase);
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
    [LiveProbeFact]
    public void LiveProbeDecidesOnThisMachine()
    {
        var capabilities = EncoderCapabilities.Instance;
        var plan = PlanCalculator.Build(
            ProbeSource(),
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

    /// <summary>
    /// Pencereyi kurup ilk yerleşimi tamamlamak ne kadar sürüyor — bir kez yoklama koşarken,
    /// bir kez yoklama hiç başlatılmadan. Ölçülen şey arayüzün beklediği süre, yoklamanın
    /// süresi değil. Pencere gösterilmiyor; <see cref="AppHost"/> Avalonia'yı kendi
    /// iş parçacığında kuruyor.
    /// </summary>
    [LiveProbeFact]
    public void TheFirstLayoutDoesNotWaitForTheProbe()
    {
        WithSettingsFile<object?>(file =>
        {
            // İlk pencere JIT ve kaynak sözlüğü maliyetini yutar; ölçülen hepsi sıcak.
            AppHost.Run(() => LayOutFirstWindow(file, false));

            // İlk yoklamalı tur ffmpeg'i gerçekten çağırır, sonrakiler _probed önbelleğini
            // bulur — gerçek uygulamada da süreç başına tek yoklama var.
            var withProbe = new List<double>();
            var withoutProbe = new List<double>();
            for (var round = 0; round < 5; round++)
            {
                withoutProbe.Add(AppHost.Run(() => LayOutFirstWindow(file, false)));
                withProbe.Add(AppHost.Run(() => LayOutFirstWindow(file, true)));
            }

            _output.WriteLine($"first layout without probe: {string.Join(" / ", withoutProbe.Select(v => v.ToString("0.0")))} ms");
            _output.WriteLine($"first layout with probe:    {string.Join(" / ", withProbe.Select(v => v.ToString("0.0")))} ms");
            _output.WriteLine($"median without={Median(withoutProbe):0.0}ms with={Median(withProbe):0.0}ms difference={Median(withProbe) - Median(withoutProbe):0.0}ms");
            var spread = withoutProbe.Max() - withoutProbe.Min();
            var difference = Median(withProbe) - Median(withoutProbe);
            _output.WriteLine($"spread of the probe-free runs={spread:0.0}ms");

            // Yoklama yerleşimi bekletseydi fark yoklamanın kendi süresi kadar olurdu.
            // Kapı o sürenin yarısına konuldu: ölçüm gürültüsü (yoklamasız turların kendi
            // yayılımı) kararı çevirmesin, gerçek bir serileşme de gözden kaçmasın.
            var probeMs = ProbeMillisecondsOfThisMachine();
            _output.WriteLine($"probe itself={probeMs}ms, gate={probeMs / 2.0:0.0}ms");
            Assert.True(
                difference < probeMs / 2.0,
                $"Yoklama ilk yerleşimi {difference:0.0} ms geciktirdi; yoklamanın kendisi {probeMs} ms sürüyor.");

            return null;
        });
    }

    /// <summary>Bu makinede seçilen kodlayıcının yoklama süresi; süreç başına önbellekli.</summary>
    private static long ProbeMillisecondsOfThisMachine()
    {
        var capabilities = EncoderCapabilities.Instance;
        var plan = PlanCalculator.Build(
            ProbeSource(),
            new PlanOptions { TargetMb = 16, Codec = CodecPreference.Auto, SpeedMode = SpeedMode.Fast },
            capabilities);
        return capabilities.Probe(plan.Codec).ElapsedMs;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
    }

    /// <summary>Pencereyi kurup ilk yerleşimi tamamlar, geçen duvar saatini ms olarak döndürür.</summary>
    private static double LayOutFirstWindow(string settingsFile, bool probe)
    {
        var clock = Stopwatch.StartNew();

        var window = new MainWindow { SettingsPathOverride = settingsFile };
        var probing = probe ? window.ProbeForMeasurement() : null;

        Assert.True(window.TryFindResource("WindowPreferredWidth", out var width));
        Assert.True(window.TryFindResource("WindowPreferredHeight", out var height));
        var size = new Size((double)width!, (double)height!);

        // Pencerenin kendi Width/Height değerleri ölçüm argümanını yutar.
        window.Width = double.NaN;
        window.Height = double.NaN;

        window.Measure(size);
        window.Arrange(new Rect(size));
        window.UpdateLayout();

        clock.Stop();

        if (probing is not null)
        {
            // Yoklamanın arayüz iş parçacığına dönen kuyruğu ölçümden sonra boşaltılıyor;
            // pencere gösterilmediği için işleri yürüten bir döngü yok.
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!probing.IsCompleted && DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
        }

        return clock.Elapsed.TotalMilliseconds;
    }
}
