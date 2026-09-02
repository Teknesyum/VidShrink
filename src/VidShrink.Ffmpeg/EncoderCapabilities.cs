using System.Diagnostics;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

/// <summary>
/// Yoklayan üç değerli cevabın arayüzü. <see cref="IEncoderAvailability.EncoderState"/>
/// süreç doğurmaz, yalnız bilineni okur; bu arayüz <b>ölçer</b>. Çağıran taraf ölçümü
/// kendi arka planına aldıysa (App'teki geçit) üçüncü cevabı buradan alır — iki değerli
/// <c>WorksAsEncoder</c>den geçirmek üçüncü cevabı geçidin girişinde yok ediyordu.
/// Arayüz Core'da değil burada, çünkü Core'daki <see cref="IEncoderAvailability"/> saf
/// okuma sözleşmesi; ölçen yol ffmpeg'e bağlı ve o bağ bu derlemede.
/// </summary>
public interface IEncoderProbeState
{
    EncoderProbeState WorksAsEncoderState(string codec);
}

public sealed class EncoderCapabilities : IEncoderAvailability, IEncoderOptionAvailability, IEncoderOptionWarmup, IHdr10EncoderAvailability, IHdr10ProbeAvailability, IEncoderProbeState
{
    private static readonly object InstanceGate = new();
    private static EncoderCapabilities? _instance;
    private static long _lastLoadTicks;

    /// <summary>Başarısız açılıştan sonra yeniden denemeden önce beklenen süre.</summary>
    internal const int ReloadAfterFailureMs = 5000;

    /// <summary>
    /// Kodlayıcı listesi bir kez okunur. Okuma <b>başarısızsa</b> sonuç kalıcı değildir:
    /// geçici bir açılış hatası (yük altında <c>Process.Start</c> tıkanması, ffmpeg'in o
    /// anda kilitli olması) boş kodlayıcı kümesini süreç ömrü boyunca sabitliyordu ve
    /// <see cref="HasEncoder"/> kalıcı olarak yanlış oluyordu. Artık en fazla
    /// <see cref="ReloadAfterFailureMs"/> ms'de bir yeniden denenir.
    /// </summary>
    public static EncoderCapabilities Instance
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

    private readonly Dictionary<string, EncoderProbeResult> _probed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _hdr10PixelFormats = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _encoderOptions = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> Encoders { get; }
    public IReadOnlySet<string> Filters { get; }
    public string Version { get; }

    /// <summary>Kodlayıcı listesi gerçekten okunabildi mi. false ise küme boş ve geçicidir.</summary>
    public bool Loaded { get; }

    /// <summary>Ölçüm dikişi: yoklamayı ffmpeg'siz sürdürmek için testlerin taktığı yer.</summary>
    internal Func<string, ProbeOutcome>? EncoderProbeHook;

    /// <summary>Ölçüm dikişi: seçenek yoklamasının ffmpeg'siz karşılığı.</summary>
    internal Func<string, string, string, ProbeOutcome>? OptionProbeHook;

    /// <summary>Ölçüm dikişi: HDR10 piksel biçimi yoklamasının ffmpeg'siz karşılığı.</summary>
    internal Func<string, string, ProbeOutcome>? Hdr10ProbeHook;

    private EncoderCapabilities(IReadOnlySet<string> encoders, IReadOnlySet<string> filters, string version, bool loaded = true)
    {
        Encoders = encoders;
        Filters = filters;
        Version = version;
        Loaded = loaded;
    }

    public bool HasEncoder(string name) => Encoders.Contains(name);
    public bool HasFilter(string name) => Filters.Contains(name);

    /// <summary>
    /// İki değerli cevap, ama <b>ölçülemeyen yoklama "yok" değil.</b> Yoklama sonuca
    /// varamadığında (zaman aşımı ya da sürecin hiç başlayamaması) yoklamadan önce
    /// bilinene, yani ffmpeg'in kendi kodlayıcı listesine düşülür. Ölçüm ekleyemediğinde
    /// zaten bilinen bilgi silinmez: <c>docs/olcumler/surucu-yoklugu.md:318-322</c>'de orta
    /// yükte 12 yoklamanın 9'u zaman aşımına uğrayıp <b>çıkış kodu 0 ile kodlayan</b> bir
    /// kodlayıcıya "yok" diyordu. Listede olmayan kodlayıcı ölçülmüş bir yokluktur ve
    /// burada da <c>false</c> döner. Üç durumu ayrı ayrı isteyen çağıran
    /// <see cref="WorksAsEncoderState"/> okur.
    /// </summary>
    public bool WorksAsEncoder(string codec)
    {
        var result = Probe(codec);
        return result.Measured ? result.Succeeded : HasEncoder(codec);
    }

