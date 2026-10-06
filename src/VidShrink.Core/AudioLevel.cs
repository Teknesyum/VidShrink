namespace VidShrink.Core;

/// <summary>
/// Ses seviyesi göstergesinin o an çizdiği değer: gösterilen seviye ve o seviyenin ne
/// kadardır tutulduğu.
/// </summary>
public readonly record struct AudioLevelHold(double Db, double HeldSeconds)
{
    public static AudioLevelHold Silent => new(AudioLevel.FloorDb, 0);
}

/// <summary>
/// Kaydedicinin ses seviyesi göstergesinin saf kararları: ham s16le örnekten tepe dBFS,
/// dB'den çubuk oranı, sessizlik eşiği, tepe tutma ve düşme, okuyucu sürecin argümanları.
/// Süreç ve arayüz burada yok.
/// </summary>
public static class AudioLevel
{
    public const int SampleRate = 8000;

    public const int WindowSamples = 400;

    public const double WindowSeconds = (double)WindowSamples / SampleRate;

    public const double FloorDb = -60;

    public const double SilenceDb = -50;

    public const double HoldSeconds = 0.5;

    public const double FallDbPerSecond = 30;

    public const string DirectShowBufferMs = "50";

    public static double Db(int peak)
        => peak <= 0 ? double.NegativeInfinity : 20 * Math.Log10(Math.Min(peak, 32768) / 32768.0);

    public static bool IsSilent(double db) => !(db > SilenceDb);

    public static double Fraction(double db)
    {
        if (IsSilent(db)) return 0;
        if (db >= 0) return 1;
        return (db - FloorDb) / (0 - FloorDb);
    }

    public static AudioLevelHold Advance(AudioLevelHold previous, double measuredDb, double elapsedSeconds)
    {
        var measured = double.IsNaN(measuredDb) ? FloorDb : Math.Clamp(measuredDb, FloorDb, 0);
        if (measured >= previous.Db) return new AudioLevelHold(measured, 0);

        var elapsed = Math.Max(0, elapsedSeconds);
        var held = previous.HeldSeconds + elapsed;
        if (held <= HoldSeconds) return previous with { HeldSeconds = held };

        return new AudioLevelHold(Math.Max(measured, previous.Db - FallDbPerSecond * elapsed), held);
    }

    public static IReadOnlyList<string> Arguments(AudioCaptureDevice device)
        => Arguments(AudioCaptureArguments.InputArguments(device));

    /// <summary>
    /// Girdi argümanlarını tek kanallı 8 kHz ham örnek akışına çevirir. dshow girdisinde
    /// tampon <see cref="DirectShowBufferMs"/> ms'ye iner; varsayılan tampon yarım saniyelik
    /// öbekler verir ve gösterge sesin gerisinde kalır.
    /// </summary>
    public static IReadOnlyList<string> Arguments(IReadOnlyList<string> input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var arguments = new List<string> { "-hide_banner", "-nostdin", "-loglevel", "error", "-threads", "1" };
        for (var i = 0; i < input.Count; i++)
        {
            arguments.Add(input[i]);
            if (i > 0 && input[i - 1] == "-f" && input[i] == "dshow")
            {
                arguments.Add("-audio_buffer_size");
                arguments.Add(DirectShowBufferMs);
            }
        }

        arguments.AddRange(new[]
        {
            "-vn", "-sn", "-dn",
            "-ac", "1",
            "-ar", SampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-threads", "1",
            "-f", "s16le",
            "pipe:1"
        });
        return arguments;
    }
}

/// <summary>
/// Ham s16le bayt akışını <see cref="AudioLevel.WindowSamples"/> örneklik pencerelere böler ve
/// her pencerenin tepe dBFS değerini verir. Öbek sınırı örneğin ortasına düşebilir; yarım
/// kalan bayt sonraki öbeğe taşınır.
/// </summary>
public sealed class AudioLevelWindow
{
    private int _pendingByte = -1;
    private int _inWindow;
    private int _peak;

    public void Add(ReadOnlySpan<byte> bytes, Action<double> emit)
    {
        ArgumentNullException.ThrowIfNull(emit);

        foreach (var value in bytes)
        {
            if (_pendingByte < 0)
            {
                _pendingByte = value;
                continue;
            }

            var sample = Math.Abs((int)(short)(_pendingByte | value << 8));
            _pendingByte = -1;
            if (sample > _peak) _peak = sample;
            if (++_inWindow < AudioLevel.WindowSamples) continue;

            emit(AudioLevel.Db(_peak));
            _inWindow = 0;
            _peak = 0;
        }
    }
}
