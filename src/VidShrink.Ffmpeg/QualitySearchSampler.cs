using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public sealed class QualitySearchSampler
{
    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(120);

    private readonly MediaInfo _info;
    private readonly PlanOptions _options;
    private readonly ComplexityProfile? _profile;
    private readonly IEncoderAvailability? _availability;
    private readonly IQualityMeasurement _meter;
    private readonly IReadOnlyList<SampleWindow> _windows;
    private readonly int? _threads;

    public QualitySearchSampler(MediaInfo info, PlanOptions options, ComplexityProfile? profile,
        IEncoderAvailability? availability, IQualityMeasurement meter, IReadOnlyList<SampleWindow> windows, int? threads = null)
    {
        _info = info;
        _options = options;
        _profile = profile;
        _availability = availability;
        _meter = meter;
        _windows = windows;
        _threads = threads;
    }

    public static Task<QualitySearchResult> RunAsync(
        MediaInfo info,
        PlanOptions options,
        double requestedQuality,
        double startMb,
        ComplexityProfile? profile,
        IEncoderAvailability? availability,
        IQualityMeasurement meter,
        SpeedMode speed,
        SceneMap? scenes,
        IProgress<QualitySearchRound>? progress,
        CancellationToken ct,
        int? threads = null)
    {
        var sampler = new QualitySearchSampler(info, options, profile, availability, meter,
            CalibrationProbe.Windows(info, speed, scenes), threads);
        return MeasuredQualitySearch.RunAsync(
            requestedQuality,
            startMb,
            PlanCalculator.QualityFloorTargetMb(info),
            PlanCalculator.QualityCeilingTargetMb(info),
            sampler.SampleAsync,
            mb => PlanCalculator.BuildDetailed(info, PlanCalculator.WithTarget(options, mb), profile, availability).PredictedQuality,
            quality => PlanCalculator.TargetMbForQuality(info, options, quality, profile, availability).TargetMb,
            progress,
            ct);
    }

    public async Task<double?> SampleAsync(double targetMb, CancellationToken ct)
    {
        var plan = PlanCalculator.BuildDetailed(_info, PlanCalculator.WithTarget(_options, targetMb), _profile, _availability).Plan;
        double sum = 0;
        double weight = 0;
        foreach (var window in _windows)
        {
            var score = await MeasureWindowAsync(plan, window, ct);
            if (score is not { } value) continue;
            var w = window.Weight > 0 ? window.Weight : 1.0;
            sum += value * w;
            weight += w;
        }
        return weight > 0 ? sum / weight : null;
    }

    private async Task<double?> MeasureWindowAsync(EncodePlan plan, SampleWindow window, CancellationToken ct)
    {
        var target = Path.Combine(Path.GetTempPath(), "vidshrink_search_" + Guid.NewGuid().ToString("N") + ComplexityProbe.SampleExtension);
        try
        {
            if (!await EncodeAsync(EncodeArgs(_info, plan, window, target, _threads), ct)) return null;
            var measured = await _meter.MeasureWindowAsync(_info.FilePath, target, window.Start, window.Length, ct);
            return measured is { Comparable: true, VmafNegMean: { } mean } ? mean : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return null; }
        finally
        {
            try { File.Delete(target); } catch { }
        }
    }

    internal static IReadOnlyList<string> EncodeArgs(MediaInfo info, EncodePlan plan, SampleWindow window, string target, int? threads)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        if (threads is { } decodeThreads) args.AddRange(new[] { "-threads", Whole(decodeThreads) });
        args.AddRange(CalibrationProbe.TrimArgs(window));
        args.AddRange(new[] { "-i", info.FilePath, "-an", "-sn", "-dn" });

        if (CalibrationProbe.BuildFilter(info, plan) is { } filter)
        {
            args.Add("-vf");
            args.Add(filter);
        }

        args.Add("-c:v");
        args.Add(plan.Codec);
        if (plan.Crf is { } crf) args.AddRange(CalibrationProbe.QualityArgs(plan.Codec, crf));
        else args.AddRange(new[] { "-b:v", Whole(plan.VideoBitrateK) + "k" });
        args.AddRange(FfmpegArguments.SpeedArgs(plan.Codec, plan.Preset));
        FfmpegArguments.WarmPsychovisual(plan.Codec, EncoderCapabilities.Instance);
        var psychovisual = FfmpegArguments.CachedPsychovisualArgs(plan.Codec, EncoderCapabilities.Instance);
        args.AddRange(FfmpegArguments.PsychovisualAndColorArgs(plan.Codec, psychovisual, plan.HdrColorArgs));
        if (threads is { } encodeThreads) args.AddRange(new[] { "-threads", Whole(encodeThreads) });
        args.AddRange(new[] { "-pix_fmt", plan.PixelFormat, "-f", ComplexityProbe.SampleFormat, target });
        return args;
    }

    private static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static async Task<bool> EncodeAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(SampleTimeout);
        var token = deadline.Token;

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args.ToArray()) };
        process.Start();
        using var registration = token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        });

        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(token);
            var stderr = process.StandardError.ReadToEndAsync(token);
            await Task.WhenAll(stdout, stderr);
            await process.WaitForExitAsync(token);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
