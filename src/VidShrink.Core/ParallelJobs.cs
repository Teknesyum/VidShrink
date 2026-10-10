namespace VidShrink.Core;

/// <summary>
/// K6: kuyrukta aynı anda kodlanan iş sayısının kararları. Saf; saat, disk ve süreç okumaz.
/// Varsayılan 1'dir ve bugünkü davranıştır: işler sırayla koşar.
/// </summary>
public static class ParallelJobs
{
    public const int Default = 1;

    /// <summary>Çekirdek sayısı ne olursa olsun aşılmayan tavan. Ötesi ölçülmedi.</summary>
    public const int Ceiling = 4;

    /// <summary>
    /// Donanım kodlayıcısında aynı anda koşan iş. NVENC'in tüketici kartlarındaki oturum sınırı
    /// sürücüye göre 2, 3, 5 ya da 8'dir; en düşüğü tutulur, çünkü sınır aşılınca ffmpeg
    /// oturumu açamadan düşer ve motorun yazılıma dönen bir yolu yoktur.
    /// </summary>
    public const int HardwareSlots = 2;

    /// <summary>Bu makinede seçilebilecek en büyük değer: her işe en az iki mantıksal çekirdek.</summary>
    public static int Max(int logicalProcessors) => Math.Clamp(logicalProcessors / 2, 1, Ceiling);

    public static int Clamp(int requested, int logicalProcessors) => Math.Clamp(requested, 1, Max(logicalProcessors));

    /// <summary>Şimdi başlatılacak iş sayısı. Duraklatılmış sıra yeni iş başlatmaz; koşanlar biter.</summary>
    public static int StartCount(int pending, int running, int limit, bool paused)
        => paused ? 0 : Math.Clamp(Math.Max(limit, 1) - running, 0, Math.Max(pending, 0));

    /// <summary>Kuyruk sonu: bekleyen ve koşan yok, sıra duraklatılmamış ve son haberden beri en az bir iş bitmiş.</summary>
    public static bool Drained(int pending, int running, bool paused, int finishedSinceDrain)
        => pending == 0 && running == 0 && !paused && finishedSinceDrain > 0;

    public static bool NeedsHardwareSlot(string? codec) => !string.IsNullOrEmpty(codec) && CodecModel.IsHardware(codec);
}

/// <summary>
/// K6: donanım kodlayıcısının oturum kapısı. Yazılım işi beklemeden geçer; donanım işi
/// <see cref="ParallelJobs.HardwareSlots"/> yuvadan birini alır, yuva yoksa kendi kuyruk yerini
/// tutarak bekler. Dönen nesne bırakılınca yuva geri verilir.
/// </summary>
public sealed class EncoderSlots
{
    private readonly SemaphoreSlim _hardware = new(ParallelJobs.HardwareSlots, ParallelJobs.HardwareSlots);

    /// <summary>Süreç genelindeki kapı: ekran kartı tek, pencere sayısı kaç olursa olsun.</summary>
    public static EncoderSlots Shared { get; } = new();

    public async Task<IDisposable> EnterAsync(string? codec, CancellationToken ct = default)
    {
        if (!ParallelJobs.NeedsHardwareSlot(codec)) return new Yuva(null);
        await _hardware.WaitAsync(ct);
        return new Yuva(_hardware);
    }

    private sealed class Yuva : IDisposable
    {
        private SemaphoreSlim? _kapi;
        public Yuva(SemaphoreSlim? kapi) => _kapi = kapi;
        public void Dispose() => Interlocked.Exchange(ref _kapi, null)?.Release();
    }
}
