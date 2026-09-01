using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

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

    [Fact]
    public async Task DetailedProbeExposesWindowQualityThroughCoreContract()
    {
        await WithClipAsync(async info =>
        {
            var meter = new FakeMeter(true);
            var result = await ComplexityProbe.RunDetailedAsync(info, SpeedMode.Fast, true, meter);

            Assert.True(result.Profile.Measured);
            Assert.True(result.HasQuality);
            Assert.Equal(meter.Calls, result.QualityMeasurements.Count);
            Assert.All(result.QualityMeasurements, q => Assert.Equal(91.0, q.VmafNegMean));
        });
    }

    [Theory]
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

    [Fact]
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

    private static async Task WithClipAsync(Func<MediaInfo, Task> body)
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_complexity_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var clip = Path.Combine(dir, "clip.mp4");
            await RunFfmpegAsync(new[] { "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=12:duration=2", "-c:v", "libx264", "-pix_fmt", "yuv420p", clip });
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
