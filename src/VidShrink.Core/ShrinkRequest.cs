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
    public const string Ack = "TAMAM";
    public const string Nack = "HATA";

    private readonly string _pipeName;
    private readonly Mutex _ownerLock;
    private readonly BlockingCollection<ShrinkRequest> _pending = new();
    private CancellationTokenSource? _cancel;
    private Task? _listenTask;
    private Task? _processTask;
    private int _started;
    private int _disposed;

    public bool IsOwner { get; }

    public string PipeName => _pipeName;

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

        if (Interlocked.Exchange(ref _started, 1) == 1)
            throw new InvalidOperationException("Kuyruk zaten başlatıldı; ikinci tüketici açılamaz.");

        _cancel = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoop(_cancel.Token));
        _processTask = Task.Run(() => ProcessLoop(handler, _cancel.Token));
    }

    public bool Submit(ShrinkRequest request, TimeSpan? timeout = null)
    {
        if (IsOwner)
        {
            try
            {
                _pending.Add(request);
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                return false;
            }
        }

        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            client.Connect((int)(timeout ?? TimeSpan.FromSeconds(5)).TotalMilliseconds);
            using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, leaveOpen: true);
            writer.WriteLine($"{request.TargetMegabytes}\t{request.Path}");
            var reply = reader.ReadLine();
            return string.Equals(reply, Ack, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException)
        {
            return false;
        }
    }

    private void ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                _pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances);
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

            try
            {
                using var reader = new StreamReader(server, leaveOpen: true);
                using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };

                var line = reader.ReadLine();
                if (line is null) continue;

                var parts = line.Split('\t', 2);
                if (parts.Length == 2 && int.TryParse(parts[0], out var target) && Accept(target, parts[1]))
                {
                    writer.WriteLine(Ack);
                }
                else
                {
                    writer.WriteLine(Nack);
                }

                if (OperatingSystem.IsWindows()) server.WaitForPipeDrain();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
            }
        }
    }

    private bool Accept(int target, string path)
    {
        try
        {
            _pending.Add(new ShrinkRequest(target, path));
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return false;
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
