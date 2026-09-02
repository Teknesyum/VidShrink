using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public readonly record struct PacketSample(double PtsSeconds, long Size);

public readonly record struct SampleWindow(double Start, double Length, double Weight);

public enum SamplingPlan
{
    Fixed,
    Profile,
    Scene
}

public static class ComplexityProbe
{
    internal const string SampleFormat = "matroska";
    internal const string SampleExtension = ".mkv";
    private const double WindowSeconds = 2.0;
    private const double MotionProbeMinSourceFps = 10.0;
    public const double MotionProbeFpsRatio = 0.5;
    private const int MaxWindows = 3;
    internal const int MinWindows = 2;
    internal const int MaxPlannedWindows = 12;
    internal const double MaxSampledShare = 0.06;
    internal const double MaxSampledCeilingShare = 0.25;
    internal const double MinSampledSeconds = 6.0;
    internal const double MaxSampledSeconds = 24.0;
    internal const double SamplingTargetError = 0.05;
    internal const double SamplingErrorScale = 1.0;
    internal const double SamplingErrorDecay = 1.0;
    internal const SamplingPlan ProductionPlan = SamplingPlan.Profile;
    private const int MinProfileSeconds = 4;

    private const double ScanPointSeconds = 1.0;
    private const double ScanWarmupSeconds = 0.75;
    private const int ScanPointCount = 40;
    private const int ScanPointsPerWindow = 4;
    private const int ScanConcurrency = 8;
    private const int ScanWidth = 480;
    private const int ScanHeight = 270;
    private const string ScanPreset = "ultrafast";
    private const string FastProbePreset = "veryfast";

    private const double PacketFullReadSeconds = 180.0;
    private const int PacketIntervalCount = 40;
    private const int PacketIntervalSeconds = 2;
    private const int PacketWindowIntervalSeconds = 3;

    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan PacketReadTimeout = TimeSpan.FromSeconds(120);

    public static Task<ComplexityProfile> RunAsync(MediaInfo info, SpeedMode speed, CancellationToken ct = default)
        => RunAsync(info, speed, null, ct);

    internal static async Task<ComplexityProfile> RunAsync(MediaInfo info, SpeedMode speed, IQualityMeasurement? qualityMeasurement, CancellationToken ct)
        => (await RunDetailedAsync(info, speed, measureQuality: false, qualityMeasurement, ct)).Profile;

    public static async Task<ProbeResult> RunDetailedAsync(
        MediaInfo info, SpeedMode speed, bool measureQuality = false,
        IQualityMeasurement? qualityMeasurement = null, CancellationToken ct = default)
    {
        try
        {
            long fullBytes = 0, halfBytes = 0;
            long fullFrames = 0, halfFrames = 0;
            var sampled = 0.0;
            var qualities = new List<WindowQualityMeasurement>();
            var meter = measureQuality ? qualityMeasurement ?? QualityMeasurement.Instance : null;
            if (meter is { IsAvailable: false }) meter = null;

            var halfWidth = EvenDown((int)Math.Round(info.Width * ComplexityProfile.ProbeScale));
            var halfHeight = EvenDown((int)Math.Round(info.Height * ComplexityProfile.ProbeScale));
            var canProbeHalf = halfWidth >= 64 && halfHeight >= 64;
            var preset = speed == SpeedMode.Fast ? FastProbePreset : ComplexityProfile.ProbePreset;

            var secondBits = await SecondProfileAsync(info, ct);
            var plan = PlanWindows(ProductionPlan, info.DurationSeconds, secondBits.Count >= MinProfileSeconds ? secondBits : null);
            var windows = plan.Select(w => w.Start).ToArray();
            var motionIndex = windows.Length / 2;
            var motionTask = MotionSampleAsync(info, windows, motionIndex, preset, speed, ct);

            var pending = windows
                .Select(start => SampleWindowAsync(info.FilePath, start, canProbeHalf ? (halfWidth, halfHeight) : null, preset, speed, meter, ct))
                .ToArray();
            var windowSamples = await Task.WhenAll(pending);
            var motion = await motionTask;

            var fullPixels = (double)info.Width * info.Height;
            var halfPixels = (double)halfWidth * halfHeight;
            double fullWeighted = 0, fullWeight = 0, halfWeighted = 0, halfWeight = 0;

            for (var i = 0; i < windowSamples.Length; i++)
            {
                var sample = windowSamples[i];
                var weight = i < plan.Count && plan[i].Weight > 0 ? plan[i].Weight : 1.0;
                if (sample.Quality is { Comparable: true, VmafNegMean: not null } quality) qualities.Add(quality);
                if (sample.FullFrames <= 0) continue;
                fullBytes += sample.FullBytes;
                fullFrames += sample.FullFrames;
                sampled += i < plan.Count ? plan[i].Length : WindowSeconds;
                fullWeighted += weight * (sample.FullBytes * 8.0 / (fullPixels * sample.FullFrames));
                fullWeight += weight;

                if (sample.HalfFrames <= 0 || halfPixels <= 0) continue;
                halfBytes += sample.HalfBytes;
                halfFrames += sample.HalfFrames;
                halfWeighted += weight * (sample.HalfBytes * 8.0 / (halfPixels * sample.HalfFrames));
                halfWeight += weight;
            }

            if (fullFrames <= 0 || fullBytes <= 0 || fullWeight <= 0)
                return new ProbeResult(ComplexityProfile.FromSourceBitrate(info), qualities);

            var fullBppf = fullWeighted / fullWeight;
            var halfBppf = halfWeight > 0 && halfBytes > 0 ? halfWeighted / halfWeight : 0.0;

            var halfFpsBppf = MotionBppf(fullBppf, motion, windowSamples, motionIndex);

            var (bias, source) = await MeasureWindowBiasAsync(info, speed, ct);

            return new ProbeResult(
                ComplexityProfile.FromProbe(fullBppf, halfBppf, sampled, fullFrames, bias, source, halfFpsBppf),
                qualities);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new ProbeResult(ComplexityProfile.FromSourceBitrate(info), Array.Empty<WindowQualityMeasurement>());
        }
    }

