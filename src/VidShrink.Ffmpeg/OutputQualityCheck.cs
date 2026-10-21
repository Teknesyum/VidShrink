using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VidShrink.Ffmpeg;

public enum OutputQualityMetric { Vmaf, Ssim, Psnr }

public enum OutputQualityBand { Transparent, Good, Visible, Poor }

public sealed record OutputQuality(OutputQualityMetric Metric, double Score, int Subsample)
{
    public OutputQualityBand Band => OutputQualityCheck.BandOf(Metric, Score);
}

public static class OutputQualityCheck
{
    public const int Threads = 2;

    public const int FrameBudget = 600;

    public const string VmafModel = "vmaf_v0.6.1neg";

    public static OutputQualityMetric? Pick(Func<string, bool> hasFilter)
    {
        ArgumentNullException.ThrowIfNull(hasFilter);
        if (!hasFilter("zscale")) return null;
        if (hasFilter("libvmaf")) return OutputQualityMetric.Vmaf;
        if (hasFilter("ssim")) return OutputQualityMetric.Ssim;
        if (hasFilter("psnr")) return OutputQualityMetric.Psnr;
        return null;
    }

    public static OutputQualityMetric? Available => Pick(EncoderCapabilities.Instance.HasFilter);

    public static bool IsAvailable => Available is not null;

    public static int Subsample(double durationSeconds, double fps)
    {
        var frames = durationSeconds * (fps > 0 ? fps : 25.0);
        if (!(frames > FrameBudget)) return 1;
        return (int)Math.Ceiling(frames / FrameBudget);
    }

    public static string ComparisonFilter(OutputQualityMetric metric, int subsample) => metric switch
    {
        OutputQualityMetric.Vmaf => $"libvmaf=model=version={VmafModel}:n_threads={Threads}:n_subsample={Math.Max(1, subsample)}",
        OutputQualityMetric.Ssim => "ssim",
        _ => "psnr"
    };

    public static IReadOnlyList<string> Arguments(
        string testPath, string referencePath, string testChain, string referenceChain,
        OutputQualityMetric metric, int subsample)
    {
        var step = metric != OutputQualityMetric.Vmaf && subsample > 1 ? $",framestep={subsample}" : "";
        var threads = Threads.ToString(CultureInfo.InvariantCulture);
        return new[]
        {
            "-hide_banner", "-nostdin",
            "-threads", threads, "-i", testPath,
            "-threads", threads, "-i", referencePath,
            "-filter_threads", threads,
            "-lavfi", MeasureFilterGraph.Build(testChain + step, referenceChain + step, ComparisonFilter(metric, subsample)),
            "-an", "-sn", "-dn", "-f", "null", "-"
        };
    }

    public static double? ParseScore(OutputQualityMetric metric, string stderr)
    {
        var pattern = metric switch
        {
            OutputQualityMetric.Vmaf => @"VMAF score:\s*(inf|[\d.]+)",
            OutputQualityMetric.Ssim => @"SSIM\s.*All:\s*(inf|[\d.]+)",
            _ => @"PSNR\s.*average:\s*(inf|[\d.]+)"
        };
        var match = Regex.Match(stderr ?? "", pattern, RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        var raw = match.Groups[1].Value;
        if (raw.Equals("inf", StringComparison.OrdinalIgnoreCase)) return double.PositiveInfinity;
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    public static OutputQualityBand BandOf(OutputQualityMetric metric, double score)
    {
        var (transparent, good, visible) = metric switch
        {
            OutputQualityMetric.Vmaf => (93.0, 85.0, 70.0),
            OutputQualityMetric.Ssim => (0.98, 0.95, 0.90),
            _ => (42.0, 37.0, 32.0)
        };
        if (score >= transparent) return OutputQualityBand.Transparent;
        if (score >= good) return OutputQualityBand.Good;
        if (score >= visible) return OutputQualityBand.Visible;
        return OutputQualityBand.Poor;
    }

    public static async Task<OutputQuality?> MeasureAsync(string referencePath, string testPath, CancellationToken ct = default)
        => await MeasureAsync(referencePath, testPath, Available, ct);

    public static Task<OutputQuality?> MeasureAsync(
        string referencePath, string testPath, OutputQualityMetric? metric, CancellationToken ct = default)
        => MeasureAsync(referencePath, testPath, metric, null, ct);

    public static async Task<OutputQuality?> MeasureAsync(
        string referencePath, string testPath, OutputQualityMetric? metric, IProgress<double>? progress, CancellationToken ct = default)
    {
        if (metric is not { } chosen) return null;
        var reference = await FfprobeClient.ProbeAsync(referencePath, ct);
        var test = await FfprobeClient.ProbeAsync(testPath, ct);
        if (QualityMeter.ColorIncompatibility(reference, test) is not null) return null;

        var subsample = Subsample(reference.DurationSeconds, reference.Fps);
        var args = Arguments(
            testPath, referencePath,
            QualityMeter.ColorFilter(test, reference, reference.Width, reference.Height),
            QualityMeter.ColorFilter(reference, reference, reference.Width, reference.Height),
            chosen, subsample);

        var stderr = await RunAsync(args, reference.DurationSeconds, progress, ct);
        var score = ParseScore(chosen, stderr);
        if (score is null)
            throw new QualityMeasurementFailedException("Kalite ölçümü puan üretmedi.");
        return new OutputQuality(chosen, score.Value, subsample);
    }

    public static double? ProgressFraction(string? line, double durationSeconds)
    {
        if (line is null || !(durationSeconds > 0)) return null;
        var match = ProgressLine.Match(line);
        if (!match.Success) return null;
        if (!long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var micros)) return null;
        return Math.Clamp(micros / 1_000_000.0 / durationSeconds, 0, 1);
    }

    public static string LogLine(string outputPath, OutputQuality? quality)
    {
        var name = Path.GetFileName(outputPath);
        if (quality is null) return $"kalite: {name} olculemedi";
        var metric = quality.Metric.ToString().ToUpperInvariant();
        var score = quality.Score.ToString(quality.Metric == OutputQualityMetric.Ssim ? "0.0000" : "0.00", CultureInfo.InvariantCulture);
        var band = quality.Band.ToString().ToLowerInvariant();
        return $"kalite: {name} {metric} {score} bant={band} alt-ornekleme={quality.Subsample.ToString(CultureInfo.InvariantCulture)}";
    }

    private static readonly Regex ProgressLine = new(@"^out_time_(?:us|ms)=(\d+)\s*$", RegexOptions.Compiled);

    private static async Task<string> RunAsync(IReadOnlyList<string> args, double durationSeconds, IProgress<double>? progress, CancellationToken ct)
    {
        var withProgress = new List<string> { "-progress", "pipe:1", "-nostats" };
        withProgress.AddRange(args);
        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, withProgress) };
        process.Start();
        using var registration = ct.Register(() => Kill(process));
        var stdout = PumpProgressAsync(process.StandardOutput, durationSeconds, progress);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync(CancellationToken.None);
        ct.ThrowIfCancellationRequested();
        var text = await stderr;
        if (process.ExitCode != 0)
            throw new QualityMeasurementFailedException($"ffmpeg ({process.ExitCode}): {Tail(text)}");
        return text;
    }

    private static async Task PumpProgressAsync(StreamReader reader, double durationSeconds, IProgress<double>? progress)
    {
        while (await reader.ReadLineAsync() is { } line)
            if (ProgressFraction(line, durationSeconds) is { } fraction) progress?.Report(fraction);
    }

    private static string Tail(string text)
        => text.Length <= 400 ? text : text[^400..];

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }
}
