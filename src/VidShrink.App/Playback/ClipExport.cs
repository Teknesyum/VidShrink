using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Playback;

internal enum ClipKind
{
    Video,
    Gif
}

internal sealed record ClipRequest(
    string Source,
    string Target,
    double StartSeconds,
    double DurationSeconds,
    ClipKind Kind,
    int Fps = ToolsOptions.DefaultGifFps,
    int Width = ToolsOptions.DefaultGifWidth);

internal sealed record ClipResult(bool Ok, string Target, string Error, TimeSpan Elapsed);

/// <summary>
/// Klip ve GIF cikarma. Surec cagrisi <see cref="FfmpegRunner"/> uzerinden yapilir:
/// stdout ve stderr ayni anda bosaltilir, cunku dolan boru ffmpeg'i kilitler.
/// </summary>
internal static class ClipExport
{
    internal static bool IsValid(ClipRequest request)
        => !string.IsNullOrWhiteSpace(request.Source)
           && !string.IsNullOrWhiteSpace(request.Target)
           && double.IsFinite(request.StartSeconds) && request.StartSeconds >= 0
           && double.IsFinite(request.DurationSeconds) && request.DurationSeconds > 0;

    internal static (double Start, double Duration) Range(double loopStart, double loopEnd, double position, double fallbackSeconds, double mediaDuration)
    {
        if (double.IsFinite(loopStart) && double.IsFinite(loopEnd) && loopEnd > loopStart)
            return (Math.Max(0, loopStart), loopEnd - Math.Max(0, loopStart));

        var start = double.IsFinite(position) && position > 0 ? position : 0;
        var length = fallbackSeconds;
        if (double.IsFinite(mediaDuration) && mediaDuration > 0) length = Math.Min(length, Math.Max(0, mediaDuration - start));
        return (start, length);
    }

    internal static IReadOnlyList<string> Arguments(ClipRequest request)
    {
        var start = Seconds(request.StartSeconds);
        var duration = Seconds(request.DurationSeconds);
        if (request.Kind == ClipKind.Gif)
            return new[]
            {
                "-y", "-hide_banner", "-nostdin",
                "-ss", start, "-t", duration, "-i", request.Source,
                "-vf", Filter(request.Fps, request.Width),
                "-loop", "0",
                request.Target
            };

        return new[]
        {
            "-y", "-hide_banner", "-nostdin",
            "-ss", start, "-i", request.Source, "-t", duration,
            "-map", "0", "-c", "copy", "-avoid_negative_ts", "make_zero",
            request.Target
        };
    }

    internal static string Filter(int fps, int width)
        => string.Concat(
            "fps=", fps.ToString(CultureInfo.InvariantCulture),
            ",scale=", width.ToString(CultureInfo.InvariantCulture),
            ":-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse");

    internal static async Task<ClipResult> RunAsync(ClipRequest request, CancellationToken ct = default)
    {
        if (!IsValid(request)) return new ClipResult(false, request.Target, "range", TimeSpan.Zero);

        try
        {
            var folder = Path.GetDirectoryName(request.Target);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ClipResult(false, request.Target, ex.Message, TimeSpan.Zero);
        }

        var run = await FfmpegRunner.RunAsync(Arguments(request), ct).ConfigureAwait(false);
        var written = run.Ok && File.Exists(request.Target) && new FileInfo(request.Target).Length > 0;
        if (!written && File.Exists(request.Target))
        {
            try { File.Delete(request.Target); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        return new ClipResult(written, request.Target, written ? "" : run.StandardError, run.Elapsed);
    }

    private static string Seconds(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