    private static Task<(long Bytes, long Frames)> MotionSampleAsync(MediaInfo info, IReadOnlyList<double> windows, int index, string preset, SpeedMode speed, CancellationToken ct)
    {
        if (windows.Count == 0 || info.Fps < MotionProbeMinSourceFps) return Task.FromResult((0L, 0L));

        var fps = info.Fps * MotionProbeFpsRatio;
        var filter = "fps=" + fps.ToString("0.###", CultureInfo.InvariantCulture);
        return SampleAsync(info.FilePath, windows[index], WindowSeconds, filter, preset, speed, ct);
    }

    private static double MotionBppf(double fullBppf, (long Bytes, long Frames) motion, IReadOnlyList<WindowSample> windowSamples, int index)
    {
        if (motion.Frames <= 0 || motion.Bytes <= 0 || index >= windowSamples.Count) return 0.0;

        var reference = windowSamples[index];
        if (reference.FullFrames <= 0 || reference.FullBytes <= 0) return 0.0;

        var ratio = motion.Bytes / (double)motion.Frames / (reference.FullBytes / (double)reference.FullFrames);
        return double.IsFinite(ratio) && ratio > 0 ? fullBppf * ratio : 0.0;
    }

    public static IEnumerable<double> Windows(double duration)
    {
        if (duration <= WindowSeconds * 1.5)
        {
            yield return 0;
            yield break;
        }

        var usable = Math.Max(0.0, duration - WindowSeconds);
        var count = duration < WindowSeconds * 6 ? 2 : MaxWindows;
        for (var i = 0; i < count; i++)
            yield return usable * (i + 0.5) / count;
    }


    public static double Heterogeneity(IReadOnlyList<double> secondBits)
    {
        if (secondBits is null) return 0.0;
        var used = new List<double>(secondBits.Count);
        foreach (var bits in secondBits)
            if (double.IsFinite(bits) && bits > 0) used.Add(bits);
        if (used.Count < MinProfileSeconds) return 0.0;

        var mean = used.Average();
        if (mean <= 0) return 0.0;

        var variance = used.Sum(v => (v - mean) * (v - mean)) / used.Count;
        var cv = Math.Sqrt(variance) / mean;
        return double.IsFinite(cv) ? cv : 0.0;
    }

    public static int PlanWindowCount(double duration, double heterogeneity, double windowSeconds = WindowSeconds)
    {
        if (!double.IsFinite(duration) || duration <= windowSeconds * 1.5) return 1;
        if (!double.IsFinite(heterogeneity) || heterogeneity <= 0) return MinWindows;

        var needed = Math.Pow(Math.Max(heterogeneity, 0.0) * SamplingErrorScale / SamplingTargetError, 1.0 / SamplingErrorDecay);
        var budget = Math.Min(
            Math.Clamp(duration * MaxSampledShare, MinSampledSeconds, MaxSampledSeconds),
            duration * MaxSampledCeilingShare);
        var affordable = Math.Floor(budget / windowSeconds);
        var ceiling = Math.Max(MinWindows, Math.Min(MaxPlannedWindows, affordable));
        return (int)Math.Clamp(Math.Ceiling(needed), MinWindows, ceiling);
    }

