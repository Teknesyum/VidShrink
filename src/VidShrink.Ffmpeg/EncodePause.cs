using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VidShrink.Ffmpeg;

/// <summary>
/// SIK-P3: koşan kodlamanın duraklatma düğmesi. Kayıtlı ffmpeg süreçleri askıya alınır
/// (Windows'ta <c>NtSuspendProcess</c>, macOS ve Linux'ta <c>SIGSTOP</c>) ve kayıtlı saatler durur;
/// sürdürünce ikisi de kaldığı yerden devam eder. Askıdayken yeni süreç başlamaz. İptal askıdaki
/// süreci de öldürür.
/// </summary>
public sealed class EncodePause
{
    private readonly object _gate = new();
    private readonly List<Process> _processes = new();
    private readonly List<Stopwatch> _clocks = new();
    private readonly HashSet<Stopwatch> _stoppedByPause = new();
    private TaskCompletionSource _resumed = Done();
    private bool _paused;

    public bool IsPaused
    {
        get { lock (_gate) return _paused; }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (_paused) return;
            _paused = true;
            _resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            foreach (var process in _processes) ProcessSuspender.Suspend(process);
            foreach (var clock in _clocks) StopClock(clock);
        }
    }

    public void Resume()
    {
        TaskCompletionSource resumed;
        lock (_gate)
        {
            if (!_paused) return;
            _paused = false;
            foreach (var process in _processes) ProcessSuspender.Resume(process);
            foreach (var clock in _stoppedByPause) clock.Start();
            _stoppedByPause.Clear();
            resumed = _resumed;
        }
        resumed.TrySetResult();
    }

    /// <summary>Askıdaysa sürdürülene ya da iptal edilene kadar bekler.</summary>
    public Task WaitWhilePausedAsync(CancellationToken ct)
    {
        Task resumed;
        lock (_gate) resumed = _resumed.Task;
        return resumed.IsCompleted ? Task.CompletedTask : resumed.WaitAsync(ct);
    }

    /// <summary>Süreci kayda alır; askıdaysa hemen askıya alınır. Bırakınca kayıttan düşer.</summary>
    internal IDisposable Track(Process process)
    {
        lock (_gate)
        {
            _processes.Add(process);
            if (_paused) ProcessSuspender.Suspend(process);
        }
        return new Release(() =>
        {
            lock (_gate) _processes.Remove(process);
        });
    }

    /// <summary>Saati kayda alır; askıdayken durur, sürdürünce işler. Bırakılan saate askı bir daha dokunmaz.</summary>
    internal IDisposable Track(Stopwatch clock)
    {
        lock (_gate)
        {
            _clocks.Add(clock);
            if (_paused) StopClock(clock);
        }
        return new Release(() =>
        {
            lock (_gate)
            {
                _clocks.Remove(clock);
                _stoppedByPause.Remove(clock);
            }
        });
    }

    private void StopClock(Stopwatch clock)
    {
        if (!clock.IsRunning) return;
        clock.Stop();
        _stoppedByPause.Add(clock);
    }

    private static TaskCompletionSource Done()
    {
        var done = new TaskCompletionSource();
        done.SetResult();
        return done;
    }

    private sealed class Release : IDisposable
    {
        private Action? _action;
        public Release(Action action) => _action = action;
        public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
    }
}

/// <summary>
/// İşletim sisteminin süreç askıya alma çağrıları. Bitmiş ya da erişilemeyen süreçte sessiz kalır:
/// askı kodlamanın sonucunu değiştirmez, yalnız zamanını.
/// </summary>
internal static class ProcessSuspender
{
    private const int SigStopLinux = 19;
    private const int SigContLinux = 18;
    private const int SigStopMac = 17;
    private const int SigContMac = 19;

    internal static bool Suspend(Process process) => Send(process, true);

    internal static bool Resume(Process process) => Send(process, false);

    private static bool Send(Process process, bool stop)
    {
        try
        {
            if (process.HasExited) return false;
            if (OperatingSystem.IsWindows())
                return (stop ? NtSuspendProcess(process.Handle) : NtResumeProcess(process.Handle)) == 0;
            var signal = OperatingSystem.IsMacOS()
                ? stop ? SigStopMac : SigContMac
                : stop ? SigStopLinux : SigContLinux;
            return Kill(process.Id, signal) == 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException
                                       or DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(IntPtr process);

    [DllImport("ntdll.dll")]
    private static extern int NtResumeProcess(IntPtr process);

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);
}
