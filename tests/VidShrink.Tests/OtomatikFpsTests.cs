using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class OtomatikFpsTests
{
    private readonly ITestOutputHelper _output;

    public OtomatikFpsTests(ITestOutputHelper output) => _output = output;

    private static MediaInfo Hareketli(double seconds, double sizeMb) => new()
    {
        FilePath = "hareketli.mp4",
        FileSizeBytes = (long)(sizeMb * Megabayt.Bayt),
        DurationSeconds = seconds,
        Width = 1920,
        Height = 1080,
        Fps = 60,
        VideoCodec = "h264",
        TotalBitrateBps = (long)(sizeMb * 8 * Megabayt.Bayt / seconds),
        AudioCodec = "aac",
        AudioBitrateBps = 160_000,
        AudioChannels = 2
    };

    private static ComplexityProfile Measured(double motionExponent) => new()
    {
        ReferenceBppf = 0.1264,
        Measured = true,
        MotionExponent = motionExponent,
        MotionMeasured = true,
        DetailExponent = 0.55,
        SampledSeconds = 6,
        SampledFrames = 360
    };

    [Theory]
    [InlineData(0.10)]
    [InlineData(0.60)]
    [InlineData(0.87)]
    public void Kaynak_fps_calisabilirken_otomatik_plan_kare_dusurmez(double motion)
    {
        var info = Hareketli(30, 120);
        var sawAggressive = false;

        foreach (var targetMb in new[] { 0.6, 1.1, 2.0, 4.0, 8.0 })
        {
            var result = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = targetMb }, Measured(motion));
            var noDrop = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = targetMb, AllowFpsDrop = false }, Measured(motion));
            _output.WriteLine($"{targetMb} MB {result.Advice.Regime} -> {result.Plan.Width}x{result.Plan.Height}@{result.Plan.Fps:0.##} {result.Plan.VideoBitrateK}k");

            if (result.Advice.Regime is CompressionRegime.Aggressive or CompressionRegime.Extreme) sawAggressive = true;
            Assert.Equal(info.Fps, result.Plan.Fps, 3);
            Assert.DoesNotContain(AdviceCode.FrameRateReduced, result.Advice.Notes);
            Assert.Equal((noDrop.Plan.Width, noDrop.Plan.Height, noDrop.Plan.VideoBitrateK), (result.Plan.Width, result.Plan.Height, result.Plan.VideoBitrateK));
        }

        Assert.True(sawAggressive, "Hiçbir hedef kare düşürmeye izin veren rejime girmedi; ölçü bir şey kanıtlamadı.");
    }

    [Fact]
    public void Kaynak_fps_kodlayiciyi_acamiyorsa_kare_yine_duser()
    {
        var info = Hareketli(3600, 4000);
        var dropped = 0;

        for (var targetMb = 2.0; targetMb <= 12.0; targetMb += 0.25)
        {
            var result = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = targetMb }, Measured(0.60));
            if (result.Plan.Fps >= info.Fps - 0.01) continue;

            _output.WriteLine($"{targetMb} MB -> {result.Plan.Width}x{result.Plan.Height}@{result.Plan.Fps:0.##} {result.Plan.VideoBitrateK}k");
            Assert.True(result.Advice.Notes.Contains(AdviceCode.FrameRateCutForFloor) || result.Advice.Notes.Contains(AdviceCode.TargetBelowCodecFloor),
                string.Join(", ", result.Advice.Notes));
            dropped++;
        }

        Assert.True(dropped > 0, "Kaynak fps'te kodlayıcının açılamadığı hiçbir hedefte kare düşmedi: istisna kolu ölü.");
    }

    [Fact]
    public void Calisabilirlik_duvari_en_kucuk_izinli_boyuttan_okunur()
    {
        var info = Hareketli(30, 120);
        var options = new PlanOptions { AllowResolutionDrop = true, AllowFpsDrop = true };
        var wall = PlanCalculator.SourceFpsRunnableK(info, options, "libx265", CompressionRegime.Extreme);
        var full = PlanCalculator.RunnableVideoBitrateK(1920, 1080, 60);

        Assert.True(wall < full, $"Duvar {wall}k, tam boyutun {full}k'sından küçük değil: ölçek merdiveni okunmadı.");
        Assert.True(PlanCalculator.SourceFpsRuns(info, options, "libx265", wall, CompressionRegime.Extreme));
        Assert.False(PlanCalculator.SourceFpsRuns(info, options, "libx265", wall - 1, CompressionRegime.Extreme));
    }
}
