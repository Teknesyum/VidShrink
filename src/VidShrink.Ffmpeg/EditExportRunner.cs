using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Core.Editing;

namespace VidShrink.Ffmpeg;

public static class EditExportRunner
{
    public const string Stage = "export";

    public static async Task<ExportPlan> PrepareAsync(
        string source, EditTimeline timeline, ExportMode mode, string outputPath, long memoryBudgetBytes, CancellationToken ct = default)
    {
        var info = await FfprobeClient.ProbeAsync(source, ct).ConfigureAwait(false);
        IReadOnlyList<double> keyframes = Array.Empty<double>();
        double startTime = 0;
        if (mode != ExportMode.Full)
            (keyframes, startTime) = EditExport.ParseKeyframes(await ProbeKeyframesAsync(source, ct).ConfigureAwait(false));

        var work = WorkDirectoryFor(outputPath);
        return EditExport.Build(timeline, info, keyframes, startTime, mode, EncodeRunner.PartialPathFor(outputPath), work, memoryBudgetBytes)
            with { OutputPath = outputPath };
    }

    public static async Task RunAsync(ExportPlan plan, IProgress<EncodeProgress>? progress, CancellationToken ct = default)
    {
        var partial = plan.Steps[^1].Args[^1];
        var total = Math.Max(0.001, plan.TotalWorkSeconds);
        var clock = Stopwatch.StartNew();
        Directory.CreateDirectory(plan.WorkDirectory);
        try
        {
            await WriteTextAsync(plan, ct).ConfigureAwait(false);
            double done = 0;
            foreach (var step in plan.Steps)
            {
                if (step.ListPath is { } list) await File.WriteAllTextAsync(list, step.ListContent ?? string.Empty, ct).ConfigureAwait(false);
                var from = done / total;
                done += step.DurationSeconds;
                await EncodeRunner.RunCommandAsync(step.Args, Math.Max(0.001, step.DurationSeconds), progress, Stage,
                    from, done / total, ct, () => clock.Elapsed).ConfigureAwait(false);
            }

            ct.ThrowIfCancellationRequested();
            File.Move(partial, plan.OutputPath, overwrite: true);
        }
        finally
        {
            await CleanupAsync(partial, plan.WorkDirectory).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Metinli planin <c>.ass</c> belgesini BOM'lu yazar ve yazi tipi klasorunu kurar.
    /// Klasor, aile bulunamasa da bos olarak kurulur; <c>fontsdir</c> var olan yolu gosterir.
    /// </summary>
    public static async Task WriteTextAsync(ExportPlan plan, CancellationToken ct = default)
    {
        if (plan.SubtitlePath is not { } path) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllBytesAsync(path, AssWriter.Bytes(plan.SubtitleContent ?? string.Empty), ct).ConfigureAwait(false);
        if (plan.FontsDirectory is not { } fonts) return;
        Directory.CreateDirectory(fonts);
        TextFonts.Prepare(Path.GetDirectoryName(Path.GetFullPath(fonts))!, plan.FontFamilies);
    }

    public static string WorkDirectoryFor(string outputPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Path.GetTempPath();
        return Path.Combine(dir, "vidshrink_partial_" + Guid.NewGuid().ToString("N"));
    }

    public static async Task<string> ProbeKeyframesAsync(string source, CancellationToken ct = default)
    {
        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, EditExport.KeyframeProbeArgs(source)) };
        process.Start();
        using var registration = ct.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        var text = await stdout.ConfigureAwait(false);
        var error = await stderr.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (process.ExitCode != 0) throw new InvalidOperationException($"ffprobe failed ({process.ExitCode}): {error.Trim()}");
        return text;
    }

    private static async Task CleanupAsync(string partial, string work)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                if (File.Exists(partial)) File.Delete(partial);
                if (Directory.Exists(work)) Directory.Delete(work, true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
        }
    }
}
