using System.Collections.Concurrent;
using System.IO.Pipes;

namespace VidShrink.Core;

public sealed record ShrinkRequest(int TargetMegabytes, string Path);

public enum ShrinkArgumentProblem
{
    NoTarget,
    TargetNotANumber,
    TargetNotPositive,
    TargetNotInQuickList,
    NoPath,
}

public sealed record ShrinkArgumentResult(ShrinkRequest? Request, ShrinkArgumentProblem? Problem)
{
    public bool Ok => Request is not null;

    public static ShrinkArgumentResult Success(ShrinkRequest request) => new(request, null);
    public static ShrinkArgumentResult Failure(ShrinkArgumentProblem problem) => new(null, problem);
}

public static class ShrinkRequestResolver
{
    public static ShrinkArgumentResult Resolve(IReadOnlyList<string>? args, IReadOnlyList<int> quickTargets)
    {
        if (args is null) return ShrinkArgumentResult.Failure(ShrinkArgumentProblem.NoTarget);

        var flagIndex = -1;
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], ShellIntegration.ShrinkFlag, StringComparison.Ordinal))
            {
                flagIndex = i;
                break;
            }
        }

        if (flagIndex < 0 || flagIndex + 1 >= args.Count)
            return ShrinkArgumentResult.Failure(ShrinkArgumentProblem.NoTarget);

        var targetToken = args[flagIndex + 1];
        if (!int.TryParse(targetToken, out var target))
            return ShrinkArgumentResult.Failure(ShrinkArgumentProblem.TargetNotANumber);

        if (target <= 0)
            return ShrinkArgumentResult.Failure(ShrinkArgumentProblem.TargetNotPositive);

        if (!quickTargets.Contains(target))
            return ShrinkArgumentResult.Failure(ShrinkArgumentProblem.TargetNotInQuickList);

        var rest = args.Skip(flagIndex + 2).ToList();
        var path = ShellIntegration.ResolveStartupPath(rest);
        if (path is null)
            return ShrinkArgumentResult.Failure(ShrinkArgumentProblem.NoPath);

        return ShrinkArgumentResult.Success(new ShrinkRequest(target, path));
    }
}

public sealed class ShrinkRequestQueue : IDisposable
{
    private readonly string _pipeName;
    private readonly Mutex _ownerLock;
    private readonly BlockingCollection<ShrinkRequest> _pending = new();
    private CancellationTokenSource? _cancel;
    private Task? _listenTask;
    private Task? _processTask;
    private int _disposed;

    public bool IsOwner { get; }

    public ShrinkRequestQueue(string channelName)
    {
        _pipeName = $"VidShrinkKuyruk-{channelName}";
        _ownerLock = new Mutex(initiallyOwned: true, name: $"VidShrinkKuyrukKilidi-{channelName}", out var createdNew);
        IsOwner = createdNew;
        if (!IsOwner) _ownerLock.Dispose();
    }

    public void StartOwning(Action<ShrinkRequest> handler)
    {
        if (!IsOwner) throw new InvalidOperationException("Bu örnek kuyruğun sahibi değil.");

        _cancel = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoop(_cancel.Token));
        _processTask = Task.Run(() => ProcessLoop(handler, _cancel.Token));
    }

    public bool Submit(ShrinkRequest request, TimeSpan? timeout = null)
    {
        if (IsOwner)
        {
            _pending.Add(request);
            return true;
        }

        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect((int)(timeout ?? TimeSpan.FromSeconds(5)).TotalMilliseconds);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine($"{request.TargetMegabytes}\t{request.Path}");
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            return false;
        }
    }

    private void ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                _pipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances);
            try
            {
                server.WaitForConnectionAsync(token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                continue;
            }

            using var reader = new StreamReader(server);
            var line = reader.ReadLine();
            if (line is null) continue;

            var parts = line.Split('\t', 2);
            if (parts.Length == 2 && int.TryParse(parts[0], out var target))
            {
                _pending.Add(new ShrinkRequest(target, parts[1]));
            }
        }
    }

    private void ProcessLoop(Action<ShrinkRequest> handler, CancellationToken token)
    {
        try
        {
            foreach (var request in _pending.GetConsumingEnumerable(token))
            {
                handler(request);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        _cancel?.Cancel();
        _pending.CompleteAdding();
        try { Task.WaitAll(new[] { _listenTask, _processTask }.Where(t => t is not null).ToArray()!, TimeSpan.FromSeconds(5)); }
        catch (AggregateException) { }

        _ownerLock.Dispose();
        _pending.Dispose();
        _cancel?.Dispose();
    }
}
