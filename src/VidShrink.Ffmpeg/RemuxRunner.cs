using VidShrink.Core;
using VidShrink.Core.Editing;

namespace VidShrink.Ffmpeg;

/// <summary>
/// <see cref="Remux"/> komutunu kosar. Cikti once ayni klasorde gecici ada yazilir ve basarida yerine tasinir; dusus ya da
/// iptal yarim dosyayi siler. Kaynagin ustune yazmak reddedilir.
/// </summary>
public static class RemuxRunner
{
    public const string Stage = "remux";

    public static async Task RunAsync(RemuxRequest request, double durationSeconds, IProgress<EncodeProgress>? progress, CancellationToken ct = default)
    {
        if (PathEquality.Same(request.Source, request.Output))
            throw new InvalidOperationException("The output must differ from the source.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(request.Output)) ?? Path.GetTempPath();
        var partial = Path.Combine(directory, "vidshrink_partial_" + Guid.NewGuid().ToString("N") + Path.GetExtension(request.Output));
        try
        {
            await EncodeRunner.RunCommandAsync(Remux.Arguments(request with { Output = partial }), Math.Max(0.001, durationSeconds), progress, Stage,
                0, 1, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            File.Move(partial, request.Output, overwrite: true);
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }
}
