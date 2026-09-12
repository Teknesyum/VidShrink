namespace VidShrink.Core;

/// <summary>
/// Cihaz listesini hangi ffmpeg girdi katmanindan okundugu. Arguman uretimi buna gore
/// dallanir: ayni cihaz adi dshow'da <c>audio=&lt;ad&gt;</c>, avfoundation'da
/// <c>:&lt;sira&gt;</c>, pulse'ta kaynak adidir.
/// </summary>
public enum CaptureBackend { DirectShow, AvFoundation, PulseAudio }

/// <summary>
/// Ses girdisinin iki rolu. Rol cihaz adindan siniflandirilir ve <b>secimde dogrulanir</b>:
/// mikrofon olarak listelenen bir cihaz sistem sesi diye secilirse arguman uretilmez.
/// </summary>
public enum AudioSourceRole { Microphone, SystemAudio }

/// <summary>
/// Listelenmis tek bir ses cihazi. <see cref="Reference"/> ffmpeg'e yazilan degerdir:
/// dshow insan okunur adi kabul eder, avfoundation sira numarasini, pulse kaynak adini.
/// <see cref="Alternative"/> dshow'un <c>@device_cm_...</c> takma adidir; ayni ada sahip
/// iki cihazi ayirmak icin tasinir, argumanda kullanilmaz.
/// </summary>
public sealed record AudioCaptureDevice(
    string Name,
    CaptureBackend Backend,
    AudioSourceRole Role,
    string? Address = null,
    string? Alternative = null)
{
    public string Reference => Backend == CaptureBackend.DirectShow ? Name : Address ?? Name;
}

/// <summary>Kullanicinin sectigi ses girdileri. Ikisi de bos olabilir (sessiz kayit).</summary>
public sealed record AudioCaptureSelection(
    AudioCaptureDevice? Microphone = null,
    AudioCaptureDevice? SystemAudio = null)
{
    public int Count => (Microphone is null ? 0 : 1) + (SystemAudio is null ? 0 : 1);
}

/// <summary>
/// Uretilen ses kolu. <see cref="Inputs"/> 8a'nin arguman dizisine video girdisinden
/// <b>sonra</b> eklenir, <see cref="FilterComplex"/> bos degilse <c>-filter_complex</c>
/// degeri olarak, <see cref="Maps"/> ise oldugu gibi.
/// </summary>
public sealed record AudioCapturePlan(
    IReadOnlyList<string> Inputs,
    string? FilterComplex,
    IReadOnlyList<string> Maps,
    int InputCount)
{
    public static AudioCapturePlan Silent { get; } =
        new(Array.Empty<string>(), null, Array.Empty<string>(), 0);
}

/// <summary>
/// Secilen cihaz listede yok ya da listedeki rolu baska. Sessiz yutmanin yerine bu
/// atilir: bu depoda SVT-AV1 tanimadigi anahtari sessizce yutmus ve uydurma anahtar da
/// "kabul" donmustu; cihaz adinda ayni tuzak, secimin dogrulanmasiyla kapatildi.
/// </summary>
public sealed class UnknownCaptureDeviceException : Exception
{
    public UnknownCaptureDeviceException(string message, string deviceName)
        : base(message) => DeviceName = deviceName;

    public string DeviceName { get; }
}

/// <summary>
/// Ses girdisinin argumanlarini uretir. Surec baslatmaz ve cihaz listesi <b>okumaz</b>:
/// liste <c>VidShrink.Ffmpeg.CaptureDevices</c>ten gelir, burasi yalniz karar verir, boylece
/// olcu cihazsiz makinede de ayni karari pimler.
/// </summary>
public static class AudioCaptureArguments
{
    /// <summary>Iki girdi birlestiginde kullanilan filtre; tek girdide filtre kurulmaz.</summary>
    public const string MixFilterName = "amix";

    /// <summary><see cref="MixFilterName"/> cikisinin etiketi.</summary>
    public const string MixOutputLabel = "aout";

    /// <summary>
    /// Secimi argumana cevirir. <paramref name="listed"/> o an listelenmis cihazlardir;
    /// secilen her cihaz bu kumede adiyla <b>ve</b> roluyle bulunmak zorundadir.
    /// <paramref name="firstInputIndex"/> ses girdilerinin ffmpeg girdi sirasindaki ilk
    /// numarasidir (video girdisi 0 ise 1).
    /// </summary>
    public static AudioCapturePlan Build(
        AudioCaptureSelection selection,
        IReadOnlyCollection<AudioCaptureDevice> listed,
        int firstInputIndex)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(listed);
        if (firstInputIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(firstInputIndex), firstInputIndex, "girdi sirasi negatif olamaz.");

