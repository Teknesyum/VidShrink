namespace VidShrink.Core.Editing;

public enum EditBatchFailure
{
    Unreadable,
    Empty,
    Stale,
    SameOutput,
    ReverseLimit
}

/// <summary>Toplu teslimde bir projenin teslimden önce düşme nedeni; <see cref="Detail"/> iletiye giren değerdir.</summary>
public sealed class EditBatchException : Exception
{
    public EditBatchException(EditBatchFailure reason, string detail, double limitSeconds = 0) : base(detail)
    {
        Reason = reason;
        Detail = detail;
        LimitSeconds = limitSeconds;
    }

    public EditBatchFailure Reason { get; }
    public string Detail { get; }
    public double LimitSeconds { get; }
}

public sealed class EditBatchItem
{
    public EditBatchItem(string path) => Path = path;

    public string Path { get; }
    public ConversionItemState State { get; internal set; } = ConversionItemState.Waiting;
    public string? Output { get; internal set; }
    public Exception? Error { get; internal set; }
}

/// <summary>
/// DZA-09: düzenleyicinin toplu teslim sırası. Proje dosyaları sırayla, birer birer teslim edilir; düşen
/// proje sırayı durdurmaz, iptal kalan projeleri iptal sayar. Her proje komut satırı teslimiyle aynı yoldan
/// geçer (<see cref="ExportOneAsync"/>): oku, <see cref="EditProject.ToJob"/>, çıktı adı, hazırla, koş.
/// </summary>
public sealed class EditBatch
{
    private readonly List<EditBatchItem> _items = new();

    public EditBatch(IEnumerable<string> paths) => Add(paths);

    public IReadOnlyList<EditBatchItem> Items => _items;

    public IReadOnlyList<EditBatchItem> Add(IEnumerable<string> paths)
    {
        var added = new List<EditBatchItem>();
        foreach (var path in paths)
        {
            if (_items.Any(item => PathEquality.Same(item.Path, path))) continue;
            var item = new EditBatchItem(path);
            _items.Add(item);
            added.Add(item);
        }
        return added;
    }

    public async Task<ConversionQueueSummary> RunAsync(
        Func<EditBatchItem, CancellationToken, Task<string>> export,
        Action<EditBatchItem>? changed,
        CancellationToken ct)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.State != ConversionItemState.Waiting) continue;
            if (ct.IsCancellationRequested)
            {
                Mark(item, ConversionItemState.Cancelled, changed);
                continue;
            }

            Mark(item, ConversionItemState.Running, changed);
            try
            {
                item.Output = await export(item, ct);
                Mark(item, ConversionItemState.Done, changed);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Mark(item, ConversionItemState.Cancelled, changed);
            }
            catch (Exception ex)
            {
                item.Error = ex;
                Mark(item, ConversionItemState.Failed, changed);
            }
        }

        return Summary();
    }

    public ConversionQueueSummary Summary() => new(
        _items.Count(item => item.State == ConversionItemState.Done),
        _items.Count(item => item.State == ConversionItemState.Failed),
        _items.Count(item => item.State == ConversionItemState.Cancelled));

    /// <summary>
    /// Bir proje dosyasını teslim eder ve çıktı yolunu döndürür. Teslimden önceki her düşüş
    /// <see cref="EditBatchException"/>'dır; hazırlık ve koşu kendi istisnalarıyla düşer.
    /// </summary>
    public static async Task<string> ExportOneAsync(string projectPath, Func<string, SourceStamp?> stampOf,
        Func<EditProjectJob, string, CancellationToken, Task<ExportPlan>> prepare,
        Func<ExportPlan, CancellationToken, Task> run, CancellationToken ct)
    {
        if (EditProject.Read(projectPath) is not { } project)
            throw new EditBatchException(EditBatchFailure.Unreadable, projectPath);
        if (project.ToJob(stampOf, out var stale) is not { } job)
            throw stale is null
                ? new EditBatchException(EditBatchFailure.Empty, projectPath)
                : new EditBatchException(EditBatchFailure.Stale, stale);

        var output = EditOutputName.For(job.Sources[0]);
        if (job.Sources.Any(source => PathEquality.Same(source, output)) || PathEquality.Same(projectPath, output))
            throw new EditBatchException(EditBatchFailure.SameOutput, output);

        var plan = await prepare(job, output, ct).ConfigureAwait(false);
        if (plan.ReverseOverLimit.Count > 0)
            throw new EditBatchException(EditBatchFailure.ReverseLimit,
                string.Join(", ", plan.ReverseOverLimit.Select(i => (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))),
                plan.ReverseLimitSeconds);

        await run(plan, ct).ConfigureAwait(false);
        return plan.OutputPath;
    }

    private static void Mark(EditBatchItem item, ConversionItemState state, Action<EditBatchItem>? changed)
    {
        item.State = state;
        changed?.Invoke(item);
    }
}
