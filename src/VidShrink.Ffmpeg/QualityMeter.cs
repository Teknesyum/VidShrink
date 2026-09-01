using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VidShrink.Ffmpeg;

public sealed record QualityScore(
    double? VmafNegMean,
    double? VmafNegHarmonic,
    double? VmafNegP10,
    double? VmafNegMin,
    double? Xpsnr,
    double? Ssim,
    bool Comparable = true,
    string? Message = null,
    string? ColorNormalization = null);

public sealed class QualityMeasurement : VidShrink.Core.IQualityMeasurement
{
    public static QualityMeasurement Instance { get; } = new();

    public bool IsAvailable
        => EncoderCapabilities.Instance.HasFilter("libvmaf")
           && EncoderCapabilities.Instance.HasFilter("zscale");

    public async Task<VidShrink.Core.WindowQualityMeasurement?> MeasureWindowAsync(
        string referencePath, string samplePath, double referenceStartSeconds,
        double durationSeconds, CancellationToken ct)
    {
        if (!IsAvailable) return null;
        var watch = Stopwatch.StartNew();
        try
        {
            var score = await QualityMeter.MeasureWindowAsync(
                referencePath, samplePath, referenceStartSeconds, 0, durationSeconds, ct);
            if (!score.Comparable || score.VmafNegMean is null) return null;
            return new VidShrink.Core.WindowQualityMeasurement(
                referenceStartSeconds, score.VmafNegMean, score.VmafNegHarmonic,
                score.VmafNegP10, true, watch.ElapsedMilliseconds, score.Message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return null; }
    }
}

public static class QualityMeter
{
    public static async Task<long> MeasureReferenceDecodeCostAsync(
        string path, double startSeconds, double durationSeconds, bool normalize, CancellationToken ct = default)
    {
        var info = await FfprobeClient.ProbeAsync(path, ct);
        var args = new List<string>
        {
            "-hide_banner", "-nostdin", "-ss", startSeconds.ToString(CultureInfo.InvariantCulture),
            "-t", durationSeconds.ToString(CultureInfo.InvariantCulture), "-i", path
        };
        if (normalize)
            args.AddRange(new[] { "-vf", ColorFilter(info, info, info.Width, info.Height) });
        args.AddRange(new[] { "-an", "-sn", "-dn", "-f", "null", "-" });

        var watch = Stopwatch.StartNew();
        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        process.Start();
        using var registration = ct.Register(() => TryKill(process));
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync(ct);
        watch.Stop();
        if (process.ExitCode != 0) throw new InvalidOperationException(await stderr);
        return watch.ElapsedMilliseconds;
    }

    public static async Task<QualityScore> MeasureAsync(string referencePath, string testPath, CancellationToken ct = default)
        => await MeasureAsync(referencePath, testPath, false, null, null, null, ct);

    public static async Task<QualityScore> MeasureTonemappedReferenceAsync(string referencePath, string testPath, CancellationToken ct = default)
        => await MeasureAsync(referencePath, testPath, true, null, null, null, ct);

    public static async Task<QualityScore> MeasureWindowAsync(string referencePath, string testPath, double startSeconds, double durationSeconds, CancellationToken ct = default)
        => await MeasureAsync(referencePath, testPath, false, startSeconds, startSeconds, durationSeconds, ct);

    public static async Task<QualityScore> MeasureWindowAsync(string referencePath, string testPath, double referenceStartSeconds, double testStartSeconds, double durationSeconds, CancellationToken ct = default)
        => await MeasureAsync(referencePath, testPath, false, referenceStartSeconds, testStartSeconds, durationSeconds, ct);

    private static async Task<QualityScore> MeasureAsync(string referencePath, string testPath, bool tonemapReference, double? referenceStartSeconds, double? testStartSeconds, double? durationSeconds, CancellationToken ct)
    {
        var reference = await FfprobeClient.ProbeAsync(referencePath, ct);
        var test = await FfprobeClient.ProbeAsync(testPath, ct);
        var measuredReference = tonemapReference
            ? reference with
            {
                IsHdr = false, PixelFormat = "yuv420p", BitDepth = 8, ColorPrimaries = "bt709",
                ColorTransfer = "bt709", ColorSpace = "bt709", ColorRange = "tv"
            }
            : reference;
        var incompatibility = ColorIncompatibility(measuredReference, test);
        if (incompatibility is not null)
            return new QualityScore(null, null, null, null, null, null, false, incompatibility);

        var normalization = tonemapReference
            ? $"HDR referans {VidShrink.Core.HdrResolver.TonemapFilter} ile bt709 limited'a tonemap edildi; test aynı uzaya normalize edildi."
            : NormalizationDescription(reference, test);

        double? vmafMean = null, vmafHarmonic = null, vmafP10 = null, vmafMin = null;
        if (EncoderCapabilities.Instance.HasFilter("libvmaf"))
            (vmafMean, vmafHarmonic, vmafP10, vmafMin) = await MeasureVmafAsync(testPath, referencePath, measuredReference, test, tonemapReference, referenceStartSeconds, testStartSeconds, durationSeconds, ct);

        double? xpsnr = EncoderCapabilities.Instance.HasFilter("xpsnr")
            ? await MeasureXpsnrAsync(testPath, referencePath, measuredReference, test, tonemapReference, referenceStartSeconds, testStartSeconds, durationSeconds, ct)
            : null;

        double? ssim = EncoderCapabilities.Instance.HasFilter("ssim")
            ? await MeasureSsimAsync(testPath, referencePath, measuredReference, test, tonemapReference, referenceStartSeconds, testStartSeconds, durationSeconds, ct)
            : null;

        return new QualityScore(vmafMean, vmafHarmonic, vmafP10, vmafMin, xpsnr, ssim, true, null, normalization);
    }

