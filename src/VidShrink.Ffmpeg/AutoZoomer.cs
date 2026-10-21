using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public sealed record AutoZoomResult(bool Ok, string? Target, string Error);

public static class AutoZoomer
{
    public static async Task<AutoZoomResult> RunAsync(string source, IReadOnlyList<ZoomRegion> regions, double scale, int threads = 0, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(regions);
        if (regions.Count == 0) return new AutoZoomResult(true, null, string.Empty);

        MediaInfo info;
        try { info = await FfprobeClient.ProbeAsync(source, ct); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        {
            return new AutoZoomResult(false, null, ex.Message);
        }

        if (info.Width <= 0 || info.Height <= 0) return new AutoZoomResult(false, null, "no video stream");

        var target = AutoZoom.Target(source, File.Exists);
        var filter = AutoZoom.Filter(regions, scale, info.Width, info.Height, info.Fps);
        var run = await FfmpegRunner.RunAsync(AutoZoom.Build(source, target, filter, info.HasAudio, threads), ct);
        if (run.Ok && File.Exists(target)) return new AutoZoomResult(true, target, string.Empty);

        try { if (File.Exists(target)) File.Delete(target); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return new AutoZoomResult(false, null, FfmpegRunner.Tail(run.StandardError));
    }
}
