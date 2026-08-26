using System.Diagnostics;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public sealed class EncoderCapabilities : IEncoderAvailability
{
    private static readonly Lazy<EncoderCapabilities> LazyInstance = new(Load);

    public static EncoderCapabilities Instance => LazyInstance.Value;

    private readonly Dictionary<string, EncoderProbeResult> _probed = new(StringComparer.OrdinalIgnoreCase);

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
    /// Yoklamanın sonucu ve süresi. Süre karar için gerekli: yoklama geçse bile sürücü
    /// içinde geri düşe düşe tamamlanan bir yol hızlı sayılmaz. Sonuç önbelleğe alınır,
    /// bir kodlayıcı süreç ömrü boyunca bir kez yoklanır.
    /// </summary>
    public EncoderProbeResult Probe(string codec)
    {
        lock (_probed)
        {
            if (_probed.TryGetValue(codec, out var cached)) return cached;
            var result = HasEncoder(codec) ? ProbeEncoder(codec) : EncoderProbeResult.Missing(codec);
            _probed[codec] = result;
            return result;
        }
    }

    private static EncoderProbeResult ProbeEncoder(string codec)
    {
        var stopwatch = Stopwatch.StartNew();
        var succeeded = RunProbe(codec);
        stopwatch.Stop();
        return new EncoderProbeResult(codec, succeeded, stopwatch.ElapsedMilliseconds);
    }

    private static bool RunProbe(string codec)
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
            if (!process.WaitForExit(4000))
            {
                try { process.Kill(true); } catch { }
                return false;
            }
            Task.WaitAll(new Task[] { output, error }, 1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
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
