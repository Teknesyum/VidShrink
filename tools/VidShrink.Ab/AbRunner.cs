using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Ab;

public sealed record Measurement(
    string Competitor,
    string Input,
    double TargetMb,
    long Bytes,
    long BaselineBytes,
    double SizeDeltaPercent,
    bool SizeEqual,
    string SizeStamp,
    string ColorGate,
    string ColorLabel,
    string ColorReason,
    string RateReason,
    bool Measured,
    string? Error,
    double? VmafNegMean,
    double? VmafNegHarmonic,
    double? VmafNegP10,
    double? VmafNegMin,
    double? Xpsnr,
    double? Ssim,
    int FrameWeight,
    string Settings,
    string CommandLine,
    string LogPath,
    string OutputPath);

public sealed record CompetitorSummary(
    string Competitor,
    double TargetMb,
    long TotalBytes,
    bool AllSizesEqual,
    bool AllMeasured,
    string ColorLabel,
    double? VmafNegMean,
    double? VmafNegHarmonic,
    double? VmafNegWorstP10,
    double? VmafNegMin,
    double? Xpsnr,
    double? Ssim,
    IReadOnlyList<string> Inputs);

public sealed record AbReport(
    string Source,
    string Mode,
    double TolerancePercent,
    string FfmpegVersion,
    string StartedUtc,
    IReadOnlyList<double> Targets,
    IReadOnlyList<Measurement> Measurements,
    IReadOnlyList<CompetitorSummary> Summaries);

public sealed class AbRunner
{
    private readonly TextWriter _log;

    public AbRunner(TextWriter log) => _log = log;

    public async Task<AbReport> RunAsync(AbSettings settings, CancellationToken ct)
    {
        Directory.CreateDirectory(settings.OutputDirectory);
        Directory.CreateDirectory(settings.LogDirectory);

        var source = await FfprobeClient.ProbeAsync(settings.SourcePath, ct);
        var referencePath = await VideoOnlyReferenceAsync(source, settings, ct);
        var reference = await FfprobeClient.ProbeAsync(referencePath, ct);

        var inputs = new List<MediaInfo>();
        if (settings.ChunkMode)
        {
            var chunkPaths = await ChunkCutter.EnsureAsync(referencePath, settings.ChunkDirectory, _log, ct);
            foreach (var path in chunkPaths) inputs.Add(await FfprobeClient.ProbeAsync(path, ct));
        }
        else
        {
            inputs.Add(reference);
        }

        var competitors = settings.Competitors.Select(Create).ToList();
        var measurements = new List<Measurement>();
        var summaries = new List<CompetitorSummary>();

        foreach (var targetMb in settings.TargetsMb)
        {
            var perCompetitor = competitors.ToDictionary(c => c.Name, _ => new List<Measurement>());

            foreach (var input in inputs)
            {
                var share = reference.DurationSeconds <= 0
                    ? targetMb
                    : targetMb * (input.DurationSeconds / reference.DurationSeconds);
                var inputTarget = settings.ChunkMode ? share : targetMb;

                _log.WriteLine($"--- {Path.GetFileName(input.FilePath)} | hedef {inputTarget:0.###} MB | {input.DurationSeconds:0.###} sn");

                var outcomes = new List<EncodeOutcome>();
                foreach (var competitor in competitors)
                {
                    _log.WriteLine($"kodlanıyor: {competitor.Name}");
                    outcomes.Add(await competitor.EncodeAsync(input, inputTarget, settings.OutputDirectory, settings.LogDirectory, ct));
                }

                var baseline = outcomes[0].Bytes;
                foreach (var outcome in outcomes)
                {
                    var parity = SizeParityCheck.Evaluate(baseline, outcome.Bytes, settings.TolerancePercent);
                    var measurement = await MeasureAsync(input, outcome, inputTarget, parity, ct);
                    measurements.Add(measurement);
                    perCompetitor[outcome.Competitor].Add(measurement);
                    _log.WriteLine(Describe(measurement));
                }
            }

            foreach (var competitor in competitors)
            {
                var parts = perCompetitor[competitor.Name];
                summaries.Add(Summarize(competitor.Name, targetMb, parts));
            }
        }

        return new AbReport(
            settings.SourcePath,
            settings.ChunkMode ? "parca" : "tam",
            settings.TolerancePercent,
            ToolLocator.GetFfmpegVersion(),
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            settings.TargetsMb,
            measurements,
            summaries);
    }

    private static CompetitorSummary Summarize(string competitor, double targetMb, IReadOnlyList<Measurement> parts)
    {
        var measured = parts.Where(p => p.Measured).ToList();
        var allMeasured = parts.Count > 0 && measured.Count == parts.Count;
        CombinedQuality? combined = null;
        if (allMeasured)
        {
            combined = ChunkAggregate.Combine(measured
                .Select(m => new ChunkQuality(m.Input, m.FrameWeight, m.VmafNegMean, m.VmafNegHarmonic, m.VmafNegP10, m.VmafNegMin, m.Xpsnr, m.Ssim))
                .ToList());
        }

        return new CompetitorSummary(
            competitor,
            targetMb,
            parts.Sum(p => p.Bytes),
            parts.All(p => p.SizeEqual),
            allMeasured,
            parts.Count == 0 ? "ölçülmedi" : string.Join(" + ", parts.Select(p => p.ColorLabel).Distinct()),
            combined?.Mean,
            combined?.Harmonic,
            combined?.WorstP10,
            combined?.Min,
            combined?.Xpsnr,
            combined?.Ssim,
            parts.Select(p => p.Input).ToList());
    }

