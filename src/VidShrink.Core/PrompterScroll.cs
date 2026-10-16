namespace VidShrink.Core;

/// <summary>
/// later.md #42, EKB-15: prompter metninin akışı. Metin <see cref="Extent"/> kadar kayar; sona varınca durur,
/// sondayken yeniden başlatmak başa sarar. Hız <see cref="Speeds"/> basamaklarından seçilir; birim saniyede
/// cihazdan bağımsız piksel, ölçü yazı boyuna değil pencereye göre.
/// </summary>
public sealed class PrompterScroll
{
    /// <summary>Saniyede kayan piksel; ilk basamak yavaş okuma, son basamak göz gezdirme.</summary>
    public static readonly double[] Speeds = { 15, 25, 40, 60, 85, 115, 150 };

    public const int DefaultStep = 2;

    public int Step { get; private set; } = DefaultStep;

    public double Speed => Speeds[Step];

    public double Offset { get; private set; }

    public double Extent { get; private set; }

    public bool Playing { get; private set; }

    public event Action? Changed;

    public static int ClampStep(int step) => step >= 0 && step < Speeds.Length ? step : DefaultStep;

    public void SetStep(int step)
    {
        Step = ClampStep(step);
        Changed?.Invoke();
    }

    /// <summary>İçerik ve görünür alan boyu değişince kayabilecek en uzak yer yeniden hesaplanır.</summary>
    public void Resize(double content, double view)
    {
        Extent = Math.Max(0, content - view);
        Offset = Math.Min(Offset, Extent);
        if (Extent == 0) Playing = false;
        Changed?.Invoke();
    }

    public void Play()
    {
        if (Extent == 0) return;
        if (Offset >= Extent) Offset = 0;
        Playing = true;
        Changed?.Invoke();
    }

    public void Pause()
    {
        if (!Playing) return;
        Playing = false;
        Changed?.Invoke();
    }

    public void Toggle()
    {
        if (Playing) Pause();
        else Play();
    }

    public void Tick(TimeSpan elapsed)
    {
        if (!Playing || elapsed <= TimeSpan.Zero) return;
        Offset = Math.Min(Extent, Offset + Speed * elapsed.TotalSeconds);
        if (Offset >= Extent) Playing = false;
        Changed?.Invoke();
    }

    /// <summary>Kullanıcı metni elle kaydırınca akış oradan sürer.</summary>
    public void MoveTo(double offset)
    {
        Offset = Math.Clamp(offset, 0, Extent);
        Changed?.Invoke();
    }

    public void Rewind() => MoveTo(0);

    public bool Faster() => Shift(1);

    public bool Slower() => Shift(-1);

    private bool Shift(int by)
    {
        var next = Step + by;
        if (next < 0 || next >= Speeds.Length) return false;
        Step = next;
        Changed?.Invoke();
        return true;
    }
}
