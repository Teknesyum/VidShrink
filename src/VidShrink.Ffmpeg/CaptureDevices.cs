using System.Diagnostics;
using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

/// <summary>
/// Bir kez okunmus cihaz listesi. <see cref="Loaded"/> "komut dondu mu" demektir,
/// "cihaz var mi" demez: koşucu makinede hic yakalama cihazi olmamasi mesru bir cevaptir
/// ve bos liste kalici yazilir; yalniz zaman asimi ya da surecin hic baslamamasi
/// yuklenmemis sayilir.
/// </summary>
public sealed record CaptureDeviceList(
    IReadOnlyList<AudioCaptureDevice> Audio,
    IReadOnlyList<string> Video,
    CaptureBackend Backend,
    bool Loaded);

/// <summary>
/// Cihaz listesini ffmpeg'in kendisinden okur ve onbellekler. Windows'ta
/// <c>-list_devices true -f dshow -i dummy</c>, macOS'ta <c>-f avfoundation
/// -list_devices true -i ""</c>, Linux'ta <c>pactl list short sources</c>.
///
/// Okuma <see cref="EncoderCapabilities"/> kalibinda: cikti <b>asenkron</b> baslar,
/// <c>ReadToEnd()</c> akis kapanana kadar bloke oldugu icin ondan sonra gelen
/// <c>WaitForExit</c> etkisiz kalirdi; dahasi cihaz listesi <b>stderr'e</b> basilir ve
/// bosaltilmayan stderr borusu 4096 baytta dolup sureci kilitler. Iki akis da okunur.
///
/// Ayristirma surecten ayri durur (<see cref="ParseDirectShow"/> ve kardesleri
/// <c>internal</c>): olcu, ffmpeg kosturmadan sabit metinle ayni listeyi pimler. Bu depoda
/// bir olcerin surum ofseti kiyasi gecersiz kilmisti; sayim pimleri bu yuzden canli
/// ciktidan degil sabit metinden okunur.
/// </summary>
public sealed class CaptureDevices
{
    private static readonly object InstanceGate = new();
    private static CaptureDeviceList? _instance;
    private static long _lastLoadTicks;

    /// <summary>Basarisiz okumadan sonra yeniden denemeden once beklenen sure.</summary>
    internal const int ReloadAfterFailureMs = 5000;

    /// <summary>Cihaz listeleyen surecin oldurulme siniri.</summary>
    internal const int CaptureKillMs = 10000;

    internal static readonly string[] DirectShowArguments =
        { "-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy" };

    internal static readonly string[] AvFoundationArguments =
        { "-hide_banner", "-f", "avfoundation", "-list_devices", "true", "-i", "" };

    internal static readonly string[] PulseArguments = { "list", "short", "sources" };

    /// <summary>
    /// Sistem sesi sayilan cihaz adi parcalari. Windows'ta dshow'un kendi geri-dongu
    /// cihazlari ("Stereo Mix", sanal kablo), Linux'ta pulse'un <c>.monitor</c> kaynagi.
    /// </summary>
    internal static readonly string[] SystemAudioMarkers =
        { "stereo mix", "stereo miks", "what u hear", "loopback", "geri dongu", "monitor of", ".monitor", "wave out mix" };

