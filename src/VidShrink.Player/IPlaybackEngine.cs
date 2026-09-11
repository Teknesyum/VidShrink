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
}

public sealed class PlaybackOpenException : Exception
{
    public PlaybackOpenException(string message) : base(message) { }
}

public sealed class PlaybackEngineUnavailableException : Exception
{
    public PlaybackEngineUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
