using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class EncodeRunnerTests
{
    private const double TargetMb = 5.0;

    [Fact]
    public void PlanThatFilledTheBandDoesNotClaimADeliberateStop()
    {
        var filled = CrfPlan(ReasonCode.PredictedQualityMeasured, ReasonCode.FillCrfLowered);
        var twoPassFill = CrfPlan(ReasonCode.PredictedQualityMeasured, ReasonCode.FillTwoPassBandCenter);
        var narrowBandFill = CrfPlan(ReasonCode.PredictedQualityMeasured, ReasonCode.FillTwoPassBandTooNarrowForCrf);
        var stopped = CrfPlan(ReasonCode.PredictedQualityMeasured);

        Assert.False(filled.StopsShortOfBandOnPurpose);
        Assert.False(twoPassFill.StopsShortOfBandOnPurpose);
        Assert.False(narrowBandFill.StopsShortOfBandOnPurpose);
        Assert.True(stopped.StopsShortOfBandOnPurpose);

        var corrected = PlanCalculator.Correct(stopped, actualMb: 1.0, targetMb: TargetMb, durationSeconds: 2);
        Assert.False(corrected.StopsShortOfBandOnPurpose);

        var handBuilt = CrfPlan();
        Assert.False(handBuilt.StopsShortOfBandOnPurpose);
    }

    [FfmpegFact]
    public async Task PlannedStopBelowTheBandIsDeliveredWithoutARetry()
    {
        await WithClipAsync(async (info, outputPath) =>
        {
            var plan = CrfPlan(ReasonCode.PredictedQualityMeasured);

            var result = await new EncodeRunner().RunAsync(
                info, plan, outputPath, TargetMb, progress: null,
                ct: CancellationToken.None, fillPolicy: FillPolicy.FillTarget);

            Assert.True(result.Success);
            Assert.True(result.OutputMb < FillBand.For(TargetMb).LowerMb,
                $"olcu anlamsiz: cikti {result.OutputMb:0.000} MB, bant alt kenari {FillBand.For(TargetMb).LowerMb:0.000} MB'in altinda degil");
            Assert.Equal(1, result.Attempts);
            Assert.DoesNotContain(result.Trace ?? Array.Empty<EncodeAttempt>(), a => a.Branch == "under band");
        });
    }

    [FfmpegFact]
    public async Task UnderBandAccidentStillRetries()
    {
        await WithClipAsync(async (info, outputPath) =>
        {
            var plan = CrfPlan(ReasonCode.PredictedQualityMeasured, ReasonCode.FillCrfLowered);

            var result = await new EncodeRunner().RunAsync(
                info, plan, outputPath, TargetMb, progress: null,
                ct: CancellationToken.None, fillPolicy: FillPolicy.FillTarget);

            Assert.True(result.Success);
            Assert.True(result.Attempts > 1,
                $"doldurmayi hedefleyen plan band altinda kalinca yeniden denemeliydi, {result.Attempts} deneme oldu");
            Assert.Contains(result.Trace ?? Array.Empty<EncodeAttempt>(), a => a.Branch == "under band");
        });
    }

    private static EncodePlan CrfPlan(params ReasonCode[] codes) => new()
    {
        Codec = "libx264",
        Mode = "crf",
        Crf = 40,
        VideoBitrateK = 20,
        AudioCodec = "aac",
        AudioBitrateK = 64,
        Width = 320,
        Height = 240,
        Fps = 10,
        Preset = "ultrafast",
        ReasonCodes = codes.Select(code => new ReasonNote(code)).ToList()
    };

    private static async Task WithClipAsync(Func<MediaInfo, string, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_runner_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var source = Path.Combine(dir, "source.mp4");
            await RunFfmpegAsync(new[]
            {
                "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=10:duration=2",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", source
            });

            var info = new MediaInfo
            {
                FilePath = source,
                FileSizeBytes = new FileInfo(source).Length,
                DurationSeconds = 2,
                Width = 320,
                Height = 240,
                Fps = 10,
                VideoCodec = "h264",
                TotalBitrateBps = 400_000
            };

            await body(info, Path.Combine(dir, "out.mp4"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static async Task RunFfmpegAsync(IEnumerable<string> args)
    {
        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        Assert.Equal(0, process.ExitCode);
    }
}
