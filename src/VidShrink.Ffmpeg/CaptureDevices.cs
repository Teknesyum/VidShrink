using System.Diagnostics;
using System.Globalization;
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
/// -list_devices true -i ""</c>, Linux'ta ses icin <c>pactl list short sources</c>, kamera icin
/// Video4Linux aygit dosyalari (<see cref="ListV4l2"/>; v4l2'nin ffmpeg'de listeleme komutu yok).
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
    private static readonly Regex DirectShowOptions = new(@"^DirectShow (video|audio) device options", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DirectShowMode = new(
        @"min s=(\d+)x(\d+) fps=([0-9.]+)\s+max s=(\d+)x(\d+) fps=([0-9.]+)", RegexOptions.Compiled);

    private static readonly object ModeGate = new();
    private static readonly Dictionary<string, IReadOnlyList<CameraMode>> ModeCache = new(StringComparer.OrdinalIgnoreCase);

    internal const double CommonCameraFps = 30;

    internal static string[] DirectShowModeArguments(string device)
        => new[] { "-hide_banner", "-f", "dshow", "-list_options", "true", "-i", "video=" + device };

    public static IReadOnlyList<CameraMode> ModesOf(string device)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(device) || device.Contains('"', StringComparison.Ordinal))
            return Array.Empty<CameraMode>();
        lock (ModeGate)
        {
            if (ModeCache.TryGetValue(device, out var known)) return known;
        }

        IReadOnlyList<CameraMode> modes;
        try
        {
            modes = ParseDirectShowModes(RunCapture(ToolLocator.Ffmpeg, DirectShowModeArguments(device)));
        }
        catch
        {
            return Array.Empty<CameraMode>();
        }

        lock (ModeGate) ModeCache[device] = modes;
        return modes;
    }

    internal static IReadOnlyList<CameraMode> ParseDirectShowModes(string text)
    {
        var ranges = new List<(int Width, int Height, double Min, double Max)>();
        string? section = null;

        foreach (var raw in Lines(text))
        {
            var line = LinePrefix.Replace(raw, "").Trim();
            var header = DirectShowOptions.Match(line);
            if (header.Success)
            {
                section = header.Groups[1].Value.ToLowerInvariant();
                continue;
            }

            if (section != "video") continue;
            var mode = DirectShowMode.Match(line);
            if (!mode.Success) continue;
            if (!int.TryParse(mode.Groups[4].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var width)) continue;
            if (!int.TryParse(mode.Groups[5].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var height)) continue;
            if (!double.TryParse(mode.Groups[3].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var min)) continue;
            if (!double.TryParse(mode.Groups[6].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var max)) continue;
            if (width <= 0 || height <= 0 || max <= 0) continue;
            ranges.Add((width, height, Math.Min(min, max), max));
        }

        var modes = new List<CameraMode>();
        foreach (var size in ranges.GroupBy(r => (r.Width, r.Height)))
        {
            var top = size.Max(r => r.Max);
            modes.Add(new CameraMode(size.Key.Width, size.Key.Height, top));
            if (top > CommonCameraFps && size.Any(r => r.Min <= CommonCameraFps && CommonCameraFps <= r.Max))
                modes.Add(new CameraMode(size.Key.Width, size.Key.Height, CommonCameraFps));
        }

        return modes
            .Distinct()
            .OrderByDescending(m => (long)m.Width * m.Height)
            .ThenByDescending(m => m.Width)
            .ThenByDescending(m => m.Fps)
            .ToList();
    }

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

        lock (ModeGate) ModeCache.Clear();
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
                return WithLoopback(ParseDirectShow(RunCapture(ToolLocator.Ffmpeg, DirectShowArguments)));
            if (OperatingSystem.IsMacOS())
                return ParseAvFoundation(RunCapture(ToolLocator.Ffmpeg, AvFoundationArguments));
            return LoadLinux();
        }
        catch
        {
            return Unloaded();
        }
    }

    private static CaptureDeviceList LoadLinux()
    {
        var cameras = ListV4l2(V4l2ByIdFolder, V4l2SysFolder);
        try
        {
            return ParsePulse(RunCapture("pactl", PulseArguments, wantStandardOutput: true)) with { Video = cameras };
        }
        catch
        {
            return Unloaded() with { Video = cameras };
        }
    }

    internal const string V4l2ByIdFolder = "/dev/v4l/by-id";

    internal const string V4l2SysFolder = "/sys/class/video4linux";

    /// <summary>
    /// Linux kameralari aygit dosyasiyla. Once <c>/dev/v4l/by-id</c>: ad kameranin kendisini
    /// soyler ve takip cikarilinca numara degismez; yalniz <c>-video-index0</c> ile bitenler
    /// alinir, UVC her kamera icin ikinci bir meta veri dugumu acar ve o goruntu vermez.
    /// Klasor yoksa (yerlesik ya da sanal kamera) <c>/sys/class/video4linux/videoN</c>'den
    /// <c>index</c>'i 0 olan dugumler <c>/dev/videoN</c> olarak yazilir.
    /// </summary>
    internal static IReadOnlyList<string> ListV4l2(string byIdFolder, string sysFolder)
    {
        try
        {
            if (Directory.Exists(byIdFolder))
            {
                var stable = Directory.GetFileSystemEntries(byIdFolder)
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .Where(name => name.EndsWith(V4l2CaptureSuffix, StringComparison.Ordinal))
                    .Order(StringComparer.Ordinal)
                    .Select(name => V4l2ByIdFolder + "/" + name)
                    .ToList();
                if (stable.Count > 0) return stable;
            }

            if (!Directory.Exists(sysFolder)) return Array.Empty<string>();
            return Directory.GetDirectories(sysFolder, "video*")
                .Select(Path.GetFileName)
                .OfType<string>()
                .Select(name => (name, number: int.TryParse(name.AsSpan(V4l2NodePrefix.Length), out var n) ? n : -1))
                .Where(node => node.number >= 0 && V4l2Index(Path.Combine(sysFolder, node.name)) == 0)
                .OrderBy(node => node.number)
                .Select(node => RecorderArguments.V4l2DevicePrefix + node.name)
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    internal const string V4l2CaptureSuffix = "-video-index0";

    private const string V4l2NodePrefix = "video";

    private static int V4l2Index(string node)
    {
        var file = Path.Combine(node, "index");
        if (!File.Exists(file)) return 0;
        return int.TryParse(File.ReadAllText(file).Trim(), out var index) ? index : -1;
    }

    /// <summary>
    /// avfoundation video listesinde ekranlar da durur (<c>Capture screen N</c>); kamera listesi
    /// onlari almaz, ekran kaydi onlari zaten adla aciyor.
    /// </summary>
    internal static bool IsAvFoundationScreen(string name)
        => name.StartsWith("Capture screen ", StringComparison.Ordinal);

    /// <summary>
    /// Windows listesinin sonuna uygulamanin kendi yakaladigi sistem sesini ekler. dshow'un
    /// "Stereo Mix" gibi cihazlari varsa onlar once gelir; secenek her Windows makinesinde
    /// ayni yerde ve ayni adla durur, makinedeki cihazlara bagli degildir.
    /// </summary>
    internal static CaptureDeviceList WithLoopback(CaptureDeviceList list)
        => list with { Audio = list.Audio.Append(LoopbackAudio.Device).ToList() };

    /// <summary>
    /// Cihaz listesi okunamadiginda donen liste. Liste okunamayinca kaydedici yalniz "Sessiz"
    /// gosteriyordu; burada her platformun ada bagli olmayan varsayilan girdileri durur:
    /// Windows'ta uygulamanin kendi sistem sesi, macOS'ta avfoundation'in <c>default</c>'u,
    /// Linux'ta pulse'un <c>default</c> kaynagi ve varsayilan cikisin geri-dongusu.
    /// </summary>
    internal static CaptureDeviceList Unloaded() => new(
        FallbackAudio(),
        Array.Empty<string>(),
        Backend,
        Loaded: false);

    internal const string DefaultDevice = "default";

    internal const string PulseDefaultMonitor = "@DEFAULT_MONITOR@";

    private static AudioCaptureDevice[] FallbackAudio() => Backend switch
    {
        CaptureBackend.DirectShow => [LoopbackAudio.Device],
        CaptureBackend.AvFoundation =>
            [new AudioCaptureDevice(DefaultDevice, CaptureBackend.AvFoundation, AudioSourceRole.Microphone, DefaultDevice)],
        _ =>
        [
            new AudioCaptureDevice(DefaultDevice, CaptureBackend.PulseAudio, AudioSourceRole.Microphone, DefaultDevice),
            new AudioCaptureDevice(PulseDefaultMonitor, CaptureBackend.PulseAudio, AudioSourceRole.SystemAudio, PulseDefaultMonitor)
        ]
    };

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
            else if (!IsAvFoundationScreen(name))
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
