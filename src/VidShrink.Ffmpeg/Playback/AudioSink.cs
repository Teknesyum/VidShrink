using NAudio.Wave;

namespace VidShrink.Ffmpeg.Playback;

public sealed class AudioSink : IDisposable
{
    public static readonly WaveFormat Format = new(48_000, 16, 2);

    private readonly object _gate = new();
    private BufferedWaveProvider? _buffer;
    private IWavePlayer? _player;
    private long _bytesWritten;
    private bool _disposed;
    private string? _deviceFailureTr;
    private string? _deviceFailureEn;

    public AudioSink(bool hasAudio)
    {
        HasAudio = hasAudio;
        if (!hasAudio) return;

        try
        {
            _buffer = new BufferedWaveProvider(Format)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(5)
            };
            _player = new WaveOutEvent { DesiredLatency = 80 };
            _player.Init(_buffer);
        }
        catch (Exception ex)
        {
            HasAudio = false;
            _buffer = null;
            _player = null;
            _deviceFailureTr = $"Ses cihazi acilamadi, sessiz oynatiliyor: {ex.Message}";
            _deviceFailureEn = $"Audio device could not be opened, playing silently: {ex.Message}";
        }
    }

    public bool HasAudio { get; private set; }

    public (string Tr, string En)? DeviceFailure =>
        _deviceFailureTr is null ? null : (_deviceFailureTr, _deviceFailureEn!);

    internal static double BytesToSeconds(long bytes) => bytes / (double)Format.AverageBytesPerSecond;

    public double PositionSeconds
    {
        get
        {
            lock (_gate)
            {
                var bytes = Interlocked.Read(ref _bytesWritten) - (_buffer?.BufferedBytes ?? 0);
                if (bytes < 0) bytes = 0;
                return BytesToSeconds(bytes);
            }
        }
    }

    public void Write(ReadOnlySpan<byte> pcm)
    {
        if (!HasAudio || _buffer is null) return;
        lock (_gate)
        {
            _buffer.AddSamples(pcm.ToArray(), 0, pcm.Length);
            Interlocked.Add(ref _bytesWritten, pcm.Length);
        }
    }

    public void Play()
    {
        if (!HasAudio || _player is null) return;
        _player.Play();
    }

    public void Pause()
    {
        if (!HasAudio || _player is null) return;
        _player.Pause();
    }

    public void Reset()
    {
        if (!HasAudio || _buffer is null) return;
        lock (_gate)
        {
            _buffer.ClearBuffer();
            Interlocked.Exchange(ref _bytesWritten, 0);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _player?.Stop(); } catch { /* cikiste cihaz hatasi onemsiz */ }
        _player?.Dispose();
    }
}
