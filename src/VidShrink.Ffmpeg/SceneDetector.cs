using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;
using VidShrink.Core.Editing;

namespace VidShrink.Ffmpeg;

public sealed record SceneScan(
    bool Ok,
    IReadOnlyList<SceneScore> Candidates,
    IReadOnlyList<ProbeFrame> Frames,
    TimeSpan Elapsed,
    string Error);

public static class SceneDetector
{
    public const double BaseThreshold = 0.012;
    public const int ProbeWidth = 640;
    public const int ProbeCrf = 23;
    public const string ProbePreset = "ultrafast";
    public const int CutThreads = 2;

    public static string[] ScanArgs(string path, string vstatsPath, double baseThreshold = BaseThreshold)
        => new[]
        {
            "-hide_banner", "-loglevel", "info", "-nostats",
            "-i", path,
            "-filter_complex", FormattableString.Invariant(
                $"[0:v]split=2[a][b];[a]select='gte(scene,{baseThreshold:0.#####})',metadata=print[sc];[b]scale={ProbeWidth}:-2[enc]"),
            "-map", "[sc]", "-f", "null", "-",
            "-map", "[enc]", "-an",
            "-c:v", "libx264", "-preset", ProbePreset, "-crf", ProbeCrf.ToString(CultureInfo.InvariantCulture),
            "-vstats_file", vstatsPath,
            "-f", "null", "-"
        };

