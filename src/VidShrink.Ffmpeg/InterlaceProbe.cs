using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public static class InterlaceProbe
{
    public const int ProbeFrames = 300;
    public const double SkipShare = 0.1;
    public const double SkipFromSeconds = 20;

    public static IReadOnlyList<string> Arguments(MediaInfo info)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-nostats" };
        if (info.DurationSeconds >= SkipFromSeconds)
            args.AddRange(new[] { "-ss", (info.DurationSeconds * SkipShare).ToString("0.###", CultureInfo.InvariantCulture) });
        args.AddRange(new[]
        {
            "-i", info.FilePath, "-map", "0:v:0", "-frames:v", ProbeFrames.ToString(CultureInfo.InvariantCulture),
            "-vf", "idet", "-an", "-sn", "-dn", "-f", "null", "-"
        });
        return args;
    }

    public static async Task<IdetCounts?> RunAsync(MediaInfo info, CancellationToken ct = default)
    {
        var run = await ProbeProcess.RunAsync(Arguments(info), ct);
        return run.ExitCode == 0 ? IdetCounts.Parse(run.StandardError) : null;
    }

    public static async Task<VideoFilterOptions> ResolveAsync(MediaInfo info, VideoFilterOptions options, CancellationToken ct = default)
    {
        if (!VideoFilterChain.NeedsInterlaceProbe(info, options))
            return VideoFilterChain.ResolveInterlace(options, info, null);
        return VideoFilterChain.ResolveInterlace(options, info, await RunAsync(info, ct));
    }
}

internal static class ProbeProcess
{
    internal static async Task<(int ExitCode, string StandardError)> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Process process;
        try
        {
            process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, arguments) };
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }

        using (process)
        using (ct.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { } }))
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(CancellationToken.None);
            _ = await stdout;
            var text = await stderr;
            ct.ThrowIfCancellationRequested();
            return (process.ExitCode, text);
        }
    }
}
