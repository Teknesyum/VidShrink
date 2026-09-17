using System.Diagnostics;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public sealed record IdleTrimResult(bool Ok, string? Target, double RemovedSeconds, string Error);

public static class IdleTrimmer
{
    public static async Task<IdleTrimResult> RunAsync(
        string source,
        double minIdleSeconds = IdleTrim.DefaultMinIdleSeconds,
        double keepSeconds = IdleTrim.DefaultKeepSeconds,
        CancellationToken ct = default)
    {
        MediaInfo info;
        try { info = await FfprobeClient.ProbeAsync(source, ct); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        {
            return new IdleTrimResult(false, null, 0, ex.Message);
        }

        var (exitCode, standardError) = await RunDetectAsync(IdleTrim.BuildDetect(source, minIdleSeconds), ct);
        if (exitCode != 0) return new IdleTrimResult(false, null, 0, FfmpegRunner.Tail(standardError));

        var freezes = IdleTrim.ParseFreezes(standardError, info.DurationSeconds);
        var kept = IdleTrim.KeptRanges(freezes, info.DurationSeconds, keepSeconds);
        var removed = IdleTrim.RemovedSeconds(kept, info.DurationSeconds);
        if (kept.Count == 0 || removed < 0.05) return new IdleTrimResult(true, null, 0, string.Empty);

        var target = IdleTrim.TrimTarget(source, File.Exists);
        var run = await FfmpegRunner.RunAsync(IdleTrim.BuildTrim(source, target, kept, info.HasAudio), ct);
        return run.Ok
            ? new IdleTrimResult(true, target, removed, string.Empty)
            : new IdleTrimResult(false, null, 0, run.StandardError);
    }

    private static async Task<(int ExitCode, string StandardError)> RunDetectAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        Process process;
        try
        {
            process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
            process.Start();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return (-1, ex.Message);
        }

        using (process)
        using (ct.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } }))
        {
            var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
            await process.WaitForExitAsync(CancellationToken.None);
            _ = await stdout;
            var text = await stderr;
            ct.ThrowIfCancellationRequested();
            return (process.ExitCode, text);
        }
    }
}