    public static IReadOnlyList<SampleWindow> PlanWindows(
        SamplingPlan plan,
        double duration,
        IReadOnlyList<double>? secondBits = null,
        IReadOnlyList<double>? sceneCuts = null,
        int windowCount = 0,
        double windowSeconds = WindowSeconds)
    {
        if (!double.IsFinite(duration) || duration <= 0) return Array.Empty<SampleWindow>();
        if (plan == SamplingPlan.Fixed || secondBits is null || secondBits.Count < MinProfileSeconds)
            return FixedWindows(duration, windowSeconds);

        var count = windowCount > 0
            ? windowCount
            : PlanWindowCount(duration, Heterogeneity(secondBits), windowSeconds);
        if (count <= 0) return FixedWindows(duration, windowSeconds);

        var placed = plan == SamplingPlan.Scene
            ? SceneWindows(duration, secondBits, sceneCuts, count, windowSeconds)
            : ProfileWindows(duration, secondBits, count, windowSeconds);

        return placed.Count == 0 ? FixedWindows(duration, windowSeconds) : placed;
    }

    private static IReadOnlyList<SampleWindow> FixedWindows(double duration, double windowSeconds)
        => Windows(duration).Select(start => new SampleWindow(start, windowSeconds, 1.0)).ToList();

    private static IReadOnlyList<SampleWindow> ProfileWindows(
        double duration, IReadOnlyList<double> secondBits, int count, double windowSeconds)
    {
        var usable = new List<int>(secondBits.Count);
        for (var i = 0; i < secondBits.Count; i++)
            if (double.IsFinite(secondBits[i]) && secondBits[i] > 0) usable.Add(i);
        if (usable.Count < MinProfileSeconds) return Array.Empty<SampleWindow>();

        usable.Sort((a, b) =>
        {
            var byBits = secondBits[a].CompareTo(secondBits[b]);
            return byBits != 0 ? byBits : a.CompareTo(b);
        });
        var strata = Math.Min(count, usable.Count);
        var taken = new HashSet<int>();
        var windows = new List<SampleWindow>(strata);

        for (var s = 0; s < strata; s++)
        {
            var from = (int)((long)usable.Count * s / strata);
            var to = (int)((long)usable.Count * (s + 1) / strata);
            if (to <= from) continue;

            var target = 0.0;
            for (var i = from; i < to; i++) target += secondBits[usable[i]];
            target /= to - from;

            var pick = NearestFreeSecond(usable, from, to, taken, secondBits, target);
            if (pick < 0) continue;
            taken.Add(pick);
            windows.Add(new SampleWindow(ClampStart(pick, duration, windowSeconds), windowSeconds, to - from));
        }

        return windows.OrderBy(w => w.Start).ToList();
    }

    private static int NearestFreeSecond(List<int> sorted, int from, int to, HashSet<int> taken, IReadOnlyList<double> secondBits, double target)
    {
        var best = -1;
        var bestDistance = double.MaxValue;
        for (var i = from; i < to; i++)
        {
            var second = sorted[i];
            if (taken.Contains(second)) continue;
            var distance = Math.Abs(secondBits[second] - target);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = second;
        }
        return best;
    }

