using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace VidShrink.Core;

public interface IWatchFileSystem
{
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    IEnumerable<string> EnumerateFiles(string directory);
    WatchFileStamp? Stat(string path);
    bool IsLocked(string path);
    bool FileExists(string path);
    string ReadAllText(string path);
    void WriteAllTextAtomic(string path, string content);
    void Move(string source, string destination);
}

public interface IWatchClock
{
    DateTime UtcNow { get; }
    Task Delay(TimeSpan delay, CancellationToken ct);
}

public readonly record struct WatchFileStamp(long Length, DateTime LastWriteUtc);

public sealed record WatchEntry
{
    public string Name { get; init; } = "";
    public long Length { get; init; }
    public bool Failed { get; init; }
    public int ExitCode { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
    public DateTime ProcessedUtc { get; init; }
}

public sealed record WatchState
{
    public int Version { get; init; } = 1;
    public List<WatchEntry> Processed { get; init; } = new();
}

public sealed record WatchStateLoad(WatchState State, string? CorruptBackup);

public sealed record WatchOutcome(int ExitCode, bool Success, string? Output, string? Error);

public enum WatchEventKind { Waiting, Processing, Done, Failed, Stopped }

public sealed record WatchEvent(WatchEventKind Kind, string Path, string? Detail = null);

public sealed record WatchOptions
{
    public required string WatchDirectory { get; init; }
    public required string OutputDirectory { get; init; }
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan StableFor { get; init; } = TimeSpan.FromSeconds(2);
    public bool Once { get; init; }
}

public enum WatchRunResult { Finished, Cancelled }

public sealed class WatchFolder
{
    public const string StateFileName = ".vidshrink-izle.json";
    public const string OutputSuffix = "_shrunk";

    private static readonly IReadOnlySet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".webm", ".avi", ".m4v", ".wmv", ".flv", ".mts", ".m2ts", ".ts", ".mpg", ".mpeg", ".3gp"
    };

