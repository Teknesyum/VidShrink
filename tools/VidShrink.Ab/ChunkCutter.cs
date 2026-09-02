using System.Diagnostics;
using System.Globalization;

namespace VidShrink.Ab;

public sealed record ChunkSpec(string Name, string StartTimecode, double DurationSeconds);

public static class ChunkCutter
{
    public static IReadOnlyList<ChunkSpec> Specs { get; } = new[]
    {
        new ChunkSpec("parca-1", "00:02:00", 60),
        new ChunkSpec("parca-2", "00:07:30", 60),
        new ChunkSpec("parca-3", "00:13:00", 60)
    };

    public static async Task<IReadOnlyList<string>> EnsureAsync(string sourcePath, string chunkDirectory, TextWriter log, CancellationToken ct)
    {
        Directory.CreateDirectory(chunkDirectory);
        var paths = new List<string>();
        foreach (var spec in Specs)
        {
            var path = Path.Combine(chunkDirectory, spec.Name + ".mkv");
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                log.WriteLine($"parça hazır: {path}");
                paths.Add(path);
                continue;
            }
            log.WriteLine($"parça kesiliyor: {path} ({spec.StartTimecode}, {spec.DurationSeconds:0} sn, -c copy)");
            await CutAsync(sourcePath, path, spec, log, ct);
            paths.Add(path);
        }
        return paths;
    }

    private static async Task CutAsync(string sourcePath, string outputPath, ChunkSpec spec, TextWriter log, CancellationToken ct)
    {
        var args = new[]
        {
            "-hide_banner", "-nostdin", "-y",
            "-ss", spec.StartTimecode,
            "-i", sourcePath,
            "-t", spec.DurationSeconds.ToString(CultureInfo.InvariantCulture),
            "-map", "0:v:0",
            "-c", "copy",
            outputPath
        };
        log.WriteLine("komut: ffmpeg " + string.Join(' ', args.Select(Quote)));

        using var process = new Process { StartInfo = ProcessLauncher.StartInfo("ffmpeg", args) };
        process.Start();
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        log.WriteLine(stderr);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Parça kesilemedi ({outputPath}); ffmpeg {process.ExitCode} verdi.");
    }

    private static string Quote(string value)
        => value.Contains(' ') ? "\"" + value + "\"" : value;
}