    private static IReadOnlyList<SampleWindow> SceneWindows(
        double duration, IReadOnlyList<double> secondBits, IReadOnlyList<double>? sceneCuts, int count, double windowSeconds)
    {
        var bounds = new List<double> { 0.0 };
        if (sceneCuts is not null)
            foreach (var cut in sceneCuts.Where(c => c > 0 && c < duration).OrderBy(c => c))
                if (cut - bounds[^1] >= windowSeconds) bounds.Add(cut);
        bounds.Add(duration);
        if (bounds.Count < 3) return ProfileWindows(duration, secondBits, count, windowSeconds);

        var scenes = new List<(double Start, double Length, double Rate)>(bounds.Count - 1);
        for (var i = 0; i + 1 < bounds.Count; i++)
        {
            var start = bounds[i];
            var length = bounds[i + 1] - start;
            if (length < windowSeconds) continue;
            var bits = SegmentBits(secondBits, start, bounds[i + 1]);
            if (bits <= 0) continue;
            scenes.Add((start, length, bits / length));
        }
        if (scenes.Count == 0) return ProfileWindows(duration, secondBits, count, windowSeconds);

        scenes.Sort((a, b) =>
        {
            var byRate = a.Rate.CompareTo(b.Rate);
            return byRate != 0 ? byRate : a.Start.CompareTo(b.Start);
        });
        var total = scenes.Sum(s => s.Length);
        var strata = Math.Min(count, scenes.Count);
        var windows = new List<SampleWindow>(strata);
        var index = 0;
        var carried = 0.0;

        for (var s = 0; s < strata; s++)
        {
            var edge = total * (s + 1) / strata;
            var first = index;
            var weight = 0.0;
            while (index < scenes.Count && (carried < edge - 1e-9 || index == first))
            {
                carried += scenes[index].Length;
                weight += scenes[index].Length;
                index++;
            }
            if (index <= first) continue;

            var target = 0.0;
            for (var i = first; i < index; i++) target += scenes[i].Rate * scenes[i].Length;
            target /= weight;

            var best = first;
            var bestDistance = double.MaxValue;
            for (var i = first; i < index; i++)
            {
                var distance = Math.Abs(scenes[i].Rate - target);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            var centre = scenes[best].Start + (scenes[best].Length - windowSeconds) / 2.0;
            windows.Add(new SampleWindow(ClampStart(centre, duration, windowSeconds), windowSeconds, weight));
        }

        return windows.Count == 0
            ? ProfileWindows(duration, secondBits, count, windowSeconds)
            : windows.OrderBy(w => w.Start).ToList();
    }

    private static double SegmentBits(IReadOnlyList<double> secondBits, double start, double end)
    {
        var first = (int)Math.Floor(start);
        var last = (int)Math.Ceiling(end) - 1;
        var sum = 0.0;
        for (var i = first; i <= last; i++)
            if (i >= 0 && i < secondBits.Count && double.IsFinite(secondBits[i]) && secondBits[i] > 0) sum += secondBits[i];
        return sum;
    }

    private static double ClampStart(double start, double duration, double windowSeconds)
        => Math.Round(Math.Clamp(start, 0.0, Math.Max(0.0, duration - windowSeconds)), 3, MidpointRounding.AwayFromZero);

    public static double WeightedBppf(IReadOnlyList<SampleWindow> windows, IReadOnlyList<(long Bytes, long Frames)> samples, int width, int height)
    {
        if (windows is null || samples is null || windows.Count != samples.Count) return 0.0;
        var pixels = (double)width * height;
        if (pixels <= 0) return 0.0;

        double weighted = 0, weight = 0;
        for (var i = 0; i < windows.Count; i++)
        {
            var (bytes, frames) = samples[i];
            if (bytes <= 0 || frames <= 0) continue;
            var w = windows[i].Weight > 0 ? windows[i].Weight : 1.0;
            weighted += w * (bytes * 8.0 / (pixels * frames));
            weight += w;
        }

        if (weight <= 0) return 0.0;
        var bppf = weighted / weight;
        return double.IsFinite(bppf) ? bppf : 0.0;
    }

    public static double PlanBias(IReadOnlyList<SampleWindow> windows, IReadOnlyList<double> secondBits)
    {
        if (windows is null || windows.Count == 0 || secondBits is null || secondBits.Count < MinProfileSeconds) return 0.0;

        double sampled = 0, weight = 0;
        foreach (var window in windows)
        {
            var seconds = SegmentSeconds(secondBits, window.Start, window.Start + window.Length);
            if (seconds.Count == 0) continue;
            var w = window.Weight > 0 ? window.Weight : 1.0;
            sampled += w * seconds.Average();
            weight += w;
        }
        if (weight <= 0) return 0.0;

        var pool = secondBits.Where(b => double.IsFinite(b) && b > 0).ToArray();
        if (pool.Length < MinProfileSeconds) return 0.0;

        var fileMean = pool.Average();
        var windowMean = sampled / weight;
        if (fileMean <= 0 || windowMean <= 0) return 0.0;

        var bias = windowMean / fileMean;
        return double.IsFinite(bias) ? bias : 0.0;
    }

    private static List<double> SegmentSeconds(IReadOnlyList<double> secondBits, double start, double end)
    {
        var values = new List<double>();
        var first = (int)Math.Floor(start);
        var last = (int)Math.Ceiling(end) - 1;
        for (var i = first; i <= last; i++)
            if (i >= 0 && i < secondBits.Count && double.IsFinite(secondBits[i]) && secondBits[i] > 0) values.Add(secondBits[i]);
        return values;
    }

    public static IReadOnlyList<double> WindowScanPoints(double duration)
    {
        var points = new SortedSet<double>();
        var span = WindowSeconds - ScanPointSeconds;
        foreach (var start in Windows(duration))
            for (var i = 0; i < ScanPointsPerWindow; i++)
                points.Add(Round(start + span * i / (ScanPointsPerWindow - 1)));
        return points.ToList();
    }

    public static IReadOnlyList<double> ScanPoints(double duration)
    {
        var points = new SortedSet<double>(WindowScanPoints(duration));
        var usable = Math.Max(0.0, duration - ScanPointSeconds);
        var extra = Math.Max(1, ScanPointCount - points.Count);
        for (var i = 0; i < extra; i++)
            points.Add(Round(usable * (i + 0.5) / extra));
        return points.ToList();
    }

    public static double ComputeScanBias(IReadOnlyList<double> points, IReadOnlyList<double> windowPoints, IReadOnlyList<(long Bytes, long Frames)> samples, double duration)
    {
        if (points.Count != samples.Count || windowPoints.Count == 0) return 0.0;
        if (points.Count <= windowPoints.Count || duration <= 0) return 0.0;

        var selected = new HashSet<double>(windowPoints);
        long spreadBytes = 0, spreadFrames = 0, windowBytes = 0, windowFrames = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var (bytes, frames) = samples[i];
            if (frames <= 0 || bytes <= 0) continue;
            if (selected.Contains(points[i]))
            {
                windowBytes += bytes;
                windowFrames += frames;
            }
            else
            {
                spreadBytes += bytes;
                spreadFrames += frames;
            }
        }

        if (windowFrames <= 0 || spreadFrames <= 0) return 0.0;

        var windowMean = windowBytes / (double)windowFrames;
        var spreadMean = spreadBytes / (double)spreadFrames;
        if (windowMean <= 0 || spreadMean <= 0) return 0.0;

        var share = Math.Clamp(Windows(duration).Count() * WindowSeconds / duration, 0.0, 0.5);
        var fileMean = share * windowMean + (1.0 - share) * spreadMean;
        if (fileMean <= 0) return 0.0;

        var bias = windowMean / fileMean;
        return double.IsFinite(bias) ? bias : 0.0;
    }