    private static readonly Regex LinePrefix = new(@"^\[[^\]]*\]\s*", RegexOptions.Compiled);
    private static readonly Regex DirectShowSection = new(@"^DirectShow (video|audio) devices", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AlternativeName = new(@"^Alternative name\s+""(.+)""\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex QuotedDevice = new(@"^""(.+)""(?:\s*\((video|audio)\))?\s*$", RegexOptions.Compiled);
    private static readonly Regex AvSection = new(@"^AVFoundation (video|audio) devices", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AvDevice = new(@"^\[(\d+)\]\s+(.+?)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Liste bir kez okunur. Okuma <b>basarisizsa</b> sonuc kalici degildir: gecici bir
    /// acilis hatasi cihazsizligi surec omru boyunca sabitliyordu. En fazla
    /// <see cref="ReloadAfterFailureMs"/> ms'de bir yeniden denenir.
    /// </summary>
    public static CaptureDeviceList Instance
    {
        get
        {
            lock (InstanceGate)
            {
                if (_instance is { Loaded: true }) return _instance;
                var now = Environment.TickCount64;
                if (_instance is not null && now - _lastLoadTicks < ReloadAfterFailureMs) return _instance;
                _lastLoadTicks = now;
                var loaded = Load();
                if (loaded.Loaded || _instance is null) _instance = loaded;
                return _instance;
            }
        }
    }

    /// <summary>
    /// Onbellegi bosaltir. Olcu bunu kullaniyor, ayrica kaydedicinin yenileme dugmesi:
    /// mikrofonu program acikken takan kullanici <see cref="ReloadAfterFailureMs"/> kadar
    /// beklemeden listeyi yeniden okutabiliyor.
    /// </summary>
    public static void Invalidate()
    {
        lock (InstanceGate)
        {
            _instance = null;
            _lastLoadTicks = 0;
        }
    }

    /// <summary>Icinde bulunulan isletim sisteminin girdi katmani.</summary>
    public static CaptureBackend Backend => OperatingSystem.IsWindows()
        ? CaptureBackend.DirectShow
        : OperatingSystem.IsMacOS()
            ? CaptureBackend.AvFoundation
            : CaptureBackend.PulseAudio;

    internal static CaptureDeviceList Load()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return ParseDirectShow(RunCapture(ToolLocator.Ffmpeg, DirectShowArguments));
            if (OperatingSystem.IsMacOS())
                return ParseAvFoundation(RunCapture(ToolLocator.Ffmpeg, AvFoundationArguments));
            return ParsePulse(RunCapture("pactl", PulseArguments, wantStandardOutput: true));
        }
        catch
        {
            return Unloaded();
        }
    }

    internal static CaptureDeviceList Unloaded() => new(
        Array.Empty<AudioCaptureDevice>(),
        Array.Empty<string>(),
        Backend,
        Loaded: false);

    /// <summary>
    /// dshow listesinin iki bicimi ayni yerden ayristirilir. ffmpeg 7 oncesi bolum basligi
    /// yazar ve adi tirnak icinde tek basina verir; 9.0 bolum basligi yazmaz, her satirin
    /// sonuna <c>(video)</c>/<c>(audio)</c> ekler. Turu ne baslikla ne son ekle belirlenen
    /// satir <b>atlanir</b> — turu tahmin etmek, video cihazini ses girdisi yapardi.
    /// Cihaz olmayan satirlar (surucu gunlukleri, "Error opening input file") eslesmez.
    /// </summary>
    internal static CaptureDeviceList ParseDirectShow(string text)
    {
        var audio = new List<AudioCaptureDevice>();
        var video = new List<string>();
        string? section = null;
        var lastWasAudio = false;

        foreach (var raw in Lines(text))
        {
            var line = LinePrefix.Replace(raw, "").Trim();
            if (line.Length == 0) continue;

            var sectionMatch = DirectShowSection.Match(line);
            if (sectionMatch.Success)
            {
                section = sectionMatch.Groups[1].Value.ToLowerInvariant();
                lastWasAudio = false;
                continue;
            }

            var alternative = AlternativeName.Match(line);
            if (alternative.Success)
            {
                if (lastWasAudio && audio.Count > 0)
                    audio[^1] = audio[^1] with { Alternative = alternative.Groups[1].Value };
                continue;
            }

            var device = QuotedDevice.Match(line);
            if (!device.Success) continue;

            var kind = device.Groups[2].Success ? device.Groups[2].Value.ToLowerInvariant() : section;
            if (kind is null) continue;

            var name = device.Groups[1].Value;
            if (kind == "audio")
            {
                audio.Add(new AudioCaptureDevice(name, CaptureBackend.DirectShow, ClassifyRole(name)));
                lastWasAudio = true;
            }
            else
            {
                video.Add(name);
                lastWasAudio = false;
            }
        }

        return new CaptureDeviceList(audio, video, CaptureBackend.DirectShow, Loaded: true);
    }

    /// <summary>
    /// avfoundation listesi sirali: argumanda kullanilan deger addan degil kose parantezli
    /// siradan gelir, bu yuzden <see cref="AudioCaptureDevice.Address"/>e o yazilir.
    /// </summary>
    internal static CaptureDeviceList ParseAvFoundation(string text)
    {
        var audio = new List<AudioCaptureDevice>();
        var video = new List<string>();
        string? section = null;

        foreach (var raw in Lines(text))
        {
            var line = LinePrefix.Replace(raw, "").Trim();
            if (line.Length == 0) continue;

            var sectionMatch = AvSection.Match(line);
            if (sectionMatch.Success)
            {
                section = sectionMatch.Groups[1].Value.ToLowerInvariant();
                continue;
            }

            if (section is null) continue;
            var device = AvDevice.Match(line);
            if (!device.Success) continue;

            var name = device.Groups[2].Value;
            if (section == "audio")
                audio.Add(new AudioCaptureDevice(name, CaptureBackend.AvFoundation, ClassifyRole(name), device.Groups[1].Value));
            else
                video.Add(name);
        }

        return new CaptureDeviceList(audio, video, CaptureBackend.AvFoundation, Loaded: true);
    }

    /// <summary>
    /// <c>pactl list short sources</c> sekmeyle bolunmus satirlar dondurur; ikinci alan
    /// kaynak adidir ve <c>-f pulse -i</c>ye oldugu gibi yazilir. <c>.monitor</c> ile
    /// biten kaynak cikisin geri-dongusudur, yani sistem sesi.
    /// </summary>
    internal static CaptureDeviceList ParsePulse(string text)
    {
        var audio = new List<AudioCaptureDevice>();

        foreach (var raw in Lines(text))
        {
            var fields = raw.Split('\t', StringSplitOptions.TrimEntries);
            if (fields.Length < 2) continue;
            var name = fields[1];
            if (name.Length == 0) continue;
            audio.Add(new AudioCaptureDevice(name, CaptureBackend.PulseAudio, ClassifyRole(name), name));
        }

        return new CaptureDeviceList(audio, Array.Empty<string>(), CaptureBackend.PulseAudio, Loaded: true);
    }

    /// <summary>
    /// Rolu cihaz adindan siniflandirir. Bilinen geri-dongu adlari sistem sesi, geri kalani
    /// mikrofon. Siniflandirma <b>tahmin</b> oldugu icin baglayici degil: secim
    /// <c>AudioCaptureArguments</c>ta rolle birlikte dogrulanir, yani yanlis rolle secilen
    /// cihaz sessizce girdi olmaz.
    /// </summary>
    internal static AudioSourceRole ClassifyRole(string name)
        => SystemAudioMarkers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase))
            ? AudioSourceRole.SystemAudio
            : AudioSourceRole.Microphone;

    private static IEnumerable<string> Lines(string text)
        => text.Split('\n').Select(line => line.TrimEnd('\r'));

    private static string RunCapture(string tool, IEnumerable<string> args, bool wantStandardOutput = false)
    {
        using var process = new Process { StartInfo = ToolLocator.StartInfo(tool, args) };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(CaptureKillMs))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException($"{tool} {CaptureKillMs} ms icinde donmedi.");
        }
        Task.WaitAll(new Task[] { output, error }, 1000);
        return wantStandardOutput ? output.Result : error.Result;
    }
}
