using System.Globalization;

namespace VidShrink.Core;

public enum StreamKind { Video, Audio, Subtitle, Attachment, Data }

public enum OutputContainer { Mp4, Mkv, WebM }

public enum TrackAction { Encode, Copy }

public enum StreamNote
{
    AudioPassthrough,
    AudioDownmixedToStereo,
    ExtraAudioDropped,
    TextSubtitleConverted,
    ImageSubtitleDropped,
    SubtitleDroppedForPlatform,
    KeepAllTracksOverriddenByPlatform,
    LosslessAudioNotPassedThrough
}

public sealed record SourceStream(
    int Index,
    StreamKind Kind,
    string Codec,
    string? Language = null,
    bool IsDefault = false,
    bool IsForced = false,
    int Channels = 0,
    long BitrateBps = 0,
    long Bytes = 0,
    bool IsAttachedPicture = false);

public sealed record StreamRequest(bool KeepAllTracks = false, bool PlatformDelivery = false, string? PreferredLanguage = null)
{
    public static StreamRequest Default { get; } = new();
}

public sealed record AudioTrack(string Map, TrackAction Action, string Codec, int BitrateK, int? Channels, string? Language)
{
    public bool Copies => Action == TrackAction.Copy;
}

public sealed record SubtitleTrack(string Map, string Codec, bool Image, string? Language, long Bytes);

public sealed record StreamPlan(
    OutputContainer Container,
    string VideoMap,
    IReadOnlyList<AudioTrack> Audio,
    IReadOnlyList<SubtitleTrack> Subtitles,
    IReadOnlyList<string> Attachments,
    IReadOnlyList<StreamNote> Notes,
    double SideK,
    StreamRequest Request)
{
    public string Extension => StreamMapping.ExtensionOf(Container);

    public IReadOnlyList<string> OutputArguments(bool dropChapters = false)
    {
        var a = new List<string> { "-map", VideoMap };
        foreach (var track in Audio) a.AddRange(new[] { "-map", track.Map });
        foreach (var track in Subtitles) a.AddRange(new[] { "-map", track.Map });
        foreach (var map in Attachments) a.AddRange(new[] { "-map", map });

        if (Audio.Count == 0)
            a.Add("-an");
        else if (Audio.Count == 1)
            a.AddRange(AudioCodecArguments(Audio[0], ""));
        else
            for (var i = 0; i < Audio.Count; i++)
                a.AddRange(AudioCodecArguments(Audio[i], ":" + i.ToString(CultureInfo.InvariantCulture)));

        if (Subtitles.Count > 0)
        {
            if (Subtitles.Select(track => track.Codec).Distinct().Count() == 1)
                a.AddRange(new[] { "-c:s", Subtitles[0].Codec });
            else
                for (var i = 0; i < Subtitles.Count; i++)
                    a.AddRange(new[] { "-c:s:" + i.ToString(CultureInfo.InvariantCulture), Subtitles[i].Codec });
        }

        if (Attachments.Count > 0) a.AddRange(new[] { "-c:t", "copy" });
        if (!Request.KeepAllTracks && Audio.Count == 1) a.AddRange(new[] { "-disposition:a:0", "default" });
        a.AddRange(new[] { "-map_metadata", "0", "-map_chapters", dropChapters ? "-1" : "0" });
        return a;
    }

    private static IEnumerable<string> AudioCodecArguments(AudioTrack track, string suffix)
    {
        if (track.Copies)
            return new[] { "-c:a" + suffix, "copy" };
        var a = new List<string> { "-c:a" + suffix, track.Codec, "-b:a" + suffix, track.BitrateK.ToString(CultureInfo.InvariantCulture) + "k" };
        if (track.Channels is > 0) a.AddRange(new[] { suffix.Length == 0 ? "-ac" : "-ac:a" + suffix, track.Channels.Value.ToString(CultureInfo.InvariantCulture) });
        return a;
    }
}

public static class StreamMapping
{
    public const double PassthroughTargetShare = 0.15;
    public const int MinimumTrackK = 24;

    private static readonly string[] TextSubtitleCodecs = { "subrip", "srt", "ass", "ssa", "mov_text", "webvtt", "text" };
    private static readonly string[] ImageSubtitleCodecs = { "hdmv_pgs_subtitle", "dvd_subtitle", "dvb_subtitle", "xsub" };
    private static readonly string[] NeverPassedThrough = { "truehd", "mlp", "dts" };

    private static readonly IReadOnlyDictionary<OutputContainer, string[]> CopyableAudio = new Dictionary<OutputContainer, string[]>
    {
        [OutputContainer.Mp4] = new[] { "aac", "ac3", "eac3", "opus", "mp3", "flac" },
        [OutputContainer.Mkv] = new[] { "aac", "ac3", "eac3", "opus", "mp3", "flac", "vorbis" },
        [OutputContainer.WebM] = new[] { "opus", "vorbis" }
    };

