using System;
using System.Runtime.InteropServices;

namespace VidShrink.App.Recorder;

/// <summary>
/// Kaydedicinin kendi denetim yüzeylerini ekran yakalamasından çıkaran Windows çağrısı
/// (<c>SetWindowDisplayAffinity</c>, <c>WDA_EXCLUDEFROMCAPTURE</c>). Çerçeve, bölge
/// düzenleyicisi ve mini şerit aynı çağrıyı buradan yapıyor; sonucu her pencere kendi
/// <c>CaptureExcluded</c> alanında tutuyor.
/// </summary>
internal static class CaptureAffinity
{
    internal const uint ExcludeFromCapture = 0x11;

    /// <summary>
    /// <c>WDA_EXCLUDEFROMCAPTURE</c> Windows 10 2004'te (yapı 19041) geldi. Daha eskisinde aynı
    /// değer <c>WDA_MONITOR</c> gibi davranıyor: pencere kayıttan çıkmıyor, içi siyah giriyor.
    /// </summary>
    internal static bool Supported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);

    internal static bool Exclude(IntPtr hwnd)
        => OperatingSystem.IsWindows() && SetWindowDisplayAffinity(hwnd, ExcludeFromCapture);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
}
