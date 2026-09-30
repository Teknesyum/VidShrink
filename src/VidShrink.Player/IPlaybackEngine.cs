namespace VidShrink.Player;

public enum SeekPrecision
{
    Exact,
    Keyframe
}

public enum SeekOutcome
{
    Shown,
    Failed,
    TimedOut,
    Superseded,
    Canceled
}

public enum HardwareDecoding
{
    Off,
    AutoCopy
}

public enum PlaybackTrackKind
{
    Video,
    Audio,
    Subtitle
}

public sealed record PlaybackTrack(long Id, PlaybackTrackKind Kind, string? Title, string? Language, bool External, bool Selected);

public sealed record MediaTags(string? Title, string? Artist, string? Album)
{
    public static readonly MediaTags Empty = new(null, null, null);

    public bool IsEmpty => Title is null && Artist is null && Album is null;
}

public readonly record struct SeekResult(SeekOutcome Outcome, double LatencyMs);

public sealed record PlaybackFault(string MessageKey, string? MessageArg = null);

public sealed record PlaybackOptions
{
    public HardwareDecoding Hardware { get; init; } = HardwareDecoding.Off;

    public TimeSpan OpenTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public TimeSpan SeekTimeout { get; init; } = TimeSpan.FromSeconds(3);

    public int RenderWidth { get; init; }

    public int RenderHeight { get; init; }

    public bool Audio { get; init; } = true;

    public bool Video { get; init; } = true;

    public bool Loop { get; init; }
}

public delegate void FrameCopy(IntPtr pixels, int width, int height, int stride);

public interface IPlaybackEngine : IDisposable
{
    string Name { get; }

    bool IsOpen { get; }

    double DurationSeconds { get; }

    bool HasAudio { get; }

    bool HasVideo => true;

    MediaTags Tags => MediaTags.Empty;

    bool IsPaused { get; }

    bool EndReached { get; }

    double PositionSeconds { get; }

    double AudioVideoOffsetSeconds { get; }

    long FramesRendered { get; }

    event EventHandler<PlaybackFault>? Faulted;

    Task OpenAsync(string path, CancellationToken ct = default);

    void Play();

    void Pause();

    Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default);

    bool TryCopyLatest(ref long seen, FrameCopy copy);

    bool TryCopyLatest(ref long seen, FrameCopy copy, out double frameSeconds)
    {
        frameSeconds = PositionSeconds;
        return TryCopyLatest(ref seen, copy);
    }

    double Speed => 1;

    double Volume => 100;

    bool Muted => false;

    double FramesPerSecond => double.NaN;

    double LoopStartSeconds => double.NaN;

    double LoopEndSeconds => double.NaN;

    void SetSpeed(double speed) { }

    void SetVolume(double volume) { }

    void SetMuted(bool muted) { }

    void StepFrame(bool backward) { }

    void SetLoop(double startSeconds, double endSeconds) { }

    IReadOnlyList<PlaybackTrack> Tracks => Array.Empty<PlaybackTrack>();

    long AudioTrack => 0;

    long SubtitleTrack => 0;

    double SubtitleDelaySeconds => 0;

    double AudioDelaySeconds => 0;

    double SubtitleScale => 1;

    double SubtitlePosition => 100;

    string SubtitleCodepage => "auto";

    void SetAudioTrack(long id) { }

    void SetSubtitleTrack(long id) { }

    bool AddSubtitle(string path) => false;

    /// <summary>
    /// Duzenleyicinin metin katmanini (<c>.ass</c>) dis altyazi izi olarak ekler ve secer;
    /// izin kimligini dondurur, eklenemezse 0.
    /// </summary>
    long AddOverlay(string path) => 0;

    /// <summary>Izi dosyadan yeniden okur (<c>sub-reload</c>); dosya yerinde yeniden yazildiktan sonra.</summary>
    bool ReloadOverlay(long id) => false;

    bool RemoveOverlay(long id) => false;

    /// <summary><c>sub-fonts-dir</c>; <c>null</c> varsayilana dondurur. Iz eklenmeden once verilir.</summary>
    void SetSubtitleFontsDir(string? directory) { }

    void SetSubtitleDelay(double seconds) { }

    void SetAudioDelay(double seconds) { }

    void SetSubtitleScale(double scale) { }

    void SetSubtitlePosition(double percent) { }

    void SetSubtitleCodepage(string codepage) { }

    int Rotation => 0;

    bool Mirrored => false;

    double AspectOverride => -1;

    bool RepeatFile => false;

    MediaDetails? Details => null;

    IReadOnlyList<double> ChapterTimes => Array.Empty<double>();

    void SetRotation(int degrees) { }

    void SetMirrored(bool mirrored) { }

    void SetAspectOverride(string ratio) { }

    void SetRepeatFile(bool repeat) { }

    Task<bool> SaveScreenshotAsync(string path, CancellationToken ct = default) => Task.FromResult(false);

    PictureAdjust Picture => PictureAdjust.Neutral;

    SoundAdjust Sound => SoundAdjust.Neutral;

    SubtitleStyle SubtitleLook => SubtitleStyle.Inherited;

    double VolumeCeiling => SoundAdjust.PlainCeiling;

    void SetPicture(PictureAdjust picture) { }

    void SetSound(SoundAdjust sound) { }

    void SetSubtitleStyle(SubtitleStyle style) { }

    IReadOnlyList<string> RecentLog => Array.Empty<string>();

    bool ReloadAudio() => false;

    Task<bool> ReopenAsync(double atSeconds, bool playing, CancellationToken ct = default) => Task.FromResult(false);
}

public sealed class PlaybackOpenException : Exception
{
    public PlaybackOpenException(string message) : base(message) { }
}

public sealed class PlaybackEngineUnavailableException : Exception
{
    public PlaybackEngineUnavailableException(string message, Exception? inner = null) : base(message, inner) { }

    public PlaybackEngineUnavailableException(string message, string messageKey, Exception? inner = null) : base(message, inner)
        => MessageKey = messageKey;

    /// <summary>
    /// Kullanıcıya gösterilecek iletinin anahtarı. Boşsa ileti motorun genel
    /// "kullanılamıyor" anahtarıyla yazılır.
    /// </summary>
    public string? MessageKey { get; }
}
