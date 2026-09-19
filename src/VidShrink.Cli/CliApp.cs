using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Cli;

public sealed class CliServices
{
    public static CliServices Default { get; } = new();

    public Func<string?> MissingTool { get; init; } = () => ToolLocator.IsAvailable(out var missing) ? null : missing;
    public Func<string, CancellationToken, Task<MediaInfo>> Probe { get; init; } = FfprobeClient.ProbeAsync;
    public Func<IEncoderAvailability?> Availability { get; init; } = () => EncoderCapabilities.Instance;
    public IWatchFileSystem WatchFileSystem { get; init; } = PhysicalWatchFileSystem.Instance;
    public IWatchClock WatchClock { get; init; } = SystemWatchClock.Instance;
    public Func<string?> WatchStateFallbackDirectory { get; init; } = () => Path.GetDirectoryName(UpdateSettings.DefaultPath) is { } settings ? Path.Combine(settings, "izle") : null;
}

public sealed record CliDecision(
    MediaInfo Info,
    PlanOptions Options,
    PlanResult Result,
    ComplexityProfile Profile,
    SceneMapAttempt? Scenes,
    QualityTargetResult? QualityTarget,
    string OutputPath,
    IReadOnlyList<string> Arguments)
{
    public double TargetMb => Options.TargetMb;
    public EncodePlan Plan => Result.Plan;
}

