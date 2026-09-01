using System.Diagnostics;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class FfmpegTheoryAttribute : TheoryAttribute
{
    public FfmpegTheoryAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
            Skip = $"{missing} bulunamadi, sonda olculeri kosturulmadi.";
    }
}

public sealed class ComplexityProbeTests
{
    private sealed class FakeMeter(bool available, bool fail = false, bool comparable = true) : IQualityMeasurement
    {
        public bool IsAvailable => available;
        public int Calls { get; private set; }

        public Task<WindowQualityMeasurement?> MeasureWindowAsync(string referencePath, string samplePath, double referenceStartSeconds, double durationSeconds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            if (fail) throw new InvalidOperationException("measurement failed");
            return Task.FromResult<WindowQualityMeasurement?>(new(
                referenceStartSeconds, comparable ? 91.0 : null, comparable ? 90.0 : null,
                comparable ? 88.0 : null, comparable, 4, comparable ? null : "karşılaştırılamaz"));
        }
    }

    private sealed class BlockingMeter : IQualityMeasurement
    {
        public bool IsAvailable => true;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<WindowQualityMeasurement?> MeasureWindowAsync(string referencePath, string samplePath, double referenceStartSeconds, double durationSeconds, CancellationToken ct)
        {
            Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return null;
        }
    }

    [FfmpegFact]
    public async Task DetailedProbeExposesWindowQualityThroughCoreContract()
    {
        await WithClipAsync(async info =>
        {
            var meter = new FakeMeter(true);
            var result = await ComplexityProbe.RunDetailedAsync(info, SpeedMode.Fast, true, meter);

            Assert.True(result.Profile.Measured);
            Assert.True(result.HasQuality);
            Assert.Equal(2, meter.Calls);
            Assert.Equal(2, result.QualityMeasurements.Count);
            Assert.All(result.QualityMeasurements, q => Assert.Equal(91.0, q.VmafNegMean));
        });
    }

    [FfmpegFact]
    public async Task DefaultDetailedProbeDoesNotMeasureQualityBeforeT89OptsIn()
    {
        await WithClipAsync(async info =>
        {
            var meter = new FakeMeter(true);
            var result = await ComplexityProbe.RunDetailedAsync(info, SpeedMode.Fast, qualityMeasurement: meter);

            Assert.True(result.Profile.Measured);
            Assert.Equal(0, meter.Calls);
            Assert.Empty(result.QualityMeasurements);
        });
    }

    [Fact]
    public void EveryProbeSampleMuxesThroughTheSameContainer()
    {
        var split = ComplexityProbe.SplitArgs("in.mp4", 1.0, (160, 120), "veryfast", SpeedMode.Quality, "full.mkv", "half.mkv");
        var motion = ComplexityProbe.SampleArgs("in.mp4", 1.0, 2.0, "fps=30", "veryfast", SpeedMode.Quality, "motion.mkv");
        var plain = ComplexityProbe.SampleArgs("in.mp4", 1.0, 2.0, null, "veryfast", SpeedMode.Quality, "plain.mkv");

        var muxers = OutputMuxers(split).Concat(OutputMuxers(motion)).Concat(OutputMuxers(plain)).ToArray();

        Assert.Equal(4, muxers.Length);
        Assert.Single(muxers.Distinct());
        Assert.DoesNotContain("null", muxers);
    }

    [FfmpegFact]
    public async Task WindowAndMotionSamplesCountTheSameByteUnit()
    {
        await WithClipAsync(async info =>
        {
            var window = await ComplexityProbe.SampleWindowAsync(info.FilePath, 1.0, (160, 120), "veryfast", SpeedMode.Quality, null, default);
            var (motionBytes, motionFrames) = await ComplexityProbe.SampleAsync(info.FilePath, 1.0, 2.0, null, "veryfast", SpeedMode.Quality, default);

            Assert.True(window.FullFrames > 0, "tam olcek ornegi kare uretmedi");
            Assert.True(motionFrames > 0, "hareket ornegi kare uretmedi");
            Assert.True(window.FullBytes > 0, "tam olcek ornegi bayt uretmedi");

            var drift = Math.Abs(motionBytes - window.FullBytes) / (double)window.FullBytes;
            Assert.True(drift < 0.08, $"pencere {window.FullBytes} B, hareket {motionBytes} B, sapma {drift:P1}");
        }, "color=c=gray:size=320x240:rate=12:duration=8");
    }

    [FfmpegFact]
    public async Task ProbeEntryPointUsedByTheAppCarriesMeasuredQualityIntoTheProfile()
    {
        await WithClipAsync(async info =>
        {
            var meter = new FakeMeter(true);
            var profile = await MainWindow.ProbeWithMeasuredQualityAsync(info, SpeedMode.Fast, meter, default);

            Assert.True(profile.Measured);
            Assert.True(meter.Calls > 0);
            Assert.True(profile.QualityMeasured);
            Assert.Equal(91.0, profile.QualityAnchor!.VmafNeg, 3);
        });
    }

    [FfmpegFact]
    public async Task AppProbeSurvivesAnUnusableQualityMeter()
    {
        await WithClipAsync(async info =>
        {
            var meter = new FakeMeter(true, comparable: false);
            var profile = await MainWindow.ProbeWithMeasuredQualityAsync(info, SpeedMode.Fast, meter, default);

            Assert.True(profile.Measured);
            Assert.False(profile.QualityMeasured);
        });
    }

    private static IEnumerable<string> OutputMuxers(IReadOnlyList<string> args)
    {
        for (var i = 0; i + 1 < args.Count; i++)
            if (args[i] == "-f") yield return args[i + 1];
    }

    [FfmpegTheory]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public async Task MissingFailedOrIncomparableQualityKeepsComplexityProbeAlive(bool available, bool fail, bool comparable)
    {
        await WithClipAsync(async info =>
        {
            var result = await ComplexityProbe.RunDetailedAsync(info, SpeedMode.Fast, true, new FakeMeter(available, fail, comparable));

            Assert.True(result.Profile.Measured);
            Assert.Empty(result.QualityMeasurements);
        });
    }

    [FfmpegFact]
    public async Task CancellationReachesQualityMeasurement()
    {
        await WithClipAsync(async info =>
        {
            using var cts = new CancellationTokenSource();
            var meter = new BlockingMeter();
            var pending = ComplexityProbe.RunDetailedAsync(info, SpeedMode.Fast, true, meter, cts.Token);
            await meter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        });
    }

    private static async Task WithClipAsync(Func<MediaInfo, Task> body, string source = "testsrc2=size=320x240:rate=12:duration=8")
    {
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_complexity_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var clip = Path.Combine(dir, "clip.mp4");
            await RunFfmpegAsync(new[] { "-y", "-f", "lavfi", "-i", source, "-c:v", "libx264", "-pix_fmt", "yuv420p", clip });
            await body(await FfprobeClient.ProbeAsync(clip));
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
