using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public static class CalibrationProbe
{
    private const double WindowSeconds = 2.0;
    private const int MaxWindows = 3;
    private const int MinWindows = 2;
    private const double CrfGap = 4.0;

    public static async Task<ComplexityProfile> RunAsync(MediaInfo info, EncodePlan draft, ComplexityProfile profile, CancellationToken ct = default, SpeedMode speed = SpeedMode.Quality)
    {
        try
        {
            if (draft.Width < 16 || draft.Height < 16 || draft.Fps <= 0) return profile.WithoutCalibration();

            var (min, max) = CodecModel.CrfRange(draft.Codec);
            var anchor = Math.Clamp(AnchorCrf(info, draft, profile), min, max);
            double lowCrf, highCrf;
            if (anchor + CrfGap <= max)
            {
                lowCrf = anchor;
                highCrf = anchor + CrfGap;
            }
            else
            {
                highCrf = anchor;
                lowCrf = Math.Max(min, anchor - CrfGap);
            }
            if (highCrf - lowCrf < 1.0) return profile.WithoutCalibration();

            long lowBytes = 0, highBytes = 0;
            long lowFrames = 0, highFrames = 0;

            var windows = Windows(info, speed).ToArray();
            var pending = new List<Task<Sample>>(windows.Length * 2);
            var batch = Stopwatch.StartNew();
            foreach (var start in windows)
            {
                pending.Add(SampleAsync(info, draft, start, lowCrf, speed, ct));
                pending.Add(SampleAsync(info, draft, start, highCrf, speed, ct));
            }

            var samples = await Task.WhenAll(pending);
            batch.Stop();

            var measured = MeasureSpeed(draft, samples, batch.Elapsed.TotalSeconds);

            for (var i = 0; i < samples.Length; i += 2)
            {
                var (lb, lf) = samples[i];
                var (hb, hf) = samples[i + 1];
                if (lf <= 0 || lb <= 0 || hf <= 0 || hb <= 0) continue;
                lowBytes += lb;
                lowFrames += lf;
                highBytes += hb;
                highFrames += hf;
            }

            if (lowFrames <= 0 || highFrames <= 0) return profile.WithoutCalibration().WithSpeed(measured);

            var pixels = (double)draft.Width * draft.Height;
            var lowBppf = lowBytes * 8.0 / (pixels * lowFrames);
            var highBppf = highBytes * 8.0 / (pixels * highFrames);

            var signature = new CalibrationSignature
            {
                Codec = draft.Codec,
                Width = draft.Width,
                Height = draft.Height,
                Fps = draft.Fps,
                Scale = info.Height <= 0 ? 1.0 : (double)draft.Height / info.Height
            };

            return profile.Calibrate(signature, lowCrf, lowBppf, highCrf, highBppf, info.Fps).WithSpeed(measured);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return profile.WithoutCalibration();
        }
    }

    private readonly record struct Sample(long Bytes, long Frames);

    private static EncodeSpeed? MeasureSpeed(EncodePlan draft, Sample[] samples, double batchSeconds)
    {
        long frames = 0;
        foreach (var sample in samples)
        {
            if (sample.Frames > 0) frames += sample.Frames;
        }

        if (frames <= 0 || batchSeconds <= 0) return null;

        return new EncodeSpeed
        {
            Codec = draft.Codec,
            Preset = draft.Preset,
            Width = draft.Width,
            Height = draft.Height,
            FramesPerSecond = frames / batchSeconds,
            Frames = frames,
            Seconds = batchSeconds
        };
    }

    private static double AnchorCrf(MediaInfo info, EncodePlan draft, ComplexityProfile profile)
    {
        if (draft.Crf is { } crf) return crf;

        if (draft.VideoBitrateK > 0)
        {
            var scale = info.Height <= 0 ? 1.0 : (double)draft.Height / info.Height;
            var bppf = draft.VideoBitrateK * 1000.0 / ((double)draft.Width * draft.Height * draft.Fps);
            var derived = profile.CrfForBppf(draft.Codec, bppf, scale, draft.Fps, info.Fps);
            if (double.IsFinite(derived)) return derived;
        }

        return CodecModel.ReferenceCrf(draft.Codec);
    }

    private static IEnumerable<double> Windows(MediaInfo info, SpeedMode speed)
    {
        var duration = info.DurationSeconds;
        if (duration <= WindowSeconds * 1.5)
        {
            yield return 0;
            yield break;
        }

        var usable = Math.Max(0.0, duration - WindowSeconds);
        var count = duration < WindowSeconds * 6 || speed == SpeedMode.Fast ? MinWindows : MaxWindows;
        for (var i = 0; i < count; i++)
            yield return usable * (i + 0.5) / count;
    }

    private static async Task<Sample> SampleAsync(MediaInfo info, EncodePlan draft, double start, double crf, SpeedMode speed, CancellationToken ct)
    {
        var args = new List<string> { "-hide_banner", "-nostdin" };
        if (speed == SpeedMode.Fast) args.AddRange(new[] { "-hwaccel", "auto" });
        args.AddRange(new[]
        {
            "-ss", start.ToString("0.###", CultureInfo.InvariantCulture),
            "-t", WindowSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", info.FilePath,
            "-an", "-sn", "-dn"
        });

        var filter = BuildFilter(info, draft);
        if (filter is not null)
        {
            args.Add("-vf");
            args.Add(filter);
        }

        args.Add("-c:v");
        args.Add(draft.Codec);
        args.Add(CodecModel.UsesCq(draft.Codec) ? "-cq" : "-crf");
        args.Add(crf.ToString("0.#", CultureInfo.InvariantCulture));
        args.Add("-preset");
        args.Add(draft.Preset);
        args.Add("-pix_fmt");
        args.Add(draft.PixelFormat);
        args.AddRange(new[] { "-f", "null", "-" });

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args.ToArray()) };
        process.Start();
        using var cancellationRegistration = ct.Register(() => TryKill(process));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        var stderr = await stderrTask;
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0) return default;
        return new Sample(ParseVideoBytes(stderr), ParseFrames(stderr));
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    private static string? BuildFilter(MediaInfo info, EncodePlan draft)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(draft.HdrVideoFilter)) parts.Add(draft.HdrVideoFilter!);
        if (draft.Width != info.Width || draft.Height != info.Height) parts.Add($"scale={draft.Width}:{draft.Height}");
        if (draft.Fps > 0 && draft.Fps < info.Fps - 0.01) parts.Add($"fps={draft.Fps.ToString("0.###", CultureInfo.InvariantCulture)}");
        return parts.Count == 0 ? null : string.Join(",", parts);
    }

    private static long ParseVideoBytes(string stderr)
    {
        var match = Regex.Match(stderr, @"video:\s*([0-9.]+)\s*([kKmMgG])?i?B", RegexOptions.RightToLeft);
        if (!match.Success) return 0;
        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return 0;

        var multiplier = match.Groups[2].Value.ToLowerInvariant() switch
        {
            "k" => 1024.0,
            "m" => 1024.0 * 1024.0,
            "g" => 1024.0 * 1024.0 * 1024.0,
            _ => 1.0
        };
        return (long)(value * multiplier);
    }

    private static long ParseFrames(string stderr)
    {
        var matches = Regex.Matches(stderr, @"frame=\s*(\d+)");
        if (matches.Count == 0) return 0;
        return long.TryParse(matches[^1].Groups[1].Value, out var frames) ? frames : 0;
    }

}
