using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;

namespace VidShrink.Core.Editing;

public readonly record struct PeakBucket(short Min, short Max);

public sealed class AudioPeaks
{
    public const int SampleRate = 8000;
    public const int BucketsPerSecond = 100;
    public const int SamplesPerBucket = SampleRate / BucketsPerSecond;
    public const long TicksPerBucket = EditTime.TicksPerSecond / BucketsPerSecond;
    public const long TicksPerSample = EditTime.TicksPerSecond / SampleRate;
    internal const int CacheLimit = 32;

    private static readonly ConcurrentDictionary<string, AudioPeaks> Cache = new(StringComparer.Ordinal);
    private readonly short[] _min;
    private readonly short[] _max;

    internal AudioPeaks(short[] min, short[] max, long sampleCount)
    {
        ArgumentNullException.ThrowIfNull(min);
        ArgumentNullException.ThrowIfNull(max);
        if (min.Length != max.Length)
            throw new ArgumentException("Min ve max kova sayisi esit olmalidir", nameof(max));
        _min = min;
        _max = max;
        SampleCount = sampleCount;
    }

    public static AudioPeaks Empty { get; } = new(Array.Empty<short>(), Array.Empty<short>(), 0);

    public int Count => _min.Length;

    public bool IsEmpty => Count == 0;

    public long SampleCount { get; }

    public long Duration => SampleCount * TicksPerSample;

    internal long ByteSize =>(long)Count * 2 * sizeof(short);

    public PeakBucket this[int index] => new(_min[index], _max[index]);

    internal static int CachedCount => Cache.Count;

    internal static void ClearCache() => Cache.Clear();

    public static AudioPeaks FromPcm(ReadOnlySpan<short> samples)
    {
        var builder = new PeakBuilder();
        builder.Add(samples);
        return builder.Build();
    }

    public PeakBucket[] Slice(long srcStart, long srcEnd, bool reversed, int buckets)
    {
        if (srcStart < 0)
            throw new ArgumentOutOfRangeException(nameof(srcStart), srcStart, "Baslangic negatif olamaz");
        if (srcEnd <= srcStart)
            throw new ArgumentOutOfRangeException(nameof(srcEnd), srcEnd, "Son baslangictan buyuk olmalidir");
        if (buckets <= 0)
            throw new ArgumentOutOfRangeException(nameof(buckets), buckets, "Kova sayisi pozitif olmalidir");

        var result = new PeakBucket[buckets];
        if (IsEmpty) return result;

        var length = srcEnd - srcStart;
        for (var j = 0; j < buckets; j++)
        {
            var from = srcStart + (long)((Int128)length * j / buckets);
            var to = srcStart + (long)((Int128)length * (j + 1) / buckets);
            var first = from / TicksPerBucket;
            if (first >= Count) break;
            var last = Math.Min(Math.Max(first, (to - 1) / TicksPerBucket), Count - 1);

            var min = short.MaxValue;
            var max = short.MinValue;
            for (var i = (int)first; i <= last; i++)
            {
                if (_min[i] < min) min = _min[i];
                if (_max[i] > max) max = _max[i];
            }

            result[reversed ? buckets - 1 - j : j] = new PeakBucket(min, max);
        }

        return result;
    }

    public static async Task<AudioPeaks> LoadAsync(string ffmpegPath, string sourcePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ffmpegPath);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);

        var info = new FileInfo(sourcePath);
        if (!info.Exists)
            throw new FileNotFoundException("Kaynak bulunamadi", sourcePath);

        var key = CacheKey(info);
        if (Cache.TryGetValue(key, out var hit)) return hit;

        var peaks = await ReadAsync(ffmpegPath, info.FullName, ct).ConfigureAwait(false);
        if (Cache.Count >= CacheLimit) Cache.Clear();
        Cache[key] = peaks;
        return peaks;
    }

    internal static string CacheKey(FileInfo info)
    {
        var path = OperatingSystem.IsWindows() ? info.FullName.ToUpperInvariant() : info.FullName;
        return path + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
    }

    internal static IReadOnlyList<string> Arguments(string sourcePath) => new[]
    {
        "-hide_banner", "-nostdin", "-v", "error",
        "-threads", "1",
        "-i", sourcePath,
        "-map", "0:a:0?",
        "-vn", "-sn", "-dn",
        "-ac", "1",
        "-ar", SampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "-threads", "1",
        "-f", "s16le",
        "pipe:1"
    };

    internal static bool ReportsNoAudio(string standardError)
        => standardError.Contains("does not contain any stream", StringComparison.OrdinalIgnoreCase)
           || standardError.Contains("matches no streams", StringComparison.OrdinalIgnoreCase);

    internal static async Task<AudioPeaks> ReadAsync(string ffmpegPath, string sourcePath, CancellationToken ct, Action<Process>? started = null)
    {
        ct.ThrowIfCancellationRequested();

        var info = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in Arguments(sourcePath)) info.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = info };
        process.Start();
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }

        var stderr = process.StandardError.ReadToEndAsync();
        var builder = new PeakBuilder();
        using (ct.Register(() => Kill(process)))
        {
            try
            {
                started?.Invoke(process);
                var stream = process.StandardOutput.BaseStream;
                var buffer = new byte[16 * 1024];
                int read;
                while ((read = await stream.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false)) > 0)
                    builder.Add(buffer.AsSpan(0, read));
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
            }
            finally
            {
                if (ct.IsCancellationRequested) Kill(process);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        var error = await stderr.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            if (builder.SampleCount == 0 && ReportsNoAudio(error)) return Empty;
            throw new InvalidOperationException($"ffmpeg ses tepelerini cikaramadi ({process.ExitCode}): {error.Trim()}");
        }

        return builder.Build();
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }
}

internal sealed class PeakBuilder
{
    private readonly List<short> _min = new();
    private readonly List<short> _max = new();
    private int _pendingByte = -1;
    private int _inBucket;
    private short _bucketMin;
    private short _bucketMax;

    public long SampleCount { get; private set; }

    public void Add(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return;

        if (_pendingByte >= 0)
        {
            Push((short)(_pendingByte | (bytes[0] << 8)));
            _pendingByte = -1;
            bytes = bytes[1..];
        }

        var whole = bytes.Length & ~1;
        for (var i = 0; i < whole; i += 2)
            Push((short)(bytes[i] | (bytes[i + 1] << 8)));

        if (whole < bytes.Length) _pendingByte = bytes[whole];
    }

    public void Add(ReadOnlySpan<short> samples)
    {
        foreach (var sample in samples) Push(sample);
    }

    public AudioPeaks Build()
    {
        if (_inBucket > 0)
        {
            _min.Add(_bucketMin);
            _max.Add(_bucketMax);
            _inBucket = 0;
        }

        return SampleCount == 0 ? AudioPeaks.Empty : new AudioPeaks(_min.ToArray(), _max.ToArray(), SampleCount);
    }

    private void Push(short sample)
    {
        if (_inBucket == 0)
        {
            _bucketMin = sample;
            _bucketMax = sample;
        }
        else
        {
            if (sample < _bucketMin) _bucketMin = sample;
            if (sample > _bucketMax) _bucketMax = sample;
        }

        SampleCount++;
        if (++_inBucket == AudioPeaks.SamplesPerBucket)
        {
            _min.Add(_bucketMin);
            _max.Add(_bucketMax);
            _inBucket = 0;
        }
    }
}
