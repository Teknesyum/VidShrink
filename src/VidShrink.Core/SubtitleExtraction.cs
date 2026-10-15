namespace VidShrink.Core;

/// <summary>
/// Secilen metin altyazi izini tek basina bir altyazi dosyasina yazan kisa komut. Resim altyazi
/// cikarilmaz: <see cref="Extension"/> ona <c>null</c> doner.
/// </summary>
public static class SubtitleExtraction
{
    public static string? Extension(string codec) => codec.ToLowerInvariant() switch
    {
        "subrip" or "srt" or "mov_text" or "text" => ".srt",
        "ass" or "ssa" => ".ass",
        "webvtt" => ".vtt",
        _ => null
    };

    public static SourceStream? Track(MediaInfo info, int ordinal)
        => info.Streams.Where(stream => stream.Kind == StreamKind.Subtitle).ElementAtOrDefault(ordinal);

    public static IReadOnlyList<string> Build(MediaInfo info, int ordinal, string outputPath)
    {
        if (Track(info, ordinal) is not { } track)
            throw new ArgumentOutOfRangeException(nameof(ordinal), $"The source has no subtitle track {ordinal}.");
        if (Extension(track.Codec) is null)
            throw new ArgumentException($"The {track.Codec} subtitle track is not text and cannot be extracted.", nameof(ordinal));
        var codec = track.Codec.ToLowerInvariant() is "mov_text" or "text" ? "srt" : "copy";
        return new[]
        {
            "-hide_banner", "-y", "-i", info.FilePath,
            "-map", "0:" + track.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-c:s", codec, outputPath
        };
    }
}
