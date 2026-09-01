using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// Kaynak agacini okuyan olculer. Yalniz derlenmis ikiliyi tasiyan bir kosumda
/// <c>tools/VidShrink.Bench/Program.cs</c> yoktur; test sessizce yesile donmez, atlanir.
/// </summary>
public sealed class BenchSourceFactAttribute : FactAttribute
{
    public static readonly string? ProgramPath = Locate();

    public BenchSourceFactAttribute()
    {
        if (ProgramPath is null)
            Skip = "tools/VidShrink.Bench/Program.cs bulunamadi, kaynak agaci olcusu kosturulmadi.";
    }

    private static string? Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tools", "VidShrink.Bench", "Program.cs");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        return null;
    }
}

public sealed class ComplexityScanTests
{
    private const string Codec = "libx264";
    private const double SourceFps = 48.0;

    private static ComplexityProfile Profile(double windowBias, WindowBiasSource source) => new()
    {
        ReferenceBppf = 0.08,
        Measured = true,
        DetailExponent = 0.55,
        SampledSeconds = 6,
        SampledFrames = 288,
        WindowBias = windowBias,
        BiasSource = source
    };

    private static ComplexityProfile Calibrated(ComplexityProfile profile)
    {
        var signature = new CalibrationSignature
        {
            Codec = Codec,
            Width = 1920,
            Height = 1080,
            Fps = SourceFps,
            Scale = 1.0
        };
        return profile.Calibrate(signature, 25, 0.12, 29, 0.06, SourceFps);
    }

    private static (long Bytes, long Frames)[] Samples(IReadOnlyList<double> points, IReadOnlyList<double> windowPoints, long windowBytes, long spreadBytes)
    {
        var selected = new HashSet<double>(windowPoints);
        return points.Select(p => (selected.Contains(p) ? windowBytes : spreadBytes, 12L)).ToArray();
    }

    [Theory]
    [InlineData(52.0)]
    [InlineData(600.0)]
    [InlineData(7200.0)]
    public void ScanPointCountIsIndependentOfDuration(double duration)
    {
        Assert.Equal(40, ComplexityProbe.ScanPoints(duration).Count);
        Assert.Equal(12, ComplexityProbe.WindowScanPoints(duration).Count);
    }

    [Theory]
    [InlineData(52.0)]
    [InlineData(7200.0)]
    public void WindowPointsAreSubsetOfScanPoints(double duration)
    {
        var points = ComplexityProbe.ScanPoints(duration);
        var windowPoints = ComplexityProbe.WindowScanPoints(duration);

        Assert.All(windowPoints, point => Assert.Contains(point, points));
        Assert.True(points.Count > windowPoints.Count);
    }

    [Theory]
    [InlineData(52.0)]
    [InlineData(7200.0)]
    public void WindowPointsStayInsideTheirWindows(double duration)
    {
        var starts = ComplexityProbe.Windows(duration).ToList();
        foreach (var point in ComplexityProbe.WindowScanPoints(duration))
            Assert.Contains(starts, start => point >= start - 0.001 && point <= start + 1.001);
    }

    [Fact]
    public void ScanPointsAreSpreadAcrossTheWholeSource()
    {
        var points = ComplexityProbe.ScanPoints(7200.0);

        Assert.True(points[0] < 200.0);
        Assert.True(points[^1] > 7000.0);
        Assert.All(points, point => Assert.InRange(point, 0.0, 7199.0));
    }

    [Fact]
    public void ComputeScanBias_FlatSamples_IsOne()
    {
        const double duration = 52.0;
        var points = ComplexityProbe.ScanPoints(duration);
        var windowPoints = ComplexityProbe.WindowScanPoints(duration);

        var bias = ComplexityProbe.ComputeScanBias(points, windowPoints, Samples(points, windowPoints, 1000, 1000), duration);

        Assert.Equal(1.0, bias, 9);
    }

