using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace VidShrink.App;

/// <summary>
/// SIK-P2: sistemin uyumasını engelleyen yüz. Yalnız uyku engellenir, ekranın kararmasına
/// dokunulmaz. Pencereler bunu <see cref="SleepGuard"/> üzerinden çağırır, testler sahtesini verir.
/// </summary>
internal interface ISleepBlocker
{
    void Block();
    void Allow();
}

/// <summary>
/// SIK-P2: süreç genelinde sayılan uyku isteği. İlk tutuş sistemden isteği alır, son bırakış geri
/// verir; ana pencere ile kuyruk penceresi aynı anda kodlasa da istek bir kez alınır.
/// </summary>
internal sealed class SleepGuard
{
    private readonly object _gate = new();
    private int _holders;

    public SleepGuard(ISleepBlocker blocker) => Blocker = blocker;

    internal static SleepGuard Shared { get; } = new(SystemSleepBlocker.Instance);

    internal ISleepBlocker Blocker { get; }

    internal int Holders
    {
        get { lock (_gate) return _holders; }
    }

    /// <summary>
    /// SIK-P2: ana pencere dışındaki kodlama yolları (dönüştürme kuyruğu, toplu ve tekli düzenleme
    /// teslimi) Ayarlar'daki kutuyu iş başlarken diskten okur. Okunamazsa varsayılan açık kalır.
    /// </summary>
    internal static bool PreventSleepSetting()
    {
        try { return AppSettings.Load().PreventSleepWhileEncoding; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
    }

    internal IDisposable? HoldIf(bool on) => on ? Hold() : null;

    internal IDisposable Hold()
    {
        lock (_gate)
        {
            if (_holders++ == 0) Blocker.Block();
        }
        return new Lease(this);
    }

    private void Release()
    {
        lock (_gate)
        {
            if (_holders == 0) return;
            if (--_holders == 0) Blocker.Allow();
        }
    }

    private sealed class Lease : IDisposable
    {
        private SleepGuard? _guard;

        public Lease(SleepGuard guard) => _guard = guard;

        public void Dispose() => Interlocked.Exchange(ref _guard, null)?.Release();
    }
}

/// <summary>
/// Gerçek sistem isteği: Windows'ta <c>SetThreadExecutionState</c> kendi iş parçacığında tutulur,
/// macOS'ta <c>caffeinate -i</c>, Linux'ta <c>systemd-inhibit --what=sleep</c> süreci. İki süreç
/// de uygulamanın süreci ölünce kendiliğinden biter. Araç yoksa istek sessizce alınmaz.
/// </summary>
internal sealed class SystemSleepBlocker : ISleepBlocker
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;

    internal static SystemSleepBlocker Instance { get; } = new();

    internal static bool Disabled { get; set; }

    private Thread? _thread;
    private ManualResetEventSlim? _stop;
    private Process? _process;

    public void Block()
    {
        if (Disabled) return;
        var pid = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        if (OperatingSystem.IsWindows()) BlockOnThread();
        else if (OperatingSystem.IsMacOS()) _process = Start("caffeinate", "-i", "-w", pid);
        else if (OperatingSystem.IsLinux())
            _process = Start("systemd-inhibit", "--what=sleep", "--who=VidShrink", "--why=Encoding", "--mode=block",
                "tail", "--pid=" + pid, "-f", "/dev/null");
    }

    public void Allow()
    {
        if (_stop is { } stop)
        {
            stop.Set();
            _thread?.Join(TimeSpan.FromSeconds(2));
            stop.Dispose();
            _stop = null;
            _thread = null;
        }
        if (_process is { } process)
        {
            try
            {
                if (!process.HasExited) process.Kill(true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { }
            process.Dispose();
            _process = null;
        }
    }

    private void BlockOnThread()
    {
        var stop = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            SetThreadExecutionState(EsContinuous | EsSystemRequired);
            stop.Wait();
            SetThreadExecutionState(EsContinuous);
        })
        {
            IsBackground = true,
            Name = nameof(SystemSleepBlocker)
        };
        _stop = stop;
        _thread = thread;
        thread.Start();
    }

    private static Process? Start(string file, params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            return Process.Start(info);
        }
        catch (Exception ex) when (ex is IOException or Win32Exception) { return null; }
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
}
