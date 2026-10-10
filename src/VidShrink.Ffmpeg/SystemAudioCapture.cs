using System.Runtime.InteropServices;

namespace VidShrink.Ffmpeg;

/// <summary>Sistem sesi kaynaginin o andaki hali.</summary>
public enum SystemAudioState
{
    /// <summary>Kayit basladigindaki cikis cihazi okunuyor.</summary>
    Capturing,

    /// <summary>Varsayilan cikis cihazi degisti; yeni cihaz okunuyor.</summary>
    Switched,

    /// <summary>Okunacak cikis cihazi yok ya da acilamadi; ses izine sessizlik yaziliyor.</summary>
    Unavailable
}

/// <summary>
/// Cikis cihazinda calan sesin kaynagi. <see cref="Read"/> beklemez: o ana kadar birikmis
/// 48 kHz, iki kanalli, 16 bitlik ornekleri verir, sistem sessizse sifir doner. Hicbir uye
/// atmaz; cihaz kaybi <see cref="State"/> ile soylenir. Olcu sahte bir kaynak verir, gercek
/// ses cihazina dokunmaz.
/// </summary>
public interface ISystemAudioCapture : IDisposable
{
    SystemAudioState State { get; }

    int Read(byte[] buffer);
}

/// <summary>Gercek kaynagin kapisi: Windows'ta WASAPI loopback, baska yerde kaynak yok.</summary>
public static class SystemAudioCapture
{
    public static ISystemAudioCapture? Open()
    {
        if (!OperatingSystem.IsWindows()) return null;

        try { return WasapiLoopbackCapture.Start(); }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or OutOfMemoryException)
        {
            return null;
        }
    }
}
