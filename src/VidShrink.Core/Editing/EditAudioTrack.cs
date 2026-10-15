namespace VidShrink.Core.Editing;

/// <summary>
/// Teslime giden ses izi. Sira kaynagin ses akislari icindeki siradir (<c>0:a:N</c>); <see cref="None"/> sesi
/// dusurur, <see cref="All"/> (varsayilan) butun ses izlerini ve kabin tasiyabildigi altyazi izlerini tasir.
/// Aralik disindaki sira ilk ize doner. Goruntu hep ilk video izinden; tek iz secilince altyazi teslime gitmez.
/// </summary>
public static class EditAudioTrack
{
    public const int All = -2;
    public const int None = -1;
    public const int First = 0;
    public const int Default = All;

    public static IReadOnlyList<SourceStream> Streams(MediaInfo info)
        => info.Streams.Where(stream => stream.Kind == StreamKind.Audio).ToArray();

    public static int Resolve(MediaInfo info, int track)
    {
        if (track == None || !info.HasAudio) return None;
        if (track == All) return Streams(info).Count > 1 ? All : First;
        return track > 0 && track < Streams(info).Count ? track : First;
    }

    /// <summary>Teslime giden ses akislarinin kaynaktaki siralari; ses yoksa bos.</summary>
    public static IReadOnlyList<int> Tracks(MediaInfo info, int track)
        => Resolve(info, track) switch
        {
            None => Array.Empty<int>(),
            All => Enumerable.Range(0, Streams(info).Count).ToArray(),
            var one => new[] { one }
        };

    public static MediaInfo Select(MediaInfo info, int track)
    {
        var resolved = Resolve(info, track);
        if (resolved == None) return info with { AudioCodec = null, AudioBitrateBps = 0, AudioChannels = 0 };
        if (resolved is First or All) return info;
        var stream = Streams(info)[resolved];
        return info with { AudioCodec = stream.Codec, AudioBitrateBps = stream.BitrateBps, AudioChannels = stream.Channels };
    }

    public static string? Language(MediaInfo info, int track)
        => Resolve(info, track) is var resolved && resolved >= 0
            ? Streams(info).ElementAtOrDefault(resolved)?.Language
            : null;
}