    private static readonly IReadOnlyDictionary<string, string[]> LanguageCodes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["ar"] = new[] { "ara" }, ["bg"] = new[] { "bul" }, ["bn"] = new[] { "ben" }, ["cs"] = new[] { "ces", "cze" },
        ["da"] = new[] { "dan" }, ["de"] = new[] { "deu", "ger" }, ["el"] = new[] { "ell", "gre" }, ["en"] = new[] { "eng" },
        ["es"] = new[] { "spa" }, ["et"] = new[] { "est" }, ["fa"] = new[] { "fas", "per" }, ["fi"] = new[] { "fin" },
        ["fr"] = new[] { "fra", "fre" }, ["he"] = new[] { "heb" }, ["hi"] = new[] { "hin" }, ["hr"] = new[] { "hrv" },
        ["hu"] = new[] { "hun" }, ["id"] = new[] { "ind" }, ["it"] = new[] { "ita" }, ["ja"] = new[] { "jpn" },
        ["ko"] = new[] { "kor" }, ["lt"] = new[] { "lit" }, ["lv"] = new[] { "lav" }, ["ms"] = new[] { "msa", "may" },
        ["nb"] = new[] { "nob", "nor" }, ["nl"] = new[] { "nld", "dut" }, ["pl"] = new[] { "pol" }, ["pt"] = new[] { "por" },
        ["ro"] = new[] { "ron", "rum" }, ["ru"] = new[] { "rus" }, ["sk"] = new[] { "slk", "slo" }, ["sl"] = new[] { "slv" },
        ["sr"] = new[] { "srp" }, ["sv"] = new[] { "swe" }, ["sw"] = new[] { "swa" }, ["ta"] = new[] { "tam" },
        ["th"] = new[] { "tha" }, ["tr"] = new[] { "tur" }, ["uk"] = new[] { "ukr" }, ["ur"] = new[] { "urd" },
        ["vi"] = new[] { "vie" }, ["zh"] = new[] { "zho", "chi" }
    };

    public static OutputContainer ContainerFor(StreamRequest request)
        => request.KeepAllTracks && !request.PlatformDelivery ? OutputContainer.Mkv : OutputContainer.Mp4;

    public static OutputContainer ContainerOf(string outputPath) => Path.GetExtension(outputPath).ToLowerInvariant() switch
    {
        ".mkv" => OutputContainer.Mkv,
        ".webm" => OutputContainer.WebM,
        _ => OutputContainer.Mp4
    };

    public static string ExtensionOf(OutputContainer container) => container switch
    {
        OutputContainer.Mkv => "mkv",
        OutputContainer.WebM => "webm",
        _ => "mp4"
    };

    public static bool IsTextSubtitle(string codec) => TextSubtitleCodecs.Contains(codec, StringComparer.OrdinalIgnoreCase);

    public static bool IsImageSubtitle(string codec) => ImageSubtitleCodecs.Contains(codec, StringComparer.OrdinalIgnoreCase);

    public static bool LanguageMatches(string? streamLanguage, string? preferred)
    {
        if (string.IsNullOrWhiteSpace(streamLanguage) || string.IsNullOrWhiteSpace(preferred)) return false;
        var primary = preferred.Split('-', '_')[0];
        if (streamLanguage.Equals(primary, StringComparison.OrdinalIgnoreCase)) return true;
        return LanguageCodes.TryGetValue(primary, out var codes) && codes.Contains(streamLanguage, StringComparer.OrdinalIgnoreCase);
    }

    public static SourceStream? PrimaryAudio(IReadOnlyList<SourceStream> streams, string? preferredLanguage)
    {
        var audio = streams.Where(stream => stream.Kind == StreamKind.Audio).ToList();
        if (audio.Count == 0) return null;
        var inLanguage = audio.Where(stream => LanguageMatches(stream.Language, preferredLanguage)).ToList();
        if (inLanguage.Count > 0) return inLanguage.FirstOrDefault(stream => stream.IsDefault) ?? inLanguage[0];
        return audio.FirstOrDefault(stream => stream.IsDefault) ?? audio[0];
    }

    public static StreamPlan Decide(
        MediaInfo info,
        StreamRequest request,
        OutputContainer container,
        int audioK,
        int? audioChannels,
        string? audioCodec,
        bool allowPassthrough,
        double targetMb)
    {
        var notes = new List<StreamNote>();
        var duration = Math.Max(info.DurationSeconds, 0.1);
        var inventory = info.Streams.Count > 0;

        if (request.KeepAllTracks && request.PlatformDelivery)
            notes.Add(StreamNote.KeepAllTracksOverriddenByPlatform);
        var keepAll = request.KeepAllTracks && !request.PlatformDelivery && container != OutputContainer.Mp4;

        var video = info.Streams.FirstOrDefault(stream => stream.Kind == StreamKind.Video && !stream.IsAttachedPicture);
        var videoMap = video is null ? "0:v:0" : Map(video);

        var audio = new List<AudioTrack>();
        if (audioCodec is not null && audioK > 0 && (info.HasAudio || !inventory))
        {
            var sources = inventory
                ? (keepAll
                    ? info.Streams.Where(stream => stream.Kind == StreamKind.Audio).ToList()
                    : new List<SourceStream> { PrimaryAudio(info.Streams, request.PreferredLanguage)! })
                : new List<SourceStream> { new(-1, StreamKind.Audio, info.AudioCodec ?? "", Channels: info.AudioChannels, BitrateBps: info.AudioBitrateBps) };

            if (!keepAll && inventory && info.Streams.Count(stream => stream.Kind == StreamKind.Audio) > 1)
                notes.Add(StreamNote.ExtraAudioDropped);

            var passthroughBudgetK = targetMb > 0 ? targetMb * PassthroughTargetShare * 8388.608 / duration : 0;
            var passedK = 0.0;
            foreach (var source in sources)
            {
                var map = source.Index < 0 ? "0:a:0?" : Map(source);
                var sourceK = source.BitrateBps / 1000.0;
                if (audioCodec == "copy")
                {
                    audio.Add(new AudioTrack(map, TrackAction.Copy, "copy", (int)Math.Round(sourceK > 0 ? sourceK : audioK), null, source.Language));
                    continue;
                }

                var lossless = NeverPassedThrough.Contains(source.Codec, StringComparer.OrdinalIgnoreCase);
                if (lossless && allowPassthrough) notes.Add(StreamNote.LosslessAudioNotPassedThrough);
                var copyable = allowPassthrough
                    && inventory
                    && !lossless
                    && CopyableAudio[container].Contains(source.Codec, StringComparer.OrdinalIgnoreCase)
                    && sourceK > 0
                    && sourceK <= audioK
                    && passedK + sourceK <= passthroughBudgetK;

                if (copyable)
                {
                    passedK += sourceK;
                    audio.Add(new AudioTrack(map, TrackAction.Copy, "copy", (int)Math.Round(sourceK), null, source.Language));
                    notes.Add(StreamNote.AudioPassthrough);
                    continue;
                }

                var channels = audioChannels;
                if (channels is null && source.Channels > 2)
                {
                    channels = 2;
                    notes.Add(StreamNote.AudioDownmixedToStereo);
                }
                var codec = audioCodec;
                if (container != OutputContainer.Mp4 && codec == "aac") codec = "libopus";
                audio.Add(new AudioTrack(map, TrackAction.Encode, codec, audioK, channels, source.Language));
            }
        }

        var subtitles = new List<SubtitleTrack>();
        foreach (var source in info.Streams.Where(stream => stream.Kind == StreamKind.Subtitle))
        {
            if (request.PlatformDelivery)
            {
                notes.Add(StreamNote.SubtitleDroppedForPlatform);
                continue;
            }

            var text = IsTextSubtitle(source.Codec);
            var image = IsImageSubtitle(source.Codec);
            if (container == OutputContainer.Mp4)
            {
                if (text)
                {
                    subtitles.Add(new SubtitleTrack(Map(source), "mov_text", false, source.Language, source.Bytes));
                    if (!source.Codec.Equals("mov_text", StringComparison.OrdinalIgnoreCase)) notes.Add(StreamNote.TextSubtitleConverted);
                }
                else if (image) notes.Add(StreamNote.ImageSubtitleDropped);
                continue;
            }

            if (container == OutputContainer.WebM)
            {
                if (text) subtitles.Add(new SubtitleTrack(Map(source), "webvtt", false, source.Language, source.Bytes));
                else if (image) notes.Add(StreamNote.ImageSubtitleDropped);
                continue;
            }

            if (text)
            {
                var mov = source.Codec.Equals("mov_text", StringComparison.OrdinalIgnoreCase);
                subtitles.Add(new SubtitleTrack(Map(source), mov ? "srt" : "copy", false, source.Language, source.Bytes));
                if (mov) notes.Add(StreamNote.TextSubtitleConverted);
            }
            else if (image)
                subtitles.Add(new SubtitleTrack(Map(source), "copy", true, source.Language, source.Bytes));
        }

        var attachments = new List<string>();
        var attachmentBytes = 0L;
        if (keepAll && container == OutputContainer.Mkv)
            foreach (var source in info.Streams.Where(stream => stream.Kind == StreamKind.Attachment))
            {
                attachments.Add(Map(source));
                attachmentBytes += source.Bytes;
            }

        var subtitleBytes = subtitles.Sum(track => track.Bytes);
        var sideK = audio.Sum(track => (double)track.BitrateK) + (subtitleBytes + attachmentBytes) * 8.0 / 1000.0 / duration;

        return new StreamPlan(container, videoMap, audio, subtitles, attachments, notes.Distinct().ToList(), sideK, request);
    }

    public static StreamPlan ForOutput(MediaInfo info, EncodePlan plan, string outputPath)
    {
        var container = ContainerOf(outputPath);
        if (plan.Streams is { } streams && streams.Container == container) return streams;
        var request = plan.Streams?.Request ?? StreamRequest.Default;
        var planned = plan.Streams is not null;
        var codec = plan.AudioCodec;
        if (planned && codec is "copy" or "libopus") codec = "aac";
        return Decide(info, request, container, plan.AudioBitrateK, plan.AudioChannels, codec, planned, plan.EffectiveTargetMb ?? 0);
    }

    private static string Map(SourceStream stream) => "0:" + stream.Index.ToString(CultureInfo.InvariantCulture);
}
