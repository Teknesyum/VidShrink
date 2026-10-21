using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public static class AppAudioSessions
{
    private const int RenderFlow = 0;
    private const int ActiveDevices = 1;
    private const int ClsCtxAll = 23;
    private const int ExpiredState = 2;
    private const int ActiveState = 1;

    private static readonly Guid SessionManagerId = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");

    public static IReadOnlyList<AppAudioSession> List()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, AppAudio.MinimumBuild)) return Array.Empty<AppAudioSession>();
        try { return ListWindows(); }
        catch (Exception ex) when (ex is COMException or InvalidCastException or InvalidComObjectException) { return Array.Empty<AppAudioSession>(); }
    }

    public static IReadOnlyList<AppAudioSession> Candidates(string processName)
    {
        var list = new List<AppAudioSession>(List());
        try
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process) list.Add(new AppAudioSession(process.Id, process.ProcessName, false));
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }

        return list;
    }

    [SupportedOSPlatform("windows")]
    private static List<AppAudioSession> ListWindows()
    {
        var found = new List<AppAudioSession>();
        var enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorClass();
        try
        {
            if (enumerator.EnumAudioEndpoints(RenderFlow, ActiveDevices, out var devices) < 0 || devices is null) return found;
            try
            {
                if (devices.GetCount(out var count) < 0) return found;
                for (uint i = 0; i < count; i++)
                {
                    if (devices.Item(i, out var device) < 0 || device is null) continue;
                    try { Collect(device, found); }
                    finally { Marshal.ReleaseComObject(device); }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(devices);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }

        return found;
    }

    [SupportedOSPlatform("windows")]
    private static void Collect(IMMDevice device, List<AppAudioSession> found)
    {
        var iid = SessionManagerId;
        if (device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out var activated) < 0 || activated is not IAudioSessionManager2 manager) return;
        try
        {
            if (manager.GetSessionEnumerator(out var sessions) < 0 || sessions is null) return;
            try
            {
                if (sessions.GetCount(out var count) < 0) return;
                for (var i = 0; i < count; i++)
                {
                    if (sessions.GetSession(i, out var raw) < 0 || raw is not IAudioSessionControl2 control) continue;
                    try
                    {
                        if (control.GetState(out var state) < 0 || state == ExpiredState) continue;
                        if (control.IsSystemSoundsSession() == 0) continue;
                        if (control.GetProcessId(out var pid) < 0 || pid == 0) continue;
                        if (NameOf((int)pid) is { } name) found.Add(new AppAudioSession((int)pid, name, state == ActiveState));
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(control);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(sessions);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(manager);
        }
    }

    private static string? NameOf(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorClass
    {
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection? devices);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int Item(uint index, out IMMDevice? device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? instance);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig]
        int GetAudioSessionControl(IntPtr sessionGuid, int flags, out IntPtr control);

        [PreserveSig]
        int GetSimpleAudioVolume(IntPtr sessionGuid, int flags, out IntPtr volume);

        [PreserveSig]
        int GetSessionEnumerator(out IAudioSessionEnumerator? sessions);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig]
        int GetCount(out int count);

        [PreserveSig]
        int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object? session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        [PreserveSig]
        int GetState(out int state);

        [PreserveSig]
        int GetDisplayName(out IntPtr name);

        [PreserveSig]
        int SetDisplayName(IntPtr name, IntPtr context);

        [PreserveSig]
        int GetIconPath(out IntPtr path);

        [PreserveSig]
        int SetIconPath(IntPtr path, IntPtr context);

        [PreserveSig]
        int GetGroupingParam(out Guid grouping);

        [PreserveSig]
        int SetGroupingParam(IntPtr grouping, IntPtr context);

        [PreserveSig]
        int RegisterAudioSessionNotification(IntPtr client);

        [PreserveSig]
        int UnregisterAudioSessionNotification(IntPtr client);

        [PreserveSig]
        int GetSessionIdentifier(out IntPtr identifier);

        [PreserveSig]
        int GetSessionInstanceIdentifier(out IntPtr identifier);

        [PreserveSig]
        int GetProcessId(out uint processId);

        [PreserveSig]
        int IsSystemSoundsSession();
    }
}
