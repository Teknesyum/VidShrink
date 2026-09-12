using System.Globalization;

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
/// Iki girdi secildiginde ciktinin kac ses izi tasiyacagi. Mimari degismiyor: iki kol da
/// ayni <see cref="AudioCapturePlan.Maps"/> listesini dolduruyor, yalniz uzunlugu ve
/// grafigi ayrisiyor.
/// </summary>
public enum AudioTrackLayout
{
    /// <summary><c>amix</c> ile tek ize karistirma; oynaticilarin hepsi tek izi calar.</summary>
    MixedSingleTrack,

    /// <summary>
    /// Her girdi kendi izine gider (<c>-map</c> basina bir iz). Kurguda mikrofon ve sistem
    /// sesi ayri ayri sessize alinabiliyor; karsiliginda oynaticilarin bir kismi yalniz ilk
    /// izi caliyor.
    /// </summary>
    SeparateTracks
}

/// <summary>
/// Girdi basina uygulanan ses filtreleri. Hepsi <c>-filter_complex</c> zincirine giriyor;
/// hicbiri secilmediginde grafik kurulmuyor ve arguman bugunku haliyle kaliyor.
/// </summary>
/// <param name="GainDb">
/// <c>volume</c> kazanci, desibel. Sifir halkayi hic kurmaz.
/// </param>
/// <param name="NoiseGate">
/// <c>agate</c>: esigin altindaki sessizlik kisiliyor. Klavye ve fan sesini kesiyor,
/// esigin yakinindaki konusmayi da kesebiliyor.
/// </param>
/// <param name="NoiseSuppression">
/// <c>afftdn</c>: genis bantli gurultuyu frekans alaninda bastiriyor.
/// </param>
public sealed record AudioFilterOptions(
    double GainDb = 0,
    bool NoiseGate = false,
    bool NoiseSuppression = false)
{
    /// <summary>Hicbir filtre kurulmayan hal.</summary>
    public static AudioFilterOptions None { get; } = new();

    /// <summary>En az bir halka kuruluyor mu.</summary>
    public bool IsSet => GainDb != 0 || NoiseGate || NoiseSuppression;
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

    /// <summary>Kazanc halkasinin filtre adi.</summary>
    public const string GainFilterName = "volume";

    /// <summary>Gurultu kapisinin filtre adi.</summary>
    public const string NoiseGateFilterName = "agate";

    /// <summary>Gurultu bastirmanin filtre adi.</summary>
    public const string NoiseSuppressionFilterName = "afftdn";

    /// <summary>Kabul edilen en dusuk kazanc, desibel.</summary>
    public const double MinGainDb = -60;

    /// <summary>Kabul edilen en yuksek kazanc, desibel.</summary>
    public const double MaxGainDb = 30;

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
        => Build(selection, listed, firstInputIndex, AudioTrackLayout.MixedSingleTrack, null);

    /// <summary>
    /// Secimi argumana cevirir; iz duzenini ve filtreleri de okur.
    /// <para>
    /// Filtre secilmediginde ve duzen <see cref="AudioTrackLayout.MixedSingleTrack"/>
    /// oldugunda uretilen plan uc argumanli asiri yuklemeninkiyle birebir ayni kaliyor:
    /// tek girdide grafik kurulmuyor, iki girdide yalniz <c>amix</c> var.
    /// </para>
    /// </summary>
    public static AudioCapturePlan Build(
        AudioCaptureSelection selection,
        IReadOnlyCollection<AudioCaptureDevice> listed,
        int firstInputIndex,
        AudioTrackLayout layout,
        AudioFilterOptions? filters)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(listed);
        if (firstInputIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(firstInputIndex), firstInputIndex, "girdi sirasi negatif olamaz.");

        var options = filters ?? AudioFilterOptions.None;
        if (options.GainDb < MinGainDb || options.GainDb > MaxGainDb)
            throw new ArgumentOutOfRangeException(
                nameof(filters), options.GainDb,
                $"kazanc {MinGainDb} ile {MaxGainDb} dB arasinda olmali; arguman uretilmedi.");

        var chosen = new List<AudioCaptureDevice>(2);
        if (selection.Microphone is { } microphone)
            chosen.Add(Verify(microphone, AudioSourceRole.Microphone, listed));
        if (selection.SystemAudio is { } system)
            chosen.Add(Verify(system, AudioSourceRole.SystemAudio, listed));

        if (chosen.Count == 0) return AudioCapturePlan.Silent;

        var inputs = new List<string>();
        foreach (var device in chosen) inputs.AddRange(InputArguments(device));

        var chain = FilterChain(options);
        var separate = layout switch
        {
            AudioTrackLayout.MixedSingleTrack => false,
            AudioTrackLayout.SeparateTracks => chosen.Count > 1,
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "tanimsiz ses izi duzeni; arguman uretilmedi.")
        };

        if (chain is null && !separate && chosen.Count == 1)
            return new AudioCapturePlan(inputs, null, new[] { $"{firstInputIndex}:a" }, 1);

        if (chain is null && separate)
            return new AudioCapturePlan(
                inputs,
                null,
                Enumerable.Range(firstInputIndex, chosen.Count).Select(i => $"{i}:a").ToArray(),
                chosen.Count);

        if (chain is null)
        {
            var labels = string.Concat(Enumerable
                .Range(firstInputIndex, chosen.Count)
                .Select(i => $"[{i}:a]"));
            var mix = $"{labels}{MixFilterName}=inputs={chosen.Count}:duration=longest:dropout_transition=0[{MixOutputLabel}]";
            return new AudioCapturePlan(inputs, mix, new[] { $"[{MixOutputLabel}]" }, chosen.Count);
        }

        if (separate)
        {
            var branches = Enumerable
                .Range(0, chosen.Count)
                .Select(track => $"[{firstInputIndex + track}:a]{chain}[{MixOutputLabel}{track}]");
            return new AudioCapturePlan(
                inputs,
                string.Join(';', branches),
                Enumerable.Range(0, chosen.Count).Select(track => $"[{MixOutputLabel}{track}]").ToArray(),
                chosen.Count);
        }

        if (chosen.Count == 1)
            return new AudioCapturePlan(
                inputs,
                $"[{firstInputIndex}:a]{chain}[{MixOutputLabel}]",
                new[] { $"[{MixOutputLabel}]" },
                1);

        var filtered = Enumerable
            .Range(0, chosen.Count)
            .Select(track => $"[{firstInputIndex + track}:a]{chain}[{MixFilterName}{track}]");
        var mixInputs = string.Concat(Enumerable.Range(0, chosen.Count).Select(track => $"[{MixFilterName}{track}]"));
        var graph = string.Join(';', filtered)
                    + $";{mixInputs}{MixFilterName}=inputs={chosen.Count}:duration=longest:dropout_transition=0[{MixOutputLabel}]";
        return new AudioCapturePlan(inputs, graph, new[] { $"[{MixOutputLabel}]" }, chosen.Count);
    }

    /// <summary>
    /// Girdi basina kurulan filtre zinciri; hicbir secim yoksa <c>null</c> ve grafik hic
    /// kurulmuyor. Sira sabit: once kazanc, sonra kapi, sonra bastirma — kapiya kazanci
    /// uygulanmis isaret giriyor, yoksa esik kullanicinin duydugu seviyeye gore degil ham
    /// seviyeye gore calisiyor.
    /// </summary>
    public static string? FilterChain(AudioFilterOptions? filters)
    {
        var options = filters ?? AudioFilterOptions.None;
        if (!options.IsSet) return null;

        var links = new List<string>(3);
        if (options.GainDb != 0)
            links.Add($"{GainFilterName}={options.GainDb.ToString("0.##", CultureInfo.InvariantCulture)}dB");
        if (options.NoiseGate) links.Add(NoiseGateFilterName);
        if (options.NoiseSuppression) links.Add(NoiseSuppressionFilterName);
        return links.Count == 0 ? null : string.Join(',', links);
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
        => TryBuild(selection, listed, firstInputIndex, AudioTrackLayout.MixedSingleTrack, null, out plan, out reason);

    /// <summary>
    /// <see cref="Build(AudioCaptureSelection, IReadOnlyCollection{AudioCaptureDevice}, int, AudioTrackLayout, AudioFilterOptions)"/>in
    /// atmayan hali. Kabul edilemez kazanc da sessizce kirpilmiyor, sebebiyle geri
    /// donuyor.
    /// </summary>
    public static bool TryBuild(
        AudioCaptureSelection selection,
        IReadOnlyCollection<AudioCaptureDevice> listed,
        int firstInputIndex,
        AudioTrackLayout layout,
        AudioFilterOptions? filters,
        out AudioCapturePlan plan,
        out string? reason)
    {
        try
        {
            plan = Build(selection, listed, firstInputIndex, layout, filters);
            reason = null;
            return true;
        }
        catch (UnknownCaptureDeviceException ex)
        {
            plan = AudioCapturePlan.Silent;
            reason = ex.Message;
            return false;
        }
        catch (ArgumentOutOfRangeException ex)
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
