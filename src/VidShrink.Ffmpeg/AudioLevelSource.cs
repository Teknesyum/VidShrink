using System.ComponentModel;
using System.Diagnostics;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

/// <summary>
/// Bir ses girişinin canlı seviyesi. <see cref="Level"/> her pencere için bir tepe dBFS
/// değeri verir, <see cref="Ended"/> kaynak kendiliğinden bittiğinde bir kez çağrılır. İkisi
/// de arka plan iş parçacığında gelir.
/// </summary>
public interface IAudioLevelSource : IDisposable
{
    event Action<double>? Level;

    event Action? Ended;
}

/// <summary>
/// Seviyeyi kayıt sürecinden ayrı, kısa ömürlü bir ffmpeg sürecinden okur: süreç cihazı
/// tek kanallı 8 kHz ham örnek olarak stdout'a yazar, tepe burada hesaplanır. stderr ayrı
/// görevde boşaltılır; boşaltılmayan boru 4096 baytta dolup süreci kilitliyor.
///
/// <para>Kayıt süreciyle aynı cihazı paylaşmak kaydı bozmuyor; ölçüm
/// <c>docs/olcumler/kaydedici-ses-seviyesi.md</c>.</para>
/// </summary>
public sealed class AudioLevelSource : IAudioLevelSource
{
    /// <summary>Doğruyken <see cref="Open"/> süreç açmaz. Test konağı açılışta kurar.</summary>
    internal static bool Disabled { get; set; }

    private readonly Process _process;
    private readonly Task _pump;
    private int _disposed;

    public event Action<double>? Level;

    public event Action? Ended;

    internal int ProcessId { get; }

    internal ProcessPriorityClass? Priority { get; }

    internal Task Completion => _pump;

    public static IAudioLevelSource? Open(AudioCaptureDevice device)
    {
        if (Disabled) return null;

        try { return Start(ToolLocator.Ffmpeg, AudioLevel.Arguments(device)); }
        catch (Exception ex) when (ex is FileNotFoundException or Win32Exception or InvalidOperationException or UnknownCaptureDeviceException)
        {
            return null;
        }
    }

    internal static AudioLevelSource Start(string ffmpegPath, IReadOnlyList<string> arguments)
    {
        var process = new Process { StartInfo = ToolLocator.StartInfo(ffmpegPath, arguments) };
        try
        {
            process.Start();
        }
        catch
        {
            process.Dispose();
            throw;
        }

        return new AudioLevelSource(process);
    }

    private AudioLevelSource(Process process)
    {
        _process = process;
        ProcessId = process.Id;
        try
        {
            process.PriorityClass = ProcessPriorityClass.BelowNormal;
            Priority = process.PriorityClass;
        }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }

        var errors = DrainAsync(process.StandardError);
        _pump = Task.Run(() => PumpAsync(errors));
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        var buffer = new char[1024];
        try
        {
            while (await reader.ReadAsync(buffer).ConfigureAwait(false) > 0) { }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private async Task PumpAsync(Task errors)
    {
        var window = new AudioLevelWindow();
        var buffer = new byte[AudioLevel.WindowSamples * 2];
        try
        {
            var stream = _process.StandardOutput.BaseStream;
            int read;
            while ((read = await stream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                window.Add(buffer.AsSpan(0, read), Raise);
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }

        await errors.ConfigureAwait(false);
        if (Volatile.Read(ref _disposed) == 0) Ended?.Invoke();
    }

    private void Raise(double db)
    {
        if (Volatile.Read(ref _disposed) == 0) Level?.Invoke(db);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }

        try { _process.WaitForExit(2000); }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }

        _pump.ContinueWith(_ => _process.Dispose(), TaskScheduler.Default);
    }
}