    private async Task<Measurement> MeasureAsync(MediaInfo reference, EncodeOutcome outcome, double targetMb, SizeParity parity, CancellationToken ct)
    {
        var candidate = await FfprobeClient.ProbeAsync(outcome.OutputPath, ct);
        var gate = ColorGate.Decide(ColorSignature.From(reference), ColorSignature.From(candidate));
        var rate = RateGate.Check(reference.Fps, candidate.Fps);
        var weight = (int)Math.Round(reference.DurationSeconds * reference.Fps, MidpointRounding.AwayFromZero);

        Measurement Fail(string error) => new(
            outcome.Competitor, Path.GetFileName(reference.FilePath), targetMb, outcome.Bytes, parity.BaselineBytes,
            parity.DeltaPercent, parity.Equal, parity.Stamp,
            gate.Kind.ToString(), gate.Label, gate.Reason, rate.Reason, false, error,
            null, null, null, null, null, null, Math.Max(weight, 1),
            outcome.Settings, outcome.CommandLine, outcome.LogPath, outcome.OutputPath);

        if (!gate.Measurable) return Fail(gate.Reason);
        if (!rate.Comparable) return Fail(rate.Reason);

        var score = gate.Kind == ColorGateKind.ReferenceTransformed
            ? await QualityMeter.MeasureTonemappedReferenceAsync(reference.FilePath, outcome.OutputPath, ct)
            : await QualityMeter.MeasureAsync(reference.FilePath, outcome.OutputPath, ct);

        if (!score.Comparable) return Fail(score.Message ?? "QualityMeter karşılaştırmayı reddetti.");
        if (score.VmafNegHarmonic is null) return Fail("VMAF-NEG okunamadı.");

        return new Measurement(
            outcome.Competitor, Path.GetFileName(reference.FilePath), targetMb, outcome.Bytes, parity.BaselineBytes,
            parity.DeltaPercent, parity.Equal, parity.Stamp,
            gate.Kind.ToString(), gate.Label, gate.Reason, rate.Reason, true, null,
            score.VmafNegMean, score.VmafNegHarmonic, score.VmafNegP10, score.VmafNegMin, score.Xpsnr, score.Ssim,
            Math.Max(weight, 1),
            outcome.Settings, outcome.CommandLine, outcome.LogPath, outcome.OutputPath);
    }

    private async Task<string> VideoOnlyReferenceAsync(MediaInfo source, AbSettings settings, CancellationToken ct)
    {
        if (!source.HasAudio) return source.FilePath;

        var target = Path.Combine(settings.ChunkDirectory,
            Path.GetFileNameWithoutExtension(source.FilePath) + "-yalniz-video.mkv");
        if (File.Exists(target) && new FileInfo(target).Length > 0)
        {
            _log.WriteLine($"video-only referans hazır: {target}");
            return target;
        }

        Directory.CreateDirectory(settings.ChunkDirectory);
        var args = new[] { "-hide_banner", "-nostdin", "-y", "-i", source.FilePath, "-map", "0:v:0", "-c", "copy", target };
        _log.WriteLine("video-only referans kesiliyor: " + ProcessLauncher.CommandLine("ffmpeg", args));
        var (exitCode, output) = await ProcessLauncher.RunAsync("ffmpeg", args, ct);
        await File.WriteAllTextAsync(Path.Combine(settings.LogDirectory, "referans-yalniz-video.log"), output, ct);
        if (exitCode != 0) throw new InvalidOperationException("Video-only referans üretilemedi.");
        return target;
    }

    private static ICompetitor Create(string name)
        => name.ToLowerInvariant() switch
        {
            "handbrake" => new HandBrakeCompetitor(),
            "vidshrink" => new VidShrinkCompetitor(),
            _ => throw new ArgumentException($"Bilinmeyen yarışmacı: {name}")
        };

    private static string Describe(Measurement m)
        => m.Measured
            ? $"{m.Competitor}: {m.Bytes} bayt ({m.SizeDeltaPercent:+0.00;-0.00;0.00}%){(m.SizeEqual ? "" : " " + m.SizeStamp)} | {m.ColorLabel} | harm={Fmt(m.VmafNegHarmonic)} p10={Fmt(m.VmafNegP10)} min={Fmt(m.VmafNegMin)} ort={Fmt(m.VmafNegMean)} XPSNR={Fmt(m.Xpsnr)}"
            : $"{m.Competitor}: {m.Bytes} bayt | ÖLÇÜLMEDİ — {m.Error}";

    internal static string Fmt(double? value)
        => value is { } v && !double.IsInfinity(v) ? v.ToString("0.00", CultureInfo.InvariantCulture) : "yok";
}
