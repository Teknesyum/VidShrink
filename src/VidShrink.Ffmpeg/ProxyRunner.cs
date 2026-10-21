using VidShrink.Core;
using VidShrink.Core.Editing;

namespace VidShrink.Ffmpeg;

public static class ProxyRunner
{
    public const string Stage = "proxy";

    public static async Task<string> CreateAsync(string source, string folder, double durationSeconds, int width, int height,
        IProgress<EncodeProgress>? progress, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentException.ThrowIfNullOrEmpty(folder);
        var stamp = SourceStamp.Of(source) ?? throw new FileNotFoundException("Kaynak okunamadi", source);
        Directory.CreateDirectory(folder);
        var output = ProxyMedia.PathFor(folder, stamp);
        var partial = Path.Combine(folder, ProxyMedia.KeyOf(source) + "-partial-" + Guid.NewGuid().ToString("N") + ProxyMedia.Extension);
        try
        {
            await EncodeRunner.RunCommandAsync(ProxyMedia.Arguments(source, partial, width, height), Math.Max(0.001, durationSeconds), progress, Stage,
                0, 1, ct, priority: EncodePriority.BelowNormal).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            File.Move(partial, output, overwrite: true);
            ProxyMedia.DeleteStale(folder, source, output);
            return output;
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }
}
