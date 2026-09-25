using System.Runtime.InteropServices;

namespace VidShrink.Player;

/// <summary>
/// Yerel kitaplık yüklenirken çağıran iş parçacığının hata kipini
/// <c>SEM_FAILCRITICALERRORS | SEM_NOOPENFILEERRORBOX</c> yapar, bırakılınca eskisine döndürür.
/// Kip yokken Windows yükleyicisi eksik giriş noktasını ("Giriş Noktası Bulunamadı")
/// kullanıcıya sistem iletişim kutusuyla sorar ve yükleme kutu kapanana dek bekler.
/// Windows dışında hiçbir şey yapmaz.
/// </summary>
internal readonly struct LoaderErrorMode : IDisposable
{
    internal const uint FailCriticalErrors = 0x0001;
    internal const uint NoOpenFileErrorBox = 0x8000;

    private readonly uint _previous;
    private readonly bool _changed;

    private LoaderErrorMode(uint previous)
    {
        _previous = previous;
        _changed = true;
    }

    public static LoaderErrorMode Suppress()
    {
        if (!OperatingSystem.IsWindows()) return default;
        return SetThreadErrorMode(FailCriticalErrors | NoOpenFileErrorBox, out var previous)
            ? new LoaderErrorMode(previous)
            : default;
    }

    public void Dispose()
    {
        if (_changed) SetThreadErrorMode(_previous, out _);
    }

    internal static uint Current => OperatingSystem.IsWindows() ? GetThreadErrorMode() : 0;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetThreadErrorMode(uint newMode, out uint oldMode);

    [DllImport("kernel32.dll")]
    private static extern uint GetThreadErrorMode();
}
