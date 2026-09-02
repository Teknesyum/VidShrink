using System.Diagnostics;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public sealed class EncoderCapabilities : IEncoderAvailability, IEncoderOptionAvailability, IEncoderOptionWarmup, IHdr10EncoderAvailability
{
    private static readonly Lazy<EncoderCapabilities> LazyInstance = new(Load);

    public static EncoderCapabilities Instance => LazyInstance.Value;

    private readonly Dictionary<string, EncoderProbeResult> _probed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _hdr10PixelFormats = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _encoderOptions = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> Encoders { get; }
    public IReadOnlySet<string> Filters { get; }
    public string Version { get; }

    private EncoderCapabilities(IReadOnlySet<string> encoders, IReadOnlySet<string> filters, string version)
    {
        Encoders = encoders;
        Filters = filters;
        Version = version;
    }

    public bool HasEncoder(string name) => Encoders.Contains(name);
    public bool HasFilter(string name) => Filters.Contains(name);

    public bool WorksAsEncoder(string codec) => Probe(codec).Succeeded;

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
        {
            if (_encoderOptions.TryGetValue(key, out var cached)) return cached;
            var supported = HasEncoder(codec) && RunOptionProbe(codec, option, value);
            _encoderOptions[key] = supported;
            return supported;
        }
    }

    private static string OptionKey(string codec, string option, string value) => $"{codec}\0{option}\0{value}";

    public string? Hdr10PixelFormat(string codec)
    {
        lock (_hdr10PixelFormats)
        {
            if (_hdr10PixelFormats.TryGetValue(codec, out var cached)) return cached;
            var (result, timedOut) = ProbeHdr10PixelFormat(codec);
            if (!timedOut) _hdr10PixelFormats[codec] = result;
            return result;
        }
    }

    /// <summary>
    /// Yoklamanın sonucu ve süresi. Süre karar için gerekli: yoklama geçse bile sürücü
    /// içinde geri düşe düşe tamamlanan bir yol hızlı sayılmaz. Sonuç önbelleğe alınır,
    /// bir kodlayıcı süreç ömrü boyunca bir kez yoklanır.
    /// </summary>
    public EncoderProbeResult Probe(string codec)
    {
        lock (_probed)
        {
            if (_probed.TryGetValue(codec, out var cached)) return cached;
            if (!HasEncoder(codec))
            {
                var missing = EncoderProbeResult.Missing(codec);
                _probed[codec] = missing;
                return missing;
            }
            var (result, timedOut) = ProbeEncoder(codec);
            if (!timedOut) _probed[codec] = result;
            return result;
        }
    }

    private static (EncoderProbeResult Result, bool TimedOut) ProbeEncoder(string codec)
    {
        var stopwatch = Stopwatch.StartNew();
        var outcome = RunProbe(codec);
        stopwatch.Stop();
        return (new EncoderProbeResult(codec, outcome == ProbeOutcome.Accepted, stopwatch.ElapsedMilliseconds),
                outcome == ProbeOutcome.TimedOut);
    }

    internal const int ProbeKillMs = 15000;

    private enum ProbeOutcome { Accepted, Rejected, TimedOut }

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
                return ProbeOutcome.TimedOut;
            }
            Task.WaitAll(new Task[] { output, error }, 1000);
            return process.ExitCode == 0 ? ProbeOutcome.Accepted : ProbeOutcome.Rejected;
        }
        catch
        {
            return ProbeOutcome.Rejected;
        }
    }

    private (string? PixelFormat, bool TimedOut) ProbeHdr10PixelFormat(string codec)
    {
        if (!HasEncoder(codec)) return (null, false);
        var timedOut = false;
        foreach (var pixelFormat in new[] { "p010le", "yuv420p10le" })
            switch (RunProbe(codec, pixelFormat))
            {
                case ProbeOutcome.Accepted: return (pixelFormat, false);
                case ProbeOutcome.TimedOut: timedOut = true; break;
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
                return ProbeOutcome.TimedOut;
            }
            Task.WaitAll(new Task[] { output, error }, 1000);
            return PixelFormatAccepted(process.ExitCode, error.Result) ? ProbeOutcome.Accepted : ProbeOutcome.Rejected;
        }
        catch
        {
            return ProbeOutcome.Rejected;
        }
    }

    internal static bool PixelFormatAccepted(int exitCode, string diagnostic)
        => exitCode == 0
           && !diagnostic.Contains("Incompatible pixel format", StringComparison.OrdinalIgnoreCase)
           && !diagnostic.Contains("auto-selecting format", StringComparison.OrdinalIgnoreCase);

    private static bool RunOptionProbe(string codec, string option, string value)
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
                return false;
            }
            Task.WaitAll(new Task[] { stdout, stderr }, 1000);
            var diagnostic = stderr.Result;
            return process.ExitCode == 0
                   && !diagnostic.Contains("Error parsing option", StringComparison.OrdinalIgnoreCase)
                   && !diagnostic.Contains("Option not found", StringComparison.OrdinalIgnoreCase)
                   && !diagnostic.Contains("Unrecognized option", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

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
            return Parse(encoders, filters, version);
        }
        catch
        {
            return new EncoderCapabilities(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "unknown");
        }
    }

    private static string RunCapture(IEnumerable<string> args)
    {
        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        return output;
    }
}
