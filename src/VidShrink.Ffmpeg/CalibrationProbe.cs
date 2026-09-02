using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public static class CalibrationProbe
{
    private const double CrfGap = 4.0;
    private const int SoftwareConcurrency = 4;
    private const int HardwareConcurrency = 2;
    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(90);

    public static async Task<ComplexityProfile> RunAsync(MediaInfo info, EncodePlan draft, ComplexityProfile profile, SpeedMode speed, CancellationToken ct = default, SceneMap? scenes = null)
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

            var windows = Windows(info, speed, scenes);
            var pending = new List<Task<Sample>>(windows.Count * 2);
            using var gate = new SemaphoreSlim(CodecModel.IsHardware(draft.Codec) ? HardwareConcurrency : SoftwareConcurrency);
            var batch = Stopwatch.StartNew();
            foreach (var window in windows)
            {
                pending.Add(GatedSampleAsync(gate, info, draft, window, lowCrf, speed, ct));
                pending.Add(GatedSampleAsync(gate, info, draft, window, highCrf, speed, ct));
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
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

    internal static IReadOnlyList<SampleWindow> Windows(MediaInfo info, SpeedMode speed, SceneMap? scenes = null)
    {
        var duration = info.DurationSeconds;
        var secondBits = SecondBits(scenes, duration);
        if (secondBits is null) return SignallessWindows(duration, speed);

        var count = ComplexityProbe.PlanWindowCount(duration, ComplexityProbe.Heterogeneity(secondBits));
        if (speed == SpeedMode.Fast) count = Math.Min(count, ComplexityProbe.MinWindows);

        var planned = ComplexityProbe.PlanWindows(
            SamplingPlan.Scene, duration, secondBits, CutTimes(scenes!, duration), count);
        return planned.Count == 0 ? SignallessWindows(duration, speed) : planned;
    }

    private static IReadOnlyList<SampleWindow> SignallessWindows(double duration, SpeedMode speed)
    {
        var plan = ComplexityProbe.PlanWindows(SamplingPlan.Fixed, duration);
        if (speed != SpeedMode.Fast || plan.Count <= ComplexityProbe.MinWindows) return plan;

        var length = plan[0].Length;
        var usable = Math.Max(0.0, duration - length);
        var count = ComplexityProbe.MinWindows;
        var capped = new List<SampleWindow>(count);
        for (var i = 0; i < count; i++)
            capped.Add(new SampleWindow(usable * (i + 0.5) / count, length, 1.0));
        return capped;
    }

    private static IReadOnlyList<double>? SecondBits(SceneMap? scenes, double duration)
    {
        if (scenes is null || !double.IsFinite(duration) || duration <= 0) return null;

        var seconds = (int)Math.Floor(duration);
        if (seconds <= 0) return null;

        var bits = new double[seconds];
        var filled = false;
        foreach (var scene in scenes.Scenes)
        {
            var rate = scene.BitsPerSecond;
            if (!double.IsFinite(rate) || rate <= 0) continue;

            var first = Math.Max(0, (int)Math.Floor(scene.Start));
            var last = Math.Min(seconds - 1, (int)Math.Ceiling(Math.Min(duration, scene.End)) - 1);
            for (var i = first; i <= last; i++)
            {
                bits[i] = rate;
                filled = true;
            }
        }

        return filled ? bits : null;
    }

    private static IReadOnlyList<double> CutTimes(SceneMap scenes, double duration)
        => scenes.Scenes
            .Skip(1)
            .Select(scene => scene.Start)
            .Where(cut => cut > 0 && cut < duration)
            .ToArray();

    private static async Task<Sample> GatedSampleAsync(SemaphoreSlim gate, MediaInfo info, EncodePlan draft, SampleWindow window, double crf, SpeedMode speed, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            return await SampleAsync(info, draft, window, crf, speed, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// The quality flag the encoder family actually understands, together with the rate control the
    /// real encode uses, so the probe measures the point the encode lands on. Verified against
    /// <c>ffmpeg -h encoder=...</c>: only the nvenc family carries <c>-cq</c>, qsv carries none of
    /// the private quality flags and takes the shared <c>-global_quality</c>, and amf carries
    /// <c>-qp_i/-qp_p/-qp_b</c> under <c>-rc cqp</c>.
    /// </summary>
    public static IReadOnlyList<string> QualityArgs(string codec, double quality)
    {
        var c = codec.ToLowerInvariant();
        var exact = quality.ToString("0.#", CultureInfo.InvariantCulture);
        var whole = Math.Round(quality).ToString("0", CultureInfo.InvariantCulture);

        if (c.Contains("nvenc")) return new[] { "-rc", "vbr", "-multipass", "fullres", "-cq", exact };
        if (c.Equals("h264_qsv")) return new[] { "-global_quality", whole, "-look_ahead", "1" };
        if (c.Contains("qsv")) return new[] { "-global_quality", whole };
        if (c.Contains("amf")) return new[] { "-rc", "cqp", "-qp_i", whole, "-qp_p", whole, "-qp_b", whole };
        return new[] { "-crf", exact };
    }

    internal static IReadOnlyList<string> TrimArgs(SampleWindow window) => new[]
    {
        "-ss", window.Start.ToString("0.###", CultureInfo.InvariantCulture),
        "-t", window.Length.ToString("0.###", CultureInfo.InvariantCulture)
    };

    private static async Task<Sample> SampleAsync(MediaInfo info, EncodePlan draft, SampleWindow window, double crf, SpeedMode speed, CancellationToken ct)
    {
        var args = new List<string> { "-hide_banner", "-nostdin" };
        if (speed == SpeedMode.Fast) args.AddRange(new[] { "-hwaccel", "auto" });
        args.AddRange(TrimArgs(window));
        args.AddRange(new[] { "-i", info.FilePath, "-an", "-sn", "-dn" });

        var filter = BuildFilter(info, draft);
        if (filter is not null)
        {
            args.Add("-vf");
            args.Add(filter);
        }

        args.Add("-c:v");
        args.Add(draft.Codec);
        args.AddRange(QualityArgs(draft.Codec, crf));
        args.Add("-preset");
        args.Add(draft.Preset);
        FfmpegArguments.WarmPsychovisual(draft.Codec, EncoderCapabilities.Instance);
        var psychovisual = FfmpegArguments.CachedPsychovisualArgs(draft.Codec, EncoderCapabilities.Instance);
        args.AddRange(FfmpegArguments.PsychovisualAndColorArgs(draft.Codec, psychovisual, draft.HdrColorArgs));
        args.Add("-pix_fmt");
        args.Add(draft.PixelFormat);
        args.AddRange(new[] { "-f", "null", "-" });

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(SampleTimeout);
        var token = deadline.Token;

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args.ToArray()) };
        process.Start();
        using var cancellationRegistration = token.Register(() => TryKill(process));

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(token);
            var stderrTask = process.StandardError.ReadToEndAsync(token);
            await Task.WhenAll(stdoutTask, stderrTask);
            var stderr = await stderrTask;
            await process.WaitForExitAsync(token);

            if (process.ExitCode != 0) return default;
            return new Sample(ParseVideoBytes(stderr), ParseFrames(stderr));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return default;
        }
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