    public static IReadOnlyList<(double Start, double Length)> PacketIntervals(double duration)
    {
        if (!double.IsFinite(duration) || duration <= PacketFullReadSeconds) return Array.Empty<(double, double)>();

        var intervals = new SortedDictionary<double, double>();
        foreach (var start in Windows(duration))
            intervals[Math.Floor(start)] = PacketWindowIntervalSeconds;

        var usable = Math.Max(0.0, duration - PacketIntervalSeconds);
        var extra = Math.Max(1, PacketIntervalCount - intervals.Count);
        for (var i = 0; i < extra; i++)
        {
            var start = Math.Floor(usable * (i + 0.5) / extra);
            if (!intervals.ContainsKey(start)) intervals[start] = PacketIntervalSeconds;
        }

        return intervals.Select(pair => (pair.Key, pair.Value)).ToList();
    }

    public static double ComputeWindowBias(IReadOnlyList<PacketSample> packets, double duration)
        => ComputeWindowBias(packets, duration, Array.Empty<(double, double)>());

    public static double ComputeWindowBias(IReadOnlyList<PacketSample> packets, double duration, IReadOnlyList<(double Start, double Length)> intervals)
    {
        var profile = SecondProfile(packets, duration);
        if (profile.Length < MinProfileSeconds) return 0.0;

        var covered = CoveredSeconds(profile.Length, intervals);
        var selected = new HashSet<int>();
        foreach (var start in Windows(duration))
        {
            var first = (int)Math.Floor(start);
            var last = (int)Math.Floor(start + WindowSeconds) - 1;
            for (var i = first; i <= last; i++)
                if (i >= 0 && i < profile.Length && covered[i] && profile[i] > 0) selected.Add(i);
        }

        var pool = new List<int>();
        for (var i = 0; i < profile.Length; i++)
            if (covered[i] && profile[i] > 0) pool.Add(i);

        if (selected.Count == 0 || pool.Count < MinProfileSeconds || selected.Count >= pool.Count) return 0.0;

        var windowMean = selected.Sum(i => profile[i]) / selected.Count;
        var fileMean = pool.Sum(i => profile[i]) / pool.Count;
        if (windowMean <= 0 || fileMean <= 0) return 0.0;

        var bias = windowMean / fileMean;
        return double.IsFinite(bias) ? bias : 0.0;
    }

    private static bool[] CoveredSeconds(int seconds, IReadOnlyList<(double Start, double Length)> intervals)
    {
        var covered = new bool[seconds];
        if (intervals.Count == 0)
        {
            Array.Fill(covered, true);
            return covered;
        }

        foreach (var (start, length) in intervals)
        {
            var first = (int)Math.Ceiling(start);
            var last = (int)Math.Floor(start + length) - 1;
            for (var i = first; i <= last; i++)
                if (i >= 0 && i < seconds) covered[i] = true;
        }
        return covered;
    }