    [Fact]
    public void ComputeScanBias_HeavyWindows_ExceedsOne()
    {
        const double duration = 52.0;
        var points = ComplexityProbe.ScanPoints(duration);
        var windowPoints = ComplexityProbe.WindowScanPoints(duration);

        var bias = ComplexityProbe.ComputeScanBias(points, windowPoints, Samples(points, windowPoints, 2000, 1000), duration);

        Assert.InRange(bias, 1.5, 2.0);
    }

    [Fact]
    public void ComputeScanBias_WithoutSpreadPoints_IsRejected()
    {
        const double duration = 52.0;
        var windowPoints = ComplexityProbe.WindowScanPoints(duration);

        var bias = ComplexityProbe.ComputeScanBias(windowPoints, windowPoints, Samples(windowPoints, windowPoints, 2000, 2000), duration);

        Assert.Equal(0.0, bias, 9);
    }

    [Theory]
    [InlineData(52.0, 0)]
    [InlineData(180.0, 0)]
    [InlineData(600.0, 40)]
    [InlineData(7200.0, 40)]
    public void PacketIntervalsStayBoundedAsDurationGrows(double duration, int expected)
    {
        var intervals = ComplexityProbe.PacketIntervals(duration);

        Assert.InRange(intervals.Count, Math.Max(0, expected - 3), expected);
        Assert.All(intervals, interval => Assert.InRange(interval.Start, 0.0, duration));
        Assert.All(intervals, interval => Assert.InRange(interval.Length, 1.0, 3.0));
    }

    [Fact]
    public void ParseVstatsSkipsWarmupFrames()
    {
        var vstats = string.Join('\n', new[]
        {
            "out=  0 st=  0 frame=     0 q= 27.0 f_size=  20000 s_size=  0KiB time= 0.010 br= 1kbits/s avg_br= 0kbits/s type= I",
            "out=  0 st=  0 frame=     1 q= 28.0 f_size=   3000 s_size= 19KiB time= 0.510 br= 1kbits/s avg_br= 1kbits/s type= P",
            "out=  0 st=  0 frame=     2 q= 28.0 f_size=   1000 s_size= 22KiB time= 0.760 br= 1kbits/s avg_br= 1kbits/s type= P",
            "out=  0 st=  0 frame=     3 q= 28.0 f_size=   1200 s_size= 23KiB time= 0.990 br= 1kbits/s avg_br= 1kbits/s type= P"
        });

        var (bytes, frames) = ComplexityProbe.ParseVstats(vstats, 0.75);

        Assert.Equal(2200, bytes);
        Assert.Equal(2, frames);
    }

    [Fact]
    public void ParseVstatsWithoutFramesAfterWarmupIsEmpty()
    {
        var vstats = "out=  0 st=  0 frame=     0 q= 27.0 f_size=  20000 s_size=  0KiB time= 0.010 br= 1kbits/s avg_br= 0kbits/s type= I";

        var (bytes, frames) = ComplexityProbe.ParseVstats(vstats, 0.75);

        Assert.Equal(0, bytes);
        Assert.Equal(0, frames);
    }

    [Fact]
    public void ScanBiasGivesTheNarrowBand()
    {
        var profile = Calibrated(Profile(1.19, WindowBiasSource.Scan));

        Assert.True(profile.Calibrated);
        Assert.Equal(0.05, profile.EstimateBandFor(Codec, 1.0, SourceFps), 9);
    }

    [Fact]
    public void PacketBiasGivesTheIntermediateBand()
    {
        var profile = Calibrated(Profile(1.06, WindowBiasSource.Packets));

        Assert.True(profile.Calibrated);
        Assert.Equal(0.08, profile.EstimateBandFor(Codec, 1.0, SourceFps), 9);
    }

