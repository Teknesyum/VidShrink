namespace VidShrink.Core;

/// <summary>
/// K6: koşan işlerin ayırdığı çıktı adları. Kodlama dosyayı ancak sonunda yerine koyar; arada
/// diske bakan ikinci iş aynı adı boş görür. Ad seçimi ve ayırma tek kilit altında yapılır,
/// böylece aynı ada iki iş düşmez. Büyük-küçük harf ayrımı yok: her dosya sisteminde güvenli taraf.
/// </summary>
public sealed class OutputReservations
{
    private readonly object _gate = new();
    private readonly HashSet<string> _held = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Süreç genelindeki defter; aynı süreçteki iki kuyruk penceresi de birbirini görür.</summary>
    public static OutputReservations Shared { get; } = new();

    /// <summary><paramref name="pick"/> ayrılmış adı soran yoklamayla ad seçer; dönen ad ayrılır.</summary>
    public string Reserve(Func<Func<string, bool>, string> pick)
    {
        lock (_gate)
        {
            var path = pick(candidate => _held.Contains(candidate));
            _held.Add(path);
            return path;
        }
    }

    public void Release(string path)
    {
        lock (_gate) _held.Remove(path);
    }

    public bool IsHeld(string path)
    {
        lock (_gate) return _held.Contains(path);
    }
}
