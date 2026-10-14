using VidShrink.Core;

namespace VidShrink.Ffmpeg;

/// <summary>ARC-P1: <see cref="LoudnessReport.Arguments"/>'i tek surecle kosturur; iptal sureci oldurur.</summary>
public static class LoudnessProbe
{
    public static async Task<LoudnessReport?> RunAsync(string path, CancellationToken ct = default)
    {
        var run = await ProbeProcess.RunAsync(LoudnessReport.Arguments(path), ct);
        return run.ExitCode == 0 ? LoudnessReport.Parse(run.StandardError) : null;
    }
}
