using System.Diagnostics;
using System.Globalization;
using System.Collections.Concurrent;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public sealed record EncodeProgress(double Fraction, TimeSpan Elapsed, TimeSpan? Remaining, double OutputMb, string Stage);

public sealed record EncodeAttempt(int Number, string Branch, double AimMb, double ActualMb, int VideoBitrateK, string Mode, double? MeasuredEfficiency = null);

/// <summary>
/// Bir kodlama kosumunun sonucu. <see cref="DroppedOptions"/> ffmpeg'in kabul etmeyip
/// sessizce dusurdugu ayarlarin tanili satirlaridir: teslim edilen dosya motorun sectigi
/// ayarlarin tamamini tasimiyorsa cagiran bunu buradan ogrenir. Dolu olmasi
/// <see cref="Success"/> degerini dusurmez.
/// </summary>
public sealed record EncodeResult(bool Success, string OutputPath, double OutputMb, EncodePlan PlanUsed, int Attempts, string? Error, bool UnderBand = false, bool CeilingExceeded = false, IReadOnlyList<EncodeAttempt>? Trace = null, IReadOnlyList<string>? DroppedOptions = null, bool OverTarget = false, TrimPlan? Trim = null, bool Saturated = false)
{
    /// <summary>Teslim edilen kodlamada motorun verdigi ayarlardan en az biri dusurulmus.</summary>
    public bool DroppedAnOption => DroppedOptions is { Count: > 0 };
}
public sealed record ConversionResult(string OutputPath, double OutputMb);

/// <summary>
/// Sahne haritasi uretilemedigi zaman hangi yoldan dusuldugu. <see cref="None"/> disindaki her
/// deger anahtar kare tavaninin <see cref="FfmpegArguments.KeyframeCeilingDefaultSeconds"/>
/// varsayilanina dustugunu soyler; cagiran bunu sessizce gecmemek icin okur.
/// </summary>
public enum SceneMapFallback
{
    /// <summary>Harita uretildi.</summary>
    None,

    /// <summary>Kaynagin suresi bilinmiyor; sahne uzunlugu hesaplanamaz.</summary>
    NoDuration,

    /// <summary>Yoklama kosmadi ya da sifirdan farkli kodla dondu. ffmpeg yoklugu da buraya duser.</summary>
    ScanFailed,

    /// <summary>Yoklama kostu ama tek sonda karesi vermedi; kare hizi turetilecek zemin yok.</summary>
    NoProbeFrames
}

/// <summary>
/// Bir sahne haritasi uretme denemesinin sonucu. <see cref="Map"/> <c>null</c> ise
/// <see cref="Fallback"/> nedeni tasir; <see cref="Detail"/> ffmpeg'in kendi metnidir.
/// </summary>
public sealed record SceneMapAttempt(SceneMap? Map, TimeSpan Elapsed, SceneMapFallback Fallback, string Detail)
{
    public bool Ok => Map is not null;
}

/// <summary>
/// Sahne yoklamasini calistiran taraf. Varsayilani <see cref="SceneDetector.ScanAsync"/>;
/// ayri verilebilmesinin sebebi dusus yollarinin ffmpeg olmadan da olculebilmesidir.
/// </summary>
public delegate Task<SceneScan> SceneScanAsync(string path, CancellationToken ct);

/// <summary>
/// What the caller is told when an attempt lands over the target. <see cref="Trims"/> is filled only
/// when the overshoot is within <see cref="OvershootTrim.ThresholdPercent"/>.
/// </summary>
public sealed record RetryPrompt(
    int Attempt,
    int MaxAttempts,
    double TargetMb,
    double ActualMb,
    TimeSpan AttemptDuration,
    bool HasUnderBandFallback,
    double FallbackMb,
    IReadOnlyList<TrimPlan>? Trims = null)
{
    public double OverMb => ActualMb - TargetMb;
    public double OverPercent => TargetMb > 0 ? (ActualMb - TargetMb) / TargetMb * 100.0 : 0.0;
    public bool CanRetry => Attempt < MaxAttempts;
    public TrimPlan? TrimFor(TrimSide side) => Trims?.FirstOrDefault(plan => plan.Side == side);
}