        var chosen = new List<AudioCaptureDevice>(2);
        if (selection.Microphone is { } microphone)
            chosen.Add(Verify(microphone, AudioSourceRole.Microphone, listed));
        if (selection.SystemAudio is { } system)
            chosen.Add(Verify(system, AudioSourceRole.SystemAudio, listed));

        if (chosen.Count == 0) return AudioCapturePlan.Silent;

        var inputs = new List<string>();
        foreach (var device in chosen) inputs.AddRange(InputArguments(device));

        if (chosen.Count == 1)
            return new AudioCapturePlan(inputs, null, new[] { $"{firstInputIndex}:a" }, 1);

        var labels = string.Concat(Enumerable
            .Range(firstInputIndex, chosen.Count)
            .Select(i => $"[{i}:a]"));
        var filter = $"{labels}{MixFilterName}=inputs={chosen.Count}:duration=longest:dropout_transition=0[{MixOutputLabel}]";
        return new AudioCapturePlan(inputs, filter, new[] { $"[{MixOutputLabel}]" }, chosen.Count);
    }

    /// <summary>
    /// <see cref="Build"/>in atmayan hali. Bilinmeyen cihaz <c>false</c> dondurur ve
    /// sebebi <paramref name="reason"/>a yazar; <c>true</c> donen yolda plan her zaman
    /// dolu bir referans tasir.
    /// </summary>
    public static bool TryBuild(
        AudioCaptureSelection selection,
        IReadOnlyCollection<AudioCaptureDevice> listed,
        int firstInputIndex,
        out AudioCapturePlan plan,
        out string? reason)
    {
        try
        {
            plan = Build(selection, listed, firstInputIndex);
            reason = null;
            return true;
        }
        catch (UnknownCaptureDeviceException ex)
        {
            plan = AudioCapturePlan.Silent;
            reason = ex.Message;
            return false;
        }
    }

    /// <summary>Tek cihazin girdi argumanlari — <c>-f &lt;katman&gt; -i &lt;deger&gt;</c>.</summary>
    public static IReadOnlyList<string> InputArguments(AudioCaptureDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (string.IsNullOrWhiteSpace(device.Reference))
            throw new UnknownCaptureDeviceException("cihazin ffmpeg referansi bos.", device.Name);

        return device.Backend switch
        {
            CaptureBackend.DirectShow => new[] { "-f", "dshow", "-i", $"audio={EscapeDirectShow(device.Reference)}" },
            CaptureBackend.AvFoundation => new[] { "-f", "avfoundation", "-i", $":{device.Reference}" },
            CaptureBackend.PulseAudio => new[] { "-f", "pulse", "-i", device.Reference },
            _ => throw new UnknownCaptureDeviceException($"tanimsiz girdi katmani: {device.Backend}.", device.Name)
        };
    }

    /// <summary>
    /// dshow'un <c>-i</c> degeri iki nokta ile bolunur, ters bolu de kacis karakteridir.
    /// "SteelSeries Sonar - Microphone (...)" gibi adlar zararsiz, ama takma adlarda ters
    /// bolu ve bazi surucu adlarinda iki nokta geciyor; ikisi de kacirilir.
    /// </summary>
    public static string EscapeDirectShow(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace(":", "\\:", StringComparison.Ordinal);

    private static AudioCaptureDevice Verify(
        AudioCaptureDevice device,
        AudioSourceRole expectedRole,
        IReadOnlyCollection<AudioCaptureDevice> listed)
    {
        var byName = listed
            .Where(d => d.Backend == device.Backend
                        && string.Equals(d.Name, device.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (byName.Count == 0)
            throw new UnknownCaptureDeviceException(
                $"\"{device.Name}\" {device.Backend} listesinde yok ({listed.Count} cihaz listelendi); arguman uretilmedi.",
                device.Name);

        var match = byName.FirstOrDefault(d => d.Role == expectedRole);
        if (match is null)
            throw new UnknownCaptureDeviceException(
                $"\"{device.Name}\" {byName[0].Role} olarak listelendi, {expectedRole} olarak secildi; arguman uretilmedi.",
                device.Name);

        return match;
    }
}
