using System.Diagnostics;
using System.Globalization;

namespace VidShrink.Ffmpeg;

/// <summary>
/// Bir dosyanin anahtar kare damgalari. T32/K1 olctu: cikarma maliyeti 1080p'de ~85 ms,
/// 4K'da ~200 ms ve tekrar cagrida dusmuyor — yani **bir kere yapilip saklanacak** bir is.
/// Hizali cekim hizasiza gore 1080p'de medyani 191 ms'den 135 ms'e, p90'i 780 ms'den
/// 260 ms'e indiriyor.
/// </summary>
public sealed record KeyframeIndex
{
    public required string FilePath { get; init; }
    public required IReadOnlyList<double> Stamps { get; init; }

    /// <summary>Dizini cikarmanin duvar saati. Saklama kararini bu sayi verdi.</summary>
    public required TimeSpan BuildTime { get; init; }

    public bool IsEmpty => Stamps.Count == 0;

    /// <summary>Anahtar kareler arasi ortalama aralik; dizin bosken <c>NaN</c>.</summary>
    public double AverageGapSeconds => Stamps.Count > 1
        ? (Stamps[^1] - Stamps[0]) / (Stamps.Count - 1)
        : double.NaN;

    /// <summary>
    /// Verilen andan onceki (veya tam o andaki) en yakin anahtar kare. Cekim buraya
    /// hizalanirsa ffmpeg GoP'un tamamini cozmek zorunda kalmaz.
    /// </summary>
    public double Floor(double atSeconds)
    {
        if (Stamps.Count == 0) return atSeconds;
        var chosen = Stamps[0];
        foreach (var stamp in Stamps)
        {
            if (stamp > atSeconds + 1e-6) break;
            chosen = stamp;
        }
        return chosen;
    }

    /// <summary>Verilen ana en yakin anahtar kare, oncesi ya da sonrasi fark etmez.</summary>
    public double Nearest(double atSeconds)
    {
        if (Stamps.Count == 0) return atSeconds;
        var best = Stamps[0];
        foreach (var stamp in Stamps)
            if (Math.Abs(stamp - atSeconds) < Math.Abs(best - atSeconds)) best = stamp;
        return best;
    }

    /// <summary>
    /// Bozuk dosya ya da okunamayan akis icin <c>null</c> doner, istisna atmaz.
    /// stdout ve stderr **ayni anda** bosaltilir; bosaltilmazsa boru dolar ve surec asilir
    /// (docs/taramalar/RAPOR.md:27).
    /// </summary>
    public static async Task<KeyframeIndex?> BuildAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return null;

        var args = new[]
        {
            "-hide_banner", "-v", "error",
            "-select_streams", "v:0",
            "-show_entries", "packet=pts_time,flags",
            "-of", "csv=p=0",
            filePath
        };

        var clock = Stopwatch.StartNew();
        try
        {
            using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, args) };
            process.Start();
            using var cancellationRegistration = ct.Register(() => TryKill(process));

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            var stdout = await stdoutTask;
            _ = await stderrTask;
            await process.WaitForExitAsync(ct);
            clock.Stop();

            if (process.ExitCode != 0) return null;

            var stamps = new List<double>();
            foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Trim().Split(',');
                if (parts.Length < 2 || !parts[1].Contains('K')) continue;
                if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var pts) && pts >= 0)
                    stamps.Add(pts);
            }

            stamps.Sort();
            return new KeyframeIndex { FilePath = filePath, Stamps = stamps, BuildTime = clock.Elapsed };
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }
}