public enum OvershootChoice
{
    Leave,
    Retry,
    AcceptLarger,
    TrimEnd,
    TrimStart,
    TrimBoth
}

/// <summary>
/// Leave ends the run without handing back the oversized file: the last under-band result is delivered
/// if there is one, otherwise no file is written. AcceptLarger delivers the oversized file as it is.
/// </summary>
public delegate Task<OvershootChoice> RetryDecisionAsync(RetryPrompt prompt, CancellationToken ct);

public sealed class EncodeRunner
{
    private const double ToleranceOver = 1.0;
    private const int MaxAttempts = 3;

    public async Task<EncodeResult> RunAsync(
        MediaInfo info,
        EncodePlan plan,
        string outputPath,
        double targetMb,
        IProgress<EncodeProgress>? progress,
        CancellationToken ct = default,
        FillPolicy fillPolicy = FillPolicy.QualityCeiling,
        ComplexityProfile? profile = null,
        RetryDecisionAsync? askBeforeRetry = null,
        SceneMap? scenes = null)
    {
        if (plan.ModeEnum == EncodeMode.PassThrough)
            return PassThrough(info, plan, outputPath);

        var effectiveTargetMb = Math.Min(targetMb, plan.EffectiveTargetMb ?? targetMb);
        var band = FillBand.For(effectiveTargetMb);
        var current = plan;
        var attempt = 0;
        var trace = new List<EncodeAttempt>();
        var usedUnderBandRetry = false;
        var usedMeasuredUnderBandRetry = false;
        var passLogPrefix = Path.Combine(Path.GetTempPath(), "vidshrink_" + Guid.NewGuid().ToString("N"));
        var partialPath = PartialPathFor(outputPath);
        var fallbackPath = PartialPathFor(outputPath);
        var fallbackMb = 0.0;
        EncodePlan? fallbackPlan = null;
        IReadOnlyList<string> fallbackDropped = Array.Empty<string>();
        var samples = new List<SizeSample>();
        var attemptLimit = MaxAttempts;
        var usedFloorStep = false;
        var usedDeadYieldStep = false;
        var lastSampleAttempt = 0;

        void KeepFallback(string path, double mb, EncodePlan used, IReadOnlyList<string> droppedOptions)
        {
            if (fallbackPlan is not null && fallbackMb >= mb && File.Exists(fallbackPath))
            {
                TryDelete(path);
                return;
            }
            TryDelete(fallbackPath);
            File.Move(path, fallbackPath, overwrite: true);
            fallbackMb = mb;
            fallbackPlan = used;
            fallbackDropped = droppedOptions;
        }

        try
        {
            while (attempt < attemptLimit)
            {
                attempt++;
                var twoPass = current.ModeEnum == EncodeMode.TwoPass && FfmpegArguments.NeedsTwoPasses(current.Codec);
                var attemptClock = Stopwatch.StartNew();
                var dropped = new List<string>();

                if (twoPass)
                {
                    dropped.AddRange((await RunOneAsync(info, current, partialPath, 1, passLogPrefix, progress, $"pass 1/2 (attempt {attempt})", 0.0, 0.5, scenes, ct)).DroppedOptions);
                    dropped.AddRange((await RunOneAsync(info, current, partialPath, 2, passLogPrefix, progress, $"pass 2/2 (attempt {attempt})", 0.5, 1.0, scenes, ct)).DroppedOptions);
                }
                else
                {
                    dropped.AddRange((await RunOneAsync(info, current, partialPath, 0, null, progress, $"encoding (attempt {attempt})", 0.0, 1.0, scenes, ct)).DroppedOptions);
                }

                attemptClock.Stop();
                var actualMb = new FileInfo(partialPath).Length / 1024.0 / 1024.0;
                var efficiency = PlanCalculator.MeasuredEncoderEfficiency(current, actualMb, info.DurationSeconds);
                var aimMb = PlanCalculator.RetryAimMb(effectiveTargetMb, efficiency);
                var over = actualMb > effectiveTargetMb * ToleranceOver;
                var belowBand = !over && fillPolicy == FillPolicy.FillTarget && actualMb < band.LowerMb;
                var stoppedOnPurpose = belowBand && actualMb >= band.HardFloorMb && current.StopsShortOfBandOnPurpose;
                var underBand = belowBand && !stoppedOnPurpose;
                var informedByYield = efficiency is not null;
                var sample = new SizeSample(current.VideoBitrateK, actualMb, over);
                var comparable = current.ModeEnum == EncodeMode.TwoPass;
                var previous = comparable && lastSampleAttempt == attempt - 1 && samples.Count > 0 ? samples[^1] : null;
                if (comparable)
                {
                    samples.Add(sample);
                    lastSampleAttempt = attempt;
                }
                var deadYield = underBand && Saturation.YieldIsDead(PlanCalculator.RawEncoderYield(current, actualMb, info.DurationSeconds));
                var retryUnderBand = underBand && !deadYield && !usedDeadYieldStep && attempt < attemptLimit
                    && (!usedUnderBandRetry || (informedByYield && !usedMeasuredUnderBandRetry));

                if (!over && !underBand)
                {
                    trace.Add(new EncodeAttempt(attempt, stoppedOnPurpose ? "below band, the plan stopped there on purpose" : "in band", aimMb, actualMb, current.VideoBitrateK, current.Mode, efficiency));
                    File.Move(partialPath, outputPath, overwrite: true);
                    return new EncodeResult(true, outputPath, actualMb, current, attempt, null, UnderBand: false, Trace: trace, DroppedOptions: dropped);
                }

                if (underBand && (deadYield || usedDeadYieldStep))
                {
                    var deadStep = !usedDeadYieldStep && attempt < attemptLimit
                        ? Saturation.StepDeadYield(current, actualMb, effectiveTargetMb, info.DurationSeconds, samples)
                        : null;
                    if (deadStep is not null)
                    {
                        trace.Add(new EncodeAttempt(attempt, "under band, the encoder did not answer the bitrate", aimMb, actualMb, current.VideoBitrateK, current.Mode, efficiency));
                        usedDeadYieldStep = true;
                        usedUnderBandRetry = true;
                        KeepFallback(partialPath, actualMb, current, dropped);
                        current = deadStep;
                        continue;
                    }

                    if (fallbackPlan is not null && fallbackMb > actualMb && File.Exists(fallbackPath))
                    {
                        TryDelete(partialPath);
                        trace.Add(new EncodeAttempt(attempt, "saturated, the fuller under-band result delivered", aimMb, fallbackMb, fallbackPlan.VideoBitrateK, fallbackPlan.Mode, efficiency));
                        File.Move(fallbackPath, outputPath, overwrite: true);
                        return new EncodeResult(true, outputPath, fallbackMb, fallbackPlan, attempt, null, UnderBand: true, Trace: trace, DroppedOptions: fallbackDropped, Saturated: true);
                    }

                    trace.Add(new EncodeAttempt(attempt, "saturated, under band delivered", aimMb, actualMb, current.VideoBitrateK, current.Mode, efficiency));
                    File.Move(partialPath, outputPath, overwrite: true);
                    return new EncodeResult(true, outputPath, actualMb, current, attempt, null, UnderBand: true, Trace: trace, DroppedOptions: dropped, Saturated: true);
                }

                if (retryUnderBand)
                {
                    trace.Add(new EncodeAttempt(attempt, "under band", aimMb, actualMb, current.VideoBitrateK, current.Mode, efficiency));
                    usedUnderBandRetry = true;
                    usedMeasuredUnderBandRetry |= informedByYield;
                    KeepFallback(partialPath, actualMb, current, dropped);
                    current = PlanCalculator.Correct(current, actualMb, effectiveTargetMb, info.DurationSeconds, fillUnderBand: true);
                    continue;
                }

                if (over)
                {
                    trace.Add(new EncodeAttempt(attempt, "over ceiling", aimMb, actualMb, current.VideoBitrateK, current.Mode, efficiency));

                    var floorStep = !usedFloorStep && previous is not null && Saturation.AtEncoderFloor(previous, sample)
                        ? Saturation.StepLayoutDown(current, previous, sample, effectiveTargetMb)
                        : null;
                    if (floorStep is not null)
                    {
                        usedFloorStep = true;
                        attemptLimit = MaxAttempts + Saturation.ExtraAttemptsAtFloor;
                    }

                    // Ending the run — whether the attempt ceiling was hit or the user chose to stop —
                    // never hands back the oversized file. The last under-band result is delivered if
                    // there is one; otherwise no file is written and the reason is reported.
                    EncodeResult EndRun()
                    {
                        TryDelete(partialPath);

                        if (fallbackPlan is not null && File.Exists(fallbackPath))
                        {
                            trace.Add(new EncodeAttempt(attempt, "fallback to the last under-band result", aimMb, fallbackMb, fallbackPlan.VideoBitrateK, fallbackPlan.Mode));
                            File.Move(fallbackPath, outputPath, overwrite: true);
                            return new EncodeResult(true, outputPath, fallbackMb, fallbackPlan, attempt, null, UnderBand: true, Trace: trace, DroppedOptions: fallbackDropped,
                                Saturated: usedDeadYieldStep || Saturation.FillIsSaturated(fallbackMb, effectiveTargetMb));
                        }

                        return new EncodeResult(false, outputPath, actualMb, current, attempt,
                            $"Stayed over the {effectiveTargetMb:0.##} MB target after {attempt} attempts (last result: {actualMb:0.0} MB); no file was written.",
                            UnderBand: false, CeilingExceeded: true, Trace: trace, DroppedOptions: dropped);
                    }

                    if (askBeforeRetry is null)
                    {
                        if (attempt >= attemptLimit || usedDeadYieldStep) return EndRun();
                        if (floorStep is not null) trace.Add(new EncodeAttempt(attempt, "encoder floor, the layout steps down", aimMb, actualMb, floorStep.VideoBitrateK, floorStep.Mode, efficiency));
                        current = floorStep ?? PlanCalculator.Correct(current, actualMb, effectiveTargetMb, info.DurationSeconds);
                        continue;
                    }

                    var targetBytes = (long)Math.Floor(effectiveTargetMb * 1024 * 1024);
                    IReadOnlyList<TrimPlan> trims = Array.Empty<TrimPlan>();
                    if (OvershootTrim.Offered(actualMb, effectiveTargetMb))
                    {
                        var map = await OvershootTrimmer.ReadAsync(partialPath, ct);
                        if (map is not null) trims = OvershootTrimmer.PlanAll(map, targetBytes);
                    }

                    var prompt = new RetryPrompt(
                        attempt,
                        attemptLimit,
                        effectiveTargetMb,
                        actualMb,
                        attemptClock.Elapsed,
                        fallbackPlan is not null && File.Exists(fallbackPath),
                        fallbackMb,
                        trims);

                    var choice = await askBeforeRetry(prompt, ct);
                    ct.ThrowIfCancellationRequested();

                    if (choice == OvershootChoice.AcceptLarger)
                    {
                        trace.Add(new EncodeAttempt(attempt, "larger result accepted by the user", aimMb, actualMb, current.VideoBitrateK, current.Mode));
                        File.Move(partialPath, outputPath, overwrite: true);
                        return new EncodeResult(true, outputPath, actualMb, current, attempt, null, Trace: trace, DroppedOptions: dropped, OverTarget: true);
                    }

                    if ((choice is OvershootChoice.TrimEnd or OvershootChoice.TrimStart or OvershootChoice.TrimBoth) && trims.Count > 0)
                    {
                        var side = choice switch
                        {
                            OvershootChoice.TrimStart => TrimSide.Start,
                            OvershootChoice.TrimBoth => TrimSide.Both,
                            _ => TrimSide.End
                        };
                        var trimmed = await OvershootTrimmer.TrimAsync(partialPath, outputPath, targetBytes, side, ct);
                        if (trimmed.Landed)
                        {
                            var trimmedMb = trimmed.Bytes / 1024.0 / 1024.0;
                            trace.Add(new EncodeAttempt(attempt, $"trimmed {trimmed.Plan!.RemovedSeconds:0.###} s ({side})", aimMb, trimmedMb, current.VideoBitrateK, current.Mode));
                            TryDelete(partialPath);
                            return new EncodeResult(true, outputPath, trimmedMb, current, attempt, null, Trace: trace, DroppedOptions: dropped, Trim: trimmed.Plan);
                        }
                        trace.Add(new EncodeAttempt(attempt, "trim did not land under the target", aimMb, actualMb, current.VideoBitrateK, current.Mode));
                        return EndRun();
                    }

                    if (choice != OvershootChoice.Retry || attempt >= attemptLimit)
                    {
                        trace.Add(new EncodeAttempt(attempt, "stopped at the user's request", aimMb, actualMb, current.VideoBitrateK, current.Mode));
                        return EndRun();
                    }

                    if (floorStep is not null) trace.Add(new EncodeAttempt(attempt, "encoder floor, the layout steps down", aimMb, actualMb, floorStep.VideoBitrateK, floorStep.Mode, efficiency));
                    current = floorStep ?? PlanCalculator.Correct(current, actualMb, effectiveTargetMb, info.DurationSeconds);
                    continue;
                }

                trace.Add(new EncodeAttempt(attempt, "under band accepted", aimMb, actualMb, current.VideoBitrateK, current.Mode, efficiency));
                File.Move(partialPath, outputPath, overwrite: true);
                return new EncodeResult(true, outputPath, actualMb, current, attempt, null, UnderBand: true, Trace: trace, DroppedOptions: dropped,
                    Saturated: Saturation.FillIsSaturated(actualMb, effectiveTargetMb));
            }

            return new EncodeResult(false, outputPath, 0, current, attempt, "Encoding loop ended unexpectedly.", Trace: trace);
        }
        catch (OperationCanceledException)
        {
            TryDelete(partialPath);
            throw;
        }
        catch
        {
            TryDelete(partialPath);
            throw;
        }
        finally
        {
            TryDelete(fallbackPath);
            CleanupPassLogs(passLogPrefix);
        }
    }

