using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VidShrink.Core.Editing;

/// <summary>
/// Otomatik kesimin ayarlari. Esik ve tur taramayi belirler; en kisa sure ve kenar payi
/// yalniz plani, bu yuzden onlar degisince yeniden taranmaz.
/// </summary>
public sealed record SilenceCutOptions
{
    public const double DefaultThresholdDb = -35;
    public const double DefaultMinSeconds = 0.5;
    public const double DefaultPaddingSeconds = 0.1;
    public const double MinThresholdDb = -90;
    public const double MaxThresholdDb = -5;
    public const double MaxSeconds = 60;

    public bool Silence { get; init; } = true;

    public bool Black { get; init; }

    public double ThresholdDb { get; init; } = DefaultThresholdDb;

    public double MinSeconds { get; init; } = DefaultMinSeconds;

    public double PaddingSeconds { get; init; } = DefaultPaddingSeconds;

    public static SilenceCutOptions Default { get; } = new();

    public bool IsValid =>
        (Silence || Black)
        && double.IsFinite(ThresholdDb) && ThresholdDb >= MinThresholdDb && ThresholdDb <= MaxThresholdDb
        && double.IsFinite(MinSeconds) && MinSeconds > 0 && MinSeconds <= MaxSeconds
        && double.IsFinite(PaddingSeconds) && PaddingSeconds >= 0 && PaddingSeconds <= MaxSeconds;

    /// <summary>Iki ayar ayni taramayi paylasir mi: tur ve esik ayni.</summary>
    public bool SameScan(SilenceCutOptions other)
        => other is not null && other.Silence == Silence && other.Black == Black && other.ThresholdDb.Equals(ThresholdDb);
}

/// <summary>Taramanin ham sonucu: kaynak saniyesinde sessiz ve siyah araliklar.</summary>
public sealed record SilenceScan(IReadOnlyList<IdleSpan> Silence, IReadOnlyList<IdleSpan> Black, SilenceCutOptions Options)
{
    public IEnumerable<IdleSpan> All => Silence.Concat(Black);
}

/// <summary>
/// ffmpeg <c>silencedetect</c>/<c>blackdetect</c> ile sessiz ve siyah araliklari bulur ve
/// kesilecek kaynak araliklarini planlar. Ayristirici ve plan saf fonksiyondur; surec
/// BelowNormal oncelikte, <c>-threads 2</c> ile kosar ve iptal edilince oldurulur.
/// </summary>
public static class SilenceCut
{
    public const double ScanMinSeconds = 0.1;
    public const string BlackPixelThreshold = "0.10";
    public const string BlackPictureThreshold = "0.98";
    public const int Threads = 2;

    private static readonly Regex SilenceLine = new(
        @"silence_(?<kind>start|end):\s*(?<value>-?[0-9]+(?:\.[0-9]+)?(?:[eE][-+]?[0-9]+)?)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BlackLine = new(
        @"black_start:\s*(?<start>[0-9]+(?:\.[0-9]+)?)\s+black_end:\s*(?<end>[0-9]+(?:\.[0-9]+)?)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ProgressLine = new(
        @"^out_time_(?:us|ms)=(?<value>[0-9]+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Multiline);

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static IReadOnlyList<string> Arguments(string sourcePath, SilenceCutOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IsValid) throw new ArgumentOutOfRangeException(nameof(options), options, "Gecersiz kesim ayari");

        var threads = Threads.ToString(CultureInfo.InvariantCulture);
        var args = new List<string>
        {
            "-hide_banner", "-nostdin", "-nostats",
            "-threads", threads,
            "-i", sourcePath
        };
        if (options.Silence)
            args.AddRange(new[] { "-map", "0:a:0?", "-af", $"silencedetect=noise={Number(options.ThresholdDb)}dB:d={Number(ScanMinSeconds)}" });
        else
            args.Add("-an");
        if (options.Black)
            args.AddRange(new[] { "-map", "0:v:0?", "-vf", $"blackdetect=d={Number(ScanMinSeconds)}:pic_th={BlackPictureThreshold}:pix_th={BlackPixelThreshold}" });
        else
            args.Add("-vn");
        args.AddRange(new[] { "-sn", "-dn", "-threads", threads, "-progress", "pipe:1", "-f", "null", "-" });
        return args;
    }

    public static IReadOnlyList<IdleSpan> ParseSilence(string standardError, double durationSeconds)
    {
        var spans = new List<IdleSpan>();
        double? open = null;
        foreach (Match match in SilenceLine.Matches(standardError ?? string.Empty))
        {
            var value = double.Parse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (match.Groups["kind"].Value == "start")
            {
                open = Math.Max(0, value);
            }
            else if (open is { } start)
            {
                if (value > start) spans.Add(new IdleSpan(start, value));
                open = null;
            }
        }

        if (open is { } tail && durationSeconds > tail) spans.Add(new IdleSpan(tail, durationSeconds));
        return spans;
    }