    private static double Round(double value) => Math.Round(Math.Max(0.0, value), 3, MidpointRounding.AwayFromZero);

    public static IReadOnlyList<double> SecondBitProfile(IReadOnlyList<PacketSample> packets, double duration)
        => SecondProfile(packets, duration);

    private static double[] SecondProfile(IReadOnlyList<PacketSample> packets, double duration)
    {
        var seconds = (int)Math.Floor(duration);
        if (seconds < MinProfileSeconds || packets.Count == 0) return Array.Empty<double>();

        var buckets = new double[seconds];
        foreach (var packet in packets)
        {
            if (!double.IsFinite(packet.PtsSeconds) || packet.PtsSeconds < 0 || packet.Size <= 0) continue;
            var index = (int)packet.PtsSeconds;
            if (index < buckets.Length) buckets[index] += packet.Size * 8.0;
        }

        var end = buckets.Length;
        while (end > 0 && buckets[end - 1] <= 0) end--;
        if (end < MinProfileSeconds) return Array.Empty<double>();
        return end == buckets.Length ? buckets : buckets[..end];
    }

    private static async Task<(double Bias, WindowBiasSource Source)> MeasureWindowBiasAsync(MediaInfo info, SpeedMode speed, CancellationToken ct)
    {
        var scan = await ScanBiasAsync(info, speed, ct);
        if (ComplexityProfile.IsTrustedBias(scan)) return (scan, WindowBiasSource.Scan);

        var packet = await PacketBiasAsync(info, ct);
        if (ComplexityProfile.IsTrustedBias(packet)) return (packet, WindowBiasSource.Packets);

        return (0.0, WindowBiasSource.None);
    }

    internal static async Task<IReadOnlyList<double>> SecondProfileAsync(MediaInfo info, CancellationToken ct)
    {
        try
        {
            var packets = await ReadPacketsAsync(info.FilePath, Array.Empty<(double Start, double Length)>(), ct);
            var profile = SecondProfile(packets, info.DurationSeconds);
            return profile.Length >= MinProfileSeconds ? profile : Array.Empty<double>();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Array.Empty<double>();
        }
    }

    public static async Task<double> ScanBiasAsync(MediaInfo info, SpeedMode speed, CancellationToken ct)
    {
        try
        {
            var points = ScanPoints(info.DurationSeconds);
            var windowPoints = WindowScanPoints(info.DurationSeconds);
            if (points.Count <= windowPoints.Count) return 0.0;

            var filter = $"scale={ScanWidth}:{ScanHeight}";
            using var gate = new SemaphoreSlim(ScanConcurrency);
            var pending = points.Select(async point =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    return await ScanSampleAsync(info.FilePath, point, filter, speed, ct);
                }
                finally
                {
                    gate.Release();
                }
            }).ToArray();

