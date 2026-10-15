using System;
using System.Collections.Generic;
using Avalonia.Threading;
using VidShrink.Core;

namespace VidShrink.App.Integration;

/// <summary>
/// Kabuktan ya da ikinci örnekten gelen açma isteği: yolların tamamı, geliş sırasıyla, ve
/// "sıraya ekle" bayrağı. Kanalda düz bir dizi olarak taşınır; bayrak varsa dizinin başında
/// durur, yoksa dizi eski sürümlerin anladığı yol listesinin aynısıdır.
/// </summary>
internal sealed record ForwardedRequest(IReadOnlyList<string> Paths, bool Enqueue)
{
    internal static readonly ForwardedRequest Empty = new(Array.Empty<string>(), false);

    internal string? First => Paths.Count > 0 ? Paths[0] : null;

    /// <summary>Bayraksız ve tek yollu istek: eski tek dosya açılışının aynısı.</summary>
    internal bool Single => !Enqueue && Paths.Count == 1;

    internal IReadOnlyList<string> Encode()
    {
        if (!Enqueue) return Paths;
        var encoded = new List<string>(Paths.Count + 1) { ShellIntegration.EnqueueFlag };
        encoded.AddRange(Paths);
        return encoded;
    }

    /// <summary>Kanaldan gelen diziyi çözer: boş parçalar atılır, tırnak ve boşluk kırpılır.</summary>
    internal static ForwardedRequest Decode(IReadOnlyList<string> raw)
    {
        var enqueue = false;
        var paths = new List<string>();
        foreach (var item in raw)
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            var path = item.Trim().Trim('"');
            if (path.Length == 0) continue;
            if (string.Equals(path, ShellIntegration.EnqueueFlag, StringComparison.Ordinal))
            {
                enqueue = true;
                continue;
            }

            paths.Add(path);
        }

        return new ForwardedRequest(paths, enqueue);
    }

    /// <summary>
    /// Komut satırından isteği kurar. Bayrak argümanların herhangi bir yerinde olabilir;
    /// kabuk komutu onu yolun arkasına yazar ki başlatıcının tek dosya kısayolu bozulmasın.
    /// Var olan bütün yollar sırayla toplanır; hiçbiri yoksa <paramref name="fallback"/>
    /// (ağ adresi) tek yol olur.
    /// </summary>
    internal static ForwardedRequest FromArguments(IReadOnlyList<string> args, string? fallback, Func<string, bool>? exists = null)
    {
        var enqueue = false;
        var rest = new List<string>(args.Count);
        foreach (var argument in args)
        {
            if (string.Equals(argument, ShellIntegration.EnqueueFlag, StringComparison.Ordinal)) enqueue = true;
            else rest.Add(argument);
        }

        var found = ShellShrinkStartup.ScanPaths(rest, 0, exists).Found;
        if (found.Count > 0) return new ForwardedRequest(found, enqueue);
        return fallback is null ? Empty with { Enqueue = enqueue } : new ForwardedRequest(new[] { fallback }, enqueue);
    }
}

internal sealed class ForwardedFiles
{
    internal static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private readonly Queue<ForwardedRequest> _pending = new();
    private Func<ForwardedRequest, bool>? _target;

    internal int Pending
    {
        get { lock (_gate) return _pending.Count; }
    }

    public bool Receive(IReadOnlyList<string> paths)
    {
        var request = ForwardedRequest.Decode(paths);
        Func<ForwardedRequest, bool>? target;
        lock (_gate)
        {
            target = _target;
            if (target is null)
            {
                _pending.Enqueue(request);
                return true;
            }
        }

        return OnUiThread(target, request);
    }

    public void Attach(Func<ForwardedRequest, bool> target)
    {
        ForwardedRequest[] waiting;
        lock (_gate)
        {
            _target = target;
            waiting = _pending.ToArray();
            _pending.Clear();
        }

        foreach (var request in waiting) target(request);
    }

    private static bool OnUiThread(Func<ForwardedRequest, bool> target, ForwardedRequest request)
    {
        if (Dispatcher.UIThread.CheckAccess()) return target(request);

        var delivery = Dispatcher.UIThread.InvokeAsync(() => target(request)).GetTask();
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