    private static EncodeResult PassThrough(MediaInfo info, EncodePlan plan, string outputPath)
    {
        var sourceExtension = Path.GetExtension(info.FilePath);
        var deliveredPath = string.IsNullOrEmpty(sourceExtension) || sourceExtension.Equals(Path.GetExtension(outputPath), StringComparison.OrdinalIgnoreCase)
            ? outputPath
            : Path.ChangeExtension(outputPath, sourceExtension);

        if (!string.Equals(Path.GetFullPath(info.FilePath), Path.GetFullPath(deliveredPath), StringComparison.OrdinalIgnoreCase))
            File.Copy(info.FilePath, deliveredPath, overwrite: true);

        var mb = new FileInfo(deliveredPath).Length / 1024.0 / 1024.0;
        var trace = new List<EncodeAttempt> { new(1, "pass-through", mb, mb, plan.VideoBitrateK, plan.Mode) };
        return new EncodeResult(true, deliveredPath, mb, plan, 1, null, Trace: trace);
    }

    public async Task<ConversionResult> ConvertAsync(
        MediaInfo info, ConversionPlan plan, string outputPath,
        IProgress<EncodeProgress>? progress, CancellationToken ct = default)
    {
        var partialPath = PartialPathFor(outputPath);
        try
        {
            var errors = ConversionArguments.Validate(info, plan);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            var duration = EffectiveDuration(info, plan);
            if (plan.Gif)
            {
                var palettePath = Path.Combine(Path.GetTempPath(), $"vidshrink_{Guid.NewGuid():N}.png");
                try
                {
                    await RunCommandAsync(ConversionArguments.Build(info, plan, palettePath, availability: EncoderCapabilities.Instance), duration, progress, "GIF palette 1/2", 0, 0.35, ct);
                    await RunCommandAsync(ConversionArguments.Build(info, plan, partialPath, palettePath, EncoderCapabilities.Instance), duration, progress, "GIF encode 2/2", 0.35, 1, ct);
                }
                finally { TryDelete(palettePath); }
            }
            else
                await RunCommandAsync(ConversionArguments.Build(info, plan, partialPath, availability: EncoderCapabilities.Instance), duration, progress, "converting", 0, 1, ct);

            File.Move(partialPath, outputPath, overwrite: true);
            return new ConversionResult(outputPath, new FileInfo(outputPath).Length / 1024.0 / 1024.0);
        }
        catch (OperationCanceledException) { TryDelete(partialPath); throw; }
        catch { TryDelete(partialPath); throw; }
    }

