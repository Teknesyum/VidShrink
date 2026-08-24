namespace VidShrink.Core.Playback;

/// <summary>
/// Sabit boylu kare tamponu havuzu. Havuz bostayken <b>ayirmaz</b> — <c>false</c> doner ve
/// cagiran yeri karar verir. Boy halkanin gozu kadardir, buyumez.
/// </summary>
/// <remarks>
/// T33/O6: sabit havuzla kare basina ayrilan bayt kare boyunun %0,5'i; kare basina yeni
/// tampon 720p'de %8 fps kaybettiriyor. Bu yuzden tavan sert.
/// </remarks>
public sealed class FramePool
{
    private readonly object _gate = new();
    private readonly Stack<PlaybackFrame> _free;
    private long _allocations;

    public FramePool(int capacity, int frameBytes)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (frameBytes < 1) throw new ArgumentOutOfRangeException(nameof(frameBytes));

        Capacity = capacity;
        FrameBytes = frameBytes;
        _free = new Stack<PlaybackFrame>(capacity);
    }

    /// <summary>Havuzun tampon sayisi tavani.</summary>
    public int Capacity { get; }

    /// <summary>Bir tamponun bayt boyu.</summary>
    public int FrameBytes { get; }

    /// <summary>
    /// Simdiye kadar gercekten ayrilan tampon sayisi. Isinma bittiginde
    /// <see cref="Capacity"/>'ye kadar cikar ve <b>bir daha artmaz</b>; kare basina ayirma
    /// olmadiginin olculebilir kaniti budur.
    /// </summary>
    public long Allocations => Interlocked.Read(ref _allocations);

    /// <summary>Su an kiralanabilir tampon sayisi.</summary>
    public int Available { get { lock (_gate) return _free.Count; } }

    /// <summary>Kiralanmis, henuz iade edilmemis tampon sayisi.</summary>
    public int Rented { get { lock (_gate) return (int)Interlocked.Read(ref _allocations) - _free.Count; } }

    /// <summary>
    /// Bir tampon kiralar. Bosta tampon yoksa ve tavan dolduysa <c>false</c> doner —
    /// bekleme de yok, ayirma da yok.
    /// </summary>
    public bool TryRent(out PlaybackFrame frame)
    {
        lock (_gate)
        {
            if (_free.Count > 0)
            {
                frame = _free.Pop();
                return true;
            }

            if (Interlocked.Read(ref _allocations) >= Capacity)
            {
                frame = null!;
                return false;
            }

            Interlocked.Increment(ref _allocations);
            frame = new PlaybackFrame(new byte[FrameBytes]);
            return true;
        }
    }

    /// <summary>Tamponu havuza geri verir. Yabanci boydaki tampon kabul edilmez.</summary>
    public void Return(PlaybackFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Buffer.Length != FrameBytes)
            throw new ArgumentException("Tampon bu havuzun kare boyunda degil.", nameof(frame));

        lock (_gate)
        {
            if (_free.Count >= Capacity) return;
            if (_free.Contains(frame)) return;
            _free.Push(frame);
        }
    }
}
