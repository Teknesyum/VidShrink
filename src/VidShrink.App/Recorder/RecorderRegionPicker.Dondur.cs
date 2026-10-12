using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

/// <summary>
/// Bölge seçicinin X11 kolu. Seçici masaüstünü kaplayan saydam bir pencere; birleştirici
/// (compositor) olmayan X11 masaüstünde saydam zemin siyah çiziliyor ve kullanıcı neyi
/// seçtiğini göremiyordu. Linux'ta masaüstünün bir karesi seçici açılmadan alınır ve
/// karartmanın altına zemin olarak konur; birleştiricili masaüstünde de aynı görünür.
/// Wayland'da X kopyası yerel pencereleri görmez, orada eski davranış sürer.
/// </summary>
internal partial class RecorderRegionPicker
{
    internal static Func<CancellationToken, Task<Bitmap?>> Freeze { get; set; } = FreezeX11Async;

    /// <summary>Seçici açılmadan alınan masaüstü karesi; yoksa zemin saydam kalır.</summary>
    internal Bitmap? Frozen { get; init; }

    internal void ApplyFrozen()
    {
        if (Frozen is null) return;
        Surface.Background = new ImageBrush(Frozen) { Stretch = Stretch.Fill };
    }

    internal static async Task<Bitmap?> FreezeX11Async(CancellationToken ct)
    {
        if (!OperatingSystem.IsLinux() || RecorderWindowsX11.IsWayland(Environment.GetEnvironmentVariable)) return null;
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        if (string.IsNullOrWhiteSpace(display) || !ToolLocator.IsAvailable(out _)) return null;

        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in new[] { "-v", "error", "-f", "x11grab", "-draw_mouse", "0", "-i", display, "-frames:v", "1", "-f", "image2pipe", "-c:v", "png", "-" })
            psi.ArgumentList.Add(a);

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return null;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var image = new MemoryStream();
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.StandardOutput.BaseStream.CopyToAsync(image, timeout.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            if (process.ExitCode != 0 || image.Length == 0) return null;
            image.Position = 0;
            return new Bitmap(image);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or InvalidOperationException
                                       or System.ComponentModel.Win32Exception or ArgumentException)
        {
            return null;
        }
    }
}
