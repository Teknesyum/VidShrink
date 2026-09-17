using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
    void Delete(string path);
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
    public bool Retried { get; init; }
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

public sealed record WatchStateLocation(string Path, bool Writable, WatchStateLoad Load);

public sealed record WatchOutcome(int ExitCode, bool Success, string? Output, string? Error);

public enum WatchEventKind { Waiting, Processing, Done, Failed, Skipped, Changed, StateNotSaved, Stopped }

public sealed record WatchEvent(WatchEventKind Kind, string Path, string? Detail = null);

public sealed record WatchOptions
{
    public required string WatchDirectory { get; init; }
    public required string OutputDirectory { get; init; }
    public string? StatePath { get; init; }
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan StableFor { get; init; } = TimeSpan.FromSeconds(2);
    public bool Once { get; init; }
}

public enum WatchRunResult { Finished, Cancelled }

public sealed class WatchFolder
{
    public const string StateFileName = ".vidshrink-izle.json";
    public const int StableConfirmations = 2;

    private static readonly IReadOnlySet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".webm", ".avi", ".m4v", ".wmv", ".flv", ".mts", ".m2ts", ".ts", ".mpg", ".mpeg", ".3gp"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWatchFileSystem _fs;
    private readonly IWatchClock _clock;
    private readonly Dictionary<string, Observation> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _retryable = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _skipLogged = new(StringComparer.OrdinalIgnoreCase);

    public WatchFolder(WatchOptions options, IWatchFileSystem fs, IWatchClock clock, WatchState? state = null)
    {
        Options = options;
        _fs = fs;
        _clock = clock;
        State = state ?? new WatchState();
        foreach (var entry in State.Processed.Where(e => e.Failed && !e.Retried)) _retryable.Add(entry.Name);
    }

    public WatchOptions Options { get; }
    public WatchState State { get; private set; }
    public string StatePath => Options.StatePath ?? Path.Combine(Options.WatchDirectory, StateFileName);
    public int PendingCount => _pending.Count;
    public int FailedCount { get; private set; }

    public static StringComparison PathComparison
        => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string? ValidateFolders(string watchDirectory, string outputDirectory, StringComparison? comparison = null)
    {
        var watch = Normalize(watchDirectory);
        var output = Normalize(outputDirectory);
        if (string.Equals(watch, output, comparison ?? PathComparison)) return "error.watch-same-output";
        return null;
    }

