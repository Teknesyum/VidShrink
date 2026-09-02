using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Ab;

public sealed record EncodeOutcome(
    string Competitor,
    string OutputPath,
    long Bytes,
    string CommandLine,
    string LogPath,
    string Settings);

public interface ICompetitor
{
    string Name { get; }

    Task<EncodeOutcome> EncodeAsync(MediaInfo reference, double targetMb, string outputDirectory, string logDirectory, CancellationToken ct);
}

public sealed class HandBrakeCompetitor : ICompetitor
{
    public const string Preset = "H.265 MKV 1080p30";

    public string Name => "handbrake";

    public static int VideoBitrateKbps(double targetMb, double durationSeconds, double containerOverhead = 0.005)
    {
        if (targetMb <= 0) throw new ArgumentOutOfRangeException(nameof(targetMb));
        if (durationSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        if (containerOverhead is < 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(containerOverhead));

        var bits = targetMb * 1024.0 * 1024.0 * 8.0 * (1.0 - containerOverhead);
        return Math.Max(1, (int)Math.Round(bits / durationSeconds / 1000.0, MidpointRounding.AwayFromZero));
    }

    public static IReadOnlyList<string> BuildArguments(string sourcePath, string outputPath, int bitrateKbps, double fps)
        => new[]
        {
            "-i", sourcePath,
            "-o", outputPath,
            "-Z", Preset,
            "-e", "x265",
            "-b", bitrateKbps.ToString(CultureInfo.InvariantCulture),
            "--multi-pass",
            "--turbo",
            "--encoder-preset", "slow",
            "-a", "none",
            "-r", fps.ToString("0.###", CultureInfo.InvariantCulture),
            "--cfr",
            "--crop", "0:0:0:0",
            "--non-anamorphic"
        };

    public async Task<EncodeOutcome> EncodeAsync(MediaInfo reference, double targetMb, string outputDirectory, string logDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(logDirectory);

        var stem = Path.GetFileNameWithoutExtension(reference.FilePath);
        var tag = $"{stem}_{Name}_{targetMb.ToString("0.###", CultureInfo.InvariantCulture)}mb";
        var outputPath = Path.Combine(outputDirectory, tag + ".mkv");
        var logPath = Path.Combine(logDirectory, tag + ".log");

        var bitrate = VideoBitrateKbps(targetMb, reference.DurationSeconds);
        var args = BuildArguments(reference.FilePath, outputPath, bitrate, reference.Fps);
        var commandLine = ProcessLauncher.CommandLine("HandBrakeCLI", args);

        if (File.Exists(outputPath)) File.Delete(outputPath);
        var (exitCode, output) = await ProcessLauncher.RunAsync("HandBrakeCLI", args, ct);
        await File.WriteAllTextAsync(logPath, commandLine + Environment.NewLine + Environment.NewLine + output, ct);
        if (exitCode != 0 || !File.Exists(outputPath))
            throw new InvalidOperationException($"HandBrakeCLI {exitCode} verdi; günlük: {logPath}");

        return new EncodeOutcome(
            Name,
            outputPath,
            new FileInfo(outputPath).Length,
            commandLine,
            logPath,
            $"{Preset} + x265 slow, {bitrate} kbit/s ABR, çoklu geçiş, turbo ilk geçiş, ses yok, {reference.Fps:0.###} fps CFR");
    }
}

public sealed class VidShrinkCompetitor : ICompetitor
{
    private const int CalibrationRounds = 2;
    private readonly HdrPolicy _hdrPolicy;

    public VidShrinkCompetitor(string name, HdrPolicy hdrPolicy)
    {
        Name = name;
        _hdrPolicy = hdrPolicy;
    }

    public string Name { get; }

    public async Task<EncodeOutcome> EncodeAsync(MediaInfo reference, double targetMb, string outputDirectory, string logDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(logDirectory);

        var stem = Path.GetFileNameWithoutExtension(reference.FilePath);
        var tag = $"{stem}_{Name}_{targetMb.ToString("0.###", CultureInfo.InvariantCulture)}mb";
        var outputPath = Path.Combine(outputDirectory, tag + ".mp4");
        var logPath = Path.Combine(logDirectory, tag + ".log");
        var log = new System.Text.StringBuilder();

        var options = new PlanOptions
        {
            TargetMb = targetMb,
            Codec = CodecPreference.Auto,
            FillPolicy = FillPolicy.FillTarget,
            SpeedMode = SpeedMode.Quality,
            AllowResolutionDrop = true,
            AllowFpsDrop = false,
            HdrPolicy = _hdrPolicy
        };

        var profile = await ComplexityProbe.RunAsync(reference, options.SpeedMode, ct);
        var planResult = PlanCalculator.BuildDetailed(reference, options, profile, EncoderCapabilities.Instance);
        var draft = planResult.Plan;
        var seed = profile;
        for (var round = 0; round < CalibrationRounds; round++)
        {
            var tuned = await CalibrationProbe.RunAsync(reference, draft, seed, options.SpeedMode, ct);
            profile = tuned;
            planResult = PlanCalculator.BuildDetailed(reference, options, profile, EncoderCapabilities.Instance);
            if (!tuned.Calibrated) break;
            var settled = planResult.Plan;
            var scale = reference.Height <= 0 ? 1.0 : (double)settled.Height / reference.Height;
            if (tuned.AppliesTo(settled.Codec, scale, settled.Fps)) break;
            draft = settled;
            seed = tuned.WithoutCalibration();
        }

        var plan = planResult.Plan;
        var commandPass = plan.ModeEnum == EncodeMode.TwoPass && !CodecModel.IsHardware(plan.Codec) ? 2 : 0;
        var commandLine = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(
            reference, plan, outputPath, commandPass,
            commandPass > 0 ? Path.Combine(outputDirectory, "pass") : null,
            EncoderCapabilities.Instance));

        log.AppendLine(commandLine);
        log.AppendLine();
        log.AppendLine($"plan: {plan.Width}x{plan.Height}@{plan.Fps:0.###} {plan.Codec}/{plan.Mode} {(plan.ModeEnum == EncodeMode.Crf ? "crf " + plan.Crf : plan.VideoBitrateK + "k")} pix={plan.PixelFormat} kalibre={profile.Calibrated}");
        log.AppendLine($"gerekçe: {plan.Reason}");

        if (File.Exists(outputPath)) File.Delete(outputPath);
        var result = await new EncodeRunner().RunAsync(
            reference, plan, outputPath, targetMb, null, ct, FillPolicy.FillTarget, profile);

        if (result.Trace is { } trace)
            foreach (var attempt in trace)
                log.AppendLine($"deneme {attempt.Number}: {attempt.Branch} hedef={attempt.AimMb:0.###} MB gerçek={attempt.ActualMb:0.###} MB {attempt.VideoBitrateK}k {attempt.Mode}");

        await File.WriteAllTextAsync(logPath, log.ToString(), ct);

        if (!result.Success || !File.Exists(outputPath))
            throw new InvalidOperationException($"VidShrink kodlaması başarısız: {result.Error ?? "bilinmeyen"}; günlük: {logPath}");

        var used = result.PlanUsed;
        return new EncodeOutcome(
            Name,
            outputPath,
            new FileInfo(outputPath).Length,
            commandLine,
            logPath,
            $"{used.Width}x{used.Height}@{used.Fps:0.###}, {used.Codec}/{used.Mode}, {(used.ModeEnum == EncodeMode.Crf ? "crf " + used.Crf : used.VideoBitrateK + "k")}, pix={used.PixelFormat}, hdr={_hdrPolicy}, deneme={result.Attempts}");
    }
}