    /// <summary>Yoklayan üç değerli cevap: ölçer, sonra üç durumdan birini döner.</summary>
    public EncoderProbeState WorksAsEncoderState(string codec) => Probe(codec).State;

    /// <summary>
    /// Bilineni okur, süreç doğurmaz. Kodlayıcı ffmpeg'in listesinde yoksa cevap
    /// süreçsiz de kesindir; listede olup henüz yoklanmamış ya da yoklaması sonuca
    /// varamamış kodlayıcı <see cref="EncoderProbeState.Unmeasured"/> döner. Ölçmek
    /// isteyen <see cref="Probe"/> çağırır ve onu arka planda koşturur.
    /// </summary>
    public EncoderProbeState EncoderState(string codec)
    {
        lock (_probed)
            if (_probed.TryGetValue(codec, out var cached)) return cached.State;

        return HasEncoder(codec) ? EncoderProbeState.Unmeasured : EncoderProbeState.NotWorking;
    }

    /// <summary>
    /// Isıtılmış sonucu okur. Süreç doğurmaz: argüman üretimi bu yolu kullanıyor ve saf
    /// kalması gerekiyor. Hiç ısıtılmamış bir seçenek desteklenmiyor sayılır; ölçüm
    /// <see cref="WarmEncoderOption"/> ile arka planda yapılır.
    /// </summary>
    public bool SupportsEncoderOption(string codec, string option, string value)
    {
        lock (_encoderOptions)
            return _encoderOptions.TryGetValue(OptionKey(codec, option, value), out var cached) && cached;
    }

    /// <summary>
    /// Seçeneği bir kez ffmpeg'e sorup sonucu süreç ömrü boyunca saklar. Süreç doğuran tek
    /// seçenek yolu burasıdır; çağıran tarafın arka planda koşturması gerekir.
    /// </summary>
    public bool WarmEncoderOption(string codec, string option, string value)
    {
        var key = OptionKey(codec, option, value);
        lock (_encoderOptions)
            if (_encoderOptions.TryGetValue(key, out var cached)) return cached;

        if (!HasEncoder(codec))
        {
            lock (_encoderOptions) _encoderOptions[key] = false;
            return false;
        }

        var outcome = (OptionProbeHook ?? RunOptionProbe)(codec, option, value);

        if (outcome == ProbeOutcome.Unmeasured) return false;

        var supported = outcome == ProbeOutcome.Accepted;
        lock (_encoderOptions)
        {
            if (_encoderOptions.TryGetValue(key, out var raced)) return raced;
            _encoderOptions[key] = supported;
        }
        return supported;
    }

    private static string OptionKey(string codec, string option, string value) => $"{codec}\0{option}\0{value}";

    /// <summary>
    /// Kodlayıcının HDR10 için kabul ettiği piksel biçimi, yoksa <c>null</c>. Ölçülemeyen
    /// yoklama da <c>null</c> döner — çağıran ikisini ayırmak istiyorsa
    /// <see cref="Hdr10State"/> okur — ama önbelleğe yazılmaz.
    /// </summary>
    public string? Hdr10PixelFormat(string codec) => Hdr10Probe(codec).PixelFormat;

    /// <summary>
    /// <see cref="EncoderState"/> ile aynı kural: bilineni okur, süreç doğurmaz. Ölçmek
    /// isteyen <see cref="Hdr10PixelFormat"/> çağırır. Mühürlenmiş bir biçim varsa
    /// <see cref="EncoderProbeState.Working"/>, mühürlenmiş yokluk varsa
    /// <see cref="EncoderProbeState.NotWorking"/>, hiç bilinmiyorsa
    /// <see cref="EncoderProbeState.Unmeasured"/>.
    /// </summary>
    public EncoderProbeState Hdr10State(string codec)
    {
        lock (_hdr10PixelFormats)
            if (_hdr10PixelFormats.TryGetValue(codec, out var cached))
                return cached is null ? EncoderProbeState.NotWorking : EncoderProbeState.Working;

        return HasEncoder(codec) ? EncoderProbeState.Unmeasured : EncoderProbeState.NotWorking;
    }

