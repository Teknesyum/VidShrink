namespace VidShrink.Core.Editing;

/// <summary>Kaynaklarin concat girisiyle uc uca kopyalanmasini engelleyen ilk fark; <see cref="None"/> uyumlu.</summary>
public enum MergeMismatch
{
    None,
    Unsupported,
    VideoCodec,
    VideoProfile,
    Resolution,
    PixelAspect,
    PixelFormat,
    FrameRate,
    TimeBase,
    AudioPresence,
    AudioCodec,
    SampleRate,
    Channels,
    StreamLayout
}

/// <summary>Cok kaynakli teslimin karari: kopya ya da bugunku yeniden kodlama ve nedeni.</summary>
public enum MergeDecision
{
    Copy,
    FullRequested,
    Edits,
    Settings,
    AudioTrack,
    NoKeyframes,
    CutOffKeyframe,
    Incompatible
}

/// <summary>
/// Kayipsiz birlestirme kapisi. Butun kaynaklar ayni kodek, profil, cozunurluk, piksel orani, piksel bicimi,
/// kare hizi, zaman tabani, ayni ses (kodek, ornekleme hizi, kanal) ve ayni akis dizilisi tasiyorsa ve cizelge yeniden kodlama
/// istemiyorsa parcalar ffmpeg'in concat girisiyle <c>-c copy</c> kopyalanir; yoksa bugunku yeniden kodlama.
/// Okunamayan piksel bicimi, zaman tabani ya da ornekleme hizi uyumsuz sayilir.
/// </summary>
public static class MergeCompatibility
{
    public const double FrameRateTolerance = 1e-3;

    public static MergeMismatch Check(IReadOnlyList<MediaInfo> sources)
    {
        if (sources.Count == 0) return MergeMismatch.Unsupported;
        foreach (var info in sources)
            if (info.IsAudioOnly || info.IsGifSource || info.Sequence is not null) return MergeMismatch.Unsupported;
        var first = sources[0];
        if (first.PixelFormat is null) return MergeMismatch.PixelFormat;
        if (first.VideoTimeBase is null) return MergeMismatch.TimeBase;
        if (first.HasAudio && SampleRate(first) <= 0) return MergeMismatch.SampleRate;
        for (var i = 1; i < sources.Count; i++)
        {
            var mismatch = Compare(first, sources[i]);
            if (mismatch != MergeMismatch.None) return mismatch;
        }

        return MergeMismatch.None;
    }

    public static MergeDecision Decide(
        EditTimeline timeline, IReadOnlyDictionary<int, ExportSource> sources, ExportMode mode,
        EditExportSettings settings, int audioTrack)
    {
        if (mode == ExportMode.Full) return MergeDecision.FullRequested;
        if (timeline.HasText || timeline.HasEffects || timeline.HasTransitions || timeline.HasMusic || timeline.Clips.Any(EditExport.NeedsReencode))
            return MergeDecision.Edits;
        if (!settings.IsDefault) return MergeDecision.Settings;

        var used = EditExport.UsedSources(timeline);
        if (used.Any(s => !sources.ContainsKey(s))) throw new ArgumentException("Parcanin kaynagi okunmamis", nameof(sources));
        if (used.Any(s => sources[s].Keyframes.Count == 0)) return MergeDecision.NoKeyframes;
        if (mode == ExportMode.Smart && timeline.Clips.Any(c => !OnKeyframe(sources[c.Source].Keyframes, EditTime.ToSeconds(c.SourceStart))))
            return MergeDecision.CutOffKeyframe;
        return Check(used.Select(s => sources[s].Info).ToArray()) == MergeMismatch.None ? MergeDecision.Copy : MergeDecision.Incompatible;
    }

    private static MergeMismatch Compare(MediaInfo a, MediaInfo b)
    {
        if (!Same(a.VideoCodec, b.VideoCodec)) return MergeMismatch.VideoCodec;
        if (!Same(a.VideoProfile, b.VideoProfile)) return MergeMismatch.VideoProfile;
        if (a.Width != b.Width || a.Height != b.Height) return MergeMismatch.Resolution;
        if ((long)a.ParNum * b.ParDen != (long)b.ParNum * a.ParDen) return MergeMismatch.PixelAspect;
        if (b.PixelFormat is null || !Same(a.PixelFormat, b.PixelFormat)) return MergeMismatch.PixelFormat;
        if (Math.Abs(a.Fps - b.Fps) > FrameRateTolerance) return MergeMismatch.FrameRate;
        if (b.VideoTimeBase is null || !Same(a.VideoTimeBase, b.VideoTimeBase)) return MergeMismatch.TimeBase;
        if (a.HasAudio != b.HasAudio) return MergeMismatch.AudioPresence;
        if (!a.HasAudio) return Layout(a).SequenceEqual(Layout(b)) ? MergeMismatch.None : MergeMismatch.StreamLayout;
        if (!Same(a.AudioCodec, b.AudioCodec)) return MergeMismatch.AudioCodec;
        if (SampleRate(b) <= 0 || SampleRate(a) != SampleRate(b)) return MergeMismatch.SampleRate;
        if (a.AudioChannels != b.AudioChannels) return MergeMismatch.Channels;
        return Layout(a).SequenceEqual(Layout(b)) ? MergeMismatch.None : MergeMismatch.StreamLayout;
    }

    private static bool OnKeyframe(IReadOnlyList<double> keyframes, double at)
        => keyframes.Any(k => Math.Abs(k - at) < EditExport.KeyframeLeadNotice);

    private static IEnumerable<StreamKind> Layout(MediaInfo info) => info.Streams.Select(s => s.Kind);

    private static int SampleRate(MediaInfo info)
        => info.Streams.FirstOrDefault(s => s.Kind == StreamKind.Audio)?.SampleRate ?? 0;

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
