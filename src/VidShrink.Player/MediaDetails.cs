namespace VidShrink.Player;

public sealed record MediaDetails(
    string? VideoCodec,
    int Width,
    int Height,
    double FramesPerSecond,
    double BitsPerSecond,
    string? AudioCodec,
    int AudioChannels,
    int AudioSampleRate);
