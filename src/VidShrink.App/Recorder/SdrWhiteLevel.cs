using System;
using System.Runtime.InteropServices;

namespace VidShrink.App.Recorder;

/// <summary>
/// Windows'un HDR ekranda SDR icerigi koydugu parlaklik ("SDR icerik parlakligi" kaydiricisi).
/// <c>QueryDisplayConfig</c> etkin yollari verir; DXGI cikisinin aygit adi
/// (<c>\\.\DISPLAY1</c>) yolun kaynak adiyla eslenir, hedefe
/// <c>DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL</c> sorulur. Deger 1000 = 80 nit olcegindedir.
/// Okunamazsa <c>null</c>; cagiran varsayilani kullanir ve soyler.
/// </summary>
internal static class SdrWhiteLevel
{
    private const uint QdcOnlyActivePaths = 2;
    private const int GetSourceName = 1;
    private const int GetSdrWhiteLevel = 11;
    private const int PathInfoSize = 72;
    private const int ModeInfoSize = 64;

    /// <summary>Ham <c>SDRWhiteLevel</c> degerini nite cevirir: <c>deger / 1000 * 80</c>.</summary>
    internal static double ToNits(uint raw) => raw / 1000.0 * 80.0;

    internal static double? Read(string deviceName)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(deviceName)) return null;
        try { return ReadCore(deviceName); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or OutOfMemoryException)
        {
            return null;
        }
    }

    private static double? ReadCore(string deviceName)
    {
        if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount) != 0 || pathCount == 0) return null;
        var paths = Marshal.AllocHGlobal((int)pathCount * PathInfoSize);
        var modes = Marshal.AllocHGlobal((int)Math.Max(1, modeCount) * ModeInfoSize);
        try
        {
            if (QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0) return null;
            for (var i = 0; i < pathCount; i++)
            {
                var path = paths + i * PathInfoSize;
                var source = new SourceName
                {
                    Header = Header(GetSourceName, Marshal.SizeOf<SourceName>(), path, 0)
                };
                if (DisplayConfigGetDeviceInfo(ref source) != 0) continue;
                if (!string.Equals(source.ViewGdiDeviceName, deviceName, StringComparison.OrdinalIgnoreCase)) continue;

                var white = new WhiteLevel
                {
                    Header = Header(GetSdrWhiteLevel, Marshal.SizeOf<WhiteLevel>(), path, 20)
                };
                if (DisplayConfigGetDeviceInfo(ref white) != 0 || white.SdrWhiteLevel == 0) return null;
                return ToNits(white.SdrWhiteLevel);
            }
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(paths);
            Marshal.FreeHGlobal(modes);
        }
    }

    private static DeviceInfoHeader Header(int type, int size, IntPtr path, int offset) => new()
    {
        Type = type,
        Size = size,
        AdapterLow = (uint)Marshal.ReadInt32(path, offset),
        AdapterHigh = Marshal.ReadInt32(path, offset + 4),
        Id = (uint)Marshal.ReadInt32(path, offset + 8)
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfoHeader
    {
        public int Type;
        public int Size;
        public uint AdapterLow;
        public int AdapterHigh;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SourceName
    {
        public DeviceInfoHeader Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ViewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WhiteLevel
    {
        public DeviceInfoHeader Header;
        public uint SdrWhiteLevel;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(uint flags, ref uint pathCount, IntPtr paths, ref uint modeCount, IntPtr modes, IntPtr topologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref SourceName request);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref WhiteLevel request);
}