    /// <summary>
    /// Kaynagin sahne haritasini uretir. Basarisiz olursa <b>atmaz</b>: haritasiz bir deneme
    /// dondurur ve nedenini soyler. Haritasiz kodlama gecerli bir kodlamadir — anahtar kare
    /// tavani <see cref="FfmpegArguments.KeyframeCeilingDefaultSeconds"/> varsayilanina duser.
    /// </summary>
    public static async Task<SceneMapAttempt> TryBuildSceneMapAsync(
        MediaInfo info, SceneScanAsync? scan = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(info);
        var clock = Stopwatch.StartNew();

        if (!double.IsFinite(info.DurationSeconds) || info.DurationSeconds <= 0)
        {
            clock.Stop();
            return new SceneMapAttempt(null, clock.Elapsed, SceneMapFallback.NoDuration,
                $"sure={info.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)}");
        }

        var runner = scan ?? ((path, token) => SceneDetector.ScanAsync(path, ct: token));
        SceneScan result;
        try
        {
            result = await runner(info.FilePath, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            clock.Stop();
            return new SceneMapAttempt(null, clock.Elapsed, SceneMapFallback.ScanFailed, ex.Message);
        }

        if (!result.Ok)
        {
            clock.Stop();
            return new SceneMapAttempt(null, clock.Elapsed, SceneMapFallback.ScanFailed, result.Error);
        }

        if (result.Frames.Count == 0)
        {
            clock.Stop();
            return new SceneMapAttempt(null, clock.Elapsed, SceneMapFallback.NoProbeFrames, "sonda karesi yok");
        }

        var map = SceneMap.BuildDerived(info.DurationSeconds, result.Candidates, result.Frames, ThresholdRule.Measured);
        clock.Stop();
        return new SceneMapAttempt(map, clock.Elapsed, SceneMapFallback.None, string.Empty);
    }

    /// <summary>
    /// Kodlama komutunu üretir. Önce psy/AQ yoklamasını ısıtır: <c>FfmpegArguments.Build</c> saf
    /// kaldığı için ısıtılmamış bir seçeneği desteklenmiyor sayar, ve ısıtmayan bir çağıran
    /// bayrakları sessizce kaybederdi. Kodlayan her yol buradan geçer; ölçüm aracı da dahil.
    /// </summary>
    public static IReadOnlyList<string> EncodeArguments(
        MediaInfo info, EncodePlan plan, string outputPath, int pass, string? passLogPrefix,
        IEncoderAvailability availability, SceneMap? scenes = null)
    {
        FfmpegArguments.WarmPsychovisual(plan.Codec, availability);
        return FfmpegArguments.Build(info, plan, outputPath, pass, passLogPrefix, availability, scenes);
    }

    private static async Task<EncodeCommandOutcome> RunOneAsync(
        MediaInfo info, EncodePlan plan, string outputPath, int pass, string? passLogPrefix,
        IProgress<EncodeProgress>? progress, string stage, double spanFrom, double spanTo,
        SceneMap? scenes, CancellationToken ct)
    {
        var args = EncodeArguments(info, plan, outputPath, pass, passLogPrefix, EncoderCapabilities.Instance, scenes);
        return await RunCommandAsync(args, info.DurationSeconds, progress, stage, spanFrom, spanTo, ct);
    }

    internal static async Task<EncodeCommandOutcome> RunCommandAsync(
        IReadOnlyList<string> commandArgs, double durationSeconds, IProgress<EncodeProgress>? progress,
        string stage, double spanFrom, double spanTo, CancellationToken ct)
    {
        var args = commandArgs.ToList();
        args.InsertRange(0, new[] { "-progress", "pipe:1", "-nostats" });

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        var stopwatch = Stopwatch.StartNew();
        var watch = new StderrWatch();

        process.Start();
        using var cancellationRegistration = ct.Register(() => TryKill(process));

        var stderrTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) is not null)
                watch.Line(line);
        }, CancellationToken.None);

        double outMb = 0;
        string? readLine;
        while ((readLine = await process.StandardOutput.ReadLineAsync(ct)) is not null)
        {
            var sep = readLine.IndexOf('=');
            if (sep <= 0) continue;
            var key = readLine[..sep];
            var value = readLine[(sep + 1)..];

            if (key == "total_size" && long.TryParse(value, out var size))
                outMb = size / 1024.0 / 1024.0;

            if (key != "out_time_ms" || !long.TryParse(value, out var us)) continue;

            var local = Math.Clamp(us / 1_000_000.0 / durationSeconds, 0, 1);
            var overall = spanFrom + local * (spanTo - spanFrom);
            var remaining = overall > 0.01
                ? TimeSpan.FromSeconds(stopwatch.Elapsed.TotalSeconds / overall - stopwatch.Elapsed.TotalSeconds)
                : (TimeSpan?)null;
            progress?.Report(new EncodeProgress(overall, stopwatch.Elapsed, remaining, outMb, stage));
        }

        await process.WaitForExitAsync(ct);

        await stderrTask;

        var outcome = watch.Close(process.ExitCode);
        ThrowIfFailed(outcome);

        progress?.Report(new EncodeProgress(spanTo, stopwatch.Elapsed, TimeSpan.Zero, outMb, stage));
        return outcome;
    }

    /// <summary>
    /// Bitmis bir ffmpeg kosumunun teslim yolundaki karari. <see cref="DroppedOptions"/> dolu
    /// olmasi kosumun basarisiz oldugu anlamina gelmez; cikis kodu 0 iken de dolabilir.
    /// </summary>
    internal sealed record EncodeCommandOutcome(
        int ExitCode,
        IReadOnlyList<string> Tail,
        IReadOnlyList<string> DroppedOptions);

    /// <summary>
    /// stderr akisini satir satir izler. Tanili satir akisin herhangi bir yerinde
    /// gecebildigi icin <see cref="Tail"/> penceresinden bagimsiz toplanir: olculen
    /// ornekte satir 35'in 12.'sindeydi ve 15 satirlik kuyruga hic girmiyordu.
    /// </summary>
    internal sealed class StderrWatch
    {
        private const int TailLines = 15;

        private readonly ConcurrentQueue<string> tail = new();
        private readonly List<string> dropped = new();

        public void Line(string line)
        {
            tail.Enqueue(line);
            while (tail.Count > TailLines) tail.TryDequeue(out _);
            if (FfmpegDiagnostics.ReportsADroppedOption(line)) dropped.Add(line);
        }

        public EncodeCommandOutcome Close(int exitCode)
            => new(exitCode, tail.ToArray(), dropped.ToArray());
    }

    /// <summary>
    /// Teslim yolunun basarisizlik kapisi. Surec baslatmaktan ayri durur: olcu, verilen
    /// cikis kodunun ve stderr metninin uretecegi karari surec kosturmadan pimler.
    /// </summary>
    internal static void ThrowIfFailed(EncodeCommandOutcome outcome)
    {
        if (outcome.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg failed ({outcome.ExitCode}):{Environment.NewLine}{string.Join(Environment.NewLine, outcome.Tail)}");
    }

    private static double EffectiveDuration(MediaInfo info, ConversionPlan plan)
    {
        var start = plan.Start?.TotalSeconds ?? 0;
        var end = plan.End?.TotalSeconds ?? info.DurationSeconds;
        return Math.Max(0.1, Math.Min(end, info.DurationSeconds) - start);
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    public static string PartialPathFor(string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        var name = "vidshrink_partial_" + Guid.NewGuid().ToString("N") + Path.GetExtension(outputPath);
        return string.IsNullOrEmpty(dir) ? name : Path.Combine(dir, name);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void CleanupPassLogs(string prefix)
    {
        try
        {
            var dir = Path.GetDirectoryName(prefix)!;
            var name = Path.GetFileName(prefix);
            foreach (var file in Directory.EnumerateFiles(dir, name + "*"))
                TryDelete(file);
        }
        catch { }
    }
}
