using System;
using System.Runtime.InteropServices;

namespace VidShrink.App.Recorder;

/// <summary>
/// Birincil ekranın yenileme hızı, Hz. Avalonia'nın <c>Screen</c> listesi konum ve
/// çözünürlük veriyor ama yenileme hızı vermiyor; otomatik kip kare hızını bu sayıdan
/// seçtiği için hız işletim sisteminden okunuyor.
///
/// <para>Windows dışında sıfır dönüyor: <c>gdigrab</c> dışındaki platformlarda bu sayının
/// ölçülmüş bir kaynağı yok ve uydurulmuyor. Sıfır okunduğunda
/// <see cref="VidShrink.Core.RecorderAutoPlan.FallbackFps"/> çalışıyor.</para>
/// </summary>
internal static class ScreenRefresh
{
    private const int CurrentSettings = -1;

    /// <summary>Birincil ekranın yenileme hızı; okunamazsa sıfır.</summary>
    internal static double PrimaryHz()
    {
        if (!OperatingSystem.IsWindows()) return 0;

        try
        {
            var mode = new DevMode { dmSize = (ushort)Marshal.SizeOf<DevMode>() };
            return EnumDisplaySettings(null, CurrentSettings, ref mode) ? mode.dmDisplayFrequency : 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return 0;
        }
    }

    [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }
}