    private static async Task<(double? Mean, double? Harmonic, double? P10, double? Min)> MeasureVmafAsync(
        string testPath, string referencePath, VidShrink.Core.MediaInfo reference, VidShrink.Core.MediaInfo test, bool tonemapReference, double? referenceStartSeconds, double? testStartSeconds, double? durationSeconds, CancellationToken ct)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "vidshrink_vmaf_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var filter = $"libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path={EscapeFilterPath(logPath)}";
            await RunFilterAsync(testPath, referencePath, reference, test, tonemapReference, referenceStartSeconds, testStartSeconds, durationSeconds, filter, ct);

            if (!File.Exists(logPath)) return (null, null, null, null);

            var scores = new List<double>();
            await using (var stream = File.OpenRead(logPath))
            {
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                foreach (var frame in doc.RootElement.GetProperty("frames").EnumerateArray())
                {
                    var metrics = frame.GetProperty("metrics");
                    if (metrics.TryGetProperty("vmaf", out var v) || metrics.TryGetProperty("vmaf_neg", out v))
                        scores.Add(NormalizeVmafCeiling(v.GetDouble()));
                }
            }

            if (scores.Count == 0) return (null, null, null, null);

            var mean = scores.Average();
            var harmonic = scores.Count / scores.Sum(x => 1.0 / Math.Max(x, 1.0));
            var sorted = scores.OrderBy(x => x).ToList();
            var p10 = Percentile(sorted, 10);
            var min = sorted[0];
            return (
                NormalizeVmafCeiling(mean),
                NormalizeVmafCeiling(harmonic),
                NormalizeVmafCeiling(p10),
                NormalizeVmafCeiling(min));
        }
        finally { TryDelete(logPath); }
    }

    private static async Task<double?> MeasureXpsnrAsync(string testPath, string referencePath, VidShrink.Core.MediaInfo reference, VidShrink.Core.MediaInfo test, bool tonemapReference, double? referenceStartSeconds, double? testStartSeconds, double? durationSeconds, CancellationToken ct)
    {
        var stderr = await RunFilterAsync(testPath, referencePath, reference, test, tonemapReference, referenceStartSeconds, testStartSeconds, durationSeconds, "xpsnr", ct);
        var match = Regex.Match(stderr, @"XPSNR\s+y:\s*(inf|[\d.]+)\s*u:\s*(inf|[\d.]+)\s*v:\s*(inf|[\d.]+)", RegexOptions.IgnoreCase);
        if (!match.Success) return null;

        var y = ParseMetric(match.Groups[1].Value);
        var u = ParseMetric(match.Groups[2].Value);
        var v = ParseMetric(match.Groups[3].Value);
        return (4 * y + u + v) / 6.0;
    }

    private static async Task<double?> MeasureSsimAsync(string testPath, string referencePath, VidShrink.Core.MediaInfo reference, VidShrink.Core.MediaInfo test, bool tonemapReference, double? referenceStartSeconds, double? testStartSeconds, double? durationSeconds, CancellationToken ct)
    {
        var stderr = await RunFilterAsync(testPath, referencePath, reference, test, tonemapReference, referenceStartSeconds, testStartSeconds, durationSeconds, "ssim", ct);
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

    // vmaf_v0.6.1neg reports about 99.87 for identical frames. Treat that
    // numerical model ceiling as the user-facing perfect score.
    private static double NormalizeVmafCeiling(double score)
        => score >= 99.8 ? 100.0 : score;

    private static async Task<string> RunFilterAsync(
        string testPath, string referencePath, VidShrink.Core.MediaInfo reference, VidShrink.Core.MediaInfo test, bool tonemapReference, double? referenceStartSeconds, double? testStartSeconds, double? durationSeconds, string filterChain, CancellationToken ct)
    {
        if (!EncoderCapabilities.Instance.HasFilter("zscale"))
            throw new InvalidOperationException("Quality measurement requires the zscale filter for explicit color normalization.");
        var testNormalization = ColorFilter(test, reference, reference.Width, reference.Height);
        var referenceNormalization = ColorFilter(reference, reference, reference.Width, reference.Height);
        var referencePrefix = tonemapReference ? VidShrink.Core.HdrResolver.TonemapFilter + "," : "";
        var args = new List<string> { "-hide_banner", "-nostdin" };
        AddInput(args, testPath, testStartSeconds, durationSeconds);
        AddInput(args, referencePath, referenceStartSeconds, durationSeconds);
        args.AddRange(new[] { "-lavfi", $"[0:v]{testNormalization}[t];[1:v]{referencePrefix}{referenceNormalization}[r];[t][r]{filterChain}", "-f", "null", "-" });

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

    private static void AddInput(List<string> args, string path, double? startSeconds, double? durationSeconds)
    {
        if (startSeconds is { } start) args.AddRange(new[] { "-ss", start.ToString(CultureInfo.InvariantCulture) });
        if (durationSeconds is { } duration) args.AddRange(new[] { "-t", duration.ToString(CultureInfo.InvariantCulture) });
        args.AddRange(new[] { "-i", path });
    }

    private static string? ColorIncompatibility(VidShrink.Core.MediaInfo reference, VidShrink.Core.MediaInfo test)
    {
        if (reference.IsHdr != test.IsHdr)
            return "Renk uzayı uyuşmuyor; HDR ve SDR/tonemap edilmiş görüntü karşılaştırılamaz.";
        if (!reference.IsHdr) return null;
        if (!Same(reference.ColorTransfer, test.ColorTransfer) || !Same(reference.ColorPrimaries, test.ColorPrimaries))
            return "Renk uzayı uyuşmuyor; HDR aktarım işlevleri veya ana renkleri farklı, karşılaştırılamaz.";
        return null;
    }

    private static string NormalizationDescription(VidShrink.Core.MediaInfo reference, VidShrink.Core.MediaInfo test)
        => reference.IsHdr
            ? $"HDR: {Describe(test)} ve {Describe(reference)} ortak {reference.ColorPrimaries}/{reference.ColorTransfer} uzayına normalize edildi."
            : $"SDR: {Describe(test)} ve {Describe(reference)} bt709 limited uzayına normalize edildi; etiketsiz yuv420p SDR için bt709 limited varsayıldı.";

    private static string Describe(VidShrink.Core.MediaInfo info)
        => $"{info.ColorPrimaries ?? "etiketsiz"}/{info.ColorTransfer ?? "etiketsiz"}/{info.ColorSpace ?? "etiketsiz"}/{info.ColorRange ?? "etiketsiz"}";

    private static string ColorFilter(VidShrink.Core.MediaInfo input, VidShrink.Core.MediaInfo reference, int width, int height)
    {
        var hdr = reference.IsHdr;
        var inputPrimaries = input.ColorPrimaries ?? (hdr ? "bt2020" : "bt709");
        var inputTransfer = input.ColorTransfer ?? (hdr ? "smpte2084" : "bt709");
        var inputMatrix = input.ColorSpace ?? (hdr ? "bt2020nc" : "bt709");
        var inputRange = Range(input.ColorRange, defaultFull: hdr);
        var outputPrimaries = hdr ? reference.ColorPrimaries ?? "bt2020" : "bt709";
        var outputTransfer = hdr ? reference.ColorTransfer ?? "smpte2084" : "bt709";
        var outputMatrix = hdr ? reference.ColorSpace ?? "bt2020nc" : "bt709";
        var outputRange = hdr ? Range(reference.ColorRange, defaultFull: true) : "limited";
        var format = hdr ? "yuv420p10le" : "yuv420p";
        return $"zscale=w={width}:h={height}:min={inputMatrix}:tin={inputTransfer}:pin={inputPrimaries}:rin={inputRange}:m={outputMatrix}:t={outputTransfer}:p={outputPrimaries}:r={outputRange},format={format}";
    }

    private static string Range(string? value, bool defaultFull)
        => value is "pc" or "jpeg" or "full" ? "full" : value is "tv" or "mpeg" or "limited" ? "limited" : defaultFull ? "full" : "limited";

    private static bool Same(string? left, string? right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static double ParseMetric(string value)
        => value.Equals("inf", StringComparison.OrdinalIgnoreCase) ? double.PositiveInfinity : double.Parse(value, CultureInfo.InvariantCulture);

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
