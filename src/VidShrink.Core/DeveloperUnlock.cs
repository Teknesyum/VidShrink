namespace VidShrink.Core;

/// <summary>
/// Gizli geliştirici sekmesinin sayacı. Android'in yapı numarasına arka arkaya
/// basma davranışının aynısı: <see cref="Threshold"/> basış, her basış arasında
/// en çok <see cref="Window"/> kadar süre. Arayı açan basış sayacı sıfırlar.
/// Saati kendi okumaz; her basışın anını çağıran verir, böylece ölçülebilir.
/// </summary>
public sealed class DeveloperUnlock
{
    public const int Threshold = 7;

    public static readonly TimeSpan Window = TimeSpan.FromSeconds(2);

    private int _count;
    private DateTimeOffset _last;

    public int Count => _count;

    public bool Tap(DateTimeOffset now)
    {
        _count = _count > 0 && now - _last <= Window ? _count + 1 : 1;
        _last = now;
        if (_count < Threshold) return false;
        _count = 0;
        return true;
    }

    public void Reset() => _count = 0;
}