    public static IReadOnlyList<IdleSpan> ParseBlack(string standardError)
    {
        var spans = new List<IdleSpan>();
        foreach (Match match in BlackLine.Matches(standardError ?? string.Empty))
        {
            var start = double.Parse(match.Groups["start"].Value, CultureInfo.InvariantCulture);
            var end = double.Parse(match.Groups["end"].Value, CultureInfo.InvariantCulture);
            if (end > start) spans.Add(new IdleSpan(start, end));
        }

        return spans;
    }

    /// <summary><c>-progress</c> satirindaki islenmis sure (saniye); baska satirda <c>null</c>.</summary>
    public static double? ParseProgress(string line)
    {
        var match = ProgressLine.Match(line ?? string.Empty);
        if (!match.Success) return null;
        return long.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture) / 1_000_000d;
    }

    /// <summary>
    /// Kesilecek kaynak araliklari: kaynaga kirpilir, ortusen ya da degen araliklar birlesir,
    /// <see cref="SilenceCutOptions.MinSeconds"/> altindakiler atilir, kalanlar her yandan
    /// kenar payi kadar daraltilir. Dosyanin basina ve sonuna degen kenarda pay birakilmaz.
    /// </summary>
    public static IReadOnlyList<IdleSpan> Plan(IEnumerable<IdleSpan> detected, double durationSeconds, SilenceCutOptions options)
    {
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(options);
        if (!(durationSeconds > 0)) return Array.Empty<IdleSpan>();

        var merged = new List<IdleSpan>();
        foreach (var span in detected
                     .Select(s => new IdleSpan(Math.Max(0, s.Start), Math.Min(durationSeconds, s.End)))
                     .Where(s => s.End > s.Start)
                     .OrderBy(s => s.Start))
        {
            if (merged.Count > 0 && span.Start <= merged[^1].End)
                merged[^1] = merged[^1] with { End = Math.Max(merged[^1].End, span.End) };
            else
                merged.Add(span);
        }

        var cuts = new List<IdleSpan>();
        foreach (var span in merged)
        {
            if (span.Length < options.MinSeconds) continue;
            var start = span.Start <= 0 ? 0 : span.Start + options.PaddingSeconds;
            var end = span.End >= durationSeconds ? durationSeconds : span.End - options.PaddingSeconds;
            if (end > start) cuts.Add(new IdleSpan(start, end));
        }

        return cuts;
    }

    public static IReadOnlyList<(long Start, long End)> ToTicks(IReadOnlyList<IdleSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);
        return spans
            .Select(s => (EditTime.FromSeconds(s.Start), EditTime.FromSeconds(s.End)))
            .Where(r => r.Item2 > r.Item1)
            .ToArray();
    }

    public static double Total(IReadOnlyList<IdleSpan> spans) => spans.Sum(s => s.Length);

    public static async Task<SilenceScan> ScanAsync(
        string ffmpegPath,
        string sourcePath,
        SilenceCutOptions options,
        double durationSeconds,
        IProgress<double>? progress = null,
        CancellationToken ct = default,
        Action<Process>? started = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(ffmpegPath);
        var args = Arguments(sourcePath, options);
        ct.ThrowIfCancellationRequested();

        var info = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in args) info.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = info };
        process.Start();
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }

        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using (ct.Register(() => AudioPeaks.Kill(process)))
        {
            try
            {
                started?.Invoke(process);
                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync(CancellationToken.None).ConfigureAwait(false)) is not null)
                {
                    if (progress is null || !(durationSeconds > 0) || ParseProgress(line) is not { } done) continue;
                    progress.Report(Math.Clamp(done / durationSeconds, 0, 1));
                }
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
            }
            finally
            {
                if (ct.IsCancellationRequested) AudioPeaks.Kill(process);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        var error = await stderr.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            if (AudioPeaks.ReportsNoAudio(error)) return new SilenceScan(Array.Empty<IdleSpan>(), Array.Empty<IdleSpan>(), options);
            throw new InvalidOperationException($"ffmpeg sessizlik taramasi basarisiz ({process.ExitCode}): {error.Trim()}");
        }

        progress?.Report(1);
        return new SilenceScan(
            options.Silence ? ParseSilence(error, durationSeconds) : Array.Empty<IdleSpan>(),
            options.Black ? ParseBlack(error) : Array.Empty<IdleSpan>(),
            options);
    }
}