    private (string? PixelFormat, bool Measured) Hdr10Probe(string codec)
    {
        lock (_hdr10PixelFormats)
            if (_hdr10PixelFormats.TryGetValue(codec, out var cached)) return (cached, true);

        var (result, timedOut) = ProbeHdr10PixelFormat(codec);
        if (timedOut) return (result, false);

        lock (_hdr10PixelFormats)
        {
            if (_hdr10PixelFormats.TryGetValue(codec, out var raced)) return (raced, true);
            _hdr10PixelFormats[codec] = result;
        }
        return (result, true);
    }

    /// <summary>
    /// Yoklamanın sonucu ve süresi. Süre karar için gerekli: yoklama geçse bile sürücü
    /// içinde geri düşe düşe tamamlanan bir yol hızlı sayılmaz. Sonuç önbelleğe alınır,
    /// bir kodlayıcı süreç ömrü boyunca bir kez yoklanır.
    /// </summary>
    public EncoderProbeResult Probe(string codec)
    {
        lock (_probed)
            if (_probed.TryGetValue(codec, out var cached)) return cached;

        if (!HasEncoder(codec))
        {
            var missing = EncoderProbeResult.Missing(codec);
            lock (_probed) _probed[codec] = missing;
            return missing;
        }

        var result = ProbeEncoder(codec);

        if (!result.Measured) return result;

        lock (_probed)
        {
            if (_probed.TryGetValue(codec, out var raced)) return raced;
            _probed[codec] = result;
        }
        return result;
    }

    private EncoderProbeResult ProbeEncoder(string codec)
    {
        var stopwatch = Stopwatch.StartNew();
        var outcome = (EncoderProbeHook ?? RunProbe)(codec);
        stopwatch.Stop();
        return outcome == ProbeOutcome.Unmeasured
            ? EncoderProbeResult.Unmeasured(codec, stopwatch.ElapsedMilliseconds)
            : new EncoderProbeResult(codec, outcome == ProbeOutcome.Accepted, stopwatch.ElapsedMilliseconds);
    }

    internal const int ProbeKillMs = 15000;

    /// <summary>
    /// Yoklamanın üç sonucu. <see cref="Unmeasured"/> hem zaman aşımını hem sürecin hiç
    /// başlayamamasını taşır; ikisi de "kodlayıcı çalışmıyor" değildir.
    /// </summary>
    internal enum ProbeOutcome { Accepted, Rejected, Unmeasured }

