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
    string? ColorNormalization = null,
    double? VmafNegWorstScene = null,
    double? WorstSceneStartSeconds = null,
    double? SceneWindowSeconds = null,
    bool TonemappedReference = false,
    TimestampAlignment? Alignment = null);

public sealed record TimestampAlignment(
    double ReferenceOffsetSeconds,
    double TestOffsetSeconds,
    double FrameDurationSeconds)
{
    public double ShiftSeconds => ReferenceOffsetSeconds - TestOffsetSeconds;

    public double ShiftFrames => FrameDurationSeconds > 0 ? ShiftSeconds / FrameDurationSeconds : 0;

    public bool Shifted => Math.Abs(ShiftSeconds) > 1e-6;

    public string? Note => Shifted
        ? "Kaynak ve test zaman damgaları "
          + (ShiftSeconds * 1000).ToString("0.###", CultureInfo.InvariantCulture) + " ms ("
          + ShiftFrames.ToString("0.###", CultureInfo.InvariantCulture)
          + " kare) ayrık; kareler zaman damgasına değil kare indeksine eşlendi."
        : null;
}

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
                score.VmafNegP10, true, watch.ElapsedMilliseconds, score.Message,
                score.VmafNegMin, score.VmafNegWorstScene,
                score.WorstSceneStartSeconds, score.SceneWindowSeconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (QualityMeasurementFailedException failure)
        {
            return new VidShrink.Core.WindowQualityMeasurement(
                referenceStartSeconds, null, null, null, false, watch.ElapsedMilliseconds, failure.Message);
        }
        catch { return null; }
    }
}

public static class MeasureFilterGraph
{
    public static readonly string FrameLock = "settb=AVTB,setpts=N";

    public static string Build(string testChain, string referenceChain, string comparisonFilter)
    {
        if (string.IsNullOrWhiteSpace(testChain))
            throw new ArgumentException("Test normalizasyonu boş olamaz.", nameof(testChain));
        if (string.IsNullOrWhiteSpace(referenceChain))
            throw new ArgumentException("Referans normalizasyonu boş olamaz.", nameof(referenceChain));
        if (string.IsNullOrWhiteSpace(comparisonFilter))
            throw new ArgumentException("Karşılaştırma filtresi boş olamaz.", nameof(comparisonFilter));

        return $"[0:v]{testChain},{FrameLock}[t];" +
               $"[1:v]{referenceChain},{FrameLock}[r];" +
               $"[t][r]{comparisonFilter}";
    }
}

public sealed class QualityMeasurementFailedException : InvalidOperationException
{
    public QualityMeasurementFailedException(string message, QualityScore? partialScore = null)
        : base(message) => PartialScore = partialScore;

    public QualityScore? PartialScore { get; }
}

