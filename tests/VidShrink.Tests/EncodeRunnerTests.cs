using System.Diagnostics;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class EncodeRunnerTests
{
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

        var corrected = PlanCalculator.Correct(stopped, actualMb: 1.0, targetMb: 5.0, durationSeconds: 2);
        Assert.False(corrected.StopsShortOfBandOnPurpose);

        Assert.False(CrfPlan().StopsShortOfBandOnPurpose);
    }

    [FfmpegFact]
    public async Task PlannedStopAboveTheHardFloorIsDeliveredWithoutARetry()
    {
        await WithClipAsync(async (info, outputPath, naturalMb) =>
        {
            var targetMb = naturalMb / 0.88;
            var band = FillBand.For(targetMb);

            var result = await new EncodeRunner().RunAsync(
                info, CrfPlan(ReasonCode.PredictedQualityMeasured), outputPath, targetMb,
                progress: null, ct: CancellationToken.None, fillPolicy: FillPolicy.FillTarget);

            Assert.True(result.Success);
            Assert.InRange(result.OutputMb, band.HardFloorMb, band.LowerMb);
            Assert.Equal(1, result.Attempts);
            Assert.DoesNotContain(result.Trace ?? Array.Empty<EncodeAttempt>(), a => a.Branch == "under band");
        });
    }

    [FfmpegFact]
    public async Task UnderBandAccidentAboveTheHardFloorStillRetries()
    {
        await WithClipAsync(async (info, outputPath, naturalMb) =>
        {
            var targetMb = naturalMb / 0.88;

            var result = await new EncodeRunner().RunAsync(
                info, CrfPlan(ReasonCode.PredictedQualityMeasured, ReasonCode.FillCrfLowered), outputPath, targetMb,
                progress: null, ct: CancellationToken.None, fillPolicy: FillPolicy.FillTarget);

            Assert.True(result.Success);
            Assert.True(result.Attempts > 1,
                $"doldurmayi hedefleyen plan band altinda kalinca yeniden denemeliydi, {result.Attempts} deneme oldu");
            Assert.Contains(result.Trace ?? Array.Empty<EncodeAttempt>(), a => a.Branch == "under band");
        });
    }

    [FfmpegFact]
    public async Task PlannedStopUnderTheHardFloorStillRetries()
    {
        await WithClipAsync(async (info, outputPath, naturalMb) =>
        {
            var targetMb = naturalMb / 0.5;
            var band = FillBand.For(targetMb);
            Assert.True(naturalMb < band.HardFloorMb);

            var result = await new EncodeRunner().RunAsync(
                info, CrfPlan(ReasonCode.PredictedQualityMeasured), outputPath, targetMb,
                progress: null, ct: CancellationToken.None, fillPolicy: FillPolicy.FillTarget);

            Assert.True(result.Success);
            Assert.True(result.Attempts > 1,
                $"sert tabanin altina dusen bilerek durma yeniden denenmeliydi, {result.Attempts} deneme oldu");
            Assert.Contains(result.Trace ?? Array.Empty<EncodeAttempt>(), a => a.Branch == "under band");
        });
    }

    [FfmpegFact]
    public async Task TheSentenceShownToTheUserAgreesWithWhatTheRunnerDelivers()
    {
        await WithClipAsync(async (info, outputPath, naturalMb) =>
        {
            foreach (var factor in new[] { 0.88, 0.5 })
            {
                var targetMb = naturalMb / factor;
                var plan = StopPlan(naturalMb, targetMb);
                var promisesStop = MainWindow.ShowsMeasuredQualityStop(plan, plan.ReasonCodes[0], FillPolicy.FillTarget);

                var result = await new EncodeRunner().RunAsync(
                    info, plan, outputPath, targetMb,
                    progress: null, ct: CancellationToken.None, fillPolicy: FillPolicy.FillTarget);

                Assert.True(result.Success);
                Assert.True(promisesStop == (result.Attempts == 1),
                    $"hedef {targetMb:0.###} MB: arayuz 'burada durur' dedi={promisesStop}, kosucu tek denemede durdu={result.Attempts == 1}");
            }
        });
    }

    private static EncodePlan StopPlan(double ceilingMb, double targetMb)
    {
        var plan = CrfPlan();
        plan.ReasonCodes = new List<ReasonNote>
        {
            new(ReasonCode.BudgetExceedsCeiling, Crf: 30, Mb: ceilingMb, TargetMb: targetMb)
        };
        return plan;
    }

    private static EncodePlan CrfPlan(params ReasonCode[] codes) => new()
    {
        Codec = "libx264",
        Mode = "crf",
        Crf = 30,
        VideoBitrateK = 200,
        AudioCodec = null,
        AudioBitrateK = 0,
        Width = 320,
        Height = 240,
        Fps = 10,
        Preset = "ultrafast",
        ReasonCodes = codes.Select(code => new ReasonNote(code)).ToList()
    };

    private static async Task WithClipAsync(Func<MediaInfo, string, double, Task> body)
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "encode-runner", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var source = Path.Combine(dir, "source.mp4");
            await RunFfmpegAsync(new[]
            {
                "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=10:duration=4",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", source
            });

            var info = new MediaInfo
            {
                FilePath = source,
                FileSizeBytes = new FileInfo(source).Length,
                DurationSeconds = 4,
                Width = 320,
                Height = 240,
                Fps = 10,
                VideoCodec = "h264",
                TotalBitrateBps = 400_000
            };

            var probePath = Path.Combine(dir, "natural.mp4");
            var natural = await new EncodeRunner().RunAsync(
                info, CrfPlan(), probePath, targetMb: 1000, progress: null,
                ct: CancellationToken.None, fillPolicy: FillPolicy.QualityCeiling);
            Assert.True(natural.Success);
            Assert.True(natural.OutputMb > 0);
            File.Delete(probePath);

            await body(info, Path.Combine(dir, "out.mp4"), natural.OutputMb);
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