    private static ProbeOutcome RunProbe(string codec)
    {
        try
        {
            var args = new[]
            {
                "-hide_banner", "-loglevel", "error",
                "-f", "lavfi", "-i", "testsrc2=size=256x256:rate=30:duration=0.1",
                "-c:v", codec, "-frames:v", "1",
                "-f", "null", OperatingSystem.IsWindows() ? "NUL" : "/dev/null"
            };
            using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(ProbeKillMs))
            {
                try { process.Kill(true); } catch { }
                return ProbeOutcome.Unmeasured;
            }
            Task.WaitAll(new Task[] { output, error }, 1000);
            return process.ExitCode == 0 ? ProbeOutcome.Accepted : ProbeOutcome.Rejected;
        }
        catch
        {
            return ProbeOutcome.Unmeasured;
        }
    }

    private (string? PixelFormat, bool TimedOut) ProbeHdr10PixelFormat(string codec)
    {
        if (!HasEncoder(codec)) return (null, false);
        var timedOut = false;
        foreach (var pixelFormat in new[] { "p010le", "yuv420p10le" })
            switch ((Hdr10ProbeHook ?? RunProbe)(codec, pixelFormat))
            {
                case ProbeOutcome.Accepted: return (pixelFormat, timedOut);
                case ProbeOutcome.Unmeasured: timedOut = true; break;
            }
        return (null, timedOut);
    }

    private static ProbeOutcome RunProbe(string codec, string pixelFormat)
    {
        try
        {
            var args = new[]
            {
                "-hide_banner", "-loglevel", "warning",
                "-f", "lavfi", "-i", "testsrc2=size=256x256:rate=30:duration=0.1",
                "-vf", $"format={pixelFormat}", "-c:v", codec, "-pix_fmt", pixelFormat,
                "-color_primaries", "bt2020", "-color_trc", "smpte2084", "-colorspace", "bt2020nc",
                "-frames:v", "1", "-f", "null", OperatingSystem.IsWindows() ? "NUL" : "/dev/null"
            };
            using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(ProbeKillMs))
            {
                try { process.Kill(true); } catch { }
                return ProbeOutcome.Unmeasured;
            }
            Task.WaitAll(new Task[] { output, error }, 1000);
            return PixelFormatAccepted(process.ExitCode, error.Result) ? ProbeOutcome.Accepted : ProbeOutcome.Rejected;
        }
        catch
        {
            return ProbeOutcome.Unmeasured;
        }
    }

    internal static bool PixelFormatAccepted(int exitCode, string diagnostic)
        => exitCode == 0
           && !diagnostic.Contains("Incompatible pixel format", StringComparison.OrdinalIgnoreCase)
           && !diagnostic.Contains("auto-selecting format", StringComparison.OrdinalIgnoreCase);

    private static ProbeOutcome RunOptionProbe(string codec, string option, string value)
    {
        try
        {
            var args = new[]
            {
                "-hide_banner", "-loglevel", "info",
                "-f", "lavfi", "-i", "testsrc2=size=256x256:rate=30:duration=0.1",
                "-c:v", codec, option, value, "-frames:v", "1",
                "-f", "null", OperatingSystem.IsWindows() ? "NUL" : "/dev/null"
            };
            using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(ProbeKillMs))
            {
                try { process.Kill(true); } catch { }
                return ProbeOutcome.Unmeasured;
            }
            Task.WaitAll(new Task[] { stdout, stderr }, 1000);
            return OptionAccepted(process.ExitCode, stderr.Result) ? ProbeOutcome.Accepted : ProbeOutcome.Rejected;
        }
        catch { return ProbeOutcome.Unmeasured; }
    }

    /// <summary>
    /// Yoklamanin karari: ffmpeg verilen secenegi kabul etti mi. Surec baslatmaktan ayri
    /// durur; olcu, verilen cikis kodunun ve stderr metninin uretecegi karari surec
    /// kosturmadan pimler.
    /// <para>
    /// "Cikis kodu 0 iken dusurulen secenek" sorusunun cevabi burada ikinci kez yazilmaz;
    /// tek sozluk <see cref="FfmpegDiagnostics"/>. Yoklama teslim yoluyla ayni metni ayni
    /// desenlerle okur.
    /// </para>
    /// </summary>
    internal static bool OptionAccepted(int exitCode, string diagnostic)
        => exitCode == 0 && FfmpegDiagnostics.DroppedOptionLines(diagnostic).Count == 0;

    public static EncoderCapabilities Parse(string encodersOutput, string filtersOutput, string versionOutput)
        => new(ParseEncoders(encodersOutput), ParseFilters(filtersOutput), ParseVersion(versionOutput));

    internal static HashSet<string> ParseEncoders(string output)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith('-') || line.Contains('=')) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            if (parts[0].Length != 6 || !parts[0].All(c => c == '.' || char.IsUpper(c))) continue;
            names.Add(parts[1]);
        }
        return names;
    }

    internal static HashSet<string> ParseFilters(string output)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith('-') || line.Contains('=')) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;
            if (!parts[0].All(c => c is '.' or '|' or 'T' or 'S' or 'C')) continue;
            names.Add(parts[1]);
        }
        return names;
    }

    internal static string ParseVersion(string output)
    {
        var line = output.Split('\n').FirstOrDefault()?.Trim() ?? "";
        return line.Replace("ffmpeg version ", "", StringComparison.OrdinalIgnoreCase);
    }

    private static EncoderCapabilities Load()
    {
        try
        {
            var encoders = RunCapture(new[] { "-hide_banner", "-encoders" });
            var filters = RunCapture(new[] { "-hide_banner", "-filters" });
            var version = RunCapture(new[] { "-version" });
            var parsed = Parse(encoders, filters, version);
            return parsed.Encoders.Count == 0 ? Unloaded() : parsed;
        }
        catch
        {
            return Unloaded();
        }
    }

    private static EncoderCapabilities Unloaded() => new(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        "unknown",
        loaded: false);

    internal const int CaptureKillMs = 5000;

    /// <summary>
    /// Çıktıyı okur. Okuma <b>asenkron</b> başlar: <c>ReadToEnd()</c> akış kapanana kadar
    /// bloke olduğu için ondan sonra gelen <c>WaitForExit(5000)</c> fiilen etkisizdi ve
    /// tıkanan bir ffmpeg açılışı süresiz bekletiyordu. Sınır aşılırsa süreç öldürülür ve
    /// fırlatılır; <see cref="Load"/> bunu "okunamadı" sayar, kalıcı boş küme yazmaz.
    /// </summary>
    private static string RunCapture(IEnumerable<string> args)
    {
        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(CaptureKillMs))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException($"ffmpeg {CaptureKillMs} ms içinde dönmedi.");
        }
        Task.WaitAll(new Task[] { output, error }, 1000);
        return output.Result;
    }
}