    private static readonly Regex OwnOutputName = new(@"_shrunk(_\d+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly IWatchFileSystem _fs;
    private readonly IWatchClock _clock;
    private readonly Dictionary<string, Observation> _pending = new(StringComparer.OrdinalIgnoreCase);

    public WatchFolder(WatchOptions options, IWatchFileSystem fs, IWatchClock clock, WatchState? state = null)
    {
        Options = options;
        _fs = fs;
        _clock = clock;
        State = state ?? new WatchState();
    }

    public WatchOptions Options { get; }
    public WatchState State { get; private set; }
    public string StatePath => Path.Combine(Options.WatchDirectory, StateFileName);
    public int PendingCount => _pending.Count;

    public static string? ValidateFolders(string watchDirectory, string outputDirectory)
    {
        var watch = Normalize(watchDirectory);
        var output = Normalize(outputDirectory);
        if (string.Equals(watch, output, StringComparison.OrdinalIgnoreCase)) return "error.watch-same-output";
        return null;
    }

    public static bool IsOwnOutput(string path)
        => OwnOutputName.IsMatch(Path.GetFileNameWithoutExtension(path));

    public static bool IsCandidate(string path)
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith('.')) return false;
        if (!VideoExtensions.Contains(Path.GetExtension(name))) return false;
        return !IsOwnOutput(path);
    }

    public static WatchStateLoad LoadState(IWatchFileSystem fs, string statePath, DateTime nowUtc)
    {
        if (!fs.FileExists(statePath)) return new WatchStateLoad(new WatchState(), null);
        try
        {
            var state = JsonSerializer.Deserialize<WatchState>(fs.ReadAllText(statePath), JsonOptions);
            if (state?.Processed is null || state.Processed.Any(e => e is null || string.IsNullOrWhiteSpace(e.Name)))
                throw new JsonException("invalid state");
            return new WatchStateLoad(state, null);
        }
        catch (JsonException)
        {
            var backup = $"{statePath}.bozuk-{nowUtc:yyyyMMdd-HHmmss}";
            fs.Move(statePath, backup);
            return new WatchStateLoad(new WatchState(), backup);
        }
    }

    public static string Serialize(WatchState state) => JsonSerializer.Serialize(state, JsonOptions);

    public bool IsProcessed(string path, long length)
        => State.Processed.Any(e => string.Equals(e.Name, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase) && e.Length == length);

    public IReadOnlyList<string> Poll(Action<WatchEvent>? log = null)
    {
        var now = _clock.UtcNow;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ready = new List<string>();
        foreach (var path in _fs.EnumerateFiles(Options.WatchDirectory).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (!IsCandidate(path)) continue;
            if (_fs.Stat(path) is not { } stamp) continue;
            if (IsProcessed(path, stamp.Length)) continue;
            seen.Add(path);

            if (!_pending.TryGetValue(path, out var previous) || previous.Stamp != stamp)
            {
                if (previous is null) log?.Invoke(new WatchEvent(WatchEventKind.Waiting, path));
                _pending[path] = new Observation(stamp, now);
                continue;
            }

            if (stamp.Length <= 0 || now - previous.Since < Options.StableFor) continue;
            if (_fs.IsLocked(path)) continue;

            _pending.Remove(path);
            ready.Add(path);
        }

        foreach (var gone in _pending.Keys.Where(k => !seen.Contains(k)).ToList()) _pending.Remove(gone);
        return ready;
    }

    public void Record(string path, WatchOutcome outcome)
    {
        var length = _fs.Stat(path)?.Length ?? 0;
        var name = Path.GetFileName(path);
        var entries = State.Processed
            .Where(e => !string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
            .Append(new WatchEntry
            {
                Name = name,
                Length = length,
                Failed = !outcome.Success,
                ExitCode = outcome.ExitCode,
                Output = outcome.Output is null ? null : Path.GetFileName(outcome.Output),
                Error = outcome.Error,
                ProcessedUtc = _clock.UtcNow
            })
            .ToList();
        State = State with { Processed = entries };
        _fs.WriteAllTextAtomic(StatePath, Serialize(State));
    }

    public async Task<WatchRunResult> RunAsync(Func<string, CancellationToken, Task<WatchOutcome>> process,
        Action<WatchEvent>? log, CancellationToken ct)
    {
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                foreach (var path in Poll(log))
                {
                    ct.ThrowIfCancellationRequested();
                    log?.Invoke(new WatchEvent(WatchEventKind.Processing, path));
                    WatchOutcome outcome;
                    try
                    {
                        outcome = await process(path, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        outcome = new WatchOutcome(1, false, null, ex.Message);
                    }
                    if (!outcome.Success) ct.ThrowIfCancellationRequested();
                    Record(path, outcome);
                    log?.Invoke(new WatchEvent(outcome.Success ? WatchEventKind.Done : WatchEventKind.Failed, path,
                        outcome.Success ? outcome.Output : outcome.Error));
                }

                if (Options.Once && _pending.Count == 0) return WatchRunResult.Finished;
                await _clock.Delay(Options.PollInterval, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            log?.Invoke(new WatchEvent(WatchEventKind.Stopped, Options.WatchDirectory));
            return WatchRunResult.Cancelled;
        }
    }

    private static string Normalize(string path)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private sealed record Observation(WatchFileStamp Stamp, DateTime Since);
}

public sealed class PhysicalWatchFileSystem : IWatchFileSystem
{
    public static PhysicalWatchFileSystem Instance { get; } = new();

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public IEnumerable<string> EnumerateFiles(string directory)
    {
        try { return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).ToList(); }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    public WatchFileStamp? Stat(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? new WatchFileStamp(info.Length, info.LastWriteTimeUtc) : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public bool IsLocked(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return false;
        }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }

    public bool FileExists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllTextAtomic(string path, string content)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, true);
    }

    public void Move(string source, string destination) => File.Move(source, destination, true);
}

public sealed class SystemWatchClock : IWatchClock
{
    public static SystemWatchClock Instance { get; } = new();

    public DateTime UtcNow => DateTime.UtcNow;

    public Task Delay(TimeSpan delay, CancellationToken ct) => Task.Delay(delay, ct);
}
