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
            foreach (var path in chunkPaths)
            {
                var chunk = await FfprobeClient.ProbeAsync(path, ct);
                var videoOnly = await EnsureVideoOnlyAsync(chunk, settings.OutputDirectory, settings.LogDirectory, ct);
                inputs.Add(ReferenceEquals(videoOnly, chunk.FilePath) || videoOnly == chunk.FilePath
                    ? chunk
                    : await FfprobeClient.ProbeAsync(videoOnly, ct));
            }
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

                if (input.HasAudio)
                    throw new InvalidOperationException(
                        $"{Path.GetFileName(input.FilePath)} ses taşıyor. HandBrake tarafı -a none ile koşuyor; sesli girdi bütçeyi eşitsiz böler.");

                _log.WriteLine($"--- {Path.GetFileName(input.FilePath)} | hedef {inputTarget:0.###} MB | {input.DurationSeconds:0.###} sn | {input.Fps:0.###} fps");

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
        var geometry = GeometryGate.Check(reference.Width, reference.Height, candidate.Width, candidate.Height);
        var alignment = !rate.Comparable ? rate.Reason
            : !geometry.Comparable ? geometry.Reason
            : $"{rate.Reason}; {geometry.Reason}";
        var weight = (int)Math.Round(reference.DurationSeconds * reference.Fps, MidpointRounding.AwayFromZero);

        Measurement Fail(string error) => new(
            outcome.Competitor, Path.GetFileName(reference.FilePath), targetMb, outcome.Bytes, parity.BaselineBytes,
            parity.DeltaPercent, parity.Equal, parity.Stamp,
            gate.Kind.ToString(), gate.Label, gate.Reason, alignment, false, error,
            null, null, null, null, null, null, Math.Max(weight, 1),
            outcome.Settings, outcome.CommandLine, outcome.LogPath, outcome.OutputPath);

        if (!gate.Measurable) return Fail(gate.Reason);
        if (!rate.Comparable) return Fail(rate.Reason);
        if (!geometry.Comparable) return Fail(geometry.Reason);

        var score = gate.Kind == ColorGateKind.ReferenceTransformed
            ? await QualityMeter.MeasureTonemappedReferenceAsync(reference.FilePath, outcome.OutputPath, ct)
            : await QualityMeter.MeasureAsync(reference.FilePath, outcome.OutputPath, ct);

        if (!score.Comparable) return Fail(score.Message ?? "QualityMeter karşılaştırmayı reddetti.");
        if (score.VmafNegHarmonic is null) return Fail("VMAF-NEG okunamadı.");

        return new Measurement(
            outcome.Competitor, Path.GetFileName(reference.FilePath), targetMb, outcome.Bytes, parity.BaselineBytes,
            parity.DeltaPercent, parity.Equal, parity.Stamp,
            gate.Kind.ToString(), gate.Label, gate.Reason, alignment, true, null,
            score.VmafNegMean, score.VmafNegHarmonic, score.VmafNegP10, score.VmafNegMin, score.Xpsnr, score.Ssim,
            Math.Max(weight, 1),
            outcome.Settings, outcome.CommandLine, outcome.LogPath, outcome.OutputPath);
    }

    private Task<string> VideoOnlyReferenceAsync(MediaInfo source, AbSettings settings, CancellationToken ct)
        => EnsureVideoOnlyAsync(source, settings.ChunkDirectory, settings.LogDirectory, ct);

    public static async Task<double> StartTimeSecondsAsync(string path, CancellationToken ct)
    {
        var args = new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=start_time", "-of", "csv=p=0", path };
        var (exitCode, output) = await ProcessLauncher.RunAsync("ffprobe", args, ct);
        if (exitCode != 0) throw new InvalidOperationException($"start_time okunamadı: {path}");
        var text = output.Trim();
        return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            ? seconds
            : 0.0;
    }

    private async Task<string> EnsureVideoOnlyAsync(MediaInfo info, string directory, string logDirectory, CancellationToken ct)
    {
        var startSeconds = await StartTimeSecondsAsync(info.FilePath, ct);
        var shifted = Math.Abs(startSeconds) > 1e-6;
        if (!info.HasAudio && !shifted) return info.FilePath;

        var name = Path.GetFileNameWithoutExtension(info.FilePath) + "-yalniz-video.mkv";
        var target = Path.Combine(directory, name);
        if (File.Exists(target) && new FileInfo(target).Length > 0
            && Math.Abs(await StartTimeSecondsAsync(target, ct)) <= 1e-6)
        {
            _log.WriteLine($"normalize girdi hazır: {target}");
            return target;
        }

        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(logDirectory);
        var args = new[]
        {
            "-hide_banner", "-nostdin", "-y", "-fflags", "+genpts", "-i", info.FilePath,
            "-map", "0:v:0", "-c", "copy", "-avoid_negative_ts", "make_zero", target
        };
        var why = info.HasAudio
            ? shifted ? "sesli ve zaman damgası kaymış" : "sesli"
            : $"zaman damgası kaymış (start_time={startSeconds.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)})";
        _log.WriteLine($"girdi normalize ediliyor ({Path.GetFileName(info.FilePath)} {why}): " + ProcessLauncher.CommandLine("ffmpeg", args));
        var (exitCode, output) = await ProcessLauncher.RunAsync("ffmpeg", args, ct);
        await File.WriteAllTextAsync(Path.Combine(logDirectory, Path.GetFileNameWithoutExtension(name) + ".log"), output, ct);
        if (exitCode != 0) throw new InvalidOperationException($"Video-only girdi üretilemedi: {info.FilePath}");
        return target;
    }

    public static async Task<(bool Measured, string Text)> InspectAsync(string referencePath, string candidatePath, CancellationToken ct)
    {
        var reference = await FfprobeClient.ProbeAsync(referencePath, ct);
        var candidate = await FfprobeClient.ProbeAsync(candidatePath, ct);
        var referenceSignature = ColorSignature.From(reference);
        var candidateSignature = ColorSignature.From(candidate);
        var gate = ColorGate.Decide(referenceSignature, candidateSignature);
        var rate = RateGate.Check(reference.Fps, candidate.Fps);
        var geometry = GeometryGate.Check(reference.Width, reference.Height, candidate.Width, candidate.Height);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"referans : {Path.GetFileName(referencePath)} | {referenceSignature.Describe()} | hdr={reference.IsHdr} | {reference.Width}x{reference.Height} | {reference.Fps:0.###} fps");
        sb.AppendLine($"aday     : {Path.GetFileName(candidatePath)} | {candidateSignature.Describe()} | hdr={candidate.IsHdr} | {candidate.Width}x{candidate.Height} | {candidate.Fps:0.###} fps");
        sb.AppendLine($"kapı     : {gate.Kind} — {gate.Reason}");
        sb.AppendLine($"kare hızı: {rate.Reason}");
        sb.AppendLine($"geometri : {geometry.Reason}");

        if (!gate.Measurable)
        {
            sb.AppendLine("sonuç    : SAYI BASILMADI — renk kapısı reddetti.");
            return (false, sb.ToString());
        }
        if (!rate.Comparable)
        {
            sb.AppendLine("sonuç    : SAYI BASILMADI — kare hızı kapısı reddetti.");
            return (false, sb.ToString());
        }
        if (!geometry.Comparable)
        {
            sb.AppendLine("sonuç    : SAYI BASILMADI — geometri kapısı reddetti.");
            return (false, sb.ToString());
        }

        var score = gate.Kind == ColorGateKind.ReferenceTransformed
            ? await QualityMeter.MeasureTonemappedReferenceAsync(referencePath, candidatePath, ct)
            : await QualityMeter.MeasureAsync(referencePath, candidatePath, ct);

        if (!score.Comparable)
        {
            sb.AppendLine($"sonuç    : SAYI BASILMADI — {score.Message}");
            return (false, sb.ToString());
        }

        sb.AppendLine($"etiket   : {gate.Label}");
        sb.AppendLine($"sonuç    : harm={Fmt(score.VmafNegHarmonic)} p10={Fmt(score.VmafNegP10)} min={Fmt(score.VmafNegMin)} ort={Fmt(score.VmafNegMean)} XPSNR={Fmt(score.Xpsnr)} SSIM={Fmt(score.Ssim)}");
        return (true, sb.ToString());
    }

    private static ICompetitor Create(string name)
        => name.ToLowerInvariant() switch
        {
            "handbrake" => new HandBrakeCompetitor(),
            "vidshrink" => new VidShrinkCompetitor("vidshrink", HdrPolicy.Preserve),
            "vidshrink-sdr" => new VidShrinkCompetitor("vidshrink-sdr", HdrPolicy.TonemapToSdr),
            _ => throw new ArgumentException($"Bilinmeyen yarışmacı: {name}")
        };

    private static string Describe(Measurement m)
        => m.Measured
            ? $"{m.Competitor}: {m.Bytes} bayt ({m.SizeDeltaPercent:+0.00;-0.00;0.00}%){(m.SizeEqual ? "" : " " + m.SizeStamp)} | {m.ColorLabel} | harm={Fmt(m.VmafNegHarmonic)} p10={Fmt(m.VmafNegP10)} min={Fmt(m.VmafNegMin)} ort={Fmt(m.VmafNegMean)} XPSNR={Fmt(m.Xpsnr)}"
            : $"{m.Competitor}: {m.Bytes} bayt | ÖLÇÜLMEDİ — {m.Error}";

    internal static string Fmt(double? value)
        => value is { } v && !double.IsInfinity(v) ? v.ToString("0.00", CultureInfo.InvariantCulture) : "yok";
}