public static class QualityMeter
{
    public static async Task<double> TimestampOffsetSecondsAsync(string path, CancellationToken ct = default)
    {
        var args = new[]
        {
            "-v", "error", "-show_entries", "stream=codec_type,start_time",
            "-of", "json", path
        };
        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, args) };
        process.Start();
        using var registration = ct.Register(() => TryKill(process));
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0) return 0;

        double? video = null;
        var earliest = double.PositiveInfinity;
        try
        {
            using var doc = JsonDocument.Parse(await stdoutTask);
            if (!doc.RootElement.TryGetProperty("streams", out var streams)) return 0;
            foreach (var stream in streams.EnumerateArray())
            {
                if (!stream.TryGetProperty("start_time", out var raw)) continue;
                if (!double.TryParse(raw.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var start)) continue;
                if (start < earliest) earliest = start;
                var type = stream.TryGetProperty("codec_type", out var t) ? t.GetString() : null;
                if (video is null && string.Equals(type, "video", StringComparison.OrdinalIgnoreCase)) video = start;
            }
        }
        catch (JsonException) { return 0; }

        if (video is null || double.IsPositiveInfinity(earliest)) return 0;
        return video.Value - earliest;
    }

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
        => await MeasureAsync(referencePath, testPath, false, null, null, null, null, ct);

    public static async Task<QualityScore> MeasureAsync(string referencePath, string testPath, VidShrink.Core.SceneMap? sceneMap, CancellationToken ct = default)
        => await MeasureAsync(referencePath, testPath, false, null, null, null, sceneMap, ct);

    public static async Task<QualityScore> MeasureTonemappedReferenceAsync(string referencePath, string testPath, CancellationToken ct = default)
        => await MeasureAsync(referencePath, testPath, true, null, null, null, null, ct);

    public static async Task<QualityScore> MeasureWindowAsync(string referencePath, string testPath, double startSeconds, double durationSeconds, CancellationToken ct = default)
        => await MeasureAsync(referencePath, testPath, false, startSeconds, startSeconds, durationSeconds, null, ct);

    public static async Task<QualityScore> MeasureWindowAsync(string referencePath, string testPath, double referenceStartSeconds, double testStartSeconds, double durationSeconds, CancellationToken ct = default)
        => await MeasureAsync(referencePath, testPath, false, referenceStartSeconds, testStartSeconds, durationSeconds, null, ct);

    public static async Task<QualityScore> MeasureWindowAsync(string referencePath, string testPath, double referenceStartSeconds, double testStartSeconds, double durationSeconds, VidShrink.Core.SceneMap? sceneMap, CancellationToken ct = default)
        => await MeasureAsync(referencePath, testPath, false, referenceStartSeconds, testStartSeconds, durationSeconds, sceneMap, ct);

    private static async Task<QualityScore> MeasureAsync(string referencePath, string testPath, bool tonemapReference, double? referenceStartSeconds, double? testStartSeconds, double? durationSeconds, VidShrink.Core.SceneMap? sceneMap, CancellationToken ct)
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
        var frameRate = test.Fps > 0 ? test.Fps : reference.Fps;
        var alignment = new TimestampAlignment(
            await TimestampOffsetSecondsAsync(referencePath, ct),
            await TimestampOffsetSecondsAsync(testPath, ct),
            frameRate > 0 ? 1.0 / frameRate : 0);

        var incompatibility = ColorIncompatibility(measuredReference, test);
        if (incompatibility is not null)
            return new QualityScore(null, null, null, null, null, null, false, incompatibility, Alignment: alignment);

        var normalization = tonemapReference
            ? $"HDR referans {VidShrink.Core.HdrResolver.TonemapFilter} ile bt709 limited'a tonemap edildi; test aynı uzaya normalize edildi."
            : NormalizationDescription(reference, test);

        VmafAggregate? vmaf = null;
        QualityMeasurementFailedException? vmafFailure = null;
        if (EncoderCapabilities.Instance.HasFilter("libvmaf"))
        {
            try
            {
                vmaf = await MeasureVmafAsync(testPath, referencePath, measuredReference, test, tonemapReference, referenceStartSeconds, testStartSeconds, durationSeconds, frameRate, sceneMap, ct);
            }
            catch (QualityMeasurementFailedException failure)
            {
                vmafFailure = failure;
            }
        }

        double? xpsnr = EncoderCapabilities.Instance.HasFilter("xpsnr")
            ? await MeasureXpsnrAsync(testPath, referencePath, measuredReference, test, tonemapReference, referenceStartSeconds, testStartSeconds, durationSeconds, ct)
            : null;

        double? ssim = EncoderCapabilities.Instance.HasFilter("ssim")
            ? await MeasureSsimAsync(testPath, referencePath, measuredReference, test, tonemapReference, referenceStartSeconds, testStartSeconds, durationSeconds, ct)
            : null;

        if (vmafFailure is not null)
            throw new QualityMeasurementFailedException(
                vmafFailure.Message,
                new QualityScore(
                    null, null, null, null, xpsnr, ssim, false, vmafFailure.Message, normalization,
                    null, null, null, tonemapReference, alignment));

        return new QualityScore(
            vmaf?.Mean, vmaf?.Harmonic, vmaf?.P10, vmaf?.Min, xpsnr, ssim, true, null, normalization,
            vmaf?.WorstScene, vmaf?.WorstSceneStartSeconds,
            vmaf?.WorstSceneUnitSeconds, tonemapReference, alignment);
    }

    public readonly record struct VmafAggregate(
        double Mean, double Harmonic, double P10, double Min, double WorstScene, double WorstSceneStartSeconds, double WorstSceneUnitSeconds);

    public static async Task<IReadOnlyList<double>> ReadVmafScoresAsync(string logPath, CancellationToken ct = default)
    {
        if (!File.Exists(logPath))
            throw new QualityMeasurementFailedException(
                $"libvmaf gunlugu yazilmadi: {logPath}. Filtre zinciri kosdu ama olcum uretmedi; bu olculmedi degil, olcum basarisiz.");

        var scores = new List<double>();
        try
        {
            await using var stream = File.OpenRead(logPath);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            foreach (var frame in doc.RootElement.GetProperty("frames").EnumerateArray())
            {
                var metrics = frame.GetProperty("metrics");
                if (metrics.TryGetProperty("vmaf", out var v) || metrics.TryGetProperty("vmaf_neg", out v))
                    scores.Add(v.GetDouble());
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException and not QualityMeasurementFailedException)
        {
            throw new QualityMeasurementFailedException(
                $"libvmaf gunlugu okunamadi: {logPath}. {ex.Message}");
        }

        if (scores.Count == 0)
            throw new QualityMeasurementFailedException(
                $"libvmaf gunlugu kare puani icermiyor: {logPath}. Olcum basarisiz.");

        return scores;
    }

    private static async Task<VmafAggregate> MeasureVmafAsync(
        string testPath, string referencePath, VidShrink.Core.MediaInfo reference, VidShrink.Core.MediaInfo test, bool tonemapReference, double? referenceStartSeconds, double? testStartSeconds, double? durationSeconds, double frameRate, VidShrink.Core.SceneMap? sceneMap, CancellationToken ct)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "vidshrink_vmaf_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var filter = $"libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path={EscapeFilterPath(logPath)}";
            await RunFilterAsync(testPath, referencePath, reference, test, tonemapReference, referenceStartSeconds, testStartSeconds, durationSeconds, filter, ct);

            var scores = await ReadVmafScoresAsync(logPath, ct);

            return AggregateVmaf(scores, frameRate, referenceStartSeconds ?? 0, sceneMap);
        }
        finally { TryDelete(logPath); }
    }

    public static VmafAggregate AggregateVmaf(
        IReadOnlyList<double> scores, double frameRate, double offsetSeconds, VidShrink.Core.SceneMap? sceneMap = null)
    {
        var mean = scores.Average();
        var harmonic = scores.Count / scores.Sum(x => 1.0 / Math.Max(x, 1.0));
        var sorted = scores.OrderBy(x => x).ToList();
        var unit = WorstSceneUnit(scores, frameRate, offsetSeconds, sceneMap);
        return new VmafAggregate(mean, harmonic, Percentile(sorted, 10), sorted[0], unit.Score, unit.StartSeconds, unit.UnitSeconds);
    }

    public const double SceneWindowSeconds = 2.0;

    public const double MinimumUnitSeconds = 0.5;

    public readonly record struct WorstUnit(double Score, double StartSeconds, double UnitSeconds);

    public static (double Worst, double StartSeconds) WorstScene(
        IReadOnlyList<double> scores, double frameRate, double offsetSeconds)
        => WorstScene(scores, frameRate, offsetSeconds, null);

    public static (double Worst, double StartSeconds) WorstScene(
        IReadOnlyList<double> scores, double frameRate, double offsetSeconds, VidShrink.Core.SceneMap? map)
    {
        var unit = WorstSceneUnit(scores, frameRate, offsetSeconds, map);
        return (unit.Score, unit.StartSeconds);
    }

    public static WorstUnit WorstSceneUnit(
        IReadOnlyList<double> scores, double frameRate, double offsetSeconds, VidShrink.Core.SceneMap? map = null)
    {
        ArgumentNullException.ThrowIfNull(scores);
        if (scores.Count == 0)
            throw new ArgumentException("En kotu birim icin en az bir kare puani gerekli.", nameof(scores));

        var fps = frameRate > 0 ? frameRate : 25.0;
        var minFrames = Math.Max(1, (int)Math.Round(fps * MinimumUnitSeconds));
        var bounds = MergeShortUnits(
            SceneBounds(map, scores.Count, fps, offsetSeconds) ?? FixedBounds(scores.Count, fps),
            minFrames);

        var worst = double.PositiveInfinity;
        var at = offsetSeconds;
        var unitFrames = scores.Count;
        for (var b = 0; b + 1 < bounds.Count; b++)
        {
            var start = bounds[b];
            var count = bounds[b + 1] - start;
            var sum = 0.0;
            for (var j = 0; j < count; j++) sum += scores[start + j];
            var mean = sum / count;
            if (mean < worst)
            {
                worst = mean;
                at = offsetSeconds + start / fps;
                unitFrames = count;
            }
        }

        return new WorstUnit(worst, at, unitFrames / fps);
    }

    private static List<int> MergeShortUnits(List<int> bounds, int minFrames)
    {
        if (bounds.Count <= 2) return bounds;

        var merged = new List<int> { bounds[0] };
        for (var b = 1; b < bounds.Count - 1; b++)
            if (bounds[b] - merged[^1] >= minFrames) merged.Add(bounds[b]);
        merged.Add(bounds[^1]);

        while (merged.Count > 2 && merged[^1] - merged[^2] < minFrames)
            merged.RemoveAt(merged.Count - 2);

        return merged;
    }

    private static List<int> FixedBounds(int count, double fps)
    {
        var step = Math.Max(1, (int)Math.Round(fps * SceneWindowSeconds));
        var bounds = new List<int>();
        for (var i = 0; i < count; i += step) bounds.Add(i);
        bounds.Add(count);
        return bounds;
    }

    private static List<int>? SceneBounds(
        VidShrink.Core.SceneMap? map, int count, double fps, double offsetSeconds)
    {
        if (map is null || map.Scenes.Count == 0) return null;

        var endSeconds = offsetSeconds + count / fps;
        var bounds = new List<int> { 0 };
        foreach (var scene in map.Scenes)
        {
            if (scene.End <= offsetSeconds || scene.End >= endSeconds) continue;
            var index = (int)Math.Round((scene.End - offsetSeconds) * fps);
            if (index > bounds[^1] && index < count) bounds.Add(index);
        }
        bounds.Add(count);
        return bounds.Count > 2 ? bounds : null;
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
        args.AddRange(new[] { "-lavfi", MeasureFilterGraph.Build(testNormalization, referencePrefix + referenceNormalization, filterChain), "-f", "null", "-" });

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
        if (!reference.IsHdr)
            return UnverifiableAssumption(reference, test) ?? UnverifiableAssumption(test, reference);
        if (!Same(reference.ColorTransfer, test.ColorTransfer) || !Same(reference.ColorPrimaries, test.ColorPrimaries))
            return "Renk uzayı uyuşmuyor; HDR aktarım işlevleri veya ana renkleri farklı, karşılaştırılamaz.";
        return null;
    }

    private static string? UnverifiableAssumption(VidShrink.Core.MediaInfo untagged, VidShrink.Core.MediaInfo tagged)
    {
        if (untagged.ColorPrimaries is not null || untagged.ColorTransfer is not null || untagged.ColorSpace is not null)
            return null;

        var conflicting = new List<string>();
        if (tagged.ColorPrimaries is { } p && !Same(p, "bt709") && !Same(p, "unknown")) conflicting.Add("ana renk " + p);
        if (tagged.ColorTransfer is { } t && !Same(t, "bt709") && !Same(t, "unknown")) conflicting.Add("aktarım " + t);
        if (tagged.ColorSpace is { } s && !Same(s, "bt709") && !Same(s, "unknown")) conflicting.Add("matris " + s);
        if (conflicting.Count == 0) return null;

        return $"Bir taraf etiketsiz, öteki taraf {string.Join(", ", conflicting)} taşıyor; "
             + "etiketsiz tarafın uzayı bt709 varsayılırsa iki taraf farklı uzaylardan normalize edilir. Ölçülemez.";
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
