using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VidShrink.Ffmpeg;

/// <summary>
/// Windows is nesnesi: bagli surecler, nesnenin son tutamaci kapaninca isletim sistemi
/// tarafindan oldurulur. Tutamac bu surecindir; surec nasil biterse bitsin (cokme,
/// oldurulme, test konaginin kapanmasi) tutamac kapanir ve bagli cocuk ayakta kalmaz.
/// Zamanlayiciyla oldurme ebeveyn yasarken calisir; ebeveyn zaman asimindan once olurse
/// asili kalan cocugu yalniz bu yol toplar. Windows disinda hicbir sey yapmaz.
/// </summary>
internal sealed class ChildJob : IDisposable
{
    private const int ExtendedLimitInformation = 9;
    private const uint KillOnJobClose = 0x2000;

    private static readonly Lazy<ChildJob> Ortak = new(() => new ChildJob());

    private IntPtr _job;

    /// <summary>Surec boyunca yasayan ortak is nesnesi.</summary>
    internal static ChildJob Shared => Ortak.Value;

    internal ChildJob()
    {
        if (!OperatingSystem.IsWindows()) return;

        var job = CreateJobObjectW(IntPtr.Zero, null);
        if (job == IntPtr.Zero) return;

        var info = new ExtendedLimit { Basic = new BasicLimit { LimitFlags = KillOnJobClose } };
        if (!SetInformationJobObject(job, ExtendedLimitInformation, ref info, (uint)Marshal.SizeOf<ExtendedLimit>()))
        {
            CloseHandle(job);
            return;
        }

        _job = job;
    }

    /// <summary>Sureci is nesnesine baglar. Baglanamadiysa <c>false</c>; cagiran yoluna devam eder.</summary>
    internal bool Attach(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (_job == IntPtr.Zero) return false;
        try { return AssignProcessToJobObject(_job, process.Handle); }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        var job = Interlocked.Exchange(ref _job, IntPtr.Zero);
        if (job != IntPtr.Zero) CloseHandle(job);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimit
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimit
    {
        public BasicLimit Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObjectW(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimit info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
