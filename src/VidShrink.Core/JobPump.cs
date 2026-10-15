namespace VidShrink.Core;

/// <summary>
/// K6: N yuvalı iş pompası. Sırayı tutar, <see cref="ParallelJobs.StartCount"/> kadarını
/// başlatır, biri bitince yerine sıradakini alır. Bir işin düşmesi ötekileri durdurmaz.
/// <see cref="Drained"/> bütün işler bitince bir kez çalışır; yeni iş gelip o da bitince bir kez daha.
/// </summary>
public sealed class JobPump<T> where T : class
{
    private readonly object _gate = new();
    private readonly List<T> _pending = new();
    private readonly List<(T Job, CancellationTokenSource Cts)> _running = new();
    private readonly Func<T, CancellationToken, Task> _run;
    private readonly Func<int> _limit;
    private bool _paused;
    private int _finishedSinceDrain;

    public JobPump(Func<T, CancellationToken, Task> run, Func<int> limit)
    {
        _run = run;
        _limit = limit;
    }

    /// <summary>Bekleyen ve koşan kalmadığında, son çağrıdan beri en az bir iş bittiyse.</summary>
    public event Action? Drained;

    /// <summary>Koşturucunun yakalamadığı istisna; iptal buraya düşmez.</summary>
    public event Action<T, Exception>? Failed;

    /// <summary>SIK-P9: koşan ve bekleyenlerin birleşik listesi değişti; iş eklendi, çıktı, kaydı ya da bitti.</summary>
    public event Action? Changed;

    public IReadOnlyList<T> Pending
    {
        get { lock (_gate) return _pending.ToArray(); }
    }

    public IReadOnlyList<T> Running
    {
        get { lock (_gate) return _running.Select(slot => slot.Job).ToArray(); }
    }

    public int PendingCount
    {
        get { lock (_gate) return _pending.Count; }
    }

    public int RunningCount
    {
        get { lock (_gate) return _running.Count; }
    }

    public bool Paused
    {
        get { lock (_gate) return _paused; }
        set
        {
            lock (_gate) _paused = value;
            if (!value) Pump();
        }
    }

    public void Enqueue(T job)
    {
        lock (_gate) _pending.Add(job);
        Changed?.Invoke();
        Pump();
    }

    public bool RemovePending(int index)
    {
        lock (_gate)
        {
            if (index < 0 || index >= _pending.Count) return false;
            _pending.RemoveAt(index);
        }
        Changed?.Invoke();
        return true;
    }

    public bool MovePending(int index, int delta)
    {
        lock (_gate)
        {
            var target = index + delta;
            if (index < 0 || index >= _pending.Count || target < 0 || target >= _pending.Count) return false;
            (_pending[index], _pending[target]) = (_pending[target], _pending[index]);
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>SIK-P7: bekleyen işi sırasını bozmadan yenisiyle değiştirir; iş kuyruktan çıkmaz.</summary>
    public bool ReplacePending(T current, T replacement)
    {
        lock (_gate)
        {
            var index = _pending.FindIndex(job => ReferenceEquals(job, current));
            if (index < 0) return false;
            _pending[index] = replacement;
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Koşan tek işi iptal eder; ötekilere ve sıraya dokunmaz.</summary>
    public bool Cancel(T job)
    {
        CancellationTokenSource? cts = null;
        lock (_gate)
        {
            foreach (var slot in _running)
                if (ReferenceEquals(slot.Job, job)) cts = slot.Cts;
        }
        if (cts is null) return false;
        Iptal(cts);
        return true;
    }

    /// <summary>Bekleyenleri siler, koşanların hepsini iptal eder.</summary>
    public void CancelAll()
    {
        CancellationTokenSource[] hepsi;
        lock (_gate)
        {
            _pending.Clear();
            hepsi = _running.Select(slot => slot.Cts).ToArray();
        }
        Changed?.Invoke();
        foreach (var cts in hepsi) Iptal(cts);
    }

    private static void Iptal(CancellationTokenSource cts)
    {
        try { cts.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    public void Pump()
    {
        List<(T Job, CancellationTokenSource Cts)> baslayan = new();
        var bosaldi = false;
        lock (_gate)
        {
            var adet = ParallelJobs.StartCount(_pending.Count, _running.Count, _limit(), _paused);
            for (var i = 0; i < adet; i++)
            {
                var slot = (_pending[0], new CancellationTokenSource());
                _pending.RemoveAt(0);
                _running.Add(slot);
                baslayan.Add(slot);
            }

            if (ParallelJobs.Drained(_pending.Count, _running.Count, _paused, _finishedSinceDrain))
            {
                _finishedSinceDrain = 0;
                bosaldi = true;
            }
        }

        foreach (var (job, cts) in baslayan) _ = RunAsync(job, cts);
        if (bosaldi) Drained?.Invoke();
    }

    private async Task RunAsync(T job, CancellationTokenSource cts)
    {
        try
        {
            await _run(job, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Failed?.Invoke(job, ex);
        }
        finally
        {
            lock (_gate)
            {
                _running.RemoveAll(slot => ReferenceEquals(slot.Job, job));
                _finishedSinceDrain++;
            }
            cts.Dispose();
        }
        Changed?.Invoke();
        Pump();
    }
}
