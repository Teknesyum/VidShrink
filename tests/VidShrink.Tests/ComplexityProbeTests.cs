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


    private static IReadOnlyList<double> Profile(params (int Seconds, double Bits)[] runs)
    {
        var profile = new List<double>();
        foreach (var (seconds, bits) in runs)
            for (var i = 0; i < seconds; i++) profile.Add(bits);
        return profile;
    }

    private static double SampledMean(IReadOnlyList<SampleWindow> windows, IReadOnlyList<double> profile)
    {
        double weighted = 0, weight = 0;
        foreach (var window in windows)
        {
            var first = (int)Math.Floor(window.Start);
            var last = (int)Math.Ceiling(window.Start + window.Length) - 1;
            double sum = 0;
            var count = 0;
            for (var i = first; i <= last; i++)
                if (i >= 0 && i < profile.Count) { sum += profile[i]; count++; }
            if (count == 0) continue;
            var w = window.Weight > 0 ? window.Weight : 1.0;
            weighted += w * (sum / count);
            weight += w;
        }
        return weight > 0 ? weighted / weight : 0.0;
    }

    [Fact]
    public void ContentDrivenWindowsTrackTheFileMeanBetterThanTheFixedWindows()
    {
        var duration = 240.0;
        var profile = Profile((180, 1.0e6), (60, 9.0e6));
        var fileMean = profile.Average();

        var fixedPlan = ComplexityProbe.PlanWindows(SamplingPlan.Fixed, duration);
        var contentPlan = ComplexityProbe.PlanWindows(SamplingPlan.Profile, duration, profile, null, 4);

        var fixedError = Math.Abs(SampledMean(fixedPlan, profile) / fileMean - 1.0);
        var contentError = Math.Abs(SampledMean(contentPlan, profile) / fileMean - 1.0);

        Assert.True(
            contentError < fixedError,
            $"icerige bagli sapma {contentError:P2}, sabit pencere sapmasi {fixedError:P2}");
    }

    [Fact]
    public void WindowPlacementFollowsWhereTheContentIsHeavyNotTheClock()
    {
        var duration = 240.0;
        var early = ComplexityProbe.PlanWindows(SamplingPlan.Profile, duration, Profile((60, 9.0e6), (180, 1.0e6)), null, 4);
        var late = ComplexityProbe.PlanWindows(SamplingPlan.Profile, duration, Profile((180, 1.0e6), (60, 9.0e6)), null, 4);

        static double HeavyShare(IReadOnlyList<SampleWindow> windows, IReadOnlyList<double> profile)
            => windows.Where(w => profile[(int)w.Start] > 5.0e6).Sum(w => w.Weight) / windows.Sum(w => w.Weight);

        var earlyProfile = Profile((60, 9.0e6), (180, 1.0e6));
        var lateProfile = Profile((180, 1.0e6), (60, 9.0e6));

        Assert.Equal(0.25, HeavyShare(early, earlyProfile), 1);
        Assert.Equal(0.25, HeavyShare(late, lateProfile), 1);
        Assert.All(early.Where(w => earlyProfile[(int)w.Start] > 5.0e6), w => Assert.True(w.Start < 60));
        Assert.All(late.Where(w => lateProfile[(int)w.Start] > 5.0e6), w => Assert.True(w.Start >= 180));
    }

    [Fact]
    public void SamplePlanIsUnbiasedWhenTheContentIsFlat()
    {
        var profile = Profile((240, 4.0e6));
        var windows = ComplexityProbe.PlanWindows(SamplingPlan.Profile, 240.0, profile, null, 4);

        Assert.Equal(1.0, ComplexityProbe.PlanBias(windows, profile), 6);
    }

    [Fact]
    public void PlanBiasReportsOverweightedSamplingAsAboveOne()
    {
        var profile = Profile((180, 1.0e6), (60, 9.0e6));
        var heavy = new[] { new SampleWindow(200, 2, 1), new SampleWindow(220, 2, 1) };

        Assert.True(ComplexityProbe.PlanBias(heavy, profile) > 1.0);
    }

    [Theory]
    [InlineData(0.05, 0.40)]
    [InlineData(0.40, 0.90)]
    public void MoreHeterogeneousContentAsksForMoreWindows(double calm, double busy)
    {
        var duration = 600.0;
        Assert.True(
            ComplexityProbe.PlanWindowCount(duration, busy) > ComplexityProbe.PlanWindowCount(duration, calm),
            $"cv {busy} icin {ComplexityProbe.PlanWindowCount(duration, busy)}, " +
            $"cv {calm} icin {ComplexityProbe.PlanWindowCount(duration, calm)}");
    }

    [Fact]
    public void WindowCountNeverExceedsTheSampledShareTheCostBudgetAllows()
    {
        foreach (var duration in new[] { 30.0, 60.0, 240.0, 600.0, 3600.0 })
        {
            var count = ComplexityProbe.PlanWindowCount(duration, 5.0);
            Assert.True(
                count == ComplexityProbe.MinWindows || count * 2.0 <= duration * ComplexityProbe.MaxSampledShare,
                $"{duration} sn icin {count} pencere butceyi asiyor");
        }
    }

    [Fact]
    public void HeterogeneityIsScaleFreeAndZeroOnFlatContent()
    {
        var flat = Profile((60, 3.0e6));
        var mixed = Profile((30, 1.0e6), (30, 5.0e6));

        Assert.Equal(0.0, ComplexityProbe.Heterogeneity(flat), 9);
        Assert.Equal(
            ComplexityProbe.Heterogeneity(mixed),
            ComplexityProbe.Heterogeneity(mixed.Select(v => v * 7.0).ToList()),
            9);
    }

    [Fact]
    public void SceneWindowsStayInsideOneSceneAndCarryItsDurationWeight()
    {
        var duration = 240.0;
        var profile = Profile((60, 1.0e6), (60, 5.0e6), (60, 2.0e6), (60, 9.0e6));
        var cuts = new[] { 60.0, 120.0, 180.0 };
        var windows = ComplexityProbe.PlanWindows(SamplingPlan.Scene, duration, profile, cuts, 4);

        Assert.NotEmpty(windows);
        foreach (var window in windows)
        {
            var start = cuts.Concat(new[] { 0.0 }).Where(c => c <= window.Start).Max();
            var end = cuts.Concat(new[] { duration }).Where(c => c > window.Start).Min();
            Assert.True(window.Start >= start && window.Start + window.Length <= end,
                $"{window.Start}+{window.Length} sahne siniri {start}-{end} disina tasti");
        }
        Assert.True(windows.Sum(w => w.Weight) > 0);
    }

    [Fact]
    public void SceneCutsChangeWhereTheScenePlanLooks()
    {
        var duration = 240.0;
        var profile = Profile((60, 1.0e6), (60, 5.0e6), (60, 2.0e6), (60, 9.0e6));
        var coarse = ComplexityProbe.PlanWindows(SamplingPlan.Scene, duration, profile, new[] { 120.0 }, 2);
        var fine = ComplexityProbe.PlanWindows(SamplingPlan.Scene, duration, profile, new[] { 30.0, 60.0, 90.0, 120.0, 150.0, 180.0, 210.0 }, 2);

        Assert.NotEqual(coarse.Select(w => w.Start).ToArray(), fine.Select(w => w.Start).ToArray());
    }

    [Fact]
    public void TheWeightedEstimateFollowsTheWeightsNotTheWindowOrder()
    {
        var samples = new[] { (100L, 10L), (300L, 10L) };
        var even = new[] { new SampleWindow(0, 2, 1), new SampleWindow(10, 2, 1) };
        var leaning = new[] { new SampleWindow(0, 2, 3), new SampleWindow(10, 2, 1) };

        var evenValue = ComplexityProbe.WeightedBppf(even, samples, 100, 100);
        var leaningValue = ComplexityProbe.WeightedBppf(leaning, samples, 100, 100);

        Assert.True(leaningValue < evenValue);
        Assert.Equal(ComplexityProbe.WeightedBppf(even, samples, 100, 100), evenValue, 12);
    }

    [Theory]
    [InlineData(8.0)]
    [InlineData(60.0)]
    [InlineData(1036.0)]
    public void WithoutContentEvidenceThePlanIsExactlyTodaysFixedWindows(double duration)
    {
        var planned = ComplexityProbe.PlanWindows(SamplingPlan.Profile, duration);
        Assert.Equal(ComplexityProbe.Windows(duration).ToArray(), planned.Select(w => w.Start).ToArray());
        Assert.All(planned, w => Assert.Equal(1.0, w.Weight));
    }

    private static async Task WithClipAsync(Func<MediaInfo, Task> body, string source = "testsrc2=size=320x240:rate=12:duration=8")
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "complexity-probe", Guid.NewGuid().ToString("N"));
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
