namespace VidShrink.Core.Playback;

/// <summary>
/// Uretici ile tuketici arasindaki cok gozlu halka. Dolduysa <b>en eski kare duser</b>,
/// uretici beklemez — canli oynatmada gecikme birikmesi kare dusmekten kotudur.
/// </summary>
/// <remarks>
/// T37: tek gozlu tamponda beslenen 361 karenin 15'i (%4) dusmustu. En az uc goz bunu kapatir.
/// Dusen kareler havuza iade edilir, cope gitmez.
/// </remarks>
public sealed class FrameRing
{
    /// <summary>Halkanin en az goz sayisi.</summary>
    public const int MinimumCapacity = 3;

    private readonly object _gate = new();
    private readonly PlaybackFrame?[] _slots;
    private readonly FramePool? _pool;
    private int _head;
    private int _count;
    private long _dropped;

    public FrameRing(int capacity, FramePool? pool = null)
    {
        if (capacity < MinimumCapacity)
            throw new ArgumentOutOfRangeException(nameof(capacity), $"Halka en az {MinimumCapacity} gozlu olmali.");

        _slots = new PlaybackFrame?[capacity];
        _pool = pool;
    }

    public int Capacity => _slots.Length;

    public int Count { get { lock (_gate) return _count; } }

    /// <summary>Dusen kare sayisi: halka doldugu icin ve bayatladigi icin dusenlerin toplami.</summary>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <summary>
    /// Yeni kareyi halkaya koyar. Halka doluysa en eskiyi dusurur ve havuza iade eder.
    /// </summary>
    public void Publish(PlaybackFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        lock (_gate)
        {
            if (_count == _slots.Length)
            {
                var oldest = _slots[_head]!;
                _slots[_head] = null;
                _head = (_head + 1) % _slots.Length;
                _count--;
                Interlocked.Increment(ref _dropped);
                _pool?.Return(oldest);
            }

            _slots[(_head + _count) % _slots.Length] = frame;
            _count++;
        }
    }

    /// <summary>
    /// Sirada bekleyen <b>en eski</b> kareyi verir. Halka bossa <c>false</c> doner.
    /// </summary>
    /// <remarks>
    /// Bekleyen kare bayat degil, sirada olandir: uretici gercek zamanli hizda besleniyor,
    /// yani her kare kendi aninda gosterilecek. Bekleyeni atip en yeniyi vermek 60 fps'lik
    /// beslemede kareyi bosuna dusuruyordu. Gecikme birikmesini <see cref="Publish"/>
    /// engelliyor: halka dolunca en eski duser, yani gecikme halkanin gozu kadar tavanli.
    /// </remarks>
    public bool TryTake(out PlaybackFrame frame)
    {
        lock (_gate)
        {
            if (_count == 0)
            {
                frame = null!;
                return false;
            }

            frame = _slots[_head]!;
            _slots[_head] = null;
            _head = (_head + 1) % _slots.Length;
            _count--;
            return true;
        }
    }

    /// <summary>
    /// En eski kareyi dusurup cagirana verir. Uretici havuz bosaldiginda tamponu buradan
    /// geri kazanir; kare dusmus sayilir.
    /// </summary>
    public bool TryEvictOldest(out PlaybackFrame frame)
    {
        lock (_gate)
        {
            if (_count == 0)
            {
                frame = null!;
                return false;
            }

            frame = _slots[_head]!;
            _slots[_head] = null;
            _head = (_head + 1) % _slots.Length;
            _count--;
            Interlocked.Increment(ref _dropped);
            return true;
        }
    }

    /// <summary>Halkayi bosaltir ve tamponlari havuza iade eder. Dusen sayisina eklenmez.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            for (var i = 0; i < _count; i++)
            {
                var index = (_head + i) % _slots.Length;
                var frame = _slots[index];
                _slots[index] = null;
                if (frame is not null) _pool?.Return(frame);
            }
            _head = 0;
            _count = 0;
        }
    }

    /// <summary>Sayaci sifirlar. Olcum ve atlama sonrasi icin.</summary>
    public void ResetCounters() => Interlocked.Exchange(ref _dropped, 0);
}
