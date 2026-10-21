using VidShrink.Core.Editing;

namespace VidShrink.Ffmpeg;

public static class ColorProbe
{
    public static async Task<ColorStats?> RunAsync(string path, double start, double length, string colorChain, CancellationToken ct = default)
    {
        var run = await ProbeProcess.RunAsync(ClipMatch.ProbeArguments(path, start, length, colorChain), ct);
        return run.ExitCode == 0 ? ClipMatch.Parse(run.StandardError) : null;
    }
}
