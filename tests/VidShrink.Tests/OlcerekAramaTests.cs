using System.IO;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class OlcerekAramaTests
{
    private const double Requested = 70;

    private static double TrueQuality(double mb) => 40 + 20 * Math.Log(mb);

    private static double TrueTarget(double quality) => Math.Exp((quality - 40) / 20);

    private sealed class FakeEncoder
    {
        private readonly Func<double, double?> _quality;
        public List<double> Calls { get; } = new();

        public FakeEncoder(Func<double, double?> quality) => _quality = quality;

        public Task<double?> SampleAsync(double mb, CancellationToken ct)
        {
            Calls.Add(mb);
            return Task.FromResult(_quality(mb));
        }
    }

    private sealed class Collect : IProgress<QualitySearchRound>
    {
        public List<QualitySearchRound> Seen { get; } = new();
        public void Report(QualitySearchRound value) => Seen.Add(value);
    }

    private sealed record Model(Func<double, double> PredictedAt, Func<double, double> TargetFor);

    private static Model Offset(double bias) => new(mb => TrueQuality(mb) + bias, q => TrueTarget(q - bias));

    private static Model HalfSlope() => new(mb => 40 + 10 * Math.Log(mb), q => Math.Exp((q - 40) / 10));

    private static QualitySearchResult Run(FakeEncoder encoder, double startMb, Model model,
        IProgress<QualitySearchRound>? progress = null, double tolerance = MeasuredQualitySearch.TolerancePoints)
        => MeasuredQualitySearch.RunAsync(
            Requested, startMb, 0.1, 500,
            encoder.SampleAsync,
            model.PredictedAt,
            model.TargetFor,
            progress, CancellationToken.None, tolerance: tolerance).GetAwaiter().GetResult();

    [Fact]
    public void ABiasedModelIsCorrectedByTheFirstMeasurementAndConvergesInTwoRounds()
    {
        var encoder = new FakeEncoder(mb => TrueQuality(mb));
        var startMb = TrueTarget(Requested - 5);

        var result = Run(encoder, startMb, Offset(5));

        Assert.True(result.Converged);
        Assert.Equal(2, result.Rounds.Count);
        Assert.Equal(2, encoder.Calls.Count);
        Assert.InRange(result.MeasuredQuality!.Value, Requested - 1, Requested + 1);
        Assert.Equal(TrueTarget(Requested), result.TargetMb, 3);
    }

    [Fact]
    public void AStartInsideTheBandStopsAfterOneRound()
    {
        var encoder = new FakeEncoder(mb => TrueQuality(mb));

        var result = Run(encoder, TrueTarget(Requested + 0.5), Offset(0));

        Assert.True(result.Converged);
        Assert.Single(encoder.Calls);
    }

    [Fact]
    public void TheSearchNeverRunsMoreThanFourRounds()
    {
        var encoder = new FakeEncoder(mb => TrueQuality(mb));

        var result = Run(encoder, TrueTarget(Requested - 6), HalfSlope(), tolerance: 1e-9);

        Assert.Equal(4, MeasuredQualitySearch.MaxRounds);
        Assert.Equal(MeasuredQualitySearch.MaxRounds, encoder.Calls.Count);
        Assert.Equal(MeasuredQualitySearch.MaxRounds, result.Rounds.Count);
        Assert.False(result.Converged);
    }

    [Fact]
    public void OnceTheTargetIsBracketedTheNextRoundBisects()
    {
        var encoder = new FakeEncoder(mb => TrueQuality(mb));

        var result = Run(encoder, TrueTarget(Requested - 6), HalfSlope(), tolerance: 0.5);

        Assert.True(encoder.Calls.Count >= 3);
        Assert.True(TrueQuality(encoder.Calls[0]) < Requested);
        Assert.True(TrueQuality(encoder.Calls[1]) > Requested);
        Assert.Equal((encoder.Calls[0] + encoder.Calls[1]) / 2, encoder.Calls[2], 9);
        Assert.True(Math.Abs(result.MeasuredQuality!.Value - Requested) < Math.Abs(TrueQuality(encoder.Calls[0]) - Requested));
    }

    [Fact]
    public void EveryRoundIsReportedBeforeAndAfterItsMeasurement()
    {
        var encoder = new FakeEncoder(mb => TrueQuality(mb));
        var progress = new Collect();

        var result = Run(encoder, TrueTarget(Requested - 5), Offset(5), progress);

        Assert.Equal(result.Rounds.Count * 2, progress.Seen.Count);
        for (var i = 0; i < result.Rounds.Count; i++)
        {
            Assert.Equal(i + 1, progress.Seen[2 * i].Round);
            Assert.Null(progress.Seen[2 * i].MeasuredQuality);
            Assert.Equal(MeasuredQualitySearch.MaxRounds, progress.Seen[2 * i].MaxRounds);
            Assert.NotNull(progress.Seen[2 * i + 1].MeasuredQuality);
        }
    }

    [Fact]
    public void AFailedMeasurementKeepsTheStartingTarget()
    {
        var encoder = new FakeEncoder(_ => null);

        var result = Run(encoder, 12.5, Offset(0));

        Assert.Equal(12.5, result.TargetMb);
        Assert.Null(result.MeasuredQuality);
        Assert.False(result.Converged);
        Assert.Single(encoder.Calls);
    }

    [Fact]
    public void WithoutConvergenceTheSmallestTargetThatReachesTheScoreWins()
    {
        var encoder = new FakeEncoder(mb => mb >= 10 ? 90 : 20);

        var result = Run(encoder, 5, Offset(0));

        Assert.False(result.Converged);
        Assert.True(result.MeasuredQuality >= Requested);
        Assert.Equal(encoder.Calls.Where(mb => mb >= 10).Min(), result.TargetMb);
    }

    [Theory]
    [InlineData(true, true, true, false, true)]
    [InlineData(false, true, true, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, true, true, true, false)]
    public void TheSearchRunsOnlyWhenEnabledQualityDrivenMeasurableAndNotOverridden(
        bool enabled, bool qualityDrives, bool meter, bool overridden, bool expected)
        => Assert.Equal(expected, MeasuredQualitySearch.ShouldRun(enabled, qualityDrives, meter, overridden));

    private const string SamplePath = @"C:\Kayitlar\olcerek-arama-1080p.mkv";

    private static MediaInfo Sample() => new()
    {
        FilePath = SamplePath,
        FileSizeBytes = 180_000_000L,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 12_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    private sealed record Secim(double TargetMb, IReadOnlyList<string> Args, IReadOnlyList<string> ActiveArgs, double ActiveTarget, int Calls, bool SamePlan);

    private static Secim Sec(bool kutu, bool kaliteSurer, double aramaMb)
        => AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = Path.Combine(Path.GetTempPath(), $"vidshrink-arama-{Guid.NewGuid():N}", "settings.json") };
            try
            {
                window.LoadWithoutProbing(SamplePath, Sample());
                window.RecalculateForTest();
                window.ChkAdvSearchByMeasure.IsChecked = kutu;
                window.KaliteSurerForTest = kaliteSurer;
                window.OlcerHazir = () => true;
                var calls = 0;
                window.OlcerekAramaKosucu = (info, options, q, start, progress, ct) =>
                {
                    calls++;
                    return Task.FromResult(new QualitySearchResult(aramaMb, q, q,
                        new[] { new QualitySearchRound(1, MeasuredQualitySearch.MaxRounds, aramaMb, q) }, true));
                };

                var active = window.ActivePlanForTest!;
                var target = window.TargetMbForTest;
                var (mb, plan) = window.BaslangicPlaniAsync(target, active, CancellationToken.None).GetAwaiter().GetResult();
                var info = Sample();
                return new Secim(mb,
                    ShrinkEngine.DisplayedArguments(info, plan, @"C:\Kayitlar\cikti.mp4", null),
                    ShrinkEngine.DisplayedArguments(info, active, @"C:\Kayitlar\cikti.mp4", null),
                    target, calls, ReferenceEquals(plan, active));
            }
            finally { window.Close(); }
        });

    [Fact]
    public void WithTheBoxOffTheStartingTargetAndArgumentsAreUnchanged()
    {
        var off = Sec(kutu: false, kaliteSurer: true, aramaMb: 7.5);

        Assert.Equal(0, off.Calls);
        Assert.True(off.SamePlan);
        Assert.Equal(off.ActiveTarget, off.TargetMb);
        Assert.Equal(off.ActiveArgs, off.Args);
    }

    [Fact]
    public void WhenTheSizeWasTypedTheSearchDoesNotRunEvenWithTheBoxOn()
    {
        var typed = Sec(kutu: true, kaliteSurer: false, aramaMb: 7.5);

        Assert.Equal(0, typed.Calls);
        Assert.Equal(typed.ActiveArgs, typed.Args);
    }

    [Fact]
    public void WithTheBoxOnTheSearchedTargetDrivesThePlan()
    {
        var on = Sec(kutu: true, kaliteSurer: true, aramaMb: 7.5);

        Assert.Equal(1, on.Calls);
        Assert.Equal(7.5, on.TargetMb);
        Assert.False(on.SamePlan);
        Assert.NotEqual(on.ActiveArgs, on.Args);
    }

    [Fact]
    public void TouchingQualityHandsTheTargetToTheSearchAndTypingASizeTakesItBack()
        => AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = Path.Combine(Path.GetTempPath(), $"vidshrink-arama-{Guid.NewGuid():N}", "settings.json") };
            try
            {
                window.LoadWithoutProbing(SamplePath, Sample());
                Assert.False(window.KaliteSurerForTest);
                window.TxtQualityTarget.Text = "72";
                Assert.True(window.KaliteSurerForTest);
                window.TxtTarget.Text = "30";
                Assert.False(window.KaliteSurerForTest);
                return 0;
            }
            finally { window.Close(); }
        });

    [Fact]
    public void TheBoxIsOffByDefaultAndItsSettingSurvivesARoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vidshrink-arama-ayar-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "settings.json");
        try
        {
            Assert.False(new AppSettings().AdvSearchByMeasure);
            new AppSettings { AdvSearchByMeasure = true }.Save(path);
            Assert.True(AppSettings.Load(path).AdvSearchByMeasure);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void TheSampleEncodeCarriesThePlansRateAndTheThreadCap()
    {
        var info = Sample();
        var plan = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 20 }, null).Plan;
        var args = QualitySearchSampler.EncodeArgs(info, plan, new SampleWindow(10, 2, 1), @"C:\t\s.mkv", 2);

        Assert.Equal(2, args.Select((a, i) => (a, i)).Count(x => x.a == "-threads" && args[x.i + 1] == "2"));
        Assert.Contains("10", args);
        if (plan.Crf is null) Assert.Contains(plan.VideoBitrateK.ToString(System.Globalization.CultureInfo.InvariantCulture) + "k", args);
        Assert.Equal(@"C:\t\s.mkv", args[^1]);
    }
}
