namespace VidShrink.Core;

/// <summary>
/// Windows'ta cikis cihazinda calan sesin (WASAPI loopback) kayda girme sozlesmesi.
/// ffmpeg bu sesi kendisi okuyamaz; uygulama yakalar ve ham PCM'i adlandirilmis borudan verir.
/// Bicim sabit oldugu icin arguman makineden bagimsizdir. Arguman uretimi boru adinin yerine
/// <see cref="PipeToken"/> yazar; sureci baslatan taraf onu benzersiz bir adla degistirir.
/// </summary>
public static class LoopbackAudio
{
    /// <summary>Ayarda saklanan sabit cihaz adi; ekranda yerellestirilmis etiket gosterilir.</summary>
    public const string LoopbackName = "WASAPI loopback";

    /// <summary>Argumanda boru adinin yerini tutan deger.</summary>
    public const string PipeToken = @"\\.\pipe\vidshrink-loopback";

    public const int SampleRate = 48000;

    public const int Channels = 2;

    public const int BytesPerFrame = 4;

    /// <summary>Windows cihaz listesinin sonuna eklenen secenek.</summary>
    public static AudioCaptureDevice Device { get; } =
        new(LoopbackName, CaptureBackend.WasapiLoopback, AudioSourceRole.SystemAudio, PipeToken);

    /// <summary>Ses izinin basina eklenebilecek en uzun sessizlik; ustu bozuk damga sayilir.</summary>
    public static readonly TimeSpan MaxLead = TimeSpan.FromSeconds(3);

    /// <summary>
    /// ffmpeg'in girdi dokumundeki <c>Duration: N/A, start: 1791630023.446262</c> satirindan
    /// ilk goruntu karesinin duvar saatini okur. Ekran yakalama damgayi Unix zamaniyla yazar;
    /// sifirdan sayan girdiler (lavfi) ve baska satirlar <c>null</c> doner.
    /// </summary>
    public static DateTime? CaptureStart(string? line)
    {
        const string key = "start: ";
        if (line is null || !line.Contains("Duration:", StringComparison.Ordinal)) return null;

        var at = line.IndexOf(key, StringComparison.Ordinal);
        if (at < 0) return null;

        var from = at + key.Length;
        var to = line.IndexOf(',', from);
        var text = to < 0 ? line[from..] : line[from..to];
        if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            return null;

        return seconds is > 1e9 and < 1e11 ? DateTime.UnixEpoch.AddSeconds(seconds) : null;
    }

    /// <summary>
    /// Goruntu ilk karesini boru baglanmadan once yakalar; ffmpeg iki girdiyi de sifirdan
    /// baslattigi icin aradaki sure kadar ses videonun onune gecer. Donen sure ses izinin basina
    /// sessizlik olarak eklenir. Damga yoksa, gelecekteyse ya da <see cref="MaxLead"/>'i
    /// asiyorsa sifir doner.
    /// </summary>
    public static TimeSpan Lead(DateTime? captureStartUtc, DateTime connectedUtc)
    {
        if (captureStartUtc is not { } start) return TimeSpan.Zero;

        var lead = connectedUtc - start;
        return lead > TimeSpan.Zero && lead <= MaxLead ? lead : TimeSpan.Zero;
    }

    /// <summary>Verilen arguman dizisi loopback borusundan okuyor mu.</summary>
    public static bool Uses(IEnumerable<string>? arguments)
        => arguments is not null && arguments.Contains(PipeToken, StringComparer.Ordinal);
}

/// <summary>Pompanin bir turda yapacagi duzeltme: once yazilacak sessizlik ve atilacak kare sayisi.</summary>
public readonly record struct LoopbackStep(int SilenceFrames, int SkipFrames);

/// <summary>
/// Ses izini duvar saatine baglar. Sistem sessizken loopback hic veri vermez; ham akista zaman
/// yazilan ornek sayisindan okundugu icin eksik doldurulmazsa ses videonun onune kayar.
/// Her turda gecen sure ve yakalanan kare sayisi verilir, donen adim boruya yazilmadan once
/// eklenecek sessizligi ya da atilacak fazlayi soyler.
/// <para>
/// Uc tolerans var: veri gelirken <see cref="DataToleranceFrames"/> (kucuk gecikme icin akis
/// delinmez), veri yokken <see cref="IdleSlackFrames"/> (yoklama titremesi sessizlik sanilmaz),
/// ileride <see cref="AheadToleranceFrames"/> (cihaz saati hizli giderse fazla atilir).
/// Ucu de olculmus sayi degil, secilmis sinir.
/// </para>
/// </summary>
public sealed class LoopbackTimeline
{
    public const int DataToleranceFrames = LoopbackAudio.SampleRate * 30 / 1000;

    public const int IdleSlackFrames = LoopbackAudio.SampleRate * 100 / 1000;

    public const int AheadToleranceFrames = LoopbackAudio.SampleRate * 100 / 1000;

    /// <summary>Tek turda yazilan en uzun sessizlik; kalan sonraki turda kapanir.</summary>
    public const int MaxSilenceFrames = LoopbackAudio.SampleRate;

    private long _written;

    public LoopbackStep Next(TimeSpan elapsed, int capturedFrames)
    {
        if (capturedFrames < 0) throw new ArgumentOutOfRangeException(nameof(capturedFrames), capturedFrames, "kare sayisi negatif olamaz.");

        var due = (long)(elapsed.TotalSeconds * LoopbackAudio.SampleRate);
        var behind = due - _written - capturedFrames;
        var silence = 0;
        var skip = 0;

        if (behind > (capturedFrames > 0 ? DataToleranceFrames : IdleSlackFrames))
            silence = (int)Math.Min(behind, MaxSilenceFrames);
        else if (-behind > AheadToleranceFrames)
            skip = (int)Math.Min(capturedFrames, -behind);

        _written += silence + capturedFrames - skip;
        return new LoopbackStep(silence, skip);
    }
}
