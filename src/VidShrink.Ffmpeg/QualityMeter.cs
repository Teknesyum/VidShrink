using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VidShrink.Ffmpeg;

public sealed record QualityScore(double? VmafNegMean, double? VmafNegHarmonic, double? VmafNegP10, double? VmafNegMin, double? Xpsnr, double? Ssim);

public static class QualityMeter
{
    public static async Task<QualityScore> MeasureAsync(string referencePath, string testPath, CancellationToken ct = default)
    {
        var reference = await FfprobeClient.ProbeAsync(referencePath, ct);

        double? vmafMean = null, vmafHarmonic = null, vmafP10 = null, vmafMin = null;
        if (EncoderCapabilities.Instance.HasFilter("libvmaf"))
            (vmafMean, vmafHarmonic, vmafP10, vmafMin) = await MeasureVmafAsync(testPath, referencePath, reference.Width, reference.Height, ct);

        double? xpsnr = EncoderCapabilities.Instance.HasFilter("xpsnr")
            ? await MeasureXpsnrAsync(testPath, referencePath, reference.Width, reference.Height, ct)
            : null;

        double? ssim = EncoderCapabilities.Instance.HasFilter("ssim")
            ? await MeasureSsimAsync(testPath, referencePath, reference.Width, reference.Height, ct)
            : null;

        return new QualityScore(vmafMean, vmafHarmonic, vmafP10, vmafMin, xpsnr, ssim);
    }

    private static async Task<(double? Mean, double? Harmonic, double? P10, double? Min)> MeasureVmafAsync(
        string testPath, string referencePath, int width, int height, CancellationToken ct)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "vidshrink_vmaf_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var filter = $"libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path={EscapeFilterPath(logPath)}";
            await RunFilterAsync(testPath, referencePath, width, height, filter, ct);

            if (!File.Exists(logPath)) return (null, null, null, null);

            var scores = new List<double>();
            await using (var stream = File.OpenRead(logPath))
            {
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                foreach (var frame in doc.RootElement.GetProperty("frames").EnumerateArray())
                {
                    var metrics = frame.GetProperty("metrics");
                    if (metrics.TryGetProperty("vmaf", out var v) || metrics.TryGetProperty("vmaf_neg", out v))
                        scores.Add(v.GetDouble());
                }
            }

            if (scores.Count == 0) return (null, null, null, null);

            var mean = scores.Average();
            var harmonic = scores.Count / scores.Sum(x => 1.0 / Math.Max(x, 1.0));
            var sorted = scores.OrderBy(x => x).ToList();
            var p10 = Percentile(sorted, 10);
            var min = sorted[0];
            return (mean, harmonic, p10, min);
        }
        finally { TryDelete(logPath); }
    }

    private static async Task<double?> MeasureXpsnrAsync(string testPath, string referencePath, int width, int height, CancellationToken ct)
    {
        var stderr = await RunFilterAsync(testPath, referencePath, width, height, "xpsnr", ct);
        var match = Regex.Match(stderr, @"XPSNR\s+y:\s*([\d.]+)\s*u:\s*([\d.]+)\s*v:\s*([\d.]+)", RegexOptions.IgnoreCase);
        if (!match.Success) return null;

        var y = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var u = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var v = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        return (4 * y + u + v) / 6.0;
    }

    private static async Task<double?> MeasureSsimAsync(string testPath, string referencePath, int width, int height, CancellationToken ct)
    {
        var stderr = await RunFilterAsync(testPath, referencePath, width, height, "ssim", ct);
        var match = Regex.Match(stderr, @"All:\s*([\d.]+)", RegexOptions.IgnoreCase);
        return match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    private static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 1) return sorted[0];
        var rank = p / 100.0 * (sorted.Count - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper) return sorted[lower];
        var frac = rank - lower;
        return sorted[lower] + (sorted[upper] - sorted[lower]) * frac;
    }

    private static async Task<string> RunFilterAsync(
        string testPath, string referencePath, int width, int height, string filterChain, CancellationToken ct)
    {
        var scaler = EncoderCapabilities.Instance.HasFilter("zscale") ? "zscale" : "scale";
        var args = new[]
        {
            "-hide_banner", "-nostdin",
            "-i", testPath,
            "-i", referencePath,
            "-lavfi", $"[0:v]{scaler}=w={width}:h={height}[t];[t][1:v]{filterChain}",
            "-f", "null", "-"
        };

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        process.Start();
        using var cancellationRegistration = ct.Register(() => TryKill(process));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        var stderr = await stderrTask;
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg failed ({process.ExitCode}): {stderr}");

        return stderr;
    }

    private static string EscapeFilterPath(string path)
        => "'" + path.Replace("\\", "/").Replace(":", "\\:") + "'";

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
