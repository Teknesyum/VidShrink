using System;
using System.Collections.Generic;
using Avalonia.Threading;

namespace VidShrink.App.Integration;

internal sealed class ForwardedFiles
{
    internal static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private readonly Queue<string?> _pending = new();
    private Func<string?, bool>? _target;

    internal int Pending
    {
        get { lock (_gate) return _pending.Count; }
    }

    public bool Receive(IReadOnlyList<string> paths)
    {
        var path = First(paths);
        Func<string?, bool>? target;
        lock (_gate)
        {
            target = _target;
            if (target is null)
            {
                _pending.Enqueue(path);
                return true;
            }
        }

        return OnUiThread(target, path);
    }

    public void Attach(Func<string?, bool> target)
    {
        string?[] waiting;
        lock (_gate)
        {
            _target = target;
            waiting = _pending.ToArray();
            _pending.Clear();
        }

        foreach (var path in waiting) target(path);
    }

    internal static string? First(IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
            if (!string.IsNullOrWhiteSpace(path)) return path.Trim().Trim('"');
        return null;
    }

    private static bool OnUiThread(Func<string?, bool> target, string? path)
    {
        if (Dispatcher.UIThread.CheckAccess()) return target(path);

        var delivery = Dispatcher.UIThread.InvokeAsync(() => target(path)).GetTask();
        try
        {
            return delivery.Wait(DeliveryTimeout) && delivery.Result;
        }
        catch (AggregateException)
        {
            return false;
        }
    }
}
