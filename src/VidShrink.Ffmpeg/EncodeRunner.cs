using System.Diagnostics;
using System.Globalization;
using System.Collections.Concurrent;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public sealed record EncodeProgress(double Fraction, TimeSpan Elapsed, TimeSpan? Remaining, double OutputMb, string Stage);

public sealed record EncodeAttempt(int Number, string Branch, double AimMb, double ActualMb, int VideoBitrateK, string Mode, double? MeasuredEfficiency = null);

public sealed record EncodeResult(bool Success, string OutputPath, double OutputMb, EncodePlan PlanUsed, int Attempts, string? Error, bool UnderBand = false, bool CeilingExceeded = false, IReadOnlyList<EncodeAttempt>? Trace = null);
public sealed record ConversionResult(string OutputPath, double OutputMb);

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
        ComplexityProfile? profile = null)
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

        try
        {
            while (attempt < MaxAttempts)
            {
                attempt++;
                var twoPass = current.ModeEnum == EncodeMode.TwoPass && FfmpegArguments.NeedsTwoPasses(current.Codec);

                if (twoPass)
                {
                    await RunOneAsync(info, current, partialPath, 1, passLogPrefix, progress, $"pass 1/2 (attempt {attempt})", 0.0, 0.5, ct);
                    await RunOneAsync(info, current, partialPath, 2, passLogPrefix, progress, $"pass 2/2 (attempt {attempt})", 0.5, 1.0, ct);
                }
                else
                {
                    await RunOneAsync(info, current, partialPath, 0, null, progress, $"encoding (attempt {attempt})", 0.0, 1.0, ct);
                }

                var actualMb = new FileInfo(partialPath).Length / 1024.0 / 1024.0;
                var efficiency = PlanCalculator.MeasuredEncoderEfficiency(current, actualMb, info.DurationSeconds);
                var aimMb = PlanCalculator.RetryAimMb(effectiveTargetMb, efficiency);
                var over = actualMb > effectiveTargetMb * ToleranceOver;
                var underBand = !over && fillPolicy == FillPolicy.FillTarget && actualMb < band.LowerMb;
                var informedByYield = efficiency is not null;
                var retryUnderBand = underBand && attempt < MaxAttempts
                    && (!usedUnderBandRetry || (informedByYield && !usedMeasuredUnderBandRetry));

                if (!over && !underBand)
                {
                    trace.Add(new EncodeAttempt(attempt, "in band", aimMb, actualMb, current.VideoBitrateK, current.Mode, efficiency));
                    File.Move(partialPath, outputPath, overwrite: true);
                    return new EncodeResult(true, outputPath, actualMb, current, attempt, null, UnderBand: false, Trace: trace);
                }

                if (retryUnderBand)
                {
                    trace.Add(new EncodeAttempt(attempt, "under band", aimMb, actualMb, current.VideoBitrateK, current.Mode, efficiency));
                    usedUnderBandRetry = true;
                    usedMeasuredUnderBandRetry |= informedByYield;
                    TryDelete(fallbackPath);
                    File.Move(partialPath, fallbackPath, overwrite: true);
                    fallbackMb = actualMb;
                    fallbackPlan = current;
                    current = PlanCalculator.Correct(current, actualMb, effectiveTargetMb, info.DurationSeconds, fillUnderBand: true);
                    continue;
                }

                if (over)
                {
                    trace.Add(new EncodeAttempt(attempt, "over ceiling", aimMb, actualMb, current.VideoBitrateK, current.Mode, efficiency));

                    if (attempt >= MaxAttempts)
                    {
                        TryDelete(partialPath);

                        if (fallbackPlan is not null && File.Exists(fallbackPath))
                        {
                            trace.Add(new EncodeAttempt(attempt, "fallback to the last under-band result", aimMb, fallbackMb, fallbackPlan.VideoBitrateK, fallbackPlan.Mode));
                            File.Move(fallbackPath, outputPath, overwrite: true);
                            return new EncodeResult(true, outputPath, fallbackMb, fallbackPlan, attempt, null, UnderBand: true, Trace: trace);
                        }

                        return new EncodeResult(false, outputPath, actualMb, current, attempt,
                            $"Stayed over the {effectiveTargetMb:0.##} MB target after {attempt} attempts (last result: {actualMb:0.0} MB); no file was written.",
                            UnderBand: false, CeilingExceeded: true, Trace: trace);
                    }

                    current = PlanCalculator.Correct(current, actualMb, effectiveTargetMb, info.DurationSeconds);
                    continue;
                }

                trace.Add(new EncodeAttempt(attempt, "under band accepted", aimMb, actualMb, current.VideoBitrateK, current.Mode, efficiency));
                File.Move(partialPath, outputPath, overwrite: true);
                return new EncodeResult(true, outputPath, actualMb, current, attempt, null, UnderBand: true, Trace: trace);
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

    private static async Task RunOneAsync(
        MediaInfo info, EncodePlan plan, string outputPath, int pass, string? passLogPrefix,
        IProgress<EncodeProgress>? progress, string stage, double spanFrom, double spanTo, CancellationToken ct)
    {
        var args = FfmpegArguments.Build(info, plan, outputPath, pass, passLogPrefix);
        await RunCommandAsync(args, info.DurationSeconds, progress, stage, spanFrom, spanTo, ct);
    }

    private static async Task RunCommandAsync(
        IReadOnlyList<string> commandArgs, double durationSeconds, IProgress<EncodeProgress>? progress,
        string stage, double spanFrom, double spanTo, CancellationToken ct)
    {
        var args = commandArgs.ToList();
        args.InsertRange(0, new[] { "-progress", "pipe:1", "-nostats" });

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        var stopwatch = Stopwatch.StartNew();
        var tail = new ConcurrentQueue<string>();

        process.Start();
        using var cancellationRegistration = ct.Register(() => TryKill(process));

        var stderrTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) is not null)
            {
                tail.Enqueue(line);
                while (tail.Count > 15) tail.TryDequeue(out _);
            }
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

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg failed ({process.ExitCode}):{Environment.NewLine}{string.Join(Environment.NewLine, tail)}");

        progress?.Report(new EncodeProgress(spanTo, stopwatch.Elapsed, TimeSpan.Zero, outMb, stage));
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
