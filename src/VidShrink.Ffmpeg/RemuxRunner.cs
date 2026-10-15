using VidShrink.Core;
using VidShrink.Core.Editing;

namespace VidShrink.Ffmpeg;

/// <summary>
/// <see cref="Remux"/> komutlarini kosar. Cikti once ayni klasorde gecici ada yazilir ve basarida yerine tasinir; dusus ya da
/// iptal yarim dosyayi siler. Girdilerden birinin ustune yazmak reddedilir.
/// </summary>
public static class RemuxRunner
{
    public const string Stage = "remux";

    public static Task RunAsync(RemuxRequest request, double durationSeconds, IProgress<EncodeProgress>? progress, CancellationToken ct = default)
    {
        var inputs = request.Tracks.Select(track => track.Source ?? request.Source).Append(request.Source);
        if (request.Cover is { } cover) inputs = inputs.Append(cover);
        return WriteAsync(inputs, request.Output, partial => Remux.Arguments(request with { Output = partial }), durationSeconds, progress, ct);
    }

    /// <summary><paramref name="track"/>'i yeniden kodlamadan <paramref name="output"/>'a tek iz olarak yazar.</summary>
    public static Task ExtractAsync(string source, RemuxTrack track, string output, double durationSeconds, IProgress<EncodeProgress>? progress,
        CancellationToken ct = default)
        => WriteAsync(new[] { track.Source ?? source }, output, partial => Remux.ExtractArguments(source, track, partial), durationSeconds, progress, ct);

    private static async Task WriteAsync(IEnumerable<string> inputs, string output, Func<string, IReadOnlyList<string>> arguments, double durationSeconds,
        IProgress<EncodeProgress>? progress, CancellationToken ct)
    {
        if (inputs.Any(input => PathEquality.Same(input, output)))
            throw new InvalidOperationException("The output must differ from the source.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(output)) ?? Path.GetTempPath();
        var partial = Path.Combine(directory, "vidshrink_partial_" + Guid.NewGuid().ToString("N") + Path.GetExtension(output));
        try
        {
            await EncodeRunner.RunCommandAsync(arguments(partial), Math.Max(0.001, durationSeconds), progress, Stage, 0, 1, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            File.Move(partial, output, overwrite: true);
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }
}