    public static async Task<SceneScan> ScanAsync(string path, double baseThreshold = BaseThreshold, CancellationToken ct = default)
    {
        var clock = Stopwatch.StartNew();
        var vstatsPath = Path.Combine(Path.GetTempPath(), $"vidshrink-sahne-{Guid.NewGuid():N}.log");
        Process process;
        try
        {
            process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, ScanArgs(path, vstatsPath, baseThreshold)) };
            process.Start();
        }
        catch (Exception ex)
        {
            clock.Stop();
            return new SceneScan(false, Array.Empty<SceneScore>(), Array.Empty<ProbeFrame>(), clock.Elapsed, ex.Message);
        }

        try
        {
            using (process)
            using (ct.Register(() => TryKill(process)))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(CancellationToken.None);
                _ = await stdout;
                var log = await stderr;
                clock.Stop();
                ct.ThrowIfCancellationRequested();

                if (process.ExitCode != 0)
                    return new SceneScan(false, Array.Empty<SceneScore>(), Array.Empty<ProbeFrame>(), clock.Elapsed, FfmpegRunner.Tail(log));

                var vstats = File.Exists(vstatsPath) ? await File.ReadAllTextAsync(vstatsPath, CancellationToken.None) : string.Empty;
                return new SceneScan(true, ParseScores(log), ParseVstats(vstats), clock.Elapsed, string.Empty);
            }
        }
        finally
        {
            try { File.Delete(vstatsPath); } catch (IOException) { }
        }
    }

    /// <summary>
    /// Duzenleyicinin hafif taramasi: kodlama yok, yalniz <see cref="SceneMap.DefaultThreshold"/>
    /// ustundeki kareler yazilir. Ilk kare her zaman gecer: hic kare secilmezse bos hedefin
    /// kodlayicisi acilamaz ve ffmpeg -22 ile duser; o kare sonuctan skoruyla elenir. Ikinci
    /// cikis butun kareleri bos hedefe akitir ki <c>-progress</c> kaynagin suresiyle ilerlesin.
    /// </summary>
    public static string[] CutArgs(string path, double threshold = SceneMap.DefaultThreshold)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!double.IsFinite(threshold) || threshold <= 0 || threshold >= 1)
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Esik 0 ile 1 arasinda olmalidir");

        var threads = CutThreads.ToString(CultureInfo.InvariantCulture);
        return new[]
        {
            "-hide_banner", "-nostdin", "-loglevel", "info", "-nostats",
            "-progress", "pipe:1",
            "-filter_complex_threads", threads,
            "-threads", threads,
            "-i", path,
            "-filter_complex", FormattableString.Invariant(
                $"[0:v:0]split=2[a][b];[a]select='gte(scene,{threshold:0.#####})+eq(n,0)',metadata=print[sc]"),
            "-map", "[sc]", "-an", "-sn", "-dn", "-f", "null", "-",
            "-map", "[b]", "-an", "-sn", "-dn", "-f", "null", "-"
        };
    }

    /// <summary>
    /// Sahne degisimlerinin kaynak saniyesi, artan sirada. Tek ffmpeg sureci, BelowNormal
    /// oncelik, <c>-threads 2</c>; iptalde surec oldurulur ve <see cref="OperationCanceledException"/>
    /// atilir, sifir olmayan cikis <see cref="InvalidOperationException"/> olur.
    /// </summary>
    public static async Task<IReadOnlyList<double>> CutsAsync(
        string path,
        double durationSeconds,
        IProgress<double>? progress = null,
        CancellationToken ct = default,
        Action<Process>? started = null)
    {
        var args = CutArgs(path);
        ct.ThrowIfCancellationRequested();

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        process.Start();
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }

        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using (ct.Register(() => TryKill(process)))
        {
            try
            {
                started?.Invoke(process);
                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync(CancellationToken.None).ConfigureAwait(false)) is not null)
                {
                    if (progress is null || !(durationSeconds > 0) || SilenceCut.ParseProgress(line) is not { } done) continue;
                    progress.Report(Math.Clamp(done / durationSeconds, 0, 1));
                }
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
            }
            finally
            {
                if (ct.IsCancellationRequested) TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        var log = await stderr.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg sahne taramasi basarisiz ({process.ExitCode}): {FfmpegRunner.Tail(log)}");

        progress?.Report(1);
        return ParseScores(log)
            .Where(s => s.Score >= SceneMap.DefaultThreshold && s.Time > 0)
            .Select(s => s.Time)
            .OrderBy(t => t)
            .ToArray();
    }

    public static IReadOnlyList<SceneScore> ParseScores(string log)
    {
        var scores = new List<SceneScore>();
        var time = double.NaN;
        foreach (var raw in log.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            var timeAt = line.IndexOf("pts_time:", StringComparison.Ordinal);
            if (timeAt >= 0)
            {
                var token = FirstToken(line[(timeAt + "pts_time:".Length)..]);
                time = double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? t : double.NaN;
                continue;
            }

            var scoreAt = line.IndexOf("lavfi.scene_score=", StringComparison.Ordinal);
            if (scoreAt < 0) continue;

            var value = FirstToken(line[(scoreAt + "lavfi.scene_score=".Length)..]);
            if (!double.IsNaN(time)
                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var score))
                scores.Add(new SceneScore(time, score));
            time = double.NaN;
        }
        return scores;
    }

    public static IReadOnlyList<ProbeFrame> ParseVstats(string vstats)
    {
        var frames = new List<ProbeFrame>();
        foreach (var raw in vstats.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            if (!TryField(line, "out=", out var outIndex) || outIndex != "1") continue;
            if (!TryField(line, "f_size=", out var sizeToken)) continue;
            if (!TryField(line, "time=", out var timeToken)) continue;

            if (long.TryParse(sizeToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)
                && double.TryParse(timeToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
                frames.Add(new ProbeFrame(time, size));
        }
        return frames;
    }

    public static async Task<(SceneMap Map, TimeSpan Elapsed)> BuildMapAsync(
        string path,
        double duration,
        ThresholdRule? rule = null,
        CancellationToken ct = default)
    {
        var scan = await ScanAsync(path, ct: ct);
        if (!scan.Ok) throw new InvalidOperationException($"Sahne taramasi basarisiz: {scan.Error}");
        var map = SceneMap.BuildDerived(duration, scan.Candidates, scan.Frames, rule ?? ThresholdRule.Measured);
        return (map, scan.Elapsed);
    }

    public static async Task<(SceneMap Map, TimeSpan Elapsed)> BuildFixedMapAsync(
        string path,
        double duration,
        double threshold,
        CancellationToken ct = default)
    {
        var scan = await ScanAsync(path, ct: ct);
        if (!scan.Ok) throw new InvalidOperationException($"Sahne taramasi basarisiz: {scan.Error}");
        return (SceneMap.Build(duration, scan.Candidates, threshold, scan.Frames), scan.Elapsed);
    }

    private static bool TryField(string line, string key, out string value)
    {
        value = string.Empty;
        var at = line.IndexOf(key, StringComparison.Ordinal);
        if (at < 0) return false;
        value = FirstToken(line[(at + key.Length)..]);
        return value.Length > 0;
    }

    private static string FirstToken(string text)
    {
        var trimmed = text.TrimStart();
        var end = 0;
        while (end < trimmed.Length && !char.IsWhiteSpace(trimmed[end])) end++;
        return trimmed[..end];
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }
}
