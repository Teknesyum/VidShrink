using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Core.Editing;

namespace VidShrink.Ffmpeg;

/// <summary>
/// Parcalari ayri yazan teslimde dusen parca. <see cref="Index"/> cizelgedeki sifir tabanli siradir.
/// </summary>
public sealed class EditSegmentException(int index, int count, Exception inner)
    : InvalidOperationException(inner.Message, inner)
{
    public int Index { get; } = index;

    public int Count { get; } = count;
}

public static class EditExportRunner
{
    public const string Stage = "export";

    /// <summary>
    /// <paramref name="sources"/> kaynak sirasiyla yollardir; ilki projenin ilk kaynagidir. Yalniz
    /// cizelgede parcasi olan kaynaklar ve birlestirmenin hedefini veren ilk kaynak okunur.
    /// </summary>
    public static async Task<ExportPlan> PrepareAsync(
        IReadOnlyList<string> sources, EditTimeline timeline, ExportMode mode, string outputPath, long memoryBudgetBytes,
        bool dropMetadata = false, CancellationToken ct = default)
    {
        var probed = await ProbeSourcesAsync(sources, timeline, mode, false, ct).ConfigureAwait(false);
        var work = WorkDirectoryFor(outputPath);
        return EditExport.Build(timeline, probed, mode, EncodeRunner.PartialPathFor(outputPath), work, memoryBudgetBytes,
                EncoderCapabilities.Instance.HasEncoder, dropMetadata)
            with { OutputPath = outputPath };
    }

    /// <summary>
    /// Her parcayi ayri dosyaya yazan planlar. Adlar <see cref="EditOutputName.Segments"/> ile
    /// <paramref name="outputPath"/>'ten turer; her parcanin kendi yarim dosyasi ve is klasoru olur.
    /// </summary>
    public static async Task<IReadOnlyList<ExportPlan>> PrepareSegmentsAsync(
        IReadOnlyList<string> sources, EditTimeline timeline, ExportMode mode, string outputPath, long memoryBudgetBytes,
        Func<string, bool>? exists = null, bool dropMetadata = false, CancellationToken ct = default)
    {
        var probed = await ProbeSourcesAsync(sources, timeline, mode, true, ct).ConfigureAwait(false);
        var outputs = EditOutputName.Segments(outputPath, timeline.Clips.Count, exists);
        var partials = outputs.Select(EncodeRunner.PartialPathFor).ToArray();
        var works = outputs.Select(WorkDirectoryFor).ToArray();
        return EditExport.BuildSegments(timeline, probed, mode, partials, works, memoryBudgetBytes,
                EncoderCapabilities.Instance.HasEncoder, dropMetadata)
            .Select((plan, i) => plan with { OutputPath = outputs[i] })
            .ToArray();
    }

    /// <summary>
    /// Planlari sirayla, tek surecle kosar; ilerleme toplam is suresi uzerinden tek cubuga iner.
    /// Bir parca duserse kalanlar yazilmaz ve <see cref="EditSegmentException"/> dusen parcayi soyler;
    /// o ana dek tamamlanan dosyalar yerinde kalir, yarim dosya kalmaz. Iptal oldugu gibi yukari cikar.
    /// </summary>
    public static async Task RunSegmentsAsync(IReadOnlyList<ExportPlan> plans, IProgress<EncodeProgress>? progress, CancellationToken ct = default)
    {
        var total = Math.Max(0.001, plans.Sum(p => Math.Max(0.001, p.TotalWorkSeconds)));
        var clock = Stopwatch.StartNew();
        double done = 0;
        for (var i = 0; i < plans.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var share = Math.Max(0.001, plans[i].TotalWorkSeconds) / total;
            var scaled = progress is null ? null : new SegmentProgress(progress, done, share, clock);
            try
            {
                await RunAsync(plans[i], scaled, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                throw new EditSegmentException(i, plans.Count, ex);
            }

            done += share;
        }
    }

    /// <summary>
    /// Anahtar kareler yalniz kopyalama yolunun kosabilecegi yerde okunur: tek kaynakli cizelgede ve
    /// parcalari ayri yazan teslimde. Birlestirme hep yeniden kodladigi icin yalniz akis bilgisi okunur.
    /// </summary>
    private static async Task<IReadOnlyDictionary<int, ExportSource>> ProbeSourcesAsync(
        IReadOnlyList<string> sources, EditTimeline timeline, ExportMode mode, bool segments, CancellationToken ct)
    {
        var used = EditExport.UsedSources(timeline);
        var deep = segments || used.Count == 1 ? mode : ExportMode.Full;
        var probed = new Dictionary<int, ExportSource>();
        foreach (var index in used.Count > 1 ? used.Prepend(0).Distinct() : used)
        {
            if (index >= sources.Count) throw new InvalidOperationException("Parcanin kaynagi yol listesinde yok");
            var (info, keyframes, startTime, cuts) = await ProbeAsync(sources[index], used.Contains(index) ? deep : ExportMode.Full, ct).ConfigureAwait(false);
            probed[index] = new ExportSource(info, keyframes, startTime, cuts);
        }

        return probed;
    }

    private static async Task<(MediaInfo Info, IReadOnlyList<double> Keyframes, double StartTime, IReadOnlyList<SmartCutPoint> Cuts)> ProbeAsync(
        string source, ExportMode mode, CancellationToken ct)
    {
        var info = await FfprobeClient.ProbeAsync(source, ct).ConfigureAwait(false);
        IReadOnlyList<double> keyframes = Array.Empty<double>();
        IReadOnlyList<SmartCutPoint> cuts = Array.Empty<SmartCutPoint>();
        double startTime = 0;
        if (mode == ExportMode.Full) return (info, keyframes, startTime, cuts);

        var csv = await ProbeKeyframesAsync(source, ct).ConfigureAwait(false);
        (keyframes, startTime) = EditExport.ParseKeyframes(csv);
        if (mode == ExportMode.Smart) cuts = SmartCutCodec.CleanCuts(SmartCutCodec.ParsePackets(csv), startTime);
        return (info, keyframes, startTime, cuts);
    }

    private sealed class SegmentProgress(IProgress<EncodeProgress> inner, double from, double share, Stopwatch clock) : IProgress<EncodeProgress>
    {
        public void Report(EncodeProgress value)
        {
            var fraction = Math.Clamp(from + Math.Clamp(value.Fraction, 0, 1) * share, 0, 1);
            var elapsed = clock.Elapsed;
            TimeSpan? remaining = fraction is > 0 and < 1 ? TimeSpan.FromSeconds(elapsed.TotalSeconds * (1 - fraction) / fraction) : null;
            inner.Report(value with { Fraction = fraction, Elapsed = elapsed, Remaining = fraction >= 1 ? TimeSpan.Zero : remaining });
        }
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
                if (step.AudioListPath is { } audio) await File.WriteAllTextAsync(audio, step.AudioListContent ?? string.Empty, ct).ConfigureAwait(false);
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