public static class CliApp
{
    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr,
        CliText text, CliServices services, CancellationToken ct)
    {
        var selection = CliText.SplitLanguage(args);
        if (selection.Invalid is { } badLanguage)
        {
            stderr.WriteLine(text.Format("error.bad-language", badLanguage, string.Join(", ", CliText.Languages)));
            stderr.WriteLine(text["usage.hint"]);
            return ExitCodes.Usage;
        }

        if (selection.Language is { } chosen) text = CliText.ForLanguage(chosen);
        args = selection.Rest;

        var parsed = CliParser.Parse(args);
        if (!parsed.Ok)
        {
            stderr.WriteLine(text.Format(parsed.ErrorKey!, parsed.ErrorArgument));
            stderr.WriteLine(text["usage.hint"]);
            return ExitCodes.Usage;
        }

        var request = parsed.Request! with { PreferredLanguage = text.Language };
        switch (request.Command)
        {
            case CliCommand.Help:
                stdout.WriteLine(text.Format("help", CliText.Version));
                return ExitCodes.InBand;
            case CliCommand.Version:
                stdout.WriteLine(CliText.Version);
                return ExitCodes.InBand;
            case CliCommand.Gunluk:
            {
                var gunluk = Gunluk.Oku();
                if (gunluk.Length == 0)
                {
                    stdout.WriteLine(text["gunluk.bos"]);
                    return ExitCodes.InBand;
                }
                stdout.Write(gunluk);
                return ExitCodes.InBand;
            }
        }

        try
        {
            if (services.MissingTool() is { } missing)
            {
                stderr.WriteLine(text.Format("error.tool-missing", missing));
                return ExitCodes.Error;
            }

            if (request.Command == CliCommand.Watch)
                return await WatchAsync(request, services, stdout, stderr, text, ct);

            return (await ProcessFileAsync(request, services, stdout, stderr, text, ct)).ExitCode;
        }
        catch (OperationCanceledException)
        {
            stderr.WriteLine(text["result.cancelled"]);
            return ExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            stderr.WriteLine(text.Format("error.failed", ex.Message));
            return ExitCodes.Error;
        }
    }

    private sealed record FileRun(int ExitCode, string? Output, string? Error);

    private static async Task<FileRun> ProcessFileAsync(CliRequest request, CliServices services, TextWriter stdout,
        TextWriter stderr, CliText text, CancellationToken ct)
    {
        var input = Path.GetFullPath(request.Input!);
        if (!File.Exists(input))
        {
            stderr.WriteLine(text.Format("error.input-missing", input));
            return new FileRun(ExitCodes.Error, null, text.Format("error.input-missing", input));
        }

        stderr.WriteLine(text["progress.probe"]);
        var info = await services.Probe(input, ct);

        if (request.Scan)
        {
            stdout.Write(request.Json ? TaramaJson(info, request) : TaramaMetni(info, request, text));
            return new FileRun(ExitCodes.InBand, null, null);
        }

        var baslikSoruluyor = request.Title is not null || request.MainFeature || request.MinDurationSeconds is not null;
        if (info.Titles.Count > 0 || baslikSoruluyor)
        {
            if (SourceTitles.Sec(info.Titles, request.Title, request.MainFeature,
                    request.MinDurationSeconds, out var baslik) is { } baslikHatasi)
            {
                var ileti = text.Format(baslikHatasi, request.Title?.ToString(CultureInfo.InvariantCulture));
                stderr.WriteLine(ileti);
                return new FileRun(ExitCodes.Usage, null, ileti);
            }
            if (baslik is not null) info = SourceTitles.Uygula(info, baslik);
        }

        if (request.Resolved(info, out request) is { } trimError)
        {
            var trimMessage = text.Format(trimError, request.ChapterTo is int son && son != request.ChapterFrom
                ? $"{request.ChapterFrom}-{son}"
                : request.ChapterFrom?.ToString());
            stderr.WriteLine(trimMessage);
            return new FileRun(ExitCodes.Usage, null, trimMessage);
        }
        var availability = services.Availability();
        var decision = request.SkipMeasurement
            ? Decide(request, info, null, null, availability)
            : await MeasureAndDecideAsync(request, info, availability, stderr, text, ct);

        if (PathEquals(decision.OutputPath, info.FilePath))
        {
            stderr.WriteLine(text["error.same-output"]);
            return new FileRun(ExitCodes.Usage, null, text["error.same-output"]);
        }

        if (request.Command == CliCommand.Plan)
        {
            stdout.Write(request.Json ? PlanJson(request, decision) : PlanText(request, decision, text));
            return new FileRun(ExitCodes.InBand, null, null);
        }

        return await ShrinkAsync(request, decision, stdout, stderr, text, ct);
    }

    /// <summary>
    /// <c>--tarama</c>'nin metni. Kodlama yok: envanter basilir ve is biter. Asgari sure
    /// verilmisse elenen basliklar hic yazilmaz — kullanici listede gordugu her numarayi
    /// <c>--baslik</c> ile verebilsin.
    /// </summary>
    private static string TaramaMetni(MediaInfo info, CliRequest request, CliText text)
    {
        var basliklar = SourceTitles.Ele(info.Titles, request.MinDurationSeconds);
        var sb = new StringBuilder();
        sb.AppendLine(text.Format("scan.titles", basliklar.Count.ToString(CultureInfo.InvariantCulture)));
        foreach (var b in basliklar)
        {
            sb.Append("  ").Append(b.Number.ToString(CultureInfo.InvariantCulture)).Append("  ")
              .Append(TimeSpan.FromSeconds(b.DurationSeconds).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture))
              .Append("  ").Append(b.Width.ToString(CultureInfo.InvariantCulture)).Append('x')
              .Append(b.Height.ToString(CultureInfo.InvariantCulture))
              .Append("  ").Append(text.Format("scan.streams", b.StreamCount.ToString(CultureInfo.InvariantCulture)));
            if (!string.IsNullOrWhiteSpace(b.Label)) sb.Append("  ").Append(b.Label);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string TaramaJson(MediaInfo info, CliRequest request)
        => Json(!request.JsonLines, writer =>
        {
            writer.WriteString("command", "scan");
            writer.WriteStartArray("titles");
            foreach (var b in SourceTitles.Ele(info.Titles, request.MinDurationSeconds))
            {
                writer.WriteStartObject();
                writer.WriteNumber("number", b.Number);
                writer.WriteNumber("durationSeconds", Math.Round(b.DurationSeconds, 3));
                writer.WriteNumber("width", b.Width);
                writer.WriteNumber("height", b.Height);
                writer.WriteNumber("streams", b.StreamCount);
                writer.WriteNumber("chapters", b.ChapterCount);
                if (b.Label is { Length: > 0 }) writer.WriteString("label", b.Label);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        });

    private static async Task<int> WatchAsync(CliRequest request, CliServices services, TextWriter stdout,
        TextWriter stderr, CliText text, CancellationToken ct)
    {
        var fs = services.WatchFileSystem;
        var clock = services.WatchClock;
        var watchDirectory = Path.GetFullPath(request.Input!);
        var outputDirectory = Path.GetFullPath(request.Output!);
        if (!fs.DirectoryExists(watchDirectory))
        {
            stderr.WriteLine(text.Format("error.watch-missing", watchDirectory));
            return ExitCodes.Error;
        }
        if (WatchFolder.ValidateFolders(watchDirectory, outputDirectory) is { } folderError)
        {
            stderr.WriteLine(text[folderError]);
            return ExitCodes.Usage;
        }
        fs.CreateDirectory(outputDirectory);

        var location = WatchFolder.OpenState(fs, watchDirectory, outputDirectory, services.WatchStateFallbackDirectory(), clock.UtcNow);
        var load = location.Load;
        if (load.CorruptBackup is { } backup) stderr.WriteLine(text.Format("watch.corrupt-state", backup));
        if (!location.Writable) stderr.WriteLine(text.Format("watch.state-not-saved", location.Path, "-"));
        else if (!string.Equals(location.Path, Path.Combine(watchDirectory, WatchFolder.StateFileName), WatchFolder.PathComparison))
            stderr.WriteLine(text.Format("watch.state-elsewhere", location.Path));

        var interval = TimeSpan.FromSeconds(request.PollSeconds ?? 2);
        var watcher = new WatchFolder(new WatchOptions
        {
            WatchDirectory = watchDirectory,
            OutputDirectory = outputDirectory,
            StatePath = location.Path,
            PollInterval = interval,
            StableFor = interval,
            Once = request.Once
        }, fs, clock, load.State);

        stderr.WriteLine(text.Format("watch.started", watchDirectory, outputDirectory));
        var result = await watcher.RunAsync(async (path, token) =>
        {
            var fileRequest = request with { Command = CliCommand.Shrink, Input = path, Output = null, OutputDirectory = outputDirectory, JsonLines = request.Json };
            var run = await ProcessFileAsync(fileRequest, services, stdout, stderr, text, token);
            return new WatchOutcome(run.ExitCode, run.ExitCode is ExitCodes.InBand or ExitCodes.UnderBand or ExitCodes.CeilingExceeded,
                run.Output, run.Error);
        }, e =>
        {
            var name = Path.GetFileName(e.Path);
            switch (e.Kind)
            {
                case WatchEventKind.Waiting: stderr.WriteLine(text.Format("watch.waiting", name)); break;
                case WatchEventKind.Processing: stderr.WriteLine(text.Format("watch.processing", name)); break;
                case WatchEventKind.Done: stderr.WriteLine(text.Format("watch.done", name, e.Detail is null ? "-" : Path.GetFileName(e.Detail))); break;
                case WatchEventKind.Failed: stderr.WriteLine(text.Format("watch.failed", name, e.Detail)); break;
                case WatchEventKind.Skipped: stderr.WriteLine(text.Format("watch.skipped", name)); break;
                case WatchEventKind.Collided: stderr.WriteLine(text.Format("watch.collided", name, e.Detail)); break;
                case WatchEventKind.Changed: stderr.WriteLine(text.Format("watch.changed", name)); break;
                case WatchEventKind.StateNotSaved: stderr.WriteLine(text.Format("watch.state-not-saved", e.Path, e.Detail)); break;
                case WatchEventKind.Stopped: stderr.WriteLine(text["watch.stopped"]); break;
            }
        }, ct);

        if (watcher.CollidedCount > 0) stderr.WriteLine(text.Format("watch.collided.summary", watcher.CollidedCount));

        return result switch
        {
            WatchRunResult.Finished => watcher.FailedCount > 0 || watcher.CollidedCount > 0 ? ExitCodes.WatchFailures : ExitCodes.InBand,
            WatchRunResult.Cancelled => ExitCodes.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    public static CliDecision Decide(CliRequest request, MediaInfo info, ComplexityProfile? profile,
        SceneMapAttempt? scenes, IEncoderAvailability? availability)
    {
        var baseTarget = request.TargetMb ?? new PlanOptions().TargetMb;
        var settled = ShrinkEngine.SettledProfile(info, request.ToPlanOptions(baseTarget, info.DurationSeconds), profile, availability);

        QualityTargetResult? qualityTarget = null;
        var target = baseTarget;
        if (request.Quality is { } quality)
        {
            qualityTarget = ShrinkEngine.TargetForQuality(info, request.ToPlanOptions(baseTarget, info.DurationSeconds), quality, settled, availability);
            var rounded = ShrinkEngine.RoundedTargetMb(qualityTarget);
            target = rounded > 0 ? rounded : qualityTarget.TargetMb;
        }

        var options = request.ToPlanOptions(target, info.DurationSeconds);
        var result = ShrinkEngine.Decide(info, options, settled, availability);
        var extension = result.Plan.Streams?.Extension ?? "mp4";
        var output = request.Output is { } path ? Path.GetFullPath(path)
            : request.OutputDirectory is { } directory ? ShrinkEngine.UniqueOutputPath(Path.Combine(Path.GetFullPath(directory), Path.GetFileName(info.FilePath)), extension: extension)
            : ShrinkEngine.UniqueOutputPath(info.FilePath, extension: extension);
        var arguments = ShrinkEngine.DisplayedArguments(info, result.Plan, output, availability, scenes?.Map);
        return new CliDecision(info, options, result, settled, scenes, qualityTarget, output, arguments);
    }

    public static async Task<CliDecision> MeasureAndDecideAsync(CliRequest request, MediaInfo info,
        IEncoderAvailability? availability, TextWriter stderr, CliText text, CancellationToken ct)
    {
        stderr.WriteLine(text["progress.scenes"]);
        var scenes = await EncodeRunner.TryBuildSceneMapAsync(info, ct: ct);
        var draft = Decide(request, info, null, scenes, availability);

        stderr.WriteLine(text["progress.measure"]);
        var profile = await ShrinkEngine.CalibrateAsync(info, scenes, draft.Options.SpeedMode,
            () => request.ToPlanOptions(draft.TargetMb, info.DurationSeconds), availability,
            (stage, _) =>
            {
                if (stage == ShrinkMeasureStage.Probed) stderr.WriteLine(text["progress.calibrate"]);
                return true;
            }, ct);

        return Decide(request, info, profile, scenes, availability);
    }

    public static int ExitCodeFor(EncodeResult result)
        => result.CeilingExceeded ? ExitCodes.CeilingExceeded
            : !result.Success ? ExitCodes.Error
            : result.UnderBand ? ExitCodes.UnderBand
            : ExitCodes.InBand;

    private static async Task<FileRun> ShrinkAsync(CliRequest request, CliDecision decision, TextWriter stdout,
        TextWriter stderr, CliText text, CancellationToken ct)
    {
        var targetMb = decision.TargetMb;
        if (DiskSpaceGuard.TryGetFreeBytes(decision.OutputPath, out var freeBytes) && !DiskSpaceGuard.HasEnoughSpace(freeBytes, targetMb))
        {
            stderr.WriteLine(text.Format("error.no-space", Num(DiskSpaceGuard.RequiredBytes(targetMb) / 1024.0 / 1024.0, "0", text)));
            return new FileRun(ExitCodes.Error, null, text.Format("error.no-space", Num(DiskSpaceGuard.RequiredBytes(targetMb) / 1024.0 / 1024.0, "0", text)));
        }

        var clock = Stopwatch.StartNew();
        var result = await ShrinkEngine.EncodeAsync(decision.Info, decision.Plan, decision.OutputPath, targetMb,
            new StderrProgress(stderr, text), ct, decision.Options.FillPolicy, decision.Profile, null, decision.Scenes?.Map);
        clock.Stop();

        QualityScore? vmaf = null;
        if (request.MeasureVmaf && result.Success && File.Exists(result.OutputPath) && QualityMeasurement.Instance.IsAvailable)
        {
            try { vmaf = await QualityMeter.MeasureAsync(decision.Info.FilePath, result.OutputPath, decision.Scenes?.Map, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { stderr.WriteLine(text.Format("error.failed", ex.Message)); }
        }

        var exit = ExitCodeFor(result);
        stdout.Write(request.Json
            ? ShrinkJson(request, decision, result, clock.Elapsed, vmaf, exit)
            : ShrinkText(decision, result, clock.Elapsed, vmaf, text));
        if (!result.Success && !result.CeilingExceeded && result.Error is { } error)
            stderr.WriteLine(text.Format("error.failed", error));
        return new FileRun(exit, result.Success ? result.OutputPath : null, result.Success ? null : result.Error);
    }

    public static string PlanText(CliRequest request, CliDecision decision, CliText text)
    {
        var info = decision.Info;
        var plan = decision.Plan;
        var estimate = decision.Result.Estimate;
        var builder = new StringBuilder();
        builder.AppendLine(text.Format("plan.input", info.FilePath));
        builder.AppendLine(text.Format("plan.source", info.Width, info.Height, Num(info.Fps, "0.##", text), Num(info.FileSizeMb, "0.0", text), Clock(TimeSpan.FromSeconds(info.DurationSeconds))));
        if (decision.QualityTarget is { } quality)
            builder.AppendLine(text.Format("plan.quality-target", Num(quality.RequestedQuality, "0.#", text), Num(decision.TargetMb, "0.#", text), quality.Bound));
        else
            builder.AppendLine(text.Format("plan.target", Num(decision.TargetMb, "0.##", text)));
        builder.AppendLine(text.Format("plan.video", plan.Codec, plan.Mode, plan.Width, plan.Height, Num(plan.Fps, "0.##", text), plan.VideoBitrateK));
        if (plan.AudioCodec is not null)
            builder.AppendLine(text.Format("plan.audio", plan.AudioCodec, plan.AudioBitrateK));
        builder.AppendLine(text.Format("plan.estimate", Num(estimate.ExpectedMb, "0.0", text), Num(estimate.LowMb, "0.0", text), Num(estimate.HighMb, "0.0", text)));
        builder.AppendLine(text.Format("plan.quality", Num(decision.Result.PredictedQuality, "0.0", text), Basis(estimate.Measured, text)));
        if (plan.ReasonCodes.Count > 0)
            builder.AppendLine(text.Format("plan.reasons", string.Join(", ", plan.ReasonCodes.Select(note => note.Code))));
        builder.AppendLine(text.Format("plan.output", decision.OutputPath));
        builder.AppendLine(text["plan.command"]);
        builder.AppendLine(FfmpegArguments.ToCommandLine(decision.Arguments));
        return builder.ToString();
    }

    public static string PlanJson(CliRequest request, CliDecision decision)
        => Json(!request.JsonLines, writer =>
        {
            writer.WriteString("command", "plan");
            WriteDecision(writer, request, decision);
        });

    private static string ShrinkText(CliDecision decision, EncodeResult result, TimeSpan elapsed, QualityScore? vmaf, CliText text)
    {
        var builder = new StringBuilder();
        builder.AppendLine(text.Format("result.output", result.OutputPath));
        builder.AppendLine(text.Format("result.size", Num(result.OutputMb, "0.0", text), Num(decision.TargetMb, "0.##", text), Num(decision.Info.FileSizeMb, "0.0", text)));
        builder.AppendLine(text.Format("result.duration", elapsed.TotalSeconds < 60 ? Num(elapsed.TotalSeconds, "0.0", text) + " s" : Clock(elapsed)));
        builder.AppendLine(text.Format("result.attempts", result.Attempts));
        builder.AppendLine(vmaf?.VmafNegMean is { } score
            ? text.Format("result.vmaf", Num(score, "0.0", text))
            : text["result.vmaf-none"]);
        builder.AppendLine(text.Format("result.predicted", Num(decision.Result.PredictedQuality, "0.0", text), Basis(decision.Result.Estimate.Measured, text)));
        if (result.Success && result.UnderBand) builder.AppendLine(text["result.under-band"]);
        if (result.CeilingExceeded)
            builder.AppendLine(result.Success
                ? text.Format("result.ceiling-kept", result.Attempts, Num(result.OutputMb, "0.000", text))
                : text.Format("result.ceiling", result.Attempts));
        return builder.ToString();
    }

    private static string ShrinkJson(CliRequest request, CliDecision decision, EncodeResult result, TimeSpan elapsed,
        QualityScore? vmaf, int exit)
        => Json(!request.JsonLines, writer =>
        {
            writer.WriteString("command", "kucult");
            WriteDecision(writer, request, decision);
            writer.WriteStartObject("result");
            writer.WriteNumber("exitCode", exit);
            writer.WriteBoolean("success", result.Success);
            writer.WriteString("output", result.OutputPath);
            writer.WriteNumber("outputMb", Math.Round(result.OutputMb, 3));
            writer.WriteNumber("elapsedSeconds", Math.Round(elapsed.TotalSeconds, 2));
            writer.WriteNumber("attempts", result.Attempts);
            writer.WriteBoolean("underBand", result.UnderBand);
            writer.WriteBoolean("ceilingExceeded", result.CeilingExceeded);
            writer.WriteBoolean("overTarget", result.OverTarget);
            if (result.Error is null) writer.WriteNull("error"); else writer.WriteString("error", result.Error);
            if (vmaf?.VmafNegMean is { } score) writer.WriteNumber("vmaf", Math.Round(score, 2)); else writer.WriteNull("vmaf");
            writer.WriteStartArray("trace");
            foreach (var attempt in result.Trace ?? Array.Empty<EncodeAttempt>())
            {
                writer.WriteStartObject();
                writer.WriteString("label", $"deneme {attempt.Number}");
                writer.WriteNumber("number", attempt.Number);
                writer.WriteString("branch", attempt.Branch);
                writer.WriteNumber("aimMb", Math.Round(attempt.AimMb, 3));
                writer.WriteNumber("actualMb", Math.Round(attempt.ActualMb, 3));
                writer.WriteNumber("videoBitrateK", attempt.VideoBitrateK);
                writer.WriteString("mode", attempt.Mode);
                writer.WriteNumber("seconds", Math.Round(attempt.Seconds, 2));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("droppedOptions");
            foreach (var dropped in result.DroppedOptions ?? Array.Empty<string>()) writer.WriteStringValue(dropped);
            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    private static void WriteDecision(Utf8JsonWriter writer, CliRequest request, CliDecision decision)
    {
        var info = decision.Info;
        var plan = decision.Plan;
        var estimate = decision.Result.Estimate;
        writer.WriteString("input", info.FilePath);
        writer.WriteString("output", decision.OutputPath);
        writer.WriteBoolean("measured", !request.SkipMeasurement);
        writer.WriteNumber("targetMb", decision.TargetMb);
        if (request.Quality is { } quality) writer.WriteNumber("requestedQuality", quality); else writer.WriteNull("requestedQuality");
        if (decision.QualityTarget is { } bound) writer.WriteString("qualityBound", bound.Bound.ToString()); else writer.WriteNull("qualityBound");
        writer.WriteStartObject("source");
        writer.WriteNumber("sizeMb", Math.Round(info.FileSizeMb, 3));
        writer.WriteNumber("durationSeconds", info.DurationSeconds);
        writer.WriteNumber("width", info.Width);
        writer.WriteNumber("height", info.Height);
        writer.WriteNumber("fps", info.Fps);
        writer.WriteString("videoCodec", info.VideoCodec);
        writer.WriteEndObject();
        writer.WriteStartObject("plan");
        writer.WriteString("codec", plan.Codec);
        writer.WriteString("mode", plan.Mode);
        writer.WriteNumber("videoBitrateK", plan.VideoBitrateK);
        if (plan.Crf is { } crf) writer.WriteNumber("crf", crf); else writer.WriteNull("crf");
        writer.WriteNumber("width", plan.Width);
        writer.WriteNumber("height", plan.Height);
        writer.WriteNumber("fps", plan.Fps);
        writer.WriteString("preset", plan.Preset);
        if (plan.AudioCodec is null) writer.WriteNull("audioCodec"); else writer.WriteString("audioCodec", plan.AudioCodec);
        writer.WriteNumber("audioBitrateK", plan.AudioBitrateK);
        writer.WriteStartArray("reasonCodes");
        foreach (var note in plan.ReasonCodes) writer.WriteStringValue(note.Code.ToString());
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteStartObject("estimate");
        writer.WriteNumber("expectedMb", Math.Round(estimate.ExpectedMb, 3));
        writer.WriteNumber("lowMb", Math.Round(estimate.LowMb, 3));
        writer.WriteNumber("highMb", Math.Round(estimate.HighMb, 3));
        writer.WriteNumber("predictedQuality", Math.Round(decision.Result.PredictedQuality, 2));
        writer.WriteString("basis", estimate.Measured ? "measured" : "estimated");
        writer.WriteEndObject();
        writer.WriteStartArray("arguments");
        foreach (var argument in decision.Arguments) writer.WriteStringValue(argument);
        writer.WriteEndArray();
        writer.WriteString("commandLine", FfmpegArguments.ToCommandLine(decision.Arguments));
    }

    private static string Json(bool indented, Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
    }

    private static string Basis(bool measured, CliText text) => text[measured ? "basis.measured" : "basis.estimated"];

    private static string Num(double value, string format, CliText text) => value.ToString(format, text.Culture);

    private static string Clock(TimeSpan span) => Saat.Ekran(span);

    private static bool PathEquals(string left, string right) => PathEquality.Same(left, right);

    private sealed class StderrProgress(TextWriter stderr, CliText text) : IProgress<EncodeProgress>
    {
        private readonly object _gate = new();
        private int _lastPercent = -1;
        private string _lastStage = "";

        public void Report(EncodeProgress value)
        {
            var percent = (int)Math.Clamp(Math.Floor(value.Fraction * 100), 0, 100);
            lock (_gate)
            {
                if (percent == _lastPercent && value.Stage == _lastStage) return;
                _lastPercent = percent;
                _lastStage = value.Stage;
                var remaining = value.Remaining is { } left ? Clock(left) : "-";
                stderr.WriteLine(text.Format("progress.encode", percent, value.Stage, remaining));
            }
        }
    }
}