    public static bool IsCandidate(string path)
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith('.')) return false;
        return VideoExtensions.Contains(Path.GetExtension(name));
    }

    public static string StateKey(string watchDirectory)
    {
        var normalized = Normalize(watchDirectory);
        if (PathComparison == StringComparison.OrdinalIgnoreCase) normalized = normalized.ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16].ToLowerInvariant();
    }

    public static IReadOnlyList<string> StateCandidates(string watchDirectory, string outputDirectory, string? fallbackDirectory)
    {
        var key = StateKey(watchDirectory);
        var list = new List<string>
        {
            Path.Combine(watchDirectory, StateFileName),
            Path.Combine(outputDirectory, $".vidshrink-izle-{key}.json")
        };
        if (!string.IsNullOrWhiteSpace(fallbackDirectory)) list.Add(Path.Combine(fallbackDirectory, $"izle-{key}.json"));
        return list;
    }

    public static WatchStateLocation OpenState(IWatchFileSystem fs, string watchDirectory, string outputDirectory,
        string? fallbackDirectory, DateTime nowUtc)
    {
        var candidates = StateCandidates(watchDirectory, outputDirectory, fallbackDirectory);
        var writable = candidates.FirstOrDefault(c => CanWrite(fs, c));
        var path = writable ?? candidates[0];
        var source = fs.FileExists(path) ? path : candidates.FirstOrDefault(fs.FileExists);
        var load = source is null ? new WatchStateLoad(new WatchState(), null) : LoadState(fs, source, nowUtc);
        return new WatchStateLocation(path, writable is not null, load);
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
            try { fs.Move(statePath, backup); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new WatchStateLoad(new WatchState(), statePath); }
            return new WatchStateLoad(new WatchState(), backup);
        }
    }

    public static string Serialize(WatchState state) => JsonSerializer.Serialize(state, JsonOptions);

    public bool IsProcessed(string path, long length)
    {
        var name = Path.GetFileName(path);
        var entry = State.Processed.LastOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (entry is null || entry.Length != length) return false;
        return !(entry.Failed && _retryable.Contains(entry.Name));
    }

    public bool IsOwnOutput(string path)
    {
        var name = Path.GetFileName(path);
        return State.Processed.Any(e => e.Output is { } output && string.Equals(output, name, PathComparison));
    }

    public IReadOnlyList<string> Poll(Action<WatchEvent>? log = null)
    {
        var now = _clock.UtcNow;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ready = new List<string>();
        foreach (var path in _fs.EnumerateFiles(Options.WatchDirectory).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (!IsCandidate(path)) continue;
            if (IsOwnOutput(path))
            {
                if (_skipLogged.Add(path)) log?.Invoke(new WatchEvent(WatchEventKind.Skipped, path));
                continue;
            }
            if (_fs.Stat(path) is not { } stamp) continue;
            if (IsProcessed(path, stamp.Length)) continue;
            seen.Add(path);

            if (!_pending.TryGetValue(path, out var previous) || previous.Stamp != stamp)
            {
                if (previous is null) log?.Invoke(new WatchEvent(WatchEventKind.Waiting, path));
                _pending[path] = new Observation(stamp, now, 0);
                continue;
            }

            var observation = previous with { Confirmations = previous.Confirmations + 1 };
            _pending[path] = observation;
            if (stamp.Length <= 0 || observation.Confirmations < StableConfirmations || now - observation.Since < Options.StableFor) continue;
            if (_fs.IsLocked(path)) continue;

            _pending.Remove(path);
            ready.Add(path);
        }

        foreach (var gone in _pending.Keys.Where(k => !seen.Contains(k)).ToList()) _pending.Remove(gone);
        return ready;
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
                    if (_fs.Stat(path) is not { } before) continue;
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

                    if (_fs.Stat(path) is not { } after || after != before)
                    {
                        DiscardOutput(outcome.Output);
                        if (_fs.Stat(path) is { } changed) _pending[path] = new Observation(changed, _clock.UtcNow, 0);
                        log?.Invoke(new WatchEvent(WatchEventKind.Changed, path));
                        continue;
                    }

                    Record(path, before, outcome, log);
                    log?.Invoke(new WatchEvent(outcome.Success ? WatchEventKind.Done : WatchEventKind.Failed, path,
                        outcome.Success ? outcome.Output : outcome.Error));
                }

                if (Options.Once && _pending.Count == 0)
                    return WatchRunResult.Finished;
                await _clock.Delay(Options.PollInterval, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            log?.Invoke(new WatchEvent(WatchEventKind.Stopped, Options.WatchDirectory));
            return WatchRunResult.Cancelled;
        }
    }


    private void DiscardOutput(string? output)
    {
        if (output is null) return;
        try
        {
            if (_fs.FileExists(output)) _fs.Delete(output);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void Record(string path, WatchFileStamp stamp, WatchOutcome outcome, Action<WatchEvent>? log)
    {
        var name = Path.GetFileName(path);
        var retried = _retryable.Remove(name);
        if (!outcome.Success) FailedCount++;
        var entries = State.Processed
            .Where(e => !string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
            .Append(new WatchEntry
            {
                Name = name,
                Length = stamp.Length,
                Failed = !outcome.Success,
                Retried = !outcome.Success && retried,
                ExitCode = outcome.ExitCode,
                Output = outcome.Output is null ? null : Path.GetFileName(outcome.Output),
                Error = outcome.Error,
                ProcessedUtc = _clock.UtcNow
            })
            .ToList();
        State = State with { Processed = entries };
        try
        {
            _fs.WriteAllTextAtomic(StatePath, Serialize(State));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log?.Invoke(new WatchEvent(WatchEventKind.StateNotSaved, StatePath, ex.Message));
        }
    }

    private static bool CanWrite(IWatchFileSystem fs, string statePath)
    {
        var probe = statePath + ".yoklama";
        try
        {
            fs.CreateDirectory(Path.GetDirectoryName(statePath)!);
            fs.WriteAllTextAtomic(probe, "");
            fs.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string Normalize(string path)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private sealed record Observation(WatchFileStamp Stamp, DateTime Since, int Confirmations);
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
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(new UTF8Encoding(false).GetBytes(content));
            stream.Flush(true);
        }
        File.Move(temp, path, true);
    }

    public void Move(string source, string destination) => File.Move(source, destination, true);

    public void Delete(string path) => File.Delete(path);
}

public sealed class SystemWatchClock : IWatchClock
{
    public static SystemWatchClock Instance { get; } = new();

    public DateTime UtcNow => DateTime.UtcNow;

    public Task Delay(TimeSpan delay, CancellationToken ct) => Task.Delay(delay, ct);
}
