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

    void SetSubtitleDelay(double seconds) { }

    void SetAudioDelay(double seconds) { }

    void SetSubtitleScale(double scale) { }

    void SetSubtitlePosition(double percent) { }

    void SetSubtitleCodepage(string codepage) { }
}

public sealed class PlaybackOpenException : Exception
{
    public PlaybackOpenException(string message) : base(message) { }
}

public sealed class PlaybackEngineUnavailableException : Exception
{
    public PlaybackEngineUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