            var samples = await Task.WhenAll(pending);
            return ComputeScanBias(points, windowPoints, samples, info.DurationSeconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return 0.0;
        }
    }

    public static async Task<double> PacketBiasAsync(MediaInfo info, CancellationToken ct)
    {
        try
        {
            var intervals = PacketIntervals(info.DurationSeconds);
            var packets = await ReadPacketsAsync(info.FilePath, intervals, ct);
            return ComputeWindowBias(packets, info.DurationSeconds, intervals);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return 0.0;
        }
    }

    private static async Task<IReadOnlyList<PacketSample>> ReadPacketsAsync(string path, IReadOnlyList<(double Start, double Length)> intervals, CancellationToken ct)
    {
        var args = new List<string>
        {
            "-v", "error", "-select_streams", "v:0",
            "-show_entries", "packet=pts_time,size",
            "-of", "csv=p=0"
        };

        if (intervals.Count > 0)
        {
            args.Add("-read_intervals");
            args.Add(string.Join(",", intervals.Select(i =>
                $"{i.Start.ToString("0.###", CultureInfo.InvariantCulture)}%+{i.Length.ToString("0.###", CultureInfo.InvariantCulture)}")));
        }

        args.Add(path);

        using var deadline = Deadline(ct, PacketReadTimeout);
        var token = deadline.Token;

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, args.ToArray()) };
        process.Start();
        using var cancellationRegistration = token.Register(() => TryKill(process));

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(token);
            var stderrTask = process.StandardError.ReadToEndAsync(token);
            await Task.WhenAll(stdoutTask, stderrTask);
            var stdout = await stdoutTask;
            await process.WaitForExitAsync(token);

            if (process.ExitCode != 0) return Array.Empty<PacketSample>();
            return ParsePackets(stdout);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Array.Empty<PacketSample>();
        }
    }

    public static IReadOnlyList<PacketSample> ParsePackets(string csv)
    {
        var samples = new List<PacketSample>();
        foreach (var line in csv.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            var comma = trimmed.IndexOf(',');
            if (comma <= 0) continue;

            if (!double.TryParse(trimmed[..comma], NumberStyles.Float, CultureInfo.InvariantCulture, out var pts)) continue;
            if (!long.TryParse(trimmed[(comma + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)) continue;

            samples.Add(new PacketSample(pts, size));
        }
        return samples;
    }

    private static int EvenDown(int value) => value % 2 == 0 ? value : value - 1;

    internal readonly record struct WindowSample(long FullBytes, long FullFrames, long HalfBytes, long HalfFrames, WindowQualityMeasurement? Quality = null);

    internal static async Task<WindowSample> SampleWindowAsync(string path, double start, (int Width, int Height)? half, string preset, SpeedMode speed, IQualityMeasurement? qualityMeasurement, CancellationToken ct)
    {
        if (half is null)
        {
            var (bytes, frames) = await SampleAsync(path, start, WindowSeconds, null, preset, speed, ct);
            return new WindowSample(bytes, frames, 0, 0);
        }

        var split = await SplitSampleAsync(path, start, half.Value, preset, speed, qualityMeasurement, ct);
        if (split is { } measured) return measured;

        var (fullBytes, fullFrames) = await SampleAsync(path, start, WindowSeconds, null, preset, speed, ct);
        var (halfBytes, halfFrames) = await SampleAsync(path, start, WindowSeconds, $"scale={half.Value.Width}:{half.Value.Height}", preset, speed, ct);
        return new WindowSample(fullBytes, fullFrames, halfBytes, halfFrames);
    }

    private static async Task<WindowSample?> SplitSampleAsync(string path, double start, (int Width, int Height) half, string preset, SpeedMode speed, IQualityMeasurement? qualityMeasurement, CancellationToken ct)
    {
        var stem = Path.Combine(Path.GetTempPath(), "vidshrink_probe_" + Guid.NewGuid().ToString("N"));
        var fullPath = stem + "_full" + SampleExtension;
        var halfPath = stem + "_half" + SampleExtension;
        try
        {
            var args = SplitArgs(path, start, half, preset, speed, fullPath, halfPath);

            using var deadline = Deadline(ct, SampleTimeout);
            var token = deadline.Token;

            using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
            process.Start();
            using var cancellationRegistration = token.Register(() => TryKill(process));

            var stdoutTask = process.StandardOutput.ReadToEndAsync(token);
            var stderrTask = process.StandardError.ReadToEndAsync(token);
            await Task.WhenAll(stdoutTask, stderrTask);
            var stderr = await stderrTask;
            await process.WaitForExitAsync(token);

            if (process.ExitCode != 0) return null;

            var frames = ParseFrames(stderr);
            if (frames <= 0) return null;

            var fullBytes = MeasureSampleBytes(fullPath);
            var halfBytes = MeasureSampleBytes(halfPath);
            if (fullBytes <= 0 || halfBytes <= 0) return null;

            WindowQualityMeasurement? quality = null;
            if (qualityMeasurement is not null)
            {
                try
                {
                    quality = await qualityMeasurement.MeasureWindowAsync(path, fullPath, start, WindowSeconds, token);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch { quality = null; }
            }

            return new WindowSample(fullBytes, frames, halfBytes, frames, quality);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
        finally
        {
            try { File.Delete(fullPath); } catch { }
            try { File.Delete(halfPath); } catch { }
        }
    }

    public static async Task<(long Bytes, long Frames)> SampleAsync(string path, double start, double length, string? filter, string? preset, SpeedMode speed, CancellationToken ct)
    {
        var target = Path.Combine(Path.GetTempPath(), "vidshrink_probe_" + Guid.NewGuid().ToString("N") + "_one" + SampleExtension);
        var args = SampleArgs(path, start, length, filter, preset, speed, target);

        using var deadline = Deadline(ct, SampleTimeout);
        var token = deadline.Token;

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        process.Start();
        using var cancellationRegistration = token.Register(() => TryKill(process));

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(token);
            var stderrTask = process.StandardError.ReadToEndAsync(token);
            await Task.WhenAll(stdoutTask, stderrTask);
            var stderr = await stderrTask;
            await process.WaitForExitAsync(token);

            if (process.ExitCode != 0) return (0, 0);

            var bytes = MeasureSampleBytes(target);
            var frames = ParseFrames(stderr);
            return bytes > 0 ? (bytes, frames) : (0, 0);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (0, 0);
        }
        finally
        {
            try { File.Delete(target); } catch { }
        }
    }

    internal static string[] SampleArgs(string path, double start, double length, string? filter, string? preset, SpeedMode speed, string target)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        if (speed == SpeedMode.Fast) args.AddRange(new[] { "-hwaccel", "auto" });
        args.AddRange(new[]
        {
            "-ss", start.ToString("0.###", CultureInfo.InvariantCulture),
            "-t", length.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", path,
            "-an", "-sn", "-dn"
        });

        if (filter is not null)
        {
            args.Add("-vf");
            args.Add(filter);
        }

        args.AddRange(EncodeTo(preset ?? ComplexityProfile.ProbePreset, target));
        return args.ToArray();
    }

    internal static string[] SplitArgs(string path, double start, (int Width, int Height) half, string preset, SpeedMode speed, string fullTarget, string halfTarget)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        if (speed == SpeedMode.Fast) args.AddRange(new[] { "-hwaccel", "auto" });
        args.AddRange(new[]
        {
            "-ss", start.ToString("0.###", CultureInfo.InvariantCulture),
            "-t", WindowSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", path,
            "-an", "-sn", "-dn",
            "-filter_complex", $"[0:v]split=2[full][raw];[raw]scale={half.Width}:{half.Height}[small]"
        });

        foreach (var (label, target) in new[] { ("[full]", fullTarget), ("[small]", halfTarget) })
        {
            args.Add("-map");
            args.Add(label);
            args.AddRange(EncodeTo(preset, target));
        }

        return args.ToArray();
    }

    private static string[] EncodeTo(string preset, string target) => new[]
    {
        "-c:v", "libx264",
        "-crf", ComplexityProfile.ProbeCrf.ToString("0", CultureInfo.InvariantCulture),
        "-preset", preset,
        "-f", SampleFormat, target
    };

    internal static long MeasureSampleBytes(string target)
    {
        try
        {
            var file = new FileInfo(target);
            return file.Exists ? file.Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static CancellationTokenSource Deadline(CancellationToken ct, TimeSpan timeout)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        return deadline;
    }

    private static async Task<(long Bytes, long Frames)> ScanSampleAsync(string path, double start, string filter, SpeedMode speed, CancellationToken ct)
    {
        var statsPath = Path.Combine(Path.GetTempPath(), "vidshrink_scan_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var args = new List<string> { "-hide_banner", "-nostdin", "-v", "error", "-y", "-vstats_file", statsPath };
            if (speed == SpeedMode.Fast) args.AddRange(new[] { "-hwaccel", "auto" });
            args.AddRange(new[]
            {
                "-ss", start.ToString("0.###", CultureInfo.InvariantCulture),
                "-t", ScanPointSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                "-i", path,
                "-an", "-sn", "-dn",
                "-vf", filter,
                "-c:v", "libx264",
                "-crf", ComplexityProfile.ProbeCrf.ToString("0", CultureInfo.InvariantCulture),
                "-preset", ScanPreset,
                "-f", "null", "-"
            });

            using var deadline = Deadline(ct, SampleTimeout);
            var token = deadline.Token;

            using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args.ToArray()) };
            process.Start();
            using var cancellationRegistration = token.Register(() => TryKill(process));

            var stdoutTask = process.StandardOutput.ReadToEndAsync(token);
            var stderrTask = process.StandardError.ReadToEndAsync(token);
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(token);

            if (process.ExitCode != 0 || !File.Exists(statsPath)) return (0, 0);
            return ParseVstats(await File.ReadAllTextAsync(statsPath, token), ScanWarmupSeconds);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (0, 0);
        }
        finally
        {
            try { File.Delete(statsPath); } catch { }
        }
    }

    public static (long Bytes, long Frames) ParseVstats(string vstats, double warmupSeconds)
    {
        long bytes = 0, frames = 0;
        foreach (var line in vstats.Split('\n'))
        {
            var sizeMatch = Regex.Match(line, @"f_size=\s*(\d+)");
            if (!sizeMatch.Success) continue;

            var timeMatch = Regex.Match(line, @"\btime=\s*([\d.]+)");
            if (!timeMatch.Success) continue;
            if (!double.TryParse(timeMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var time)) continue;
            if (time < warmupSeconds) continue;
            if (!long.TryParse(sizeMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)) continue;

            bytes += size;
            frames++;
        }
        return (bytes, frames);
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    private static long ParseFrames(string stderr)
    {
        var matches = Regex.Matches(stderr, @"frame=\s*(\d+)");
        if (matches.Count == 0) return 0;
        return long.TryParse(matches[^1].Groups[1].Value, out var frames) ? frames : 0;
    }
}