    [Fact]
    public void WithoutBiasTheBandStaysAtTheMeasuredValue()
    {
        var profile = Calibrated(Profile(0.0, WindowBiasSource.None));

        Assert.True(profile.Calibrated);
        Assert.Equal(0.14, profile.EstimateBandFor(Codec, 1.0, SourceFps), 9);
        Assert.Equal(0.32, (profile with { Measured = false }).EstimateBandFor(Codec, 1.0, SourceFps), 9);
    }

    [Fact]
    public void BandDoesNotNarrowWithoutCalibration()
    {
        var profile = Profile(1.19, WindowBiasSource.Scan);

        Assert.False(profile.Calibrated);
        Assert.Equal(0.14, profile.EstimateBandFor(Codec, 1.0, SourceFps), 9);
    }

    [Theory]
    [InlineData(0.49)]
    [InlineData(2.01)]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    public void BiasOutsideTheClampIsNotApplied(double bias)
    {
        var profile = ComplexityProfile.FromProbe(0.1, 0.05, 6, 288, bias);
        var plain = ComplexityProfile.FromProbe(0.1, 0.05, 6, 288);

        Assert.Equal(WindowBiasSource.None, profile.BiasSource);
        Assert.False(ComplexityProfile.IsTrustedBias(bias));
        Assert.Equal(plain.ReferenceBppf, profile.ReferenceBppf, 9);
        Assert.Equal(0.14, Calibrated(profile).EstimateBandFor(Codec, 1.0, SourceFps), 9);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.19)]
    [InlineData(2.0)]
    public void BiasInsideTheClampCorrectsTheReference(double bias)
    {
        var profile = ComplexityProfile.FromProbe(0.1, 0.05, 6, 288, bias);

        Assert.Equal(WindowBiasSource.Scan, profile.BiasSource);
        Assert.Equal(0.1 / bias, profile.ReferenceBppf, 9);
    }

    [Fact]
    public void TheWindowTheProfileCorrectsForIsTheWindowTheProbeCuts()
    {
        Assert.Single(ComplexityProbe.Windows(ComplexityProfile.SampleWindowSeconds * 1.5 - 0.01));
        Assert.Equal(2, ComplexityProbe.Windows(ComplexityProfile.SampleWindowSeconds * 1.5 + 0.01).Count());
        Assert.Equal(ComplexityProbe.MotionProbeFpsRatio, ComplexityProfile.SampleMotionFpsRatio, 9);
    }

    [Theory]
    [InlineData(24, 866)]
    [InlineData(24, 890)]
    [InlineData(24, 925)]
    [InlineData(24, 963)]
    [InlineData(60, 1151)]
    [InlineData(96, 1539)]
    [InlineData(120, 1441)]
    [InlineData(120, 1499)]
    [InlineData(120, 1547)]
    [InlineData(120, 1624)]
    public void TheContainerOverheadModelMatchesTheMeasuredMatroskaCost(int frames, int measuredBytes)
    {
        var predicted = ComplexityProfile.SampleContainerFixedBytes + ComplexityProfile.SampleContainerBytesPerFrame * frames;
        var deviation = Math.Abs(predicted - measuredBytes) / measuredBytes;

        Assert.True(deviation <= 0.12,
            $"{frames} karelik pencerede model {predicted:0} B soyluyor, olculen {measuredBytes} B, sapma %{deviation * 100:0.0}.");
    }

    [Fact]
    public void ContainerOverheadIsTakenOutOfTheMeasuredUnit()
    {
        const int width = 640;
        const int height = 360;
        const double pixels = (double)width * height;
        const long sampledFrames = 288;
        const double sampledSeconds = 6;
        const double framesPerWindow = sampledFrames / (sampledSeconds / ComplexityProfile.SampleWindowSeconds);
        const double motionFrames = framesPerWindow * ComplexityProfile.SampleMotionFpsRatio;

        const double cleanFullBytesPerFrame = 60.0;
        const double cleanMotionExponent = 0.15;
        var cleanMotionBytesPerFrame = cleanFullBytesPerFrame * Math.Pow(2, cleanMotionExponent);

        var fullBytesPerFrame = cleanFullBytesPerFrame + (764.3 / framesPerWindow + 6.545);
        var motionBytesPerFrame = cleanMotionBytesPerFrame + (764.3 / motionFrames + 6.545);

        var fullBppf = fullBytesPerFrame * 8.0 / pixels;
        var motionBppf = fullBppf * (motionBytesPerFrame / fullBytesPerFrame);
        var measured = ComplexityProfile.FromProbe(fullBppf, fullBppf * 0.6, sampledSeconds, sampledFrames, 0, WindowBiasSource.Scan, motionBppf);

        Assert.True(measured.MotionExponent > cleanMotionExponent + 0.05,
            $"Kirlenme zaten yok: olculen ustel {measured.MotionExponent:0.0000}.");

        var clean = measured.WithoutSampleContainerBias(width, height);

        Assert.Equal(cleanMotionExponent, clean.MotionExponent, 6);
        Assert.Equal(cleanFullBytesPerFrame * 8.0 / pixels, clean.ReferenceBppf, 12);
        Assert.Same(clean, clean.WithoutSampleContainerBias(width, height));
    }

    private sealed class ColdCapabilities : IEncoderAvailability, IEncoderOptionAvailability, IEncoderOptionWarmup
    {
        private readonly HashSet<string> _warm = new();

        public int Warmups { get; private set; }

        public bool HasEncoder(string name) => true;

        public bool WorksAsEncoder(string codec) => true;

        public bool SupportsEncoderOption(string codec, string option, string value)
            => _warm.Contains(codec + option + value);

        public bool WarmEncoderOption(string codec, string option, string value)
        {
            Warmups++;
            _warm.Add(codec + option + value);
            return true;
        }
    }

    private static (MediaInfo Info, EncodePlan Plan) PsychovisualCase()
    {
        var info = new MediaInfo
        {
            FilePath = "kaynak.mkv",
            FileSizeBytes = 400L * 1024 * 1024,
            DurationSeconds = 120,
            Width = 1920,
            Height = 1080,
            Fps = 30,
            VideoCodec = "h264",
            TotalBitrateBps = 28_000_000,
            AudioCodec = "aac",
            AudioBitrateBps = 128_000,
            AudioChannels = 2
        };
        var plan = new EncodePlan
        {
            Codec = "libx265",
            Preset = "slow",
            Mode = "crf",
            Crf = 24,
            Width = 1920,
            Height = 1080,
            Fps = 30,
            AudioBitrateK = 128,
            AudioCodec = "aac"
        };
        return (info, plan);
    }

    [Fact]
    public void ThePrintedCommandIsTheCommandThatWouldRun()
    {
        var (info, plan) = PsychovisualCase();
        var capabilities = new ColdCapabilities();

        var unwarmed = FfmpegArguments.Build(info, plan, "cikti.mp4", 0, null, capabilities);
        Assert.DoesNotContain("-x265-params", unwarmed);
        Assert.Equal(0, capabilities.Warmups);

        var printed = EncodeRunner.EncodeArguments(info, plan, "cikti.mp4", 0, null, capabilities);
        var run = EncodeRunner.EncodeArguments(info, plan, "cikti.mp4", 0, null, capabilities);

        Assert.Contains("-x265-params", printed);
        Assert.Contains("psy-rd=2:psy-rdoq=1:aq-mode=2", printed);
        Assert.Equal(run, printed);
    }

    [BenchSourceFact]
    public void TheBenchPrintsTheCommandThroughTheWarmingPath()
    {
        var program = BenchSourceFactAttribute.ProgramPath!;
        var printLine = File.ReadLines(program).FirstOrDefault(line => line.Contains("\"komut: \""));
        Assert.NotNull(printLine);
        Assert.Contains("EncodeRunner.EncodeArguments", printLine);
        Assert.DoesNotContain("FfmpegArguments.Build", printLine);
    }

}
